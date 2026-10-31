// EchoRaidHours 1.4.0 (Nick, 31 Oct): CLAN RAIDS START THEMSELVES. In raid hours the first explosive hit on a clan's base
//   starts a raid: red bubble, 45 min timer, only the attacking group and the defending clan can fight or do damage inside it.
//   Attackers win by destroying the TC (EchoBountyTeams pays the bounty). Defenders win if the TC survives the timer or raid
//   hours end - then their base gets a 30 min blue bubble to rebuild. Results feed the clan leaderboard (EchoClans).
// EchoRaidHours 1.2.0 (Nick, 31 Oct): Bounty Lake purple hour is back - in the first hour of each raid window
//   (EchoBountyLake's daily times) bases at the lake get a PURPLE bubble: raidable, and EchoBountyLake boosts the damage.
// EchoRaidHours 1.1.0 (Nick, 31 Oct 2026): the WHOLE raid system, simple enough for anyone.
//   AUTO: protected bases get a blue bubble (green during wipe grace); every bubble drops when raid hours start.
//   MANUAL: /raidadmin opens a panel with 4 big buttons - AUTO, RAID NOW, PROTECT ALL, FREE FOR ALL
//           (or /raidadmin auto|raid|protect|ffa). It sets EchoTC's clock, so vending, vitals, animals, NPCs
//           and the clan $ bounty all follow the same switch.
// EchoRaidHours 1.0.0: the simple raid system.
//   - EVERY player base is protected outside raid hours - no registering, no bubbles, no capture bars.
//   - Raid hours = EchoTC's bounty hours (Wed/Thu 5-10 PM, Sat/Sun 3-11 PM Sydney) and the free-for-all days after it.
//   - First hour after a wipe: grace, nobody can raid.
//   - Offline protection: in raid hours a base whose players have ALL been offline 10+ minutes takes 50% damage.
//   - Owners, their clan/team and anyone authed on the base's TC can always damage their own stuff (removing, rebuilding).
//   - Decay, NPCs and animals are never blocked. Player vehicles are not covered.
//   - A small banner shows the state to everyone; /raid explains it.
// Console: raidhours.status
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoRaidHours", "Echo 10X", "1.4.0")]
    [Description("Simple raid hours: every base protected outside raid hours, offline protection inside them.")]
    public class EchoRaidHours : RustPlugin
    {
        [PluginReference] private Plugin EchoTC, EchoClans, EchoBountyLake;
        private const float GraceMinutes = 60f, OfflineMinutes = 10f, OfflineScale = 0.5f;
        private const string Ui = "EchoRaidHours.Banner";
        private const string Tag = "<color=#39d2d1>ECHO</color> ";

        private enum State { Grace, Protected, Raid, Ffa }
        private State state = State.Protected; private double secsLeft = -1; private string hoursText = "";
        private readonly Dictionary<ulong, float> offlineSince = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> told = new Dictionary<ulong, float>();
        private readonly HashSet<ulong> uiOff = new HashSet<ulong>();
        private int blocked, halved;
        private float loadedAt;

        private void OnServerInitialized()
        {
            loadedAt = Time.realtimeSinceStartup;
            cmd.AddChatCommand("raid", this, nameof(CmdRaid)); cmd.AddChatCommand("raidhours", this, nameof(CmdRaid)); cmd.AddChatCommand("raidadmin", this, nameof(CmdAdmin));
            Tick(); timer.Every(5f, Tick); timer.Every(30f, Bubbles); timer.Once(3f, Bubbles);
        }
        private void Unload() { foreach (var p in BasePlayer.activePlayerList) { CuiHelper.DestroyUi(p, Ui); CuiHelper.DestroyUi(p, Panel); } KillAllDomes(); }

        // ================= bubbles =================
        private const string Blue = "assets/bundled/prefabs/modding/events/twitch/br_sphere.prefab";
        private const string Green = "assets/bundled/prefabs/modding/events/twitch/br_sphere_green.prefab";
        private const string Purple = "assets/bundled/prefabs/modding/events/twitch/br_sphere_purple.prefab";
        private const string Red = "assets/bundled/prefabs/modding/events/twitch/br_sphere_red.prefab";

        // ================= clan raids =================
        private const float RaidMinutes = 45f, RebuildMinutes = 30f;
        private class Raid { public uint B; public ulong Tc; public Vector3 C; public float R; public string Att, AttL, Def, DefL; public ulong AttU, DefU; public float Ends; public string Grid; }
        private readonly Dictionary<uint, Raid> raidByB = new Dictionary<uint, Raid>();
        private IEnumerable<Raid> raids => raidByB.Values;
        private readonly Dictionary<uint, float> rebuildUntil = new Dictionary<uint, float>();
        private bool Cooling(uint b) { float t; return rebuildUntil.TryGetValue(b, out t) && Time.realtimeSinceStartup < t; }

        private string TagOf(ulong u) { try { return EchoClans?.Call("GetClanTag", u) as string; } catch { return null; } }
        private string GroupOf(BasePlayer p)
        {
            var t = TagOf(p.userID); if (!string.IsNullOrEmpty(t)) return "c:" + t.ToUpperInvariant();
            return p.currentTeam != 0 ? "t:" + p.currentTeam : "p:" + (ulong)p.userID;
        }
        private string LabelOf(BasePlayer p) { var t = TagOf(p.userID); return !string.IsNullOrEmpty(t) ? "[" + t + "]" : p.currentTeam != 0 ? p.displayName + "'s team" : p.displayName; }
        private bool InRaid(Raid r, BasePlayer p) { var g = GroupOf(p); return g == r.Att || g == r.Def; }
        private Raid RaidAt(Vector3 pos)
        {
            foreach (var r in raidByB.Values) { var d = pos - r.C; d.y = 0f; if (d.magnitude <= r.R) return r; }
            return null;
        }
        private static string GridOf(Vector3 pos)
        {
            float size = TerrainMeta.Size.x; int cells = Mathf.Max(1, Mathf.FloorToInt(size / 146.3f)); float cell = size / cells;
            int x = Mathf.Clamp(Mathf.FloorToInt((pos.x + size / 2f) / cell), 0, cells - 1), z = Mathf.Clamp(Mathf.FloorToInt((size / 2f - pos.z) / cell), 0, cells - 1);
            string col = ""; int n = x; do { col = (char)('A' + n % 26) + col; n = n / 26 - 1; } while (n >= 0);
            return col + z;
        }
        private static bool Shape(BuildingPrivlidge tc, out Vector3 c, out float r)
        {
            c = tc.transform.position; r = 12f;
            var blocks = tc.GetBuilding()?.buildingBlocks; if (blocks == null || blocks.Count == 0) return false;
            Vector3 sum = Vector3.zero; int n = 0; foreach (var bl in blocks) { if (bl == null) continue; sum += bl.transform.position; n++; }
            if (n == 0) return false; c = sum / n;
            float m = 0f; foreach (var bl in blocks) { if (bl == null) continue; var d = bl.transform.position - c; d.y = 0f; m = Mathf.Max(m, d.magnitude); }
            r = Mathf.Clamp(m + 6f, 12f, 60f); return true;
        }

        private void TryStartRaid(DecayEntity de, BasePlayer atk)
        {
            var tc = de.GetBuildingPrivilege(); if (tc == null || tc.net == null || raidByB.ContainsKey(tc.buildingID)) return;
            if ((tc.GetBuilding()?.buildingBlocks?.Count ?? 0) < MinPieces) return;
            ulong defU = tc.OwnerID; string defTag = TagOf(defU);
            if (string.IsNullOrEmpty(defTag)) foreach (var u in tc.authorizedPlayers) { defTag = TagOf(u); if (!string.IsNullOrEmpty(defTag)) { defU = u; break; } }
            if (string.IsNullOrEmpty(defTag)) return;                                   // not a clan base: plain raiding, no raid event
            string att = GroupOf(atk), def = "c:" + defTag.ToUpperInvariant();
            if (att == def) return;
            Vector3 c; float r; Shape(tc, out c, out r);
            var raid = new Raid { B = tc.buildingID, Tc = tc.net.ID.Value, C = c, R = r, Att = att, AttL = LabelOf(atk), Def = def, DefL = "[" + defTag + "]", AttU = (ulong)atk.userID, DefU = defU, Ends = Time.realtimeSinceStartup + RaidMinutes * 60f, Grid = GridOf(c) };
            raidByB[raid.B] = raid;
            Server.Broadcast(Tag + $"<color=#ff5a4a>RAID STARTED:</color> {raid.AttL} is raiding {raid.DefL}'s base at {raid.Grid}. {RaidMinutes:0} min - destroy the TC to win. Only those two can fight inside the red bubble.");
            foreach (var u in tc.authorizedPlayers) { var op = BasePlayer.FindByID(u); if (op != null && op.IsConnected) op.ChatMessage(Tag + $"<color=#ff5a4a>Your base at {raid.Grid} is being raided by {raid.AttL}.</color> Keep your TC alive for {RaidMinutes:0} min to win."); }
            Puts($"raid started: {raid.AttL} ({atk.displayName}) on {raid.DefL} at {raid.Grid}");
            Bubbles();
        }

        private void EndRaid(Raid r, bool attackersWon, string why)
        {
            if (!raidByB.Remove(r.B)) return;
            if (attackersWon)
            {
                Server.Broadcast(Tag + $"<color=#ff5a4a>RAID WON:</color> {r.AttL} destroyed {r.DefL}'s TC at {r.Grid}.");
                Interface.CallHook("OnEchoBubbleCaptureWon", r.Tc.ToString(), "p:" + r.AttU, "p:" + r.DefU, new List<ulong> { r.AttU });
            }
            else
            {
                rebuildUntil[r.B] = Time.realtimeSinceStartup + RebuildMinutes * 60f;
                Server.Broadcast(Tag + $"<color=#7CFC9A>RAID DEFENDED:</color> {r.DefL} held their base at {r.Grid} ({why}). Their base gets a {RebuildMinutes:0} min bubble to rebuild.");
                Interface.CallHook("OnEchoBubbleCaptureDefended", r.Tc.ToString(), "p:" + r.AttU, "p:" + r.DefU);
            }
            Puts($"raid ended: {r.AttL} vs {r.DefL} at {r.Grid} - " + (attackersWon ? "attackers won" : "defended (" + why + ")"));
            Bubbles();
        }

        private void RaidTick()
        {
            float now = Time.realtimeSinceStartup;
            foreach (var r in raidByB.Values.ToList())
            {
                var tc = BaseNetworkable.serverEntities.Find(new NetworkableId(r.Tc)) as BuildingPrivlidge;
                if (tc == null || tc.IsDestroyed) EndRaid(r, true, "TC destroyed");
                else if (state != State.Raid) EndRaid(r, false, state == State.Ffa ? "free for all started" : "raid hours ended");
                else if (now >= r.Ends) EndRaid(r, false, "timer ran out");
            }
            foreach (var k in rebuildUntil.Where(kv => now >= kv.Value).Select(kv => kv.Key).ToList()) rebuildUntil.Remove(k);
        }

        private void OnEntityDeath(BuildingPrivlidge tc, HitInfo info)
        {
            if (tc == null) return; Raid r;
            if (raidByB.TryGetValue(tc.buildingID, out r) && r.Tc == (tc.net?.ID.Value ?? 0UL)) NextTick(() => EndRaid(r, true, "TC destroyed"));
        }

        private void TellLocked(BasePlayer p, Raid r)
        {
            float t; if (told.TryGetValue(p.userID, out t) && Time.realtimeSinceStartup - t < 8f) return;
            told[p.userID] = Time.realtimeSinceStartup;
            p.ChatMessage(Tag + $"That's a raid between {r.AttL} and {r.DefL} - you can't fight inside their red bubble.");
        }
        private int purpleUp;
        private string LakePhase(Vector3 pos) { try { return EchoBountyLake?.Call("API_LakePhase", pos) as string; } catch { return null; } }
        private class Dome { public List<SphereEntity> Spheres = new List<SphereEntity>(); public Vector3 Centre; public float Radius; public string Prefab; }
        private readonly Dictionary<ulong, Dome> domes = new Dictionary<ulong, Dome>();
        private int Layers = 2, MinPieces = 5;

        private void Bubbles()
        {
            bool shield = state == State.Grace || state == State.Protected;
            if (state == State.Ffa) { if (domes.Count > 0) KillAllDomes(); purpleUp = 0; return; }
            var seen = new HashSet<ulong>(); int purples = 0;
            foreach (var tc in BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>().ToList())
            {
                if (tc == null || tc.IsDestroyed || tc.net == null || tc.OwnerID == 0UL || !tc.OwnerID.IsSteamId()) continue;
                var b = tc.GetBuilding(); var blocks = b?.buildingBlocks;
                if (blocks == null || blocks.Count < MinPieces) continue;
                // raid hours: only Bounty Lake bases in their purple hour get a (purple) bubble; protected: blue / green in grace
                uint bid0 = tc.buildingID;
                string prefab = raidByB.ContainsKey(bid0) ? Red : Cooling(bid0) ? Blue : shield ? (state == State.Grace ? Green : Blue) : (LakePhase(tc.transform.position) == "purple" ? Purple : null);
                if (prefab == null) continue;
                ulong id = tc.net.ID.Value; seen.Add(id); if (prefab == Purple) purples++;
                Vector3 c = Vector3.zero; int n = 0; foreach (var bl in blocks) { if (bl == null) continue; c += bl.transform.position; n++; }
                if (n == 0) continue; c /= n;
                float r = 0f; foreach (var bl in blocks) { if (bl == null) continue; var d = bl.transform.position - c; d.y = 0f; r = Mathf.Max(r, d.magnitude); }
                r = Mathf.Clamp(r + 6f, 12f, 60f);
                Dome dm;
                if (domes.TryGetValue(id, out dm) && dm.Prefab == prefab && Mathf.Abs(dm.Radius - r) < 2f && (dm.Centre - c).sqrMagnitude < 4f && dm.Spheres.All(x => x != null && !x.IsDestroyed)) continue;
                if (dm != null) Kill(dm);
                if (prefab == Purple && (dm == null || dm.Prefab != Purple))
                    foreach (var uid in tc.authorizedPlayers) { var op = BasePlayer.FindByID(uid); if (op != null && op.IsConnected) op.ChatMessage(Tag + "<color=#b05cff>PURPLE HOUR - DOUBLE DAMAGE:</color> your Bounty Lake base can be raided and takes boosted raid damage for the next hour."); }
                dm = new Dome { Centre = c, Radius = r, Prefab = prefab };
                for (int i = 0; i < Layers; i++)
                {
                    var sp = GameManager.server.CreateEntity(prefab, c, Quaternion.identity) as SphereEntity;
                    if (sp == null) break;
                    sp.enableSaving = false; sp.currentRadius = r * 2f; sp.lerpRadius = r * 2f; sp.UpdateScale(); sp.Spawn();
                    dm.Spheres.Add(sp);
                }
                domes[id] = dm;
            }
            foreach (var k in domes.Keys.Where(k => !seen.Contains(k)).ToList()) { Kill(domes[k]); domes.Remove(k); }
            purpleUp = purples;
        }
        private string LakePhaseNow() { try { var o = EchoBountyLake?.Call("API_LakePhase", Vector3.zero); } catch { } return EchoBountyLake == null ? "plugin off" : "on"; }
        private static void Kill(Dome d) { foreach (var s in d.Spheres) if (s != null && !s.IsDestroyed) s.Kill(); d.Spheres.Clear(); }
        private void KillAllDomes() { foreach (var d in domes.Values) Kill(d); domes.Clear(); }

        // ================= staff panel =================
        private const string Panel = "EchoRaidHours.Panel";
        private string force = "auto";
        private static bool Staff(BasePlayer p) => p != null && ((p.net?.connection?.authLevel ?? 0) >= 2 || p.IsAdmin);

        private void CmdAdmin(BasePlayer p, string command, string[] args)
        {
            if (!Staff(p)) { p.ChatMessage(Tag + "Staff only."); return; }
            if (args.Length > 0) { p.ChatMessage(Tag + SetMode(args[0], p.displayName)); return; }
            OpenPanel(p);
        }

        private string SetMode(string m, string by)
        {
            m = (m ?? "").ToLowerInvariant();
            string tc = m == "auto" ? "auto" : m == "raid" ? "bounty" : m == "protect" ? "peace" : m == "ffa" ? "ffa" : null;
            if (tc == null) return "Use: auto, raid, protect or ffa";
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "echotc.force", tc);
            Puts($"{by} set the raid system to {m.ToUpperInvariant()}");
            timer.Once(1.5f, () => { Tick(); Bubbles(); foreach (var p in BasePlayer.activePlayerList) if (Staff(p)) RefreshPanel(p); });
            return m == "auto" ? "AUTO: raid hours follow the schedule again." : m == "raid" ? "RAID NOW: every base can be raided until you switch back to AUTO." : m == "protect" ? "PROTECT ALL: every base is protected until you switch back to AUTO." : "FREE FOR ALL: raid any time until you switch back to AUTO.";
        }

        // 1.3.0: the clan's main base (biggest TC any clan member owns or is authed on) - replaces EchoTC's "registered base"
        // for the bounty money, map markers and the free-fall nuke. 0 = not in a clan / no base.
        private readonly Dictionary<string, KeyValuePair<float, ulong>> clanBaseCache = new Dictionary<string, KeyValuePair<float, ulong>>();
        private object API_ClanBaseTc(ulong uid)
        {
            Dictionary<string, object> info = null; try { info = EchoClans?.Call("API_ClanInfo", uid) as Dictionary<string, object>; } catch { }
            if (info == null || !(info.ContainsKey("inClan") && info["inClan"] is bool && (bool)info["inClan"])) return 0UL;
            string tag = info.ContainsKey("tag") ? info["tag"]?.ToString() ?? "" : "";
            KeyValuePair<float, ulong> hit; if (clanBaseCache.TryGetValue(tag, out hit) && Time.realtimeSinceStartup - hit.Key < 20f) return hit.Value;
            var ids = new HashSet<ulong> { uid };
            var mem = info.ContainsKey("members") ? info["members"] as System.Collections.IEnumerable : null;
            if (mem != null) foreach (var m in mem) { var d = m as Dictionary<string, object>; ulong id; if (d != null && d.ContainsKey("id") && d["id"] != null && ulong.TryParse(d["id"].ToString(), out id)) ids.Add(id); }
            ulong best = 0UL; int bestN = 0;
            foreach (var tc in BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>())
            {
                if (tc == null || tc.IsDestroyed || tc.net == null) continue;
                if (!ids.Contains(tc.OwnerID) && !tc.authorizedPlayers.Any(ids.Contains)) continue;
                int n = tc.GetBuilding()?.buildingBlocks?.Count ?? 0;
                if (n > bestN) { bestN = n; best = tc.net.ID.Value; }
            }
            clanBaseCache[tag] = new KeyValuePair<float, ulong>(Time.realtimeSinceStartup, best);
            return best;
        }

        // API for Echo Command's Raid page (one simple page instead of the old Shields + Raid pages)
        private string API_Set(string mode, string by) => SetMode(mode, by ?? "staff");
        private Dictionary<string, object> API_Status() => new Dictionary<string, object>
        {
            ["state"] = state == State.Grace ? "WIPE GRACE" : state == State.Raid ? "RAID HOURS LIVE" : state == State.Ffa ? "FREE FOR ALL" : "BASES PROTECTED",
            ["raidable"] = state == State.Raid || state == State.Ffa,
            ["time"] = secsLeft > 0 && state != State.Ffa ? Fmt(secsLeft) + (state == State.Raid ? " left" : " to go") : "",
            ["mode"] = force == "bounty" ? "RAID NOW" : force == "peace" ? "PROTECT ALL" : force == "ffa" ? "FREE FOR ALL" : "AUTO",
            ["bubbles"] = domes.Count, ["purple"] = purpleUp, ["hours"] = Hours()
        };
        private readonly HashSet<ulong> panelOpen = new HashSet<ulong>();
        private void RefreshPanel(BasePlayer p) { if (panelOpen.Contains((ulong)p.userID)) OpenPanel(p); }

        private void OpenPanel(BasePlayer p)
        {
            CuiHelper.DestroyUi(p, Panel); panelOpen.Add((ulong)p.userID);
            string now = state == State.Grace ? "WIPE GRACE" : state == State.Raid ? "RAID HOURS LIVE" : state == State.Ffa ? "FREE FOR ALL" : "BASES PROTECTED";
            string col = state == State.Raid || state == State.Ffa ? "1 0.45 0.4 1" : "0.5 1 0.6 1";
            string mode = force == "bounty" ? "RAID NOW (manual)" : force == "peace" ? "PROTECT ALL (manual)" : force == "ffa" ? "FREE FOR ALL (manual)" : "AUTO (schedule)";
            var c = new CuiElementContainer();
            c.Add(new CuiPanel { Image = { Color = "0.06 0.07 0.09 0.97" }, RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-270 -170", OffsetMax = "270 170" }, CursorEnabled = true }, "Overlay", Panel);
            c.Add(new CuiLabel { Text = { Text = "RAID SYSTEM", FontSize = 22, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = "0.22 0.82 0.82 1" }, RectTransform = { AnchorMin = "0 0.84", AnchorMax = "1 0.98" } }, Panel);
            c.Add(new CuiLabel { Text = { Text = now + (secsLeft > 0 && state != State.Ffa ? "  ·  " + Fmt(secsLeft) + (state == State.Raid ? " left" : " to go") : ""), FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = col }, RectTransform = { AnchorMin = "0 0.73", AnchorMax = "1 0.84" } }, Panel);
            c.Add(new CuiLabel { Text = { Text = $"Mode: {mode}   ·   bubbles up: {domes.Count}", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.75 0.78 0.82 1" }, RectTransform = { AnchorMin = "0 0.66", AnchorMax = "1 0.73" } }, Panel);
            Btn(c, "AUTO\n<size=10>follow the raid-hours schedule</size>", "raidhours.set auto", "0.2 0.45 0.75 1", "0.04 0.36", "0.49 0.62");
            Btn(c, "RAID NOW\n<size=10>every base raidable</size>", "raidhours.set raid", "0.72 0.16 0.12 1", "0.51 0.36", "0.96 0.62");
            Btn(c, "PROTECT ALL\n<size=10>bubbles up on every base</size>", "raidhours.set protect", "0.16 0.55 0.28 1", "0.04 0.08", "0.49 0.34");
            Btn(c, "FREE FOR ALL\n<size=10>raid any time, no bubbles</size>", "raidhours.set ffa", "0.55 0.25 0.6 1", "0.51 0.08", "0.96 0.34");
            c.Add(new CuiButton { Button = { Command = "raidhours.close", Color = "0 0 0 0.5" }, Text = { Text = "X", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.93 0.89", AnchorMax = "0.99 0.98" } }, Panel);
            CuiHelper.AddUi(p, c);
        }
        private void Btn(CuiElementContainer c, string text, string cmd, string color, string min, string max) =>
            c.Add(new CuiButton { Button = { Command = cmd, Color = color }, Text = { Text = text, FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }, RectTransform = { AnchorMin = min, AnchorMax = max } }, Panel);

        [ConsoleCommand("raidhours.set")]
        private void CSet(ConsoleSystem.Arg a)
        {
            var p = a.Player();
            if (p != null && !Staff(p)) return;
            var r = SetMode(a.GetString(0, ""), p != null ? p.displayName : "console");
            if (p != null) p.ChatMessage(Tag + r); else a.ReplyWith(r);
        }
        [ConsoleCommand("raidhours.close")]
        private void CClose(ConsoleSystem.Arg a) { var p = a.Player(); if (p == null) return; panelOpen.Remove((ulong)p.userID); CuiHelper.DestroyUi(p, Panel); }
        private void OnPlayerDisconnected(BasePlayer p) { if (p != null) offlineSince[p.userID] = Time.realtimeSinceStartup; }
        private void OnPlayerConnected(BasePlayer p) { if (p != null) { offlineSince.Remove(p.userID); timer.Once(5f, () => Banner(p)); } }

        // ---------- state ----------
        private void Tick()
        {
            var prev = state;
            double sinceWipe = 1e9;
            try { sinceWipe = (DateTime.UtcNow - SaveRestore.SaveCreatedTime.ToUniversalTime()).TotalMinutes; } catch { }
            bool ffa = false, bounty = false; secsLeft = -1;
            try { ffa = EchoTC?.Call("API_FfaNow") is bool f && f; } catch { }
            try
            {
                var info = EchoTC?.Call("API_BountyInfo") as Dictionary<string, object>;
                if (info != null)
                {
                    bounty = info["bounty"] is bool b && b;
                    hoursText = info.ContainsKey("hoursText") ? info["hoursText"]?.ToString() ?? "" : "";
                    force = info.ContainsKey("force") ? info["force"]?.ToString() ?? "auto" : "auto";
                    if (info["edge"] is DateTime e && info["now"] is DateTime n && e != DateTime.MaxValue) secsLeft = (e - n).TotalSeconds;
                }
            }
            catch { }
            if (sinceWipe < GraceMinutes) { state = State.Grace; secsLeft = (GraceMinutes - sinceWipe) * 60.0; }
            else if (ffa) state = State.Ffa;
            else state = bounty ? State.Raid : State.Protected;
            if (prev != state)
            {
                if (state == State.Raid) Server.Broadcast(Tag + "<color=#ff5a4a>RAID HOURS ARE LIVE</color> - every base can be raided now. Bases where everyone is offline take half damage.");
                else if (state == State.Protected && prev == State.Raid) Server.Broadcast(Tag + "<color=#7CFC9A>Raid hours are over</color> - all bases are protected again. /raid for the times.");
                else if (state == State.Ffa) Server.Broadcast(Tag + "<color=#ff5a4a>FREE FOR ALL</color> - bases can be raided any time until the wipe.");
            }
            RaidTick();
            foreach (var p in BasePlayer.activePlayerList) Banner(p);
            if (prev != state) Bubbles();
        }

        private bool Protected => state == State.Grace || state == State.Protected;

        // ---------- damage ----------
        private object OnEntityTakeDamage(BaseCombatEntity ent, HitInfo info)
        {
            if (ent == null || info == null || ent is BaseNpc) return null;
            if (ent is BasePlayer)
            {
                if (raidByB.Count == 0) return null;
                var v = (BasePlayer)ent; var a = info.InitiatorPlayer;
                if (a == null || a == v || a.IsNpc || v.IsNpc || !((ulong)a.userID).IsSteamId()) return null;
                var rr = RaidAt(v.transform.position) ?? RaidAt(a.transform.position);
                if (rr != null && (!InRaid(rr, a) || !InRaid(rr, v))) { if (!InRaid(rr, a)) TellLocked(a, rr); return true; }
                return null;
            }
            var de = ent as DecayEntity; if (de == null) return null;
            if (de.OwnerID == 0UL || !de.OwnerID.IsSteamId()) return null;          // map / event stuff
            if (info.damageTypes.Has(Rust.DamageType.Decay)) return null;
            var atk = info.InitiatorPlayer;
            bool fire = info.damageTypes.Get(Rust.DamageType.Heat) > 0f;
            if (atk == null && !fire) return null;                                     // NPCs, heli, environment
            if (atk != null && (atk.IsNpc || !((ulong)atk.userID).IsSteamId())) return null;
            if (atk != null && Friendly(atk, de)) return null;                         // own base / clan / authed
            if (Protected)
            {
                blocked++;
                if (atk != null) Tell(atk);
                return true;
            }
            if (state == State.Raid && de.buildingID != 0)
            {
                Raid live;
                if (raidByB.TryGetValue(de.buildingID, out live)) { if (atk != null && !InRaid(live, atk)) { TellLocked(atk, live); return true; } }
                else if (Cooling(de.buildingID)) { blocked++; if (atk != null) atk.ChatMessage(Tag + "This base just held off a raid - it's rebuilding under a bubble."); return true; }
                else if (atk != null && info.damageTypes.Has(Rust.DamageType.Explosion)) TryStartRaid(de, atk);
            }
            if (state == State.Raid && AllOffline(de)) { info.damageTypes.ScaleAll(OfflineScale); halved++; }
            return null;
        }

        private bool Friendly(BasePlayer atk, DecayEntity de)
        {
            ulong a = atk.userID, o = de.OwnerID;
            if (a == o) return true;
            if (atk.currentTeam != 0 && RelationshipManager.ServerInstance?.FindPlayersTeam(o)?.teamID == atk.currentTeam) return true;
            try { if (EchoClans?.Call("IsMember", a, o) is bool m && m) return true; } catch { }
            var tc = de.GetBuildingPrivilege();
            return tc != null && tc.IsAuthed(atk);
        }

        private bool AllOffline(DecayEntity de)
        {
            var tc = de.GetBuildingPrivilege();
            IEnumerable<ulong> who = tc != null ? tc.authorizedPlayers : new HashSet<ulong> { de.OwnerID };
            float now = Time.realtimeSinceStartup;
            foreach (var id in who)
            {
                var p = BasePlayer.FindByID(id);
                if (p != null && p.IsConnected) return false;
                float since; if (!offlineSince.TryGetValue(id, out since)) since = loadedAt - OfflineMinutes * 60f;   // offline since before load
                if (now - since < OfflineMinutes * 60f) return false;
            }
            return true;
        }

        private void Tell(BasePlayer p)
        {
            float t; if (told.TryGetValue(p.userID, out t) && Time.realtimeSinceStartup - t < 10f) return;
            told[p.userID] = Time.realtimeSinceStartup;
            p.ChatMessage(Tag + (state == State.Grace ? $"Wipe grace - no raiding for another {Fmt(secsLeft)}." : $"Bases are protected right now. Raid hours: {Hours()}. /raid"));
        }

        // ---------- banner + /raid ----------
        private string Hours() => string.IsNullOrEmpty(hoursText) ? "Wed & Thu 5-10 PM, Sat & Sun 3-11 PM (Sydney)" : hoursText;
        private static string Fmt(double s)
        {
            if (s < 0) return "";
            var t = TimeSpan.FromSeconds(s);
            return t.TotalHours >= 24 ? $"{(int)t.TotalDays}d {t.Hours}h" : t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m {t.Seconds:00}s";
        }

        private void Banner(BasePlayer p)
        {
            if (p == null || !p.IsConnected) return;
            CuiHelper.DestroyUi(p, Ui);
            if (uiOff.Contains(p.userID)) return;
            string text, col;
            Raid mine = state == State.Raid ? raidByB.Values.FirstOrDefault(r => InRaid(r, p)) : null;
            if (mine != null)
            {
                bool att = GroupOf(p) == mine.Att; float left = mine.Ends - Time.realtimeSinceStartup;
                text = (att ? $"RAIDING {mine.DefL}" : $"DEFEND vs {mine.AttL}") + $" at {mine.Grid}  ·  {Fmt(left)} left"; col = att ? "0.70 0.12 0.10 0.90" : "0.60 0.35 0.05 0.90";
            }
            else
            switch (state)
            {
                case State.Grace: text = $"WIPE GRACE  ·  raiding opens in {Fmt(secsLeft)}"; col = "0.15 0.45 0.75 0.80"; break;
                case State.Raid: text = "RAID HOURS LIVE" + (secsLeft > 0 ? $"  ·  {Fmt(secsLeft)} left" : ""); col = "0.70 0.12 0.10 0.85"; break;
                case State.Ffa: text = "FREE FOR ALL  ·  raid any time"; col = "0.70 0.12 0.10 0.85"; break;
                default: text = "BASES PROTECTED" + (secsLeft > 0 ? $"  ·  raids open in {Fmt(secsLeft)}" : ""); col = "0.12 0.50 0.25 0.80"; break;
            }
            var c = new CuiElementContainer();
            c.Add(new CuiPanel { Image = { Color = col }, RectTransform = { AnchorMin = "0.40 0.965", AnchorMax = "0.60 0.988" } }, "Hud", Ui);
            c.Add(new CuiLabel { Text = { Text = text, FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, Ui);
            CuiHelper.AddUi(p, c);
        }

        private void CmdRaid(BasePlayer p, string command, string[] args)
        {
            if (args.Length > 0 && args[0].ToLowerInvariant() == "hide") { uiOff.Add(p.userID); CuiHelper.DestroyUi(p, Ui); p.ChatMessage(Tag + "Raid banner hidden. /raid show brings it back."); return; }
            if (args.Length > 0 && args[0].ToLowerInvariant() == "show") { uiOff.Remove(p.userID); Banner(p); return; }
            string now = state == State.Grace ? $"WIPE GRACE (raiding opens in {Fmt(secsLeft)})" : state == State.Raid ? "RAID HOURS LIVE" + (secsLeft > 0 ? $" ({Fmt(secsLeft)} left)" : "") : state == State.Ffa ? "FREE FOR ALL" : "BASES PROTECTED" + (secsLeft > 0 ? $" (raids open in {Fmt(secsLeft)})" : "");
            p.ChatMessage(Tag + $"<color=#ffd27f>RAIDING</color>\nRight now: {now}\nRaid hours: {Hours()}\n" +
                "Outside raid hours every base is protected and shows a blue bubble - no registering needed.\n" +
                "In raid hours, the first rocket/C4 on a clan base starts a 45 min raid: red bubble, only those two clans can fight inside. Destroy the TC to win the bounty; if it survives, the defenders win and get a 30 min rebuild bubble.\n" +
                "A base where everyone is offline (10+ min) takes half damage.\n" +
                "First hour after wipe is grace. Free-for-all after the bounty days: raid any time.\n/raid hide | /raid show - the banner");
        }

        [ConsoleCommand("raidhours.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoRaidHours 1.4.0: {raids.Count} raid(s) live | {state} (mode {force}), {domes.Count} bubbles up ({purpleUp} purple, lake {LakePhaseNow()})" + (secsLeft > 0 ? $" ({Fmt(secsLeft)})" : "") + $" | hours {Hours()} | since load: {blocked} hits blocked, {halved} offline-halved");
        }
    }
}

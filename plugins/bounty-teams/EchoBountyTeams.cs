// EchoBountyTeams 1.0.0 (Nick, 31 Oct 2026) - the Echo Bounty System, run by the server itself.
//  * Who: the top 10 clans on the EchoClans leaderboard (score) that have 10+ players in their Rust team (team UI).
//    Disqualified clans are skipped for the rest of the wipe.
//  * When: the list is locked at the start of every bounty window (EchoTC bounty hours) and announced to everyone.
//  * Map: while bounty hours run, each bounty team's registered base (EchoTC) gets a red circle and a "BOUNTY [TAG]" label.
//  * Proof: every hit on a bounty team's base by another clan/player is logged (who, when, how many hits, explosives),
//    and a destroyed bounty TC is announced and logged - staff use the log instead of photo/video evidence.
//  * Wipe: list, log and disqualifications reset on a new map.
// Players:  /bountyteams          Staff: /bountyteams log | console bountyteams.status|log|dq <tag> [reason]|undq <tag>|refresh
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoBountyTeams", "Echo 10X", "1.0.0")]
    [Description("Automatic bounty teams: top 10 clans with 10+ players, locked each bounty window, base markers and a raid log.")]
    public class EchoBountyTeams : RustPlugin
    {
        [PluginReference] private Plugin EchoClans, EchoTC;
        private const int MaxTeams = 10, MinTeam = 10;
        private const float BaseRadius = 60f;
        private const string Tag = "<color=#ff6b5a>[Bounty Teams]</color> ";

        private class Store
        {
            public List<string> Teams = new List<string>();
            public Dictionary<string, string> Dq = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public List<string> Log = new List<string>();
            public string LockedAt = "";
        }
        private class Hit { public string Name, Clan, Target, Grid; public int Hits, Boom; public DateTime First, Last; public bool Logged; }

        private Store data;
        private bool bounty;
        private readonly Dictionary<string, Vector3> baseAt = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ulong> tcAt = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        private readonly List<BaseEntity> markers = new List<BaseEntity>(); private string markerSnap = "";
        private readonly Dictionary<string, Hit> hits = new Dictionary<string, Hit>();

        private void OnServerInitialized()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<Store>("EchoBountyTeams") ?? new Store();
            timer.Every(10f, Tick);
            timer.Every(300f, FlushHits);
            timer.Once(15f, Tick);
        }
        private void Unload() { FlushHits(); ClearMarkers(); Save(); }
        private void OnNewSave(string f) { data = new Store(); Save(); Puts("New wipe: bounty teams, raid log and disqualifications reset."); }
        private void Save() => Interface.Oxide.DataFileSystem.WriteObject("EchoBountyTeams", data);
        private void Log(string line) { var l = DateTime.UtcNow.ToString("ddd HH:mm") + " UTC  " + line; data.Log.Add(l); if (data.Log.Count > 400) data.Log.RemoveAt(0); Save(); Puts(line); }

        // ---------- bounty hours ----------
        private bool BountyNow() { try { var r = EchoTC?.Call("API_BountyNow"); return r is bool && (bool)r; } catch { return false; } }

        private void Tick()
        {
            bool b = BountyNow();
            if (b && !bounty) { bounty = true; Lock(); Announce(); }
            else if (!b && bounty) { bounty = false; FlushHits(); ClearMarkers(); baseAt.Clear(); tcAt.Clear(); Server.Broadcast(Tag + "Bounty hours are over. Bounty teams are shielded again until the next window."); }
            if (bounty) RefreshBases();
        }

        // ---------- who qualifies ----------
        private string ClanTagOf(ulong id)
        {
            try { var d = EchoClans?.Call("API_ClanInfo", id) as Dictionary<string, object>; object v; if (d != null && d.TryGetValue("inClan", out v) && v is bool && (bool)v && d.TryGetValue("tag", out v)) return v as string; } catch { }
            return null;
        }
        // tag -> one member id + Rust team size (the team UI count)
        private Dictionary<string, KeyValuePair<ulong, int>> Rosters()
        {
            var res = new Dictionary<string, KeyValuePair<ulong, int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in BasePlayer.allPlayerList.ToList())
            {
                if (p == null || p.IsNpc) continue;
                var tag = ClanTagOf(p.userID); if (string.IsNullOrEmpty(tag)) continue;
                int team = 0; try { var t = RelationshipManager.ServerInstance?.FindPlayersTeam(p.userID); if (t != null) team = t.members.Count; } catch { }
                KeyValuePair<ulong, int> cur;
                if (!res.TryGetValue(tag, out cur) || team > cur.Value) res[tag] = new KeyValuePair<ulong, int>(p.userID, team);
            }
            return res;
        }
        private List<string> Candidates(out List<string> why)
        {
            why = new List<string>(); var list = new List<string>();
            List<Dictionary<string, object>> board = null;
            try { board = EchoClans?.Call("API_Leaderboard", "score", 50) as List<Dictionary<string, object>>; } catch { }
            if (board == null) { why.Add("clans leaderboard not available"); return list; }
            var rosters = Rosters();
            foreach (var row in board)
            {
                var tag = row["tag"] as string; if (string.IsNullOrEmpty(tag)) continue;
                string dq; if (data.Dq.TryGetValue(tag, out dq)) { why.Add($"[{tag}] disqualified: {dq}"); continue; }
                KeyValuePair<ulong, int> r; int team = rosters.TryGetValue(tag, out r) ? r.Value : 0;
                if (team < MinTeam) { why.Add($"[{tag}] has {team}/{MinTeam} in its team"); continue; }
                list.Add(tag);
                if (list.Count >= MaxTeams) break;
            }
            return list;
        }
        private void Lock()
        {
            List<string> why; data.Teams = Candidates(out why); data.LockedAt = DateTime.UtcNow.ToString("u"); Save();
            Log("Bounty window started. Bounty teams: " + (data.Teams.Count > 0 ? string.Join(", ", data.Teams.Select(t => "[" + t + "]")) : "none qualify"));
        }
        private void Announce()
        {
            if (data.Teams.Count == 0) { Server.Broadcast(Tag + "BOUNTY HOURS! No clan qualifies as a bounty team yet: you need 10+ players in your team and a top 10 leaderboard spot. /bountyteams"); return; }
            Server.Broadcast(Tag + "BOUNTY HOURS! This window's bounty teams: <color=#ffd27f>" + string.Join("  ", data.Teams.Select(t => "[" + t + "]")) + "</color>. Their bases are marked on the map. /bountyteams");
        }

        // ---------- bases on the map ----------
        private void RefreshBases()
        {
            var rosters = Rosters();
            foreach (var tag in data.Teams)
            {
                KeyValuePair<ulong, int> r; if (!rosters.TryGetValue(tag, out r)) continue;
                ulong tc = 0; try { var o = EchoTC?.Call("API_ClanBaseTc", r.Key); if (o is ulong) tc = (ulong)o; } catch { }
                if (tc == 0) { tcAt.Remove(tag); baseAt.Remove(tag); continue; }
                var ent = BaseNetworkable.serverEntities.Find(new NetworkableId(tc)) as BaseEntity;
                if (ent == null) continue;
                tcAt[tag] = tc; baseAt[tag] = ent.transform.position;
            }
            var snap = string.Join(";", baseAt.OrderBy(k => k.Key).Select(k => k.Key + k.Value.ToString("F0")));
            if (snap == markerSnap && markers.Count > 0) return;   // nothing moved: keep the markers (no flicker)
            ClearMarkers();
            markerSnap = snap;
            foreach (var kv in baseAt)
            {
                var m = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", kv.Value) as MapMarkerGenericRadius;
                if (m != null) { m.alpha = 0.55f; m.color1 = new Color(1f, 0.25f, 0.2f); m.color2 = Color.black; m.radius = 0.3f; m.Spawn(); m.SendUpdate(); markers.Add(m); }
                var v = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", kv.Value) as VendingMachineMapMarker;
                if (v != null) { v.markerShopName = "BOUNTY [" + kv.Key + "]"; v.Spawn(); v.SendNetworkUpdate(); markers.Add(v); }
            }
        }
        private void ClearMarkers() { markerSnap = ""; foreach (var m in markers) if (m != null && !m.IsDestroyed) m.Kill(); markers.Clear(); }

        // ---------- raid log ----------
        private string BountyBaseNear(Vector3 pos)
        {
            foreach (var kv in baseAt) if ((kv.Value - pos).sqrMagnitude < BaseRadius * BaseRadius) return kv.Key;
            return null;
        }
        private object OnEntityTakeDamage(BaseCombatEntity e, HitInfo info)
        {
            if (!bounty || baseAt.Count == 0 || e == null || info == null) return null;
            if (!(e is BuildingBlock || e is Door || e is BuildingPrivlidge || e is SimpleBuildingBlock)) return null;
            var a = info.InitiatorPlayer; if (a == null || a.IsNpc) return null;
            var target = BountyBaseNear(e.transform.position); if (target == null) return null;
            var ac = ClanTagOf(a.userID);
            if (ac != null && string.Equals(ac, target, StringComparison.OrdinalIgnoreCase)) return null;
            var key = target + "|" + a.UserIDString; Hit h;
            if (!hits.TryGetValue(key, out h)) { h = new Hit { Name = a.displayName, Clan = ac ?? "no clan", Target = target, Grid = Grid(e.transform.position), First = DateTime.UtcNow }; hits[key] = h; Log($"RAID: {h.Name} [{h.Clan}] started hitting bounty team [{target}]'s base at {h.Grid}"); }
            h.Hits++; h.Last = DateTime.UtcNow;
            if (info.damageTypes.Has(Rust.DamageType.Explosion)) h.Boom++;
            return null;
        }
        private void OnEntityDeath(BaseCombatEntity e, HitInfo info)
        {
            var tc = e as BuildingPrivlidge; if (tc == null || tc.net == null || !bounty) return;
            foreach (var kv in tcAt.ToList())
            {
                if (kv.Value != tc.net.ID.Value) continue;
                var a = info?.InitiatorPlayer; var who = a != null ? $"{a.displayName} [{ClanTagOf(a.userID) ?? "no clan"}]" : "unknown";
                Log($"RAID: bounty team [{kv.Key}]'s TC at {Grid(tc.transform.position)} was DESTROYED by {who}");
                Server.Broadcast(Tag + $"Bounty team <color=#ffd27f>[{kv.Key}]</color> just lost their TC to {who}!");
                tcAt.Remove(kv.Key);
            }
        }
        private void FlushHits()
        {
            foreach (var h in hits.Values.Where(x => x.Hits > 0).ToList())
                Log($"RAID SUMMARY: {h.Name} [{h.Clan}] on [{h.Target}] at {h.Grid}: {h.Hits} hits ({h.Boom} explosive), {h.First:HH:mm}-{h.Last:HH:mm} UTC");
            hits.Clear();
        }
        private static string Grid(Vector3 p)
        {
            float size = TerrainMeta.Size.x; int cells = Mathf.FloorToInt(size / 146.3f); float cell = size / cells;
            int col = Mathf.Clamp(Mathf.FloorToInt((p.x + size / 2f) / cell), 0, cells - 1), row = Mathf.Clamp(Mathf.FloorToInt((size / 2f - p.z) / cell), 0, cells - 1);
            string c = col < 26 ? ((char)('A' + col)).ToString() : "A" + (char)('A' + col - 26);
            return c + row;
        }

        // ---------- commands ----------
        [ChatCommand("bountyteams")]
        private void ChTeams(BasePlayer p, string cmd, string[] args)
        {
            bool staff = p.IsAdmin;
            if (staff && args.Length > 0 && args[0] == "log") { p.ChatMessage(Tag + "Raid log (latest):\n" + string.Join("\n", data.Log.Skip(Math.Max(0, data.Log.Count - 12)))); return; }
            List<string> why; var now = Candidates(out why);
            var lines = new List<string>();
            lines.Add(Tag + "Top 10 clans with 10+ players in their team become bounty teams, locked at the start of each bounty window.");
            if (bounty) lines.Add("Bounty teams right now: " + (data.Teams.Count > 0 ? string.Join("  ", data.Teams.Select(t => "[" + t + "]")) : "none qualify"));
            else lines.Add("If bounty started now: " + (now.Count > 0 ? string.Join("  ", now.Select(t => "[" + t + "]")) : "no clan qualifies yet"));
            lines.Add("Prizes: real-money prize pool, store credit and private giveaways. Cheating = disqualified.");
            p.ChatMessage(string.Join("\n", lines));
        }
        [ConsoleCommand("bountyteams.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (!Staff(a)) return; List<string> why; var now = Candidates(out why);
            a.ReplyWith($"EchoBountyTeams: bounty {(bounty ? "ON" : "off")} | locked: {string.Join(", ", data.Teams)} ({data.LockedAt}) | would qualify now: {string.Join(", ", now)} | bases marked {baseAt.Count} | DQ: {string.Join(", ", data.Dq.Keys)}\n" + string.Join("\n", why.Take(15)));
        }
        [ConsoleCommand("bountyteams.log")] private void CLog(ConsoleSystem.Arg a) { if (!Staff(a)) return; a.ReplyWith(string.Join("\n", data.Log.Skip(Math.Max(0, data.Log.Count - 40)))); }
        [ConsoleCommand("bountyteams.dq")]
        private void CDq(ConsoleSystem.Arg a)
        {
            if (!Staff(a) || !a.HasArgs(1)) { a.ReplyWith("bountyteams.dq <tag> [reason]"); return; }
            var tag = a.GetString(0); var reason = a.HasArgs(2) ? string.Join(" ", a.Args.Skip(1)) : "broke server rules";
            data.Dq[tag] = reason; data.Teams.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)); Save();
            Log($"[{tag}] DISQUALIFIED for this wipe: {reason}");
            Server.Broadcast(Tag + $"[{tag}] has been disqualified from the bounty system for this wipe.");
            if (bounty) RefreshBases();
            a.ReplyWith($"[{tag}] disqualified");
        }
        [ConsoleCommand("bountyteams.undq")] private void CUndq(ConsoleSystem.Arg a) { if (!Staff(a) || !a.HasArgs(1)) return; data.Dq.Remove(a.GetString(0)); Save(); a.ReplyWith("removed"); }
        [ConsoleCommand("bountyteams.refresh")] private void CRefresh(ConsoleSystem.Arg a) { if (!Staff(a)) return; Lock(); if (bounty) { RefreshBases(); Announce(); } a.ReplyWith("locked: " + string.Join(", ", data.Teams)); }
        private static bool Staff(ConsoleSystem.Arg a) => a.Connection == null || a.IsAdmin;
    }
}

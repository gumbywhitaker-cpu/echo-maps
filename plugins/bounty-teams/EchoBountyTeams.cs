// EchoBountyTeams 1.3.0 (Nick, 31 Oct): a raid takes HALF. Every clan starts the month on $50. Each time a clan's TC is
//   destroyed in bounty hours the raiding clan takes money: the 1st loss takes HALF the defender's money, the 2nd loss takes
//   EVERYTHING left - lose 2 raids and you're out. Winning a raid (e.g. raiding back) clears your strikes.
// EchoBountyTeams 1.2.1: no duplicate markers. The map already marks every clan base (EchoStaffMap in EchoTweaks,
//   "[TAG] $50 BOUNTY K13"); this plugin now writes each clan's live money into that label through EchoBountyPrices
//   instead of spawning its own markers. A clan's money = its own $50 (until someone takes it) + what it has won.
// EchoBountyTeams 1.2.0 (Nick, 31 Oct): TEST bounty money. Every clan with a registered base carries a $50 bounty with its own map marker
//   in bounty hours. Destroy a clan's TC in bounty hours and your clan takes their $50; the losing clan can raid you back to take it
//   back. Day 3 is free for all (EchoTC). The bank lasts across wipes and resets monthly; the richest clan can cash out.
//   TEST ONLY: not real currency until the full release.
// EchoBountyTeams 1.1.0: staff card in EchoCommand (API_GetEventCard/API_OpenPanel) with an Echo-style control window,
//   AUTO/MANUAL mode (manual: staff add/remove teams by hand), and player/clan-leader state for the /pp BOUNTY tab.
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
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("EchoBountyTeams", "Echo 10X", "1.5.0")]
    [Description("Automatic bounty teams: top 10 clans with 10+ players, locked each bounty window, base markers and a raid log.")]
    public class EchoBountyTeams : RustPlugin
    {
        [PluginReference] private Plugin EchoClans, EchoTC, EchoBountyPrices, EchoRaidHours;
        private const int MaxTeams = 10, MinTeam = 10;
        // 1.2.0 test weekend: every clan with a registered base is a bounty clan (no 10-player / top-10 rule)
        private const bool TestAllClans = true;
        private const int BountyAmount = 50;
        private const string TestNote = "TEST: bounty money is not real currency until the full release.";
        private const float BaseRadius = 60f;
        private const string Tag = "<color=#ff6b5a>[Bounty Teams]</color> ";

        private class Store
        {
            public List<string> Teams = new List<string>();
            public Dictionary<string, string> Dq = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public List<string> Log = new List<string>();
            public string LockedAt = "";
            public bool Manual;                                   // 1.1.0: staff pick the teams by hand
            public List<string> ManualTeams = new List<string>();
        }
        private class Claim { public string From, To; public int Amount; public string At; public bool Open = true; }
        private class Bank
        {
            public string Month = "";
            public Dictionary<string, int> Money = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public List<Claim> Claims = new List<Claim>();                       // this wipe's claims (reset on wipe)
            public List<string> Claimed = new List<string>();                    // (1.2.x, unused since 1.3.0)
            public Dictionary<string, int> Losses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // 1.3.0 strikes this month
            public List<string> History = new List<string>();                    // past month winners
        }
        private Bank bank;
        private void SaveBank() => Interface.Oxide.DataFileSystem.WriteObject("EchoBountyTeams_Bank", bank);
        private static string MonthNow() { var syd = DateTime.UtcNow.AddHours(11); return syd.ToString("yyyy-MM"); }
        // 1.3.0: Money holds each clan's whole balance; a clan with no entry yet has the starting $50
        private int MoneyOf(string tag) { int m; return tag == null ? 0 : bank.Money.TryGetValue(tag, out m) ? m : BountyAmount; }
        private int Total(string tag) => MoneyOf(tag);
        private static int HalfOf(int bal) => bal <= 0 ? 0 : Math.Max(1, bal / 2);
        private int StrikesOf(string tag) { int n; return tag != null && bank.Losses != null && bank.Losses.TryGetValue(tag, out n) ? n : 0; }
        private readonly Dictionary<string, string> pushed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // write every clan's money into the existing base-marker label (EchoBountyPrices -> EchoStaffMap), only when it changed
        private void SyncPrices()
        {
            if (EchoBountyPrices == null || !EchoBountyPrices.IsLoaded) return;
            List<Dictionary<string, object>> dir = null;
            try { dir = EchoClans?.Call("API_ClanDirectory") as List<Dictionary<string, object>>; } catch { }
            if (dir == null) return;
            foreach (var c in dir)
            {
                var tag = c.ContainsKey("tag") ? c["tag"]?.ToString() : null; if (string.IsNullOrEmpty(tag)) continue;
                var v = Total(tag).ToString(); string old;
                if (pushed.TryGetValue(tag, out old) && old == v) continue;
                try { EchoBountyPrices.Call("API_SetClanBounty", null, tag, v); pushed[tag] = v; } catch { }
            }
        }
        private void AddMoney(string tag, int amt) { bank.Money[tag] = Math.Max(0, MoneyOf(tag) + amt); }
        private string BountyLabel(string tag)
        {
            return "$" + MoneyOf(tag) + (StrikesOf(tag) >= 1 ? " (1 loss - next raid takes all)" : " (a raid takes half)");
            var c = bank.Claims.LastOrDefault(x => x.Open && string.Equals(x.From, tag, StringComparison.OrdinalIgnoreCase));
            return c != null ? "$0 - taken by [" + c.To + "]" : "$0";
        }
        private void CheckMonth()
        {
            var now = MonthNow();
            if (bank.Month == now) return;
            if (!string.IsNullOrEmpty(bank.Month) && bank.Money.Count > 0)
            {
                var top = bank.Money.Keys.Select(k => new KeyValuePair<string, int>(k, Total(k))).OrderByDescending(k => k.Value).First();
                if (top.Value <= BountyAmount) { bank.Month = now; bank.Money.Clear(); SaveBank(); return; }   // nobody gained anything
                var line = $"{bank.Month}: [{top.Key}] finished with ${top.Value} and can cash out (TEST - not real money)";
                bank.History.Add(line); Log("MONTH END " + line);
                Server.Broadcast(Tag + $"Month over! <color=#ffd27f>[{top.Key}]</color> finished with <color=#ffd27f>${top.Value}</color> and can cash out. Bounty bank reset. <size=11>({TestNote})</size>");
            }
            bank.Month = now; bank.Money.Clear(); bank.Losses?.Clear(); SaveBank();
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
            bank = Interface.Oxide.DataFileSystem.ReadObject<Bank>("EchoBountyTeams_Bank") ?? new Bank();
            CheckMonth();
            timer.Every(10f, Tick);
            timer.Every(300f, FlushHits);
            timer.Once(15f, Tick);
        }
        private void Unload() { FlushHits(); ClearMarkers(); Save(); foreach (var pl in BasePlayer.activePlayerList) CuiHelper.DestroyUi(pl, UI); }
        private void OnNewSave(string f) { var man = data != null && data.Manual; data = new Store { Manual = man }; Save(); if (bank != null) { bank.Claims.Clear(); bank.Claimed.Clear(); SaveBank(); } Puts("New wipe: bounty teams, raid log, claims and disqualifications reset (bank money kept for the month)."); }
        private void Save() => Interface.Oxide.DataFileSystem.WriteObject("EchoBountyTeams", data);
        private void Log(string line) { var l = DateTime.UtcNow.ToString("ddd HH:mm") + " UTC  " + line; data.Log.Add(l); if (data.Log.Count > 400) data.Log.RemoveAt(0); Save(); Puts(line); }

        // ---------- bounty hours ----------
        private bool BountyNow() { try { var r = EchoTC?.Call("API_BountyNow"); return r is bool && (bool)r; } catch { return false; } }

        private void Tick()
        {
            CheckMonth();
            SyncPrices();
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
                if (!TestAllClans && team < MinTeam) { why.Add($"[{tag}] has {team}/{MinTeam} in its team"); continue; }
                list.Add(tag);
                if (!TestAllClans && list.Count >= MaxTeams) break;
            }
            return list;
        }
        private void Lock()
        {
            List<string> why;
            data.Teams = data.Manual ? data.ManualTeams.Where(t => !data.Dq.ContainsKey(t)).Take(MaxTeams).ToList() : Candidates(out why);
            data.LockedAt = DateTime.UtcNow.ToString("u"); Save();
            Log((data.Manual ? "[MANUAL] " : "") + "Bounty window started. Bounty teams: " + (data.Teams.Count > 0 ? string.Join(", ", data.Teams.Select(t => "[" + t + "]")) : "none qualify"));
        }
        private void Announce()
        {
            if (data.Teams.Count == 0) { Server.Broadcast(Tag + (TestAllClans ? "BOUNTY HOURS! No clan has a bounty yet - make a clan (/clan create) and build a base. /bountyteams" : "BOUNTY HOURS! No clan qualifies as a bounty team yet: you need 10+ players in your team and a top 10 leaderboard spot. /bountyteams")); return; }
            Server.Broadcast(Tag + "BOUNTY HOURS! Every clan base on the map shows its bounty. Destroy a clan's TC and your clan takes <color=#ffd27f>half their money</color> - lose 2 raids and you're out. Got raided? Raid them back to win it back. /bountyteams <size=11>(" + TestNote + ")</size>");
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
            ClearMarkers();   // 1.2.1: markers come from EchoStaffMap; we only feed it the money
            SyncPrices();
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
            // 1.5.0 (Nick, 31 Oct): in a clan raid (EchoRaidHours) breaking the TC only starts the capture - the bounty is paid
            // when the attackers fill the capture bar (OnEchoRaidWon below).
            try { if (EchoRaidHours?.Call("API_RaidOn", tc.buildingID) is bool live && live) return; } catch { }
            // 1.4.0 (Nick, 31 Oct): no more base registration - ANY clan TC (guarding 10+ building pieces) destroyed in raid
            // hours pays out. Registered TCs still count the old way below.
            if (!tcAt.Values.Contains(tc.net.ID.Value))
            {
                var vic = tc.OwnerID != 0UL ? ClanTagOf(tc.OwnerID) : null;
                int size = 0; try { size = tc.GetBuilding()?.buildingBlocks?.Count ?? 0; } catch { }
                if (!string.IsNullOrEmpty(vic) && size >= 10)
                {
                    var a2 = info?.InitiatorPlayer; var who2 = a2 != null ? $"{a2.displayName} [{ClanTagOf(a2.userID) ?? "no clan"}]" : "unknown";
                    Log($"RAID: [{vic}]'s TC at {Grid(tc.transform.position)} ({size} pieces) was DESTROYED by {who2}");
                    Payout(vic, a2 != null ? ClanTagOf(a2.userID) : null, who2);
                    markerSnap = "";
                }
                return;
            }
            foreach (var kv in tcAt.ToList())
            {
                if (kv.Value != tc.net.ID.Value) continue;
                var a = info?.InitiatorPlayer; var who = a != null ? $"{a.displayName} [{ClanTagOf(a.userID) ?? "no clan"}]" : "unknown";
                Log($"RAID: bounty team [{kv.Key}]'s TC at {Grid(tc.transform.position)} was DESTROYED by {who}");
                var ac = a != null ? ClanTagOf(a.userID) : null;
                Payout(kv.Key, ac, who);
                tcAt.Remove(kv.Key);
                markerSnap = "";
            }
        }
        private void OnEchoRaidWon(string victim, string winner, string who)
        {
            if (string.IsNullOrEmpty(victim)) return;
            Log($"RAID: [{winner}] CAPTURED [{victim}]'s base ({who})");
            Payout(victim, winner, who); markerSnap = "";
        }
        // 1.2.0: the money. victim = clan whose TC died, winner = attacking clan (null = no clan, nobody is paid)
        private void Payout(string victim, string winner, string who)
        {
            if (string.IsNullOrEmpty(winner) || string.Equals(winner, victim, StringComparison.OrdinalIgnoreCase))
            { Server.Broadcast(Tag + $"<color=#ffd27f>[{victim}]</color> just lost their TC to {who}. Only a clan can claim a bounty."); return; }
            if (bank.Losses == null) bank.Losses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int strikes = StrikesOf(victim) + 1;
            int bal = MoneyOf(victim), amt = strikes >= 2 ? bal : HalfOf(bal);
            bank.Losses[victim] = strikes; bank.Losses[winner] = 0;   // winning a raid clears your strikes
            if (amt <= 0) { Server.Broadcast(Tag + $"<color=#ffd27f>[{winner}]</color> raided <color=#ffd27f>[{victim}]</color>, but [{victim}] has no bounty money left."); return; }
            bank.Money[victim] = bal - amt;
            bank.Money[winner] = MoneyOf(winner) + amt;
            bank.Claims.Add(new Claim { From = victim, To = winner, Amount = amt, At = DateTime.UtcNow.ToString("u"), Open = false });
            if (bank.Claims.Count > 500) bank.Claims.RemoveRange(0, bank.Claims.Count - 500);
            SaveBank(); pushed.Clear(); SyncPrices();
            Log($"BOUNTY: [{winner}] raided [{victim}] and took ${amt} of ${bal} (loss {strikes}). [{winner}] ${MoneyOf(winner)}, [{victim}] ${MoneyOf(victim)}");
            Server.Broadcast(Tag + $"<color=#ffd27f>[{winner}]</color> destroyed <color=#ffd27f>[{victim}]</color>'s TC and took <color=#ffd27f>${amt}</color>" + (strikes >= 2 ? $" - everything [{victim}] had left. [{victim}] is OUT (2 raids lost)" : $" - half their bounty. One more loss and [{victim}] is out") + $". [{winner}] now ${MoneyOf(winner)}. Raid them back to win it back. <size=11>({TestNote})</size>");
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


        // ================= 1.1.0: leaderboard rows, APIs for EchoCommand and /pp =================
        private class Row { public string Tag, Name; public int Rank, Team, Score; public bool Qual; public string Why = ""; }
        private List<Row> BoardRows()
        {
            var rows = new List<Row>();
            List<Dictionary<string, object>> board = null;
            try { board = EchoClans?.Call("API_Leaderboard", "score", 50) as List<Dictionary<string, object>>; } catch { }
            if (board == null) return rows;
            var rosters = Rosters(); int i = 0;
            foreach (var r in board)
            {
                var tag = r["tag"] as string; if (string.IsNullOrEmpty(tag)) continue; i++;
                KeyValuePair<ulong, int> ro; int team = rosters.TryGetValue(tag, out ro) ? ro.Value : 0;
                var row = new Row { Tag = tag, Name = r.ContainsKey("name") ? r["name"] as string : "", Rank = i, Team = team, Score = r.ContainsKey("score") ? Convert.ToInt32(r["score"]) : 0 };
                string dq;
                if (data.Dq.TryGetValue(tag, out dq)) row.Why = "disqualified: " + dq;
                else if (!TestAllClans && team < MinTeam) row.Why = $"{team}/{MinTeam} in team";
                else row.Qual = true;
                rows.Add(row);
            }
            int q = 0; foreach (var r in rows) if (r.Qual) { q++; if (!TestAllClans && q > MaxTeams) { r.Qual = false; r.Why = "outside the top 10 qualifiers"; } }
            return rows;
        }

        // EchoCommand event card
        private Dictionary<string, object> API_GetEventCard()
        {
            return new Dictionary<string, object>
            {
                ["id"] = "bountyteams", ["title"] = "Bounty Teams", ["image"] = "ev_bounty", ["active"] = bounty, ["auto"] = !data.Manual,
                ["flag"] = data.Manual ? "MANUAL" : "AUTO",
                ["status"] = bounty ? (data.Teams.Count > 0 ? data.Teams.Count + " bounty team" + (data.Teams.Count == 1 ? "" : "s") + " live: " + string.Join(" ", data.Teams.Take(4).Select(t => "[" + t + "]")) : "Bounty hours - no team qualifies")
                                    : (data.Manual ? "Manual: " + data.ManualTeams.Count + " picked" : "Auto: locks at the next bounty window")
            };
        }
        private void API_OpenPanel(BasePlayer p) { if (p != null && p.IsConnected && p.IsAdmin) DrawStaff(p); }
        private string API_Act(BasePlayer p, string op, string arg) => Act(p, op, arg);

        // /pp BOUNTY tab (players and clan leaders)
        private Dictionary<string, object> API_PlayerState(BasePlayer p)
        {
            var rows = BoardRows();
            var teams = bounty ? data.Teams : (data.Manual ? data.ManualTeams.Where(t => !data.Dq.ContainsKey(t)).Take(MaxTeams).ToList() : rows.Where(r => r.Qual).Select(r => r.Tag).ToList());
            var list = teams.Select(t => { var r = rows.FirstOrDefault(x => string.Equals(x.Tag, t, StringComparison.OrdinalIgnoreCase)); return (object)new Dictionary<string, object> { ["tag"] = t, ["rank"] = r?.Rank ?? 0, ["team"] = r?.Team ?? 0, ["base"] = baseAt.ContainsKey(t), ["label"] = BountyLabel(t), ["money"] = MoneyOf(t) }; }).ToList();
            var my = new Dictionary<string, object> { ["inClan"] = false };
            var tag = p != null ? ClanTagOf(p.userID) : null;
            if (tag != null)
            {
                var r = rows.FirstOrDefault(x => string.Equals(x.Tag, tag, StringComparison.OrdinalIgnoreCase));
                int team = 0; try { var t = RelationshipManager.ServerInstance?.FindPlayersTeam(p.userID); if (t != null) team = t.members.Count; } catch { }
                bool isB = teams.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
                ulong tc = 0; try { var o = EchoTC?.Call("API_ClanBaseTc", p.userID); if (o is ulong) tc = (ulong)o; } catch { }
                string dq; data.Dq.TryGetValue(tag, out dq);
                my = new Dictionary<string, object> { ["inClan"] = true, ["tag"] = tag, ["team"] = team, ["need"] = Math.Max(0, MinTeam - team), ["rank"] = r?.Rank ?? 0, ["bounty"] = isB, ["qual"] = r != null && r.Qual, ["why"] = r?.Why ?? "not on the leaderboard yet", ["base"] = tc != 0, ["dq"] = dq ?? "" };
            }
            if (tag != null) { my["money"] = Total(tag); my["bountyLabel"] = BountyLabel(tag); }
            var rich = bank.Money.Keys.Select(k => new KeyValuePair<string, int>(k, Total(k))).Where(k => k.Value > 0).OrderByDescending(k => k.Value).Take(5).Select(k => (object)("[" + k.Key + "] $" + k.Value)).ToList();
            return new Dictionary<string, object> { ["bounty"] = bounty, ["manual"] = data.Manual, ["teams"] = list, ["my"] = my, ["min"] = MinTeam, ["max"] = MaxTeams,
                ["test"] = TestAllClans, ["amount"] = BountyAmount, ["rich"] = rich, ["month"] = bank.Month, ["note"] = TestNote };
        }

        // ---------- staff actions (UI buttons, EchoCommand, console) ----------
        private string Act(BasePlayer p, string op, string arg)
        {
            if (p != null && !p.IsAdmin) return "staff only";
            string who = p != null ? p.displayName : "console";
            switch (op)
            {
                case "auto": data.Manual = false; Save(); Log($"{who} switched bounty teams to AUTO"); if (bounty) { Lock(); RefreshBases(); Announce(); } return "Auto: the server picks the top 10 qualifying clans.";
                case "manual": data.Manual = true; if (data.ManualTeams.Count == 0) data.ManualTeams = new List<string>(data.Teams); Save(); Log($"{who} switched bounty teams to MANUAL"); return "Manual: staff pick the teams.";
                case "toggle": return Act(p, data.Manual ? "auto" : "manual", arg);
                case "add":
                    if (string.IsNullOrEmpty(arg)) return "no clan";
                    if (!data.ManualTeams.Contains(arg, StringComparer.OrdinalIgnoreCase)) data.ManualTeams.Add(arg);
                    if (!data.Manual) { data.Manual = true; Log($"{who} switched bounty teams to MANUAL"); }
                    Save(); Log($"{who} added [{arg}] as a bounty team"); if (bounty) { Lock(); RefreshBases(); } return $"[{arg}] added.";
                case "remove":
                    data.ManualTeams.RemoveAll(t => string.Equals(t, arg, StringComparison.OrdinalIgnoreCase));
                    if (data.Manual) data.Teams.RemoveAll(t => string.Equals(t, arg, StringComparison.OrdinalIgnoreCase));
                    Save(); Log($"{who} removed [{arg}] from the bounty teams"); if (bounty) RefreshBases(); return $"[{arg}] removed.";
                case "dq":
                    if (string.IsNullOrEmpty(arg)) return "no clan";
                    data.Dq[arg] = "broke server rules"; data.Teams.RemoveAll(t => string.Equals(t, arg, StringComparison.OrdinalIgnoreCase)); data.ManualTeams.RemoveAll(t => string.Equals(t, arg, StringComparison.OrdinalIgnoreCase)); Save();
                    Log($"[{arg}] DISQUALIFIED for this wipe by {who}"); Server.Broadcast(Tag + $"[{arg}] has been disqualified from the bounty system for this wipe."); if (bounty) RefreshBases(); return $"[{arg}] disqualified.";
                case "undq": data.Dq.Remove(arg ?? ""); Save(); Log($"{who} lifted the disqualification on [{arg}]"); return $"[{arg}] can qualify again.";
                case "bankreset": bank.Money.Clear(); bank.Claims.Clear(); bank.Claimed.Clear(); bank.Losses?.Clear(); SaveBank(); Log($"{who} reset the bounty bank"); return "Bounty bank reset.";
                case "relock": Lock(); if (bounty) { RefreshBases(); Announce(); } return "Locked: " + (data.Teams.Count > 0 ? string.Join(" ", data.Teams.Select(t => "[" + t + "]")) : "none");
            }
            return "unknown action";
        }

        // ---------- staff window (Echo art kit from EchoCommand_Images) ----------
        private const string UI = "EchoBountyTeams.UI";
        private readonly Dictionary<string, string> art = new Dictionary<string, string>();
        private readonly Dictionary<ulong, string> view = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, string> msg = new Dictionary<ulong, string>();
        private static readonly string[] ArtKeys = { "window_bg_1000x640", "ev_bounty", "close_34x34", "pane_318x510", "pane_648x510", "btn_on_145x32", "btn_145x32", "btn_red_144x32", "btn_teal_145x32", "btn_90x26", "btn_on_94x28", "prow_632x36", "flag_ok_70x16", "flag_cy_70x16", "flag_red_70x16", "chip_on_112x26", "chip_112x26" };
        private void LoadArt()
        {
            if (art.Count > 0) return;
            var net = CommunityEntity.ServerInstance.net.ID;
            foreach (var f in new[] { "EchoCommand_Images", "EchoCommand_Images_Extra" })
            {
                Dictionary<string, string> kit = null;
                try { if (Interface.Oxide.DataFileSystem.ExistsDatafile(f)) kit = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>(f); } catch { }
                if (kit == null) continue;
                foreach (var k in ArtKeys) { string b; if (!art.ContainsKey(k) && kit.TryGetValue(k, out b)) try { art[k] = FileStorage.server.Store(Convert.FromBase64String(b), FileStorage.Type.png, net, 0u).ToString(); } catch { } }
            }
        }
        private const string Cy = "0.098 0.925 1 1", White = "1 1 1 1", Dim = "0.62 0.72 0.78 1", Ok = "0.45 0.95 0.55 1", Red = "1 0.42 0.36 1", Gold = "1 0.82 0.4 1";
        private static string Off(float x0, float y0) => $"{x0} {-y0}";
        private void Pic(CuiElementContainer c, string parent, string key, float x0, float y0, float x1, float y1, string fallback = null)
        {
            string id;
            if (art.TryGetValue(key, out id)) c.Add(new CuiElement { Parent = parent, Components = { new CuiRawImageComponent { Png = id }, new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{x0} {-y1}", OffsetMax = $"{x1} {-y0}" } } });
            else if (fallback != null) c.Add(new CuiPanel { Image = { Color = fallback }, RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{x0} {-y1}", OffsetMax = $"{x1} {-y0}" } }, parent);
        }
        private void T(CuiElementContainer c, string parent, string text, int size, string color, TextAnchor a, float x0, float y0, float x1, float y1, bool bold = false)
            => c.Add(new CuiElement { Parent = parent, Components = { new CuiTextComponent { Text = text, FontSize = size, Color = color, Align = a, Font = bold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{x0} {-y1}", OffsetMax = $"{x1} {-y0}" } } });
        private void B(CuiElementContainer c, string parent, string cmd, string label, string skin, string color, float x0, float y0, float x1, float y1, int size = 11)
        {
            Pic(c, parent, skin, x0, y0, x1, y1, "0.12 0.2 0.25 0.95");
            T(c, parent, label, size, color, TextAnchor.MiddleCenter, x0, y0, x1, y1, true);
            c.Add(new CuiButton { Button = { Command = cmd, Color = "0 0 0 0" }, Text = { Text = "" }, RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{x0} {-y1}", OffsetMax = $"{x1} {-y0}" } }, parent);
        }

        private void DrawStaff(BasePlayer p)
        {
            LoadArt();
            CuiHelper.DestroyUi(p, UI);
            string v; if (!view.TryGetValue(p.userID, out v)) v = "teams";
            var c = new CuiElementContainer();
            c.Add(new CuiPanel { Image = { Color = "0 0 0 0.55" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }, CursorEnabled = true, KeyboardEnabled = true }, "Overlay", UI);
            var w = UI + ".w";
            c.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-500 -320", OffsetMax = "500 320" } }, UI, w);
            Pic(c, w, "window_bg_1000x640", 0, 0, 1000, 640, "0.04 0.07 0.09 0.98");
            T(c, w, "BOUNTY TEAMS", 24, White, TextAnchor.MiddleLeft, 30, 16, 500, 50, true);
            T(c, w, "ECHO BOUNTY SYSTEM  ·  $" + BountyAmount + " start, 1st loss half, 2nd loss all  ·  bank " + bank.Month + "  ·  " + TestNote, 11, Cy, TextAnchor.MiddleLeft, 30, 46, 930, 64);
            Pic(c, w, "close_34x34", 946, 16, 980, 50, "0.6 0.15 0.15 0.9");
            c.Add(new CuiButton { Button = { Command = "bountyteams.ui close", Color = "0 0 0 0" }, Text = { Text = "" }, RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "946 -50", OffsetMax = "980 -16" } }, w);

            // left: art + mode + status
            Pic(c, w, "pane_318x510", 20, 74, 338, 620, "0.07 0.11 0.14 0.95");
            Pic(c, w, "ev_bounty", 34, 88, 324, 270);
            float y = 282;
            T(c, w, "MODE", 11, Dim, TextAnchor.MiddleLeft, 36, y, 150, y + 20, true);
            B(c, w, "bountyteams.ui do toggle", data.Manual ? "MANUAL" : "AUTO", data.Manual ? "btn_145x32" : "btn_on_145x32", data.Manual ? Gold : Ok, 170, y - 4, 315, y + 28);
            y += 40;
            T(c, w, "BOUNTY HOURS", 11, Dim, TextAnchor.MiddleLeft, 36, y, 170, y + 20, true);
            T(c, w, bounty ? "ON - bases marked" : "off", 12, bounty ? Red : White, TextAnchor.MiddleRight, 170, y, 315, y + 20, true);
            y += 26;
            T(c, w, "LOCKED", 11, Dim, TextAnchor.MiddleLeft, 36, y, 170, y + 20, true);
            T(c, w, string.IsNullOrEmpty(data.LockedAt) ? "not yet" : data.LockedAt.Replace("Z", " UTC"), 11, White, TextAnchor.MiddleRight, 150, y, 315, y + 20);
            y += 26;
            T(c, w, "TEAMS / DQ", 11, Dim, TextAnchor.MiddleLeft, 36, y, 170, y + 20, true);
            T(c, w, (bounty ? data.Teams.Count : (data.Manual ? data.ManualTeams.Count : BoardRows().Count(r => r.Qual))) + " / " + data.Dq.Count, 12, White, TextAnchor.MiddleRight, 170, y, 315, y + 20, true);
            y += 36;
            B(c, w, "bountyteams.ui do relock", "RE-LOCK NOW", "btn_teal_145x32", White, 34, y, 179, y + 32);
            B(c, w, "bountyteams.ui view log", "RAID LOG", v == "log" ? "btn_on_145x32" : "btn_145x32", White, 183, y, 324, y + 32);
            y += 44;
            T(c, w, data.Manual ? "Manual: only the clans you ADD are bounty teams. Switch back to AUTO and the server picks the top 10 again."
                                : "Auto: at every bounty window the server takes the top 10 clans on the leaderboard with 10+ players in their team.", 10, Dim, TextAnchor.UpperLeft, 36, y, 320, y + 70);
            string m; if (msg.TryGetValue(p.userID, out m) && !string.IsNullOrEmpty(m)) T(c, w, m, 11, Gold, TextAnchor.MiddleLeft, 36, 586, 330, 610, true);

            // right: tabs
            Pic(c, w, "pane_648x510", 352, 74, 980, 620, "0.07 0.11 0.14 0.95");
            B(c, w, "bountyteams.ui view teams", "BOUNTY TEAMS", v == "teams" ? "chip_on_112x26" : "chip_112x26", White, 368, 88, 498, 114, 10);
            B(c, w, "bountyteams.ui view board", "LEADERBOARD", v == "board" ? "chip_on_112x26" : "chip_112x26", White, 504, 88, 634, 114, 10);
            B(c, w, "bountyteams.ui view log", "RAID LOG", v == "log" ? "chip_on_112x26" : "chip_112x26", White, 640, 88, 770, 114, 10);
            y = 126;
            if (v == "log")
            {
                var lines = data.Log.Skip(Math.Max(0, data.Log.Count - 22)).ToList();
                if (lines.Count == 0) T(c, w, "Nothing logged yet this wipe.", 12, Dim, TextAnchor.MiddleLeft, 372, y, 960, y + 24);
                foreach (var l in lines) { T(c, w, l, 10, l.Contains("DESTROYED") || l.Contains("DISQUALIFIED") ? Red : l.Contains("RAID") ? Gold : White, TextAnchor.MiddleLeft, 372, y, 966, y + 20); y += 21; }
            }
            else if (v == "board")
            {
                T(c, w, "#   CLAN                         TEAM      BANK      STATUS", 10, Dim, TextAnchor.MiddleLeft, 376, y, 960, y + 18, true); y += 22;
                foreach (var r in BoardRows().Take(14))
                {
                    bool isT = data.Teams.Contains(r.Tag, StringComparer.OrdinalIgnoreCase) || (data.Manual && data.ManualTeams.Contains(r.Tag, StringComparer.OrdinalIgnoreCase));
                    Pic(c, w, "prow_632x36", 366, y, 966, y + 30, "0.09 0.13 0.16 0.85");
                    T(c, w, r.Rank + ".", 11, Dim, TextAnchor.MiddleLeft, 376, y, 400, y + 30, true);
                    T(c, w, "[" + r.Tag + "] " + r.Name, 11, isT ? Gold : White, TextAnchor.MiddleLeft, 400, y, 600, y + 30, true);
                    T(c, w, r.Team + "/" + MinTeam, 11, r.Team >= MinTeam ? Ok : Dim, TextAnchor.MiddleLeft, 606, y, 660, y + 30);
                    T(c, w, "$" + Total(r.Tag), 11, Gold, TextAnchor.MiddleLeft, 664, y, 730, y + 30, true);
                    T(c, w, isT ? "BOUNTY TEAM" : r.Qual ? "qualifies" : r.Why, 10, isT ? Gold : r.Qual ? Ok : Dim, TextAnchor.MiddleLeft, 734, y, 860, y + 30);
                    if (data.Manual && !isT && !data.Dq.ContainsKey(r.Tag)) B(c, w, "bountyteams.ui do add " + r.Tag, "ADD", "btn_90x26", Ok, 866, y + 3, 956, y + 27, 10);
                    else if (data.Dq.ContainsKey(r.Tag)) B(c, w, "bountyteams.ui do undq " + r.Tag, "UN-DQ", "btn_90x26", White, 866, y + 3, 956, y + 27, 10);
                    y += 33;
                }
            }
            else
            {
                var teams = bounty ? data.Teams : (data.Manual ? data.ManualTeams : BoardRows().Where(r => r.Qual).Select(r => r.Tag).ToList());
                T(c, w, bounty ? "LOCKED FOR THIS BOUNTY WINDOW" : data.Manual ? "PICKED BY STAFF (locks when bounty hours start)" : "WOULD BE LOCKED IF BOUNTY STARTED NOW", 11, Cy, TextAnchor.MiddleLeft, 372, y, 960, y + 20, true); y += 26;
                if (teams.Count == 0) T(c, w, data.Manual ? "No teams picked. Open LEADERBOARD and press ADD." : "No clan qualifies yet: 10+ players in the team and a top 10 leaderboard spot.", 12, Dim, TextAnchor.MiddleLeft, 372, y, 960, y + 24);
                var rows = BoardRows();
                foreach (var t in teams.Take(MaxTeams))
                {
                    var r = rows.FirstOrDefault(x => string.Equals(x.Tag, t, StringComparison.OrdinalIgnoreCase));
                    Pic(c, w, "prow_632x36", 366, y, 966, y + 36, "0.09 0.13 0.16 0.85");
                    T(c, w, "[" + t + "]" + (r != null && !string.IsNullOrEmpty(r.Name) ? " " + r.Name : ""), 12, Gold, TextAnchor.MiddleLeft, 378, y, 600, y + 36, true);
                    T(c, w, (r != null ? "#" + r.Rank + "  ·  " + r.Team + " in team" : "not on the board") + (bounty ? (baseAt.ContainsKey(t) ? "  ·  base marked" : "  ·  no base registered") : ""), 10, Dim, TextAnchor.MiddleLeft, 600, y, 790, y + 36);
                    if (data.Manual) B(c, w, "bountyteams.ui do remove " + t, "REMOVE", "btn_90x26", White, 774, y + 5, 864, y + 31, 10);
                    B(c, w, "bountyteams.ui do dq " + t, "DISQUALIFY", "btn_red_144x32", White, 868, y + 5, 960, y + 31, 9);
                    y += 40;
                }
            }
            CuiHelper.AddUi(p, c);
        }

        [ConsoleCommand("bountyteams.ui")]
        private void CUi(ConsoleSystem.Arg a)
        {
            var p = a.Player(); if (p == null || !p.IsAdmin) return;
            string op = a.GetString(0, "");
            if (op == "close") { CuiHelper.DestroyUi(p, UI); return; }
            if (op == "view") view[p.userID] = a.GetString(1, "teams");
            if (op == "do") msg[p.userID] = Act(p, a.GetString(1, ""), a.GetString(2, ""));
            DrawStaff(p);
        }
        [ChatCommand("bountyadmin")] private void ChAdmin(BasePlayer p, string c, string[] args) { if (p.IsAdmin) DrawStaff(p); }

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

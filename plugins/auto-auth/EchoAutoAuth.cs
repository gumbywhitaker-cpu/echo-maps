// EchoAutoAuth 2.0.0 (Nick, 31 Oct 2026): access follows your CLAN (and team), not individual players.
//   - Tool cupboards, auto turrets and code locks placed by a player are shared with everyone in their clan and team.
//   - Join the clan/team -> you're added to all of them. Leave or get kicked -> you're taken off all of them.
//   - New code locks lock themselves with a random code; the owner is told the code; clan + team are whitelisted.
//   - Only access this plugin added is ever removed; anything a player set up by hand stays.
// Re-checked every 30 s and straight away on clan/team changes. Console: autoauth.status
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("EchoAutoAuth", "Echo 10X", "2.0.0")]
    [Description("Clan/team-based access to TCs, turrets and code locks, plus auto code locks.")]
    public class EchoAutoAuth : RustPlugin
    {
        [PluginReference] private Plugin EchoClans;
        private class Store { public Dictionary<ulong, HashSet<ulong>> Added = new Dictionary<ulong, HashSet<ulong>>(); } // entity net id -> players we added
        private Store data;
        private readonly HashSet<BaseEntity> ents = new HashSet<BaseEntity>();
        private int added, removed, codes; private bool dirty, queued;

        private void Init() { data = Interface.Oxide.DataFileSystem.ReadObject<Store>("EchoAutoAuth") ?? new Store(); }
        private void OnServerInitialized()
        {
            foreach (var n in BaseNetworkable.serverEntities) Track(n as BaseEntity);
            timer.Every(30f, SyncAll); timer.Every(60f, Save); Queue();
        }
        private void Unload() => Save();
        private void OnNewSave(string f) { data = new Store(); dirty = true; Save(); }
        private void Save() { if (!dirty) return; dirty = false; Interface.Oxide.DataFileSystem.WriteObject("EchoAutoAuth", data); }

        private static bool Kind(BaseEntity e) => e is BuildingPrivlidge || e is AutoTurret || e is CodeLock;
        private void Track(BaseEntity e) { if (e != null && Kind(e)) ents.Add(e); }
        private void OnEntitySpawned(BaseNetworkable n) { var e = n as BaseEntity; if (e == null || !Kind(e)) return; ents.Add(e); NextTick(() => { if (e is CodeLock) AutoCode((CodeLock)e); Sync(e); }); }
        private void OnEntityKill(BaseNetworkable n) { var e = n as BaseEntity; if (e == null || !Kind(e)) return; ents.Remove(e); if (e.net != null && data.Added.Remove(e.net.ID.Value)) dirty = true; }

        // clan / team changes -> resync
        private void OnClanMemberJoined(string tag, string id, string name) => Queue();
        private void OnClanMemberLeft(string tag, string id, string why) => Queue();
        private void OnClanDisbanded(string tag) => Queue();
        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player) => Queue();
        private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player) => Queue();
        private void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong target) => Queue();
        private void OnTeamDisbanded(RelationshipManager.PlayerTeam team) => Queue();
        private void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team) => Queue();
        private void Queue() { if (queued) return; queued = true; timer.Once(1f, () => { queued = false; SyncAll(); }); }

        // owner's group = owner + clan members + team members
        private readonly Dictionary<ulong, HashSet<ulong>> groupCache = new Dictionary<ulong, HashSet<ulong>>();
        private HashSet<ulong> Group(ulong owner)
        {
            HashSet<ulong> g; if (groupCache.TryGetValue(owner, out g)) return g;
            g = new HashSet<ulong> { owner };
            try
            {
                var d = EchoClans?.Call("API_ClanInfo", owner) as Dictionary<string, object>;
                if (d != null && d.ContainsKey("inClan") && (bool)d["inClan"] && d.ContainsKey("members"))
                    foreach (var m in (d["members"] as System.Collections.IEnumerable) ?? new object[0])
                    { var md = m as Dictionary<string, object>; ulong id; if (md != null && md.ContainsKey("id") && ulong.TryParse(md["id"]?.ToString(), out id)) g.Add(id); }
            }
            catch { }
            var team = RelationshipManager.ServerInstance?.FindPlayersTeam(owner);
            if (team != null) foreach (var m in team.members) g.Add(m);
            groupCache[owner] = g; return g;
        }

        private void SyncAll() { groupCache.Clear(); foreach (var e in ents.ToList()) Sync(e); }

        private void Sync(BaseEntity e)
        {
            if (e == null || e.IsDestroyed || e.net == null) { if (e != null) ents.Remove(e); return; }
            ulong owner = e.OwnerID; if (owner == 0UL || !owner.IsSteamId()) return;
            var want = Group(owner);
            ulong id = e.net.ID.Value;
            HashSet<ulong> ours; data.Added.TryGetValue(id, out ours);
            bool changed = false;
            foreach (var m in want)
            {
                if (!m.IsSteamId() || Has(e, m)) continue;
                if (!Add(e, m)) continue;
                changed = true; added++;
                if (ours == null) data.Added[id] = ours = new HashSet<ulong>();
                ours.Add(m); dirty = true;
            }
            if (ours != null)
                foreach (var m in ours.ToList())
                {
                    if (want.Contains(m)) continue;
                    ours.Remove(m); dirty = true;
                    if (Remove(e, m)) { changed = true; removed++; }
                }
            if (changed)
            {
                e.SendNetworkUpdate();
                if (e is BuildingPrivlidge) foreach (var m in want) { var p = BasePlayer.FindByID(m); if (p != null && p.IsConnected) p.SendNetworkUpdate(); }
            }
        }

        private static bool Has(BaseEntity e, ulong m)
        {
            var tc = e as BuildingPrivlidge; if (tc != null) return tc.authorizedPlayers.Contains(m);
            var t = e as AutoTurret; if (t != null) return t.authorizedPlayers.Contains(m);
            var l = e as CodeLock; if (l != null) return l.whitelistPlayers.Contains(m);
            return true;
        }
        private static bool Add(BaseEntity e, ulong m)
        {
            var tc = e as BuildingPrivlidge; if (tc != null) { tc.authorizedPlayers.Add(m); return true; }
            var t = e as AutoTurret; if (t != null) { t.authorizedPlayers.Add(m); return true; }
            var l = e as CodeLock; if (l != null) { l.whitelistPlayers.Add(m); return true; }
            return false;
        }
        private static bool Remove(BaseEntity e, ulong m)
        {
            var tc = e as BuildingPrivlidge; if (tc != null) return tc.authorizedPlayers.Remove(m);
            var t = e as AutoTurret; if (t != null) return t.authorizedPlayers.Remove(m);
            var l = e as CodeLock; if (l != null) return l.whitelistPlayers.Remove(m);
            return false;
        }

        // ---------- auto code lock ----------
        private void AutoCode(CodeLock l)
        {
            if (l == null || l.IsDestroyed || l.IsLocked() || !string.IsNullOrEmpty(l.code)) return;
            ulong owner = l.OwnerID; if (owner == 0UL || !owner.IsSteamId()) return;
            var code = UnityEngine.Random.Range(1000, 10000).ToString();
            l.code = code;
            l.hasCode = true;
            if (!l.whitelistPlayers.Contains(owner)) l.whitelistPlayers.Add(owner);
            l.SetFlag(BaseEntity.Flags.Locked, true);
            l.SendNetworkUpdate(); codes++;
            var p = BasePlayer.FindByID(owner);
            p?.ChatMessage($"<color=#39d2d1>ECHO</color> Code lock set and locked. Code: <color=#ffd27f>{code}</color> - your clan and team can open it.");
        }

        [ConsoleCommand("autoauth.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoAutoAuth 2.0.0: on | watching {ents.OfType<BuildingPrivlidge>().Count()} TCs, {ents.OfType<AutoTurret>().Count()} turrets, {ents.OfType<CodeLock>().Count()} code locks | {data.Added.Sum(k => k.Value.Count)} clan/team auths in place | since load: {added} added, {removed} removed, {codes} code locks auto-set");
        }
    }
}

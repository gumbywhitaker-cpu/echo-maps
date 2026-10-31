// EchoAutoAuth 1.0.0 (Nick, 31 Oct 2026): everyone in your team/clan is authorised on your tool cupboards automatically.
// - A TC placed by a player authorises their whole team straight away (clans are synced to Rust teams by EchoClans).
// - Join a team -> you get added to every TC your teammates placed. Leave / get kicked -> you're taken off them again.
// - Only auths this plugin added are ever removed; anyone a player authorised by hand stays put.
// - Re-checked every 30 s and on every team change, so nothing is missed after restarts or clan syncs.
// Console: autoauth.status
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("EchoAutoAuth", "Echo 10X", "1.0.0")]
    [Description("Auto-authorise team/clan members on each other's tool cupboards.")]
    public class EchoAutoAuth : RustPlugin
    {
        private class Store { public Dictionary<ulong, HashSet<ulong>> Added = new Dictionary<ulong, HashSet<ulong>>(); } // tc net id -> players we added
        private Store data;
        private readonly HashSet<BuildingPrivlidge> tcs = new HashSet<BuildingPrivlidge>();
        private int added, removed; private bool dirty, queued;

        private void Init() { data = Interface.Oxide.DataFileSystem.ReadObject<Store>("EchoAutoAuth") ?? new Store(); }
        private void OnServerInitialized()
        {
            foreach (var n in BaseNetworkable.serverEntities) { var t = n as BuildingPrivlidge; if (t != null) tcs.Add(t); }
            timer.Every(30f, SyncAll); timer.Every(60f, Save); Queue();
        }
        private void Unload() => Save();
        private void OnNewSave(string f) { data = new Store(); dirty = true; Save(); }
        private void Save() { if (!dirty) return; dirty = false; Interface.Oxide.DataFileSystem.WriteObject("EchoAutoAuth", data); }

        private void OnEntitySpawned(BuildingPrivlidge tc) { if (tc == null) return; tcs.Add(tc); NextTick(() => Sync(tc)); }
        private void OnEntityKill(BuildingPrivlidge tc) { if (tc == null) return; tcs.Remove(tc); if (tc.net != null && data.Added.Remove(tc.net.ID.Value)) dirty = true; }

        // any team change -> resync shortly after Rust has applied it
        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player) => Queue();
        private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player) => Queue();
        private void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong target) => Queue();
        private void OnTeamDisbanded(RelationshipManager.PlayerTeam team) => Queue();
        private void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team) => Queue();
        private void Queue() { if (queued) return; queued = true; timer.Once(1f, () => { queued = false; SyncAll(); }); }

        private void SyncAll() { foreach (var tc in tcs.ToList()) Sync(tc); }

        private void Sync(BuildingPrivlidge tc)
        {
            if (tc == null || tc.IsDestroyed || tc.net == null) { if (tc != null) tcs.Remove(tc); return; }
            ulong owner = tc.OwnerID; if (owner == 0UL || !owner.IsSteamId()) return;
            var team = RelationshipManager.ServerInstance?.FindPlayersTeam(owner);
            var want = team != null ? new HashSet<ulong>(team.members) : new HashSet<ulong> { owner };
            ulong id = tc.net.ID.Value;
            HashSet<ulong> ours; if (!data.Added.TryGetValue(id, out ours)) ours = null;
            bool changed = false;
            foreach (var m in want)
            {
                if (!m.IsSteamId() || tc.authorizedPlayers.Contains(m)) continue;
                tc.authorizedPlayers.Add(m); changed = true; added++;
                if (ours == null) data.Added[id] = ours = new HashSet<ulong>();
                ours.Add(m); dirty = true;
            }
            if (ours != null)
                foreach (var m in ours.ToList())
                {
                    if (want.Contains(m)) continue;
                    ours.Remove(m); dirty = true;
                    if (tc.authorizedPlayers.Remove(m)) { changed = true; removed++; }
                }
            if (changed)
            {
                tc.SendNetworkUpdate();
                foreach (var m in want) { var p = BasePlayer.FindByID(m); if (p != null && p.IsConnected) p.SendNetworkUpdate(); }
            }
        }

        [ConsoleCommand("autoauth.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoAutoAuth 1.0.0: on | {tcs.Count} TCs watched | {data.Added.Sum(k => k.Value.Count)} team auths in place | since load: {added} added, {removed} removed (players who left a team)");
        }
    }
}

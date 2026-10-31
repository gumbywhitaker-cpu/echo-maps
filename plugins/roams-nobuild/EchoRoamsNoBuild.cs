// EchoRoamsNoBuild 1.0.0 (Nick, 31 Oct 2026): no building or deploying on Roam Island.
// The island centre comes live from EchoRoams (API_Centre), so it follows every map. Covers building blocks and every
// deployable (TCs, boxes, turrets, sleeping bags, ladders...) within the island radius. Admins (ownerid) can still build.
// Console: roamsnobuild.status | roamsnobuild.list (player-built things already on the island)
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoRoamsNoBuild", "Echo 10X", "1.0.0")]
    [Description("No building or deploying on Roam Island.")]
    public class EchoRoamsNoBuild : RustPlugin
    {
        [PluginReference] private Plugin EchoRoams;
        private const float Radius = 160f;   // EchoRoams island radius is 150 m; a little extra so nobody builds on the edge
        private Vector3 centre; private bool have; private int blocked;

        private void OnServerInitialized() { Refresh(); timer.Every(60f, Refresh); }
        private void OnPluginLoaded(Plugin p) { if (p != null && p.Name == "EchoRoams") timer.Once(5f, Refresh); }
        private void Refresh()
        {
            try { var r = EchoRoams?.Call("API_Centre"); if (r is Vector3 && ((Vector3)r).sqrMagnitude > 1f) { centre = (Vector3)r; have = true; } } catch { }
        }

        private bool OnIsland(Vector3 pos) => have && new Vector2(pos.x - centre.x, pos.z - centre.z).sqrMagnitude <= Radius * Radius;

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var p = planner?.GetOwnerPlayer();
            if (p == null || (p.net?.connection?.authLevel ?? 0) >= 2) return null;
            Vector3 pos = target.entity != null ? target.entity.transform.position : target.position;
            if (!OnIsland(pos) && !OnIsland(p.transform.position)) return null;
            blocked++;
            p.ChatMessage("<color=#39d2d1>ECHO</color> No building on <color=#ffd27f>Roam Island</color> - it's an event arena.");
            return false;
        }

        [ConsoleCommand("roamsnobuild.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith(have ? $"EchoRoamsNoBuild: on | Roam Island centre {centre:F0}, no building within {Radius:0} m | {blocked} attempts blocked since load" : "EchoRoamsNoBuild: waiting for the Roam Island centre from EchoRoams");
        }

        [ConsoleCommand("roamsnobuild.list")]
        private void CList(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            var ents = BaseNetworkable.serverEntities.OfType<BaseEntity>().Where(e => e != null && !e.IsDestroyed && e.OwnerID != 0UL && e.OwnerID.IsSteamId() && (e is BuildingBlock || e is DecayEntity || e is StorageContainer) && OnIsland(e.transform.position)).ToList();
            var by = ents.GroupBy(e => e.OwnerID).Select(g => (BasePlayer.FindAwakeOrSleeping(g.Key.ToString())?.displayName ?? g.Key.ToString()) + " " + g.Count());
            a.ReplyWith($"Player-built on Roam Island: {ents.Count} ({ents.OfType<BuildingPrivlidge>().Count()} TCs) | " + string.Join(", ", by));
        }
    }
}

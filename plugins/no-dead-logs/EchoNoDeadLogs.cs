// EchoNoDeadLogs 1.0.0 (Nick, 1 Nov 2026): fallen dead logs trap players, so there are none.
//   Removes every dead log (logs_dry / logs_snow / logs_wet) on load and stops new ones spawning. Works on every map.
//   Standing trees, wood piles and everything else are untouched.
// Console: nodeadlogs.status
namespace Oxide.Plugins
{
    [Info("EchoNoDeadLogs", "Echo 10X", "1.0.0")]
    [Description("No fallen dead logs - they trap players.")]
    public class EchoNoDeadLogs : RustPlugin
    {
        private int removed;
        private readonly System.Collections.Generic.Queue<BaseEntity> q = new System.Collections.Generic.Queue<BaseEntity>();
        private Timer drain;

        private static bool IsLog(BaseNetworkable n) => n != null && n.PrefabName != null && n.PrefabName.Contains("/autospawn/resource/logs_");

        private void OnServerInitialized()
        {
            foreach (var n in BaseNetworkable.serverEntities) if (IsLog(n)) q.Enqueue((BaseEntity)n);
            drain = timer.Every(0.2f, () =>
            {
                for (int i = 0; i < 100 && q.Count > 0; i++) { var e = q.Dequeue(); if (e != null && !e.IsDestroyed) { e.Kill(); removed++; } }
                if (q.Count == 0) { drain?.Destroy(); drain = null; }
            });
        }

        private void OnEntitySpawned(BaseNetworkable n)
        {
            if (!IsLog(n)) return;
            NextTick(() => { if (n != null && !n.IsDestroyed) { n.Kill(); removed++; } });
        }

        private void Unload() { drain?.Destroy(); }

        [ConsoleCommand("nodeadlogs.status")]
        private void CStatus(ConsoleSystem.Arg a) { if (a.Connection != null && !a.IsAdmin) return; a.ReplyWith($"EchoNoDeadLogs 1.0.0: {removed} dead logs removed since load, {q.Count} still queued"); }
    }
}

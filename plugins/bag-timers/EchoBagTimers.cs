// EchoBagTimers 1.0.0 (Nick, 31 Oct 2026): respawn cooldowns - sleeping bags (and towels) 90 s, beds 60 s.
// Applied to every bag on load and to each new one as it's placed. Console: bagtimers.status
using System.Linq;

namespace Oxide.Plugins
{
    [Info("EchoBagTimers", "Echo 10X", "1.0.0")]
    [Description("Sleeping bag 90 s / bed 60 s respawn cooldowns.")]
    public class EchoBagTimers : RustPlugin
    {
        private const float BagSeconds = 90f, BedSeconds = 60f;
        private int set;

        private void OnServerInitialized() { foreach (var b in BaseNetworkable.serverEntities.OfType<SleepingBag>().ToList()) Apply(b); }
        private void OnEntitySpawned(SleepingBag b) => NextTick(() => Apply(b));

        private void Apply(SleepingBag b)
        {
            if (b == null || b.IsDestroyed) return;
            float want = b.ShortPrefabName.Contains("bed") ? BedSeconds : BagSeconds;
            if (b.secondsBetweenReuses == want) return;
            // shorten a cooldown that is already running so nobody waits the old time
            float left = b.unlockTime - UnityEngine.Time.realtimeSinceStartup;
            if (left > want) b.unlockTime = UnityEngine.Time.realtimeSinceStartup + want;
            b.secondsBetweenReuses = want; set++;
            b.SendNetworkUpdate();
        }

        [ConsoleCommand("bagtimers.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoBagTimers 1.0.0: bags {BagSeconds:0}s, beds {BedSeconds:0}s | {set} bags set since load");
        }
    }
}

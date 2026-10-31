// EchoRocketDamage 1.0.0 (Nick, 31 Oct 2026): rockets do at most 60 damage to a player (was ~100+).
// Only player victims are changed - damage to bases, deployables and vehicles is exactly as before.
// Covers basic, HV, incendiary and seeker rockets. Console: rocketdmg.status
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoRocketDamage", "Echo 10X", "1.0.0")]
    [Description("Rockets do at most 60 damage to players.")]
    public class EchoRocketDamage : RustPlugin
    {
        private const float MaxToPlayer = 60f;
        private int scaled;

        private static bool IsRocket(HitInfo info)
        {
            var w = info?.WeaponPrefab; if (w == null) return false;
            var n = w.ShortPrefabName ?? "";
            return n.StartsWith("rocket_") && !n.Contains("mlrs");
        }

        private void OnEntityTakeDamage(BasePlayer victim, HitInfo info)
        {
            if (victim == null || info == null || victim.IsNpc || !IsRocket(info)) return;
            float total = info.damageTypes.Total();
            if (total <= MaxToPlayer) return;
            info.damageTypes.ScaleAll(MaxToPlayer / total);
            scaled++;
        }

        [ConsoleCommand("rocketdmg.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoRocketDamage 1.0.0: rockets max {MaxToPlayer:0} damage to players (bases unchanged) | {scaled} hits scaled since load");
        }
    }
}

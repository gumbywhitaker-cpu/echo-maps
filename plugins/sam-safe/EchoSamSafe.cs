// EchoSamSafe 1.0.0 (Nick, 1 Nov 2026): monument SAM sites never shoot minicopters or scrap helis.
//   Only SAM sites with no player owner (monuments / map) are affected. Player-placed SAM sites work as normal.
// Console: samsafe.status
namespace Oxide.Plugins
{
    [Info("EchoSamSafe", "Echo 10X", "1.0.0")]
    [Description("Monument SAM sites ignore minicopters and scrap transport helicopters.")]
    public class EchoSamSafe : RustPlugin
    {
        private int ignored;

        private object OnSamSiteTarget(SamSite sam, object target)   // object: works with both the old and new hook signature
        {
            if (sam == null || target == null || sam.OwnerID != 0UL) return null;
            if (target is Minicopter || target is ScrapTransportHelicopter) { ignored++; return false; }
            return null;
        }

        [ConsoleCommand("samsafe.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoSamSafe 1.0.0: monument SAM sites ignore minicopters and scrap helis | {ignored} target checks skipped since load");
        }
    }
}

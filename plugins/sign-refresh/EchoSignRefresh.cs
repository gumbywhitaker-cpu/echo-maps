// EchoSignRefresh 1.0.0 (Nick, 1 Nov 2026): outpost + casino signs came up blank after a restart because they were
//   placed while the server was still starting. This re-places them (echobrand.respawn) 2 minutes after every server
//   start, when everything is fully up. Quiet - nothing in chat.
namespace Oxide.Plugins
{
    [Info("EchoSignRefresh", "Echo 10X", "1.0.0")]
    [Description("Re-places the Echo outpost/casino signs 2 minutes after the server starts so their images always show.")]
    public class EchoSignRefresh : RustPlugin
    {
        private void OnServerInitialized(bool initial)
        {
            if (!initial) return;                         // only after a real server start, not a plugin reload
            timer.Once(120f, () =>
            {
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "echobrand.respawn");
                Puts("Re-placed the outpost and casino signs after server start.");
            });
        }
    }
}

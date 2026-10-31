// EchoPop 1.0.0 (Nick, 31 Oct 2026): /pop and !pop - how many players are on. Answered to the asker only (no chat spam).
using System.Linq;
using ConVar;

namespace Oxide.Plugins
{
    [Info("EchoPop", "Echo 10X", "1.0.0")]
    [Description("/pop and !pop: online, joining, queued and sleeping players.")]
    public class EchoPop : RustPlugin
    {
        private float last;

        private void OnServerInitialized() { cmd.AddChatCommand("pop", this, nameof(CmdPop)); cmd.AddChatCommand("online", this, nameof(CmdPop)); }

        private string Text()
        {
            int on = BasePlayer.activePlayerList.Count;
            int joining = ServerMgr.Instance?.connectionQueue?.Joining ?? 0;
            int queued = ServerMgr.Instance?.connectionQueue?.Queued ?? 0;
            int sleep = BasePlayer.sleepingPlayerList.Count;
            return $"<color=#39d2d1>ECHO 10X</color> <color=#ffd27f>{on}</color>/{ConVar.Server.maxplayers} online" + (joining > 0 ? $", {joining} joining" : "") + (queued > 0 ? $", {queued} in queue" : "") + $", {sleep} sleeping";
        }

        private void CmdPop(BasePlayer p, string command, string[] args) => p?.ChatMessage(Text());

        private void OnPlayerChat(BasePlayer p, string message, Chat.ChatChannel channel)
        {
            if (p == null || string.IsNullOrEmpty(message)) return;
            var m = message.Trim().ToLowerInvariant();
            if (m != "!pop" && m != "!online") return;
            NextTick(() => p.ChatMessage(Text()));
        }
    }
}

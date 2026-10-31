// EchoNoGod 1.0.0 (Nick, 31 Oct 2026): only owners (Rust ownerid) and the Oxide "admin" group may use god mode.
// Moderators (moderatorid) get Rust's admin flag, and with it the F1 "god" command - a moderator who also plays could
// make themselves unkillable. Every second, anyone in god mode who isn't an owner/admin has the admin flag taken off
// (which ends god mode at once; their moderator rank in users.cfg is not touched and comes back on their next join).
// Each catch is logged (oxide/logs/EchoNoGod) and told to online owners/admins. Console: nogod.status
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("EchoNoGod", "Echo 10X", "1.0.0")]
    [Description("Only owners and admins can use god mode.")]
    public class EchoNoGod : RustPlugin
    {
        private int caught; private readonly Dictionary<ulong, int> per = new Dictionary<ulong, int>();

        private void OnServerInitialized() { timer.Every(1f, Check); }

        private bool Allowed(BasePlayer p) =>
            (p.net?.connection?.authLevel ?? 0) >= 2 || permission.UserHasGroup(p.UserIDString, "admin");

        private void Check()
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p == null || !p.IsConnected || !p.IsGod() || Allowed(p)) continue;
                p.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                p.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, false);
                p.SendNetworkUpdateImmediate();
                caught++; int n; per.TryGetValue(p.userID, out n); per[p.userID] = ++n;
                p.ChatMessage("<color=#39d2d1>ECHO</color> God mode is for server owners only - it's been switched off.");
                var msg = $"GOD MODE blocked: {p.displayName} ({p.UserIDString}) at {MapHelper.PositionToString(p.transform.position)} - admin flag removed until they rejoin (time #{n}).";
                Puts(msg);
                LogToFile("EchoNoGod", DateTime.UtcNow.ToString("u") + " " + msg, this);
                foreach (var a in BasePlayer.activePlayerList) if (a != null && a != p && Allowed(a)) a.ChatMessage("<color=#ff9a3c>[NO GOD]</color> " + msg);
            }
        }

        [ConsoleCommand("nogod.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoNoGod 1.0.0: on | god mode allowed for owners (ownerid) and the admin group only | {caught} blocked since load");
        }
    }
}

// EchoAutoDoor 1.0.0 (Nick, 1 Nov 2026): doors close by themselves.
//   Every door a player opens (doors, double doors, hatches, garage doors, high external gates) shuts again after 5 s.
//   /ad            - turn it on/off for yourself
//   /ad <seconds>  - set your own delay (2-30 s)
// Console: autodoor.status
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("EchoAutoDoor", "Echo 10X", "1.0.0")]
    [Description("Doors close by themselves after a few seconds. /ad to toggle or set the delay.")]
    public class EchoAutoDoor : RustPlugin
    {
        private const float DefaultDelay = 5f;
        private const string Tag = "<color=#39d2d1>ECHO</color> ";
        private class Pref { public bool Off; public float Delay = DefaultDelay; }
        private Dictionary<ulong, Pref> prefs;
        private readonly Dictionary<ulong, Timer> pending = new Dictionary<ulong, Timer>();
        private int closed; private bool dirty;

        private void Init() { prefs = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Pref>>("EchoAutoDoor") ?? new Dictionary<ulong, Pref>(); }
        private void OnServerInitialized() { cmd.AddChatCommand("ad", this, nameof(CmdAd)); cmd.AddChatCommand("autodoor", this, nameof(CmdAd)); timer.Every(120f, Save); }
        private void Unload() { foreach (var t in pending.Values) t?.Destroy(); pending.Clear(); Save(); }
        private void Save() { if (!dirty) return; dirty = false; Interface.Oxide.DataFileSystem.WriteObject("EchoAutoDoor", prefs); }

        private Pref P(ulong id) { Pref p; if (!prefs.TryGetValue(id, out p)) { p = new Pref(); prefs[id] = p; } return p; }

        private void OnDoorOpened(Door door, BasePlayer player)
        {
            if (door == null || player == null || door.net == null) return;
            Pref pr; prefs.TryGetValue(player.userID, out pr);
            if (pr != null && pr.Off) return;
            float delay = pr != null ? pr.Delay : DefaultDelay;
            ulong id = door.net.ID.Value;
            Timer old; if (pending.TryGetValue(id, out old)) old?.Destroy();
            pending[id] = timer.Once(delay, () =>
            {
                pending.Remove(id);
                if (door == null || door.IsDestroyed || !door.IsOpen()) return;
                door.SetOpen(false);
                closed++;
            });
        }

        private void OnDoorClosed(Door door, BasePlayer player)
        {
            if (door?.net == null) return; Timer t;
            if (pending.TryGetValue(door.net.ID.Value, out t)) { t?.Destroy(); pending.Remove(door.net.ID.Value); }
        }

        private void CmdAd(BasePlayer p, string command, string[] args)
        {
            if (p == null) return;
            var pr = P(p.userID); dirty = true;
            float s;
            if (args.Length > 0 && float.TryParse(args[0], out s))
            {
                pr.Delay = UnityEngine.Mathf.Clamp(s, 2f, 30f); pr.Off = false;
                p.ChatMessage(Tag + $"Auto door close ON - your doors shut after <color=#ffd27f>{pr.Delay:0} s</color>.");
                return;
            }
            pr.Off = !pr.Off;
            p.ChatMessage(Tag + (pr.Off ? "Auto door close OFF for you. /ad to turn it back on." : $"Auto door close ON - your doors shut after <color=#ffd27f>{pr.Delay:0} s</color>. /ad 10 sets 10 s."));
        }

        [ConsoleCommand("autodoor.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            int off = 0; foreach (var v in prefs.Values) if (v.Off) off++;
            a.ReplyWith($"EchoAutoDoor 1.0.0: on (default {DefaultDelay:0} s) | {pending.Count} doors waiting to close | {closed} closed since load | {off} players turned it off");
        }
    }
}

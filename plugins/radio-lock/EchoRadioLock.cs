// EchoRadioLock 1.0.1 (Nick, 31 Oct 2026): the Outpost radio (and every other map-placed boombox) plays the
// built-in stations only. A custom link typed in is switched back to a real station within 2 s. Player-owned
// boomboxes at bases are not touched.
// 1.0.1: lean - scans the map ONCE at load and tracks new boomboxes as they spawn; the 2 s check only looks at
//   the handful of owner-0 boomboxes instead of all 66k entities (1.0.0 cost 159 s hook time / 12 GB alloc).
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoRadioLock", "Echo 10X", "1.0.1")]
    [Description("Map/Outpost radios play built-in stations only - no custom links.")]
    public class EchoRadioLock : RustPlugin
    {
        private const string Default = "rustradio.facepunch.com";
        private int _resets;
        private readonly Dictionary<BaseEntity, BoomBox> _boxes = new Dictionary<BaseEntity, BoomBox>();

        private void OnServerInitialized()
        {
            foreach (var n in BaseNetworkable.serverEntities) Track(n as BaseEntity);
            timer.Every(2f, Check);
        }

        private void OnEntitySpawned(BaseNetworkable n) { var e = n as BaseEntity; if (e != null) NextTick(() => Track(e)); }

        private void Track(BaseEntity e)
        {
            if (e == null || e.IsDestroyed || e.OwnerID != 0) return;
            BoomBox bb = null;
            var db = e as DeployableBoomBox; if (db != null) bb = db.BoxController;
            else { var hb = e as HeldBoomBox; if (hb != null) bb = hb.BoxController; }
            if (bb == null && e.ShortPrefabName.Contains("boombox")) bb = e.GetComponent<BoomBox>();
            if (bb != null) _boxes[e] = bb;
        }

        private static bool In(Dictionary<string, string> d, string url) => d != null && (d.ContainsKey(url) || d.ContainsValue(url));
        private static bool Allowed(string url) =>
            string.IsNullOrEmpty(url) || url == Default || In(BoomBox.ValidStations, url) || In(BoomBox.ServerValidStations, url);

        private readonly List<BaseEntity> _dead = new List<BaseEntity>();
        private void Check()
        {
            _dead.Clear();
            foreach (var kv in _boxes)
            {
                var e = kv.Key; var bb = kv.Value;
                if (e == null || e.IsDestroyed || bb == null || e.OwnerID != 0) { _dead.Add(e); continue; }
                if (Allowed(bb.CurrentRadioIp)) continue;
                var was = bb.CurrentRadioIp;
                bb.CurrentRadioIp = Default;
                e.SendNetworkUpdate();
                _resets++;
                foreach (var p in BasePlayer.activePlayerList)
                    if (p != null && (p.transform.position - e.transform.position).sqrMagnitude < 625f)
                        p.ChatMessage("<color=#39d2d1>ECHO</color> The Outpost radio plays the built-in stations only - pick one from the list.");
                Puts($"Custom link '{was}' on {e.ShortPrefabName} at {e.transform.position:F0} switched back to {Default}.");
            }
            foreach (var d in _dead) _boxes.Remove(d);
        }

        [ConsoleCommand("radiolock.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoRadioLock 1.0.1: on, watching {_boxes.Count} map radio(s), {BoomBox.ValidStations?.Count ?? 0} built-in + {BoomBox.ServerValidStations?.Count ?? 0} server stations, {_resets} custom link(s) switched back since load");
        }
    }
}

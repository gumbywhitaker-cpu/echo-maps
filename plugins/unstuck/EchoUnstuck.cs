// EchoUnstuck 1.0.0 (Nick, 1 Nov 2026): /stuck (or /unstuck) frees a player trapped in a fallen tree, rock or terrain.
//   Moves you to the nearest clear spot on the ground within 15 m. Never into a building, under a roof, or onto
//   someone else's building-privileged ground. 60 s cooldown, not while combat/raid blocked.
// Console: unstuck.status
using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoUnstuck", "Echo 10X", "1.0.0")]
    [Description("/stuck - get out of a fallen tree, rock or terrain.")]
    public class EchoUnstuck : RustPlugin
    {
        [PluginReference] private Plugin NoEscape;
        private const string Tag = "<color=#39d2d1>ECHO</color> ";
        private readonly Dictionary<ulong, float> last = new Dictionary<ulong, float>();
        private int used;
        private static readonly int Block = Rust.Layers.Mask.Default | Rust.Layers.Mask.World | Rust.Layers.Mask.Construction | Rust.Layers.Mask.Deployed | Rust.Layers.Mask.Tree | Rust.Layers.Mask.Terrain;

        private void OnServerInitialized() { cmd.AddChatCommand("stuck", this, nameof(CmdStuck)); cmd.AddChatCommand("unstuck", this, nameof(CmdStuck)); }

        private void CmdStuck(BasePlayer p, string command, string[] args)
        {
            if (p == null || p.IsDead()) return;
            bool staff = (p.net?.connection?.authLevel ?? 0) >= 2;
            float t; if (!staff && last.TryGetValue(p.userID, out t) && Time.realtimeSinceStartup - t < 60f) { p.ChatMessage(Tag + $"/stuck is on cooldown for {Mathf.CeilToInt(60f - (Time.realtimeSinceStartup - t))}s."); return; }
            if (p.isMounted || p.IsSwimming()) { p.ChatMessage(Tag + "You can't use /stuck right now."); return; }
            try { if (!staff && NoEscape?.Call("IsBlocked", p) is bool b && b) { p.ChatMessage(Tag + "You can't use /stuck while combat or raid blocked."); return; } } catch { }
            Vector3 o = p.transform.position, spot;
            if (!Find(p, o, out spot)) { p.ChatMessage(Tag + "Couldn't find a clear spot nearby. Ask a staff member for help."); return; }
            last[p.userID] = Time.realtimeSinceStartup; used++;
            p.Teleport(spot); p.SendNetworkUpdateImmediate();
            p.ChatMessage(Tag + "Moved you to a clear spot.");
        }

        private bool Find(BasePlayer p, Vector3 o, out Vector3 spot)
        {
            for (float r = 2.5f; r <= 15f; r += 1.25f)
                for (int a = 0; a < 16; a++)
                {
                    float ang = a * Mathf.PI / 8f;
                    var q = new Vector3(o.x + Mathf.Cos(ang) * r, 0f, o.z + Mathf.Sin(ang) * r);
                    q.y = TerrainMeta.HeightMap.GetHeight(q) + 0.15f;
                    if (q.y < TerrainMeta.WaterMap.GetHeight(q) - 0.3f) continue;                                       // not into water
                    if (Physics.CheckCapsule(q + Vector3.up * 0.5f, q + Vector3.up * 1.7f, 0.45f, Block, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Raycast(q + Vector3.up * 1.7f, Vector3.up, 30f, Rust.Layers.Mask.Construction)) continue;  // under a roof = inside a base
                    var priv = p.GetBuildingPrivilege(new OBB(q, Quaternion.identity, new Bounds(Vector3.zero, Vector3.one)));
                    if (priv != null && !priv.IsAuthed(p)) continue;                                                     // someone else's base
                    spot = q; return true;
                }
            spot = o; return false;
        }

        [ConsoleCommand("unstuck.status")]
        private void CStatus(ConsoleSystem.Arg a) { if (a.Connection != null && !a.IsAdmin) return; a.ReplyWith($"EchoUnstuck 1.0.0: /stuck used {used} times since load"); }
    }
}

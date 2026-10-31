// EchoRoamsNoBuild 1.1.0 (Nick, 31 Oct 2026): Roam Island rules.
//   - No building or deploying on the island (admins/ownerid exempt).
//   - No kits on the island (EchoKits CanClaimEchoKit hook).
//   - No crafting shotguns, Eokas or anything that goes boom while on the island.
//   - /roamst [name] - teleport to a team/clan mate who is on Roam Island (not while combat/raid blocked, 60 s cooldown).
// The island centre comes live from EchoRoams (API_Centre), so it follows every map.
// Console: roamsnobuild.status | roamsnobuild.list | roamsnobuild.clear (removes player builds on the island)
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoRoamsNoBuild", "Echo 10X", "1.1.1")]
    [Description("Roam Island rules: no building, no kits, no shotgun/boom crafting, /roamst.")]
    public class EchoRoamsNoBuild : RustPlugin
    {
        [PluginReference] private Plugin EchoRoams, EchoClans, NoEscape;
        private const float Radius = 160f;   // EchoRoams island radius is 150 m; a little extra so nobody builds on the edge
        private const float TpCooldown = 60f;
        private const string Tag = "<color=#39d2d1>ECHO</color> ";
        private Vector3 centre; private bool have; private int blocked, kitsBlocked, craftsBlocked, tps;
        private readonly Dictionary<ulong, float> lastTp = new Dictionary<ulong, float>();

        private static readonly HashSet<string> NoCraft = new HashSet<string>
        {
            "shotgun.double", "shotgun.pump", "shotgun.spas12", "shotgun.waterpipe", "shotgun.m4", "pistol.eoka",
            "ammo.shotgun", "ammo.shotgun.slug", "ammo.shotgun.fire",
            "explosive.timed", "explosive.satchel", "explosives", "surveycharge", "grenade.f1", "grenade.beancan", "grenade.molotov",
            "rocket.launcher", "ammo.rocket.basic", "ammo.rocket.hv", "ammo.rocket.fire", "ammo.rocket.smoke", "ammo.rocket.seeker",
            "homingmissile.launcher", "multiplegrenadelauncher", "ammo.grenadelauncher.he", "ammo.grenadelauncher.buckshot", "ammo.grenadelauncher.smoke",
            "flamethrower", "catapult.ammo.explosive", "ammo.rifle.explosive"
        };

        private void OnServerInitialized() { Refresh(); timer.Every(60f, Refresh); cmd.AddChatCommand("roamst", this, nameof(CmdRoamsT)); }
        private void OnPluginLoaded(Plugin p) { if (p != null && p.Name == "EchoRoams") timer.Once(5f, Refresh); }
        private void Refresh()
        {
            try { var r = EchoRoams?.Call("API_Centre"); if (r is Vector3 && ((Vector3)r).sqrMagnitude > 1f) { centre = (Vector3)r; have = true; } } catch { }
        }

        private bool OnIsland(Vector3 pos) => have && new Vector2(pos.x - centre.x, pos.z - centre.z).sqrMagnitude <= Radius * Radius;
        private static bool Admin(BasePlayer p) => (p?.net?.connection?.authLevel ?? 0) >= 2;

        // ---------- no building ----------
        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var p = planner?.GetOwnerPlayer();
            if (p == null || Admin(p)) return null;
            // 1.1.1: fighting cover is allowed - high external walls, gates and barricades. Only real bases are blocked.
            var fn = prefab?.fullName ?? "";
            if (fn.Contains("wall.external.high") || fn.Contains("gates.external.high") || fn.Contains("barricade")) return null;
            Vector3 pos = target.entity != null ? target.entity.transform.position : target.position;
            if (!OnIsland(pos) && !OnIsland(p.transform.position)) return null;
            blocked++;
            p.ChatMessage(Tag + "No bases on <color=#ffd27f>Roam Island</color> - high walls and barricades for cover are fine.");
            return false;
        }

        // ---------- no kits ----------
        private object CanClaimEchoKit(BasePlayer p, string kitId)
        {
            if (p == null || Admin(p) || !OnIsland(p.transform.position)) return null;
            kitsBlocked++;
            return "No kits on Roam Island - leave the island to claim one.";
        }

        // ---------- no shotgun / boom crafting ----------
        private object CanCraft(ItemCrafter crafter, ItemBlueprint bp, int amount)
        {
            var p = crafter?.owner; var sn = bp?.targetItem?.shortname;
            if (p == null || sn == null || Admin(p) || !NoCraft.Contains(sn) || !OnIsland(p.transform.position)) return null;
            craftsBlocked++;
            p.ChatMessage(Tag + "You can't craft shotguns or explosives on <color=#ffd27f>Roam Island</color>.");
            return false;
        }

        // ---------- /roamst ----------
        private bool Mate(BasePlayer a, BasePlayer b)
        {
            if (a == null || b == null || a == b) return false;
            if (a.currentTeam != 0 && a.currentTeam == b.currentTeam) return true;
            try { if (EchoClans?.Call("IsMember", (ulong)a.userID, (ulong)b.userID) is bool m && m) return true; } catch { }
            return false;
        }

        private void CmdRoamsT(BasePlayer p, string command, string[] args)
        {
            if (p == null) return;
            if (!have) { p.ChatMessage(Tag + "Roam Island isn't set up on this map."); return; }
            var mates = BasePlayer.activePlayerList.Where(o => o != null && o.IsConnected && !o.IsDead() && !o.IsSleeping() && Mate(p, o) && OnIsland(o.transform.position)).ToList();
            if (mates.Count == 0) { p.ChatMessage(Tag + "None of your team or clan mates are on Roam Island right now."); return; }
            BasePlayer t = mates[0];
            if (args.Length > 0)
            {
                var q = string.Join(" ", args).ToLowerInvariant();
                t = mates.FirstOrDefault(o => o.displayName.ToLowerInvariant().Contains(q));
                if (t == null) { p.ChatMessage(Tag + "Mates on Roam Island: " + string.Join(", ", mates.Select(o => o.displayName))); return; }
            }
            if (OnIsland(p.transform.position)) { p.ChatMessage(Tag + "You're already on Roam Island."); return; }
            if (p.IsWounded() || p.isMounted) { p.ChatMessage(Tag + "Can't teleport right now."); return; }
            try { if (NoEscape?.Call("IsBlocked", p) is bool b && b) { p.ChatMessage(Tag + "Can't teleport while combat or raid blocked."); return; } } catch { }
            float last; if (!Admin(p) && lastTp.TryGetValue(p.userID, out last) && Time.realtimeSinceStartup - last < TpCooldown)
            { p.ChatMessage(Tag + $"/roamst is on cooldown for {Mathf.CeilToInt(TpCooldown - (Time.realtimeSinceStartup - last))}s."); return; }
            lastTp[p.userID] = Time.realtimeSinceStartup; tps++;
            p.Teleport(t.transform.position + Vector3.up * 0.3f);
            p.SendNetworkUpdateImmediate();
            p.ChatMessage(Tag + "Teleported to " + t.displayName + " on Roam Island.");
            t.ChatMessage(Tag + p.displayName + " teleported to you on Roam Island (/roamst).");
        }

        // ---------- staff ----------
        private List<BaseEntity> Builds() => BaseNetworkable.serverEntities.OfType<BaseEntity>().Where(e => e != null && !e.IsDestroyed && e.OwnerID != 0UL && e.OwnerID.IsSteamId() && !(e is BasePlayer) && !e.HasParent() && (e is BuildingBlock || e is DecayEntity || e is StorageContainer || e is SleepingBag) && !(e is BaseVehicle) && OnIsland(e.transform.position)).ToList();

        [ConsoleCommand("roamsnobuild.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith(have ? $"EchoRoamsNoBuild 1.1.0: Roam Island centre {centre:F0}, rules within {Radius:0} m | blocked since load: {blocked} builds, {kitsBlocked} kits, {craftsBlocked} crafts | {tps} /roamst teleports" : "EchoRoamsNoBuild: waiting for the Roam Island centre from EchoRoams");
        }

        [ConsoleCommand("roamsnobuild.list")]
        private void CList(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            var ents = Builds();
            var by = ents.GroupBy(e => e.OwnerID).Select(g => (BasePlayer.FindAwakeOrSleeping(g.Key.ToString())?.displayName ?? g.Key.ToString()) + " " + g.Count());
            a.ReplyWith($"Player-built on Roam Island: {ents.Count} | " + string.Join(", ", by));
        }

        [ConsoleCommand("roamsnobuild.clear")]
        private void CClear(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            var ents = Builds(); int n = 0;
            foreach (var e in ents.Where(e => !(e is BuildingBlock))) if (!e.IsDestroyed) { e.Kill(); n++; }
            foreach (var e in ents.Where(e => e is BuildingBlock)) if (!e.IsDestroyed) { e.Kill(); n++; }
            a.ReplyWith($"Removed {n} player-built things from Roam Island.");
        }
    }
}

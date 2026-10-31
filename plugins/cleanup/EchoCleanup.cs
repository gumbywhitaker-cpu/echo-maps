// EchoCleanup 1.0.0 (Nick, 1 Nov 2026): end-of-bounty-day clean-up.
//   When raid/bounty hours end, removes:
//     - raided / abandoned bases and fobs: player buildings with NO tool cupboard
//     - decaying bases: TC with no upkeep left
//     - 1.1.0: bases with a TC but nobody asleep in them and none of their owners online, plus loose high walls/gates
//     - loose loot: death bags, dropped loot bags, corpses and items lying on the ground
//   Never touches: a building with someone asleep in it, a building with an awake player inside or right next to it
//   (someone building), map/event buildings (no player owner), anything inside a live clan raid.
//   Removal is spread over several seconds so it doesn't lag the server. Quiet: nothing is said in chat.
// Console (admin): cleanup.dryrun - what it would remove now | cleanup.run - do it now | cleanup.status
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoCleanup", "Echo 10X", "1.1.0")]
    [Description("Removes raided/abandoned/decaying bases, fobs and loose loot when bounty hours end.")]
    public class EchoCleanup : RustPlugin
    {
        [PluginReference] private Plugin EchoTC, EchoRaidHours;
        private bool? lastBounty;
        private readonly Queue<BaseNetworkable> killQ = new Queue<BaseNetworkable>();
        private string lastReport = "not run yet";
        private Timer drain;

        private void OnServerInitialized() { timer.Every(30f, Watch); }

        private void Watch()
        {
            bool b = false;
            try { b = EchoTC?.Call("API_BountyNow") is bool x && x; } catch { return; }
            if (lastBounty == true && !b) { var r = Clean(false); Puts("Bounty hours ended - " + r); }
            lastBounty = b;
        }

        private class Plan { public int Raided, Decaying, Abandoned, Pieces, Walls, Bags, Corpses, Items, SkippedSleeper, SkippedAwake, SkippedOnline; public List<BaseNetworkable> Kill = new List<BaseNetworkable>(); }

        private static bool Staff(ulong id) => ServerUsers.Is(id, ServerUsers.UserGroup.Owner) || ServerUsers.Is(id, ServerUsers.UserGroup.Moderator);
        private static bool Online(ulong id) { var p = BasePlayer.FindByID(id); return p != null && p.IsConnected; }

        private Plan Build()
        {
            var plan = new Plan();
            var sleepers = BasePlayer.sleepingPlayerList.Where(s => s != null && !s.IsDestroyed).Select(s => s.transform.position).ToList();
            var awake = BasePlayer.activePlayerList.Where(s => s != null && !s.IsDead() && !s.IsSleeping()).Select(s => s.transform.position).ToList();
            var removedAreas = new List<KeyValuePair<Vector3, float>>(); var removedTcs = new HashSet<BuildingPrivlidge>();
            foreach (var bld in BuildingManager.server.buildingDictionary.Values.ToList())
            {
                if (bld == null || bld.decayEntities == null || bld.decayEntities.Count == 0) continue;
                var ents = bld.decayEntities.Where(e => e != null && !e.IsDestroyed).ToList();
                if (ents.Count == 0 || !ents.Any(e => e.OwnerID != 0UL && e.OwnerID.IsSteamId())) continue;   // map / event build
                if (ents.Any(e => e.OwnerID.IsSteamId() && Staff(e.OwnerID))) continue;                       // staff builds (sea fort etc.)
                bool raidLive = false; try { raidLive = EchoRaidHours?.Call("API_RaidOn", bld.ID) is bool l && l; } catch { }
                if (raidLive) continue;
                var tc = bld.GetDominatingBuildingPrivilege();
                bool noTc = tc == null || tc.IsDestroyed;
                bool decaying = !noTc && tc.GetProtectedMinutes() <= 0f;
                Vector3 c = Vector3.zero; foreach (var e in ents) c += e.transform.position; c /= ents.Count;
                float r = 0f; foreach (var e in ents) { var d = e.transform.position - c; d.y = 0f; r = Mathf.Max(r, d.magnitude); }
                if (sleepers.Any(s => Near(s, c, r + 4f))) { plan.SkippedSleeper++; continue; }
                if (awake.Any(s => Near(s, c, r + 15f))) { plan.SkippedAwake++; continue; }
                if (!noTc && !decaying)
                {
                    // 1.1.0 (Nick): a base with a TC but nobody asleep in it goes too - unless one of its owners is online
                    var who = new HashSet<ulong>(tc.authorizedPlayers); who.Add(tc.OwnerID);
                    if (who.Any(Online)) { plan.SkippedOnline++; continue; }
                    plan.Abandoned++;
                }
                else if (noTc) plan.Raided++; else plan.Decaying++;
                plan.Pieces += ents.Count;
                removedAreas.Add(new KeyValuePair<Vector3, float>(c, r + 30f)); if (!noTc) removedTcs.Add(tc);
                foreach (var e in ents.OrderBy(e => e is BuildingBlock ? 1 : 0)) plan.Kill.Add(e);   // deployables first, then the building
            }
            foreach (var n in BaseNetworkable.serverEntities.ToList())
            {
                if (n == null || n.IsDestroyed) continue;
                if (n is DroppedItemContainer) { plan.Bags++; plan.Kill.Add(n); continue; }
                if (n is PlayerCorpse) { plan.Corpses++; plan.Kill.Add(n); continue; }
                if (n is DroppedItem) { plan.Items++; plan.Kill.Add(n); continue; }
                // 1.1.0: high walls, gates and other bits outside any building, round a removed base or with no TC at all
                var de = n as DecayEntity;
                if (de == null || de.buildingID != 0 || de.OwnerID == 0UL || !de.OwnerID.IsSteamId() || Staff(de.OwnerID) || Online(de.OwnerID)) continue;
                if (!(de is SimpleBuildingBlock) && !(de is Door) && !(de is Barricade)) continue;
                var pos = de.transform.position;
                var priv = de.GetBuildingPrivilege();
                bool byRemoved = removedAreas.Any(a => Near(pos, a.Key, a.Value));
                if (priv != null && !removedTcs.Contains(priv) && !byRemoved) continue;
                if (sleepers.Any(s => Near(s, pos, 6f)) || awake.Any(s => Near(s, pos, 15f))) continue;
                plan.Walls++; plan.Kill.Add(de);
            }
            return plan;
        }
        private static bool Near(Vector3 p, Vector3 c, float r) { var d = p - c; if (Mathf.Abs(d.y) > 60f) return false; d.y = 0f; return d.magnitude <= r; }

        private string Clean(bool dry)
        {
            var plan = Build();
            string s = $"{(dry ? "WOULD remove" : "removing")} {plan.Raided} raided bases/fobs (no TC) + {plan.Decaying} decaying + {plan.Abandoned} with a TC but nobody in them ({plan.Pieces} pieces), {plan.Walls} loose walls/gates/barricades, {plan.Bags} loot/death bags, {plan.Corpses} corpses, {plan.Items} dropped items. Kept: {plan.SkippedSleeper} with a sleeper inside, {plan.SkippedAwake} with a player right there, {plan.SkippedOnline} whose owner is online.";
            if (!dry)
            {
                foreach (var e in plan.Kill) killQ.Enqueue(e);
                if (drain == null) drain = timer.Every(0.1f, Drain);
                lastReport = System.DateTime.Now.ToString("ddd HH:mm") + ": " + s;
            }
            return s;
        }
        private void Drain()
        {
            for (int i = 0; i < 150 && killQ.Count > 0; i++) { var e = killQ.Dequeue(); if (e != null && !e.IsDestroyed) e.Kill(BaseNetworkable.DestroyMode.None); }
            if (killQ.Count == 0) { drain?.Destroy(); drain = null; }
        }
        private void Unload() { drain?.Destroy(); }

        [ConsoleCommand("cleanup.dryrun")] private void CDry(ConsoleSystem.Arg a) { if (a.Connection != null && !a.IsAdmin) return; a.ReplyWith("EchoCleanup dry run: " + Clean(true)); }
        [ConsoleCommand("cleanup.run")] private void CRun(ConsoleSystem.Arg a) { if (a.Connection != null && !a.IsAdmin) return; a.ReplyWith("EchoCleanup: " + Clean(false)); }
        [ConsoleCommand("cleanup.status")] private void CStatus(ConsoleSystem.Arg a) { if (a.Connection != null && !a.IsAdmin) return; a.ReplyWith($"EchoCleanup 1.1.0: runs when bounty hours end (bounty now: {lastBounty?.ToString() ?? "?"}) | queue {killQ.Count} | last: {lastReport}"); }
    }
}

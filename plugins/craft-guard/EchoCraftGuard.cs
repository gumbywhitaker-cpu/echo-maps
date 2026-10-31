// EchoCraftGuard 1.0.0 (Nick, 31 Oct 2026): stops the instant-craft lag trick.
// On 31 Oct someone crafted ~3,000 wooden spears and dropped them in one pile -> 6,000 entities, server down to 1-7 FPS.
// Rules (players only - admins, owners and staff are never touched):
//   - One craft of a non-stacking item (weapons, tools, spears, deployables...) is capped at 50 at a time.
//   - Non-stacking items crafted in the last 60 s: warn at 150, KICK at 250 (the craft that crosses the line is refused).
//   - Items dropped on the ground in the last 30 s: warn at 50, KICK at 100. On a kick, everything they dropped in the
//     last 2 minutes is removed from the ground so the lag goes away with them.
// Every warning and kick is logged (oxide/logs/EchoCraftGuard) and told to online staff only - players never see it.
// Console: craftguard.status
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoCraftGuard", "Echo 10X", "1.0.0")]
    [Description("Kicks players who mass-craft or mass-drop items to lag the server.")]
    public class EchoCraftGuard : RustPlugin
    {
        private const int MaxPerCraft = 50;
        private const float CraftWindow = 60f; private const int CraftWarn = 150, CraftKick = 250;
        private const float DropWindow = 30f; private const int DropWarn = 50, DropKick = 100;
        private const float CleanWindow = 120f;
        private const string Tag = "<color=#39d2d1>ECHO</color> ";

        private class P
        {
            public readonly Queue<KeyValuePair<float, int>> Crafts = new Queue<KeyValuePair<float, int>>();
            public readonly Queue<float> Drops = new Queue<float>();
            public readonly List<KeyValuePair<float, BaseEntity>> Dropped = new List<KeyValuePair<float, BaseEntity>>();
            public float WarnedCraft, WarnedDrop;
        }
        private readonly Dictionary<ulong, P> who = new Dictionary<ulong, P>();
        private readonly Dictionary<ulong, KeyValuePair<ulong, float>> lastOwner = new Dictionary<ulong, KeyValuePair<ulong, float>>(); // item uid -> player, time
        private int kicks, warns, refused, cleaned;

        private P Of(ulong id) { P p; if (!who.TryGetValue(id, out p)) who[id] = p = new P(); return p; }
        private static float Now => Time.realtimeSinceStartup;
        private static bool Exempt(BasePlayer p) => p == null || p.IsNpc || !((ulong)p.userID).IsSteamId() || p.IsAdmin || p.net?.connection?.authLevel > 0;

        private void OnServerInitialized() { timer.Every(30f, Prune); }

        // ---------- crafting ----------
        private object OnItemCraft(ItemCraftTask task, BasePlayer player, Item fromTempBlueprint)
        {
            if (task?.blueprint?.targetItem == null || Exempt(player)) return null;
            var def = task.blueprint.targetItem;
            if (def.stackable > 1) return null;
            int amount = Math.Max(1, task.amount) * Math.Max(1, task.blueprint.amountToCreate);
            if (amount > MaxPerCraft)
            {
                refused++;
                player.ChatMessage(Tag + $"You can craft up to {MaxPerCraft} {def.displayName.english} at a time.");
                return false;
            }
            var p = Of(player.userID); Trim(p);
            int recent = p.Crafts.Sum(c => c.Value);
            if (recent + amount >= CraftKick) { Kick(player, $"mass crafting ({recent + amount} {def.shortname} and other non-stacking items in {CraftWindow:0} s)"); return false; }
            p.Crafts.Enqueue(new KeyValuePair<float, int>(Now, amount));
            if (recent + amount >= CraftWarn && Now - p.WarnedCraft > 30f)
            {
                p.WarnedCraft = Now; warns++;
                player.ChatMessage(Tag + "<color=#ff9a3c>Slow down</color> - crafting this many items at once lags the server. Keep going and you'll be kicked.");
                Staff($"WARN {player.displayName} ({player.UserIDString}) crafted {recent + amount} non-stacking items in {CraftWindow:0} s (kick at {CraftKick}).");
            }
            return null;
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
        {
            var p = crafter?.owner; if (item == null || Exempt(p)) return;
            lastOwner[item.uid.Value] = new KeyValuePair<ulong, float>(p.userID, Now);   // crafted straight onto the ground still counts
        }

        // ---------- dropping ----------
        private void OnItemRemovedFromContainer(ItemContainer c, Item item)
        {
            var p = c?.playerOwner; if (item == null || Exempt(p)) return;
            lastOwner[item.uid.Value] = new KeyValuePair<ulong, float>(p.userID, Now);
        }

        private void OnItemDropped(Item item, BaseEntity world)
        {
            if (item == null || world == null) return;
            KeyValuePair<ulong, float> o;
            if (!lastOwner.TryGetValue(item.uid.Value, out o) || Now - o.Value > 5f) return;
            lastOwner.Remove(item.uid.Value);
            var player = BasePlayer.FindByID(o.Key); if (Exempt(player)) return;
            var p = Of(o.Key); Trim(p);
            p.Drops.Enqueue(Now);
            p.Dropped.Add(new KeyValuePair<float, BaseEntity>(Now, world));
            int n = p.Drops.Count;
            if (n >= DropKick) { Kick(player, $"mass dropping ({n} items on the ground in {DropWindow:0} s)"); return; }
            if (n >= DropWarn && Now - p.WarnedDrop > 30f)
            {
                p.WarnedDrop = Now; warns++;
                player.ChatMessage(Tag + "<color=#ff9a3c>Slow down</color> - dropping this many items lags the server. Keep going and you'll be kicked.");
                Staff($"WARN {player.displayName} ({player.UserIDString}) dropped {n} items in {DropWindow:0} s (kick at {DropKick}).");
            }
        }

        // ---------- kick + clean up ----------
        private void Kick(BasePlayer player, string why)
        {
            var p = Of(player.userID);
            int n = 0;
            foreach (var d in p.Dropped) if (d.Value != null && !d.Value.IsDestroyed && Now - d.Key <= CleanWindow) { d.Value.Kill(); n++; }
            p.Dropped.Clear(); p.Drops.Clear(); p.Crafts.Clear();
            cleaned += n; kicks++;
            Staff($"KICKED {player.displayName} ({player.UserIDString}) at {MapHelper.PositionToString(player.transform.position)} for {why}. Removed {n} of their dropped items.");
            player.Kick("Kicked by ECHO: mass crafting/dropping items lags the server. Please don't.");
        }

        private void Staff(string msg)
        {
            Puts(msg);
            LogToFile("EchoCraftGuard", DateTime.UtcNow.ToString("u") + " " + msg, this);
            foreach (var a in BasePlayer.activePlayerList) if (a != null && a.IsAdmin) a.ChatMessage("<color=#ff9a3c>[CRAFT GUARD]</color> " + msg);
        }

        private static void Trim(P p)
        {
            while (p.Crafts.Count > 0 && Now - p.Crafts.Peek().Key > CraftWindow) p.Crafts.Dequeue();
            while (p.Drops.Count > 0 && Now - p.Drops.Peek() > DropWindow) p.Drops.Dequeue();
            p.Dropped.RemoveAll(d => d.Value == null || d.Value.IsDestroyed || Now - d.Key > CleanWindow);
        }

        private void Prune()
        {
            foreach (var k in lastOwner.Where(kv => Now - kv.Value.Value > 10f).Select(kv => kv.Key).ToList()) lastOwner.Remove(k);
            foreach (var k in who.Keys.ToList()) { var p = who[k]; Trim(p); if (p.Crafts.Count == 0 && p.Drops.Count == 0 && p.Dropped.Count == 0) who.Remove(k); }
        }

        [ConsoleCommand("craftguard.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoCraftGuard 1.0.0: on | max {MaxPerCraft} non-stacking per craft | crafts warn {CraftWarn}/kick {CraftKick} per {CraftWindow:0}s | drops warn {DropWarn}/kick {DropKick} per {DropWindow:0}s | since load: {warns} warnings, {kicks} kicks, {refused} big crafts refused, {cleaned} items cleaned | staff exempt");
        }
    }
}

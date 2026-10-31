// NoEscape (Echo 10X build) 1.0.0 (Nick, 31 Oct 2026): combat block + raid block for the whole server.
// Named "NoEscape" on purpose: BGrade, Remover Tool and EchoHome already ask a plugin called NoEscape whether a player
// is blocked, so they all obey it with no changes.
//   COMBAT BLOCK  20 s after you hurt a player or a player hurts you. Ends the moment you die.
//   RAID BLOCK    120 s for everyone within 60 m of explosive / fire damage to a player building (raiders and defenders).
//                 Walking into an active raid area blocks you too.
// While blocked: no teleport (EchoHome asks IsRaidBlocked; combat via IsBlocked), no BGrade, no Remover Tool.
// Raid block building rule: anything built or upgraded inside an active raid area starts at no more than 400 HP and
// can't be repaired until that raid block ends.
// A small timer shows on screen while you're blocked. Console: noescape.status
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoEscape", "Echo 10X", "1.0.0")]
    [Description("Echo combat block + raid block (BGrade, Remover Tool and EchoHome obey it).")]
    public class NoEscape : RustPlugin
    {
        private const float CombatSeconds = 20f, RaidSeconds = 120f, RaidRadius = 60f, RaidHpCap = 400f;
        private const string Ui = "NoEscape.Timer";

        private class Spot { public Vector3 Pos; public float Until; }
        private readonly List<Spot> spots = new List<Spot>();
        private readonly Dictionary<ulong, float> combatUntil = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> raidUntil = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> capped = new Dictionary<ulong, float>();   // building net id -> no repair until
        private readonly HashSet<ulong> shown = new HashSet<ulong>();
        private int raids, capCount;
        private static float Now => Time.realtimeSinceStartup;

        private void OnServerInitialized() { timer.Every(1f, Tick); }
        private void Unload() { foreach (var p in BasePlayer.activePlayerList) CuiHelper.DestroyUi(p, Ui); }

        // ---------- API (BGrade: IsBlocked, EchoHome: IsRaidBlocked) ----------
        private bool IsCombatBlocked(BasePlayer p) { float u; return p != null && combatUntil.TryGetValue(p.userID, out u) && u > Now; }
        private bool IsRaidBlocked(BasePlayer p) { float u; return p != null && raidUntil.TryGetValue(p.userID, out u) && u > Now; }
        private bool IsBlocked(BasePlayer p) => IsCombatBlocked(p) || IsRaidBlocked(p);
        private bool IsEscapeBlocked(BasePlayer p) => IsBlocked(p);
        private double API_CombatLeft(BasePlayer p) { float u; return p != null && combatUntil.TryGetValue(p.userID, out u) ? Math.Max(0, u - Now) : 0; }
        private double API_RaidLeft(BasePlayer p) { float u; return p != null && raidUntil.TryGetValue(p.userID, out u) ? Math.Max(0, u - Now) : 0; }

        private static bool Human(BasePlayer p) => p != null && !p.IsNpc && p.userID.IsSteamId();

        // ---------- detection ----------
        private void OnEntityTakeDamage(BaseCombatEntity ent, HitInfo info)
        {
            if (ent == null || info == null) return;
            var victim = ent as BasePlayer;
            if (victim != null)
            {
                var atk = info.InitiatorPlayer;
                if (!Human(victim) || !Human(atk) || atk == victim || info.damageTypes.Total() <= 0f) return;
                combatUntil[victim.userID] = Now + CombatSeconds;
                combatUntil[atk.userID] = Now + CombatSeconds;
                return;
            }
            if (!(ent is BuildingBlock || ent is Door || ent is SimpleBuildingBlock || ent is DecayEntity)) return;
            if (ent.OwnerID == 0UL || !ent.OwnerID.IsSteamId()) return;
            bool boom = info.damageTypes.Get(Rust.DamageType.Explosion) > 0f || info.damageTypes.Get(Rust.DamageType.Heat) > 0f;
            if (!boom) return;
            var raider = info.InitiatorPlayer;
            if (raider != null && !Human(raider)) return;
            StartRaid(ent.transform.position);
            if (raider != null) raidUntil[raider.userID] = Now + RaidSeconds;
        }

        private void StartRaid(Vector3 pos)
        {
            foreach (var s in spots) if ((s.Pos - pos).sqrMagnitude < 30f * 30f) { s.Until = Now + RaidSeconds; return; }
            spots.Add(new Spot { Pos = pos, Until = Now + RaidSeconds }); raids++;
        }

        private Spot SpotAt(Vector3 pos)
        {
            foreach (var s in spots) if (s.Until > Now && new Vector2(pos.x - s.Pos.x, pos.z - s.Pos.z).sqrMagnitude <= RaidRadius * RaidRadius) return s;
            return null;
        }

        private void OnPlayerDeath(BasePlayer p, HitInfo info) { if (p != null) { combatUntil.Remove(p.userID); Hide(p); } }   // combat block ends at death

        // ---------- raid-block building rule ----------
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var e = go?.ToBaseEntity() as BaseCombatEntity; if (e == null) return;
            var s = SpotAt(e.transform.position); if (s == null) return;
            NextTick(() => Cap(e, s));
        }

        private void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (block == null) return;
            var s = SpotAt(block.transform.position); if (s == null) return;
            NextTick(() => Cap(block, s));
        }

        private void Cap(BaseCombatEntity e, Spot s)
        {
            if (e == null || e.IsDestroyed || e.net == null) return;
            if (e.health > RaidHpCap) { e.health = RaidHpCap; e.SendNetworkUpdate(); }
            capped[e.net.ID.Value] = s.Until; capCount++;
        }

        private object OnStructureRepair(BaseCombatEntity e, BasePlayer p)
        {
            if (e?.net == null) return null;
            float u; if (!capped.TryGetValue(e.net.ID.Value, out u)) return null;
            if (u <= Now) { capped.Remove(e.net.ID.Value); return null; }
            p?.ChatMessage($"<color=#ff9a3c>RAID BLOCK</color> Built during a raid - it can't be repaired for {Mathf.CeilToInt(u - Now)}s.");
            return false;
        }

        // ---------- tick: area blocking + on-screen timer ----------
        private void Tick()
        {
            spots.RemoveAll(s => s.Until <= Now);
            if (capped.Count > 0) foreach (var k in capped.Where(kv => kv.Value <= Now).Select(kv => kv.Key).ToList()) capped.Remove(k);
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (!Human(p) || p.IsDead()) { if (p != null) Hide(p); continue; }
                var s = SpotAt(p.transform.position);
                if (s != null) { float u; if (!raidUntil.TryGetValue(p.userID, out u) || u < s.Until) raidUntil[p.userID] = s.Until; }
                float r = (float)API_RaidLeft(p), c = (float)API_CombatLeft(p);
                if (r <= 0f && c <= 0f) { Hide(p); continue; }
                Show(p, r > 0f ? $"RAID BLOCKED  {Fmt(r)}" : $"COMBAT BLOCKED  {Fmt(c)}", r > 0f);
            }
        }

        private static string Fmt(float s) { int t = Mathf.CeilToInt(s); return t >= 60 ? $"{t / 60}:{t % 60:00}" : t + "s"; }

        private void Show(BasePlayer p, string text, bool raid)
        {
            CuiHelper.DestroyUi(p, Ui);
            var c = new CuiElementContainer();
            c.Add(new CuiPanel { Image = { Color = raid ? "0.55 0.08 0.08 0.75" : "0.6 0.35 0.05 0.75" }, RectTransform = { AnchorMin = "0.435 0.115", AnchorMax = "0.565 0.142" } }, "Hud", Ui);
            c.Add(new CuiLabel { Text = { Text = text, FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, Ui);
            CuiHelper.AddUi(p, c); shown.Add(p.userID);
        }

        private void Hide(BasePlayer p) { if (p != null && shown.Remove(p.userID)) CuiHelper.DestroyUi(p, Ui); }

        [ConsoleCommand("noescape.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            int cb = combatUntil.Count(kv => kv.Value > Now), rb = raidUntil.Count(kv => kv.Value > Now);
            a.ReplyWith($"NoEscape (Echo) 1.0.0: combat {CombatSeconds:0}s (ends on death), raid {RaidSeconds:0}s / {RaidRadius:0}m, raid builds capped {RaidHpCap:0} HP no repair | now: {cb} combat blocked, {rb} raid blocked, {spots.Count} raid areas | since load: {raids} raids, {capCount} pieces capped");
        }
    }
}

// EchoEssentials 1.0.0 (Nick, 1 Nov 2026): 14 small Echo plugins folded into one. Each one's code is unchanged and runs as
//   its own module below; data files, chat and console commands are exactly the same as before:
//   EchoAutoDoor, EchoSamSafe, EchoUnstuck, EchoNoDeadLogs, EchoSignRefresh, EchoPop, EchoBagTimers, EchoRocketDamage, EchoNoGod, EchoRadioLock, EchoCraftGuard, EchoCleanup, EchoAutoAuth, EchoRoamsNoBuild
using Oxide.Core.Plugins;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoEssentials", "Echo 10X", "1.0.0")]
    [Description("Echo small-features pack: auto doors, /stuck, /pop, SAM safe, dead logs, sign refresh, bag timers, rocket damage, no god, radio lock, craft guard, clean-up, auto auth, roams rules.")]
    public class EchoEssentials : RustPlugin
    {
    private readonly M_EchoAutoDoor m_EchoAutoDoor = new M_EchoAutoDoor();
    private readonly M_EchoSamSafe m_EchoSamSafe = new M_EchoSamSafe();
    private readonly M_EchoUnstuck m_EchoUnstuck = new M_EchoUnstuck();
    private readonly M_EchoNoDeadLogs m_EchoNoDeadLogs = new M_EchoNoDeadLogs();
    private readonly M_EchoSignRefresh m_EchoSignRefresh = new M_EchoSignRefresh();
    private readonly M_EchoPop m_EchoPop = new M_EchoPop();
    private readonly M_EchoBagTimers m_EchoBagTimers = new M_EchoBagTimers();
    private readonly M_EchoRocketDamage m_EchoRocketDamage = new M_EchoRocketDamage();
    private readonly M_EchoNoGod m_EchoNoGod = new M_EchoNoGod();
    private readonly M_EchoRadioLock m_EchoRadioLock = new M_EchoRadioLock();
    private readonly M_EchoCraftGuard m_EchoCraftGuard = new M_EchoCraftGuard();
    private readonly M_EchoCleanup m_EchoCleanup = new M_EchoCleanup();
    private readonly M_EchoAutoAuth m_EchoAutoAuth = new M_EchoAutoAuth();
    private readonly M_EchoRoamsNoBuild m_EchoRoamsNoBuild = new M_EchoRoamsNoBuild();

    public EchoEssentials()
    {
        m_EchoAutoDoor.EP = this;
        m_EchoSamSafe.EP = this;
        m_EchoUnstuck.EP = this;
        m_EchoNoDeadLogs.EP = this;
        m_EchoSignRefresh.EP = this;
        m_EchoPop.EP = this;
        m_EchoBagTimers.EP = this;
        m_EchoRocketDamage.EP = this;
        m_EchoNoGod.EP = this;
        m_EchoRadioLock.EP = this;
        m_EchoCraftGuard.EP = this;
        m_EchoCleanup.EP = this;
        m_EchoAutoAuth.EP = this;
        m_EchoRoamsNoBuild.EP = this;
    }

    // ---- shared plumbing for the modules ----
    internal Plugin Find(string n) => plugins.Find(n);
    internal void AddChat(string c, Action<BasePlayer, string, string[]> cb) => cmd.AddChatCommand(c, this, cb);
    internal PluginTimers Timers => timer;
    internal Oxide.Core.Libraries.Permission Perm => permission;
    internal void Log(string mod, string m) => Puts("[" + mod + "] " + m);
    internal void Warn(string mod, string m) => PrintWarning("[" + mod + "] " + m);
    internal void Err(string mod, string m) => PrintError("[" + mod + "] " + m);
    internal void NT(Action a) => NextTick(a);
    internal void LogFile(string f, string m, Plugin p) => LogToFile(f, m, p);
    private void Safe(string mod, string hook, Action a) { try { a(); } catch (Exception e) { PrintError("[" + mod + "] " + hook + ": " + e); } }
    private object SafeO(string mod, string hook, Func<object> f) { try { return f(); } catch (Exception e) { PrintError("[" + mod + "] " + hook + ": " + e); return null; } }

    internal abstract class EchoMod
    {
        internal EchoEssentials EP;
        internal abstract string ModName { get; }
        internal PluginTimers timer => EP.Timers;
        internal Oxide.Core.Libraries.Permission permission => EP.Perm;
        internal string Name => ModName;
        internal void Puts(string m) => EP.Log(ModName, m);
        internal void Puts(string f, params object[] a) => EP.Log(ModName, string.Format(f, a));
        internal void PrintWarning(string m) => EP.Warn(ModName, m);
        internal void PrintError(string m) => EP.Err(ModName, m);
        internal void NextTick(Action a) => EP.NT(a);
        internal void LogToFile(string f, string m, Plugin p) => EP.LogFile(f, m, p);
    }

    // ---- hooks: each one is handed to every module that uses it ----
    private void Init()
    {
        Safe("EchoAutoDoor", "Init", () => m_EchoAutoDoor.Init());
        Safe("EchoAutoAuth", "Init", () => m_EchoAutoAuth.Init());
    }
    private void OnServerInitialized(bool initial)
    {
        Safe("EchoAutoDoor", "OnServerInitialized", () => m_EchoAutoDoor.OnServerInitialized());
        Safe("EchoUnstuck", "OnServerInitialized", () => m_EchoUnstuck.OnServerInitialized());
        Safe("EchoNoDeadLogs", "OnServerInitialized", () => m_EchoNoDeadLogs.OnServerInitialized());
        Safe("EchoSignRefresh", "OnServerInitialized", () => m_EchoSignRefresh.OnServerInitialized(initial));
        Safe("EchoPop", "OnServerInitialized", () => m_EchoPop.OnServerInitialized());
        Safe("EchoBagTimers", "OnServerInitialized", () => m_EchoBagTimers.OnServerInitialized());
        Safe("EchoNoGod", "OnServerInitialized", () => m_EchoNoGod.OnServerInitialized());
        Safe("EchoRadioLock", "OnServerInitialized", () => m_EchoRadioLock.OnServerInitialized());
        Safe("EchoCraftGuard", "OnServerInitialized", () => m_EchoCraftGuard.OnServerInitialized());
        Safe("EchoCleanup", "OnServerInitialized", () => m_EchoCleanup.OnServerInitialized());
        Safe("EchoAutoAuth", "OnServerInitialized", () => m_EchoAutoAuth.OnServerInitialized());
        Safe("EchoRoamsNoBuild", "OnServerInitialized", () => m_EchoRoamsNoBuild.OnServerInitialized());
    }
    private void Unload()
    {
        Safe("EchoAutoDoor", "Unload", () => m_EchoAutoDoor.Unload());
        Safe("EchoNoDeadLogs", "Unload", () => m_EchoNoDeadLogs.Unload());
        Safe("EchoCleanup", "Unload", () => m_EchoCleanup.Unload());
        Safe("EchoAutoAuth", "Unload", () => m_EchoAutoAuth.Unload());
    }
    private void OnDoorOpened(Door door, BasePlayer player)
    {
        Safe("EchoAutoDoor", "OnDoorOpened", () => m_EchoAutoDoor.OnDoorOpened(door, player));
    }
    private void OnDoorClosed(Door door, BasePlayer player)
    {
        Safe("EchoAutoDoor", "OnDoorClosed", () => m_EchoAutoDoor.OnDoorClosed(door, player));
    }
    private object OnSamSiteTarget(SamSite sam, object target)
    {
        { var r_EchoSamSafe = SafeO("EchoSamSafe", "OnSamSiteTarget", () => m_EchoSamSafe.OnSamSiteTarget(sam, target)); if (r_EchoSamSafe != null) return r_EchoSamSafe; }
        return null;
    }
    private void OnEntitySpawned(BaseNetworkable n)
    {
        Safe("EchoNoDeadLogs", "OnEntitySpawned", () => m_EchoNoDeadLogs.OnEntitySpawned(n));
        if (n is SleepingBag) { m_EchoBagTimers.OnEntitySpawned((n as SleepingBag)); }
        Safe("EchoRadioLock", "OnEntitySpawned", () => m_EchoRadioLock.OnEntitySpawned(n));
        Safe("EchoAutoAuth", "OnEntitySpawned", () => m_EchoAutoAuth.OnEntitySpawned(n));
    }
    private void OnPlayerChat(BasePlayer p, string message, ConVar.Chat.ChatChannel channel)
    {
        Safe("EchoPop", "OnPlayerChat", () => m_EchoPop.OnPlayerChat(p, message, channel));
    }
    private void OnEntityTakeDamage(BasePlayer victim, HitInfo info)
    {
        Safe("EchoRocketDamage", "OnEntityTakeDamage", () => m_EchoRocketDamage.OnEntityTakeDamage(victim, info));
    }
    private object OnItemCraft(ItemCraftTask task, BasePlayer player, Item fromTempBlueprint)
    {
        { var r_EchoCraftGuard = SafeO("EchoCraftGuard", "OnItemCraft", () => m_EchoCraftGuard.OnItemCraft(task, player, fromTempBlueprint)); if (r_EchoCraftGuard != null) return r_EchoCraftGuard; }
        return null;
    }
    private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
    {
        Safe("EchoCraftGuard", "OnItemCraftFinished", () => m_EchoCraftGuard.OnItemCraftFinished(task, item, crafter));
    }
    private void OnItemRemovedFromContainer(ItemContainer c, Item item)
    {
        Safe("EchoCraftGuard", "OnItemRemovedFromContainer", () => m_EchoCraftGuard.OnItemRemovedFromContainer(c, item));
    }
    private void OnItemDropped(Item item, BaseEntity world)
    {
        Safe("EchoCraftGuard", "OnItemDropped", () => m_EchoCraftGuard.OnItemDropped(item, world));
    }
    private void OnNewSave(string f)
    {
        Safe("EchoAutoAuth", "OnNewSave", () => m_EchoAutoAuth.OnNewSave(f));
    }
    private void OnEntityKill(BaseNetworkable n)
    {
        Safe("EchoAutoAuth", "OnEntityKill", () => m_EchoAutoAuth.OnEntityKill(n));
    }
    private void OnClanMemberJoined(string tag, string id, string name)
    {
        Safe("EchoAutoAuth", "OnClanMemberJoined", () => m_EchoAutoAuth.OnClanMemberJoined(tag, id, name));
    }
    private void OnClanMemberLeft(string tag, string id, string why)
    {
        Safe("EchoAutoAuth", "OnClanMemberLeft", () => m_EchoAutoAuth.OnClanMemberLeft(tag, id, why));
    }
    private void OnClanDisbanded(string tag)
    {
        Safe("EchoAutoAuth", "OnClanDisbanded", () => m_EchoAutoAuth.OnClanDisbanded(tag));
    }
    private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
    {
        Safe("EchoAutoAuth", "OnTeamAcceptInvite", () => m_EchoAutoAuth.OnTeamAcceptInvite(team, player));
    }
    private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
    {
        Safe("EchoAutoAuth", "OnTeamLeave", () => m_EchoAutoAuth.OnTeamLeave(team, player));
    }
    private void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong target)
    {
        Safe("EchoAutoAuth", "OnTeamKick", () => m_EchoAutoAuth.OnTeamKick(team, player, target));
    }
    private void OnTeamDisbanded(RelationshipManager.PlayerTeam team)
    {
        Safe("EchoAutoAuth", "OnTeamDisbanded", () => m_EchoAutoAuth.OnTeamDisbanded(team));
    }
    private void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team)
    {
        Safe("EchoAutoAuth", "OnTeamCreated", () => m_EchoAutoAuth.OnTeamCreated(player, team));
    }
    private void OnEntityBuilt(Planner plan, UnityEngine.GameObject go)
    {
        Safe("EchoAutoAuth", "OnEntityBuilt", () => m_EchoAutoAuth.OnEntityBuilt(plan, go));
    }
    private void OnPluginLoaded(Plugin p)
    {
        Safe("EchoRoamsNoBuild", "OnPluginLoaded", () => m_EchoRoamsNoBuild.OnPluginLoaded(p));
    }
    private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
    {
        { var r_EchoRoamsNoBuild = SafeO("EchoRoamsNoBuild", "CanBuild", () => m_EchoRoamsNoBuild.CanBuild(planner, prefab, target)); if (r_EchoRoamsNoBuild != null) return r_EchoRoamsNoBuild; }
        return null;
    }
    private object CanClaimEchoKit(BasePlayer p, string kitId)
    {
        { var r_EchoRoamsNoBuild = SafeO("EchoRoamsNoBuild", "CanClaimEchoKit", () => m_EchoRoamsNoBuild.CanClaimEchoKit(p, kitId)); if (r_EchoRoamsNoBuild != null) return r_EchoRoamsNoBuild; }
        return null;
    }
    private object CanCraft(ItemCrafter crafter, ItemBlueprint bp, int amount)
    {
        { var r_EchoRoamsNoBuild = SafeO("EchoRoamsNoBuild", "CanCraft", () => m_EchoRoamsNoBuild.CanCraft(crafter, bp, amount)); if (r_EchoRoamsNoBuild != null) return r_EchoRoamsNoBuild; }
        return null;
    }

    // ---- console commands (same names as before) ----
    [ConsoleCommand("autodoor.status")] private void cc_EchoAutoDoor_CStatus(ConsoleSystem.Arg a) => m_EchoAutoDoor.CStatus(a);
    [ConsoleCommand("samsafe.status")] private void cc_EchoSamSafe_CStatus(ConsoleSystem.Arg a) => m_EchoSamSafe.CStatus(a);
    [ConsoleCommand("unstuck.status")] private void cc_EchoUnstuck_CStatus(ConsoleSystem.Arg a) => m_EchoUnstuck.CStatus(a);
    [ConsoleCommand("nodeadlogs.status")] private void cc_EchoNoDeadLogs_CStatus(ConsoleSystem.Arg a) => m_EchoNoDeadLogs.CStatus(a);
    [ConsoleCommand("bagtimers.status")] private void cc_EchoBagTimers_CStatus(ConsoleSystem.Arg a) => m_EchoBagTimers.CStatus(a);
    [ConsoleCommand("rocketdmg.status")] private void cc_EchoRocketDamage_CStatus(ConsoleSystem.Arg a) => m_EchoRocketDamage.CStatus(a);
    [ConsoleCommand("nogod.status")] private void cc_EchoNoGod_CStatus(ConsoleSystem.Arg a) => m_EchoNoGod.CStatus(a);
    [ConsoleCommand("radiolock.status")] private void cc_EchoRadioLock_CStatus(ConsoleSystem.Arg a) => m_EchoRadioLock.CStatus(a);
    [ConsoleCommand("craftguard.status")] private void cc_EchoCraftGuard_CStatus(ConsoleSystem.Arg a) => m_EchoCraftGuard.CStatus(a);
    [ConsoleCommand("cleanup.dryrun")] private void cc_EchoCleanup_CDry(ConsoleSystem.Arg a) => m_EchoCleanup.CDry(a);
    [ConsoleCommand("cleanup.run")] private void cc_EchoCleanup_CRun(ConsoleSystem.Arg a) => m_EchoCleanup.CRun(a);
    [ConsoleCommand("cleanup.status")] private void cc_EchoCleanup_CStatus(ConsoleSystem.Arg a) => m_EchoCleanup.CStatus(a);
    [ConsoleCommand("autoauth.status")] private void cc_EchoAutoAuth_CStatus(ConsoleSystem.Arg a) => m_EchoAutoAuth.CStatus(a);
    [ConsoleCommand("roamsnobuild.status")] private void cc_EchoRoamsNoBuild_CStatus(ConsoleSystem.Arg a) => m_EchoRoamsNoBuild.CStatus(a);
    [ConsoleCommand("roamsnobuild.list")] private void cc_EchoRoamsNoBuild_CList(ConsoleSystem.Arg a) => m_EchoRoamsNoBuild.CList(a);
    [ConsoleCommand("roamsnobuild.clear")] private void cc_EchoRoamsNoBuild_CClear(ConsoleSystem.Arg a) => m_EchoRoamsNoBuild.CClear(a);

    // ===================== EchoAutoDoor =====================
    internal class M_EchoAutoDoor : EchoMod
    {
        internal override string ModName => "EchoAutoDoor";

        internal const float DefaultDelay = 5f;
        internal const string Tag = "<color=#39d2d1>ECHO</color> ";
        internal class Pref { public bool Off; public float Delay = DefaultDelay; }
        internal Dictionary<ulong, Pref> prefs;
        internal readonly Dictionary<ulong, Timer> pending = new Dictionary<ulong, Timer>();
        internal int closed; internal bool dirty;

        internal void Init() { prefs = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Pref>>("EchoAutoDoor") ?? new Dictionary<ulong, Pref>(); }
        internal void OnServerInitialized() { EP.AddChat("ad", CmdAd); EP.AddChat("autodoor", CmdAd); timer.Every(120f, Save); }
        internal void Unload() { foreach (var t in pending.Values) t?.Destroy(); pending.Clear(); Save(); }
        internal void Save() { if (!dirty) return; dirty = false; Interface.Oxide.DataFileSystem.WriteObject("EchoAutoDoor", prefs); }

        internal Pref P(ulong id) { Pref p; if (!prefs.TryGetValue(id, out p)) { p = new Pref(); prefs[id] = p; } return p; }

        internal void OnDoorOpened(Door door, BasePlayer player)
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

        internal void OnDoorClosed(Door door, BasePlayer player)
        {
            if (door?.net == null) return; Timer t;
            if (pending.TryGetValue(door.net.ID.Value, out t)) { t?.Destroy(); pending.Remove(door.net.ID.Value); }
        }

        internal void CmdAd(BasePlayer p, string command, string[] args)
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

        internal void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            int off = 0; foreach (var v in prefs.Values) if (v.Off) off++;
            a.ReplyWith($"EchoAutoDoor 1.0.0: on (default {DefaultDelay:0} s) | {pending.Count} doors waiting to close | {closed} closed since load | {off} players turned it off");
        }
    
    }
    // ===================== EchoSamSafe =====================
    internal class M_EchoSamSafe : EchoMod
    {
        internal override string ModName => "EchoSamSafe";

        internal int ignored;

        internal object OnSamSiteTarget(SamSite sam, object target)   // object: works with both the old and new hook signature
        {
            if (sam == null || target == null || sam.OwnerID != 0UL) return null;
            if (target is Minicopter || target is ScrapTransportHelicopter) { ignored++; return false; }
            return null;
        }

        internal void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoSamSafe 1.0.0: monument SAM sites ignore minicopters and scrap helis | {ignored} target checks skipped since load");
        }
    
    }
    // ===================== EchoUnstuck =====================
    internal class M_EchoUnstuck : EchoMod
    {
        internal override string ModName => "EchoUnstuck";

        internal Plugin NoEscape => EP.Find("NoEscape");
        internal const string Tag = "<color=#39d2d1>ECHO</color> ";
        internal readonly Dictionary<ulong, float> last = new Dictionary<ulong, float>();
        internal int used;
        internal static readonly int Block = Rust.Layers.Mask.Default | Rust.Layers.Mask.World | Rust.Layers.Mask.Construction | Rust.Layers.Mask.Deployed | Rust.Layers.Mask.Tree | Rust.Layers.Mask.Terrain;

        internal void OnServerInitialized() { EP.AddChat("stuck", CmdStuck); EP.AddChat("unstuck", CmdStuck); }

        internal void CmdStuck(BasePlayer p, string command, string[] args)
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

        internal bool Find(BasePlayer p, Vector3 o, out Vector3 spot)
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

        internal void CStatus(ConsoleSystem.Arg a) { if (a.Connection != null && !a.IsAdmin) return; a.ReplyWith($"EchoUnstuck 1.0.0: /stuck used {used} times since load"); }
    
    }
    // ===================== EchoNoDeadLogs =====================
    internal class M_EchoNoDeadLogs : EchoMod
    {
        internal override string ModName => "EchoNoDeadLogs";

        internal int removed;
        internal readonly System.Collections.Generic.Queue<BaseEntity> q = new System.Collections.Generic.Queue<BaseEntity>();
        internal Timer drain;

        internal static bool IsLog(BaseNetworkable n) => n != null && n.PrefabName != null && n.PrefabName.Contains("/autospawn/resource/logs_");

        internal void OnServerInitialized()
        {
            foreach (var n in BaseNetworkable.serverEntities) if (IsLog(n)) q.Enqueue((BaseEntity)n);
            drain = timer.Every(0.2f, () =>
            {
                for (int i = 0; i < 100 && q.Count > 0; i++) { var e = q.Dequeue(); if (e != null && !e.IsDestroyed) { e.Kill(); removed++; } }
                if (q.Count == 0) { drain?.Destroy(); drain = null; }
            });
        }

        internal void OnEntitySpawned(BaseNetworkable n)
        {
            if (!IsLog(n)) return;
            NextTick(() => { if (n != null && !n.IsDestroyed) { n.Kill(); removed++; } });
        }

        internal void Unload() { drain?.Destroy(); }

        internal void CStatus(ConsoleSystem.Arg a) { if (a.Connection != null && !a.IsAdmin) return; a.ReplyWith($"EchoNoDeadLogs 1.0.0: {removed} dead logs removed since load, {q.Count} still queued"); }
    
    }
    // ===================== EchoSignRefresh =====================
    internal class M_EchoSignRefresh : EchoMod
    {
        internal override string ModName => "EchoSignRefresh";

        internal void OnServerInitialized(bool initial)
        {
            if (!initial) return;                         // only after a real server start, not a plugin reload
            timer.Once(120f, () =>
            {
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "echobrand.respawn");
                Puts("Re-placed the outpost and casino signs after server start.");
            });
        }
    
    }
    // ===================== EchoPop =====================
    internal class M_EchoPop : EchoMod
    {
        internal override string ModName => "EchoPop";

        internal float last;

        internal void OnServerInitialized() { EP.AddChat("pop", CmdPop); EP.AddChat("online", CmdPop); }

        internal string Text()
        {
            int on = BasePlayer.activePlayerList.Count;
            int joining = ServerMgr.Instance?.connectionQueue?.Joining ?? 0;
            int queued = ServerMgr.Instance?.connectionQueue?.Queued ?? 0;
            int sleep = BasePlayer.sleepingPlayerList.Count;
            return $"<color=#39d2d1>ECHO 10X</color> <color=#ffd27f>{on}</color>/{ConVar.Server.maxplayers} online" + (joining > 0 ? $", {joining} joining" : "") + (queued > 0 ? $", {queued} in queue" : "") + $", {sleep} sleeping";
        }

        internal void CmdPop(BasePlayer p, string command, string[] args) => p?.ChatMessage(Text());

        internal void OnPlayerChat(BasePlayer p, string message, ConVar.Chat.ChatChannel channel)
        {
            if (p == null || string.IsNullOrEmpty(message)) return;
            var m = message.Trim().ToLowerInvariant();
            if (m != "!pop" && m != "!online") return;
            NextTick(() => p.ChatMessage(Text()));
        }
    
    }
    // ===================== EchoBagTimers =====================
    internal class M_EchoBagTimers : EchoMod
    {
        internal override string ModName => "EchoBagTimers";

        internal const float BagSeconds = 90f, BedSeconds = 60f;
        internal int set;

        internal void OnServerInitialized() { foreach (var b in BaseNetworkable.serverEntities.OfType<SleepingBag>().ToList()) Apply(b); }
        internal void OnEntitySpawned(SleepingBag b) => NextTick(() => Apply(b));

        internal void Apply(SleepingBag b)
        {
            if (b == null || b.IsDestroyed) return;
            float want = b.ShortPrefabName.Contains("bed") ? BedSeconds : BagSeconds;
            if (b.secondsBetweenReuses == want) return;
            // shorten a cooldown that is already running so nobody waits the old time
            float left = b.unlockTime - UnityEngine.Time.realtimeSinceStartup;
            if (left > want) b.unlockTime = UnityEngine.Time.realtimeSinceStartup + want;
            b.secondsBetweenReuses = want; set++;
            b.SendNetworkUpdate();
        }

        internal void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoBagTimers 1.0.0: bags {BagSeconds:0}s, beds {BedSeconds:0}s | {set} bags set since load");
        }
    
    }
    // ===================== EchoRocketDamage =====================
    internal class M_EchoRocketDamage : EchoMod
    {
        internal override string ModName => "EchoRocketDamage";

        internal const float MaxToPlayer = 60f;
        internal int scaled;

        internal static bool IsRocket(HitInfo info)
        {
            var w = info?.WeaponPrefab; if (w == null) return false;
            var n = w.ShortPrefabName ?? "";
            return n.StartsWith("rocket_") && !n.Contains("mlrs");
        }

        internal void OnEntityTakeDamage(BasePlayer victim, HitInfo info)
        {
            if (victim == null || info == null || victim.IsNpc || !IsRocket(info)) return;
            float total = info.damageTypes.Total();
            if (total <= MaxToPlayer) return;
            info.damageTypes.ScaleAll(MaxToPlayer / total);
            scaled++;
        }

        internal void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoRocketDamage 1.0.0: rockets max {MaxToPlayer:0} damage to players (bases unchanged) | {scaled} hits scaled since load");
        }
    
    }
    // ===================== EchoNoGod =====================
    internal class M_EchoNoGod : EchoMod
    {
        internal override string ModName => "EchoNoGod";

        internal int caught; internal readonly Dictionary<ulong, int> per = new Dictionary<ulong, int>();

        internal void OnServerInitialized() { timer.Every(1f, Check); }

        internal bool Allowed(BasePlayer p) =>
            (p.net?.connection?.authLevel ?? 0) >= 2 || permission.UserHasGroup(p.UserIDString, "admin");

        internal void Check()
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
                LogToFile("EchoNoGod", DateTime.UtcNow.ToString("u") + " " + msg, EP);
                foreach (var a in BasePlayer.activePlayerList) if (a != null && a != p && Allowed(a)) a.ChatMessage("<color=#ff9a3c>[NO GOD]</color> " + msg);
            }
        }

        internal void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoNoGod 1.0.0: on | god mode allowed for owners (ownerid) and the admin group only | {caught} blocked since load");
        }
    
    }
    // ===================== EchoRadioLock =====================
    internal class M_EchoRadioLock : EchoMod
    {
        internal override string ModName => "EchoRadioLock";

        internal const string Default = "rustradio.facepunch.com";
        internal int _resets;
        internal readonly Dictionary<BaseEntity, BoomBox> _boxes = new Dictionary<BaseEntity, BoomBox>();

        internal void OnServerInitialized()
        {
            foreach (var n in BaseNetworkable.serverEntities) Track(n as BaseEntity);
            timer.Every(2f, Check);
        }

        internal void OnEntitySpawned(BaseNetworkable n) { var e = n as BaseEntity; if (e != null) NextTick(() => Track(e)); }

        internal void Track(BaseEntity e)
        {
            if (e == null || e.IsDestroyed || e.OwnerID != 0) return;
            BoomBox bb = null;
            var db = e as DeployableBoomBox; if (db != null) bb = db.BoxController;
            else { var hb = e as HeldBoomBox; if (hb != null) bb = hb.BoxController; }
            if (bb == null && e.ShortPrefabName.Contains("boombox")) bb = e.GetComponent<BoomBox>();
            if (bb != null) _boxes[e] = bb;
        }

        internal static bool In(Dictionary<string, string> d, string url) => d != null && (d.ContainsKey(url) || d.ContainsValue(url));
        internal static bool Allowed(string url) =>
            string.IsNullOrEmpty(url) || url == Default || In(BoomBox.ValidStations, url) || In(BoomBox.ServerValidStations, url);

        internal readonly List<BaseEntity> _dead = new List<BaseEntity>();
        internal void Check()
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

        internal void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoRadioLock 1.0.1: on, watching {_boxes.Count} map radio(s), {BoomBox.ValidStations?.Count ?? 0} built-in + {BoomBox.ServerValidStations?.Count ?? 0} server stations, {_resets} custom link(s) switched back since load");
        }
    
    }
    // ===================== EchoCraftGuard =====================
    internal class M_EchoCraftGuard : EchoMod
    {
        internal override string ModName => "EchoCraftGuard";

        internal const int MaxPerCraft = 50;
        internal const float CraftWindow = 60f; internal const int CraftWarn = 150, CraftKick = 250;
        internal const float DropWindow = 30f; internal const int DropWarn = 50, DropKick = 100;
        internal const float CleanWindow = 120f;
        internal const string Tag = "<color=#39d2d1>ECHO</color> ";

        internal class P
        {
            public readonly Queue<KeyValuePair<float, int>> Crafts = new Queue<KeyValuePair<float, int>>();
            public readonly Queue<float> Drops = new Queue<float>();
            public readonly List<KeyValuePair<float, BaseEntity>> Dropped = new List<KeyValuePair<float, BaseEntity>>();
            public float WarnedCraft, WarnedDrop;
        }
        internal readonly Dictionary<ulong, P> who = new Dictionary<ulong, P>();
        internal readonly Dictionary<ulong, KeyValuePair<ulong, float>> lastOwner = new Dictionary<ulong, KeyValuePair<ulong, float>>(); // item uid -> player, time
        internal int kicks, warns, refused, cleaned;

        internal P Of(ulong id) { P p; if (!who.TryGetValue(id, out p)) who[id] = p = new P(); return p; }
        internal static float Now => Time.realtimeSinceStartup;
        internal static bool Exempt(BasePlayer p) => p == null || p.IsNpc || !((ulong)p.userID).IsSteamId() || p.IsAdmin || p.net?.connection?.authLevel > 0;

        internal void OnServerInitialized() { timer.Every(30f, Prune); }

        // ---------- crafting ----------
        internal object OnItemCraft(ItemCraftTask task, BasePlayer player, Item fromTempBlueprint)
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

        internal void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
        {
            var p = crafter?.owner; if (item == null || Exempt(p)) return;
            lastOwner[item.uid.Value] = new KeyValuePair<ulong, float>(p.userID, Now);   // crafted straight onto the ground still counts
        }

        // ---------- dropping ----------
        internal void OnItemRemovedFromContainer(ItemContainer c, Item item)
        {
            var p = c?.playerOwner; if (item == null || Exempt(p)) return;
            lastOwner[item.uid.Value] = new KeyValuePair<ulong, float>(p.userID, Now);
        }

        internal void OnItemDropped(Item item, BaseEntity world)
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
        internal void Kick(BasePlayer player, string why)
        {
            var p = Of(player.userID);
            int n = 0;
            foreach (var d in p.Dropped) if (d.Value != null && !d.Value.IsDestroyed && Now - d.Key <= CleanWindow) { d.Value.Kill(); n++; }
            p.Dropped.Clear(); p.Drops.Clear(); p.Crafts.Clear();
            cleaned += n; kicks++;
            Staff($"KICKED {player.displayName} ({player.UserIDString}) at {MapHelper.PositionToString(player.transform.position)} for {why}. Removed {n} of their dropped items.");
            player.Kick("Kicked by ECHO: mass crafting/dropping items lags the server. Please don't.");
        }

        internal void Staff(string msg)
        {
            Puts(msg);
            LogToFile("EchoCraftGuard", DateTime.UtcNow.ToString("u") + " " + msg, EP);
            foreach (var a in BasePlayer.activePlayerList) if (a != null && a.IsAdmin) a.ChatMessage("<color=#ff9a3c>[CRAFT GUARD]</color> " + msg);
        }

        internal static void Trim(P p)
        {
            while (p.Crafts.Count > 0 && Now - p.Crafts.Peek().Key > CraftWindow) p.Crafts.Dequeue();
            while (p.Drops.Count > 0 && Now - p.Drops.Peek() > DropWindow) p.Drops.Dequeue();
            p.Dropped.RemoveAll(d => d.Value == null || d.Value.IsDestroyed || Now - d.Key > CleanWindow);
        }

        internal void Prune()
        {
            foreach (var k in lastOwner.Where(kv => Now - kv.Value.Value > 10f).Select(kv => kv.Key).ToList()) lastOwner.Remove(k);
            foreach (var k in who.Keys.ToList()) { var p = who[k]; Trim(p); if (p.Crafts.Count == 0 && p.Drops.Count == 0 && p.Dropped.Count == 0) who.Remove(k); }
        }

        internal void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoCraftGuard 1.0.0: on | max {MaxPerCraft} non-stacking per craft | crafts warn {CraftWarn}/kick {CraftKick} per {CraftWindow:0}s | drops warn {DropWarn}/kick {DropKick} per {DropWindow:0}s | since load: {warns} warnings, {kicks} kicks, {refused} big crafts refused, {cleaned} items cleaned | staff exempt");
        }
    
    }
    // ===================== EchoCleanup =====================
    internal class M_EchoCleanup : EchoMod
    {
        internal override string ModName => "EchoCleanup";

        internal Plugin EchoTC => EP.Find("EchoTC"); internal Plugin EchoRaidHours => EP.Find("EchoRaidHours");
        internal bool? lastBounty;
        internal readonly Queue<BaseNetworkable> killQ = new Queue<BaseNetworkable>();
        internal string lastReport = "not run yet";
        internal Timer drain;

        internal void OnServerInitialized() { timer.Every(30f, Watch); }

        internal void Watch()
        {
            bool b = false;
            try { b = EchoTC?.Call("API_BountyNow") is bool x && x; } catch { return; }
            if (lastBounty == true && !b) { var r = Clean(false); Puts("Bounty hours ended - " + r); }
            lastBounty = b;
        }

        internal class Plan { public int Raided, Decaying, Abandoned, Pieces, Walls, Bags, Corpses, Items, SkippedSleeper, SkippedAwake, SkippedOnline; public List<BaseNetworkable> Kill = new List<BaseNetworkable>(); }

        internal static bool Staff(ulong id) => ServerUsers.Is(id, ServerUsers.UserGroup.Owner) || ServerUsers.Is(id, ServerUsers.UserGroup.Moderator);
        internal static bool Online(ulong id) { var p = BasePlayer.FindByID(id); return p != null && p.IsConnected; }

        internal Plan Build()
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
        internal static bool Near(Vector3 p, Vector3 c, float r) { var d = p - c; if (Mathf.Abs(d.y) > 60f) return false; d.y = 0f; return d.magnitude <= r; }

        internal string Clean(bool dry)
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
        internal void Drain()
        {
            for (int i = 0; i < 150 && killQ.Count > 0; i++) { var e = killQ.Dequeue(); if (e != null && !e.IsDestroyed) e.Kill(BaseNetworkable.DestroyMode.None); }
            if (killQ.Count == 0) { drain?.Destroy(); drain = null; }
        }
        internal void Unload() { drain?.Destroy(); }

        internal void CDry(ConsoleSystem.Arg a) { if (a.Connection != null && !a.IsAdmin) return; a.ReplyWith("EchoCleanup dry run: " + Clean(true)); }
        internal void CRun(ConsoleSystem.Arg a) { if (a.Connection != null && !a.IsAdmin) return; a.ReplyWith("EchoCleanup: " + Clean(false)); }
        internal void CStatus(ConsoleSystem.Arg a) { if (a.Connection != null && !a.IsAdmin) return; a.ReplyWith($"EchoCleanup 1.1.0: runs when bounty hours end (bounty now: {lastBounty?.ToString() ?? "?"}) | queue {killQ.Count} | last: {lastReport}"); }
    
    }
    // ===================== EchoAutoAuth =====================
    internal class M_EchoAutoAuth : EchoMod
    {
        internal override string ModName => "EchoAutoAuth";

        internal Plugin EchoClans => EP.Find("EchoClans");
        internal class Store { public Dictionary<ulong, HashSet<ulong>> Added = new Dictionary<ulong, HashSet<ulong>>(); public Dictionary<ulong, string> Codes = new Dictionary<ulong, string>(); } // entity net id -> players we added
        internal Store data;
        internal readonly HashSet<BaseEntity> ents = new HashSet<BaseEntity>();
        internal int added, removed, codes; internal bool dirty, queued;

        internal void Init() { data = Interface.Oxide.DataFileSystem.ReadObject<Store>("EchoAutoAuth") ?? new Store(); }
        internal void OnServerInitialized()
        {
            foreach (var n in BaseNetworkable.serverEntities) Track(n as BaseEntity);
            timer.Every(30f, SyncAll); timer.Every(60f, Save); Queue();
            if (data.Codes == null) data.Codes = new Dictionary<ulong, string>();
            EP.AddChat("mycode", CmdMyCode);
        }
        internal void Unload() => Save();
        internal void OnNewSave(string f) { data = new Store(); dirty = true; Save(); }
        internal void Save() { if (!dirty) return; dirty = false; Interface.Oxide.DataFileSystem.WriteObject("EchoAutoAuth", data); }

        internal static bool Kind(BaseEntity e) => e is BuildingPrivlidge || e is AutoTurret || e is CodeLock;
        internal void Track(BaseEntity e) { if (e != null && Kind(e)) ents.Add(e); }
        internal void OnEntitySpawned(BaseNetworkable n) { var e = n as BaseEntity; if (e == null || !Kind(e)) return; ents.Add(e); NextTick(() => { if (e is CodeLock) AutoCode((CodeLock)e); Sync(e); }); }
        internal void OnEntityKill(BaseNetworkable n) { var e = n as BaseEntity; if (e == null || !Kind(e)) return; ents.Remove(e); if (e.net != null && data.Added.Remove(e.net.ID.Value)) dirty = true; }

        // clan / team changes -> resync
        internal void OnClanMemberJoined(string tag, string id, string name) => Queue();
        internal void OnClanMemberLeft(string tag, string id, string why) => Queue();
        internal void OnClanDisbanded(string tag) => Queue();
        internal void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player) => Queue();
        internal void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player) => Queue();
        internal void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong target) => Queue();
        internal void OnTeamDisbanded(RelationshipManager.PlayerTeam team) => Queue();
        internal void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team) => Queue();
        internal void Queue() { if (queued) return; queued = true; timer.Once(1f, () => { queued = false; SyncAll(); }); }

        // owner's group = owner + clan members + team members
        internal readonly Dictionary<ulong, HashSet<ulong>> groupCache = new Dictionary<ulong, HashSet<ulong>>();
        internal HashSet<ulong> Group(ulong owner)
        {
            HashSet<ulong> g; if (groupCache.TryGetValue(owner, out g)) return g;
            g = new HashSet<ulong> { owner };
            try
            {
                var d = EchoClans?.Call("API_ClanInfo", owner) as Dictionary<string, object>;
                if (d != null && d.ContainsKey("inClan") && (bool)d["inClan"] && d.ContainsKey("members"))
                    foreach (var m in (d["members"] as System.Collections.IEnumerable) ?? new object[0])
                    { var md = m as Dictionary<string, object>; ulong id; if (md != null && md.ContainsKey("id") && ulong.TryParse(md["id"]?.ToString(), out id)) g.Add(id); }
            }
            catch { }
            var team = RelationshipManager.ServerInstance?.FindPlayersTeam(owner);
            if (team != null) foreach (var m in team.members) g.Add(m);
            groupCache[owner] = g; return g;
        }

        internal void SyncAll() { groupCache.Clear(); foreach (var e in ents.ToList()) Sync(e); }

        internal void Sync(BaseEntity e)
        {
            if (e == null || e.IsDestroyed || e.net == null) { if (e != null) ents.Remove(e); return; }
            ulong owner = e.OwnerID; if (owner == 0UL || !owner.IsSteamId()) return;
            var want = Group(owner);
            ulong id = e.net.ID.Value;
            HashSet<ulong> ours; data.Added.TryGetValue(id, out ours);
            bool changed = false;
            foreach (var m in want)
            {
                if (!m.IsSteamId() || Has(e, m)) continue;
                if (!Add(e, m)) continue;
                changed = true; added++;
                if (ours == null) data.Added[id] = ours = new HashSet<ulong>();
                ours.Add(m); dirty = true;
            }
            if (ours != null)
                foreach (var m in ours.ToList())
                {
                    if (want.Contains(m)) continue;
                    ours.Remove(m); dirty = true;
                    if (Remove(e, m)) { changed = true; removed++; }
                }
            if (changed)
            {
                e.SendNetworkUpdate();
                if (e is BuildingPrivlidge) foreach (var m in want) { var p = BasePlayer.FindByID(m); if (p != null && p.IsConnected) p.SendNetworkUpdate(); }
            }
        }

        internal static bool Has(BaseEntity e, ulong m)
        {
            var tc = e as BuildingPrivlidge; if (tc != null) return tc.authorizedPlayers.Contains(m);
            var t = e as AutoTurret; if (t != null) return t.authorizedPlayers.Contains(m);
            var l = e as CodeLock; if (l != null) return l.whitelistPlayers.Contains(m);
            return true;
        }
        internal static bool Add(BaseEntity e, ulong m)
        {
            var tc = e as BuildingPrivlidge; if (tc != null) { tc.authorizedPlayers.Add(m); return true; }
            var t = e as AutoTurret; if (t != null) { t.authorizedPlayers.Add(m); return true; }
            var l = e as CodeLock; if (l != null) { l.whitelistPlayers.Add(m); return true; }
            return false;
        }
        internal static bool Remove(BaseEntity e, ulong m)
        {
            var tc = e as BuildingPrivlidge; if (tc != null) return tc.authorizedPlayers.Remove(m);
            var t = e as AutoTurret; if (t != null) return t.authorizedPlayers.Remove(m);
            var l = e as CodeLock; if (l != null) return l.whitelistPlayers.Remove(m);
            return false;
        }

        // ---------- 2.1.0: free code lock on every door ----------
        internal const string CodeLockPrefab = "assets/prefabs/locks/keypad/lock.code.prefab";
        internal int doorLocks;
        internal readonly Dictionary<ulong, float> toldCode = new Dictionary<ulong, float>();
        internal string CodeFor(ulong owner)
        {
            if (data.Codes == null) data.Codes = new Dictionary<ulong, string>();
            string c; if (!data.Codes.TryGetValue(owner, out c) || string.IsNullOrEmpty(c)) { c = UnityEngine.Random.Range(1000, 10000).ToString(); data.Codes[owner] = c; dirty = true; }
            return c;
        }
        internal void OnEntityBuilt(Planner plan, UnityEngine.GameObject go)
        {
            var door = go != null ? go.GetComponent<BaseEntity>() : null; if (!(door is Door) && !(door is BuildingPrivlidge)) return; var p = plan?.GetOwnerPlayer();   // 2.1.1: TCs too
            if (door == null || p == null || !((ulong)p.userID).IsSteamId()) return;
            NextTick(() =>
            {
                if (door == null || door.IsDestroyed || !door.HasSlot(BaseEntity.Slot.Lock) || door.GetSlot(BaseEntity.Slot.Lock) != null) return;
                var l = GameManager.server.CreateEntity(CodeLockPrefab, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity) as CodeLock;
                if (l == null) return;
                l.OwnerID = p.userID;
                l.SetParent(door, door.GetSlotAnchorName(BaseEntity.Slot.Lock));
                l.Spawn();
                door.SetSlot(BaseEntity.Slot.Lock, l);
                doorLocks++;
            });
        }
        internal void CmdMyCode(BasePlayer p, string command, string[] args)
        {
            if (p == null) return;
            p.ChatMessage($"<color=#39d2d1>ECHO</color> Your door code: <color=#ffd27f>{CodeFor(p.userID)}</color> (every door you place gets a code lock with it; your clan and team can open them).");
        }

        // ---------- auto code lock ----------
        internal void AutoCode(CodeLock l)
        {
            if (l == null || l.IsDestroyed || l.IsLocked() || !string.IsNullOrEmpty(l.code)) return;
            ulong owner = l.OwnerID; if (owner == 0UL || !owner.IsSteamId()) return;
            var code = CodeFor(owner);
            l.code = code;
            l.hasCode = true;
            if (!l.whitelistPlayers.Contains(owner)) l.whitelistPlayers.Add(owner);
            using (var f = l.StartSetFlags(BaseEntity.FlagsUpdateMode.SendNetworkUpdate)) f.Set(BaseEntity.Flags.Locked, true);
            l.SendNetworkUpdate(); codes++;
            var p = BasePlayer.FindByID(owner); float t;
            if (p != null && (!toldCode.TryGetValue(owner, out t) || UnityEngine.Time.realtimeSinceStartup - t > 600f))
            {
                toldCode[owner] = UnityEngine.Time.realtimeSinceStartup;
                p.ChatMessage($"<color=#39d2d1>ECHO</color> Code lock placed and locked. Code: <color=#ffd27f>{code}</color> (same on all your doors, /mycode) - your clan and team can open it.");
            }
        }

        internal void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith($"EchoAutoAuth 2.1.1: on | {doorLocks} door locks auto-placed | watching {ents.OfType<BuildingPrivlidge>().Count()} TCs, {ents.OfType<AutoTurret>().Count()} turrets, {ents.OfType<CodeLock>().Count()} code locks | {data.Added.Sum(k => k.Value.Count)} clan/team auths in place | since load: {added} added, {removed} removed, {codes} code locks auto-set");
        }
    
    }
    // ===================== EchoRoamsNoBuild =====================
    internal class M_EchoRoamsNoBuild : EchoMod
    {
        internal override string ModName => "EchoRoamsNoBuild";

        internal Plugin EchoRoams => EP.Find("EchoRoams"); internal Plugin EchoClans => EP.Find("EchoClans"); internal Plugin NoEscape => EP.Find("NoEscape");
        internal const float Radius = 160f;   // EchoRoams island radius is 150 m; a little extra so nobody builds on the edge
        internal const float TpCooldown = 60f;
        internal const string Tag = "<color=#39d2d1>ECHO</color> ";
        internal Vector3 centre; internal bool have; internal int blocked, kitsBlocked, craftsBlocked, tps;
        internal readonly Dictionary<ulong, float> lastTp = new Dictionary<ulong, float>();

        internal static readonly HashSet<string> NoCraft = new HashSet<string>
        {
            "shotgun.double", "shotgun.pump", "shotgun.spas12", "shotgun.waterpipe", "shotgun.m4", "pistol.eoka",
            "ammo.shotgun", "ammo.shotgun.slug", "ammo.shotgun.fire",
            "explosive.timed", "explosive.satchel", "explosives", "surveycharge", "grenade.f1", "grenade.beancan", "grenade.molotov",
            "rocket.launcher", "ammo.rocket.basic", "ammo.rocket.hv", "ammo.rocket.fire", "ammo.rocket.smoke", "ammo.rocket.seeker",
            "homingmissile.launcher", "multiplegrenadelauncher", "ammo.grenadelauncher.he", "ammo.grenadelauncher.buckshot", "ammo.grenadelauncher.smoke",
            "flamethrower", "catapult.ammo.explosive", "ammo.rifle.explosive"
        };

        internal void OnServerInitialized() { Refresh(); timer.Every(60f, Refresh); EP.AddChat("roamst", CmdRoamsT); }
        internal void OnPluginLoaded(Plugin p) { if (p != null && p.Name == "EchoRoams") timer.Once(5f, Refresh); }
        internal void Refresh()
        {
            try { var r = EchoRoams?.Call("API_Centre"); if (r is Vector3 && ((Vector3)r).sqrMagnitude > 1f) { centre = (Vector3)r; have = true; } } catch { }
        }

        internal bool OnIsland(Vector3 pos) => have && new Vector2(pos.x - centre.x, pos.z - centre.z).sqrMagnitude <= Radius * Radius;
        internal static bool Admin(BasePlayer p) => (p?.net?.connection?.authLevel ?? 0) >= 2;

        // ---------- no building ----------
        internal object CanBuild(Planner planner, Construction prefab, Construction.Target target)
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
        internal object CanClaimEchoKit(BasePlayer p, string kitId)
        {
            if (p == null || Admin(p) || !OnIsland(p.transform.position)) return null;
            kitsBlocked++;
            return "No kits on Roam Island - leave the island to claim one.";
        }

        // ---------- no shotgun / boom crafting ----------
        internal object CanCraft(ItemCrafter crafter, ItemBlueprint bp, int amount)
        {
            var p = crafter?.owner; var sn = bp?.targetItem?.shortname;
            if (p == null || sn == null || Admin(p) || !NoCraft.Contains(sn) || !OnIsland(p.transform.position)) return null;
            craftsBlocked++;
            p.ChatMessage(Tag + "You can't craft shotguns or explosives on <color=#ffd27f>Roam Island</color>.");
            return false;
        }

        // ---------- /roamst ----------
        internal bool Mate(BasePlayer a, BasePlayer b)
        {
            if (a == null || b == null || a == b) return false;
            if (a.currentTeam != 0 && a.currentTeam == b.currentTeam) return true;
            try { if (EchoClans?.Call("IsMember", (ulong)a.userID, (ulong)b.userID) is bool m && m) return true; } catch { }
            return false;
        }

        internal void CmdRoamsT(BasePlayer p, string command, string[] args)
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
        internal List<BaseEntity> Builds() => BaseNetworkable.serverEntities.OfType<BaseEntity>().Where(e => e != null && !e.IsDestroyed && e.OwnerID != 0UL && e.OwnerID.IsSteamId() && !(e is BasePlayer) && !e.HasParent() && (e is BuildingBlock || e is DecayEntity || e is StorageContainer || e is SleepingBag) && !(e is BaseVehicle) && OnIsland(e.transform.position)).ToList();

        internal void CStatus(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            a.ReplyWith(have ? $"EchoRoamsNoBuild 1.1.0: Roam Island centre {centre:F0}, rules within {Radius:0} m | blocked since load: {blocked} builds, {kitsBlocked} kits, {craftsBlocked} crafts | {tps} /roamst teleports" : "EchoRoamsNoBuild: waiting for the Roam Island centre from EchoRoams");
        }

        internal void CList(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            var ents = Builds();
            var by = ents.GroupBy(e => e.OwnerID).Select(g => (BasePlayer.FindAwakeOrSleeping(g.Key.ToString())?.displayName ?? g.Key.ToString()) + " " + g.Count());
            a.ReplyWith($"Player-built on Roam Island: {ents.Count} | " + string.Join(", ", by));
        }

        internal void CClear(ConsoleSystem.Arg a)
        {
            if (a.Connection != null && !a.IsAdmin) return;
            var ents = Builds(); int n = 0;
            foreach (var e in ents.Where(e => !(e is BuildingBlock))) if (!e.IsDestroyed) { e.Kill(); n++; }
            foreach (var e in ents.Where(e => e is BuildingBlock)) if (!e.IsDestroyed) { e.Kill(); n++; }
            a.ReplyWith($"Removed {n} player-built things from Roam Island.");
        }
    
    }

    }
}

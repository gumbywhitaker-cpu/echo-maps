// EchoSeaFort 1.0.0 (Nick, 31 Oct 2026): builds a sea fortress as a normal raidable base owned by Arki (Nick).
//   - Stone ring wall standing in shallow sea (high external stone walls in a circle).
//   - A 6x6, 5-storey sheet-metal keep in the middle: spiral stairs, windows, battlements on the roof,
//     an armoured door with a code lock, and a TC stocked with upkeep.
//   - Four 2x2 sheet-metal corner towers just inside the ring.
// Site: auto-picked shallow open sea near the map edge, far from player bases and monuments - or "seafort.build x z".
// Console (server / RCON / owner): seafort.build [x z] | seafort.remove | seafort.status
// After it's built it's an ordinary base (saved with the world); the plugin only keeps the list so it can be removed.
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoSeaFort", "Echo 10X", "1.0.0")]
    [Description("Builds the sea fortress base for Arki.")]
    public class EchoSeaFort : RustPlugin
    {
        private const ulong OwnerId = 76561198330214004UL;   // Arki (Nick)
        private const string PFound = "assets/prefabs/building core/foundation/foundation.prefab";
        private const string PFloor = "assets/prefabs/building core/floor/floor.prefab";
        private const string PWall = "assets/prefabs/building core/wall/wall.prefab";
        private const string PWindow = "assets/prefabs/building core/wall.window/wall.window.prefab";
        private const string PDoorway = "assets/prefabs/building core/wall.doorway/wall.doorway.prefab";
        private const string PLow = "assets/prefabs/building core/wall.low/wall.low.prefab";
        private const string PStairs = "assets/prefabs/building core/stairs.spiral/block.stair.spiral.prefab";
        private const string PHigh = "assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab";
        private const string PTc = "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab";
        private const string PDoor = "assets/prefabs/building/door.hinged/door.hinged.toptier.prefab";
        private const string PLock = "assets/prefabs/locks/keypad/lock.code.prefab";
        private const float Cell = 3f, LevelH = 3f, DeckY = 2f, RingR = 34f;
        private const int Keep = 6, KeepLevels = 5, TowerLevels = 2;

        private class Store { public List<ulong> Ids = new List<ulong>(); public float[] Centre; public string Code = ""; }
        private Store data;
        private readonly List<BaseEntity> parts = new List<BaseEntity>();
        private uint bid;

        private void Init() { data = Interface.Oxide.DataFileSystem.ReadObject<Store>("EchoSeaFort") ?? new Store(); }
        private void Save() => Interface.Oxide.DataFileSystem.WriteObject("EchoSeaFort", data);
        private bool Staff(ConsoleSystem.Arg a) => a.Connection == null || a.Connection.authLevel >= 2;

        // ---------- site ----------
        private static float Ground(float x, float z) => TerrainMeta.HeightMap.GetHeight(new Vector3(x, 0f, z));

        private Vector3? FindSite(out string why)
        {
            float half = TerrainMeta.Size.x * 0.5f;
            var tcs = BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>().Where(t => t != null).Select(t => t.transform.position).ToList();
            var mons = TerrainMeta.Path?.Monuments?.Where(m => m != null).Select(m => m.transform.position).ToList() ?? new List<Vector3>();
            Vector3? best = null; float bestScore = float.MinValue; int tried = 0;
            for (float r = half * 0.62f; r <= half * 0.9f; r += 40f)
                for (int i = 0; i < 72; i++)
                {
                    float a = i * Mathf.PI * 2f / 72f;
                    var c = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); tried++;
                    // every point inside the ring + 8 m must be water 2-12 m deep
                    bool ok = true; float minD = 99f, maxD = 0f;
                    for (int k = 0; k < 24 && ok; k++)
                        for (float rr = 0f; rr <= RingR + 8f && ok; rr += 7f)
                        {
                            float aa = k * Mathf.PI * 2f / 24f; float d = -Ground(c.x + Mathf.Cos(aa) * rr, c.z + Mathf.Sin(aa) * rr);
                            if (d < 2f || d > 12f) ok = false; minD = Mathf.Min(minD, d); maxD = Mathf.Max(maxD, d);
                        }
                    if (!ok) continue;
                    float nearTc = tcs.Count == 0 ? 9999f : tcs.Min(t => Vector2.Distance(new Vector2(t.x, t.z), new Vector2(c.x, c.z)));
                    float nearMon = mons.Count == 0 ? 9999f : mons.Min(m => Vector2.Distance(new Vector2(m.x, m.z), new Vector2(c.x, c.z)));
                    if (nearTc < 250f || nearMon < 250f) continue;
                    float score = -(maxD - minD) * 10f - Mathf.Abs(((minD + maxD) / 2f) - 5f) * 5f + Mathf.Min(nearTc, 1000f) / 100f;
                    if (score > bestScore) { bestScore = score; best = c; }
                }
            why = best.HasValue ? $"best of {tried} sea spots (shallow, flat, 250 m+ from bases and monuments)" : $"no shallow open-sea spot found in {tried} tries - use seafort.build x z";
            return best;
        }

        // ---------- spawning ----------
        private BaseEntity Spawn(string prefab, Vector3 pos, Quaternion rot, BuildingGrade.Enum grade = BuildingGrade.Enum.Metal)
        {
            if (!StringPool.toNumber.ContainsKey(prefab)) { PrintWarning("missing prefab " + prefab); return null; }
            var e = GameManager.server.CreateEntity(prefab, pos, rot);
            if (e == null) return null;
            e.OwnerID = OwnerId;
            var b = e as BuildingBlock;
            if (b != null) { b.blockDefinition = PrefabAttribute.server.Find<Construction>(b.prefabID); b.grounded = true; }
            e.Spawn();
            foreach (var gw in e.GetComponents<GroundWatch>()) UnityEngine.Object.Destroy(gw);
            foreach (var dg in e.GetComponents<DestroyOnGroundMissing>()) UnityEngine.Object.Destroy(dg);
            if (b != null)
            {
                try { b.AttachToBuilding(bid); } catch { }
                b.SetGrade(grade); b.SetHealthToMax(); b.grounded = true; b.SendNetworkUpdate();
            }
            else { var dec = e as DecayEntity; if (dec != null) { try { dec.AttachToBuilding(bid); } catch { } } }
            parts.Add(e);
            return e;
        }

        private static Quaternion Yaw(float y) => Quaternion.Euler(0f, y, 0f);

        // a square block of n x n cells with walls, floors, stairs; returns the stairs-free cell list
        private void Block(Vector3 c, int n, int levels, bool keep)
        {
            float off = (n - 1) * Cell / 2f;
            Func<int, int, float, Vector3> at = (i, j, y) => new Vector3(c.x - off + i * Cell, y, c.z - off + j * Cell);
            int si = 1, sj = 1;   // stairs cell (inside corner)
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) Spawn(PFound, at(i, j, DeckY), Quaternion.identity);
            for (int lv = 0; lv < levels; lv++)
            {
                float y = DeckY + lv * LevelH;
                if (n > 1) Spawn(PStairs, at(si, sj, y), Quaternion.identity); else Spawn(PStairs, at(0, 0, y), Quaternion.identity);
                for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
                {
                    bool stairsCell = n > 1 ? (i == si && j == sj) : true;
                    if (!(stairsCell && lv < levels - 1)) Spawn(PFloor, at(i, j, y + LevelH), Quaternion.identity);
                }
                // perimeter walls
                for (int k = 0; k < n; k++)
                {
                    bool mid = k == n / 2;
                    // south (-z) side: front door on the keep's ground floor
                    string south = lv == 0 && mid ? PDoorway : (lv > 0 && k % 2 == 1 ? PWindow : PWall);
                    string other = lv > 0 && k % 2 == 1 ? PWindow : PWall;
                    var sPos = at(k, 0, y) + new Vector3(0f, 0f, -Cell / 2f);
                    var w = Spawn(south, sPos, Yaw(90f));
                    if (keep && lv == 0 && mid && w != null) Door(sPos, Yaw(90f));
                    Spawn(other, at(k, n - 1, y) + new Vector3(0f, 0f, Cell / 2f), Yaw(-90f));
                    Spawn(other, at(0, k, y) + new Vector3(-Cell / 2f, 0f, 0f), Yaw(180f));
                    Spawn(other, at(n - 1, k, y) + new Vector3(Cell / 2f, 0f, 0f), Yaw(0f));
                }
            }
            // battlements on the roof
            float roof = DeckY + levels * LevelH;
            for (int k = 0; k < n; k++)
            {
                Spawn(PLow, at(k, 0, roof) + new Vector3(0f, 0f, -Cell / 2f), Yaw(90f));
                Spawn(PLow, at(k, n - 1, roof) + new Vector3(0f, 0f, Cell / 2f), Yaw(-90f));
                Spawn(PLow, at(0, k, roof) + new Vector3(-Cell / 2f, 0f, 0f), Yaw(180f));
                Spawn(PLow, at(n - 1, k, roof) + new Vector3(Cell / 2f, 0f, 0f), Yaw(0f));
            }
            if (keep)
            {
                // TC next to the stairs, stocked with upkeep
                var tc = Spawn(PTc, at(si + 1, sj, DeckY) + new Vector3(0f, 0.05f, 0f), Quaternion.identity) as BuildingPrivlidge;
                if (tc != null)
                {
                    tc.authorizedPlayers.Add(OwnerId);
                    foreach (var kv in new Dictionary<string, int> { { "wood", 20000 }, { "stones", 50000 }, { "metal.fragments", 50000 }, { "metal.refined", 2000 } })
                    { var it = ItemManager.CreateByName(kv.Key, kv.Value); if (it != null && !it.MoveToContainer(tc.inventory)) it.Remove(); }
                    tc.SendNetworkUpdate();
                }
            }
        }

        private void Door(Vector3 pos, Quaternion rot)
        {
            var d = Spawn(PDoor, pos, rot) as Door;
            if (d == null) return;
            var l = GameManager.server.CreateEntity(PLock, Vector3.zero, Quaternion.identity) as CodeLock;
            if (l == null) return;
            l.OwnerID = OwnerId;
            l.SetParent(d, d.GetSlotAnchorName(BaseEntity.Slot.Lock));
            l.Spawn();
            d.SetSlot(BaseEntity.Slot.Lock, l);
            data.Code = UnityEngine.Random.Range(1000, 10000).ToString();
            l.code = data.Code; l.hasCode = true;
            l.whitelistPlayers.Add(OwnerId);
            using (var f = l.StartSetFlags(BaseEntity.FlagsUpdateMode.SendNetworkUpdate)) f.Set(BaseEntity.Flags.Locked, true);
            parts.Add(l);
        }

        private string Build(Vector3? at)
        {
            Remove();
            string why = "set by admin";
            var site = at ?? FindSite(out why);
            if (!site.HasValue) return "Sea fort: " + why;
            var c = site.Value; c.y = 0f;
            bid = BuildingManager.server.NewBuildingID();
            parts.Clear();

            // ring wall: high external stone walls standing on the sea bed, tops ~2.5 m above the water
            int segs = Mathf.CeilToInt(2f * Mathf.PI * RingR / 5.6f);
            for (int s = 0; s < segs; s++)
            {
                float a = s * Mathf.PI * 2f / segs;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var p = c + dir * RingR;
                p.y = Mathf.Max(Ground(p.x, p.z), -3.5f);
                Spawn(PHigh, p, Quaternion.LookRotation(dir), BuildingGrade.Enum.Stone);
            }
            // the keep
            Block(c, Keep, KeepLevels, true);
            // four corner towers inside the ring
            foreach (var deg in new[] { 45f, 135f, 225f, 315f })
            {
                float a = deg * Mathf.Deg2Rad;
                Block(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (RingR - 7f), 2, TowerLevels, false);
            }

            data.Ids = parts.Where(e => e != null && e.net != null).Select(e => e.net.ID.Value).ToList();
            data.Centre = new[] { c.x, c.y, c.z };
            Save();
            var owner = BasePlayer.FindByID(OwnerId);
            owner?.ChatMessage($"<color=#39d2d1>ECHO</color> Your sea fort is built at {MapHelper.PositionToString(c)}. Front door code: <color=#ffd27f>{data.Code}</color>");
            return $"Sea fort built at {MapHelper.PositionToString(c)} {c:F0} ({why}) - {parts.Count} pieces, door code {data.Code}, owner Arki.";
        }

        private int Remove()
        {
            int n = 0;
            foreach (var id in data.Ids ?? new List<ulong>())
            {
                var e = BaseNetworkable.serverEntities.Find(new NetworkableId(id)) as BaseEntity;
                if (e != null && !e.IsDestroyed) { e.Kill(); n++; }
            }
            data.Ids = new List<ulong>(); Save();
            return n;
        }

        [ConsoleCommand("seafort.build")]
        private void CBuild(ConsoleSystem.Arg a)
        {
            if (!Staff(a)) return;
            Vector3? at = null;
            if (a.Args != null && a.Args.Length >= 2) { float x, z; if (float.TryParse(a.Args[0], out x) && float.TryParse(a.Args[1], out z)) at = new Vector3(x, 0f, z); }
            a.ReplyWith(Build(at));
        }

        [ConsoleCommand("seafort.remove")]
        private void CRemove(ConsoleSystem.Arg a) { if (Staff(a)) a.ReplyWith($"Sea fort removed ({Remove()} pieces)."); }

        [ConsoleCommand("seafort.status")]
        private void CStatus(ConsoleSystem.Arg a)
        {
            if (!Staff(a)) return;
            int alive = (data.Ids ?? new List<ulong>()).Count(id => BaseNetworkable.serverEntities.Find(new NetworkableId(id)) != null);
            a.ReplyWith(data.Centre == null ? "Sea fort: not built." : $"Sea fort at {MapHelper.PositionToString(new Vector3(data.Centre[0], 0, data.Centre[2]))}: {alive}/{data.Ids.Count} pieces standing, door code {data.Code}");
        }
    }
}

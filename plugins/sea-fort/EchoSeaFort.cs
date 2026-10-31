// EchoSeaFort 2.0.0 (Nick, 31 Oct 2026): sea fortress base owned by Arki (Nick) - a normal raidable base.
//   - Tall tiered sheet-metal keep: 8x8 -> 6x6 -> 4x4 -> 2x2 tiers (7 storeys, ~25 m), battlements on every step,
//     four corner turrets on the first roof, spiral stairs all the way up, armoured front door + code lock, stocked TC.
//   - Double-height stone ring wall standing in the sea, with four 2x2 towers built into the ring (3 storeys).
//   - A boat gate in the ring, a dock with steps, and a walkway from the dock to the keep's front door.
// Site: auto-picked shallow open sea near the map edge, far from bases and monuments - or "seafort.build x z".
// Built in batches over a few seconds so the server never hitches.
// Console (server / RCON / owner): seafort.build [x z] | seafort.remove | seafort.status
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoSeaFort", "Echo 10X", "2.0.0")]
    [Description("Builds the sea fortress base for Arki.")]
    public class EchoSeaFort : RustPlugin
    {
        private const ulong OwnerId = 76561198330214004UL;   // Arki (Nick)
        private const string PFound = "assets/prefabs/building core/foundation/foundation.prefab";
        private const string PSteps = "assets/prefabs/building core/foundation.steps/foundation.steps.prefab";
        private const string PFloor = "assets/prefabs/building core/floor/floor.prefab";
        private const string PWall = "assets/prefabs/building core/wall/wall.prefab";
        private const string PWindow = "assets/prefabs/building core/wall.window/wall.window.prefab";
        private const string PDoorway = "assets/prefabs/building core/wall.doorway/wall.doorway.prefab";
        private const string PLow = "assets/prefabs/building core/wall.low/wall.low.prefab";
        private const string PStairs = "assets/prefabs/building core/stairs.spiral/block.stair.spiral.prefab";
        private const string PHigh = "assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab";
        private const string PGate = "assets/prefabs/building/gates.external.high/gates.external.high.stone/gates.external.high.stone.prefab";
        private const string PTc = "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab";
        private const string PDoor = "assets/prefabs/building/door.hinged/door.hinged.toptier.prefab";
        private const string PLock = "assets/prefabs/locks/keypad/lock.code.prefab";
        private const float Cell = 3f, LevelH = 3f, DeckY = 2f, RingR = 42f;

        private class Store { public List<ulong> Ids = new List<ulong>(); public float[] Centre; public string Code = ""; }
        private Store data;
        private readonly List<BaseEntity> parts = new List<BaseEntity>();
        private readonly Queue<Action> jobs = new Queue<Action>();
        private uint bid; private Vector3 c; private string why = ""; private bool building;

        private void Init() { data = Interface.Oxide.DataFileSystem.ReadObject<Store>("EchoSeaFort") ?? new Store(); }
        private void Save() => Interface.Oxide.DataFileSystem.WriteObject("EchoSeaFort", data);
        private bool Staff(ConsoleSystem.Arg a) => a.Connection == null || a.Connection.authLevel >= 2;
        private static float Ground(float x, float z) => TerrainMeta.HeightMap.GetHeight(new Vector3(x, 0f, z));
        private static Quaternion Yaw(float y) => Quaternion.Euler(0f, y, 0f);

        // ---------- site ----------
        private Vector3? FindSite(out string reason)
        {
            float half = TerrainMeta.Size.x * 0.5f;
            var tcs = BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>().Where(t => t != null).Select(t => t.transform.position).ToList();
            var mons = TerrainMeta.Path?.Monuments?.Where(m => m != null).Select(m => m.transform.position).ToList() ?? new List<Vector3>();
            Vector3? best = null; float bestScore = float.MinValue; int tried = 0;
            for (float r = half * 0.6f; r <= half * 0.9f; r += 40f)
                for (int i = 0; i < 72; i++)
                {
                    float a = i * Mathf.PI * 2f / 72f;
                    var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); tried++;
                    bool ok = true; float minD = 99f, maxD = 0f;
                    for (int k = 0; k < 24 && ok; k++)
                        for (float rr = 0f; rr <= RingR + 8f && ok; rr += 7f)
                        {
                            float aa = k * Mathf.PI * 2f / 24f; float d = -Ground(p.x + Mathf.Cos(aa) * rr, p.z + Mathf.Sin(aa) * rr);
                            if (d < 2f || d > 12f) ok = false; minD = Mathf.Min(minD, d); maxD = Mathf.Max(maxD, d);
                        }
                    if (!ok) continue;
                    float nearTc = tcs.Count == 0 ? 9999f : tcs.Min(t => Vector2.Distance(new Vector2(t.x, t.z), new Vector2(p.x, p.z)));
                    float nearMon = mons.Count == 0 ? 9999f : mons.Min(m => Vector2.Distance(new Vector2(m.x, m.z), new Vector2(p.x, p.z)));
                    if (nearTc < 250f || nearMon < 250f) continue;
                    float score = -(maxD - minD) * 10f - Mathf.Abs(((minD + maxD) / 2f) - 5f) * 5f + Mathf.Min(nearTc, 1000f) / 100f;
                    if (score > bestScore) { bestScore = score; best = p; }
                }
            reason = best.HasValue ? $"best of {tried} sea spots" : $"no shallow open-sea spot found in {tried} tries - use seafort.build x z";
            return best;
        }

        // ---------- spawning ----------
        private BaseEntity Spawn(string prefab, Vector3 pos, Quaternion rot, BuildingGrade.Enum grade = BuildingGrade.Enum.Metal, string fallback = null)
        {
            if (!StringPool.toNumber.ContainsKey(prefab)) { if (fallback != null) return Spawn(fallback, pos, rot, grade); PrintWarning("missing prefab " + prefab); return null; }
            var e = GameManager.server.CreateEntity(prefab, pos, rot);
            if (e == null) return null;
            e.OwnerID = OwnerId;
            var b = e as BuildingBlock;
            if (b != null) { b.blockDefinition = PrefabAttribute.server.Find<Construction>(b.prefabID); b.grounded = true; }
            e.Spawn();
            foreach (var gw in e.GetComponents<GroundWatch>()) UnityEngine.Object.Destroy(gw);
            foreach (var dg in e.GetComponents<DestroyOnGroundMissing>()) UnityEngine.Object.Destroy(dg);
            if (b != null) { try { b.AttachToBuilding(bid); } catch { } b.SetGrade(grade); b.SetHealthToMax(); b.grounded = true; b.SendNetworkUpdate(); }
            else { var dec = e as DecayEntity; if (dec != null) { try { dec.AttachToBuilding(bid); } catch { } } }
            parts.Add(e);
            return e;
        }
        private void Q(string prefab, Vector3 pos, Quaternion rot, BuildingGrade.Enum grade = BuildingGrade.Enum.Metal, string fallback = null) { if (prefab != null) jobs.Enqueue(() => Spawn(prefab, pos, rot, grade, fallback)); }

        private Vector3 Cxz(float x, float z, float y) => new Vector3(c.x + x, y, c.z + z);

        // perimeter of a square tier of n cells (centre-relative cell centres at (i - (n-1)/2) * Cell)
        private void Perimeter(float ox, float oz, int n, float y, Func<int, string> pick, BuildingGrade.Enum g = BuildingGrade.Enum.Metal)
        {
            float off = (n - 1) * Cell / 2f, h = Cell / 2f;
            for (int k = 0; k < n; k++)
            {
                float t = -off + k * Cell;
                Q(pick(k), Cxz(ox + t, oz - off - h, y), Yaw(90f), g);      // south
                Q(pick(k + 100), Cxz(ox + t, oz + off + h, y), Yaw(-90f), g);  // north
                Q(pick(k + 200), Cxz(ox - off - h, oz + t, y), Yaw(180f), g);  // west
                Q(pick(k + 300), Cxz(ox + off + h, oz + t, y), Yaw(0f), g);    // east
            }
        }

        private void Floors(float ox, float oz, int n, float y, Func<float, float, bool> skip = null)
        {
            float off = (n - 1) * Cell / 2f;
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
            {
                float x = ox - off + i * Cell, z = oz - off + j * Cell;
                if (skip != null && skip(x, z)) continue;
                Q(PFloor, Cxz(x, z, y), Quaternion.identity);
            }
        }

        // ---------- plan ----------
        private void Plan()
        {
            var towerAngles = new[] { 45f, 135f, 225f, 315f };
            int segs = Mathf.CeilToInt(2f * Mathf.PI * RingR / 5.6f);
            PlanRing(towerAngles, segs);
            foreach (var deg in towerAngles) { float a = deg * Mathf.Deg2Rad; Tower(Mathf.Cos(a) * RingR, Mathf.Sin(a) * RingR); }
            Keep();
            Dock();
        }

        private void PlanRing(float[] towerAngles, int segs)
        {
            for (int s = 0; s < segs; s++)
            {
                float a = s * Mathf.PI * 2f / segs, deg = a * Mathf.Rad2Deg;
                if (towerAngles.Any(t => Mathf.Abs(Mathf.DeltaAngle(deg, t)) < 6.5f)) continue;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var p = new Vector3(c.x, 0f, c.z) + dir * RingR;
                bool gate = Mathf.Abs(Mathf.DeltaAngle(deg, 270f)) < 180f / segs;
                float bed = Ground(p.x, p.z);
                if (!gate && bed < -1.5f) Q(PHigh, new Vector3(p.x, bed, p.z), Quaternion.LookRotation(dir), BuildingGrade.Enum.Stone);
                Q(gate ? PGate : PHigh, new Vector3(p.x, -1f, p.z), Quaternion.LookRotation(dir), BuildingGrade.Enum.Stone, PHigh);
            }
        }

        private void Tower(float ox, float oz)
        {
            const int n = 2, levels = 3;
            float off = (n - 1) * Cell / 2f;
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) Q(PFound, Cxz(ox - off + i * Cell, oz - off + j * Cell, DeckY), Quaternion.identity);
            float sx = ox - off, sz = oz - off;   // stairs cell
            for (int lv = 0; lv < levels; lv++)
            {
                float y = DeckY + lv * LevelH;
                Q(PStairs, Cxz(sx, sz, y), Quaternion.identity);
                bool top = lv == levels - 1;
                Floors(ox, oz, n, y + LevelH, (x, z) => !top && Mathf.Abs(x - sx) < 0.1f && Mathf.Abs(z - sz) < 0.1f);
                int L = lv;
                Perimeter(ox, oz, n, y, k => L == 0 ? PWall : ((k % 100) == 1 ? PWindow : PWall));
            }
            Perimeter(ox, oz, n, DeckY + levels * LevelH, k => PLow);
        }

        private void Keep()
        {
            // tiers: (cells, storeys)
            var tiers = new[] { new[] { 8, 2 }, new[] { 6, 2 }, new[] { 4, 2 }, new[] { 2, 1 } };
            int total = tiers.Sum(t => t[1]);
            float sx = -Cell / 2f, sz = -Cell / 2f;   // stairs cell - inside every tier
            float off8 = 7 * Cell / 2f;
            for (int i = 0; i < 8; i++) for (int j = 0; j < 8; j++) Q(PFound, Cxz(-off8 + i * Cell, -off8 + j * Cell, DeckY), Quaternion.identity);
            int lv = 0;
            for (int t = 0; t < tiers.Length; t++)
            {
                int n = tiers[t][0];
                for (int s = 0; s < tiers[t][1]; s++, lv++)
                {
                    float y = DeckY + lv * LevelH;
                    bool last = lv == total - 1;
                    Q(PStairs, Cxz(sx, sz, y), Quaternion.identity);
                    // floor above: this tier's cells; the floor that ends a tier also roofs the step around the next tier
                    Floors(0f, 0f, n, y + LevelH, (x, z) => !last && Mathf.Abs(x - sx) < 0.1f && Mathf.Abs(z - sz) < 0.1f);
                    int L = lv, N = n;
                    Perimeter(0f, 0f, n, y, k =>
                    {
                        int side = k / 100, idx = k % 100;
                        if (L == 0 && side == 0 && idx == N / 2) return PDoorway;     // front door, south
                        if (L == 0) return PWall;
                        return idx % 2 == 1 ? PWindow : PWall;
                    });
                }
                // battlements around this tier's roof edge (the first roof's corners belong to the turrets)
                int NN = n, T = t;
                Perimeter(0f, 0f, n, DeckY + lv * LevelH, k => T == 0 && ((k % 100) == 0 || (k % 100) == NN - 1) ? null : PLow);
                // corner turrets on the first roof
                if (t == 0)
                {
                    float o = 7 * Cell / 2f, ty = DeckY + lv * LevelH;
                    foreach (var cx in new[] { -o, o }) foreach (var cz in new[] { -o, o })
                    {
                        for (int s = 0; s < 2; s++)
                        {
                            Perimeter(cx, cz, 1, ty + s * LevelH, k => s == 1 ? PWindow : PWall);
                            Q(PFloor, Cxz(cx, cz, ty + (s + 1) * LevelH), Quaternion.identity);
                        }
                        Perimeter(cx, cz, 1, ty + 2 * LevelH, k => PLow);
                    }
                }
            }
            // front door + lock, TC
            float doorZ = -off8 - Cell / 2f;
            jobs.Enqueue(() => Door(Cxz(Cell / 2f, doorZ, DeckY), Yaw(90f)));   // n/2 = 4 -> cell x = -10.5 + 4*3 = 1.5
            jobs.Enqueue(() => Tc(Cxz(sx + Cell, sz, DeckY + 0.05f)));
        }

        private void Dock()
        {
            // walkway from the keep door (south) to a dock beside the boat gate, low walls as railings, steps into the water
            float start = -(7 * Cell / 2f) - Cell, end = -(RingR - 6f);
            float x0 = Cell / 2f;
            for (float z = start; z >= end; z -= Cell)
            {
                Q(PFound, Cxz(x0, z, DeckY), Quaternion.identity, BuildingGrade.Enum.Stone);
                Q(PFound, Cxz(x0 - Cell, z, DeckY), Quaternion.identity, BuildingGrade.Enum.Stone);
                Q(PLow, Cxz(x0 + Cell / 2f, z, DeckY), Yaw(0f), BuildingGrade.Enum.Stone);
                Q(PLow, Cxz(x0 - Cell - Cell / 2f, z, DeckY), Yaw(180f), BuildingGrade.Enum.Stone);
            }
            // dock platform 4 wide at the end + steps down to the water on both sides
            float dz = end - Cell;
            for (int i = -1; i < 3; i++) Q(PFound, Cxz(x0 - Cell + (i - 0) * Cell - Cell, dz, DeckY), Quaternion.identity, BuildingGrade.Enum.Stone);
            Q(PSteps, Cxz(x0 - 3.5f * Cell, dz, DeckY), Yaw(180f), BuildingGrade.Enum.Stone);
            Q(PSteps, Cxz(x0 + Cell / 2f, dz, DeckY), Yaw(0f), BuildingGrade.Enum.Stone);
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

        private void Tc(Vector3 pos)
        {
            var tc = Spawn(PTc, pos, Quaternion.identity) as BuildingPrivlidge;
            if (tc == null) return;
            tc.authorizedPlayers.Add(OwnerId);
            foreach (var kv in new Dictionary<string, int> { { "wood", 30000 }, { "stones", 80000 }, { "metal.fragments", 80000 }, { "metal.refined", 3000 } })
            { var it = ItemManager.CreateByName(kv.Key, kv.Value); if (it != null && !it.MoveToContainer(tc.inventory)) it.Remove(); }
            tc.SendNetworkUpdate();
        }

        // ---------- run ----------
        private string Build(Vector3? at)
        {
            if (building) return "Sea fort: already building.";
            Remove();
            why = "set by admin";
            var site = at ?? FindSite(out why);
            if (!site.HasValue) return "Sea fort: " + why;
            c = site.Value; c.y = 0f;
            bid = BuildingManager.server.NewBuildingID();
            parts.Clear(); jobs.Clear();
            Plan();
            int n = jobs.Count; building = true;
            Step();
            return $"Sea fort: building {n} pieces at {MapHelper.PositionToString(c)} {c:F0} ({why}) - done in a few seconds. seafort.status to check.";
        }

        private void Step()
        {
            int k = 0;
            while (jobs.Count > 0 && k++ < 120) { try { jobs.Dequeue()(); } catch (Exception e) { PrintWarning("sea fort piece failed: " + e.Message); } }
            if (jobs.Count > 0) { timer.Once(0.15f, Step); return; }
            building = false;
            data.Ids = parts.Where(e => e != null && e.net != null).Select(e => e.net.ID.Value).ToList();
            data.Centre = new[] { c.x, c.y, c.z };
            Save();
            Puts($"Sea fort built at {MapHelper.PositionToString(c)} {c:F0}: {parts.Count} pieces, door code {data.Code}, owner Arki.");
            BasePlayer.FindByID(OwnerId)?.ChatMessage($"<color=#39d2d1>ECHO</color> Your sea fort is built at {MapHelper.PositionToString(c)}. Front door code: <color=#ffd27f>{data.Code}</color>");
        }

        private int Remove()
        {
            int n = 0;
            foreach (var id in data.Ids ?? new List<ulong>())
            {
                var e = BaseNetworkable.serverEntities.Find(new NetworkableId(id)) as BaseEntity;
                if (e != null && !e.IsDestroyed) { e.Kill(); n++; }
            }
            data.Ids = new List<ulong>(); data.Centre = null; Save();
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
            if (building) { a.ReplyWith($"Sea fort: building, {jobs.Count} pieces to go."); return; }
            int alive = (data.Ids ?? new List<ulong>()).Count(id => BaseNetworkable.serverEntities.Find(new NetworkableId(id)) != null);
            a.ReplyWith(data.Centre == null ? "Sea fort: not built." : $"Sea fort at {MapHelper.PositionToString(new Vector3(data.Centre[0], 0, data.Centre[2]))}: {alive}/{data.Ids.Count} pieces standing, door code {data.Code}");
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // (the Snow* ones belong to the snow map: A meadow, B pine valley, C cliff ascent, D1 low snow, D2 ice lake plateau, D3 castle)
    public enum Zone { Town, Meadow, AutumnForest, DarkPineForest, Farmland, Lake, Mountain, Lair, SnowMeadow, SnowPine, SnowCliff, SnowField, SnowPlateau, SnowCastle }

    // Where everything is on the 380 x 380 m map (X east, Z north, the camera never turns so north is the top of the screen).
    // Plain data + a few queries; the scene itself is made by WorldBuilder from these numbers.
    public static class MapLayout
    {
        public const float Half = Game.ArenaHalf;
        public const float ChunkSize = 38f;                                   // 10 x 10 chunks
        // The biome the scene / logic is currently in (Game.SetBiome). Everything below answers for it; the meadow values are the original ones.
        public static Biome Current = Biome.Meadow;

        public static readonly Vector3 MeadowTownSpawn = new Vector3(0f, 0f, -150f);
        public static readonly Vector3 MeadowDummyPos = new Vector3(25f, 0f, -140f);
        public static readonly Vector3 MeadowLair = new Vector3(0f, 0f, 150f);

        public static Vector3 TownSpawn { get { return Current == Biome.Snow ? SnowLayout.Spawn : MeadowTownSpawn; } }      // where knights start and come back
        public static readonly Vector3 BasePoint = new Vector3(0f, 0f, 0f);       // Phase 1 loop: the base is the empty central lawn (always the meadow scene)
        public static readonly Vector3 TownCenter = new Vector3(0f, 0f, -145f);
        public static readonly Vector3 Gate = new Vector3(0f, 0f, -100f);
        public static Vector3 DummyPos { get { return Current == Biome.Snow ? SnowLayout.Dummy : MeadowDummyPos; } }
        public static Vector3 Lair { get { return Current == Biome.Snow ? SnowLayout.Lair : MeadowLair; } }
        public static readonly Vector3 MenuFocus = new Vector3(0f, 0f, -142f);
        public static readonly Vector3 Crossroads = new Vector3(0f, 0f, -20f);
        public static float LairRadius { get { return Current == Biome.Snow ? SnowLayout.LairRadius : 25f; } }
        public const float TownMinX = -60f, TownMaxX = 60f, TownMinZ = -190f, TownMaxZ = -100f;

        // The same answers for a biome that is not the current one (saves and old worlds: where is a pile, where do they stand up).
        public static Vector3 TownSpawnOf(Biome b) { return b == Biome.Snow ? SnowLayout.Spawn : MeadowTownSpawn; }
        public static bool WalkableIn(Biome b, float x, float z) { return b == Biome.Snow ? SnowLayout.Walkable(x, z) : MeadowWalkable(x, z); }

        public struct SignInfo { public Vector3 pos; public string text; }
        public static void AddSnowSign(Vector3 pos, string text) { snowSigns.Add(new SignInfo { pos = pos, text = text }); }
        public static void ClearSnowSigns() { snowSigns.Clear(); }
        static readonly List<SignInfo> meadowSigns = new List<SignInfo>(), snowSigns = new List<SignInfo>();
        public static List<SignInfo> Signs { get { return Current == Biome.Snow ? snowSigns : meadowSigns; } }

        // The town is a rectangle (not a circle): the safe zone, spawn and revive point.
        public static bool InTown(float x, float z)
        {
            if (Current == Biome.Snow) return SnowLayout.InTown(x, z);
            return x >= TownMinX && x <= TownMaxX && z >= TownMinZ && z <= TownMaxZ;
        }

        // Priority: lair, town, lake, mountain, the other regions, then meadow as the default.
        public static Zone ZoneAt(float x, float z)
        {
            if (Current == Biome.Snow) return SnowZoneAt(x, z);
            float dx = x - Lair.x, dz = z - Lair.z;
            if (dx * dx + dz * dz <= LairRadius * LairRadius) return Zone.Lair;
            if (InTown(x, z)) return Zone.Town;
            if (x <= -90f && z <= -90f) return Zone.Lake;
            if (x <= -80f && z >= 100f) return Zone.Mountain;
            if (x <= -90f && z >= -90f && z <= 110f) return Zone.AutumnForest;
            if (x >= 100f && z >= 40f) return Zone.DarkPineForest;
            if (x >= 90f && z >= -150f && z <= -30f) return Zone.Farmland;
            return Zone.Meadow;
        }

        // Snow map: lair circle, village (safe), then water / cliffs / mountains / castle walls from the baked table, then the design's
        // rectangles (A meadow, B pine valley, C cliff ascent, D1 low snow, D2 plateau, D3 castle); the rest is open snow.
        static Zone SnowZoneAt(float x, float z)
        {
            Vector3 lair = SnowLayout.Lair;
            float dx = x - lair.x, dz = z - lair.z, r = SnowLayout.LairRadius;
            if (dx * dx + dz * dz <= r * r) return Zone.Lair;
            if (SnowLayout.InTown(x, z)) return Zone.Town;
            int code = SnowLayout.BlockAt(x, z);
            if (code == 2) return Zone.Lake;
            if (code != 0) return Zone.Mountain;
            float tier;
            switch (SnowLayout.RectZone(x, z, out tier))
            {
                case 0: return Zone.SnowMeadow;
                case 1: return Zone.SnowPine;
                case 2: return Zone.SnowCliff;
                case 3: return Zone.SnowField;
                case 4: return Zone.SnowPlateau;
                case 5: return Zone.SnowCastle;
                default: return Zone.SnowField;
            }
        }

        // Multiplier of the slimes' blue / red chances in a zone; 0 = nothing spawns there.
        public static float SpawnTier(Zone z)
        {
            switch (z)
            {
                case Zone.Meadow: case Zone.Farmland: return 1f;
                case Zone.AutumnForest: return 1.5f;
                case Zone.DarkPineForest: case Zone.Lair: return 2f;
                case Zone.SnowMeadow: return 1f;
                case Zone.SnowPine: return 1.5f;
                case Zone.SnowCliff: case Zone.SnowField: case Zone.SnowPlateau: case Zone.SnowCastle: return 2f;
                default: return 0f;
            }
        }

        public static bool Walkable(float x, float z)
        {
            if (Current == Biome.Snow) return SnowLayout.Walkable(x, z);
            return MeadowWalkable(x, z);
        }

        static bool MeadowWalkable(float x, float z)
        {
            Zone zn = ZoneAt(x, z);
            return zn != Zone.Lake && zn != Zone.Mountain;
        }
    }

    // Collision for the scenery: circles and (rotated) boxes on a grid of 8 m cells. Layers (bit mask): 1 = knights, 2 = enemies.
    // Trees only stop knights, so slimes hopping in a straight line never get stuck behind one.
    public static class Obstacles
    {
        public const int Players = 1, Enemies = 2, Both = 3;
        const float Cell = 8f, Origin = -Game.ArenaHalf - 2f;
        const int N = 48;

        class Obs { public bool box; public float x, z, r, hx, hz, c, s; public int mask; }

        // one set per biome (the meadow scene is built once and the snow scene once; switching only changes which set answers)
        class Set { public List<Obs> items = new List<Obs>(); public List<int>[,] cells = new List<int>[N, N]; }
        static readonly Set[] sets = { new Set(), new Set() };
        static Set cur = sets[0];
        static List<Obs> items { get { return cur.items; } }
        static List<int>[,] cells { get { return cur.cells; } set { cur.cells = value; } }

        public static void Use(Biome b) { cur = sets[(int)b]; }

        // ---- test helpers: the set of a biome (not necessarily the one in use)
        public static int CountOf(Biome b) { return sets[(int)b].items.Count; }

        // A number that depends on every obstacle (kind, position, size, yaw, mask) in order: two builds of the same map must agree.
        public static int HashOf(Biome b)
        {
            unchecked
            {
                int h = 17;
                foreach (Obs o in sets[(int)b].items)
                {
                    h = h * 31 + (o.box ? 1 : 0);
                    h = h * 31 + Mathf.RoundToInt(o.x * 100f);
                    h = h * 31 + Mathf.RoundToInt(o.z * 100f);
                    h = h * 31 + Mathf.RoundToInt((o.box ? o.hx : o.r) * 100f);
                    h = h * 31 + Mathf.RoundToInt(o.hz * 100f);
                    h = h * 31 + Mathf.RoundToInt(Mathf.Atan2(o.s, o.c) * Mathf.Rad2Deg * 10f);
                    h = h * 31 + o.mask;
                }
                return h;
            }
        }

        // How many obstacles of that biome have their centre inside the rectangle (the central lawn rule).
        public static int CentersIn(Biome b, float minX, float maxX, float minZ, float maxZ, bool circlesOnly = false)
        {
            int n = 0;
            foreach (Obs o in sets[(int)b].items) if (o.x >= minX && o.x <= maxX && o.z >= minZ && o.z <= maxZ && !(circlesOnly && o.box)) n++;
            return n;
        }

        // True when a circle of this radius at p overlaps something on that layer (the set in use).
        public static bool Overlaps(Vector3 p, float radius, int layer)
        {
            Vector3 q = Push(p, radius, layer);
            return (q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z) > 1e-6f;
        }

        public static int Count { get { return items.Count; } }

        // Test helper: the circle nearest to a point whose layer mask is exactly this one (trees = Players).
        public static bool NearestCircle(Vector2 from, int exactMask, out Vector2 centre, out float radius)
        {
            centre = Vector2.zero; radius = 0f;
            float best = float.MaxValue;
            for (int i = 0; i < items.Count; i++)
            {
                Obs o = items[i];
                if (o.box || o.mask != exactMask) continue;
                float d = (new Vector2(o.x, o.z) - from).sqrMagnitude;
                if (d >= best) continue;
                best = d; centre = new Vector2(o.x, o.z); radius = o.r;
            }
            return best < float.MaxValue;
        }

        // clears the set in use (WorldBuilder calls it with the meadow set selected, as before)
        public static void Clear()
        {
            cur.items.Clear();
            cur.cells = new List<int>[N, N];
        }

        static int Ci(float v) { return Mathf.Clamp(Mathf.FloorToInt((v - Origin) / Cell), 0, N - 1); }

        static void Register(Obs o, float minX, float maxX, float minZ, float maxZ)
        {
            int id = items.Count;
            items.Add(o);
            int x0 = Ci(minX), x1 = Ci(maxX), z0 = Ci(minZ), z1 = Ci(maxZ);
            for (int i = x0; i <= x1; i++)
                for (int j = z0; j <= z1; j++)
                {
                    if (cells[i, j] == null) cells[i, j] = new List<int>();
                    cells[i, j].Add(id);
                }
        }

        public static void AddCircle(Vector2 c, float r, int mask)
        {
            Register(new Obs { x = c.x, z = c.y, r = r, mask = mask }, c.x - r, c.x + r, c.y - r, c.y + r);
        }

        // half = half extents along the box's own X / Z; yaw in degrees (Unity convention: 0 = faces +Z).
        public static void AddBox(Vector2 c, Vector2 half, float yaw, int mask)
        {
            float a = yaw * Mathf.Deg2Rad;
            var o = new Obs { box = true, x = c.x, z = c.y, hx = half.x, hz = half.y, c = Mathf.Cos(a), s = Mathf.Sin(a), mask = mask };
            float ex = Mathf.Abs(o.c) * half.x + Mathf.Abs(o.s) * half.y, ez = Mathf.Abs(o.s) * half.x + Mathf.Abs(o.c) * half.y;
            Register(o, c.x - ex, c.x + ex, c.y - ez, c.y + ez);
        }

        // Returns p moved out of everything on the given layer that a circle of this radius would overlap.
        public static Vector3 Push(Vector3 p, float radius, int layer)
        {
            if (items.Count == 0) return p;
            for (int pass = 0; pass < 2; pass++)
            {
                bool moved = false;
                int x0 = Ci(p.x - radius), x1 = Ci(p.x + radius), z0 = Ci(p.z - radius), z1 = Ci(p.z + radius);
                for (int i = x0; i <= x1; i++)
                    for (int j = z0; j <= z1; j++)
                    {
                        List<int> l = cells[i, j];
                        if (l == null) continue;
                        for (int n = 0; n < l.Count; n++)
                        {
                            Obs o = items[l[n]];
                            if ((o.mask & layer) == 0) continue;
                            if (o.box) moved |= PushBox(o, ref p, radius); else moved |= PushCircle(o, ref p, radius);
                        }
                    }
                if (!moved) break;
            }
            return p;
        }

        static bool PushCircle(Obs o, ref Vector3 p, float radius)
        {
            float dx = p.x - o.x, dz = p.z - o.z;
            float min = o.r + radius;
            float d2 = dx * dx + dz * dz;
            if (d2 >= min * min) return false;
            float d = Mathf.Sqrt(d2);
            if (d < 0.0001f) { dx = 1f; dz = 0f; d = 1f; }
            p.x = o.x + dx / d * min;
            p.z = o.z + dz / d * min;
            return true;
        }

        static bool PushBox(Obs o, ref Vector3 p, float radius)
        {
            float wx = p.x - o.x, wz = p.z - o.z;
            float lx = wx * o.c - wz * o.s, lz = wx * o.s + wz * o.c;           // into the box's own frame
            float cx = Mathf.Clamp(lx, -o.hx, o.hx), cz = Mathf.Clamp(lz, -o.hz, o.hz);
            float dx = lx - cx, dz = lz - cz;
            float d2 = dx * dx + dz * dz;
            float nx, nz;
            if (d2 < 0.000001f)
            {
                // centre inside the box: leave through the nearest face
                float px = o.hx - Mathf.Abs(lx), pz = o.hz - Mathf.Abs(lz);
                if (px < pz) { nx = (lx >= 0f ? o.hx : -o.hx) + (lx >= 0f ? radius : -radius); nz = lz; }
                else { nx = lx; nz = (lz >= 0f ? o.hz : -o.hz) + (lz >= 0f ? radius : -radius); }
            }
            else
            {
                if (d2 >= radius * radius) return false;
                float d = Mathf.Sqrt(d2);
                nx = cx + dx / d * radius;
                nz = cz + dz / d * radius;
            }
            p.x = o.x + nx * o.c + nz * o.s;
            p.z = o.z - nx * o.s + nz * o.c;
            return true;
        }
    }

    // Merges the many small pieces of the scenery into one mesh per (chunk, material, shadow): a 380 m map of
    // trees and houses would be ~15,000 separate renderers otherwise. Unity culls each chunk by its own bounds.
    public class StaticBatcher
    {
        readonly Dictionary<Material, int> matIndex = new Dictionary<Material, int>();
        readonly List<Material> mats = new List<Material>();
        readonly Dictionary<long, List<CombineInstance>> buckets = new Dictionary<long, List<CombineInstance>>();
        public int Instances { get; private set; }

        public void Add(Mesh mesh, Matrix4x4 xf, Material mat, bool shadows, int subMesh = 0)
        {
            int mi;
            if (!matIndex.TryGetValue(mat, out mi)) { mi = mats.Count; mats.Add(mat); matIndex[mat] = mi; }
            Vector3 p = xf.GetColumn(3);
            int per = Mathf.RoundToInt(MapLayout.Half * 2f / MapLayout.ChunkSize);
            int cx = Mathf.Clamp(Mathf.FloorToInt((p.x + MapLayout.Half) / MapLayout.ChunkSize), 0, per - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt((p.z + MapLayout.Half) / MapLayout.ChunkSize), 0, per - 1);
            long key = (((long)cz * 16 + cx) * 4096 + mi) * 2 + (shadows ? 1 : 0);
            List<CombineInstance> l;
            if (!buckets.TryGetValue(key, out l)) { l = new List<CombineInstance>(); buckets[key] = l; }
            l.Add(new CombineInstance { mesh = mesh, subMeshIndex = subMesh, transform = xf });
            Instances++;
        }

        public int Build(Transform parent)
        {
            int made = 0;
            foreach (KeyValuePair<long, List<CombineInstance>> kv in buckets)
            {
                long key = kv.Key;
                bool shadows = (key & 1) != 0;
                int mi = (int)((key >> 1) % 4096);
                int chunk = (int)((key >> 1) / 4096);
                var m = new Mesh { name = "Chunk " + (chunk % 16) + "_" + (chunk / 16), indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.CombineMeshes(kv.Value.ToArray(), true, true);
                m.RecalculateBounds();
                var go = new GameObject(m.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mats[mi];
                mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = true;
                made++;
            }
            return made;
        }
    }

    public class Spinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        public float degPerSec = 20f;
        void Update() { transform.Rotate(axis, degPerSec * Time.deltaTime, Space.Self); }
    }

    // Builds the whole scene from MapLayout: ground, town, roads, forests, farm, lake, mountains, lair, fence.
    // Deterministic (System.Random(11), never UnityEngine.Random): every machine in a co-op game must get the same collision.
    public sealed class WorldBuilder
    {
        readonly StaticBatcher b = new StaticBatcher();
        readonly System.Random rng = new System.Random(11);
        readonly List<Vector3> keep = new List<Vector3>();                    // x, z, radius: no trees / flowers here
        Material grass, grassA, grassB, dirt, stone, stoneDark, wood, woodDark, trunk, plaster, plasterB, roofRed, roofBlue, roofGreen,
            door, window, hay, cloth1, cloth2, water, snow, rock, rockB, bone, ember, gold, soilA, soilB, white;
        Material[] autumn, pines, meadowLeaf, flowers, tileMat;

        float R(float a, float c) { return a + (float)rng.NextDouble() * (c - a); }

        // Art_kit scenery (Kit.cs). Its own fixed-seed stream: the main rng above is never touched by the kit, so every
        // position and every obstacle stays exactly as before and only the look of the pieces changes.
        readonly System.Random deco = new System.Random(11);
        readonly Dictionary<string, int> kitCount = new Dictionary<string, int>();
        readonly List<Vector4> kitHouses = new List<Vector4>();               // x, z, yaw, depth of the cottages
        Material brickRoof;
        Dictionary<string, Material>[] autumnOver;
        float D(float a, float c) { return a + (float)deco.NextDouble() * (c - a); }

        // Puts a kit prefab (all its meshes) into the batcher. False = no catalog: the caller draws the old primitives.
        bool PutKit(string name, Vector3 pos, float yaw, float scale, Dictionary<string, Material> over = null, bool shadow = true)
        {
            Kit.Part[] parts = Kit.Get(name);
            if (parts == null || parts.Length == 0) return false;
            Matrix4x4 root = Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
            foreach (Kit.Part part in parts)
            {
                int n = part.mesh.subMeshCount;
                for (int i = 0; i < n; i++)
                {
                    int j = Mathf.Min(i, part.mats.Length - 1);
                    Material m = part.mats[j];
                    Material ov;
                    if (over != null && over.TryGetValue(part.keys[j], out ov)) m = ov;
                    b.Add(part.mesh, root * part.local, m, shadow, i);
                }
            }
            int c;
            kitCount.TryGetValue(name, out c);
            kitCount[name] = c + 1;
            return true;
        }

        static bool HasKit { get { return Kit.Get("Tree_Pine") != null; } }

        public static void Build(Transform parent)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Obstacles.Clear();
            MapLayout.Signs.Clear();
            var w = new WorldBuilder();
            w.MakeMaterials();
            w.Ground();
            w.Town();
            w.Roads();
            w.Lair();
            w.Landmarks();
            w.Farm();
            w.Lake();
            w.Mountains();
            w.Forests();
            w.Decoration();
            w.EdgeFence();
            w.KitProps();
            int chunks = w.b.Build(parent);
            Debug.Log("WORLD built in " + sw.ElapsedMilliseconds + " ms: " + w.b.Instances + " pieces merged into " + chunks + " chunk meshes, " + Obstacles.Count + " obstacles");
            var kc = new System.Text.StringBuilder();
            int kt = 0;
            foreach (KeyValuePair<string, int> kv in w.kitCount) { kc.Append(kv.Key).Append('=').Append(kv.Value).Append(' '); kt += kv.Value; }
            Debug.Log("WORLD kit: " + kt + " kit pieces placed: " + kc);
        }

        void MakeMaterials()
        {
            grass = Prims.Lit(new Color(0.37f, 0.62f, 0.28f));
            grassA = Prims.Lit(new Color(0.42f, 0.67f, 0.3f));
            grassB = Prims.Lit(new Color(0.32f, 0.56f, 0.26f));
            dirt = Prims.Lit(new Color(0.62f, 0.5f, 0.32f));
            stone = Prims.Lit(new Color(0.62f, 0.62f, 0.64f));
            stoneDark = Prims.Lit(new Color(0.45f, 0.45f, 0.48f));
            wood = Prims.Lit(new Color(0.45f, 0.3f, 0.16f));
            woodDark = Prims.Lit(new Color(0.32f, 0.2f, 0.11f));
            trunk = Prims.Lit(new Color(0.36f, 0.24f, 0.13f));
            plaster = Prims.Lit(new Color(0.86f, 0.8f, 0.66f));
            plasterB = Prims.Lit(new Color(0.62f, 0.62f, 0.64f));
            roofRed = Prims.Lit(new Color(0.6f, 0.2f, 0.17f));
            roofBlue = Prims.Lit(new Color(0.28f, 0.36f, 0.5f));
            roofGreen = Prims.Lit(new Color(0.25f, 0.42f, 0.3f));
            door = Prims.Lit(new Color(0.2f, 0.13f, 0.08f));
            window = Prims.Lit(new Color(0.2f, 0.28f, 0.4f));
            hay = Prims.Lit(new Color(0.86f, 0.72f, 0.3f));
            cloth1 = Prims.Lit(new Color(0.8f, 0.15f, 0.13f));
            cloth2 = Prims.Lit(new Color(0.16f, 0.32f, 0.8f));
            water = Prims.Lit(new Color(0.2f, 0.52f, 0.62f), 0.7f);
            snow = Prims.Lit(new Color(0.94f, 0.95f, 0.98f));
            rock = Prims.Lit(new Color(0.46f, 0.44f, 0.42f));
            rockB = Prims.Lit(new Color(0.38f, 0.36f, 0.36f));
            bone = Prims.Lit(new Color(0.9f, 0.88f, 0.8f));
            ember = Prims.Glow(new Color(1f, 0.55f, 0.12f));
            gold = Prims.Lit(new Color(0.96f, 0.78f, 0.22f), 0.7f, 0.8f);
            soilA = Prims.Lit(new Color(0.55f, 0.47f, 0.25f));
            soilB = Prims.Lit(new Color(0.46f, 0.52f, 0.22f));
            white = Prims.Glow(new Color(0.95f, 0.95f, 0.9f));
            autumn = new[] { Prims.Lit(new Color(0.85f, 0.45f, 0.12f)), Prims.Lit(new Color(0.72f, 0.22f, 0.12f)), Prims.Lit(new Color(0.9f, 0.7f, 0.18f)) };
            pines = new[] { Prims.Lit(new Color(0.08f, 0.22f, 0.14f)), Prims.Lit(new Color(0.1f, 0.26f, 0.17f)), Prims.Lit(new Color(0.06f, 0.18f, 0.12f)) };
            meadowLeaf = new[] { Prims.Lit(new Color(0.16f, 0.45f, 0.2f)), Prims.Lit(new Color(0.2f, 0.52f, 0.22f)), Prims.Lit(new Color(0.13f, 0.38f, 0.2f)) };
            flowers = new[] { Prims.Lit(new Color(0.95f, 0.9f, 0.35f)), Prims.Lit(new Color(0.95f, 0.5f, 0.6f)), Prims.Lit(new Color(0.9f, 0.92f, 0.98f)) };
        }

        // ---------------------------------------------------------------- tiny placement helpers

        static Mesh roofMesh;
        static Mesh Roof()
        {
            if (roofMesh != null) return roofMesh;
            var m = new MeshB();
            Vector3 a = new Vector3(-0.5f, 0f, -0.5f), c = new Vector3(0.5f, 0f, -0.5f), d = new Vector3(0.5f, 0f, 0.5f), e = new Vector3(-0.5f, 0f, 0.5f);
            Vector3 r0 = new Vector3(0f, 1f, -0.5f), r1 = new Vector3(0f, 1f, 0.5f);
            m.Quad(a, r0, r1, e, new Vector3(-1f, 1f, 0f));
            m.Quad(c, r0, r1, d, new Vector3(1f, 1f, 0f));
            m.Tri(a, c, r0, Vector3.back);
            m.Tri(e, d, r1, Vector3.forward);
            m.Quad(a, c, d, e, Vector3.down);
            roofMesh = m.Build("Roof");
            return roofMesh;
        }

        void Put(Mesh mesh, Vector3 pos, Vector3 scale, Quaternion rot, Material mat, bool shadow = true)
        {
            b.Add(mesh, Matrix4x4.TRS(pos, rot, scale), mat, shadow);
        }

        void Box(Vector3 center, Vector3 size, float yaw, Material mat, bool shadow = true)
        {
            Put(Prims.Cube, center, size, Quaternion.Euler(0f, yaw, 0f), mat, shadow);
        }

        void Cyl(Vector3 basePos, float diam, float h, int sides, Material mat, bool shadow = true)
        {
            Put(Prims.Cyl(sides), basePos, new Vector3(diam, h, diam), Quaternion.identity, mat, shadow);
        }

        void Cone(Vector3 basePos, float diam, float h, int sides, Material mat, bool shadow = true)
        {
            Put(Prims.Cone(sides), basePos, new Vector3(diam, h, diam), Quaternion.identity, mat, shadow);
        }

        void Ball(Vector3 center, Vector3 size, int sides, Material mat, bool shadow = true)
        {
            Put(Prims.Ball(sides), center, size, Quaternion.identity, mat, shadow);
        }

        void RoofAt(Vector3 baseCenter, float w, float h, float d, float yaw, Material mat)
        {
            Put(Roof(), baseCenter, new Vector3(w, h, d), Quaternion.Euler(0f, yaw, 0f), mat);
        }

        static Vector3 Local(Vector3 origin, float yaw, float lx, float ly, float lz)
        {
            return origin + Quaternion.Euler(0f, yaw, 0f) * new Vector3(lx, ly, lz);
        }

        void Keep(float x, float z, float r) { keep.Add(new Vector3(x, z, r)); }

        bool Kept(float x, float z, float margin)
        {
            for (int i = 0; i < keep.Count; i++)
            {
                float dx = x - keep[i].x, dz = z - keep[i].y, rr = keep[i].z + margin;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            return false;
        }

        static bool OnRoad(float x, float z)
        {
            if (Mathf.Abs(x) < 4.2f && z > -150f && z < 128f) return true;                       // the north road
            if (Mathf.Abs(z - MapLayout.Crossroads.z) < 3.8f && Mathf.Abs(x) < 110f) return true;   // the crossroads east / west
            return false;
        }

        // ---------------------------------------------------------------- ground

        void Ground()
        {
            // a huge dark field under everything (and beyond the fence)
            Box(new Vector3(0f, -0.52f, 0f), new Vector3(520f, 1f, 520f), 0f, Prims.Lit(new Color(0.2f, 0.4f, 0.2f)), false);
            const float T = 10f;
            int n = Mathf.RoundToInt(MapLayout.Half * 2f / T);
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float cx = -MapLayout.Half + (i + 0.5f) * T, cz = -MapLayout.Half + (j + 0.5f) * T;
                    Zone z = MapLayout.ZoneAt(cx, cz);
                    bool alt = ((i + j) & 1) == 0;
                    Material m;
                    float top = 0f;
                    switch (z)
                    {
                        case Zone.Town: m = alt ? Prims.Lit(new Color(0.55f, 0.54f, 0.52f)) : Prims.Lit(new Color(0.5f, 0.49f, 0.47f)); break;
                        case Zone.Lake: m = water; top = -0.08f; break;
                        case Zone.Mountain: m = alt ? Prims.Lit(new Color(0.4f, 0.38f, 0.36f)) : Prims.Lit(new Color(0.36f, 0.34f, 0.33f)); break;
                        case Zone.Lair: m = Prims.Lit(new Color(0.36f, 0.35f, 0.37f)); break;
                        case Zone.AutumnForest: m = alt ? Prims.Lit(new Color(0.5f, 0.43f, 0.22f)) : Prims.Lit(new Color(0.46f, 0.38f, 0.2f)); break;
                        case Zone.DarkPineForest: m = alt ? Prims.Lit(new Color(0.16f, 0.27f, 0.18f)) : Prims.Lit(new Color(0.13f, 0.23f, 0.15f)); break;
                        case Zone.Farmland: m = alt ? soilA : soilB; break;
                        default: m = alt ? grass : grassA; break;
                    }
                    Box(new Vector3(cx, top - 0.5f, cz), new Vector3(T, 1f, T), 0f, m, false);
                }
            }
            // mottled patches, three per chunk
            for (int cx = 0; cx < 10; cx++)
            {
                for (int cz = 0; cz < 10; cz++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float x = -MapLayout.Half + (cx + R(0.1f, 0.9f)) * MapLayout.ChunkSize, z = -MapLayout.Half + (cz + R(0.1f, 0.9f)) * MapLayout.ChunkSize;
                        Zone zn = MapLayout.ZoneAt(x, z);
                        if (zn == Zone.Town || zn == Zone.Lake || zn == Zone.Mountain || zn == Zone.Lair || OnRoad(x, z)) continue;
                        Material m = k % 2 == 0 ? grassA : grassB;
                        if (zn == Zone.AutumnForest) m = k % 2 == 0 ? Prims.Lit(new Color(0.56f, 0.46f, 0.22f)) : Prims.Lit(new Color(0.42f, 0.34f, 0.18f));
                        else if (zn == Zone.DarkPineForest) m = k % 2 == 0 ? Prims.Lit(new Color(0.11f, 0.2f, 0.13f)) : Prims.Lit(new Color(0.18f, 0.3f, 0.2f));
                        else if (zn == Zone.Farmland) continue;
                        Box(new Vector3(x, 0.012f, z), new Vector3(R(3f, 8f), 0.03f, R(3f, 8f)), R(0f, 90f), m, false);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- roads

        void Roads()
        {
            // north road from the town plaza up to the lair, split in 30 m pieces so every chunk culls its own
            for (float z = -150f; z < 125f; z += 30f)
            {
                float len = Mathf.Min(30f, 125f - z);
                Box(new Vector3(0f, 0.03f, z + len * 0.5f), new Vector3(3.6f, 0.04f, len), 0f, dirt, false);
            }
            for (float x = -105f; x < 105f; x += 30f)
            {
                float len = Mathf.Min(30f, 105f - x);
                Box(new Vector3(x + len * 0.5f, 0.032f, MapLayout.Crossroads.z), new Vector3(len, 0.04f, 3.2f), 0f, dirt, false);
            }
            // signs: an arrow board on a post, with the text shown on the HUD when a knight walks up to it
            Sign(new Vector3(6f, 0f, -96f), 0f, new Color(0.9f, 0.4f, 0.2f), "North road: the Minotaur's lair is far beyond the crossroads");
            Sign(new Vector3(5f, 0f, -24f), 0f, new Color(0.9f, 0.4f, 0.2f), "North: Minotaur's Lair");
            Sign(new Vector3(9f, 0f, -24f), 90f, new Color(0.85f, 0.75f, 0.3f), "East: Farmland and the Dark Pine Forest");
            Sign(new Vector3(-9f, 0f, -24f), -90f, new Color(0.9f, 0.55f, 0.15f), "West: Autumn Forest");
            Sign(new Vector3(5f, 0f, 118f), 0f, new Color(0.9f, 0.3f, 0.2f), "The Minotaur's lair is just ahead");
        }

        // dirYaw 0 = pointing north (+Z), 90 = east, -90 = west
        void Sign(Vector3 p, float dirYaw, Color tint, string text)
        {
            Cyl(p, 0.2f, 2.5f, 6, woodDark);
            Box(p + new Vector3(0f, 2.2f, 0f), new Vector3(0.14f, 0.55f, 1.7f), dirYaw, Prims.Lit(tint));
            Vector3 tip = Local(p + new Vector3(0f, 2.2f, 0f), dirYaw, 0f, 0f, 0.85f);
            Put(Prims.Cone(4), tip, new Vector3(0.7f, 0.7f, 0.5f), Quaternion.Euler(0f, dirYaw, 0f) * Quaternion.Euler(90f, 0f, 0f), Prims.Lit(tint));
            Obstacles.AddCircle(new Vector2(p.x, p.z), 0.3f, Obstacles.Both);
            Keep(p.x, p.z, 3f);
            MapLayout.Signs.Add(new MapLayout.SignInfo { pos = p, text = text });
        }

        // ---------------------------------------------------------------- town

        void House(float x, float z, float yaw, Material walls, Material roof, float w = 7f, float d = 6f, float h = 4.2f)
        {
            Vector3 o = new Vector3(x, 0f, z);
            // the kit cottage (its front faces -Z in the prefab, the primitive house had its door on +Z) replaces the ordinary
            // houses; the barn (w > 7.5) stays a primitive. The collision box below is the same either way.
            Dictionary<string, Material> over = null;
            if (roof == roofRed)
            {
                if (brickRoof == null) brickRoof = Prims.Lit(new Color(0.71f, 0.32f, 0.23f));       // #B5523B, brick roof from the palette
                over = new Dictionary<string, Material> { { "roof_purple", brickRoof } };
            }
            if (w <= 7.5f && PutKit("Cottage", o, yaw + 180f, 1.25f, over))
            {
                kitHouses.Add(new Vector4(x, z, yaw, d));
            }
            else
            {
            Box(o + new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), yaw, walls);
            RoofAt(o + new Vector3(0f, h, 0f), w + 1f, 2.6f, d + 1f, yaw, roof);
            Box(Local(o, yaw, 0f, 1.1f, d * 0.5f + 0.04f), new Vector3(1.2f, 2.2f, 0.12f), yaw, door);
            Box(Local(o, yaw, -w * 0.28f, 2.4f, d * 0.5f + 0.04f), new Vector3(0.9f, 0.9f, 0.12f), yaw, window);
            Box(Local(o, yaw, w * 0.28f, 2.4f, d * 0.5f + 0.04f), new Vector3(0.9f, 0.9f, 0.12f), yaw, window);
            Box(Local(o, yaw, -w * 0.5f + 0.1f, h * 0.5f, d * 0.5f + 0.05f), new Vector3(0.25f, h, 0.14f), yaw, woodDark);
            Box(Local(o, yaw, w * 0.5f - 0.1f, h * 0.5f, d * 0.5f + 0.05f), new Vector3(0.25f, h, 0.14f), yaw, woodDark);
            Box(Local(o, yaw, 0f, h - 0.5f, d * 0.5f + 0.05f), new Vector3(w, 0.25f, 0.14f), yaw, woodDark);
            }
            Obstacles.AddBox(new Vector2(x, z), new Vector2(w * 0.5f + 0.1f, d * 0.5f + 0.1f), yaw, Obstacles.Both);
            Keep(x, z, Mathf.Max(w, d) * 0.5f + 4f);
        }

        void Town()
        {
            // plaza of stone with a well; the spawn is the open ground in front of the well
            Cyl(new Vector3(0f, 0f, -150f), 25f, 0.05f, 16, stoneDark, false);
            Cyl(new Vector3(0f, 0f, -150f), 23f, 0.06f, 16, stone, false);
            Cyl(new Vector3(0f, 0f, -158f), 3.4f, 1.1f, 10, stoneDark);
            Cyl(new Vector3(0f, 0.9f, -158f), 2.6f, 0.2f, 10, water, false);
            Box(new Vector3(-1.3f, 2.1f, -158f), new Vector3(0.18f, 2.2f, 0.18f), 0f, woodDark);
            Box(new Vector3(1.3f, 2.1f, -158f), new Vector3(0.18f, 2.2f, 0.18f), 0f, woodDark);
            RoofAt(new Vector3(0f, 3.1f, -158f), 3.4f, 1.2f, 3f, 0f, roofRed);
            Obstacles.AddCircle(new Vector2(0f, -158f), 1.7f, Obstacles.Both);
            Keep(0f, -150f, 14f);

            // houses on both sides of the plaza (the yard of the straw dummy keeps the east middle free)
            float[] xs = { -50f, -38f, -26f, 26f, 38f, 50f };
            float[] zs = { -112f, -130f, -150f, -170f };
            int hi = 0;
            for (int a = 0; a < xs.Length; a++)
            {
                for (int c = 0; c < zs.Length; c++)
                {
                    float x = xs[a], z = zs[c];
                    if ((x == 26f || x == 38f) && (z == -130f || z == -150f)) continue;
                    float yaw = x < 0f ? 90f : -90f;
                    Material walls = (hi % 3 == 0) ? plasterB : plaster;
                    Material roof = hi % 3 == 0 ? roofBlue : hi % 3 == 1 ? roofRed : roofGreen;
                    House(x, z + R(-1f, 1f), yaw, walls, roof);
                    hi++;
                }
            }

            // the clock tower: the tallest thing in town, seen from the whole north road
            Vector3 tp = new Vector3(-15f, 0f, -122f);
            Box(tp + new Vector3(0f, 7f, 0f), new Vector3(5f, 14f, 5f), 0f, stone);
            Box(tp + new Vector3(0f, 14.2f, 0f), new Vector3(6f, 0.6f, 6f), 0f, stoneDark);
            Cone(tp + new Vector3(0f, 14.5f, 0f), 6.4f, 5.5f, 4, roofRed);
            Box(tp + new Vector3(0f, 11f, 2.56f), new Vector3(2.4f, 2.4f, 0.16f), 0f, white, false);
            Box(tp + new Vector3(0f, 11f, 2.66f), new Vector3(0.2f, 1.0f, 0.12f), 0f, door, false);
            Obstacles.AddBox(new Vector2(tp.x, tp.z), new Vector2(2.7f, 2.7f), 0f, Obstacles.Both);
            Keep(tp.x, tp.z, 9f);

            // the north wall with the gate
            for (int side = -1; side <= 1; side += 2)
            {
                float x0 = side * 5.5f, x1 = side * 60f;
                float cxw = (x0 + x1) * 0.5f, len = Mathf.Abs(x1 - x0);
                for (float s = 0f; s < len; s += 12f)
                {
                    float seg = Mathf.Min(12f, len - s);
                    float cx = side * (5.5f + s + seg * 0.5f);
                    Box(new Vector3(cx, 1.3f, -100f), new Vector3(seg, 2.6f, 1.2f), 0f, stoneDark);
                    Box(new Vector3(cx, 2.75f, -100f), new Vector3(seg, 0.3f, 1.6f), 0f, stone);
                }
                Obstacles.AddBox(new Vector2(cxw, -100f), new Vector2(len * 0.5f, 0.7f), 0f, Obstacles.Both);
                // gate tower
                Box(new Vector3(side * 4.6f, 3f, -100f), new Vector3(1.9f, 6f, 1.9f), 0f, stone);
                Box(new Vector3(side * 4.6f, 6.2f, -100f), new Vector3(2.4f, 0.5f, 2.4f), 0f, stoneDark);
                Cone(new Vector3(side * 4.6f, 6.4f, -100f), 2.6f, 2.2f, 4, roofRed);
                Obstacles.AddCircle(new Vector2(side * 4.6f, -100f), 1.1f, Obstacles.Both);
            }
            Box(new Vector3(0f, 5.4f, -100f), new Vector3(9.4f, 0.9f, 1.6f), 0f, stone);
            Box(new Vector3(-4.6f, 4.2f, -98.95f), new Vector3(0.12f, 2.2f, 0.9f), 0f, cloth1);
            Box(new Vector3(4.6f, 4.2f, -98.95f), new Vector3(0.12f, 2.2f, 0.9f), 0f, cloth2);
            Keep(0f, -100f, 10f);

            // the training yard of the straw dummy: a low fence round three sides, open to the plaza
            Vector3 dp = MapLayout.DummyPos;
            YardFence(new Vector3(dp.x + 12f, 0f, dp.z), 0f, 28f);          // east side
            YardFence(new Vector3(dp.x, 0f, dp.z + 14f), 90f, 24f);         // north side
            YardFence(new Vector3(dp.x, 0f, dp.z - 14f), 90f, 24f);         // south side
            Box(new Vector3(dp.x + 9f, 0.8f, dp.z + 11f), new Vector3(2.4f, 1.6f, 0.4f), 0f, wood);   // weapon rack
            Box(new Vector3(dp.x + 9f, 1.7f, dp.z + 11f), new Vector3(2.6f, 0.2f, 0.5f), 0f, woodDark);
            Keep(dp.x, dp.z, 15f);

            // four market stalls round the plaza
            float[] sx = { -15f, 15f, -15f, 15f };
            float[] sz = { -141f, -163f, -163f, -141f };
            for (int i = 0; i < 4; i++)
            {
                Vector3 s = new Vector3(sx[i], 0f, sz[i]);
                Material awn = i % 2 == 0 ? cloth1 : cloth2;
                Box(s + new Vector3(0f, 0.55f, 0f), new Vector3(3f, 1.1f, 1.4f), 0f, wood);
                Box(s + new Vector3(-1.4f, 1.5f, -0.6f), new Vector3(0.14f, 3f, 0.14f), 0f, woodDark);
                Box(s + new Vector3(1.4f, 1.5f, -0.6f), new Vector3(0.14f, 3f, 0.14f), 0f, woodDark);
                RoofAt(s + new Vector3(0f, 2.9f, -0.3f), 3.4f, 0.8f, 2.4f, 0f, awn);
                Obstacles.AddBox(new Vector2(s.x, s.z), new Vector2(1.6f, 0.8f), 0f, Obstacles.Both);
                Keep(s.x, s.z, 3f);
            }
        }

        void YardFence(Vector3 center, float yaw, float length)
        {
            float a = yaw * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));                // along the fence
            // yaw 0 runs along +Z; the east side is such a fence, the north / south sides are rotated by 90
            bool kitFence = HasKit;
            if (kitFence)
            {
                // kit fence pieces (2 m, along their own X, rails on the local -Z side): turn them along the line, rails toward the yard
                float psi = yaw - 90f;
                Vector3 front = new Vector3(-Mathf.Sin(psi * Mathf.Deg2Rad), 0f, -Mathf.Cos(psi * Mathf.Deg2Rad));
                if (Vector3.Dot(front, center - MapLayout.DummyPos) > 0f) psi += 180f;
                int pieces = Mathf.CeilToInt(length / 1.95f);
                float step = length / pieces;
                for (int i = 0; i < pieces; i++)
                    PutKit("Fence_Straight_2m", center + dir * (-length * 0.5f + step * (i + 0.5f)), psi, step / 1.95f);
            }
            else
            {
            for (float s = -length * 0.5f; s <= length * 0.5f + 0.01f; s += 2.8f)
                Box(center + dir * s + Vector3.up * 0.65f, new Vector3(0.26f, 1.3f, 0.26f), yaw, woodDark);
            Box(center + Vector3.up * 1.0f, new Vector3(0.12f, 0.14f, length), yaw, wood);
            Box(center + Vector3.up * 0.5f, new Vector3(0.12f, 0.14f, length), yaw, wood);
            }
            Obstacles.AddBox(new Vector2(center.x, center.z), new Vector2(0.25f, length * 0.5f), yaw, Obstacles.Both);
        }

        // ---------------------------------------------------------------- the lair, landmarks, farm

        void Lair()
        {
            Vector3 L = MapLayout.Lair;
            Cyl(L, 26f, 0.08f, 20, stoneDark, false);
            Cyl(L, 18f, 0.1f, 16, stone, false);
            Cyl(L, 6f, 0.12f, 10, stoneDark, false);
            // ruined colonnade: a ring of broken columns open to the south, where the road comes in
            for (int i = 0; i < 16; i++)
            {
                float ang = 30f + i * 20f;                                            // 0 = north
                if (ang > 140f && ang < 220f) continue;                                // open to the south, where the road comes in
                float rad = 21f;
                Vector3 p = new Vector3(L.x + Mathf.Sin(ang * Mathf.Deg2Rad) * rad, 0f, L.z + Mathf.Cos(ang * Mathf.Deg2Rad) * rad);
                float h = R(1.8f, 5.2f);
                Box(p + Vector3.up * 0.15f, new Vector3(1.7f, 0.3f, 1.7f), 0f, stoneDark);
                Cyl(p, 1.1f, h, 8, stone);
                if (h > 3.6f) Box(p + Vector3.up * (h + 0.12f), new Vector3(1.6f, 0.24f, 1.6f), 0f, stoneDark);
                Obstacles.AddCircle(new Vector2(p.x, p.z), 0.8f, Obstacles.Both);
            }
            // the gate: two bone-white horns, and braziers that can be seen from far away
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 gp = new Vector3(L.x + side * 7f, 0f, L.z - 20f);
                Box(gp + Vector3.up * 2f, new Vector3(1.4f, 4f, 1.4f), 0f, stoneDark);
                Put(Prims.Cone(5), gp + new Vector3(side * 0.7f, 4.2f, 0f), new Vector3(0.9f, 2.6f, 0.9f), Quaternion.Euler(0f, 0f, -side * 28f), bone);
                Obstacles.AddCircle(new Vector2(gp.x, gp.z), 1.0f, Obstacles.Both);
                Vector3 fp = new Vector3(L.x + side * 12f, 0f, L.z - 4f);
                Cyl(fp, 0.9f, 7f, 6, stoneDark);
                Ball(fp + Vector3.up * 7.3f, new Vector3(1.4f, 1.4f, 1.4f), 6, ember, false);
                Obstacles.AddCircle(new Vector2(fp.x, fp.z), 0.7f, Obstacles.Both);
            }
            Keep(L.x, L.z, 30f);
        }

        void Landmarks()
        {
            // the old watchtower
            Vector3 w = new Vector3(-60f, 0f, 70f);
            Cyl(w, 6.4f, 14f, 8, stone);
            Cyl(w + Vector3.up * 14f, 7.6f, 1f, 8, stoneDark);
            Cone(w + Vector3.up * 15f, 7.8f, 4.5f, 8, roofBlue);
            Box(w + new Vector3(0f, 9f, 3.2f), new Vector3(0.9f, 1.4f, 0.2f), 0f, door);
            Obstacles.AddCircle(new Vector2(w.x, w.z), 3.4f, Obstacles.Both);
            Keep(w.x, w.z, 8f);
            // a few boulders round it
            for (int i = 0; i < 5; i++) Rock(w.x + R(-9f, 9f), w.z + R(-9f, 9f), R(1.2f, 2.4f));
        }

        void Rock(float x, float z, float s)
        {
            string kitRock = null;
            float ks = 1f;
            if (HasKit)
            {
                // same blocking rule as before (s >= 1.4 blocks, smaller ones never did), so the size classes follow it:
                // low rocks you can step over, then medium and large boulders that have a collision circle
                char v = "ABC"[Mathf.Abs(Mathf.FloorToInt(x * 7.3f + z * 3.1f)) % 3];
                if (s < 1.4f) { kitRock = "Rock_Small_" + v; ks = s * 1.7f; }
                else if (s < 1.9f) { kitRock = "Rock_Medium_" + v; ks = s * 1.05f; }
                else { kitRock = "Rock_Large_" + v; ks = Mathf.Lerp(0.7f, 1f, (s - 1.9f) / 0.5f); }
            }
            if (kitRock != null) PutKit(kitRock, new Vector3(x, 0f, z), Mathf.Abs(x * 13f + z * 5f) % 360f, ks);
            else Ball(new Vector3(x, 0.3f * s, z), new Vector3(1.5f, 1f, 1.2f) * s, 6, s > 1.6f ? rock : rockB);
            if (s >= 1.4f) Obstacles.AddCircle(new Vector2(x, z), 0.7f * s, Obstacles.Both);
        }

        void Farm()
        {
            // plough lines on the soil
            for (float z = -146f; z < -32f; z += 8f)
                Box(new Vector3(140f, 0.02f, z), new Vector3(96f, 0.03f, 3.6f), 0f, ((int)(z / 8f) & 1) == 0 ? soilB : soilA, false);
            // the windmill
            Vector3 m = new Vector3(130f, 0f, -100f);
            Cyl(m, 7f, 10f, 8, plaster);
            Cone(m + Vector3.up * 10f, 8.4f, 4f, 8, roofRed);
            Obstacles.AddCircle(new Vector2(m.x, m.z), 3.8f, Obstacles.Both);
            Keep(m.x, m.z, 10f);
            var hub = new GameObject("WindmillBlades").transform;
            hub.position = m + new Vector3(0f, 8.5f, -3.9f);
            hub.gameObject.AddComponent<Spinner>().degPerSec = 22f;
            Prims.Box(hub, "Arm1", Vector3.zero, new Vector3(0.9f, 12f, 0.2f), wood);
            Prims.Box(hub, "Arm2", Vector3.zero, new Vector3(12f, 0.9f, 0.2f), wood);
            Prims.Part(hub, "Hub", Prims.Ball(6), new Vector3(0f, 0f, 0.1f), new Vector3(1.2f, 1.2f, 1.2f), woodDark);
            // barn and farmhouse
            House(150f, -60f, 180f, plasterB, roofRed, 12f, 8f, 5f);
            House(120f, -130f, 0f, plaster, roofBlue);
            House(165f, -120f, -90f, plaster, roofGreen);
            // hay bales
            for (int i = 0; i < 9; i++)
            {
                float x = R(100f, 180f), z = R(-140f, -40f);
                if (Kept(x, z, 1.5f)) continue;
                Put(Prims.Cyl(8), new Vector3(x, 0f, z), new Vector3(1.6f, 1.3f, 1.6f), Quaternion.Euler(0f, R(0f, 90f), 0f), hay);
                Obstacles.AddCircle(new Vector2(x, z), 0.9f, Obstacles.Both);
            }
        }

        void Lake()
        {
            // the water itself is made of ground tiles (Ground()); here: a wooden dock, a boat, the box that keeps everybody out
            Obstacles.AddBox(new Vector2(-140f, -140f), new Vector2(50f, 50f), 0f, Obstacles.Both);
            Box(new Vector3(-88f, 0.12f, -140f), new Vector3(14f, 0.18f, 3f), 0f, wood, false);
            for (float x = -93f; x <= -83f; x += 3.3f)
            {
                Box(new Vector3(x, 0.05f, -141.4f), new Vector3(0.3f, 0.9f, 0.3f), 0f, woodDark);
                Box(new Vector3(x, 0.05f, -138.6f), new Vector3(0.3f, 0.9f, 0.3f), 0f, woodDark);
            }
            Box(new Vector3(-104f, 0.1f, -146f), new Vector3(4f, 0.7f, 1.6f), 12f, wood);
            Box(new Vector3(-104f, 1.6f, -146f), new Vector3(0.14f, 2.6f, 0.14f), 0f, woodDark);
            Box(new Vector3(-104.4f, 2.2f, -146f), new Vector3(0.06f, 1.6f, 1.3f), 12f, plaster, false);
            Keep(-88f, -140f, 10f);
        }

        void Mountains()
        {
            Obstacles.AddBox(new Vector2(-135f, 145f), new Vector2(55f, 45f), 0f, Obstacles.Both);
            // x, z, base radius, height
            float[,] m =
            {
                { -170f, 170f, 22f, 44f }, { -140f, 160f, 18f, 36f }, { -112f, 176f, 16f, 30f }, { -168f, 126f, 16f, 28f }, { -122f, 132f, 14f, 24f },
                { -148f, 146f, 20f, 40f }, { -98f, 160f, 9f, 18f }, { -183f, 108f, 10f, 17f }, { -102f, 114f, 11f, 17f }, { -126f, 104f, 8f, 12f },
            };
            for (int i = 0; i < m.GetLength(0); i++)
            {
                float x = m[i, 0], z = m[i, 1], r = m[i, 2], h = m[i, 3];
                Material mat = i % 2 == 0 ? rock : rockB;
                Put(Prims.Cone(6), new Vector3(x, 0f, z), new Vector3(r * 2f, h, r * 2f), Quaternion.Euler(0f, R(0f, 60f), 0f), mat);
                Put(Prims.Cone(6), new Vector3(x, h * 0.66f, z), new Vector3(r * 0.74f, h * 0.35f, r * 0.74f), Quaternion.identity, snow);
                Keep(x, z, r + 2f);
            }
        }

        // ---------------------------------------------------------------- trees, rocks, flowers

        void Tree(float x, float z, Zone zone)
        {
            float sc = R(0.85f, 1.5f);
            int v = Mathf.FloorToInt(R(0f, 3f));
            bool pine = zone == Zone.DarkPineForest || (zone != Zone.AutumnForest && rng.NextDouble() < 0.3);
            if (HasKit)
            {
                Dictionary<string, Material> over = null;
                if (!pine && zone == Zone.AutumnForest)
                {
                    // the autumn forest keeps its orange leaves (zone identity), the trunks and shapes are the kit's
                    if (autumnOver == null) autumnOver = new Dictionary<string, Material>[3];
                    if (autumnOver[v] == null)
                        autumnOver[v] = new Dictionary<string, Material> { { "leaf_light", autumn[v] }, { "leaf_under", autumn[(v + 1) % 3] } };
                    over = autumnOver[v];
                }
                string nm = pine ? "Tree_Pine" : (v == 2 ? "Tree_Round_Wide" : "Tree_Round_Slender");
                PutKit(nm, new Vector3(x, 0f, z), D(0f, 360f), sc * 0.9f, over);
                Obstacles.AddCircle(new Vector2(x, z), 0.5f * sc, Obstacles.Players);
                return;
            }
            Cyl(new Vector3(x, 0f, z), 0.55f * sc, (pine ? 1.4f : 1.9f) * sc, 6, trunk);
            if (pine)
            {
                Material leaf = zone == Zone.DarkPineForest ? pines[v] : meadowLeaf[v];
                Cone(new Vector3(x, 1.0f * sc, z), 3.1f * sc, 2.4f * sc, 6, leaf);
                Cone(new Vector3(x, 2.2f * sc, z), 2.4f * sc, 2.1f * sc, 6, leaf);
                Cone(new Vector3(x, 3.3f * sc, z), 1.6f * sc, 1.8f * sc, 6, leaf);
            }
            else
            {
                Material leaf = zone == Zone.AutumnForest ? autumn[v] : meadowLeaf[v];
                Ball(new Vector3(x, 2.7f * sc, z), new Vector3(3.0f, 2.6f, 3.0f) * sc, 7, leaf);
                Ball(new Vector3(x + 0.5f * sc, 3.7f * sc, z - 0.2f * sc), new Vector3(1.9f, 1.7f, 1.9f) * sc, 6, leaf);
            }
            Obstacles.AddCircle(new Vector2(x, z), 0.5f * sc, Obstacles.Players);
        }

        void Forests()
        {
            // a jittered grid over the whole map; the density comes from the zone
            for (float gz = -MapLayout.Half + 2f; gz < MapLayout.Half - 2f; gz += 5f)
            {
                for (float gx = -MapLayout.Half + 2f; gx < MapLayout.Half - 2f; gx += 5f)
                {
                    float x = gx + R(-2f, 2f), z = gz + R(-2f, 2f);
                    Zone zn = MapLayout.ZoneAt(x, z);
                    double chance;
                    switch (zn)
                    {
                        case Zone.AutumnForest: chance = 0.8; break;
                        case Zone.DarkPineForest: chance = 0.85; break;
                        case Zone.Meadow: chance = 0.07; break;
                        case Zone.Farmland: chance = 0.02; break;
                        default: chance = 0.0; break;
                    }
                    if (rng.NextDouble() >= chance) continue;
                    if (Mathf.Abs(x) < 28f && z > -42f && z < 32f) continue;       // the crossroads clearing (also the test field)
                    if (OnRoad(x, z) || Kept(x, z, 0.8f)) continue;
                    if (Mathf.Abs(x) > MapLayout.Half - 1.5f || Mathf.Abs(z) > MapLayout.Half - 1.5f) continue;
                    Tree(x, z, zn);
                }
            }
            // boulders in the open
            for (int i = 0; i < 70; i++)
            {
                float x = R(-MapLayout.Half + 4f, MapLayout.Half - 4f), z = R(-MapLayout.Half + 4f, MapLayout.Half - 4f);
                Zone zn = MapLayout.ZoneAt(x, z);
                if (zn == Zone.Town || zn == Zone.Lake || zn == Zone.Mountain || zn == Zone.Lair) continue;
                if (OnRoad(x, z) || Kept(x, z, 1.5f)) continue;
                if (Mathf.Abs(x) < 28f && z > -42f && z < 32f) continue;
                Rock(x, z, R(0.8f, 2.4f));
            }
            // trees outside the fence, so the edge of the world is not a blank wall
            for (float gz = -MapLayout.Half - 24f; gz < MapLayout.Half + 24f; gz += 8f)
            {
                for (float gx = -MapLayout.Half - 24f; gx < MapLayout.Half + 24f; gx += 8f)
                {
                    if (Mathf.Abs(gx) < MapLayout.Half + 2f && Mathf.Abs(gz) < MapLayout.Half + 2f) continue;
                    if (rng.NextDouble() > 0.65) continue;
                    float x = gx + R(-3f, 3f), z = gz + R(-3f, 3f);
                    if (Mathf.Abs(x) < MapLayout.Half + 2.5f && Mathf.Abs(z) < MapLayout.Half + 2.5f) continue;
                    float sc = R(0.9f, 1.6f);
                    if (HasKit)
                    {
                        R(0f, 3f); R(0f, 3f);                                   // keep the main rng stream identical
                        PutKit("Tree_Pine", new Vector3(x, 0f, z), D(0f, 360f), sc * 0.9f);
                        continue;
                    }
                    Cyl(new Vector3(x, 0f, z), 0.55f * sc, 1.4f * sc, 6, trunk);
                    Cone(new Vector3(x, 1.0f * sc, z), 3.1f * sc, 2.6f * sc, 6, pines[Mathf.FloorToInt(R(0f, 3f))]);
                    Cone(new Vector3(x, 2.5f * sc, z), 2.2f * sc, 2.2f * sc, 6, pines[Mathf.FloorToInt(R(0f, 3f))]);
                }
            }
        }

        void Decoration()
        {
            // flowers in the meadow, fallen leaves in the autumn forest, pebbles everywhere in the open; none of it blocks
            for (int i = 0; i < 700; i++)
            {
                float x = R(-MapLayout.Half + 2f, MapLayout.Half - 2f), z = R(-MapLayout.Half + 2f, MapLayout.Half - 2f);
                Zone zn = MapLayout.ZoneAt(x, z);
                if (zn == Zone.Town || zn == Zone.Lake || zn == Zone.Mountain || zn == Zone.Lair || zn == Zone.Farmland || OnRoad(x, z)) continue;
                if (i % 5 == 0) Ball(new Vector3(x, 0.08f, z), new Vector3(R(0.3f, 0.6f), R(0.18f, 0.3f), R(0.3f, 0.5f)), 5, stoneDark, false);
                else if (zn == Zone.AutumnForest) Box(new Vector3(x, 0.07f, z), new Vector3(0.3f, 0.05f, 0.3f), R(0f, 90f), autumn[i % 3], false);
                else if (zn == Zone.DarkPineForest) continue;
                else Box(new Vector3(x, 0.09f, z), new Vector3(0.16f, 0.16f, 0.16f), R(0f, 90f), flowers[i % 3], false);
            }
        }

        // Small kit pieces with no collision (like the flowers and pebbles of Decoration): grass, mushrooms, stumps, stepping
        // stones, barrels. Nothing here touches Obstacles, so the collision map is exactly the one of the primitive scenery.
        void KitProps()
        {
            if (!HasKit) return;
            // grass tufts over the open ground
            for (int i = 0; i < 1100; i++)
            {
                float x = D(-MapLayout.Half + 3f, MapLayout.Half - 3f), z = D(-MapLayout.Half + 3f, MapLayout.Half - 3f);
                Zone zn = MapLayout.ZoneAt(x, z);
                if (zn == Zone.Town || zn == Zone.Lake || zn == Zone.Mountain || zn == Zone.Lair || zn == Zone.Farmland || OnRoad(x, z)) continue;
                if (zn == Zone.DarkPineForest && i % 3 != 0) continue;
                PutKit("Grass_Tuft", new Vector3(x, 0f, z), D(0f, 360f), D(1.1f, 1.9f), null, false);
            }
            // red mushrooms and tree stumps, a few of each, near where the trees are
            int mush = 0, stumps = 0;
            for (int i = 0; i < 2000 && (mush < 70 || stumps < 28); i++)
            {
                float x = D(-MapLayout.Half + 4f, MapLayout.Half - 4f), z = D(-MapLayout.Half + 4f, MapLayout.Half - 4f);
                Zone zn = MapLayout.ZoneAt(x, z);
                if (zn != Zone.Meadow && zn != Zone.AutumnForest && zn != Zone.DarkPineForest) continue;
                if (OnRoad(x, z) || Kept(x, z, 1.5f)) continue;
                if (Mathf.Abs(x) < 28f && z > -42f && z < 32f) continue;
                if (mush < 70 && i % 2 == 0) { PutKit("Mushroom_Red_Cluster", new Vector3(x, 0f, z), D(0f, 360f), D(1.4f, 2.2f), null, false); mush++; }
                else if (stumps < 28) { PutKit("Tree_Stump", new Vector3(x, 0f, z), D(0f, 360f), D(0.9f, 1.4f)); stumps++; }
            }
            // by the town houses: a path of stepping stones from the door, a barrel standing against the wall, sometimes a lying one
            for (int h = 0; h < kitHouses.Count; h++)
            {
                Vector4 k = kitHouses[h];
                Vector3 o = new Vector3(k.x, 0f, k.y);
                float yaw = k.z, d = k.w;
                for (int s = 0; s < 3; s++)
                    PutKit("Stepping_Stone", Local(o, yaw, D(-0.35f, 0.35f), 0.02f, d * 0.5f + 1.6f + s * 1.1f), D(0f, 360f), D(1.5f, 2.0f), null, false);
                PutKit("Barrel_Upright", Local(o, yaw, 2.5f, 0f, d * 0.5f + 0.5f), D(0f, 360f), 1f);
                if (h % 2 == 0) PutKit("Barrel_Lying", Local(o, yaw, 3.2f, 0f, d * 0.5f + 0.7f), yaw + D(-20f, 20f), 1f);
                PutKit("Grass_Tuft", Local(o, yaw, -2.8f, 0f, d * 0.5f + 0.5f), D(0f, 360f), 1.5f, null, false);
            }
            // a barrel at each market stall
            float[] sx = { -15f, 15f, -15f, 15f };
            float[] sz = { -141f, -163f, -163f, -141f };
            for (int i = 0; i < 4; i++)
                PutKit("Barrel_Upright", new Vector3(sx[i] + 2.4f, 0f, sz[i] + 0.2f), D(0f, 360f), 1f);
        }

        void EdgeFence()
        {
            float f = MapLayout.Half + 0.7f;
            bool kitFence = HasKit;
            for (int side = 0; side < 4 && kitFence; side++)
            {
                // kit fence pieces round the map, rails toward the inside (the piece's front is -Z, like the north side)
                Quaternion q = Quaternion.Euler(0f, side * 90f, 0f);
                int pieces = Mathf.CeilToInt(2f * f / 1.95f);
                float step = 2f * f / pieces;
                for (int i = 0; i < pieces; i++)
                    PutKit("Fence_Straight_2m", q * new Vector3(-f + step * (i + 0.5f), 0f, f), side * 90f, step / 1.95f);
            }
            for (int side = 0; side < 4 && !kitFence; side++)
            {
                Quaternion q = Quaternion.Euler(0f, side * 90f, 0f);
                for (float x = -f; x <= f + 0.01f; x += 2.53f)
                    Put(Prims.Cube, q * new Vector3(x, 0.65f, f), new Vector3(0.26f, 1.3f, 0.26f), q, woodDark);
                for (float x = -f; x < f; x += 38f)
                {
                    float len = Mathf.Min(38f, f - x);
                    Vector3 c = q * new Vector3(x + len * 0.5f, 1.0f, f);
                    Put(Prims.Cube, c, new Vector3(len, 0.14f, 0.12f), q, wood);
                    Put(Prims.Cube, new Vector3(c.x, 0.5f, c.z), new Vector3(len, 0.14f, 0.12f), q, wood);
                }
            }
            // flags on the four corners
            int bi = 0;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 p = new Vector3(sx * (MapLayout.Half - 0.6f), 0f, sz * (MapLayout.Half - 0.6f));
                    Cyl(p, 0.16f, 4.2f, 6, woodDark);
                    Box(p + new Vector3(-sx * 0.62f, 3.3f, 0f), new Vector3(1.2f, 1.5f, 0.06f), 0f, bi++ % 2 == 0 ? cloth1 : cloth2);
                    Ball(p + Vector3.up * 4.3f, new Vector3(0.3f, 0.3f, 0.3f), 6, gold);
                }
            }
        }
    }
}

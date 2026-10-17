using System;

namespace MedievalKnight
{
    // Data written by Assets/Editor/SnowBake.cs into Resources/Snow (fixed files, the same on every machine).
    // Game coordinates: x = E - 190, z = N - 190, y = height (m).
    [Serializable]
    public class SnowLayoutDto
    {
        public int version = 1;
        public string[] protos;     // prefab names (Kit.Get)
        public float[] inst;        // stride 11: proto index, x, y, z, qx, qy, qz, qw, sx, sy, sz
        public bool[] world;        // per instance: true = a map-size file with its own world coordinates (own renderer, not batched)
        public float[] obs;         // stride 7: kind (0 circle / 1 box), x, z, r or hx, hz, yawDeg, mask (1 players, 3 both)
        public string[] names;      // per instance name (windmill sails etc.)
    }

    [Serializable]
    public class SnowMeta
    {
        public float[] spawn;       // x, z
        public float[] safeCenter;  // x, z
        public float safeRadius;
        public float[] lair;        // x, z
        public float lairRadius;
        public float lairClearance;
        public float[] dummy;       // x, z
        public float[] gate;        // x, z of the castle gate
        public float[] zones;       // stride 6: zone id, minX, maxX, minZ, maxZ, tier (first match wins)
        public float[] waypoints;   // stride 2: x, z (16 points)
        public float[] signs;       // stride 2
        public string[] signText;
        public float lakeLift;
        public int gridBoxes;
        public int circleCount;
        public int boxCount;
        public int instanceCount;
    }
}

namespace MedievalKnight
{
    // The snow map's fixed data (see SnowLayoutDto / SnowMeta): height table, walkable table, spawn, lair, zones, placed pieces.
    // Loaded once from Resources/Snow; nothing here is random, so every machine has the same numbers.
    public static class SnowLayout
    {
        public const int N = 381;
        const float Off = 190f;

        static bool loaded;
        static SnowLayoutDto lay;
        static SnowMeta meta;
        static byte[] heightBytes;
        static byte[] block;

        public static bool Loaded { get { return loaded; } }
        public static SnowLayoutDto Layout { get { Load(); return lay; } }
        public static SnowMeta Meta { get { Load(); return meta; } }

        public static void Load()
        {
            if (loaded) return;
            var l = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Snow/snow_layout");
            var m = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Snow/snow_meta");
            var h = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Snow/snow_height");
            var b = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Snow/snow_block");
            if (l == null || m == null || h == null || b == null) throw new InvalidOperationException("snow map data is missing (Resources/Snow)");
            lay = UnityEngine.JsonUtility.FromJson<SnowLayoutDto>(l.text);
            meta = UnityEngine.JsonUtility.FromJson<SnowMeta>(m.text);
            heightBytes = h.bytes;
            block = b.bytes;
            if (heightBytes.Length != N * N * 2 || block.Length != N * N) throw new InvalidOperationException("snow map tables have the wrong size");
            loaded = true;
        }

        static UnityEngine.Vector3 V(float[] a) { return new UnityEngine.Vector3(a[0], 0f, a[1]); }
        public static UnityEngine.Vector3 Spawn { get { Load(); return V(meta.spawn); } }
        public static UnityEngine.Vector3 Lair { get { Load(); return V(meta.lair); } }
        public static UnityEngine.Vector3 Dummy { get { Load(); return V(meta.dummy); } }
        public static UnityEngine.Vector3 Gate { get { Load(); return V(meta.gate); } }
        public static UnityEngine.Vector3 SafeCenter { get { Load(); return V(meta.safeCenter); } }
        public static float SafeRadius { get { Load(); return meta.safeRadius; } }
        public static float LairRadius { get { Load(); return meta.lairRadius; } }

        static float Cm(int i, int j)
        {
            int k = (j * N + i) * 2;
            return ((heightBytes[k] | (heightBytes[k + 1] << 8)) - 2000) / 100f;
        }

        // Ground height at game coordinates (bilinear on the 1 m table).
        public static float GroundY(float x, float z)
        {
            if (!loaded) Load();
            float u = UnityEngine.Mathf.Clamp(x + Off, 0f, N - 1.001f), v = UnityEngine.Mathf.Clamp(z + Off, 0f, N - 1.001f);
            int i = (int)u, j = (int)v;
            float fu = u - i, fv = v - j;
            float a = Cm(i, j), b = Cm(i + 1, j), c = Cm(i, j + 1), d = Cm(i + 1, j + 1);
            return (a + (b - a) * fu) * (1f - fv) + (c + (d - c) * fu) * fv;
        }

        // 0 free, 1 slope / edge, 2 water, 3 mountain, 4 castle, 5 no ground
        public static int BlockAt(float x, float z)
        {
            if (!loaded) Load();
            int i = UnityEngine.Mathf.RoundToInt(x + Off), j = UnityEngine.Mathf.RoundToInt(z + Off);
            if (i < 0 || j < 0 || i >= N || j >= N) return 5;
            return block[j * N + i];
        }

        public static bool Walkable(float x, float z) { return BlockAt(x, z) == 0; }

        public static bool InTown(float x, float z)
        {
            if (!loaded) Load();
            float dx = x - meta.safeCenter[0], dz = z - meta.safeCenter[1];
            return dx * dx + dz * dz <= meta.safeRadius * meta.safeRadius;
        }

        // design zone id (0 A meadow, 1 B pine valley, 2 C cliff ascent, 3 D1 low snow, 4 D2 high snow, 5 D3 castle), -1 = none
        public static int RectZone(float x, float z, out float tier)
        {
            if (!loaded) Load();
            float[] zs = meta.zones;
            for (int i = 0; i + 5 < zs.Length; i += 6)
                if (x >= zs[i + 1] && x <= zs[i + 2] && z >= zs[i + 3] && z <= zs[i + 4]) { tier = zs[i + 5]; return (int)zs[i]; }
            tier = 2f;
            return -1;
        }
    }
}

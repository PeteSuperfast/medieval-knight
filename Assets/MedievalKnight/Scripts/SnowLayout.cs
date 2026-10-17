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

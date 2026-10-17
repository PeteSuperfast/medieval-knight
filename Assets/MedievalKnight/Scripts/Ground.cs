using UnityEngine;

namespace MedievalKnight
{
    // Height of the ground at (x, z) in the current biome. The meadow is flat (0); the snow map uses the baked table.
    // Only the picture follows it (knights, enemies, loot stand on it, the camera rises with it): fighting, ranges and collision
    // stay flat 2D on the XZ plane.
    public static class Ground
    {
        public static float Y(float x, float z)
        {
            return MapLayout.Current == Biome.Snow ? SnowLayout.GroundY(x, z) : 0f;
        }

        // The highest ground within r of a point (flat ground pictures that must not sink into a slope: rings, markers).
        public static float Top(float x, float z, float r)
        {
            float y = Y(x, z);
            if (MapLayout.Current != Biome.Snow || r <= 0.01f) return y;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                y = Mathf.Max(y, Y(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r));
            }
            return y;
        }

        public static float Y(Vector3 p) { return Y(p.x, p.z); }

        public static Vector3 Snap(Vector3 p)
        {
            p.y = Y(p.x, p.z);
            return p;
        }
    }
}

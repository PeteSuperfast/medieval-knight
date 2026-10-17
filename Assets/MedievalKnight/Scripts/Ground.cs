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
            return 0f;
        }

        public static float Y(Vector3 p) { return Y(p.x, p.z); }

        public static Vector3 Snap(Vector3 p)
        {
            p.y = Y(p.x, p.z);
            return p;
        }
    }
}

namespace MedievalKnight
{
    // The kind of world a run is played in. The base (central lawn of the meadow scene) is always Meadow; a world is entered in
    // the biome the deciding machine (single player / host) picked, and old worlds keep theirs (OldWorld.biome).
    public enum Biome { Meadow = 0, Snow = 1 }

    public static class Biomes
    {
        public const int Count = 2;

        public static bool Valid(int v) { return v >= 0 && v < Count; }

        // Pure and stable: the same seed always gives the same biome (tests, replays). New real worlds use Roll.
        public static Biome FromSeed(int seed) { return (Biome)(int)(((uint)seed >> 16) % (uint)Count); }

        // Host / single player only, when a new world is made (50 : 50).
        public static Biome Roll(System.Random rng) { return (Biome)rng.Next(Count); }

        public static string Label(Biome b) { return b == Biome.Snow ? "SNOW" : "MEADOW"; }
    }
}

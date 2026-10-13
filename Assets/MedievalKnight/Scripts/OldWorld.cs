using System.Collections.Generic;

namespace MedievalKnight
{
    // Phase 2: a world where the player fell. Plain data (no Unity objects), saved with the Loadout. The map is the same
    // one every time; "seed" is only the world's identity (its #CODE) and what phase 3 will pick a biome from.
    public class OldWorld
    {
        public const int StartLeft = 3;                      // returns home (from any world) before it crumbles
        public const int MaxPile = 48;                       // items in one pile (the rest is lost)
        public const int MaxWorlds = 99;                     // kept on loading a save
        public const float HpMul = 1.5f, DmgMul = 1.25f, PackMul = 1.5f;   // enemy health / damage / pack size
        public const int GoneForever = 0;

        public int seed;                                     // never 0; unique inside one save
        public long fellUnix;                                // when the player fell (display only)
        public int left = StartLeft;                         // successful returns left before it crumbles
        public float x, z;                                   // the pile
        public readonly List<Item> pile = new List<Item>();

        // "#A3F2": the low 16 bits of the seed.
        public static string CodeOf(int seed) { return "#" + (seed & 0xFFFF).ToString("X4"); }
        public string Code { get { return CodeOf(seed); } }
    }

    // A world that crumbled (or was closed by the safety limits): what the player is told at the base.
    public struct CrumbNote
    {
        public int seed;
        public int items;                                    // lost forever with it
        public string Code { get { return OldWorld.CodeOf(seed); } }
    }

    // What the end of a run did to the world it was played in.
    public enum WorldNote { None = 0, Created, Added, Recovered, StillInPile, Crumbled }

    // One line of the OLD WORLDS block of the summary: a counter that went down.
    public struct CrumbLine
    {
        public int seed, before, after, itemsLost;
        public bool crumbled;
        public string Code { get { return OldWorld.CodeOf(seed); } }
    }
}

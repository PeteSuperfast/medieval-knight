using System.Collections.Generic;

namespace MedievalKnight
{
    // Phase 4: one heap of lost items at one spot of an old world. A world has several (one per place somebody fell).
    // A pile never moves; a drop within OldWorld.MergeRadius of it joins it. `id` runs per world and is not saved
    // (it is what the network messages call the pile).
    public class Pile
    {
        public int id;
        public float x, z;
        public readonly List<Item> items = new List<Item>();
    }

    // Phase 2: a world where the player fell. Plain data (no Unity objects), saved with the Loadout. The map is the same
    // one every time; "seed" is only the world's identity (its #CODE) and what phase 3 will pick a biome from.
    public class OldWorld
    {
        public const int StartLeft = 3;                      // returns home (from any world) before it crumbles
        public const int MaxPile = 48;                       // items in one world (all its piles together; the rest is lost)
        public const int MaxWorlds = 99;                     // kept on loading a save
        public const float MergeRadius = 3f;                 // a drop this close to a pile joins it
        public const float HpMul = 1.5f, DmgMul = 1.25f, PackMul = 1.5f;   // enemy health / damage / pack size
        public const int GoneForever = 0;

        public int seed;                                     // never 0; unique inside one save
        public long fellUnix;                                // when the player fell (display only)
        public int left = StartLeft;                         // successful returns left before it crumbles
        public float x, z;                                   // where the first pile was made (a new pile through `pile` starts here)
        public readonly List<Pile> piles = new List<Pile>();
        int nextPileId = 1;

        // "#A3F2": the low 16 bits of the seed.
        public static string CodeOf(int seed) { return "#" + (seed & 0xFFFF).ToString("X4"); }
        public string Code { get { return CodeOf(seed); } }

        // Items in every pile together.
        public int ItemCount
        {
            get
            {
                int n = 0;
                foreach (Pile p in piles) n += p.items.Count;
                return n;
            }
        }

        // The first pile's items (made at x, z when there is none yet): the single-pile view of a world that
        // old code and tests use. Several piles: this is only the first one.
        public List<Item> pile
        {
            get
            {
                if (piles.Count == 0) NewPile(x, z);
                return piles[0].items;
            }
        }

        public Pile NewPile(float px, float pz)
        {
            var p = new Pile { id = nextPileId++, x = px, z = pz };
            piles.Add(p);
            return p;
        }

        // A pile that was made earlier (loading a save): a new id.
        public Pile AddLoaded(Pile p)
        {
            p.id = nextPileId++;
            piles.Add(p);
            return p;
        }

        public Pile PileById(int id)
        {
            foreach (Pile p in piles) if (p.id == id) return p;
            return null;
        }

        // The pile closest to (px, pz) that is within MergeRadius, otherwise null.
        public Pile PileNear(float px, float pz)
        {
            Pile best = null;
            float bd = MergeRadius * MergeRadius + 0.0001f;
            foreach (Pile p in piles)
            {
                float dx = p.x - px, dz = p.z - pz, d = dx * dx + dz * dz;
                if (d <= bd) { bd = d; best = p; }
            }
            return best;
        }

        // Lays items down at (px, pz): they join the pile within MergeRadius or start a new one. Over MaxPile
        // (all piles of the world together) they go into `overflow` instead and are lost. Returns the pile used.
        public Pile AddDrop(float px, float pz, List<Item> items, List<Item> overflow)
        {
            if (items == null || items.Count == 0) return null;
            Pile target = PileNear(px, pz);
            bool made = false;
            if (target == null) { target = NewPile(px, pz); made = true; }
            int count = ItemCount;
            foreach (Item it in items)
            {
                if (count < MaxPile) { target.items.Add(it); count++; }
                else if (overflow != null) overflow.Add(it);
            }
            if (made && target.items.Count == 0) { piles.Remove(target); return null; }
            if (piles.Count == 1) { x = piles[0].x; z = piles[0].z; }
            return target;
        }

        public void RemoveEmptyPiles()
        {
            for (int i = piles.Count - 1; i >= 0; i--) if (piles[i].items.Count == 0) piles.RemoveAt(i);
        }

        // Every item of every pile, pile after pile.
        public List<Item> AllItems()
        {
            var l = new List<Item>();
            foreach (Pile p in piles) l.AddRange(p.items);
            return l;
        }

        // The pile that still holds something and is nearest to (px, pz).
        public Pile Nearest(float px, float pz)
        {
            Pile best = null;
            float bd = float.MaxValue;
            foreach (Pile p in piles)
            {
                if (p.items.Count == 0) continue;
                float dx = p.x - px, dz = p.z - pz, d = dx * dx + dz * dz;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }
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

    // One place where somebody's carry lies down (a fall / a wipe / a player who left).
    public class Drop
    {
        public float x, z;
        public string who = "";
        public readonly List<Item> items = new List<Item>();
    }

    // One line of the team summary (G3): who ended how, and what happened to what they carried.
    public struct TeamLine
    {
        public string name;
        public int status;                                   // TeamRun.Survived / Fell / Left
        public int items;
    }
}

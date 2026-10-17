using System.Collections.Generic;

namespace MedievalKnight
{
    // Phase 1 loop data: what the player owns at the base (stash), what he takes into the world (carry) and
    // how a run ends. Plain classes, no Unity objects: the rules are testable without a scene (see SelfTest.cs).

    public enum ItemKind : byte { None = 0, Weapon = 1, Food = 2 }

    // One slot's content. Weapon: id = index into Weapons.All, plus 0..Weapons.MaxPlus. Food: id = index into Foods.All.
    public struct Item
    {
        public ItemKind kind;
        public int id, plus;

        public static readonly Item None = new Item();
        public static Item Weapon(int id, int plus = 0) { return new Item { kind = ItemKind.Weapon, id = id, plus = plus }; }
        public static Item Food(int id) { return new Item { kind = ItemKind.Food, id = id }; }

        public bool IsNone { get { return kind == ItemKind.None; } }
        public bool IsWeapon { get { return kind == ItemKind.Weapon; } }

        public bool Valid
        {
            get
            {
                if (kind == ItemKind.Weapon) return id >= 0 && id < Weapons.All.Length && plus >= 0 && plus <= Weapons.MaxPlus;
                if (kind == ItemKind.Food) return id >= 0 && id < Foods.All.Length && plus == 0;
                return kind == ItemKind.None;
            }
        }

        // "Iron Sword +2", "Apple"
        public string Name
        {
            get
            {
                if (kind == ItemKind.Weapon) return Weapons.All[id].name + (plus > 0 ? " +" + plus : "");
                if (kind == ItemKind.Food) return Foods.All[id].name;
                return "";
            }
        }

        public string BaseName
        {
            get { return kind == ItemKind.Weapon ? Weapons.All[id].name : kind == ItemKind.Food ? Foods.All[id].name : ""; }
        }

        public bool Same(Item o) { return kind == o.kind && id == o.id && plus == o.plus; }
    }

    public enum Section { Weapons = 0, Foods = 1, Supplies = 2 }

    public enum BagResult { Added, Merged, Full }

    // Three groups of fixed slots: Weapons, Foods, Supplies (Supplies takes anything). An empty slot has kind None.
    public class ItemBag
    {
        public readonly Item[] weapons, foods, supplies;

        public ItemBag(int capWeapons, int capFoods, int capSupplies)
        {
            weapons = new Item[capWeapons];
            foods = new Item[capFoods];
            supplies = new Item[capSupplies];
        }

        public Item[] Slots(Section s) { return s == Section.Weapons ? weapons : s == Section.Foods ? foods : supplies; }

        public int Count(Section s)
        {
            int n = 0;
            foreach (Item it in Slots(s)) if (!it.IsNone) n++;
            return n;
        }

        public int Total { get { return Count(Section.Weapons) + Count(Section.Foods) + Count(Section.Supplies); } }

        public bool HasWeapon
        {
            get
            {
                foreach (Item it in weapons) if (it.IsWeapon) return true;
                foreach (Item it in supplies) if (it.IsWeapon) return true;
                return false;
            }
        }

        public bool HasWeaponId(int id)
        {
            foreach (Item it in weapons) if (it.IsWeapon && it.id == id) return true;
            foreach (Item it in supplies) if (it.IsWeapon && it.id == id) return true;
            return false;
        }

        // The "+N" rule of v1 (a duplicate refines the weapon by +1) applied to two stacks: plus = min(5, a + b + 1).
        public static int MergePlus(int a, int b) { return System.Math.Min(Weapons.MaxPlus, a + b + 1); }

        static int FirstEmpty(Item[] arr)
        {
            for (int i = 0; i < arr.Length; i++) if (arr[i].IsNone) return i;
            return -1;
        }

        // Weapon: merges with the same weapon already in Weapons / Supplies, else the first free Weapons slot, else
        // the first free Supplies slot. Food: free Foods slot, else free Supplies slot. Nothing is lost on Full.
        public BagResult Add(Item it)
        {
            if (it.IsNone) return BagResult.Added;
            if (it.IsWeapon)
            {
                for (int i = 0; i < weapons.Length; i++)
                    if (weapons[i].IsWeapon && weapons[i].id == it.id) { weapons[i].plus = MergePlus(weapons[i].plus, it.plus); return BagResult.Merged; }
                for (int i = 0; i < supplies.Length; i++)
                    if (supplies[i].IsWeapon && supplies[i].id == it.id) { supplies[i].plus = MergePlus(supplies[i].plus, it.plus); return BagResult.Merged; }
                int w = FirstEmpty(weapons);
                if (w >= 0) { weapons[w] = it; return BagResult.Added; }
            }
            else
            {
                int f = FirstEmpty(foods);
                if (f >= 0) { foods[f] = it; return BagResult.Added; }
            }
            int s = FirstEmpty(supplies);
            if (s >= 0) { supplies[s] = it; return BagResult.Added; }
            return BagResult.Full;
        }

        // Would Add succeed? (merging counts as success)
        public bool CanAdd(Item it)
        {
            if (it.IsNone) return true;
            if (it.IsWeapon && HasWeaponId(it.id)) return true;
            if (it.IsWeapon && FirstEmpty(weapons) >= 0) return true;
            if (!it.IsWeapon && FirstEmpty(foods) >= 0) return true;
            return FirstEmpty(supplies) >= 0;
        }

        public void Clear()
        {
            for (int i = 0; i < weapons.Length; i++) weapons[i] = Item.None;
            for (int i = 0; i < foods.Length; i++) foods[i] = Item.None;
            for (int i = 0; i < supplies.Length; i++) supplies[i] = Item.None;
        }

        // Every stored item, Weapons first, then Foods, then Supplies.
        public List<Item> All()
        {
            var list = new List<Item>();
            foreach (Item it in weapons) if (!it.IsNone) list.Add(it);
            foreach (Item it in foods) if (!it.IsNone) list.Add(it);
            foreach (Item it in supplies) if (!it.IsNone) list.Add(it);
            return list;
        }

        // After loading: invalid slots are emptied and two stacks of the same weapon become one.
        public void Normalize()
        {
            foreach (Section sec in new[] { Section.Weapons, Section.Foods, Section.Supplies })
            {
                Item[] arr = Slots(sec);
                for (int i = 0; i < arr.Length; i++)
                {
                    if (!arr[i].Valid) { arr[i] = Item.None; continue; }
                    if (sec == Section.Weapons && !arr[i].IsNone && !arr[i].IsWeapon) arr[i] = Item.None;
                    if (sec == Section.Foods && !arr[i].IsNone && arr[i].kind != ItemKind.Food) arr[i] = Item.None;
                }
            }
            var seen = new Dictionary<int, int[]>();       // weapon id -> (section, index) of the first stack
            foreach (Section sec in new[] { Section.Weapons, Section.Supplies })
            {
                Item[] arr = Slots(sec);
                for (int i = 0; i < arr.Length; i++)
                {
                    if (!arr[i].IsWeapon) continue;
                    int[] at;
                    if (!seen.TryGetValue(arr[i].id, out at)) { seen[arr[i].id] = new[] { (int)sec, i }; continue; }
                    Item[] first = Slots((Section)at[0]);
                    first[at[1]].plus = MergePlus(first[at[1]].plus, arr[i].plus);
                    arr[i] = Item.None;
                }
            }
        }
    }

    // One line of the end-of-run summary.
    public struct ResultLine
    {
        public Item item;
        public bool isNew;
    }

    // What a run did to the player's things; the summary screen is drawn from this.
    public class RunResult
    {
        public bool victory, abandoned, ironRestored;
        public readonly List<ResultLine> brought = new List<ResultLine>();    // victory: went into the stash
        public readonly List<ResultLine> leftBehind = new List<ResultLine>(); // victory: stash was full, gone for good
        public readonly List<ResultLine> lost = new List<ResultLine>();       // death / abandoned
        public readonly List<ResultLine> safe = new List<ResultLine>();       // death: was in the stash, not taken along
        public int stashWeapons, stashFoods;                                  // stash usage after the run
        // phase 2 (old worlds)
        public WorldNote worldNote;                                           // what happened to the world this run was played in
        public int worldSeed;                                                 // that world (Created / Added / Recovered / StillInPile)
        public int pileCount;                                                 // items in its piles (all of them) after the run
        public int pilesN;                                                    // how many piles it has after the run
        public readonly List<TeamLine> team = new List<TeamLine>();           // online: one line per player (G3)
        public int teamSize;                                                  // online: how many played this run (0 = single player)
        public int worldLeft;                                                 // returns it has left after the run
        public bool inOldWorld;                                               // the run was played in an old world
        public int worldCount;                                                // old worlds that exist after the run
        public readonly List<ResultLine> overflow = new List<ResultLine>();   // did not fit in the pile (MaxPile): gone
        public readonly List<CrumbLine> crumbs = new List<CrumbLine>();       // victory: every old world's counter
        public string WorldCode { get { return OldWorld.CodeOf(worldSeed); } }
    }

    public class Loadout
    {
        // [weapons, foods, supplies]
        public static readonly int[] StashCaps = { 12, 8, 16 };
        public static readonly int[] CarryCaps = { 2, 3, 4 };

        public ItemBag stash = new ItemBag(StashCaps[0], StashCaps[1], StashCaps[2]);
        public ItemBag carry = new ItemBag(CarryCaps[0], CarryCaps[1], CarryCaps[2]);
        public int heldSlot;                   // index into carry.weapons
        public bool inRun;                     // a run is open (set when entering the world, cleared when it ends)
        public bool introShown;                // the "everything you carry is lost" line was shown once
        bool[] ownedAtStart;                   // weapon ids known when the run began (to mark NEW)

        // ---- phase 2: old worlds
        public readonly List<OldWorld> worlds = new List<OldWorld>();
        public int runWorld;                   // the old world the open run is played in (0 = a new world)
        public Biome runBiome = Biome.Meadow;  // the kind of map the open run is played in (an old world's own; a new world's is picked when it is entered)
        public float runX, runZ;               // where the knight last was in the open run (autosaved)
        public bool runPosValid;
        public bool runCoop;                   // phase 4: the open run is an online run of a client (its world and piles are the host's)
        public readonly List<CrumbNote> unseen = new List<CrumbNote>();   // crumbled worlds the player has not been told about at the base
        public string trimNote = "";           // set by Sanitize when a damaged / edited save lost worlds or loot

        public OldWorld Find(int seed)
        {
            if (seed == 0) return null;
            foreach (OldWorld w in worlds) if (w.seed == seed) return w;
            return null;
        }

        // Fewest returns left first, then the most recent fall.
        public List<OldWorld> Sorted()
        {
            var list = new List<OldWorld>(worlds);
            list.Sort((a, b) => a.left != b.left ? a.left.CompareTo(b.left) : b.fellUnix.CompareTo(a.fellUnix));
            return list;
        }

        int seedCounter;                       // makes two worlds made in the same second (and with the same count of worlds) differ

        int NewSeed(long now)
        {
            var rng = new System.Random(unchecked((int)(now * 31 + worlds.Count * 7919 + (seedCounter++) * 104729 + 17)));
            for (int tries = 0; tries < 2000; tries++)
            {
                int s = rng.Next(1, int.MaxValue);
                if ((s & 0xFFFF) == 0) continue;
                bool clash = false;
                foreach (OldWorld w in worlds) if ((w.seed & 0xFFFF) == (s & 0xFFFF)) clash = true;
                if (!clash) return s;
            }
            return worlds.Count + 1;
        }

        // A brand-new save: the stash holds one Iron Sword and nothing else.
        public static Loadout NewGame()
        {
            var l = new Loadout();
            l.stash.weapons[0] = Item.Weapon(Weapons.Iron);
            return l;
        }

        public Item Held { get { return heldSlot >= 0 && heldSlot < carry.weapons.Length ? carry.weapons[heldSlot] : Item.None; } }
        public bool CanEnter { get { return Held.IsWeapon; } }

        // heldSlot must point at a Weapons slot that is not empty; otherwise the other one, otherwise 0.
        public void EnsureHeld()
        {
            if (heldSlot >= 0 && heldSlot < carry.weapons.Length && carry.weapons[heldSlot].IsWeapon) return;
            for (int i = 0; i < carry.weapons.Length; i++)
                if (carry.weapons[i].IsWeapon) { heldSlot = i; return; }
            heldSlot = 0;
        }

        public bool SetHeld(int slot)
        {
            if (slot < 0 || slot >= carry.weapons.Length || !carry.weapons[slot].IsWeapon) return false;
            heldSlot = slot;
            return true;
        }

        // Stash -> carry. Returns false when the carry has no room for it (Carry is full); nothing changes then.
        public bool MoveToCarry(Section sec, int index)
        {
            return Move(stash, carry, sec, index);
        }

        public bool MoveToStash(Section sec, int index)
        {
            return Move(carry, stash, sec, index);
        }

        static bool Move(ItemBag from, ItemBag to, Section sec, int index)
        {
            Item[] arr = from.Slots(sec);
            if (index < 0 || index >= arr.Length || arr[index].IsNone) return false;
            Item it = arr[index];
            if (!to.CanAdd(it)) return false;
            arr[index] = Item.None;
            to.Add(it);
            return true;
        }

        // A weapon that landed in a Supplies slot (the Weapons slots were full) moves into a free Weapons slot, so it can
        // be held. Returns false when no Weapons slot is free.
        public bool MoveWithinCarry(int supplyIndex)
        {
            if (supplyIndex < 0 || supplyIndex >= carry.supplies.Length || !carry.supplies[supplyIndex].IsWeapon) return false;
            for (int i = 0; i < carry.weapons.Length; i++)
                if (carry.weapons[i].IsNone) { carry.weapons[i] = carry.supplies[supplyIndex]; carry.supplies[supplyIndex] = Item.None; return true; }
            return false;
        }

        // The first stash weapon goes into the carry when the carry has no weapon (used when arriving at the base).
        public void AutoPack()
        {
            foreach (Item it in carry.weapons)
                if (it.IsWeapon) { EnsureHeld(); return; }              // a weapon is packed already: just hold it
            bool moved = false;
            for (int i = 0; i < stash.weapons.Length && !moved; i++)
                if (stash.weapons[i].IsWeapon) moved = MoveToCarry(Section.Weapons, i);
            for (int i = 0; i < stash.supplies.Length && !moved; i++)
                if (stash.supplies[i].IsWeapon) moved = MoveToCarry(Section.Supplies, i);
            EnsureHeld();
        }

        // The weapon ids owned right now (stash + carry), used to mark NEW at the end of a run.
        bool[] OwnedIds()
        {
            var owned = new bool[Weapons.All.Length];
            foreach (ItemBag bag in new[] { stash, carry })
            {
                foreach (Item it in bag.weapons) if (it.IsWeapon) owned[it.id] = true;
                foreach (Item it in bag.supplies) if (it.IsWeapon) owned[it.id] = true;
            }
            return owned;
        }

        // Entering the world: the run is open from here on.
        // coop: an online run of a client - the world (and its piles) belong to the host, so runWorld stays 0.
        public void BeginRun(int oldWorldSeed = 0, bool coop = false, Biome biome = Biome.Meadow)
        {
            EnsureHeld();
            ownedAtStart = OwnedIds();
            inRun = true;
            runCoop = coop;
            runWorld = !coop && Find(oldWorldSeed) != null ? oldWorldSeed : 0;
            OldWorld known = Find(runWorld);
            runBiome = known != null ? known.biome : biome;       // an old world keeps its own map
            runPosValid = false;
        }

        // Items taken from a pile go into the carry by the normal rules (merge +N, own group, Supplies); a full carry
        // leaves the item in the pile. A pile that runs empty is removed from its world. Returns false when it did not fit.
        public bool TakeFromPile(OldWorld w, Pile p, int index)
        {
            if (w == null || p == null || index < 0 || index >= p.items.Count) return false;
            Item it = p.items[index];
            if (!carry.CanAdd(it)) return false;
            carry.Add(it);
            p.items.RemoveAt(index);
            if (p.items.Count == 0) w.piles.Remove(p);
            EnsureHeld();
            return true;
        }

        // Everything of that pile that fits, in pile order. Returns how many items were taken.
        public int TakeAll(OldWorld w, Pile p)
        {
            if (w == null || p == null) return 0;
            int n = 0;
            for (int i = 0; i < p.items.Count;)
            {
                if (TakeFromPile(w, p, i)) n++;
                else i++;
            }
            return n;
        }

        // Every weapon the player owns anywhere? If not, a free Iron Sword goes into the stash. Returns true when it did.
        public bool EnsureIron()
        {
            if (stash.HasWeapon || carry.HasWeapon) return false;
            if (stash.Add(Item.Weapon(Weapons.Iron)) == BagResult.Full) return false;
            return true;
        }

        static ResultLine Line(Item it, bool isNew) { return new ResultLine { item = it, isNew = isNew }; }

        // Reached the portal: everything carried goes into the stash; what does not fit is gone (LEFT BEHIND).
        // decay: false for a client of an online team - the old worlds belong to the host, so the counters of the ones in
        // this save (single player ones) are not touched and no world is closed.
        public RunResult Survive(bool decay = true)
        {
            var r = new RunResult { victory = true };
            bool[] before = ownedAtStart ?? OwnedIds();
            foreach (Item it in carry.All())
            {
                bool isNew = it.IsWeapon && !before[it.id];
                if (stash.Add(it) == BagResult.Full) r.leftBehind.Add(Line(it, false));
                else r.brought.Add(Line(it, isNew));
            }
            carry.Clear();
            if (decay) SurviveWorlds(r);
            Finish(r);
            return r;
        }

        // A client of an online team lost its run (the team fell, he left, the host closed): everything carried is gone
        // from him (it lies in the host's world as piles, or nowhere). His own worlds, counters and stash are not touched.
        public RunResult LoseCarry(bool abandoned)
        {
            var r = new RunResult { victory = false, abandoned = abandoned };
            foreach (Item it in carry.All()) r.lost.Add(Line(it, false));
            carry.Clear();
            foreach (Item it in stash.All()) r.safe.Add(Line(it, false));
            r.worldCount = worlds.Count;
            Finish(r);
            return r;
        }

        // The old worlds after a successful return: the world played in is closed when its pile is empty, then every
        // counter goes down (DecayWorlds).
        void SurviveWorlds(RunResult r)
        {
            OldWorld cur = Find(runWorld);
            if (cur != null)
            {
                r.inOldWorld = true;
                r.worldSeed = cur.seed;
                if (cur.ItemCount == 0)
                {
                    worlds.Remove(cur);                                   // everything recovered: closed, not a crumble
                    r.worldNote = WorldNote.Recovered;
                }
            }
            DecayWorlds(r, null);
            if (cur != null && worlds.Contains(cur))
            {
                r.worldNote = WorldNote.StillInPile;
                r.pileCount = cur.ItemCount;
                r.pilesN = cur.piles.Count;
                r.worldLeft = cur.left;
            }
        }

        // Every way back to the base (alive, dead, abandoned) takes one off the counter of each old world that existed
        // before this run (the world just made by this death, `skip`, is not touched). At 0 the world, its pile and
        // whatever lay in it are gone for good (`unseen` tells the base). Fills r.crumbs. Returns the worlds that crumbled.
        List<OldWorld> DecayWorlds(RunResult r, OldWorld skip)
        {
            var gone = new List<OldWorld>();
            foreach (OldWorld w in new List<OldWorld>(worlds))
            {
                if (w == skip) continue;
                var line = new CrumbLine { seed = w.seed, before = w.left, after = w.left - 1, itemsLost = w.ItemCount };
                w.left--;
                if (w.left <= 0)
                {
                    line.crumbled = true;
                    line.after = 0;
                    worlds.Remove(w);
                    unseen.Add(new CrumbNote { seed = w.seed, items = w.ItemCount });
                    gone.Add(w);
                }
                r.crumbs.Add(line);
            }
            r.crumbs.Sort((a, b) => a.after != b.after ? a.after.CompareTo(b.after) : a.seed.CompareTo(b.seed));
            return gone;
        }

        public static long NowUnix() { return System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }

        // Died (or left the game mid-run) with no known place: everything carried lies at the town gate.
        public RunResult Die(bool abandoned)
        {
            UnityEngine.Vector3 home = MapLayout.TownSpawnOf(runBiome);
            return Die(abandoned, home.x, home.z, NowUnix());
        }

        // Died / left the game at (x, z): everything carried is gone from the knight and lies there - in the old world
        // he was playing in (it joins a pile within 3 m of that spot, else a new pile; piles never move) or in a new old
        // world. Then the old counters go down.
        public RunResult Die(bool abandoned, float x, float z, long nowUnix)
        {
            var d = new Drop { x = x, z = z };
            d.items.AddRange(carry.All());
            return Fall(new List<Drop> { d }, abandoned, nowUnix);
        }

        // The end of a run that was lost: every drop (one per player who fell, the host's own carry included - it is
        // cleared here) lies where its player was. One old world holds them all: the world the run was played in, or a new
        // one at the first drop. Then the old counters go down (not the new world's).
        public RunResult Fall(List<Drop> drops, bool abandoned, long nowUnix)
        {
            var r = new RunResult { victory = false, abandoned = abandoned };
            int total = 0;
            Drop first = null;
            foreach (Drop d in drops)
            {
                foreach (Item it in d.items) r.lost.Add(Line(it, false));
                total += d.items.Count;
                if (first == null && d.items.Count > 0) first = d;
            }
            carry.Clear();
            foreach (Item it in stash.All()) r.safe.Add(Line(it, false));

            OldWorld w = Find(runWorld), created = null;
            if (w != null)
            {
                r.inOldWorld = true;
                if (total > 0) r.worldNote = WorldNote.Added;
            }
            else if (total > 0)
            {
                w = new OldWorld { seed = NewSeed(nowUnix), fellUnix = nowUnix, left = OldWorld.StartLeft, x = first.x, z = first.z, biome = runBiome };
                worlds.Add(w);
                created = w;
                r.worldNote = WorldNote.Created;
            }
            if (w != null)
            {
                var over = new List<Item>();
                foreach (Drop d in drops) w.AddDrop(d.x, d.z, d.items, over);
                foreach (Item it in over) r.overflow.Add(Line(it, false));
                r.worldSeed = w.seed;
                r.pileCount = w.ItemCount;
                r.pilesN = w.piles.Count;
                r.worldLeft = w.left;
            }
            // the way back counts: every old world that existed before this death goes down by one (the new one does not)
            List<OldWorld> gone = DecayWorlds(r, created);
            if (w != null)
            {
                if (gone.Contains(w)) r.worldNote = WorldNote.Crumbled;     // died inside a world that crumbled: pile gone too
                r.worldLeft = w.left < 0 ? 0 : w.left;
            }
            Finish(r);
            return r;
        }

        void Finish(RunResult r)
        {
            r.ironRestored = EnsureIron();
            if (r.ironRestored && !r.victory)
            {
                r.safe.Clear();
                foreach (Item it in stash.All()) r.safe.Add(Line(it, false));
            }
            r.stashWeapons = stash.Count(Section.Weapons) + CountWeaponsInSupplies(stash);
            r.stashFoods = stash.Count(Section.Foods);
            r.worldCount = worlds.Count;
            inRun = false;
            runCoop = false;
            runWorld = 0;
            runBiome = Biome.Meadow;
            runPosValid = false;
            ownedAtStart = null;
            heldSlot = 0;
        }

        static int CountWeaponsInSupplies(ItemBag b)
        {
            int n = 0;
            foreach (Item it in b.supplies) if (it.IsWeapon) n++;
            return n;
        }

        // After loading a file: everything inside the tables, no duplicates, always at least one weapon.
        public void Sanitize()
        {
            stash.Normalize();
            carry.Normalize();
            // a weapon that is both in the stash and in the carry: keep the carried one. It cannot happen through the
            // game, but a hand-edited file could do it. Only with no run open: during a run the carry may hold a
            // pick-up of a weapon that is also at home (the game allows it) and the stash copy must stay.
            if (!inRun)
            {
                foreach (Item it in new List<Item>(carry.All()))
                {
                    if (!it.IsWeapon) continue;
                    for (int i = 0; i < stash.weapons.Length; i++)
                        if (stash.weapons[i].IsWeapon && stash.weapons[i].id == it.id) stash.weapons[i] = Item.None;
                    for (int i = 0; i < stash.supplies.Length; i++)
                        if (stash.supplies[i].IsWeapon && stash.supplies[i].id == it.id) stash.supplies[i] = Item.None;
                }
            }
            SanitizeWorlds();
            EnsureHeld();
            EnsureIron();
        }

        // Old worlds from a file: ids unique, counters 1..3, piles of valid items (at most MaxPile), at most MaxWorlds.
        void SanitizeWorlds()
        {
            trimNote = "";
            var seen = new HashSet<int>();
            var keep = new List<OldWorld>();
            bool trimmedPile = false;
            foreach (OldWorld w in worlds)
            {
                if (w == null || w.seed == 0 || !seen.Add(w.seed)) continue;
                if (!Biomes.Valid((int)w.biome)) w.biome = Biome.Meadow;     // a biome this build does not know: the meadow
                if (w.left <= 0) continue;                               // already expired
                if (w.left > OldWorld.StartLeft) w.left = OldWorld.StartLeft;
                float lim = Game.ArenaHalf - 4f;
                if (float.IsNaN(w.x) || float.IsNaN(w.z) || float.IsInfinity(w.x) || float.IsInfinity(w.z)) { UnityEngine.Vector3 ts = MapLayout.TownSpawnOf(w.biome); w.x = ts.x; w.z = ts.z; }
                w.x = System.Math.Max(-lim, System.Math.Min(lim, w.x));
                w.z = System.Math.Max(-lim, System.Math.Min(lim, w.z));
                // piles: valid items only, every spot inside the map, all of them together at most MaxPile items,
                // no empty pile (a world may hold none: it closes at the next return)
                var old = new List<Pile>(w.piles);
                w.piles.Clear();
                int count = 0;
                foreach (Pile p in old)
                {
                    if (p == null) continue;
                    if (float.IsNaN(p.x) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.z)) { p.x = w.x; p.z = w.z; }
                    p.x = System.Math.Max(-lim, System.Math.Min(lim, p.x));
                    p.z = System.Math.Max(-lim, System.Math.Min(lim, p.z));
                    var items = new List<Item>();
                    foreach (Item it in p.items)
                    {
                        if (it.IsNone || !it.Valid) continue;
                        if (count >= OldWorld.MaxPile) { trimmedPile = true; continue; }
                        items.Add(it);
                        count++;
                    }
                    p.items.Clear();
                    p.items.AddRange(items);
                    if (p.items.Count > 0) w.AddLoaded(p);
                }
                if (w.piles.Count > 0) { w.x = w.piles[0].x; w.z = w.piles[0].z; }
                keep.Add(w);
            }
            worlds.Clear();
            for (int i = 0; i < keep.Count; i++)
            {
                if (i < OldWorld.MaxWorlds) worlds.Add(keep[i]);
                else
                {
                    unseen.Add(new CrumbNote { seed = keep[i].seed, items = keep[i].ItemCount });
                    trimNote = "Too many old worlds: the extra ones crumbled.";
                }
            }
            if (trimmedPile && trimNote.Length == 0) trimNote = "An old world held too many items: the extra ones are gone.";
            if (runWorld != 0 && Find(runWorld) == null) runWorld = 0;
            if (!Biomes.Valid((int)runBiome)) runBiome = Biome.Meadow;
            { OldWorld rw = Find(runWorld); if (rw != null) runBiome = rw.biome; }
            if (float.IsNaN(runX) || float.IsNaN(runZ) || float.IsInfinity(runX) || float.IsInfinity(runZ)) runPosValid = false;
        }
    }
}

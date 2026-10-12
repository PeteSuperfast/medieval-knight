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
        public void BeginRun()
        {
            EnsureHeld();
            ownedAtStart = OwnedIds();
            inRun = true;
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
        public RunResult Survive()
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
            Finish(r);
            return r;
        }

        // Died (or left the game mid-run): everything carried is gone.
        public RunResult Die(bool abandoned)
        {
            var r = new RunResult { victory = false, abandoned = abandoned };
            foreach (Item it in carry.All()) r.lost.Add(Line(it, false));
            carry.Clear();
            foreach (Item it in stash.All()) r.safe.Add(Line(it, false));
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
            inRun = false;
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
            // a weapon that is both in the stash and in the carry becomes one stack in the stash... it cannot happen
            // through the game, but a hand-edited file could do it: keep the carried one, merge into it
            foreach (Item it in new List<Item>(carry.All()))
            {
                if (!it.IsWeapon) continue;
                for (int i = 0; i < stash.weapons.Length; i++)
                    if (stash.weapons[i].IsWeapon && stash.weapons[i].id == it.id) stash.weapons[i] = Item.None;
                for (int i = 0; i < stash.supplies.Length; i++)
                    if (stash.supplies[i].IsWeapon && stash.supplies[i].id == it.id) stash.supplies[i] = Item.None;
            }
            EnsureHeld();
            EnsureIron();
        }
    }
}

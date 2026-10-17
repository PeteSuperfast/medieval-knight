using System;
using System.IO;
using UnityEngine;

namespace MedievalKnight
{
    // Reads / writes the Phase 1 stash + carry as one JSON file on this machine (persistentDataPath, or -save <file>).
    // Automated runs never touch the real file: with no -save they keep the data in memory only (Disabled).
    public static class LoopSave
    {
        public const int Version = 1;
        public const string FileName = "medieval-knight-save.json";

        [Serializable] class ItemDto { public int k, id, p; }
        [Serializable] class BagDto { public ItemDto[] w, f, s; }
        [Serializable] class PileDto { public float x, z; public ItemDto[] items; }
        // pv = 2: the piles are in "piles" (several, none moves). Without pv (a phase 1-3 save) there is one pile at x, z holding "pile".
        // x / z / pile of a pv 2 file hold the first pile too, so an older build still reads something sensible.
        [Serializable] class WorldDto { public int seed, left; public long t; public float x, z; public ItemDto[] pile; public int pv; public PileDto[] piles; public int bm; }
        [Serializable] class CrumbDto { public int seed, n; }
        // Phase 2 fields (ow, runWorld, rx, rz, rpos, unseen) are missing in a phase 1 file: that reads as "no old worlds".
        [Serializable]
        class SaveDto
        {
            public int v; public bool inRun, intro; public int held; public BagDto stash, carry;
            public WorldDto[] ow; public int runWorld; public float rx, rz; public bool rpos; public CrumbDto[] unseen;
            public bool rc;                                    // phase 4: the open run is an online run of a client
            public int rb;                                     // snow-map-models: the biome of the open run (missing = 0 = meadow)
        }

        public static string PathOverride;                 // test hook: use this file (SelfTest sets it)
        public static bool ForceMemory;                    // test hook: keep everything in memory
        public static string LastError = "";               // the most recent problem ("" = none)
        public static string LastLoadNote = "";            // set by Load: what happened to a damaged file
        static string memory;                              // the "file" while Disabled
        static string pathCache;

        // Scripted runs without -save must not overwrite the player's real save.
        public static bool Disabled
        {
            get { return ForceMemory || (PathOverride == null && Args.savePath == null && Args.Scripted); }
        }

        public static string Path
        {
            get
            {
                if (PathOverride != null) return PathOverride;
                if (Args.savePath != null) return Args.savePath;
                if (pathCache == null) pathCache = System.IO.Path.Combine(Application.persistentDataPath, FileName);
                return pathCache;
            }
        }

        // Forget the in-memory copy (tests start from a new game).
        public static void ResetMemory() { memory = null; }

        // ---------------------------------------------------------------- JSON <-> Loadout

        static ItemDto[] ToDto(Item[] arr)
        {
            var d = new ItemDto[arr.Length];
            for (int i = 0; i < arr.Length; i++) d[i] = new ItemDto { k = (int)arr[i].kind, id = arr[i].id, p = arr[i].plus };
            return d;
        }

        // Pile items of a file: anything that is not a Weapon / Food is skipped (Sanitize drops the invalid ones).
        static void ReadItems(ItemDto[] src, System.Collections.Generic.List<Item> dst)
        {
            if (src == null) return;
            foreach (ItemDto id in src)
            {
                if (id == null || id.k <= 0 || id.k > (int)ItemKind.Food) continue;
                dst.Add(new Item { kind = (ItemKind)id.k, id = id.id, plus = id.p });
            }
        }

        static BagDto ToDto(ItemBag b) { return new BagDto { w = ToDto(b.weapons), f = ToDto(b.foods), s = ToDto(b.supplies) }; }

        static void FromDto(ItemDto[] src, Item[] dst)
        {
            for (int i = 0; i < dst.Length; i++)
            {
                dst[i] = Item.None;
                if (src == null || i >= src.Length || src[i] == null) continue;
                if (src[i].k < 0 || src[i].k > (int)ItemKind.Food) continue;
                dst[i] = new Item { kind = (ItemKind)src[i].k, id = src[i].id, plus = src[i].p };
            }
        }

        static void FromDto(BagDto src, ItemBag dst)
        {
            FromDto(src != null ? src.w : null, dst.weapons);
            FromDto(src != null ? src.f : null, dst.foods);
            FromDto(src != null ? src.s : null, dst.supplies);
        }

        public static string ToJson(Loadout l)
        {
            var dto = new SaveDto { v = Version, inRun = l.inRun, intro = l.introShown, held = l.heldSlot, stash = ToDto(l.stash), carry = ToDto(l.carry) };
            dto.ow = new WorldDto[l.worlds.Count];
            for (int i = 0; i < dto.ow.Length; i++)
            {
                OldWorld w = l.worlds[i];
                Pile first = w.piles.Count > 0 ? w.piles[0] : null;
                var wd = new WorldDto
                {
                    bm = (int)w.biome, seed = w.seed, left = w.left, t = w.fellUnix, x = first != null ? first.x : w.x, z = first != null ? first.z : w.z,
                    pile = ToDto(first != null ? first.items.ToArray() : new Item[0]), pv = 2, piles = new PileDto[w.piles.Count]
                };
                for (int p = 0; p < wd.piles.Length; p++)
                    wd.piles[p] = new PileDto { x = w.piles[p].x, z = w.piles[p].z, items = ToDto(w.piles[p].items.ToArray()) };
                dto.ow[i] = wd;
            }
            dto.rb = (int)l.runBiome; dto.runWorld = l.runWorld; dto.rx = l.runX; dto.rz = l.runZ; dto.rpos = l.runPosValid; dto.rc = l.runCoop;
            dto.unseen = new CrumbDto[l.unseen.Count];
            for (int i = 0; i < dto.unseen.Length; i++) dto.unseen[i] = new CrumbDto { seed = l.unseen[i].seed, n = l.unseen[i].items };
            return JsonUtility.ToJson(dto);
        }

        // null = not a save of this version (garbage, empty, another version).
        public static Loadout FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            SaveDto dto;
            try { dto = JsonUtility.FromJson<SaveDto>(json); }
            catch (Exception) { return null; }
            if (dto == null || dto.v != Version || dto.stash == null || dto.carry == null) return null;
            var l = new Loadout { inRun = dto.inRun, introShown = dto.intro, heldSlot = dto.held };
            FromDto(dto.stash, l.stash);
            FromDto(dto.carry, l.carry);
            if (dto.ow != null)
                foreach (WorldDto wd in dto.ow)
                {
                    if (wd == null) continue;
                    var w = new OldWorld { seed = wd.seed, left = wd.left, fellUnix = wd.t, x = wd.x, z = wd.z, biome = Biomes.Valid(wd.bm) ? (Biome)wd.bm : Biome.Meadow };
                    if (wd.pv >= 2)
                    {
                        if (wd.piles != null)
                            foreach (PileDto pd in wd.piles)
                            {
                                if (pd == null) continue;
                                var p = new Pile { x = pd.x, z = pd.z };
                                ReadItems(pd.items, p.items);
                                if (p.items.Count > 0) w.AddLoaded(p);
                            }
                    }
                    else if (wd.pile != null)                          // a phase 1-3 save: one pile at x, z
                    {
                        var p = new Pile { x = wd.x, z = wd.z };
                        ReadItems(wd.pile, p.items);
                        if (p.items.Count > 0) w.AddLoaded(p);
                    }
                    l.worlds.Add(w);
                }
            l.runBiome = Biomes.Valid(dto.rb) ? (Biome)dto.rb : Biome.Meadow;
            l.runWorld = dto.runWorld; l.runX = dto.rx; l.runZ = dto.rz; l.runPosValid = dto.rpos; l.runCoop = dto.rc;
            if (dto.unseen != null)
                foreach (CrumbDto c in dto.unseen) if (c != null && c.seed != 0) l.unseen.Add(new CrumbNote { seed = c.seed, items = Mathf.Max(0, c.n) });
            l.Sanitize();
            return l;
        }

        // ---------------------------------------------------------------- file

        static string TryRead(string file)
        {
            try { return File.Exists(file) ? File.ReadAllText(file) : null; }
            catch (Exception e) { LastError = "Could not read the save: " + e.Message; return null; }
        }

        // The saved loadout, or a new game (Iron Sword only). LastLoadNote says when a damaged file was set aside.
        public static Loadout Load()
        {
            LastLoadNote = "";
            if (Disabled)
            {
                Loadout m = FromJson(memory);
                return m ?? Loadout.NewGame();
            }
            string file = Path;
            string text = TryRead(file);
            Loadout l = FromJson(text);
            if (l != null) return l;
            string bakText = TryRead(file + ".bak");
            l = FromJson(bakText);
            if (l != null)
            {
                if (text != null) LastLoadNote = "The save file was damaged - restored the previous one.";
                return l;
            }
            if (text != null)
            {
                try
                {
                    string bad = file + ".corrupt";
                    if (File.Exists(bad)) File.Delete(bad);
                    File.Move(file, bad);
                }
                catch (Exception e) { LastError = "Could not set the damaged save aside: " + e.Message; }
                LastLoadNote = "The save file was damaged - started a new one.";
            }
            return Loadout.NewGame();
        }

        // false = it could not be written (the game goes on; the caller shows "Could not save").
        public static bool Save(Loadout l)
        {
            string json = ToJson(l);
            if (Disabled) { memory = json; return true; }
            string file = Path, tmp = file + ".tmp";
            try
            {
                string dir = System.IO.Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(tmp, json);
                if (File.Exists(file))
                {
                    File.Copy(file, file + ".bak", true);
                    File.Delete(file);
                }
                File.Move(tmp, file);
                LastError = "";
                return true;
            }
            catch (Exception e)
            {
                LastError = "Could not save: " + e.Message;
                Debug.LogWarning("LOOPSAVE " + LastError);
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
                return false;
            }
        }
    }
}

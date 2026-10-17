using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // snow-map-models: the cases of the snow biome. Data / save / rules run inside the loop cases; the rest runs alone with -snowonly.
    public partial class SelfTest
    {
#if ENABLE_INPUT_SYSTEM
        // ---- pure data: biome pick, old world map, save roundtrip, old saves, net protocol
        void SnowDataTests()
        {
            Check("snow: Biomes.FromSeed is stable (same seed, same map) and uses the high bits",
                Biomes.FromSeed(0x10000) == Biome.Snow && Biomes.FromSeed(0x20000) == Biome.Meadow && Biomes.FromSeed(12345) == Biomes.FromSeed(12345) && Biomes.FromSeed(-1) == Biomes.FromSeed(-1));
            var rng = new System.Random(5);
            int snow = 0;
            for (int i = 0; i < 200; i++) if (Biomes.Roll(rng) == Biome.Snow) snow++;
            Check("snow: Biomes.Roll gives both maps (about half, " + snow + " of 200 are snow)", snow > 60 && snow < 140);
            Check("snow: Net.Protocol is 7 (biome byte in Enter / Worlds / Team)", Net.Protocol == 7);
            Check("snow: Biomes.Valid rejects unknown values", Biomes.Valid(0) && Biomes.Valid(1) && !Biomes.Valid(-1) && !Biomes.Valid(2) && !Biomes.Valid(99));

            // an old world remembers the map it was made on; a run in an old world uses that map, a new world uses the one picked
            Loadout l = Loadout.NewGame();
            l.worlds.Add(new OldWorld { seed = 0x12345, left = 2, fellUnix = 10, x = 5f, z = 6f, biome = Biome.Snow });
            l.worlds[0].NewPile(5f, 6f).items.Add(Item.Food(Foods.Apple));
            l.worlds.Add(new OldWorld { seed = 0x23456, left = 3, fellUnix = 20, x = 1f, z = 2f });
            l.worlds[1].NewPile(1f, 2f).items.Add(Item.Food(Foods.Meat));
            l.BeginRun(0x23456, false, Biome.Snow);
            Check("snow: a run in an old meadow world uses the world's own map, whatever biome was asked for", l.runBiome == Biome.Meadow);
            l.Die(false, 0f, 0f, 100);
            l.BeginRun(0x12345, false, Biome.Meadow);
            Check("snow: a run in an old snow world is a snow run", l.runBiome == Biome.Snow);
            l.Die(false, 0f, 0f, 100);

            var n = Loadout.NewGame();
            n.carry.Add(Item.Food(Foods.Apple));
            n.BeginRun(0, false, Biome.Snow);
            n.carry.Add(Item.Food(Foods.Meat));
            RunResult rr = n.Die(false, 10f, 20f, 1000);
            OldWorld made = n.worlds.Count > 0 ? n.worlds[n.worlds.Count - 1] : null;
            Check("snow: a death in a new snow run makes an old world on the snow map", made != null && made.biome == Biome.Snow && rr.worldNote == WorldNote.Created && n.runBiome == Biome.Meadow);

            // save: roundtrip keeps the world's map and the open run's map
            var s = Loadout.NewGame();
            s.worlds.Add(new OldWorld { seed = 0x3456, left = 2, fellUnix = 10, x = 5f, z = 6f, biome = Biome.Snow });
            s.worlds[0].NewPile(5f, 6f).items.Add(Item.Food(Foods.Apple));
            s.worlds.Add(new OldWorld { seed = 0x4567, left = 3, fellUnix = 20, x = 1f, z = 2f, biome = Biome.Meadow });
            s.worlds[1].NewPile(1f, 2f).items.Add(Item.Food(Foods.Meat));
            s.BeginRun(0, false, Biome.Snow);
            string json = LoopSave.ToJson(s);
            Loadout back = LoopSave.FromJson(json);
            Check("snow: save roundtrip keeps each old world's map and the open run's map",
                back != null && back.worlds.Count == 2 && back.Find(0x3456).biome == Biome.Snow && back.Find(0x4567).biome == Biome.Meadow && back.runBiome == Biome.Snow && back.inRun);

            // old save (no "bm" / "rb") reads as the meadow; a map this build does not know reads as the meadow
            string old = json.Replace("\"bm\":", "\"zz\":").Replace("\"rb\":", "\"zy\":");
            Loadout o = LoopSave.FromJson(old);
            Check("snow: a save without the biome fields loads and every old world is a meadow world",
                o != null && o.worlds.Count == 2 && o.worlds[0].biome == Biome.Meadow && o.worlds[1].biome == Biome.Meadow && o.runBiome == Biome.Meadow);
            string weird = json.Replace("\"bm\":1", "\"bm\":9");
            Loadout wl = LoopSave.FromJson(weird);
            Check("snow: a save with an unknown map number loads as a meadow world", wl != null && wl.Find(0x3456) != null && wl.Find(0x3456).biome == Biome.Meadow);

            // the base is always the meadow; scripted runs never roll
            Check("snow: Args.biome is not set in the test and MapLayout starts as the meadow", Args.biome == -1 && MapLayout.Current == Biome.Meadow);
        }

        // Teleports the local knight to a point of the snow map (game coordinates) and photographs it.
        IEnumerator SnowLook(string label, float x, float z)
        {
            k.transform.position = new Vector3(x, Ground.Y(x, z), z);
            g.SnapCamera();
            yield return W(0.7f);
            Shot(label);
            yield return W(0.2f);
        }

        IEnumerator SnowTests()
        {
            SnowDataTests();
            yield return null;
            Log("---- snow map: the scene and y-follow");
            Args.biome = (int)Biome.Snow;                                   // a new world of this run is a snow world
            yield return StartLoopWorld(null);
            RunLoop run = g.run;
            Check("snow: a new world entered with -biome snow is the snow map (MapLayout.Current)", run.InWorld && MapLayout.Current == Biome.Snow && run.data.runBiome == Biome.Snow);
            Check("snow: the snow scene was built behind the black screen: " + SnowBuilder.LastStats, SnowBuilder.LastStats.StartsWith("SNOWWORLD built") && SnowBuilder.LastStats.Contains("0 prefabs missing"));
            Vector3 sp = MapLayout.TownSpawn;
            Check("snow: the knight spawns at the village (" + V(sp) + ") standing on the ground (y " + F(k.transform.position.y) + " = " + F(Ground.Y(sp.x, sp.z)) + ")",
                Mathf.Abs(k.transform.position.x - Game.SpawnPointFor(0).x) < 0.1f && Mathf.Abs(k.transform.position.y - Ground.Y(k.transform.position.x, k.transform.position.z)) < 0.05f && k.transform.position.y > 0f);
            yield return W(0.4f);
            Shot("snow_spawn");
            yield return W(0.4f);
            float[,] look = { { -95f - 0f, -102f }, { -55f, -50f }, { -5f, 0f }, { 35f, 45f }, { 135f, 81f }, { 150f, 130f } };
            string[] names = { "snow_bridge", "snow_forest", "snow_valley", "snow_cliff", "snow_lake", "snow_castle" };
            for (int i = 0; i < names.Length; i++)
            {
                yield return SnowLook(names[i], look[i, 0], look[i, 1]);
                Check("snow: " + names[i] + " y follows the ground (" + F(k.transform.position.y) + " vs " + F(Ground.Y(look[i, 0], look[i, 1])) + ")", Mathf.Abs(k.transform.position.y - Ground.Y(k.transform.position.x, k.transform.position.z)) < 0.2f);
            }
            Args.biome = -1;
        }
#endif
    }
}

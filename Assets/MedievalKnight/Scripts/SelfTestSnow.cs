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
            yield return SnowCollisionTests();
            Args.biome = -1;
        }

        // ---- step 5: collision data, reachability, bridge, walking into walls
        bool SnowFree(float x, float z)
        {
            return SnowLayout.Walkable(x, z) && !Obstacles.Overlaps(new Vector3(x, 0f, z), 0.4f, Obstacles.Players);
        }

        int walkBad;
        float walkMinFree;

        IEnumerator SnowWalk(Vector3 from, Vector2 dir, float seconds)
        {
            k.transform.position = new Vector3(from.x, Ground.Y(from.x, from.z), from.z);
            k.moveOverride = dir;
            walkBad = 0;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                yield return null;
                Vector3 p = k.transform.position;
                if (SnowLayout.BlockAt(p.x, p.z) != 0 && SnowLayout.BlockAt(p.x + 0.0f, p.z) != 0) walkBad++;
            }
            k.moveOverride = null;
        }

        IEnumerator SnowCollisionTests()
        {
            Log("---- snow map: collision, reachability, bridge");
            int c1 = Obstacles.CountOf(Biome.Snow), h1 = Obstacles.HashOf(Biome.Snow);
            SnowBuilder.MakeObstacles();
            int c2 = Obstacles.CountOf(Biome.Snow), h2 = Obstacles.HashOf(Biome.Snow);
            Log("SNOW obstacles=" + c1 + " hash=" + h1);
            Check("snow: " + c1 + " obstacles (hash " + h1 + "), the same count and hash when they are made again, within the 8,000 budget", c1 == c2 && h1 == h2 && c1 > 1000 && c1 <= 8000);
            Check("snow: the central lawn (|x| < 28, z -42..32) holds no tree / rock / pillar circle (the cliffs of the baked table are terrain, not scenery)", Obstacles.CentersIn(Biome.Snow, -28f, 28f, -42f, 32f, true) == 0);
            Check("snow: the meadow's obstacles were not touched (count " + Obstacles.CountOf(Biome.Meadow) + ")", Obstacles.CountOf(Biome.Meadow) > 1000);

            // reachability on the 1 m grid (8 neighbours, no corner cutting), obstacles of the players layer included
            SnowMeta meta = SnowLayout.Meta;
            int N = SnowLayout.N;
            var seen = new bool[N * N];
            var open = new Queue<int>();
            int sx = Mathf.RoundToInt(meta.spawn[0] + 190f), sz = Mathf.RoundToInt(meta.spawn[1] + 190f);
            seen[sz * N + sx] = true; open.Enqueue(sz * N + sx);
            int reached = 0;
            while (open.Count > 0)
            {
                int c = open.Dequeue(); reached++;
                int ci = c % N, cj = c / N;
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int ni = ci + di, nj = cj + dj;
                        if (ni < 0 || nj < 0 || ni >= N || nj >= N || seen[nj * N + ni]) continue;
                        if (!SnowFree(ni - 190f, nj - 190f)) continue;
                        if (di != 0 && dj != 0 && (!SnowFree(ci + di - 190f, cj - 190f) || !SnowFree(ci - 190f, cj + dj - 190f))) continue;
                        seen[nj * N + ni] = true; open.Enqueue(nj * N + ni);
                    }
            }
            int okWp = 0; string bad = "";
            for (int w = 0; w < 15; w++)
            {
                float wx = meta.waypoints[w * 2], wz = meta.waypoints[w * 2 + 1];
                bool found = false;
                for (int dj = -3; dj <= 3 && !found; dj++)
                    for (int di = -3; di <= 3 && !found; di++)
                    {
                        int ii = Mathf.RoundToInt(wx + 190f) + di, jj = Mathf.RoundToInt(wz + 190f) + dj;
                        if (ii >= 0 && jj >= 0 && ii < N && jj < N && seen[jj * N + ii]) found = true;
                    }
                if (found) okWp++; else bad += " " + w;
            }
            Check("snow: waypoints 0-14 of the main road are reachable from the spawn by walking (" + okWp + " of 15" + (bad.Length > 0 ? ", not:" + bad : "") + "), " + reached + " cells reachable", okWp == 15);
            Vector3 lr = MapLayout.Lair;
            bool lairOk = seen[Mathf.RoundToInt(lr.z + 190f) * N + Mathf.RoundToInt(lr.x + 190f)];
            Check("snow: the lair (" + V(lr) + ", radius " + F(MapLayout.LairRadius) + ") is reachable from the spawn", lairOk && MapLayout.LairRadius >= 18f);
            Check("snow: the spawn and the dummy spot are open ground", SnowFree(meta.spawn[0], meta.spawn[1]) && SnowFree(meta.dummy[0], meta.dummy[1]));

            // bridge S1: the deck is one walkable ribbon with a smooth height
            Vector3 b0 = new Vector3(95f - 190f, 0f, 88f - 190f);
            Vector2 axis = new Vector2(Mathf.Cos(48.43f * Mathf.Deg2Rad), Mathf.Sin(48.43f * Mathf.Deg2Rad));
            float prev = Ground.Y(b0.x - axis.x * 8f, b0.z - axis.y * 8f), maxStep = 0f, minY = 999f, maxY = -999f;
            bool deckFree = true;
            for (float t = -8f; t <= 8f; t += 0.25f)
            {
                float x = b0.x + axis.x * t, z = b0.z + axis.y * t, y = Ground.Y(x, z);
                maxStep = Mathf.Max(maxStep, Mathf.Abs(y - prev)); prev = y;
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                if (!SnowFree(x, z)) deckFree = false;
            }
            Check("snow: bridge S1 is walkable end to end and its height changes smoothly (largest step " + F(maxStep) + " m per 0.25 m, " + F(minY) + ".." + F(maxY) + ")", deckFree && maxStep <= 0.5f);

            // walking into things: the river, a cliff, the mountain wall, the map edge, the castle - never inside a blocked cell
            int totalBad = 0;
            string[] what = { "river", "north mountains", "castle walls", "east edge" };
            Vector3[] starts = { new Vector3(-115f, 0f, -118f), new Vector3(-60f, 0f, 110f), new Vector3(130f, 0f, 120f), new Vector3(170f, 0f, -20f) };
            Vector2[] dirs = { new Vector2(1f, 0.7f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            for (int i = 0; i < what.Length; i++)
            {
                yield return SnowWalk(starts[i], dirs[i], 3.2f);
                totalBad += walkBad;
                Vector3 e = k.transform.position;
                Log("snow walk into " + what[i] + ": ends at " + V(e) + " blocked frames " + walkBad);
                Check("snow: walking into the " + what[i] + " never enters a blocked cell (" + walkBad + " frames inside)", walkBad == 0);
            }
            // from the road's waypoints in all four diagonals: whatever is there (cliffs, trees, rocks, water) the knight is never inside it
            int tries = 0, wbad = 0;
            Vector2[] diag = { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) };
            int[] wps = { 1, 4, 6, 8, 10, 11, 12, 13 };
            for (int i = 0; i < wps.Length; i++)
            {
                float wx = meta.waypoints[wps[i] * 2], wz = meta.waypoints[wps[i] * 2 + 1];
                if (!SnowFree(wx, wz)) continue;
                yield return SnowWalk(new Vector3(wx, 0f, wz), diag[i % 4], 2.4f);
                tries++; wbad += walkBad;
                Vector3 p = k.transform.position;
                if (Obstacles.Overlaps(p, 0.38f, Obstacles.Players)) wbad++;
            }
            Check("snow: walking diagonally from " + tries + " points of the road never ends inside a blocked cell or obstacle (" + wbad + " bad frames)", tries >= 6 && wbad == 0);
            k.moveOverride = null;
            yield return null;
        }
#endif
    }
}

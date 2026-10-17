using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
            float[,] look = { { -95f - 0f, -102f }, { -55f, -50f }, { -5f, 0f }, { 35f, 45f }, { 135f, 81f }, { 140f, 126f } };
            string[] names = { "snow_bridge", "snow_forest", "snow_valley", "snow_cliff", "snow_lake", "snow_castle" };
            for (int i = 0; i < names.Length; i++)
            {
                yield return SnowLook(names[i], look[i, 0], look[i, 1]);
                Check("snow: " + names[i] + " y follows the ground (" + F(k.transform.position.y) + " vs " + F(Ground.Y(look[i, 0], look[i, 1])) + ")", Mathf.Abs(k.transform.position.y - Ground.Y(k.transform.position.x, k.transform.position.z)) < 0.2f);
            }
            yield return SnowCollisionTests();
            yield return SnowFrameTests();
            yield return SnowActorTests();
            yield return SnowOldWorldTests();
            Args.biome = -1;
        }

        // ---- step 7: frame time (vsync off, 1280 x 720) at the village, the pine forest and the castle road
        IEnumerator SnowFrameTests()
        {
            Log("---- snow map: frame time");
            int vs = QualitySettings.vSyncCount; int tf = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            SnowMeta meta = SnowLayout.Meta;
            int[] at = { 0, 5, 12, 14 };
            string[] names = { "village", "pine forest", "snowfield", "castle road" };
            float sum = 0f, worst = 0f;
            for (int i = 0; i < at.Length; i++)
            {
                float wx = meta.waypoints[at[i] * 2], wz = meta.waypoints[at[i] * 2 + 1];
                k.transform.position = new Vector3(wx, Ground.Y(wx, wz), wz);
                g.SnapCamera();
                yield return W(0.8f);
                float t0 = Time.realtimeSinceStartup; int frames = 0;
                while (Time.realtimeSinceStartup - t0 < 2.5f) { yield return null; frames++; }
                float ms = (Time.realtimeSinceStartup - t0) / frames * 1000f;
                Log("snow frame time at the " + names[i] + ": " + ms.ToString("0.00") + " ms (" + frames + " frames)");
                sum += ms; worst = Mathf.Max(worst, ms);
            }
            QualitySettings.vSyncCount = vs; Application.targetFrameRate = tf;
            Check("snow: the average frame time at 1280 x 720 is " + (sum / at.Length).ToString("0.00") + " ms (worst " + worst.ToString("0.00") + " ms), budget 8 ms", sum / at.Length <= 8f);
        }

        // ---- step 6: dummy, boss, slime packs, loot and portal stand on the snow ground
        IEnumerator SnowActorTests()
        {
            Log("---- snow map: dummy, boss, slimes, loot, portal");
            RunLoop run = g.run;
            Vector3 dp = MapLayout.DummyPos, lair = MapLayout.Lair;
            Check("snow: the straw dummy stands in the village (" + V(g.dummy.transform.position) + ") on the ground",
                g.dummy != null && Mathf.Abs(g.dummy.transform.position.x - dp.x) < 0.1f && Mathf.Abs(g.dummy.transform.position.y - Ground.Y(dp.x, dp.z)) < 0.05f && MapLayout.InTown(dp.x, dp.z));
            Check("snow: the Minotaur stands at the lair (" + V(g.minotaur.transform.position) + ", y " + F(g.minotaur.transform.position.y) + ") on the ground",
                g.minotaur != null && Mathf.Abs(g.minotaur.transform.position.x - lair.x) < 0.1f && Mathf.Abs(g.minotaur.transform.position.z - lair.z) < 0.1f
                && Mathf.Abs(g.minotaur.transform.position.y - Ground.Y(lair.x, lair.z)) < 0.05f);
            Check("snow: the village is a safe zone and the lair is the lair", MapLayout.ZoneAt(MapLayout.TownSpawn.x, MapLayout.TownSpawn.z) == Zone.Town && MapLayout.ZoneAt(lair.x, lair.z) == Zone.Lair
                && MapLayout.SpawnTier(Zone.Town) == 0f && MapLayout.SpawnTier(MapLayout.ZoneAt(-78f, -75f)) > 0f);

            // the M map shows the snow ground (one picture per biome), banner / signs of the snow map
            yield return Tap(Key.M);
            yield return W(0.4f);
            g.Map.Probe();
            Texture2D tex = g.Map.MapTexture;
            Vector3[] mp = { MapLayout.TownSpawn, MapLayout.Lair, new Vector3(120f, 0f, 0f), new Vector3(-100f, 0f, -20f), new Vector3(-30f, 0f, 30f), new Vector3(40f, 0f, 40f) };
            int mapBad = 0; string mapDesc = "";
            foreach (Vector3 q in mp)
            {
                int px = Mathf.FloorToInt((q.x + 190f) / 380f * tex.width), py = Mathf.FloorToInt((q.z + 190f) / 380f * tex.height);
                Vector2 wc = MapUi.TexelToWorld(px, py);
                Color want = MapUi.ZoneColor(MapLayout.ZoneAt(wc.x, wc.y));
                Color have = tex.GetPixel(px, py);
                if (Mathf.Abs(want.r - have.r) + Mathf.Abs(want.g - have.g) + Mathf.Abs(want.b - have.b) > 0.02f) mapBad++;
                mapDesc += MapLayout.ZoneAt(wc.x, wc.y) + " ";
            }
            Check("snow: the M map draws the snow zones from the snow table (" + mapDesc + ", " + mapBad + " wrong)", g.Map.Shown && mapBad == 0 && MapLayout.ZoneAt(MapLayout.TownSpawn.x, MapLayout.TownSpawn.z) == Zone.Town
                && MapUi.ZoneColor(Zone.SnowField) != MapUi.ZoneColor(Zone.Meadow));
            Shot("snow_map");
            yield return W(0.4f);
            yield return Tap(Key.M);
            yield return W(0.2f);
            Check("snow: the snow map has " + MapLayout.Signs.Count + " road signs with text", MapLayout.Signs.Count == 4 && MapLayout.Signs[0].text.Length > 5);

            // slime packs: out of the village, on open ground, standing on the ground
            g.packsOff = false;
            SnowMeta meta = SnowLayout.Meta;
            float wx = meta.waypoints[4 * 2], wz = meta.waypoints[4 * 2 + 1];
            k.transform.position = new Vector3(wx, Ground.Y(wx, wz), wz);
            g.SnapCamera();
            float t0 = Time.realtimeSinceStartup;
            int got = 0;
            while (got < 4 && Time.realtimeSinceStartup - t0 < 16f)
            {
                yield return W(0.4f);
                got = 0;
                foreach (Enemy e in Enemy.All) if (e is Slime && !e.Dead) got++;
            }
            int badY = 0, badPlace = 0;
            foreach (Enemy e in Enemy.All)
            {
                if (!(e is Slime) || e.Dead) continue;
                Vector3 p = e.transform.position;
                if (Mathf.Abs(p.y - Ground.Y(p.x, p.z)) > 0.06f) badY++;
                if (!MapLayout.Walkable(p.x, p.z) || Obstacles.Overlaps(p, 0.3f, Obstacles.Enemies)) badPlace++;
            }
            Check("snow: a slime pack comes out of the village (" + got + " slimes), all on walkable open ground (" + badPlace + " bad) and on the ground (" + badY + " floating / sunk)", got >= 4 && badPlace == 0 && badY == 0);
            yield return W(2.5f);                                                   // they hop at the knight: still on the ground while moving
            badY = 0;
            foreach (Enemy e in Enemy.All)
            {
                if (!(e is Slime) || e.Dead) continue;
                Vector3 p = e.transform.position;
                if (Mathf.Abs(p.y - Ground.Y(p.x, p.z)) > 0.06f) badY++;
            }
            Check("snow: hopping slimes keep their feet on the ground (" + badY + " off)", badY == 0);
            Shot("snow_slimes");
            yield return W(0.4f);
            g.packsOff = true;
            foreach (Enemy e in new List<Enemy>(Enemy.All)) if (e is Slime) { e.gameObject.SetActive(false); Destroy(e.gameObject); }

            // boss: dies at the lair, the portal opens there, the loot lies on the ground
            k.transform.position = new Vector3(lair.x - 6f, Ground.Y(lair.x - 6f, lair.z), lair.z);
            g.SnapCamera();
            g.foodOverride = -1; g.dropOverride = -1;
            Vector3 fell = g.minotaur.transform.position;
            KillMinotaur();
            yield return W(2.4f);
            yield return ClearLevelUps();
            yield return W(0.5f);
            Check("snow: when the Minotaur dies the portal opens where he fell (" + (run.portal != null ? V(run.portal.transform.position) : "none") + ") on the ground",
                run.BossDown && run.portal != null && (run.portal.transform.position - fell).magnitude < 6f && Mathf.Abs(run.portal.transform.position.y - Ground.Y(run.portal.transform.position.x, run.portal.transform.position.z)) < 0.05f);
            int pickBad = 0;
            foreach (Pickup pk in Pickup.All) { Vector3 p = pk.transform.position; if (Mathf.Abs(p.y - Ground.Y(p.x, p.z)) > 0.06f) pickBad++; }
            Check("snow: the boss's loot lies on the ground (" + Pickup.All.Count + " pickups, " + pickBad + " floating / sunk)", Pickup.All.Count >= 1 && pickBad == 0);
            Shot("snow_portal");
            yield return W(0.4f);
        }

        // ---- step 6: falling in the snow makes a snow old world; its pile stands where he fell, on walkable ground, after a save and load
        IEnumerator SnowOldWorldTests()
        {
            Log("---- snow map: old world made on the snow");
            Args.biome = (int)Biome.Snow;
            yield return StartLoopWorld(d => { d.carry.foods[0] = Item.Food(Foods.Apple); });
            RunLoop run = g.run;
            Loadout d0 = run.data;
            SnowMeta meta = SnowLayout.Meta;
            float wx = meta.waypoints[6 * 2], wz = meta.waypoints[6 * 2 + 1];
            k.transform.position = new Vector3(wx, Ground.Y(wx, wz), wz);
            g.SnapCamera();
            yield return W(0.4f);
            Vector3 fellAt = k.Pos;
            k.TakeDamage(9999f, k.transform.position + Vector3.right, "selftest");
            yield return W(3.5f);
            OldWorld w = d0.worlds.Count == 1 ? d0.worlds[0] : null;
            Check("snow: falling in a new snow world makes a SNOW old world with the pile where he fell", w != null && w.biome == Biome.Snow && w.pile.Count >= 2
                && Mathf.Abs(w.x - fellAt.x) < 1f && Mathf.Abs(w.z - fellAt.z) < 1f);
            string saved = File.ReadAllText(LoopSave.Path);
            Check("snow: the save file says the old world is a snow world", saved.Contains("\"bm\":1"));
            Loadout reloaded = LoopSave.Load();
            Check("snow: loading that save gives the same snow old world", reloaded != null && reloaded.worlds.Count == 1 && reloaded.worlds[0].biome == Biome.Snow);
            yield return Click(run.ui.ReturnRect.position);
            yield return W(0.5f);
            Check("snow: back at the base the scene is the meadow again (MapLayout " + MapLayout.Current + ")", MapLayout.Current == Biome.Meadow && !run.InWorld);

            Args.biome = (int)Biome.Meadow;                                          // asking for a meadow world changes nothing about an old snow world
            if (!run.PrepareOpen) { g.SetInventory(true); yield return W(0.2f); }
            yield return Click(run.ui.OldWorldsRect.position);
            yield return W(0.3f);
            string rowText = run.ui.WorldRowText(0);
            Check("snow: the OLD WORLDS list marks the world as SNOW (" + rowText + ")", rowText.Contains("SNOW") && rowText.Contains(w.Code));
            yield return Click(run.ui.WorldsEnterRect.position);
            yield return W(0.15f);
            if (run.ui.PromptShown) yield return Tap(Key.Enter);
            float tIn = Time.realtimeSinceStartup;
            while (!run.InWorld && Time.realtimeSinceStartup - tIn < 4f) yield return null;
            yield return W(0.8f);
            k = g.knight;
            g.packsOff = true;
            yield return W(1.0f);
            Check("snow: entering that old world plays on the snow map (MapLayout " + MapLayout.Current + ")", run.InWorld && MapLayout.Current == Biome.Snow && d0.runBiome == Biome.Snow);
            LootPile lp = Object.FindAnyObjectByType<LootPile>();
            Check("snow: its pile stands where he fell (" + (lp != null ? V(lp.transform.position) : "none") + ") on walkable ground, on the ground",
                lp != null && Vector3.Distance(new Vector3(lp.transform.position.x, 0f, lp.transform.position.z), new Vector3(fellAt.x, 0f, fellAt.z)) < 3.5f
                && MapLayout.Walkable(lp.transform.position.x, lp.transform.position.z) && Mathf.Abs(lp.transform.position.y - Ground.Y(lp.transform.position.x, lp.transform.position.z)) < 0.05f);
            Check("snow: the old snow world's badge says SNOW (" + run.ui.BadgeText.Replace('\n', '/') + ")", run.ui.BadgeText.Contains("SNOW") && run.ui.BadgeText.Contains(w.Code));
            Shot("snow_old_world");
            yield return W(0.4f);
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

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace MedievalKnight
{
    // Automated smoke test, only active when the game is started with:
    //     MedievalKnight.exe -selftest <output folder>
    // It walks, uses every sword attack and skill, kills slimes, fights the Minotaur, levels up and
    // picks upgrades and stat points (by key and by mouse click), checks that they changed combat numbers,
    // picks up dropped swords, equips each one (inventory panel by key and click) and measures its damage /
    // swing time / crit, checks that the six sound clips load and play on the right events,
    // saves screenshots and logs "SELFTEST ..." lines, then quits. Normal play never runs this.
    public class SelfTest : MonoBehaviour
    {
        string dir;
        int shot, fails;
        Game g;
        Knight k;

        public static void StartIfRequested(GameObject host)
        {
#if ENABLE_INPUT_SYSTEM
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-selftest") continue;
                host.AddComponent<SelfTest>().dir = args[i + 1];
                Sfx.I.logPlays = true;                                 // one "SFX play ..." log line per sound
                return;
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static WaitForSecondsRealtime W(float s) { return new WaitForSecondsRealtime(s); }
        static string F(float v) { return v.ToString("0.00"); }
        static string V(Vector3 v) { return "(" + v.x.ToString("0.0") + ", " + v.z.ToString("0.0") + ")"; }

        void Log(string s) { Debug.Log("SELFTEST " + s); }

        void Check(string what, bool ok)
        {
            if (!ok) fails++;
            Debug.Log("SELFTEST " + (ok ? "PASS " : "FAIL ") + what);
        }

        void Heal()
        {
            k.hp = k.MaxHp; k.armor = k.MaxArmor; k.stamina = k.MaxStamina;
        }

        static int Snds(Snd s) { return Sfx.I.Count(s); }

        // The key of the stat card with the fewest points so far (so no stat hits its cap during the test).
        Key StatKey()
        {
            int best = 0;
            for (int i = 1; i < 3; i++) if (k.progress.attr[i] < k.progress.attr[best]) best = i;
            return best == 0 ? Key.Digit1 : best == 1 ? Key.Digit2 : Key.Digit3;
        }

        IEnumerator Click(Vector2 pos)
        {
            SetMouse(pos, false);
            yield return null; yield return null; yield return null;
            SetMouse(pos, true);
            yield return null; yield return null; yield return null;
            SetMouse(pos, false);
            yield return null;
        }

        // Packs only come when a knight walks through open country, so tests that need slimes put some in the parking corner.
        void EnsureSlimes(int n)
        {
            int have = 0;
            foreach (Enemy e in Enemy.All) if (e is Slime && !e.Dead) have++;
            for (int i = have; i < n; i++)
                Slime.Spawn(0, new Vector3(-15.5f + (i % 5) * 0.4f, 0f, -15.5f + (i / 5) * 0.4f), g.transform);
        }

        // Slimes chase from 17 m away; the long dummy / sword tests need them out of the way in the south-west corner.
        void ParkSlimes()
        {
            Vector3 corner = new Vector3(-16.5f, 0f, -16.5f);
            foreach (Enemy e in Enemy.All)
            {
                Slime sl = e as Slime;
                if (sl != null && !sl.Dead) sl.Warp(corner + new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f)));
            }
        }

        static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        void Shot(string label)
        {
            shot++;
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, shot.ToString("00") + "_" + label + ".png"));
        }

        // Replaces the whole keyboard state: the listed keys are down, everything else is up.
        static void Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
        }

        IEnumerator Tap(Key key)
        {
            Keys(key);
            yield return null; yield return null; yield return null;
            Keys();
            yield return null;
        }

        static void SetMouse(Vector2 pos, bool left, bool right = false)
        {
            var st = new MouseState { position = pos };
            if (left) st = st.WithButton(MouseButton.Left, true);
            if (right) st = st.WithButton(MouseButton.Right, true);
            InputSystem.QueueStateEvent(Mouse.current, st);
        }

        T Nearest<T>(Vector3 from, float minDist = 0f) where T : Enemy
        {
            T best = null;
            float bd = 1e9f;
            foreach (Enemy e in Enemy.All)
            {
                T t = e as T;
                if (t == null || t.Dead) continue;
                float d = (t.transform.position - from).magnitude;
                if (d < minDist || d >= bd) continue;
                bd = d; best = t;
            }
            return best;
        }

        void StandNear(Enemy e, float gap)
        {
            Vector3 p = e.transform.position;
            Vector3 side = p.x > -Game.ArenaHalf + 4f ? Vector3.left : Vector3.right;
            k.transform.position = new Vector3(p.x, 0f, p.z) + side * (e.radius + gap);
            k.aimOverride = p;
            g.SnapCamera();
        }

        // Picks card 1 and then a stat for any level-up that opens while another test is running (the panel pauses the game).
        IEnumerator ClearLevelUps()
        {
            int guard = 0;
            while ((g.LevelUpOpen || g.progress.pendingChoices > 0) && guard++ < 60)
            {
                if (g.LevelUpOpen) yield return Tap(g.LevelStep == 2 ? StatKey() : Key.Digit1);
                yield return W(0.2f);
            }
        }

        IEnumerator WaitIdle(float max)
        {
            float t0 = Time.realtimeSinceStartup;
            while (k.IsAttacking && Time.realtimeSinceStartup - t0 < max) yield return null;
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
            Directory.CreateDirectory(dir);
            g = Game.I; k = g.knight;
            Dummy d = g.dummy;
            Random.InitState(12345);
            g.foodOverride = 0;                                         // no random food: the food test decides when some falls
            yield return W(1.3f);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-looponly") >= 0)   // quick run of the Phase 1 cases only
            {
                yield return LoopTests();
                yield return W(0.5f);
                Log("done (loop cases only), screenshots=" + shot + ", failed checks=" + fails);
                Application.Quit();
                yield break;
            }

            // ------------------------------------------------ the knight starts in town, which is safe: the first pack comes when he leaves it
            {
                int inTown = 0;
                foreach (Enemy e in Enemy.All) if (e is Slime && !e.Dead) inTown++;
                yield return W(3.5f);
                int stillNone = 0;
                foreach (Enemy e in Enemy.All) if (e is Slime && !e.Dead) stillNone++;
                Check("town is a safe zone: no slimes appear while the knight stands in town (" + inTown + " at start, " + stillNone + " after 3.5 s)", inTown == 0 && stillNone == 0);
                k.transform.position = new Vector3(2f, 0f, -5f); g.SnapCamera();
                float tw = Time.realtimeSinceStartup;
                int got = 0;
                while (got < 3 && Time.realtimeSinceStartup - tw < 12f)
                {
                    yield return null;
                    got = 0;
                    foreach (Enemy e in Enemy.All) if (e is Slime && !e.Dead) got++;
                }
                Log("first pack: " + got + " slimes " + (Time.realtimeSinceStartup - tw).ToString("0.0") + " s after the knight left town");
            }

            // ------------------------------------------------ scene inventory
            int slimes = 0;
            foreach (Enemy e in Enemy.All) if (e is Slime) slimes++;
            Log("scene: screen " + Screen.width + "x" + Screen.height + ", enemies " + Enemy.All.Count + " (slimes " + slimes
                + ", minotaur " + (g.minotaur != null) + ", dummy " + (g.dummy != null) + "), renderers "
                + FindObjectsByType<MeshRenderer>().Length
                + ", pipeline " + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "none")
                + ", lit shader " + Prims.Lit(Color.white).shader.name + ", camera pitch " + g.cam.transform.eulerAngles.x.ToString("0"));
            Check("scene has slimes, minotaur and dummy", slimes >= 3 && g.minotaur != null && g.dummy != null);
            {
                bool okIcons = true;
                var seenHash = new System.Collections.Generic.HashSet<int>();
                string info = "";
                for (int i = 0; i < Weapons.All.Length; i++)
                {
                    Texture2D t = SwordIcons.Get(i);
                    int px = SwordIcons.NonBackground(i);
                    okIcons &= t != null && t.width == 128 && t.height == 128 && px > 300 && seenHash.Add(SwordIcons.Hash(i));
                    info += " " + Weapons.All[i].name + "=" + px + "px";
                }
                Log("sword icons made by '" + SwordIcons.Method + "':" + info);
                Check("the six sword icons exist (128 x 128), are not empty and all look different", okIcons && seenHash.Count == Weapons.All.Length);
            }
            Shot("start");
            yield return null;

            // park the slimes in the south-west corner so the dummy tests are not disturbed by kills
            Vector3 corner = new Vector3(-15.5f, 0f, -15.5f);
            foreach (Enemy e in Enemy.All)
            {
                Slime sl = e as Slime;
                if (sl != null) sl.Warp(corner + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)));
            }

            // ------------------------------------------------ sound: clips, listener, the "ready" line at start
            {
                bool allLoaded = true;
                string info = "";
                for (int i = 0; i < Sfx.Files.Length; i++)
                {
                    AudioClip c = Sfx.I.clips[i];
                    bool ok = c != null && c.length > 0f && c.samples > 0;
                    allLoaded &= ok;
                    info += " " + Sfx.Files[i] + "=" + (c == null ? "NULL" : c.length.ToString("0.00") + "s/" + c.loadState);
                }
                int listeners = FindObjectsByType<AudioListener>().Length;
                AudioConfiguration cfg = AudioSettings.GetConfiguration();
                Log("audio: clips" + info + "; listeners " + listeners + ", output " + AudioSettings.outputSampleRate + " Hz " + cfg.speakerMode
                    + ", master volume " + AudioListener.volume.ToString("0.00") + ", voices playing " + Sfx.I.PlayingVoices() + "/" + Sfx.Voices);
                Check("all six sound clips loaded from Resources/Audio (non-null, length > 0)", allLoaded);
                Check("exactly one AudioListener in the scene", listeners == 1);
                Check("the 'ready' voice line played at game start", Snds(Snd.Ready) == 1 && Snds(Snd.Swing) == 0 && Snds(Snd.Grunt) == 0);

                // M cycles the master volume: 100% > 60% > 30% > muted > 100%
                string steps = Sfx.I.VolumeLabel;
                bool volOk = Mathf.Abs(AudioListener.volume - 1f) < 0.001f;
                float[] want = { 0.6f, 0.3f, 0f, 1f };
                for (int i = 0; i < 4; i++)
                {
                    yield return Tap(Key.M);
                    steps += " > " + Sfx.I.VolumeLabel;
                    volOk &= Mathf.Abs(AudioListener.volume - want[i]) < 0.001f;
                    if (i == 2) { yield return W(0.15f); Shot("sound_muted_notice"); yield return null; volOk &= g.hud.NoticeShown == "Sound: MUTED"; }
                }
                Log("volume key M: " + steps);
                Check("M cycles the master volume 100% > 60% > 30% > muted > 100%", volOk);

                // the grunt has a minimum gap, so back-to-back heavy attacks cannot stack voices
                bool g1 = Sfx.I.Grunt(true), g2 = Sfx.I.Grunt(true);
                yield return null;
                bool playing = Sfx.I.LastSource != null && Sfx.I.LastSource.isPlaying;
                Log("grunt twice in one frame: first=" + g1 + " second=" + g2 + ", source playing=" + playing + ", voices playing " + Sfx.I.PlayingVoices());
                Check("a second grunt right after the first is throttled", g1 && !g2);
                Check("the AudioSource reports it is playing", playing);
                yield return W(1.0f);
                bool decoded = true;
                foreach (AudioClip c in Sfx.I.clips) decoded &= c != null && c.loadState == AudioDataLoadState.Loaded;
                Check("all six clips are decoded and ready (loadState Loaded)", decoded);
            }
            k.transform.position = new Vector3(2f, 0f, -5f);          // away from the corner, then put the slimes back in it
            ParkSlimes();
            g.packsOff = true;                                          // the long tests below need a fixed set of enemies (the spawn rules are tested in their own section)

            // ------------------------------------------------ EXP table
            string table = "";
            int total = 0;
            for (int l = 1; l < Progress.MaxLevel; l++) { table += " L" + l + ">" + (l + 1) + "=" + Progress.Need(l); total += Progress.Need(l); }
            Log("exp table:" + table + "  total=" + total);
            { bool inc = true; for (int l = 2; l < Progress.MaxLevel; l++) inc &= Progress.Need(l) > Progress.Need(l - 1); Check("exp table: 24 steps, strictly increasing, 30 / 45 first, 1970 last, 16,410 in total", inc && Progress.MaxLevel == 25 && Progress.Need(1) == 30 && Progress.Need(2) == 45 && Progress.Need(24) == 1970 && total == 16410); }

            // ------------------------------------------------ the big map: layout, collision, zones, frame time
            {
                Check("map: ArenaHalf 190 (380 x 380 m), the spawn point is in town, the lair is 300 m north of it",
                    Game.ArenaHalf == 190f && MapLayout.InTown(Game.SpawnPointFor(0).x, Game.SpawnPointFor(0).z) && Mathf.Abs((MapLayout.Lair - MapLayout.TownSpawn).magnitude - 300f) < 0.5f);
                Check("map: zones are where the layout says (town, meadow, autumn, pine, farm, lake, mountain, lair) and only lake / mountain are not walkable",
                    MapLayout.ZoneAt(0f, -150f) == Zone.Town && MapLayout.ZoneAt(0f, -20f) == Zone.Meadow && MapLayout.ZoneAt(-140f, 40f) == Zone.AutumnForest
                    && MapLayout.ZoneAt(150f, 120f) == Zone.DarkPineForest && MapLayout.ZoneAt(140f, -90f) == Zone.Farmland && MapLayout.ZoneAt(-140f, -140f) == Zone.Lake
                    && MapLayout.ZoneAt(-140f, 150f) == Zone.Mountain && MapLayout.ZoneAt(0f, 150f) == Zone.Lair
                    && !MapLayout.Walkable(-140f, -140f) && !MapLayout.Walkable(-140f, 150f) && MapLayout.Walkable(0f, 0f));

                // walk into the corner, the lake, the mountain, the town wall (real movement, collision runs in Knight.Simulate)
                k.aimOverride = null;
                k.moveOverride = new Vector2(1f, -1f).normalized;
                k.transform.position = new Vector3(186f, 0f, -186f); g.SnapCamera();
                yield return W(1.2f);
                Vector3 pc = k.transform.position;
                k.moveOverride = new Vector2(-1f, 0f);
                k.transform.position = new Vector3(-86f, 0f, -140f);
                yield return W(1.0f);
                Vector3 pl = k.transform.position;
                k.moveOverride = new Vector2(-1f, 0f);
                k.transform.position = new Vector3(-70f, 0f, 130f);
                yield return W(1.6f);
                Vector3 pm = k.transform.position;
                k.moveOverride = new Vector2(0f, -1f);
                k.transform.position = new Vector3(30f, 0f, -97f);
                yield return W(1.0f);
                Vector3 pw = k.transform.position;
                k.transform.position = new Vector3(0f, 0f, -97f);
                yield return W(1.2f);
                Vector3 pg = k.transform.position;
                k.moveOverride = null;
                Log("map walls: corner " + V(pc) + ", lake " + V(pl) + ", mountain " + V(pm) + ", town wall " + V(pw) + ", gate " + V(pg));
                Check("map: the corner is clamped at the fence (x, z within 189.7)", Mathf.Abs(pc.x) <= 189.7f && Mathf.Abs(pc.z) <= 189.7f && pc.x > 185.5f && pc.z < -185.5f);
                Check("map: the lake stops the knight at its shore (x >= -90)", pl.x >= -90.01f && pl.x < -88f);
                Check("map: the mountain stops the knight at its foot (x >= -80)", pm.x >= -80.01f && pm.x < -78f);
                Check("map: the town wall stops the knight (z >= -99.0) but the gate lets him through (z < -102)", pw.z >= -99.05f && pw.z < -98f && pg.z < -102f);
                Vector2 tc; float tr;
                bool haveTree = Obstacles.NearestCircle(new Vector2(0f, 60f), Obstacles.Players, out tc, out tr);
                Vector3 treeP = new Vector3(tc.x, 0f, tc.y);
                Vector3 pushedK = Obstacles.Push(treeP, 0.4f, Obstacles.Players), pushedS = Obstacles.Push(treeP, 0.4f, Obstacles.Enemies);
                Check("map: a tree stops a knight but not a slime (" + Obstacles.Count + " obstacles in total)",
                    haveTree && (pushedK - treeP).magnitude > 0.4f && (pushedS - treeP).magnitude < 0.001f);

                // frame time in the middle of the town, the open meadow and deep in a forest (log only)
                string ft = "";
                Vector3[] spots = { new Vector3(0f, 0f, -150f), new Vector3(0f, 0f, 0f), new Vector3(-140f, 0f, 40f), new Vector3(150f, 0f, 120f), new Vector3(-30f, 0f, -140f), new Vector3(130f, 0f, -85f), new Vector3(0f, 0f, -108f) };
                string[] names = { "town", "meadow", "autumn forest", "pine forest", "town houses", "farm", "gate" };
                for (int i = 0; i < spots.Length; i++)
                {
                    k.transform.position = spots[i]; g.SnapCamera();
                    yield return W(0.6f);
                    float sum = 0f; int frames = 0, worst = 0;
                    float t0 = Time.realtimeSinceStartup;
                    float maxDt = 0f;
                    while (Time.realtimeSinceStartup - t0 < 2f) { yield return null; sum += Time.unscaledDeltaTime; maxDt = Mathf.Max(maxDt, Time.unscaledDeltaTime); frames++; }
                    ft += names[i] + " " + (sum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms (worst " + (maxDt * 1000f).ToString("0") + " ms)  ";
                    worst = frames;
                    g.camZoom = 2.4f; g.SnapCamera();                          // pulled back for the asset screenshots (after the timing)
                    yield return W(0.4f);
                    Shot("asset_" + names[i].Replace(' ', '_'));
                    yield return W(0.3f);                                      // the capture happens at the end of this frame: stay put
                    g.camZoom = 1f; g.SnapCamera();
                }
                Log("frame time (1280x720, selftest window): " + ft);
                k.transform.position = new Vector3(2f, 0f, -5f); g.SnapCamera();
                ParkSlimes();
                yield return W(0.3f);
            }

            // ------------------------------------------------ pack spawning, leash, lair arrow
            {
                g.packsOff = false;
                foreach (Enemy e in new List<Enemy>(Enemy.All)) if (e is Slime) Destroy(e.gameObject);
                yield return W(0.4f);

                // in town nothing spawns, even after many seconds
                k.transform.position = new Vector3(0f, 0f, -150f); g.SnapCamera();
                yield return W(4.5f);
                int inTown = 0;
                foreach (Enemy e in Enemy.All) if (e is Slime && !e.Dead) inTown++;

                // in the meadow a pack arrives, on a ring round him, never in town / lake / mountain
                Vector3 kp = new Vector3(0f, 0f, 20f);
                k.transform.position = kp; g.SnapCamera();
                float tw = Time.realtimeSinceStartup;
                int packSize = 0;
                while (packSize < 4 && Time.realtimeSinceStartup - tw < 10f)
                {
                    yield return null;
                    packSize = 0;
                    foreach (Enemy e in Enemy.All) if (e is Slime && !e.Dead) packSize++;
                }
                float minD = 1e9f, maxD = 0f; bool zonesOk = true;
                foreach (Enemy e in Enemy.All)
                {
                    if (!(e is Slime) || e.Dead) continue;
                    Vector3 sp = e.transform.position;
                    float dd = new Vector2(sp.x - kp.x, sp.z - kp.z).magnitude;
                    minD = Mathf.Min(minD, dd); maxD = Mathf.Max(maxD, dd);
                    zonesOk &= MapLayout.SpawnTier(MapLayout.ZoneAt(sp.x, sp.z)) > 0f && MapLayout.Walkable(sp.x, sp.z);
                }
                Log("pack: " + packSize + " slimes " + (Time.realtimeSinceStartup - tw).ToString("0.0") + " s after he stood in the meadow, distance " + F(minD) + " .. " + F(maxD) + " m");
                Check("no slimes in town; in the meadow a pack of 4+ comes within 10 s", inTown == 0 && packSize >= 4);
                Check("the pack stands 9.5 - 26.5 m from the knight, never in town, lake or mountains", minD >= 9.5f && maxD <= 26.5f && zonesOk);

                // a slime far from everybody (more than 70 m) is removed without a kill
                Slime far = Nearest<Slime>(kp);
                bool hadFar = far != null;
                int kills0 = k.progress.kills;
                Vector3 farAt = new Vector3(0f, 0f, 95f);
                if (far != null) { far.Warp(farAt); far.transform.position = farAt; }
                yield return W(1.6f);
                Check("a slime more than 70 m from every knight disappears quietly (no kill counted)", hadFar && (far == null || !far.gameObject.activeInHierarchy) && k.progress.kills == kills0);

                // slimes do not chase a knight who stands in town
                foreach (Enemy e in new List<Enemy>(Enemy.All)) if (e is Slime) Destroy(e.gameObject);
                yield return W(0.3f);
                g.packsOff = true;
                k.transform.position = new Vector3(0f, 0f, -112f); g.SnapCamera();
                Slime chaser = Slime.Spawn(0, new Vector3(0f, 0f, -92f), g.transform);
                yield return W(4.0f);
                float chaseDist = chaser != null ? new Vector2(chaser.transform.position.x - k.transform.position.x, chaser.transform.position.z - k.transform.position.z).magnitude : 0f;
                Check("a slime 20 m away does not chase a knight standing in town (distance after 4 s: " + F(chaseDist) + " m)", chaser != null && chaseDist > 12f);
                if (chaser != null) Destroy(chaser.gameObject);

                // Minotaur leash: dragged 50 m from his lair with nobody near, hurt, he returns healed
                Minotaur m = g.minotaur;
                bool leashOk = false;
                if (m != null)
                {
                    k.transform.position = new Vector3(0f, 0f, -5f); g.SnapCamera();
                    m.transform.position = new Vector3(0f, 0f, 100f);
                    m.hp = 100f;
                    float tl = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - tl < 9f && (m.transform.position - MapLayout.Lair).magnitude > 1.5f) yield return null;
                    leashOk = (m.transform.position - MapLayout.Lair).magnitude < 1.5f && m.hp >= m.maxHp - 0.5f;
                    Log("leash: back at the lair after " + (Time.realtimeSinceStartup - tl).ToString("0.0") + " s, hp " + F(m.hp) + " / " + F(m.maxHp));
                }
                Check("the Minotaur returns to his lair with full health when nobody is near him for 5 s", leashOk);

                // arrow to the lair: right distance and direction, hidden inside 30 m of it, boss bar only within 45 m
                k.transform.position = new Vector3(100f, 0f, 150f); g.SnapCamera();
                yield return W(0.4f);
                bool west = g.hud.LairArrowShown && g.hud.LairArrowText.Contains("100") && Mathf.Abs(g.hud.LairArrowAngle - 90f) < 1f;
                bool bossFar = !g.hud.BossPanelShown;
                k.transform.position = new Vector3(0f, 0f, 135f); g.SnapCamera();
                yield return W(0.4f);
                bool hiddenNear = !g.hud.LairArrowShown && g.hud.BossPanelShown;
                k.transform.position = new Vector3(0f, 0f, -5f); g.SnapCamera();
                yield return W(0.4f);
                Check("lair arrow: 100 m away it points west (" + g.hud.LairArrowText + "), is hidden in the lair, and the boss bar shows only within 45 m", west && bossFar && hiddenNear);
                Shot("lair_arrow_meadow");
                foreach (Enemy e in new List<Enemy>(Enemy.All)) if (e is Slime) Destroy(e.gameObject);
                for (int i = 0; i < 5; i++) Slime.Spawn(0, new Vector3(-15.5f + i * 0.3f, 0f, -15.5f), g.transform);      // the usual five green slimes for the tests below
                yield return W(0.3f);
                g.packsOff = true;
                ParkSlimes();
            }

            // ------------------------------------------------ movement (real key path)
            Vector3 p0 = k.transform.position;
            Keys(Key.W);
            yield return W(0.5f);
            Keys(Key.D);
            yield return W(0.5f);
            Keys();
            yield return null;
            Vector3 p1 = k.transform.position;
            Log("move: W 0.5s then D 0.5s: " + V(p0) + " -> " + V(p1));
            Check("WASD moves the knight on X and Z", p1.z - p0.z > 1.5f && p1.x - p0.x > 1.5f);
            Shot("moved");

            // ------------------------------------------------ basic attacks on the dummy
            Progress.ForceCrit = 1;
            float sideSlashTime = 0f;
            int swingSounds = 0, grunt0 = Snds(Snd.Grunt);
            bool firstHitSound = false;
            for (int i = 0; i < Knight.Attacks.Length; i++)
            {
                AttackDef a = Knight.Attacks[i];
                Heal();
                StandNear(d, 1.3f);
                yield return W(0.35f);
                float s0 = k.stamina, hp0 = d.hp, ar0 = d.armor;
                int h0 = d.hits;
                float t0 = Time.time;
                int sw0 = Snds(Snd.Swing), ai0 = Snds(Snd.ArmorImpact), sl0 = Snds(Snd.Slice);
                k.DebugAttack(i);
                yield return null;
                bool noSoundYet = Snds(Snd.Swing) == sw0;              // the whoosh belongs to the strike, not the wind-up
                yield return W(a.windupTime + a.strikeTime * 0.7f);
                Shot("attack" + (i + 1) + "_" + a.name.Replace(" ", ""));
                yield return WaitIdle(2f);
                float took = Time.time - t0;
                if (i == 0) sideSlashTime = took;
                Log("attack " + (i + 1) + " " + a.name + ": stamina " + F(s0) + " -> " + F(k.stamina) + ", dummy armor " + F(ar0) + " -> " + F(d.armor)
                    + ", hp " + F(hp0) + " -> " + F(d.hp) + ", hits +" + (d.hits - h0) + ", last " + d.lastHit + ", duration " + F(took) + "s");
                Check("attack " + (i + 1) + " hits the dummy for base damage " + a.damage + " and costs no stamina", d.hits - h0 == 1 && Mathf.Abs(d.lastDamage - a.damage) < 0.01f && k.stamina >= s0 - 0.01f);
                if (Snds(Snd.Swing) - sw0 == 1 && noSoundYet) swingSounds++;
                if (i == 0) firstHitSound = Snds(Snd.ArmorImpact) - ai0 == 1 && Snds(Snd.Slice) == sl0;
                Log("  sounds: swing +" + (Snds(Snd.Swing) - sw0) + " (silent during wind-up: " + noSoundYet + "), armor_impact +" + (Snds(Snd.ArmorImpact) - ai0)
                    + ", slice +" + (Snds(Snd.Slice) - sl0) + ", grunts so far " + (Snds(Snd.Grunt) - grunt0) + ", hit split armor " + F(d.lastToArmor) + " / hp " + F(d.lastToHp));
                yield return W(0.15f);
            }
            Check("each of the 5 sword attacks plays sword_swing once, at the strike", swingSounds == 5);
            Check("a hit soaked by the dummy's armor plays armor_impact (not slice)", firstHitSound);
            Check("heavy attacks (chop / spin / thrust) make the knight grunt, light slashes do not always", Snds(Snd.Grunt) - grunt0 >= 2 && Snds(Snd.Grunt) - grunt0 <= 5);
            {
                // same slash on bare straw: the slice sound instead
                yield return W(0.4f);
                Heal(); StandNear(d, 1.3f);
                d.armor = 0f;
                yield return W(0.3f);
                int ai0 = Snds(Snd.ArmorImpact), sl0 = Snds(Snd.Slice);
                k.DebugAttack(0);
                yield return WaitIdle(2f);
                Log("slash with the dummy's armor at 0: slice +" + (Snds(Snd.Slice) - sl0) + ", armor_impact +" + (Snds(Snd.ArmorImpact) - ai0) + ", hit split armor " + F(d.lastToArmor) + " / hp " + F(d.lastToHp));
                Check("a hit that reaches health plays sword_slice (not armor_impact)", Snds(Snd.Slice) - sl0 == 1 && Snds(Snd.ArmorImpact) == ai0);
                d.ResetAll();
            }

            // ------------------------------------------------ combo through real input (J key + mouse button)
            {
                Heal(); StandNear(d, 1.3f);
                yield return W(1.0f);
                int h0 = d.hits;
                yield return Tap(Key.J);
                bool atk1 = k.IsAttacking; string n1 = k.LastAction;
                yield return WaitIdle(2f);
                Vector2 scr = g.cam.WorldToScreenPoint(d.transform.position);
                k.aimOverride = null;                                  // real mouse aim from here
                SetMouse(scr, false);
                yield return null; yield return null;
                SetMouse(scr, true);
                yield return null; yield return null; yield return null;
                bool atk2 = k.IsAttacking; string n2 = k.LastAction;
                SetMouse(scr, false);
                yield return WaitIdle(2f);
                yield return W(0.1f);
                Vector3 toD = d.transform.position - k.transform.position; toD.y = 0f;
                Log("input: J -> attacking=" + atk1 + " (" + n1 + "), left click -> attacking=" + atk2 + " (" + n2 + "), dummy hits +" + (d.hits - h0)
                    + ", facing-vs-cursor angle " + Vector3.Angle(k.Forward, toD).ToString("0") + " deg");
                Check("J and left click run the combo (Side Slash then Overhead Chop)", atk1 && atk2 && n1 == "Side Slash" && n2 == "Overhead Chop");
                Check("knight faces the mouse cursor", Vector3.Angle(k.Forward, toD) < 15f);
                k.aimOverride = d.transform.position;
            }

            // ------------------------------------------------ block
            {
                Heal();
                SetMouse(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), false, true);       // right mouse button held = shield up
                yield return W(0.3f);
                float a0 = k.armor, s0 = k.stamina;
                int cl0 = Snds(Snd.Clash), ai0 = Snds(Snd.ArmorImpact);
                k.TakeDamage(20f, k.transform.position + k.Forward * 2f, "selftest");
                Log("block: blocking=" + k.IsBlocking + " armor " + F(a0) + " -> " + F(k.armor) + ", stamina " + F(s0) + " -> " + F(k.stamina)
                    + ", sword_clash +" + (Snds(Snd.Clash) - cl0) + ", armor_impact +" + (Snds(Snd.ArmorImpact) - ai0));
                Check("frontal block (right mouse button) cuts 20 damage to 4 and costs no stamina", k.IsBlocking && Mathf.Abs(a0 - k.armor - 4f) < 0.01f && Mathf.Abs(s0 - k.stamina) < 0.6f);
                Check("a successful block plays sword_clash (and nothing else)", Snds(Snd.Clash) - cl0 == 1 && Snds(Snd.ArmorImpact) == ai0);
                yield return W(0.1f);
                Shot("block");
                SetMouse(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), false, false);
                yield return W(0.2f);
                Heal();
                float a1 = k.armor, h1 = k.hp;
                k.armor = 10f;
                k.TakeDamage(30f, k.transform.position + k.Forward * 2f, "selftest");
                Log("unblocked 30 with 10 armor: armor 10 -> " + F(k.armor) + ", hp " + F(h1) + " -> " + F(k.hp));
                Check("armor absorbs first, the rest hits health", k.armor < 0.01f && Mathf.Abs(h1 - k.hp - 20f) < 0.01f);
                Heal();
                yield return W(0.2f);
                int ai1 = Snds(Snd.ArmorImpact), cl1 = Snds(Snd.Clash);
                k.TakeDamage(12f, k.transform.position + k.Forward * 2f, "selftest");
                Check("an unblocked blow on the knight's armor plays armor_impact", Snds(Snd.ArmorImpact) - ai1 == 1 && Snds(Snd.Clash) == cl1);
                Heal();
            }

            // ------------------------------------------------ run (Left Shift), dash (Space), Space no longer swings
            {
                ParkSlimes();
                Heal(); d.ResetAll();
                k.moveOverride = null;
                k.aimOverride = new Vector3(0f, 0f, 8f);
                yield return W(1.0f);                                   // dash cooldown, regen
                Vector3 home = new Vector3(9f, 0f, -10f);                  // 26 m from the parked slimes (they chase within 17 m)

                // walk 0.5 s, then run 0.5 s over the same ground
                k.transform.position = home; yield return W(0.2f);
                Keys(Key.D); yield return W(0.5f); Keys(); yield return null;
                float walkDist = (k.transform.position - home).magnitude;
                k.transform.position = home; Heal(); yield return W(0.2f);
                float stRun0 = k.stamina;
                Keys(Key.D, Key.LeftShift); yield return W(0.5f);
                bool wasRunning = k.Running;
                Keys(); yield return null;
                float runDist = (k.transform.position - home).magnitude;
                Log("run: walk 0.5 s = " + F(walkDist) + " m, run 0.5 s = " + F(runDist) + " m (x" + F(runDist / Mathf.Max(0.01f, walkDist)) + "), stamina " + F(stRun0) + " -> " + F(k.stamina) + ", running=" + wasRunning);
                Check("Left Shift runs about 1.5x faster and costs no stamina", wasRunning && runDist / walkDist > 1.35f && runDist / walkDist < 1.65f && k.stamina >= stRun0 - 0.01f);

                {
                    int want = Mathf.FloorToInt(k.MaxStamina / Knight.DashCost) - 1;
                    bool placed = g.hud.StaminaTicksShown == want && want >= 1;
                    for (int i = 0; i < want && placed; i++)
                        placed &= Mathf.Abs(g.hud.StaminaTickX(i) - (3f + 282f * (i + 1) * Knight.DashCost / k.MaxStamina)) < 1.5f;
                    Check("the stamina bar has a divider every 25 points (" + g.hud.StaminaTicksShown + " dividers for max " + F(k.MaxStamina) + ")", placed);
                }

                // dash: 6 m, -25 stamina, 0.5 s untouchable, 0.8 s cooldown
                k.transform.position = home; Heal(); yield return W(1.0f);
                k.aimOverride = home + new Vector3(0f, 0f, 8f);       // looking north
                Vector3 q0 = k.transform.position;
                float stD0 = k.stamina; int sw0 = Snds(Snd.Swing), acts0 = k.ActionsSeen;
                float tDash = Time.time;
                Keys(Key.D, Key.Space);
                yield return null; yield return null; yield return null;
                Keys();
                float stAfter = k.stamina;
                yield return W(0.12f);
                bool dashing = k.Dashing;
                yield return W(0.2f);
                float dashDist = (k.transform.position - q0).magnitude;
                float hpD = k.hp, arD = k.armor;
                int dodge0 = k.DodgeCount;
                k.TakeDamage(30f, k.transform.position + Vector3.forward * 2f, "selftest dash");
                bool unhurt = Mathf.Approximately(k.hp, hpD) && Mathf.Approximately(k.armor, arD);
                int popups = k.DodgeCount - dodge0;
                Log("dash: " + F(dashDist) + " m sideways, stamina " + F(stD0) + " -> " + F(stAfter) + ", dashing=" + dashing + ", " + F(Time.time - tDash) + " s in, blow ignored=" + unhurt + " (DODGE popup " + popups + "), swing sound +" + (Snds(Snd.Swing) - sw0));
                Check("Space dashes ~6 m in the pressed direction and costs 25 stamina", dashDist > 5.4f && dashDist < 6.6f && Mathf.Abs(stD0 - stAfter - Knight.DashCost) < 1.5f && k.ActionsSeen == acts0 + 1);
                Check("a blow during the dash does no damage and shows DODGE", dashing && unhurt && popups == 1);
                float tPass = Time.time - tDash;
                // second Space inside the 0.8 s cooldown is refused
                Vector3 qMid = k.transform.position; float stMid = k.stamina;
                yield return Tap(Key.Space);
                Check("a second dash inside the 0.8 s cooldown is refused", Mathf.Abs(k.stamina - stMid) < 3f && (k.transform.position - qMid).magnitude < 0.5f && k.ActionsSeen == acts0 + 1);
                // after 0.55 s the protection is over
                while (Time.time - tDash < 0.56f) yield return null;
                float hpE = k.hp, arE = k.armor;
                k.TakeDamage(10f, k.transform.position + Vector3.forward * 2f, "selftest after dash");
                Check("0.5 s after the dash the knight can be hurt again", !k.Dashing && (k.hp < hpE - 0.01f || k.armor < arE - 0.01f));
                Heal(); yield return W(1.0f);

                // no direction held: a hop backwards (away from the cursor)
                k.transform.position = home; k.aimOverride = home + new Vector3(0f, 0f, 8f); yield return W(0.3f);
                Vector3 b0 = k.transform.position;
                yield return Tap(Key.Space);
                yield return W(0.35f);
                Vector3 bd = k.transform.position - b0;
                Log("dash without direction: moved (" + F(bd.x) + ", " + F(bd.z) + ")");
                Check("a dash with no direction moves backwards from the cursor", bd.z < -5f && Mathf.Abs(bd.x) < 0.8f);

                // not enough stamina: refused, the bar flashes red
                Heal(); yield return W(1.0f);
                k.stamina = 10f;
                Vector3 n0 = k.transform.position; int acts1 = k.ActionsSeen;
                yield return Tap(Key.Space);
                yield return W(0.2f);
                Check("a dash with less than 25 stamina is refused", (k.transform.position - n0).magnitude < 0.5f && k.ActionsSeen == acts1 && Time.unscaledTime - k.StaminaDeniedTime < 0.6f);
                Heal(); yield return W(1.0f);

                // Space does not swing; a dash cuts a swing short
                int comboBefore = k.ActionsSeen;
                k.aimOverride = home + new Vector3(0f, 0f, 8f);
                k.DebugAttack(1);                                       // Overhead Chop: slow enough to catch
                yield return W(0.08f);
                bool swinging = k.IsAttacking;
                yield return Tap(Key.Space);
                yield return null;
                Check("a dash cancels a sword swing in progress", swinging && !k.IsAttacking && k.Dashing);
                yield return W(0.9f);
                int actsB = k.ActionsSeen;
                yield return Tap(Key.Space);
                yield return W(0.3f);
                Check("Space only dashes: it never starts a sword attack", k.LastAction == "Dash" && k.ActionsSeen == actsB + 1);
                Heal(); yield return W(1.0f);
                k.transform.position = new Vector3(2f, 0f, -5f);
                k.aimOverride = d.transform.position;
            }

            // ------------------------------------------------ sword drop, pickup, inventory panel, the six swords
            {
                Inventory inv = k.inventory;
                AttackDef slash = Knight.Attacks[0];
                ParkSlimes();
                Check("the knight starts with only the Iron Sword, equipped", inv.OwnedCount == 1 && inv.equipped == Weapons.Iron && k.SwordModel.name == "Iron Sword");

                // inventory rules on a scratch object: new > +1 .. +5 > EXP
                var scratch = new Inventory();
                string seq = "";
                for (int n = 0; n < 8; n++) seq += scratch.Add(Weapons.Fang);
                Check("duplicates: new, then +1..+5, then EXP (" + seq + ")", seq == "01111122" && scratch.plus[Weapons.Fang] == Weapons.MaxPlus);
                int[] dropCount = new int[Weapons.All.Length];
                for (int n = 0; n < 1000; n++) dropCount[Weapons.SlimeDrop(n / 1000f)]++;
                Log("slime drop table over 1000 even rolls: rapier " + dropCount[Weapons.Rapier] + ", greatsword " + dropCount[Weapons.Greatsword] + ", fang " + dropCount[Weapons.Fang]
                    + ", banana dao " + dropCount[Weapons.Dao] + ", iron " + dropCount[Weapons.Iron] + ", boss " + dropCount[Weapons.Boss] + "; chance green/blue/red "
                    + Weapons.SlimeDropChance[0] + "/" + Weapons.SlimeDropChance[1] + "/" + Weapons.SlimeDropChance[2] + ", minotaur " + Weapons.MinotaurDropChance);
                Check("slimes only drop the four slime swords, in a 40.5 / 31.5 / 18 / 10 split",
                    Mathf.Abs(dropCount[Weapons.Rapier] - 405) <= 1 && Mathf.Abs(dropCount[Weapons.Greatsword] - 315) <= 1 && Mathf.Abs(dropCount[Weapons.Fang] - 180) <= 1 && Mathf.Abs(dropCount[Weapons.Dao] - 100) <= 1
                    && dropCount[Weapons.Iron] == 0 && dropCount[Weapons.Boss] == 0);

                // a slime killed with the drop forced on: the same RollDrop path as every kill
                Slime victim = Nearest<Slime>(corner);
                Vector3 dropAt = new Vector3(8f, 0f, -10f);            // far from the parked slimes (they chase within 17 m)
                victim.Warp(dropAt);
                yield return null;
                int pk0 = Pickup.All.Count;
                g.dropOverride = 1;
                victim.TakeHit(99999f, 1f, dropAt + Vector3.right, false, 0f, 0f, "selftest");
                g.dropOverride = -1;
                yield return W(0.5f);
                Pickup pk = g.lastPickup;
                bool dropped = Pickup.All.Count == pk0 + 1 && pk != null && System.Array.IndexOf(Weapons.SlimeDropIds, pk.weaponId) >= 0;
                Check("a killed slime leaves a sword pickup on the ground (drop forced on by the self-test)", dropped);
                int firstId = dropped ? pk.weaponId : Weapons.Rapier;
                if (!dropped) pk = g.SpawnPickup(firstId, dropAt);
                Vector3 pkPos = pk.transform.position;
                k.transform.position = pkPos + new Vector3(-3.2f, 0f, 0f);
                k.aimOverride = pkPos;
                g.camZoom = 0.55f;
                yield return W(0.7f);
                Shot("pickup_on_ground");
                yield return null;
                bool farKeeps = Pickup.All.Contains(pk) && !inv.owned[firstId];      // 3.2 m away: not collected yet
                k.moveOverride = new Vector2(1f, 0f);
                float tw = Time.realtimeSinceStartup;
                while (Pickup.All.Contains(pk) && Time.realtimeSinceStartup - tw < 2.5f) yield return null;
                k.moveOverride = null;
                float walked = (k.transform.position - (pkPos + new Vector3(-3.2f, 0f, 0f))).magnitude;
                yield return null; yield return null;
                Log("pickup: " + Weapons.All[firstId].name + " at " + V(pkPos) + ", knight walked " + F(walked) + " m, owned=" + inv.owned[firstId]
                    + ", still equipped " + k.Weapon.name + ", notice \"" + g.hud.NoticeShown + "\"");
                Check("walking over the pickup collects it, keeps the old sword equipped and shows a notice",
                    farKeeps && inv.owned[firstId] && !Pickup.All.Contains(pk) && inv.equipped == Weapons.Iron && g.hud.NoticeShown.Contains(Weapons.All[firstId].name));
                yield return W(0.25f);
                Shot("pickup_notice");
                yield return null;

                // a second one of the same sword refines it
                g.SpawnPickup(firstId, k.transform.position);
                yield return W(0.7f);
                Log("duplicate: " + inv.NameOf(firstId) + " damage x" + F(inv.DamageOf(firstId)) + ", notice \"" + g.lastNotice + "\"");
                Check("a duplicate refines the sword to +1 (+5% damage)",
                    inv.plus[firstId] == 1 && Mathf.Abs(inv.DamageOf(firstId) - Weapons.All[firstId].damage * 1.05f) < 0.0001f && g.lastNotice.Contains("refined") && Pickup.All.Count == 0);
                inv.plus[firstId] = 0;                                  // back to +0 so the table below is measured with the plain stats

                // the Tab window with only two swords owned: pictures for the owned ones, dark cut-outs and "???" for the rest
                yield return Tap(Key.Tab);
                yield return W(0.4f);
                {
                    bool shapes = true, hidden = true;
                    string names = "";
                    for (int i = 0; i < Weapons.All.Length; i++)
                    {
                        bool own = inv.owned[i];
                        names += g.hud.SlotName(i) + " | ";
                        if (own) shapes &= g.hud.IconTexture(i) == SwordIcons.Get(i) && g.hud.SlotName(i) == Weapons.All[i].name;
                        else hidden &= g.hud.IconTexture(i) == SwordIcons.Shadow(i) && g.hud.SlotName(i) == "???";
                    }
                    Log("tab window slots: " + names + "equipped mark on slot 0=" + g.hud.SlotEquippedMark(0) + ", panel " + g.hud.TabPanelRect.rect.width + "x" + g.hud.TabPanelRect.rect.height);
                    Check("Tab window: owned swords show their picture, unowned ones a dark cut-out and '???'", shapes && hidden && inv.OwnedCount == 2);
                    Check("Tab window: the equipped slot is marked and the window is about 1000 x 520",
                        g.hud.SlotEquippedMark(Weapons.Iron) && !g.hud.SlotEquippedMark(firstId) && g.hud.TabPanelRect.rect.width > 990f && g.hud.TabPanelRect.rect.height > 510f);
                    Check("Tab window: CHARACTER and SOUL / Kills text are filled in", g.hud.TabCharacterText.Contains("Lv ") && g.hud.TabSoulText.Contains("SOUL") && g.hud.TabSoulText.Contains("Kills"));
                    Shot("tab_window_two_swords");
                }
                yield return Tap(Key.Tab);
                yield return W(0.3f);

                // the rest are handed out as pickups under his feet (self-test shortcut, not kills)
                for (int id = 1; id < Weapons.All.Length; id++)
                    if (!inv.owned[id]) g.SpawnPickup(id, k.transform.position + new Vector3(0.25f * id, 0f, 0f));
                yield return W(0.8f);
                Check("all six swords are owned after collecting the pickups", inv.OwnedCount == 6 && Pickup.All.Count == 0);
                g.camZoom = 1f;

                // --- the Tab window: open, equip by key, equip by click, close ---
                yield return Tap(Key.Tab);
                yield return W(0.3f);
                Check("Tab opens the inventory window and pauses the game", g.InventoryOpen && g.hud.InventoryVisible && Time.timeScale == 0f);
                Shot("inventory_panel");
                yield return null;
                yield return Tap(Key.Digit2);
                yield return null;
                bool byKey = inv.equipped == Weapons.Rapier && k.SwordModel.name == "Swift Rapier";
                Check("key 2 in the panel equips the Swift Rapier and swaps the held model", byKey);
                yield return Tap(Key.Digit6);
                yield return W(0.3f);
                bool byKey6 = inv.equipped == Weapons.Dao && k.SwordModel.name == "Banana Dao" && g.hud.SlotEquippedMark(Weapons.Dao) && !g.hud.SlotEquippedMark(Weapons.Rapier);
                Check("key 6 in the panel equips the Banana Dao (6th slot, model from file) and swaps the held model", byKey6);
                Shot("inventory_banana_dao_equipped");
                yield return null;
                for (int key1 = 0; key1 < 5; key1++)         // the old keys still work: 1..5 each pick their own sword
                {
                    Key[] digits = { Key.Digit1, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit2 };
                    int[] want = { Weapons.Iron, Weapons.Greatsword, Weapons.Fang, Weapons.Boss, Weapons.Rapier };
                    yield return Tap(digits[key1]);
                    yield return null;
                    Check("key " + (System.Array.IndexOf(new[] { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 }, digits[key1]) + 1) + " in the panel still equips " + Weapons.All[want[key1]].name,
                        inv.equipped == want[key1] && k.SwordModel.name == Weapons.All[want[key1]].name);
                }
                Vector2 rowPos = g.hud.RowRect(Weapons.Greatsword).position;
                SetMouse(rowPos, false);
                yield return null; yield return null; yield return null;
                Shot("inventory_hover_rapier_equipped");
                yield return null;
                yield return Click(rowPos);
                yield return null;
                Log("panel: after key 2 -> " + (byKey ? "Swift Rapier" : "?") + ", after click on row 3 at " + rowPos + " -> " + k.Weapon.name + " (model " + k.SwordModel.name + ")");
                Check("clicking row 3 equips the Iron Greatsword and swaps the held model", inv.equipped == Weapons.Greatsword && k.SwordModel.name == "Iron Greatsword");
                yield return Tap(Key.Tab);
                yield return W(0.3f);
                Check("Tab closes the window and the game resumes", !g.InventoryOpen && !g.hud.InventoryVisible && Time.timeScale > 0f);

                // Esc with the window open closes only the window: no "Press Esc again" notice, and the next Esc does not leave
                yield return Tap(Key.Tab);
                yield return W(0.25f);
                bool openedAgain = g.InventoryOpen;
                yield return Tap(Key.Escape);
                yield return W(0.25f);
                Check("Esc closes the open Tab window and does not start the 'Esc again' leave prompt",
                    openedAgain && !g.InventoryOpen && !g.hud.InventoryVisible && Time.timeScale > 0f && !g.hud.NoticeShown.Contains("Esc again") && g.InSession);

                // --- every sword on the dummy: model, plain hit, forced crit, swing time, stamina, crit chance ---
                float ironTime = 0f;
                float[] tipDist = new float[Weapons.All.Length];
                bool distinct = true;
                Progress pr = k.progress;
                for (int id = 0; id < Weapons.All.Length; id++)
                {
                    WeaponDef w = Weapons.All[id];
                    ParkSlimes();
                    g.EquipWeapon(id);                                  // direct call; the key / click path was checked above
                    yield return W(0.6f);
                    d.ResetAll(); Heal(); StandNear(d, 1.3f);
                    g.camZoom = 0.36f;
                    yield return W(0.6f);
                    Shot("sword" + (id + 1) + "_" + w.name.Replace(" ", "").Replace("'", ""));
                    yield return W(0.3f);                              // let the screenshot's slow frame pass before timing a swing
                    Transform tip = Find(k.SwordModel, "Tip");
                    tipDist[id] = tip != null ? (tip.position - k.SwordModel.position).magnitude : -1f;
                    int parts = k.SwordModel.GetComponentsInChildren<MeshRenderer>().Length;
                    for (int o = 0; o < id; o++) if (Mathf.Abs(tipDist[o] - tipDist[id]) < 0.03f) distinct = false;

                    Progress.ForceCrit = 1;
                    float st0 = k.stamina, tA = Time.time;
                    k.DebugAttack(0);
                    float cost = st0 - k.stamina;
                    yield return WaitIdle(3f);
                    float took = Time.time - tA, plain = d.lastDamage;
                    if (id == 0) ironTime = took;
                    yield return W(0.6f);
                    d.ResetAll(); Heal(); StandNear(d, 1.3f);
                    yield return W(0.3f);
                    Progress.ForceCrit = 2;
                    k.DebugAttack(0);
                    yield return WaitIdle(3f);
                    float critHit = d.lastDamage;
                    Progress.ForceCrit = 0;
                    int crits = 0;
                    const int Rolls = 4000;
                    for (int n = 0; n < Rolls; n++) { bool c; k.RollDamage(-1, 10f, out c); if (c) crits++; }
                    float chance = crits / (float)Rolls;

                    float wantPlain = slash.damage * w.damage, wantCrit = wantPlain * (Progress.BaseCritMult + w.critMult), wantCost = 0f;                         // stamina is for the dash only
                    float wantChance = Progress.BaseCritChance + w.critChance, speedSeen = ironTime / took;
                    Log("sword " + (id + 1) + " " + w.name + " [" + w.rarity + "]: model '" + k.SwordModel.name + "' " + parts + " parts, hand-to-tip " + F(tipDist[id]) + " m"
                        + " | hit " + F(plain) + " (want " + F(wantPlain) + ") | crit hit " + F(critHit) + " (want " + F(wantCrit) + ")"
                        + " | swing " + F(took) + "s = x" + F(speedSeen) + " of iron (want x" + F(w.speed) + ") | stamina " + F(cost) + " (want " + F(wantCost) + ")"
                        + " | crit chance over " + Rolls + " rolls " + F(chance * 100f) + "% (want " + F(wantChance * 100f) + "%)"
                        + " | reach " + F(k.Reach(slash)) + " m (want " + F(slash.reach * w.reach) + ")");
                    bool ok = k.SwordModel.name == w.name && tip != null && Mathf.Abs(plain - wantPlain) < 0.01f && Mathf.Abs(critHit - wantCrit) < 0.01f
                        && Mathf.Abs(cost - wantCost) < 0.05f && Mathf.Abs(chance - wantChance) < 0.03f && Mathf.Abs(k.Reach(slash) - slash.reach * w.reach) < 0.001f
                        && Mathf.Abs(speedSeen - w.speed) < 0.15f * w.speed;
                    Check("sword " + (id + 1) + " " + w.name + ": model, damage x" + w.damage + ", speed x" + w.speed + ", crit +" + (w.critChance * 100f) + "% / +" + w.critMult + "x, reach x" + w.reach, ok);
                    if (!string.IsNullOrEmpty(w.modelPath))
                    {
                        // a model from a file: axes (+Z hand to tip, hand at the origin), no stray camera / light / collider, our colours
                        Transform fileRoot = Find(k.SwordModel, "File");
                        float zMin = 9f, zMax = -9f, xAbs = 0f;
                        foreach (Renderer rd in k.SwordModel.GetComponentsInChildren<Renderer>())
                        {
                            Bounds bb = rd.localBounds;                  // the renderer's own box: exact whatever pose the sword is in
                            for (int cx = 0; cx < 8; cx++)
                            {
                                Vector3 pw = rd.transform.TransformPoint(bb.center + Vector3.Scale(bb.extents, new Vector3((cx & 1) == 0 ? -1f : 1f, (cx & 2) == 0 ? -1f : 1f, (cx & 4) == 0 ? -1f : 1f)));
                                Vector3 pl = k.SwordModel.InverseTransformPoint(pw);
                                zMin = Mathf.Min(zMin, pl.z); zMax = Mathf.Max(zMax, pl.z); xAbs = Mathf.Max(xAbs, Mathf.Abs(pl.x));
                            }
                        }
                        int stray = k.SwordModel.GetComponentsInChildren<Camera>(true).Length + k.SwordModel.GetComponentsInChildren<Light>(true).Length
                            + k.SwordModel.GetComponentsInChildren<Collider>(true).Length + k.SwordModel.GetComponentsInChildren<MonoBehaviour>(true).Length;
                        Material[] mats = fileRoot != null ? fileRoot.GetComponentInChildren<MeshRenderer>().sharedMaterials : new Material[0];
                        bool colours = mats.Length == 3 && mats[0].GetColor(Prims.BaseColor) == w.bladeColor && mats[1].GetColor(Prims.BaseColor) == w.guardColor && mats[2].GetColor(Prims.BaseColor) == w.gripColor;
                        Log("file model " + w.modelPath + ": local z " + F(zMin) + " .. " + F(zMax) + ", |x| up to " + F(xAbs) + ", tip marker " + V(tip.localPosition)
                            + ", stray components " + stray + ", materials " + mats.Length + " colours ok=" + colours);
                        Check("sword " + (id + 1) + " file model points +Z (tip near z 1.39, hand/pommel near z -0.17, hand at the origin), has no camera / light / collider / script, uses the weapon colours",
                            fileRoot != null && zMax > 1.3f && zMax < 1.55f && zMin > -0.3f && zMin < 0f && xAbs < 0.45f && stray == 0 && colours);
                    }
                    yield return W(0.3f);
                }
                Check("the six sword models have six different lengths", distinct);
                g.camZoom = 1f;

                // reach on the field: from 3.6 m the Iron Sword misses the dummy, the Greatsword reaches it
                ParkSlimes();
                g.EquipWeapon(Weapons.Iron);
                d.ResetAll(); Heal(); StandNear(d, 3.05f);
                yield return W(0.4f);
                Progress.ForceCrit = 1;
                int hr = d.hits;
                k.DebugAttack(0);
                yield return WaitIdle(3f);
                bool ironMiss = d.hits == hr;
                g.EquipWeapon(Weapons.Greatsword);
                Heal(); StandNear(d, 3.05f);
                yield return W(0.4f);
                k.DebugAttack(0);
                yield return WaitIdle(3f);
                bool greatHit = d.hits == hr + 1;
                Log("reach from 3.6 m: Iron Sword hit=" + !ironMiss + ", Iron Greatsword hit=" + greatHit
                    + "; with the Greatsword: Whirlwind dmg x" + F(k.DamageMult(0)) + ", Shield Bash dmg x" + F(k.DamageMult(2)) + " (shield, no sword bonus)");
                Check("the Greatsword's longer reach hits where the Iron Sword misses", ironMiss && greatHit);
                Check("sword damage applies to sword skills but not to Shield Bash", Mathf.Abs(k.DamageMult(0) - 1.7f) < 0.001f && Mathf.Abs(k.DamageMult(2) - 1f) < 0.001f);

                // back to the Iron Sword through the panel (key 1), so the older checks below see base numbers
                yield return W(0.4f);
                yield return Tap(Key.Tab);
                yield return W(0.25f);
                yield return Tap(Key.Digit1);
                yield return Tap(Key.Tab);
                yield return W(0.3f);
                Check("key 1 in the panel re-equips the Iron Sword", inv.equipped == Weapons.Iron && k.SwordModel.name == "Iron Sword" && !g.InventoryOpen);
                d.ResetAll(); Heal(); StandNear(d, 1.3f);
                Progress.ForceCrit = 1;
            }

            // ------------------------------------------------ skills (real keys Q / E / R / F)
            Key[] skillKeys = { Key.Q, Key.E, Key.R, Key.F };
            int killsBefore = g.progress.kills;
            float expBefore = g.progress.totalExp;
            int skillGrunts0 = Snds(Snd.Grunt), skillSwings0 = Snds(Snd.Swing);
            for (int i = 0; i < 4; i++)
            {
                SkillDef s = Knight.Skills[i];
                yield return WaitIdle(2f);
                Heal();
                Enemy target = d;
                if (i >= 2)
                {
                    Slime sl = Nearest<Slime>(i == 2 ? corner : k.transform.position);
                    if (sl != null) target = sl;
                }
                Vector3 tp = target.transform.position;
                Vector3 inward = new Vector3(-tp.x, 0f, -tp.z).normalized;       // stand on the side facing the arena centre
                if (i == 1) { k.transform.position = tp + Vector3.left * 3.5f; k.aimOverride = tp; }
                else if (i == 2) { k.transform.position = tp + inward * (target.radius + 0.9f); k.aimOverride = tp; }
                else if (i == 3) { k.transform.position = tp + inward * 5.5f; k.aimOverride = tp; }
                else StandNear(target, 1.2f);
                yield return W(0.3f);
                if (target is Slime) { tp = target.transform.position; k.aimOverride = tp; yield return W(0.1f); }
                Vector3 kp0 = k.transform.position;
                float st0 = k.stamina, thp0 = target.hp + target.armor;
                int h0 = target.hits;
                yield return Tap(skillKeys[i]);
                bool started = k.IsAttacking && k.LastAction == s.name;
                float cd0 = k.cdMax[i];
                yield return W(i == 0 ? 0.45f : i == 3 ? 0.25f : 0.1f);
                Shot("skill" + s.key + "_" + s.name.Replace(" ", ""));
                yield return WaitIdle(3f);
                yield return W(0.1f);
                bool tdead = target == null || target.Dead;
                Log("skill " + s.key + " " + s.name + ": started=" + started + ", cooldown " + F(cd0) + "s (base " + s.cooldown + "), stamina " + F(st0) + " -> " + F(k.stamina)
                    + ", target " + target.label + " hits +" + (target.hits - h0) + " hp+armor " + F(thp0) + " -> " + F(target.hp + target.armor) + (tdead ? " (killed)" : "")
                    + ", last " + target.lastHit + ", stunned=" + target.Stunned + ", knight moved " + F((k.transform.position - kp0).magnitude) + " m");
                bool ok = started && Mathf.Abs(cd0 - s.cooldown) < 0.01f && target.hits > h0;
                if (i == 0) ok &= target.hits - h0 == 3;
                if (i == 1) ok &= (k.transform.position - kp0).magnitude > 4f;
                if (i == 2) ok &= tdead || target.Stunned;
                if (i == 3) ok &= (k.transform.position - kp0).magnitude > 3f;
                Check("skill " + s.key + " " + s.name + " works (cooldown, hit" + (i == 0 ? ", 3 ticks" : i == 1 ? ", dash" : i == 2 ? ", stun" : ", leap") + ")", ok);
                // pressing it again while on cooldown must be refused
                if (i == 0)
                {
                    float stB = k.stamina;
                    yield return Tap(skillKeys[i]);
                    Check("skill on cooldown is refused", !k.IsAttacking && Mathf.Abs(k.stamina - stB) < 2f && k.cd[0] > 0f);
                }
            }
            Log("skill sounds: grunts +" + (Snds(Snd.Grunt) - skillGrunts0) + " over 4 skills, sword_swing +" + (Snds(Snd.Swing) - skillSwings0) + " (Q 3 spins, E 1, F 1; the bash has none)");
            Check("every skill makes the knight grunt, sword skills play sword_swing", Snds(Snd.Grunt) - skillGrunts0 >= 3 && Snds(Snd.Swing) - skillSwings0 == 5);

            // ------------------------------------------------ kill slimes -> EXP -> first level-up by kills
            {
                Progress pr = g.progress;
                int kills0 = killsBefore;
                float exp0 = expBefore;
                int guard = 0;
                if (pr.level >= 2) Log("level 2 was already reached by the Ground Slam kills (kills " + (pr.kills - kills0) + ")");
                bool shotKill = false;
                while (pr.level < 2 && guard++ < 60)
                {
                    Slime sl = Nearest<Slime>(k.transform.position);
                    if (sl == null) { yield return W(0.5f); continue; }
                    Heal();
                    Vector3 sp = sl.transform.position;
                    k.transform.position = new Vector3(sp.x + 1.5f, 0f, sp.z);
                    k.aimOverride = sp;
                    yield return W(0.12f);
                    float before = pr.totalExp;
                    k.DebugAttack(guard % 2);
                    yield return W(0.14f);
                    if (!shotKill && sl.Dead) { shotKill = true; Shot("slime_killed"); }
                    yield return WaitIdle(1.5f);
                    if (pr.totalExp > before) Log("slime kill: " + sl.label + " exp " + F(before) + " -> " + F(pr.totalExp) + " (kills " + pr.kills + ", level " + pr.level + ")");
                }
                Check("killing slimes gives EXP and reaches level 2 (kills " + (pr.kills - kills0) + ", exp " + F(exp0) + " -> " + F(pr.totalExp) + ")",
                    pr.level >= 2 && pr.kills > kills0);

                // the level-up panel must open by itself and pause the game
                float t0 = Time.realtimeSinceStartup;
                while (!g.LevelUpOpen && Time.realtimeSinceStartup - t0 < 4f) yield return null;
                yield return W(0.4f);
                Shot("levelup_panel");
                yield return W(0.2f);
                Check("level-up panel opened with 3 choices and paused the game", g.LevelUpOpen && g.offers != null && g.offers.Length == 3 && Time.timeScale == 0f);
                if (g.LevelUpOpen)
                {
                    string all = "";
                    foreach (UpgradeOffer o in g.offers) all += " [" + o.title + " | " + o.line + "]";
                    Log("offers:" + all);
                    UpgradeOffer pick = g.offers[1];
                    float cur0 = pr.Current(pick.stat, pick.target);
                    int pts0 = pr.statPoints;
                    yield return Tap(Key.Digit2);
                    float cur1 = pr.Current(pick.stat, pick.target);
                    Log("picked card 2 with key '2': " + pick.title + " " + pick.line + ": x" + F(cur0) + " -> x" + F(cur1) + ", panel step " + g.LevelStep + ", unspent stat points " + pr.statPoints);
                    Check("key 2 applies the upgrade (+" + pick.amount + "x) and moves on to the stat point step",
                        g.LevelStep == 2 && Mathf.Abs(cur1 - cur0 - pick.amount) < 0.001f && pr.pendingChoices == 0 && pts0 >= 1 && Time.timeScale == 0f);
                    yield return W(0.3f);
                    Shot("statpoint_panel");
                    yield return W(0.15f);

                    // stat point by keyboard: 1 = HP
                    Heal();
                    float mh0 = k.MaxHp;
                    k.hp = mh0 - 30f;
                    float hpWas = k.hp;
                    int left0 = pr.statPoints;
                    yield return Tap(Key.Digit1);
                    Log("stat point by key 1 (HP): max health " + F(mh0) + " -> " + F(k.MaxHp) + ", health " + F(hpWas) + " -> " + F(k.hp) + ", HP points " + pr.Points(Attr.Hp)
                        + ", unspent " + left0 + " -> " + pr.statPoints + ", panel step " + g.LevelStep);
                    Check("key 1 puts the point into HP: +20 max health, panel closes",
                        !g.LevelUpOpen && pr.Points(Attr.Hp) == 1 && Mathf.Abs(k.MaxHp - mh0 - Progress.HpPerPoint) < 0.001f && Mathf.Abs(k.hp - hpWas - Progress.HpPerPoint) < 0.001f
                        && pr.statPoints == left0 - 1 && Mathf.Abs(k.MaxArmor - Knight.BaseArmor) < 0.001f && Mathf.Abs(k.MaxStamina - Knight.BaseStamina) < 0.001f);
                    yield return W(0.4f);
                    Check("game resumes after picking", Time.timeScale > 0f);
                }
            }

            // ------------------------------------------------ kill one slime with plain sword attacks
            {
                Progress pr = g.progress;
                float t0 = Time.realtimeSinceStartup;
                EnsureSlimes(3);
                yield return W(0.5f);
                while (Nearest<Slime>(k.transform.position) == null && Time.realtimeSinceStartup - t0 < 8f) yield return null;
                yield return W(0.7f);
                Slime sl = Nearest<Slime>(k.transform.position);
                float e0 = pr.totalExp;
                int soulsA = k.souls;
                int swings = 0;
                bool found = sl != null;                               // a dead slime is destroyed, so do not test the reference later
                while (found && !sl.Dead && swings < 14)
                {
                    Heal();
                    Vector3 sp = sl.transform.position;
                    k.transform.position = new Vector3(sp.x + 1.5f, 0f, sp.z);
                    k.aimOverride = sp;
                    yield return W(0.12f);
                    k.DebugAttack(swings++ % 2);
                    yield return W(0.15f);
                    if (sl.Dead) Shot("slime_killed_by_sword");
                    yield return WaitIdle(1.5f);
                }
                int slKind = found ? sl.kind : 0;
                bool ok = found && sl.Dead && pr.totalExp - e0 >= Slime.ExpFor(slKind) - 0.01f;
                Log("sword kill: " + (found ? sl.label + " maxHp " + F(sl.maxHp) + " dead=" + sl.Dead : "no slime") + " after " + swings + " swings, total exp " + F(e0) + " -> " + F(pr.totalExp));
                Check("sword attacks kill a slime and its EXP is granted", ok);
                yield return W(0.2f);
                Check("the kill adds that slime kind's Soul (green 1 / blue 3 / red 6) and the HUD row shows it", found && k.souls - soulsA >= Slime.SoulsFor(slKind) && g.hud.SoulShown == k.souls.ToString("N0"));
                yield return W(0.8f);
                yield return ClearLevelUps();
            }

            // ------------------------------------------------ food: Apple 15%, Roast Meat 40% of max health; full health does not eat
            {
                yield return WaitIdle(3f);
                yield return ClearLevelUps();
                ParkSlimes();
                k.moveOverride = Vector2.zero;
                Vector3 spot = new Vector3(9f, 0f, -10f);                // 26 m from the parked slimes
                k.transform.position = spot;
                k.aimOverride = spot + Vector3.forward * 5f;
                Heal();
                yield return W(0.6f);

                // drop rolls: the Apple is certain with the override, and appears next to the dead slime
                float tFood = Time.realtimeSinceStartup;
                EnsureSlimes(2);
                yield return W(0.5f);
                while (Nearest<Slime>(Vector3.zero) == null && Time.realtimeSinceStartup - tFood < 6f) yield return null;
                Slime food = Nearest<Slime>(new Vector3(-16.5f, 0f, -16.5f));
                int food0 = 0;
                foreach (Pickup q in Pickup.All) if (q.kind == PickupKind.Food) food0++;
                if (food != null)
                {
                    food.Warp(new Vector3(12f, 0f, 6f));
                    yield return null;
                    g.dropOverride = 0; g.foodOverride = 1;
                    food.TakeHit(99999f, 1f, food.transform.position + Vector3.right, false, 0f, 0f, "selftest");
                    g.dropOverride = -1; g.foodOverride = 0;
                }
                int food1 = 0;
                foreach (Pickup q in Pickup.All) if (q.kind == PickupKind.Food && q.weaponId == Foods.Apple) food1++;
                Check("a killed slime can leave an Apple on the ground (drop forced by the self-test)", food != null && food1 == food0 + 1);
                foreach (Pickup q in new System.Collections.Generic.List<Pickup>(Pickup.All)) Destroy(q.gameObject);
                yield return W(0.2f);
                yield return W(0.8f);                                    // the kill gave EXP: a level-up panel may open
                yield return ClearLevelUps();
                Heal();
                float max = k.MaxHp;
                k.hp = max * 0.4f;
                float hp0 = k.hp;
                Pickup apple = g.SpawnFood(Foods.Apple, spot + new Vector3(0.8f, 0f, 0f));
                float life = apple.Life;
                yield return W(0.9f);
                float gain = k.hp - hp0;
                Log("apple: hp " + F(hp0) + " -> " + F(k.hp) + " of " + F(max) + " (+" + F(gain) + ", want " + F(max * 0.15f) + "), lifetime " + F(life) + " s, notice \"" + g.lastNotice + "\"");
                Check("an Apple heals 15% of max health and is gone from the ground", Mathf.Abs(gain - max * 0.15f) < 1f && !Pickup.All.Contains(apple) && g.lastFoodHeal == Mathf.RoundToInt(max * 0.15f));
                Check("food lives 60 seconds on the ground (swords 90)", Mathf.Abs(life - 60f) < 0.01f && Mathf.Abs(Pickup.Lifetime - 90f) < 0.01f);

                k.hp = max * 0.2f;
                hp0 = k.hp;
                Pickup meat = g.SpawnFood(Foods.Meat, spot + new Vector3(-0.8f, 0f, 0f));
                yield return W(0.9f);
                gain = k.hp - hp0;
                Check("Roast Meat heals 40% of max health", Mathf.Abs(gain - max * 0.40f) < 1f && !Pickup.All.Contains(meat));

                k.hp = max * 0.95f;
                Pickup over = g.SpawnFood(Foods.Meat, spot + new Vector3(0f, 0f, 0.8f));
                yield return W(0.9f);
                Check("healing stops at full health (never above max)", Mathf.Abs(k.hp - max) < 0.01f && !Pickup.All.Contains(over) && g.lastFoodHeal == Mathf.RoundToInt(max * 0.05f));

                // full health: it stays, one "HP full" hint, and it expires on its own
                Heal();
                int hints0 = g.fullHints;
                Pickup stay = g.SpawnFood(Foods.Apple, spot + new Vector3(0.6f, 0f, 0f));
                yield return W(0.8f);
                bool still = Pickup.All.Contains(stay) && Mathf.Abs(k.hp - max) < 0.01f;
                int hints1 = g.fullHints;
                yield return W(0.6f);
                Check("with full health the food stays on the ground and says 'HP full' once", still && hints1 == hints0 + 1 && g.fullHints == hints1);
                stay.age = stay.Life - 0.3f;
                yield return W(0.8f);
                Check("food nobody ate disappears when its time is up", !Pickup.All.Contains(stay));
                yield return W(0.3f);
                Shot("food_eaten");
            }

            // ------------------------------------------------ Minotaur
            {
                Minotaur m = g.minotaur;
                Check("minotaur exists", m != null);
                if (m != null)
                {
                    Heal();
                    k.transform.position = m.transform.position + new Vector3(0f, 0f, -(m.radius + 1.4f));
                    k.aimOverride = m.transform.position;
                    yield return W(0.3f);
                    Shot("minotaur_close");
                    float mh0 = m.hp + m.armor;
                    int clash0 = Snds(Snd.Clash), mArmorImpact0 = Snds(Snd.ArmorImpact);
                    for (int i = 0; i < 5; i++)
                    {
                        Heal();
                        k.transform.position = m.transform.position - (m.transform.position - k.transform.position).normalized * (m.radius + 1.4f);
                        k.aimOverride = m.transform.position;
                        k.DebugAttack(i);
                        yield return WaitIdle(2f);
                        yield return W(0.05f);
                    }
                    yield return ClearLevelUps();
                    Log("minotaur: hp+armor " + F(mh0) + " -> " + F(m.hp + m.armor) + " after 5 sword attacks, state " + m.State + ", hits " + m.hits);
                    Check("sword attacks damage the Minotaur", m.hp + m.armor < mh0 - 30f && m.hits >= 3);
                    Log("minotaur hit sounds while his armor held: sword_clash +" + (Snds(Snd.Clash) - clash0) + ", armor_impact +" + (Snds(Snd.ArmorImpact) - mArmorImpact0) + ", armor left " + F(m.armor));
                    Check("sword blows on the Minotaur's armor play sword_clash", Snds(Snd.Clash) - clash0 >= 2);

                    // stand still and let it swing: screenshot each telegraph once
                    bool sawSweep = false, sawSmash = false, sawCharge = false;
                    float worstHp = k.MaxHp + k.MaxArmor;
                    Heal();
                    float tEnd = Time.realtimeSinceStartup + 16f;
                    Minotaur.S last = m.State;
                    float stateAt = Time.realtimeSinceStartup;
                    bool shotThis = false;
                    while (Time.realtimeSinceStartup < tEnd && !(sawSweep && sawSmash && sawCharge) && m != null && !m.Dead)
                    {
                        if (m.State != last) { last = m.State; stateAt = Time.realtimeSinceStartup; shotThis = false; }
                        float inState = Time.realtimeSinceStartup - stateAt;
                        if (!shotThis && inState > 0.55f)
                        {
                            if (m.State == Minotaur.S.SweepTele && !sawSweep) { sawSweep = true; shotThis = true; Shot("minotaur_sweep_telegraph"); }
                            if (m.State == Minotaur.S.SmashTele && !sawSmash) { sawSmash = true; shotThis = true; Shot("minotaur_smash_telegraph"); }
                            if (m.State == Minotaur.S.ChargeTele && !sawCharge) { sawCharge = true; shotThis = true; Shot("minotaur_charge_telegraph"); }
                        }
                        worstHp = Mathf.Min(worstHp, k.hp + k.armor);
                        if (k.hp < 50f) Heal();
                        if (g.LevelUpOpen) { yield return Tap(g.LevelStep == 2 ? StatKey() : Key.Digit1); tEnd += 1f; }
                        // once both melee attacks were seen, back off so that it charges
                        if (sawSweep && sawSmash && !sawCharge && m.State == Minotaur.S.Chase && (m.transform.position - k.transform.position).magnitude < 8f)
                        {
                            Vector3 away = m.transform.position.z > 0f ? new Vector3(0f, 0f, -11f) : new Vector3(0f, 0f, 11f);
                            k.transform.position = new Vector3(Mathf.Clamp(m.transform.position.x, -8f, 8f), 0f, m.transform.position.z + away.z);
                            k.aimOverride = m.transform.position;
                        }
                        yield return null;
                    }
                    Log("minotaur attacks seen: sweep=" + sawSweep + " smash=" + sawSmash + " charge=" + sawCharge + ", knight lowest hp+armor " + F(worstHp) + " / " + F(k.MaxHp + k.MaxArmor));
                    Check("Minotaur telegraphs and lands attacks on the knight", (sawSweep || sawSmash) && worstHp < k.MaxHp + k.MaxArmor - 5f);
                    yield return W(0.6f);
                    Shot("minotaur_fight");
                    yield return ClearLevelUps();
                }
            }

            // ------------------------------------------------ second level-up, picked with a mouse click
            {
                Progress pr = g.progress;
                yield return WaitIdle(2f);
                Heal();
                int need = Progress.Need(pr.level) - Mathf.FloorToInt(pr.exp);
                Log("granting " + need + " EXP directly (self-test shortcut, not from kills) to open the next level-up");
                g.GrantExp(need, k.transform.position + Vector3.up * 2f);
                float t0 = Time.realtimeSinceStartup;
                while (!g.LevelUpOpen && Time.realtimeSinceStartup - t0 < 4f) yield return null;
                yield return W(0.5f);
                if (g.LevelUpOpen)
                {
                    UpgradeOffer pick = g.offers[0];
                    float cur0 = pr.Current(pick.stat, pick.target);
                    Vector2 c = g.hud.CardRect(0).position;
                    SetMouse(c, false);
                    yield return null; yield return null; yield return null;
                    Shot("levelup_hover");
                    yield return null;
                    SetMouse(c, true);
                    yield return null; yield return null; yield return null;
                    SetMouse(c, false);
                    yield return null;
                    float cur1 = pr.Current(pick.stat, pick.target);
                    Log("picked card 1 with a mouse click at " + c + ": " + pick.title + " " + pick.line + ": x" + F(cur0) + " -> x" + F(cur1) + ", panel step " + g.LevelStep);
                    Check("mouse click applies the upgrade (+" + pick.amount + "x) and moves on to the stat point step", g.LevelStep == 2 && Mathf.Abs(cur1 - cur0 - pick.amount) < 0.001f);

                    // stat point by mouse: click the middle card = BLOCK
                    Heal();
                    float ma0 = k.MaxArmor, br0 = pr.BlockReduction;
                    int bp0 = pr.Points(Attr.Block);
                    Vector2 c2 = g.hud.CardRect(1).position;
                    SetMouse(c2, false);
                    yield return W(0.5f);                              // clicks are ignored for the first 0.35 s of a step
                    Shot("statpoint_hover_block");
                    yield return null;
                    yield return Click(c2);
                    Log("stat point by mouse click on card 2 (BLOCK) at " + c2 + ": max armor " + F(ma0) + " -> " + F(k.MaxArmor) + ", shield stops " + F(br0 * 100f) + "% -> " + F(pr.BlockReduction * 100f)
                        + "%, BLOCK points " + pr.Points(Attr.Block) + ", panel step " + g.LevelStep);
                    Check("mouse click puts the point into BLOCK: +10 max armor, shield stops 1.2% more",
                        !g.LevelUpOpen && pr.Points(Attr.Block) == bp0 + 1 && Mathf.Abs(k.MaxArmor - ma0 - Progress.ArmorPerPoint) < 0.001f
                        && Mathf.Abs(pr.BlockReduction - br0 - Progress.BlockPerPoint) < 0.0001f && Mathf.Abs(pr.BlockReduction - (0.80f + Progress.BlockPerPoint * pr.Points(Attr.Block))) < 0.0001f);
                    yield return W(0.4f);

                    // and the better block is what a real blocked blow uses
                    Heal();
                    k.aimOverride = k.transform.position + Vector3.right * 3f;
                    SetMouse(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), false, true);
                    yield return W(0.4f);
                    float ab = k.armor;
                    k.TakeDamage(20f, k.transform.position + k.Forward * 2f, "selftest");
                    float wantLoss = 20f * (1f - pr.BlockReduction);
                    Log("block with " + pr.Points(Attr.Block) + " BLOCK point(s): blocking=" + k.IsBlocking + ", 20 damage -> armor " + F(ab) + " -> " + F(k.armor) + " (want -" + F(wantLoss) + ", was -4.00 with no points)");
                    Check("a blocked 20 damage blow now costs less armor than the base 4 (20 x (1 - block%))", k.IsBlocking && wantLoss < 3.99f && Mathf.Abs(ab - k.armor - wantLoss) < 0.01f);
                    SetMouse(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), false, false);
                    yield return W(0.2f);
                    Heal();
                }
                else Check("second level-up panel opened", false);
                yield return W(0.4f);
            }

            // ------------------------------------------------ to max level; always take the first card
            {
                Progress pr = g.progress;
                // finish the Minotaur with one huge hit: checks its EXP reward and gets it out of the way
                Minotaur m = g.minotaur;
                Pickup bossDrop = null;
                if (m != null)
                {
                    float e0 = pr.totalExp, wantExp = m.expReward;
                    int souls0 = k.souls;
                    int l0 = pr.level;
                    g.foodOverride = -1;                                  // the Minotaur's own rule: two Roast Meats
                    m.TakeHit(99999f, 1f, k.transform.position, false, 0f, 0f, "selftest");
                    g.foodOverride = 0;
                    yield return W(0.3f);
                    Shot("minotaur_slain");
                    Log("minotaur finished by the self-test with a 99999 hit: total exp " + F(e0) + " -> " + F(pr.totalExp) + ", level " + l0 + " -> " + pr.level
                        + ", pending choices " + pr.pendingChoices + ", respawn in " + F(g.MinotaurRespawnIn) + "s");
                    Check("Minotaur death gives its EXP (250 x the scale from when it appeared: " + F(wantExp) + ") and starts the respawn timer", Mathf.Abs(pr.totalExp - e0 - wantExp) < 0.01f && g.minotaur == null && g.MinotaurRespawnIn > 15f);
                    Check("Minotaur death gives 50 Soul", k.souls - souls0 == Minotaur.SoulReward);
                    bossDrop = g.lastPickup;
                    Log("minotaur drop (normal 100% roll, not forced): " + (bossDrop != null ? Weapons.All[bossDrop.weaponId].name + " at " + V(bossDrop.transform.position) : "nothing") + ", pickups on the ground " + Pickup.All.Count);
                    Check("the Minotaur drops the Horned King's Blade", bossDrop != null && bossDrop.weaponId == Weapons.Boss && Pickup.All.Contains(bossDrop));
                    int meats = 0;
                    foreach (Pickup q in Pickup.All) if (q.kind == PickupKind.Food && q.weaponId == Foods.Meat) meats++;
                    Check("the Minotaur also leaves two Roast Meats", meats == Foods.MinotaurMeat);
                    foreach (Pickup q in new System.Collections.Generic.List<Pickup>(Pickup.All)) if (q.kind == PickupKind.Food) Destroy(q.gameObject);
                }
                Heal(); StandNear(d, 1.3f);
                int safety = 0;
                bool staminaDone = false;
                while ((!pr.IsMax || pr.pendingChoices > 0) && safety++ < 70)
                {
                    Heal();
                    if (pr.pendingChoices == 0 && !g.LevelUpOpen) g.GrantExp(Progress.Need(pr.level) - pr.exp, k.transform.position + Vector3.up * 2f);
                    float t0 = Time.realtimeSinceStartup;
                    while (!g.LevelUpOpen && Time.realtimeSinceStartup - t0 < 4f) yield return null;
                    yield return W(0.15f);
                    if (pr.level == 6) Shot("levelup_panel_lv6");
                    // prefer an "all attacks" card so the dummy check below sees a change
                    if (g.LevelStep == 1)
                    {
                        int idx = 0;
                        for (int i = 0; i < g.offers.Length; i++) if (g.offers[i].target == 0) idx = i;
                        yield return Tap(idx == 0 ? Key.Digit1 : idx == 1 ? Key.Digit2 : Key.Digit3);
                        yield return W(0.1f);
                    }
                    if (g.LevelStep == 2 && !staminaDone)
                    {
                        staminaDone = true;
                        int sp0 = pr.Points(Attr.Stamina);
                        // stat point by keyboard: 3 = STAMINA
                        Heal();
                        float ms0 = k.MaxStamina, rg0 = pr.StaminaRegenMult;
                        yield return Tap(Key.Digit3);
                        Log("stat point by key 3 (STAMINA): max stamina " + F(ms0) + " -> " + F(k.MaxStamina) + ", regen x" + F(rg0) + " -> x" + F(pr.StaminaRegenMult)
                            + ", STAMINA points " + pr.Points(Attr.Stamina));
                        Check("key 3 puts the point into STAMINA: +15 max stamina, +5% regen",
                            g.LevelStep != 2 && pr.Points(Attr.Stamina) == sp0 + 1 && Mathf.Abs(k.MaxStamina - ms0 - Progress.StaminaPerPoint) < 0.001f
                            && Mathf.Abs(pr.StaminaRegenMult - rg0 - Progress.RegenPerPoint) < 0.0001f);
                    }
                    else if (g.LevelStep == 2) yield return Tap(StatKey());
                    yield return W(0.35f);
                }
                Log("stat points after 24 level-ups: HP " + pr.Points(Attr.Hp) + ", BLOCK " + pr.Points(Attr.Block) + ", STAMINA " + pr.Points(Attr.Stamina) + ", unspent " + pr.statPoints
                    + " -> max health " + F(k.MaxHp) + " (100 + 10 x 24 levels + 20 x " + pr.Points(Attr.Hp) + "), max armor " + F(k.MaxArmor) + ", max stamina " + F(k.MaxStamina)
                    + ", shield stops " + F(pr.BlockReduction * 100f) + "%");
                Check("all 24 stat points are spent and add up",
                    pr.statPoints == 0 && pr.Points(Attr.Hp) + pr.Points(Attr.Block) + pr.Points(Attr.Stamina) == 24
                    && Mathf.Abs(k.MaxHp - (100f + 240f + 20f * pr.Points(Attr.Hp))) < 0.001f && Mathf.Abs(k.MaxArmor - (60f + 10f * pr.Points(Attr.Block))) < 0.001f
                    && Mathf.Abs(k.MaxStamina - (100f + Progress.StaminaPerPoint * pr.Points(Attr.Stamina))) < 0.001f);
                {
                    // the cap, on a scratch Progress: 6 points per stat, the 7th is refused
                    var scratch = new Progress { statPoints = 12 };
                    int spent = 0;
                    for (int n = 0; n < 12; n++) if (scratch.Spend(Attr.Block)) spent++;
                    Check("a stat takes at most 10 points (block tops out at 92%)", spent == Progress.AttrCap && scratch.statPoints == 2 && Mathf.Abs(scratch.BlockReduction - 0.92f) < 0.0001f && !scratch.CanSpend(Attr.Block) && scratch.CanSpend(Attr.Hp));
                }
                Log("EXP granted directly by the self-test up to max level. level " + pr.level + ", pending " + pr.pendingChoices
                    + ", all: dmg x" + F(pr.DamageMult(-1)) + " spd x" + F(pr.SpeedMult(-1)) + " (sword x" + F(pr.BasicSpeed()) + ") crit " + F(pr.CritChance(-1) * 100f) + "% x" + F(pr.CritMult(-1)));
                for (int i = 0; i < 4; i++)
                    Log("  skill " + Knight.Skills[i].key + ": dmg x" + F(pr.DamageMult(i)) + " spd x" + F(pr.SpeedMult(i)) + " crit " + F(pr.CritChance(i) * 100f) + "% x" + F(pr.CritMult(i)));
                Check("level caps at 25 with all 24 upgrades picked", pr.level == Progress.MaxLevel && pr.pendingChoices == 0 && !g.LevelUpOpen);
                int lv = pr.level;
                g.GrantExp(500f, k.transform.position);
                Check("no EXP past max level", pr.level == lv && pr.exp == 0f && pr.pendingChoices == 0);
                yield return W(0.5f);
                Shot("max_level_hud");

                // ------------------------------------------ the Minotaur's sword: second copy, so it refines the first to +1
                if (bossDrop != null && Pickup.All.Contains(bossDrop))
                {
                    yield return WaitIdle(2f);
                    Heal();
                    Vector3 bp = bossDrop.transform.position;
                    k.transform.position = bp + new Vector3(-2.6f, 0f, 0f);
                    k.aimOverride = bp;
                    g.camZoom = 0.6f;
                    yield return W(0.5f);
                    Shot("boss_sword_drop");
                    yield return null;
                    k.moveOverride = new Vector2(1f, 0f);
                    float tb = Time.realtimeSinceStartup;
                    while (Pickup.All.Contains(bossDrop) && Time.realtimeSinceStartup - tb < 2.5f) yield return null;
                    k.moveOverride = null;
                    g.camZoom = 1f;
                    yield return null;
                    Log("boss sword collected: " + k.inventory.NameOf(Weapons.Boss) + " damage x" + F(k.inventory.DamageOf(Weapons.Boss)) + ", notice \"" + g.lastNotice + "\"");
                    Check("the dropped boss sword is collected by walking over it (already owned, so it becomes +1: x1.575)",
                        !Pickup.All.Contains(bossDrop) && k.inventory.plus[Weapons.Boss] == 1 && Mathf.Abs(k.inventory.DamageOf(Weapons.Boss) - 1.575f) < 0.0001f);
                }
                else Check("boss sword pickup still on the ground after levelling", false);

                // ------------------------------------------ stamina regen with the STAMINA points
                {
                    yield return W(0.8f);
                    k.stamina = 10f;
                    yield return null;
                    float sA = k.stamina, tR = Time.time;
                    yield return W(0.5f);
                    float rate = (k.stamina - sA) / Mathf.Max(0.001f, Time.time - tR), wantRate = Knight.StaminaRegen * pr.StaminaRegenMult;
                    Log("stamina regen with " + pr.Points(Attr.Stamina) + " STAMINA point(s): " + F(rate) + "/s measured, want 26 x " + F(pr.StaminaRegenMult) + " = " + F(wantRate));
                    Check("STAMINA points speed up stamina regeneration", pr.Points(Attr.Stamina) >= 1 && Mathf.Abs(rate - wantRate) < wantRate * 0.06f);
                    Heal();
                }

                // ------------------------------------------ do the numbers reach combat?
                yield return W(2.6f);      // let the dummy rebuild / regen if needed
                d.ResetAll();
                Heal(); StandNear(d, 1.3f);
                yield return W(0.4f);
                Progress.ForceCrit = 1;
                float tA = Time.time;
                k.DebugAttack(0);
                yield return WaitIdle(2f);
                float took = Time.time - tA;
                float expect = Knight.Attacks[0].damage * k.DamageMult(-1);
                Log("after upgrades, Side Slash (no crit): dealt " + F(d.lastDamage) + ", expected 10 x " + F(k.DamageMult(-1)) + " = " + F(expect)
                    + "; duration " + F(took) + "s vs " + F(sideSlashTime) + "s at level 1 (sword speed x" + F(pr.BasicSpeed()) + ")");
                Check("damage upgrade multiplies the hit (Iron Sword equipped: " + (k.Weapon.id == Weapons.Iron) + ")", Mathf.Abs(d.lastDamage - expect) < 0.01f && k.Weapon.id == Weapons.Iron);
                if (pr.BasicSpeed() > 1.01f) Check("attack speed upgrade shortens the swing", took < sideSlashTime * 0.9f);
                else Log("no ALL attack-speed upgrade was rolled, swing time check skipped");

                yield return W(0.7f);
                d.ResetAll(); Heal(); StandNear(d, 1.3f);
                yield return W(0.3f);
                Progress.ForceCrit = 2;
                k.DebugAttack(0);
                yield return W(0.16f);
                Shot("crit_hit");
                yield return WaitIdle(2f);
                float expectCrit = expect * k.CritMult(-1);
                Log("forced crit Side Slash: dealt " + F(d.lastDamage) + " crit=" + d.lastCrit + ", expected " + F(expect) + " x " + F(pr.CritMult(-1)) + " = " + F(expectCrit));
                Check("crit multiplies the hit by the crit multiplier", d.lastCrit && Mathf.Abs(d.lastDamage - expectCrit) < 0.01f);
                Progress.ForceCrit = 0;

                // upgrades and a sword together: the Horned King's Blade +1 on top of the level-up totals
                yield return W(0.7f);
                g.EquipWeapon(Weapons.Boss);
                d.ResetAll(); Heal(); StandNear(d, 1.3f);
                g.camZoom = 0.36f;
                yield return W(0.5f);
                Shot("boss_sword_equipped_max_level");
                yield return null;
                Progress.ForceCrit = 2;
                k.DebugAttack(0);
                yield return WaitIdle(2f);
                Progress.ForceCrit = 0;
                g.camZoom = 1f;
                float bossDmg = k.inventory.DamageOf(Weapons.Boss);
                float comboCrit = Mathf.Min(Progress.CritMultCap, pr.CritMult(-1) + 0.5f);
                float comboWant = 10f * pr.DamageMult(-1) * bossDmg * comboCrit;
                Log("level-up totals x sword: forced crit with " + k.inventory.NameOf(Weapons.Boss) + " dealt " + F(d.lastDamage) + ", want 10 x " + F(pr.DamageMult(-1)) + " (upgrades) x " + F(bossDmg) + " (sword) x "
                    + F(comboCrit) + " (crit " + F(pr.CritMult(-1)) + " + 0.5) = " + F(comboWant)
                    + "; crit chance " + F(k.CritChance(-1) * 100f) + "% = " + F(pr.CritChance(-1) * 100f) + "% + 10%, swing speed x" + F(k.SwingSpeed) + " = " + F(pr.BasicSpeed()) + " x 1.1");
                Check("sword stats multiply / add on top of the level-up upgrades", Mathf.Abs(d.lastDamage - comboWant) < 0.02f
                    && Mathf.Abs(k.CritChance(-1) - Mathf.Min(Progress.CritChanceCap, pr.CritChance(-1) + 0.10f)) < 0.0001f
                    && Mathf.Abs(k.SwingSpeed - Mathf.Clamp(pr.BasicSpeed() * 1.1f, Knight.MinSwingSpeed, Knight.MaxSwingSpeed)) < 0.0001f);

                yield return W(0.5f);
                Heal();
                for (int i = 0; i < 4; i++) k.cd[i] = 0f;
                yield return Tap(Key.Q);
                float wantCd = Knight.Skills[0].cooldown / pr.SpeedMult(0);
                Log("Whirlwind cooldown after upgrades: " + F(k.cdMax[0]) + "s, expected 6 / " + F(pr.SpeedMult(0)) + " = " + F(wantCd));
                Check("attack speed divides skill cooldown", Mathf.Abs(k.cdMax[0] - wantCd) < 0.01f);
                yield return W(0.5f);
                Shot("whirlwind_upgraded");
                yield return WaitIdle(3f);
            }

            // ------------------------------------------------ new monsters scale with level
            {
                Slime sl = Nearest<Slime>(k.transform.position);
                Log("enemy scaling at level " + g.progress.level + ": hp x" + F(g.EnemyHpScale) + ", damage x" + F(g.EnemyDamageScale)
                    + ", wave " + g.wave + ", slimes alive " + g.slimesAlive + (sl != null ? ", nearest " + sl.label + " maxHp " + F(sl.maxHp) : ""));
            }

            // ------------------------------------------------ death and revive: back to level 1, swords + Soul stay, YOU DIED screen
            {
                yield return WaitIdle(3f);
                int owned0 = k.inventory.OwnedCount, equipped0 = k.inventory.equipped, ready0 = Snds(Snd.Ready);
                int lvBefore = g.progress.level, souls0 = k.souls, kills0 = g.progress.kills;
                float tDie = Time.realtimeSinceStartup;
                k.TakeDamage(9999f, k.transform.position + Vector3.right, "selftest");
                bool dead = k.Dead;
                yield return W(0.7f);
                Shot("knight_down");
                bool overlay = g.hud.DeathVisible;
                string lose = g.hud.DeathLoseText, keep = g.hud.DeathKeepText, count1 = g.hud.DeathCountText;
                float in1 = k.ReviveIn;
                bool stillAtLevel = g.progress.level == lvBefore;            // the reset happens when he stands up, not before
                yield return W(1.2f);
                string count2 = g.hud.DeathCountText;
                float t0 = Time.realtimeSinceStartup;
                while (k.Dead && Time.realtimeSinceStartup - t0 < 6f) yield return null;
                float downTime = Time.realtimeSinceStartup - tDie;
                yield return W(0.3f);
                Log("death: dead=" + dead + " -> revived=" + !k.Dead + " after " + F(downTime) + "s, hp " + F(k.hp) + "/" + F(k.MaxHp) + " at " + V(k.transform.position)
                    + ", level " + lvBefore + " -> " + g.progress.level + ", overlay \"" + lose.Replace("\n", " / ") + "\" \"" + keep + "\" \"" + count1 + "\" then \"" + count2 + "\"");
                Check("knight dies and revives after about 3 seconds with full health", dead && !k.Dead && k.hp >= k.MaxHp - 0.01f && downTime > 2.5f && downTime < 4.2f);
                Check("dying sends the level, EXP, upgrades and stat points back to level 1",
                    lvBefore == Progress.MaxLevel && g.progress.level == 1 && g.progress.exp == 0f && g.progress.statPoints == 0 && g.progress.pendingChoices == 0
                    && g.progress.Points(Attr.Hp) + g.progress.Points(Attr.Block) + g.progress.Points(Attr.Stamina) == 0
                    && Mathf.Abs(g.progress.DamageMult(-1) - 1f) < 0.001f && Mathf.Abs(k.MaxHp - 100f) < 0.001f && Mathf.Abs(k.MaxStamina - 100f) < 0.001f
                    && g.progress.kills == kills0);
                Check("YOU DIED screen shows while down, with the level from before the reset and the counter going down",
                    overlay && stillAtLevel && lose.Contains("Lv " + lvBefore + "  >  Lv 1") && keep.Contains("Swords (" + owned0 + " / " + Weapons.All.Length + ")")
                    && keep.Contains("Souls " + souls0.ToString("N0")) && in1 > 1.5f && in1 <= 3f && count1.Contains("Returning to town in") && count1 != count2);
                Check("the YOU DIED screen is gone after the revive", !g.hud.DeathVisible);
                Log("after death: swords owned " + owned0 + " -> " + k.inventory.OwnedCount + ", equipped " + k.inventory.NameOf(k.inventory.equipped) + " (model " + k.SwordModel.name
                    + "), souls " + souls0 + " -> " + k.souls + ", knight_ready +" + (Snds(Snd.Ready) - ready0));
                Check("death keeps the swords, the equipped sword and the Soul",
                    k.inventory.OwnedCount == owned0 && owned0 == 6 && k.inventory.equipped == equipped0 && k.SwordModel.name == k.Weapon.name && k.souls == souls0 && souls0 > 0);
                Check("reviving plays the 'ready' voice line", Snds(Snd.Ready) - ready0 == 1);
                Shot("revived");
            }

            // ------------------------------------------------ restart key
            {
                g.SpawnPickup(Weapons.Fang, new Vector3(6f, 0f, 6f));      // must be swept away by the restart
                int readyF5 = Snds(Snd.Ready);
                int soulsPre = k.souls;
                yield return W(3.2f);                                      // (the ready line has a 3 s minimum gap)
                yield return Tap(Key.F5);
                yield return W(1.2f);
                Progress f5 = g.progress;
                Log("F5: swords owned " + k.inventory.OwnedCount + ", equipped " + k.Weapon.name + " (model " + k.SwordModel.name + ", +" + k.inventory.plus[Weapons.Boss] + " on the boss sword), stat points "
                    + f5.Points(Attr.Hp) + "/" + f5.Points(Attr.Block) + "/" + f5.Points(Attr.Stamina) + " unspent " + f5.statPoints + ", max health/armor/stamina " + F(k.MaxHp) + "/" + F(k.MaxArmor) + "/" + F(k.MaxStamina)
                    + ", pickups " + Pickup.All.Count + ", knight_ready +" + (Snds(Snd.Ready) - readyF5));
                Check("F5 resets swords (Iron Sword only), stat points and their bonuses, and clears pickups",
                    k.inventory.OwnedCount == 1 && k.inventory.equipped == Weapons.Iron && k.SwordModel.name == "Iron Sword" && k.inventory.plus[Weapons.Boss] == 0
                    && f5.Points(Attr.Hp) + f5.Points(Attr.Block) + f5.Points(Attr.Stamina) + f5.statPoints == 0
                    && Mathf.Abs(k.MaxHp - 100f) < 0.001f && Mathf.Abs(k.MaxArmor - 60f) < 0.001f && Mathf.Abs(k.MaxStamina - 100f) < 0.001f
                    && Mathf.Abs(f5.BlockReduction - 0.8f) < 0.0001f && Pickup.All.Count == 0 && Mathf.Abs(k.DamageMult(-1) - 1f) < 0.001f);
                Check("restart plays the 'ready' voice line", Snds(Snd.Ready) - readyF5 == 1);
                Check("F5 clears Soul too (it was " + soulsPre + ")", soulsPre > 0 && k.souls == 0 && g.hud.SoulShown == "0");
                Log("F5: level " + g.progress.level + ", dmg x" + F(g.progress.DamageMult(-1)) + ", enemies " + Enemy.All.Count + ", minotaur " + (g.minotaur != null) + ", knight at " + V(k.transform.position));
                Check("F5 restarts at level 1 with no upgrades", g.progress.level == 1 && Mathf.Abs(g.progress.DamageMult(-1) - 1f) < 0.001f && g.minotaur != null);
                k.aimOverride = null;
                SetMouse(new Vector2(Screen.width * 0.62f, Screen.height * 0.6f), false);
                yield return W(0.8f);
                Shot("after_restart");
            }

            yield return LoopTests();

            yield return W(0.4f);
            Log("done, screenshots=" + shot + ", failed checks=" + fails);
            Application.Quit();
        }

        // ================================================================ Phase 1 loop (base / world / save)
        // Runs after every v1 case above, so those are not touched by the loop. See Docs/phase1-minimal-loop/tech-test.md.
        IEnumerator LoopTests()
        {
            Log("---- loop: data rules");
            LoopData();
            yield return null;
            Log("---- old world: data rules");
            OldWorldData();
            yield return null;
            Log("---- loop: save file");
            LoopSaveTests();
            yield return null;
            Log("---- loop: base");
            yield return LoopBaseTests();
            Log("---- loop: PREPARE");
            yield return LoopPrepareTests();
            Log("---- loop: enter the world");
            yield return LoopWorldTests();
            Log("---- loop: pickups / eat / drop / carry window");
            yield return LoopPickupTests();
            Log("---- loop: death, return, abandoned");
            yield return LoopDeathTests();
            Log("---- loop: boss, portal, victory");
            yield return LoopVictoryTests();
        }



        // The loop session used by the cases below: its own save file inside the test folder, a new game every time.
        IEnumerator StartLoopSession()
        {
            string file = Path.Combine(dir, "save_loop.json");
            foreach (string suffix in new[] { "", ".bak", ".tmp", ".corrupt" }) if (File.Exists(file + suffix)) File.Delete(file + suffix);
            LoopSave.PathOverride = file;
            LoopSave.ResetMemory();
            if (g.InSession) g.LeaveToMenu();
            yield return W(0.3f);
            Net.StartSingle("Test");
            g.StartSession(true);
            k = g.knight;
            k.aimOverride = null;
            yield return W(0.4f);
        }


        IEnumerator RightClick(Vector2 pos)
        {
            SetMouse(pos, false, false);
            yield return null; yield return null; yield return null;
            SetMouse(pos, false, true);
            yield return null; yield return null; yield return null;
            SetMouse(pos, false, false);
            yield return null;
        }

        static Vector2 Mid(RectTransform rt) { return rt.position; }

        // PREPARE: moving items, HELD, the full-carry refusal, ENTER WORLD rules (nothing is entered in these cases).
        IEnumerator LoopPrepareTests()
        {
            yield return StartLoopSession();
            RunLoop run = g.run;
            Loadout d = run.data;
            Check("loop prepare: a new save opens PREPARE with the Iron Sword already HELD in the carry and the intro line shown",
                run.PrepareOpen && d.Held.IsWeapon && d.Held.id == Weapons.Iron && d.carry.weapons[0].IsWeapon && d.stash.Total == 0
                && run.ui.IntroText.Contains("lost if you fall"));
            Check("loop prepare: with no food packed the warning says so (and the button is enabled)",
                run.ui.WarnText.Contains("No food packed") && d.CanEnter);

            // give the stash something to move
            d.stash.Add(Item.Weapon(Weapons.Rapier, 2));
            d.stash.Add(Item.Weapon(Weapons.Fang));
            d.stash.Add(Item.Food(Foods.Apple));
            d.stash.Add(Item.Food(Foods.Meat));
            yield return W(0.2f);
            Shot("loop_prepare");
            yield return W(0.3f);
            yield return Click(Mid(run.ui.CellRect(0, Section.Weapons, 0)));
            yield return W(0.1f);
            Check("loop prepare: clicking a stash weapon moves it to the carry Weapons group (Rapier +2 in slot 2), the stash slot empties",
                d.carry.weapons[1].IsWeapon && d.carry.weapons[1].id == Weapons.Rapier && d.carry.weapons[1].plus == 2 && d.stash.weapons[0].IsNone);
            yield return RightClick(Mid(run.ui.CellRect(1, Section.Weapons, 1)));
            yield return W(0.1f);
            Check("loop prepare: right click on a carried Weapon makes it the HELD one", d.heldSlot == 1 && d.Held.id == Weapons.Rapier);
            yield return Click(Mid(run.ui.CellRect(1, Section.Weapons, 1)));
            yield return W(0.1f);
            Check("loop prepare: clicking a carried item sends it back to the stash, HELD goes back to the Iron Sword",
                d.carry.weapons[1].IsNone && d.stash.HasWeaponId(Weapons.Rapier) && d.heldSlot == 0 && d.Held.id == Weapons.Iron);

            // food goes to its own group, the third weapon to Supplies when the Weapons group is full
            yield return Click(Mid(run.ui.CellRect(0, Section.Foods, 0)));
            yield return W(0.1f);
            Check("loop prepare: a stash food goes to the carry Food group and the warning about food is gone",
                d.carry.foods[0].kind == ItemKind.Food && !run.ui.WarnText.Contains("No food"));

            // fill the carry, then try to add one more
            for (int i = 0; i < 6; i++) d.carry.Add(Item.Weapon(i % 2 == 0 ? Weapons.Greatsword : Weapons.Dao));   // two weapons + Supplies (merge into one)
            d.carry.Add(Item.Food(Foods.Meat)); d.carry.Add(Item.Food(Foods.Meat)); d.carry.Add(Item.Food(Foods.Meat));
            d.carry.Add(Item.Food(Foods.Apple)); d.carry.Add(Item.Food(Foods.Apple)); d.carry.Add(Item.Food(Foods.Apple));
            int carryBefore = d.carry.Total, stashBefore = d.stash.Total;
            int at = -1;
            for (int i = 0; i < d.stash.weapons.Length; i++) if (d.stash.weapons[i].IsWeapon) at = i;
            yield return W(0.2f);
            Check("loop prepare: (setup) the carry is full", !d.carry.CanAdd(Item.Weapon(Weapons.Boss)) && !d.carry.CanAdd(Item.Food(Foods.Apple)));
            yield return Click(Mid(run.ui.CellRect(0, Section.Weapons, at)));
            yield return W(0.1f);
            Check("loop prepare: moving into a full carry is refused: nothing moves, the cells shake, the line says Carry is full",
                d.carry.Total == carryBefore && d.stash.Total == stashBefore && run.ui.WarnText == "Carry is full");

            // keyboard: the cursor starts on the first stash cell; Q / E switch side; Enter moves the item under it
            d.carry.Clear();
            d.carry.weapons[0] = Item.Weapon(Weapons.Iron);
            d.heldSlot = 0;
            yield return W(0.2f);
            yield return Tap(Key.Q);
            yield return Tap(Key.E);
            int stashN = d.stash.Total;
            yield return Tap(Key.Enter);
            yield return W(0.1f);
            Check("loop prepare: Enter moves the item under the keyboard cursor (stash -> carry)", d.stash.Total == stashN - 1 && d.carry.Total == 2);

            // ENTER WORLD rules
            d.carry.Clear();
            yield return W(0.2f);
            int enters = run.EnterRequests;
            yield return Click(Mid(run.ui.EnterRect));
            yield return W(0.1f);
            Check("loop prepare: ENTER WORLD with no Weapon packed does nothing and says Pack a Weapon first",
                run.EnterRequests == enters && run.ui.WarnText == "Pack a Weapon first" && run.PrepareOpen);
            d.carry.weapons[0] = Item.Weapon(Weapons.Iron);
            d.heldSlot = 0;
            yield return W(0.2f);
            yield return Click(Mid(run.ui.EnterRect));
            yield return W(0.1f);
            Check("loop prepare: with no food packed ENTER WORLD asks first (Enter without food?) and does not enter",
                run.ui.PromptShown && run.EnterRequests == enters && run.PrepareOpen && run.ui.WarnText.Contains("Enter without food?"));
            yield return Tap(Key.Escape);
            yield return W(0.1f);
            Check("loop prepare: Esc answers the question with Back: PREPARE stays open", !run.ui.PromptShown && run.PrepareOpen && g.InSession);
            d.carry.foods[0] = Item.Food(Foods.Apple);
            yield return W(0.2f);
            yield return Click(Mid(run.ui.EnterRect));
            yield return W(0.1f);
            Check("loop prepare: with food packed ENTER WORLD goes in without a question (PREPARE closes, the fade starts)",
                run.EnterRequests == enters + 1 && !run.ui.PromptShown && run.Busy && !run.PrepareOpen);
        }


        // ENTER WORLD: the save says "in a run", the world is built at level 1 holding the HELD weapon, keys 1 / 2 swap, Esc warns.
        IEnumerator LoopWorldTests()
        {
            yield return StartLoopSession();
            RunLoop run = g.run;
            Loadout d = run.data;
            d.carry.weapons[1] = Item.Weapon(Weapons.Rapier, 1);
            d.heldSlot = 1;
            yield return W(0.2f);
            yield return Click(Mid(run.ui.EnterRect));
            yield return W(0.1f);
            bool asked = run.ui.PromptShown;
            yield return Tap(Key.Enter);                                   // no food packed: the question, then Enter = go
            yield return W(0.3f);
            Check("loop world: no food packed -> the question first, Enter on it starts the fade", asked && run.Busy);
            Check("loop world: while fading nothing is built yet and PREPARE cannot be reopened", run.Busy && !g.InventoryOpen && run.State == LoopState.Base);
            yield return W(1.1f);
            Check("loop world: ENTER WORLD builds the world: State World, level 1, Minotaur and dummy exist, time runs, fade gone",
                run.State == LoopState.World && g.progress.level == 1 && g.minotaur != null && g.dummy != null && !g.InventoryOpen
                && Time.timeScale > 0.5f && run.ui.FadeAlpha < 0.05f && !run.Busy);
            Check("loop world: the knight holds the HELD weapon with its +N (Swift Rapier +1, model swapped) and stands at the town spawn",
                k.Weapon.id == Weapons.Rapier && k.inventory.plus[Weapons.Rapier] == 1 && k.SwordModel.name == "Swift Rapier"
                && (k.transform.position - MapLayout.TownSpawn).magnitude < 3f && !k.Dead);
            string saved = File.Exists(LoopSave.Path) ? File.ReadAllText(LoopSave.Path) : "";
            Check("loop world: the save file says inRun = true while the run is open", saved.Contains("\"inRun\":true") && d.inRun);
            Check("loop world: objective line tells the player to defeat the Minotaur", run.ui.ObjectiveText.Contains("Defeat the Minotaur"));
            yield return Tap(Key.Digit1);
            yield return W(0.1f);
            bool one = k.Weapon.id == Weapons.Iron && d.heldSlot == 0 && k.SwordModel.name == "Iron Sword";
            yield return Tap(Key.Digit2);
            yield return W(0.1f);
            Check("loop world: key 1 holds weapon slot 1 (Iron Sword), key 2 holds slot 2 (Swift Rapier +1) and swaps the model",
                one && k.Weapon.id == Weapons.Rapier && d.heldSlot == 1 && k.inventory.plus[Weapons.Rapier] == 1);
            Check("loop world: the weapon strip shows both weapons and \"no food\"", run.ui.StripText.Contains("[1] Iron Sword") && run.ui.StripText.Contains("[2] Swift Rapier +1") && run.ui.StripText.Contains("[C] no food"));
            yield return Tap(Key.Escape);
            yield return W(0.2f);
            Check("loop world: Esc in the world warns that leaving counts as a death (and does not leave yet)",
                g.hud.NoticeShown.Contains("counts as a death") && g.InSession && run.InWorld);
            Check("loop world: F5 does nothing in the loop (the stash would be wiped)", !g.RequestReset() && run.InWorld && g.progress.level == 1);
        }


        // A loop session already inside the world: Iron Sword (+ whatever "setup" puts in the carry), town spawn, no packs.
        IEnumerator StartLoopWorld(System.Action<Loadout> setup)
        {
            yield return StartLoopSession();
            RunLoop run = g.run;
            if (setup != null) setup(run.data);
            run.data.EnsureHeld();
            yield return W(0.2f);
            yield return Click(run.ui.EnterRect.position);
            yield return W(0.1f);
            if (run.ui.PromptShown) yield return Tap(Key.Enter);          // no food packed: answer the question
            float t0 = Time.realtimeSinceStartup;
            while (!run.InWorld && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            yield return W(0.8f);
            k = g.knight;
            g.packsOff = true;
        }

        // Pickups go into the carry (own group first, then Supplies), a full pack leaves them lying, C eats, Tab shows the
        // carry window (click eats / holds, right click drops), a dropped item waits until the knight steps away.
        IEnumerator LoopPickupTests()
        {
            yield return StartLoopWorld(d => d.carry.foods[0] = Item.Food(Foods.Apple));
            RunLoop run = g.run;
            Loadout d = run.data;
            Check("loop pickup: (setup) in the world with a full-health knight, Iron Sword + one Apple packed", run.InWorld && k.hp >= k.MaxHp - 0.01f && d.carry.Total == 2);
            Vector3 here = k.transform.position;

            g.SpawnPickup(Weapons.Fang, here + new Vector3(0.4f, 0f, 0f));
            yield return W(0.8f);
            Check("loop pickup: walking over a dropped weapon puts it in the carry Weapons group; the pickup is gone; the notice says so",
                d.carry.weapons[1].IsWeapon && d.carry.weapons[1].id == Weapons.Fang && Pickup.All.Count == 0 && g.lastNotice.StartsWith("Picked up Crimson Fang"));

            g.SpawnPickup(Weapons.Fang, here + new Vector3(0.4f, 0f, 0f));
            yield return W(0.8f);
            Check("loop pickup: a duplicate refines the carried weapon (+1) instead of taking a slot", d.carry.weapons[1].plus == 1 && d.carry.Count(Section.Weapons) == 2 && g.lastNotice.Contains("refined to +1"));

            g.SpawnFood(Foods.Meat, here + new Vector3(-0.4f, 0f, 0f));
            yield return W(0.8f);
            Check("loop pickup: food is stored (not eaten) even at full health, into the Food group",
                d.carry.foods[1].kind == ItemKind.Food && d.carry.foods[1].id == Foods.Meat && k.hp >= k.MaxHp - 0.01f && Pickup.All.Count == 0);

            // C eats the first food; at full health it says HP full and keeps it
            int foods0 = d.carry.Count(Section.Foods);
            int hints0 = g.fullHints;
            yield return Tap(Key.C);
            yield return W(0.2f);
            Check("loop pickup: C at full health does not eat (HP full) and the food stays", d.carry.Count(Section.Foods) == foods0 && g.fullHints == hints0 + 1);
            k.hp = k.MaxHp * 0.4f;
            float hpBefore = k.hp;
            yield return Tap(Key.C);
            yield return W(0.2f);
            Check("loop pickup: C with missing health eats the first food (the Apple: +15% max health) and uses it up",
                d.carry.Count(Section.Foods) == foods0 - 1 && d.carry.foods[0].IsNone && Mathf.Abs(k.hp - (hpBefore + 0.15f * k.MaxHp)) < 0.5f && g.lastFoodHeal > 0);

            // the pack is full: Weapons 2/2, Food 3/3, Supplies 4/4
            d.carry.foods[0] = Item.Food(Foods.Apple); d.carry.foods[2] = Item.Food(Foods.Apple);
            for (int i = 0; i < 4; i++) d.carry.supplies[i] = Item.Food(Foods.Meat);
            Check("loop pickup: (setup) the pack is full", d.carry.Count(Section.Weapons) == 2 && d.carry.Count(Section.Foods) == 3 && d.carry.Count(Section.Supplies) == 4);
            int fullHints = run.PackFullHints;
            Pickup boss = g.SpawnPickup(Weapons.Boss, here + new Vector3(0.4f, 0f, 0f));
            Pickup apple = g.SpawnFood(Foods.Apple, here + new Vector3(-0.4f, 0f, 0.4f));
            yield return W(1.0f);
            Check("loop pickup: with a full pack the item stays on the ground and Pack full is shown",
                boss != null && apple != null && Pickup.All.Contains(boss) && Pickup.All.Contains(apple) && run.PackFullHints > fullHints
                && g.lastNotice.StartsWith("Pack full! Can't pick up"));
            g.SpawnPickup(Weapons.Fang, here + new Vector3(0.4f, 0f, 0f));
            yield return W(0.8f);
            Check("loop pickup: a duplicate still merges when the pack is full (Crimson Fang +2)", d.carry.weapons[1].plus == 2);

            // Tab window in the world
            foreach (Pickup pk in new List<Pickup>(Pickup.All)) { pk.gameObject.SetActive(false); Destroy(pk.gameObject); }
            yield return W(0.2f);
            yield return Tap(Key.Tab);
            yield return W(0.2f);
            Check("loop pickup: Tab in the world opens the carry window and pauses (single player)", g.InventoryOpen && run.ui.CarryVisible && Time.timeScale == 0f && !run.ui.PrepareVisible);
            Shot("loop_carry_window");
            yield return W(0.3f);
            k.hp = k.MaxHp * 0.3f;
            int f1 = d.carry.Count(Section.Foods);
            yield return Click(run.ui.CarryCellRect(Section.Foods, 0).position);
            yield return W(0.2f);
            Check("loop pickup: clicking a food in the carry window eats it", d.carry.Count(Section.Foods) == f1 - 1 && k.hp > k.MaxHp * 0.3f + 1f);
            yield return Click(run.ui.CarryCellRect(Section.Weapons, 0).position);
            yield return W(0.2f);
            Check("loop pickup: clicking a Weapon slot holds it (Iron Sword) and swaps the model", d.heldSlot == 0 && k.Weapon.id == Weapons.Iron);
            yield return Tap(Key.Tab);
            yield return W(0.2f);
            Check("loop pickup: Tab closes the carry window", !g.InventoryOpen && !run.ui.CarryVisible && Time.timeScale > 0.5f);

            // Drop: right click in the window; the last Weapon stays
            yield return Tap(Key.Tab);
            yield return W(0.2f);
            yield return RightClick(run.ui.CarryCellRect(Section.Weapons, 1).position);
            yield return W(0.2f);
            Pickup dropped = Pickup.All.Count == 1 ? Pickup.All[0] : null;
            Check("loop pickup: right click on a carried Weapon drops it at the knight's feet as a pickup that keeps its +2",
                dropped != null && dropped.kind == PickupKind.Sword && dropped.weaponId == Weapons.Fang && dropped.plus == 2 && dropped.dropped && d.carry.weapons[1].IsNone);
            yield return RightClick(run.ui.CarryCellRect(Section.Weapons, 0).position);
            yield return W(0.2f);
            Check("loop pickup: the last Weapon in the Weapons slots cannot be dropped", d.carry.weapons[0].IsWeapon && Pickup.All.Count == 1 && g.hud.NoticeShown.Contains("last Weapon"));
            yield return Tap(Key.Tab);
            yield return W(1.2f);
            Check("loop pickup: the dropped weapon is not taken back while the knight stands on it", Pickup.All.Contains(dropped) && d.carry.weapons[1].IsNone);
            k.transform.position = dropped.transform.position + new Vector3(5f, 0f, 0f);
            yield return W(0.5f);
            k.transform.position = dropped.transform.position + new Vector3(0.3f, 0f, 0f);
            yield return W(0.8f);
            Check("loop pickup: after stepping away and back it is picked up again with its +2",
                !Pickup.All.Contains(dropped) && d.carry.weapons[1].IsWeapon && d.carry.weapons[1].id == Weapons.Fang && d.carry.weapons[1].plus == 2);
            g.packsOff = false;
        }


        // Dying ends the run: no revive, no YOU DIED screen, YOU FELL with LOST / SAFE IN STASH, carry gone, RETURN TO BASE;
        // only weapon dead -> Iron Sword restored; closing the game mid-run -> ABANDONED next time.
        IEnumerator LoopDeathTests()
        {
            // --- a run with spare things at home
            yield return StartLoopWorld(d =>
            {
                d.carry.foods[0] = Item.Food(Foods.Apple);
                d.stash.Add(Item.Weapon(Weapons.Rapier, 2));          // not taken along: safe at home
                d.stash.Add(Item.Food(Foods.Meat));
            });
            RunLoop run = g.run;
            Loadout d0 = run.data;
            k.TakeDamage(9999f, k.transform.position + Vector3.right, "selftest");
            yield return W(0.3f);
            bool dim = run.Dying && !run.SummaryOpen;
            yield return W(0.8f);
            bool noOldScreen = !g.hud.DeathVisible;
            yield return W(1.2f);
            yield return W(3f);
            Check("loop death: the knight stays down (no revive after 5 s) and the old YOU DIED screen never shows",
                k.Dead && noOldScreen && !g.hud.DeathVisible && run.State == LoopState.Summary);
            Check("loop death: the screen dims first (dying, no summary yet) and then the summary YOU FELL shows",
                dim && run.ui.SummaryVisible && run.ui.SummaryTitle == "YOU FELL" && Time.timeScale == 0f);
            Shot("loop_you_fell");
            yield return W(0.3f);
            string body = run.ui.SummaryBody;
            Check("loop death: LOST lists what was carried (Iron Sword, Apple) and SAFE IN STASH lists what stayed home (Rapier +2, Roast Meat)",
                body.Contains("LOST") && body.Contains("Iron Sword") && body.Contains("Apple") && body.Contains("SAFE IN STASH")
                && body.Contains("Swift Rapier +2") && body.Contains("Roast Meat") && !body.Contains("Iron Sword restored"));
            string saved = File.ReadAllText(LoopSave.Path);
            Check("loop death: the carry is empty, the stash that stayed home is intact and the save says the run is over",
                d0.carry.Total == 0 && d0.stash.HasWeaponId(Weapons.Rapier) && !d0.stash.HasWeaponId(Weapons.Iron) && !d0.inRun && saved.Contains("\"inRun\":false"));
            yield return Click(run.ui.ReturnRect.position);
            yield return W(0.4f);
            Check("loop death: RETURN TO BASE: at the base, alive at full health, no enemies, PREPARE open with the Rapier +2 held",
                run.PrepareOpen && !k.Dead && k.hp >= k.MaxHp - 0.01f && Enemy.All.Count == 0 && (k.transform.position - MapLayout.BasePoint).magnitude < 1f
                && run.data.Held.id == Weapons.Rapier && run.data.Held.plus == 2 && !run.ui.SummaryVisible && Time.timeScale == 0f);
            Check("loop death: the knight starts level 1 again and holds the Rapier model", g.progress.level == 1 && k.Weapon.id == Weapons.Rapier && k.SwordModel.name == "Swift Rapier");

            // --- dying with the only weapon: a free Iron Sword is put back
            yield return StartLoopWorld(null);
            run = g.run;
            k.TakeDamage(9999f, k.transform.position + Vector3.right, "selftest");
            yield return W(2.4f);
            Check("loop death: dying with the only Weapon restores a free Iron Sword (+0) in the stash and says so",
                run.SummaryOpen && run.result.ironRestored && run.data.stash.HasWeaponId(Weapons.Iron) && run.ui.SummaryBody.Contains("Iron Sword restored")
                && run.result.lost.Count == 1);
            yield return Tap(Key.Enter);
            yield return W(0.4f);
            Check("loop death: Enter on the summary also returns to the base, with the Iron Sword packed (the loop can go on)",
                run.PrepareOpen && run.data.Held.IsWeapon && run.data.Held.id == Weapons.Iron && run.data.Held.plus == 0);

            // --- closing the game in the middle of a run
            yield return Tap(Key.Escape);                                            // closes PREPARE (empty carry would not matter)
            yield return W(0.2f);
            run.data.carry.foods[0] = Item.Food(Foods.Meat);
            run.data.carry.weapons[1] = Item.Weapon(Weapons.Dao);
            run.data.stash.Add(Item.Weapon(Weapons.Greatsword));
            g.SetInventory(true);
            yield return W(0.2f);
            yield return Click(run.ui.EnterRect.position);
            yield return W(0.1f);
            if (run.ui.PromptShown) yield return Tap(Key.Enter);
            float t0 = Time.realtimeSinceStartup;
            while (!run.InWorld && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            yield return W(0.5f);
            Check("loop death: (setup) in a new run, the save says inRun", run.InWorld && File.ReadAllText(LoopSave.Path).Contains("\"inRun\":true"));
            g.LeaveToMenu();                                                          // what Esc Esc / closing the window leaves behind
            yield return W(0.3f);
            Check("loop death: leaving mid-run does not touch the save (the next start finds inRun)", File.ReadAllText(LoopSave.Path).Contains("\"inRun\":true"));
            Net.StartSingle("Test");
            g.StartSession(true);
            k = g.knight;
            yield return W(0.5f);
            run = g.run;
            Check("loop death: starting again after a mid-run exit shows ABANDONED with what was carried listed as LOST",
                run.SummaryOpen && run.result.abandoned && run.ui.SummaryTitle == "ABANDONED" && run.ui.SummaryBody.Contains("Dao") && run.ui.SummaryBody.Contains("Roast Meat")
                && run.ui.SummarySubtitle.Contains("left the world mid-run"));
            Check("loop death: after ABANDONED the carry is gone, the stash kept the Greatsword and the save is closed",
                run.data.carry.Total == 0 && run.data.stash.HasWeaponId(Weapons.Greatsword) && !run.data.inRun && File.ReadAllText(LoopSave.Path).Contains("\"inRun\":false"));
            yield return Click(run.ui.ReturnRect.position);
            yield return W(0.4f);
            Check("loop death: RETURN TO BASE after ABANDONED opens PREPARE", run.PrepareOpen);
        }


        // Kill the world boss with a direct hit (the Minotaur has no invulnerable phase).
        void KillMinotaur()
        {
            Minotaur m = g.minotaur;
            if (m != null && !m.Dead) m.TakeHit(999999f, 1f, m.transform.position + Vector3.back * 2f, false, 0f, 0f, "selftest", k);
        }

        // The Minotaur dies: portal (no respawn), loot is picked up into the carry, 1.5 s inside the portal = VICTORY and
        // everything goes into the stash with its +N; a full stash says LEFT BEHIND; dying after the boss is still YOU FELL.
        IEnumerator LoopVictoryTests()
        {
            yield return StartLoopWorld(d =>
            {
                d.carry.weapons[1] = Item.Weapon(Weapons.Rapier, 1);
                d.carry.foods[0] = Item.Food(Foods.Apple);
                d.stash.Add(Item.Weapon(Weapons.Fang));                        // stays at home
            });
            RunLoop run = g.run;
            Loadout d0 = run.data;
            Check("loop victory: (setup) no portal while the boss lives, his bar and the lair arrow are the v1 ones",
                run.InWorld && !run.BossDown && run.portal == null && g.minotaur != null && !g.minotaur.Dead);
            g.foodOverride = -1; g.dropOverride = -1;                       // the selftest turned random food off: the boss drops 2 Roast Meat as in the game
            Vector3 lairPos = g.minotaur.transform.position;
            KillMinotaur();
            yield return W(2.2f);
            yield return ClearLevelUps();                                   // the boss gives a lot of EXP: the level-up cards pause the game
            yield return W(0.5f);
            Check("loop victory: when the Minotaur dies the portal opens where he fell, the objective changes and a banner says so",
                run.BossDown && run.portal != null && g.minotaur == null && run.ui.ObjectiveText.Contains("Step into the Portal")
                && g.hud.BannerShown.Contains("MINOTAUR HAS FALLEN") && g.hud.BannerShown.Contains("Portal open")
                && (run.portal.transform.position - lairPos).magnitude < 6f && run.ui.PortalLabelShown);
            Check("loop victory: the boss's loot is on the ground (Horned King's Blade and 2 Roast Meat) - the run did not end by itself",
                run.InWorld && Pickup.All.Count >= 3 && g.hud.LairArrowText.StartsWith("Portal"));
            // the old respawn would bring him back after MinotaurRespawn (20 s) + the check interval
            yield return W(0.3f);
            yield return ClearLevelUps();
            float tRespawn = Time.time;
            yield return W(Game.MinotaurRespawn + 3f);
            yield return ClearLevelUps();
            Check("loop victory: the Minotaur does not respawn in the loop (still none after 23 s) and the run goes on",
                g.minotaur == null && run.InWorld && run.BossDown);

            // collect the loot (Weapons are full -> the blade lands in Supplies, the meat in Food)
            Vector3 portalPos = run.portal.transform.position;
            foreach (Pickup pk in new List<Pickup>(Pickup.All))
            {
                if (pk == null) continue;
                k.transform.position = pk.transform.position;
                yield return W(0.45f);
            }
            Check("loop victory: the loot went into the carry (Horned King's Blade in Supplies, Roast Meat in Food)",
                d0.carry.HasWeaponId(Weapons.Boss) && d0.carry.Count(Section.Foods) >= 2 && Pickup.All.Count == 0);

            // step in, step out (cancels), step in again
            k.transform.position = portalPos + new Vector3(Portal.Radius + 3f, 0f, 0f);
            yield return W(0.3f);
            Check("loop victory: standing outside the portal does nothing", run.portalHold == 0f && run.InWorld);
            k.transform.position = portalPos + new Vector3(0.6f, 0f, 0f);
            yield return W(0.8f);
            float partial = run.portalHold;
            int lit = run.portal.SegmentsLit;
            Check("loop victory: standing inside fills the loading ring (about 0.8 s of 1.5 s, still in the world)", partial > 0.5f && partial < 1.4f && lit > 5 && run.InWorld);
            k.transform.position = portalPos + new Vector3(Portal.Radius + 3f, 0f, 0f);
            yield return W(0.4f);
            Check("loop victory: stepping out before 1.5 s cancels it (the ring empties, still in the world)", run.portalHold == 0f && run.InWorld && run.portal.SegmentsLit == 0);
            // a level-up card panel that opens while he stands in the portal must come first: no VICTORY screen on top of it
            k.transform.position = portalPos + new Vector3(0.6f, 0f, 0f);
            g.GrantExp(Progress.Need(g.progress.level) - g.progress.exp, k.transform.position + Vector3.up * 2f);
            float tc = Time.realtimeSinceStartup;
            while (!g.LevelUpOpen && Time.realtimeSinceStartup - tc < 3f) yield return null;
            yield return W(2.0f);
            Check("loop victory: standing in the portal with the level-up cards open does not end the run (cards first, ring empty)",
                g.LevelUpOpen && run.InWorld && !run.SummaryOpen && run.portalHold == 0f);
            tc = Time.realtimeSinceStartup;
            while ((g.LevelUpOpen || g.progress.pendingChoices > 0) && Time.realtimeSinceStartup - tc < 10f)
            {
                if (g.LevelStep == 1) g.ChooseUpgrade(0); else if (g.LevelStep == 2) g.ChooseStat(0);
                yield return W(0.45f);
            }
            k.transform.position = portalPos + new Vector3(0.6f, 0f, 0f);
            yield return W(2.0f);
            Check("loop victory: staying 1.5 s inside the portal ends the run: VICTORY",
                run.SummaryOpen && run.result.victory && run.ui.SummaryTitle == "VICTORY" && Time.timeScale == 0f);
            Shot("loop_victory");
            yield return W(0.3f);
            string body = run.ui.SummaryBody;
            Check("loop victory: BROUGHT HOME lists the Iron Sword, Swift Rapier +1 (with +N), the Horned King's Blade as NEW, Apple and Roast Meat",
                body.Contains("BROUGHT HOME") && body.Contains("Iron Sword") && body.Contains("Swift Rapier +1") && body.Contains("Horned King's Blade") && body.Contains("NEW")
                && body.Contains("Apple") && body.Contains("Roast Meat") && !body.Contains("LEFT BEHIND"));
            Check("loop victory: the stash holds all of it (Iron, Rapier +1, Fang that stayed home, Blade), the carry is empty and the save says the run is over",
                d0.stash.HasWeaponId(Weapons.Iron) && d0.stash.HasWeaponId(Weapons.Rapier) && d0.stash.HasWeaponId(Weapons.Fang) && d0.stash.HasWeaponId(Weapons.Boss)
                && d0.carry.Total == 0 && !d0.inRun && File.ReadAllText(LoopSave.Path).Contains("\"inRun\":false")
                && RapierPlus(d0) == 1);
            yield return Click(run.ui.ReturnRect.position);
            yield return W(0.5f);
            Check("loop victory: RETURN TO BASE: PREPARE is open at the base, nothing of the world is left, the portal is gone",
                run.PrepareOpen && run.portal == null && !run.BossDown && Enemy.All.Count == 0 && Pickup.All.Count == 0 && !run.ui.PortalLabelShown);

            // --- a full stash: what does not fit is left behind
            yield return StartLoopWorld(d =>
            {
                for (int i = 0; i < Loadout.StashCaps[1]; i++) d.stash.foods[i] = Item.Food(Foods.Apple);
                for (int i = 0; i < Loadout.StashCaps[2]; i++) d.stash.supplies[i] = Item.Food(Foods.Meat);
                d.carry.foods[0] = Item.Food(Foods.Apple);
                d.carry.foods[1] = Item.Food(Foods.Apple);
            });
            run = g.run;
            KillMinotaur();
            yield return W(0.4f);
            yield return W(1.0f);
            yield return ClearLevelUps();
            k.transform.position = run.portal.transform.position + new Vector3(0.5f, 0f, 0f);
            yield return W(2.0f);
            Check("loop victory: with a full stash the food that does not fit is shown as LEFT BEHIND (stash full) in yellow and is lost",
                run.SummaryOpen && run.result.victory && run.result.leftBehind.Count == 2 && run.ui.SummaryBody.Contains("LEFT BEHIND (stash full)") && run.data.carry.Total == 0);

            // --- falling after the boss but before the portal is still a death
            yield return StartLoopWorld(d => d.carry.foods[0] = Item.Food(Foods.Apple));
            run = g.run;
            KillMinotaur();
            yield return W(0.4f);
            yield return W(1.0f);
            yield return ClearLevelUps();
            k.TakeDamage(9999f, k.transform.position + Vector3.right, "selftest");
            yield return W(2.4f);
            Check("loop victory: dying after the boss fell but before the portal is YOU FELL (the run is only won in the portal)",
                run.SummaryOpen && !run.result.victory && run.ui.SummaryTitle == "YOU FELL" && run.result.lost.Count == 2);
        }

        static int RapierPlus(Loadout l)
        {
            foreach (Item it in l.stash.weapons) if (it.IsWeapon && it.id == Weapons.Rapier) return it.plus;
            foreach (Item it in l.stash.supplies) if (it.IsWeapon && it.id == Weapons.Rapier) return it.plus;
            return -1;
        }

        // Single Player from the menu: the base (no enemies, PREPARE open and pausing), Tab / Esc, no boss respawn.
        IEnumerator LoopBaseTests()
        {
            yield return StartLoopSession();
            RunLoop run = g.run;
            Vector3 bp = MapLayout.BasePoint;
            Check("loop base: the session is a loop (Game.Loop) and the knight stands at the base point",
                g.Loop && run != null && run.State == LoopState.Prepare && (k.transform.position - bp).magnitude < 1f);
            Check("loop base: there is no actor at the base (no enemies, no Minotaur, no dummy, no pickups)",
                Enemy.All.Count == 0 && g.minotaur == null && g.dummy == null && Pickup.All.Count == 0);
            Check("loop base: PREPARE is open on arrival and pauses the game", g.InventoryOpen && g.PanelOpen && Time.timeScale == 0f && run.ui.PrepareVisible);
            yield return W(0.2f);
            Check("loop base: no Soul row on the HUD and the controls panel describes the loop (1 / 2 hold, C eat, Carry window)",
                !g.hud.SoulRowVisible && g.hud.ControlsShown.Contains("Hold Weapon slot") && g.hud.ControlsShown.Contains("Carry window"));
            yield return Tap(Key.Escape);
            yield return W(0.2f);
            Check("loop base: Esc closes PREPARE (back to the base, time runs again)",
                !g.InventoryOpen && run.State == LoopState.Base && Time.timeScale > 0.5f && !run.ui.PrepareVisible && g.InSession);
            yield return Tap(Key.Tab);
            yield return W(0.2f);
            Check("loop base: Tab opens PREPARE again", g.InventoryOpen && run.State == LoopState.Prepare && Time.timeScale == 0f);
            yield return Tap(Key.Tab);
            yield return W(0.2f);
            Vector3 p0 = k.transform.position;
            k.moveOverride = new Vector2(1f, 0f);
            yield return W(0.8f);
            k.moveOverride = null;
            Check("loop base: the knight walks around the base after PREPARE is closed", (k.transform.position - p0).magnitude > 2f);
            yield return W(4f);
            Check("loop base: still no enemies 5 s later: no slime packs and no Minotaur at the base", Enemy.All.Count == 0 && g.minotaur == null && g.wave == 0);

            // the v1 arena is untouched: -single / -selftest style sessions are not loops
            g.LeaveToMenu();
            yield return W(0.3f);
            Net.StartSingle("Test");
            g.StartSession();
            k = g.knight;
            yield return W(0.6f);
            Check("loop base: StartSession() without the loop flag is the v1 arena (Game.Loop false, Minotaur and dummy exist)",
                !g.Loop && g.run == null && g.minotaur != null && g.dummy != null && !g.InventoryOpen);
            yield return W(0.2f);
            Check("loop base: the v1 arena still shows Soul and the v1 controls (nothing of the loop leaks into it)",
                g.hud.SoulRowVisible && !g.hud.ControlsShown.Contains("Carry window") && g.hud.ControlsShown.Contains("Restart (online: host)"));
            g.LeaveToMenu();
            yield return W(0.3f);
        }

        // Save / load with a file of its own inside the test folder; the real save file must not be touched.
        void LoopSaveTests()
        {
            string real = Path.Combine(Application.persistentDataPath, LoopSave.FileName);
            bool hadReal = File.Exists(real);
            System.DateTime realTime = hadReal ? File.GetLastWriteTimeUtc(real) : System.DateTime.MinValue;
            string file = Path.Combine(dir, "save_test.json");
            foreach (string suffix in new[] { "", ".bak", ".tmp", ".corrupt" }) if (File.Exists(file + suffix)) File.Delete(file + suffix);
            LoopSave.PathOverride = file;
            LoopSave.ResetMemory();

            Check("loop save: no file -> a new game with one Iron Sword and no note", LoopSave.Load().stash.Total == 1 && LoopSave.LastLoadNote == "");

            Loadout l = Loadout.NewGame();
            l.stash.Add(Item.Weapon(Weapons.Boss, 3));
            l.stash.Add(Item.Weapon(Weapons.Dao, 5));
            l.stash.Add(Item.Food(Foods.Meat));
            l.stash.supplies[3] = Item.Food(Foods.Apple);
            l.carry.Add(Item.Weapon(Weapons.Rapier, 1));
            l.carry.Add(Item.Weapon(Weapons.Fang, 2));
            l.carry.Add(Item.Food(Foods.Apple));
            l.carry.supplies[2] = Item.Weapon(Weapons.Greatsword, 4);
            l.heldSlot = 1; l.inRun = true; l.introShown = true;
            bool saved = LoopSave.Save(l);
            Loadout r = LoopSave.Load();
            bool same = saved && r != null && r.heldSlot == 1 && r.inRun && r.introShown;
            foreach (Section sec in new[] { Section.Weapons, Section.Foods, Section.Supplies })
                for (int i = 0; i < l.stash.Slots(sec).Length; i++)
                    same &= l.stash.Slots(sec)[i].Same(r.stash.Slots(sec)[i]) && (i >= l.carry.Slots(sec).Length || l.carry.Slots(sec)[i].Same(r.carry.Slots(sec)[i]));
            Check("loop save: writing then loading gives the same value in every slot, held slot, inRun and intro flag", same);
            Check("loop save: the file used is the one given (not persistentDataPath) and the JSON carries inRun",
                LoopSave.Path == file && file != real && File.Exists(file) && File.ReadAllText(file).Contains("\"inRun\":true"));

            // damaged main file, good .bak: the second Save made the .bak out of the first one
            Loadout l2 = Loadout.NewGame();
            l2.stash.Add(Item.Food(Foods.Apple));
            LoopSave.Save(l2);                                                 // main = l2, .bak = l
            File.WriteAllText(file, "{ this is not json ");
            Loadout fromBak = LoopSave.Load();
            Check("loop save: a damaged main file with a good .bak loads the .bak (previous save)",
                fromBak != null && fromBak.inRun && fromBak.stash.HasWeaponId(Weapons.Boss) && LoopSave.LastLoadNote.Contains("damaged"));

            // garbage everywhere: the file is set aside as .corrupt and a new game starts
            File.WriteAllText(file, "garbage");
            File.WriteAllText(file + ".bak", "more garbage");
            Loadout fresh = LoopSave.Load();
            Check("loop save: garbage in both files starts a new game (Iron only) and keeps the bad file as .corrupt",
                fresh.stash.Total == 1 && fresh.stash.HasWeaponId(Weapons.Iron) && !fresh.inRun && File.Exists(file + ".corrupt") && !File.Exists(file));

            // ids / plus out of range are repaired, a save with no weapon gets an Iron Sword
            File.WriteAllText(file, "{\"v\":1,\"inRun\":false,\"intro\":false,\"held\":7,"
                + "\"stash\":{\"w\":[{\"k\":1,\"id\":99,\"p\":0},{\"k\":1,\"id\":2,\"p\":9},{\"k\":2,\"id\":0,\"p\":0}],\"f\":[{\"k\":2,\"id\":1,\"p\":0},{\"k\":2,\"id\":77,\"p\":0}],\"s\":[]},"
                + "\"carry\":{\"w\":[],\"f\":[{\"k\":1,\"id\":0,\"p\":0}],\"s\":[]}}");
            Loadout rep = LoopSave.Load();
            bool noBad = true;
            foreach (Item it in rep.stash.All()) noBad &= it.Valid;
            foreach (Item it in rep.carry.All()) noBad &= it.Valid;
            Check("loop save: out-of-range ids / plus, a food in the Weapons group and a weapon in Foods are dropped; Iron is restored; held slot repaired",
                noBad && rep.stash.HasWeaponId(Weapons.Iron) && rep.stash.Count(Section.Foods) == 1 && rep.carry.Total == 0 && rep.heldSlot == 0
                && !rep.stash.HasWeaponId(Weapons.Greatsword));

            // two stacks of the same weapon in a hand-edited file become one (+N rule)
            File.WriteAllText(file, "{\"v\":1,\"inRun\":false,\"intro\":false,\"held\":0,"
                + "\"stash\":{\"w\":[{\"k\":1,\"id\":0,\"p\":1},{\"k\":1,\"id\":0,\"p\":0}],\"f\":[],\"s\":[]},\"carry\":{\"w\":[],\"f\":[],\"s\":[]}}");
            Loadout dup = LoopSave.Load();
            Check("loop save: two stacks of one weapon in the file are merged with the +N rule (+1 and +0 -> +2)",
                dup.stash.Count(Section.Weapons) == 1 && dup.stash.weapons[0].plus == ItemBag.MergePlus(1, 0));

            Check("loop save: the player's real save file was not touched by the test",
                File.Exists(real) == hadReal && (!hadReal || File.GetLastWriteTimeUtc(real) == realTime));

            foreach (string suffix in new[] { "", ".bak", ".tmp", ".corrupt" }) if (File.Exists(file + suffix)) File.Delete(file + suffix);
            LoopSave.PathOverride = null;
            LoopSave.ResetMemory();
            LoopSave.ForceMemory = true;
            Loadout mem = Loadout.NewGame();
            mem.inRun = true;
            LoopSave.Save(mem);
            Check("loop save: ForceMemory keeps the data in memory only (no file written)",
                LoopSave.Disabled && LoopSave.Load().inRun && !File.Exists(file));
            LoopSave.ForceMemory = false;
            LoopSave.ResetMemory();
        }

        // Pure data: Item / ItemBag / Loadout rules (no scene needed).
        void LoopData()
        {
            Loadout l = Loadout.NewGame();
            Check("loop data: a new stash holds exactly one Iron Sword (+0) and nothing else",
                l.stash.Total == 1 && l.stash.weapons[0].IsWeapon && l.stash.weapons[0].id == Weapons.Iron && l.stash.weapons[0].plus == 0 && l.carry.Total == 0);
            Check("loop data: stash slots are 12 / 8 / 16 and carry slots 2 / 3 / 4",
                l.stash.weapons.Length == 12 && l.stash.foods.Length == 8 && l.stash.supplies.Length == 16
                && l.carry.weapons.Length == 2 && l.carry.foods.Length == 3 && l.carry.supplies.Length == 4);

            // category first, then Supplies, then Full - nothing is lost
            var bag = new ItemBag(2, 3, 4);
            bool order = true;
            order &= bag.Add(Item.Weapon(Weapons.Iron)) == BagResult.Added && bag.weapons[0].IsWeapon;
            order &= bag.Add(Item.Weapon(Weapons.Rapier)) == BagResult.Added && bag.weapons[1].IsWeapon;
            order &= bag.Add(Item.Weapon(Weapons.Fang)) == BagResult.Added && bag.supplies[0].IsWeapon && bag.supplies[0].id == Weapons.Fang;   // Weapons full -> Supplies
            order &= bag.Add(Item.Food(Foods.Apple)) == BagResult.Added && bag.foods[0].kind == ItemKind.Food;
            order &= bag.Add(Item.Food(Foods.Apple)) == BagResult.Added && bag.Add(Item.Food(Foods.Meat)) == BagResult.Added;
            order &= bag.Add(Item.Food(Foods.Apple)) == BagResult.Added && bag.supplies[1].kind == ItemKind.Food;                                 // Foods full -> Supplies
            Check("loop data: a weapon / food goes to its own group first, then to Supplies", order);
            bag.Add(Item.Food(Foods.Meat)); bag.Add(Item.Food(Foods.Meat));                      // Supplies now full (4)
            int before = bag.Total;
            Item extra = Item.Food(Foods.Apple);
            Check("loop data: when both the group and Supplies are full the item is refused and nothing is lost",
                bag.Add(extra) == BagResult.Full && bag.Total == before && !bag.CanAdd(extra));
            Check("loop data: a duplicate weapon still fits a full bag (it merges)",
                bag.CanAdd(Item.Weapon(Weapons.Fang, 0)) && bag.Add(Item.Weapon(Weapons.Fang, 0)) == BagResult.Merged && bag.supplies[0].plus == 1 && bag.Total == before);

            // +N merge: (+0)+(+0) = +1, (+2)+(+0) = +3, capped at +5
            Check("loop data: merge +0 and +0 gives +1, +2 and +0 gives +3, the result is capped at +5",
                ItemBag.MergePlus(0, 0) == 1 && ItemBag.MergePlus(2, 0) == 3 && ItemBag.MergePlus(5, 0) == 5 && ItemBag.MergePlus(4, 3) == 5);

            // moves
            Loadout m = Loadout.NewGame();
            m.stash.Add(Item.Weapon(Weapons.Rapier, 2));
            m.stash.Add(Item.Food(Foods.Apple));
            bool mv = m.MoveToCarry(Section.Weapons, 0) && m.carry.weapons[0].id == Weapons.Iron && m.stash.weapons[0].IsNone;
            mv &= m.MoveToCarry(Section.Foods, 0) && m.carry.foods[0].kind == ItemKind.Food && m.stash.foods[0].IsNone;
            mv &= !m.MoveToCarry(Section.Foods, 0);                                    // empty slot: nothing to move
            mv &= m.MoveToStash(Section.Weapons, 0) && m.stash.weapons[0].id == Weapons.Iron && m.carry.weapons[0].IsNone;
            Check("loop data: moving between stash and carry keeps the item and empties the source slot", mv);
            m.MoveToCarry(Section.Weapons, 0); m.MoveToCarry(Section.Weapons, 1);    // carry weapons: Iron, Rapier +2
            m.stash.Add(Item.Weapon(Weapons.Dao));
            int daoAt = -1;
            for (int i = 0; i < m.stash.weapons.Length; i++) if (m.stash.weapons[i].IsWeapon && m.stash.weapons[i].id == Weapons.Dao) daoAt = i;
            Check("loop data: with both Weapons slots taken a moved weapon lands in Carry Supplies",
                daoAt >= 0 && m.MoveToCarry(Section.Weapons, daoAt) && m.carry.supplies[0].id == Weapons.Dao && m.stash.weapons[daoAt].IsNone);
            for (int i = 0; i < 5; i++) m.carry.Add(Item.Food(Foods.Meat));        // Foods (Apple + 2) full, Supplies (Dao + 3) full
            m.stash.Add(Item.Weapon(Weapons.Boss));
            int stashBefore = m.stash.Total;
            int bossAt = -1;
            for (int i = 0; i < m.stash.weapons.Length; i++) if (m.stash.weapons[i].IsWeapon && m.stash.weapons[i].id == Weapons.Boss) bossAt = i;
            Check("loop data: carry full -> MoveToCarry returns false and nothing changes",
                !m.MoveToCarry(Section.Weapons, bossAt) && m.stash.Total == stashBefore && m.stash.weapons[bossAt].id == Weapons.Boss);

            // held slot
            Loadout h = Loadout.NewGame();
            h.AutoPack();
            Check("loop data: AutoPack puts the first stash weapon into HELD (Iron on a new save)",
                h.Held.IsWeapon && h.Held.id == Weapons.Iron && h.CanEnter && h.stash.weapons[0].IsNone);
            h.carry.Add(Item.Weapon(Weapons.Rapier));
            h.SetHeld(1);
            Check("loop data: SetHeld picks the other weapon slot; an empty slot cannot be held", h.heldSlot == 1 && h.Held.id == Weapons.Rapier && !h.SetHeld(0 + 5));
            h.carry.weapons[1] = Item.None;
            h.EnsureHeld();
            Check("loop data: when the held slot is emptied HELD moves to the other weapon", h.heldSlot == 0 && h.Held.id == Weapons.Iron);

            // survive / die
            Loadout s = Loadout.NewGame();
            s.stash.Add(Item.Weapon(Weapons.Greatsword));
            s.stash.Add(Item.Food(Foods.Meat));
            s.MoveToCarry(Section.Weapons, 0);                                      // Iron
            s.BeginRun();
            s.carry.Add(Item.Weapon(Weapons.Boss));                                 // picked up in the run: NEW
            s.carry.Add(Item.Weapon(Weapons.Iron));                                 // a duplicate of the carried one: Iron +1
            s.carry.Add(Item.Food(Foods.Apple));
            RunResult rs = s.Survive();
            bool bossNew = false, ironPlus1 = false;
            foreach (ResultLine ln in rs.brought)
            {
                if (ln.item.IsWeapon && ln.item.id == Weapons.Boss && ln.isNew) bossNew = true;
                if (ln.item.IsWeapon && ln.item.id == Weapons.Iron && ln.item.plus == 1 && !ln.isNew) ironPlus1 = true;
            }
            Check("loop data: Survive moves every carried item into the stash, keeps +N, marks only unseen weapons NEW",
                rs.victory && !s.inRun && s.carry.Total == 0 && bossNew && ironPlus1 && rs.leftBehind.Count == 0
                && s.stash.HasWeaponId(Weapons.Boss) && s.stash.HasWeaponId(Weapons.Iron) && s.stash.HasWeaponId(Weapons.Greatsword)
                && s.stash.Count(Section.Foods) == 2);

            Loadout f = Loadout.NewGame();
            for (int i = 0; i < 5; i++) f.stash.Add(Item.Weapon(i));                // Iron merges into +1: five weapon stacks
            for (int i = 0; i < 8; i++) f.stash.Add(Item.Food(i % 2));
            for (int i = 0; i < 16; i++) f.stash.Add(Item.Food(Foods.Apple));
            f.carry.Add(Item.Food(Foods.Meat));
            f.BeginRun();
            RunResult rf = f.Survive();
            Check("loop data: Survive with a full stash reports the item as LEFT BEHIND and loses it",
                rf.leftBehind.Count == 1 && rf.leftBehind[0].item.kind == ItemKind.Food && f.carry.Total == 0);

            Loadout d = Loadout.NewGame();
            d.stash.Add(Item.Weapon(Weapons.Fang));
            d.stash.Add(Item.Food(Foods.Apple));
            d.MoveToCarry(Section.Weapons, 0);                                      // Iron
            d.MoveToCarry(Section.Foods, 0);
            d.BeginRun();
            RunResult rd = d.Die(false);
            Check("loop data: Die empties the carry, lists it as LOST, keeps the stash (Crimson Fang) and does not restore Iron",
                !rd.victory && rd.lost.Count == 2 && d.carry.Total == 0 && !d.inRun && !rd.ironRestored
                && rd.safe.Count == 1 && rd.safe[0].item.id == Weapons.Fang && d.stash.HasWeaponId(Weapons.Fang) && !d.stash.HasWeaponId(Weapons.Iron));

            Loadout n = Loadout.NewGame();
            n.AutoPack();
            n.BeginRun();
            RunResult rn = n.Die(false);
            Check("loop data: dying with the only weapon gives a free Iron Sword back in the stash",
                rn.ironRestored && n.stash.HasWeaponId(Weapons.Iron) && n.stash.weapons[0].plus == 0 && n.stash.Total == 1);

            Loadout a = Loadout.NewGame();
            a.AutoPack();
            a.inRun = true;
            RunResult ra = a.Die(true);
            Check("loop data: an abandoned run is a death flagged abandoned", ra.abandoned && !ra.victory && ra.lost.Count == 1);

            Loadout e = new Loadout();
            Check("loop data: EnsureIron on an empty loadout adds one Iron Sword once", e.EnsureIron() && !e.EnsureIron() && e.stash.Total == 1);
        }

        // Phase 2: old worlds (pure data).
        void OldWorldData()
        {
            // a run that ends in death creates an old world holding everything that was carried
            Loadout l = Loadout.NewGame();
            l.stash.Add(Item.Weapon(Weapons.Rapier, 2));
            l.MoveToCarry(Section.Weapons, 0);                                      // Iron
            l.carry.Add(Item.Food(Foods.Apple));
            l.carry.Add(Item.Food(Foods.Apple));
            l.BeginRun();
            RunResult r = l.Die(false, 12.5f, -40f, 1000);
            OldWorld w = l.worlds.Count == 1 ? l.worlds[0] : null;
            Check("old world: dying in a new world creates one old world: 3 returns left, the pile holds all 3 carried items where he fell, seed not 0",
                w != null && w.left == 3 && w.pile.Count == 3 && w.seed != 0 && Mathf.Abs(w.x - 12.5f) < 0.01f && Mathf.Abs(w.z + 40f) < 0.01f
                && r.worldNote == WorldNote.Created && r.worldSeed == w.seed && r.pileCount == 3 && l.carry.Total == 0 && !l.inRun && l.runWorld == 0
                && w.fellUnix == 1000 && w.Code.Length == 5 && w.Code[0] == '#');
            Check("old world: what stayed at home is still in the stash and the run did not touch another old world",
                l.stash.HasWeaponId(Weapons.Rapier) && r.safe.Count >= 1 && !l.stash.HasWeaponId(Weapons.Iron) == !r.ironRestored);

            // dying with an empty carry in a new world creates nothing
            Loadout e = Loadout.NewGame();
            e.BeginRun();
            RunResult re = e.Die(false, 0f, 0f, 5);
            Check("old world: dying with nothing carried creates no old world", e.worlds.Count == 0 && re.worldNote == WorldNote.None);

            // two weapons of one kind lie as two items (they merge only when they are taken back)
            Loadout m = Loadout.NewGame();
            m.carry.weapons[0] = Item.Weapon(Weapons.Iron, 1);
            m.carry.supplies[0] = Item.Weapon(Weapons.Fang, 0);
            m.carry.foods[0] = Item.Food(Foods.Meat);
            m.BeginRun();
            m.Die(false, 1f, 1f, 50);
            OldWorld mw = m.worlds[0];
            int ironInPile = 0;
            foreach (Item it in mw.pile) if (it.IsWeapon && it.id == Weapons.Iron && it.plus == 1) ironInPile++;
            Check("old world: items keep their +N in the pile (Iron +1, Fang, Roast Meat)", mw.pile.Count == 3 && ironInPile == 1);

            // entering the old world, dying again: same world, same counter, the pile moves to the new spot and grows
            mw.left = 2;
            m.carry.weapons[0] = Item.Weapon(Weapons.Dao, 0);
            m.carry.foods[0] = Item.Food(Foods.Apple);
            m.BeginRun(mw.seed);
            RunResult r2 = m.Die(false, -30f, 20f, 99999);
            Check("old world: dying again in the old world adds to the same pile (3 + 2 items), keeps the counter (2) and moves the pile",
                m.worlds.Count == 1 && mw.pile.Count == 5 && mw.left == 2 && Mathf.Abs(mw.x + 30f) < 0.01f && Mathf.Abs(mw.z - 20f) < 0.01f
                && r2.worldNote == WorldNote.Added && r2.inOldWorld && mw.fellUnix == 50 && r2.pileCount == 5);

            // taking things back: merge +N, the pack limit, Take all
            Loadout t = Loadout.NewGame();
            OldWorld tw = new OldWorld { seed = 0x1234, left = 3 };
            tw.pile.Add(Item.Weapon(Weapons.Iron, 0));
            tw.pile.Add(Item.Weapon(Weapons.Iron, 0));
            tw.pile.Add(Item.Weapon(Weapons.Rapier, 3));
            tw.pile.Add(Item.Weapon(Weapons.Fang, 0));
            tw.pile.Add(Item.Weapon(Weapons.Dao, 0));
            tw.pile.Add(Item.Weapon(Weapons.Boss, 0));
            for (int i = 0; i < 4; i++) tw.pile.Add(Item.Food(Foods.Apple));
            t.worlds.Add(tw);
            t.carry.weapons[0] = Item.Weapon(Weapons.Greatsword, 0);
            bool a = t.TakeFromPile(tw, 0) && t.carry.weapons[1].id == Weapons.Iron;            // free Weapons slot
            bool b = t.TakeFromPile(tw, 0) && t.carry.weapons[1].plus == 1;                   // the other Iron merges: +1
            int n = t.TakeAll(tw);                                                            // Rapier, Fang, Dao, Boss, 4 Apples: the 4th Apple does not fit
            Check("old world: taking from the pile follows the pack rules (second Iron merges to +1, Take all stops when the pack is full and leaves the rest)",
                a && b && n == 7 && tw.pile.Count == 1 && t.carry.Count(Section.Weapons) == 2 && t.carry.Count(Section.Supplies) == 4 && t.carry.Count(Section.Foods) == 3);
            int before = tw.pile.Count;
            Item left = before > 0 ? tw.pile[0] : Item.None;
            Check("old world: a full pack refuses the item and the pile does not change", before == 1 && !t.carry.CanAdd(left) && !t.TakeFromPile(tw, 0) && tw.pile.Count == before);

            // the pile limit
            Loadout big = Loadout.NewGame();
            OldWorld bw = new OldWorld { seed = 777, left = 3 };
            for (int i = 0; i < OldWorld.MaxPile; i++) bw.pile.Add(Item.Food(Foods.Apple));
            big.worlds.Add(bw);
            big.carry.foods[0] = Item.Food(Foods.Meat);
            big.carry.foods[1] = Item.Food(Foods.Meat);
            big.BeginRun(777);
            RunResult rb = big.Die(false, 0f, 0f, 1);
            Check("old world: a pile holds at most " + OldWorld.MaxPile + " items, the rest is reported as overflow",
                bw.pile.Count == OldWorld.MaxPile && rb.overflow.Count == 2);

            // survive: counters of every old world go down; the world played in is closed when its pile is empty
            Loadout s = Loadout.NewGame();
            OldWorld w1 = new OldWorld { seed = 0x1111, left = 3 }; w1.pile.Add(Item.Weapon(Weapons.Fang));
            OldWorld w2 = new OldWorld { seed = 0x2222, left = 1 }; w2.pile.Add(Item.Food(Foods.Apple)); w2.pile.Add(Item.Food(Foods.Meat));
            OldWorld w3 = new OldWorld { seed = 0x3333, left = 2 };
            s.worlds.Add(w1); s.worlds.Add(w2); s.worlds.Add(w3);
            s.AutoPack();
            s.BeginRun(0x1111);                                                               // played in w1 (pile not empty)
            RunResult rs = s.Survive();
            Check("old world: a successful return counts every old world down (3->2, 2->1), the one at 1 crumbles with its pile and is reported",
                rs.victory && w1.left == 2 && w3.left == 1 && s.Find(0x2222) == null && s.worlds.Count == 2
                && s.unseen.Count == 1 && s.unseen[0].seed == 0x2222 && s.unseen[0].items == 2 && rs.crumbs.Count == 3
                && rs.worldNote == WorldNote.StillInPile && rs.worldLeft == 2 && rs.pileCount == 1);
            s.AutoPack();
            s.BeginRun(0x3333);                                                               // w3 has an empty pile: closes
            RunResult rs2 = s.Survive();
            Check("old world: returning from an old world whose pile is empty closes it (not a crumble, nothing in unseen) and still counts the others down",
                s.Find(0x3333) == null && rs2.worldNote == WorldNote.Recovered && s.unseen.Count == 1 && w1.left == 1 && s.worlds.Count == 1);
            s.AutoPack();
            s.BeginRun();
            s.Survive();
            Check("old world: the third return from any world crumbles the first one and everything in it is gone for good",
                s.worlds.Count == 0 && s.unseen.Count == 2 && s.unseen[1].seed == 0x1111 && s.unseen[1].items == 1);

            // abandoned = a death in the world he was in
            Loadout ab = Loadout.NewGame();
            ab.AutoPack();
            ab.BeginRun();
            ab.runX = 40f; ab.runZ = 15f; ab.runPosValid = true;
            RunResult rab = ab.Die(true, ab.runX, ab.runZ, 10);
            Check("old world: an abandoned run puts the carried items in a pile at the last saved spot", rab.abandoned && ab.worlds.Count == 1 && Mathf.Abs(ab.worlds[0].x - 40f) < 0.01f);

            // many worlds: every code differs
            Loadout many = Loadout.NewGame();
            var codes = new HashSet<string>();
            for (int i = 0; i < 30; i++)
            {
                many.carry.foods[0] = Item.Food(Foods.Apple);
                many.BeginRun();
                many.Die(false, 0f, 0f, 1000);                                                // the same clock second on purpose
            }
            foreach (OldWorld mwld in many.worlds) codes.Add(mwld.Code);
            Check("old world: 30 deaths in new worlds make 30 old worlds with 30 different codes", many.worlds.Count == 30 && codes.Count == 30);

            // Sanitize
            Loadout sz = new Loadout();
            sz.stash.weapons[0] = Item.Weapon(Weapons.Fang);
            sz.carry.weapons[0] = Item.Weapon(Weapons.Fang);
            sz.inRun = true;
            sz.Sanitize();
            Check("old world: during a run a weapon in both stash and carry is left alone (the stash copy is safe)", sz.stash.HasWeaponId(Weapons.Fang) && sz.carry.HasWeaponId(Weapons.Fang));
            sz.inRun = false;
            sz.Sanitize();
            Check("old world: with no run open the duplicate is removed from the stash (carry wins)", !sz.stash.HasWeaponId(Weapons.Fang) && sz.carry.HasWeaponId(Weapons.Fang));
            var junk = new Loadout();
            junk.worlds.Add(new OldWorld { seed = 5, left = 9 });
            junk.worlds.Add(new OldWorld { seed = 5, left = 2 });
            junk.worlds.Add(new OldWorld { seed = 0, left = 2 });
            junk.worlds.Add(new OldWorld { seed = 6, left = 0 });
            var fat = new OldWorld { seed = 7, left = 1, x = float.NaN, z = 5000f };
            for (int i = 0; i < 60; i++) fat.pile.Add(Item.Food(Foods.Apple));
            fat.pile.Add(new Item { kind = ItemKind.Weapon, id = 999 });
            junk.worlds.Add(fat);
            junk.runWorld = 12345;
            junk.Sanitize();
            Check("old world: Sanitize repairs a damaged list (left 9 -> 3, duplicate / zero seed / expired dropped, pile cut to 48, position clamped, unknown runWorld cleared)",
                junk.worlds.Count == 2 && junk.Find(5).left == 3 && junk.Find(7).pile.Count == OldWorld.MaxPile && junk.runWorld == 0
                && !float.IsNaN(junk.Find(7).x) && Mathf.Abs(junk.Find(7).z) < Game.ArenaHalf && junk.trimNote.Length > 0);
            var hundred = new Loadout();
            for (int i = 1; i <= 105; i++) hundred.worlds.Add(new OldWorld { seed = i, left = 3 });
            hundred.Sanitize();
            Check("old world: more than " + OldWorld.MaxWorlds + " worlds in a file: the extra ones are dropped and reported", hundred.worlds.Count == OldWorld.MaxWorlds && hundred.unseen.Count == 6 && hundred.trimNote.Length > 0);
        }
#endif
    }
}

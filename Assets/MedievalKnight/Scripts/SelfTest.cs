using System.Collections;
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
                Check("the five sword icons exist (128 x 128), are not empty and all look different", okIcons && seenHash.Count == Weapons.All.Length);
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
                Vector3[] spots = { new Vector3(0f, 0f, -150f), new Vector3(0f, 0f, 0f), new Vector3(-140f, 0f, 40f), new Vector3(150f, 0f, 120f) };
                string[] names = { "town", "meadow", "autumn forest", "pine forest" };
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
                }
                Log("frame time (1280x720, selftest window): " + ft);
                k.transform.position = new Vector3(2f, 0f, -5f); g.SnapCamera();
                ParkSlimes();
                yield return W(0.3f);
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

            // ------------------------------------------------ sword drop, pickup, inventory panel, the five swords
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
                    + ", iron " + dropCount[Weapons.Iron] + ", boss " + dropCount[Weapons.Boss] + "; chance green/blue/red "
                    + Weapons.SlimeDropChance[0] + "/" + Weapons.SlimeDropChance[1] + "/" + Weapons.SlimeDropChance[2] + ", minotaur " + Weapons.MinotaurDropChance);
                Check("slimes only drop the three slime swords, in a 45 / 35 / 20 split",
                    Mathf.Abs(dropCount[Weapons.Rapier] - 450) <= 1 && Mathf.Abs(dropCount[Weapons.Greatsword] - 350) <= 1 && Mathf.Abs(dropCount[Weapons.Fang] - 200) <= 1
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
                bool dropped = Pickup.All.Count == pk0 + 1 && pk != null && pk.weaponId >= Weapons.Rapier && pk.weaponId <= Weapons.Fang;
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
                Check("all five swords are owned after collecting the pickups", inv.OwnedCount == 5 && Pickup.All.Count == 0);
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
                    yield return W(0.3f);
                }
                Check("the five sword models have five different lengths", distinct);
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
                while (Nearest<Slime>(Vector3.zero) == null && Time.realtimeSinceStartup - tFood < 6f) yield return null;   // the next wave
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
                    k.inventory.OwnedCount == owned0 && owned0 == 5 && k.inventory.equipped == equipped0 && k.SwordModel.name == k.Weapon.name && k.souls == souls0 && souls0 > 0);
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

            yield return W(0.4f);
            Log("done, screenshots=" + shot + ", failed checks=" + fails);
            Application.Quit();
        }
#endif
    }
}

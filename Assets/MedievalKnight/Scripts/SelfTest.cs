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
    // picks upgrades (by key and by mouse click), checks that the upgrade changed combat numbers,
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
            k.hp = k.MaxHp; k.armor = Knight.MaxArmor; k.stamina = Knight.MaxStamina;
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

        static void SetMouse(Vector2 pos, bool left)
        {
            var st = new MouseState { position = pos };
            if (left) st = st.WithButton(MouseButton.Left, true);
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
        }

        // Picks card 1 for any level-up that opens while another test is running (the panel pauses the game).
        IEnumerator ClearLevelUps()
        {
            int guard = 0;
            while ((g.LevelUpOpen || g.progress.pendingChoices > 0) && guard++ < 40)
            {
                if (g.LevelUpOpen) yield return Tap(Key.Digit1);
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
            Shot("start");
            yield return null;

            // park the slimes in the south-west corner so the dummy tests are not disturbed by kills
            Vector3 corner = new Vector3(-15.5f, 0f, -15.5f);
            foreach (Enemy e in Enemy.All)
            {
                Slime sl = e as Slime;
                if (sl != null) sl.Warp(corner + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)));
            }

            // ------------------------------------------------ EXP table
            string table = "";
            int total = 0;
            for (int l = 1; l < Progress.MaxLevel; l++) { table += " L" + l + ">" + (l + 1) + "=" + Progress.Need(l); total += Progress.Need(l); }
            Log("exp table:" + table + "  total=" + total);
            Check("exp curve is strictly increasing by x1.5", Progress.Need(1) == 30 && Progress.Need(2) == 45 && Progress.Need(9) > Progress.Need(8));

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
            for (int i = 0; i < Knight.Attacks.Length; i++)
            {
                AttackDef a = Knight.Attacks[i];
                Heal();
                StandNear(d, 1.3f);
                yield return W(0.35f);
                float s0 = k.stamina, hp0 = d.hp, ar0 = d.armor;
                int h0 = d.hits;
                float t0 = Time.time;
                k.DebugAttack(i);
                yield return W(a.windupTime + a.strikeTime * 0.7f);
                Shot("attack" + (i + 1) + "_" + a.name.Replace(" ", ""));
                yield return WaitIdle(2f);
                float took = Time.time - t0;
                if (i == 0) sideSlashTime = took;
                Log("attack " + (i + 1) + " " + a.name + ": stamina " + F(s0) + " -> " + F(k.stamina) + " (cost " + a.stamina + "), dummy armor " + F(ar0) + " -> " + F(d.armor)
                    + ", hp " + F(hp0) + " -> " + F(d.hp) + ", hits +" + (d.hits - h0) + ", last " + d.lastHit + ", duration " + F(took) + "s");
                Check("attack " + (i + 1) + " hits the dummy for base damage " + a.damage, d.hits - h0 == 1 && Mathf.Abs(d.lastDamage - a.damage) < 0.01f);
                yield return W(0.15f);
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
                Keys(Key.LeftShift);
                yield return W(0.3f);
                float a0 = k.armor, s0 = k.stamina;
                k.TakeDamage(20f, k.transform.position + k.Forward * 2f, "selftest");
                Log("block: blocking=" + k.IsBlocking + " armor " + F(a0) + " -> " + F(k.armor) + ", stamina " + F(s0) + " -> " + F(k.stamina));
                Check("frontal block cuts 20 damage to 4 and costs 15 stamina", k.IsBlocking && Mathf.Abs(a0 - k.armor - 4f) < 0.01f && Mathf.Abs(s0 - k.stamina - 15f) < 0.6f);
                yield return W(0.1f);
                Shot("block");
                Keys();
                yield return W(0.2f);
                Heal();
                float a1 = k.armor, h1 = k.hp;
                k.armor = 10f;
                k.TakeDamage(30f, k.transform.position + k.Forward * 2f, "selftest");
                Log("unblocked 30 with 10 armor: armor 10 -> " + F(k.armor) + ", hp " + F(h1) + " -> " + F(k.hp));
                Check("armor absorbs first, the rest hits health", k.armor < 0.01f && Mathf.Abs(h1 - k.hp - 20f) < 0.01f);
                Heal();
            }

            // ------------------------------------------------ skills (real keys Q / E / R / F)
            Key[] skillKeys = { Key.Q, Key.E, Key.R, Key.F };
            int killsBefore = g.progress.kills;
            float expBefore = g.progress.totalExp;
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
                    yield return Tap(Key.Digit2);
                    float cur1 = pr.Current(pick.stat, pick.target);
                    Log("picked card 2 with key '2': " + pick.title + " " + pick.line + ": x" + F(cur0) + " -> x" + F(cur1));
                    Check("key 2 applies the upgrade (+" + pick.amount + "x) and closes the panel",
                        !g.LevelUpOpen && Mathf.Abs(cur1 - cur0 - pick.amount) < 0.001f && pr.pendingChoices == 0);
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
                bool ok = found && sl.Dead && pr.totalExp - e0 >= Slime.ExpFor(sl.kind) - 0.01f;
                Log("sword kill: " + (found ? sl.label + " maxHp " + F(sl.maxHp) + " dead=" + sl.Dead : "no slime") + " after " + swings + " swings, total exp " + F(e0) + " -> " + F(pr.totalExp));
                Check("sword attacks kill a slime and its EXP is granted", ok);
                yield return W(0.8f);
                yield return ClearLevelUps();
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

                    // stand still and let it swing: screenshot each telegraph once
                    bool sawSweep = false, sawSmash = false, sawCharge = false;
                    float worstHp = k.MaxHp + Knight.MaxArmor;
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
                        if (g.LevelUpOpen) { yield return Tap(Key.Digit1); tEnd += 1f; }
                        // once both melee attacks were seen, back off so that it charges
                        if (sawSweep && sawSmash && !sawCharge && m.State == Minotaur.S.Chase && (m.transform.position - k.transform.position).magnitude < 8f)
                        {
                            Vector3 away = m.transform.position.z > 0f ? new Vector3(0f, 0f, -11f) : new Vector3(0f, 0f, 11f);
                            k.transform.position = new Vector3(Mathf.Clamp(m.transform.position.x, -8f, 8f), 0f, m.transform.position.z + away.z);
                            k.aimOverride = m.transform.position;
                        }
                        yield return null;
                    }
                    Log("minotaur attacks seen: sweep=" + sawSweep + " smash=" + sawSmash + " charge=" + sawCharge + ", knight lowest hp+armor " + F(worstHp) + " / " + F(k.MaxHp + Knight.MaxArmor));
                    Check("Minotaur telegraphs and lands attacks on the knight", (sawSweep || sawSmash) && worstHp < k.MaxHp + Knight.MaxArmor - 5f);
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
                    Log("picked card 1 with a mouse click at " + c + ": " + pick.title + " " + pick.line + ": x" + F(cur0) + " -> x" + F(cur1));
                    Check("mouse click applies the upgrade (+" + pick.amount + "x)", !g.LevelUpOpen && Mathf.Abs(cur1 - cur0 - pick.amount) < 0.001f);
                }
                else Check("second level-up panel opened", false);
                yield return W(0.4f);
            }

            // ------------------------------------------------ to max level; always take the first card
            {
                Progress pr = g.progress;
                // finish the Minotaur with one huge hit: checks its EXP reward and gets it out of the way
                Minotaur m = g.minotaur;
                if (m != null)
                {
                    float e0 = pr.totalExp;
                    int l0 = pr.level;
                    m.TakeHit(99999f, 1f, k.transform.position, false, 0f, 0f, "selftest");
                    yield return W(0.3f);
                    Shot("minotaur_slain");
                    Log("minotaur finished by the self-test with a 99999 hit: total exp " + F(e0) + " -> " + F(pr.totalExp) + ", level " + l0 + " -> " + pr.level
                        + ", pending choices " + pr.pendingChoices + ", respawn in " + F(g.MinotaurRespawnIn) + "s");
                    Check("Minotaur death gives 250 EXP and starts the respawn timer", Mathf.Abs(pr.totalExp - e0 - Minotaur.ExpReward) < 0.01f && g.minotaur == null && g.MinotaurRespawnIn > 15f);
                }
                Heal(); StandNear(d, 1.3f);
                int safety = 0;
                while ((!pr.IsMax || pr.pendingChoices > 0) && safety++ < 30)
                {
                    Heal();
                    if (pr.pendingChoices == 0 && !g.LevelUpOpen) g.GrantExp(Progress.Need(pr.level) - pr.exp, k.transform.position + Vector3.up * 2f);
                    float t0 = Time.realtimeSinceStartup;
                    while (!g.LevelUpOpen && Time.realtimeSinceStartup - t0 < 4f) yield return null;
                    yield return W(0.15f);
                    if (pr.level == 6) Shot("levelup_panel_lv6");
                    // prefer an "all attacks" card so the dummy check below sees a change
                    int idx = 0;
                    for (int i = 0; i < g.offers.Length; i++) if (g.offers[i].target == 0) idx = i;
                    yield return Tap(idx == 0 ? Key.Digit1 : idx == 1 ? Key.Digit2 : Key.Digit3);
                    yield return W(0.35f);
                }
                Log("EXP granted directly by the self-test up to max level. level " + pr.level + ", pending " + pr.pendingChoices
                    + ", all: dmg x" + F(pr.DamageMult(-1)) + " spd x" + F(pr.SpeedMult(-1)) + " (sword x" + F(pr.BasicSpeed()) + ") crit " + F(pr.CritChance(-1) * 100f) + "% x" + F(pr.CritMult(-1)));
                for (int i = 0; i < 4; i++)
                    Log("  skill " + Knight.Skills[i].key + ": dmg x" + F(pr.DamageMult(i)) + " spd x" + F(pr.SpeedMult(i)) + " crit " + F(pr.CritChance(i) * 100f) + "% x" + F(pr.CritMult(i)));
                Check("level caps at 10 with all 9 upgrades picked", pr.level == Progress.MaxLevel && pr.pendingChoices == 0 && !g.LevelUpOpen);
                int lv = pr.level;
                g.GrantExp(500f, k.transform.position);
                Check("no EXP past max level", pr.level == lv && pr.exp == 0f && pr.pendingChoices == 0);
                yield return W(0.5f);
                Shot("max_level_hud");

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
                float expect = Knight.Attacks[0].damage * pr.DamageMult(-1);
                Log("after upgrades, Side Slash (no crit): dealt " + F(d.lastDamage) + ", expected 10 x " + F(pr.DamageMult(-1)) + " = " + F(expect)
                    + "; duration " + F(took) + "s vs " + F(sideSlashTime) + "s at level 1 (sword speed x" + F(pr.BasicSpeed()) + ")");
                Check("damage upgrade multiplies the hit", Mathf.Abs(d.lastDamage - expect) < 0.01f);
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
                float expectCrit = expect * pr.CritMult(-1);
                Log("forced crit Side Slash: dealt " + F(d.lastDamage) + " crit=" + d.lastCrit + ", expected " + F(expect) + " x " + F(pr.CritMult(-1)) + " = " + F(expectCrit));
                Check("crit multiplies the hit by the crit multiplier", d.lastCrit && Mathf.Abs(d.lastDamage - expectCrit) < 0.01f);
                Progress.ForceCrit = 0;

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

            // ------------------------------------------------ death and revive
            {
                yield return WaitIdle(3f);
                k.TakeDamage(9999f, k.transform.position + Vector3.right, "selftest");
                bool dead = k.Dead;
                yield return W(0.5f);
                Shot("knight_down");
                float t0 = Time.realtimeSinceStartup;
                while (k.Dead && Time.realtimeSinceStartup - t0 < 6f) yield return null;
                yield return W(0.3f);
                Log("death: dead=" + dead + " -> revived=" + !k.Dead + " hp " + F(k.hp) + "/" + F(k.MaxHp) + " at " + V(k.transform.position) + ", level kept " + g.progress.level);
                Check("knight dies and revives with full health, level kept", dead && !k.Dead && k.hp >= k.MaxHp - 0.01f && g.progress.level == Progress.MaxLevel);
                Shot("revived");
            }

            // ------------------------------------------------ restart key
            {
                yield return Tap(Key.F5);
                yield return W(1.2f);
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

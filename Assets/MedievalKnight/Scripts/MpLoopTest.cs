using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace MedievalKnight
{
    // Phase 4: scripted online test of the LOOP across several game processes on this PC (127.0.0.1). Active with
    //   -mptest <folder> -role H|A|B|C   and WITHOUT -arena (with -arena MpTest.cs runs the v1 suite).
    //
    //   host:    MedievalKnight.exe -host -bind 127.0.0.1 -port P -name Host -mptest out\H -role H -expect 3 [-refusals]
    //   clients: MedievalKnight.exe -join 127.0.0.1 -port P -name Alice -mptest out\A -role A      (then B, C)
    //
    // Like MpTest the host conducts over Msg.Test ("c|id|target|name|args" and "r|id|role|payload") and most checks
    // compare what every process reports about itself. Saves stay in memory (scripted runs never touch the real file).
    // Test-only time hooks: Game.TestDownSeconds (60 -> 6) and TeamRun.TestHomeSeconds (15 -> 3).
    public class MpLoopTest : MonoBehaviour
    {
        string dir, role;
        int shot, fails, nextId;
        Game g;
        readonly Dictionary<string, string> replies = new Dictionary<string, string>();     // "id|role" -> payload (conductor)
        readonly Dictionary<string, int> pidOf = new Dictionary<string, int>();             // role -> pid (conductor)
        readonly List<string> clients = new List<string>();                                 // roles that reported in (conductor)
        bool expectHostQuit;
        string lastBye = "";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void StartIfRequested(GameObject host)
        {
#if ENABLE_INPUT_SYSTEM
            if (Args.mpTestDir == null || Args.arena) return;
            var t = host.AddComponent<MpLoopTest>();
            t.dir = Args.mpTestDir;
            t.role = Args.role.Length > 0 ? Args.role : (Args.host ? "H" : "A");
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static WaitForSecondsRealtime W(float s) { return new WaitForSecondsRealtime(s); }
        static float RT { get { return Time.realtimeSinceStartup; } }
        static string F1(float v) { return v.ToString("0.0", Inv); }
        static string F2(float v) { return v.ToString("0.00", Inv); }
        static float P(string s) { float v; return float.TryParse(s, NumberStyles.Float, Inv, out v) ? v : float.NaN; }

        void Log(string s) { Debug.Log("MPTEST [" + role + "] " + s); }

        void Check(string what, bool ok)
        {
            if (!ok) fails++;
            Debug.Log("MPTEST " + (ok ? "PASS" : "FAIL") + " [" + role + "] " + what);
        }

        void Shot(string label)
        {
            shot++;
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, shot.ToString("00") + "_" + label + ".png"));
        }

        void Finish() { Log("done, screenshots=" + shot + ", failed checks=" + fails); }

        static void Keys(params Key[] keys) { InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys)); }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
            Directory.CreateDirectory(dir);
            g = Game.I;
            Net.OnTest = OnTest;
            Progress.ForceCrit = 1;
            Game.TestDownSeconds = 6f;                                // 60 -> 6: a fall can be waited out
            TeamRun.TestHomeSeconds = 3f;                             // 15 -> 3
            StartCoroutine(Watchdog(role == "H" ? 420f : 450f));
            if (Args.expectText.Length > 0) yield return RefusedScript();
            else if (role == "H") yield return HostScript();
            else yield return ClientScript();
        }

        IEnumerator Watchdog(float seconds)
        {
            yield return W(seconds);
            Check("the test finished within " + seconds + " s (watchdog)", false);
            Finish();
            Application.Quit();
        }

        // keeps the local knight alive and the level-up panels answered, like MpTest
        bool autoHeal = true, autoLevel = true;
        float panelSince;

        void Update()
        {
            if (g == null || !g.InSession || g.knight == null) return;
            Knight k = g.knight;
            if (autoHeal && !k.Dead) { k.hp = k.MaxHp; k.armor = k.MaxArmor; k.stamina = k.MaxStamina; }
            if (g.LevelStep == 0) panelSince = Time.unscaledTime;
            else if (autoLevel && Time.unscaledTime - panelSince > 0.4f)
            {
                panelSince = Time.unscaledTime;
                if (g.LevelStep == 1) g.ChooseUpgrade(0);
                else
                {
                    int best = 0;
                    for (int i = 1; i < 3; i++) if (k.progress.attr[i] < k.progress.attr[best]) best = i;
                    g.ChooseStat(best);
                }
            }
        }

        // ---------------------------------------------------------------- messages / conductor

        void OnTest(string text)
        {
            string[] p = text.Split('|');
            if (p[0] == "hello" && role == "H" && p.Length >= 3)
            {
                int pid;
                if (int.TryParse(p[2], out pid) && !pidOf.ContainsKey(p[1])) { pidOf[p[1]] = pid; clients.Add(p[1]); clients.Sort(); }
            }
            else if (p[0] == "r" && role == "H" && p.Length >= 4) replies[p[1] + "|" + p[2]] = p[3];
            else if (p[0] == "c" && p.Length >= 4 && (p[2] == role || p[2] == "all"))
            {
                var args = new string[p.Length - 4];
                System.Array.Copy(p, 4, args, 0, args.Length);
                StartCoroutine(Exec(p[1], p[3], args));
            }
        }

        void Reply(string id, string payload)
        {
            if (role == "H") replies[id + "|H"] = payload;
            else Net.SendTest("r|" + id + "|" + role + "|" + payload);
        }

        int Send(string target, string name, params string[] args)
        {
            int id = ++nextId;
            string msg = "c|" + id + "|" + target + "|" + name;
            foreach (string a in args) msg += "|" + a;
            if (target != "H") Net.SendTest(msg);
            if (target == "H" || target == "all") StartCoroutine(Exec(id.ToString(), name, args));
            return id;
        }

        List<string> Targets(string target)
        {
            var l = new List<string>();
            if (target == "all") { l.Add("H"); l.AddRange(clients); }
            else l.Add(target);
            return l;
        }

        IEnumerator Wait(int id, string target, float timeout = 15f)
        {
            float t0 = RT;
            List<string> want = Targets(target);
            while (RT - t0 < timeout)
            {
                bool all = true;
                foreach (string r in want) if (!replies.ContainsKey(id + "|" + r)) all = false;
                if (all) yield break;
                yield return null;
            }
            foreach (string r in want)
                if (!replies.ContainsKey(id + "|" + r)) Check("answer from " + r + " to command " + id + " within " + timeout + " s", false);
        }

        int last;

        IEnumerator Cmd(string target, string name, params string[] args)
        {
            if (name == "shot" && fails == 0) yield break;            // screenshots only once something has failed
            last = Send(target, name, args);
            yield return Wait(last, target, 20f);
        }

        string Rep(int id, string r) { string s; return replies.TryGetValue(id + "|" + r, out s) ? s : ""; }

        class Map : Dictionary<string, string>
        {
            public string G(string key) { string v; return TryGetValue(key, out v) ? v : ""; }
            public float F(string key) { return P(G(key)); }
        }

        static Map Parse(string payload)
        {
            var d = new Map();
            foreach (string part in payload.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0) d[part.Substring(0, eq)] = part.Substring(eq + 1);
            }
            return d;
        }

        readonly Dictionary<string, Map> sums = new Dictionary<string, Map>();

        IEnumerator Sum()
        {
            yield return Cmd("all", "sum");
            sums.Clear();
            foreach (string r in Targets("all")) sums[r] = Parse(Rep(last, r));
        }

        // the summary of ONE process (cheaper than all)
        IEnumerator SumOf(string r)
        {
            yield return Cmd(r, "sum");
            sums[r] = Parse(Rep(last, r));
        }

        string S(string r, string key) { Map d; return sums.TryGetValue(r, out d) ? d.G(key) : ""; }
        float N(string r, string key) { return P(S(r, key)); }

        string note = "";

        bool Eq(string key)
        {
            string seen = "", first = null;
            bool same = true;
            foreach (string r in Targets("all"))
            {
                string v = S(r, key);
                seen += r + "=" + v + " ";
                if (first == null) first = v; else if (v != first) same = false;
            }
            note += key + "[" + seen.Trim() + "] ";
            return same;
        }

        void Ck(bool ok, string what)
        {
            Check(what + (note.Length > 0 ? "   {" + note.Trim() + "}" : ""), ok);
            note = "";
        }

        static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        // ---------------------------------------------------------------- what every process reports about itself

        string Summary()
        {
            var sb = new StringBuilder();
            RunLoop run = g.run;
            Knight me = g.knight;
            TeamRun t = run != null ? run.team : null;
            sb.Append("st=").Append(run != null ? run.State.ToString() : "arena")
              .Append(";coop=").Append(g.Coop ? 1 : 0)
              .Append(";mode=").Append(Net.Mode)
              .Append(";n=").Append(g.players.Count)
              .Append(";en=").Append(CountEnemies())
              .Append(";mino=").Append(g.minotaur != null && !g.minotaur.Dead ? "alive" : "none");
            if (t != null)
            {
                sb.Append(";live=").Append(t.runLive ? 1 : 0).Append(";play=").Append(t.LocalPlaying ? 1 : 0).Append(";rseed=").Append(t.worldSeed).Append(";rleft=").Append(t.worldLeft);
                string room = "", ready = "", inrun = "", stat = "";
                foreach (Member m in t.roster)
                {
                    room += m.pid + ":" + m.name + ",";
                    ready += m.pid + ":" + (m.ready ? 1 : 0) + ",";
                    inrun += m.pid + ":" + (m.inRun ? 1 : 0) + ",";
                    stat += m.pid + ":" + (int)m.status + ",";
                }
                sb.Append(";room=").Append(room).Append(";ready=").Append(ready).Append(";inrun=").Append(inrun).Append(";stat=").Append(stat);
                sb.Append(";rows=").Append(run.teamUi != null ? run.teamUi.RowsShown : -1).Append(";wait=").Append((run.teamUi != null ? run.teamUi.WaitingText : "").Replace(';', ',').Replace('=', ':'));
            }
            if (run != null && run.data != null)
            {
                Loadout d = run.data;
                sb.Append(";carry=").Append(d.carry.Total).Append(";stash=").Append(d.stash.Total).Append(";worlds=").Append(d.worlds.Count)
                  .Append(";inRun=").Append(d.inRun ? 1 : 0).Append(";rc=").Append(d.runCoop ? 1 : 0).Append(";held=").Append(d.Held.Name.Replace(';', ','));
                string cl = "";
                foreach (Item it in d.carry.All()) cl += (it.IsWeapon ? "W" : "F") + it.id + "+" + it.plus + ",";
                sb.Append(";carrylist=").Append(cl);
                string wl = "";
                foreach (OldWorld w in d.worlds) wl += w.seed + "/" + w.left + "/" + w.piles.Count + "/" + w.ItemCount + ",";
                sb.Append(";wlist=").Append(wl);
            }
            if (me != null)
            {
                Vector3 p = me.Pos;
                sb.Append(";x=").Append(F2(p.x)).Append(";z=").Append(F2(p.z)).Append(";lvl=").Append(me.progress.level).Append(";hp=").Append(F1(me.hp)).Append(";mhp=").Append(F1(me.MaxHp))
                  .Append(";dead=").Append(me.Dead ? 1 : 0).Append(";fallen=").Append(me.Fallen ? 1 : 0).Append(";protect=").Append(me.Glowing ? 1 : 0);
            }
            var ps = new List<Knight>(g.players);
            ps.Sort((a, b) => a.pid.CompareTo(b.pid));
            foreach (Knight k in ps)
            {
                Vector3 p = k.Pos;
                sb.Append(";x").Append(k.pid).Append('=').Append(F2(p.x)).Append(";z").Append(k.pid).Append('=').Append(F2(p.z))
                  .Append(";lv").Append(k.pid).Append('=').Append(k.progress.level)
                  .Append(";dead").Append(k.pid).Append('=').Append(k.Dead ? 1 : 0)
                  .Append(";fall").Append(k.pid).Append('=').Append(k.Fallen ? 1 : 0)
                  .Append(";hp").Append(k.pid).Append('=').Append(F1(k.hp));
            }
            string pk = "";
            foreach (Pickup q in Pickup.All) if (!q.hidden) pk += q.weaponId + "@" + F1(q.transform.position.x) + "," + F1(q.transform.position.z) + " ";
            sb.Append(";pk=").Append(Pickup.All.Count).Append(";pklist=").Append(pk.Trim());
            sb.Append(";notice=").Append(g.hud.NoticeShown.Replace(';', ',').Replace('=', ':').Replace('|', '/'))
              .Append(";banner=").Append(g.hud.BannerShown.Replace(';', ',').Replace('=', ':').Replace('|', '/').Replace('\n', ' '));
            if (run != null && run.ui != null)
                sb.Append(";sum=").Append(run.SummaryOpen ? 1 : 0).Append(";sumtitle=").Append(run.ui.SummaryTitle.Replace(';', ',').Replace('=', ':'))
                  .Append(";sumbody=").Append(run.ui.SummaryBody.Replace(';', ',').Replace('=', ':').Replace('|', '/').Replace('\n', '~'));
            sb.Append(";menu=").Append(g.menu != null && g.menu.Visible ? 1 : 0).Append(";menumsg=").Append(g.menu != null ? g.menu.Message.Replace(';', ',').Replace('=', ':') : "");
            return sb.ToString();
        }

        static int CountEnemies()
        {
            int n = 0;
            foreach (Enemy e in Enemy.All) if (!e.Dead && !(e is Dummy)) n++;
            return n;
        }

        IEnumerator Exec(string id, string name, string[] a)
        {
            Knight k = g.knight;
            RunLoop run = g.run;
            if (name == "sum") { Reply(id, g.InSession && k != null ? Summary() : "st=nosession;menu=" + (g.menu != null && g.menu.Visible ? 1 : 0) + ";menumsg=" + (g.menu != null ? g.menu.Message : "")); yield break; }
            if (k == null || run == null) { Reply(id, "nosession"); yield break; }
            switch (name)
            {
                case "shot":
                    Shot(a[0]);
                    yield return null;
                    Reply(id, "ok");
                    break;

                case "tp":                                            // x z
                    k.transform.position = new Vector3(P(a[0]), 0f, P(a[1]));
                    k.aimOverride = k.transform.position + Vector3.forward * 5f;
                    k.moveOverride = Vector2.zero;
                    g.SnapCamera();
                    yield return W(0.25f);
                    Reply(id, "x=" + F2(k.transform.position.x) + ";z=" + F2(k.transform.position.z));
                    break;

                case "aim":                                           // a free local knight: no input device needed
                    k.aimOverride = k.transform.position + Vector3.forward * 5f;
                    k.moveOverride = Vector2.zero;
                    Reply(id, "ok");
                    break;

                case "carryclear":
                    run.data.carry.Clear();
                    run.data.heldSlot = 0;
                    run.team.SendPack();
                    Reply(id, "carry=" + run.data.carry.Total);
                    break;

                case "give":                                          // kind(w|f) id plus: into the carry by the normal rules
                {
                    Item it = a[0] == "w" ? Item.Weapon(int.Parse(a[1]), int.Parse(a[2])) : Item.Food(int.Parse(a[1]));
                    BagResult r = run.data.carry.Add(it);
                    run.data.EnsureHeld();
                    run.team.SendPack();
                    Reply(id, "r=" + r + ";carry=" + run.data.carry.Total);
                    break;
                }

                case "stashadd":
                {
                    Item it = a[0] == "w" ? Item.Weapon(int.Parse(a[1]), int.Parse(a[2])) : Item.Food(int.Parse(a[1]));
                    run.data.stash.Add(it);
                    Reply(id, "stash=" + run.data.stash.Total);
                    break;
                }

                case "world":                                         // seed left x z item...: an old world on the host (test setup)
                {
                    var w = new OldWorld { seed = int.Parse(a[0]), left = int.Parse(a[1]), fellUnix = 1760000000L };
                    Pile p = w.NewPile(P(a[2]), P(a[3]));
                    for (int i = 4; i + 2 < a.Length; i += 3)
                        p.items.Add(a[i] == "w" ? Item.Weapon(int.Parse(a[i + 1]), int.Parse(a[i + 2])) : Item.Food(int.Parse(a[i + 1])));
                    run.data.worlds.Add(w);
                    Reply(id, "worlds=" + run.data.worlds.Count);
                    break;
                }

                case "ready":                                         // 1 / 0: the READY button of a client
                    if (run.State != LoopState.Prepare) g.SetInventory(true);
                    yield return null;
                    if (a[0] == "1") { if (!run.team.LocalReady) run.UiReady(); }
                    else if (run.team.LocalReady) run.UiReady();
                    yield return null;
                    Reply(id, "ready=" + (run.team.LocalReady ? 1 : 0) + ";warn=" + run.ui.WarnText.Replace(';', ','));
                    break;

                case "enter":                                         // seed ("0" = new world): the host's ENTER button
                    if (run.State != LoopState.Prepare) g.SetInventory(true);
                    yield return null;
                    run.UiEnter(int.Parse(a[0]));
                    yield return null;
                    Reply(id, "asked=" + (run.ui.PromptShown ? 1 : 0) + ";prompt=" + run.ui.WarnText.Replace(';', ','));
                    break;

                case "confirm":
                    run.UiConfirmEnter();
                    yield return null;
                    Reply(id, "ok");
                    break;

                case "autoheal":
                    autoHeal = a[0] == "1";
                    Reply(id, "ok");
                    break;

                case "settle":
                {
                    float t0 = RT;
                    while ((k.progress.pendingChoices > 0 || g.LevelStep != 0) && RT - t0 < 12f) yield return null;
                    Reply(id, "ok");
                    break;
                }

                case "quit":
                    Finish();
                    Reply(id, "ok");
                    yield return W(0.3f);
                    Application.Quit();
                    break;

                case "crash":
                    Log("killing this process now (simulated crash)");
                    Finish();
                    yield return W(0.2f);
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                    break;

                case "prepquit":
                    expectHostQuit = true;
                    Reply(id, "ok");
                    break;

                default:
                    Reply(id, "unknown");
                    break;
            }
        }

        // ---------------------------------------------------------------- a client

        IEnumerator ClientScript()
        {
            float t0 = RT;
            while (!g.InSession && RT - t0 < 40f) yield return null;
            Check("joined the host's game (" + (g.InSession ? "player " + Net.LocalPid : "menu says: " + g.menu.Message) + ")", g.InSession && Net.IsClient);
            if (!g.InSession) { Shot("not_connected"); yield return W(0.5f); Finish(); Application.Quit(); yield break; }
            Check("the host's mode came with the welcome: this client plays the loop (Game.Coop, base, PREPARE open)", g.Coop && g.run != null && g.run.PrepareOpen);
            g.knight.aimOverride = g.knight.transform.position + Vector3.forward * 5f;
            g.knight.moveOverride = Vector2.zero;
            for (int i = 0; i < 3; i++) { Net.SendTest("hello|" + role + "|" + Net.LocalPid); yield return W(0.4f); }

            while (g.InSession) yield return null;
            yield return W(0.6f);
            Log("session ended; menu visible=" + g.menu.Visible + ", message '" + g.menu.Message + "', knights " + g.players.Count + ", enemies " + Enemy.All.Count);
            Check("the host quitting was announced beforehand by the test", expectHostQuit);
            Check("back at the menu with a message that the host closed the game", g.menu.Visible && g.menu.Message.ToLowerInvariant().Contains("host closed") && g.players.Count == 0 && Net.Mode == NetMode.Single);
            Shot("menu_after_host_quit");
            yield return W(0.6f);
            Finish();
            Application.Quit();
        }

        IEnumerator RefusedScript()
        {
            float t0 = RT;
            while (!g.InSession && !(g.menu.Current == Menu.Page.Join && g.menu.Message.Length > 0) && RT - t0 < 45f) yield return null;
            string msg = g.menu.Message;
            Log("join attempt ended after " + F1(RT - t0) + " s: in session=" + g.InSession + ", message '" + msg + "'");
            Check("the join was refused cleanly with a message containing '" + Args.expectText + "'",
                !g.InSession && g.menu.Visible && msg.ToLowerInvariant().Contains(Args.expectText.ToLowerInvariant()) && Net.Mode == NetMode.Single && !Net.Connecting);
            yield return W(0.9f);
            Finish();
            Application.Quit();
        }

        // ---------------------------------------------------------------- the host conducts

        IEnumerator HostScript()
        {
            yield return W(1.0f);
            Check("hosting: session running and listening on port " + Net.Port, g.InSession && Net.IsHost);
            if (!g.InSession) { Shot("not_hosting"); Log("menu says: " + g.menu.Message); yield return W(0.5f); Finish(); Application.Quit(); yield break; }
            g.dropOverride = 0;
            g.foodOverride = 0;
            g.knight.aimOverride = g.knight.transform.position + Vector3.forward * 5f;
            g.knight.moveOverride = Vector2.zero;

            float t0 = RT;
            while (clients.Count < Args.expect && RT - t0 < 60f) yield return W(0.25f);
            Check(Args.expect + " client(s) joined and reported in (" + string.Join(",", clients.ToArray()) + ")", clients.Count == Args.expect);
            if (clients.Count < 3) { Finish(); Application.Quit(); yield break; }
            string A = clients[0], B = clients[1], C = clients[2];
            int pidA = pidOf[A], pidB = pidOf[B], pidC = pidOf[C];
            Log("roles: " + A + " = player " + pidA + ", " + B + " = player " + pidB + ", " + C + " = player " + pidC + "; the host is player 0");
            yield return W(0.8f);

            // ------------------------------------------------ 1. the room: everybody at their own base, one roster
            yield return Sum();
            Ck(Eq("coop") && S("H", "coop") == "1" && Eq("st") && S("H", "st") == "Prepare", "everybody plays the loop and stands in his own PREPARE (Game.Coop, no arena)");
            Ck(Eq("room") && S("H", "room").Split(',').Length == 5, "every process lists the same 4 players in the room (" + S("H", "room") + ")");
            Ck(Eq("n") && S("H", "n") == "1" && Eq("en") && S("H", "en") == "0", "at the base nobody sees the others' knights and there are no enemies (each has 1 knight, 0 enemies)");
            Ck(Eq("live") && S("H", "live") == "0", "no run is open");
            Shot("room_4");

            // ------------------------------------------------ 2. READY needs a Weapon
            yield return Cmd(A, "carryclear");
            yield return Cmd(A, "ready", "1");
            Map rdy = Parse(Rep(last, A));
            yield return W(0.4f);
            yield return Sum();
            Check("READY without a Weapon is refused (" + A + " says '" + rdy.G("warn") + "', ready=" + rdy.G("ready") + ", the host sees " + S("H", "ready") + ")",
                rdy.G("ready") == "0" && rdy.G("warn").Contains("Weapon") && !S("H", "ready").Contains(pidA + ":1"));
            yield return Cmd(A, "give", "w", Weapons.Iron.ToString(), "0");
            yield return Cmd(A, "give", "f", Foods.Apple.ToString());
            yield return Cmd(A, "ready", "1");
            yield return W(0.5f);
            yield return Sum();
            Ck(Eq("ready") && S("H", "ready").Contains(pidA + ":1") && S(A, "carry") == "2", A + " packed an Iron Sword and an Apple and is READY on every screen");
            yield return Cmd(B, "ready", "1");
            yield return W(0.5f);
            yield return Sum();
            Ck(Eq("ready") && S("H", "ready").Contains(pidA + ":1") && S("H", "ready").Contains(pidB + ":1") && S("H", "ready").Contains(pidC + ":0"), "A and B are READY, C is not (all four rosters agree)");

            // ------------------------------------------------ 3. the host starts without C: asked first, then A and B come along
            yield return Cmd("H", "give", "f", Foods.Apple.ToString());
            yield return Cmd("H", "enter", "0");
            Map ask = Parse(Rep(last, "H"));
            Check("ENTER with Cara not ready asks first: the question names her (" + ask.G("prompt") + ")", ask.G("asked") == "1" && S("H", "live") == "0");
            yield return W(0.2f);
            yield return Cmd("H", "confirm");
            yield return W(2.2f);
            yield return Sum();
            Ck(Eq("live") && S("H", "live") == "1", "the run is open on every screen");
            Check("A, B and the host are in the world (state World), C waits at the base (still PREPARE) with the 'run in progress' line",
                S("H", "st") == "World" && S(A, "st") == "World" && S(B, "st") == "World" && S(C, "st") == "Prepare" && S(C, "play") == "0" && S(C, "wait").Contains("Run in progress"));
            Check("the three players see each other: 3 knights on H, A and B; C has only his own and no enemies",
                S("H", "n") == "3" && S(A, "n") == "3" && S(B, "n") == "3" && S(C, "n") == "1" && S(C, "en") == "0" && N("H", "en") >= 1f);
            Check("everybody in the world starts at level 1 next to each other",
                S("H", "lvl") == "1" && S(A, "lvl") == "1" && S(B, "lvl") == "1" && Flat(new Vector3(N("H", "x"), 0f, N("H", "z")), new Vector3(N("H", "x" + pidA), 0f, N("H", "z" + pidA))) < 5f
                && Flat(new Vector3(N("H", "x"), 0f, N("H", "z")), new Vector3(N("H", "x" + pidB), 0f, N("H", "z" + pidB))) < 8f);
            Ck(Eq("rseed") && S("H", "rseed") == "0", "it is a new world (no old world seed) everywhere");
            Shot("in_world");

            yield return Cmd("all", "prepquit");
            Finish();
            yield return W(0.5f);
            Application.Quit();
        }
#endif
    }
}

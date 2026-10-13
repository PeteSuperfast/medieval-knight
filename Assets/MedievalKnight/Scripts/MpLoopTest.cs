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

        // like Eq but only over the processes that play the run (the host and the clients in `who`)
        bool EqP(string key, params string[] who)
        {
            string seen = "", first = null;
            bool same = true;
            foreach (string r in who)
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
                if (Net.IsHost)
                    foreach (Member m in t.roster)
                    {
                        string ml = "";
                        foreach (Item it in m.mirror.All()) ml += (it.IsWeapon ? "W" : "F") + it.id + "+" + it.plus + ",";
                        sb.Append(";mir").Append(m.pid).Append('=').Append(m.mirror.Total).Append(";mirlist").Append(m.pid).Append('=').Append(ml).Append(";grants").Append(m.pid).Append('=').Append(m.grants);
                    }
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
            if (run != null) sb.Append(";portal=").Append(run.portal != null ? 1 : 0).Append(";boss=").Append(run.BossDown ? 1 : 0)
                .Append(";home=").Append(t != null && t.HomeStarted ? F1(t.HomeLeft) : "-").Append(";hometext=").Append(run.teamUi != null ? run.teamUi.HomeTextShown.Replace(';', ',').Replace('=', ':').Replace('\n', '~') : "");
            if (run != null && run.teamUi != null)
                sb.Append(";downui=").Append(run.teamUi.DownShown ? 1 : 0).Append(";downsub=").Append(run.teamUi.DownSubText.Replace(';', ',').Replace('=', ':'))
                  .Append(";revby=").Append(run.teamUi.ReviveByText.Replace(';', ',').Replace('=', ':')).Append(";revprompt=").Append(run.teamUi.RevivePromptText.Replace(';', ',').Replace('=', ':'))
                  .Append(";spec=").Append(run.teamUi.SpectatingText.Replace(';', ',').Replace('=', ':')).Append(";overhead=").Append(run.teamUi.OverheadShown);
            if (me != null) sb.Append(";lastact=").Append(me.LastAction).Append(";downleft=").Append(F1(me.DownLeft));
            if (run != null)
            {
                sb.Append(";piles=").Append(run.PileObjects).Append(";lootopen=").Append(run.LootOpen ? 1 : 0).Append(";packfull=").Append(run.PackFullHints);
                if (t != null)
                {
                    string hw = "";
                    foreach (OldWorld w in t.HostWorlds()) hw += w.seed + "/" + w.left + "/" + w.piles.Count + "/" + w.ItemCount + ",";
                    sb.Append(";hw=").Append(t.HostWorlds().Count).Append(";hwlist=").Append(hw);
                }
            }
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

                case "hp":                                           // fraction of the maximum: autoheal off
                    autoHeal = false;
                    k.hp = k.MaxHp * P(a[0]);
                    Reply(id, "hp=" + F1(k.hp));
                    break;

                case "eat":
                {
                    int before = run.data.carry.Total;
                    bool ate = run.EatFirst();
                    yield return null;
                    Reply(id, "ate=" + ate + ";carry=" + run.data.carry.Total + ";before=" + before + ";hp=" + F1(k.hp));
                    break;
                }

                case "drop":                                         // section index
                {
                    int before = run.data.carry.Total;
                    bool ok = run.UiCarryDrop((Section)int.Parse(a[0]), int.Parse(a[1]));
                    yield return null;
                    Reply(id, "ok=" + ok + ";carry=" + run.data.carry.Total + ";before=" + before);
                    break;
                }

                case "ret":                                           // RETURN TO BASE on the summary
                    run.UiReturn();
                    yield return null;
                    Reply(id, "st=" + run.State);
                    break;

                case "hold":                                          // seconds: hold the real F key
                    Keys(Key.F);
                    yield return W(P(a[0]));
                    Keys();
                    yield return null;
                    Reply(id, "lastact=" + k.LastAction);
                    break;

                case "die":
                    autoHeal = false;
                    k.TakeDamage(99999f, k.transform.position + Vector3.right, "mptest");
                    Reply(id, "dead=" + k.Dead);
                    break;

                case "gopile":                                        // index: stand 1 m from that pile (the window opens by itself)
                {
                    int i = int.Parse(a[0]);
                    if (i >= run.PileObjects) { Reply(id, "nopile"); break; }
                    k.transform.position = run.PilePosAt(i) + new Vector3(1.0f, 0f, 0f);
                    k.aimOverride = k.transform.position + Vector3.forward * 5f;
                    g.SnapCamera();
                    yield return W(0.7f);
                    Reply(id, "lootopen=" + (run.LootOpen ? 1 : 0) + ";items=" + run.PileItemsAt(i));
                    break;
                }

                case "take":                                          // index of the open pile
                {
                    int before = run.data.carry.Total;
                    run.UiTake(int.Parse(a[0]));
                    yield return W(0.8f);
                    Reply(id, "carry=" + run.data.carry.Total + ";before=" + before + ";notice=" + g.hud.NoticeShown.Replace(';', ',').Replace('=', ':'));
                    break;
                }

                case "takeall":
                {
                    int before = run.data.carry.Total;
                    run.UiTakeAll();
                    yield return W(0.8f);
                    Reply(id, "carry=" + run.data.carry.Total + ";before=" + before);
                    break;
                }

                case "closeloot":
                    if (run.LootOpen) g.SetInventory(false);
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

            // ------------------------------------------------ 4. items: pickup into the mirror, a full pack, a race, eat, drop
            yield return Cmd("all", "autoheal", "1");
            g.packsOff = true;
            foreach (Enemy e in new List<Enemy>(Enemy.All)) if (e is Slime) Destroy(e.gameObject);
            Vector3 spot = new Vector3(-6f, 0f, -140f);
            yield return Cmd(A, "tp", F2(spot.x), F2(spot.z));
            yield return Cmd(B, "tp", F2(spot.x + 30f), F2(spot.z));
            yield return Cmd("H", "tp", F2(spot.x - 20f), F2(spot.z));
            yield return W(0.4f);
            yield return Sum();
            int carryA0 = (int)N(A, "carry");
            g.SpawnPickup(Weapons.Fang, spot + new Vector3(0.2f, 0f, 0f));
            yield return W(1.2f);
            yield return Sum();
            Ck(S(A, "carry") == (carryA0 + 1).ToString() && S("H", "mir" + pidA) == (carryA0 + 1).ToString() && EqP("pk", "H", A, B) && S("H", "pk") == "0",
                A + " walked over a Crimson Fang: it is in his carry (" + S(A, "carry") + ") and in the host's copy (" + S("H", "mir" + pidA) + "), gone from the ground everywhere");
            Ck(S("H", "mirlist" + pidA) == S(A, "carrylist") || S("H", "mirlist" + pidA).Length == S(A, "carrylist").Length, "the host's copy of " + A + "'s pack lists the same items (" + S("H", "mirlist" + pidA) + " / " + S(A, "carrylist") + ")");

            // a full pack: the item stays on the ground, nothing is lost
            for (int i = 0; i < 6; i++) yield return Cmd(A, "give", i % 2 == 0 ? "f" : "w", i % 2 == 0 ? Foods.Meat.ToString() : (i == 1 ? Weapons.Dao : i == 3 ? Weapons.Rapier : Weapons.Greatsword).ToString(), "0");
            yield return W(0.5f);
            yield return Sum();
            int full = (int)N(A, "carry");
            g.SpawnPickup(Weapons.Boss, spot + new Vector3(0.3f, 0f, 0f));
            yield return W(1.2f);
            yield return Sum();
            Ck(S(A, "carry") == full.ToString() && S("H", "mir" + pidA) == full.ToString() && S("H", "pk") == "1" && EqP("pk", "H", A, B),
                A + "'s pack is full (" + full + "): the Horned King's Blade stays on the ground on every screen, nothing is lost");
            Check(A + " was told 'Pack full' (" + S(A, "notice") + ")", S(A, "notice").Contains("Pack full"));

            // eating: HP + one item less, the host's copy follows
            yield return Cmd(A, "hp", "0.4");
            yield return Cmd(A, "eat");
            Map ate = Parse(Rep(last, A));
            yield return W(0.6f);
            yield return Sum();
            Ck(ate.G("ate") == "True" && S(A, "carry") == (full - 1).ToString() && S("H", "mir" + pidA) == (full - 1).ToString(), A + " ate: his carry and the host's copy both went " + full + " -> " + (full - 1));
            yield return Cmd(A, "autoheal", "1");
            // the freed slot: the item on the ground is not taken by itself (A has room, a Blade is a Weapon: needs a Weapons / Supplies slot)
            yield return W(1.0f);
            yield return Sum();
            Ck(EqP("pk", "H", A, B), "after the meal the pickups are still the same on every screen (" + S("H", "pk") + ")");

            // dropping: A puts a Dao down; B walks over it
            yield return Cmd(A, "drop", "2", "0");
            Map dropped = Parse(Rep(last, A));
            yield return W(0.8f);
            yield return Sum();
            Ck(dropped.G("ok") == "True" && EqP("pk", "H", A, B) && S("H", "mir" + pidA) == S(A, "carry"), A + " dropped an item: it lies in the world on every screen and the host's copy matches his pack (" + S(A, "carry") + ")");
            Shot("items");

            // ------------------------------------------------ 5. the boss dies, the team goes home together
            {
                yield return Cmd("all", "autoheal", "1");
                // an old world that exists on the host: its counter goes down ONCE for the team, not once per player
                var ow = new OldWorld { seed = 0xA1B2, left = 3, fellUnix = 1760000000L };
                ow.NewPile(30f, -100f).items.Add(Item.Weapon(Weapons.Fang));
                ow.piles[0].items.Add(Item.Food(Foods.Meat));
                g.run.data.worlds.Add(ow);
                yield return Sum();
                int stashA0 = (int)N(A, "stash"), carryA = (int)N(A, "carry"), carryB = (int)N(B, "carry"), carryH = (int)N("H", "carry");
                Minotaur m = g.minotaur;
                Check("the Minotaur is alive on the host before the kill", m != null && !m.Dead);
                if (m != null) m.TakeHit(999999f, 1f, m.transform.position + Vector3.back * 2f, false, 0f, 0f, "mptest", g.knight);
                yield return W(1.5f);
                yield return Cmd("all", "settle");
                yield return Sum();
                Ck(S("H", "portal") == "1" && S(A, "portal") == "1" && S(B, "portal") == "1" && S(C, "portal") == "0", "the portal opened at the Minotaur's place for the three who play (not for " + C + " who waits)");
                Vector3 pp = g.run.portal != null ? g.run.portal.transform.position : Vector3.zero;
                yield return Cmd(B, "tp", F2(pp.x + 0.5f), F2(pp.z));
                yield return W(2.0f);
                yield return Sum();
                Ck(S("H", "home") != "-" && S(A, "home") != "-" && S(B, "home") != "-", B + " stood in the portal for 1.5 s: the countdown home runs on all three screens");
                Check("the countdown is shown (" + S(A, "hometext") + ")", S(A, "hometext").Contains("returning in"));
                yield return W(3.5f);
                yield return Sum();
                Ck(S("H", "sum") == "1" && S(A, "sum") == "1" && S(B, "sum") == "1" && S(C, "sum") == "0", "everybody who played got the summary at the same time (" + C + " did not: he was not in the run)");
                Check("the summary says the team returns home and lists the three players (" + S("H", "sumtitle") + ")",
                    S("H", "sumtitle").Contains("TEAM RETURNS HOME") && S(A, "sumbody").Contains("Alice") && S(A, "sumbody").Contains("Bob") && S(A, "sumbody").Contains("Host") && S(A, "sumbody").Contains("SURVIVED"));
                Check(A + "'s carry (" + carryA + " items) went into his stash (" + stashA0 + " -> " + S(A, "stash") + "), his carry is empty and no run is open",
                    N(A, "stash") == stashA0 + carryA && S(A, "carry") == "0" && S(A, "inRun") == "0" && S(A, "rc") == "0");
                Check(B + " and the host brought their things home too (stash " + S(B, "stash") + " / " + S("H", "stash") + ")", N(B, "stash") >= carryB && N("H", "stash") >= carryH && S("H", "carry") == "0");
                Check("no old world was made by the win and the host's old world #A1B2 went 3 -> 2 exactly once (" + S("H", "wlist") + ")",
                    S("H", "worlds") == "1" && S("H", "wlist").StartsWith("41394/2/"));
                Check(A + " and " + B + " (clients) have no old worlds of their own touched (" + S(A, "worlds") + ", " + S(B, "worlds") + ")", S(A, "worlds") == "0" && S(B, "worlds") == "0");
                yield return Cmd("all", "shot", "victory_summary");
                yield return Cmd(A, "ret");
                yield return Cmd(B, "ret");
                yield return Cmd("H", "ret");
                yield return W(0.8f);
                yield return Sum();
                Ck(Eq("st") && S("H", "st") == "Prepare" && Eq("n") && S("H", "n") == "1" && Eq("live") && S("H", "live") == "0",
                    "everybody is back at his own base with PREPARE open, one knight each, no run open");
            }

            // ------------------------------------------------ 6. a second run with four: A falls and B revives him, then A falls for good
            {
                // everybody gets a weapon and READY (C now too)
                foreach (string r in new[] { A, B, C })
                {
                    yield return Cmd(r, "carryclear");
                    yield return Cmd(r, "give", "w", Weapons.Iron.ToString(), "0");
                    yield return Cmd(r, "give", "f", Foods.Apple.ToString());
                    yield return Cmd(r, "ready", "1");
                }
                yield return W(0.5f);
                yield return Cmd("H", "carryclear");
                yield return Cmd("H", "give", "w", Weapons.Iron.ToString(), "0");
                yield return Cmd("H", "give", "f", Foods.Apple.ToString());
                yield return Cmd("H", "enter", "0");
                yield return W(2.4f);
                yield return Sum();
                Ck(Eq("live") && S("H", "live") == "1" && Eq("n") && S("H", "n") == "4" && S(C, "st") == "World", "the second run has all four players in the world (" + C + " too)");
                g.packsOff = true;
                foreach (Enemy e in new List<Enemy>(Enemy.All)) if (e is Slime) Destroy(e.gameObject);
                Vector3 camp = new Vector3(30f, 0f, -120f);
                yield return Cmd(A, "tp", F2(camp.x), F2(camp.z));
                yield return Cmd(B, "tp", F2(camp.x + 2f), F2(camp.z));
                yield return Cmd(C, "tp", F2(camp.x - 2f), F2(camp.z + 2f));
                yield return Cmd("H", "tp", F2(camp.x), F2(camp.z + 4f));
                yield return W(0.4f);
                yield return Cmd(A, "die");
                yield return W(0.9f);
                yield return Sum();
                Ck(S(A, "dead") == "1" && S(A, "fallen") == "0" && S("H", "dead" + pidA) == "1" && S(B, "dead" + pidA) == "1" && S(C, "dead" + pidA) == "1" && S("H", "fall" + pidA) == "0",
                    A + " is down (not fallen) and all four screens show him down");
                Check(A + "'s own screen: YOU ARE DOWN with the countdown (" + S(A, "downsub") + "), the others show DOWN over his head (" + S(B, "overhead") + ")",
                    S(A, "downui") == "1" && S(A, "downsub").Contains("Help is needed") && S(B, "overhead") == "1" && S("H", "overhead") == "1");
                Check("the roster says Down on the host and on C (" + S("H", "stat") + ")", S("H", "stat").Contains(pidA + ":1") && S(C, "stat").Contains(pidA + ":1"));
                Shot("a_down");
                // B stands next to him and holds F (the real key) for 2.4 s
                yield return Cmd(B, "tp", F2(camp.x + 1.5f), F2(camp.z));
                int hold = Send(B, "hold", "2.6");
                yield return W(0.9f);
                yield return Sum();
                Check("while B holds F: B sees 'Hold F: Revive " + "Alice" + "' (" + S(B, "revprompt") + ") and A sees who is reviving him (" + S(A, "revby") + ")",
                    S(B, "revprompt").Contains("Revive") && S(A, "revby").Contains("Bob"));
                yield return Wait(hold, B, 8f);
                yield return W(0.6f);
                yield return Sum();
                Ck(S(A, "dead") == "0" && S("H", "dead" + pidA) == "0" && S(B, "dead" + pidA) == "0" && S(C, "dead" + pidA) == "0", A + " is back on his feet on every screen after B held F for 2 s");
                float hpFrac = N(A, "hp") / N(A, "mhp");
                Check(A + " came back with about 30% health (" + S(A, "hp") + " / " + S(A, "mhp") + ") and the shield glow (" + S(A, "protect") + ")", hpFrac > 0.25f && hpFrac < 0.4f && S(A, "protect") == "1");
                Check("holding F did not start a Ground Slam for B (last action '" + S(B, "lastact") + "')", S(B, "lastact") != "Ground Slam");
                yield return Cmd("all", "shot", "a_revived");

                // not revived in time: fallen for good (6 s in the test), the others see FELL, A watches
                yield return Cmd(A, "autoheal", "1");
                yield return W(3.5f);                                  // (the shield of the revive)
                yield return Cmd(A, "tp", F2(camp.x - 6f), F2(camp.z + 8f));
                yield return Cmd(A, "die");
                yield return W(1.0f);
                yield return Sum();
                Check(A + " is down again (" + S(A, "downleft") + " s left)", S(A, "dead") == "1" && S(A, "fallen") == "0" && N(A, "downleft") > 0f);
                yield return W(Game.TestDownSeconds + 1.6f);
                yield return Sum();
                Ck(S(A, "fallen") == "1" && S("H", "fall" + pidA) == "1" && S(B, "fall" + pidA) == "1" && S(C, "fall" + pidA) == "1", A + " fell for good: fallen on every screen");
                Check("the roster says Fallen (" + S("H", "stat") + ") and A watches a friend (" + S(A, "spec") + ")", S("H", "stat").Contains(pidA + ":2") && S(A, "spec").Contains("SPECTATING"));
                Shot("a_fallen");
                // revive does not work any more
                yield return Cmd(B, "tp", F2(camp.x - 6f), F2(camp.z + 7f));
                yield return Cmd(B, "hold", "2.6");
                yield return W(0.5f);
                yield return Sum();
                Check("a fallen player can not be revived any more", S(A, "dead") == "1" && S(A, "fallen") == "1");

                // the team kills the boss with A fallen: A's things are still safe and come home
                yield return Sum();
                int carryAfell = (int)N(A, "carry"), stashAbefore = (int)N(A, "stash");
                Minotaur m2 = g.minotaur;
                if (m2 != null) m2.TakeHit(999999f, 1f, m2.transform.position + Vector3.back * 2f, false, 0f, 0f, "mptest", g.knight);
                yield return W(1.5f);
                yield return Cmd("all", "settle");
                Vector3 pp2 = g.run.portal != null ? g.run.portal.transform.position : Vector3.zero;
                yield return Cmd(C, "tp", F2(pp2.x + 0.5f), F2(pp2.z));
                yield return W(5.5f);
                yield return Sum();
                Ck(S("H", "sum") == "1" && S(A, "sum") == "1" && S(B, "sum") == "1" && S(C, "sum") == "1", "the team went home together with A fallen: everybody has the summary");
                Check("A's summary says he FELL but his " + carryAfell + " items came home (stash " + stashAbefore + " -> " + S(A, "stash") + ")",
                    S(A, "sumbody").Contains("FELL") && N(A, "stash") == stashAbefore + carryAfell && S(A, "carry") == "0");
                Check("no old world was made (the host still has 1: #A1B2) and the counter went 2 -> 1 once more (" + S("H", "wlist") + ")", S("H", "worlds") == "1" && S("H", "wlist").StartsWith("41394/1/"));
                yield return Cmd("all", "shot", "second_victory");
                foreach (string r in new[] { A, B, C, "H" }) yield return Cmd(r, "ret");
                yield return W(0.6f);
            }

            // ------------------------------------------------ 7. everybody falls: one old world, a pile for each place somebody fell
            int worldSeedNew = 0;
            {
                foreach (string r in new[] { A, B, C })
                {
                    yield return Cmd(r, "carryclear");
                    yield return Cmd(r, "give", "w", Weapons.Iron.ToString(), "0");
                    yield return Cmd(r, "give", "f", Foods.Apple.ToString());
                    yield return Cmd(r, "ready", "1");
                }
                yield return Cmd(A, "give", "w", Weapons.Dao.ToString(), "1");
                yield return Cmd(C, "give", "f", Foods.Meat.ToString());
                yield return W(0.5f);
                yield return Cmd("H", "carryclear");
                yield return Cmd("H", "give", "w", Weapons.Iron.ToString(), "0");
                yield return Cmd("H", "give", "w", Weapons.Fang.ToString(), "2");
                yield return Cmd("H", "give", "f", Foods.Apple.ToString());
                yield return Cmd("H", "enter", "0");
                yield return W(2.4f);
                yield return Sum();
                Check("a third run with all four starts (" + S("H", "n") + " knights)", S("H", "live") == "1" && S("H", "n") == "4");
                g.packsOff = true;
                foreach (Enemy e in new List<Enemy>(Enemy.All)) if (e is Slime) Destroy(e.gameObject);
                yield return Sum();
                int cA = (int)N(A, "carry"), cB = (int)N(B, "carry"), cC = (int)N(C, "carry"), cH = (int)N("H", "carry");
                OldWorld a1b2 = g.run.data.Find(0xA1B2);
                if (a1b2 != null) a1b2.left = 3;                                   // (it would crumble with this return; keep it for the count)
                int worlds0 = (int)N("H", "worlds");
                int stashA = (int)N(A, "stash"), stashC = (int)N(C, "stash");
                Vector3 p1 = new Vector3(40f, 0f, -100f);
                yield return Cmd(A, "tp", F2(p1.x), F2(p1.z));
                yield return Cmd(B, "tp", F2(p1.x + 1.5f), F2(p1.z));              // 1.5 m from A: their things share one pile
                yield return Cmd(C, "tp", F2(-40f), F2(-100f));
                yield return Cmd("H", "tp", F2(0f), F2(-60f));
                yield return W(0.5f);
                foreach (string r in new[] { A, B, C }) yield return Cmd(r, "autoheal", "0");
                yield return Cmd("H", "autoheal", "0");
                yield return Cmd(A, "die");
                yield return W(0.4f);
                yield return Sum();
                Check("with only some fallen the run goes on (no wipe yet): the host still plays", S("H", "st") == "World" && S("H", "live") == "1");
                yield return Cmd(B, "die");
                yield return Cmd(C, "die");
                yield return W(0.3f);
                yield return Cmd("H", "die");
                yield return W(1.5f);
                yield return Sum();
                Ck(S("H", "sum") == "1" && S(A, "sum") == "1" && S(B, "sum") == "1" && S(C, "sum") == "1", "the last one went down: the run ended for all four at once (the wipe)");
                Check("the summary says ALL FELL with the four names and 'items dropped' (" + S("H", "sumtitle") + ")",
                    S(A, "sumtitle").Contains("ALL FELL") && S(A, "sumbody").Contains("items dropped") && S(A, "sumbody").Contains("Alice") && S(A, "sumbody").Contains("Cara"));
                Check("one old world #" + "made on the host: " + S("H", "worlds") + " worlds now (was " + worlds0 + "), carry gone on every machine (" + S("H", "carry") + S(A, "carry") + S(B, "carry") + S(C, "carry") + ")",
                    N("H", "worlds") == worlds0 + 1 && S("H", "carry") == "0" && S(A, "carry") == "0" && S(B, "carry") == "0" && S(C, "carry") == "0");
                // the host's newest world: its piles
                OldWorld nw = null;
                foreach (OldWorld w in g.run.data.worlds) if (w.seed != 0xA1B2 && (nw == null || w.fellUnix >= nw.fellUnix)) nw = w;
                int total = cA + cB + cC + cH;
                Check("the new world holds all " + total + " items in 3 piles (A + B within 3 m share one, C one, the host one) at the places they fell",
                    nw != null && nw.ItemCount == total && nw.piles.Count == 3 && nw.left == 3);
                if (nw != null)
                {
                    worldSeedNew = nw.seed;
                    int near = 0;
                    foreach (Pile pl in nw.piles) { if (Flat(new Vector3(pl.x, 0f, pl.z), p1) < 3f) near = pl.items.Count; }
                    Check("the pile at A's place holds A's and B's things (" + near + " = " + (cA + cB) + ")", near == cA + cB);
                }
                Check("the stash and the worlds of A and C are untouched (stash " + S(A, "stash") + " / " + S(C, "stash") + ", worlds " + S(A, "worlds") + ")",
                    N(A, "stash") >= stashA && N(C, "stash") >= stashC && S(A, "worlds") == "0" && S(C, "worlds") == "0");
                // the others see the host's worlds list (read only) with the new world in it
                yield return W(0.6f);
                yield return Sum();
                Check("every client sees the host's old worlds list with the new world (" + S(A, "hwlist") + " / " + S(C, "hwlist") + ")",
                    S(A, "hw") == S("H", "worlds") && S(C, "hw") == S("H", "worlds") && S(A, "hwlist").Contains(worldSeedNew + "/3/3/"));
                Shot("wipe_summary");
                foreach (string r in new[] { A, B, C, "H" }) yield return Cmd(r, "ret");
                yield return W(0.6f);
            }

            // ------------------------------------------------ 8. back into that old world: loot of the others, a race for one item
            if (worldSeedNew != 0)
            {
                foreach (string r in new[] { A, B, C })
                {
                    yield return Cmd(r, "autoheal", "1");
                    yield return Cmd(r, "carryclear");
                    yield return Cmd(r, "give", "w", Weapons.Iron.ToString(), "0");
                    yield return Cmd(r, "give", "f", Foods.Apple.ToString());
                    yield return Cmd(r, "ready", "1");
                }
                yield return Cmd("H", "autoheal", "1");
                yield return Cmd("H", "carryclear");
                yield return Cmd("H", "give", "w", Weapons.Iron.ToString(), "0");
                yield return Cmd("H", "give", "f", Foods.Apple.ToString());
                yield return W(0.4f);
                yield return Sum();
                Check("the clients' OLD WORLDS view lists the world and the host's button reads OLD WORLDS (2)", S(A, "hw") == "2" || S(A, "hw") == "3");
                yield return Cmd("H", "enter", worldSeedNew.ToString());
                yield return W(2.6f);
                yield return Sum();
                Ck(S("H", "live") == "1" && S("H", "st") == "World" && S(A, "st") == "World" && S(B, "st") == "World" && S(C, "st") == "World", "all four are in the old world #" + (worldSeedNew & 0xFFFF).ToString("X4"));
                Check("every machine knows the world (seed " + S(A, "rseed") + ") and shows 3 piles",
                    S(A, "rseed") == worldSeedNew.ToString() && S(C, "rseed") == worldSeedNew.ToString() && S("H", "piles") == "3" && S(A, "piles") == "3" && S(B, "piles") == "3" && S(C, "piles") == "3");
                g.packsOff = true;
                foreach (Enemy e in new List<Enemy>(Enemy.All)) if (e is Slime) Destroy(e.gameObject);
                // B walks into the biggest pile (A's and B's things): the window opens and the game keeps running
                int big = 0, bigN = -1;
                for (int i = 0; i < g.run.PileObjects; i++) if (g.run.PileItemsAt(i) > bigN) { bigN = g.run.PileItemsAt(i); big = i; }
                yield return Cmd(B, "gopile", big.ToString());
                Map gp = Parse(Rep(last, B));
                Check("B touched the pile of " + bigN + ": the loot window opened by itself and time runs (the world is not paused)", gp.G("lootopen") == "1");
                yield return Sum();
                Check("... the others see him in the world and nothing stopped (time scale on B " + Time.timeScale.ToString("0.0", Inv) + ")", S("H", "st") == "World");
                int carryB0 = (int)N(B, "carry");
                yield return Cmd(B, "takeall");
                Map ta = Parse(Rep(last, B));
                yield return W(0.5f);
                yield return Sum();
                Check("B took everything that fits (his carry " + ta.G("before") + " -> " + ta.G("carry") + ")", ta.F("carry") > ta.F("before"));
                Ck(S("H", "piles") == "2" && EqP("piles", "H", A, B, C), "B emptied that pile: it is gone on all four screens (2 piles left)");
                yield return Cmd(B, "closeloot");

                // the race: A and C both stand at the host's pile and take item 0 at the same moment
                int hostPile = -1;
                for (int i = 0; i < g.run.PileObjects; i++) if (Flat(g.run.PilePosAt(i), new Vector3(0f, 0f, -60f)) < 6f) hostPile = i;
                if (hostPile >= 0)
                {
                    int items0 = g.run.PileItemsAt(hostPile);
                    yield return Cmd(A, "gopile", hostPile.ToString());
                    yield return Cmd(C, "gopile", hostPile.ToString());
                    yield return Sum();
                    int a0 = (int)N(A, "carry"), c0 = (int)N(C, "carry");
                    int sa = Send(A, "take", "2");                                 // the Apple of the host's pile
                    int sc = Send(C, "take", "2");
                    yield return Wait(sa, A);
                    yield return Wait(sc, C);
                    yield return W(0.5f);
                    yield return Sum();
                    int da = (int)N(A, "carry") - a0, dc = (int)N(C, "carry") - c0;
                    Check("two players took the Apple of the same pile at the same moment: exactly one got it (A +" + da + ", C +" + dc + ") and the pile lost exactly one (" + items0 + " -> " + (g.run.PileObjects > hostPile ? g.run.PileItemsAt(hostPile) : 0) + ")",
                        da + dc == 1);
                    string loserNotice = da == 0 ? Parse(Rep(sa, A)).G("notice") : Parse(Rep(sc, C)).G("notice");
                    Check("the one who lost it was told 'Taken by' (" + loserNotice + ") and nothing vanished silently", loserNotice.Contains("Taken by"));
                }
                else Check("the host's pile exists for the race", false);
                yield return Cmd("all", "shot", "old_world_loot");
            }

            // ------------------------------------------------ the extra join attempts were turned away
            if (Args.refusals)
            {
                t0 = RT;
                while ((Net.RefusedFull < 1 || Net.RefusedVersion < 1) && RT - t0 < 40f) yield return W(0.25f);
                Check("a 5th player was refused because the room is full (" + Net.RefusedFull + " refusal(s)) while the room kept its 4 players", Net.RefusedFull >= 1 && g.run.team.RoomCount == 4);
                Check("a client with another network version was refused (" + Net.RefusedVersion + " refusal(s))", Net.RefusedVersion >= 1);
            }

            yield return Cmd("all", "prepquit");
            Finish();
            yield return W(0.5f);
            Application.Quit();
        }
#endif
    }
}

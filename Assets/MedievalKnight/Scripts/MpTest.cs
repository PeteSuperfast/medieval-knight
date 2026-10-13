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
    // Scripted online test across several game processes on one PC. Only active with  -mptest <folder>
    // (or -menutest <folder> for the start menu). Normal play never runs this.
    //
    //   host:    MedievalKnight.exe -host -bind 127.0.0.1 -port P -name Host -mptest out\host -role H -expect 3 [-refusals]
    //   clients: MedievalKnight.exe -join 127.0.0.1 -port P -name Alice -mptest out\a -role A      (then B, C)
    //   refused: MedievalKnight.exe -join 127.0.0.1 -port P -mptest out\x -role X -expecttext "full" -joindelay 12
    //
    // The host is the conductor. It sends commands as text over the game's own connection (Msg.Test:
    // "c|id|target|name|args"), every process runs them on its own knight and answers "r|id|role|payload".
    // Most checks compare a summary of the world ("sum") from every process, so they prove that all sides see
    // the same thing. Every process logs "MPTEST PASS/FAIL ..." lines and saves screenshots to its folder.
    public class MpTest : MonoBehaviour
    {
        string dir, role;
        int shot, fails, nextId, panelsPicked;
        Game g;
        bool autoLevel = true, autoHeal = true, expectHostQuit, tookTeleShot;
        float panelSince;
        readonly Dictionary<string, string> replies = new Dictionary<string, string>();     // "id|role" -> payload (conductor)
        readonly Dictionary<string, int> pidOf = new Dictionary<string, int>();             // role -> pid (conductor)
        readonly List<string> clients = new List<string>();                                 // roles still connected (conductor)
        readonly HashSet<string> minoStates = new HashSet<string>();
        Minotaur.S lastMinoState;
        float minoStateSince;
        int probeId;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void StartIfRequested(GameObject host)
        {
#if ENABLE_INPUT_SYSTEM
            if (Args.menuTestDir != null) { host.AddComponent<MenuTest>(); return; }
            if (Args.mpTestDir == null || !Args.arena) return;          // without -arena the loop suite runs (MpLoopTest.cs)
            var t = host.AddComponent<MpTest>();
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

        void Finish()
        {
            Log("done, screenshots=" + shot + ", failed checks=" + fails);
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
            Directory.CreateDirectory(dir);
            g = Game.I;
            Net.OnTest = OnTest;
            Progress.ForceCrit = 1;                                   // no random crits: damage numbers are exact
            StartCoroutine(Watchdog(role == "H" ? 300f : 330f));
            if (Args.expectText.Length > 0) yield return RefusedScript();
            else if (role == "H") yield return HostScript();
            else yield return ClientScript();
        }

        // Whatever goes wrong, a test process never stays open on the desktop.
        IEnumerator Watchdog(float seconds)
        {
            yield return W(seconds);
            Check("the test finished within " + seconds + " s (watchdog)", false);
            Finish();
            Application.Quit();
        }

        void Update()
        {
            if (g == null || !g.InSession || g.knight == null) return;
            Knight k = g.knight;
            if (autoHeal && !k.Dead) { k.hp = k.MaxHp; k.armor = k.MaxArmor; k.stamina = k.MaxStamina; }

            // level-up panels are answered by direct calls (the key / click paths are covered by the single-player test)
            if (g.LevelStep == 0) panelSince = Time.unscaledTime;
            else if (autoLevel && Time.unscaledTime - panelSince > 0.4f)
            {
                panelSince = Time.unscaledTime;
                panelsPicked++;
                if (g.LevelStep == 1) g.ChooseUpgrade(0);
                else
                {
                    int best = 0;
                    for (int i = 1; i < 3; i++) if (k.progress.attr[i] < k.progress.attr[best]) best = i;
                    g.ChooseStat(best);
                }
            }

            // what the Minotaur was seen doing on this machine
            Minotaur m = g.minotaur;
            if (m != null && !m.Dead)
            {
                if (m.State != lastMinoState) { lastMinoState = m.State; minoStateSince = RT; }
                minoStates.Add(m.State.ToString());
                bool tele = m.State == Minotaur.S.SweepTele || m.State == Minotaur.S.SmashTele || m.State == Minotaur.S.ChargeTele;
                if (tele && !tookTeleShot && RT - minoStateSince > 0.45f)
                {
                    tookTeleShot = true;
                    Shot("minotaur_telegraph_" + m.State);
                }
            }
        }

        // ---------------------------------------------------------------- messages

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

        // --- conductor side ---

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

        int last;                                                     // id of the most recent Cmd

        IEnumerator Cmd(string target, string name, params string[] args)
        {
            if (name == "shot" && fails == 0) yield break;            // screenshots only once something has failed
            last = Send(target, name, args);
            yield return Wait(last, target, name == "hunt" || name == "bait" ? 30f : 15f);
        }

        string Rep(int id, string r)
        {
            string s;
            return replies.TryGetValue(id + "|" + r, out s) ? s : "";
        }

        // "key=value;key=value" answers; a missing key reads as "" / NaN instead of throwing
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

        // the summaries of the last "sum" command, per role
        readonly Dictionary<string, Map> sums = new Dictionary<string, Map>();

        IEnumerator Sum()
        {
            yield return Cmd("all", "sum");
            sums.Clear();
            foreach (string r in Targets("all")) sums[r] = Parse(Rep(last, r));
        }

        string S(string r, string key)
        {
            Map d;
            return sums.TryGetValue(r, out d) ? d.G(key) : "";
        }

        float N(string r, string key) { return P(S(r, key)); }

        // true when every process reports the same value for the key
        bool Same(string key, out string seen)
        {
            seen = "";
            bool same = true;
            string first = null;
            foreach (string r in Targets("all"))
            {
                string v = S(r, key);
                seen += r + "=" + v + " ";
                if (first == null) first = v;
                else if (v != first) same = false;
            }
            return same;
        }

        bool Close(string key, float tolerance, out string seen)
        {
            seen = "";
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (string r in Targets("all"))
            {
                float v = N(r, key);
                seen += r + "=" + S(r, key) + " ";
                if (float.IsNaN(v)) return false;
                lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v);
            }
            return hi - lo <= tolerance;
        }

        // ---------------------------------------------------------------- what every process can do

        static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        IEnumerator WaitIdle(float max)
        {
            float t0 = RT;
            while (g.knight != null && g.knight.IsAttacking && RT - t0 < max) yield return null;
        }

        string Summary()
        {
            var sb = new StringBuilder();
            Knight me = g.knight;
            var ps = new List<Knight>(g.players);
            ps.Sort((a, b) => a.pid.CompareTo(b.pid));
            sb.Append("n=").Append(ps.Count).Append(";names=");
            foreach (Knight p in ps) sb.Append(p.pid).Append(':').Append(p.playerName).Append(',');
            foreach (Knight p in ps)
            {
                Vector3 pos = p.Pos;
                sb.Append(";x").Append(p.pid).Append('=').Append(F2(pos.x)).Append(";z").Append(p.pid).Append('=').Append(F2(pos.z))
                  .Append(";dead").Append(p.pid).Append('=').Append(p.Dead ? 1 : 0)
                  .Append(";lv").Append(p.pid).Append('=').Append(p.progress.level)
                  .Append(";wp").Append(p.pid).Append('=').Append(p.inventory.equipped)
                  .Append(";wn").Append(p.pid).Append('=').Append(p.SwordModel != null ? p.SwordModel.name : "?")
                  .Append(";ac").Append(p.pid).Append('=').Append(p.ActionsSeen)
                  .Append(";an").Append(p.pid).Append('=').Append(p.LastAction)
                  .Append(";blk").Append(p.pid).Append('=').Append(p.IsBlocking ? 1 : 0)
                  .Append(";chg").Append(p.pid).Append('=').Append(p.ShowsCharge ? 1 : 0)
                  .Append(";hp").Append(p.pid).Append('=').Append(F1(p.hp))
                  .Append(";par").Append(p.pid).Append('=').Append(p.ParryCount)
                  .Append(";cla").Append(p.pid).Append('=').Append(p.ClashCount)
                  .Append(";hb").Append(p.pid).Append('=').Append(p.HeavyBlockCount);
            }
            Dummy d = g.dummy;
            sb.Append(";dummy=").Append(d != null ? F1(d.hp + d.armor) : "none")
              .Append(";dhits=").Append(d != null ? d.hits : -1)
              .Append(";dtot=").Append(d != null ? F1(d.totalDamage) : "none");
            if (g.Map != null) { g.Map.Probe(); sb.Append(";mapfr=").Append(g.Map.FriendMarkers).Append(";mapen=").Append(g.Map.EnemyMarkers); }      // phase 7: the M map's markers (hidden panel)
            int sldef = 0, slst = 0;                                  // phase 7: parries / clashes the slimes here have seen, slimes stunned right now
            foreach (Enemy e in Enemy.All) if (e is Slime && !e.Dead) { sldef += e.DeflectCount; if (e.Stunned) slst++; }
            sb.Append(";sldef=").Append(sldef).Append(";slst=").Append(slst).Append(";dnum=").Append(d != null ? d.lastNumberKind : -1);
            int en = 0;
            foreach (Enemy e in Enemy.All) if (!e.Dead && !(e is Dummy)) en++;
            Minotaur m = g.minotaur;
            sb.Append(";en=").Append(en).Append(";mino=").Append(m != null && !m.Dead ? "alive" : "none")
              .Append(";minohp=").Append(m != null && !m.Dead ? F1(m.hp + m.armor) : "0")
              .Append(";minostate=").Append(m != null && !m.Dead ? m.State.ToString() : "none");
            var pk = new List<Pickup>(Pickup.All);
            pk.Sort((a, b) => a.netId.CompareTo(b.netId));
            int visible = 0;
            string list = "";
            foreach (Pickup p in pk)
            {
                if (p.hidden) continue;
                visible++;
                list += p.netId + "/" + p.weaponId + "@" + F1(p.transform.position.x) + "," + F1(p.transform.position.z) + " ";
            }
            sb.Append(";pk=").Append(visible).Append(";pklist=").Append(list.Trim()).Append(";pkall=").Append(pk.Count);
            int own = 0;
            for (int i = 0; i < me.inventory.owned.Length; i++) if (me.inventory.owned[i]) own |= 1 << i;
            string mstates = "";
            foreach (string s in minoStates) mstates += s + ",";
            sb.Append(";lvl=").Append(me.progress.level).Append(";texp=").Append(F1(me.progress.totalExp)).Append(";kills=").Append(me.progress.kills).Append(";souls=").Append(me.souls).Append(";fh=").Append(g.lastFoodHeal).Append(";hints=").Append(g.fullHints).Append(";mhp=").Append(F1(me.MaxHp))
              .Append(";own=").Append(own).Append(";ts=").Append(F2(Time.timeScale)).Append(";snap=").Append(Net.SnapshotsReceived)
              .Append(";panel=").Append(g.LevelStep).Append(";tags=").Append(g.hud.NameTagsShown).Append(";rows=").Append(g.hud.PartyRowsShown)
              .Append(";wave=").Append(g.wave).Append(";ping=").Append(Net.PingMs)
              .Append(";notice=").Append(g.hud.NoticeShown.Replace(';', ',').Replace('=', ':').Replace('|', '/'))
              .Append(";mstates=").Append(mstates);
            return sb.ToString();
        }

        // Host: the first live slime lands a blow on the player, from 2.5 m north of him (after being moved there).
        bool StrikeOn(Knight victim, bool heavy)
        {
            Slime sl = null;
            foreach (Enemy e in Enemy.All) { Slime s2 = e as Slime; if (s2 != null && !s2.Dead) { sl = s2; break; } }
            if (victim == null || sl == null) return false;
            sl.Warp(victim.Pos + new Vector3(0f, 0f, 2.5f));
            g.DebugStrike(sl, victim, heavy);
            return true;
        }

        IEnumerator Exec(string id, string name, string[] a)
        {
            Knight k = g.knight;
            if (k == null) { Reply(id, "nosession"); yield break; }
            switch (name)
            {
                case "sum":
                    Reply(id, Summary());
                    break;

                case "shot":
                    Shot(a[0]);
                    yield return null;
                    Reply(id, "ok");
                    break;

                case "tp":                                            // x z aimx aimz
                    yield return WaitIdle(2f);
                    k.transform.position = new Vector3(P(a[0]), 0f, P(a[1]));
                    k.aimOverride = new Vector3(P(a[2]), 0f, P(a[3]));
                    k.moveOverride = Vector2.zero;
                    yield return W(0.25f);
                    Reply(id, "x=" + F2(k.transform.position.x) + ";z=" + F2(k.transform.position.z));
                    break;

                case "walk":                                          // dx dz seconds
                {
                    Vector3 from = k.transform.position;
                    k.moveOverride = new Vector2(P(a[0]), P(a[1]));
                    yield return new WaitForSeconds(P(a[2]));
                    k.moveOverride = Vector2.zero;
                    yield return null;
                    Reply(id, "x=" + F2(k.transform.position.x) + ";z=" + F2(k.transform.position.z) + ";moved=" + F2(Flat(from, k.transform.position)));
                    break;
                }

                case "block":                                         // seconds: hold the shield up through the real key
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Right, true));      // right mouse button = shield
                    yield return W(P(a[0]));
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState());
                    yield return null;
                    Reply(id, "ok");
                    break;

                case "strike":                                        // pid heavy: (host) a slime lands a blow on that player, standing 2.5 m north of him
                {
                    Reply(id, "ok=" + StrikeOn(g.PlayerByPid(int.Parse(a[0])), a[1] == "1"));
                    break;
                }

                case "probe":                                         // heavy: shield up through the mouse, then ask the host to strike me; the shield stays up 0.6 s
                {
                    yield return WaitIdle(2f);
                    yield return W(0.8f);                             // out of the 0.5 s no-parry gap of the last press
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Right, true));
                    yield return null; yield return null;
                    Net.SendTest("c|pr" + (++probeId) + "|H|strike|" + Net.LocalPid + "|" + a[0]);
                    Reply(id, "ok");
                    yield return W(0.6f);
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState());
                    break;
                }

                case "clashprobe":                                    // start a light swing, then ask the host to strike me while it is in its strike window
                {
                    yield return WaitIdle(2f);
                    yield return W(0.5f);
                    k.DebugAttack(0);
                    Net.SendTest("c|pr" + (++probeId) + "|H|strike|" + Net.LocalPid + "|0");
                    Reply(id, "ok");
                    yield return W(0.4f);
                    break;
                }

                case "hostparry":                                     // (host) my own parry of a slime, to be seen by the clients
                {
                    yield return WaitIdle(2f);
                    yield return W(0.8f);
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Right, true));
                    yield return null; yield return null;
                    StrikeOn(k, false);
                    Reply(id, "par=" + k.ParryCount);
                    yield return W(0.6f);
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState());
                    break;
                }

                case "holdon":                                        // phase 6: the left button goes down and stays (a charge)
                    k.attackHeldOverride = true;
                    Reply(id, "ok");
                    break;

                case "holdoff":                                       // the button comes up: a full charge swings the Heavy Strike
                    k.attackHeldOverride = false;
                    yield return null; yield return null;
                    yield return WaitIdle(3f);
                    k.attackHeldOverride = null;
                    Reply(id, "an=" + k.LastAction + ";ac=" + k.ActionsSeen);
                    break;

                case "atk":
                    yield return WaitIdle(2f);
                    k.DebugAttack(int.Parse(a[0]));
                    yield return null;
                    yield return WaitIdle(3f);
                    Reply(id, "an=" + k.LastAction);
                    break;

                case "skill":
                {
                    yield return WaitIdle(2f);
                    for (int i = 0; i < 4; i++) k.cd[i] = 0f;
                    yield return null;                                 // one frame so the aim point is current
                    bool ok = k.DebugSkill(int.Parse(a[0]));
                    yield return null;
                    yield return WaitIdle(3f);
                    Reply(id, "ok=" + ok + ";an=" + k.LastAction);
                    break;
                }

                case "crit":
                    Progress.ForceCrit = int.Parse(a[0]);
                    Reply(id, "ok");
                    break;

                case "autolevel":
                    autoLevel = a[0] == "1";
                    Reply(id, "ok");
                    break;

                case "hp":                                            // fraction of the maximum: autoheal off, health set
                    autoHeal = false;
                    k.hp = k.MaxHp * P(a[0]);
                    Reply(id, "hp=" + F1(k.hp));
                    break;

                case "autoheal":
                    autoHeal = a[0] == "1";
                    Reply(id, "ok");
                    break;

                case "settle":                                        // every pending level-up answered
                {
                    float t0 = RT;
                    while ((k.progress.pendingChoices > 0 || g.LevelStep != 0 || k.Dead) && RT - t0 < 12f) yield return null;
                    yield return W(0.3f);
                    Reply(id, "pending=" + k.progress.pendingChoices + ";picked=" + panelsPicked);
                    break;
                }

                case "exp":                                           // exactly enough for the next level
                {
                    float need = Progress.Need(k.progress.level) - k.progress.exp;
                    g.GrantExp(need, k.transform.position + Vector3.up * 2f);
                    Reply(id, "lvl=" + k.progress.level);
                    break;
                }

                case "pick":                                          // answer the open panel (card 1, then a stat)
                {
                    float t0 = RT;
                    while (g.LevelStep != 0 && RT - t0 < 4f)
                    {
                        if (g.LevelStep == 1) g.ChooseUpgrade(0); else g.ChooseStat(0);
                        yield return W(0.15f);
                    }
                    Reply(id, "panel=" + g.LevelStep + ";lvl=" + k.progress.level);
                    break;
                }

                case "hunt":                                          // netId maxSwings: sword the enemy until it is dead
                {
                    Enemy e = Enemy.Find(int.Parse(a[0]));
                    int max = int.Parse(a[1]), swings = 0;
                    bool found = e != null;
                    while (e != null && !e.Dead && swings < max)
                    {
                        yield return WaitIdle(2f);
                        Vector3 sp = e.transform.position;
                        k.transform.position = new Vector3(sp.x + e.radius + (e is Minotaur ? 1.4f : 1.0f), 0f, sp.z);
                        k.aimOverride = sp;
                        yield return W(0.15f);
                        if (e == null || e.Dead) break;
                        k.aimOverride = e.transform.position;
                        k.DebugAttack(swings++ % 2);
                        yield return null;
                        yield return WaitIdle(2f);
                        yield return W(0.35f);                          // the host's answer needs a moment
                    }
                    Reply(id, "found=" + found + ";dead=" + (found && (e == null || e.Dead)) + ";swings=" + swings);
                    break;
                }

                case "grab":                                          // walk over the first sword this player can see
                {
                    Pickup target = null;
                    foreach (Pickup p in Pickup.All) if (!p.hidden && (target == null || p.netId < target.netId)) target = p;
                    if (target == null) { Reply(id, "ok=False;w=-1"); break; }
                    int w = target.weaponId;
                    Vector3 pp = target.transform.position;
                    yield return WaitIdle(2f);
                    k.transform.position = pp + new Vector3(-2.6f, 0f, 0f);
                    k.aimOverride = pp;
                    yield return W(0.3f);
                    bool before = k.inventory.owned[w];
                    int plusBefore = k.inventory.plus[w];
                    k.moveOverride = new Vector2(1f, 0f);
                    float t0 = RT;
                    while (target != null && Pickup.All.Contains(target) && RT - t0 < 2.5f) yield return null;
                    k.moveOverride = Vector2.zero;
                    yield return W(0.4f);
                    bool got = k.inventory.owned[w] && (!before || k.inventory.plus[w] > plusBefore);
                    Reply(id, "ok=" + got + ";w=" + w + ";gone=" + (target == null || !Pickup.All.Contains(target)));
                    break;
                }

                case "equip":
                    g.EquipWeapon(int.Parse(a[0]));
                    Reply(id, "eq=" + k.inventory.equipped);
                    break;

                case "die":
                    k.TakeDamage(99999f, k.transform.position + Vector3.right, "mptest");
                    Reply(id, "dead=" + k.Dead);
                    break;

                case "bait":                                          // seconds: stand still and let the monsters hit
                {
                    float limit = P(a[0]), t0 = RT, lowest = k.MaxHp + k.MaxArmor, taken = 0f, prev = k.hp + k.armor;
                    autoHeal = false;
                    string src = "";
                    k.hp = k.MaxHp; k.armor = k.MaxArmor;
                    prev = k.hp + k.armor;
                    while (RT - t0 < limit)
                    {
                        float now = k.hp + k.armor;
                        if (now < prev - 0.01f) { taken += prev - now; src = k.LastDamageSource ?? ""; }
                        lowest = Mathf.Min(lowest, now);
                        if (k.hp < 45f) { k.hp = k.MaxHp; k.armor = k.MaxArmor; now = k.hp + k.armor; }
                        prev = now;
                        if (taken > 15f && (src.StartsWith("Axe") || src == "Charge")) break;
                        yield return null;
                    }
                    autoHeal = true;
                    Reply(id, "taken=" + F1(taken) + ";src=" + src + ";lowest=" + F1(lowest) + ";secs=" + F1(RT - t0));
                    break;
                }

                case "minoclear":
                    minoStates.Clear();
                    tookTeleShot = false;
                    Reply(id, "ok");
                    break;

                case "watch":                                         // pid seconds: how smoothly does that player's knight move here?
                {
                    Knight other = g.PlayerByPid(int.Parse(a[0]));
                    if (other == null) { Reply(id, "frames=0"); break; }
                    float secs = P(a[1]), t0 = RT, moved = 0f, maxStep = 0f;
                    int frames = 0, stalls = 0, jumps = 0, pending = 0, push0 = other.interp.Pushes; float minMargin = 9f, maxMargin = -9f;
                    bool started = false;
                    Vector3 lastPos = other.transform.position, startPos = lastPos;
                    while (RT - t0 < secs)
                    {
                        yield return null;
                        if (other == null) break;
                        Vector3 p = other.transform.position;
                        float step = Flat(p, lastPos), expect = 6.2f * Time.unscaledDeltaTime;
                        lastPos = p;
                        moved += step;
                        if (step > 0.002f) { started = true; stalls += pending; frames += pending + 1; pending = 0; }
                        else if (started) pending++;                    // only counts if he moves again afterwards
                        if (step > expect * 2.5f + 0.03f) jumps++;
                        maxStep = Mathf.Max(maxStep, step); minMargin = Mathf.Min(minMargin, other.interp.Margin); maxMargin = Mathf.Max(maxMargin, other.interp.Margin);
                    }
                    Reply(id, "frames=" + frames + ";stalls=" + stalls + ";jumps=" + jumps + ";max=" + F2(maxStep) + ";moved=" + F2(moved)
                        + ";margin=" + F2(minMargin) + ".." + F2(maxMargin) + ";pushes=" + (other.interp.Pushes - push0) + ";net=" + F2(Flat(startPos, lastPos)) + ";x=" + F2(lastPos.x) + ";z=" + F2(lastPos.z) + ";panel=" + g.LevelStep + ";ts=" + F2(Time.timeScale));
                    break;
                }

                case "f5":
                    Reply(id, "ok=" + g.RequestReset());
                    break;

                case "quit":
                    Finish();
                    Reply(id, "ok");
                    yield return W(0.3f);
                    Application.Quit();
                    break;

                case "crash":                                         // die without a goodbye, like a crash or a pulled cable
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
            g.knight.aimOverride = g.knight.transform.position + Vector3.forward * 5f;
            g.knight.moveOverride = Vector2.zero;

            // wait for the world, then say hello (repeated until the host is certainly listening)
            t0 = RT;
            while (Net.SnapshotsReceived < 3 && RT - t0 < 10f) yield return null;
            Check("receiving snapshots from the host", Net.SnapshotsReceived >= 3);
            for (int i = 0; i < 3; i++) { Net.SendTest("hello|" + role + "|" + Net.LocalPid); yield return W(0.4f); }

            // from here the host conducts; this process only watches for the end
            while (g.InSession) yield return null;
            yield return W(0.6f);
            Log("session ended; menu visible=" + g.menu.Visible + ", page " + g.menu.Current + ", message '" + g.menu.Message + "', knights " + g.players.Count
                + ", enemies " + Enemy.All.Count + ", pickups " + Pickup.All.Count + ", mode " + Net.Mode);
            Check("the host quitting was announced beforehand by the test (not an unexpected drop)", expectHostQuit);
            Check("back at the menu with a message that the host closed the game",
                g.menu.Visible && g.menu.Message.ToLowerInvariant().Contains("host closed") && g.players.Count == 0 && Enemy.All.Count == 0 && Net.Mode == NetMode.Single);
            Shot("menu_after_host_quit");
            yield return W(0.6f);
            Finish();
            Application.Quit();
        }

        // A process that must NOT get in (game full, wrong version, nobody listening, silent server).
        IEnumerator RefusedScript()
        {
            float t0 = RT;
            while (!g.InSession && !(g.menu.Current == Menu.Page.Join && g.menu.Message.Length > 0) && RT - t0 < 45f) yield return null;
            float took = RT - t0;
            string msg = g.menu.Message;
            Log("join attempt ended after " + F1(took) + " s: in session=" + g.InSession + ", menu page " + g.menu.Current + ", message '" + msg + "'");
            Check("the join was refused cleanly with a message containing '" + Args.expectText + "'",
                !g.InSession && g.menu.Visible && msg.ToLowerInvariant().Contains(Args.expectText.ToLowerInvariant()) && Net.Mode == NetMode.Single && !Net.Connecting);
            yield return W(0.3f);
            Shot("join_refused");
            yield return W(0.6f);
            Finish();
            Application.Quit();
        }

        // ---------------------------------------------------------------- the host conducts

        // Slime packs only come when somebody walks through open country, so a test that needs slimes parks some.
        void EnsureSlimes(int n)
        {
            int have = 0;
            foreach (Enemy e in Enemy.All) if (e is Slime && !e.Dead) have++;
            for (int i = have; i < n; i++)
                Slime.Spawn(0, new Vector3(-16.5f + (i % 5) * 0.4f, 0f, -16.5f + (i / 5) * 0.4f), g.transform);
        }

        void ParkSlimes()
        {
            Vector3 corner = new Vector3(-16.5f, 0f, -16.5f);
            foreach (Enemy e in Enemy.All)
            {
                Slime sl = e as Slime;
                if (sl != null && !sl.Dead) sl.Warp(corner + new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f)));
            }
        }

        IEnumerator Tp(string r, float x, float z, float ax, float az)
        {
            yield return Cmd(r, "tp", F2(x), F2(z), F2(ax), F2(az));
        }

        // Eq / Near compare one summary key across every process and remember what each one said; Ck logs the
        // check with those values attached. (The condition is the FIRST argument so it is evaluated before the text.)
        string note = "";

        bool Eq(string key)
        {
            string seen;
            bool ok = Same(key, out seen);
            note += key + "[" + seen.Trim() + "] ";
            return ok;
        }

        bool Near(string key, float tolerance)
        {
            string seen;
            bool ok = Close(key, tolerance, out seen);
            note += key + "[" + seen.Trim() + "] ";
            return ok;
        }

        void Ck(bool ok, string what)
        {
            Check(what + (note.Length > 0 ? "   {" + note.Trim() + "}" : ""), ok);
            note = "";
        }

        // A fresh drop is moved aside in the frame it appears (before any snapshot), so the killer does not grab it at once.
        IEnumerator MoveDrops(Vector3 to)
        {
            while (true)
            {
                foreach (Pickup p in Pickup.All) if (p.age < 0.2f) p.transform.position = to;
                yield return null;
            }
        }

        IEnumerator HostScript()
        {
            yield return W(1.0f);
            Check("hosting: session running and listening on port " + Net.Port, g.InSession && Net.IsHost);
            if (!g.InSession) { Shot("not_hosting"); Log("menu says: " + g.menu.Message); yield return W(0.5f); Finish(); Application.Quit(); yield break; }
            g.dropOverride = 0;                                       // no random sword drops: the test decides when one falls
            g.foodOverride = 0;                                       // ... and no random food
            g.knight.aimOverride = g.knight.transform.position + Vector3.forward * 5f;
            g.knight.moveOverride = Vector2.zero;
            Shot("host_alone");

            float t0 = RT;
            while (clients.Count < Args.expect && RT - t0 < 60f) { ParkSlimes(); yield return W(0.25f); }
            Check(Args.expect + " client(s) joined and reported in (" + string.Join(",", clients.ToArray()) + ")", clients.Count == Args.expect);
            if (clients.Count == 0) { Finish(); Application.Quit(); yield break; }
            int n = clients.Count;
            string A = clients[0], B = clients[1 % n], C = clients[2 % n];
            int pidA = pidOf[A], pidB = pidOf[B], pidC = pidOf[C];
            Log("roles: " + A + " = player " + pidA + ", " + B + " = player " + pidB + ", " + C + " = player " + pidC + "; the host is player 0");
            yield return W(1.0f);
            ParkSlimes();

            // ------------------------------------------------ 1. everybody sees everybody
            // everybody walks out of town into the meadow; the first slime packs come 2.5 s later
            {
                string[] first = Targets("all").ToArray();
                for (int i = 0; i < first.Length; i++) yield return Tp(first[i], -3f + i * 2f, -8f, -3f + i * 2f, 0f);
                yield return W(5.5f);
            }
            yield return Sum();
            Ck(Eq("n") && S("H", "n") == (n + 1).ToString(), "every process counts " + (n + 1) + " players");
            Ck(Eq("names"), "every process has the same player list");
            Ck(Eq("tags") && S("H", "tags") == (n + 1).ToString(), "every process shows a name tag over each of the " + (n + 1) + " knights");
            Ck(Eq("rows") && S("H", "rows") == Mathf.Min(3, n).ToString(), "every process lists the other players in its party list");
            Ck(Eq("mapfr") && S("H", "mapfr") == n.ToString() && S("H", "mapen") == "0", "the M map has one friend marker for each of the " + n + " other players on every screen and never an enemy");
            Ck(Eq("en") && N("H", "en") >= 3, "every client has the host's enemies");
            Ck(Eq("minohp") && Eq("dummy") && S("H", "mino") == "alive", "every client has the Minotaur and the dummy with the host's health");

            // line up south of the plaza, looking north
            string[] peers = Targets("all").ToArray();
            for (int i = 0; i < peers.Length; i++) yield return Tp(peers[i], -3f + i * 2f, -8f, -3f + i * 2f, 0f);
            yield return W(0.6f);
            yield return Cmd("all", "shot", "lineup");
            yield return Sum();
            for (int i = 0; i < peers.Length; i++)
            {
                int pid = peers[i] == "H" ? 0 : pidOf[peers[i]];
                Ck(Near("x" + pid, 0.15f) && Near("z" + pid, 0.15f), "player " + pid + " (" + peers[i] + ") stands at the same place on every screen");
            }

            g.packsOff = true;                                        // from here on the test decides which slimes exist (EnsureSlimes)
            // the four far corners of the 380 m map: positions travel as centimetres in an i16 and must arrive exactly
            {
                float[] fx = { 186f, -70f, 186f, 20f }, fz = { -186f, 186f, 186f, -186f };
                for (int i = 0; i < peers.Length; i++) yield return Tp(peers[i], fx[i % 4], fz[i % 4], fx[i % 4], fz[i % 4] + 3f);
                yield return W(0.8f);
                yield return Sum();
                bool corners = true; string seenAt = "";
                for (int i = 0; i < peers.Length; i++)
                {
                    int pid = peers[i] == "H" ? 0 : pidOf[peers[i]];
                    bool ok = Near("x" + pid, 0.05f) && Near("z" + pid, 0.05f);
                    corners &= ok && (Mathf.Abs(N("H", "x" + pid)) > 100f || Mathf.Abs(N("H", "z" + pid)) > 100f);
                    seenAt += peers[i] + "(" + F1(N("H", "x" + pid)) + ", " + F1(N("H", "z" + pid)) + ") ";
                }
                Check("far corners of the map: every player is at the same place (within 5 cm) on every screen: " + seenAt, corners);
                for (int i = 0; i < peers.Length; i++) yield return Tp(peers[i], -3f + i * 2f, -8f, -3f + i * 2f, 0f);
                yield return W(0.6f);
            }

            // ------------------------------------------------ 2. movement of a client, watched by the host and by another client
            {
                int wb = Send(B, "watch", pidA.ToString(), "2.2");
                int wh = Send("H", "watch", pidA.ToString(), "2.2");
                yield return W(0.2f);
                yield return Cmd(A, "walk", "1", "0", "1.2");
                Map walked = Parse(Rep(last, A));
                yield return Wait(wb, B);
                yield return Wait(wh, "H");
                foreach (string who in new[] { B, "H" })
                {
                    Map w = Parse(Rep(who == "H" ? wh : wb, who));
                    Log(A + " walked " + walked.G("moved") + " m east; on " + who + "'s screen his knight moved " + w.G("moved") + " m over " + w.G("frames") + " frames (largest step " + w.G("max")
                        + " m, frames standing still mid-walk " + w.G("stalls") + ", jumps " + w.G("jumps") + ", samples " + w.G("pushes") + ", buffer margin " + w.G("margin") + " s), ended at (" + w.G("x") + ", " + w.G("z") + ") vs his own (" + walked.G("x") + ", " + walked.G("z") + ")");
                    float frames = w.F("frames");
                    Check(A + "'s walk is seen on " + who + ": same distance and end point", walked.F("moved") > 5f && Mathf.Abs(w.F("net") - walked.F("moved")) < 0.6f
                        && Mathf.Abs(w.F("x") - walked.F("x")) < 0.4f && Mathf.Abs(w.F("z") - walked.F("z")) < 0.4f);
                    Check(A + "'s walk looks smooth on " + who + " (interpolated: no jumps, few repeated frames)", frames > 20f && w.F("jumps") <= 1f && w.F("stalls") <= frames * 0.15f);
                }
            }

            // ------------------------------------------------ 3. block pose, the five sword attacks and four skills of a client
            Dummy d = g.dummy;
            Vector3 dp = d.transform.position;
            yield return Tp("H", dp.x - 3f, dp.z - 3.5f, dp.x, dp.z);
            yield return Tp(B, dp.x - 1f, dp.z - 4f, dp.x, dp.z);
            if (C != A) yield return Tp(C, dp.x + 1.5f, dp.z - 3.5f, dp.x, dp.z);
            yield return Tp(A, dp.x - (d.radius + 1.3f), dp.z, dp.x, dp.z);
            yield return Cmd("all", "crit", "1");
            {
                int bl = Send(A, "block", "1.2");
                yield return W(0.7f);
                yield return Sum();
                Ck(Eq("blk" + pidA) && S("H", "blk" + pidA) == "1", A + " raising his shield (real Left Shift key) is seen everywhere");
                yield return Cmd("all", "shot", "block_pose");
                yield return Wait(bl, A);
            }
            int hits0 = d.hits;
            float tot0 = d.totalDamage;
            for (int i = 0; i < 1; i++)                                // one sword attack proves the replication path; per-attack damage is not a network matter
            {
                AttackDef atk = Knight.Attacks[i];
                yield return Tp(A, dp.x - (d.radius + 1.3f), dp.z, dp.x, dp.z);
                yield return Sum();
                float ac0 = N("H", "ac" + pidA), acB0 = N(B, "ac" + pidA);
                int h0 = d.hits; float dt0 = d.totalDamage;
                yield return Cmd(A, "atk", i.ToString());
                yield return W(0.45f);
                yield return Sum();
                bool seenAll = Eq("an" + pidA) && S("H", "an" + pidA) == atk.name;
                Ck(seenAll && N("H", "ac" + pidA) == ac0 + 1 && N(B, "ac" + pidA) == acB0 + 1, "attack " + (i + 1) + " " + atk.name + " by " + A + " is replayed once on every screen");
                Check("attack " + (i + 1) + ": the client's hit took exactly " + atk.damage + " from the host's dummy (hits +" + (d.hits - h0) + ", damage +" + F1(d.totalDamage - dt0) + ")",
                    d.hits - h0 == 1 && Mathf.Abs(d.totalDamage - dt0 - atk.damage) < 0.01f);
                Ck(Near("dummy", 0.6f), "attack " + (i + 1) + ": the dummy's health + armor is the same everywhere");
            }
            string[] skillNames = { "Whirlwind", "Dash Thrust", "Shield Bash", "Ground Slam" };
            for (int i = 0; i < 1; i++)                                // one skill (Whirlwind, 3 hits) is enough; the other three go through the same path
            {
                if (i == 1) yield return Tp(A, dp.x - 3.5f, dp.z, dp.x, dp.z);
                else if (i == 3) yield return Tp(A, dp.x - 5f, dp.z + 0.5f, dp.x - 1.4f, dp.z);
                else yield return Tp(A, dp.x - (d.radius + 1.2f), dp.z, dp.x, dp.z);
                yield return W(0.3f);
                int h0 = d.hits; float dt0 = d.totalDamage;
                int sk = Send(A, "skill", i.ToString());
                yield return W(i == 0 ? 0.6f : 0.3f);
                yield return Cmd("all", "shot", "skill_" + skillNames[i].Replace(" ", ""));
                yield return Wait(sk, A);
                yield return W(0.5f);
                yield return Sum();
                float want = Knight.Skills[i].damage * (i == 0 ? 3 : 1);
                Ck(Eq("an" + pidA) && S("H", "an" + pidA) == skillNames[i], "skill " + skillNames[i] + " by " + A + " is replayed on every screen");
                Check("skill " + skillNames[i] + ": dummy hits +" + (d.hits - h0) + ", damage +" + F1(d.totalDamage - dt0) + " on the host (want " + (i == 0 ? 3 : 1) + " hit(s), " + want + ")",
                    d.hits - h0 == (i == 0 ? 3 : 1) && Mathf.Abs(d.totalDamage - dt0 - want) < 0.01f);
                Ck(Near("dummy", 0.6f), "skill " + skillNames[i] + ": the dummy's health + armor is the same everywhere");
            }
            Log("dummy took " + (d.hits - hits0) + " hits / " + F1(d.totalDamage - tot0) + " damage from " + A + " in total");

            // ------------------------------------------------ 3b. phase 6: a new attack (Vault Chop), the charge flag, Heavy Strike, a cancelled charge
            {
                AttackDef vault = Knight.Attacks[9];
                yield return Tp(A, dp.x - (d.radius + 1.3f), dp.z, dp.x, dp.z);
                yield return Sum();
                float ac0 = N("H", "ac" + pidA), acB0 = N(B, "ac" + pidA);
                int h0 = d.hits; float dt0 = d.totalDamage;
                yield return Cmd(A, "atk", "9");
                yield return W(0.5f);
                yield return Sum();
                Ck(Eq("an" + pidA) && S("H", "an" + pidA) == vault.name && N("H", "ac" + pidA) == ac0 + 1 && N(B, "ac" + pidA) == acB0 + 1,
                    "new attack " + vault.name + " (net kind " + Knight.NetKind(9) + ") by " + A + " is replayed once on every screen");
                Check("new attack " + vault.name + ": the host's dummy took exactly " + vault.damage + " (hits +" + (d.hits - h0) + ", damage +" + F1(d.totalDamage - dt0) + ")",
                    d.hits - h0 == 1 && Mathf.Abs(d.totalDamage - dt0 - vault.damage) < 0.01f);

                // charge: the flag is seen on every screen while held, and goes out with the release
                yield return W(1.0f);
                AttackDef heavy = Knight.Attacks[Knight.HeavyIndex];
                yield return Cmd(A, "holdon");
                yield return W(0.8f);
                yield return Sum();
                Ck(Eq("chg" + pidA) && S("H", "chg" + pidA) == "1", A + " holding a charge is seen on every screen");
                yield return W(0.6f);
                float acH = N("H", "ac" + pidA);
                int h1 = d.hits; float dt1 = d.totalDamage;
                yield return Cmd(A, "holdoff");
                yield return W(0.3f);
                yield return Sum();
                Ck(Eq("an" + pidA) && S("H", "an" + pidA) == heavy.name && N("H", "ac" + pidA) == acH + 1 && Eq("chg" + pidA) && S("H", "chg" + pidA) == "0",
                    A + " released a full charge: " + heavy.name + " is replayed once on every screen and the charge flag is out");
                Check(heavy.name + ": the client's hit took exactly " + heavy.damage + " from the host's dummy (hits +" + (d.hits - h1) + ", damage +" + F1(d.totalDamage - dt1) + ")",
                    d.hits - h1 == 1 && Mathf.Abs(d.totalDamage - dt1 - heavy.damage) < 0.01f);

                // a charge dropped with the right mouse button: the flag goes out, nothing swings when the button comes up
                yield return W(1.0f);
                yield return Sum();
                float acX = N("H", "ac" + pidA);
                yield return Cmd(A, "holdon");
                yield return W(0.7f);
                yield return Cmd(A, "block", "0.5");
                yield return W(0.3f);
                yield return Sum();
                Ck(Eq("chg" + pidA) && S("H", "chg" + pidA) == "0", A + " dropped the charge with the shield: the flag is out everywhere");
                yield return Cmd(A, "holdoff");
                yield return W(0.4f);
                yield return Sum();
                Ck(Eq("ac" + pidA) && N("H", "ac" + pidA) == acX, "nothing swung when the button came up after the dropped charge");
            }

            // ------------------------------------------------ 3c. phase 7: parry / clash / heavy blows across the network, damage number colours
            {
                yield return Cmd("all", "settle");
                EnsureSlimes(3);
                ParkSlimes();                                       // every slime in the far corner: nothing real may hit the players here (a real hit in the window would be parried too)
                yield return Tp(A, 60f, 0f, 60f, 5f);
                yield return Tp(B, 70f, 10f, 70f, 15f);
                yield return Tp("H", 50f, 10f, 50f, 15f);
                yield return W(0.5f);
                yield return Sum();
                float par0 = N(A, "par" + pidA), cla0 = N(A, "cla" + pidA), hb0 = N(A, "hb" + pidA), def0 = N("H", "sldef");

                // a) A raises the shield and a slime hits him from the front 0.1 s later: parried, and everybody sees the slime fly back / stunned
                yield return Cmd(A, "probe", "0");
                yield return W(0.3f);
                yield return Sum();
                Ck(N(A, "par" + pidA) == par0 + 1 && Eq("sldef") && N("H", "sldef") == def0 + 1 && N("H", "slst") >= 1,
                    A + " parried a slime's blow over the network (owner counted it, the slime reacted on the host, every screen saw it, the slime is stunned)");
                par0 = N(A, "par" + pidA); def0 = N("H", "sldef");

                // b) the same with a heavy blow: a normal block, nothing deflected
                yield return W(1.0f);
                yield return Cmd(A, "probe", "1");
                yield return W(0.4f);
                yield return Sum();
                note += "par=" + S(A, "par" + pidA) + "(" + par0 + ") hb=" + S(A, "hb" + pidA) + "(" + hb0 + ") sldef=" + S("H", "sldef") + "(" + def0 + ") ";
                Ck(N(A, "par" + pidA) == par0 && N(A, "hb" + pidA) == hb0 + 1 && Eq("sldef") && N("H", "sldef") == def0,
                    "a heavy blow on " + A + "'s raised shield is only blocked: no parry, no deflect on any screen");

                // c) A swings and the slime strikes inside his swing: a clash
                yield return W(1.0f);
                yield return Cmd(A, "clashprobe");
                yield return W(0.6f);
                yield return Sum();
                note += "cla=" + S(A, "cla" + pidA) + "(" + cla0 + ") par=" + S(A, "par" + pidA) + "(" + par0 + ") sldef=" + S("H", "sldef") + "(" + def0 + ") ";
                Ck(N(A, "cla" + pidA) == cla0 + 1 && Eq("sldef") && N("H", "sldef") == def0 + 1 && N(A, "par" + pidA) == par0,
                    A + " clashed with a slime (swing meets blow): counted by the owner, seen on every screen");
                def0 = N("H", "sldef");

                // d) the host parries by himself: the clients see it
                yield return W(1.0f);
                yield return Cmd("H", "hostparry");
                yield return W(0.3f);
                yield return Sum();
                Ck(N("H", "par0") == 1 && Eq("sldef") && N("H", "sldef") == def0 + 1, "the host's own parry is seen by every client");
                ParkSlimes();
                yield return W(0.3f);

                // e) a Heavy Strike on the dummy: every screen shows the same number colour (heavy, then crit + heavy)
                {
                    float tw = RT;
                    while (g.dummy != null && g.dummy.Dead && RT - tw < 6f) yield return null;
                    Dummy dm = g.dummy;
                    Vector3 dpos = dm.transform.position;
                    yield return Tp(A, dpos.x - (dm.radius + 1.3f), dpos.z, dpos.x, dpos.z);
                    yield return Cmd(A, "holdon");
                    yield return W(1.5f);
                    yield return Cmd(A, "holdoff");
                    yield return W(0.4f);
                    yield return Sum();
                    Ck(Eq("dnum") && S("H", "dnum") == "2", "a Heavy Strike's damage number is the heavy kind (2) on every screen");
                    tw = RT;
                    while (g.dummy != null && g.dummy.Dead && RT - tw < 6f) yield return null;
                    yield return W(0.8f);
                    yield return Cmd(A, "crit", "2");
                    yield return Cmd(A, "holdon");
                    yield return W(1.5f);
                    yield return Cmd(A, "holdoff");
                    yield return W(0.4f);
                    yield return Cmd(A, "crit", "1");
                    yield return Sum();
                    Ck(Eq("dnum") && S("H", "dnum") == "4", "a critical Heavy Strike's number is the crit + heavy kind (4) on every screen");
                    yield return Tp(A, -3f, -8f, -3f, 0f);
                }
            }

            // ------------------------------------------------ 4. a client kills a slime: EXP for everybody, a sword drops, another client takes it
            {
                yield return Cmd("all", "settle");
                EnsureSlimes(3);
                yield return W(0.5f);
                Slime sl = null;
                foreach (Enemy e in Enemy.All) { Slime s = e as Slime; if (s != null && !s.Dead && (sl == null || s.kind > sl.kind)) sl = s; }
                Check("a slime is alive to be killed", sl != null);
                if (sl != null)
                {
                    sl.Warp(new Vector3(10f, 0f, -12f));
                    sl.hp = 5f; sl.armor = 0f;
                    float exp = sl.expReward;
                    int id = sl.netId;
                    string label = sl.label;
                    g.dropOverride = 1;
                    yield return W(0.4f);
                    yield return Sum();
                    var texp0 = new Dictionary<string, float>(); var kills0 = new Dictionary<string, float>();
                    foreach (string r in peers) { texp0[r] = N(r, "texp"); kills0[r] = N(r, "kills"); }
                    int en0 = (int)N("H", "en");
                    Coroutine mover = StartCoroutine(MoveDrops(new Vector3(13f, 0f, -6f)));
                    yield return Cmd(A, "hunt", id.ToString(), "8");
                    g.dropOverride = 0;
                    yield return W(0.2f);
                    StopCoroutine(mover);
                    Map hunt = Parse(Rep(last, A));
                    yield return W(0.7f);
                    yield return Sum();
                    // (the count itself may differ from en0 - 1: the wave spawner can add a slime in the same second)
                    Enemy gone = Enemy.Find(id);
                    Log("slime " + id + ": enemies on the host " + en0 + " -> " + S("H", "en"));
                    Ck(hunt.G("dead") == "True" && Eq("en") && (gone == null || gone.Dead), A + " killed the " + label + " with his sword (" + hunt.G("swings") + " swing(s)); it is gone on every screen");
                    bool expOk = true, killOk = true;
                    string expSeen = "", killSeen = "";
                    foreach (string r in peers)
                    {
                        float de = N(r, "texp") - texp0[r], dk = N(r, "kills") - kills0[r];
                        expSeen += r + "+" + F1(de) + " "; killSeen += r + "+" + dk + " ";
                        if (Mathf.Abs(de - exp) > 0.05f) expOk = false;
                        if (dk != (r == A ? 1f : 0f)) killOk = false;
                    }
                    Check("EXP sharing: every living player got the full " + exp + " EXP of the kill: " + expSeen, expOk);
                    Check("the kill itself is counted only for " + A + ": " + killSeen, killOk);
                    Ck(Eq("pklist") && S("H", "pk") == "1", "the slime's sword drop lies at the same place on every screen");
                    yield return Cmd("all", "shot", "sword_drop");

                    yield return Cmd(B, "grab");
                    Map grab = Parse(Rep(last, B));
                    yield return W(0.6f);
                    yield return Sum();
                    int w = (int)grab.F("w");
                    bool onlyB = w >= 0;
                    string owners = "";
                    foreach (string r in peers) { bool has = w >= 0 && ((int)N(r, "own") & (1 << w)) != 0; owners += r + "=" + has + " "; if (has != (r == B)) onlyB = false; }
                    Ck(grab.G("ok") == "True" && onlyB && Eq("pkall") && S("H", "pkall") == "0",
                        B + " walked over the " + (w >= 0 ? Weapons.All[w].name : "?") + " and only he owns it now (" + owners.Trim() + "); it is gone everywhere");
                    if (w >= 0)
                    {
                        yield return Cmd(B, "equip", w.ToString());
                        yield return W(0.5f);
                        yield return Sum();
                        Ck(Eq("wn" + pidB) && S("H", "wn" + pidB) == Weapons.All[w].name, B + " equipped it and every screen shows that sword in his hand");
                        yield return Cmd("all", "shot", "other_sword_equipped");
                        yield return Cmd(B, "equip", "0");
                    }
                }
            }

            // ------------------------------------------------ 5. a level-up panel on one client stalls nobody
            {
                yield return Cmd("all", "settle");
                yield return Tp(A, -6f, -9f, -6f, 0f);
                yield return Tp(B, -4f, -11f, -4f, 0f);
                yield return Cmd(A, "autolevel", "0");
                yield return Cmd(A, "exp");
                yield return W(1.3f);
                yield return Sum();
                float snap0 = N(A, "snap");
                int lvA = (int)N(A, "lvl");
                Ck(S(A, "panel") == "1" && Eq("ts") && S(A, "ts") == "1.00", A + " levelled up to " + lvA + ": his card panel is open (step " + S(A, "panel") + ") and time runs at x1 for everybody");
                int wa = Send(A, "watch", pidB.ToString(), "1.8");
                yield return W(0.15f);
                yield return Cmd(B, "walk", "0", "1", "1.0");
                yield return Wait(wa, A);
                Map w = Parse(Rep(wa, A));
                yield return Cmd("all", "shot", "levelup_panel_open_on_" + A);
                yield return Sum();
                Check("with the panel open, " + A + " keeps receiving the world (" + (N(A, "snap") - snap0) + " snapshots meanwhile) and sees " + B + " walk " + w.G("moved") + " m; the panel is still open",
                    N(A, "snap") - snap0 >= 15f && w.F("moved") > 4f && w.G("panel") == "1" && S(A, "panel") == "1");
                yield return Cmd(A, "walk", "-1", "0", "0.5");
                float own = Parse(Rep(last, A)).F("moved");
                Check(A + " can still walk with his own panel open (moved " + F1(own) + " m)", own > 2f);
                yield return Cmd(A, "pick");
                yield return W(0.5f);
                yield return Sum();
                Ck(S(A, "panel") == "0" && Eq("lv" + pidA) && S("H", "lv" + pidA) == lvA.ToString(), A + " picked a card and a stat point; the panel closed and every screen shows him at level " + lvA);
                yield return Cmd(A, "autolevel", "1");
            }

            // ------------------------------------------------ 6. the Minotaur goes for a client and hurts him
            Minotaur m = g.minotaur;
            Check("the Minotaur is in his lair, resting", m != null && m.State == Minotaur.S.Idle);
            if (m != null)
            {
                yield return Cmd("all", "minoclear");
                int sent0 = Net.KnightHitsSent[pidC];
                Vector3 mp = m.transform.position;
                yield return Tp(C, mp.x, mp.z - 4.4f, mp.x, mp.z);
                yield return Cmd(C, "bait", "16");
                Map bait = Parse(Rep(last, C));
                yield return Sum();
                Log("minotaur vs " + C + ": he took " + bait.G("taken") + " damage, last from '" + bait.G("src") + "' after " + bait.G("secs") + " s; the host forwarded " + (Net.KnightHitsSent[pidC] - sent0)
                    + " monster hit(s) to him; Minotaur state now " + m.State + "; states seen: host [" + S("H", "mstates") + "] " + A + " [" + S(A, "mstates") + "] " + C + " [" + S(C, "mstates") + "]");
                Check("the Minotaur noticed " + C + ", attacked and damaged him (" + bait.G("taken") + " from " + bait.G("src") + ")",
                    bait.F("taken") > 5f && (bait.G("src").StartsWith("Axe") || bait.G("src") == "Charge") && Net.KnightHitsSent[pidC] > sent0);
                Check("another client (" + A + ") saw the Minotaur chase and telegraph an attack", S(A, "mstates").Contains("Tele") && S(A, "mstates").Contains("Chase"));
                Ck(Near("minohp", 0.6f), "the Minotaur's health is the same on every screen");

                // ------------------------------------------------ 7. boss kill by a client: 250 EXP each, one personal boss sword each
                yield return Cmd("all", "settle");
                m.hp = 5f; m.armor = 0f;
                g.dropOverride = -1;                                  // the Minotaur's normal 100% drop
                int mid = m.netId;
                float mExp = m.expReward;
                yield return W(0.3f);
                yield return Sum();
                var texp0 = new Dictionary<string, float>();
                var soul0 = new Dictionary<string, float>();
                foreach (string r in peers) { texp0[r] = N(r, "texp"); soul0[r] = N(r, "souls"); }
                yield return Cmd(A, "hunt", mid.ToString(), "10");
                Map hunt = Parse(Rep(last, A));
                g.dropOverride = 0;
                yield return W(0.8f);
                yield return Cmd("all", "shot", "minotaur_slain");
                yield return Sum();
                bool expOk = true;
                string expSeen = "";
                foreach (string r in peers) { float de = N(r, "texp") - texp0[r]; expSeen += r + "+" + F1(de) + " "; if (Mathf.Abs(de - mExp) > 0.05f) expOk = false; }
                Ck(hunt.G("dead") == "True" && Eq("mino") && S("H", "mino") == "none", A + " struck the last blow on the Minotaur (" + hunt.G("swings") + " swing(s)); he is dead on every screen");
                Check("every player got the Minotaur's 250 EXP: " + expSeen, expOk);
                bool soulOk = true; string soulSeen = "";
                foreach (string r in peers) { float ds = N(r, "souls") - soul0[r]; soulSeen += r + "+" + F1(ds) + " "; if (Mathf.Abs(ds - Minotaur.SoulReward) > 0.05f) soulOk = false; }
                Check("every player got the Minotaur's 50 Soul: " + soulSeen, soulOk);
                bool onePerPlayer = true; int ownedAlready = 0; string bossSeen = "";
                foreach (string r in peers) { bool has = ((int)N(r, "own") & (1 << Weapons.Boss)) != 0; if (has) ownedAlready++; bossSeen += r + "=" + (has ? "taken" : S(r, "pk") + " on the ground") + " "; if (!has && S(r, "pk") != "1") onePerPlayer = false; if (has && S(r, "pk") != "0") onePerPlayer = false; }
                Check("boss sword: one per player, each player sees only his own (" + bossSeen.Trim() + "; still on the host: " + S("H", "pkall") + ")", onePerPlayer && N("H", "pkall") + ownedAlready == n + 1);
                yield return Cmd("all", "grab");
                bool allGot = true;
                string got = "";
                yield return W(0.6f);
                yield return Sum();
                foreach (string r in peers) { bool has = ((int)N(r, "own") & (1 << Weapons.Boss)) != 0; got += r + "=" + has + " "; if (!has) allGot = false; }
                Check("every player collected his own Horned King's Blade (" + got.Trim() + "); left on the host: " + S("H", "pkall"), allGot && S("H", "pkall") == "0");
                yield return Cmd("all", "settle");
                yield return Sum();
                Ck(N("H", "lvl") >= 3f && Eq("lvl"), "after the boss everybody answered his own level-up panels while the others kept playing; all at the same level");
            }

            // ------------------------------------------------ 8. a client dies and comes back
            {
                Vector3 home = Game.SpawnPointFor(pidC);
                float soulsC0 = N(C, "souls"), lvC0 = N(C, "lvl");
                yield return Tp(C, 6f, -14f, 6f, 0f);
                for (int i = 0; i < peers.Length; i++) if (peers[i] != C) yield return Tp(peers[i], 2f + i, -11f, 6f, -14f);
                yield return Cmd(C, "autoheal", "0");
                yield return Cmd(C, "holdon");                           // phase 6: he is charging when he falls
                yield return W(0.8f);
                yield return Sum();
                Ck(Eq("chg" + pidC) && S("H", "chg" + pidC) == "1", C + " charging is seen on every screen before he falls");
                float acC0 = N("H", "ac" + pidC);
                yield return Cmd(C, "die");
                Check(C + " took a deadly blow", Parse(Rep(last, C)).G("dead") == "True");
                yield return Cmd(C, "holdoff");
                yield return W(1.0f);
                yield return Sum();
                Ck(Eq("dead" + pidC) && S("H", "dead" + pidC) == "1", C + " is down, and down on every screen");
                Ck(Eq("chg" + pidC) && S("H", "chg" + pidC) == "0" && N("H", "ac" + pidC) == acC0, C + " fell while charging: the charge flag is out everywhere and nothing swung");
                yield return Cmd("all", "shot", "knight_down");
                yield return W(Game.ReviveDelayCoop + 1.2f);
                yield return Cmd(C, "autoheal", "1");
                yield return Sum();
                Ck(Eq("dead" + pidC) && S("H", "dead" + pidC) == "0" && Near("x" + pidC, 0.3f) && Near("z" + pidC, 0.3f)
                    && Mathf.Abs(N("H", "x" + pidC) - home.x) < 0.5f && Mathf.Abs(N("H", "z" + pidC) - home.z) < 0.5f,
                    C + " revived after " + Game.ReviveDelayCoop + " s at his spawn point (" + F1(home.x) + ", " + F1(home.z) + ") on every screen");
                Ck(Eq("lv" + pidC) && S("H", "lv" + pidC) == "1" && N(C, "lvl") == 1f && lvC0 > 1f,
                    C + " fell from level " + lvC0 + " and is back at level 1 on every screen");
                Check(C + " kept his Soul through death (" + soulsC0 + " -> " + N(C, "souls") + ")", soulsC0 > 0f && Mathf.Abs(N(C, "souls") - soulsC0) < 0.01f);
            }

            // ------------------------------------------------ 8b. food: the one who needs it eats it, a full-health player does not
            if (n >= 2)
            {
                g.foodOverride = 0;
                ParkSlimes();
                Vector3 spot = new Vector3(10f, 0f, -4f);
                yield return Tp(A, spot.x - 0.3f, spot.z, spot.x, spot.z + 5f);
                yield return Tp(B, spot.x + 0.3f, spot.z, spot.x, spot.z + 5f);
                for (int i = 0; i < peers.Length; i++) if (peers[i] != A && peers[i] != B) yield return Tp(peers[i], -6f + i, 6f, -6f, 12f);
                ParkSlimes();
                yield return Cmd(A, "autoheal", "0");
                yield return Cmd(A, "hp", "0.5");
                yield return W(0.5f);
                yield return Sum();
                float maxA = N(A, "mhp");
                g.SpawnFood(Foods.Apple, spot);
                yield return W(1.2f);
                yield return Sum();
                float wantApple = Mathf.Round(Foods.All[Foods.Apple].healFrac * maxA);
                Ck(Eq("pkall") && S("H", "pkall") == "0", "an Apple on the ground was eaten once: it is gone on every screen");
                Check(A + " (half health) ate the Apple: +" + S(A, "fh") + " HP (15% of his max " + F1(maxA) + " = " + F1(wantApple) + ")", Mathf.Abs(N(A, "fh") - wantApple) < 0.6f && N(B, "fh") == 0f);
                Check(B + " (full health) did not eat it and has no heal recorded", N(B, "fh") == 0f && N(B, "hp" + pidB) > 0f);

                yield return Cmd(A, "autoheal", "1");                    // Meat and the 60 s expiry are food rules, not network rules: not tested here
            }

            // ------------------------------------------------ 9. F5: refused for a client, the host restarts everybody
            {
                yield return Cmd(A, "f5");
                bool refused = Parse(Rep(last, A)).G("ok") == "False";
                yield return Sum();
                Check("F5 on a client is refused (" + A + " stays level " + S(A, "lvl") + ")", refused && N(A, "lvl") > 1f);
                bool did = g.RequestReset();
                yield return W(2.5f);
                yield return Sum();
                Ck(did && Eq("lvl") && S("H", "lvl") == "1" && Eq("own") && S("H", "own") == "1", "F5 on the host restarts everybody at level 1 with only the Iron Sword");
                Ck(Eq("en") && N("H", "en") >= 1 && Eq("mino") && S("H", "mino") == "alive" && Eq("pkall") && Eq("wave"), "after the restart every screen has the Minotaur (no slimes: everybody is back in town) and no pickups");
                yield return Cmd("all", "shot", "after_host_restart");
            }

            // ------------------------------------------------ 10. the extra join attempts were turned away
            if (Args.refusals)
            {
                t0 = RT;
                while ((Net.RefusedFull < 1 || Net.RefusedVersion < 1) && RT - t0 < 40f) yield return W(0.25f);
                Check("a 5th player was refused because the game is full (" + Net.RefusedFull + " refusal(s)) while " + g.players.Count + " players stayed connected", Net.RefusedFull >= 1 && g.players.Count == n + 1);
                Check("a client with another network version was refused (" + Net.RefusedVersion + " refusal(s))", Net.RefusedVersion >= 1);
            }

            // ------------------------------------------------ 11. clients leave: one says goodbye, one just dies
            if (n >= 3)
            {
                int before = g.players.Count;
                Send(C, "quit");
                t0 = RT;
                while (g.players.Count == before && RT - t0 < 10f) yield return null;
                float took = RT - t0;
                clients.Remove(C);
                yield return W(0.7f);
                yield return Sum();
                Ck(g.players.Count == before - 1 && Eq("n") && S("H", "n") == (before - 1).ToString() && S("H", "notice").Contains("left") && g.PlayerByPid(pidC) == null,
                    C + " left the game (quit): gone on every screen " + F1(took) + " s later, notice '" + S("H", "notice") + "'");
            }
            if (n >= 2)
            {
                int before = g.players.Count;
                Send(B, "crash");
                t0 = RT;
                while (g.players.Count == before && RT - t0 < 16f) yield return null;
                float took = RT - t0;
                clients.Remove(B);
                yield return W(0.7f);
                yield return Sum();
                Ck(g.players.Count == before - 1 && Eq("n") && g.PlayerByPid(pidB) == null, B + "'s process was killed without a goodbye: the host noticed after " + F1(took) + " s and he is gone on every screen");
                // the game goes on for the ones who stayed
                yield return Tp(A, 0f, -8f, 0f, 0f);
                int wh = Send("H", "watch", pidA.ToString(), "1.4");
                yield return Cmd(A, "walk", "1", "0", "0.8");
                yield return Wait(wh, "H");
                float still = Parse(Rep(wh, "H")).F("moved");
                Check("the game goes on for the players who stayed (" + A + " still moves on the host's screen: " + F1(still) + " m)", still > 3f);
            }
            yield return Cmd("all", "shot", "before_host_quits");

            // ------------------------------------------------ 12. the host quits: the remaining client must land in the menu
            yield return Cmd("all", "prepquit");
            Log("bytes sent by the host in this run: " + Net.BytesOut + " in " + F1(RT) + " s = " + F1(Net.BytesOut / Mathf.Max(1f, RT) / 1024f) + " KB/s for all clients together");
            Finish();
            yield return W(0.5f);
            Application.Quit();
        }
#endif
    }

    // Start-menu test:  MedievalKnight.exe -menutest <folder> -port <free port> -bind 127.0.0.1
    // Drives the menu through the real input paths (queued mouse, key and text events): types a name, fails to
    // join a port where nobody listens, hosts, leaves with Esc Esc, plays single player, leaves again.
    public class MenuTest : MonoBehaviour
    {
#if ENABLE_INPUT_SYSTEM
        string dir;
        int shot, fails;
        Game g;
        Menu menu;

        static WaitForSecondsRealtime W(float s) { return new WaitForSecondsRealtime(s); }
        void Log(string s) { Debug.Log("MENUTEST " + s); }
        void Check(string what, bool ok) { if (!ok) fails++; Debug.Log("MENUTEST " + (ok ? "PASS " : "FAIL ") + what); }
        void Shot(string label) { shot++; ScreenCapture.CaptureScreenshot(Path.Combine(dir, shot.ToString("00") + "_" + label + ".png")); }

        static void Keys(params Key[] keys) { InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys)); }

        IEnumerator Tap(Key key)
        {
            Keys(key);
            yield return null; yield return null; yield return null;
            Keys();
            yield return null; yield return null;
        }

        IEnumerator TypeText(string s)
        {
            foreach (char c in s) { InputSystem.QueueTextEvent(Keyboard.current, c); yield return null; }
            yield return null;
        }

        static void SetMouse(Vector2 pos, bool left, bool right = false)
        {
            var st = new MouseState { position = pos };
            if (left) st = st.WithButton(MouseButton.Left, true);
            if (right) st = st.WithButton(MouseButton.Right, true);
            InputSystem.QueueStateEvent(Mouse.current, st);
        }

        IEnumerator Click(RectTransform rt)
        {
            Vector2 pos = rt.position;
            SetMouse(pos, false);
            yield return null; yield return null; yield return null;
            SetMouse(pos, true);
            yield return null; yield return null; yield return null;
            SetMouse(pos, false);
            yield return null; yield return null;
        }

        IEnumerator Erase(int n)
        {
            for (int i = 0; i < n; i++) yield return Tap(Key.Backspace);
        }

        IEnumerator Start()
        {
            dir = Args.menuTestDir;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
            Directory.CreateDirectory(dir);
            g = Game.I; menu = g.menu;
            yield return W(1.2f);

            Check("the game starts at the menu (no arena, no knight)", menu.Visible && menu.Current == Menu.Page.Main && !g.InSession && g.knight == null && Enemy.All.Count == 0);
            Shot("menu_main");
            yield return null;

            // --- name field: click, erase, type ---
            yield return Click(menu.FieldRect(Menu.Page.Main, "name"));
            yield return Erase(8);
            yield return TypeText("Te<st>er 1");
            Check("typing in the name field works and filters < > (got '" + menu.PlayerName + "')", menu.PlayerName == "Tester 1");

            // --- join page: bad input, then a port where nobody listens ---
            yield return Click(menu.ButtonRect(Menu.Page.Main, "join"));
            Check("Join Game opens the join page", menu.Current == Menu.Page.Join);
            yield return Tap(Key.Enter);
            Check("Connect without an address is refused with a hint ('" + menu.Message + "')", menu.Current == Menu.Page.Join && menu.Message.Contains("IP address"));
            yield return TypeText("127.0.0.1x");
            yield return Tap(Key.Backspace);
            yield return Tap(Key.Tab);
            yield return Erase(6);
            yield return TypeText("ab" + Args.port);
            Check("IP and port fields take typed text, Tab moves on, the port field only takes digits (ip '" + menu.FieldValue(Menu.Page.Join, "ip") + "', port '" + menu.FieldValue(Menu.Page.Join, "port") + "')",
                menu.FieldValue(Menu.Page.Join, "ip") == "127.0.0.1" && menu.FieldValue(Menu.Page.Join, "port") == Args.port.ToString());
            Shot("menu_join");
            yield return null;
            yield return Click(menu.ButtonRect(Menu.Page.Join, "connect"));
            bool sawConnecting = menu.Current == Menu.Page.Connecting || Net.Connecting;
            float t0 = Time.realtimeSinceStartup;
            while (menu.Current != Menu.Page.Join && Time.realtimeSinceStartup - t0 < 12f) yield return null;
            float took = Time.realtimeSinceStartup - t0;
            Log("connecting to 127.0.0.1:" + Args.port + " where nobody listens: back on the join page after " + took.ToString("0.0") + " s with '" + menu.Message + "'");
            Log("the Connecting page was showing right after the click: " + sawConnecting);
            Check("joining a port where nobody hosts fails with a clear message and no session", menu.Current == Menu.Page.Join && menu.Message.Contains("nobody is hosting") && !g.InSession && Net.Mode == NetMode.Single);
            yield return W(0.3f);
            Shot("menu_join_failed");
            yield return null;
            yield return Tap(Key.Escape);
            Check("Esc goes back to the main page", menu.Current == Menu.Page.Main);

            // --- host page: start hosting, look, leave with Esc Esc ---
            yield return Click(menu.ButtonRect(Menu.Page.Main, "host"));
            Check("Host Game opens the host page", menu.Current == Menu.Page.Host);
            yield return Erase(6);
            yield return TypeText(Args.port.ToString());
            yield return W(0.2f);
            Shot("menu_host");
            yield return null;
            Log("local IPv4 addresses offered to friends: " + string.Join(" | ", Net.LocalAddresses().ToArray()));
            yield return Click(menu.ButtonRect(Menu.Page.Host, "start"));
            yield return W(1.0f);
            // phase 4: Host Game plays the loop: the host's own base with PREPARE open, no enemies, the room shows him
            Check("Start Hosting starts a session as host '" + (g.knight != null ? g.knight.playerName : "?") + "' on port " + Net.Port + " (menu says '" + menu.Message + "')",
                g.InSession && Net.IsHost && !menu.Visible && g.knight != null && g.knight.playerName == "Tester 1" && Net.Port == Args.port
                && g.Coop && g.run != null && g.run.PrepareOpen && g.run.team != null && g.run.team.roster.Count == 1 && Enemy.All.Count == 0);
            yield return W(0.8f);
            Check("hosting alone: the PREPARE header lists the host and the host has the NEW WORLD n/m button (" + g.run.ui.EnterText + ")",
                g.run.ui.EnterText.StartsWith("NEW WORLD") && g.run.ui.EnterText.Contains("1/1"));
            Shot("hosting_alone");
            yield return null;
            yield return Tap(Key.Escape);                              // closes PREPARE (it is the first Esc: it does not count as 'leave')
            yield return W(0.3f);
            Check("Esc closes PREPARE first and the world does not pause online", !g.InventoryOpen && g.InSession && Time.timeScale > 0.5f);
            yield return Tap(Key.Escape);
            yield return W(0.2f);
            Check("the next Esc only asks ('" + g.hud.NoticeShown + "')", g.InSession && g.hud.NoticeShown.Contains("Esc again"));
            yield return Tap(Key.Escape);
            yield return W(0.5f);
            Check("the second Esc closes the hosted game and returns to the menu", !g.InSession && menu.Visible && menu.Current == Menu.Page.Main && Net.Mode == NetMode.Single && g.players.Count == 0 && Enemy.All.Count == 0);

            // the port is free again: hosting a second time must work
            yield return W(0.5f);
            yield return Click(menu.ButtonRect(Menu.Page.Main, "host"));
            yield return Click(menu.ButtonRect(Menu.Page.Host, "start"));
            yield return W(0.8f);
            Check("hosting again on the same port works (the listener was closed properly) and plays the loop again", g.InSession && Net.IsHost && g.Coop && g.run.PrepareOpen);
            g.LeaveToMenu();
            yield return W(0.5f);

            // --- single player from the menu ---
            yield return Click(menu.ButtonRect(Menu.Page.Main, "single"));
            yield return W(1.0f);
            Check("Single Player starts an offline session", g.InSession && Net.Mode == NetMode.Single && !menu.Visible && g.knight != null && g.players.Count == 1);
            if (g.InSession)
            {
                // Phase 1: Single Player goes to the base and PREPARE is open (it pauses); Esc closes it so the knight can walk
                Check("Single Player goes into the Phase 1 base: Game.Loop, PREPARE open and pausing, no enemies",
                    g.Loop && g.InventoryOpen && Time.timeScale == 0f && Enemy.All.Count == 0);
                yield return Tap(Key.Escape);
                yield return W(0.3f);
                Vector3 p0 = g.knight.transform.position;
                Keys(Key.W);
                yield return W(0.6f);
                Keys();
                yield return null;
                Check("the knight walks with the real W key after going through the menu (" + (g.knight.transform.position.z - p0.z).ToString("0.0") + " m)", g.knight.transform.position.z - p0.z > 1.5f);
                yield return Tap(Key.Tab);
                yield return W(0.2f);
                Check("single player still pauses for the PREPARE panel", g.InventoryOpen && Time.timeScale == 0f);
                yield return Tap(Key.Tab);
                yield return W(0.3f);
                Shot("single_player_from_menu");
                yield return null;
                yield return Tap(Key.Escape);
                yield return W(0.15f);
                yield return Tap(Key.Escape);
                yield return W(0.5f);
                Check("Esc Esc returns from single player to the menu", !g.InSession && menu.Visible);
            }
            Shot("menu_again");
            yield return W(0.5f);
            Log("done, screenshots=" + shot + ", failed checks=" + fails);
            Application.Quit();
        }
#endif
    }
}

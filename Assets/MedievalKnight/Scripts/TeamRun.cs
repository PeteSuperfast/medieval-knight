using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // How a player stands in the open run of the team.
    public enum Stat : byte { Alive = 0, Down = 1, Fallen = 2, Left = 3 }

    // One player of the room. The host keeps every field; a client gets the first group from the Team message.
    public class Member
    {
        public int pid;
        public string name = "";
        public bool ready;                    // READY is pressed (his carry is locked)
        public bool inRun;                    // plays the open run
        public Stat status;                   // while inRun
        public float downLeft;                // seconds until he falls for good, while Down

        // host only
        public readonly ItemBag mirror = new ItemBag(Loadout.CarryCaps[0], Loadout.CarryCaps[1], Loadout.CarryCaps[2]);   // his carry as last reported
        public int grants;                    // items the host gave him from the world (pickups, takes); his Pack counts what he applied
        public Vector3 lastPos;               // where he last was (a drop spot if he falls or leaves)
        public bool hasPack;                  // a Pack has arrived: the mirror is real
        public float dropWindow;              // rate limit of Drop messages
        public int dropsInWindow;
    }

    // Phase 4: the team around a RunLoop - who is in the room, who plays the open run, (host) the copy of every carry,
    // revive / wipe / portal / the end of the run. null in single player. The host decides; a client follows the host.
    public class TeamRun
    {
        public readonly List<Member> roster = new List<Member>();
        // players who left in the middle of the open run (their slot is free again): kept for the table and for the wipe
        public readonly List<Member> leavers = new List<Member>();

        // everybody the open run counts: the ones who play and the ones who left it
        public List<Member> RunMembers()
        {
            var l = new List<Member>();
            foreach (Member m in roster) if (m.inRun) l.Add(m);
            l.AddRange(leavers);
            return l;
        }
        readonly RunLoop run;
        readonly Game g;

        public bool runLive;                  // a run is open (somebody is playing in the world)
        public int worldSeed, worldLeft;      // that run's old world (0 = a new world) and its returns left
        bool dirty;                           // the roster changed: send Team
        float nextTeamSend;

        public static float TestHomeSeconds = 15f;                   // seconds from the first one in the portal until the team goes home (the test shortens it)

        public TeamRun(RunLoop r, Game game) { run = r; g = game; }

        // ---------------------------------------------------------------- the room

        public bool Has(int pid) { return Find(pid) != null; }

        // client: the host's old worlds (read only; the Worlds message fills it)

        public List<OldWorld> HostWorlds() { return hostWorlds2; }

        public Member Find(int pid)
        {
            foreach (Member m in roster) if (m.pid == pid) return m;
            return null;
        }

        public Member Me { get { return Find(Net.LocalPid); } }

        public bool IsPlaying(int pid) { Member m = Find(pid); return m != null && m.inRun && m.status != Stat.Left; }
        public bool LocalPlaying { get { Member m = Me; return m != null && m.inRun && run.InWorld; } }

        public void AddSelf(int pid, string name)
        {
            if (Find(pid) == null) roster.Add(new Member { pid = pid, name = name });
            roster.Sort((a, b) => a.pid.CompareTo(b.pid));
            dirty = true;
        }

        // Somebody came into the room (host: a connection was accepted; client: the host said so).
        public Member OnJoined(int pid, string name)
        {
            Member m = Find(pid);
            if (m == null)
            {
                m = new Member { pid = pid, name = name };
                roster.Add(m);
                roster.Sort((a, b) => a.pid.CompareTo(b.pid));
            }
            m.name = name;
            dirty = true;
            if (Net.IsHost) SendWorlds(pid);                          // he can look at the old worlds of this room while he packs
            return m;
        }

        // Somebody left the game (closed it, lost the connection). In an open run his knight goes and he is LEFT.
        public void OnLeft(int pid, string why)
        {
            Member m = Find(pid);
            if (m == null) return;
            Knight k = g.PlayerByPid(pid);
            if (m.inRun && k != null && k.remote) m.lastPos = k.Pos;
            if (k != null && k.remote)
            {
                g.players.Remove(k);
                Fx.I.Dust(k.transform.position, 14, 1.4f);
                k.gameObject.SetActive(false);
                Object.Destroy(k.gameObject);
            }
            g.hud.Notice(m.name + " left the game", new Color(1f, 0.7f, 0.5f));
            Debug.Log("PLAYER left: " + pid + " '" + m.name + "' (" + why + "), in run " + m.inRun);
            roster.Remove(m);
            if (m.inRun && runLive)
            {
                m.status = Stat.Left;
                m.ready = false;
                leavers.Add(m);                                       // his carry (the mirror) and his last place still count for the end of the run
            }
            dirty = true;
        }

        // ---------------------------------------------------------------- READY

        public bool LocalReady { get { Member m = Me; return m != null && m.ready; } }

        // host: connected players that are not ready and are not in the open run (the ones "Start without waiting?" is about)
        public Member FirstWaiting()
        {
            foreach (Member m in roster)
                if (m.pid != Net.LocalPid && !m.ready && !m.inRun && m.status != Stat.Left) return m;
            return null;
        }

        public int ReadyCount
        {
            get
            {
                int n = 0;
                foreach (Member m in roster) if (m.pid == Net.LocalPid || m.ready) n++;
                return n;
            }
        }

        public int RoomCount
        {
            get
            {
                int n = 0;
                foreach (Member m in roster) if (m.status != Stat.Left) n++;
                return n;
            }
        }

        // The local READY button / un-ready. The host is always ready (he is the one who enters).
        public void SetLocalReady(bool on)
        {
            Member me = Me;
            if (me == null || me.ready == on) return;
            me.ready = on;
            dirty = true;
            SendPack();
        }

        // client: the Pack message (ready flag, the grants applied so far, the carry); host: just the roster
        public int applied;                   // client: grants applied (PickupTaken, TakeAck items)

        public void SendPack()
        {
            Member me = Me;
            if (me == null) return;
            if (Net.IsClient) Net.ToServer(TeamNet.Pack(me.ready, applied, run.data.carry));
        }

        // host: a client's Pack
        public void OnPack(int pid, bool ready, int appliedGrants, ItemBag bag)
        {
            Member m = Find(pid);
            if (m == null || !Net.IsHost) return;
            if (appliedGrants == (m.grants & 0xFFFF))                 // not stale: nothing was granted that he has not applied yet
            {
                for (int i = 0; i < m.mirror.weapons.Length; i++) m.mirror.weapons[i] = bag.weapons[i];
                for (int i = 0; i < m.mirror.foods.Length; i++) m.mirror.foods[i] = bag.foods[i];
                for (int i = 0; i < m.mirror.supplies.Length; i++) m.mirror.supplies[i] = bag.supplies[i];
                m.hasPack = true;
            }
            bool r = ready && m.mirror.HasWeapon && !m.inRun;
            if (r != m.ready) { m.ready = r; dirty = true; }
        }

        // ---------------------------------------------------------------- the run

        // host: ENTER. The host and every ready player take part; returns their pids. Sends Enter to everybody.
        public List<int> StartRunHost(int seed, int left)
        {
            var pids = new List<int>();
            foreach (Member m in roster)
            {
                bool play = m.pid == Net.LocalPid || (m.ready && m.status != Stat.Left);
                m.inRun = play;
                m.status = Stat.Alive;
                m.downLeft = 0f;
                if (play) pids.Add(m.pid);
            }
            runLive = true;
            leavers.Clear();
            worldSeed = seed;
            worldLeft = left;
            Net.ToAll(TeamNet.Enter(seed, left, pids), null);
            dirty = true;
            return pids;
        }

        // client: the host started a run
        public void OnEnter(int seed, int left, List<int> pids)
        {
            runLive = true;
            worldSeed = seed;
            worldLeft = left;
            foreach (Member m in roster)
            {
                m.inRun = pids.Contains(m.pid);
                m.status = Stat.Alive;
                m.downLeft = 0f;
            }
            run.OnTeamEnter(pids.Contains(Net.LocalPid), pids);
        }

        // client: the host's roster
        public void OnTeam(bool live, int seed, int left, List<Member> list)
        {
            runLive = live;
            worldSeed = seed;
            worldLeft = left;
            bool myReady = LocalReady;
            roster.Clear();
            leavers.Clear();
            foreach (Member m in list)
            {
                if (m.status == Stat.Left) { leavers.Add(m); continue; }
                if (m.pid == Net.LocalPid) m.ready = myReady;       // my own button is the truth for me
                roster.Add(m);
            }
            if (Me == null) AddSelf(Net.LocalPid, Net.LocalName);
            roster.Sort((a, b) => a.pid.CompareTo(b.pid));
        }

        // The run is over (everywhere): nobody plays, nobody is ready.
        public void EndRun()
        {
            homeAt = -1f;
            reachedSent = false;
            leavers.Clear();
            view = null;
            lastTakerPid = 255;
            runLive = false;
            worldSeed = 0;
            worldLeft = 0;
            foreach (Member m in roster)
            {
                m.inRun = false;
                m.ready = false;
                m.status = Stat.Alive;
                m.downLeft = 0f;
            }
            dirty = true;
        }

        // The knights of the other players who take part (made when the world is built, gone when the run ends).
        public void SpawnTeammates()
        {
            foreach (Member m in roster)
            {
                if (m.pid == Net.LocalPid || !m.inRun || m.status == Stat.Left) continue;
                if (g.PlayerByPid(m.pid) != null) continue;
                g.SpawnKnight(m.pid, m.name, true);
            }
        }

        public void RemoveTeammates()
        {
            for (int i = g.players.Count - 1; i >= 0; i--)
            {
                Knight k = g.players[i];
                if (!k.remote) continue;
                g.players.RemoveAt(i);
                k.gameObject.SetActive(false);
                Object.Destroy(k.gameObject);
            }
        }

        // ---------------------------------------------------------------- falling, items (later steps)

        // ---------------------------------------------------------------- falling, reviving, watching

        public const float ReviveSeconds = 2f, ReviveReach = 3f, ReviveReachHost = 3.5f;

        public Knight ReviveCandidate { get; private set; }          // a downed friend within reach: F revives him instead of the Ground Slam
        public Knight SpectateTarget { get; private set; }           // the local knight has fallen for good: the camera follows this one
        int holdTarget = -1;                                         // pid being revived by the local knight (-1 none)
        float holdT, holdHurt, interruptedAt = -10f;

        public float ReviveProgress { get { return holdTarget >= 0 ? Mathf.Clamp01(holdT / ReviveSeconds) : 0f; } }
        public bool ReviveInterrupted { get { return Time.unscaledTime - interruptedAt < 0.6f; } }

        // The local knight hit 0 health: down, a friend can still bring him back for Game.TestDownSeconds.
        public void OnLocalDown()
        {
            StopHold(false);
            g.hud.Notice("You are down - a friend can revive you (F)", new Color(1f, 0.45f, 0.4f));
            Debug.Log("TEAM local knight is down");
        }

        // The time ran out: he stays on the ground for good and watches the team.
        public void OnLocalFallen()
        {
            g.hud.Notice("You fell. Waiting for the team...", new Color(0.7f, 0.67f, 0.63f));
            Debug.Log("TEAM local knight has fallen");
        }

        public void OnLocalRevived()
        {
            g.hud.Banner("You are back on your feet", new Color(0.435f, 0.82f, 0.435f), 2.5f);
            SpectateTarget = null;
        }

        // Every frame while in the world, on every machine: who can I revive, am I holding F, who do I watch.
        public void TickLocal()
        {
            Knight me = g.knight;
            if (me == null) return;
            Knight best = null;
            float bd = ReviveReach * ReviveReach;
            if (!me.Dead)
                foreach (Knight k in g.players)
                {
                    if (k == me || !k.Down) continue;
                    Vector3 d = k.Pos - me.Pos; d.y = 0f;
                    float m = d.sqrMagnitude;
                    if (m <= bd) { bd = m; best = k; }
                }
            ReviveCandidate = best;
            bool holding = best != null && InputX.Held(KeyCode.F) && !me.IsAttacking && !me.Charging && !g.PanelOpen && Time.unscaledTime >= g.inputBlockUntil;
            if (holding)
            {
                if (holdTarget != best.pid)
                {
                    StopHold(true);
                    holdTarget = best.pid;
                    holdT = 0f;
                    holdHurt = me.LastHurtTime;
                    SendRevive(best.pid, 1);
                }
                if (me.LastHurtTime > holdHurt)                        // hit while reviving: starts over
                {
                    holdT = 0f;
                    holdHurt = me.LastHurtTime;
                    interruptedAt = Time.unscaledTime;
                }
                holdT += Time.unscaledDeltaTime;
                me.RevTarget = best.pid;
                if (holdT >= ReviveSeconds)
                {
                    int t = best.pid;
                    StopHold(false);
                    SendRevive(t, 2);
                }
            }
            else if (holdTarget >= 0) StopHold(true);

            // watching: a fallen knight follows a friend who is still on his feet (Q / E or the arrows switch)
            if (me.Fallen)
            {
                var alive = new List<Knight>();
                foreach (Knight k in g.players) if (k != me && !k.Dead) alive.Add(k);
                if (alive.Count == 0) { SpectateTarget = null; return; }
                int idx = alive.IndexOf(SpectateTarget);
                if (idx < 0) idx = 0;
                else if (InputX.Down(KeyCode.Q) || InputX.Down(KeyCode.LeftArrow)) idx = (idx + alive.Count - 1) % alive.Count;
                else if (InputX.Down(KeyCode.E) || InputX.Down(KeyCode.RightArrow)) idx = (idx + 1) % alive.Count;
                SpectateTarget = alive[idx];
            }
            else SpectateTarget = null;
        }

        void StopHold(bool tell)
        {
            if (holdTarget >= 0 && tell) SendRevive(holdTarget, 0);
            holdTarget = -1;
            holdT = 0f;
            if (g.knight != null) g.knight.RevTarget = 255;
        }

        // phase 0 let go / 1 holding / 2 done (2 s)
        void SendRevive(int target, int phase)
        {
            if (Net.IsHost) HostRevive(Net.LocalPid, target, phase);
            else Net.ToServer(TeamNet.Revive(target, phase));
        }

        // host: a reviver's message (or his own). Checked loosely (friends only): both in the run, the target really is
        // down, the reviver is on his feet and near. Then it goes on to the target, whose machine stands him up.
        public void HostRevive(int reviver, int target, int phase)
        {
            if (!Net.IsHost || !runLive || phase < 0 || phase > 2) return;
            Member mr = Find(reviver), mt = Find(target);
            Knight kr = g.PlayerByPid(reviver), kt = g.PlayerByPid(target);
            if (mr == null || mt == null || kr == null || kt == null || !mr.inRun || !mt.inRun || reviver == target) return;
            if (phase != 0)
            {
                Vector3 d = kt.Pos - kr.Pos; d.y = 0f;
                if (kr.Dead || !kt.Down || d.magnitude > ReviveReachHost) return;
            }
            if (target == Net.LocalPid) OnReviveRelay(reviver, target, phase);
            else Net.ToPid(target, TeamNet.ReviveRelay(reviver, target, phase));
            if (phase == 2) g.WorldBanner(kr.playerName + " revived " + kt.playerName, new Color(0.435f, 0.82f, 0.435f));
        }

        // The target's machine: "X is reviving you" (0 / 1) or "stand up" (2).
        public void OnReviveRelay(int reviver, int target, int phase)
        {
            if (target != Net.LocalPid || g.knight == null) return;
            if (phase == 2) g.knight.Revive();
        }

        // host, every frame: everybody who plays is down or fallen (and the team is not on its way home): the run is lost.
        void TickWipe()
        {
            int n = 0;
            foreach (Member m in roster)
            {
                if (!m.inRun || m.status == Stat.Left) continue;
                n++;
                if (m.status == Stat.Alive) return;
            }
            if (n == 0 || run.Busy || !run.InWorld) return;
            run.HostWipe();
        }

        // host, every frame: what each player's knight says about him (down / fallen / on his feet) is the roster's status
        void TickStatus()
        {
            foreach (Member m in roster)
            {
                if (!m.inRun || m.status == Stat.Left) continue;
                Knight k = g.PlayerByPid(m.pid);
                if (k == null) continue;
                Stat s = k.Fallen ? Stat.Fallen : k.Dead ? Stat.Down : Stat.Alive;
                m.downLeft = k.DownLeft;
                if (s == m.status) continue;
                Color red = new Color(1f, 0.45f, 0.4f);
                if (s == Stat.Down) g.WorldBanner(m.name + " is down", red);
                else if (s == Stat.Fallen) g.WorldBanner(m.name + " has fallen", red);
                m.status = s;
                dirty = true;
            }
        }

        // ---------------------------------------------------------------- the portal, going home together

        public const int Survived = 0, Fell = 1, Left = 2;           // TeamLine.status in the summary
        public float homeAt = -1f;                                    // unscaled time when the team goes home (< 0: not started)
        bool reachedSent;
        public bool HomeStarted { get { return homeAt >= 0f; } }
        public float HomeLeft { get { return homeAt < 0f ? 0f : Mathf.Max(0f, homeAt - Time.unscaledTime); } }

        // client: the Minotaur is dead on the host and the portal is here
        public void OnPortal(float x, float z) { run.OnBossKilled(new Vector3(x, 0f, z)); }

        // The local knight stood in the portal for 1.5 s: the countdown home starts for everybody (once).
        public void OnLocalReached()
        {
            if (reachedSent || homeAt >= 0f) return;
            reachedSent = true;
            if (Net.IsHost) HostReached(Net.LocalPid);
            else Net.ToServer(TeamNet.Reached());
        }

        // host: somebody (alive) stood in the portal. The first one starts the countdown; then everybody who is still in
        // the world goes home together (the fallen and the dead too - their things are safe).
        public void HostReached(int pid)
        {
            if (!Net.IsHost || !runLive || homeAt >= 0f || !run.BossDown) return;
            Member m = Find(pid);
            if (m == null || !m.inRun || m.status == Stat.Left || m.status == Stat.Fallen) return;
            homeAt = Time.unscaledTime + TestHomeSeconds;
            Net.ToAll(TeamNet.HomeIn(TestHomeSeconds), null);
            g.hud.Banner(m.name + " reached the portal - everybody returns in " + Mathf.RoundToInt(TestHomeSeconds), new Color(0.435f, 0.784f, 1f), 3f);
            Debug.Log("TEAM portal reached by " + m.name + ": home in " + TestHomeSeconds + " s");
        }

        // client: the host started the countdown
        public void OnHomeIn(float seconds)
        {
            if (Net.IsHost || !runLive) return;
            homeAt = Time.unscaledTime + Mathf.Clamp(seconds, 0f, 120f);
            g.hud.Banner("Portal reached - everybody returns in " + Mathf.RoundToInt(seconds), new Color(0.435f, 0.784f, 1f), 3f);
        }

        // client: the host ended the run (the table says how it went for everybody)
        public void OnRunEnd(RunEndInfo e) { run.OnTeamRunEnd(e); }

        // ---------------------------------------------------------------- the piles and the host's old worlds

        public OldWorld view;                                         // client: the piles of the world in play, as the host last said
        public int lastTakerPid = 255;                                // who emptied something from them last
        readonly List<OldWorld> hostWorlds2 = new List<OldWorld>();

        // host: the piles changed (somebody took something): everybody in the run gets the list
        public void BroadcastPiles(int byPid)
        {
            if (!Net.IsHost || !runLive) return;
            Net.ToAll(TeamNet.Piles(byPid, run.PileWorldInPlay), null);
        }

        // client: the host's piles of the world in play (the loot windows and markers follow)
        public void OnPiles(int byPid, List<Pile> list)
        {
            if (Net.IsHost) return;
            if (view == null) view = new OldWorld { seed = worldSeed, left = worldLeft };
            // update in place by id so an open loot window keeps its pile; new ones are added, missing ones go
            var keep = new List<Pile>();
            foreach (Pile np in list)
            {
                Pile p = view.PileById(np.id);
                if (p == null) { p = new Pile { id = np.id, x = np.x, z = np.z }; view.Adopt(p); }
                p.x = np.x; p.z = np.z;
                p.items.Clear();
                p.items.AddRange(np.items);
                keep.Add(p);
            }
            view.piles.RemoveAll(p => !keep.Contains(p));
            if (byPid != 255) lastTakerPid = byPid;
            run.OnPilesChanged(byPid);
        }

        // host: a client takes items from a pile. Each one must still be there (same place, same item) and must fit his
        // mirror; what is granted leaves the pile and is told to him (TakeAck, counted like a pickup); what is gone already
        // is reported as failed (he sees "Taken by ..."). Everybody gets the new piles.
        public void HostTake(int pid, int pileId, List<int> index, List<Item> items)
        {
            Member m = Find(pid);
            OldWorld w = run.PileWorldInPlay;
            if (!Net.IsHost || m == null || !m.inRun || m.status == Stat.Left || m.status == Stat.Down || m.status == Stat.Fallen || w == null) return;
            Pile p = w.PileById(pileId);
            var granted = new List<Item>();
            int failed = 0;
            if (p == null) failed = index.Count;
            else
            {
                var removed = new List<int>();
                for (int i = 0; i < index.Count; i++)
                {
                    int at = index[i];
                    Item it = items[i];
                    if (at < 0 || at >= p.items.Count || removed.Contains(at) || it.IsNone || !p.items[at].Same(it) || !m.mirror.CanAdd(it)) { failed++; continue; }
                    m.mirror.Add(it);
                    m.grants++;
                    removed.Add(at);
                    granted.Add(it);
                }
                removed.Sort();
                for (int i = removed.Count - 1; i >= 0; i--) p.items.RemoveAt(removed[i]);
                if (p.items.Count == 0) w.piles.Remove(p);
            }
            Net.ToPid(pid, TeamNet.TakeAck(pileId, granted, failed));
            if (granted.Count > 0)
            {
                lastTakerPid = pid;
                run.SaveWorldChange();
                run.OnPilesChanged(pid);
                BroadcastPiles(pid);
            }
            Debug.Log("TAKE by " + m.name + ": " + granted.Count + " granted, " + failed + " gone already");
        }

        // client: the host answered a Take: the granted items are mine now (counted like pickups)
        public void OnTakeAck(int pileId, List<Item> got, int failed)
        {
            run.OnTakeAck(got, failed);
        }

        // host: the old worlds of this save go to one client (pid >= 0) or to everybody (-1)
        public void SendWorlds(int pid)
        {
            if (!Net.IsHost) return;
            byte[] msg = TeamNet.Worlds(run.data.Sorted());
            if (pid >= 0) Net.ToPid(pid, msg); else Net.ToAll(msg, null);
        }

        public List<OldWorld> HostWorldsView() { return hostWorlds2; }

        // client: the host's old worlds, read only (the OLD WORLDS list at the base)
        public void OnWorlds(List<OldWorld> list)
        {
            hostWorlds2.Clear();
            hostWorlds2.AddRange(list);
            hostWorlds2.Sort((a, b) => a.left != b.left ? a.left.CompareTo(b.left) : b.fellUnix.CompareTo(a.fellUnix));
        }

        // ---------------------------------------------------------------- items

        bool packDirty;
        float nextPackSend;

        // The local carry changed (eaten, dropped, picked up, moved): a client tells the host (not more than 4 times a second).
        public void PackChanged() { packDirty = true; }

        // host: a teammate stands on an item. It goes into his mirror and he is told (PickupTaken: the item, its +N) -
        // true; when the mirror says it does not fit it stays on the ground (false) and nothing changes.
        public bool HostCollect(Knight k, Pickup p)
        {
            Member m = Find(k.pid);
            if (m == null || !m.inRun || m.status == Stat.Left || !Net.IsHost) return false;
            Item it = p.kind == PickupKind.Food ? Item.Food(p.weaponId) : Item.Weapon(p.weaponId, p.plus);
            if (!it.Valid || it.IsNone || !m.mirror.CanAdd(it)) return false;
            m.mirror.Add(it);
            m.grants++;
            int id = p.netId, wid = p.weaponId, plus = p.plus;
            PickupKind kind = p.kind;
            Vector3 at = p.transform.position;
            p.gameObject.SetActive(false);
            Object.Destroy(p.gameObject);
            Net.BroadcastPickupTaken(id, k.pid, (int)kind, wid, at, plus);
            Debug.Log("PICKUP by " + m.name + ": " + it.Name + " (mirror " + m.mirror.Total + " items)");
            return true;
        }

        // host: one message to every client who plays the open run (not to the ones waiting at the base)
        public void SendToPlaying(byte[] msg)
        {
            if (!Net.IsHost) return;
            foreach (Member m in roster)
                if (m.inRun && m.pid != Net.LocalPid && m.status != Stat.Left) Net.ToPid(m.pid, msg);
        }

        // host: a client put an item down. It lies there as a pickup that nobody retakes until the dropper steps away.
        // (A believable rate only: friends play, but a runaway client must not fill the world with pickups.)
        public void OnDrop(int pid, Item it, float x, float z)
        {
            Member m = Find(pid);
            if (m == null || !m.inRun || m.status == Stat.Left || it.IsNone || !Net.IsHost) return;
            if (Time.unscaledTime - m.dropWindow > 5f) { m.dropWindow = Time.unscaledTime; m.dropsInWindow = 0; }
            if (++m.dropsInWindow > 20) return;
            Knight k = g.PlayerByPid(pid);
            var at = new Vector3(Mathf.Clamp(x, -Game.ArenaHalf + 1f, Game.ArenaHalf - 1f), 0f, Mathf.Clamp(z, -Game.ArenaHalf + 1f, Game.ArenaHalf - 1f));
            if (k != null)
            {
                Vector3 d = at - k.Pos; d.y = 0f;
                if (d.magnitude > 6f) at = k.Pos;                       // a believable spot: next to him
            }
            Pickup p = it.IsWeapon ? g.SpawnPickup(it.id, at) : g.SpawnFood(it.id, at);
            p.plus = it.plus;
            p.dropped = true;
            Debug.Log("DROP by " + m.name + ": " + it.Name);
        }

        // ---------------------------------------------------------------- every frame

        public void Tick()
        {
            if (Net.IsHost && runLive && homeAt >= 0f && Time.unscaledTime >= homeAt)
            {
                homeAt = -1f;
                run.HostVictory();
            }
            if (Net.IsClient && packDirty && Time.unscaledTime >= nextPackSend)
            {
                packDirty = false;
                nextPackSend = Time.unscaledTime + 0.25f;
                SendPack();
            }
            if (Net.IsHost)
            {
                foreach (Member m in roster)
                {
                    Knight k = g.PlayerByPid(m.pid);
                    if (k != null && m.inRun && m.status != Stat.Left) m.lastPos = k.Pos;
                }
                if (runLive) TickStatus();
                if (runLive && homeAt < 0f) TickWipe();
                if (dirty && Time.unscaledTime >= nextTeamSend)
                {
                    dirty = false;
                    nextTeamSend = Time.unscaledTime + 0.1f;
                    Net.ToAll(TeamNet.Team(this), null);
                }
            }
        }
    }
}

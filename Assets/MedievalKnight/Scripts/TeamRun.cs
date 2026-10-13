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
    }

    // Phase 4: the team around a RunLoop - who is in the room, who plays the open run, (host) the copy of every carry,
    // revive / wipe / portal / the end of the run. null in single player. The host decides; a client follows the host.
    public class TeamRun
    {
        public readonly List<Member> roster = new List<Member>();
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
        public readonly List<OldWorld> hostWorlds = new List<OldWorld>();
        public List<OldWorld> HostWorlds() { return hostWorlds; }

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
            if (m.inRun && runLive) { m.status = Stat.Left; m.ready = false; }
            else roster.Remove(m);
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
            foreach (Member m in list)
            {
                if (m.pid == Net.LocalPid) m.ready = myReady;       // my own button is the truth for me
                roster.Add(m);
            }
            if (Me == null) AddSelf(Net.LocalPid, Net.LocalName);
            roster.Sort((a, b) => a.pid.CompareTo(b.pid));
        }

        // The run is over (everywhere): nobody plays, nobody is ready.
        public void EndRun()
        {
            roster.RemoveAll(m => m.status == Stat.Left);
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

        public void OnLocalDown() { }

        // host: a teammate stands on an item. Taken into his mirror (true) or left lying (false).
        public bool HostCollect(Knight k, Pickup p) { return false; }

        // ---------------------------------------------------------------- every frame

        public void Tick()
        {
            if (Net.IsHost)
            {
                foreach (Member m in roster)
                {
                    Knight k = g.PlayerByPid(m.pid);
                    if (k != null && m.inRun && m.status != Stat.Left) m.lastPos = k.Pos;
                }
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

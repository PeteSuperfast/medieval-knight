using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // Phase 4: the messages of the loop online (Msg.Pack ... Msg.RunEnd). The encoders build the bytes, OnHost / OnClient
    // decode what arrives and hand it to TeamRun (which does the deciding). Layouts: Docs/phase4-coop/tech-net.md.
    // Everything is untrusted input: a short or odd message throws (Net.Poll catches it and drops that connection) and
    // every number is clamped / validated before it is used.
    // What a RunEnd message says (the table of the team summary G3 and what happened to the world).
    public class RunEndInfo
    {
        public int kind;                                  // 1 = the team returned home, 2 = everybody fell, 3 = the host closed the game
        public int worldSeed, worldNote, leftBefore, leftAfter, pilesN, itemsInPiles;
        public readonly List<TeamLine> team = new List<TeamLine>();
        public readonly List<int> pids = new List<int>();
        public readonly List<CrumbLine> crumbs = new List<CrumbLine>();
    }

    internal static class TeamNet
    {
        // ---------------------------------------------------------------- pieces

        public static void PutItem(NetW w, Item it) { w.U8((int)it.kind).U8(it.id).U8(it.plus); }

        // An item that is not a valid Weapon / Food reads as "nothing".
        public static Item GetItem(NetR r)
        {
            int k = r.U8(), id = r.U8(), plus = r.U8();
            if (k < 1 || k > (int)ItemKind.Food) return Item.None;
            var it = new Item { kind = (ItemKind)k, id = id, plus = plus };
            return it.Valid ? it : Item.None;
        }

        // the carry as 9 items: Weapons (2), Foods (3), Supplies (4)
        static void PutBag(NetW w, ItemBag b)
        {
            foreach (Item it in b.weapons) PutItem(w, it);
            foreach (Item it in b.foods) PutItem(w, it);
            foreach (Item it in b.supplies) PutItem(w, it);
        }

        static void ReadBag(NetR r, ItemBag dst)
        {
            for (int i = 0; i < dst.weapons.Length; i++) dst.weapons[i] = GetItem(r);
            for (int i = 0; i < dst.foods.Length; i++) dst.foods[i] = GetItem(r);
            for (int i = 0; i < dst.supplies.Length; i++) dst.supplies[i] = GetItem(r);
            dst.Normalize();
        }

        // ---------------------------------------------------------------- encoders

        // C>H: ready flag, how many host grants this client has applied, the carry
        public static byte[] Pack(bool ready, int applied, ItemBag carry)
        {
            var w = new NetW(Msg.Pack).U8(ready ? 1 : 0).U16(applied & 0xFFFF);
            PutBag(w, carry);
            return w.Done();
        }

        // C>H: an item put down in the world
        public static byte[] Drop(Item it, float x, float z)
        {
            var w = new NetW(Msg.Drop);
            PutItem(w, it);
            return w.Pos(x).Pos(z).Done();
        }

        // C>H: I am holding F on this player (phase 1) / let go (0) / 2 s are up (2)
        public static byte[] Revive(int target, int phase) { return new NetW(Msg.Revive).U8(target).U8(phase).Done(); }

        // H>C: the same, relayed to the one who is being revived (with who does it)
        public static byte[] ReviveRelay(int reviver, int target, int phase) { return new NetW(Msg.Revive).U8(reviver).U8(target).U8(phase).Done(); }

        // C>H: stood in the portal for the whole 1.5 s
        public static byte[] Reached() { return new NetW(Msg.Reached).Done(); }

        // H>C: the Minotaur is dead: the way home is here
        public static byte[] Portal(float x, float z) { return new NetW(Msg.Portal).Pos(x).Pos(z).Done(); }

        // H>C: the team goes home in this many seconds
        public static byte[] HomeIn(float seconds) { return new NetW(Msg.HomeIn).F32(seconds).Done(); }

        // H>C: the run is over: how, what happened to the world, one line per player (pid, status, items), the old worlds' counters
        public static byte[] RunEnd(RunEndInfo e)
        {
            var w = new NetW(Msg.RunEnd).U8(e.kind).I32(e.worldSeed).U8(e.worldNote).U8(e.leftBefore).U8(e.leftAfter)
                .U8(e.pilesN).U16(e.itemsInPiles).U8(e.team.Count);
            for (int i = 0; i < e.team.Count; i++) w.U8(e.pids[i]).U8(e.team[i].status).U8(Mathf.Clamp(e.team[i].items, 0, 255));
            int nc = Mathf.Min(e.crumbs.Count, 12);
            w.U8(nc);
            for (int i = 0; i < nc; i++)
            {
                CrumbLine c = e.crumbs[i];
                w.I32(c.seed).U8(c.before).U8(c.after).U16(Mathf.Clamp(c.itemsLost, 0, 65535)).U8(c.crumbled ? 1 : 0);
            }
            return w.Done();
        }

        // H>C: who is in the room and what state they are in
        public static byte[] Team(TeamRun t)
        {
            var w = new NetW(Msg.Team).U8(t.runLive ? 1 : 0).I32(t.worldSeed).U8(t.worldLeft).U8(t.roster.Count);
            foreach (Member m in t.roster)
                w.U8(m.pid).Str(m.name).U8((m.ready ? 1 : 0) | (m.inRun ? 2 : 0)).U8((int)m.status).U16(Mathf.Clamp(Mathf.RoundToInt(m.downLeft), 0, 65535));
            return w.Done();
        }

        // H>C: the run begins in this world; the listed players take part (the others wait)
        public static byte[] Enter(int seed, int left, List<int> pids)
        {
            var w = new NetW(Msg.Enter).I32(seed).U8(left).U8(pids.Count);
            foreach (int p in pids) w.U8(p);
            return w.Done();
        }

        // ---------------------------------------------------------------- decoders

        // host: a client's message that Net does not know (the arena has none of these)
        public static void OnHost(Conn c, Msg type, NetR r, Game g)
        {
            if (!g.Coop || g.run.team == null) return;
            TeamRun t = g.run.team;
            switch (type)
            {
                case Msg.Drop:
                {
                    Item it = GetItem(r);
                    float x = r.Pos(), z = r.Pos();
                    t.OnDrop(c.pid, it, x, z);
                    break;
                }

                case Msg.Reached:
                    t.HostReached(c.pid);
                    break;

                case Msg.Revive:
                {
                    int target = r.U8(), phase = r.U8();
                    t.HostRevive(c.pid, target, phase);
                    break;
                }

                case Msg.Pack:
                {
                    bool ready = r.U8() != 0;
                    int applied = r.U16();
                    var bag = new ItemBag(Loadout.CarryCaps[0], Loadout.CarryCaps[1], Loadout.CarryCaps[2]);
                    ReadBag(r, bag);
                    t.OnPack(c.pid, ready, applied, bag);
                    break;
                }
            }
        }

        // client: a host message that Net does not know
        public static void OnClient(Msg type, NetR r, Game g)
        {
            if (!g.Coop || g.run.team == null) return;
            TeamRun t = g.run.team;
            switch (type)
            {
                case Msg.Team:
                {
                    bool live = r.U8() != 0;
                    int seed = r.I32(), left = r.U8();
                    int n = Mathf.Min(r.U8(), Net.MaxPlayers);
                    var list = new List<Member>();
                    for (int i = 0; i < n; i++)
                    {
                        var m = new Member { pid = r.U8(), name = Net.CleanName(r.Str(), i) };
                        int flags = r.U8();
                        m.ready = (flags & 1) != 0; m.inRun = (flags & 2) != 0;
                        m.status = (Stat)Mathf.Clamp(r.U8(), 0, 3);
                        m.downLeft = r.U16();
                        list.Add(m);
                    }
                    t.OnTeam(live, seed, left, list);
                    break;
                }

                case Msg.Portal:
                {
                    float x = r.Pos(), z = r.Pos();
                    t.OnPortal(x, z);
                    break;
                }

                case Msg.HomeIn:
                    t.OnHomeIn(r.F32());
                    break;

                case Msg.Revive:
                {
                    int reviver = r.U8(), target = r.U8(), phase = r.U8();
                    t.OnReviveRelay(reviver, target, phase);
                    break;
                }

                case Msg.RunEnd:
                {
                    var e = new RunEndInfo { kind = r.U8(), worldSeed = r.I32(), worldNote = r.U8(), leftBefore = r.U8(), leftAfter = r.U8(), pilesN = r.U8(), itemsInPiles = r.U16() };
                    int n = Mathf.Min(r.U8(), Net.MaxPlayers);
                    for (int i = 0; i < n; i++)
                    {
                        int pid = r.U8(), status = r.U8(), items = r.U8();
                        Member m = t.Find(pid);
                        e.pids.Add(pid);
                        e.team.Add(new TeamLine { name = m != null ? m.name : "Player " + (pid + 1), status = Mathf.Clamp(status, 0, 2), items = items });
                    }
                    int nc = Mathf.Min(r.U8(), 12);
                    for (int i = 0; i < nc; i++)
                        e.crumbs.Add(new CrumbLine { seed = r.I32(), before = r.U8(), after = r.U8(), itemsLost = r.U16(), crumbled = r.U8() != 0 });
                    t.OnRunEnd(e);
                    break;
                }

                case Msg.Enter:
                {
                    int seed = r.I32(), left = r.U8();
                    int n = Mathf.Min(r.U8(), Net.MaxPlayers);
                    var pids = new List<int>();
                    for (int i = 0; i < n; i++) pids.Add(r.U8());
                    t.OnEnter(seed, left, pids);
                    break;
                }
            }
        }
    }
}

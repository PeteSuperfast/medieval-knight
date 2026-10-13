using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // Phase 4: the messages of the loop online (Msg.Pack ... Msg.RunEnd). The encoders build the bytes, OnHost / OnClient
    // decode what arrives and hand it to TeamRun (which does the deciding). Layouts: Docs/phase4-coop/tech-net.md.
    // Everything is untrusted input: a short or odd message throws (Net.Poll catches it and drops that connection) and
    // every number is clamped / validated before it is used.
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

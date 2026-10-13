using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MedievalKnight
{
    public enum NetMode { Single, Host, Client }

    // Every message is  [length : u16][type : u8][payload]  on one TCP stream per client (little endian).
    //   client -> host : Hello, State, Act, Hit, Ping, Bye, Test
    //   host -> client : Welcome, Refuse, Join, Leave, Snapshot, Act, EnemyHit, KnightHit, Exp, PickupTaken, Banner, Reset, Pong, Bye, Test
    public enum Msg : byte
    {
        Hello = 1,      // magic, build version, player name
        Welcome,        // your pid, version, the players already in the game
        Refuse,         // reason text (wrong version, game full); the host then closes the connection
        Join,           // pid, name: somebody came in
        Leave,          // pid: somebody left
        Bye,            // reason text: the sender is closing on purpose (the host quitting, a client leaving)
        Ping, Pong,     // time echo, once a second, for the ping read-out
        State,          // a client's own knight, 20 times a second
        Snapshot,       // the host's world, 15 times a second: counters, every knight, every enemy, every pickup
        Act,            // a knight started a sword attack / skill (host relays it with the pid in front)
        Hit,            // client: "my blow touched enemy N for D damage" (the host checks and applies it)
        EnemyHit,       // host: an enemy was hit / killed (numbers, sparks, who did it, health left)
        KnightHit,      // host to one client: a monster hit your knight (the client applies block / armor itself)
        Exp,            // host to one client: EXP from a kill, and whether the kill counts as yours
        PickupTaken,    // host: a sword or food was collected by pid (kind, item)
        Banner,         // host: a line about the shared world (wave, Minotaur, wipe)
        Reset,          // host pressed F5: everybody restarts at level 1
        Test,           // text relayed between the test scripts (MpTest.cs); the game itself never sends it
        // ---- phase 4 (the loop online) - see TeamNet.cs and Docs/phase4-coop/tech-net.md
        Pack,           // C>H: ready flag + the carry (+ how many grants it has applied)
        Drop,           // C>H: an item put down in the world
        Take,           // C>H: items taken from a pile
        Revive,         // C>H: holding F on a downed friend (phase 0 stop / 1 hold / 2 done); H>C: relayed
        Reached,        // C>H: stood in the portal for 1.5 s
        Team,           // H>C: the roster
        Worlds,         // H>C: the host's old worlds (read only)
        Enter,          // H>C: the run begins (world, who plays)
        Piles,          // H>C: the piles of the world
        TakeAck,        // H>C: what a Take got
        Portal,         // H>C: the Minotaur is dead, the portal is here
        HomeIn,         // H>C: the team goes home in N seconds
        RunEnd          // H>C: the run is over (win / wipe / host quit) with the table
    }

    // Builds one message. The first two bytes are the length prefix, filled in by Done().
    public class NetW
    {
        byte[] b = new byte[256];
        int n = 2;

        public NetW(Msg type) { U8((int)type); }

        void Need(int k) { if (n + k > b.Length) Array.Resize(ref b, Math.Max(b.Length * 2, n + k)); }
        public NetW U8(int v) { Need(1); b[n++] = (byte)v; return this; }
        public NetW U16(int v) { Need(2); b[n++] = (byte)v; b[n++] = (byte)(v >> 8); return this; }
        public NetW I32(int v) { Need(4); b[n++] = (byte)v; b[n++] = (byte)(v >> 8); b[n++] = (byte)(v >> 16); b[n++] = (byte)(v >> 24); return this; }
        public NetW F32(float v) { return I32(BitConverter.SingleToInt32Bits(v)); }
        // a coordinate in centimetres (i16 reaches +-327 m; the arena is 380 m wide)
        public NetW Pos(float v) { return U16(Mathf.Clamp(Mathf.RoundToInt(v * 100f), -32767, 32767) & 0xFFFF); }
        public NetW Vec(Vector3 v) { return Pos(v.x).Pos(v.y).Pos(v.z); }
        public NetW Yaw(float deg) { return U16(Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 360f * 65535f)); }
        // a non-negative amount with one decimal (health, armor)
        public NetW Amount(float v) { return U16(Mathf.Clamp(Mathf.RoundToInt(v * 10f), 0, 65535)); }

        public NetW Str(string s)
        {
            byte[] u = Encoding.UTF8.GetBytes(s ?? "");
            int len = Math.Min(u.Length, 4000);
            U16(len);
            Need(len);
            Buffer.BlockCopy(u, 0, b, n, len);
            n += len;
            return this;
        }

        public int Length { get { return n; } }

        public byte[] Done()
        {
            int len = n - 2;
            if (len > 65535) throw new InvalidOperationException("message too long: " + len);
            var r = new byte[n];
            Buffer.BlockCopy(b, 0, r, 0, n);
            r[0] = (byte)len; r[1] = (byte)(len >> 8);
            return r;
        }
    }

    // Reads one message body (without the length prefix). Reading past the end throws; the caller treats
    // that as a broken message.
    public class NetR
    {
        readonly byte[] d;
        int p;

        public NetR(byte[] data) { d = data; }

        public int U8() { return d[p++]; }
        public int U16() { int v = d[p] | (d[p + 1] << 8); p += 2; return v; }
        public int I32() { int v = d[p] | (d[p + 1] << 8) | (d[p + 2] << 16) | (d[p + 3] << 24); p += 4; return v; }
        public float F32() { return BitConverter.Int32BitsToSingle(I32()); }
        public float Pos() { return (short)U16() / 100f; }
        public Vector3 Vec() { float x = Pos(), y = Pos(), z = Pos(); return new Vector3(x, y, z); }
        public float Yaw() { return U16() / 65535f * 360f; }
        public float Amount() { return U16() / 10f; }

        public string Str()
        {
            int len = U16();
            if (p + len > d.Length) throw new IndexOutOfRangeException("string past the end of the message");
            string s = Encoding.UTF8.GetString(d, p, len);
            p += len;
            return s;
        }
    }

    // One TCP connection with its own reader and writer thread, so the game's main thread never waits on a socket.
    // Whole messages that arrive are queued for the main thread (Net.Poll); messages to send are queued for the writer.
    class Conn
    {
        public int pid = -1;                 // host side: the player slot once the handshake is done
        public string name = "?";
        public bool ready;                   // handshake finished
        public float connectedAt, lastRecv;  // main-thread clock (Time.realtimeSinceStartup)
        public float hitWindow;
        public int hitsInWindow, rejectedHits;
        public readonly string remote;

        readonly TcpClient tcp;
        readonly NetworkStream stream;
        readonly Queue<byte[]> outq = new Queue<byte[]>();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        Thread reader, writer;
        volatile bool closing, flushThenClose;
        int closedFlag;

        public Conn(TcpClient client)
        {
            tcp = client;
            tcp.NoDelay = true;                              // small messages must leave at once, not wait to be batched
            stream = tcp.GetStream();
            string r = "?";
            try { r = tcp.Client.RemoteEndPoint.ToString(); } catch (Exception) { }
            remote = r;
        }

        public void Start()
        {
            reader = new Thread(ReadLoop) { IsBackground = true, Name = "MK net read" };
            writer = new Thread(WriteLoop) { IsBackground = true, Name = "MK net write" };
            reader.Start();
            writer.Start();
        }

        public void Send(byte[] msg)
        {
            if (closing) return;
            bool overflow = false;
            lock (outq)
            {
                if (outq.Count > 800) overflow = true;       // the other side stopped reading: give up on it
                else outq.Enqueue(msg);
            }
            if (overflow) Close("send queue overflow");
            else wake.Set();
        }

        bool ReadFull(byte[] buf, int len)
        {
            int got = 0;
            while (got < len)
            {
                int r = stream.Read(buf, got, len - got);
                if (r <= 0) return false;
                got += r;
            }
            return true;
        }

        void ReadLoop()
        {
            string why = "connection closed";
            try
            {
                var head = new byte[2];
                while (!closing)
                {
                    if (!ReadFull(head, 2)) break;
                    int len = head[0] | (head[1] << 8);
                    if (len == 0) { why = "bad data"; break; }
                    var body = new byte[len];
                    if (!ReadFull(body, len)) break;
                    Net.Enqueue(Net.EvMessage, this, body, null);
                }
            }
            catch (Exception e) { why = closing ? "closed" : "connection lost (" + Kind(e) + ")"; }
            Close(why);
        }

        static string Kind(Exception e) { return e.GetType().Name.Replace("Exception", " error"); }

        void WriteLoop()
        {
            string why = "closed";
            try
            {
                var batch = new List<byte[]>();
                while (true)
                {
                    wake.WaitOne(200);
                    batch.Clear();
                    int total = 0;
                    lock (outq)
                    {
                        while (outq.Count > 0) { byte[] m = outq.Dequeue(); batch.Add(m); total += m.Length; }
                    }
                    if (total > 0)
                    {
                        var all = new byte[total];           // everything queued this frame goes out as one write
                        int at = 0;
                        for (int i = 0; i < batch.Count; i++) { Buffer.BlockCopy(batch[i], 0, all, at, batch[i].Length); at += batch[i].Length; }
                        stream.Write(all, 0, total);
                    }
                    if (closing) break;
                    if (flushThenClose)
                    {
                        bool empty;
                        lock (outq) empty = outq.Count == 0;
                        if (empty) break;
                    }
                }
            }
            catch (Exception e) { why = closing ? "closed" : "connection lost (" + Kind(e) + ")"; }
            Close(why);
        }

        // Send what is queued, then close (used for the Bye / Refuse messages).
        public void CloseAfterFlush()
        {
            flushThenClose = true;
            wake.Set();
        }

        public void WaitFlushed(int ms)
        {
            if (writer != null && writer.IsAlive) writer.Join(ms);
        }

        public bool Closed { get { return closedFlag != 0; } }

        public void Close(string reason)
        {
            if (Interlocked.Exchange(ref closedFlag, 1) != 0) return;
            closing = true;
            try { tcp.Close(); } catch (Exception) { }
            wake.Set();
            Net.Enqueue(Net.EvClosed, this, null, reason);
        }
    }

    // Host-authoritative co-op over plain TCP sockets, no relay server and no extra packages.
    //   Host   : listens, runs the whole world (enemies, waves, damage, drops, EXP) and sends snapshots.
    //   Client : simulates only its own knight, reports state / attacks / hits, shows the host's world.
    // All socket work is on background threads (Conn); everything in this class runs on the main thread from
    // Net.Poll (start of Game.Update) and Net.LateTick (Game.LateUpdate).
    public static class Net
    {
        public const int DefaultPort = 7777, MaxPlayers = 4, Protocol = 4;     // 4 (phase 6): Act kinds 30..36 (combo / heavy attacks), Hit.src 30..36, NetFlags bit 32 = charging; 3 (phase 4): Welcome has a mode, knights carry a revive target + NetFallen, PickupTaken a plus, the team messages (TeamNet.cs)
        public const int Magic = 0x4D4B4E54;                                 // "MKNT"
        public const float SnapshotRate = 15f, StateRate = 20f;              // messages per second
        public const float ConnectTimeout = 6f, HandshakeTimeout = 6f, SilenceTimeout = 10f;
        public const float EnemyDelay = 0.1f, KnightDelay = 0.13f;           // how far in the past remote things are drawn
        const int MaxHitsPerSecond = 150;

        public static NetMode Mode = NetMode.Single;
        public static int LocalPid;
        public static string LocalName = "Knight";
        public static int Port = DefaultPort;
        public static string HostAddress = "";                               // client: what was typed; host: the first LAN address
        public static int PingMs = -1;
        public static bool Connecting { get; private set; }                  // a join attempt is in progress
        public static int SnapshotsReceived { get; private set; }
        public static int BytesOut { get; private set; }
        public static int Version { get { return Args.netVersion; } }        // the build's protocol number (-netversion fakes another one)

        public static bool Multiplayer { get { return Mode != NetMode.Single; } }
        public static bool Authority { get { return Mode != NetMode.Client; } }
        public static bool IsHost { get { return Mode == NetMode.Host; } }
        public static bool IsClient { get { return Mode == NetMode.Client; } }
        // players in the game: the knights in the arena; in the loop everybody in the room (they are not all in the world)
        public static int PlayerCount
        {
            get
            {
                if (Game.I == null) return 0;
                if (Game.I.Coop && Game.I.run.team != null) return Game.I.run.team.RoomCount;
                return Game.I.players.Count;
            }
        }

        public static Action<string> OnTest;                                 // test-script messages (MpTest.cs)
        public static int RefusedFull, RefusedVersion;                       // host: how many joins were turned away, by reason
        public static readonly int[] KnightHitsSent = new int[MaxPlayers];   // host: monster hits forwarded to each remote player

        // --- events from the socket threads ---
        public const int EvMessage = 0, EvClosed = 1, EvAccepted = 2, EvConnected = 3, EvConnectFailed = 4;

        struct Ev
        {
            public int kind, gen;
            public Conn conn;
            public byte[] data;
            public string text;
        }

        static readonly Queue<Ev> events = new Queue<Ev>();
        static readonly List<Conn> conns = new List<Conn>();                 // host: every accepted connection
        static TcpListener listener;
        static Conn server;                                                  // client: the connection to the host
        static bool welcomed;
        static int joinGen;
        static float handshakeDeadline, nextSnapshot, nextState, nextPing;

        internal static void Enqueue(int kind, Conn conn, byte[] data, string text, int gen = 0)
        {
            lock (events) events.Enqueue(new Ev { kind = kind, conn = conn, data = data, text = text, gen = gen });
        }

        static float Now { get { return Time.realtimeSinceStartup; } }

        // ---------------------------------------------------------------- starting / stopping

        public static string CleanName(string s, int pid)
        {
            var sb = new StringBuilder();
            if (s != null)
            {
                foreach (char c in s)
                {
                    if (c < 32 || c == '<' || c == '>') continue;            // no control characters, no rich-text tags
                    sb.Append(c);
                    if (sb.Length >= 14) break;
                }
            }
            string r = sb.ToString().Trim();
            return r.Length == 0 ? "Player " + (pid + 1) : r;
        }

        public static void StartSingle(string name)
        {
            Shutdown(null);
            Mode = NetMode.Single;
            LocalPid = 0;
            LocalName = CleanName(name, 0);
        }

        // Starts listening. Returns null when it worked, otherwise a line for the menu.
        public static string StartHost(int port, string name)
        {
            Shutdown(null);
            if (port < 1 || port > 65535) return "The port must be a number from 1 to 65535.";
            try
            {
                IPAddress bind = IPAddress.Any;
                if (!string.IsNullOrEmpty(Args.bind) && !IPAddress.TryParse(Args.bind, out bind)) bind = IPAddress.Any;
                listener = new TcpListener(bind, port);
                listener.Start(8);
            }
            catch (SocketException e)
            {
                listener = null;
                return e.SocketErrorCode == SocketError.AddressAlreadyInUse
                    ? "Port " + port + " is already in use on this PC. Pick another port."
                    : "Could not open port " + port + ": " + e.Message;
            }
            catch (Exception e)
            {
                listener = null;
                return "Could not open port " + port + ": " + e.Message;
            }

            TcpListener l = listener;
            var t = new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        TcpClient c = l.AcceptTcpClient();
                        Conn conn;
                        try { conn = new Conn(c); }
                        catch (Exception) { try { c.Close(); } catch (Exception) { } continue; }
                        Enqueue(EvAccepted, conn, null, null);
                        conn.Start();
                    }
                }
                catch (Exception) { }                                        // listener.Stop() ends the loop
            }) { IsBackground = true, Name = "MK net accept" };
            t.Start();

            Mode = NetMode.Host;
            LocalPid = 0;
            LocalName = CleanName(name, 0);
            Port = port;
            List<string> mine = LocalAddresses();
            HostAddress = mine.Count > 0 ? mine[0].Split(' ')[0] : "127.0.0.1";
            PingMs = -1;
            Debug.Log("NET hosting on port " + port + " as '" + LocalName + "', version " + Version + ", addresses: " + string.Join(", ", mine.ToArray()));
            return null;
        }

        // Starts connecting in the background. The outcome arrives through Poll: the session starts, or
        // Menu.JoinFailed gets a line of text.
        public static void Join(string host, int port, string name)
        {
            Shutdown(null);
            host = (host ?? "").Trim();
            LocalName = CleanName(name, 0);
            HostAddress = host;
            Port = port;
            Connecting = true;
            int gen = ++joinGen;
            Debug.Log("NET connecting to " + host + ":" + port + " as '" + LocalName + "', version " + Version);
            var t = new Thread(() =>
            {
                TcpClient c = null;
                try
                {
                    c = new TcpClient(AddressFamily.InterNetwork);
                    IAsyncResult ar = c.BeginConnect(host, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne((int)(ConnectTimeout * 1000f)))
                    {
                        try { c.Close(); } catch (Exception) { }
                        Enqueue(EvConnectFailed, null, null, "No answer from " + host + ":" + port + " (timed out). Check the address, the port and the host's firewall / port forwarding.", gen);
                        return;
                    }
                    c.EndConnect(ar);
                    var conn = new Conn(c);
                    Enqueue(EvConnected, conn, null, null, gen);
                    conn.Start();
                }
                catch (SocketException e)
                {
                    try { if (c != null) c.Close(); } catch (Exception) { }
                    string why = e.SocketErrorCode == SocketError.ConnectionRefused ? "nobody is hosting on that port (connection refused)"
                        : e.SocketErrorCode == SocketError.HostNotFound ? "that address was not found"
                        : e.SocketErrorCode == SocketError.TimedOut ? "timed out"
                        : e.SocketErrorCode.ToString();
                    Enqueue(EvConnectFailed, null, null, "Could not connect to " + host + ":" + port + " - " + why + ".", gen);
                }
                catch (Exception e)
                {
                    try { if (c != null) c.Close(); } catch (Exception) { }
                    Enqueue(EvConnectFailed, null, null, "Could not connect to " + host + ":" + port + " - " + e.Message, gen);
                }
            }) { IsBackground = true, Name = "MK net connect" };
            t.Start();
        }

        public static void CancelJoin()
        {
            if (Connecting || (server != null && !welcomed)) Shutdown(null);
        }

        // Closes every socket. bye != null: tell the other side first (and give the writer a moment to send it).
        public static void Shutdown(string bye)
        {
            joinGen++;
            Connecting = false;
            if (listener != null)
            {
                try { listener.Stop(); } catch (Exception) { }
                listener = null;
            }
            var all = new List<Conn>(conns);
            if (server != null) all.Add(server);
            conns.Clear();
            server = null;
            welcomed = false;
            if (all.Count > 0)
            {
                byte[] msg = bye != null ? new NetW(Msg.Bye).Str(bye).Done() : null;
                foreach (Conn c in all)
                {
                    if (msg != null) c.Send(msg);
                    c.CloseAfterFlush();
                }
                foreach (Conn c in all) c.WaitFlushed(300);
                foreach (Conn c in all) c.Close("shutdown");
            }
            Mode = NetMode.Single;
            LocalPid = 0;
            PingMs = -1;
        }

        // IPv4 addresses of this PC that a friend could type, each followed by the adapter name.
        public static List<string> LocalAddresses()
        {
            var list = new List<string>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        IPAddress a = ua.Address;
                        if (a.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(a)) continue;
                        string s = a.ToString();
                        if (s.StartsWith("169.254.")) continue;              // no network on that adapter
                        list.Add(s + "   (" + ni.Name + ")");
                    }
                }
            }
            catch (Exception) { }
            if (list.Count == 0)
            {
                try
                {
                    foreach (IPAddress a in Dns.GetHostAddresses(Dns.GetHostName()))
                        if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)) list.Add(a.ToString());
                }
                catch (Exception) { }
            }
            return list;
        }

        public static string StatusLine
        {
            get
            {
                if (IsHost) return "HOSTING   " + HostAddress + " : " + Port + "   players " + PlayerCount + " / " + MaxPlayers;
                if (IsClient) return "ONLINE   host " + HostAddress + " : " + Port + "   players " + PlayerCount + " / " + MaxPlayers
                    + (PingMs >= 0 ? "   ping " + PingMs + " ms" : "");
                return "";
            }
        }

        // ---------------------------------------------------------------- main-thread pump

        public static void Poll()
        {
            while (true)
            {
                Ev e;
                lock (events)
                {
                    if (events.Count == 0) break;
                    e = events.Dequeue();
                }
                try
                {
                    switch (e.kind)
                    {
                        case EvAccepted:
                            if (!IsHost) { e.conn.Close("not hosting"); break; }
                            e.conn.connectedAt = e.conn.lastRecv = Now;
                            conns.Add(e.conn);
                            break;
                        case EvConnected:
                            if (e.gen != joinGen) { e.conn.Close("cancelled"); break; }
                            server = e.conn;
                            server.lastRecv = Now;
                            welcomed = false;
                            Connecting = false;
                            handshakeDeadline = Now + HandshakeTimeout;
                            server.Send(new NetW(Msg.Hello).I32(Magic).U16(Version).Str(LocalName).Done());
                            break;
                        case EvConnectFailed:
                            if (e.gen == joinGen) JoinFailed(e.text);
                            break;
                        case EvMessage:
                            // (a message that arrived just before the socket closed still counts: Refuse and Bye are exactly that)
                            e.conn.lastRecv = Now;
                            if (e.conn == server) ClientMessage(new NetR(e.data));
                            else if (conns.Contains(e.conn)) HostMessage(e.conn, new NetR(e.data));
                            break;
                        case EvClosed:
                            if (e.conn == server) ServerLost(e.text);
                            else if (conns.Contains(e.conn)) Drop(e.conn, e.text);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // a message that does not parse: never trust it, never let it stop the game
                    Debug.LogWarning("NET ERROR while handling event " + e.kind + ": " + ex);
                    if (e.conn != null && e.conn != server && conns.Contains(e.conn)) Drop(e.conn, "sent bad data");
                }
            }

            // --- silence ---
            float now = Now;
            if (IsHost)
            {
                for (int i = conns.Count - 1; i >= 0; i--)
                {
                    Conn c = conns[i];
                    if (!c.ready && now - c.connectedAt > HandshakeTimeout) Drop(c, "no handshake");
                    else if (c.ready && now - c.lastRecv > SilenceTimeout) Drop(c, "timed out");
                }
            }
            else if (server != null)
            {
                if (!welcomed && now > handshakeDeadline)
                    JoinFailed("Connected to " + HostAddress + ":" + Port + " but it did not answer like a Medieval Knight host (timed out).");
                else if (welcomed && now - server.lastRecv > SilenceTimeout) ServerLost("timed out");
            }
        }

        // Once per frame after all movement: the client reports its knight, the host sends the world.
        public static void LateTick()
        {
            float now = Now;
            if (IsHost)
            {
                if (now < nextSnapshot) return;
                nextSnapshot = Mathf.Max(nextSnapshot + 1f / SnapshotRate, now - 0.5f / SnapshotRate);
                bool any = false;
                for (int i = 0; i < conns.Count; i++) if (conns[i].ready && Game.I.WantsSnapshot(conns[i].pid)) any = true;
                if (!any) return;
                byte[] snap = Game.I.WriteSnapshot().Done();
                for (int i = 0; i < conns.Count; i++)
                {
                    Conn c = conns[i];
                    if (!c.ready || !Game.I.WantsSnapshot(c.pid)) continue;     // the loop: only the ones inside the run see the world
                    BytesOut += snap.Length;
                    c.Send(snap);
                }
            }
            else if (IsClient && server != null && welcomed)
            {
                if (now >= nextState)
                {
                    nextState = Mathf.Max(nextState + 1f / StateRate, now - 0.5f / StateRate);
                    if (Game.I.SendsState)                                       // the loop: not while waiting at the base
                    {
                        var w = new NetW(Msg.State);
                        WriteKnight(w, Game.I.knight);
                        ToServer(w.Done());
                    }
                }
                if (now >= nextPing)
                {
                    nextPing = now + 1f;
                    ToServer(new NetW(Msg.Ping).F32(now).Done());
                }
            }
        }

        internal static void ToServer(byte[] msg)
        {
            if (server == null) return;
            BytesOut += msg.Length;
            server.Send(msg);
        }

        internal static void ToAll(byte[] msg, Conn except)
        {
            for (int i = 0; i < conns.Count; i++)
            {
                Conn c = conns[i];
                if (!c.ready || c == except) continue;
                BytesOut += msg.Length;
                c.Send(msg);
            }
        }

        // One message to one player (the host).
        internal static void ToPid(int pid, byte[] msg)
        {
            Conn c = IsHost ? ConnOf(pid) : null;
            if (c == null) return;
            BytesOut += msg.Length;
            c.Send(msg);
        }

        internal static Conn ConnOf(int pid)
        {
            for (int i = 0; i < conns.Count; i++) if (conns[i].ready && conns[i].pid == pid) return conns[i];
            return null;
        }

        // ---------------------------------------------------------------- knight state (State message and inside Snapshot)

        public static void WriteKnight(NetW w, Knight k)
        {
            Vector3 p = k.Pos;
            w.F32(k.NetTime).Vec(p).Yaw(k.NetYaw).U8(k.NetFlags)
                .Amount(k.hp).Amount(k.MaxHp).Amount(k.armor).Amount(k.MaxArmor)
                .U8(k.progress.level).U8(k.inventory.equipped).U8(k.inventory.plus[k.inventory.equipped])
                .U8(k.RevTarget);                                                // 255 = not reviving anybody (protocol 3)
        }

        // k == null: read and ignore (the local knight, or somebody not known here).
        public static void ReadKnight(NetR r, Knight k)
        {
            float time = r.F32();
            Vector3 p = r.Vec();
            float yaw = r.Yaw();
            int flags = r.U8();
            float hp = r.Amount(), maxHp = r.Amount(), armor = r.Amount(), maxArmor = r.Amount();
            int level = r.U8(), weapon = r.U8(), plus = r.U8(), revTarget = r.U8();
            if (k == null || !k.remote) return;
            float lim = Game.ArenaHalf + 1f;
            p.x = Mathf.Clamp(p.x, -lim, lim); p.z = Mathf.Clamp(p.z, -lim, lim); p.y = Mathf.Clamp(p.y, 0f, 6f);
            k.ApplyNet(time, p, yaw, flags, hp, maxHp, armor, maxArmor, level, weapon, plus, revTarget);
        }

        // ---------------------------------------------------------------- host: messages from a client

        static void HostMessage(Conn c, NetR r)
        {
            Msg type = (Msg)r.U8();
            if (!c.ready)
            {
                if (type == Msg.Hello) Hello(c, r);
                return;                                                      // nothing else counts before the handshake
            }
            Game g = Game.I;
            switch (type)
            {
                case Msg.State:
                    ReadKnight(r, g.PlayerByPid(c.pid));
                    break;

                case Msg.Act:
                {
                    int kind = r.U8();
                    float speed = r.F32();
                    Vector3 target = r.Vec();
                    Knight k = g.PlayerByPid(c.pid);
                    if (k != null) k.PlayRemoteAct(kind, speed, target);
                    ToAll(new NetW(Msg.Act).U8(c.pid).U8(kind).F32(speed).Vec(target).Done(), c);
                    break;
                }

                case Msg.Hit:
                {
                    int id = r.U16();
                    float damage = r.F32(), pierce = r.F32();
                    bool crit = r.U8() != 0;
                    float knock = r.F32(), stun = r.F32();
                    Vector3 origin = r.Vec();
                    int src = r.U8();
                    float now = Now;
                    if (now - c.hitWindow > 1f) { c.hitWindow = now; c.hitsInWindow = 0; }
                    bool ok = ++c.hitsInWindow <= MaxHitsPerSecond
                        && g.ClientHit(g.PlayerByPid(c.pid), id, damage, pierce, crit, knock, stun, origin, src);
                    if (!ok && ++c.rejectedHits <= 5) Debug.Log("NET hit from " + c.name + " not applied (enemy " + id + ", damage " + damage.ToString("0.0") + ")");
                    break;
                }

                case Msg.Ping:
                    c.Send(new NetW(Msg.Pong).F32(r.F32()).Done());
                    break;

                case Msg.Bye:
                    Drop(c, "left the game");
                    break;

                case Msg.Test:
                {
                    string text = r.Str();
                    ToAll(new NetW(Msg.Test).Str(text).Done(), c);
                    if (OnTest != null) OnTest(text);
                    break;
                }

                default:
                    TeamNet.OnHost(c, type, r, g);                          // phase 4: Pack, Drop, Take, Revive, Reached
                    break;
            }
        }

        static void Hello(Conn c, NetR r)
        {
            int magic = r.I32();
            int version = r.U16();
            string name = r.Str();
            if (magic != Magic) { Drop(c, "not a Medieval Knight client"); return; }
            if (version != Version)
            {
                RefusedVersion++;
                Refuse(c, "Version mismatch: the host runs network version " + Version + ", your game is version " + version + ". Everybody needs the same build.");
                return;
            }
            if (Game.I == null || !Game.I.InSession) { Refuse(c, "The host is not in a game right now."); return; }
            int pid = -1;
            for (int i = 1; i < MaxPlayers && pid < 0; i++) if (Game.I.PidFree(i)) pid = i;
            if (pid < 0)
            {
                RefusedFull++;
                Refuse(c, "The game is full (" + MaxPlayers + " / " + MaxPlayers + " players).");
                return;
            }

            c.pid = pid;
            c.name = CleanName(name, pid);
            // protocol 3: the mode (0 = the v1 arena, 1 = the loop) after the version, then who is in (the roster in the loop)
            List<KeyValuePair<int, string>> who = Game.I.WelcomeList();
            var w = new NetW(Msg.Welcome).U8(pid).U16(Version).U8(Game.I.Loop ? 1 : 0).U8(who.Count);
            foreach (KeyValuePair<int, string> k in who) w.U8(k.Key).Str(k.Value);
            c.Send(w.Done());
            c.ready = true;
            ToAll(new NetW(Msg.Join).U8(pid).Str(c.name).Done(), c);
            Debug.Log("NET join: '" + c.name + "' from " + c.remote + " is player " + pid);
            Game.I.OnPlayerJoined(pid, c.name, false);
        }

        static void Refuse(Conn c, string reason)
        {
            Debug.Log("NET refused " + c.remote + ": " + reason);
            c.Send(new NetW(Msg.Refuse).Str(reason).Done());
            conns.Remove(c);
            c.CloseAfterFlush();
        }

        // A connection is gone (left, crashed, timed out, sent rubbish): free its slot and tell the others.
        static void Drop(Conn c, string why)
        {
            if (!conns.Remove(c)) return;
            c.Close(why);
            if (!c.ready) return;
            c.ready = false;
            Debug.Log("NET leave: '" + c.name + "' (player " + c.pid + "): " + why);
            ToAll(new NetW(Msg.Leave).U8(c.pid).Done(), null);
            if (Game.I != null) Game.I.OnPlayerLeft(c.pid, why);
        }

        // ---------------------------------------------------------------- client: messages from the host

        static void JoinFailed(string text)
        {
            Debug.Log("NET join failed: " + text);
            Shutdown(null);
            if (Game.I != null && Game.I.menu != null) Game.I.menu.JoinFailed(text);
        }

        static void ServerLost(string why)
        {
            if (!welcomed) { JoinFailed("The connection to " + HostAddress + ":" + Port + " was closed (" + why + ")."); return; }
            Debug.Log("NET lost the host: " + why);
            Shutdown(null);
            if (Game.I != null) Game.I.EndSession("The connection to the host was lost (" + why + ").");
        }

        static void ClientMessage(NetR r)
        {
            Msg type = (Msg)r.U8();
            Game g = Game.I;
            if (!welcomed)
            {
                if (type == Msg.Welcome)
                {
                    int pid = r.U8();
                    int version = r.U16();
                    int loopMode = r.U8();                                   // protocol 3: 0 = the v1 arena, 1 = the loop (the host decides)
                    int n = r.U8();
                    Mode = NetMode.Client;
                    LocalPid = pid;
                    LocalName = CleanName(LocalName, pid);
                    welcomed = true;
                    SnapshotsReceived = 0;
                    nextState = nextPing = 0f;
                    Debug.Log("NET welcome: I am player " + pid + " on a version " + version + " host with " + n + " player(s) already in");
                    g.StartSession(loopMode == 1);
                    for (int i = 0; i < n; i++)
                    {
                        int other = r.U8();
                        string name = r.Str();
                        g.OnPlayerJoined(other, name, true);
                    }
                }
                else if (type == Msg.Refuse) JoinFailed(r.Str());
                return;
            }

            switch (type)
            {
                case Msg.Snapshot:
                    SnapshotsReceived++;
                    g.ReadSnapshot(r);
                    break;

                case Msg.Join:
                {
                    int pid = r.U8();
                    g.OnPlayerJoined(pid, r.Str(), false);
                    break;
                }

                case Msg.Leave:
                    g.OnPlayerLeft(r.U8(), "left the game");
                    break;

                case Msg.Act:
                {
                    int pid = r.U8(), kind = r.U8();
                    float speed = r.F32();
                    Vector3 target = r.Vec();
                    Knight k = g.PlayerByPid(pid);
                    if (k != null) k.PlayRemoteAct(kind, speed, target);
                    break;
                }

                case Msg.EnemyHit:
                {
                    int id = r.U16();
                    float damage = r.F32(), toArmor = r.F32(), toHp = r.F32();
                    int flags = r.U8();
                    Vector3 away = new Vector3((sbyte)r.U8() / 127f, 0f, (sbyte)r.U8() / 127f);
                    int by = r.U8();
                    float hp = r.F32(), armor = r.F32();
                    Enemy e = Enemy.Find(id);
                    if (e != null) e.NetHit(damage, toArmor, toHp, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, away, by != LocalPid, hp, armor);
                    break;
                }

                case Msg.KnightHit:
                {
                    float damage = r.F32();
                    Vector3 from = r.Vec();
                    float knock = r.F32();
                    string source = r.Str();
                    g.knight.TakeDamage(damage, from, source, knock);
                    break;
                }

                case Msg.Exp:
                {
                    float amount = r.F32();
                    Vector3 at = r.Vec();
                    bool credit = r.U8() != 0;
                    if (credit) g.progress.kills++;
                    int souls = r.U16();
                    if (amount > 0f) g.GrantExp(amount, at);
                    if (souls > 0) g.GrantSouls(souls, at);
                    break;
                }

                case Msg.PickupTaken:
                {
                    int id = r.U16(), pid = r.U8(), kind = r.U8(), item = r.U8();
                    Vector3 at = r.Vec();
                    int plus = r.U8();                                        // protocol 3: the +N of a weapon (the loop)
                    g.OnPickupTaken(id, pid, kind, item, at, plus);
                    break;
                }

                case Msg.Banner:
                {
                    string text = r.Str();
                    var col = new Color(r.U8() / 255f, r.U8() / 255f, r.U8() / 255f);
                    g.hud.Banner(text, col);
                    break;
                }

                case Msg.Reset:
                    Debug.Log("NET the host restarted the game");
                    g.ResetAll();
                    break;

                case Msg.Pong:
                    PingMs = Mathf.Max(0, Mathf.RoundToInt((Now - r.F32()) * 1000f));
                    break;

                case Msg.Bye:
                {
                    string why = r.Str();
                    Debug.Log("NET the host said goodbye: " + why);
                    Shutdown(null);
                    g.EndSession(why);
                    break;
                }

                case Msg.Test:
                    if (OnTest != null) OnTest(r.Str());
                    break;

                default:
                    TeamNet.OnClient(type, r, g);                           // phase 4: Team, Worlds, Enter, Piles, TakeAck, Portal, HomeIn, RunEnd, Revive
                    break;
            }
        }

        // ---------------------------------------------------------------- hooks called by the gameplay code

        // The local knight started a sword attack (kind 0..4, 30..36), a skill (10..13) or a dash (20). target: where a Ground Slam lands / the dash goal; x = +-1 = the side of a Sidestep Slash.
        public static void SendAct(int kind, float speed, Vector3 target)
        {
            if (IsClient) ToServer(new NetW(Msg.Act).U8(kind).F32(speed).Vec(target).Done());
            else if (IsHost) ToAll(new NetW(Msg.Act).U8(LocalPid).U8(kind).F32(speed).Vec(target).Done(), null);
        }

        // Client: my blow touched this enemy.
        public static void SendHit(Enemy e, float damage, float pierce, bool crit, float knock, float stun, Vector3 origin, int src)
        {
            if (!IsClient) return;
            ToServer(new NetW(Msg.Hit).U16(e.netId).F32(damage).F32(pierce).U8(crit ? 1 : 0).F32(knock).F32(stun).Vec(origin).U8(src < 0 ? 255 : src).Done());
        }

        public static void BroadcastEnemyHit(Enemy e, float damage, float toArmor, float toHp, bool crit, bool stun, bool killed, Vector3 away, int byPid)
        {
            if (!IsHost) return;
            ToAll(new NetW(Msg.EnemyHit).U16(e.netId).F32(damage).F32(toArmor).F32(toHp)
                .U8((crit ? 1 : 0) | (stun ? 2 : 0) | (killed ? 4 : 0))
                .U8(Mathf.RoundToInt(Mathf.Clamp(away.x, -1f, 1f) * 127f) & 0xFF).U8(Mathf.RoundToInt(Mathf.Clamp(away.z, -1f, 1f) * 127f) & 0xFF)
                .U8(byPid).F32(e.hp).F32(e.armor).Done(), null);
        }

        public static void SendKnightHit(int pid, float damage, Vector3 from, string source, float knockback)
        {
            Conn c = IsHost ? ConnOf(pid) : null;
            if (c == null) return;
            byte[] m = new NetW(Msg.KnightHit).F32(damage).Vec(from).F32(knockback).Str(source).Done();
            if (pid >= 0 && pid < MaxPlayers) KnightHitsSent[pid]++;
            BytesOut += m.Length;
            c.Send(m);
        }

        public static void SendExp(int pid, float amount, Vector3 at, bool killCredit, int souls)
        {
            Conn c = IsHost ? ConnOf(pid) : null;
            if (c == null) return;
            byte[] m = new NetW(Msg.Exp).F32(amount).Vec(at).U8(killCredit ? 1 : 0).U16(Mathf.Clamp(souls, 0, 65535)).Done();
            BytesOut += m.Length;
            c.Send(m);
        }

        public static void BroadcastPickupTaken(int id, int pid, int kind, int item, Vector3 at, int plus = 0)
        {
            if (IsHost) ToAll(new NetW(Msg.PickupTaken).U16(id).U8(pid).U8(kind).U8(item).Vec(at).U8(plus).Done(), null);
        }

        internal static byte[] BannerMsg(string text, Color color)
        {
            return new NetW(Msg.Banner).Str(text).U8((int)(Mathf.Clamp01(color.r) * 255f)).U8((int)(Mathf.Clamp01(color.g) * 255f)).U8((int)(Mathf.Clamp01(color.b) * 255f)).Done();
        }

        public static void BroadcastBanner(string text, Color color)
        {
            if (IsHost) ToAll(BannerMsg(text, color), null);
        }

        public static void BroadcastReset()
        {
            if (IsHost) ToAll(new NetW(Msg.Reset).Done(), null);
        }

        // Test scripts only: a line of text to every other machine.
        public static void SendTest(string text)
        {
            byte[] m = new NetW(Msg.Test).Str(text).Done();
            if (IsClient) ToServer(m);
            else if (IsHost) ToAll(m, null);
        }
    }
}

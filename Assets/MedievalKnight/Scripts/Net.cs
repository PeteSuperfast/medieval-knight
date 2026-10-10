using UnityEngine;

namespace MedievalKnight
{
    public enum NetMode { Single, Host, Client }

    // Who runs what. Single player and the host simulate the world (Authority); a client only simulates
    // its own knight and shows what the host sends. (The sockets come in the next step; these are the hooks.)
    public static class Net
    {
        public const int DefaultPort = 7777, MaxPlayers = 4, Protocol = 1;

        public static NetMode Mode = NetMode.Single;
        public static int LocalPid;
        public static string LocalName = "Knight";

        public static bool Multiplayer { get { return Mode != NetMode.Single; } }
        public static bool Authority { get { return Mode != NetMode.Client; } }
        public static bool IsHost { get { return Mode == NetMode.Host; } }
        public static bool IsClient { get { return Mode == NetMode.Client; } }

        public static void Poll() { }
        public static void LateTick() { }
        public static void SendAct(int kind, float speed, Vector3 target) { }
        public static void SendHit(Enemy e, float damage, float pierce, bool crit, float knock, float stun, Vector3 origin, int src) { }
        public static void BroadcastEnemyHit(Enemy e, float damage, float toArmor, float toHp, bool crit, bool stun, bool killed, Vector3 away, int byPid) { }
        public static void SendKnightHit(int pid, float damage, Vector3 from, string source, float knockback) { }
        public static void SendExp(int pid, float amount, Vector3 at, bool killCredit) { }
        public static void BroadcastPickupTaken(int id, int pid, int weaponId, Vector3 at) { }
        public static void BroadcastBanner(string text, Color color) { }
        public static void BroadcastReset() { }
    }
}

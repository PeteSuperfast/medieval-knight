using System.Globalization;

namespace MedievalKnight
{
    // Command-line switches (all optional). Normal play uses none of them and starts at the menu.
    //   -selftest <folder>      single-player automated test (SelfTest.cs)
    //   -single                 skip the menu, start single player
    //   -host                   skip the menu, host a game          -port <n>  -name <text>
    //   -join <ip>              skip the menu, join a host          -port <n>  -name <text>
    //   -bind <ip>              host: listen on this address only (the tests use 127.0.0.1)
    //   -netversion <n>         pretend to be another build (handshake test)
    //   -joindelay <seconds>    wait before connecting
    //   -mptest <folder>        scripted multi-process test (MpTest.cs), with -role and -expect <clients>
    //   -menutest <folder>      scripted menu test (MpTest.cs)
    public static class Args
    {
        public static string selfTestDir, mpTestDir, menuTestDir, joinHost, name, bind, role = "";
        public static bool host, single;
        public static int port = Net.DefaultPort, netVersion = Net.Protocol, expect = 1;
        public static float joinDelay;
        static bool parsed;

        // any automated run: keeps running without focus, never touches the saved menu settings
        public static bool Scripted { get { return selfTestDir != null || mpTestDir != null || menuTestDir != null; } }

        public static void Parse()
        {
            if (parsed) return;
            parsed = true;
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++)
            {
                string next = i + 1 < a.Length ? a[i + 1] : null;
                switch (a[i])
                {
                    case "-host": host = true; break;
                    case "-single": single = true; break;
                    case "-selftest": if (next != null) selfTestDir = next; break;
                    case "-mptest": if (next != null) mpTestDir = next; break;
                    case "-menutest": if (next != null) menuTestDir = next; break;
                    case "-join": if (next != null) joinHost = next; break;
                    case "-name": if (next != null) name = next; break;
                    case "-bind": if (next != null) bind = next; break;
                    case "-role": if (next != null) role = next; break;
                    case "-port": Int(next, ref port); break;
                    case "-netversion": Int(next, ref netVersion); break;
                    case "-expect": Int(next, ref expect); break;
                    case "-joindelay":
                        float f;
                        if (next != null && float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) joinDelay = f;
                        break;
                }
            }
        }

        static void Int(string s, ref int target)
        {
            int v;
            if (s != null && int.TryParse(s, out v)) target = v;
        }
    }
}

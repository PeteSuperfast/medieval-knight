using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MedievalKnight
{
    // The start menu: Single Player / Host Game / Join Game, with text fields for the player name, the host's
    // IP address and the port. Built from code like the HUD, on its own canvas above it.
    // There is no EventSystem in this project: buttons and fields are hit-tested by hand against the mouse
    // (InputX), and typing comes from the keyboard's text events (InputX.Typed), so it works with the new
    // Input System alone.
    public class Menu : MonoBehaviour
    {
        class Button
        {
            public string id;
            public RectTransform rt;
            public Image bg;
            public System.Action click;
        }

        class Field
        {
            public string id;
            public RectTransform rt;
            public Image bg;
            public Text text;
            public string value = "";
            public int max = 16;
            public int kind;                 // 0 = name, 1 = digits (port), 2 = address
        }

        public enum Page { Main, Host, Join, Connecting }

        Font font;
        Canvas canvas;
        RectTransform root;
        readonly GameObject[] pages = new GameObject[4];
        readonly List<Button>[] buttons = { new List<Button>(), new List<Button>(), new List<Button>(), new List<Button>() };
        readonly List<Field>[] fields = { new List<Field>(), new List<Field>(), new List<Field>(), new List<Field>() };
        Field nameField, portField, ipField, joinPortField, focus;
        Text message, addresses, connecting;
        float shownAt, nextRepeat;

        public Page Current { get; private set; }
        public bool Visible { get { return canvas != null && canvas.enabled; } }
        public string Message { get { return message.text; } }
        public string PlayerName { get { return nameField.value; } }

        static readonly Color ButtonCol = new Color(0.14f, 0.16f, 0.24f, 0.96f), ButtonHover = new Color(0.3f, 0.36f, 0.56f, 1f);
        static readonly Color FieldCol = new Color(0.05f, 0.05f, 0.08f, 0.96f), FieldFocus = new Color(0.12f, 0.14f, 0.24f, 1f);
        static readonly Color Gold = new Color(1f, 0.88f, 0.35f), Soft = new Color(0.8f, 0.83f, 0.9f);

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            var cgo = new GameObject("MenuCanvas");
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f;
            root = canvas.GetComponent<RectTransform>();

            Image dim = Box("Dim", root, Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.06f, 0.72f));
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = Vector2.zero; dim.rectTransform.offsetMax = Vector2.zero;

            Text title = Label("Title", root, new Vector2(0, 262), new Vector2(1000, 70), 58, "MEDIEVAL KNIGHT");
            title.fontStyle = FontStyle.Bold;
            title.color = Gold;
            Label("Sub", root, new Vector2(0, 214), new Vector2(1000, 28), 18, "Slimes, a Minotaur and five swords  -  alone or online with up to 3 friends").color = Soft;

            for (int i = 0; i < pages.Length; i++)
            {
                var go = new GameObject(((Page)i).ToString(), typeof(RectTransform));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(root, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = Vector2.zero;
                pages[i] = go;
            }

            // --- main ---
            Transform p = pages[(int)Page.Main].transform;
            Label("NameCaption", p, new Vector2(0, 150), new Vector2(400, 24), 16, "Your name (shown above your knight)").color = Soft;
            nameField = MakeField(Page.Main, "name", new Vector2(0, 112), 360, 14, 0);
            MakeButton(Page.Main, "single", "Single Player", new Vector2(0, 40), 360, StartSingle);
            MakeButton(Page.Main, "host", "Host Game", new Vector2(0, -24), 360, () => SetPage(Page.Host, ""));
            MakeButton(Page.Main, "join", "Join Game", new Vector2(0, -88), 360, () => SetPage(Page.Join, ""));
            MakeButton(Page.Main, "quit", "Quit", new Vector2(0, -152), 360, Application.Quit);

            // --- host ---
            p = pages[(int)Page.Host].transform;
            Label("Head", p, new Vector2(0, 160), new Vector2(800, 36), 30, "HOST A GAME").fontStyle = FontStyle.Bold;
            Label("PortCaption", p, new Vector2(-150, 104), new Vector2(300, 24), 16, "Port (TCP)").color = Soft;
            portField = MakeField(Page.Host, "port", new Vector2(60, 104), 160, 5, 1);
            addresses = Label("Addresses", p, new Vector2(0, 22), new Vector2(900, 110), 17, "");
            addresses.supportRichText = true;
            Text help = Label("Help", p, new Vector2(0, -96), new Vector2(980, 110), 14,
                "Same network (LAN / Wi-Fi): friends choose Join Game and type one of the addresses above and the port.\n"
                + "Over the internet: forward this TCP port on your router to this PC and give friends your public IP,\n"
                + "or use a virtual LAN tool (Radmin VPN, ZeroTier, ...) and give them the address that tool shows.\n"
                + "Windows may ask to let the game through the firewall the first time you host - allow it, or nobody can connect.\n"
                + "Up to " + Net.MaxPlayers + " players. Everybody needs the same build of the game.");
            help.color = Soft;
            MakeButton(Page.Host, "start", "Start Hosting", new Vector2(-130, -196), 240, StartHost);
            MakeButton(Page.Host, "back", "Back", new Vector2(130, -196), 240, () => SetPage(Page.Main, ""));

            // --- join ---
            p = pages[(int)Page.Join].transform;
            Label("Head", p, new Vector2(0, 160), new Vector2(800, 36), 30, "JOIN A GAME").fontStyle = FontStyle.Bold;
            Label("IpCaption", p, new Vector2(0, 112), new Vector2(500, 24), 16, "Host IP address  (Ctrl+V pastes)").color = Soft;
            ipField = MakeField(Page.Join, "ip", new Vector2(0, 74), 360, 40, 2);
            Label("PortCaption", p, new Vector2(0, 26), new Vector2(300, 24), 16, "Port").color = Soft;
            joinPortField = MakeField(Page.Join, "port", new Vector2(0, -12), 160, 5, 1);
            Label("Help", p, new Vector2(0, -90), new Vector2(980, 70), 14,
                "Ask the host for the IPv4 address and port on their Host screen (for example 192.168.1.20 and " + Net.DefaultPort + ").\n"
                + "Not on the same network? The host must forward the port, or you both use a virtual LAN tool (Radmin VPN, ZeroTier, ...).").color = Soft;
            MakeButton(Page.Join, "connect", "Connect", new Vector2(-130, -196), 240, Connect);
            MakeButton(Page.Join, "back", "Back", new Vector2(130, -196), 240, () => SetPage(Page.Main, ""));

            // --- connecting ---
            p = pages[(int)Page.Connecting].transform;
            Label("Head", p, new Vector2(0, 120), new Vector2(800, 36), 30, "CONNECTING").fontStyle = FontStyle.Bold;
            connecting = Label("Status", p, new Vector2(0, 40), new Vector2(900, 60), 18, "");
            MakeButton(Page.Connecting, "cancel", "Cancel", new Vector2(0, -120), 240, () => { Net.CancelJoin(); SetPage(Page.Join, ""); });

            message = Label("Message", root, new Vector2(0, -262), new Vector2(1160, 56), 18, "");
            message.color = new Color(1f, 0.62f, 0.45f);
            message.fontStyle = FontStyle.Bold;
            Label("Foot", root, new Vector2(0, -330), new Vector2(1000, 22), 13,
                "Click a button or a text field      Tab: next field      Enter: start hosting / connect      Esc: back / quit").color = new Color(0.62f, 0.65f, 0.72f);

            nameField.value = "Knight";
            portField.value = joinPortField.value = Net.DefaultPort.ToString();
            ipField.value = "";
            if (!Args.Scripted)
            {
                nameField.value = PlayerPrefs.GetString("mk_name", nameField.value);
                ipField.value = PlayerPrefs.GetString("mk_ip", "");
                portField.value = PlayerPrefs.GetString("mk_hostport", portField.value);
                joinPortField.value = PlayerPrefs.GetString("mk_joinport", joinPortField.value);
            }
            if (Args.name != null) nameField.value = Net.CleanName(Args.name, 0);
            SetPage(Page.Main, "");
            Hide();
        }

        // ---------------------------------------------------------------- builders

        RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        Image Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var img = Rect(name, parent, pos, size).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text Label(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, string text)
        {
            var t = Rect(name, parent, pos, size).gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.9f);
            o.effectDistance = new Vector2(1.2f, -1.2f);
            return t;
        }

        void MakeButton(Page page, string id, string text, Vector2 pos, float width, System.Action click)
        {
            Transform p = pages[(int)page].transform;
            Image frame = Box("Button " + id, p, pos, new Vector2(width, 52), new Color(0.75f, 0.62f, 0.3f, 1f));
            var b = new Button { id = id, rt = frame.rectTransform, click = click };
            b.bg = Box("Bg", frame.transform, Vector2.zero, new Vector2(width - 6, 46), ButtonCol);
            Text t = Label("Text", frame.transform, Vector2.zero, new Vector2(width, 46), 22, text);
            t.fontStyle = FontStyle.Bold;
            buttons[(int)page].Add(b);
        }

        Field MakeField(Page page, string id, Vector2 pos, float width, int max, int kind)
        {
            Transform p = pages[(int)page].transform;
            Image frame = Box("Field " + id, p, pos, new Vector2(width, 44), new Color(0.45f, 0.48f, 0.58f, 1f));
            var f = new Field { id = id, rt = frame.rectTransform, max = max, kind = kind };
            f.bg = Box("Bg", frame.transform, Vector2.zero, new Vector2(width - 4, 40), FieldCol);
            f.text = Label("Text", frame.transform, Vector2.zero, new Vector2(width - 16, 40), 22, "");
            fields[(int)page].Add(f);
            return f;
        }

        // ---------------------------------------------------------------- pages

        public void Show(string text)
        {
            canvas.enabled = true;
            shownAt = Time.unscaledTime;
            InputX.Typed();                                   // drop whatever was typed while playing
            // a lost connection comes back to the Join page so it is one click to try again
            SetPage(!string.IsNullOrEmpty(text) && Current == Page.Connecting ? Page.Join : Page.Main, text);
        }

        public void Hide()
        {
            canvas.enabled = false;
            focus = null;
        }

        public void SetPage(Page page, string text)
        {
            Current = page;
            for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == (int)page);
            message.text = text ?? "";
            focus = page == Page.Main ? nameField : page == Page.Host ? portField : page == Page.Join ? ipField : null;
            if (page == Page.Host) RefreshAddresses();
        }

        void RefreshAddresses()
        {
            List<string> list = Net.LocalAddresses();
            var sb = new System.Text.StringBuilder("Friends type one of these IPv4 addresses of this PC:\n");
            if (list.Count == 0) sb.Append("<color=#ffb080>no network adapter found - check your connection</color>");
            for (int i = 0; i < list.Count && i < 4; i++)
            {
                int cut = list[i].IndexOf(' ');
                string ip = cut > 0 ? list[i].Substring(0, cut) : list[i], rest = cut > 0 ? list[i].Substring(cut) : "";
                sb.Append("<color=#ffe066><b>").Append(ip).Append("</b></color>").Append(rest).Append('\n');
            }
            addresses.text = sb.ToString();
        }

        int Port(Field f)
        {
            int v;
            return int.TryParse(f.value, out v) ? v : -1;
        }

        void SavePrefs()
        {
            if (Args.Scripted) return;
            PlayerPrefs.SetString("mk_name", nameField.value);
            PlayerPrefs.SetString("mk_ip", ipField.value);
            PlayerPrefs.SetString("mk_hostport", portField.value);
            PlayerPrefs.SetString("mk_joinport", joinPortField.value);
            PlayerPrefs.Save();
        }

        void StartSingle()
        {
            SavePrefs();
            Net.StartSingle(nameField.value);
            Game.I.StartSession();
        }

        void StartHost()
        {
            int port = Port(portField);
            if (port < 1 || port > 65535) { message.text = "The port must be a number from 1 to 65535 (the usual one is " + Net.DefaultPort + ")."; return; }
            SavePrefs();
            string error = Net.StartHost(port, nameField.value);
            if (error != null) { message.text = error; return; }
            Game.I.StartSession();
        }

        void Connect()
        {
            int port = Port(joinPortField);
            string ip = ipField.value.Trim();
            if (ip.Length == 0) { message.text = "Type the host's IP address first (the host sees it on the Host Game screen)."; focus = ipField; return; }
            if (port < 1 || port > 65535) { message.text = "The port must be a number from 1 to 65535 (the usual one is " + Net.DefaultPort + ")."; focus = joinPortField; return; }
            SavePrefs();
            BeginJoin(ip, port, nameField.value);
        }

        // Also used by the -join command-line switch.
        public void BeginJoin(string host, int port, string name)
        {
            ipField.value = host;
            joinPortField.value = port.ToString();
            ShowConnecting(host, port);
            Net.Join(host, port, name);
        }

        public void ShowConnecting(string host, int port)
        {
            canvas.enabled = true;
            shownAt = Time.unscaledTime;
            SetPage(Page.Connecting, "");
            connecting.text = "Connecting to " + host + " : " + port + " ...";
        }

        public void JoinFailed(string text)
        {
            canvas.enabled = true;
            SetPage(Page.Join, text);
            Debug.Log("MENU join failed: " + text);
        }

        // ---------------------------------------------------------------- test access

        public RectTransform ButtonRect(Page page, string id)
        {
            foreach (Button b in buttons[(int)page]) if (b.id == id) return b.rt;
            return null;
        }

        public RectTransform FieldRect(Page page, string id)
        {
            foreach (Field f in fields[(int)page]) if (f.id == id) return f.rt;
            return null;
        }

        public string FieldValue(Page page, string id)
        {
            foreach (Field f in fields[(int)page]) if (f.id == id) return f.value;
            return null;
        }

        // ---------------------------------------------------------------- input

        static bool Accepts(Field f, char c)
        {
            if (c < 32 || c == 127 || c == '<' || c == '>') return false;
            if (f.kind == 1) return c >= '0' && c <= '9';
            if (f.kind == 2) return char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == ':';
            return true;
        }

        void Type(string s)
        {
            if (focus == null) return;
            foreach (char c in s)
                if (focus.value.Length < focus.max && Accepts(focus, c)) focus.value += c;
        }

        void Update()
        {
            if (!Visible) return;
            string typed = InputX.Typed();
            float now = Time.unscaledTime;
            int page = (int)Current;
            Vector2 m = InputX.MousePos;
            bool click = InputX.HasMouse && InputX.MouseDown(0) && now - shownAt > 0.2f;

            Button pressed = null;
            foreach (Button b in buttons[page])
            {
                bool hover = InputX.HasMouse && RectTransformUtility.RectangleContainsScreenPoint(b.rt, m, null);
                b.bg.color = Color.Lerp(b.bg.color, hover ? ButtonHover : ButtonCol, 0.35f);
                if (hover && click) pressed = b;
            }
            foreach (Field f in fields[page])
            {
                if (click && RectTransformUtility.RectangleContainsScreenPoint(f.rt, m, null)) focus = f;
                f.bg.color = f == focus ? FieldFocus : FieldCol;
                bool caret = f == focus && Mathf.Repeat(now, 1f) < 0.55f;
                f.text.text = f.value + (caret ? "|" : f == focus ? " " : "");
            }

            if (focus != null)
            {
                bool ctrl = InputX.Held(KeyCode.LeftControl) || InputX.Held(KeyCode.RightControl);
                if (ctrl && InputX.Down(KeyCode.V)) Type(GUIUtility.systemCopyBuffer ?? "");
                else if (!ctrl) Type(typed);

                bool erase = false;
                if (InputX.Down(KeyCode.Backspace)) { erase = true; nextRepeat = now + 0.4f; }
                else if (InputX.Held(KeyCode.Backspace) && now >= nextRepeat) { erase = true; nextRepeat = now + 0.05f; }
                if (erase && focus.value.Length > 0) focus.value = focus.value.Substring(0, focus.value.Length - 1);

                if (InputX.Down(KeyCode.Tab) && fields[page].Count > 1)
                    focus = fields[page][(fields[page].IndexOf(focus) + 1) % fields[page].Count];
            }

            if (pressed != null) { pressed.click(); return; }
            if (InputX.Down(KeyCode.Return) || InputX.Down(KeyCode.KeypadEnter))
            {
                if (Current == Page.Host) StartHost();
                else if (Current == Page.Join) Connect();
                return;
            }
            if (InputX.Down(KeyCode.Escape) && now - shownAt > 0.3f)
            {
                if (Current == Page.Main) Application.Quit();
                else
                {
                    if (Current == Page.Connecting) Net.CancelJoin();
                    SetPage(Current == Page.Connecting ? Page.Join : Page.Main, "");
                }
            }
        }
    }
}

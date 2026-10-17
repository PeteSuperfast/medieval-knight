using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MedievalKnight
{
    // Phase 7: the M map. A square picture of the 380 x 380 m world (north up, like the screen) with the knight, his friends
    // (online), the portal, the loot piles of this world, the Minotaur's lair and the start town. It never pauses the game and takes
    // no clicks. It draws only what this machine already knows (no network message exists for it) - never enemies.
    // The ground is a fixed table (MapLayout.ZoneAt) that is the same in every world, so the picture is made once.
    public class MapUi : MonoBehaviour
    {
        const float MapPx = 520f;                    // the picture on screen (380 m)
        const int TexSize = 190;                     // 2 m per texel
        const int MaxPiles = 24, MaxFriends = 4;

        static readonly Color Gold = new Color(0.961f, 0.761f, 0.353f), Red = new Color(0.784f, 0.282f, 0.227f), Blue = new Color(0.082f, 0.58f, 0.733f),
            Sand = new Color(0.847f, 0.8f, 0.69f);

        Game g;
        Font font;
        RectTransform panel, mapRect;
        Texture2D mapTex;                            // the ground of the biome showing now
        readonly Texture2D[] texByBiome = new Texture2D[2];   // one picture per biome, made the first time that biome's map opens
        RawImage groundImage;
        Image you;
        Image portal, lair, town;
        Text lairText, soundText;
        readonly Image[] friends = new Image[MaxFriends];
        readonly Text[] friendNames = new Text[MaxFriends];
        readonly Image[] piles = new Image[MaxPiles];
        readonly Text[] pileText = new Text[MaxPiles];
        readonly List<Vector3> pilePos = new List<Vector3>();
        readonly List<int> pileCount = new List<int>();
        Sprite sTriangle, sDisc, sRing, sDiamond, sCross, sSquare;
        bool built;

        // test read-outs (SelfTest / MpTest): how many markers of each kind are showing
        public bool Shown { get { return built && panel.gameObject.activeSelf; } }
        public int FriendMarkers { get { return CountActive(friends); } }
        public int PileMarkers { get { return CountActive(piles); } }
        public bool PortalMarker { get { return built && portal.gameObject.activeSelf; } }
        public bool YouMarker { get { return built && you.gameObject.activeSelf; } }
        public int EnemyMarkers { get { return 0; } }         // there is no such marker (decisions 6)
        public Texture2D MapTexture { get { Build(); UseGroundOfCurrentBiome(); return mapTex; } }

        // The ground picture follows MapLayout.Current (the table behind ZoneAt is fixed per biome, so each picture is made once).
        void UseGroundOfCurrentBiome()
        {
            int b = (int)MapLayout.Current;
            if (texByBiome[b] == null)
            {
                var t = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                var cols = new Color[TexSize * TexSize];
                for (int y = 0; y < TexSize; y++)
                    for (int x = 0; x < TexSize; x++)
                    {
                        Vector2 w = TexelToWorld(x, y);
                        cols[y * TexSize + x] = ZoneColor(MapLayout.ZoneAt(w.x, w.y));
                    }
                t.SetPixels(cols);
                t.Apply();
                texByBiome[b] = t;
            }
            mapTex = texByBiome[b];
            if (groundImage != null && groundImage.texture != mapTex) groundImage.texture = mapTex;
        }

        static int CountActive(Image[] a)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++) if (a[i] != null && a[i].gameObject.activeSelf) n++;
            return n;
        }

        public static MapUi Create(Transform parent, Game game)
        {
            var go = new GameObject("MapUi");
            go.transform.SetParent(parent, false);
            var ui = go.AddComponent<MapUi>();
            ui.g = game;
            return ui;
        }

        // ---------------------------------------------------------------- building (once, the first time the map opens)

        public static Color ZoneColor(Zone z)
        {
            switch (z)
            {
                case Zone.Town: return Sand;
                case Zone.Meadow: return new Color32(0x8D, 0xB0, 0x4A, 255);
                case Zone.AutumnForest: return new Color32(0xC7, 0x84, 0x3A, 255);
                case Zone.DarkPineForest: return new Color32(0x46, 0x58, 0x20, 255);
                case Zone.Farmland: return new Color32(0xA0, 0xB8, 0x5B, 255);
                case Zone.Lake: return new Color32(0x15, 0x94, 0xBB, 255);
                case Zone.Mountain: return new Color32(0xAD, 0xAB, 0xB7, 255);
                case Zone.Lair: return new Color32(0x7A, 0x3A, 0x30, 255);
                // the snow map (style.md palette): meadow, pine valley, cliffs, low snow, ice plateau, castle
                case Zone.SnowMeadow: return new Color32(0x8D, 0xB0, 0x4A, 255);
                case Zone.SnowPine: return new Color32(0x6C, 0x85, 0x35, 255);
                case Zone.SnowCliff: return new Color32(0x95, 0x8A, 0x80, 255);
                case Zone.SnowField: return new Color32(0xEE, 0xF3, 0xF8, 255);
                case Zone.SnowPlateau: return new Color32(0xBF, 0xE6, 0xF2, 255);
                case Zone.SnowCastle: return new Color32(0x8F, 0xD0, 0xE4, 255);
            }
            return Color.magenta;
        }

        // texel (px, py) -> world point at its centre (py counts up = north)
        public static Vector2 TexelToWorld(int px, int py)
        {
            return new Vector2((px + 0.5f) * (2f * MapLayout.Half / TexSize) - MapLayout.Half, (py + 0.5f) * (2f * MapLayout.Half / TexSize) - MapLayout.Half);
        }

        Sprite MakeSprite(int n, System.Func<float, float, float> alpha)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;      // -1..1, up = +v
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v)));
                }
            t.SetPixels(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        void Build()
        {
            if (built) return;
            built = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            UseGroundOfCurrentBiome();                               // the ground

            sTriangle = MakeSprite(32, (u, v) => (v > -0.9f && Mathf.Abs(u) < (0.9f - v) * 0.5f) ? 1f : 0f);
            sDisc = MakeSprite(32, (u, v) => (1f - Mathf.Sqrt(u * u + v * v)) * 12f);
            sRing = MakeSprite(32, (u, v) => { float r = Mathf.Sqrt(u * u + v * v); return Mathf.Min((1f - r) * 12f, (r - 0.55f) * 12f); });
            sDiamond = MakeSprite(32, (u, v) => (1f - (Mathf.Abs(u) + Mathf.Abs(v))) * 8f);
            sCross = MakeSprite(32, (u, v) => { float a = Mathf.Abs(u - v), b = Mathf.Abs(u + v); return (Mathf.Min(a, b) < 0.28f && Mathf.Abs(u) < 0.95f && Mathf.Abs(v) < 0.95f) ? 1f : 0f; });
            sSquare = MakeSprite(32, (u, v) => (Mathf.Abs(u) < 0.8f && Mathf.Abs(v) < 0.8f) ? 1f : 0f);

            var cgo = new GameObject("Canvas");
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;                               // above the HUD (100), below TeamUi (140) and LoopUi (150)
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f;
            RectTransform root = canvas.GetComponent<RectTransform>();

            Vector2 mid = new Vector2(0.5f, 0.5f);
            Image frame = Box("Frame", root, mid, Vector2.zero, new Vector2(566f, 646f), Sand);
            panel = frame.rectTransform;
            Box("Bg", panel, mid, Vector2.zero, new Vector2(560f, 640f), new Color(0.227f, 0.227f, 0.227f, 0.9f));

            Text title = Label("Title", panel, new Vector2(0f, 1f), new Vector2(20f, -22f), 20, TextAnchor.MiddleLeft);
            title.text = "MAP";
            title.fontStyle = FontStyle.Bold;
            soundText = Label("Sound", panel, new Vector2(1f, 1f), new Vector2(-20f, -22f), 16, TextAnchor.MiddleRight);

            var map = new GameObject("Ground", typeof(RectTransform));
            mapRect = map.GetComponent<RectTransform>();
            mapRect.SetParent(panel, false);
            mapRect.anchorMin = mapRect.anchorMax = mapRect.pivot = mid;
            mapRect.anchoredPosition = new Vector2(0f, 4f);
            mapRect.sizeDelta = new Vector2(MapPx, MapPx);
            var raw = map.AddComponent<RawImage>();
            raw.texture = mapTex;
            raw.raycastTarget = false;
            groundImage = raw;

            Text legend = Label("Legend", panel, new Vector2(0.5f, 0f), new Vector2(0f, 38f), 14, TextAnchor.MiddleCenter);
            legend.text = "<color=#FFFFFF>^</color> you   <color=#9DD4FF>o</color> friend   <color=#E05050>x</color> down   <color=#F5C25A>*</color> loot   <color=#28A8E0>O</color> portal   LAIR   TOWN";
            Text keys = Label("Keys", panel, new Vector2(0.5f, 0f), new Vector2(0f, 16f), 14, TextAnchor.MiddleCenter);
            keys.text = "M / Esc  close        F2  sound";
            keys.color = new Color(0.85f, 0.85f, 0.85f);

            town = Marker("Town", sSquare, 12f, new Color(0.36f, 0.24f, 0.12f));
            Text townText = Label("TownText", town.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -13f), 12, TextAnchor.MiddleCenter);
            townText.text = "START";
            lair = Marker("Lair", sDiamond, 14f, new Color(0.55f, 0.1f, 0.08f));
            lairText = Label("LairText", lair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), 12, TextAnchor.MiddleCenter);
            portal = Marker("Portal", sRing, 22f, Blue);
            for (int i = 0; i < MaxPiles; i++)
            {
                piles[i] = Marker("Pile" + i, sDiamond, 11f, Gold);
                pileText[i] = Label("Count", piles[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), 12, TextAnchor.MiddleLeft);
                pileText[i].color = Gold;
            }
            for (int i = 0; i < MaxFriends; i++)
            {
                friends[i] = Marker("Friend" + i, sDisc, 12f, Color.white);
                friendNames[i] = Label("Name", friends[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), 12, TextAnchor.MiddleCenter);
            }
            you = Marker("You", sTriangle, 18f, Color.white);
            var outline = you.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            panel.gameObject.SetActive(false);
        }

        Image Box(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // A marker lives under the ground picture, positioned in picture pixels from its centre.
        Image Marker(string name, Sprite sprite, float size, Color color)
        {
            Image img = Box(name, mapRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), color);
            img.sprite = sprite;
            img.gameObject.SetActive(false);
            return img;
        }

        Text Label(string name, Transform parent, Vector2 anchor, Vector2 pos, int fontSize, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(540f, 24f);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.9f);
            o.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        // ---------------------------------------------------------------- show / update

        static Vector2 ToPx(Vector3 world)
        {
            return new Vector2(world.x / (2f * MapLayout.Half) * MapPx, world.z / (2f * MapLayout.Half) * MapPx);
        }

        void Place(Image img, Vector3 world)
        {
            Vector2 p = ToPx(world);
            p.x = Mathf.Clamp(p.x, -MapPx * 0.5f, MapPx * 0.5f);
            p.y = Mathf.Clamp(p.y, -MapPx * 0.5f, MapPx * 0.5f);
            img.rectTransform.anchoredPosition = p;
            img.gameObject.SetActive(true);
        }

        // Test hook: builds the picture if needed and moves the markers although the panel is hidden.
        public void Probe() { Build(); Tick(); }

        public void Show(bool on)
        {
            if (on) Build();
            if (!built) return;
            if (panel.gameObject.activeSelf != on) panel.gameObject.SetActive(on);
            if (on) Tick();
        }

        // Every frame while it is showing: move the markers to where this machine knows things are.
        public void Tick()
        {
            if (!built || g == null || g.knight == null) return;
            UseGroundOfCurrentBiome();
            Knight me = g.knight;
            Place(you, me.Pos);
            you.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -me.transform.eulerAngles.y);

            Place(town, MapLayout.TownSpawn);
            Place(lair, MapLayout.Lair);
            Vector3 d = MapLayout.Lair - me.Pos; d.y = 0f;
            lairText.text = "LAIR " + Mathf.RoundToInt(d.magnitude) + " m";
            soundText.text = "Sound " + Sfx.I.VolumeLabel;

            // friends: another player's knight on this machine; down (waiting for a revive) = a red cross, fallen for good = not shown
            int f = 0;
            var ps = g.players;
            for (int i = 0; i < ps.Count && f < MaxFriends; i++)
            {
                Knight p = ps[i];
                if (!p.remote || p.Fallen) continue;
                Image m = friends[f];
                m.sprite = p.Dead ? sCross : sDisc;
                m.color = p.Dead ? Red : Knight.UiColor(p.pid);
                m.rectTransform.sizeDelta = p.Dead ? new Vector2(14f, 14f) : new Vector2(12f, 12f);
                friendNames[f].text = p.playerName;
                Place(m, p.Pos);
                f++;
            }
            for (; f < MaxFriends; f++) friends[f].gameObject.SetActive(false);

            // the portal and the loot piles (loop worlds only; both come from messages this machine got anyway)
            bool loop = g.Loop && g.run != null;
            if (loop && g.run.portal != null) Place(portal, g.run.portal.transform.position); else portal.gameObject.SetActive(false);
            pilePos.Clear(); pileCount.Clear();
            if (loop) g.run.PileMarks(pilePos, pileCount);
            for (int i = 0; i < MaxPiles; i++)
            {
                if (i < pilePos.Count)
                {
                    Place(piles[i], pilePos[i]);
                    pileText[i].text = "x" + pileCount[i];
                }
                else piles[i].gameObject.SetActive(false);
            }
        }
    }
}

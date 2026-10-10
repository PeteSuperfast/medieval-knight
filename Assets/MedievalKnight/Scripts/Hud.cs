using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MedievalKnight
{
    // All UI, built with uGUI from code: health / armor / stamina / EXP bars, upgrade totals,
    // skill cooldown icons, boss bar, banners, floating damage numbers, the controls panel
    // and the level-up "pick 1 of 3" panel.
    public class Hud : MonoBehaviour
    {
        class BarUi
        {
            public RectTransform fill, ghost;
            public Image fillImg, frame;
            public Text value, label;
            public float shown = 1f, ghostShown = 1f;
            public Color color;
        }

        class PopupUi
        {
            public Text text;
            public Vector3 world;
            public float life, maxLife;
            public Color color;
        }

        class SlotUi
        {
            public Image bg, icon;
            public RectTransform cool;
            public Text key, name, cdText, cost;
        }

        class CardUi
        {
            public RectTransform rt;
            public Image border, bg;
            public Text key, title, line, detail;
        }

        class RowUi
        {
            public RectTransform rt;
            public Image bg, swatch;
            public Text key, name, stats, state;
        }

        // one line of the party list: another player's colour, name, level and health
        class PartyUi
        {
            public GameObject go;
            public Image swatch;
            public RectTransform fill;
            public Text name, level, value;
        }

        readonly PartyUi[] party = new PartyUi[Net.MaxPlayers - 1];
        readonly List<Text> tags = new List<Text>();           // name tags above the knights (online)
        GameObject partyRoot;
        Image partyPanel, levelDim, invDim;
        Text netText, invFoot;

        Font font;
        Canvas canvas;
        RectTransform root, popupRoot;
        BarUi hpBar, armorBar, staminaBar, expBar, bossBar;
        Text statsText, bannerText, actionText, noticeText, controlsText, bossText, levelTitle, levelSub, invSlot, invStats;
        GameObject controlsPanel, bossPanel, levelRoot, invRoot;
        readonly Image[] staminaTicks = new Image[10];
        float lastStamina, tickFlashAt = -10f;
        int tickFlash = -1;
        readonly SlotUi[] slots = new SlotUi[4];
        readonly CardUi[] cards = new CardUi[3];
        readonly RowUi[] rows = new RowUi[Weapons.All.Length];
        bool showControls = true;
        float bannerTime, actionTime, noticeTime, levelOpenedAt, invOpenedAt;
        Color bannerColor, actionColor, noticeColor;
        readonly List<PopupUi> popups = new List<PopupUi>();
        readonly Stack<PopupUi> popupPool = new Stack<PopupUi>();

        public bool LevelUpVisible { get { return levelRoot.activeSelf; } }
        public bool InventoryVisible { get { return invRoot.activeSelf; } }
        public string NoticeShown { get { return noticeTime > 0f ? noticeText.text : ""; } }
        public RectTransform CardRect(int i) { return cards[i].rt; }
        public RectTransform RowRect(int i) { return rows[i].rt; }
        public float ScaleFactor { get { return canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f; } }

        static readonly Color PanelCol = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color FrameCol = new Color(0.06f, 0.06f, 0.08f, 0.95f);

        public static Color StatColor(StatKind s)
        {
            return s == StatKind.Damage ? new Color(1f, 0.42f, 0.32f) : s == StatKind.Speed ? new Color(1f, 0.86f, 0.3f) : new Color(0.8f, 0.55f, 1f);
        }

        // same colours as the three bars
        public static Color AttrColor(Attr a)
        {
            return a == Attr.Hp ? new Color(0.95f, 0.32f, 0.3f) : a == Attr.Block ? new Color(0.55f, 0.74f, 1f) : new Color(1f, 0.82f, 0.28f);
        }

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            var cgo = new GameObject("Canvas");
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f;
            root = canvas.GetComponent<RectTransform>();

            popupRoot = Rect("Popups", Vector2.zero, Vector2.zero, Vector2.zero);

            // --- knight bars + level / EXP, top-left ---
            Panel("BarsPanel", new Vector2(0, 1), new Vector2(16, -16), new Vector2(400, 146), PanelCol);
            hpBar = MakeBar("HEALTH", new Vector2(0, 1), new Vector2(116, -28), 288, new Color(0.86f, 0.2f, 0.2f));
            armorBar = MakeBar("ARMOR", new Vector2(0, 1), new Vector2(116, -60), 288, new Color(0.5f, 0.68f, 0.92f));
            staminaBar = MakeBar("STAMINA", new Vector2(0, 1), new Vector2(116, -92), 288, new Color(0.95f, 0.78f, 0.22f));
            expBar = MakeBar("LV 1", new Vector2(0, 1), new Vector2(116, -124), 288, new Color(0.66f, 0.4f, 0.95f));
            expBar.label.fontStyle = FontStyle.Bold;
            expBar.label.color = new Color(1f, 0.9f, 0.45f);
            expBar.label.fontSize = 18;

            // thin dividers on the stamina bar every 25 points: one section = one dash
            for (int i = 0; i < staminaTicks.Length; i++)
            {
                Image t = Panel("Tick" + (i + 1), Vector2.zero, Vector2.zero, new Vector2(2f, -8f), new Color(0f, 0f, 0f, 0.7f), staminaBar.frame.transform);
                RectTransform tr = t.rectTransform;
                tr.pivot = new Vector2(0.5f, 0.5f);
                tr.anchorMin = new Vector2(0f, 0f);
                tr.anchorMax = new Vector2(0f, 1f);
                staminaTicks[i] = t;
            }

            // --- upgrade totals, under the bars ---
            Panel("StatsPanel", new Vector2(0, 1), new Vector2(16, -168), new Vector2(400, 152), PanelCol);
            statsText = MakeText("Stats", new Vector2(0, 1), new Vector2(28, -174), new Vector2(380, 142), 13, TextAnchor.UpperLeft);
            statsText.supportRichText = true;

            // --- online: connection line and the other players, under the stats ---
            RectTransform pr = Rect("Party", new Vector2(0, 1), Vector2.zero, Vector2.zero);
            partyRoot = pr.gameObject;
            partyPanel = Panel("PartyPanel", new Vector2(0, 1), new Vector2(16, -326), new Vector2(400, 110), PanelCol, pr);
            netText = MakeText("NetStatus", new Vector2(0, 1), new Vector2(28, -330), new Vector2(380, 20), 13, TextAnchor.MiddleLeft, pr);
            netText.color = new Color(0.75f, 0.95f, 0.8f);
            for (int i = 0; i < party.Length; i++)
            {
                var u = new PartyUi();
                float y = -354f - i * 26f;
                RectTransform row = Rect("PartyRow" + i, new Vector2(0, 1), Vector2.zero, Vector2.zero, pr);
                u.go = row.gameObject;
                u.swatch = Panel("Swatch", new Vector2(0, 1), new Vector2(28, y - 2), new Vector2(10, 18), Color.white, row);
                u.name = MakeText("Name", new Vector2(0, 1), new Vector2(44, y), new Vector2(132, 22), 14, TextAnchor.MiddleLeft, row);
                u.name.fontStyle = FontStyle.Bold;
                u.level = MakeText("Level", new Vector2(0, 1), new Vector2(180, y), new Vector2(50, 22), 14, TextAnchor.MiddleLeft, row);
                u.level.color = new Color(1f, 0.9f, 0.45f);
                Image frame = Panel("Bar", new Vector2(0, 1), new Vector2(232, y - 2), new Vector2(172, 18), FrameCol, row);
                u.fill = Stretch(Panel("Fill", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.86f, 0.2f, 0.2f), frame.transform));
                u.value = MakeText("Value", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(164, 18), 12, TextAnchor.MiddleCenter, frame.transform);
                party[i] = u;
            }
            partyRoot.SetActive(false);

            // --- boss bar, top-centre ---
            bossPanel = Panel("BossPanel", new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(380, 50), PanelCol).gameObject;
            bossText = MakeText("BossText", new Vector2(0.5f, 1), new Vector2(0, -3), new Vector2(360, 20), 14, TextAnchor.MiddleCenter, bossPanel.transform);
            bossText.fontStyle = FontStyle.Bold;
            bossBar = MakeBar(null, new Vector2(0.5f, 1), new Vector2(0, -25), 360, new Color(0.8f, 0.25f, 0.15f), bossPanel.transform);

            // --- banner + last action ---
            bannerText = MakeText("Banner", new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(800, 50), 30, TextAnchor.MiddleCenter);
            bannerText.fontStyle = FontStyle.Bold;
            bannerText.text = "";
            actionText = MakeText("Action", new Vector2(0.5f, 0), new Vector2(0, 132), new Vector2(600, 30), 20, TextAnchor.MiddleCenter);
            actionText.fontStyle = FontStyle.Bold;
            actionText.text = "";

            // --- controls, top-right ---
            controlsPanel = Panel("ControlsPanel", new Vector2(1, 1), new Vector2(-16, -16), new Vector2(390, 246), PanelCol).gameObject;
            controlsText = MakeText("Controls", new Vector2(1, 1), new Vector2(-26, -22), new Vector2(370, 232), 14, TextAnchor.UpperLeft);
            controlsText.text =
                "CONTROLS   (F1 hides this)\n"
                + "WASD / Arrows    Move\n"
                + "Mouse    Aim - the knight faces the cursor\n"
                + "Left Click / J    Sword combo\n"
                + "     Side > Chop > Rising > Spin > Thrust\n"
                + "Left Shift (hold)    Run\n"
                + "Space    Dash - dodge, 0.5 s untouchable (25 stamina)\n"
                + "Right Click (hold)    Shield block\n"
                + "Q  Whirlwind          E  Dash Thrust\n"
                + "R  Shield Bash        F  Ground Slam\n"
                + "1 / 2 / 3 or Click    Level-up card, then stat point\n"
                + "Tab  Inventory + stats (1 - 5 / Click equips)\n"
                + "Walk over a dropped sword to take it\n"
                + "M  Sound volume / mute\n"
                + "H  Test hit   F5  Restart (online: host)   Esc  Menu";

            // --- skill bar, bottom-centre ---
            for (int i = 0; i < 4; i++) slots[i] = MakeSlot(i);

            BuildLevelPanel();
            BuildInventoryPanel();

            // --- pick-up / equip / volume notice: above the last-action line, on top of the panels ---
            noticeText = MakeText("Notice", new Vector2(0.5f, 0), new Vector2(0, 148), new Vector2(900, 30), 21, TextAnchor.MiddleCenter);
            noticeText.fontStyle = FontStyle.Bold;
            noticeText.text = "";
        }

        // ---------------------------------------------------------------- builders

        RectTransform Rect(string name, Vector2 anchor, Vector2 pos, Vector2 size, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent != null ? parent : root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        Image Panel(string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Transform parent = null)
        {
            var img = Rect(name, anchor, pos, size, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text MakeText(string name, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Transform parent = null)
        {
            var t = Rect(name, anchor, pos, size, parent).gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.9f);
            o.effectDistance = new Vector2(1.2f, -1.2f);
            return t;
        }

        // A framed bar whose top-left (or top-centre) corner sits at pos. label == null: no label on the left.
        BarUi MakeBar(string label, Vector2 anchor, Vector2 pos, float width, Color color, Transform parent = null)
        {
            var b = new BarUi { color = color };
            if (label != null)
            {
                b.label = MakeText(label + "Label", anchor, new Vector2(pos.x - 88f, pos.y), new Vector2(90, 24), 15, TextAnchor.MiddleLeft, parent);
                b.label.text = label;
            }
            b.frame = Panel((label ?? "Boss") + "Bar", anchor, pos, new Vector2(width, 24), FrameCol, parent);
            Transform frame = b.frame.transform;
            b.ghost = Stretch(Panel("Ghost", Vector2.zero, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.75f), frame));
            b.fillImg = Panel("Fill", Vector2.zero, Vector2.zero, Vector2.zero, color, frame);
            b.fill = Stretch(b.fillImg);
            b.value = MakeText("Value", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width - 8, 24), 14, TextAnchor.MiddleCenter, frame);
            return b;
        }

        static RectTransform Stretch(Image img)
        {
            RectTransform rt = img.rectTransform;
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(3, 3);
            rt.offsetMax = new Vector2(-3, -3);
            return rt;
        }

        static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        SlotUi MakeSlot(int i)
        {
            SkillDef sk = Knight.Skills[i];
            var s = new SlotUi();
            float x = (i - 1.5f) * 104f;
            s.bg = Panel("Slot" + sk.key, new Vector2(0.5f, 0), new Vector2(x, 16), new Vector2(96, 96), FrameCol);
            Transform p = s.bg.transform;
            s.icon = Panel("Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88, 88), sk.color * 0.55f, p);
            s.cool = Panel("Cooldown", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.72f), p).rectTransform;
            s.cool.pivot = new Vector2(0.5f, 0f);
            s.cool.anchorMin = new Vector2(0f, 0f);
            s.cool.anchorMax = new Vector2(1f, 0f);
            s.cool.offsetMin = new Vector2(4, 4);
            s.cool.offsetMax = new Vector2(-4, 4);
            s.key = MakeText("Key", new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(90, 34), 28, TextAnchor.MiddleCenter, p);
            s.key.fontStyle = FontStyle.Bold;
            s.key.text = sk.key;
            s.name = MakeText("Name", new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(96, 18), 13, TextAnchor.MiddleCenter, p);
            s.name.text = sk.name;
            s.cost = MakeText("Cost", new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(96, 16), 11, TextAnchor.MiddleCenter, p);
            s.cdText = MakeText("Cd", new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(90, 30), 24, TextAnchor.MiddleCenter, p);
            s.cdText.fontStyle = FontStyle.Bold;
            s.cdText.text = "";
            return s;
        }

        void BuildLevelPanel()
        {
            RectTransform lr = Rect("LevelUp", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Fill(lr);
            levelRoot = lr.gameObject;
            levelDim = Panel("Dim", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0.04f, 0.68f), lr);
            Fill(levelDim.rectTransform);
            levelTitle = MakeText("Title", new Vector2(0.5f, 0.5f), new Vector2(0, 215), new Vector2(900, 60), 46, TextAnchor.MiddleCenter, lr);
            levelTitle.fontStyle = FontStyle.Bold;
            levelTitle.color = new Color(1f, 0.88f, 0.35f);
            levelSub = MakeText("Sub", new Vector2(0.5f, 0.5f), new Vector2(0, 168), new Vector2(900, 30), 20, TextAnchor.MiddleCenter, lr);

            for (int i = 0; i < 3; i++)
            {
                var c = new CardUi();
                c.border = Panel("Card" + (i + 1), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 340f, -10), new Vector2(312, 250), Color.white, lr);
                c.rt = c.border.rectTransform;
                c.bg = Panel("Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(302, 240), new Color(0.09f, 0.09f, 0.13f, 1f), c.rt);
                c.key = MakeText("Key", new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(280, 40), 34, TextAnchor.MiddleCenter, c.rt);
                c.key.fontStyle = FontStyle.Bold;
                c.key.text = "[ " + (i + 1) + " ]";
                c.title = MakeText("Title", new Vector2(0.5f, 1), new Vector2(0, -62), new Vector2(290, 28), 19, TextAnchor.MiddleCenter, c.rt);
                c.title.fontStyle = FontStyle.Bold;
                c.line = MakeText("Line", new Vector2(0.5f, 1), new Vector2(0, -98), new Vector2(290, 40), 27, TextAnchor.MiddleCenter, c.rt);
                c.line.fontStyle = FontStyle.Bold;
                c.detail = MakeText("Detail", new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(290, 60), 16, TextAnchor.UpperCenter, c.rt);
                c.detail.color = new Color(0.85f, 0.87f, 0.92f);
                var foot = MakeText("Foot", new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(290, 20), 13, TextAnchor.MiddleCenter, c.rt);
                foot.color = new Color(0.7f, 0.72f, 0.78f);
                foot.text = "press " + (i + 1) + " or click";
                cards[i] = c;
            }
            levelRoot.SetActive(false);
        }

        // The I panel: one row per sword (owned or not) and the stat-point totals underneath.
        void BuildInventoryPanel()
        {
            RectTransform ir = Rect("Inventory", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Fill(ir);
            invRoot = ir.gameObject;
            invDim = Panel("Dim", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0.04f, 0.6f), ir);
            Fill(invDim.rectTransform);
            Image frame = Panel("Frame", new Vector2(0.5f, 0.5f), new Vector2(0, 42), new Vector2(836, 446), new Color(0.75f, 0.62f, 0.3f, 1f), ir);
            RectTransform p = Panel("Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(830, 440), new Color(0.07f, 0.07f, 0.1f, 1f), frame.transform).rectTransform;

            Text title = MakeText("Title", new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(800, 34), 26, TextAnchor.MiddleCenter, p);
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(1f, 0.88f, 0.35f);
            title.text = "KNIGHT  -  WEAPONS & STATS";
            invSlot = MakeText("Slot", new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(800, 22), 16, TextAnchor.MiddleCenter, p);
            invSlot.supportRichText = true;

            for (int i = 0; i < rows.Length; i++)
            {
                var r = new RowUi();
                r.bg = Panel("Row" + (i + 1), new Vector2(0.5f, 1), new Vector2(0, -76 - i * 56), new Vector2(798, 50), Color.white, p);
                r.rt = r.bg.rectTransform;
                r.key = MakeText("Key", new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(44, 40), 22, TextAnchor.MiddleCenter, r.rt);
                r.key.fontStyle = FontStyle.Bold;
                r.key.text = "[" + (i + 1) + "]";
                r.swatch = Panel("Swatch", new Vector2(0, 0.5f), new Vector2(62, 0), new Vector2(12, 36), Color.white, r.rt);
                r.name = MakeText("Name", new Vector2(0, 1), new Vector2(86, -4), new Vector2(460, 22), 17, TextAnchor.MiddleLeft, r.rt);
                r.name.fontStyle = FontStyle.Bold;
                r.name.supportRichText = true;
                r.stats = MakeText("Stats", new Vector2(0, 1), new Vector2(86, -27), new Vector2(520, 18), 13, TextAnchor.MiddleLeft, r.rt);
                r.state = MakeText("State", new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(200, 40), 14, TextAnchor.MiddleRight, r.rt);
                r.state.fontStyle = FontStyle.Bold;
                rows[i] = r;
            }

            invStats = MakeText("Points", new Vector2(0.5f, 1), new Vector2(0, -362), new Vector2(798, 44), 14, TextAnchor.UpperLeft, p);
            invStats.supportRichText = true;
            invFoot = MakeText("Foot", new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(800, 20), 13, TextAnchor.MiddleCenter, p);
            invFoot.color = new Color(0.7f, 0.72f, 0.78f);
            invRoot.SetActive(false);
        }

        // ---------------------------------------------------------------- public API

        // Longer-lived line for things the player should not miss: sword picked up, equipped, sound volume.
        public void Notice(string text, Color color)
        {
            noticeText.text = text;
            noticeColor = color;
            noticeTime = 3.5f;
        }

        public void Banner(string text, Color color)
        {
            bannerText.text = text;
            bannerColor = color;
            bannerTime = 1.6f;
        }

        // Small line above the skill bar: the attack / skill just used.
        public void Action(string text, Color color)
        {
            actionText.text = text;
            actionColor = color;
            actionTime = 0.9f;
        }

        public void ToggleControls() { showControls = !showControls; }

        // The whole in-game HUD is hidden while the start menu is up.
        public void SetVisible(bool on) { canvas.enabled = on; }

        // Floating text at a world position (damage numbers, BLOCK, +EXP...).
        public void Popup(Vector3 world, string text, Color color, int size)
        {
            PopupUi p;
            if (popupPool.Count > 0) p = popupPool.Pop();
            else
            {
                p = new PopupUi();
                p.text = MakeText("Popup", Vector2.zero, Vector2.zero, new Vector2(200, 40), size, TextAnchor.MiddleCenter, popupRoot);
                p.text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                p.text.fontStyle = FontStyle.Bold;
            }
            p.text.gameObject.SetActive(true);
            p.text.text = text;
            p.text.fontSize = size;
            p.world = world;
            p.color = color;
            p.maxLife = p.life = 0.85f;
            popups.Add(p);
        }

        public void ClearPopups()
        {
            foreach (PopupUi p in popups) { p.text.gameObject.SetActive(false); popupPool.Push(p); }
            popups.Clear();
        }

        public void ShowLevelUp(UpgradeOffer[] offers, int newLevel)
        {
            levelRoot.SetActive(true);
            levelOpenedAt = Time.unscaledTime;
            bannerTime = 0f; bannerText.text = "";
            levelTitle.text = "LEVEL UP!   Lv " + newLevel;
            levelSub.text = "Choose 1 of 3 upgrades   (keys 1 / 2 / 3 or click a card)" + OnlineNote();
            DimFor(levelDim, 0.68f);
            for (int i = 0; i < 3; i++)
            {
                bool on = i < offers.Length;
                cards[i].rt.gameObject.SetActive(on);
                if (!on) continue;
                UpgradeOffer o = offers[i];
                Color col = StatColor(o.stat);
                cards[i].border.color = col;
                cards[i].title.text = o.title;
                cards[i].line.text = o.line;
                cards[i].line.color = col;
                cards[i].detail.text = o.detail;
                cards[i].rt.localScale = Vector3.one;
            }
        }

        // Step 2 of the level-up panel: the same three cards now stand for HP / BLOCK / STAMINA.
        public void ShowStatChoice(Knight k)
        {
            Progress pr = k.progress;
            levelRoot.SetActive(true);
            levelOpenedAt = Time.unscaledTime;
            bannerTime = 0f; bannerText.text = "";
            levelTitle.text = "STAT POINT" + (pr.statPoints > 1 ? "   (" + pr.statPoints + " to spend)" : "");
            levelSub.text = "Spend 1 stat point   (keys 1 / 2 / 3 or click a card)" + OnlineNote();
            DimFor(levelDim, 0.68f);
            for (int i = 0; i < 3; i++)
            {
                Attr a = (Attr)i;
                int n = pr.Points(a);
                bool maxed = n >= Progress.AttrCap;
                Color col = maxed ? new Color(0.45f, 0.45f, 0.5f) : AttrColor(a);
                CardUi c = cards[i];
                c.rt.gameObject.SetActive(true);
                c.rt.localScale = Vector3.one;
                c.border.color = col;
                c.title.text = Progress.AttrName(a);
                c.line.color = col;
                string pts = "\nPoints  " + n + " > " + (n + 1) + "   (max " + Progress.AttrCap + ")";
                if (maxed)
                {
                    c.line.text = "MAXED";
                    c.detail.text = "Points  " + n + " / " + Progress.AttrCap;
                }
                else if (a == Attr.Hp)
                {
                    c.line.text = "+" + Progress.HpPerPoint.ToString("0") + " Max Health";
                    c.detail.text = "Max health  " + k.MaxHp.ToString("0") + " > " + (k.MaxHp + Progress.HpPerPoint).ToString("0") + pts;
                }
                else if (a == Attr.Block)
                {
                    c.line.text = "+2% Block  +" + Progress.ArmorPerPoint.ToString("0") + " Armor";
                    c.detail.text = "Shield stops  " + (pr.BlockReduction * 100f).ToString("0") + "% > " + ((pr.BlockReduction + Progress.BlockPerPoint) * 100f).ToString("0") + "%"
                        + "\nMax armor  " + k.MaxArmor.ToString("0") + " > " + (k.MaxArmor + Progress.ArmorPerPoint).ToString("0") + pts;
                }
                else
                {
                    c.line.text = "+" + Progress.StaminaPerPoint.ToString("0") + " Max Stamina";
                    c.detail.text = "Max stamina  " + k.MaxStamina.ToString("0") + " > " + (k.MaxStamina + Progress.StaminaPerPoint).ToString("0")
                        + "\nRegen  +" + ((pr.StaminaRegenMult - 1f) * 100f).ToString("0") + "% > +" + ((pr.StaminaRegenMult - 1f + Progress.RegenPerPoint) * 100f).ToString("0") + "%" + pts;
                }
            }
        }

        public void HideLevelUp() { levelRoot.SetActive(false); }

        // Online the panels do not pause anything: say so, and darken the arena less so the fight stays visible.
        static string OnlineNote()
        {
            return Net.Multiplayer ? "\nOnline: the game is NOT paused - you are shielded for " + Game.LevelUpProtect.ToString("0") + " seconds, and can still walk" : "";
        }

        static void DimFor(Image dim, float alpha)
        {
            Color c = dim.color;
            c.a = Net.Multiplayer ? alpha * 0.5f : alpha;
            dim.color = c;
        }

        public void ShowInventory()
        {
            invRoot.SetActive(true);
            invOpenedAt = Time.unscaledTime;
            DimFor(invDim, 0.6f);
            invFoot.text = "1 - 5 or click a row: equip an owned sword          Tab / Esc: close          "
                + (Net.Multiplayer ? "(online: the game keeps running!)" : "(the game is paused)");
            RefreshInventory(-1);
        }

        public void HideInventory() { invRoot.SetActive(false); }

        static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

        void RefreshInventory(int hover)
        {
            Knight k = Game.I.knight;
            Inventory inv = k.inventory;
            Progress pr = k.progress;
            invSlot.text = "Weapon slot:  <color=" + Hex(k.Weapon.uiColor) + "><b>" + inv.NameOf(inv.equipped) + "</b></color>     Owned " + inv.OwnedCount + " / " + Weapons.All.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                WeaponDef w = Weapons.All[i];
                RowUi r = rows[i];
                bool own = inv.owned[i], eq = inv.equipped == i;
                r.bg.color = eq ? new Color(0.2f, 0.26f, 0.16f, 1f) : i == hover && own ? new Color(0.2f, 0.2f, 0.3f, 1f) : new Color(0.12f, 0.12f, 0.17f, 1f);
                r.swatch.color = own ? w.uiColor : new Color(0.3f, 0.3f, 0.34f);
                Color text = own ? Color.white : new Color(0.5f, 0.5f, 0.55f);
                r.key.color = text;
                r.name.color = own ? w.uiColor : text;
                r.name.text = inv.NameOf(i) + "   <size=12><color=" + Hex(text) + ">" + w.rarity + "  -  " + w.hint + "</color></size>";
                r.stats.color = text;
                r.stats.text = "DMG x" + inv.DamageOf(i).ToString("0.00") + w.StatLine().Substring(w.StatLine().IndexOf("   SPEED"));
                r.state.color = eq ? new Color(0.6f, 1f, 0.5f) : own ? new Color(0.85f, 0.87f, 0.92f) : new Color(0.6f, 0.55f, 0.45f);
                r.state.text = eq ? "EQUIPPED" : own ? "press " + (i + 1) + " or click" : w.dropHint;
            }
            invStats.text =
                "<b>STAT POINTS</b>    <color=" + Hex(AttrColor(Attr.Hp)) + ">HP " + pr.Points(Attr.Hp) + "/" + Progress.AttrCap + "</color>  (+" + pr.HpBonus.ToString("0") + " max health)"
                + "     <color=" + Hex(AttrColor(Attr.Block)) + ">BLOCK " + pr.Points(Attr.Block) + "/" + Progress.AttrCap + "</color>  (+" + pr.ArmorBonus.ToString("0") + " armor, shield stops " + (pr.BlockReduction * 100f).ToString("0") + "%)"
                + "     <color=" + Hex(AttrColor(Attr.Stamina)) + ">STAMINA " + pr.Points(Attr.Stamina) + "/" + Progress.AttrCap + "</color>  (+" + pr.StaminaBonus.ToString("0") + ", regen +" + ((pr.StaminaRegenMult - 1f) * 100f).ToString("0") + "%)"
                + "\n<b>NOW</b>    Health " + k.MaxHp.ToString("0") + "   Armor " + k.MaxArmor.ToString("0") + "   Stamina " + k.MaxStamina.ToString("0")
                + "     |     Sword hit x" + k.DamageMult(-1).ToString("0.00") + "   swing x" + k.SwingSpeed.ToString("0.00")
                + "   crit " + (k.CritChance(-1) * 100f).ToString("0") + "% x" + k.CritMult(-1).ToString("0.0") + "   reach x" + k.Weapon.reach.ToString("0.00");
        }

        // Returns the sword to equip (index into Weapons.All) or -1. Keys 1..5 or a left click on an owned row.
        public int InventoryInput()
        {
            if (!invRoot.activeSelf) return -1;
            Inventory inv = Game.I.knight.inventory;
            int pick = -1;
            KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
            for (int i = 0; i < rows.Length && i < keys.Length; i++)
                if (InputX.Down(keys[i])) pick = i;

            int hover = -1;
            if (InputX.HasMouse)
            {
                Vector2 m = InputX.MousePos;
                for (int i = 0; i < rows.Length; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(rows[i].rt, m, null)) hover = i;
            }
            if (pick < 0 && hover >= 0 && Time.unscaledTime - invOpenedAt > 0.2f && InputX.MouseDown(0)) pick = hover;
            if (pick >= 0 && !inv.owned[pick])
            {
                Notice(Weapons.All[pick].name + " not found yet  -  " + Weapons.All[pick].dropHint, new Color(1f, 0.6f, 0.5f));
                pick = -1;
            }
            if (pick == inv.equipped) pick = -1;
            RefreshInventory(hover);
            return pick;
        }

        // Returns the picked card (0..2) or -1. Keys 1/2/3 or a left click on a card.
        public int LevelUpInput(int count)
        {
            if (!levelRoot.activeSelf) return -1;
            if (count > 0 && (InputX.Down(KeyCode.Alpha1) || InputX.Down(KeyCode.Keypad1))) return 0;
            if (count > 1 && (InputX.Down(KeyCode.Alpha2) || InputX.Down(KeyCode.Keypad2))) return 1;
            if (count > 2 && (InputX.Down(KeyCode.Alpha3) || InputX.Down(KeyCode.Keypad3))) return 2;

            int hover = -1;
            if (InputX.HasMouse)
            {
                Vector2 m = InputX.MousePos;
                for (int i = 0; i < count && i < 3; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(cards[i].rt, m, null)) hover = i;
            }
            for (int i = 0; i < 3; i++)
            {
                float target = i == hover ? 1.06f : 1f;
                cards[i].rt.localScale = Vector3.Lerp(cards[i].rt.localScale, Vector3.one * target, 0.3f);
                cards[i].bg.color = i == hover ? new Color(0.16f, 0.16f, 0.24f, 1f) : new Color(0.09f, 0.09f, 0.13f, 1f);
            }
            // a short delay so a sword click that was already on its way does not pick a card
            if (hover >= 0 && Time.unscaledTime - levelOpenedAt > 0.35f && InputX.MouseDown(0)) return hover;
            return -1;
        }

        // ---------------------------------------------------------------- update

        void LateUpdate()
        {
            Game g = Game.I;
            if (g == null || g.knight == null) return;
            float dt = Time.unscaledDeltaTime;
            Knight k = g.knight;
            Progress pr = g.progress;

            UpdateBar(hpBar, k.hp, k.MaxHp, dt, false, null);
            UpdateBar(armorBar, k.armor, k.MaxArmor, dt, false, null);
            UpdateBar(staminaBar, k.stamina, k.MaxStamina, dt, Time.unscaledTime - k.StaminaDeniedTime < 0.35f, null);
            UpdateStaminaTicks(k);
            expBar.label.text = "LV " + pr.level;
            if (pr.IsMax) UpdateBar(expBar, 1f, 1f, dt, false, "MAX LEVEL");
            else UpdateBar(expBar, pr.exp, Progress.Need(pr.level), dt, false, Mathf.FloorToInt(pr.exp) + " / " + Progress.Need(pr.level) + " EXP");

            // upgrade totals (damage and crit include the equipped sword), sword, stat points
            var sb = new System.Text.StringBuilder(600);
            sb.Append("<b>DAMAGE</b> <color=#ff6b52>x").Append(k.DamageMult(-1).ToString("0.0"))
              .Append("</color>   <b>ATK SPEED</b> <color=#ffdb4d>x").Append(pr.SpeedMult(-1).ToString("0.0"))
              .Append("</color>   <b>CRIT</b> <color=#cc8cff>").Append((k.CritChance(-1) * 100f).ToString("0")).Append("% x").Append(k.CritMult(-1).ToString("0.0")).Append("</color>\n");
            for (int i = 0; i < 4; i++)
            {
                SkillDef s = Knight.Skills[i];
                sb.Append(s.key).Append(' ').Append(s.name.PadRight(12))
                  .Append("  dmg x").Append(k.DamageMult(i).ToString("0.0"))
                  .Append("  spd x").Append(pr.SpeedMult(i).ToString("0.0"))
                  .Append("  crit ").Append((k.CritChance(i) * 100f).ToString("0")).Append("% x").Append(k.CritMult(i).ToString("0.0")).Append('\n');
            }
            sb.Append("<b>SWORD</b> <color=").Append(Hex(k.Weapon.uiColor)).Append('>').Append(k.inventory.NameOf(k.inventory.equipped))
              .Append("</color>  swing x").Append(k.SwingSpeed.ToString("0.00")).Append("  reach x").Append(k.Weapon.reach.ToString("0.00")).Append("   [Tab]\n");
            sb.Append("<b>POINTS</b>  <color=").Append(Hex(AttrColor(Attr.Hp))).Append(">HP ").Append(pr.Points(Attr.Hp))
              .Append("</color>   <color=").Append(Hex(AttrColor(Attr.Block))).Append(">BLOCK ").Append(pr.Points(Attr.Block)).Append(" (").Append((pr.BlockReduction * 100f).ToString("0")).Append("%)")
              .Append("</color>   <color=").Append(Hex(AttrColor(Attr.Stamina))).Append(">STAMINA ").Append(pr.Points(Attr.Stamina)).Append("</color>");
            if (pr.statPoints > 0) sb.Append("   <color=#ffe066><b>+").Append(pr.statPoints).Append(" unspent</b></color>");
            sb.Append('\n');
            sb.Append("Wave ").Append(g.wave).Append("    Slimes ").Append(g.slimesAlive).Append("    Kills ").Append(pr.kills);
            statsText.text = sb.ToString();

            // skill slots
            for (int i = 0; i < 4; i++)
            {
                SkillDef s = Knight.Skills[i];
                SlotUi u = slots[i];
                float left = k.cd[i];
                float frac = k.cdMax[i] > 0f ? Mathf.Clamp01(left / k.cdMax[i]) : 0f;
                u.cool.offsetMax = new Vector2(-4, 4 + 88f * frac);
                u.cool.gameObject.SetActive(left > 0f);
                u.cdText.text = left > 0f ? left.ToString(left < 1f ? "0.0" : "0") : "";
                u.icon.color = left > 0f ? s.color * 0.35f : s.color * 0.75f;
                u.key.color = left > 0f ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                u.cost.text = (s.cooldown / pr.SpeedMult(i)).ToString("0.0") + "s";
            }

            // boss bar
            Minotaur m = g.minotaur;
            if (m != null && !m.Dead)
            {
                bossBar.frame.gameObject.SetActive(true);
                bossText.text = "MINOTAUR" + (m.State == Minotaur.S.Idle ? "   (resting)" : m.Stunned ? "   (stunned)" : "");
                UpdateBar(bossBar, m.hp, m.maxHp, dt, false, Mathf.CeilToInt(m.hp) + " / " + Mathf.CeilToInt(m.maxHp)
                    + (m.armor > 0.5f ? "   armor " + Mathf.CeilToInt(m.armor) : ""));
            }
            else
            {
                bossBar.frame.gameObject.SetActive(false);
                bossText.text = "Minotaur returns in " + Mathf.Max(0, Mathf.CeilToInt(g.MinotaurRespawnIn)) + "s";
            }

            UpdateParty(g, k);

            bool controlsOn = showControls && !g.PanelOpen;       // out of the way of the level-up / weapon panels
            if (controlsPanel.activeSelf != controlsOn) controlsPanel.SetActive(controlsOn);
            if (controlsText.gameObject.activeSelf != controlsOn) controlsText.gameObject.SetActive(controlsOn);

            if (bannerTime > 0f)
            {
                bannerTime -= dt;
                Color c = bannerColor;
                c.a = Mathf.Clamp01(bannerTime / 0.35f);
                bannerText.color = c;
                float pop = 1f + Mathf.Clamp01((bannerTime - 1.45f) / 0.15f) * 0.25f;
                bannerText.rectTransform.localScale = Vector3.one * pop;
            }
            else if (bannerText.text.Length > 0) bannerText.text = "";

            if (actionTime > 0f)
            {
                actionTime -= dt;
                Color c = actionColor;
                c.a = Mathf.Clamp01(actionTime / 0.3f);
                actionText.color = c;
            }
            else if (actionText.text.Length > 0) actionText.text = "";

            if (noticeTime > 0f)
            {
                noticeTime -= dt;
                Color c = noticeColor;
                c.a = Mathf.Clamp01(noticeTime / 0.5f);
                noticeText.color = c;
            }
            else if (noticeText.text.Length > 0) noticeText.text = "";

            float scale = ScaleFactor;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                PopupUi p = popups[i];
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.text.gameObject.SetActive(false);
                    popupPool.Push(p);
                    popups.RemoveAt(i);
                    continue;
                }
                float t = 1f - p.life / p.maxLife;
                Vector3 sp = g.cam.WorldToScreenPoint(p.world + new Vector3(0f, t * 1.2f, 0f));
                p.text.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y) / scale;
                Color c = p.color;
                c.a = Mathf.Clamp01((1f - t) * 2.5f);
                p.text.color = c;
                p.text.rectTransform.localScale = Vector3.one * (1f + Mathf.Clamp01(1f - t * 6f) * 0.6f);
            }
        }

        // Dividers at 25, 50, ... of the maximum (one dash each). The one that was just crossed upwards flashes;
        // when a dash is refused the next divider shakes.
        public int StaminaTicksShown { get { int n = 0; foreach (Image t in staminaTicks) if (t.gameObject.activeSelf) n++; return n; } }
        public float StaminaTickX(int i) { return staminaTicks[i].rectTransform.anchoredPosition.x; }

        void UpdateStaminaTicks(Knight k)
        {
            float max = k.MaxStamina;
            float width = staminaBar.frame.rectTransform.sizeDelta.x;
            int crossed = Mathf.FloorToInt(k.stamina / Knight.DashCost + 0.0001f);
            if (crossed > Mathf.FloorToInt(lastStamina / Knight.DashCost + 0.0001f) && crossed >= 1) { tickFlash = crossed - 1; tickFlashAt = Time.unscaledTime; }
            lastStamina = k.stamina;
            bool denied = Time.unscaledTime - k.StaminaDeniedTime < 0.35f;
            int next = Mathf.FloorToInt(k.stamina / Knight.DashCost + 0.0001f);       // the divider that is still out of reach
            for (int i = 0; i < staminaTicks.Length; i++)
            {
                Image t = staminaTicks[i];
                float at = (i + 1) * Knight.DashCost;
                bool on = at < max - 0.01f;
                if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
                if (!on) continue;
                float x = 3f + (width - 6f) * at / max;
                if (denied && i == next) x += Mathf.Sin(Time.unscaledTime * 90f) * 3f;
                t.rectTransform.anchoredPosition = new Vector2(x, 0f);
                bool flash = i == tickFlash && Time.unscaledTime - tickFlashAt < 0.15f;
                t.color = flash ? new Color(1f, 0.95f, 0.6f, 1f) : new Color(0f, 0f, 0f, 0.7f);
            }
        }

        // Online only: the connection line, one row per other player, and a name tag over every knight.
        void UpdateParty(Game g, Knight me)
        {
            bool online = Net.Multiplayer;
            if (partyRoot.activeSelf != online) partyRoot.SetActive(online);
            int tagCount = 0;
            if (online)
            {
                netText.text = Net.StatusLine;
                int row = 0;
                float scale = ScaleFactor;
                for (int i = 0; i < g.players.Count; i++)
                {
                    Knight p = g.players[i];
                    Color col = Knight.UiColor(p.pid);

                    // name tag
                    Vector3 sp = g.cam.WorldToScreenPoint(p.transform.position + new Vector3(0f, 2.75f, 0f));
                    if (sp.z > 0f)
                    {
                        if (tagCount == tags.Count)
                        {
                            Text nt = MakeText("NameTag", Vector2.zero, Vector2.zero, new Vector2(220, 24), 16, TextAnchor.MiddleCenter, popupRoot);
                            nt.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                            nt.fontStyle = FontStyle.Bold;
                            tags.Add(nt);
                        }
                        Text t = tags[tagCount++];
                        if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                        t.text = p.Dead ? p.playerName + "  (down)" : p.playerName;
                        t.color = p == me ? new Color(col.r, col.g, col.b, 0.8f) : col;
                        t.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y) / scale;
                    }

                    // party row (the local knight has the big bars already)
                    if (p == me || row >= party.Length) continue;
                    PartyUi u = party[row++];
                    if (!u.go.activeSelf) u.go.SetActive(true);
                    u.swatch.color = Knight.MainColors[p.pid & 3];
                    u.name.text = p.playerName;
                    u.name.color = col;
                    u.level.text = "Lv " + p.progress.level;
                    float f = p.MaxHp > 0f ? Mathf.Clamp01(p.hp / p.MaxHp) : 0f;
                    u.fill.anchorMax = new Vector2(Mathf.Max(0.0001f, f), 1f);
                    u.fill.offsetMax = new Vector2(-3f * f, -3f);
                    u.value.text = p.Dead ? "DOWN" : Mathf.CeilToInt(p.hp) + " / " + Mathf.CeilToInt(p.MaxHp);
                }
                for (int i = row; i < party.Length; i++) if (party[i].go.activeSelf) party[i].go.SetActive(false);
                partyPanel.rectTransform.sizeDelta = new Vector2(400f, row == 0 ? 28f : 32f + row * 26f);
                if (row == 0 && g.players.Count <= 1) netText.text += Net.IsHost ? "   - waiting for friends" : "";
            }
            for (int i = tagCount; i < tags.Count; i++) if (tags[i].gameObject.activeSelf) tags[i].gameObject.SetActive(false);
        }

        // test access: the visible name tags and party rows
        public int NameTagsShown { get { int n = 0; foreach (Text t in tags) if (t.gameObject.activeSelf) n++; return n; } }
        public int PartyRowsShown { get { int n = 0; foreach (PartyUi u in party) if (u.go.activeSelf && partyRoot.activeSelf) n++; return n; } }

        void UpdateBar(BarUi b, float value, float max, float dt, bool denied, string text)
        {
            float k = max > 0f ? Mathf.Clamp01(value / max) : 0f;
            b.shown = Mathf.MoveTowards(b.shown, k, dt * 3f);
            if (b.ghostShown < b.shown) b.ghostShown = b.shown;                   // gains snap
            else b.ghostShown = Mathf.MoveTowards(b.ghostShown, b.shown, dt * 0.35f);   // losses drain slowly

            b.fill.anchorMax = new Vector2(Mathf.Max(0.0001f, b.shown), 1f);
            b.fill.offsetMax = new Vector2(-3f * b.shown, -3f);
            b.ghost.anchorMax = new Vector2(Mathf.Max(0.0001f, b.ghostShown), 1f);
            b.ghost.offsetMax = new Vector2(-3f * b.ghostShown, -3f);
            b.fillImg.enabled = b.shown > 0.002f;

            b.value.text = text ?? (Mathf.CeilToInt(value) + " / " + Mathf.CeilToInt(max));
            b.fillImg.color = denied ? Color.Lerp(b.color, Color.red, 0.75f) : b.color;
            b.frame.color = denied ? new Color(0.6f, 0.05f, 0.05f, 0.95f) : FrameCol;
        }
    }
}

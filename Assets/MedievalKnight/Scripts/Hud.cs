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

        // one sword slot of the Tab window
        class RowUi
        {
            public RectTransform rt;
            public Image border, bg, eqBar;
            public RawImage icon;
            public Text key, name, badge, state, qmark;
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
        Image partyPanel, levelDim, invDim, invFrame;
        RawImage eqIcon;
        Text eqName, eqStats, ownedText, charText, charStats, expValue, statHead, skillsText, soulKills, tabHint, tabCornerHint;
        RectTransform expFill;
        readonly RectTransform[] statFill = new RectTransform[3];
        readonly Text[] statNum = new Text[3], statEffect = new Text[3];
        float lastTabRefresh, sessionShownAt;
        Text netText, invFoot;

        Font font;
        Canvas canvas;
        RectTransform root, popupRoot;
        BarUi hpBar, armorBar, staminaBar, expBar, bossBar;
        Text bannerText, actionText, noticeText, controlsText, bossText, levelTitle, levelSub;
        GameObject controlsPanel, bossPanel, levelRoot, invRoot;
        RectTransform lairArrow;
        Text lairText;
        public static readonly Color SoulColor = new Color(0.62f, 0.91f, 1f);
        GameObject deathRoot;
        Image deathDim;
        Text deathTitle, deathLose, deathKeep, deathCount;
        Knight deathKnight;
        float deathShownAt;
        Image soulIcon;
        Text soulText;
        int soulShown;
        float soulBumpAt = -10f;
        readonly Image[] staminaTicks = new Image[10];
        float lastStamina, tickFlashAt = -10f;
        int tickFlash = -1;
        readonly SlotUi[] slots = new SlotUi[4];
        readonly CardUi[] cards = new CardUi[3];
        readonly RowUi[] rows = new RowUi[Weapons.All.Length];
        bool showControls = true;
        float bannerTime, actionTime, noticeTime, levelOpenedAt, invOpenedAt, bannerLength = 1.6f;
        Vector3? arrowPoint;
        string arrowName = "Lair";
        float arrowMin = 30f;
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
            Panel("BarsPanel", new Vector2(0, 1), new Vector2(16, -16), new Vector2(400, 180), PanelCol);
            hpBar = MakeBar("HEALTH", new Vector2(0, 1), new Vector2(116, -28), 288, new Color(0.86f, 0.2f, 0.2f));
            armorBar = MakeBar("ARMOR", new Vector2(0, 1), new Vector2(116, -60), 288, new Color(0.5f, 0.68f, 0.92f));
            staminaBar = MakeBar("STAMINA", new Vector2(0, 1), new Vector2(116, -92), 288, new Color(0.95f, 0.78f, 0.22f));
            expBar = MakeBar("LV 1", new Vector2(0, 1), new Vector2(116, -124), 288, new Color(0.66f, 0.4f, 0.95f));
            expBar.label.fontStyle = FontStyle.Bold;
            expBar.label.color = new Color(1f, 0.9f, 0.45f);
            expBar.label.fontSize = 18;

            // --- Soul, the fifth row of the top-left panel ---
            Text soulLabel = MakeText("SoulLabel", new Vector2(0, 1), new Vector2(28, -156), new Vector2(90, 24), 15, TextAnchor.MiddleLeft);
            soulLabel.text = "SOUL";
            soulLabel.color = SoulColor;
            soulIcon = Panel("SoulIcon", new Vector2(0, 1), new Vector2(116, -158), new Vector2(14, 20), SoulColor);
            soulIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            soulText = MakeText("SoulValue", new Vector2(0, 1), new Vector2(136, -156), new Vector2(240, 24), 20, TextAnchor.MiddleLeft);
            soulText.fontStyle = FontStyle.Bold;
            soulText.color = SoulColor;
            soulText.text = "0";

            // "TAB !" next to the Soul row while a stat point waits, and a one-off hint in the lower left corner
            tabHint = MakeText("TabHint", new Vector2(0, 1), new Vector2(330, -156), new Vector2(66, 24), 16, TextAnchor.MiddleLeft);
            tabHint.fontStyle = FontStyle.Bold;
            tabHint.color = new Color(1f, 0.88f, 0.3f);
            tabHint.text = "TAB !";
            tabHint.gameObject.SetActive(false);
            tabCornerHint = MakeText("TabCornerHint", new Vector2(0, 0), new Vector2(16, 16), new Vector2(400, 24), 15, TextAnchor.MiddleLeft);
            tabCornerHint.text = "Tab = Inventory & Stats";

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

            // --- online: connection line and the other players, right under the top-left panel ---
            RectTransform pr = Rect("Party", new Vector2(0, 1), Vector2.zero, Vector2.zero);
            partyRoot = pr.gameObject;
            partyPanel = Panel("PartyPanel", new Vector2(0, 1), new Vector2(16, -206), new Vector2(400, 110), PanelCol, pr);
            netText = MakeText("NetStatus", new Vector2(0, 1), new Vector2(28, -210), new Vector2(380, 20), 13, TextAnchor.MiddleLeft, pr);
            netText.color = new Color(0.75f, 0.95f, 0.8f);
            for (int i = 0; i < party.Length; i++)
            {
                var u = new PartyUi();
                float y = -234f - i * 26f;
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

            // --- an arrow to the Minotaur's lair with the distance, under the banner; hidden when you are already there ---
            lairArrow = Rect("LairArrow", new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(44, 44));
            lairArrow.pivot = new Vector2(0.5f, 0.5f);
            lairArrow.anchoredPosition = new Vector2(0, -190);
            Color arrowCol = new Color(1f, 0.62f, 0.25f);
            Panel("Shaft", new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(6, 22), arrowCol, lairArrow);
            Image head = Panel("Head", new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(15, 15), arrowCol, lairArrow);
            head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            lairText = MakeText("LairText", new Vector2(0.5f, 1), new Vector2(0, -216), new Vector2(240, 22), 16, TextAnchor.MiddleCenter);
            lairText.fontStyle = FontStyle.Bold;
            lairText.color = arrowCol;
            lairArrow.gameObject.SetActive(false);
            lairText.gameObject.SetActive(false);

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

            BuildDeathPanel();

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

        static RectTransform Stretch(Image img, float inset)
        {
            RectTransform rt = Stretch(img);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
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

        // The Tab window: left = the equipped sword and the six sword slots (a picture each), right = character,
        // stat points, skills, Soul and kills. About 1000 x 520, centred.
        void BuildInventoryPanel()
        {
            RectTransform ir = Rect("Inventory", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Fill(ir);
            invRoot = ir.gameObject;
            invDim = Panel("Dim", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0.04f, 0.6f), ir);
            Fill(invDim.rectTransform);
            invFrame = Panel("Frame", new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(1006, 526), new Color(0.75f, 0.62f, 0.3f, 1f), ir);
            RectTransform p = Panel("Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 520), new Color(0.07f, 0.07f, 0.1f, 1f), invFrame.transform).rectTransform;
            Color gray = new Color(0.7f, 0.72f, 0.78f);
            Vector2 tl = new Vector2(0, 1);

            Text title = MakeText("Title", tl, new Vector2(20, -8), new Vector2(700, 34), 24, TextAnchor.MiddleLeft, p);
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(1f, 0.88f, 0.35f);
            title.text = "KNIGHT  -  INVENTORY & STATS";
            Text closeHint = MakeText("Close", new Vector2(1, 1), new Vector2(-20, -12), new Vector2(200, 26), 15, TextAnchor.MiddleRight, p);
            closeHint.color = gray;
            closeHint.text = "[Tab] close";

            // ---- left: the equipped sword
            Panel("EquippedBox", tl, new Vector2(16, -50), new Vector2(626, 196), new Color(0.1f, 0.1f, 0.14f, 1f), p);
            Text eqLabel = MakeText("EqLabel", tl, new Vector2(28, -56), new Vector2(200, 22), 15, TextAnchor.MiddleLeft, p);
            eqLabel.fontStyle = FontStyle.Bold;
            eqLabel.color = new Color(0.6f, 1f, 0.55f);
            eqLabel.text = "EQUIPPED";
            Image eqFrame = Panel("EqIconFrame", tl, new Vector2(28, -80), new Vector2(160, 160), new Color(0.4f, 1f, 0.45f), p);
            eqIcon = AddRaw("EqIcon", eqFrame.transform, new Vector2(156, 156));
            eqName = MakeText("EqName", tl, new Vector2(204, -82), new Vector2(420, 54), 22, TextAnchor.UpperLeft, p);
            eqName.supportRichText = true;
            eqStats = MakeText("EqStats", tl, new Vector2(204, -140), new Vector2(430, 80), 16, TextAnchor.UpperLeft, p);
            eqStats.supportRichText = true;
            ownedText = MakeText("Owned", tl, new Vector2(500, -56), new Vector2(130, 22), 15, TextAnchor.MiddleRight, p);
            ownedText.color = gray;

            // ---- left: one slot per sword (six) in a row, sized to fit the same width
            Text swLabel = MakeText("SwordsLabel", tl, new Vector2(16, -254), new Vector2(500, 22), 15, TextAnchor.MiddleLeft, p);
            swLabel.color = gray;
            swLabel.text = "SWORDS   (click or press 1-6 to equip)";
            // the slots share 622 px (5 slots: 118 wide as before, 6 slots: 97 wide) with an 8 px gap
            float slotW = Mathf.Floor((622f - 8f * (rows.Length - 1)) / rows.Length);
            bool narrow = slotW < 110f;
            for (int i = 0; i < rows.Length; i++)
            {
                var r = new RowUi();
                float x = 16 + i * (slotW + 8f);
                r.border = Panel("Slot" + (i + 1) + "Border", tl, new Vector2(x, -278), new Vector2(slotW, 190), Color.white, p);
                r.rt = r.border.rectTransform;
                r.bg = Panel("Bg", tl, new Vector2(3, -3), new Vector2(slotW - 6f, 184), SlotCol, r.rt);
                r.key = MakeText("Key", tl, new Vector2(8, -6), new Vector2(40, 22), 16, TextAnchor.MiddleLeft, r.bg.transform);
                r.key.fontStyle = FontStyle.Bold;
                r.key.text = "[" + (i + 1) + "]";
                r.icon = AddRaw("Icon", r.bg.transform, new Vector2(Mathf.Min(90f, slotW - 12f), Mathf.Min(90f, slotW - 12f)));
                r.icon.rectTransform.anchorMin = r.icon.rectTransform.anchorMax = r.icon.rectTransform.pivot = new Vector2(0.5f, 1f);
                r.icon.rectTransform.anchoredPosition = new Vector2(0f, -24f);
                r.qmark = MakeText("Q", new Vector2(0.5f, 1), new Vector2(0, -69), new Vector2(100, 56), 46, TextAnchor.MiddleCenter, r.bg.transform);
                r.qmark.fontStyle = FontStyle.Bold;
                r.qmark.color = new Color(0.55f, 0.55f, 0.62f);
                r.qmark.text = "?";
                r.name = MakeText("Name", new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(slotW - 4f, 34), narrow ? 12 : 14, TextAnchor.MiddleCenter, r.bg.transform);
                r.name.fontStyle = FontStyle.Bold;
                r.name.horizontalOverflow = HorizontalWrapMode.Wrap;
                r.state = MakeText("State", new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(slotW - 4f, 26), narrow ? 10 : 11, TextAnchor.MiddleCenter, r.bg.transform);
                r.state.horizontalOverflow = HorizontalWrapMode.Wrap;
                r.badge = MakeText("Plus", new Vector2(1, 1), new Vector2(-6, -14), new Vector2(76, 20), 14, TextAnchor.MiddleRight, r.bg.transform);
                r.badge.fontStyle = FontStyle.Bold;
                r.badge.color = new Color(1f, 0.9f, 0.4f);
                r.eqBar = Panel("EqBar", new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(slotW - 14f, 18), new Color(0.2f, 0.55f, 0.25f, 1f), r.bg.transform);
                Text eqt = MakeText("Text", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(slotW - 8f, 18), narrow ? 10 : 12, TextAnchor.MiddleCenter, r.eqBar.transform);
                eqt.fontStyle = FontStyle.Bold;
                eqt.text = "EQUIPPED  *";
                r.eqBar.gameObject.SetActive(false);
                rows[i] = r;
            }

            // ---- right: character
            float cx = 662;
            Panel("CharBox", tl, new Vector2(cx - 8, -50), new Vector2(338, 122), new Color(0.1f, 0.1f, 0.14f, 1f), p);
            Text charHead = MakeText("CharHead", tl, new Vector2(cx, -54), new Vector2(200, 22), 15, TextAnchor.MiddleLeft, p);
            charHead.fontStyle = FontStyle.Bold;
            charHead.color = new Color(1f, 0.88f, 0.35f);
            charHead.text = "CHARACTER";
            charText = MakeText("CharText", tl, new Vector2(cx, -74), new Vector2(322, 20), 15, TextAnchor.MiddleLeft, p);
            charText.supportRichText = true;
            Image expFrame = Panel("ExpBar", tl, new Vector2(cx, -96), new Vector2(322, 18), FrameCol, p);
            charStats = MakeText("CharStats", tl, new Vector2(cx, -118), new Vector2(322, 54), 14, TextAnchor.UpperLeft, p);
            charStats.supportRichText = true;
            expFill = Stretch(Panel("Fill", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.66f, 0.4f, 0.95f), expFrame.transform), 2f);
            expValue = MakeText("Val", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 20), 12, TextAnchor.MiddleCenter, expFrame.transform);

            // ---- right: stat points
            Panel("StatBox", tl, new Vector2(cx - 8, -174), new Vector2(338, 136), new Color(0.1f, 0.1f, 0.14f, 1f), p);
            statHead = MakeText("StatHead", tl, new Vector2(cx, -178), new Vector2(322, 22), 15, TextAnchor.MiddleLeft, p);
            statHead.supportRichText = true;
            Attr[] attrs = { Attr.Hp, Attr.Block, Attr.Stamina };
            for (int a = 0; a < 3; a++)
            {
                float y = -204 - a * 34;
                Text lab = MakeText("Stat" + a, tl, new Vector2(cx, y), new Vector2(80, 20), 14, TextAnchor.MiddleLeft, p);
                lab.fontStyle = FontStyle.Bold;
                lab.color = AttrColor(attrs[a]);
                lab.text = Progress.AttrName(attrs[a]);
                Image bar = Panel("StatBar" + a, tl, new Vector2(cx + 76, y - 2), new Vector2(190, 16), FrameCol, p);
                statFill[a] = Stretch(Panel("Fill", Vector2.zero, Vector2.zero, Vector2.zero, AttrColor(attrs[a]), bar.transform), 2f);
                statNum[a] = MakeText("Num", tl, new Vector2(cx + 272, y), new Vector2(54, 20), 14, TextAnchor.MiddleLeft, p);
                statEffect[a] = MakeText("Effect", tl, new Vector2(cx + 76, y - 17), new Vector2(250, 16), 11, TextAnchor.MiddleLeft, p);
                statEffect[a].color = gray;
            }

            // ---- right: skills, Soul, kills
            Panel("SkillBox", tl, new Vector2(cx - 8, -316), new Vector2(338, 104), new Color(0.1f, 0.1f, 0.14f, 1f), p);
            skillsText = MakeText("Skills", tl, new Vector2(cx, -320), new Vector2(326, 98), 13, TextAnchor.UpperLeft, p);
            skillsText.supportRichText = true;
            Panel("SoulBox", tl, new Vector2(cx - 8, -426), new Vector2(338, 36), new Color(0.1f, 0.1f, 0.14f, 1f), p);
            soulKills = MakeText("SoulKills", tl, new Vector2(cx, -432), new Vector2(326, 24), 18, TextAnchor.MiddleLeft, p);
            soulKills.supportRichText = true;

            invFoot = MakeText("Foot", new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(960, 22), 14, TextAnchor.MiddleCenter, p);
            invFoot.color = gray;
            invRoot.SetActive(false);
        }

        RawImage AddRaw(string name, Transform parent, Vector2 size)
        {
            RectTransform rt = Rect(name, new Vector2(0.5f, 0.5f), Vector2.zero, size, parent);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            return raw;
        }

        // The YOU DIED screen. Not clickable (nothing under it can be pressed anyway: a dead knight takes no input).
        void BuildDeathPanel()
        {
            RectTransform dr = Rect("Death", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Fill(dr);
            deathRoot = dr.gameObject;
            deathDim = Panel("Dim", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.12f, 0f, 0f, 0f), dr);
            Fill(deathDim.rectTransform);
            deathTitle = MakeText("Title", new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(900, 90), 60, TextAnchor.MiddleCenter, dr);
            deathTitle.fontStyle = FontStyle.Bold;
            deathTitle.color = new Color(0.77f, 0.17f, 0.17f);
            deathTitle.text = "YOU DIED";
            deathLose = MakeText("Lose", new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(900, 60), 22, TextAnchor.UpperCenter, dr);
            deathLose.color = new Color(0.8f, 0.82f, 0.86f);
            deathLose.supportRichText = true;
            deathKeep = MakeText("Keep", new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(900, 34), 22, TextAnchor.MiddleCenter, dr);
            deathKeep.color = new Color(1f, 0.88f, 0.45f);
            deathKeep.supportRichText = true;
            deathCount = MakeText("Count", new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(900, 34), 24, TextAnchor.MiddleCenter, dr);
            deathCount.fontStyle = FontStyle.Bold;
            deathRoot.SetActive(false);
        }

        public bool DeathVisible { get { return deathRoot.activeSelf; } }
        public string DeathLoseText { get { return deathLose.text; } }
        public string DeathKeepText { get { return deathKeep.text; } }
        public string DeathCountText { get { return deathCount.text; } }

        // The numbers are the ones from before the reset (it happens when the knight stands up again).
        public void ShowDeath(int level, int swords, int swordTotal, int souls, Knight k)
        {
            deathRoot.SetActive(true);
            deathKnight = k;
            deathShownAt = Time.unscaledTime;
            bannerTime = 0f; bannerText.text = "";
            deathLose.text = "You lose:   Lv " + level + "  >  Lv 1\nall upgrade cards and stat points";
            deathKeep.text = "You keep:   Swords (" + swords + " / " + swordTotal + ")      <color=" + Hex(SoulColor) + ">Souls " + souls.ToString("N0") + "</color>";
            Color c = deathDim.color; c.a = 0f; deathDim.color = c;
        }

        public void HideDeath()
        {
            if (deathRoot != null) deathRoot.SetActive(false);
            deathKnight = null;
        }

        // ---------------------------------------------------------------- public API

        // Longer-lived line for things the player should not miss: sword picked up, equipped, sound volume.
        public void Notice(string text, Color color)
        {
            noticeText.text = text;
            noticeColor = color;
            noticeTime = 3.5f;
        }

        public void Banner(string text, Color color, float seconds = 1.6f)
        {
            bannerText.text = text;
            bannerColor = color;
            bannerTime = seconds;
            bannerLength = seconds;
        }

        public string BannerShown { get { return bannerTime > 0f ? bannerText.text : ""; } }

        // Phase 1 loop: the arrow under the banner points at this world point instead of the Minotaur's lair (null: the lair).
        // It shows when the point is farther than minDistance.
        public void SetArrowTarget(Vector3? point, string label, float minDistance)
        {
            arrowPoint = point;
            arrowName = label;
            arrowMin = minDistance;
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
        public void SetVisible(bool on) { canvas.enabled = on; if (on) sessionShownAt = Time.unscaledTime; }

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
                    c.line.text = "+" + (Progress.BlockPerPoint * 100f).ToString("0.#") + "% Block  +" + Progress.ArmorPerPoint.ToString("0") + " Armor";
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
            invFoot.text = "1-6 / Click: equip          Tab / Esc: close          "
                + (Net.Multiplayer ? "(online: the game keeps running!)" : "(the game is paused)");
            lastTabRefresh = -10f;
            RefreshInventory(-1);
        }

        public void HideInventory() { invRoot.SetActive(false); }

        static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

        static readonly Color RarityCommon = new Color(0.69f, 0.69f, 0.69f), RarityUncommon = new Color(0.37f, 0.81f, 0.42f),
            RarityRare = new Color(0.85f, 0.29f, 0.29f), RarityLegendary = new Color(1f, 0.79f, 0.24f);
        static readonly Color SlotCol = new Color(0.12f, 0.12f, 0.17f, 1f);

        static Color RarityColor(string rarity)
        {
            return rarity == "Uncommon" ? RarityUncommon : rarity == "Rare" ? RarityRare : rarity == "Legendary" ? RarityLegendary : RarityCommon;
        }

        // Test read-outs for the Tab window.
        public Texture IconTexture(int slot) { return rows[slot].icon.texture; }
        public Color IconTint(int slot) { return rows[slot].icon.color; }
        public string SlotName(int slot) { return rows[slot].name.text; }
        public bool SlotEquippedMark(int slot) { return rows[slot].eqBar.gameObject.activeSelf; }
        public string SlotBadge(int slot) { return rows[slot].badge.text; }
        public string TabCharacterText { get { return charText.text; } }
        public string TabSoulText { get { return soulKills.text; } }
        public Texture EquippedIconTexture { get { return eqIcon.texture; } }
        public bool TabHintBlinking { get { return tabHint.gameObject.activeSelf; } }
        public RectTransform TabPanelRect { get { return invFrame.rectTransform; } }

        // Slot colours and the cheap per-frame parts; the heavy text is rebuilt only every 0.2 s (or after an equip).
        void RefreshInventory(int hover)
        {
            Knight k = Game.I.knight;
            Inventory inv = k.inventory;
            Progress pr = k.progress;
            bool rebuild = Time.unscaledTime - lastTabRefresh > 0.2f;
            if (rebuild) lastTabRefresh = Time.unscaledTime;

            for (int i = 0; i < rows.Length; i++)
            {
                WeaponDef w = Weapons.All[i];
                RowUi r = rows[i];
                bool own = inv.owned[i], eq = inv.equipped == i;
                Color rc = RarityColor(w.rarity);
                r.border.color = eq ? new Color(0.4f, 1f, 0.45f) : own ? rc : new Color(rc.r * 0.35f, rc.g * 0.35f, rc.b * 0.35f);
                r.bg.color = i == hover && own ? new Color(0.2f, 0.2f, 0.3f, 1f) : eq ? new Color(0.14f, 0.2f, 0.16f, 1f) : SlotCol;
                if (!rebuild) continue;
                r.key.color = own ? Color.white : new Color(0.55f, 0.55f, 0.6f);
                r.icon.texture = own ? SwordIcons.Get(i) : SwordIcons.Shadow(i);
                r.icon.color = Color.white;
                r.qmark.gameObject.SetActive(!own);
                r.eqBar.gameObject.SetActive(eq);
                r.name.color = own ? w.uiColor : new Color(0.55f, 0.55f, 0.6f);
                r.name.text = own ? w.name : "???";
                r.badge.text = own && inv.plus[i] > 0 ? (inv.plus[i] >= Weapons.MaxPlus ? "+" + inv.plus[i] + " MAX" : "+" + inv.plus[i]) : "";
                r.state.color = own ? new Color(0.78f, 0.8f, 0.86f) : new Color(0.7f, 0.62f, 0.45f);
                r.state.text = eq ? "" : own ? "press " + (i + 1) : w.dropHint;
            }
            if (!rebuild) return;

            // equipped sword: big picture + numbers
            WeaponDef cur = k.Weapon;
            eqIcon.texture = SwordIcons.Get(cur.id);
            eqName.text = "<color=" + Hex(cur.uiColor) + "><b>" + inv.NameOf(inv.equipped) + "</b></color>\n<size=14><color=" + Hex(RarityColor(cur.rarity)) + ">" + cur.rarity + "</color></size>";
            eqStats.text = "DMG x" + inv.DamageOf(cur.id).ToString("0.00") + "   SPD x" + cur.speed.ToString("0.00")
                + "\nCRIT +" + (cur.critChance * 100f).ToString("0") + "% / +" + cur.critMult.ToString("0.0") + "x   REACH x" + cur.reach.ToString("0.00")
                + "\n<color=#9aa0b0>" + cur.hint + "</color>";
            ownedText.text = "Owned " + inv.OwnedCount + " / " + Weapons.All.Length;

            // CHARACTER
            bool max = pr.IsMax;
            charText.text = "<b>Lv " + pr.level + " / " + Progress.MaxLevel + "</b>" + (max ? "   <color=#ffd34d>MAX LEVEL</color>" : "");
            charStats.text = "<b>Health</b> " + k.MaxHp.ToString("0") + "     <b>Armor</b> " + k.MaxArmor.ToString("0") + "     <b>Stamina</b> " + k.MaxStamina.ToString("0")
                + "\nSword hit x" + k.DamageMult(-1).ToString("0.0") + "    Swing x" + k.SwingSpeed.ToString("0.0")
                + "\nCrit " + (k.CritChance(-1) * 100f).ToString("0") + "% x" + k.CritMult(-1).ToString("0.0") + "    Reach x" + cur.reach.ToString("0.00");
            float expFrac = max ? 1f : Mathf.Clamp01(pr.exp / Progress.Need(pr.level));
            expFill.anchorMax = new Vector2(Mathf.Max(0.0001f, expFrac), 1f);
            expFill.offsetMax = new Vector2(-2f * expFrac, -2f);
            expValue.text = max ? "MAX" : Mathf.FloorToInt(pr.exp) + " / " + Progress.Need(pr.level);

            // STAT POINTS
            int spent = pr.Points(Attr.Hp) + pr.Points(Attr.Block) + pr.Points(Attr.Stamina);
            statHead.text = "<b>STAT POINTS</b>    spent " + spent + " / " + (Progress.MaxLevel - 1)
                + (pr.statPoints > 0 ? "   <color=#ffe066>(" + pr.statPoints + " unspent)</color>" : "");
            string[] effect =
            {
                "+" + pr.HpBonus.ToString("0") + " max health",
                "+" + pr.ArmorBonus.ToString("0") + " armor, shield stops " + (pr.BlockReduction * 100f).ToString("0") + "%",
                "+" + pr.StaminaBonus.ToString("0") + " stamina, regen +" + ((pr.StaminaRegenMult - 1f) * 100f).ToString("0") + "%"
            };
            for (int a = 0; a < 3; a++)
            {
                float f = pr.attr[a] / (float)Progress.AttrCap;
                statFill[a].anchorMax = new Vector2(Mathf.Max(0.0001f, f), 1f);
                statFill[a].offsetMax = new Vector2(-2f * f, -2f);
                statNum[a].text = pr.attr[a] + "/" + Progress.AttrCap;
                statEffect[a].text = effect[a];
            }

            // SKILLS
            var sb = new System.Text.StringBuilder(300);
            sb.Append("<b>SKILLS</b>   (upgrades)\n");
            for (int i = 0; i < 4; i++)
            {
                SkillDef s = Knight.Skills[i];
                sb.Append(s.key).Append(' ').Append(s.name.PadRight(12))
                  .Append(" dmg x").Append(k.DamageMult(i).ToString("0.0"))
                  .Append("  spd x").Append(pr.SpeedMult(i).ToString("0.0"))
                  .Append("  crit ").Append((k.CritChance(i) * 100f).ToString("0")).Append("%\n");
            }
            skillsText.text = sb.ToString();

            soulKills.text = "<color=" + Hex(SoulColor) + "><b>SOUL " + k.souls.ToString("N0") + "</b></color>        Kills " + pr.kills;
        }

        // Returns the sword to equip (index into Weapons.All) or -1. Keys 1..6 or a left click on an owned slot.
        public int InventoryInput()
        {
            if (!invRoot.activeSelf) return -1;
            Inventory inv = Game.I.knight.inventory;
            int pick = -1;
            KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6 };
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
            if (pick >= 0) lastTabRefresh = -10f;                     // show the change in this very frame
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

        // test read-outs
        public bool LairArrowShown { get { return lairArrow != null && lairArrow.gameObject.activeSelf; } }
        public string LairArrowText { get { return lairText != null ? lairText.text : ""; } }
        public float LairArrowAngle { get { return lairArrow != null ? lairArrow.localEulerAngles.z : 0f; } }
        public bool BossPanelShown { get { return bossPanel != null && bossPanel.activeSelf; } }

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
            UpdateSoul(k);
            if (deathRoot.activeSelf && deathKnight != null)
            {
                // fades in over 0.6 s: dark red, a little less dark online (the others keep fighting)
                float a = Mathf.Clamp01((Time.unscaledTime - deathShownAt) / 0.6f) * (Net.Multiplayer ? 0.35f : 0.75f);
                Color dc = deathDim.color; dc.a = a; deathDim.color = dc;
                int left = Mathf.CeilToInt(deathKnight.ReviveIn - 0.001f);
                deathCount.text = (Net.Multiplayer ? "Returning in  " : "Returning to town in  ") + Mathf.Max(0, left) + " ...";
                deathCount.color = new Color(1f, 1f, 1f, 0.85f);
            }
            expBar.label.text = "LV " + pr.level;
            if (pr.IsMax) UpdateBar(expBar, 1f, 1f, dt, false, "MAX LEVEL");
            else UpdateBar(expBar, pr.exp, Progress.Need(pr.level), dt, false, Mathf.FloorToInt(pr.exp) + " / " + Progress.Need(pr.level) + " EXP");

            // "TAB !" blinks while there are unspent stat points; the corner hint shows for the first 5 seconds
            bool blink = pr.statPoints > 0 && !g.PanelOpen && Mathf.Sin(Time.unscaledTime * 7f) > -0.3f;
            if (tabHint.gameObject.activeSelf != blink) tabHint.gameObject.SetActive(blink);
            float hintAge = Time.unscaledTime - sessionShownAt;
            bool hintOn = hintAge < 5f && !g.PanelOpen;
            if (tabCornerHint.gameObject.activeSelf != hintOn) tabCornerHint.gameObject.SetActive(hintOn);
            if (hintOn) tabCornerHint.color = new Color(1f, 1f, 1f, Mathf.Clamp01((5f - hintAge) / 1f) * 0.7f);

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

            // boss bar: only when the Minotaur is within 45 m (the map is big); the arrow to the lair covers the rest
            Minotaur m = g.minotaur;
            bool bossFar = m != null && !m.Dead && Vector3.Distance(m.transform.position, k.Pos) > 45f;
            bool loopIdle = g.Loop && (!g.run.InWorld || m == null || m.Dead);        // Phase 1: nothing to show at the base or after the boss
            bool hideBoss = bossFar || loopIdle;
            if (bossPanel.activeSelf == hideBoss) bossPanel.SetActive(!hideBoss);
            Vector3 toLair = (arrowPoint.HasValue ? arrowPoint.Value : MapLayout.Lair) - k.Pos; toLair.y = 0f;
            bool arrowOn = toLair.magnitude >= arrowMin && !k.Dead && !(g.Loop && !g.run.InWorld);
            if (lairArrow.gameObject.activeSelf != arrowOn) { lairArrow.gameObject.SetActive(arrowOn); lairText.gameObject.SetActive(arrowOn); }
            if (arrowOn)
            {
                // the camera never turns: north on the map is the top of the screen
                lairArrow.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(toLair.x, toLair.z) * Mathf.Rad2Deg);
                lairText.text = arrowName + "  " + Mathf.RoundToInt(toLair.magnitude) + " m";
            }
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
                float pop = 1f + Mathf.Clamp01((bannerTime - (bannerLength - 0.15f)) / 0.15f) * 0.25f;
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

        public string SoulShown { get { return soulText.text; } }

        void UpdateSoul(Knight k)
        {
            if (k.souls != soulShown)
            {
                if (k.souls > soulShown) soulBumpAt = Time.unscaledTime;
                soulShown = k.souls;
                soulText.text = soulShown.ToString("N0");
            }
            float t = Mathf.Clamp01((Time.unscaledTime - soulBumpAt) / 0.3f);
            float s = 1f + 0.2f * Mathf.Sin(t * Mathf.PI);              // a short 1.2x bounce when it goes up
            soulText.rectTransform.localScale = Vector3.one * s;
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

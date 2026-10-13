using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MedievalKnight
{
    // The Phase 1 screens on a canvas of their own (above the HUD, below the start menu): PREPARE, the end-of-run
    // summary, the carry window in the world, the fade. Like Menu there is no EventSystem: every button and cell
    // is hit-tested by hand against the mouse (InputX), so it works with the new Input System alone.
    // LoopUi only reads the Loadout and draws it; every change goes back through RunLoop.
    public class LoopUi : MonoBehaviour
    {
        public static readonly Color Green = new Color(0.435f, 0.82f, 0.435f), Red = new Color(0.878f, 0.314f, 0.314f),
            Yellow = new Color(0.949f, 0.757f, 0.306f), Gold = new Color(0.851f, 0.643f, 0.255f), Disabled = new Color(0.333f, 0.333f, 0.333f),
            PanelBg = new Color(0.063f, 0.078f, 0.094f, 0.85f), FrameCol = new Color(0.227f, 0.267f, 0.314f),
            SlotBg = new Color(0.12f, 0.12f, 0.17f, 1f), EmptyBorder = new Color(0.2f, 0.23f, 0.28f), Gray = new Color(0.7f, 0.72f, 0.78f);
        // Phase 2: the old-world theme #5E5878 (frames) and a lighter tone of it that reads on the dark panels (text), loot gold #F5C25A
        public static readonly Color OldFrame = new Color(0.369f, 0.345f, 0.471f), OldText = new Color(0.68f, 0.64f, 0.86f), LootGold = new Color(0.961f, 0.761f, 0.353f);

        class Cell
        {
            public int side;                    // 0 = stash, 1 = carry
            public Section sec;
            public int index;
            public RectTransform rt;
            public Image border, bg, cursor, tile;
            public RawImage icon;
            public Text name, badge, tag;
            public float shakeUntil;
            public Vector2 basePos;
        }

        class Btn
        {
            public RectTransform rt;
            public Image frame, bg;
            public Text text;
        }

        RunLoop run;
        Font font;
        Canvas canvas;
        RectTransform root;

        // PREPARE
        GameObject prepareRoot;
        readonly Cell[][][] cells = new Cell[2][][];
        readonly List<Cell> allCells = new List<Cell>();
        readonly Text[][] sectionLabels = new Text[2][];
        Text warn, info, introText, hint;
        Btn enterBtn, backBtn;
        // phase 2: OLD WORLDS button on PREPARE, the red "crumbled" bar, the OLD WORLDS list (H), objective line 2, world badge
        Btn oldBtn;
        Btn readyBtn;                       // online, a client: READY instead of NEW WORLD (the host enters)
        bool coop, coopHost;
        Image oldDot;
        Text crumbText;
        int btnFocus;                       // keyboard focus on the bottom buttons: 0 none, 1 NEW WORLD, 2 OLD WORLDS
        const int WorldRowsShown = 5, PileCellsShown = 12;
        class WorldRow
        {
            public RectTransform rt;
            public Image frame, bg;
            public Text text, state, more;
            public Image[] pips = new Image[OldWorld.StartLeft];
            public Image[] tiles = new Image[4];
            public RawImage[] icons = new RawImage[4];
        }
        GameObject worldsRoot;
        readonly WorldRow[] worldRows = new WorldRow[WorldRowsShown];
        readonly List<Cell> worldCells = new List<Cell>();
        Text worldsHint, worldsMore, worldsPileMore, worldsNote, worldsWarn;
        Btn worldsEnterBtn, worldsBackBtn;
        int worldSel, worldScroll;
        List<OldWorld> worldList = new List<OldWorld>();
        Text objective2, badge;
        Image badgeFrame;
        string badgeShown = "";
        // phase 2: the loot window (L) and the marker of the pile
        const int LootCols = 6, LootRows = 4;
        GameObject lootRoot;
        readonly List<Cell> lootPileCells = new List<Cell>();
        readonly Cell[][] lootPackGrid = new Cell[3][];
        readonly List<Cell> lootPackCells = new List<Cell>();
        Text lootTitle, lootPileHead, lootInfo, lootWarn;
        Btn lootTakeAllBtn;
        int lootCursor, lootScroll;
        float lootWarnUntil;
        Text lootLabel, lootArrow, lootDist;
        // always on top: the fade, the objective line, the carried-weapons strip
        // the carry window (Tab in the world)
        GameObject carryRoot;
        readonly List<Cell> carryCells = new List<Cell>();
        readonly Cell[][] carryGrid = new Cell[3][];
        Text carryChar, carryInfo, carryHead;
        // end-of-run summary
        GameObject summaryRoot;
        Image deathDim;
        Text sumTitle, sumSub, sumBody;
        Btn returnBtn;
        Image fade;
        float fadeFrom, fadeTarget, fadeStart, fadeDur = 1f;
        Text objective, strip, portalLabel;
        Cell cursorCell;
        string prompt = "";
        float warnUntil;

        public static LoopUi Create(Transform parent, RunLoop run)
        {
            var go = new GameObject("LoopUi");
            go.transform.SetParent(parent, false);
            var ui = go.AddComponent<LoopUi>();
            ui.run = run;
            ui.Build();
            return ui;
        }

        // ---------------------------------------------------------------- builders

        RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        Image Box(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var img = Rect(name, parent, anchor, pos, size).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text Label(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, string text = "")
        {
            var t = Rect(name, parent, anchor, pos, size).gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.text = text;
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.9f);
            o.effectDistance = new Vector2(1.2f, -1.2f);
            return t;
        }

        static readonly Vector2 TL = new Vector2(0f, 1f), Mid = new Vector2(0.5f, 0.5f);

        Btn MakeButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string text, int fontSize)
        {
            var b = new Btn();
            b.frame = Box(name, parent, anchor, pos, size, FrameCol);
            b.rt = b.frame.rectTransform;
            b.bg = Box("Bg", b.rt, Mid, Vector2.zero, size - new Vector2(6, 6), new Color(0.14f, 0.16f, 0.24f, 0.96f));
            b.text = Label("Text", b.rt, Mid, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, text);
            b.text.fontStyle = FontStyle.Bold;
            return b;
        }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            var cgo = new GameObject("Canvas");
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f;
            root = canvas.GetComponent<RectTransform>();

            BuildPrepare();
            BuildWorlds();
            BuildLoot();
            BuildCarry();
            BuildSummary();
            BuildOverlays();
        }

        void BuildOverlays()
        {
            objective = Label("Objective", root, new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(900, 28), 21, TextAnchor.MiddleCenter);
            objective.fontStyle = FontStyle.Bold;
            objective.color = Yellow;
            objective2 = Label("Objective2", root, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(900, 26), 19, TextAnchor.MiddleCenter);
            objective2.fontStyle = FontStyle.Bold;
            objective2.color = LootGold;
            // under the health / level box (the controls panel of the HUD sits in the top right corner)
            badgeFrame = Box("BadgeFrame", root, new Vector2(0f, 1f), new Vector2(16f, -206f), new Vector2(250f, 70f), OldFrame);
            badgeFrame.rectTransform.pivot = new Vector2(0f, 1f);
            Box("Bg", badgeFrame.rectTransform, Mid, Vector2.zero, new Vector2(244f, 64f), new Color(0.03f, 0.03f, 0.06f, 0.7f));
            badge = Label("Badge", badgeFrame.rectTransform, Mid, Vector2.zero, new Vector2(240f, 64f), 16, TextAnchor.MiddleCenter);
            badgeFrame.gameObject.SetActive(false);
            strip = Label("Strip", root, new Vector2(0f, 0f), new Vector2(16f, 46f), new Vector2(700, 26), 18, TextAnchor.MiddleLeft);
            strip.rectTransform.pivot = new Vector2(0f, 0.5f);
            strip.fontStyle = FontStyle.Bold;
            strip.text = "";
            portalLabel = Label("PortalLabel", root, new Vector2(0f, 0f), Vector2.zero, new Vector2(300, 28), 22, TextAnchor.MiddleCenter, "RETURN PORTAL");
            portalLabel.rectTransform.pivot = Mid;
            portalLabel.fontStyle = FontStyle.Bold;
            portalLabel.color = new Color(0.435f, 0.784f, 1f);
            portalLabel.gameObject.SetActive(false);
            lootLabel = Label("LootLabel", root, new Vector2(0f, 0f), Vector2.zero, new Vector2(300, 28), 22, TextAnchor.MiddleCenter, "YOUR LOOT");
            lootLabel.rectTransform.pivot = Mid; lootLabel.fontStyle = FontStyle.Bold; lootLabel.color = LootGold;
            lootLabel.gameObject.SetActive(false);
            lootArrow = Label("LootArrow", root, new Vector2(0f, 0f), Vector2.zero, new Vector2(60, 60), 54, TextAnchor.MiddleCenter, ">");
            lootArrow.rectTransform.pivot = Mid; lootArrow.fontStyle = FontStyle.Bold; lootArrow.color = LootGold;
            lootArrow.gameObject.SetActive(false);
            lootDist = Label("LootDist", root, new Vector2(0f, 0f), Vector2.zero, new Vector2(200, 26), 18, TextAnchor.MiddleCenter, "Loot");
            lootDist.rectTransform.pivot = Mid; lootDist.fontStyle = FontStyle.Bold; lootDist.color = LootGold;
            lootDist.gameObject.SetActive(false);
            fade = Box("Fade", root, Mid, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            fade.rectTransform.anchorMin = Vector2.zero; fade.rectTransform.anchorMax = Vector2.one;
            fade.rectTransform.offsetMin = fade.rectTransform.offsetMax = Vector2.zero;
        }

        // ---------------------------------------------------------------- fade / objective / weapon strip

        public float FadeAlpha { get { return fade.color.a; } }
        public string ObjectiveText { get { return objective.text; } }
        public string StripText { get { return strip.text; } }

        // Black screen towards "alpha" (0 clear, 1 black) over "seconds" of real time.
        public void FadeTo(float alpha, float seconds)
        {
            fadeFrom = fade.color.a;
            fadeTarget = alpha;
            fadeStart = Time.unscaledTime;
            fadeDur = Mathf.Max(0.001f, seconds);
        }

        public void SetFade(float alpha)
        {
            fadeFrom = fadeTarget = alpha;
            fadeStart = Time.unscaledTime - 10f;
            fade.color = new Color(0f, 0f, 0f, alpha);
        }

        public void SetObjective(string text) { objective.text = text ?? ""; }

        // Second line of the objective (old world: "Recover your loot", gold)
        public void SetObjective2(string text) { objective2.text = text ?? ""; }
        public string Objective2Text { get { return objective2.text; } }

        // Top right badge in an old world: "OLD WORLD #A3F2 / HP x1.5  DMG x1.25 / Crumbles in 3 returns" (null = hidden).
        // last: one return left (yellow frame).
        public void SetOldBadge(string text, bool last)
        {
            string key = text == null ? "" : text + (last ? "!" : "");
            if (key == badgeShown) return;
            badgeShown = key;
            badgeFrame.gameObject.SetActive(text != null);
            if (text != null) { badge.text = text; badgeFrame.color = last ? Yellow : OldFrame; }
        }
        public string BadgeText { get { return badgeFrame.gameObject.activeSelf ? badge.text : ""; } }
        public bool BadgeYellow { get { return badgeFrame.color == Yellow; } }

        public bool PortalLabelShown { get { return portalLabel.gameObject.activeSelf; } }

        // "RETURN PORTAL" floating over the portal (world point -> screen).
        public void SetPortalLabel(Camera cam, Vector3 world, bool show)
        {
            if (!show || cam == null) { if (portalLabel.gameObject.activeSelf) portalLabel.gameObject.SetActive(false); return; }
            Vector3 sp = cam.WorldToScreenPoint(world);
            bool on = sp.z > 0f;
            if (portalLabel.gameObject.activeSelf != on) portalLabel.gameObject.SetActive(on);
            if (on) portalLabel.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y) / Mathf.Max(0.01f, canvas.scaleFactor);
        }

        // "[1] Iron Sword +2   [2] Swift Rapier   [C] Apple x2" (the held weapon in green)
        public void SetStrip(Loadout d, bool show)
        {
            if (!show) { strip.text = ""; return; }
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < d.carry.weapons.Length; i++)
            {
                Item it = d.carry.weapons[i];
                string label = it.IsNone ? "-" : it.Name;
                string col = i == d.heldSlot && it.IsWeapon ? "#6FD16F" : "#C8CCD6";
                sb.Append("<color=" + col + ">[" + (i + 1) + "] " + label + "</color>   ");
            }
            // C eats the first food: its name and how many of that kind, plus how many other foods are packed
            Item food = Item.None;
            int same = 0, other = 0;
            foreach (Item it in d.carry.foods) if (it.kind == ItemKind.Food) { if (food.IsNone) food = it; if (it.id == food.id) same++; else other++; }
            foreach (Item it in d.carry.supplies) if (it.kind == ItemKind.Food) { if (food.IsNone) food = it; if (it.id == food.id) same++; else other++; }
            sb.Append(same > 0 ? "<color=#F2C14E>[C] " + food.BaseName + " x" + same + (other > 0 ? " (+" + other + ")" : "") + "</color>" : "<color=#8A8F9C>[C] no food</color>");
            strip.text = sb.ToString();
        }

        void Update()
        {
            bool panel = (prepareRoot != null && prepareRoot.activeSelf) || (carryRoot != null && carryRoot.activeSelf) || (summaryRoot != null && summaryRoot.activeSelf)
                || (lootRoot != null && lootRoot.activeSelf);
            if (objective2.gameObject.activeSelf == panel) objective2.gameObject.SetActive(!panel);
            if (badgeShown.Length > 0 && badgeFrame.gameObject.activeSelf == panel) badgeFrame.gameObject.SetActive(!panel);
            if (objective.gameObject.activeSelf == panel) objective.gameObject.SetActive(!panel);
            if (strip.gameObject.activeSelf == panel) strip.gameObject.SetActive(!panel);
            float t = Mathf.Clamp01((Time.unscaledTime - fadeStart) / fadeDur);
            float a = Mathf.Lerp(fadeFrom, fadeTarget, t);
            if (Mathf.Abs(fade.color.a - a) > 0.001f) fade.color = new Color(0f, 0f, 0f, a);
        }

        // PREPARE: 1000 x 600. Stash (cells 54 px, 8 per row) on the left, the pack to take into the world (cells 88 px) on the right.
        void BuildPrepare()
        {
            RectTransform pr = Rect("Prepare", root, Mid, Vector2.zero, Vector2.zero);
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
            prepareRoot = pr.gameObject;
            Image dim = Box("Dim", pr, Mid, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0.03f, 0.5f));
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
            Image frame = Box("Frame", pr, Mid, Vector2.zero, new Vector2(1006, 578), FrameCol);
            RectTransform p = Box("Bg", frame.transform, Mid, Vector2.zero, new Vector2(1000, 572), PanelBg).rectTransform;

            Text title = Label("Title", p, TL, new Vector2(20, -14), new Vector2(300, 44), 36, TextAnchor.MiddleLeft, "PREPARE");
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.pivot = TL; title.rectTransform.anchoredPosition = new Vector2(20, -10);
            introText = Label("Intro", p, TL, new Vector2(300, -20), new Vector2(680, 24), 15, TextAnchor.MiddleLeft,
                "Everything you carry is lost if you fall. Defeat the Minotaur to return.");
            introText.color = Yellow;
            introText.rectTransform.pivot = TL; introText.rectTransform.anchoredPosition = new Vector2(250, -24);

            Text head0 = Label("StashHead", p, TL, Vector2.zero, new Vector2(300, 24), 18, TextAnchor.MiddleLeft, "STASH");
            head0.fontStyle = FontStyle.Bold; head0.rectTransform.pivot = TL; head0.rectTransform.anchoredPosition = new Vector2(20, -62);
            Text head1 = Label("CarryHead", p, TL, Vector2.zero, new Vector2(400, 24), 18, TextAnchor.MiddleLeft, "CARRY INTO WORLD");
            head1.fontStyle = FontStyle.Bold; head1.rectTransform.pivot = TL; head1.rectTransform.anchoredPosition = new Vector2(540, -62);
            Box("Divider", p, TL, new Vector2(520, -60), new Vector2(2, 380), FrameCol).rectTransform.pivot = TL;

            cells[0] = new Cell[3][];
            cells[1] = new Cell[3][];
            sectionLabels[0] = new Text[3];
            sectionLabels[1] = new Text[3];
            string[] names = { "Weapons", "Food", "Supplies" };
            // stash
            float[] labelY0 = { 86f, 228f, 312f };
            for (int s = 0; s < 3; s++)
            {
                int cap = Loadout.StashCaps[s];
                sectionLabels[0][s] = Label(names[s] + "Label0", p, TL, Vector2.zero, new Vector2(400, 20), 14, TextAnchor.MiddleLeft);
                sectionLabels[0][s].color = Gray; sectionLabels[0][s].rectTransform.pivot = TL;
                sectionLabels[0][s].rectTransform.anchoredPosition = new Vector2(20, -labelY0[s]);
                cells[0][s] = new Cell[cap];
                for (int i = 0; i < cap; i++)
                    cells[0][s][i] = MakeCell(p, 0, (Section)s, i, new Vector2(20 + (i % 8) * 60f, -(labelY0[s] + 22f + (i / 8) * 60f)), 54f);
            }
            // carry
            float[] labelY1 = { 86f, 206f, 326f };
            for (int s = 0; s < 3; s++)
            {
                int cap = Loadout.CarryCaps[s];
                sectionLabels[1][s] = Label(names[s] + "Label1", p, TL, Vector2.zero, new Vector2(400, 20), 14, TextAnchor.MiddleLeft);
                sectionLabels[1][s].color = Gray; sectionLabels[1][s].rectTransform.pivot = TL;
                sectionLabels[1][s].rectTransform.anchoredPosition = new Vector2(540, -labelY1[s]);
                cells[1][s] = new Cell[cap];
                for (int i = 0; i < cap; i++)
                    cells[1][s][i] = MakeCell(p, 1, (Section)s, i, new Vector2(540 + i * 96f, -(labelY1[s] + 22f)), 88f);
            }

            info = Label("Info", p, TL, Vector2.zero, new Vector2(960, 40), 15, TextAnchor.UpperLeft);
            info.rectTransform.pivot = TL; info.rectTransform.anchoredPosition = new Vector2(20, -452);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            warn = Label("Warn", p, TL, Vector2.zero, new Vector2(380, 30), 17, TextAnchor.MiddleLeft);
            warn.rectTransform.pivot = TL; warn.rectTransform.anchoredPosition = new Vector2(20, -512);
            warn.fontStyle = FontStyle.Bold;
            enterBtn = MakeButton("EnterButton", p, TL, Vector2.zero, new Vector2(220, 52), "NEW WORLD", 24);
            enterBtn.rt.pivot = TL; enterBtn.rt.anchoredPosition = new Vector2(400, -498);
            oldBtn = MakeButton("OldWorldsButton", p, TL, Vector2.zero, new Vector2(240, 52), "OLD WORLDS (0)", 22);
            oldBtn.rt.pivot = TL; oldBtn.rt.anchoredPosition = new Vector2(630, -498);
            readyBtn = MakeButton("ReadyButton", p, TL, Vector2.zero, new Vector2(220, 52), "READY", 24);
            readyBtn.rt.pivot = TL; readyBtn.rt.anchoredPosition = new Vector2(400, -498);
            readyBtn.rt.gameObject.SetActive(false);
            oldDot = Box("Dot", oldBtn.rt, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(14f, 14f), Yellow);
            oldDot.rectTransform.pivot = new Vector2(1f, 1f);
            oldDot.gameObject.SetActive(false);
            backBtn = MakeButton("BackButton", p, TL, Vector2.zero, new Vector2(110, 52), "Esc: Back", 15);
            backBtn.rt.pivot = TL; backBtn.rt.anchoredPosition = new Vector2(880, -498);
            crumbText = Label("Crumbled", p, TL, Vector2.zero, new Vector2(730, 24), 15, TextAnchor.MiddleLeft);
            crumbText.color = Red; crumbText.fontStyle = FontStyle.Bold;
            crumbText.rectTransform.pivot = TL; crumbText.rectTransform.anchoredPosition = new Vector2(250, -24);
            crumbText.gameObject.SetActive(false);
            hint = Label("Hint", p, TL, Vector2.zero, new Vector2(960, 20), 12, TextAnchor.MiddleLeft,
                "Click an item to move it   -   Right click a carried Weapon to hold it   -   Arrows / WASD: move cursor   Enter: move   Q / E: other side   Tab / Esc: close");
            hint.color = Gray; hint.rectTransform.pivot = TL; hint.rectTransform.anchoredPosition = new Vector2(20, -552);
            prepareRoot.SetActive(false);
        }

        Cell MakeCell(Transform parent, int side, Section sec, int index, Vector2 pos, float size, List<Cell> list = null)
        {
            var c = new Cell { side = side, sec = sec, index = index };
            Image b = Box("Cell", parent, TL, pos, new Vector2(size, size), EmptyBorder);
            b.rectTransform.pivot = TL;
            c.border = b; c.rt = b.rectTransform; c.basePos = pos;
            c.bg = Box("Bg", c.rt, Mid, Vector2.zero, new Vector2(size - 4f, size - 4f), SlotBg);
            c.tile = Box("Tile", c.rt, Mid, Vector2.zero, new Vector2(size * 0.5f, size * 0.5f), Color.white);
            var rawGo = new GameObject("Icon", typeof(RectTransform));
            rawGo.transform.SetParent(c.rt, false);
            c.icon = rawGo.AddComponent<RawImage>();
            c.icon.raycastTarget = false;
            RectTransform irt = rawGo.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.55f);
            irt.sizeDelta = new Vector2(size * 0.74f, size * 0.74f);
            irt.anchoredPosition = Vector2.zero;
            int fs = size > 70f ? 13 : 9;
            c.name = Label("Name", c.rt, new Vector2(0.5f, 0f), new Vector2(0f, size > 70f ? 3f : 1f), new Vector2(size, 16), fs, TextAnchor.LowerCenter);
            c.name.horizontalOverflow = HorizontalWrapMode.Wrap;
            c.badge = Label("Badge", c.rt, new Vector2(1f, 1f), new Vector2(-3f, -2f), new Vector2(size * 0.7f, 18), size > 70f ? 15 : 11, TextAnchor.UpperRight);
            c.badge.rectTransform.pivot = new Vector2(1f, 1f);
            c.badge.fontStyle = FontStyle.Bold;
            c.badge.color = new Color(1f, 0.9f, 0.4f);
            c.tag = Label("Tag", c.rt, new Vector2(0f, 1f), new Vector2(3f, -2f), new Vector2(size * 0.8f, 18), 11, TextAnchor.UpperLeft);
            c.tag.rectTransform.pivot = new Vector2(0f, 1f);
            c.tag.fontStyle = FontStyle.Bold;
            c.tag.color = Green;
            c.cursor = Box("Cursor", c.rt, Mid, Vector2.zero, new Vector2(size + 6f, size + 6f), new Color(1f, 1f, 1f, 0f));
            (list ?? allCells).Add(c);
            return c;
        }

        // ---------------------------------------------------------------- PREPARE

        public bool PrepareVisible { get { return prepareRoot != null && prepareRoot.activeSelf; } }
        public RectTransform CellRect(int side, Section sec, int index) { return cells[side][(int)sec][index].rt; }
        public RectTransform EnterRect { get { return enterBtn.rt; } }                 // NEW WORLD
        public RectTransform ReadyRect { get { return readyBtn.rt; } }
        public string ReadyText { get { return readyBtn.text.text; } }
        public string EnterText { get { return enterBtn.text.text; } }
        public RectTransform OldWorldsRect { get { return oldBtn.rt; } }
        public string OldWorldsText { get { return oldBtn.text.text; } }

        // Online: a client gets READY in place of NEW WORLD; everybody gets the roster (TeamUi).
        string rosterLine = "";
        public void SetRosterLine(string s) { rosterLine = s ?? ""; }

        public void SetCoop(bool on, bool host)
        {
            coop = on; coopHost = host;
            readyBtn.rt.gameObject.SetActive(on && !host);
            enterBtn.rt.gameObject.SetActive(!on || host);
        }
        public bool OldWorldsEnabled { get { return oldBtn.frame.color != Disabled; } }
        public string CrumbBarText { get { return crumbText.gameObject.activeSelf ? crumbText.text : ""; } }
        public bool OldDotShown { get { return oldDot.gameObject.activeSelf; } }
        public int ButtonFocus { get { return btnFocus; } }
        public string WarnText { get { return warn.text; } }
        public string InfoText { get { return info.text; } }
        public string IntroText { get { return introText.gameObject.activeSelf ? introText.text : ""; } }
        public bool PromptShown { get { return prompt.Length > 0; } }

        static Color RarityColor(string rarity)
        {
            return rarity == "Uncommon" ? new Color(0.37f, 0.81f, 0.42f) : rarity == "Rare" ? new Color(0.85f, 0.29f, 0.29f)
                : rarity == "Legendary" ? new Color(1f, 0.79f, 0.24f) : new Color(0.69f, 0.69f, 0.69f);
        }

        void FillCell(Cell c, Item it, bool held)
        {
            bool empty = it.IsNone;
            c.icon.gameObject.SetActive(!empty && it.IsWeapon);
            c.tile.gameObject.SetActive(!empty && !it.IsWeapon);
            c.name.text = empty ? "" : it.BaseName;
            c.badge.text = !empty && it.IsWeapon && it.plus > 0 ? "+" + it.plus : "";
            c.tag.text = held ? "HELD" : "";
            if (empty) { c.border.color = EmptyBorder; c.bg.color = SlotBg; return; }
            if (it.IsWeapon)
            {
                WeaponDef w = Weapons.All[it.id];
                c.icon.texture = SwordIcons.Get(it.id);
                c.border.color = held ? Green : RarityColor(w.rarity);
                c.name.color = w.uiColor;
            }
            else
            {
                FoodDef f = Foods.All[it.id];
                c.tile.color = f.color;
                c.border.color = f.color * 0.8f;
                c.name.color = Color.white;
            }
            c.bg.color = held ? new Color(0.14f, 0.2f, 0.16f, 1f) : SlotBg;
        }

        // Redraws every cell and counter from the data.
        public void RefreshPrepare(Loadout d)
        {
            for (int side = 0; side < 2; side++)
            {
                ItemBag bag = side == 0 ? d.stash : d.carry;
                for (int s = 0; s < 3; s++)
                {
                    Item[] arr = bag.Slots((Section)s);
                    for (int i = 0; i < arr.Length; i++)
                        FillCell(cells[side][s][i], arr[i], side == 1 && s == 0 && i == d.heldSlot && arr[i].IsWeapon);
                }
            }
            string[] names = { "Weapons", "Food", "Supplies" };
            for (int s = 0; s < 3; s++)
            {
                sectionLabels[0][s].text = names[s] + "  " + d.stash.Count((Section)s) + "/" + Loadout.StashCaps[s];
                sectionLabels[1][s].text = names[s] + "  " + d.carry.Count((Section)s) + "/" + Loadout.CarryCaps[s];
            }
            bool crumb = d.unseen.Count > 0;
            introText.gameObject.SetActive(coop ? !crumb : !d.introShown && !crumb);
            if (coop) introText.text = rosterLine;                 // online: who is here and who is ready, where the intro line was
            crumbText.gameObject.SetActive(crumb);
            if (crumb) crumbText.text = CrumbSentence(d);
            bool can = d.CanEnter;
            enterBtn.bg.color = can ? new Color(0.45f, 0.34f, 0.12f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f);
            enterBtn.frame.color = btnFocus == 1 ? Color.white : can ? Gold : Disabled;
            int n = run.WorldCount;
            bool last = false;
            foreach (OldWorld w in run.WorldList()) if (w.left <= 1) last = true;
            oldBtn.text.text = "OLD WORLDS (" + n + ")";
            if (coop)
            {
                if (coopHost) enterBtn.text.text = "NEW WORLD  " + run.team.ReadyCount + "/" + run.team.RoomCount;
                else
                {
                    bool rdy = run.team.LocalReady;
                    readyBtn.text.text = rdy ? "READY  (cancel)" : "READY";
                    readyBtn.bg.color = rdy ? new Color(0.14f, 0.3f, 0.16f, 1f) : can ? new Color(0.45f, 0.34f, 0.12f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f);
                    readyBtn.frame.color = btnFocus == 1 ? Color.white : rdy ? Green : can ? Gold : Disabled;
                }
            }
            oldBtn.bg.color = n > 0 ? new Color(0.22f, 0.2f, 0.32f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f);
            oldBtn.frame.color = btnFocus == 2 ? Color.white : n > 0 ? OldText : Disabled;
            oldBtn.text.color = n > 0 ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
            oldDot.gameObject.SetActive(n > 0 && last);
            if (Time.unscaledTime >= warnUntil) ShowDefaultWarn(d);
        }

        // "! Old World #B71C has crumbled. 4 items are gone forever."
        static string CrumbSentence(Loadout d)
        {
            int items = 0;
            foreach (CrumbNote c in d.unseen) items += c.items;
            string gone = items > 0 ? " " + items + (items == 1 ? " item is" : " items are") + " gone forever." : "";
            if (d.unseen.Count == 1) return "! Old World " + d.unseen[0].Code + " has crumbled." + gone;
            var sb = new System.Text.StringBuilder("! " + d.unseen.Count + " Old Worlds have crumbled (");
            for (int i = 0; i < d.unseen.Count && i < 4; i++) sb.Append(i > 0 ? ", " : "").Append(d.unseen[i].Code);
            if (d.unseen.Count > 4) sb.Append(", ...");
            return sb.Append(")." + gone).ToString();
        }

        void ShowDefaultWarn(Loadout d)
        {
            if (prompt.Length > 0) { warn.text = prompt; warn.color = Yellow; return; }
            if (!d.CanEnter) { warn.text = "Pack a Weapon first"; warn.color = Red; return; }
            if (coop && !coopHost)
            {
                bool food0 = d.carry.Count(Section.Foods) > 0;
                if (!food0) foreach (Item it in d.carry.supplies) if (it.kind == ItemKind.Food) food0 = true;
                if (run.team.LocalReady) { warn.text = "Waiting for the host to enter a world"; warn.color = Green; return; }
                warn.text = food0 ? "Press READY when your pack is set" : "! No food packed - press READY when set";
                warn.color = Yellow;
                return;
            }
            bool food = d.carry.Count(Section.Foods) > 0;
            if (!food) foreach (Item it in d.carry.supplies) if (it.kind == ItemKind.Food) food = true;
            warn.text = food ? "" : "! No food packed";
            warn.color = Yellow;
        }

        public void SetWarn(string text, Color color, float seconds)
        {
            warn.text = text;
            warn.color = color;
            warnUntil = Time.unscaledTime + seconds;
        }

        public void SetPrompt(string text)
        {
            prompt = text ?? "";
            warnUntil = 0f;
        }

        public void Shake(int side, Section sec)
        {
            foreach (Cell c in cells[side][(int)sec]) c.shakeUntil = Time.unscaledTime + 0.4f;
        }

        public void ShowPrepare(Loadout d)
        {
            prepareRoot.SetActive(true);
            prompt = "";
            warnUntil = 0f;
            btnFocus = 0;
            if (cursorCell == null) cursorCell = cells[0][0][0];
            RefreshPrepare(d);
            UpdateInfo(d, cursorCell);
        }

        public void HideAll()
        {
            if (prepareRoot != null) prepareRoot.SetActive(false);
            if (worldsRoot != null) worldsRoot.SetActive(false);
            if (lootRoot != null) lootRoot.SetActive(false);
            if (carryRoot != null) carryRoot.SetActive(false);
            if (summaryRoot != null) summaryRoot.SetActive(false);
        }

        Item ItemOf(Loadout d, Cell c)
        {
            return (c.side == 0 ? d.stash : d.carry).Slots(c.sec)[c.index];
        }

        void UpdateInfo(Loadout d, Cell c)
        {
            Item it = c != null ? ItemOf(d, c) : Item.None;
            if (it.IsNone) { info.text = "<color=#9aa0b0>Select an item to see what it does.</color>"; return; }
            if (it.IsWeapon)
            {
                WeaponDef w = Weapons.All[it.id];
                float dmg = w.damage * (1f + Weapons.DamagePerPlus * it.plus);
                info.text = "<b><color=#" + ColorUtility.ToHtmlStringRGB(w.uiColor) + ">" + it.Name + "</color></b>  <color=#9aa0b0>" + w.rarity + " - " + w.hint + "</color>\n"
                    + "DMG x" + dmg.ToString("0.00") + "   SPD x" + w.speed.ToString("0.00") + "   CRIT +" + (w.critChance * 100f).ToString("0") + "% / +"
                    + w.critMult.ToString("0.0") + "x   REACH x" + w.reach.ToString("0.00");
            }
            else
            {
                FoodDef f = Foods.All[it.id];
                info.text = "<b>" + f.name + "</b>  <color=#9aa0b0>Food</color>\nHeals " + Mathf.RoundToInt(f.healFrac * 100f) + "% of your max health. Press C in the world to eat.";
            }
        }

        // Which cell is under the mouse (null: none).
        Cell CellAt(Vector2 m)
        {
            if (!InputX.HasMouse) return null;
            foreach (Cell c in allCells)
                if (c.rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(c.rt, m, null)) return c;
            return null;
        }

        // The cell nearest to "from" in a direction (screen space), for the keyboard cursor.
        Cell Neighbour(Cell from, Vector2 dir)
        {
            Cell best = null;
            float bestScore = float.MaxValue;
            Vector2 a = from.rt.position;
            foreach (Cell c in allCells)
            {
                if (c == from) continue;
                Vector2 d = (Vector2)c.rt.position - a;
                float along = Vector2.Dot(d, dir);
                if (along < 8f) continue;
                float across = Mathf.Abs(d.x * dir.y - d.y * dir.x);
                float score = along + across * 2.5f;
                if (score < bestScore) { bestScore = score; best = c; }
            }
            return best ?? from;
        }

        Cell OtherSide(Cell from)
        {
            Cell best = null;
            float bestD = float.MaxValue;
            foreach (Cell c in allCells)
            {
                if (c.side == from.side) continue;
                float d = Mathf.Abs(c.rt.position.y - from.rt.position.y) + (c.sec == from.sec ? 0f : 40f) + c.index * 2f;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best ?? from;
        }

        // Called every frame while PREPARE is open.
        public void PrepareInput(Loadout d)
        {
            float now = Time.unscaledTime;
            Vector2 m = InputX.MousePos;
            Cell hover = CellAt(m);
            bool onEnter = InputX.HasMouse && enterBtn.rt.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(enterBtn.rt, m, null);
            bool onReady = InputX.HasMouse && readyBtn.rt.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(readyBtn.rt, m, null);
            bool onBack = InputX.HasMouse && RectTransformUtility.RectangleContainsScreenPoint(backBtn.rt, m, null);
            bool onOld = InputX.HasMouse && RectTransformUtility.RectangleContainsScreenPoint(oldBtn.rt, m, null);
            if (onOld && run.WorldCount == 0) SetWarn("No old worlds yet", Gray, 0.2f);
            backBtn.bg.color = Color.Lerp(backBtn.bg.color, onBack ? new Color(0.3f, 0.36f, 0.56f, 1f) : new Color(0.14f, 0.16f, 0.24f, 0.96f), 0.35f);
            if (onEnter && d.CanEnter) enterBtn.bg.color = new Color(0.6f, 0.46f, 0.16f, 1f);
            if (onOld && run.WorldCount > 0) oldBtn.bg.color = new Color(0.32f, 0.3f, 0.46f, 1f);

            // keyboard: the cursor over the cells; Down from the last row goes to the two buttons, Left / Right swap them, Up goes back
            bool kl = InputX.Down(KeyCode.LeftArrow) || InputX.Down(KeyCode.A), kr = InputX.Down(KeyCode.RightArrow) || InputX.Down(KeyCode.D);
            bool ku = InputX.Down(KeyCode.UpArrow) || InputX.Down(KeyCode.W), kd = InputX.Down(KeyCode.DownArrow) || InputX.Down(KeyCode.S);
            bool kq = InputX.Down(KeyCode.Q) || InputX.Down(KeyCode.E);
            if (btnFocus > 0)
            {
                if (kl || kr) btnFocus = btnFocus == 1 ? 2 : 1;
                else if (ku) btnFocus = 0;
                if (kq) { btnFocus = 0; cursorCell = OtherSide(cursorCell); }
            }
            else
            {
                Cell before = cursorCell;
                if (kl) cursorCell = Neighbour(cursorCell, Vector2.left);
                if (kr) cursorCell = Neighbour(cursorCell, Vector2.right);
                if (ku) cursorCell = Neighbour(cursorCell, Vector2.up);
                if (kd) { cursorCell = Neighbour(cursorCell, Vector2.down); if (cursorCell == before) btnFocus = 1; }
                if (kq) cursorCell = OtherSide(cursorCell);
            }

            bool clickL = InputX.HasMouse && InputX.MouseDown(0), clickR = InputX.HasMouse && InputX.MouseDown(1);
            bool key = InputX.Down(KeyCode.Return) || InputX.Down(KeyCode.KeypadEnter) || InputX.Down(KeyCode.Space);
            bool consumed = false;
            if (clickL && onReady) { btnFocus = 0; run.UiReady(); consumed = true; }
            else if (clickL && onEnter) { btnFocus = 0; run.UiEnter(); consumed = true; }
            else if (clickL && onOld) { btnFocus = 0; run.UiOpenWorlds(); consumed = true; }
            else if (clickL && onBack) { run.UiBack(); consumed = true; }
            else if (key && prompt.Length > 0) { run.UiConfirmEnter(); consumed = true; }       // "[Enter] Go"
            else if (key && btnFocus == 1) { if (coop && !coopHost) run.UiReady(); else run.UiEnter(); consumed = true; }
            else if (key && btnFocus == 2) { run.UiOpenWorlds(); consumed = true; }
            else if (clickL && hover != null) { btnFocus = 0; cursorCell = hover; run.UiMove(hover.side, hover.sec, hover.index); consumed = true; }
            else if (clickR && hover != null) { btnFocus = 0; cursorCell = hover; if (hover.side == 1 && hover.sec == Section.Weapons) run.UiHold(hover.index); consumed = true; }
            else if (key && cursorCell != null) { run.UiMove(cursorCell.side, cursorCell.sec, cursorCell.index); consumed = true; }
            if (!run.PrepareOpen) return;                                   // that click entered the world
            RefreshPrepare(d);

            Cell shown = hover ?? cursorCell;
            UpdateInfo(d, shown);
            foreach (Cell c in allCells)
            {
                bool shaking = now < c.shakeUntil;
                c.rt.anchoredPosition = shaking ? c.basePos + new Vector2(Mathf.Sin(now * 70f) * 3f, 0f) : c.basePos;
                if (shaking) c.border.color = Red;
                c.cursor.color = (c == cursorCell && btnFocus == 0) || c == hover ? new Color(1f, 1f, 1f, c == hover ? 0.28f : 0.5f) : new Color(1f, 1f, 1f, 0f);
            }
            if (now >= warnUntil) ShowDefaultWarn(d);
        }
        // ---------------------------------------------------------------- OLD WORLDS list (over PREPARE)

        void BuildWorlds()
        {
            RectTransform wr = Rect("Worlds", root, Mid, Vector2.zero, Vector2.zero);
            wr.anchorMin = Vector2.zero; wr.anchorMax = Vector2.one; wr.offsetMin = wr.offsetMax = Vector2.zero;
            worldsRoot = wr.gameObject;
            Image dim = Box("Dim", wr, Mid, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0.03f, 0.72f));
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
            Image frame = Box("Frame", wr, Mid, Vector2.zero, new Vector2(1006, 566), OldFrame);
            RectTransform p = Box("Bg", frame.transform, Mid, Vector2.zero, new Vector2(1000, 560), PanelBg).rectTransform;

            Text title = Label("Title", p, TL, Vector2.zero, new Vector2(400, 44), 36, TextAnchor.MiddleLeft, "OLD WORLDS");
            title.fontStyle = FontStyle.Bold; title.rectTransform.pivot = TL; title.rectTransform.anchoredPosition = new Vector2(20, -10);
            worldsHint = Label("Harder", p, new Vector2(1f, 1f), new Vector2(-20f, -22f), new Vector2(560, 22), 15, TextAnchor.MiddleRight,
                "Harder: enemies HP x" + OldWorld.HpMul.ToString("0.0") + ", DMG x" + OldWorld.DmgMul.ToString("0.00") + ", +" + Mathf.RoundToInt((OldWorld.PackMul - 1f) * 100f) + "% pack size");
            worldsHint.color = OldText; worldsHint.rectTransform.pivot = new Vector2(1f, 1f);

            for (int i = 0; i < WorldRowsShown; i++)
            {
                var r = new WorldRow();
                r.frame = Box("Row", p, TL, new Vector2(20, -(64 + i * 58)), new Vector2(960, 54), FrameCol);
                r.frame.rectTransform.pivot = TL;
                r.rt = r.frame.rectTransform;
                r.bg = Box("Bg", r.rt, Mid, Vector2.zero, new Vector2(954, 48), SlotBg);
                r.text = Label("Text", r.rt, TL, new Vector2(14, -14), new Vector2(380, 26), 19, TextAnchor.MiddleLeft);
                r.text.rectTransform.pivot = TL;
                for (int t = 0; t < 4; t++)
                {
                    float x = 420f + t * 34f;
                    r.tiles[t] = Box("Tile", r.rt, TL, new Vector2(x, -14), new Vector2(26, 26), Color.white);
                    r.tiles[t].rectTransform.pivot = TL;
                    var rawGo = new GameObject("Icon", typeof(RectTransform));
                    rawGo.transform.SetParent(r.rt, false);
                    r.icons[t] = rawGo.AddComponent<RawImage>();
                    r.icons[t].raycastTarget = false;
                    RectTransform irt = rawGo.GetComponent<RectTransform>();
                    irt.anchorMin = irt.anchorMax = irt.pivot = TL;
                    irt.sizeDelta = new Vector2(30, 30);
                    irt.anchoredPosition = new Vector2(x - 2f, -12f);
                }
                r.more = Label("More", r.rt, TL, new Vector2(556, -14), new Vector2(138, 26), 17, TextAnchor.MiddleLeft);
                r.more.rectTransform.pivot = TL; r.more.color = LootGold;
                for (int t = 0; t < OldWorld.StartLeft; t++)
                {
                    r.pips[t] = Box("Pip", r.rt, TL, new Vector2(700 + t * 24, -19), new Vector2(16, 16), Gold);
                    r.pips[t].rectTransform.pivot = TL;
                }
                r.state = Label("Left", r.rt, TL, new Vector2(790, -14), new Vector2(150, 26), 19, TextAnchor.MiddleLeft);
                r.state.rectTransform.pivot = TL; r.state.fontStyle = FontStyle.Bold;
                worldRows[i] = r;
            }
            worldsMore = Label("MoreRows", p, TL, Vector2.zero, new Vector2(500, 20), 13, TextAnchor.MiddleLeft);
            worldsMore.rectTransform.pivot = TL; worldsMore.rectTransform.anchoredPosition = new Vector2(20, -356); worldsMore.color = Gray;

            Text lootHead = Label("LootHead", p, TL, Vector2.zero, new Vector2(300, 24), 18, TextAnchor.MiddleLeft, "LOOT IN PILE");
            lootHead.fontStyle = FontStyle.Bold; lootHead.color = LootGold;
            lootHead.rectTransform.pivot = TL; lootHead.rectTransform.anchoredPosition = new Vector2(20, -380);
            for (int i = 0; i < PileCellsShown; i++)
                MakeCell(p, 3, Section.Supplies, i, new Vector2(20 + i * 60f, -406f), 54f, worldCells);
            worldsPileMore = Label("PileMore", p, TL, Vector2.zero, new Vector2(200, 54), 17, TextAnchor.MiddleLeft);
            worldsPileMore.rectTransform.pivot = TL; worldsPileMore.rectTransform.anchoredPosition = new Vector2(20 + PileCellsShown * 60f + 6f, -406f);
            worldsPileMore.color = LootGold; worldsPileMore.fontStyle = FontStyle.Bold;

            worldsNote = Label("Note", p, TL, Vector2.zero, new Vector2(960, 40), 14, TextAnchor.UpperLeft);
            worldsNote.rectTransform.pivot = TL; worldsNote.rectTransform.anchoredPosition = new Vector2(20, -466);
            worldsNote.horizontalOverflow = HorizontalWrapMode.Wrap; worldsNote.color = Gray;
            worldsWarn = Label("Warn", p, TL, Vector2.zero, new Vector2(520, 30), 17, TextAnchor.MiddleLeft);
            worldsWarn.rectTransform.pivot = TL; worldsWarn.rectTransform.anchoredPosition = new Vector2(20, -512);
            worldsWarn.fontStyle = FontStyle.Bold;
            worldsEnterBtn = MakeButton("EnterOldButton", p, TL, Vector2.zero, new Vector2(300, 52), "ENTER OLD WORLD", 24);
            worldsEnterBtn.rt.pivot = TL; worldsEnterBtn.rt.anchoredPosition = new Vector2(560, -498);
            worldsBackBtn = MakeButton("BackOldButton", p, TL, Vector2.zero, new Vector2(110, 52), "Esc: Back", 15);
            worldsBackBtn.rt.pivot = TL; worldsBackBtn.rt.anchoredPosition = new Vector2(880, -498);
            worldsRoot.SetActive(false);
        }

        public bool WorldsVisible { get { return worldsRoot != null && worldsRoot.activeSelf; } }
        public RectTransform WorldsEnterRect { get { return worldsEnterBtn.rt; } }
        public RectTransform WorldRowRect(int row) { return worldRows[row].rt; }
        public int WorldSelected { get { return worldSel; } }
        public string WorldRowText(int row) { return worldRows[row].rt.gameObject.activeSelf ? worldRows[row].text.text + "  " + worldRows[row].state.text : ""; }
        public string WorldsNoteText { get { return worldsNote.text; } }
        public string WorldsWarnText { get { return worldsWarn.text; } }
        public int PileCellsFilled { get { int n = 0; foreach (Cell c in worldCells) if (c.rt.gameObject.activeSelf) n++; return n; } }

        public void ShowWorlds(Loadout d)
        {
            worldsRoot.SetActive(true);
            prompt = "";
            warnUntil = 0f;
            worldSel = 0;
            worldScroll = 0;
            RefreshWorlds(d);
        }

        public void HideWorlds() { if (worldsRoot != null) worldsRoot.SetActive(false); }

        static string FellText(long unix)
        {
            string when;
            try { when = System.DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("MMM d, HH:mm", System.Globalization.CultureInfo.InvariantCulture); }
            catch (System.Exception) { when = "?"; }
            return "Fell " + when;
        }

        void RefreshWorlds(Loadout d)
        {
            worldList = run.WorldList();
            int n = worldList.Count;
            worldSel = Mathf.Clamp(worldSel, 0, Mathf.Max(0, n - 1));
            if (worldSel < worldScroll) worldScroll = worldSel;
            if (worldSel >= worldScroll + WorldRowsShown) worldScroll = worldSel - WorldRowsShown + 1;
            worldScroll = Mathf.Clamp(worldScroll, 0, Mathf.Max(0, n - WorldRowsShown));
            for (int i = 0; i < WorldRowsShown; i++)
            {
                WorldRow r = worldRows[i];
                int idx = worldScroll + i;
                bool has = idx < n;
                r.rt.gameObject.SetActive(has);
                if (!has) continue;
                OldWorld w = worldList[idx];
                bool sel = idx == worldSel;
                r.frame.color = sel ? Gold : FrameCol;
                r.bg.color = sel ? new Color(0.17f, 0.15f, 0.25f, 1f) : SlotBg;
                r.text.text = "<b><color=" + Hex(OldText) + ">" + w.Code + "</color></b>   " + FellText(w.fellUnix) + "   " + w.ItemCount + (w.ItemCount == 1 ? " item" : " items");
                List<Item> all = w.AllItems();
                for (int t = 0; t < 4; t++)
                {
                    bool show = t < all.Count;
                    Item it = show ? all[t] : Item.None;
                    r.tiles[t].gameObject.SetActive(show && !it.IsWeapon);
                    r.icons[t].gameObject.SetActive(show && it.IsWeapon);
                    if (!show) continue;
                    if (it.IsWeapon) r.icons[t].texture = SwordIcons.Get(it.id);
                    else r.tiles[t].color = Foods.All[it.id].color;
                }
                r.more.text = (all.Count > 4 ? "+" + (all.Count - 4) : "") + (w.piles.Count > 1 ? (all.Count > 4 ? "  " : "") + w.piles.Count + " piles" : "");
                for (int t = 0; t < r.pips.Length; t++) r.pips[t].color = t < w.left ? (w.left <= 1 ? Yellow : Gold) : new Color(0.2f, 0.2f, 0.26f, 1f);
                r.state.text = w.left <= 1 ? "1 left!" : w.left + " left";
                r.state.color = w.left <= 1 ? Yellow : Color.white;
            }
            worldsMore.text = n > WorldRowsShown ? "Showing " + (worldScroll + 1) + "-" + Mathf.Min(n, worldScroll + WorldRowsShown) + " of " + n + "   (Up / Down for more)" : "";

            OldWorld cur = n > 0 ? worldList[worldSel] : null;
            List<Item> curItems = cur != null ? cur.AllItems() : new List<Item>();
            for (int i = 0; i < worldCells.Count; i++)
            {
                bool has = cur != null && i < curItems.Count;
                worldCells[i].rt.gameObject.SetActive(true);
                FillCell(worldCells[i], has ? curItems[i] : Item.None, false);
            }
            worldsPileMore.text = cur != null && curItems.Count > PileCellsShown ? "+" + (curItems.Count - PileCellsShown) + " more" : "";
            if (cur != null)
                worldsNote.text = (cur.left <= 1 ? "Crumbles after your next return." : "Crumbles after " + cur.left + " more returns.")
                    + "   Leave room in your pack to carry loot back.   " + (cur.piles.Count > 1 ? cur.piles.Count + " piles lie in this world.   " : "") + "Dying here adds a pile.";
            else worldsNote.text = "";
            bool viewOnly = coop && !coopHost;
            worldsEnterBtn.text.text = viewOnly ? "HOST ENTERS" : "ENTER OLD WORLD";
            bool can = d.CanEnter && cur != null && !viewOnly;
            worldsEnterBtn.bg.color = can ? new Color(0.45f, 0.34f, 0.12f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f);
            worldsEnterBtn.frame.color = can ? Gold : Disabled;
        }

        // Called every frame while the OLD WORLDS list is open.
        public void WorldsInput(Loadout d)
        {
            Vector2 m = InputX.MousePos;
            bool has = InputX.HasMouse;
            int hoverRow = -1;
            if (has)
                for (int i = 0; i < WorldRowsShown; i++)
                    if (worldRows[i].rt.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(worldRows[i].rt, m, null)) hoverRow = i;
            bool onEnter = has && RectTransformUtility.RectangleContainsScreenPoint(worldsEnterBtn.rt, m, null);
            bool onBack = has && RectTransformUtility.RectangleContainsScreenPoint(worldsBackBtn.rt, m, null);
            worldsBackBtn.bg.color = Color.Lerp(worldsBackBtn.bg.color, onBack ? new Color(0.3f, 0.36f, 0.56f, 1f) : new Color(0.14f, 0.16f, 0.24f, 0.96f), 0.35f);
            bool clickL = has && InputX.MouseDown(0);
            bool key = InputX.Down(KeyCode.Return) || InputX.Down(KeyCode.KeypadEnter) || InputX.Down(KeyCode.Space);
            if (InputX.Down(KeyCode.UpArrow) || InputX.Down(KeyCode.W)) worldSel = Mathf.Max(0, worldSel - 1);
            if (InputX.Down(KeyCode.DownArrow) || InputX.Down(KeyCode.S)) worldSel = Mathf.Min(worldList.Count - 1, worldSel + 1);
            int seed = worldSel >= 0 && worldSel < worldList.Count ? worldList[worldSel].seed : 0;
            if (clickL && hoverRow >= 0) worldSel = worldScroll + hoverRow;
            else if (clickL && onEnter && seed != 0) run.UiEnter(seed);
            else if (clickL && onBack) run.UiCloseWorlds();
            else if (key && prompt.Length > 0) run.UiConfirmEnter();
            else if (key && seed != 0) run.UiEnter(seed);
            if (!run.WorldsOpen) return;                                    // that entered the world / closed the list
            if (Time.unscaledTime >= warnUntil) ShowDefaultWarn(d);
            worldsWarn.text = warn.text; worldsWarn.color = warn.color;
            RefreshWorlds(d);
        }

        // ---------------------------------------------------------------- the pile in the world: label over it / arrow at the screen edge

        public bool LootLabelShown { get { foreach (Text t in lootLabels) if (t.gameObject.activeSelf) return true; return false; } }
        public bool LootArrowShown { get { return lootArrow.gameObject.activeSelf; } }
        public string LootLabelText { get { foreach (Text t in lootLabels) if (t.gameObject.activeSelf) return t.text; return lootLabel.text; } }
        public string LootArrowText { get { return lootDist.text; } }
        public int LootLabelsShown { get { int n = 0; foreach (Text t in lootLabels) if (t.gameObject.activeSelf) n++; return n; } }

        readonly List<Text> lootLabels = new List<Text>();            // [0] is lootLabel; more are made as piles need them

        // "LOOT x5" over every pile that is on screen, and - when the nearest pile is off screen - one gold arrow at the edge
        // of the screen with "Loot 142 m" (and "(3 piles)" when there are several). nearest < 0 / show == false: all hidden.
        public void SetLootMarkers(Camera cam, List<Vector3> worlds, List<int> counts, int nearest, float distance, bool show)
        {
            if (lootLabels.Count == 0) lootLabels.Add(lootLabel);
            int n = show && cam != null && worlds != null ? worlds.Count : 0;
            float w = Screen.width, h = Screen.height, mx = 70f, mTop = 130f, mBottom = 150f;   // the skill bar is at the bottom, the objective lines at the top
            float scale = Mathf.Max(0.01f, canvas.scaleFactor);
            int used = 0;
            bool nearestOn = false;
            Vector3 nearestSp = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(worlds[i]);
                bool on = sp.z > 0f && sp.x > mx * 0.6f && sp.x < w - mx * 0.6f && sp.y > mBottom * 0.6f && sp.y < h - mTop * 0.6f;
                if (i == nearest) { nearestOn = on; nearestSp = sp; }
                if (!on) continue;
                if (used == lootLabels.Count)
                {
                    Text t = Label("LootLabel" + used, root, new Vector2(0f, 0f), Vector2.zero, new Vector2(300, 28), 22, TextAnchor.MiddleCenter, "LOOT");
                    t.rectTransform.pivot = Mid; t.fontStyle = FontStyle.Bold; t.color = LootGold;
                    lootLabels.Add(t);
                }
                Text lab = lootLabels[used++];
                if (!lab.gameObject.activeSelf) lab.gameObject.SetActive(true);
                lab.text = "LOOT  x" + counts[i];
                lab.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y) / scale;
            }
            for (int i = used; i < lootLabels.Count; i++) if (lootLabels[i].gameObject.activeSelf) lootLabels[i].gameObject.SetActive(false);

            bool arrow = n > 0 && nearest >= 0 && !nearestOn;
            if (lootArrow.gameObject.activeSelf != arrow) { lootArrow.gameObject.SetActive(arrow); lootDist.gameObject.SetActive(arrow); }
            if (!arrow) return;
            Vector3 spn = nearestSp;
            Vector2 c = new Vector2(w * 0.5f, h * 0.5f);
            Vector2 dir = new Vector2(spn.x, spn.y) - c;
            if (spn.z < 0f) dir = -dir;
            if (dir.sqrMagnitude < 1f) dir = Vector2.up;
            dir.Normalize();
            float tx = Mathf.Abs(dir.x) > 0.0001f ? (w * 0.5f - mx) / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 0.0001f ? ((dir.y > 0f ? h - mTop - h * 0.5f : h * 0.5f - mBottom)) / Mathf.Abs(dir.y) : float.MaxValue;
            Vector2 pos = c + dir * Mathf.Min(tx, ty);
            lootArrow.rectTransform.anchoredPosition = pos / scale;
            lootArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            lootDist.rectTransform.anchoredPosition = (pos + new Vector2(0f, dir.y > 0f ? -34f : 34f)) / scale;
            lootDist.text = "Loot " + Mathf.RoundToInt(distance) + " m" + (n > 1 ? " (" + n + " piles)" : "");
        }

        // ---------------------------------------------------------------- loot window (L): the pile on the left, the pack on the right

        void BuildLoot()
        {
            RectTransform lr = Rect("Loot", root, Mid, Vector2.zero, Vector2.zero);
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            lootRoot = lr.gameObject;
            Image dim = Box("Dim", lr, Mid, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0.03f, 0.6f));
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
            Image frame = Box("Frame", lr, Mid, Vector2.zero, new Vector2(886, 526), OldFrame);
            RectTransform p = Box("Bg", frame.transform, Mid, Vector2.zero, new Vector2(880, 520), PanelBg).rectTransform;

            lootTitle = Label("Title", p, TL, Vector2.zero, new Vector2(520, 40), 30, TextAnchor.MiddleLeft, "YOUR LOOT");
            lootTitle.fontStyle = FontStyle.Bold; lootTitle.color = LootGold;
            lootTitle.rectTransform.pivot = TL; lootTitle.rectTransform.anchoredPosition = new Vector2(20, -10);
            lootTakeAllBtn = MakeButton("TakeAll", p, new Vector2(1f, 1f), new Vector2(-20f, -12f), new Vector2(210, 42), "Take all  [Q]", 19);
            lootTakeAllBtn.rt.pivot = new Vector2(1f, 1f);
            lootPileHead = Label("PileHead", p, TL, Vector2.zero, new Vector2(460, 24), 18, TextAnchor.MiddleLeft, "PILE");
            lootPileHead.fontStyle = FontStyle.Bold; lootPileHead.rectTransform.pivot = TL; lootPileHead.rectTransform.anchoredPosition = new Vector2(20, -62);
            for (int i = 0; i < LootCols * LootRows; i++)
                MakeCell(p, 4, Section.Supplies, i, new Vector2(20 + (i % LootCols) * 76f, -(90f + (i / LootCols) * 76f)), 68f, lootPileCells);
            Box("Divider", p, TL, new Vector2(500, -60), new Vector2(2, 330), FrameCol).rectTransform.pivot = TL;

            Text packHead = Label("PackHead", p, TL, Vector2.zero, new Vector2(300, 24), 18, TextAnchor.MiddleLeft, "YOUR PACK");
            packHead.fontStyle = FontStyle.Bold; packHead.rectTransform.pivot = TL; packHead.rectTransform.anchoredPosition = new Vector2(520, -62);
            string[] names = { "Weapons", "Food", "Supplies" };
            float[] ly = { 90f, 190f, 290f };
            for (int s = 0; s < 3; s++)
            {
                Text l = Label(names[s] + "Label", p, TL, Vector2.zero, new Vector2(300, 20), 14, TextAnchor.MiddleLeft, names[s]);
                l.color = Gray; l.rectTransform.pivot = TL; l.rectTransform.anchoredPosition = new Vector2(520, -ly[s]);
                lootPackGrid[s] = new Cell[Loadout.CarryCaps[s]];
                for (int i = 0; i < Loadout.CarryCaps[s]; i++)
                    lootPackGrid[s][i] = MakeCell(p, 5, (Section)s, i, new Vector2(520 + i * 76f, -(ly[s] + 20f)), 68f, lootPackCells);
            }
            lootInfo = Label("Info", p, TL, Vector2.zero, new Vector2(840, 44), 15, TextAnchor.UpperLeft);
            lootInfo.rectTransform.pivot = TL; lootInfo.rectTransform.anchoredPosition = new Vector2(20, -402);
            lootInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            lootWarn = Label("Warn", p, TL, Vector2.zero, new Vector2(840, 28), 17, TextAnchor.MiddleLeft);
            lootWarn.rectTransform.pivot = TL; lootWarn.rectTransform.anchoredPosition = new Vector2(20, -454);
            lootWarn.fontStyle = FontStyle.Bold;
            Text foot = Label("Foot", p, TL, Vector2.zero, new Vector2(840, 20), 12, TextAnchor.MiddleLeft,
                "Click / Enter: take   -   Q: take all   -   Right click a pack item: drop it   -   Arrows / WASD: move   -   Tab / Esc: close");
            foot.color = Gray; foot.rectTransform.pivot = TL; foot.rectTransform.anchoredPosition = new Vector2(20, -490);
            lootRoot.SetActive(false);
        }

        public bool LootVisible { get { return lootRoot != null && lootRoot.activeSelf; } }
        public RectTransform LootPileCellRect(int i) { return lootPileCells[i].rt; }
        public RectTransform LootTakeAllRect { get { return lootTakeAllBtn.rt; } }
        public RectTransform LootPackCellRect(Section sec, int i) { return lootPackGrid[(int)sec][i].rt; }
        public string LootWarnText { get { return lootWarn.text; } }
        public string LootTitleText { get { return lootTitle.text; } }
        public string LootPileHeadText { get { return lootPileHead.text; } }

        public void ShowLoot(Loadout d, string code, Pile w)
        {
            lootRoot.SetActive(true);
            lootCursor = 0;
            lootScroll = 0;
            lootWarnUntil = 0f;
            RefreshLoot(d, code, w);
        }

        public void SetLootWarn(string text, Color color, float seconds, Section? shake)
        {
            lootWarn.text = text;
            lootWarn.color = color;
            lootWarnUntil = Time.unscaledTime + seconds;
            if (shake.HasValue) foreach (Cell c in lootPackGrid[(int)shake.Value]) c.shakeUntil = Time.unscaledTime + 0.4f;
        }

        static string ItemInfo(Item it)
        {
            if (it.IsNone) return "<color=#9aa0b0>Point at an item to see what it does.</color>";
            if (it.IsWeapon)
            {
                WeaponDef w = Weapons.All[it.id];
                return "<b><color=#" + ColorUtility.ToHtmlStringRGB(w.uiColor) + ">" + it.Name + "</color></b>  <color=#9aa0b0>" + w.rarity + "</color>\n"
                    + "DMG x" + (w.damage * (1f + Weapons.DamagePerPlus * it.plus)).ToString("0.00") + "  SPD x" + w.speed.ToString("0.00")
                    + "  CRIT +" + (w.critChance * 100f).ToString("0") + "% / +" + w.critMult.ToString("0.0") + "x  REACH x" + w.reach.ToString("0.00");
            }
            FoodDef f = Foods.All[it.id];
            return "<b>" + f.name + "</b>\nHeals " + Mathf.RoundToInt(f.healFrac * 100f) + "% of your max health.";
        }

        void RefreshLoot(Loadout d, string code, Pile w)
        {
            int n = w != null ? w.items.Count : 0;
            lootCursor = Mathf.Clamp(lootCursor, 0, Mathf.Max(0, n - 1));
            int rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)LootCols));
            int curRow = lootCursor / LootCols;
            if (curRow < lootScroll) lootScroll = curRow;
            if (curRow >= lootScroll + LootRows) lootScroll = curRow - LootRows + 1;
            lootScroll = Mathf.Clamp(lootScroll, 0, Mathf.Max(0, rows - LootRows));
            int cantTake = 0;
            for (int i = 0; i < lootPileCells.Count; i++)
            {
                int idx = lootScroll * LootCols + i;
                Cell c = lootPileCells[i];
                bool has = idx < n;
                FillCell(c, has ? w.items[idx] : Item.None, false);
                if (has && !d.carry.CanAdd(w.items[idx])) { c.tag.text = "FULL"; c.tag.color = Red; }
                else c.tag.color = Green;
            }
            if (w != null) foreach (Item it in w.items) if (!d.carry.CanAdd(it)) cantTake++;
            for (int s = 0; s < 3; s++)
            {
                Item[] arr = d.carry.Slots((Section)s);
                for (int i = 0; i < arr.Length; i++) FillCell(lootPackGrid[s][i], arr[i], s == 0 && i == d.heldSlot && arr[i].IsWeapon);
            }
            lootTitle.text = "LOOT  " + code;
            lootPileHead.text = "PILE  " + n + (n > LootCols * LootRows ? "   (rows " + (lootScroll + 1) + "-" + Mathf.Min(rows, lootScroll + LootRows) + " of " + rows + ")" : "");
            if (Time.unscaledTime >= lootWarnUntil)
            {
                if (n == 0) { lootWarn.text = "Everything recovered."; lootWarn.color = Green; }
                else if (cantTake > 0) { lootWarn.text = "Pack full: " + cantTake + " left in the pile"; lootWarn.color = Yellow; }
                else { lootWarn.text = ""; lootWarn.color = Yellow; }
            }
            bool can = n > 0 && cantTake < n;
            lootTakeAllBtn.bg.color = can ? new Color(0.45f, 0.34f, 0.12f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f);
            lootTakeAllBtn.frame.color = can ? Gold : Disabled;
        }

        // Called every frame while the loot window is open.
        public void LootInput(Loadout d, string code, Pile w)
        {
            if (w == null) return;
            Vector2 m = InputX.MousePos;
            bool has = InputX.HasMouse;
            int hoverPile = -1;
            Cell hoverPack = null;
            if (has)
            {
                for (int i = 0; i < lootPileCells.Count; i++)
                    if (lootScroll * LootCols + i < w.items.Count && RectTransformUtility.RectangleContainsScreenPoint(lootPileCells[i].rt, m, null)) hoverPile = lootScroll * LootCols + i;
                foreach (Cell c in lootPackCells)
                    if (RectTransformUtility.RectangleContainsScreenPoint(c.rt, m, null)) hoverPack = c;
            }
            bool onAll = has && RectTransformUtility.RectangleContainsScreenPoint(lootTakeAllBtn.rt, m, null);
            bool clickL = has && InputX.MouseDown(0), clickR = has && InputX.MouseDown(1);
            bool key = InputX.Down(KeyCode.Return) || InputX.Down(KeyCode.KeypadEnter) || InputX.Down(KeyCode.Space);
            int n = w.items.Count;
            if (n > 0)
            {
                if (InputX.Down(KeyCode.LeftArrow) || InputX.Down(KeyCode.A)) lootCursor = Mathf.Max(0, lootCursor - 1);
                if (InputX.Down(KeyCode.RightArrow) || InputX.Down(KeyCode.D)) lootCursor = Mathf.Min(n - 1, lootCursor + 1);
                if (InputX.Down(KeyCode.UpArrow) || InputX.Down(KeyCode.W)) lootCursor = Mathf.Max(0, lootCursor - LootCols);
                if (InputX.Down(KeyCode.DownArrow) || InputX.Down(KeyCode.S)) lootCursor = Mathf.Min(n - 1, lootCursor + LootCols);
            }
            if (clickL && hoverPile >= 0) { lootCursor = hoverPile; run.UiTake(hoverPile); }
            else if ((clickL && onAll) || InputX.Down(KeyCode.Q)) run.UiTakeAll();
            else if (clickR && hoverPack != null) run.UiCarryDrop(hoverPack.sec, hoverPack.index);
            else if (key && n > 0) run.UiTake(lootCursor);
            if (!run.LootOpen) return;                                      // the last item was taken: the window closed
            RefreshLoot(d, code, w);
            Item shown = hoverPile >= 0 ? w.items[hoverPile] : hoverPack != null ? d.carry.Slots(hoverPack.sec)[hoverPack.index]
                : lootCursor < w.items.Count ? w.items[lootCursor] : Item.None;
            lootInfo.text = ItemInfo(shown);
            float now = Time.unscaledTime;
            for (int i = 0; i < lootPileCells.Count; i++)
            {
                bool cur = lootScroll * LootCols + i == lootCursor && n > 0;
                bool hov = lootScroll * LootCols + i == hoverPile;
                lootPileCells[i].cursor.color = cur || hov ? new Color(1f, 1f, 1f, hov ? 0.3f : 0.5f) : new Color(1f, 1f, 1f, 0f);
            }
            foreach (Cell c in lootPackCells)
            {
                bool shaking = now < c.shakeUntil;
                c.rt.anchoredPosition = shaking ? c.basePos + new Vector2(Mathf.Sin(now * 70f) * 3f, 0f) : c.basePos;
                if (shaking) c.border.color = Red;
                c.cursor.color = c == hoverPack ? new Color(1f, 1f, 1f, 0.3f) : new Color(1f, 1f, 1f, 0f);
            }
        }

        // ---------------------------------------------------------------- carry window (Tab in the world)

        void BuildCarry()
        {
            RectTransform cr = Rect("Carry", root, Mid, Vector2.zero, Vector2.zero);
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = cr.offsetMax = Vector2.zero;
            carryRoot = cr.gameObject;
            Image dim = Box("Dim", cr, Mid, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0.03f, 0.55f));
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
            Image frame = Box("Frame", cr, Mid, Vector2.zero, new Vector2(766, 476), FrameCol);
            RectTransform p = Box("Bg", frame.transform, Mid, Vector2.zero, new Vector2(760, 470), PanelBg).rectTransform;

            carryHead = Label("Title", p, TL, Vector2.zero, new Vector2(400, 40), 30, TextAnchor.MiddleLeft, "CARRY");
            carryHead.fontStyle = FontStyle.Bold; carryHead.rectTransform.pivot = TL; carryHead.rectTransform.anchoredPosition = new Vector2(20, -10);
            Text close = Label("Close", p, new Vector2(1f, 1f), new Vector2(-20f, -18f), new Vector2(200, 24), 15, TextAnchor.MiddleRight, "[Tab] close");
            close.color = Gray; close.rectTransform.pivot = new Vector2(1f, 1f);

            string[] names = { "Weapons", "Food", "Supplies" };
            float[] ly = { 56f, 180f, 304f };
            int[] caps = Loadout.CarryCaps;
            for (int s = 0; s < 3; s++)
            {
                Text l = Label(names[s] + "Label", p, TL, Vector2.zero, new Vector2(300, 20), 14, TextAnchor.MiddleLeft, names[s]);
                l.color = Gray; l.rectTransform.pivot = TL; l.rectTransform.anchoredPosition = new Vector2(20, -ly[s]);
                carryGrid[s] = new Cell[caps[s]];
                for (int i = 0; i < caps[s]; i++)
                    carryGrid[s][i] = MakeCell(p, 2, (Section)s, i, new Vector2(20 + i * 96f, -(ly[s] + 22f)), 88f, carryCells);
            }
            carryChar = Label("Char", p, TL, Vector2.zero, new Vector2(320, 200), 15, TextAnchor.UpperLeft);
            carryChar.rectTransform.pivot = TL; carryChar.rectTransform.anchoredPosition = new Vector2(420, -60);
            carryInfo = Label("Info", p, TL, Vector2.zero, new Vector2(320, 90), 15, TextAnchor.UpperLeft);
            carryInfo.rectTransform.pivot = TL; carryInfo.rectTransform.anchoredPosition = new Vector2(420, -300);
            carryInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            Text foot = Label("Foot", p, TL, Vector2.zero, new Vector2(720, 20), 12, TextAnchor.MiddleLeft,
                "Click: hold a Weapon / eat   -   Right click: drop on the ground   -   1 / 2: hold   C: eat   Tab / Esc: close");
            foot.color = Gray; foot.rectTransform.pivot = TL; foot.rectTransform.anchoredPosition = new Vector2(20, -440);
            carryRoot.SetActive(false);
        }

        public bool CarryVisible { get { return carryRoot != null && carryRoot.activeSelf; } }
        public RectTransform CarryCellRect(Section sec, int index) { return carryGrid[(int)sec][index].rt; }
        public string CarryInfoText { get { return carryInfo.text; } }

        public void ShowCarry(Loadout d, Knight k)
        {
            carryRoot.SetActive(true);
            RefreshCarry(d, k);
        }

        void RefreshCarry(Loadout d, Knight k)
        {
            for (int s = 0; s < 3; s++)
            {
                Item[] arr = d.carry.Slots((Section)s);
                for (int i = 0; i < arr.Length; i++) FillCell(carryGrid[s][i], arr[i], s == 0 && i == d.heldSlot && arr[i].IsWeapon);
            }
            Progress pr = k.progress;
            carryChar.text = "<b>Lv " + pr.level + " / " + Progress.MaxLevel + "</b>   EXP " + (pr.IsMax ? "MAX" : Mathf.FloorToInt(pr.exp) + " / " + Progress.Need(pr.level))
                + "\n<b>Health</b> " + Mathf.CeilToInt(k.hp) + " / " + Mathf.CeilToInt(k.MaxHp)
                + "     <b>Armor</b> " + Mathf.CeilToInt(k.armor) + " / " + Mathf.CeilToInt(k.MaxArmor)
                + "\n<b>Stamina</b> " + Mathf.CeilToInt(k.stamina) + " / " + Mathf.CeilToInt(k.MaxStamina)
                + "\nSword hit x" + k.DamageMult(-1).ToString("0.0") + "    Swing x" + k.SwingSpeed.ToString("0.0")
                + "\nCrit " + (k.CritChance(-1) * 100f).ToString("0") + "% x" + k.CritMult(-1).ToString("0.0") + "    Reach x" + k.Weapon.reach.ToString("0.00")
                + "\nKills " + pr.kills;
        }

        // Called every frame while the carry window is open.
        public void CarryInput(Loadout d, Knight k)
        {
            Vector2 m = InputX.MousePos;
            Cell hover = null;
            if (InputX.HasMouse)
                foreach (Cell c in carryCells)
                    if (RectTransformUtility.RectangleContainsScreenPoint(c.rt, m, null)) hover = c;
            bool clickL = InputX.HasMouse && InputX.MouseDown(0), clickR = InputX.HasMouse && InputX.MouseDown(1);
            if (hover != null && clickL) run.UiCarryUse(hover.sec, hover.index);
            else if (hover != null && clickR) run.UiCarryDrop(hover.sec, hover.index);
            if (InputX.Down(KeyCode.Alpha1) || InputX.Down(KeyCode.Keypad1)) run.SwitchHeld(0);
            if (InputX.Down(KeyCode.Alpha2) || InputX.Down(KeyCode.Keypad2)) run.SwitchHeld(1);
            if (InputX.Down(KeyCode.C)) run.EatFirst();
            if (!run.CarryOpen) return;
            RefreshCarry(d, k);
            Item it = hover != null ? d.carry.Slots(hover.sec)[hover.index] : Item.None;
            if (it.IsNone) carryInfo.text = "<color=#9aa0b0>Point at an item to see what it does.</color>";
            else if (it.IsWeapon)
            {
                WeaponDef w = Weapons.All[it.id];
                carryInfo.text = "<b><color=#" + ColorUtility.ToHtmlStringRGB(w.uiColor) + ">" + it.Name + "</color></b>  <color=#9aa0b0>" + w.rarity + "</color>\n"
                    + "DMG x" + (w.damage * (1f + Weapons.DamagePerPlus * it.plus)).ToString("0.00") + "  SPD x" + w.speed.ToString("0.00")
                    + "\nCRIT +" + (w.critChance * 100f).ToString("0") + "% / +" + w.critMult.ToString("0.0") + "x  REACH x" + w.reach.ToString("0.00");
            }
            else
            {
                FoodDef f = Foods.All[it.id];
                carryInfo.text = "<b>" + f.name + "</b>\nHeals " + Mathf.RoundToInt(f.healFrac * 100f) + "% of your max health. Click or press C to eat.";
            }
            foreach (Cell c in carryCells)
                c.cursor.color = c == hover ? new Color(1f, 1f, 1f, 0.3f) : new Color(1f, 1f, 1f, 0f);
        }
        // ---------------------------------------------------------------- end-of-run summary

        void BuildSummary()
        {
            deathDim = Box("DeathDim", root, Mid, Vector2.zero, Vector2.zero, new Color(0.12f, 0f, 0f, 0f));
            deathDim.rectTransform.anchorMin = Vector2.zero; deathDim.rectTransform.anchorMax = Vector2.one;
            deathDim.rectTransform.offsetMin = deathDim.rectTransform.offsetMax = Vector2.zero;

            RectTransform sr = Rect("Summary", root, Mid, Vector2.zero, Vector2.zero);
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero;
            summaryRoot = sr.gameObject;
            Image frame = Box("Frame", sr, Mid, Vector2.zero, new Vector2(706, 596), FrameCol);
            RectTransform p = Box("Bg", frame.transform, Mid, Vector2.zero, new Vector2(700, 590), PanelBg).rectTransform;
            sumTitle = Label("Title", p, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(660, 56), 46, TextAnchor.MiddleCenter, "VICTORY");
            sumTitle.fontStyle = FontStyle.Bold;
            sumSub = Label("Sub", p, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(660, 26), 19, TextAnchor.MiddleCenter);
            sumSub.color = Gray;
            sumBody = Label("Body", p, TL, Vector2.zero, new Vector2(640, 390), 19, TextAnchor.UpperLeft);
            sumBody.rectTransform.pivot = TL; sumBody.rectTransform.anchoredPosition = new Vector2(30, -124);
            sumBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            returnBtn = MakeButton("ReturnButton", p, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(340, 56), "RETURN TO BASE", 26);
            returnBtn.rt.pivot = new Vector2(0.5f, 0f);
            returnBtn.frame.color = Gold;
            returnBtn.bg.color = new Color(0.45f, 0.34f, 0.12f, 1f);
            summaryRoot.SetActive(false);
        }

        public bool SummaryVisible { get { return summaryRoot != null && summaryRoot.activeSelf; } }
        public RectTransform ReturnRect { get { return returnBtn.rt; } }
        public string SummaryTitle { get { return sumTitle.text; } }
        public string SummarySubtitle { get { return sumSub.text; } }
        public string SummaryBody { get { return sumBody.text; } }

        // "Iron Sword +2, Apple x2, Horned King's Blade NEW"
        static string Group(List<ResultLine> lines)
        {
            var order = new List<string>();
            var count = new Dictionary<string, int>();
            var isNew = new Dictionary<string, bool>();
            foreach (ResultLine l in lines)
            {
                string n = l.item.Name;
                if (!count.ContainsKey(n)) { order.Add(n); count[n] = 0; isNew[n] = false; }
                count[n]++;
                if (l.isNew) isNew[n] = true;
            }
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < order.Count; i++)
            {
                if (i > 0) sb.Append(",  ");
                sb.Append(order[i]);
                if (count[order[i]] > 1) sb.Append(" x").Append(count[order[i]]);
                if (isNew[order[i]]) sb.Append(" <color=#F2C14E>NEW</color>");
            }
            return sb.ToString();
        }

        static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

        public void ShowSummary(RunResult r)
        {
            HideAll();
            summaryRoot.SetActive(true);
            var sb = new System.Text.StringBuilder();
            if (r.victory)
            {
                sumTitle.text = "VICTORY"; sumTitle.color = Green;
                sumSub.text = "The Minotaur is defeated and the way home is open.";
                sb.Append("<color=" + Hex(Green) + "><b>BROUGHT HOME</b></color>\n");
                sb.Append(r.brought.Count > 0 ? Group(r.brought) : "<color=#9aa0b0>nothing</color>").Append("\n\n");
                if (r.leftBehind.Count > 0)
                    sb.Append("<color=" + Hex(Yellow) + "><b>LEFT BEHIND (stash full)</b></color>\n").Append(Group(r.leftBehind)).Append("\n\n");
            }
            else
            {
                sumTitle.text = r.abandoned ? "ABANDONED" : "YOU FELL"; sumTitle.color = Red;
                sumSub.text = r.abandoned ? "You left the world mid-run. Everything you carried was lost." : "Everything you carried is lost.";
                sb.Append("<color=" + Hex(Red) + "><b>LOST</b></color>\n");
                sb.Append(r.lost.Count > 0 ? Group(r.lost) : "<color=#9aa0b0>nothing</color>").Append("\n\n");
                sb.Append("<color=" + Hex(Green) + "><b>SAFE IN STASH</b></color>\n");
                sb.Append(r.safe.Count > 0 ? Group(r.safe) : "<color=#9aa0b0>nothing</color>").Append("\n\n");
            }
            AppendWorldNotes(sb, r);
            if (r.ironRestored) sb.Append("<color=" + Hex(Green) + ">Iron Sword restored</color>\n");
            sb.Append("Stash:  " + r.stashWeapons + "/" + Loadout.StashCaps[0] + " Weapons    " + r.stashFoods + "/" + Loadout.StashCaps[1] + " Food");
            sumBody.text = sb.ToString();
        }

        // Phase 2: what the run did to the old worlds (a new pile / the pile grew / counters going down / crumbled).
        static void AppendWorldNotes(System.Text.StringBuilder sb, RunResult r)
        {
            string violet = Hex(OldText), gold = Hex(LootGold);
            if (!r.victory)
            {
                if (r.worldNote == WorldNote.Created)
                {
                    sb.Append("<color=" + violet + "><b>OLD WORLD CREATED  " + r.WorldCode + "</b></color>\n");
                    sb.Append(r.pileCount + (r.pileCount == 1 ? " item lies" : " items lie") + " where you fell.  Crumbles after " + r.worldLeft + " returns.\n");
                }
                else if (r.worldNote == WorldNote.Added)
                {
                    sb.Append("<color=" + violet + "><b>ADDED TO OLD WORLD  " + r.WorldCode + "</b></color>\n");
                    if (r.pilesN > 1)
                        sb.Append("The world now holds " + r.pileCount + " items in " + r.pilesN + " piles, " + r.worldLeft + (r.worldLeft == 1 ? " return" : " returns") + " left.\n");
                    else
                        sb.Append("The pile now holds " + r.pileCount + (r.pileCount == 1 ? " item" : " items") + ", " + r.worldLeft + (r.worldLeft == 1 ? " return" : " returns")
                            + " left.\n");
                }
                else if (r.worldNote == WorldNote.Crumbled)
                {
                    sb.Append("<color=" + Hex(Red) + "><b>" + r.WorldCode + " CRUMBLED</b></color>\n");
                    sb.Append("<color=" + Hex(Red) + ">The world you fell in crumbled with this return. Everything in it is gone for good, including what you just lost.</color>\n");
                }
                else if (r.inOldWorld)
                    sb.Append("<color=" + violet + ">" + r.WorldCode + " is still there: " + r.pileCount + " in the pile, " + r.worldLeft + " returns left.</color>\n");
                if (r.overflow.Count > 0)
                    sb.Append("<color=" + Hex(Red) + ">" + r.overflow.Count + " items did not fit in the pile (limit " + OldWorld.MaxPile + ") and are gone.</color>\n");
                sb.Append("\n");
            }
            else if (r.worldNote == WorldNote.Recovered)
                sb.Append("<color=" + Hex(Green) + ">" + r.WorldCode + ": all loot recovered. World closed.</color>\n");
            else if (r.worldNote == WorldNote.StillInPile)
                sb.Append("<color=" + gold + ">" + r.WorldCode + ": " + r.pileCount + (r.pileCount == 1 ? " item" : " items") + " still in the pile</color>\n");
            if (r.crumbs.Count > 0)
            {
                sb.Append("<color=" + violet + "><b>OLD WORLDS</b></color>\n");
                foreach (CrumbLine c in r.crumbs)
                {
                    if (c.crumbled)
                        sb.Append("<color=" + Hex(Red) + ">" + c.Code + "  " + c.before + " -> 0  CRUMBLED" + (c.itemsLost > 0 ? ", " + c.itemsLost
                            + (c.itemsLost == 1 ? " item" : " items") + " lost forever" : "") + "</color>\n");
                    else
                        sb.Append(c.Code + "  " + c.before + " -> " + c.after + " left" + (c.after == 1 ? "  <color=" + Hex(Yellow) + ">last return!</color>" : "") + "\n");
                }
                sb.Append("\n");
            }
        }

        public void HideSummary() { if (summaryRoot != null) summaryRoot.SetActive(false); }

        public void SetDeathDim(float alpha) { deathDim.color = new Color(0.12f, 0f, 0f, alpha); }

        // Called every frame while the summary is open.
        public void SummaryInput()
        {
            Vector2 m = InputX.MousePos;
            bool on = InputX.HasMouse && RectTransformUtility.RectangleContainsScreenPoint(returnBtn.rt, m, null);
            returnBtn.bg.color = on ? new Color(0.6f, 0.46f, 0.16f, 1f) : new Color(0.45f, 0.34f, 0.12f, 1f);
            bool click = InputX.HasMouse && InputX.MouseDown(0) && on;
            bool key = InputX.Down(KeyCode.Return) || InputX.Down(KeyCode.KeypadEnter) || InputX.Down(KeyCode.Space);
            if (click || key) run.UiReturn();
        }
    }
}

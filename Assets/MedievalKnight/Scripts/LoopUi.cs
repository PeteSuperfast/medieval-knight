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
            Image frame = Box("Frame", pr, Mid, Vector2.zero, new Vector2(1006, 606), FrameCol);
            RectTransform p = Box("Bg", frame.transform, Mid, Vector2.zero, new Vector2(1000, 600), PanelBg).rectTransform;

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
            Box("Divider", p, TL, new Vector2(520, -60), new Vector2(2, 420), FrameCol).rectTransform.pivot = TL;

            cells[0] = new Cell[3][];
            cells[1] = new Cell[3][];
            sectionLabels[0] = new Text[3];
            sectionLabels[1] = new Text[3];
            string[] names = { "Weapons", "Food", "Supplies" };
            // stash
            float[] labelY0 = { 90f, 238f, 328f };
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
            float[] labelY1 = { 90f, 222f, 354f };
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
            info.rectTransform.pivot = TL; info.rectTransform.anchoredPosition = new Vector2(20, -486);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            warn = Label("Warn", p, TL, Vector2.zero, new Vector2(520, 30), 17, TextAnchor.MiddleLeft);
            warn.rectTransform.pivot = TL; warn.rectTransform.anchoredPosition = new Vector2(20, -548);
            warn.fontStyle = FontStyle.Bold;
            enterBtn = MakeButton("EnterButton", p, TL, Vector2.zero, new Vector2(300, 52), "ENTER WORLD", 26);
            enterBtn.rt.pivot = TL; enterBtn.rt.anchoredPosition = new Vector2(560, -536);
            backBtn = MakeButton("BackButton", p, TL, Vector2.zero, new Vector2(110, 52), "Esc: Back", 15);
            backBtn.rt.pivot = TL; backBtn.rt.anchoredPosition = new Vector2(880, -536);
            hint = Label("Hint", p, TL, Vector2.zero, new Vector2(960, 20), 12, TextAnchor.MiddleLeft,
                "Click an item to move it   -   Right click a carried Weapon to hold it   -   Arrows / WASD: move cursor   Enter: move   Q / E: other side   Tab / Esc: close");
            hint.color = Gray; hint.rectTransform.pivot = TL; hint.rectTransform.anchoredPosition = new Vector2(20, -588);
            prepareRoot.SetActive(false);
        }

        Cell MakeCell(Transform parent, int side, Section sec, int index, Vector2 pos, float size)
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
            allCells.Add(c);
            return c;
        }

        // ---------------------------------------------------------------- PREPARE

        public bool PrepareVisible { get { return prepareRoot != null && prepareRoot.activeSelf; } }
        public RectTransform CellRect(int side, Section sec, int index) { return cells[side][(int)sec][index].rt; }
        public RectTransform EnterRect { get { return enterBtn.rt; } }
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
            introText.gameObject.SetActive(!d.introShown);
            bool can = d.CanEnter;
            enterBtn.bg.color = can ? new Color(0.45f, 0.34f, 0.12f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f);
            enterBtn.frame.color = can ? Gold : Disabled;
            if (Time.unscaledTime >= warnUntil) ShowDefaultWarn(d);
        }

        void ShowDefaultWarn(Loadout d)
        {
            if (prompt.Length > 0) { warn.text = prompt; warn.color = Yellow; return; }
            if (!d.CanEnter) { warn.text = "Pack a Weapon first"; warn.color = Red; return; }
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
            if (cursorCell == null) cursorCell = cells[0][0][0];
            RefreshPrepare(d);
            UpdateInfo(d, cursorCell);
        }

        public void HideAll()
        {
            if (prepareRoot != null) prepareRoot.SetActive(false);
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
            bool onEnter = InputX.HasMouse && RectTransformUtility.RectangleContainsScreenPoint(enterBtn.rt, m, null);
            bool onBack = InputX.HasMouse && RectTransformUtility.RectangleContainsScreenPoint(backBtn.rt, m, null);
            backBtn.bg.color = Color.Lerp(backBtn.bg.color, onBack ? new Color(0.3f, 0.36f, 0.56f, 1f) : new Color(0.14f, 0.16f, 0.24f, 0.96f), 0.35f);
            if (onEnter && d.CanEnter) enterBtn.bg.color = new Color(0.6f, 0.46f, 0.16f, 1f);

            // keyboard cursor
            if (InputX.Down(KeyCode.LeftArrow) || InputX.Down(KeyCode.A)) cursorCell = Neighbour(cursorCell, Vector2.left);
            if (InputX.Down(KeyCode.RightArrow) || InputX.Down(KeyCode.D)) cursorCell = Neighbour(cursorCell, Vector2.right);
            if (InputX.Down(KeyCode.UpArrow) || InputX.Down(KeyCode.W)) cursorCell = Neighbour(cursorCell, Vector2.up);
            if (InputX.Down(KeyCode.DownArrow) || InputX.Down(KeyCode.S)) cursorCell = Neighbour(cursorCell, Vector2.down);
            if (InputX.Down(KeyCode.Q) || InputX.Down(KeyCode.E)) cursorCell = OtherSide(cursorCell);

            bool clickL = InputX.HasMouse && InputX.MouseDown(0), clickR = InputX.HasMouse && InputX.MouseDown(1);
            bool key = InputX.Down(KeyCode.Return) || InputX.Down(KeyCode.KeypadEnter) || InputX.Down(KeyCode.Space);
            bool consumed = false;
            if (clickL && onEnter) { run.UiEnter(); consumed = true; }
            else if (clickL && onBack) { run.UiBack(); consumed = true; }
            else if (key && prompt.Length > 0) { run.UiEnter(); consumed = true; }       // "[Enter] Go"
            else if (clickL && hover != null) { cursorCell = hover; run.UiMove(hover.side, hover.sec, hover.index); consumed = true; }
            else if (clickR && hover != null) { cursorCell = hover; if (hover.side == 1 && hover.sec == Section.Weapons) run.UiHold(hover.index); consumed = true; }
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
                c.cursor.color = c == cursorCell || c == hover ? new Color(1f, 1f, 1f, c == hover ? 0.28f : 0.5f) : new Color(1f, 1f, 1f, 0f);
            }
            if (now >= warnUntil) ShowDefaultWarn(d);
        }
    }
}

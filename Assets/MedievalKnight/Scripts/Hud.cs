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

        Font font;
        Canvas canvas;
        RectTransform root, popupRoot;
        BarUi hpBar, armorBar, staminaBar, expBar, bossBar;
        Text statsText, bannerText, actionText, controlsText, bossText, levelTitle, levelSub;
        GameObject controlsPanel, bossPanel, levelRoot;
        readonly SlotUi[] slots = new SlotUi[4];
        readonly CardUi[] cards = new CardUi[3];
        bool showControls = true;
        float bannerTime, actionTime, levelOpenedAt;
        Color bannerColor, actionColor;
        readonly List<PopupUi> popups = new List<PopupUi>();
        readonly Stack<PopupUi> popupPool = new Stack<PopupUi>();

        public bool LevelUpVisible { get { return levelRoot.activeSelf; } }
        public RectTransform CardRect(int i) { return cards[i].rt; }
        public float ScaleFactor { get { return canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f; } }

        static readonly Color PanelCol = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color FrameCol = new Color(0.06f, 0.06f, 0.08f, 0.95f);

        public static Color StatColor(StatKind s)
        {
            return s == StatKind.Damage ? new Color(1f, 0.42f, 0.32f) : s == StatKind.Speed ? new Color(1f, 0.86f, 0.3f) : new Color(0.8f, 0.55f, 1f);
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

            // --- upgrade totals, under the bars ---
            Panel("StatsPanel", new Vector2(0, 1), new Vector2(16, -168), new Vector2(400, 122), PanelCol);
            statsText = MakeText("Stats", new Vector2(0, 1), new Vector2(28, -174), new Vector2(380, 112), 13, TextAnchor.UpperLeft);
            statsText.supportRichText = true;

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
            controlsPanel = Panel("ControlsPanel", new Vector2(1, 1), new Vector2(-16, -16), new Vector2(350, 178), PanelCol).gameObject;
            controlsText = MakeText("Controls", new Vector2(1, 1), new Vector2(-26, -22), new Vector2(330, 200), 14, TextAnchor.UpperLeft);
            controlsText.text =
                "CONTROLS   (F1 hides this)\n"
                + "WASD / Arrows    Move\n"
                + "Mouse    Aim - the knight faces the cursor\n"
                + "Left Click / J / Space    Sword combo\n"
                + "     Side > Chop > Rising > Spin > Thrust\n"
                + "Right Click / Left Shift    Shield block\n"
                + "Q  Whirlwind          E  Dash Thrust\n"
                + "R  Shield Bash        F  Ground Slam\n"
                + "1 / 2 / 3 or Click    Pick a level-up card\n"
                + "H  Test hit     F5  Restart     Esc  Quit";

            // --- skill bar, bottom-centre ---
            for (int i = 0; i < 4; i++) slots[i] = MakeSlot(i);

            BuildLevelPanel();
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
            Fill(Panel("Dim", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0.04f, 0.68f), lr).rectTransform);
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

        // ---------------------------------------------------------------- public API

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
            levelSub.text = "Choose 1 of 3 upgrades   (keys 1 / 2 / 3 or click a card)";
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

        public void HideLevelUp() { levelRoot.SetActive(false); }

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
            UpdateBar(armorBar, k.armor, Knight.MaxArmor, dt, false, null);
            UpdateBar(staminaBar, k.stamina, Knight.MaxStamina, dt, Time.unscaledTime - k.StaminaDeniedTime < 0.35f, null);
            expBar.label.text = "LV " + pr.level;
            if (pr.IsMax) UpdateBar(expBar, 1f, 1f, dt, false, "MAX LEVEL");
            else UpdateBar(expBar, pr.exp, Progress.Need(pr.level), dt, false, Mathf.FloorToInt(pr.exp) + " / " + Progress.Need(pr.level) + " EXP");

            // upgrade totals
            var sb = new System.Text.StringBuilder(400);
            sb.Append("<b>DAMAGE</b> <color=#ff6b52>x").Append(pr.DamageMult(-1).ToString("0.0"))
              .Append("</color>   <b>ATK SPEED</b> <color=#ffdb4d>x").Append(pr.SpeedMult(-1).ToString("0.0"))
              .Append("</color>   <b>CRIT</b> <color=#cc8cff>").Append((pr.CritChance(-1) * 100f).ToString("0")).Append("% x").Append(pr.CritMult(-1).ToString("0.0")).Append("</color>\n");
            for (int i = 0; i < 4; i++)
            {
                SkillDef s = Knight.Skills[i];
                sb.Append(s.key).Append(' ').Append(s.name.PadRight(12))
                  .Append("  dmg x").Append(pr.DamageMult(i).ToString("0.0"))
                  .Append("  spd x").Append(pr.SpeedMult(i).ToString("0.0"))
                  .Append("  crit ").Append((pr.CritChance(i) * 100f).ToString("0")).Append("% x").Append(pr.CritMult(i).ToString("0.0")).Append('\n');
            }
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
                bool noStamina = left <= 0f && k.stamina < s.stamina;
                u.icon.color = left > 0f ? s.color * 0.35f : noStamina ? new Color(0.45f, 0.2f, 0.2f) : s.color * 0.75f;
                u.key.color = left > 0f ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                u.cost.text = (s.cooldown / pr.SpeedMult(i)).ToString("0.0") + "s   " + (int)s.stamina + " st";
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

            if (controlsPanel.activeSelf != showControls) controlsPanel.SetActive(showControls);
            if (controlsText.gameObject.activeSelf != showControls) controlsText.gameObject.SetActive(showControls);

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

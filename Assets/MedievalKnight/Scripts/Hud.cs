using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MedievalKnight
{
    // All UI, built with uGUI from code: health / armor / stamina bars, dummy read-out,
    // attack banner, floating damage numbers and the on-screen controls.
    public class Hud : MonoBehaviour
    {
        class BarUi
        {
            public RectTransform fill, ghost;
            public Image fillImg, frame;
            public Text value;
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

        Font font;
        Canvas canvas;
        RectTransform root;
        BarUi hpBar, armorBar, staminaBar;
        Text dummyText, bannerText, controlsText;
        GameObject controlsPanel;
        bool showControls = true;
        float bannerTime;
        Color bannerColor;
        readonly List<PopupUi> popups = new List<PopupUi>();
        readonly Stack<PopupUi> popupPool = new Stack<PopupUi>();

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

            // --- knight bars, top-left ---
            Panel("BarsPanel", new Vector2(0, 1), new Vector2(16, -16), new Vector2(400, 112), new Color(0, 0, 0, 0.4f));
            hpBar = MakeBar("HEALTH", 0, new Color(0.86f, 0.2f, 0.2f));
            armorBar = MakeBar("ARMOR", 1, new Color(0.5f, 0.68f, 0.92f));
            staminaBar = MakeBar("STAMINA", 2, new Color(0.95f, 0.78f, 0.22f));

            // --- dummy read-out, top-right ---
            Panel("DummyPanel", new Vector2(1, 1), new Vector2(-16, -16), new Vector2(400, 112), new Color(0, 0, 0, 0.4f));
            dummyText = MakeText("DummyText", new Vector2(1, 1), new Vector2(-28, -22), new Vector2(376, 100), 16, TextAnchor.UpperLeft);

            // --- attack banner, top-centre ---
            bannerText = MakeText("Banner", new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(800, 50), 30, TextAnchor.MiddleCenter);
            bannerText.fontStyle = FontStyle.Bold;
            bannerText.text = "";

            // --- controls, right column under the dummy read-out (keeps the fighting area clear) ---
            controlsPanel = Panel("ControlsPanel", new Vector2(1, 1), new Vector2(-16, -136), new Vector2(400, 204), new Color(0, 0, 0, 0.4f)).gameObject;
            controlsText = MakeText("Controls", new Vector2(1, 1), new Vector2(-28, -142), new Vector2(376, 192), 14, TextAnchor.UpperLeft, null);
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

        BarUi MakeBar(string label, int row, Color color)
        {
            float y = -28f - row * 32f;
            MakeText(label + "Label", new Vector2(0, 1), new Vector2(28, y), new Vector2(90, 24), 15, TextAnchor.MiddleLeft).text = label;

            var b = new BarUi { color = color };
            b.frame = Panel(label + "Bar", new Vector2(0, 1), new Vector2(116, y), new Vector2(288, 24), new Color(0.06f, 0.06f, 0.08f, 0.95f));
            Transform frame = b.frame.transform;

            b.ghost = Stretch(Panel(label + "Ghost", Vector2.zero, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.75f), frame));
            b.fillImg = Panel(label + "Fill", Vector2.zero, Vector2.zero, Vector2.zero, color, frame);
            b.fill = Stretch(b.fillImg);

            b.value = MakeText(label + "Value", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280, 24), 14, TextAnchor.MiddleCenter, frame);
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

        // ---------------------------------------------------------------- public API

        public void Banner(string text, Color color)
        {
            bannerText.text = text;
            bannerColor = color;
            bannerTime = 1.1f;
        }

        // Floating text at a world position (damage numbers, BLOCK...).
        public void Popup(Vector3 world, string text, Color color, int size)
        {
            PopupUi p;
            if (popupPool.Count > 0) p = popupPool.Pop();
            else
            {
                p = new PopupUi();
                p.text = MakeText("Popup", Vector2.zero, Vector2.zero, new Vector2(200, 40), size, TextAnchor.MiddleCenter);
                p.text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                p.text.fontStyle = FontStyle.Bold;
            }
            p.text.gameObject.SetActive(true);
            p.text.text = text;
            p.text.fontSize = size;
            p.world = world;
            p.color = color;
            p.maxLife = p.life = 0.8f;
            popups.Add(p);
        }

        // ---------------------------------------------------------------- update

        void LateUpdate()
        {
            Game g = Game.I;
            if (g == null || g.knight == null || g.dummy == null) return;
            float dt = Time.unscaledDeltaTime;
            Knight k = g.knight;

            UpdateBar(hpBar, k.hp, Knight.MaxHp, dt, false);
            UpdateBar(armorBar, k.armor, Knight.MaxArmor, dt, false);
            UpdateBar(staminaBar, k.stamina, Knight.MaxStamina, dt, Time.unscaledTime - k.StaminaDeniedTime < 0.35f);

            Dummy d = g.dummy;
            dummyText.text = "STRAW DUMMY" + (d.Broken ? "   (rebuilding...)" : "")
                + "\nHealth " + Mathf.CeilToInt(d.hp) + " / " + (int)Dummy.MaxHp
                + "     Armor " + Mathf.CeilToInt(d.armor) + " / " + (int)Dummy.MaxArmor
                + "\nLast hit:  " + d.lastHit
                + "\nHits " + d.hits + "     Total " + Mathf.RoundToInt(d.totalDamage) + "     DPS (5s) " + d.Dps.ToString("0.0");

            if (InputX.Down(KeyCode.F1)) showControls = !showControls;
            controlsPanel.SetActive(showControls);
            controlsText.text = !showControls ? "" :
                "CONTROLS   (F1 hides this)\n"
                + "A / D or Left / Right    Move\n"
                + "Space    Jump\n"
                + "Hold Left Shift    Raise shield (block)\n"
                + "1  Side Slash        2  Overhead Chop\n"
                + "3  Rising Slash      4  Spin Slash\n"
                + "5 or K    Thrust (half ignores armor)\n"
                + "J    Slash combo  1 > 2 > 3 > 4\n"
                + "H    Take a test hit (20 damage)\n"
                + "T    Dummy counter-attack  [" + (g.counterMode ? "ON" : "OFF") + "]\n"
                + "R    Reset everything        Esc    Quit";

            if (bannerTime > 0f)
            {
                bannerTime -= dt;
                Color c = bannerColor;
                c.a = Mathf.Clamp01(bannerTime / 0.35f);
                bannerText.color = c;
                float pop = 1f + Mathf.Clamp01((bannerTime - 0.95f) / 0.15f) * 0.25f;
                bannerText.rectTransform.localScale = Vector3.one * pop;
            }
            else if (bannerText.text.Length > 0) bannerText.text = "";

            float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
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
                Vector3 sp = g.cam.WorldToScreenPoint(p.world + new Vector3(0f, t * 1.1f, 0f));
                p.text.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y) / scale;
                Color c = p.color;
                c.a = Mathf.Clamp01((1f - t) * 2.5f);
                p.text.color = c;
                p.text.rectTransform.localScale = Vector3.one * (1f + Mathf.Clamp01(1f - t * 6f) * 0.6f);
            }
        }

        void UpdateBar(BarUi b, float value, float max, float dt, bool denied)
        {
            float k = Mathf.Clamp01(value / max);
            b.shown = Mathf.MoveTowards(b.shown, k, dt * 3f);
            if (b.ghostShown < b.shown) b.ghostShown = b.shown;                   // gains snap
            else b.ghostShown = Mathf.MoveTowards(b.ghostShown, b.shown, dt * 0.35f);   // losses drain slowly

            b.fill.anchorMax = new Vector2(Mathf.Max(0.0001f, b.shown), 1f);
            b.fill.offsetMax = new Vector2(-3f * b.shown, -3f);
            b.ghost.anchorMax = new Vector2(Mathf.Max(0.0001f, b.ghostShown), 1f);
            b.ghost.offsetMax = new Vector2(-3f * b.ghostShown, -3f);
            b.fillImg.enabled = b.shown > 0.002f;

            b.value.text = Mathf.CeilToInt(value) + " / " + (int)max;
            b.fillImg.color = denied ? Color.Lerp(b.color, Color.red, 0.75f) : (k < 0.25f ? Color.Lerp(b.color, Color.white, Mathf.PingPong(Time.unscaledTime * 4f, 0.5f)) : b.color);
            b.frame.color = denied ? new Color(0.6f, 0.05f, 0.05f, 0.95f) : new Color(0.06f, 0.06f, 0.08f, 0.95f);
        }
    }
}

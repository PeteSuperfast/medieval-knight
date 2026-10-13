using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MedievalKnight
{
    // Phase 4: what only an online team needs on screen, on a canvas of its own (above the HUD, below LoopUi):
    //   R1 / T1  the roster: one row per player (ready / packing / DOWN 42s / FELL / LEFT, level, health)
    //   R4       "Run in progress - you'll join the next one" for somebody who waits at the base
    //   K1 S1 X1 (later steps) the fallen screen, spectating, the countdown home
    // It only reads TeamRun / Game and draws; every decision stays in RunLoop / TeamRun.
    public class TeamUi : MonoBehaviour
    {
        const int MaxRows = 4;
        static readonly Color Green = new Color(0.435f, 0.82f, 0.435f), Red = new Color(0.878f, 0.314f, 0.314f), Yellow = new Color(0.949f, 0.757f, 0.306f),
            Gray = new Color(0.584f, 0.541f, 0.502f), Sky = new Color(0.435f, 0.784f, 1f);

        RunLoop run;
        Game g;
        Font font;
        Canvas canvas;
        RectTransform root;
        readonly Text[] rows = new Text[MaxRows];
        readonly Image[] swatches = new Image[MaxRows];
        Text waiting;

        public static TeamUi Create(Transform parent, RunLoop run)
        {
            var go = new GameObject("TeamUi");
            go.transform.SetParent(parent, false);
            var ui = go.AddComponent<TeamUi>();
            ui.run = run;
            ui.g = Game.I;
            ui.Build();
            return ui;
        }

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

        Text Label(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
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
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.9f);
            o.effectDistance = new Vector2(1.2f, -1.2f);
            return t;
        }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            var cgo = new GameObject("Canvas");
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 140;                               // above the HUD (100), below LoopUi (150) and the menu (200)
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f;
            root = canvas.GetComponent<RectTransform>();

            Vector2 tl = new Vector2(0f, 1f);
            for (int i = 0; i < MaxRows; i++)
            {
                swatches[i] = Box("Swatch" + i, root, tl, new Vector2(18f, -(238f + i * 26f)), new Vector2(14f, 14f), Color.white);
                swatches[i].rectTransform.pivot = tl;
                rows[i] = Label("Row" + i, root, tl, new Vector2(38f, -(234f + i * 26f)), new Vector2(380f, 22f), 17, TextAnchor.MiddleLeft);
                rows[i].rectTransform.pivot = tl;
                rows[i].fontStyle = FontStyle.Bold;
                rows[i].gameObject.SetActive(false);
                swatches[i].gameObject.SetActive(false);
            }
            waiting = Label("Waiting", root, new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(900f, 30f), 22, TextAnchor.MiddleCenter);
            waiting.fontStyle = FontStyle.Bold;
            waiting.color = Yellow;
            waiting.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- test read-outs

        public int RowsShown { get { int n = 0; foreach (Text t in rows) if (t.gameObject.activeSelf) n++; return n; } }
        public string RowText(int i) { return rows[i].gameObject.activeSelf ? rows[i].text : ""; }
        public string WaitingText { get { return waiting.gameObject.activeSelf ? waiting.text : ""; } }

        // ---------------------------------------------------------------- drawing

        static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

        // The state of one player as a short coloured string (R1 at the base, T1 in the world).
        public static string StatusOf(Game g, TeamRun t, Member m, bool inWorld)
        {
            if (m.status == Stat.Left && m.inRun) return "<color=" + Hex(Gray) + ">LEFT</color>";
            if (!inWorld)
            {
                if (m.inRun) return "<color=" + Hex(Yellow) + ">IN THE WORLD</color>";
                if (m.ready || m.pid == 0) return "<color=" + Hex(Green) + ">READY</color>";
                return "<color=" + Hex(Yellow) + ">packing...</color>";
            }
            if (!m.inRun) return "<color=" + Hex(Gray) + ">WAITING</color>";
            Knight k = g.PlayerByPid(m.pid);
            switch (m.status)
            {
                case Stat.Down:
                {
                    float left = k != null ? k.DownLeft : m.downLeft;
                    bool on = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;
                    return "<color=" + Hex(on ? Red : new Color(0.55f, 0.2f, 0.2f)) + ">DOWN " + Mathf.CeilToInt(left) + "s</color>";
                }
                case Stat.Fallen: return "<color=" + Hex(Gray) + ">FELL</color>";
                case Stat.Left: return "<color=" + Hex(Gray) + ">LEFT</color>";
            }
            string lv = k != null ? "Lv " + k.progress.level : "";
            string hp = k != null ? "   " + Mathf.CeilToInt(k.hp) + " / " + Mathf.CeilToInt(k.MaxHp) : "";
            return lv + hp;
        }

        // One line with everybody (PREPARE's header): "Peat (host) READY    Alice packing...".
        public static string RosterLine(Game g, TeamRun t)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Member m in t.roster)
            {
                if (sb.Length > 0) sb.Append("      ");
                string nm = m.name + (m.pid == 0 ? " (host)" : "");
                sb.Append("<color=").Append(Hex(Knight.UiColor(m.pid))).Append(">").Append(nm).Append("</color>  ").Append(StatusOf(g, t, m, false));
            }
            return sb.ToString();
        }

        void LateUpdate()
        {
            TeamRun t = run != null ? run.team : null;
            if (t == null || g == null || !g.InSession || g.knight == null) { SetAll(false); return; }
            bool inWorld = run.InWorld;
            bool panel = run.PrepareOpen || run.CarryOpen || run.LootOpen || run.SummaryOpen;

            // R4: a run is going on and this player is not in it
            bool wait = t.runLive && !t.LocalPlaying && !inWorld && !run.Busy && !run.SummaryOpen;
            if (waiting.gameObject.activeSelf != wait) waiting.gameObject.SetActive(wait);
            if (wait) waiting.text = "Run in progress - you'll join the next one";

            // R1 / T1: the roster rows (hidden under PREPARE: its header line has them)
            bool show = !run.PrepareOpen && !run.SummaryOpen && !run.Busy;
            int n = 0;
            if (show)
                foreach (Member m in t.roster)
                {
                    if (n >= MaxRows) break;
                    Text tx = rows[n];
                    if (!tx.gameObject.activeSelf) { tx.gameObject.SetActive(true); swatches[n].gameObject.SetActive(true); }
                    swatches[n].color = Knight.MainColors[m.pid & 3];
                    string nm = m.name + (m.pid == Net.LocalPid ? " (you)" : m.pid == 0 ? " (host)" : "");
                    tx.text = "<color=" + Hex(Knight.UiColor(m.pid)) + ">" + nm + "</color>   " + StatusOf(g, t, m, inWorld);
                    n++;
                }
            for (int i = n; i < MaxRows; i++)
                if (rows[i].gameObject.activeSelf) { rows[i].gameObject.SetActive(false); swatches[i].gameObject.SetActive(false); }
            if (panel && inWorld) { }
        }

        void SetAll(bool on)
        {
            foreach (Text t in rows) if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            foreach (Image i in swatches) if (i.gameObject.activeSelf != on) i.gameObject.SetActive(on);
            if (waiting.gameObject.activeSelf != on) waiting.gameObject.SetActive(on);
        }
    }
}

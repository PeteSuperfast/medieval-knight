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
        float revSince;                                               // when somebody started reviving the local knight
        Image homeFrame;                                              // X1: the countdown home
        Text homeText;
        // K1: the local knight is down; S1: fallen for good, watching
        Image vignette, revBarBg, revBar, holdBarBg, holdBar;
        Text downTitle, downSub, revBy, revPrompt, specTitle, specHint, specKeys;
        readonly Text[] overhead = new Text[MaxRows];                // "DOWN 42" over every downed knight

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

            homeFrame = Box("HomeFrame", root, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(430f, 76f), Sky);
            homeFrame.rectTransform.pivot = new Vector2(0.5f, 1f);
            Box("Bg", homeFrame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(424f, 70f), new Color(0.04f, 0.07f, 0.12f, 0.85f));
            homeText = Label("HomeText", homeFrame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 70f), 22, TextAnchor.MiddleCenter);
            homeText.fontStyle = FontStyle.Bold;
            homeText.color = Sky;
            homeFrame.gameObject.SetActive(false);

            // K1
            vignette = Box("Vignette", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.55f, 0.02f, 0.02f, 0f));
            vignette.rectTransform.anchorMin = Vector2.zero; vignette.rectTransform.anchorMax = Vector2.one;
            vignette.rectTransform.offsetMin = vignette.rectTransform.offsetMax = Vector2.zero;
            downTitle = Label("DownTitle", root, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(900f, 52f), 40, TextAnchor.MiddleCenter);
            downTitle.fontStyle = FontStyle.Bold; downTitle.color = Red; downTitle.text = "YOU ARE DOWN";
            downSub = Label("DownSub", root, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(700f, 30f), 22, TextAnchor.MiddleCenter);
            revBy = Label("ReviveBy", root, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(700f, 28f), 22, TextAnchor.MiddleCenter);
            revBy.fontStyle = FontStyle.Bold; revBy.color = Green;
            revBarBg = Box("RevBarBg", root, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(224f, 18f), new Color(0f, 0f, 0f, 0.7f));
            revBar = Box("RevBar", revBarBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(220f, 14f), Green);
            revBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            // the reviver: "Hold F: Revive Bob" and the bar
            revPrompt = Label("RevivePrompt", root, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(700f, 30f), 24, TextAnchor.MiddleCenter);
            revPrompt.fontStyle = FontStyle.Bold; revPrompt.color = Green;
            holdBarBg = Box("HoldBarBg", root, new Vector2(0.5f, 0f), new Vector2(0f, 164f), new Vector2(224f, 18f), new Color(0f, 0f, 0f, 0.7f));
            holdBar = Box("HoldBar", holdBarBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(220f, 14f), Green);
            holdBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            // S1
            specTitle = Label("SpecTitle", root, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(900f, 40f), 30, TextAnchor.MiddleCenter);
            specTitle.fontStyle = FontStyle.Bold;
            specHint = Label("SpecHint", root, new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(900f, 32f), 24, TextAnchor.MiddleCenter);
            specHint.color = Gray; specHint.fontStyle = FontStyle.Bold; specHint.text = "You fell. Waiting for the team...";
            specKeys = Label("SpecKeys", root, new Vector2(0.5f, 0f), new Vector2(0f, 184f), new Vector2(900f, 26f), 18, TextAnchor.MiddleCenter);
            specKeys.color = Gray; specKeys.text = "[Q / E or Left / Right: switch]";
            for (int i = 0; i < MaxRows; i++)
            {
                overhead[i] = Label("Overhead" + i, root, new Vector2(0f, 0f), Vector2.zero, new Vector2(260f, 28f), 22, TextAnchor.MiddleCenter);
                overhead[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                overhead[i].fontStyle = FontStyle.Bold;
                overhead[i].gameObject.SetActive(false);
            }
            SetFallenUi(false, false, false);
        }

        // shows / hides the K1 (down) and S1 (fallen) texts and the reviver's prompt
        void SetFallenUi(bool down, bool fallen, bool prompt)
        {
            if (downTitle.gameObject.activeSelf != down) { downTitle.gameObject.SetActive(down); downSub.gameObject.SetActive(down); }
            if (vignette.gameObject.activeSelf != (down || fallen)) vignette.gameObject.SetActive(down || fallen);
            if (specTitle.gameObject.activeSelf != fallen) { specTitle.gameObject.SetActive(fallen); specHint.gameObject.SetActive(fallen); specKeys.gameObject.SetActive(fallen); }
            if (revPrompt.gameObject.activeSelf != prompt) { revPrompt.gameObject.SetActive(prompt); holdBarBg.gameObject.SetActive(prompt); }
            if (!down && revBy.gameObject.activeSelf) { revBy.gameObject.SetActive(false); revBarBg.gameObject.SetActive(false); }
        }

        // ---------------------------------------------------------------- test read-outs

        public int RowsShown { get { int n = 0; foreach (Text t in rows) if (t.gameObject.activeSelf) n++; return n; } }
        public string RowText(int i) { return rows[i].gameObject.activeSelf ? rows[i].text : ""; }
        public string WaitingText { get { return waiting.gameObject.activeSelf ? waiting.text : ""; } }
        public string HomeTextShown { get { return homeFrame.gameObject.activeSelf ? homeText.text : ""; } }
        public bool DownShown { get { return downTitle.gameObject.activeSelf; } }
        public string DownSubText { get { return downSub.text; } }
        public string ReviveByText { get { return revBy.gameObject.activeSelf ? revBy.text : ""; } }
        public string RevivePromptText { get { return revPrompt.gameObject.activeSelf ? revPrompt.text : ""; } }
        public string SpectatingText { get { return specTitle.gameObject.activeSelf ? specTitle.text : ""; } }
        public int OverheadShown { get { int n = 0; foreach (Text t in overhead) if (t.gameObject.activeSelf) n++; return n; } }

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
            {
                var rows4 = new List<Member>(t.roster);
                if (inWorld) rows4.AddRange(t.leavers);                  // LEFT, grey, until the run is over
                foreach (Member m in rows4)
                {
                    if (n >= MaxRows) break;
                    Text tx = rows[n];
                    if (!tx.gameObject.activeSelf) { tx.gameObject.SetActive(true); swatches[n].gameObject.SetActive(true); }
                    swatches[n].color = Knight.MainColors[m.pid & 3];
                    string nm = m.name + (m.pid == Net.LocalPid ? " (you)" : m.pid == 0 ? " (host)" : "");
                    tx.text = "<color=" + Hex(Knight.UiColor(m.pid)) + ">" + nm + "</color>   " + StatusOf(g, t, m, inWorld);
                    n++;
                }
            }
            for (int i = n; i < MaxRows; i++)
                if (rows[i].gameObject.activeSelf) { rows[i].gameObject.SetActive(false); swatches[i].gameObject.SetActive(false); }
            // K1 / S1: the local knight is down / fallen; the reviver's prompt; "DOWN 42" over every downed knight
            Knight me = g.knight;
            bool amDown = inWorld && me.Down;
            bool amFallen = inWorld && me.Fallen;
            bool prompt = inWorld && t.ReviveCandidate != null && !me.Dead;
            SetFallenUi(amDown, amFallen, prompt);
            if (amDown || amFallen)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                vignette.color = new Color(0.55f, 0.02f, 0.02f, amDown ? 0.14f + 0.12f * pulse : 0.1f);
            }
            if (amDown)
            {
                downSub.text = "Help is needed: " + Mathf.CeilToInt(me.DownLeft) + " s";
                Knight by = null;
                foreach (Knight k in g.players) if (k != me && k.RevTarget == Net.LocalPid && !k.Dead) by = k;
                bool rv = by != null;
                if (revBy.gameObject.activeSelf != rv) { revBy.gameObject.SetActive(rv); revBarBg.gameObject.SetActive(rv); }
                if (rv)
                {
                    revBy.text = by.playerName + " is reviving you...";
                    float f = Mathf.Clamp01((Time.unscaledTime - revSince) / TeamRun.ReviveSeconds);
                    revBar.rectTransform.sizeDelta = new Vector2(220f * f, 14f);
                }
                else revSince = Time.unscaledTime;
            }
            else revSince = Time.unscaledTime;
            if (amFallen)
            {
                Knight w = t.SpectateTarget;
                specTitle.text = w != null ? "SPECTATING  " + w.playerName + "   (" + Mathf.CeilToInt(w.hp) + " / " + Mathf.CeilToInt(w.MaxHp) + ")" : "SPECTATING";
                specTitle.color = w != null ? Knight.UiColor(w.pid) : Gray;
            }
            if (prompt)
            {
                Knight c = t.ReviveCandidate;
                revPrompt.text = t.ReviveInterrupted ? "Interrupted!" : "Hold F: Revive " + c.playerName;
                revPrompt.color = t.ReviveInterrupted ? Red : Green;
                holdBar.rectTransform.sizeDelta = new Vector2(220f * t.ReviveProgress, 14f);
            }
            // over the head of every downed friend (clamped to the screen edge when he is off screen: "Bob  DOWN 42")
            int oh = 0;
            if (inWorld)
                foreach (Knight k in g.players)
                {
                    if (k == me || !k.Down || oh >= MaxRows) continue;
                    Vector3 sp = g.cam.WorldToScreenPoint(k.transform.position + new Vector3(0f, 3.2f, 0f));
                    bool on = sp.z > 0f && sp.x > 40f && sp.x < Screen.width - 40f && sp.y > 90f && sp.y < Screen.height - 90f;
                    Vector2 pos = new Vector2(sp.x, sp.y);
                    if (!on)
                    {
                        Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                        Vector2 dir = pos - c;
                        if (sp.z < 0f) dir = -dir;
                        if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                        float sx = Mathf.Abs(dir.x) > 0.001f ? (Screen.width * 0.5f - 90f) / Mathf.Abs(dir.x) : float.MaxValue;
                        float sy = Mathf.Abs(dir.y) > 0.001f ? (Screen.height * 0.5f - 110f) / Mathf.Abs(dir.y) : float.MaxValue;
                        pos = c + dir * Mathf.Min(sx, sy);
                    }
                    Text tx = overhead[oh++];
                    if (!tx.gameObject.activeSelf) tx.gameObject.SetActive(true);
                    bool blink = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;
                    tx.text = (on ? "" : k.playerName + "  ") + "DOWN " + Mathf.CeilToInt(k.DownLeft);
                    tx.color = blink ? Red : new Color(0.65f, 0.25f, 0.25f);
                    tx.rectTransform.anchoredPosition = pos / Mathf.Max(0.01f, canvas.scaleFactor);
                }
            for (int i = oh; i < MaxRows; i++) if (overhead[i].gameObject.activeSelf) overhead[i].gameObject.SetActive(false);

            // X1: the countdown home (everybody goes together, the fallen too)
            bool home = inWorld && t.HomeStarted && !run.SummaryOpen;
            if (homeFrame.gameObject.activeSelf != home) homeFrame.gameObject.SetActive(home);
            if (home) homeText.text = "PORTAL OPEN - returning in " + Mathf.CeilToInt(t.HomeLeft) + "\n<size=16>Everyone returns together</size>";
        }

        void SetAll(bool on)
        {
            foreach (Text t in rows) if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            foreach (Image i in swatches) if (i.gameObject.activeSelf != on) i.gameObject.SetActive(on);
            if (waiting.gameObject.activeSelf != on) waiting.gameObject.SetActive(on);
            if (homeFrame.gameObject.activeSelf != on) homeFrame.gameObject.SetActive(on);
            if (!on) { SetFallenUi(false, false, false); foreach (Text o in overhead) if (o.gameObject.activeSelf) o.gameObject.SetActive(false); }
        }
    }
}

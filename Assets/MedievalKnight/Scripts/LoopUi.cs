using UnityEngine;
using UnityEngine.UI;

namespace MedievalKnight
{
    // The Phase 1 screens on a canvas of their own (above the HUD, below the start menu): PREPARE, the end-of-run
    // summary, the carry window in the world, the fade. Placeholder in this step: the real pages come next.
    public class LoopUi : MonoBehaviour
    {
        Canvas canvas;
        GameObject prepareRoot;
        Text prepareText;
        Font font;

        public static LoopUi Create(Transform parent, RunLoop run)
        {
            var go = new GameObject("LoopUi");
            go.transform.SetParent(parent, false);
            var ui = go.AddComponent<LoopUi>();
            ui.Build();
            return ui;
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

            prepareRoot = new GameObject("Prepare", typeof(RectTransform));
            prepareRoot.transform.SetParent(cgo.transform, false);
            var img = prepareRoot.AddComponent<Image>();
            img.color = new Color(0.063f, 0.078f, 0.094f, 0.85f);
            var rt = prepareRoot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1000, 560);
            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(prepareRoot.transform, false);
            prepareText = tgo.AddComponent<Text>();
            prepareText.font = font;
            prepareText.fontSize = 36;
            prepareText.alignment = TextAnchor.UpperLeft;
            prepareText.color = Color.white;
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(24, 24); trt.offsetMax = new Vector2(-24, -24);
            prepareRoot.SetActive(false);
        }

        public bool PrepareVisible { get { return prepareRoot != null && prepareRoot.activeSelf; } }

        public void ShowPrepare(Loadout data)
        {
            prepareRoot.SetActive(true);
            prepareText.text = "PREPARE";
        }

        public void HideAll()
        {
            prepareRoot.SetActive(false);
        }
    }
}

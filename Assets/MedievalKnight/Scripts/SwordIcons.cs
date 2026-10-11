using UnityEngine;

namespace MedievalKnight
{
    // The picture of each sword shown in the Tab window (and nowhere else).
    // Built once at start: the real sword model (Weapons.BuildModel) is placed far below the arena on layer 31,
    // photographed by a throw-away orthographic camera into a 128 x 128 texture and copied to a plain Texture2D,
    // so nothing renders a sword icon again after that. A solid background colour is used (no alpha needed).
    // If the render comes out empty (a pipeline that refuses a manual Camera.Render), plan B draws a flat
    // silhouette of the sword with a few rotated rectangles - no rendering involved.
    public static class SwordIcons
    {
        public const int Size = 128;
        public const int StageLayer = 31;
        public static readonly Color Background = new Color(0.12f, 0.12f, 0.17f);

        static Texture2D[] icons, shadows;
        static int[] nonBackground;
        public static string Method = "none";           // "render" | "drawn" (what was used), for the test log

        public static Texture2D Get(int weaponId)
        {
            if (icons == null) Build(null);
            return icons[Mathf.Clamp(weaponId, 0, icons.Length - 1)];
        }

        // The same picture as a black cut-out on the slot colour: the sword of the owner-to-be.
        public static Texture2D Shadow(int weaponId)
        {
            if (shadows == null) Build(null);
            return shadows[Mathf.Clamp(weaponId, 0, shadows.Length - 1)];
        }

        // Pixels that differ from the background (test read-out) and a cheap picture hash.
        public static int NonBackground(int weaponId) { return nonBackground[weaponId]; }

        public static int Hash(int weaponId)
        {
            Color32[] px = icons[weaponId].GetPixels32();
            unchecked
            {
                int h = 17;
                for (int i = 0; i < px.Length; i += 7) h = h * 31 + px[i].r * 3 + px[i].g * 5 + px[i].b * 7;
                return h;
            }
        }

        public static void Build(Camera mainCamera)
        {
            int n = Weapons.All.Length;
            icons = new Texture2D[n];
            shadows = new Texture2D[n];
            nonBackground = new int[n];
            Method = "render";
            for (int i = 0; i < n; i++)
            {
                Texture2D t = null;
                try { t = Render(Weapons.All[i]); }
                catch (System.Exception e) { Debug.LogWarning("SwordIcons render failed: " + e.Message); }
                int count = t != null ? CountForeground(t) : 0;
                if (t == null || count < 300)
                {
                    if (t != null) Object.Destroy(t);
                    t = Draw(Weapons.All[i]);
                    count = CountForeground(t);
                    Method = "drawn";
                }
                icons[i] = t;
                nonBackground[i] = count;
                shadows[i] = MakeShadow(t);
            }
            if (mainCamera != null) mainCamera.cullingMask &= ~(1 << StageLayer);
            Debug.Log("SWORDICONS built " + n + " icons by " + Method + ", non-background pixels " + string.Join("/", System.Array.ConvertAll(nonBackground, v => v.ToString())));
        }

        static bool IsBackground(Color32 c, Color32 bg)
        {
            return Mathf.Abs(c.r - bg.r) < 7 && Mathf.Abs(c.g - bg.g) < 7 && Mathf.Abs(c.b - bg.b) < 7;
        }

        static int CountForeground(Texture2D t)
        {
            Color32[] px = t.GetPixels32();
            int n = 0;
            Color32 bg = px[(Size - 1) * Size];                  // the top-left corner is always background (the sword runs corner to corner)
            for (int i = 0; i < px.Length; i++) if (!IsBackground(px[i], bg)) n++;
            return n;
        }

        static Texture2D MakeShadow(Texture2D t)
        {
            Color32[] px = t.GetPixels32();
            var o = new Color32[px.Length];
            Color32 src = px[(Size - 1) * Size], bg = Background, ink = new Color32(8, 8, 12, 255);
            for (int i = 0; i < px.Length; i++) o[i] = IsBackground(px[i], src) ? bg : ink;
            var s = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            s.SetPixels32(o);
            s.Apply();
            return s;
        }

        // ---------------------------------------------------------------- plan A: photograph the model

        static Texture2D Render(WeaponDef w)
        {
            var stage = new GameObject("SwordIconStage");
            stage.transform.position = new Vector3(0f, -1000f, 0f);
            Transform model = Weapons.BuildModel(stage.transform, w);
            model.localRotation = Quaternion.Euler(0f, 45f, 0f);       // tip to the upper right, whatever the camera does
            foreach (Transform t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = StageLayer;
            Bounds b = new Bounds(model.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in stage.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }

            var camGo = new GameObject("SwordIconCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.cullingMask = 1 << StageLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.allowMSAA = false;
            cam.allowHDR = false;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 40f;
            float half = Mathf.Max(b.size.x, b.size.z) * 0.5f * 1.12f + 0.05f;
            cam.orthographicSize = half;
            cam.aspect = 1f;
            camGo.transform.rotation = Quaternion.Euler(72f, 0f, 0f);   // looking down, tilted a little so the guard has some depth
            camGo.transform.position = b.center - camGo.transform.forward * 15f;

            var rt = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 1;
            cam.targetTexture = rt;
            cam.Render();

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Object.DestroyImmediate(camGo);                // immediately: a delayed Destroy left every earlier sword in the next photo
            Object.DestroyImmediate(stage);
            return tex;
        }

        // ---------------------------------------------------------------- plan B: draw a flat picture

        struct Part { public float z0, z1, halfW; public Color color; }

        static Texture2D Draw(WeaponDef w)
        {
            float gripBack = w.gripLen - 0.17f;
            float gz = 0.2f, z0 = gz + 0.035f;
            float guardHalf = w.guard == GuardStyle.Wide ? 0.37f : w.guard == GuardStyle.Cup ? 0.2f : w.guard == GuardStyle.Fang ? 0.26f : w.guard == GuardStyle.Horns ? 0.34f : 0.23f;
            float total = gripBack + z0 + w.bladeLen + 0.06f;
            var parts = new System.Collections.Generic.List<Part>
            {
                new Part { z0 = -gripBack - 0.08f, z1 = -gripBack + 0.06f, halfW = 0.07f, color = w.guardColor },       // pommel
                new Part { z0 = -gripBack, z1 = 0.17f, halfW = 0.035f, color = w.gripColor },                           // grip
                new Part { z0 = gz - 0.05f, z1 = gz + 0.05f, halfW = guardHalf, color = w.guardColor },                 // guard
                new Part { z0 = z0, z1 = z0 + w.bladeLen - w.tipLen, halfW = w.bladeWidth * 0.5f, color = w.bladeColor },
            };
            if (w.coreColor.a > 0f) parts.Add(new Part { z0 = z0 + 0.05f, z1 = z0 + w.bladeLen - w.tipLen, halfW = w.bladeWidth * 0.14f, color = w.coreColor });
            float scale = 98f / Mathf.Max(0.3f, total);                     // pixels per metre
            var px = new Color32[Size * Size];
            Color32 bg = Background;
            Vector2 u = new Vector2(0.7071f, 0.7071f), v = new Vector2(-0.7071f, 0.7071f);   // along the sword / across it
            float zMid = (-gripBack - 0.08f + z0 + w.bladeLen) * 0.5f;
            float tipStart = z0 + w.bladeLen - w.tipLen, tipEnd = z0 + w.bladeLen;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x - Size * 0.5f, dy = y - Size * 0.5f;
                    float z = (dx * u.x + dy * u.y) / scale + zMid;
                    float s = (dx * v.x + dy * v.y) / scale;
                    Color32 c = bg;
                    for (int i = 0; i < parts.Count; i++)
                    {
                        Part p = parts[i];
                        if (z >= p.z0 && z <= p.z1 && Mathf.Abs(s) <= p.halfW) c = p.color;
                    }
                    if (z >= tipStart && z <= tipEnd)                        // the point: the blade narrows to nothing
                    {
                        float k = 1f - (z - tipStart) / Mathf.Max(0.01f, tipEnd - tipStart);
                        if (Mathf.Abs(s) <= w.bladeWidth * 0.5f * k) c = w.bladeColor;
                    }
                    px[y * Size + x] = c;
                }
            }
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px);
            t.Apply();
            return t;
        }
    }
}

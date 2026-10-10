using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // Loads the PNGs in Resources/Sprites (art from the requester's reference pack) and slices them
    // into sprites in code, plus a few small procedural helpers. Everything is 16 pixels per unit.
    public static class Art
    {
        public const float PPU = 16f;

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Texture2D Tex(string name)
        {
            var t = Resources.Load<Texture2D>("Sprites/" + name);
            if (t == null)
            {
                Debug.LogWarning("Art: missing texture " + name);
                t = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var px = new Color32[64];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 0, 255, 255);
                t.SetPixels32(px);
                t.Apply();
            }
            t.filterMode = FilterMode.Point;
            return t;
        }

        // Pivot is normalised (0..1). fullRect is needed for SpriteRenderer.drawMode = Tiled.
        public static Sprite Get(string name, float pivotX = 0.5f, float pivotY = 0.5f, bool fullRect = false)
        {
            string key = name + "|" + pivotX + "|" + pivotY + "|" + fullRect;
            Sprite s;
            if (cache.TryGetValue(key, out s) && s != null) return s;
            s = Make(Tex(name), pivotX, pivotY, PPU, fullRect);
            s.name = name;
            cache[key] = s;
            return s;
        }

        static Sprite Make(Texture2D t, float px, float py, float ppu, bool fullRect)
        {
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(px, py), ppu, 0,
                fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight);
        }

        // Same shape as the named sprite but every visible pixel is white. Used for the hit flash.
        public static Sprite Silhouette(string name, float pivotX, float pivotY)
        {
            string key = "sil|" + name + "|" + pivotX + "|" + pivotY;
            Sprite s;
            if (cache.TryGetValue(key, out s) && s != null) return s;

            Texture2D src = Tex(name);
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            Color32[] px;
            try { px = src.GetPixels32(); }
            catch (System.Exception) { px = new Color32[src.width * src.height]; }
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, px[i].a);
            t.SetPixels32(px);
            t.Apply();
            s = Make(t, pivotX, pivotY, PPU, false);
            cache[key] = s;
            return s;
        }

        // 1x1 unit white square. pivotX 0 = grows to the right (bars), 0.5 = centred (particles).
        public static Sprite Pixel(float pivotX = 0.5f)
        {
            string key = "pixel|" + pivotX;
            Sprite s;
            if (cache.TryGetValue(key, out s) && s != null) return s;
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(px);
            t.Apply();
            s = Make(t, pivotX, 0.5f, 4f, false);
            cache[key] = s;
            return s;
        }

        // Soft round blob, 1 unit across. Used for the ground shadows.
        public static Sprite Blob()
        {
            Sprite s;
            if (cache.TryGetValue("blob", out s) && s != null) return s;
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(a * 1.8f)));
                }
            t.SetPixels32(px);
            t.Apply();
            s = Make(t, 0.5f, 0.5f, n, false);
            cache["blob"] = s;
            return s;
        }

        // 16x16 grass tile for the walkable floor, built from the green top rows of ground_grass.png
        // so it matches the reference palette.
        public static Sprite GrassTop()
        {
            Sprite s;
            if (cache.TryGetValue("grasstop", out s) && s != null) return s;
            Texture2D src = Tex("ground_grass");
            var pal = new List<Color32>();
            try
            {
                Color32[] sp = src.GetPixels32();
                for (int y = src.height - 3; y < src.height; y++)
                    for (int x = 0; x < src.width; x++)
                    {
                        Color32 c = sp[y * src.width + x];
                        if (c.a > 200 && c.g > c.r && c.g > c.b) pal.Add(c);
                    }
            }
            catch (System.Exception) { }
            if (pal.Count == 0) pal.Add(new Color32(92, 168, 72, 255));

            const int n = 16;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            var px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                int h = (i * 7919 + (i / n) * 104729 + 13) * 2654435 & 0x7fffffff;
                px[i] = pal[(h >> 8) % pal.Count];
            }
            t.SetPixels32(px);
            t.Apply();
            s = Make(t, 0.5f, 0.5f, PPU, true);
            cache["grasstop"] = s;
            return s;
        }

        public static SpriteRenderer NewSprite(string name, Sprite sprite, Transform parent, int order)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // Flat-shaded mesh builder: every triangle owns its three vertices, so faces look low-poly.
    // "want" is the direction the face should look at; the winding is flipped to match it.
    public class MeshB
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> t = new List<int>();

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 want)
        {
            Tri(a, b, c, want, Vector2.zero, Vector2.zero, Vector2.zero);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 want, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a);
            if (nn.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(nn, want) < 0f)
            {
                Vector3 tp = b; b = c; c = tp;
                Vector2 tu = ub; ub = uc; uc = tu;
                nn = -nn;
            }
            nn.Normalize();
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(nn); n.Add(nn); n.Add(nn);
            uv.Add(ua); uv.Add(ub); uv.Add(uc);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
        {
            Tri(a, b, c, want);
            Tri(a, c, d, want);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Tri(a, b, c, want, ua, ub, uc);
            Tri(a, c, d, want, ua, uc, ud);
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    // Everything visual is made here from code: materials (instances of the three base materials
    // in Resources/Mats, which BuildScript generates so the URP shaders are in the build) and
    // low-poly procedural meshes (boxes, lathed shapes, extruded strips, flat arcs for effects).
    public static class Prims
    {
        static Material litBase, unlitBase, fxBase;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static Mesh cube;
        static Texture2D gradient, white;

        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        static void Load()
        {
            if (litBase != null) return;
            litBase = Resources.Load<Material>("Mats/Lit");
            unlitBase = Resources.Load<Material>("Mats/Unlit");
            fxBase = Resources.Load<Material>("Mats/Fx");
            if (litBase == null) { Debug.LogError("Prims: Resources/Mats/Lit missing"); litBase = new Material(Shader.Find("Universal Render Pipeline/Lit")); }
            if (unlitBase == null) { Debug.LogError("Prims: Resources/Mats/Unlit missing"); unlitBase = new Material(Shader.Find("Universal Render Pipeline/Unlit")); }
            if (fxBase == null) { Debug.LogError("Prims: Resources/Mats/Fx missing"); fxBase = unlitBase; }
        }

        // ---------------------------------------------------------------- materials

        public static Material Lit(Color c, float smooth = 0.12f, float metal = 0f)
        {
            Load();
            string key = "L" + ColorUtility.ToHtmlStringRGBA(c) + smooth.ToString("0.00") + metal.ToString("0.00");
            Material m;
            if (mats.TryGetValue(key, out m)) return m;
            m = new Material(litBase) { name = key };
            m.SetColor(BaseColor, c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            mats[key] = m;
            return m;
        }

        // Unlit opaque colour (glowing eyes, bars, particles).
        public static Material Glow(Color c)
        {
            Load();
            string key = "U" + ColorUtility.ToHtmlStringRGBA(c);
            Material m;
            if (mats.TryGetValue(key, out m)) return m;
            m = new Material(unlitBase) { name = key };
            m.SetColor(BaseColor, c);
            mats[key] = m;
            return m;
        }

        // Shared transparent unlit material; colour / texture are set per renderer with a property block.
        public static Material FxMaterial { get { Load(); return fxBase; } }

        // White texture whose alpha rises from 0 to 1 along U: makes slash trails fade at the tail.
        public static Texture2D Gradient
        {
            get
            {
                if (gradient != null) return gradient;
                gradient = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int i = 0; i < 64; i++)
                {
                    float a = i / 63f;
                    gradient.SetPixel(i, 0, new Color(1f, 1f, 1f, a * a));
                }
                gradient.Apply();
                return gradient;
            }
        }

        public static Texture2D White
        {
            get
            {
                if (white != null) return white;
                white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                white.SetPixel(0, 0, Color.white);
                white.Apply();
                return white;
            }
        }

        // ---------------------------------------------------------------- meshes

        public static Mesh Cube
        {
            get
            {
                if (cube != null) return cube;
                var b = new MeshB();
                const float h = 0.5f;
                Vector3 p000 = new Vector3(-h, -h, -h), p100 = new Vector3(h, -h, -h), p010 = new Vector3(-h, h, -h), p110 = new Vector3(h, h, -h);
                Vector3 p001 = new Vector3(-h, -h, h), p101 = new Vector3(h, -h, h), p011 = new Vector3(-h, h, h), p111 = new Vector3(h, h, h);
                b.Quad(p000, p010, p110, p100, Vector3.back);
                b.Quad(p001, p011, p111, p101, Vector3.forward);
                b.Quad(p000, p001, p011, p010, Vector3.left);
                b.Quad(p100, p101, p111, p110, Vector3.right);
                b.Quad(p010, p011, p111, p110, Vector3.up);
                b.Quad(p000, p001, p101, p100, Vector3.down);
                cube = b.Build("Cube");
                return cube;
            }
        }

        // Surface of revolution around Y. ry = radius, height pairs from bottom to top.
        // Cylinders, cones, domes, barrels and low-poly spheres all come from this.
        public static Mesh Lathe(string key, int sides, params float[] ry)
        {
            Mesh m;
            if (meshes.TryGetValue(key, out m)) return m;
            var b = new MeshB();
            int n = ry.Length / 2;
            float ymid = 0f;
            for (int i = 0; i < n; i++) ymid += ry[i * 2 + 1];
            ymid /= n;
            Vector3 centre = new Vector3(0f, ymid, 0f);
            for (int i = 0; i < n - 1; i++)
            {
                float r0 = ry[i * 2], y0 = ry[i * 2 + 1], r1 = ry[i * 2 + 2], y1 = ry[i * 2 + 3];
                for (int j = 0; j < sides; j++)
                {
                    float a0 = j * Mathf.PI * 2f / sides, a1 = (j + 1) * Mathf.PI * 2f / sides;
                    Vector3 d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                    Vector3 A = d0 * r0 + Vector3.up * y0, B = d1 * r0 + Vector3.up * y0;
                    Vector3 C = d1 * r1 + Vector3.up * y1, D = d0 * r1 + Vector3.up * y1;
                    Vector3 want = (A + B + C + D) * 0.25f - centre;
                    // side walls must look outwards even when the centre test is ambiguous
                    Vector3 radial = (d0 + d1) * 0.5f;
                    if (Mathf.Abs(y1 - y0) > 1e-5f) want = radial * Mathf.Abs(y1 - y0) + Vector3.up * (r0 - r1) * Mathf.Sign(y1 - y0);
                    b.Tri(A, B, C, want);
                    b.Tri(A, C, D, want);
                }
            }
            m = b.Build(key);
            meshes[key] = m;
            return m;
        }

        // A band between two 2D outlines in the XY plane, extruded along Z (thickness).
        // Used for the labrys blades, the knight's shield and other plate-like shapes.
        public static Mesh Strip(string key, Vector2[] a, Vector2[] c, float thick)
        {
            Mesh m;
            if (meshes.TryGetValue(key, out m)) return m;
            var b = new MeshB();
            float h = thick * 0.5f;
            int n = a.Length;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 a0 = a[i], a1 = a[i + 1], c0 = c[i], c1 = c[i + 1];
                Vector3 f = Vector3.forward * h;
                b.Quad(a0 + f, a1 + f, c1 + f, c0 + f, Vector3.forward);
                b.Quad(a0 - f, a1 - f, c1 - f, c0 - f, Vector3.back);
                Vector3 mid = (a0 + a1 + c0 + c1) * 0.25f;
                b.Quad(a0 - f, a1 - f, a1 + f, a0 + f, (a0 + a1) * 0.5f - mid);
                b.Quad(c0 - f, c1 - f, c1 + f, c0 + f, (c0 + c1) * 0.5f - mid);
            }
            Vector3 fz = Vector3.forward * h;
            Vector3 s0 = a[0], s1 = c[0], e0 = a[n - 1], e1 = c[n - 1];
            b.Quad(s0 - fz, s1 - fz, s1 + fz, s0 + fz, ((Vector3)(a[0] + c[0]) - (Vector3)(a[1] + c[1])));
            b.Quad(e0 - fz, e1 - fz, e1 + fz, e0 + fz, ((Vector3)(a[n - 1] + c[n - 1]) - (Vector3)(a[n - 2] + c[n - 2])));
            m = b.Build(key);
            meshes[key] = m;
            return m;
        }

        // Flat ring segment lying on the XZ plane. Angles in degrees, 0 = +Z, positive turns towards +X.
        // UV.x runs 0..1 from fromDeg to toDeg, so the Gradient texture fades the starting end.
        public static Mesh Arc(float r0, float r1, float fromDeg, float toDeg)
        {
            string key = "arc" + r0.ToString("0.00") + "_" + r1.ToString("0.00") + "_" + fromDeg.ToString("0") + "_" + toDeg.ToString("0");
            Mesh m;
            if (meshes.TryGetValue(key, out m)) return m;
            var b = new MeshB();
            int seg = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(toDeg - fromDeg) / 10f));
            for (int i = 0; i < seg; i++)
            {
                float u0 = i / (float)seg, u1 = (i + 1) / (float)seg;
                float a0 = Mathf.Lerp(fromDeg, toDeg, u0) * Mathf.Deg2Rad, a1 = Mathf.Lerp(fromDeg, toDeg, u1) * Mathf.Deg2Rad;
                Vector3 d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                b.Quad(d0 * r0, d0 * r1, d1 * r1, d1 * r0, Vector3.up,
                    new Vector2(u0, 0f), new Vector2(u0, 1f), new Vector2(u1, 1f), new Vector2(u1, 0f));
            }
            m = b.Build(key);
            meshes[key] = m;
            return m;
        }

        // Flat rectangle on the XZ plane from z = 0 to z = 1, x = -0.5 .. 0.5 (scale it to size). UV.x runs along Z.
        public static Mesh Lane
        {
            get
            {
                Mesh m;
                if (meshes.TryGetValue("lane", out m)) return m;
                var b = new MeshB();
                b.Quad(new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f), new Vector3(0.5f, 0f, 0f), Vector3.up,
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
                m = b.Build("lane");
                meshes["lane"] = m;
                return m;
            }
        }

        // ---------------------------------------------------------------- objects

        public static Transform Node(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        public static Transform Part(Transform parent, string name, Mesh mesh, Vector3 pos, Vector3 scale, Material mat, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = shadows;
            return go.transform;
        }

        public static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool shadows = true)
        {
            return Part(parent, name, Cube, pos, size, mat, shadows);
        }

        public static Transform Rot(this Transform t, float x, float y, float z)
        {
            t.localRotation = Quaternion.Euler(x, y, z);
            return t;
        }

        // Common lathe shapes (unit size: radius 0.5, height 1 unless noted).
        public static Mesh Cyl(int sides) { return Lathe("cyl" + sides, sides, 0f, 0f, 0.5f, 0f, 0.5f, 1f, 0f, 1f); }
        public static Mesh Cone(int sides) { return Lathe("cone" + sides, sides, 0f, 0f, 0.5f, 0f, 0f, 1f); }
        public static Mesh Ball(int sides)
        {
            return Lathe("ball" + sides, sides, 0f, -0.5f, 0.32f, -0.38f, 0.5f, -0.05f, 0.46f, 0.2f, 0.28f, 0.42f, 0f, 0.5f);
        }
    }

    // Temporarily recolours every renderer under a root (hit flash, hurt tint) with a property block.
    public class Tint
    {
        readonly Renderer[] rs;
        readonly Color[][] cols;                 // base colour of every material slot of every renderer (a model from a file has several)
        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        bool active;

        public Tint(Transform root)
        {
            rs = root.GetComponentsInChildren<Renderer>(true);
            cols = new Color[rs.Length][];
            for (int i = 0; i < rs.Length; i++)
            {
                Material[] ms = rs[i].sharedMaterials;
                cols[i] = new Color[Mathf.Max(1, ms.Length)];
                for (int j = 0; j < cols[i].Length; j++)
                {
                    Material m = j < ms.Length ? ms[j] : null;
                    cols[i][j] = m != null && m.HasProperty(Prims.BaseColor) ? m.GetColor(Prims.BaseColor) : Color.white;
                }
            }
        }

        public void Set(Color c, float k)
        {
            if (k <= 0.002f)
            {
                if (!active) return;
                active = false;
                for (int i = 0; i < rs.Length; i++)
                {
                    if (rs[i] == null) continue;
                    if (cols[i].Length > 1) for (int j = 0; j < cols[i].Length; j++) rs[i].SetPropertyBlock(null, j);
                    else rs[i].SetPropertyBlock(null);
                }
                return;
            }
            active = true;
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null) continue;
                for (int j = 0; j < cols[i].Length; j++)
                {
                    mpb.SetColor(Prims.BaseColor, Color.Lerp(cols[i][j], c, k));
                    if (cols[i].Length > 1) rs[i].SetPropertyBlock(mpb, j);
                    else rs[i].SetPropertyBlock(mpb);
                }
            }
        }
    }
}

using UnityEngine;

namespace MedievalKnight
{
    // The way home in the Phase 1 loop: appears where the Minotaur fell. A tall ring of light (about 3 m) with a
    // beam over it, a ground ring that marks the "stand here" radius and a ring of 24 segments that fills while the
    // knight stands inside. Built from Prims / Fx like everything else (no prefab).
    public class Portal : MonoBehaviour
    {
        public const float Radius = 2.0f, HoldSeconds = 1.5f;
        const int Segments = 24;
        static readonly Color Blue = new Color(0.435f, 0.784f, 1f), White = new Color(0.92f, 0.98f, 1f);

        Transform ringRoot, beam, groundRing;
        readonly Transform[] fill = new Transform[Segments];
        int shown;
        float age, nextSpark;

        public static Portal Create(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Portal");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, 0f, pos.z);
            var p = go.AddComponent<Portal>();
            p.Build();
            return p;
        }

        void Build()
        {
            Material blue = Prims.Glow(Blue), white = Prims.Glow(White);
            ringRoot = Prims.Node(transform, "Ring", new Vector3(0f, 1.7f, 0f));
            const int n = 20;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Transform seg = Prims.Box(ringRoot, "Seg", new Vector3(Mathf.Cos(a) * 1.5f, Mathf.Sin(a) * 1.5f, 0f), new Vector3(0.5f, 0.3f, 0.3f), i % 2 == 0 ? blue : white, false);
                seg.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
            }
            Prims.Part(ringRoot, "Core", Prims.Arc(0f, 1.25f, 0f, 360f), Vector3.zero, Vector3.one, Prims.Glow(new Color(0.55f, 0.85f, 1f, 1f)), false)
                .localRotation = Quaternion.Euler(90f, 0f, 0f);
            beam = Prims.Box(transform, "Beam", new Vector3(0f, 4.5f, 0f), new Vector3(0.18f, 9f, 0.18f), blue, false);
            groundRing = Fx.I.MakeDecal(Prims.Arc(Radius - 0.12f, Radius, 0f, 360f), "PortalGround");
            groundRing.gameObject.SetActive(true);
            for (int i = 0; i < Segments; i++)
            {
                float from = i * 360f / Segments, to = from + 360f / Segments - 1.5f;
                fill[i] = Fx.I.MakeDecal(Prims.Arc(Radius - 0.7f, Radius - 0.2f, from, to), "PortalFill" + i);
                fill[i].position = new Vector3(transform.position.x, 0.07f, transform.position.z);
            }
            groundRing.position = new Vector3(transform.position.x, 0.06f, transform.position.z);
            Fx.I.Ring(new Vector3(transform.position.x, 0.06f, transform.position.z), 0.5f, 6f, new Color(Blue.r, Blue.g, Blue.b, 0.9f), 0.8f);
            Fx.I.Burst(transform.position + Vector3.up, 30, Blue, 2f, 9f, Vector3.up, 0.8f, 1.0f, 3f, 0.14f);
        }

        // 0..1: how much of the 1.5 s the knight has stood inside. Each segment lights as it is reached.
        public void SetProgress(float f)
        {
            int want = Mathf.Clamp(Mathf.FloorToInt(f * Segments + 0.0001f), 0, Segments);
            if (f >= 1f) want = Segments;
            if (want == shown) return;
            for (int i = 0; i < Segments; i++)
            {
                bool on = i < want;
                if (fill[i] == null || fill[i].gameObject.activeSelf == on) continue;
                fill[i].gameObject.SetActive(on);
                if (on) Fx.I.SetDecal(fill[i], new Color(White.r, White.g, White.b, 0.95f));
            }
            shown = want;
        }

        public int SegmentsLit { get { return shown; } }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * 3.2f);
            beam.localScale = new Vector3(0.14f + 0.08f * pulse, 9f, 0.14f + 0.08f * pulse);
            if (groundRing != null) Fx.I.SetDecal(groundRing, new Color(Blue.r, Blue.g, Blue.b, 0.45f + 0.35f * pulse));
            if (age >= nextSpark)
            {
                nextSpark = age + 0.35f;
                float a = age * 5.3f;
                Fx.I.Burst(transform.position + new Vector3(Mathf.Sin(a) * 1.2f, 0.2f, Mathf.Cos(a * 1.7f) * 1.2f), 1, Blue, 0.6f, 1.6f, Vector3.up, 1.2f, 0.9f, -1f, 0.1f);
            }
        }

        void OnDestroy()
        {
            if (groundRing != null) Destroy(groundRing.gameObject);
            foreach (Transform t in fill) if (t != null) Destroy(t.gameObject);
        }
    }
}

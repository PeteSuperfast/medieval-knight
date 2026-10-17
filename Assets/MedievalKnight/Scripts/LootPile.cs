using UnityEngine;

namespace MedievalKnight
{
    // Phase 2: the pile of an old world - what the player carried when he fell. A low sack with a weapon hilt sticking
    // out, a gold ground ring and a tall thin gold beam (about 12 m, so it can be seen from far away; the portal's is
    // blue and shorter). Built from Prims / Fx like everything else (no prefab). RunLoop decides when the knight "touches"
    // it (TouchRadius) and opens the loot window; this class only looks like a pile.
    public class LootPile : MonoBehaviour
    {
        public const float TouchRadius = 1.5f, RearmRadius = 4f, BeamHeight = 12f;
        static readonly Color Gold = new Color(0.961f, 0.761f, 0.353f);

        Transform beam, groundRing;
        float age, nextSpark;

        public static LootPile Create(Transform parent, Vector3 pos)
        {
            var go = new GameObject("LootPile");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, Ground.Y(pos.x, pos.z), pos.z);
            var p = go.AddComponent<LootPile>();
            p.Build();
            return p;
        }

        void Build()
        {
            Material sack = Prims.Lit(new Color(0.46f, 0.33f, 0.2f), 0.1f), cord = Prims.Lit(new Color(0.24f, 0.17f, 0.1f), 0.1f);
            Material steel = Prims.Lit(new Color(0.78f, 0.8f, 0.86f), 0.55f, 0.8f), gold = Prims.Glow(Gold);
            Prims.Part(transform, "Sack", Prims.Ball(10), new Vector3(0f, 0.42f, 0f), new Vector3(1.2f, 0.84f, 1.2f), sack);
            Prims.Part(transform, "Knot", Prims.Ball(8), new Vector3(0f, 0.9f, 0f), new Vector3(0.42f, 0.34f, 0.42f), cord);
            Transform blade = Prims.Box(transform, "Blade", new Vector3(0.32f, 1.05f, 0.1f), new Vector3(0.09f, 1.1f, 0.03f), steel);
            blade.localRotation = Quaternion.Euler(0f, 0f, -24f);
            Transform guard = Prims.Box(transform, "Guard", new Vector3(0.12f, 0.52f, 0.1f), new Vector3(0.36f, 0.07f, 0.09f), gold, false);
            guard.localRotation = Quaternion.Euler(0f, 0f, -24f);
            beam = Prims.Box(transform, "Beam", new Vector3(0f, BeamHeight * 0.5f, 0f), new Vector3(0.4f, BeamHeight, 0.4f), gold, false);
            groundRing = Fx.I.MakeDecal(Prims.Arc(1.3f, 1.55f, 0f, 360f), "PileGround");
            groundRing.gameObject.SetActive(true);
            groundRing.position = new Vector3(transform.position.x, Ground.Top(transform.position.x, transform.position.z, 1.6f) + 0.06f, transform.position.z);
            Fx.I.Ring(new Vector3(transform.position.x, transform.position.y + 0.06f, transform.position.z), 0.5f, 4f, new Color(Gold.r, Gold.g, Gold.b, 0.9f), 0.7f);
        }

        public Vector3 Top { get { return transform.position + Vector3.up * 2.2f; } }

        // The last item was taken: a puff of smoke and sparkles, then the pile is gone.
        public void Vanish()
        {
            Vector3 p = transform.position;
            Fx.I.Burst(p + Vector3.up * 0.6f, 26, new Color(0.7f, 0.7f, 0.74f), 1.2f, 4f, Vector3.up, 1.0f, 0.9f, -1f, 0.2f);
            Fx.I.Burst(p + Vector3.up * 0.8f, 18, Gold, 1.5f, 6f, Vector3.up, 0.9f, 0.7f, 4f, 0.12f);
            Destroy(gameObject);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * 2.6f);
            float w = 0.3f + 0.15f * pulse;
            beam.localScale = new Vector3(w, BeamHeight, w);
            if (groundRing != null) Fx.I.SetDecal(groundRing, new Color(Gold.r, Gold.g, Gold.b, 0.4f + 0.35f * pulse));
            if (age >= nextSpark)
            {
                nextSpark = age + 0.45f;
                float a = age * 4.1f;
                Fx.I.Burst(transform.position + new Vector3(Mathf.Sin(a) * 0.9f, 0.4f, Mathf.Cos(a * 1.3f) * 0.9f), 1, Gold, 0.5f, 1.5f, Vector3.up, 1.2f, 0.9f, -1f, 0.1f);
            }
        }

        void OnDestroy()
        {
            if (groundRing != null) Destroy(groundRing.gameObject);
        }
    }
}

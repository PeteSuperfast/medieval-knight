using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // Hand-rolled 3D effects: little cube particles, and flat fading meshes on the ground plane
    // (slash arcs, shock rings, thrust streaks). No ParticleSystem / TrailRenderer.
    public class Fx : MonoBehaviour
    {
        public static Fx I;

        class Particle
        {
            public Transform t;
            public MeshRenderer mr;
            public Vector3 vel, spin;
            public float life, maxLife, gravity, size;
        }

        class Flat
        {
            public Transform t;
            public MeshFilter mf;
            public MeshRenderer mr;
            public float life, maxLife, scale0, scale1, spin, alpha;
            public Color color;
            public Transform follow;
            public Vector3 followOffset, baseScale;
        }

        readonly List<Particle> particles = new List<Particle>();
        readonly Stack<Particle> particlePool = new Stack<Particle>();
        readonly List<Flat> flats = new List<Flat>();
        readonly Stack<Flat> flatPool = new Stack<Flat>();
        MaterialPropertyBlock mpb;

        void Awake()
        {
            I = this;
            mpb = new MaterialPropertyBlock();
        }

        // ---------------------------------------------------------------- particles

        // dir = main direction, spread 0 = straight along dir, 1 = any direction.
        public void Burst(Vector3 pos, int count, Color color, float speedMin, float speedMax,
            Vector3 dir, float spread, float life, float gravity, float size)
        {
            Material mat = Prims.Glow(color);
            if (dir.sqrMagnitude < 0.0001f) { dir = Vector3.up; spread = 1f; }
            dir.Normalize();
            for (int i = 0; i < count; i++)
            {
                Particle p = particlePool.Count > 0 ? particlePool.Pop() : NewParticle();
                Vector3 d = Vector3.Slerp(dir, Random.onUnitSphere, spread * Random.value).normalized;
                p.vel = d * Random.Range(speedMin, speedMax);
                p.maxLife = p.life = life * Random.Range(0.6f, 1.15f);
                p.gravity = gravity;
                p.size = size * Random.Range(0.7f, 1.3f);
                p.spin = Random.onUnitSphere * Random.Range(200f, 700f);
                p.mr.sharedMaterial = mat;
                p.t.position = pos + Random.insideUnitSphere * 0.15f;
                p.t.rotation = Random.rotation;
                p.t.localScale = Vector3.one * p.size;
                p.t.gameObject.SetActive(true);
                particles.Add(p);
            }
        }

        // Dust puff on the ground.
        public void Dust(Vector3 at, int n, float power = 1f)
        {
            Burst(new Vector3(at.x, 0.12f, at.z), n, new Color(0.78f, 0.72f, 0.56f), 1f * power, 3.2f * power, Vector3.up, 0.85f, 0.4f, 3f, 0.16f);
        }

        Particle NewParticle()
        {
            var p = new Particle();
            p.t = Prims.Box(transform, "Particle", Vector3.zero, Vector3.one, Prims.Glow(Color.white), false);
            p.mr = p.t.GetComponent<MeshRenderer>();
            return p;
        }

        // ---------------------------------------------------------------- flat fading meshes

        // A slash arc on the XZ plane. yaw = world heading of "0 degrees" of the mesh.
        // The mesh fades from its fromDeg end (tail) to its toDeg end (leading edge).
        public void Arc(Vector3 pos, float yaw, float r0, float r1, float fromDeg, float toDeg, Color color, float life,
            float grow = 0.15f, float spin = 0f, Transform follow = null)
        {
            Spawn(Prims.Arc(r0, r1, fromDeg, toDeg), Prims.Gradient, pos, yaw, 1f, 1f + grow, color, life, spin, follow);
        }

        // Straight streak on the XZ plane starting at pos and running "length" along yaw (thrusts, dashes).
        public void Streak(Vector3 pos, float yaw, float length, float width, Color color, float life)
        {
            Spawn(Prims.Lane, Prims.Gradient, pos, yaw, 1f, 1f, color, life, 0f, null);
            Flat f = flats[flats.Count - 1];
            f.baseScale = new Vector3(width, 1f, length);
            f.t.localScale = f.baseScale;
        }

        // Expanding shock ring on the ground.
        public void Ring(Vector3 pos, float radiusFrom, float radiusTo, Color color, float life)
        {
            Spawn(Prims.Arc(0.8f, 1f, 0f, 360f), Prims.White, pos, 0f, radiusFrom, radiusTo, color, life, 0f, null);
        }

        // Filled disc that fades out (impact flash).
        public void Disc(Vector3 pos, float radiusFrom, float radiusTo, Color color, float life)
        {
            Spawn(Prims.Arc(0f, 1f, 0f, 360f), Prims.White, pos, 0f, radiusFrom, radiusTo, color, life, 0f, null);
        }

        void Spawn(Mesh mesh, Texture tex, Vector3 pos, float yaw, float s0, float s1, Color color, float life, float spin, Transform follow)
        {
            Flat f = flatPool.Count > 0 ? flatPool.Pop() : NewFlat();
            f.mf.sharedMesh = mesh;
            f.color = color;
            f.alpha = color.a;
            f.maxLife = f.life = life;
            f.scale0 = s0; f.scale1 = s1;
            f.spin = spin;
            f.follow = follow;
            f.followOffset = follow != null ? pos - follow.position : Vector3.zero;
            f.t.position = pos;
            f.t.rotation = Quaternion.Euler(0f, yaw, 0f);
            f.baseScale = Vector3.one;
            f.t.localScale = new Vector3(s0, 1f, s0);
            mpb.SetTexture(Prims.BaseMap, tex);
            mpb.SetColor(Prims.BaseColor, color);
            f.mr.SetPropertyBlock(mpb);
            f.t.gameObject.SetActive(true);
            flats.Add(f);
        }

        Flat NewFlat()
        {
            var f = new Flat();
            f.t = Prims.Part(transform, "Flat", null, Vector3.zero, Vector3.one, Prims.FxMaterial, false);
            f.mf = f.t.GetComponent<MeshFilter>();
            f.mr = f.t.GetComponent<MeshRenderer>();
            return f;
        }

        // A ground marker owned by someone else (enemy attack telegraphs). Colour it with SetDecal.
        public Transform MakeDecal(Mesh mesh, string name)
        {
            Transform t = Prims.Part(transform, name, mesh, Vector3.zero, Vector3.one, Prims.FxMaterial, false);
            t.gameObject.SetActive(false);
            return t;
        }

        public void SetDecal(Transform decal, Color color)
        {
            mpb.SetTexture(Prims.BaseMap, Prims.White);
            mpb.SetColor(Prims.BaseColor, color);
            decal.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);
        }

        public void Clear()
        {
            for (int i = particles.Count - 1; i >= 0; i--) { particles[i].t.gameObject.SetActive(false); particlePool.Push(particles[i]); }
            particles.Clear();
            for (int i = flats.Count - 1; i >= 0; i--) { flats[i].t.gameObject.SetActive(false); flatPool.Push(flats[i]); }
            flats.Clear();
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.deltaTime;

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.t.gameObject.SetActive(false);
                    particlePool.Push(p);
                    particles.RemoveAt(i);
                    continue;
                }
                p.vel.y -= p.gravity * dt;
                p.vel *= 1f - Mathf.Clamp01(1.5f * dt);
                Vector3 pos = p.t.position + p.vel * dt;
                if (pos.y < 0.06f && p.gravity > 0f)   // bounce on the floor
                {
                    pos.y = 0.06f;
                    p.vel.y = -p.vel.y * 0.35f;
                    p.vel.x *= 0.6f; p.vel.z *= 0.6f;
                }
                p.t.position = pos;
                p.t.Rotate(p.spin * dt, Space.Self);
                float k = p.life / p.maxLife;
                p.t.localScale = Vector3.one * p.size * Mathf.Clamp01(k * 1.6f);
            }

            for (int i = flats.Count - 1; i >= 0; i--)
            {
                Flat f = flats[i];
                f.life -= dt;
                if (f.life <= 0f)
                {
                    f.t.gameObject.SetActive(false);
                    flatPool.Push(f);
                    flats.RemoveAt(i);
                    continue;
                }
                float k = 1f - f.life / f.maxLife;   // 0 -> 1
                Color c = f.color;
                c.a = f.alpha * (1f - k * k);
                f.mr.GetPropertyBlock(mpb);
                mpb.SetColor(Prims.BaseColor, c);
                f.mr.SetPropertyBlock(mpb);
                float s = Mathf.Lerp(f.scale0, f.scale1, 1f - (1f - k) * (1f - k));
                f.t.localScale = new Vector3(f.baseScale.x * s, 1f, f.baseScale.z * s);
                if (f.spin != 0f) f.t.Rotate(0f, f.spin * dt, 0f, Space.World);
                if (f.follow != null) f.t.position = f.follow.position + f.followOffset;
            }
        }
    }
}

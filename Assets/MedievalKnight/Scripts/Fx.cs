using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // Hand-rolled effects: square "pixel" particles and fading sprite ghosts (slash arcs, thrust
    // streaks, sword after-images). No ParticleSystem / TrailRenderer, so no extra materials needed.
    public class Fx : MonoBehaviour
    {
        public static Fx I;

        class Particle
        {
            public SpriteRenderer sr;
            public Vector3 vel;
            public float life, maxLife, gravity, size, spin;
            public Color color;
        }

        class Ghost
        {
            public SpriteRenderer sr;
            public float life, maxLife, grow, spin, alpha;
            public Vector3 baseScale;
            public Color color;
        }

        readonly List<Particle> particles = new List<Particle>();
        readonly Stack<Particle> particlePool = new Stack<Particle>();
        readonly List<Ghost> ghosts = new List<Ghost>();
        readonly Stack<Ghost> ghostPool = new Stack<Ghost>();

        void Awake() { I = this; }

        // angleDeg: 0 = right, 90 = up. spreadDeg = total cone width.
        public void Burst(Vector3 pos, int count, Color color, float speedMin, float speedMax,
            float angleDeg, float spreadDeg, float life, float gravity, float size)
        {
            for (int i = 0; i < count; i++)
            {
                Particle p = particlePool.Count > 0 ? particlePool.Pop() : NewParticle();
                float a = (angleDeg + Random.Range(-spreadDeg, spreadDeg) * 0.5f) * Mathf.Deg2Rad;
                float sp = Random.Range(speedMin, speedMax);
                p.vel = new Vector3(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp, 0f);
                p.maxLife = p.life = life * Random.Range(0.6f, 1.15f);
                p.gravity = gravity;
                p.size = size * Random.Range(0.7f, 1.3f);
                p.spin = Random.Range(-540f, 540f);
                p.color = color;
                p.sr.color = color;
                p.sr.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.12f);
                p.sr.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 90f));
                p.sr.transform.localScale = Vector3.one * p.size;
                p.sr.gameObject.SetActive(true);
                particles.Add(p);
            }
        }

        Particle NewParticle()
        {
            var p = new Particle();
            p.sr = Art.NewSprite("Particle", Art.Pixel(), transform, 12);
            return p;
        }

        // A sprite that fades out. scale.x / scale.y may be negative to mirror it.
        // grow = extra scale gained over its life (0.25 = +25%), spin = degrees per second.
        public void SpawnSprite(Sprite sprite, Vector3 pos, float rotZ, Vector3 scale, Color color,
            float life, float grow, float spin, int order)
        {
            if (sprite == null) return;
            Ghost g = ghostPool.Count > 0 ? ghostPool.Pop() : NewGhost();
            g.sr.sprite = sprite;
            g.sr.sortingOrder = order;
            g.sr.color = color;
            g.color = color;
            g.alpha = color.a;
            g.maxLife = g.life = life;
            g.grow = grow;
            g.spin = spin;
            g.baseScale = scale;
            g.sr.transform.position = pos;
            g.sr.transform.rotation = Quaternion.Euler(0, 0, rotZ);
            g.sr.transform.localScale = scale;
            g.sr.gameObject.SetActive(true);
            ghosts.Add(g);
        }

        Ghost NewGhost()
        {
            var g = new Ghost();
            g.sr = Art.NewSprite("Ghost", null, transform, 10);
            return g;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.sr.gameObject.SetActive(false);
                    particlePool.Push(p);
                    particles.RemoveAt(i);
                    continue;
                }
                p.vel.y -= p.gravity * dt;
                p.vel *= 1f - Mathf.Clamp01(1.5f * dt);
                Transform t = p.sr.transform;
                Vector3 pos = t.position + p.vel * dt;
                if (pos.y < 0.05f && p.gravity > 0f)   // bounce on the floor
                {
                    pos.y = 0.05f;
                    p.vel.y = -p.vel.y * 0.35f;
                    p.vel.x *= 0.6f;
                }
                t.position = pos;
                t.Rotate(0, 0, p.spin * dt);
                float k = p.life / p.maxLife;
                Color c = p.color;
                c.a *= Mathf.Clamp01(k * 2.5f);
                p.sr.color = c;
                t.localScale = Vector3.one * p.size * (0.5f + 0.5f * k);
            }

            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                Ghost g = ghosts[i];
                g.life -= dt;
                if (g.life <= 0f)
                {
                    g.sr.gameObject.SetActive(false);
                    ghostPool.Push(g);
                    ghosts.RemoveAt(i);
                    continue;
                }
                float k = 1f - g.life / g.maxLife;   // 0 -> 1
                Color c = g.color;
                c.a = g.alpha * (1f - k);
                g.sr.color = c;
                g.sr.transform.localScale = g.baseScale * (1f + g.grow * k);
                if (g.spin != 0f) g.sr.transform.Rotate(0, 0, g.spin * dt);
            }
        }
    }
}

using UnityEngine;

namespace MedievalKnight
{
    // Hopping blob. Squashes before a hop, stretches in the air, squashes again on landing.
    // kind 0 = green (small), 1 = blue, 2 = red (big). Hurts the knight when it lands on him.
    public class Slime : Enemy
    {
        public int kind;
        public float damage;
        public bool Frozen;                        // test hook (SelfTest): no AI, no hop

        static readonly Color[] Colors =
        {
            new Color(0.35f, 0.85f, 0.3f), new Color(0.25f, 0.6f, 0.95f), new Color(0.92f, 0.28f, 0.25f)
        };
        static readonly string[] Names = { "Green Slime", "Blue Slime", "Red Slime" };
        static readonly float[] BaseHp = { 30f, 70f, 130f };
        static readonly float[] BaseArmor = { 0f, 0f, 30f };
        static readonly float[] BaseDamage = { 8f, 13f, 19f };
        static readonly float[] Exp = { 10f, 24f, 48f };
        static readonly int[] Souls = { 1, 3, 6 };
        static readonly float[] Sizes = { 0.95f, 1.3f, 1.7f };

        public const float ChaseRange = 30f;       // slimes notice a knight from this far (the screen shows about 26 x 16 m)

        public static float ExpFor(int kind) { return Exp[kind]; }
        public static int SoulsFor(int kind) { return Souls[kind]; }

        enum St { Wait, Squash, Hop, Land }
        St state = St.Wait;
        float stateT, size, spawnT, deathT, hopHeight;
        Vector3 hopFrom, hopTo, squash = Vector3.one;
        bool hitDone;
        Color color;
        Transform warnDecal;                      // red slime only: the ring that shrinks while it squashes (phase 7)
        bool warned;

        public static Slime Spawn(int kind, Vector3 pos, Transform parent)
        {
            var go = new GameObject(Names[kind]);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, Ground.Y(pos.x, pos.z), pos.z);
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var s = go.AddComponent<Slime>();
            s.Setup(kind);
            return s;
        }

        void Setup(int k)
        {
            kind = k;
            label = Names[k];
            size = Sizes[k];
            color = Colors[k];
            radius = 0.5f * size; height = 0.75f * size; mass = size * size;
            expReward = Exp[k] * Game.I.EnemyExpScale;           // scaled by the party level at the moment it appears
            soulReward = Souls[k];
            damage = BaseDamage[k] * Game.I.EnemyDamageScale;
            InitStats(BaseHp[k] * Game.I.EnemyHpScale, BaseArmor[k] * Game.I.EnemyArmorScale);

            rig = Prims.Node(transform, "Rig", Vector3.zero);
            Mesh dome = Prims.Lathe("slime", 9, 0f, 0f, 0.5f, 0f, 0.58f, 0.2f, 0.5f, 0.45f, 0.3f, 0.66f, 0f, 0.75f);
            Prims.Part(rig, "Body", dome, Vector3.zero, Vector3.one, Prims.Lit(color, 0.75f));
            Prims.Part(rig, "Core", Prims.Ball(6), new Vector3(0f, 0.2f, -0.05f), new Vector3(0.5f, 0.3f, 0.5f), Prims.Lit(color * 0.7f, 0.75f));
            Material eyeW = Prims.Glow(Color.white), eyeB = Prims.Glow(new Color(0.08f, 0.08f, 0.1f));
            for (int s = -1; s <= 1; s += 2)
            {
                Prims.Box(rig, "Eye", new Vector3(0.19f * s, 0.42f, 0.42f), new Vector3(0.17f, 0.2f, 0.1f), eyeW, false).Rot(-18f, 0f, 0f);
                Prims.Box(rig, "Pupil", new Vector3(0.19f * s, 0.41f, 0.47f), new Vector3(0.09f, 0.11f, 0.06f), eyeB, false).Rot(-18f, 0f, 0f);
            }
            Prims.Box(rig, "Shine", new Vector3(-0.2f, 0.62f, 0.05f), new Vector3(0.14f, 0.04f, 0.1f), Prims.Glow(Color.Lerp(color, Color.white, 0.7f)), false).Rot(0f, 0f, 25f);

            tint = new Tint(rig);
            BuildBar(0.9f * size);
            stateT = Random.Range(0.3f, 0.9f);
            rig.localScale = Vector3.zero;
        }

        // Moves the slime somewhere else and cancels the hop in progress (a hop would drag it back).
        public void Warp(Vector3 p)
        {
            transform.position = new Vector3(p.x, Ground.Y(p.x, p.z), p.z);
            knock = Vector3.zero;
            state = St.Wait;
            stateT = 0.4f;
        }

        protected override Color SparkColor(bool flesh) { return flesh ? Color.Lerp(color, Color.white, 0.25f) : new Color(0.7f, 0.85f, 1f); }

        void OnDestroy() { if (warnDecal != null) Destroy(warnDecal.gameObject); }
        public override bool HeavyWarn { get { return warned; } }

        // Phase 7: the red slime's heavy blow is announced: a red ring shrinking to the body, a red glow and one "!" when the squash starts.
        // Every machine shows it (the puppet's state comes from the host).
        void Warn(bool on, float k)
        {
            if (kind != 2) return;
            if (!on)
            {
                warned = false; glowAmt = 0f;
                if (warnDecal != null && warnDecal.gameObject.activeSelf) warnDecal.gameObject.SetActive(false);
                return;
            }
            if (warnDecal == null) warnDecal = Fx.I.MakeDecal(Prims.Arc(0f, 1f, 0f, 360f), "SlimeWarnMarker");
            if (!warned)
            {
                warned = true;
                Game.I.hud.Popup(Center + Vector3.up * height * 0.8f, "!", new Color(0.78f, 0.28f, 0.23f), 28);
            }
            float r = Mathf.Lerp(2.2f, 0.9f, Mathf.Clamp01(k));
            warnDecal.gameObject.SetActive(true);
            warnDecal.position = new Vector3(transform.position.x, Ground.Top(transform.position.x, transform.position.z, r) + 0.04f, transform.position.z);
            warnDecal.localScale = new Vector3(r, 1f, r);
            float blink = Mathf.Sin(Time.time * Mathf.PI * 8f) > 0f ? 1f : 0.7f;      // 4 Hz
            Fx.I.SetDecal(warnDecal, new Color(0.78f, 0.28f, 0.23f, (0.35f + 0.45f * Mathf.Clamp01(k)) * blink));
            glowColor = new Color(1f, 0.35f, 0.24f);
            glowAmt = 0.15f + 0.35f * Mathf.Clamp01(k);
        }

        protected override void OnDeath(Vector3 away)
        {
            Warn(false, 0f);
            Fx.I.Burst(Center, 18 + kind * 8, color, 2f, 7f, away + Vector3.up, 0.9f, 0.6f, 10f, 0.2f * size);
            Fx.I.Disc(new Vector3(transform.position.x, 0.05f, transform.position.z), radius, radius * 2.4f, new Color(color.r, color.g, color.b, 0.55f), 0.5f);
        }

        // ---------------------------------------------------------------- online

        public override int NetType { get { return kind; } }

        protected override void WriteExtra(NetW w) { w.U8((int)state); }

        protected override void ReadExtra(NetR r)
        {
            St ns = (St)r.U8();
            if (ns == state) return;
            if (ns == St.Land) Fx.I.Dust(transform.position, 4, 0.7f * size);
            state = ns;
            stateT = 0f;
        }

        // A client's slime: the host moves it; the squash, stretch and hop arc are played from its state.
        void PuppetPose(float dt)
        {
            Vector3 target = Vector3.one;
            float y = 0f;
            if (Stunned)
            {
                float w = Mathf.Sin(Time.time * 30f) * 0.08f;
                target = new Vector3(1.15f + w, 0.75f, 1.15f - w);
            }
            else if (state == St.Wait)
            {
                float br = Mathf.Sin(Time.time * 4f + size * 7f) * 0.05f;
                target = new Vector3(1f + br, 1f - br, 1f + br);
            }
            else if (state == St.Squash) { stateT += dt / 0.28f; target = new Vector3(1.3f, 0.6f, 1.3f); }
            else if (state == St.Hop)
            {
                stateT += dt / 0.42f;
                y = Mathf.Sin(Mathf.Clamp01(stateT) * Mathf.PI) * (0.9f + 0.35f * kind);
                target = new Vector3(0.8f, 1.3f, 0.8f);
            }
            else target = new Vector3(1.35f, 0.55f, 1.35f);

            squash = Vector3.Lerp(squash, target, 1f - Mathf.Exp(-22f * dt));
            Warn(state == St.Squash && !Stunned, stateT);
            rig.localScale = squash * size;
            rig.localPosition = new Vector3(0f, y, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Dead)
            {
                deathT += dt / 0.25f;
                rig.localScale = new Vector3(1f + deathT * 0.6f, Mathf.Max(0f, 1f - deathT), 1f + deathT * 0.6f) * size;
                if (deathT >= 1f) Destroy(gameObject);
                return;
            }
            BaseTick(dt);
            if (puppet) PuppetMove();
            Knight k = Target();
            if (k == null && !puppet) return;

            if (spawnT < 1f)
            {
                spawnT += dt / 0.4f;
                float pop = Mathf.Sin(Mathf.Clamp01(spawnT) * Mathf.PI * 0.5f);
                rig.localScale = Vector3.one * size * pop * (1f + 0.25f * Mathf.Sin(Mathf.Clamp01(spawnT) * Mathf.PI));
                return;
            }
            if (puppet) { PuppetPose(dt); return; }
            if (Frozen) { rig.localScale = Vector3.one * size; return; }           // test hook: stands still, harmless (it only takes knock-back / stun)

            Vector3 target = Vector3.one;
            float y = 0f;
            if (Stunned)
            {
                if (state == St.Hop) { state = St.Wait; stateT = 0.3f; }
                float w = Mathf.Sin(Time.time * 30f) * 0.08f;
                target = new Vector3(1.15f + w, 0.75f, 1.15f - w);
            }
            else
            {
                switch (state)
                {
                    case St.Wait:
                        stateT -= dt;
                        float br = Mathf.Sin(Time.time * 4f + size * 7f) * 0.05f;
                        target = new Vector3(1f + br, 1f - br, 1f + br);
                        if (stateT <= 0f) { state = St.Squash; stateT = 0f; }
                        break;

                    case St.Squash:
                        stateT += dt / 0.28f;
                        target = new Vector3(1.3f, 0.6f, 1.3f);
                        Vector3 toK = k.Pos - transform.position; toK.y = 0f;
                        bool chase = !k.Dead && toK.magnitude < ChaseRange && !MapLayout.InTown(k.Pos.x, k.Pos.z);   // the town is safe
                        if (chase) TurnTowards(toK, 500f, dt);
                        if (stateT >= 1f)
                        {
                            Vector3 dir; float len;
                            if (chase)
                            {
                                dir = toK.normalized;
                                len = Mathf.Min(2.3f + kind * 0.35f, Mathf.Max(0.4f, toK.magnitude - 0.3f));
                            }
                            else
                            {
                                dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                                len = 1.2f;
                                transform.rotation = Quaternion.LookRotation(dir);
                            }
                            hopFrom = transform.position;
                            hopTo = hopFrom + dir * len;
                            hopHeight = 0.9f + 0.35f * kind;
                            hitDone = false;
                            state = St.Hop; stateT = 0f;
                        }
                        break;

                    case St.Hop:
                        stateT += dt / 0.42f;
                        float t = Mathf.Clamp01(stateT);
                        Vector3 p = Vector3.Lerp(hopFrom, hopTo, t);
                        y = Mathf.Sin(t * Mathf.PI) * hopHeight;
                        transform.position = new Vector3(p.x, Ground.Y(p.x, p.z), p.z);
                        hopFrom += knock * dt; hopTo += knock * dt;
                        target = new Vector3(0.8f, 1.3f, 0.8f);
                        if (!hitDone && t > 0.55f)
                        {
                            // lands on whoever is underneath (one knight per hop)
                            var ps = Game.I.players;
                            for (int i = 0; i < ps.Count && !hitDone; i++)
                            {
                                Knight kn = ps[i];
                                if (kn.Dead || kn.Airborne || FlatDist(transform.position, kn.Pos) >= radius + 0.6f) continue;
                                hitDone = true;
                                Game.I.DamageKnight(kn, damage, transform.position, label, 6f, this, kind == 2);   // the red slime is a heavy blow (no clash / parry)
                            }
                        }
                        if (stateT >= 1f)
                        {
                            state = St.Land; stateT = 0f;
                            Fx.I.Dust(transform.position, 4, 0.7f * size);
                        }
                        break;

                    case St.Land:
                        stateT += dt / 0.14f;
                        target = new Vector3(1.35f, 0.55f, 1.35f);
                        if (stateT >= 1f) { state = St.Wait; stateT = Random.Range(0.25f, 0.65f) - 0.05f * kind; }
                        break;
                }
            }

            Warn(state == St.Squash && !Stunned, stateT);
            squash = Vector3.Lerp(squash, target, 1f - Mathf.Exp(-22f * dt));
            rig.localScale = squash * size;
            rig.localPosition = new Vector3(0f, y, 0f);
        }
    }
}

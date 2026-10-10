using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // The straw training dummy: harmless, gives no EXP, wobbles when hit, rebuilds itself after breaking.
    public class Dummy : Enemy
    {
        public const float MaxHp = 200f, MaxArmor = 60f;

        public bool Broken { get { return Dead; } }
        public float Dps { get { return recent / 5f; } }

        readonly Queue<Vector2> window = new Queue<Vector2>();   // (time, damage)
        float recent, rebuildAt, lastHitTime = -10f;
        Vector2 tilt, tiltVel;                                   // spring wobble (x = around X, y = around Z)
        float collapse;

        void Awake()
        {
            label = "Straw Dummy";
            radius = 0.55f; height = 2.1f; solid = true; expReward = 0f; mass = 1000f;
            knockResist = 0f; stunResist = 0f;
            InitStats(MaxHp, MaxArmor);

            rig = Prims.Node(transform, "Rig", Vector3.zero);
            Material wood = Prims.Lit(new Color(0.42f, 0.27f, 0.14f));
            Material darkWood = Prims.Lit(new Color(0.3f, 0.19f, 0.1f));
            Material straw = Prims.Lit(new Color(0.9f, 0.74f, 0.3f));
            Material strawDark = Prims.Lit(new Color(0.75f, 0.58f, 0.2f));
            Material rope = Prims.Lit(new Color(0.35f, 0.25f, 0.15f));
            Material red = Prims.Lit(new Color(0.8f, 0.15f, 0.12f));
            Material white = Prims.Lit(new Color(0.92f, 0.9f, 0.85f));
            Material iron = Prims.Lit(new Color(0.5f, 0.52f, 0.56f), 0.5f, 0.6f);

            // feet stay on the ground, everything else wobbles
            Prims.Box(transform, "FootA", new Vector3(0f, 0.07f, 0f), new Vector3(1.3f, 0.14f, 0.2f), darkWood);
            Prims.Box(transform, "FootB", new Vector3(0f, 0.07f, 0f), new Vector3(0.2f, 0.14f, 1.3f), darkWood);

            Prims.Part(rig, "Post", Prims.Cyl(6), Vector3.zero, new Vector3(0.22f, 2.0f, 0.22f), wood);
            Mesh barrel = Prims.Lathe("dummyBody", 8, 0f, 0f, 0.3f, 0f, 0.46f, 0.3f, 0.48f, 0.65f, 0.36f, 0.95f, 0f, 0.95f);
            Prims.Part(rig, "Body", barrel, new Vector3(0f, 0.6f, 0f), Vector3.one, straw);
            Prims.Part(rig, "Rope1", Prims.Cyl(8), new Vector3(0f, 0.86f, 0f), new Vector3(0.96f, 0.07f, 0.96f), rope);
            Prims.Part(rig, "Rope2", Prims.Cyl(8), new Vector3(0f, 1.3f, 0f), new Vector3(0.92f, 0.07f, 0.92f), rope);
            Prims.Box(rig, "Arms", new Vector3(0f, 1.42f, 0f), new Vector3(1.7f, 0.13f, 0.13f), wood);
            Prims.Part(rig, "HandL", Prims.Ball(6), new Vector3(-0.85f, 1.42f, 0f), new Vector3(0.36f, 0.3f, 0.36f), strawDark);
            Prims.Part(rig, "HandR", Prims.Ball(6), new Vector3(0.85f, 1.42f, 0f), new Vector3(0.36f, 0.3f, 0.36f), strawDark);
            Prims.Part(rig, "Head", Prims.Ball(7), new Vector3(0f, 1.87f, 0f), new Vector3(0.62f, 0.62f, 0.62f), straw);
            Prims.Part(rig, "Helm", Prims.Lathe("dummyHelm", 7, 0.36f, 0f, 0.34f, 0.16f, 0.2f, 0.3f, 0f, 0.34f), new Vector3(0f, 1.95f, 0f), Vector3.one, iron);
            // painted target on the chest (dummy faces its local +Z)
            Prims.Part(rig, "TargetW", Prims.Cyl(10), new Vector3(0f, 1.05f, 0.44f), new Vector3(0.46f, 0.04f, 0.46f), white).Rot(90f, 0f, 0f);
            Prims.Part(rig, "TargetR", Prims.Cyl(10), new Vector3(0f, 1.05f, 0.47f), new Vector3(0.24f, 0.04f, 0.24f), red).Rot(90f, 0f, 0f);
            // little wooden shield on one arm
            Prims.Part(rig, "Shield", Prims.Cyl(8), new Vector3(-0.85f, 1.3f, 0.18f), new Vector3(0.62f, 0.07f, 0.62f), darkWood).Rot(90f, 0f, 0f);
            Prims.Part(rig, "Boss", Prims.Ball(6), new Vector3(-0.85f, 1.3f, 0.25f), new Vector3(0.18f, 0.18f, 0.18f), iron);

            tint = new Tint(rig);
            BuildBar(1.4f);
        }

        protected override Color SparkColor(bool flesh) { return flesh ? new Color(0.95f, 0.8f, 0.35f) : new Color(0.7f, 0.85f, 1f); }

        protected override void OnHit(Vector3 from, float damage)
        {
            lastHitTime = Time.time;
            window.Enqueue(new Vector2(Time.time, damage));
            recent += damage;
            Vector3 away = transform.InverseTransformDirection((transform.position - from).normalized);
            float power = Mathf.Clamp(damage, 6f, 60f) * 9f;
            tiltVel += new Vector2(away.z, -away.x) * power;
        }

        protected override void OnDeath(Vector3 away)
        {
            rebuildAt = Time.time + 2.5f;
            Fx.I.Burst(Center, 26, new Color(0.9f, 0.74f, 0.3f), 2f, 7f, Vector3.up, 0.9f, 0.7f, 9f, 0.16f);
            Game.I.hud.Popup(Center + Vector3.up, "BROKEN", new Color(1f, 0.85f, 0.4f), 26);
        }

        public override int NetType { get { return 4; } }

        // A client's dummy breaks and rebuilds when the host's does.
        protected override void OnNetDead(bool dead)
        {
            if (dead == Dead) return;
            Dead = dead;
            ShowBar(!dead);
            if (!dead) Fx.I.Dust(transform.position, 10);
        }

        public void ResetAll()
        {
            Dead = false;
            InitStats(MaxHp, MaxArmor);
            hits = 0; totalDamage = 0f; lastDamage = 0f; lastHit = "-"; lastCrit = false;
            window.Clear(); recent = 0f;
            tilt = tiltVel = Vector2.zero; collapse = 0f;
            ShowBar(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            BaseTick(dt);

            while (window.Count > 0 && Time.time - window.Peek().x > 5f) recent -= window.Dequeue().y;
            if (window.Count == 0) recent = 0f;

            if (Dead)
            {
                collapse = Mathf.MoveTowards(collapse, 1f, dt * 5f);
                if (!puppet && Time.time >= rebuildAt)
                {
                    Dead = false;
                    InitStats(MaxHp, MaxArmor);
                    ShowBar(true);
                    Fx.I.Dust(transform.position, 10);
                }
            }
            else
            {
                collapse = Mathf.MoveTowards(collapse, 0f, dt * 4f);
                if (!puppet && Time.time - lastHitTime > 4f)   // patch itself up when left alone
                {
                    armor = Mathf.Min(maxArmor, armor + 30f * dt);
                    hp = Mathf.Min(maxHp, hp + 40f * dt);
                }
            }

            // damped spring back to upright
            tiltVel += (-tilt * 120f - tiltVel * 7f) * dt;
            tilt += tiltVel * dt;
            tilt = Vector2.ClampMagnitude(tilt, 28f);
            rig.localRotation = Quaternion.Euler(tilt.x + collapse * 80f, 0f, tilt.y);
            rig.localScale = new Vector3(1f, 1f - collapse * 0.25f, 1f);
        }
    }
}

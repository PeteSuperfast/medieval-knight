using UnityEngine;

namespace MedievalKnight
{
    // Boss-like enemy: a big horned bull-man with a labrys (Greek double-headed axe).
    // Three telegraphed attacks, each shown as a red marker on the ground before it lands:
    //   Sweep  - wide horizontal axe swing in front of him
    //   Smash  - overhead chop on a circle in front of him
    //   Charge - lowers the horns and runs in a straight lane
    public class Minotaur : Enemy
    {
        public const float BaseHp = 520f, BaseArmor = 120f, ExpReward = 250f, AggroRange = 12.5f;
        const float SweepRange = 4.7f, SweepHalfArc = 110f, SmashRadius = 2.4f, LaneLength = 15f, LaneWidth = 2.6f;
        const float SweepDamage = 26f, SmashDamage = 38f, ChargeDamage = 30f;

        public enum S { Idle, Chase, SweepTele, SweepStrike, SmashTele, SmashStrike, ChargeTele, Charging, Recover, Stun }
        public S State { get; private set; }

        Transform body, head, legL, legR, armL, axePivot;
        Transform decalSector, decalDisc, decalLane;
        float t, atkCd = 0.6f, chargeCd = 3f, walkPhase, axeYaw = 25f, axePitch = -40f, lean = 12f, deathT, dmgScale;
        Vector3 chargeDir, smashPoint;
        bool hitDone, roared;
        int chargeMask;                     // players (bit = pid) already run over by the current charge

        public static Minotaur Spawn(Vector3 pos, Transform parent)
        {
            var go = new GameObject("Minotaur");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, 0f, pos.z);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            return go.AddComponent<Minotaur>();
        }

        void Awake()
        {
            label = "Minotaur";
            radius = 1.15f; height = 3.3f; mass = 40f; solid = true;
            knockResist = 0.12f; stunResist = 0.5f;
            expReward = ExpReward;
            dmgScale = Game.I.EnemyDamageScale;
            InitStats(BaseHp * Game.I.EnemyHpScale, BaseArmor * Game.I.EnemyHpScale);
            Build();
            tint = new Tint(rig);
            BuildBar(2.4f);

            decalSector = Fx.I.MakeDecal(Prims.Arc(0.9f, SweepRange, -SweepHalfArc, SweepHalfArc), "SweepMarker");
            decalDisc = Fx.I.MakeDecal(Prims.Arc(0f, 1f, 0f, 360f), "SmashMarker");
            decalLane = Fx.I.MakeDecal(Prims.Lane, "ChargeMarker");
        }

        void OnDestroy()
        {
            if (decalSector != null) Destroy(decalSector.gameObject);
            if (decalDisc != null) Destroy(decalDisc.gameObject);
            if (decalLane != null) Destroy(decalLane.gameObject);
        }

        // ---------------------------------------------------------------- model

        void Build()
        {
            Material hide = Prims.Lit(new Color(0.45f, 0.26f, 0.15f));
            Material hideDark = Prims.Lit(new Color(0.3f, 0.17f, 0.1f));
            Material tan = Prims.Lit(new Color(0.72f, 0.52f, 0.36f));
            Material hoof = Prims.Lit(new Color(0.1f, 0.09f, 0.09f), 0.4f);
            Material horn = Prims.Lit(new Color(0.95f, 0.92f, 0.8f), 0.4f);
            Material red = Prims.Lit(new Color(0.7f, 0.12f, 0.1f));
            Material gold = Prims.Lit(new Color(0.95f, 0.75f, 0.2f), 0.7f, 0.8f);
            Material steel = Prims.Lit(new Color(0.72f, 0.75f, 0.8f), 0.75f, 0.85f);
            Material edge = Prims.Lit(new Color(0.95f, 0.96f, 1f), 0.85f, 0.9f);
            Material wood = Prims.Lit(new Color(0.33f, 0.2f, 0.1f));
            Material eye = Prims.Glow(new Color(1f, 0.25f, 0.1f));

            rig = Prims.Node(transform, "Rig", Vector3.zero);

            legL = Prims.Node(rig, "LegL", new Vector3(-0.42f, 1.15f, 0f));
            legR = Prims.Node(rig, "LegR", new Vector3(0.42f, 1.15f, 0f));
            foreach (Transform leg in new[] { legL, legR })
            {
                Prims.Box(leg, "Thigh", new Vector3(0f, -0.35f, 0f), new Vector3(0.52f, 0.78f, 0.58f), hide);
                Prims.Box(leg, "Shin", new Vector3(0f, -0.82f, 0.04f), new Vector3(0.36f, 0.5f, 0.4f), hideDark);
                Prims.Box(leg, "Hoof", new Vector3(0f, -1.05f, 0.1f), new Vector3(0.44f, 0.2f, 0.54f), hoof);
            }

            body = Prims.Node(rig, "Body", new Vector3(0f, 1.15f, 0f));
            Prims.Box(body, "Hips", new Vector3(0f, 0.15f, 0f), new Vector3(1.15f, 0.5f, 0.75f), hide);
            Prims.Box(body, "Belt", new Vector3(0f, 0.38f, 0f), new Vector3(1.22f, 0.16f, 0.82f), gold);
            Prims.Box(body, "Cloth", new Vector3(0f, -0.2f, 0.4f), new Vector3(0.7f, 0.7f, 0.1f), red);
            Prims.Box(body, "ClothBack", new Vector3(0f, -0.2f, -0.4f), new Vector3(0.7f, 0.6f, 0.1f), red);
            Prims.Box(body, "Torso", new Vector3(0f, 1.0f, 0.05f), new Vector3(1.75f, 1.15f, 1.05f), hide);
            Prims.Box(body, "Chest", new Vector3(0f, 1.05f, 0.56f), new Vector3(1.3f, 0.75f, 0.14f), tan);
            Prims.Box(body, "Strap", new Vector3(0f, 1.0f, 0.6f), new Vector3(1.7f, 0.16f, 0.12f), gold).Rot(0f, 0f, 32f);
            Prims.Part(body, "ShoulderL", Prims.Ball(6), new Vector3(-1.0f, 1.45f, 0f), new Vector3(0.82f, 0.7f, 0.82f), hideDark);
            Prims.Part(body, "ShoulderR", Prims.Ball(6), new Vector3(1.0f, 1.45f, 0f), new Vector3(0.9f, 0.75f, 0.9f), gold);
            Prims.Box(body, "Mane", new Vector3(0f, 1.6f, -0.35f), new Vector3(0.95f, 0.65f, 0.75f), hideDark);

            head = Prims.Node(body, "Head", new Vector3(0f, 1.85f, 0.42f));
            Prims.Box(head, "Skull", Vector3.zero, new Vector3(0.78f, 0.72f, 0.82f), hide);
            Prims.Box(head, "Snout", new Vector3(0f, -0.16f, 0.55f), new Vector3(0.52f, 0.42f, 0.46f), tan);
            Prims.Box(head, "Nose", new Vector3(0f, -0.12f, 0.79f), new Vector3(0.36f, 0.16f, 0.04f), hoof);
            Prims.Part(head, "Ring", Prims.Lathe("ring", 8, 0.5f, 0f, 0.5f, 1f, 0.32f, 1f, 0.32f, 0f), new Vector3(0f, -0.4f, 0.74f), new Vector3(0.26f, 0.05f, 0.26f), gold).Rot(90f, 0f, 0f);
            Prims.Box(head, "Brow", new Vector3(0f, 0.2f, 0.4f), new Vector3(0.8f, 0.14f, 0.12f), hideDark);
            Prims.Box(head, "Forelock", new Vector3(0f, 0.42f, 0.05f), new Vector3(0.4f, 0.16f, 0.6f), hideDark);
            for (int s = -1; s <= 1; s += 2)
            {
                Prims.Box(head, "Eye", new Vector3(0.22f * s, 0.06f, 0.42f), new Vector3(0.14f, 0.1f, 0.05f), eye, false);
                Prims.Box(head, "Ear", new Vector3(0.48f * s, 0.05f, -0.1f), new Vector3(0.26f, 0.14f, 0.2f), tan).Rot(0f, 0f, -20f * s);
                // two-piece horn: out to the side, then curving up and forward
                Transform h = Prims.Node(head, "Horn", new Vector3(0.36f * s, 0.3f, 0.02f));
                h.localRotation = Quaternion.Euler(-10f, 0f, -68f * s);
                Prims.Part(h, "HornBase", Prims.Lathe("hornBase", 6, 0.5f, 0f, 0.34f, 1f, 0f, 1f), Vector3.zero, new Vector3(0.34f, 0.6f, 0.34f), horn);
                Transform tip = Prims.Node(h, "TipPivot", new Vector3(0f, 0.56f, 0f));
                tip.localRotation = Quaternion.Euler(-35f, 0f, 48f * s);
                Prims.Part(tip, "HornTip", Prims.Cone(6), Vector3.zero, new Vector3(0.24f, 0.7f, 0.24f), horn);
            }

            armL = Prims.Node(body, "ArmL", new Vector3(-1.08f, 1.35f, 0.05f));
            Prims.Box(armL, "Upper", new Vector3(0f, -0.45f, 0.05f), new Vector3(0.44f, 0.95f, 0.48f), hide);
            Prims.Box(armL, "Bracer", new Vector3(0f, -0.8f, 0.08f), new Vector3(0.5f, 0.3f, 0.54f), gold);
            Prims.Part(armL, "Fist", Prims.Ball(6), new Vector3(0f, -1.08f, 0.12f), new Vector3(0.56f, 0.5f, 0.56f), hideDark);

            // right arm + labrys: the pivot yaws for sweeps and pitches for the overhead smash
            axePivot = Prims.Node(rig, "AxePivot", new Vector3(0.98f, 2.55f, 0.15f));
            Prims.Box(axePivot, "Arm", new Vector3(0f, 0f, 0.5f), new Vector3(0.44f, 0.44f, 1.1f), hide);
            Prims.Box(axePivot, "Bracer", new Vector3(0f, 0f, 0.82f), new Vector3(0.5f, 0.5f, 0.3f), gold);
            Prims.Part(axePivot, "Fist", Prims.Ball(6), new Vector3(0f, 0f, 1.1f), new Vector3(0.56f, 0.56f, 0.56f), hideDark);
            Transform axe = Prims.Node(axePivot, "Labrys", new Vector3(0f, 0f, 1.1f));
            Prims.Part(axe, "Shaft", Prims.Cyl(6), new Vector3(0f, 0f, -0.9f), new Vector3(0.15f, 3.5f, 0.15f), wood).Rot(90f, 0f, 0f);
            Prims.Part(axe, "Band1", Prims.Cyl(6), new Vector3(0f, 0f, 1.45f), new Vector3(0.22f, 0.12f, 0.22f), gold).Rot(90f, 0f, 0f);
            Prims.Part(axe, "Band2", Prims.Cyl(6), new Vector3(0f, 0f, -0.8f), new Vector3(0.22f, 0.12f, 0.22f), gold).Rot(90f, 0f, 0f);
            Prims.Box(axe, "Socket", new Vector3(0f, 0f, 1.95f), new Vector3(0.3f, 0.24f, 0.5f), gold);
            Prims.Part(axe, "Spike", Prims.Cone(6), new Vector3(0f, 0f, 2.6f), new Vector3(0.2f, 0.55f, 0.2f), steel).Rot(90f, 0f, 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                const int n = 9;
                var inner = new Vector2[n]; var outer = new Vector2[n]; var mid = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    float u = Mathf.Lerp(-1f, 1f, i / (n - 1f));
                    inner[i] = new Vector2(0.06f * s, 0.2f * u);
                    outer[i] = new Vector2((0.52f + 0.3f * Mathf.Cos(u * 1.25f)) * s, 0.88f * Mathf.Sin(u * 1.25f) / Mathf.Sin(1.25f));
                    mid[i] = Vector2.Lerp(inner[i], outer[i], 0.8f);
                }
                Prims.Part(axe, "Blade", Prims.Strip("labrys" + s, inner, mid, 0.08f), new Vector3(0f, 0f, 1.95f), Vector3.one, steel).Rot(90f, 0f, 0f);
                Prims.Part(axe, "Edge", Prims.Strip("labrysEdge" + s, mid, outer, 0.05f), new Vector3(0f, 0f, 1.95f), Vector3.one, edge).Rot(90f, 0f, 0f);
            }
        }

        // ---------------------------------------------------------------- hits

        protected override Color SparkColor(bool flesh) { return flesh ? new Color(1f, 0.4f, 0.25f) : new Color(1f, 0.85f, 0.4f); }

        public override Snd ArmorSound { get { return Snd.Clash; } }       // the sword rings off his bracers and axe

        protected override void OnHit(Vector3 from, float damage)
        {
            if (!puppet && State == S.Idle) Begin(S.Chase);
        }

        // Co-op party wipe: back to the lair, healed, resting.
        public void ResetToLair(Vector3 home)
        {
            InitStats(maxHp, maxArmor);
            transform.position = new Vector3(home.x, 0f, home.z);
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            knock = Vector3.zero;
            stunUntil = 0f;
            roared = false;
            atkCd = 0.6f; chargeCd = 3f;
            head.localRotation = Quaternion.identity;
            Begin(S.Idle);
        }

        // --- effects of the three attacks: played by the AI, and by a client's puppet when it sees the state change ---

        void FxSweep()
        {
            Fx.I.Arc(transform.position + Vector3.up * 1.2f, transform.eulerAngles.y, 1.2f, SweepRange, SweepHalfArc, -SweepHalfArc,
                new Color(1f, 0.55f, 0.25f, 0.85f), 0.3f, 0.08f);
        }

        void FxSmash()
        {
            Vector3 g = new Vector3(smashPoint.x, 0.06f, smashPoint.z);
            Fx.I.Ring(g, 0.6f, SmashRadius + 0.8f, new Color(1f, 0.6f, 0.25f, 0.9f), 0.4f);
            Fx.I.Disc(g, SmashRadius * 0.6f, SmashRadius, new Color(1f, 0.85f, 0.5f, 0.5f), 0.22f);
            Fx.I.Dust(g, 22, 1.8f);
            Fx.I.Burst(g + Vector3.up * 0.2f, 14, new Color(0.45f, 0.36f, 0.25f), 3f, 9f, Vector3.up, 0.7f, 0.7f, 14f, 0.2f);
            Game.I.Shake(0.4f, 0.3f);
        }

        void FxWall()
        {
            Game.I.Shake(0.3f, 0.3f);
            Fx.I.Dust(transform.position + chargeDir, 16, 1.6f);
        }

        protected override void OnDeath(Vector3 away)
        {
            HideDecals();
            Game.I.hud.Banner("MINOTAUR SLAIN!", new Color(1f, 0.85f, 0.3f));
            Game.I.Shake(0.5f, 0.5f);
            Fx.I.Burst(Center, 40, new Color(1f, 0.8f, 0.3f), 3f, 10f, Vector3.up, 1f, 0.9f, 10f, 0.22f);
            Fx.I.Ring(new Vector3(transform.position.x, 0.06f, transform.position.z), 1f, 6f, new Color(1f, 0.8f, 0.3f, 0.8f), 0.6f);
        }

        void HideDecals()
        {
            decalSector.gameObject.SetActive(false);
            decalDisc.gameObject.SetActive(false);
            decalLane.gameObject.SetActive(false);
        }

        void Begin(S s)
        {
            State = s;
            t = 0f;
            hitDone = false;
            chargeMask = 0;
            HideDecals();
        }

        void Recover(float seconds)
        {
            Begin(S.Recover);
            t = seconds;
        }

        void ShowDecal(Transform d, Vector3 pos, float yaw, Vector3 scale, float k)
        {
            d.gameObject.SetActive(true);
            d.position = new Vector3(pos.x, 0.04f, pos.z);
            d.rotation = Quaternion.Euler(0f, yaw, 0f);
            d.localScale = scale;
            float a = 0.3f + 0.4f * k + 0.1f * Mathf.Sin(Time.time * 22f) * k;
            Fx.I.SetDecal(d, new Color(1f, 0.05f + 0.25f * (1f - k), 0.03f, a));
        }

        // ---------------------------------------------------------------- AI

        void Update()
        {
            float dt = Time.deltaTime;
            if (Dead)
            {
                deathT += dt;
                float f = Mathf.Clamp01(deathT / 0.7f);
                rig.localRotation = Quaternion.Euler(88f * f * f, 0f, 0f);
                if (deathT > 2.2f) rig.localPosition = new Vector3(0f, -(deathT - 2.2f) * 1.5f, 0f);
                if (deathT > 3.6f) Destroy(gameObject);
                return;
            }
            BaseTick(dt);

            // sim = the AI runs here (single player / host). A client's puppet only poses: its State, t and position
            // come from the host, and nothing below may start a state or hurt anybody.
            bool sim = !puppet;
            if (puppet) PuppetMove();
            Knight k = Target();
            if (k == null) return;
            var ps = Game.I.players;
            Vector3 toK = k.Pos - transform.position; toK.y = 0f;
            float dist = toK.magnitude;
            float yawT = 25f, pitchT = -40f, leanT = 12f, poseRate = 9f;
            bool walking = false;
            chargeCd -= dt;

            if (sim && Stunned && State != S.Stun && State != S.Charging) Begin(S.Stun);

            switch (State)
            {
                case S.Idle:
                    if (sim && !k.Dead && dist < AggroRange) Begin(S.Chase);
                    break;

                case S.Chase:
                    if (!sim) { walking = puppetSpeed > 0.6f; break; }
                    if (!roared)
                    {
                        roared = true;
                        Game.I.WorldBanner("The Minotaur notices you!", new Color(1f, 0.5f, 0.35f));
                    }
                    if (k.Dead || dist > 34f) { roared = false; Begin(S.Idle); break; }
                    TurnTowards(toK, 230f, dt);
                    if (dist > 3.1f)
                    {
                        transform.position += transform.forward * 3.0f * dt;
                        walking = true;
                    }
                    atkCd -= dt;
                    if (atkCd <= 0f)
                    {
                        if (dist < 4.1f) Begin(Random.value < 0.6f ? S.SweepTele : S.SmashTele);
                        else if (dist > 7f && dist < 18f && chargeCd <= 0f) Begin(S.ChargeTele);
                    }
                    break;

                case S.SweepTele:
                    t += dt / 0.75f;
                    if (sim) TurnTowards(toK, 80f, dt);
                    yawT = 125f; pitchT = -8f; leanT = 4f; poseRate = 12f;
                    ShowDecal(decalSector, transform.position, transform.eulerAngles.y, Vector3.one, Mathf.Clamp01(t));
                    if (sim && t >= 1f)
                    {
                        Begin(S.SweepStrike);
                        FxSweep();
                    }
                    break;

                case S.SweepStrike:
                    t += dt / 0.2f;
                    axeYaw = Mathf.Lerp(125f, -115f, Mathf.Clamp01(t));
                    axePitch = 4f;
                    yawT = axeYaw; pitchT = axePitch; leanT = 20f; poseRate = 0f;
                    if (!hitDone && t >= 0.45f)
                    {
                        hitDone = true;
                        Game.I.Shake(0.15f, 0.2f);
                        for (int i = 0; sim && i < ps.Count; i++)          // everyone inside the arc
                        {
                            Knight p = ps[i];
                            if (p.Dead || p.Airborne) continue;
                            Vector3 toP = p.Pos - transform.position; toP.y = 0f;
                            float dp = toP.magnitude;
                            if (dp < SweepRange + 0.4f && (dp < 1.6f || Vector3.Angle(transform.forward, toP) < SweepHalfArc))
                                Game.I.DamageKnight(p, SweepDamage * dmgScale, transform.position, "Axe Sweep", 9f);
                        }
                    }
                    if (sim && t >= 1f) Recover(0.75f);
                    break;

                case S.SmashTele:
                    t += dt / 0.85f;
                    if (sim && t < 0.65f)
                    {
                        TurnTowards(toK, 140f, dt);
                        smashPoint = transform.position + transform.forward * 3.1f + transform.right * 0.7f;
                    }
                    yawT = 0f; pitchT = -125f; leanT = -6f; poseRate = 10f;
                    ShowDecal(decalDisc, smashPoint, 0f, new Vector3(SmashRadius, 1f, SmashRadius), Mathf.Clamp01(t));
                    if (sim && t >= 1f) Begin(S.SmashStrike);
                    break;

                case S.SmashStrike:
                    t += dt / 0.14f;
                    axeYaw = 0f;
                    axePitch = Mathf.Lerp(-125f, 38f, Mathf.Clamp01(t) * Mathf.Clamp01(t));
                    yawT = axeYaw; pitchT = axePitch; leanT = 26f; poseRate = 0f;
                    if (sim && t >= 1f)
                    {
                        FxSmash();
                        for (int i = 0; i < ps.Count; i++)                 // everyone inside the circle
                        {
                            Knight p = ps[i];
                            if (!p.Dead && !p.Airborne && FlatDist(p.Pos, smashPoint) < SmashRadius + 0.35f)
                                Game.I.DamageKnight(p, SmashDamage * dmgScale, smashPoint, "Axe Smash", 7f);
                        }
                        Recover(1.0f);
                        axePitch = 38f;
                    }
                    break;

                case S.ChargeTele:
                    t += dt / 0.9f;
                    if (sim && t < 0.7f) TurnTowards(toK, 220f, dt);
                    chargeDir = transform.forward;
                    yawT = 12f; pitchT = -80f; leanT = -10f + 40f * Mathf.Clamp01(t);
                    walking = true;                                    // paws the ground
                    ShowDecal(decalLane, transform.position, transform.eulerAngles.y, new Vector3(LaneWidth, 1f, LaneLength), Mathf.Clamp01(t));
                    if (Random.value < dt * 14f) Fx.I.Dust(transform.position - transform.forward * 0.6f, 2, 1.2f);
                    if (sim && t >= 1f) Begin(S.Charging);
                    break;

                case S.Charging:
                    t += dt;
                    Vector3 before = transform.position;
                    bool wall = false;
                    if (sim)
                    {
                        transform.position += chargeDir * 15f * dt;
                        ClampToArena();
                        wall = (transform.position - before).magnitude < 15f * dt * 0.5f && dt > 0f;
                    }
                    else chargeDir = transform.forward;
                    yawT = 12f; pitchT = -80f; leanT = 34f; walking = true;
                    walkPhase += dt * 14f;
                    if (Random.value < dt * 40f) Fx.I.Dust(transform.position - chargeDir * 0.8f, 3, 1.4f);
                    if (!sim) break;
                    for (int i = 0; i < ps.Count; i++)                     // runs over each knight in the lane once
                    {
                        Knight p = ps[i];
                        int bit = 1 << (p.pid & 31);
                        if ((chargeMask & bit) != 0 || p.Dead || p.Airborne || FlatDist(before, p.Pos) >= radius + 1.0f) continue;
                        chargeMask |= bit;
                        Game.I.DamageKnight(p, ChargeDamage * dmgScale, transform.position - chargeDir, "Charge", 15f);
                        Game.I.Shake(0.35f, 0.3f);
                    }
                    if (t >= LaneLength / 15f || wall)
                    {
                        chargeCd = 9f;
                        if (wall) FxWall();
                        Recover(wall ? 1.6f : 1.1f);
                    }
                    break;

                case S.Recover:
                    t -= dt;
                    leanT = 18f; poseRate = 5f;
                    if (sim && t <= 0f) { Begin(S.Chase); atkCd = 0.3f; }
                    break;

                case S.Stun:
                    leanT = 22f; yawT = 60f; pitchT = 30f;
                    head.localRotation = Quaternion.Euler(20f, Mathf.Sin(Time.time * 14f) * 18f, 0f);
                    if (sim && !Stunned) { head.localRotation = Quaternion.identity; Begin(S.Chase); atkCd = 0.4f; }
                    break;
            }

            // --- pose ---
            if (poseRate > 0f)
            {
                float e = 1f - Mathf.Exp(-poseRate * dt);
                axeYaw = Mathf.Lerp(axeYaw, yawT, e);
                axePitch = Mathf.Lerp(axePitch, pitchT, e);
            }
            lean = Mathf.Lerp(lean, leanT, 1f - Mathf.Exp(-10f * dt));
            axePivot.localRotation = Quaternion.Euler(axePitch, axeYaw, 0f);
            body.localRotation = Quaternion.Euler(lean, 0f, 0f);
            if (walking) walkPhase += dt * 7f;
            float sw = walking ? Mathf.Sin(walkPhase) * 28f : 0f;
            legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(sw, 0f, 0f), 1f - Mathf.Exp(-14f * dt));
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(-sw, 0f, 0f), 1f - Mathf.Exp(-14f * dt));
            armL.localRotation = Quaternion.Euler(-sw * 0.6f - lean, 0f, 0f);
            float breathe = Mathf.Sin(Time.time * 2f) * 0.02f;
            rig.localPosition = new Vector3(0f, breathe + (walking ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.08f : 0f), 0f);
        }
    }
}

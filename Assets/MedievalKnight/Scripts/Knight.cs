using System.Collections;
using UnityEngine;

namespace MedievalKnight
{
    public enum AttackKind { Slash, Spin, Thrust }

    // One attack = wind-up pose -> fast strike -> recover. Angles are for a knight facing RIGHT:
    // 0 = sword up, -90 = sword forward, -180 = sword down (same convention as the reference pack).
    public class AttackDef
    {
        public string name = "Attack";
        public AttackKind kind = AttackKind.Slash;
        public float windupAngle = 70f, strikeAngle = -150f;
        public Vector2 windupOffset = new Vector2(-0.1f, 0.1f);   // sword hand offset from the rest position
        public Vector2 strikeOffset = new Vector2(0.15f, -0.05f);
        public float windupTime = 0.09f, strikeTime = 0.10f, recoverTime = 0.18f;
        public float lunge = 2f;                // forward speed of the body during the strike
        public float leanWindup = 6f, leanStrike = -8f;           // body lean in degrees (+ = backwards)
        public float hopWindup = 0f, hopStrike = 0f;              // body lift during wind-up / strike
        public float fxRotation = 0f, fxScale = 0.75f;
        public bool fxFlipY = false;
        public Vector2 fxOffset = new Vector2(0.3f, 0.9f);
        public Color fxColor = Color.white;
        public float reach = 2.4f;
        public float damage = 10f;
        public float pierce = 0f;               // share of damage that ignores armor
        public float stamina = 12f;
        public float shake = 0.1f;
        public float sparkDir = -45f;           // direction hit sparks fly, for a knight facing right
    }

    // The player: movement, health / armor / stamina, shield block and the five test attacks.
    public class Knight : MonoBehaviour
    {
        public const float MaxHp = 100f, MaxArmor = 60f, MaxStamina = 100f;
        const float MoveSpeed = 5.5f, JumpSpeed = 11.5f, Gravity = 32f;
        const float RestAngle = -20f;
        static readonly Vector2 SwordRest = new Vector2(0.25f, 0.875f);
        static readonly Vector2 ShieldRest = new Vector2(0.1f, 0.8f);
        static readonly Vector2 ShieldBlock = new Vector2(0.5f, 0.95f);
        const int ComboRequest = 100;

        public static readonly AttackDef[] Attacks =
        {
            new AttackDef { name = "Side Slash", damage = 10f, stamina = 12f },

            new AttackDef { name = "Overhead Chop", windupAngle = 35f, strikeAngle = -125f,
                windupOffset = new Vector2(-0.15f, 0.5f), strikeOffset = new Vector2(0.25f, -0.25f),
                windupTime = 0.16f, strikeTime = 0.09f, recoverTime = 0.24f, lunge = 3.5f,
                leanWindup = 12f, leanStrike = -16f, hopWindup = 0.35f,
                fxRotation = 35f, fxScale = 0.95f, fxOffset = new Vector2(0.35f, 1.0f),
                fxColor = new Color(1f, 0.93f, 0.75f),
                damage = 16f, stamina = 18f, shake = 0.2f, sparkDir = -80f },

            new AttackDef { name = "Rising Slash", windupAngle = -170f, strikeAngle = 30f,
                windupOffset = new Vector2(0.1f, -0.3f), strikeOffset = new Vector2(0.1f, 0.3f),
                windupTime = 0.10f, strikeTime = 0.10f, recoverTime = 0.2f, lunge = 1.5f,
                leanWindup = -10f, leanStrike = 10f, hopStrike = 0.3f,
                fxRotation = -10f, fxFlipY = true, fxOffset = new Vector2(0.3f, 0.85f),
                fxColor = new Color(0.8f, 1f, 0.9f),
                damage = 12f, stamina = 15f, shake = 0.11f, sparkDir = 65f },

            new AttackDef { name = "Spin Slash", kind = AttackKind.Spin, windupAngle = 110f, strikeAngle = -330f,
                windupOffset = new Vector2(-0.3f, 0.05f), strikeOffset = new Vector2(0.2f, 0f),
                windupTime = 0.17f, strikeTime = 0.3f, recoverTime = 0.28f, lunge = 0.6f,
                leanWindup = 8f, leanStrike = 0f, hopStrike = 0.15f,
                fxScale = 1.0f, fxOffset = new Vector2(0f, 0.9f), fxColor = new Color(1f, 0.85f, 0.95f),
                reach = 2.6f, damage = 22f, stamina = 28f, shake = 0.22f, sparkDir = 15f },

            new AttackDef { name = "Thrust", kind = AttackKind.Thrust, windupAngle = -80f, strikeAngle = -90f,
                windupOffset = new Vector2(-0.45f, 0f), strikeOffset = new Vector2(0.8f, 0f),
                windupTime = 0.13f, strikeTime = 0.07f, recoverTime = 0.22f, lunge = 6f,
                leanWindup = 9f, leanStrike = -13f,
                fxScale = 0.75f, fxOffset = new Vector2(1.7f, 0.9f),
                reach = 3.1f, damage = 20f, pierce = 0.5f, stamina = 20f, shake = 0.15f, sparkDir = 0f },
        };

        public float hp = MaxHp, armor = MaxArmor, stamina = MaxStamina;
        public int Facing { get; private set; }
        public bool IsAttacking { get; private set; }
        public bool IsBlocking { get; private set; }
        public bool Dead { get; private set; }
        public float StaminaDeniedTime { get; private set; }   // last time an action was refused for stamina

        Transform rig, sword, shield;
        SpriteRenderer body, swordSr, shieldSr, shadow;
        Sprite arcSprite, streakSprite;

        float vy, y;
        bool grounded = true;
        float swordAngle = RestAngle, rigLean, rigHop, rigScaleX = 1f;
        float lastSpendTime = -10f, lastDamageTime = -10f, lastAttackEnd = -10f, hurtTint, knockVel;
        int comboIndex, queued = -1;
        float queuedTime, ghostTimer, walkPhase;
        Coroutine attackCo;

        void Awake()
        {
            Facing = 1;
            StaminaDeniedTime = -10f;

            rig = new GameObject("Rig").transform;
            rig.SetParent(transform, false);

            body = Art.NewSprite("Body", Art.Get("knight", 0.5f, 0f), rig, 5);
            swordSr = Art.NewSprite("Sword", Art.Get("sword"), rig, 6);
            shieldSr = Art.NewSprite("Shield", Art.Get("shield"), rig, 7);
            sword = swordSr.transform;
            shield = shieldSr.transform;
            sword.localPosition = SwordRest;
            sword.localRotation = Quaternion.Euler(0, 0, RestAngle);
            shield.localPosition = ShieldRest;

            shadow = Art.NewSprite("Shadow", Art.Blob(), null, -9);
            shadow.transform.SetParent(transform.parent, false);
            shadow.color = new Color(0, 0, 0, 0.4f);
            shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            arcSprite = Art.Get("slash_arc");
            streakSprite = Art.Get("thrust_streak");
        }

        public void ResetAll()
        {
            if (attackCo != null) StopCoroutine(attackCo);
            StopAllCoroutines();
            attackCo = null;
            hp = MaxHp; armor = MaxArmor; stamina = MaxStamina;
            Dead = false; IsAttacking = false; IsBlocking = false;
            queued = -1; comboIndex = 0; knockVel = 0f; hurtTint = 0f;
            rigLean = 0f; rigHop = 0f; rigScaleX = 1f;
            SetSword(RestAngle, SwordRest);
            shield.localPosition = ShieldRest;
            y = 0f; vy = 0f; grounded = true;
            Vector3 p = transform.position;
            transform.position = new Vector3(0f, 0f, p.z);
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.deltaTime;
            Dummy dummy = Game.I.dummy;

            // --- resources ---
            if (!Dead)
            {
                if (!IsBlocking && Time.time - lastSpendTime > 0.6f)
                    stamina = Mathf.Min(MaxStamina, stamina + 26f * dt);
                if (Time.time - lastDamageTime > 4f)
                    armor = Mathf.Min(MaxArmor, armor + 8f * dt);
            }

            // --- input ---
            float x = Dead ? 0f : InputX.MoveX();
            IsBlocking = !Dead && !IsAttacking && grounded && InputX.Held(KeyCode.LeftShift);

            if (!Dead)
            {
                int req = -1;
                if (InputX.Down(KeyCode.Alpha1)) req = 0;
                else if (InputX.Down(KeyCode.Alpha2)) req = 1;
                else if (InputX.Down(KeyCode.Alpha3)) req = 2;
                else if (InputX.Down(KeyCode.Alpha4)) req = 3;
                else if (InputX.Down(KeyCode.Alpha5) || InputX.Down(KeyCode.K)) req = 4;
                else if (InputX.Down(KeyCode.J)) req = ComboRequest;

                if (req >= 0)
                {
                    if (IsAttacking) { queued = req; queuedTime = Time.time; }   // buffer the next attack
                    else TryStartAttack(req);
                }
                else if (!IsAttacking && queued >= 0)
                {
                    int q = queued;
                    queued = -1;
                    if (Time.time - queuedTime < 0.45f) TryStartAttack(q);
                }
            }

            // --- movement ---
            if (x != 0f && !IsAttacking) Facing = x > 0f ? 1 : -1;
            float speedMul = IsAttacking ? 0.25f : IsBlocking ? 0.4f : 1f;
            Vector3 p = transform.position;
            p.x += x * MoveSpeed * speedMul * dt;
            p.x += knockVel * dt;
            knockVel = Mathf.MoveTowards(knockVel, 0f, 30f * dt);

            if (!Dead && grounded && !IsAttacking && (InputX.Down(KeyCode.Space) || InputX.Down(KeyCode.W) || InputX.Down(KeyCode.UpArrow)))
            {
                vy = JumpSpeed;
                grounded = false;
                Dust(p, 6);
            }
            if (!grounded)
            {
                vy -= Gravity * dt;
                y += vy * dt;
                if (y <= 0f)
                {
                    y = 0f; vy = 0f; grounded = true;
                    Dust(p, 8);
                }
            }

            // the dummy is solid
            if (!dummy.Broken && y < 1.9f)
            {
                float dx = p.x - dummy.transform.position.x;
                float min = Dummy.HalfWidth + 0.4f;
                if (Mathf.Abs(dx) < min) p.x = dummy.transform.position.x + (dx >= 0f ? min : -min);
            }
            p.x = Mathf.Clamp(p.x, -Game.ArenaHalf, Game.ArenaHalf);
            p.y = y;
            transform.position = p;
            transform.localScale = new Vector3(Facing, 1f, 1f);

            // --- idle / walk / block poses (attacks drive these themselves) ---
            bool moving = x != 0f && grounded;
            if (!IsAttacking && !Dead)
            {
                float targetAngle = IsBlocking ? RestAngle + 30f : RestAngle + (moving ? Mathf.Sin(walkPhase) * 6f : Mathf.Sin(Time.time * 2.2f) * 2f);
                swordAngle = Mathf.Lerp(swordAngle, targetAngle, dt * 14f);
                sword.localRotation = Quaternion.Euler(0, 0, swordAngle);
                sword.localPosition = Vector3.Lerp(sword.localPosition, SwordRest, dt * 14f);
                shield.localPosition = Vector3.Lerp(shield.localPosition, IsBlocking ? ShieldBlock : ShieldRest, dt * 18f);
                shield.localScale = Vector3.Lerp(shield.localScale, Vector3.one * (IsBlocking ? 1.25f : 1f), dt * 18f);

                if (moving) walkPhase += dt * 14f;
                float bob = moving ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.08f : Mathf.Sin(Time.time * 2.2f) * 0.02f;
                float lean = moving ? -5f : IsBlocking ? 4f : 0f;
                if (!grounded) lean = vy > 0f ? 6f : -6f;
                rigHop = Mathf.Lerp(rigHop, bob, dt * 20f);
                rigLean = Mathf.Lerp(rigLean, lean, dt * 12f);
                rigScaleX = Mathf.Lerp(rigScaleX, 1f, dt * 20f);
            }
            if (!Dead)
            {
                rig.localPosition = new Vector3(0f, rigHop, 0f);
                rig.localRotation = Quaternion.Euler(0, 0, rigLean);
                rig.localScale = new Vector3(rigScaleX, 1f, 1f);
            }

            // --- tint + shadow ---
            hurtTint = Mathf.Max(0f, hurtTint - dt / 0.3f);
            Color tint = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), hurtTint);
            body.color = tint; shieldSr.color = tint; swordSr.color = tint;

            float air = Mathf.Clamp01(y / 4f);
            shadow.transform.position = new Vector3(p.x, 0.012f, p.z);
            shadow.transform.localScale = new Vector3(1.5f, 0.85f, 1f) * (1f - air * 0.5f);
        }

        void Dust(Vector3 at, int n)
        {
            Fx.I.Burst(new Vector3(at.x, 0.1f, at.z), n, new Color(0.85f, 0.82f, 0.7f, 0.8f), 0.8f, 2.6f, 90f, 170f, 0.35f, 2f, 0.12f);
        }

        // ---------------------------------------------------------------- attacks

        // Used by SelfTest to trigger an attack without the keyboard.
        public void DebugAttack(int index) { if (!Dead && !IsAttacking) TryStartAttack(index); }

        void TryStartAttack(int request)
        {
            int index = request;
            bool fromCombo = request == ComboRequest;
            if (fromCombo)
            {
                if (Time.time - lastAttackEnd > 0.9f) comboIndex = 0;   // combo timed out
                index = comboIndex;
            }
            AttackDef a = Attacks[index];
            if (stamina < a.stamina)
            {
                StaminaDeniedTime = Time.unscaledTime;
                Game.I.hud.Banner("Not enough stamina for " + a.name, new Color(1f, 0.5f, 0.4f));
                return;
            }
            stamina -= a.stamina;
            lastSpendTime = Time.time;
            if (fromCombo) comboIndex = (index + 1) % 4;
            Game.I.hud.Banner((index + 1) + "  " + a.name, a.kind == AttackKind.Thrust ? new Color(0.7f, 0.9f, 1f) : new Color(1f, 0.95f, 0.75f));
            attackCo = StartCoroutine(AttackRoutine(a));
        }

        IEnumerator AttackRoutine(AttackDef a)
        {
            IsAttacking = true;
            IsBlocking = false;
            int dir = Facing;

            // 1) wind-up
            float startAngle = swordAngle, startLean = rigLean, startHop = rigHop;
            Vector2 startPos = sword.localPosition;
            Vector2 windPos = SwordRest + a.windupOffset;
            for (float t = 0; t < 1f; t += Time.deltaTime / a.windupTime)
            {
                float e = 1f - (1f - t) * (1f - t);                       // ease out
                SetSword(Mathf.Lerp(startAngle, a.windupAngle, e), Vector2.Lerp(startPos, windPos, e));
                rigLean = Mathf.Lerp(startLean, a.leanWindup, e);
                rigHop = Mathf.Lerp(startHop, a.hopWindup, e);
                yield return null;
            }

            // 2) strike
            SpawnAttackFx(a, dir);
            if (a.lunge > 2.5f) Dust(transform.position, 5);
            Vector2 strikePos = SwordRest + a.strikeOffset;
            bool hitDone = false;
            ghostTimer = 0f;
            for (float t = 0; t < 1f; t += Time.deltaTime / a.strikeTime)
            {
                float e = a.kind == AttackKind.Spin ? t : t * t;          // spin turns evenly, the rest accelerate
                SetSword(Mathf.Lerp(a.windupAngle, a.strikeAngle, e), Vector2.Lerp(windPos, strikePos, e));
                rigLean = Mathf.Lerp(a.leanWindup, a.leanStrike, e);
                rigHop = Mathf.Lerp(a.hopWindup, 0f, e) + Mathf.Sin(t * Mathf.PI) * a.hopStrike;
                if (a.kind == AttackKind.Spin) rigScaleX = Mathf.Cos(t * Mathf.PI * 2f);   // paper-spin the whole body

                Vector3 p = transform.position;
                p.x += dir * a.lunge * Time.deltaTime;
                transform.position = p;

                ghostTimer -= Time.deltaTime;
                if (ghostTimer <= 0f) { ghostTimer = 0.012f; SwordGhost(a); }

                if (!hitDone && t > (a.kind == AttackKind.Spin ? 0.2f : 0.35f)) { DoHit(a, dir); hitDone = true; }
                yield return null;
            }
            if (!hitDone) DoHit(a, dir);
            rigScaleX = 1f;
            if (a.hopWindup > 0.2f)   // overhead chop slams the ground
            {
                Dust(transform.position + new Vector3(dir * 1.3f, 0f, 0f), 10);
                Game.I.Shake(a.shake * 0.5f, 0.12f);
            }

            // 3) recover
            float endAngle = Mathf.Repeat(a.strikeAngle + 180f, 360f) - 180f;   // unwind full turns (spin)
            for (float t = 0; t < 1f; t += Time.deltaTime / a.recoverTime)
            {
                float e = t * t * (3f - 2f * t);                          // smoothstep
                SetSword(Mathf.LerpAngle(endAngle, RestAngle, e), Vector2.Lerp(strikePos, SwordRest, e));
                rigLean = Mathf.Lerp(a.leanStrike, 0f, e);
                rigHop = Mathf.Lerp(rigHop, 0f, e);
                if (queued >= 0 && t > 0.45f) break;                       // a buffered attack cancels the tail
                yield return null;
            }
            swordAngle = Mathf.Repeat(swordAngle + 180f, 360f) - 180f;
            IsAttacking = false;
            lastAttackEnd = Time.time;
            attackCo = null;
        }

        void SetSword(float angle, Vector2 pos)
        {
            swordAngle = angle;
            sword.localRotation = Quaternion.Euler(0, 0, angle);
            sword.localPosition = pos;
        }

        // Fading after-image of the blade = the swing trail.
        void SwordGhost(AttackDef a)
        {
            float sx = Facing * (rigScaleX < 0f ? -1f : 1f);
            Color c = a.kind == AttackKind.Thrust ? new Color(0.6f, 0.9f, 1f, 0.5f) : new Color(a.fxColor.r, a.fxColor.g, a.fxColor.b, 0.45f);
            Fx.I.SpawnSprite(swordSr.sprite, sword.position, sx * (swordAngle + rigLean), new Vector3(sx, 1f, 1f), c, 0.13f, 0f, 0f, 4);
        }

        void SpawnAttackFx(AttackDef a, int dir)
        {
            Vector3 origin = transform.position;
            Vector3 pos = origin + new Vector3(a.fxOffset.x * dir, a.fxOffset.y, 0f);
            switch (a.kind)
            {
                case AttackKind.Thrust:
                    Fx.I.SpawnSprite(streakSprite, pos, 0f, new Vector3(dir * a.fxScale, a.fxScale, 1f), Color.white, 0.22f, 0.35f, 0f, 10);
                    Fx.I.Burst(origin + new Vector3(dir * 0.8f, 0.9f, 0f), 7, new Color(0.75f, 0.92f, 1f, 0.9f), 6f, 13f, dir > 0 ? 0f : 180f, 8f, 0.18f, 0f, 0.07f);
                    break;
                case AttackKind.Spin:
                    // two arcs chasing each other = a full ring around the knight
                    Fx.I.SpawnSprite(arcSprite, pos, 0f, new Vector3(dir * a.fxScale, a.fxScale, 1f), a.fxColor, 0.36f, 0.25f, -dir * 1100f, 10);
                    Fx.I.SpawnSprite(arcSprite, pos, 180f, new Vector3(dir * a.fxScale, a.fxScale, 1f), a.fxColor, 0.36f, 0.25f, -dir * 1100f, 10);
                    Dust(origin, 10);
                    break;
                default:
                    Fx.I.SpawnSprite(arcSprite, pos, a.fxRotation * dir, new Vector3(dir * a.fxScale, (a.fxFlipY ? -1f : 1f) * a.fxScale, 1f), a.fxColor, 0.2f, 0.25f, 0f, 10);
                    break;
            }
        }

        void DoHit(AttackDef a, int dir)
        {
            Dummy d = Game.I.dummy;
            if (d.Broken || y > 2.2f) return;
            float dx = d.transform.position.x - transform.position.x;
            bool inRange = a.kind == AttackKind.Spin
                ? Mathf.Abs(dx) <= a.reach
                : dx * dir >= -0.3f && dx * dir <= a.reach;
            if (!inRange) return;

            float side = dx >= 0f ? 1f : -1f;                 // which way the blow pushes
            float sparks = side > 0f ? a.sparkDir : 180f - a.sparkDir;
            d.TakeHit(a.damage, a.pierce, transform.position.x, a.name, sparks, a.kind == AttackKind.Thrust);
            Game.I.Shake(a.shake, 0.16f);
            Game.I.HitStop(a.damage >= 16f ? 0.075f : 0.05f);
        }

        // ---------------------------------------------------------------- taking damage

        // Armor absorbs first. A raised shield facing the blow cuts it by 80% for 15 stamina.
        public void TakeDamage(float damage, float fromX)
        {
            if (Dead) return;
            float side = fromX >= transform.position.x ? 1f : -1f;   // where the blow comes from
            Vector3 chest = transform.position + new Vector3(0f, 1.1f, 0f);
            bool blocked = false;

            if (IsBlocking && Mathf.Approximately(side, Facing))
            {
                if (stamina >= 15f)
                {
                    blocked = true;
                    stamina -= 15f;
                    lastSpendTime = Time.time;
                    damage *= 0.2f;
                    Game.I.hud.Popup(chest + new Vector3(0f, 1.3f, 0f), "BLOCK", new Color(1f, 0.9f, 0.45f), 26);
                    Fx.I.Burst(chest + new Vector3(side * 0.5f, 0f, 0f), 12, new Color(1f, 0.92f, 0.6f), 3f, 8f, side > 0 ? 0f : 180f, 140f, 0.25f, 5f, 0.1f);
                }
                else
                {
                    StaminaDeniedTime = Time.unscaledTime;
                    Game.I.hud.Popup(chest + new Vector3(0f, 1.3f, 0f), "GUARD BROKEN", new Color(1f, 0.5f, 0.4f), 24);
                }
            }

            float toArmor = Mathf.Min(armor, damage);
            armor -= toArmor;
            float toHp = damage - toArmor;
            hp = Mathf.Max(0f, hp - toHp);
            lastDamageTime = Time.time;

            if (toArmor >= 0.5f) Game.I.hud.Popup(chest + new Vector3(-0.3f, 0.7f, 0f), "-" + Mathf.RoundToInt(toArmor), new Color(0.65f, 0.82f, 1f), 24);
            if (toHp >= 0.5f) Game.I.hud.Popup(chest + new Vector3(0.3f, 0.95f, 0f), "-" + Mathf.RoundToInt(toHp), new Color(1f, 0.35f, 0.25f), 28);

            knockVel = -side * (blocked ? 2.5f : 6f);
            if (!blocked)
            {
                hurtTint = 1f;
                Fx.I.Burst(chest, 8, toHp > 0.5f ? new Color(1f, 0.3f, 0.25f) : new Color(0.7f, 0.85f, 1f), 2f, 6f, side > 0 ? 180f : 0f, 120f, 0.3f, 8f, 0.1f);
            }
            Game.I.Shake(blocked ? 0.08f : 0.18f, 0.18f);

            if (hp <= 0f)
            {
                if (attackCo != null) { StopCoroutine(attackCo); attackCo = null; }
                StartCoroutine(DownRoutine());
            }
        }

        IEnumerator DownRoutine()
        {
            Dead = true;
            IsAttacking = false;
            IsBlocking = false;
            queued = -1;
            rigScaleX = 1f;
            Game.I.hud.Banner("Knight down! Reviving...", new Color(1f, 0.5f, 0.4f));
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)     // fall on the back
            {
                rig.localRotation = Quaternion.Euler(0, 0, 88f * t * t);
                rig.localPosition = new Vector3(0f, 0.15f * t, 0f);
                yield return null;
            }
            yield return new WaitForSeconds(1.3f);
            hp = MaxHp; armor = MaxArmor; stamina = MaxStamina;
            rigLean = 0f; rigHop = 0f;
            SetSword(RestAngle, SwordRest);
            Dead = false;
        }
    }
}

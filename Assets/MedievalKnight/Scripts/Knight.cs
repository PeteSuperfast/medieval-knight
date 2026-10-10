using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    public enum AttackKind { Slash, Chop, Spin, Thrust }

    // One basic sword attack = wind-up pose -> fast strike -> recover.
    // The sword turns around the knight: yaw 0 = straight ahead, +90 = to his right; pitch < 0 = tip up.
    public class AttackDef
    {
        public string name = "Attack";
        public AttackKind kind = AttackKind.Slash;
        public float yawWind = 115f, yawStrike = -105f;
        public float pitchWind = -8f, pitchStrike = 6f;
        public float handWind = 0.3f, handStrike = 0.5f;     // how far the sword hand is pushed out
        public float windupTime = 0.09f, strikeTime = 0.10f, recoverTime = 0.18f;
        public float lunge = 2f;                // forward speed of the body during the strike
        public float leanWind = -6f, leanStrike = 10f;        // body lean in degrees (+ = forwards)
        public float hopWind = 0f, hopStrike = 0f;
        public Color fxColor = Color.white;
        public float reach = 2.7f, halfArc = 80f;
        public float damage = 10f;
        public float pierce = 0f;               // share of damage that ignores armor
        public float stamina = 12f;
        public float shake = 0.1f, knock = 3.5f;
    }

    public class SkillDef
    {
        public string name, key, hint;
        public float cooldown, stamina, damage;
        public Color color;
    }

    // What a knight is asked to do in one frame. It comes from the keyboard / mouse (Knight.ReadInput) or from a
    // test script; the simulation (Knight.Simulate) never reads a device itself.
    public struct KnightInput
    {
        public Vector2 move;        // walk direction on the ground plane (x = right, y = world +Z)
        public Vector3 aim;         // world point the knight faces and leaps to
        public bool block;
        public int request;         // -1 = nothing, Knight.ComboRequest = next sword attack, Knight.SkillRequest + n = skill n
    }

    // A player: top-down movement, health / armor / stamina, shield block, the 5-step sword combo
    // (4 slashes + thrust) and the four active skills. He always faces the mouse cursor.
    // Everything that belongs to one player lives on this object: level / upgrades / stat points (progress)
    // and the owned + equipped swords (inventory).
    // A knight is either local (remote == false: driven by ReadInput + Simulate on this machine) or remote
    // (another player's knight: position, pose flags and numbers come from the network, attacks are replayed
    // for show only and never hit anything here).
    public class Knight : MonoBehaviour
    {
        public const float BaseHp = 100f, HpPerLevel = 10f, BaseArmor = 60f, BaseStamina = 100f, StaminaRegen = 26f, BlockCost = 15f;
        public const float MinSwingSpeed = 0.4f, MaxSwingSpeed = 4f;
        public const int ComboRequest = 100, SkillRequest = 200;
        const float MoveSpeed = 6.2f;
        const float RestYaw = 55f, RestPitch = -25f, RestHand = 0.36f;
        static readonly Vector3 ShieldRestPos = new Vector3(-0.5f, 1.05f, 0.12f), ShieldBlockPos = new Vector3(-0.05f, 1.12f, 0.55f);

        // tabard + shield colour and plume + cape colour per player slot (slot 0 is the classic blue / red knight)
        public static readonly Color[] MainColors =
        {
            new Color(0.16f, 0.32f, 0.8f), new Color(0.82f, 0.16f, 0.13f), new Color(0.14f, 0.6f, 0.24f), new Color(0.58f, 0.26f, 0.8f)
        };
        public static readonly Color[] AccentColors =
        {
            new Color(0.85f, 0.13f, 0.12f), new Color(0.97f, 0.82f, 0.2f), new Color(0.93f, 0.93f, 0.96f), new Color(1f, 0.55f, 0.15f)
        };
        public static Color UiColor(int pid) { return Color.Lerp(MainColors[pid & 3], Color.white, 0.35f); }

        public static readonly AttackDef[] Attacks =
        {
            new AttackDef { name = "Side Slash", damage = 10f, stamina = 12f, fxColor = new Color(1f, 1f, 1f) },

            new AttackDef { name = "Overhead Chop", kind = AttackKind.Chop, yawWind = 8f, yawStrike = 0f, pitchWind = -118f, pitchStrike = 42f,
                handWind = 0.3f, handStrike = 0.62f,
                windupTime = 0.16f, strikeTime = 0.09f, recoverTime = 0.24f, lunge = 3.5f,
                leanWind = -12f, leanStrike = 18f, hopWind = 0.35f,
                fxColor = new Color(1f, 0.93f, 0.7f), reach = 3.0f, halfArc = 34f,
                damage = 16f, stamina = 18f, shake = 0.2f, knock = 4.5f },

            new AttackDef { name = "Rising Slash", yawWind = -115f, yawStrike = 105f, pitchWind = 28f, pitchStrike = -32f,
                windupTime = 0.10f, strikeTime = 0.10f, recoverTime = 0.2f, lunge = 1.5f,
                leanWind = 8f, leanStrike = -8f, hopStrike = 0.3f,
                fxColor = new Color(0.75f, 1f, 0.88f),
                damage = 12f, stamina = 15f, shake = 0.11f },

            new AttackDef { name = "Spin Slash", kind = AttackKind.Spin, yawWind = 100f, yawStrike = 100f, pitchWind = -5f, pitchStrike = 0f,
                handWind = 0.4f, handStrike = 0.6f,
                windupTime = 0.17f, strikeTime = 0.3f, recoverTime = 0.28f, lunge = 0.6f,
                leanWind = -4f, leanStrike = 0f, hopStrike = 0.15f,
                fxColor = new Color(1f, 0.82f, 0.95f), reach = 2.9f, halfArc = 180f,
                damage = 22f, stamina = 28f, shake = 0.22f, knock = 5f },

            new AttackDef { name = "Thrust", kind = AttackKind.Thrust, yawWind = 4f, yawStrike = 0f, pitchWind = -4f, pitchStrike = 0f,
                handWind = 0.05f, handStrike = 1.05f,
                windupTime = 0.13f, strikeTime = 0.07f, recoverTime = 0.22f, lunge = 6f,
                leanWind = -9f, leanStrike = 14f,
                fxColor = new Color(0.7f, 0.92f, 1f), reach = 3.7f, halfArc = 18f,
                damage = 20f, pierce = 0.5f, stamina = 20f, shake = 0.15f, knock = 6f },
        };

        public static readonly SkillDef[] Skills =
        {
            new SkillDef { name = "Whirlwind", key = "Q", hint = "Spin, 3 hits all around", cooldown = 6f, stamina = 25f, damage = 14f, color = new Color(0.45f, 0.85f, 1f) },
            new SkillDef { name = "Dash Thrust", key = "E", hint = "Dash through enemies", cooldown = 4f, stamina = 20f, damage = 30f, color = new Color(0.6f, 1f, 0.6f) },
            new SkillDef { name = "Shield Bash", key = "R", hint = "Cone stun + knockback", cooldown = 7f, stamina = 20f, damage = 12f, color = new Color(1f, 0.85f, 0.35f) },
            new SkillDef { name = "Ground Slam", key = "F", hint = "Leap to cursor, big AoE", cooldown = 10f, stamina = 35f, damage = 45f, color = new Color(1f, 0.5f, 0.3f) },
        };
        const float WhirlRadius = 3.2f, DashLength = 6f, BashReach = 2.9f, BashArc = 60f, BashStun = 2.0f, SlamRadius = 3.6f, SlamRange = 7f;

        public int pid;                                     // player slot 0..3 (0 = single player / the host)
        public string playerName = "Knight";
        public bool remote;                                 // another player's knight, shown here from network state
        public Vector3 spawnPoint;
        public Progress progress = new Progress();
        public readonly Inventory inventory = new Inventory();
        public readonly NetInterp interp = new NetInterp();
        public float hp = BaseHp, armor = BaseArmor, stamina = BaseStamina;
        public readonly float[] cd = new float[4], cdMax = new float[4];
        public bool IsAttacking { get; private set; }      // busy with a basic attack or a skill
        public bool IsBlocking { get; private set; }
        public bool Dead { get; private set; }
        public bool Airborne { get; private set; }
        public bool Invulnerable { get { return invuln || Glowing; } }
        // just revived, or picking a level-up card online: cannot be hurt, and glows
        public bool Glowing { get { return remote ? netGlow : Time.time < invulnUntil || Time.time < protectUntil; } }
        public float StaminaDeniedTime { get; private set; }
        public string LastAction { get; private set; }
        public int LastHitCount { get; private set; }
        public int ActionsSeen { get; private set; }       // attacks / skills started (local) or replayed (remote)
        public float MaxHp { get { return remote ? netMaxHp : BaseHp + HpPerLevel * (progress.level - 1) + progress.HpBonus; } }
        public float MaxArmor { get { return remote ? netMaxArmor : BaseArmor + progress.ArmorBonus; } }
        public float MaxStamina { get { return BaseStamina + progress.StaminaBonus; } }
        // Where the rules see this knight: a remote one is at his newest reported position, not the smoothed one on screen.
        public Vector3 Pos { get { return remote ? netPos : transform.position; } }
        public Vector3 Forward { get { return transform.forward; } }
        public WeaponDef Weapon { get { return inventory.Weapon; } }
        public Transform SwordModel { get { return swordModel; } }

        // --- combat numbers: level-up totals (Progress) combined with the equipped sword ---
        // skill: -1 = basic sword attack, 0..3 = Q / E / R / F. Shield Bash (2) does not use the sword.
        static bool UsesSword(int skill) { return skill != 2; }
        public float DamageMult(int skill) { return progress.DamageMult(skill) * (UsesSword(skill) ? inventory.Damage : 1f); }
        public float CritChance(int skill) { return Mathf.Min(Progress.CritChanceCap, progress.CritChance(skill) + (UsesSword(skill) ? Weapon.critChance : 0f)); }
        public float CritMult(int skill) { return Mathf.Min(Progress.CritMultCap, progress.CritMult(skill) + (UsesSword(skill) ? Weapon.critMult : 0f)); }
        public float SwingSpeed { get { return Mathf.Clamp(progress.BasicSpeed() * Weapon.speed, MinSwingSpeed, MaxSwingSpeed); } }
        public float Reach(AttackDef a) { return a.reach * Weapon.reach; }
        public float SwingCost(AttackDef a) { return a.stamina / SwingSpeed; }      // faster swings cost less each, a slow heavy sword costs more

        // The one place where the knight's damage is rolled (multipliers, crit chance, crit multiplier).
        public float RollDamage(int skill, float baseDamage, out bool isCrit)
        {
            float d = baseDamage * DamageMult(skill);
            isCrit = Progress.ForceCrit == 2 || (Progress.ForceCrit == 0 && Random.value < CritChance(skill));
            if (isCrit) d *= CritMult(skill);
            return d;
        }

        // self-test hooks: aim at a world point / walk in a direction without real input devices
        public Vector3? aimOverride;
        public Vector2? moveOverride;

        Transform rig, chest, legL, legR, swordPivot, swordNode, swordModel, armR, shieldNode;
        Tint tint;
        float swYaw = RestYaw, swPitch = RestPitch, swHand = RestHand;
        float rigLean, rigHop, rigSpin, jumpY, walkPhase, hurtTint;
        float lastSpendTime = -10f, lastDamageTime = -10f, lastAttackEnd = -10f, invulnUntil, protectUntil;
        bool invuln, lockFacing, whirl, shieldPush;
        float shieldPushK;
        Vector3 knockVel, aimPoint;
        int comboIndex, queued = -1;
        float queuedTime;

        // remote knights only
        Vector3 netPos, slamTarget;
        float netMaxHp = BaseHp, netMaxArmor = BaseArmor, remoteSpeed, netTime, netYaw;
        bool netGlow, netSynced;
        Coroutine act;

        void Awake()
        {
            StaminaDeniedTime = -10f;
            LastAction = "";
        }

        // Called once, right after AddComponent.
        public void Init(int playerId, string name, bool isRemote)
        {
            pid = playerId;
            playerName = name;
            remote = isRemote;
            spawnPoint = Game.SpawnPointFor(pid);
            Build();
            transform.position = spawnPoint;
            netPos = spawnPoint;
            interp.Snap(spawnPoint, 0f);
            interp.delay = Net.KnightDelay;
        }

        // ---------------------------------------------------------------- model

        void Build()
        {
            Material steel = Prims.Lit(new Color(0.74f, 0.77f, 0.82f), 0.7f, 0.75f);
            Material darkSteel = Prims.Lit(new Color(0.42f, 0.45f, 0.52f), 0.55f, 0.6f);
            Material blue = Prims.Lit(MainColors[pid & 3]);       // tabard + shield: the player's colour
            Material red = Prims.Lit(AccentColors[pid & 3]);      // cape + plume
            Material gold = Prims.Lit(new Color(0.96f, 0.78f, 0.22f), 0.7f, 0.8f);
            Material leather = Prims.Lit(new Color(0.35f, 0.22f, 0.12f));
            Material black = Prims.Lit(new Color(0.06f, 0.06f, 0.08f));

            rig = Prims.Node(transform, "Rig", Vector3.zero);

            legL = Prims.Node(rig, "LegL", new Vector3(-0.17f, 0.75f, 0f));
            legR = Prims.Node(rig, "LegR", new Vector3(0.17f, 0.75f, 0f));
            foreach (Transform leg in new[] { legL, legR })
            {
                Prims.Box(leg, "Leg", new Vector3(0f, -0.36f, 0f), new Vector3(0.24f, 0.72f, 0.28f), darkSteel);
                Prims.Box(leg, "Boot", new Vector3(0f, -0.68f, 0.05f), new Vector3(0.27f, 0.14f, 0.38f), leather);
            }

            chest = Prims.Node(rig, "Chest", new Vector3(0f, 0.75f, 0f));
            Prims.Box(chest, "Cuirass", new Vector3(0f, 0.36f, 0f), new Vector3(0.68f, 0.62f, 0.42f), steel);
            Prims.Box(chest, "Tabard", new Vector3(0f, 0.3f, 0f), new Vector3(0.44f, 0.72f, 0.47f), blue);
            Prims.Box(chest, "Emblem", new Vector3(0f, 0.42f, 0.24f), new Vector3(0.18f, 0.18f, 0.03f), gold);
            Prims.Box(chest, "Belt", new Vector3(0f, 0.08f, 0f), new Vector3(0.7f, 0.1f, 0.49f), leather);
            Prims.Part(chest, "PauldronL", Prims.Ball(6), new Vector3(-0.44f, 0.66f, 0f), new Vector3(0.4f, 0.3f, 0.44f), steel);
            Prims.Part(chest, "PauldronR", Prims.Ball(6), new Vector3(0.44f, 0.66f, 0f), new Vector3(0.4f, 0.3f, 0.44f), steel);
            Prims.Box(chest, "Cape", new Vector3(0f, 0.3f, -0.27f), new Vector3(0.5f, 0.62f, 0.05f), red).Rot(10f, 0f, 0f);
            Mesh helm = Prims.Lathe("helm", 8, 0f, 0f, 0.25f, 0f, 0.29f, 0.2f, 0.25f, 0.38f, 0.12f, 0.5f, 0f, 0.52f);
            Prims.Part(chest, "Helm", helm, new Vector3(0f, 0.68f, 0f), Vector3.one, steel);
            Prims.Box(chest, "Visor", new Vector3(0f, 0.9f, 0.25f), new Vector3(0.3f, 0.06f, 0.08f), black);
            Prims.Box(chest, "Plume", new Vector3(0f, 1.26f, -0.06f), new Vector3(0.09f, 0.22f, 0.5f), red);
            Prims.Box(chest, "PlumeTail", new Vector3(0f, 1.14f, -0.34f), new Vector3(0.09f, 0.3f, 0.16f), red).Rot(-25f, 0f, 0f);

            // sword arm: the pivot sits in the chest and turns the whole arm + sword
            swordPivot = Prims.Node(rig, "SwordPivot", new Vector3(0.12f, 1.2f, 0.05f));
            armR = Prims.Box(swordPivot, "ArmR", new Vector3(0f, 0f, 0.2f), new Vector3(0.17f, 0.17f, 0.4f), darkSteel);
            swordNode = Prims.Node(swordPivot, "Sword", new Vector3(0f, 0f, RestHand));
            Prims.Part(swordNode, "Gauntlet", Prims.Ball(6), Vector3.zero, new Vector3(0.24f, 0.24f, 0.24f), steel);
            swordModel = Weapons.BuildModel(swordNode, Weapon);       // the equipped sword; replaced by Equip()
            swordModel.name = Weapon.name;

            // shield arm
            shieldNode = Prims.Node(rig, "Shield", ShieldRestPos);
            Vector2[] l = { new Vector2(-0.36f, 0.42f), new Vector2(-0.36f, 0.05f), new Vector2(-0.25f, -0.3f), new Vector2(-0.02f, -0.56f) };
            Vector2[] r = { new Vector2(0.36f, 0.42f), new Vector2(0.36f, 0.05f), new Vector2(0.25f, -0.3f), new Vector2(0.02f, -0.56f) };
            Prims.Part(shieldNode, "Plate", Prims.Strip("shield", l, r, 0.08f), Vector3.zero, Vector3.one, blue);
            Prims.Box(shieldNode, "Top", new Vector3(0f, 0.42f, 0f), new Vector3(0.76f, 0.07f, 0.11f), gold);
            Prims.Box(shieldNode, "CrossV", new Vector3(0f, -0.04f, 0.03f), new Vector3(0.11f, 0.86f, 0.06f), gold);
            Prims.Box(shieldNode, "CrossH", new Vector3(0f, 0.14f, 0.03f), new Vector3(0.66f, 0.11f, 0.06f), gold);
            Prims.Box(shieldNode, "ArmL", new Vector3(0f, 0.05f, -0.16f), new Vector3(0.17f, 0.17f, 0.3f), darkSteel);

            tint = new Tint(rig);
        }

        // ---------------------------------------------------------------- weapon slot / stat points

        // Puts an owned sword into the weapon slot and swaps the model in the knight's hand.
        public bool Equip(int id)
        {
            if (id < 0 || id >= Weapons.All.Length || !inventory.owned[id]) return false;
            inventory.equipped = id;
            RebuildSword();
            return true;
        }

        void RebuildSword()
        {
            tint.Set(Color.white, 0f);
            if (swordModel != null)
            {
                swordModel.gameObject.SetActive(false);
                swordModel.SetParent(null);                 // Destroy is delayed; the new Tint must not see the old parts
                Destroy(swordModel.gameObject);
            }
            swordModel = Weapons.BuildModel(swordNode, Weapon);
            swordModel.name = Weapon.name;
            tint = new Tint(rig);
        }

        // Spends one stat point; the new maximum is filled in right away.
        public bool SpendPoint(Attr a)
        {
            if (!progress.Spend(a)) return false;
            if (a == Attr.Hp) hp = Mathf.Min(MaxHp, hp + Progress.HpPerPoint);
            else if (a == Attr.Block) armor = Mathf.Min(MaxArmor, armor + Progress.ArmorPerPoint);
            else stamina = Mathf.Min(MaxStamina, stamina + Progress.StaminaPerPoint);
            return true;
        }

        // F5: back to level 1, no upgrades, no stat points, only the starting sword.
        public void ResetAll()
        {
            StopAllCoroutines();
            progress = new Progress();
            inventory.Reset();
            RebuildSword();
            hp = MaxHp; armor = MaxArmor; stamina = MaxStamina;
            Dead = false; IsAttacking = false; IsBlocking = false; Airborne = false;
            invuln = false; lockFacing = false; whirl = false; shieldPush = false;
            queued = -1; comboIndex = 0; knockVel = Vector3.zero; hurtTint = 0f;
            rigLean = 0f; rigHop = 0f; rigSpin = 0f; jumpY = 0f;
            swYaw = RestYaw; swPitch = RestPitch; swHand = RestHand;
            for (int i = 0; i < 4; i++) { cd[i] = 0f; cdMax[i] = 0f; }
            invulnUntil = 0f; protectUntil = 0f;
            transform.position = spawnPoint;
            transform.rotation = Quaternion.identity;
        }

        public void OnLevelUp()
        {
            hp = MaxHp; armor = MaxArmor; stamina = MaxStamina;
            LevelUpFx();
        }

        void LevelUpFx()
        {
            Vector3 g = new Vector3(transform.position.x, 0.06f, transform.position.z);
            Fx.I.Ring(g, 0.5f, 5f, new Color(1f, 0.9f, 0.35f, 0.9f), 0.7f);
            Fx.I.Ring(g, 0.3f, 3f, new Color(1f, 1f, 0.8f, 0.9f), 0.5f);
            Fx.I.Burst(transform.position + Vector3.up, 30, new Color(1f, 0.9f, 0.4f), 2f, 8f, Vector3.up, 0.6f, 0.9f, 6f, 0.14f);
        }

        // Online: a short spell of invulnerability while the level-up panel is open (seconds <= 0 ends it).
        public void Protect(float seconds)
        {
            protectUntil = seconds > 0f ? Time.time + seconds : 0f;
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.deltaTime;
            if (remote) { RemoteTick(dt); return; }
            if (Game.I.Paused) return;

            // --- resources ---
            if (!Dead)
            {
                if (!IsBlocking && Time.time - lastSpendTime > 0.6f)
                    stamina = Mathf.Min(MaxStamina, stamina + StaminaRegen * progress.StaminaRegenMult * dt);
                if (Time.time - lastDamageTime > 4f)
                    armor = Mathf.Min(MaxArmor, armor + 8f * dt);
            }
            for (int i = 0; i < 4; i++) cd[i] = Mathf.Max(0f, cd[i] - dt);

            // an open panel (online: the game is not paused) takes the combat keys and the mouse; walking still works
            bool canInput = !Dead && Time.unscaledTime >= Game.I.inputBlockUntil && !Game.I.PanelOpen;
            Simulate(ReadInput(canInput), canInput, dt);
        }

        // The only place where the knight looks at the keyboard and mouse (or at the self-test's overrides).
        KnightInput ReadInput(bool canInput)
        {
            var inp = new KnightInput { request = -1 };
            if (!Dead) inp.move = moveOverride.HasValue ? moveOverride.Value : InputX.Move();
            inp.aim = aimOverride.HasValue ? aimOverride.Value : Game.I.AimPoint();
            if (!canInput) return inp;
            inp.block = InputX.Held(KeyCode.LeftShift) || InputX.MouseHeld(1);
            if (InputX.Down(KeyCode.Q)) inp.request = SkillRequest;
            else if (InputX.Down(KeyCode.E)) inp.request = SkillRequest + 1;
            else if (InputX.Down(KeyCode.R)) inp.request = SkillRequest + 2;
            else if (InputX.Down(KeyCode.F)) inp.request = SkillRequest + 3;
            else if (InputX.MouseDown(0) || InputX.Down(KeyCode.J) || InputX.Down(KeyCode.Space)) inp.request = ComboRequest;
            return inp;
        }

        // One frame of the local knight: actions, facing, movement, pose.
        void Simulate(KnightInput inp, bool canInput, float dt)
        {
            Vector2 mv = inp.move;
            aimPoint = inp.aim;
            IsBlocking = canInput && !IsAttacking && inp.block;

            if (canInput)
            {
                int req = inp.request;
                if (req >= 0)
                {
                    if (IsAttacking) { queued = req; queuedTime = Time.time; }   // buffer the next action
                    else Request(req);
                }
                else if (!IsAttacking && queued >= 0)
                {
                    int q = queued;
                    queued = -1;
                    if (Time.time - queuedTime < 0.45f) Request(q);
                }
            }

            // --- facing: always towards the cursor, unless an attack has committed to a direction ---
            if (!Dead && !lockFacing)
            {
                Vector3 aim = inp.aim - transform.position;
                aim.y = 0f;
                if (aim.sqrMagnitude > 0.05f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(aim), 1100f * dt);
            }

            // --- movement on the XZ plane ---
            float speedMul = whirl ? 0.7f : IsAttacking ? 0.2f : IsBlocking ? 0.4f : 1f;
            if (lockFacing && IsAttacking && !whirl) speedMul = Airborne ? 0f : 0.15f;
            Vector3 p = transform.position;
            p.y = 0f;
            p += new Vector3(mv.x, 0f, mv.y) * MoveSpeed * speedMul * dt;
            p += knockVel * dt;
            knockVel = Vector3.MoveTowards(knockVel, Vector3.zero, 30f * dt);

            // solid enemies (dummy, minotaur) push the knight out (not while leaping or dashing through)
            if (!Airborne && !invuln)
            {
                for (int i = 0; i < Enemy.All.Count; i++)
                {
                    Enemy e = Enemy.All[i];
                    if (!e.solid || e.Dead) continue;
                    Vector3 d = p - e.transform.position; d.y = 0f;
                    float min = e.radius + 0.4f;
                    if (d.sqrMagnitude >= min * min) continue;
                    if (d.sqrMagnitude < 0.0001f) d = -transform.forward;
                    p = new Vector3(e.transform.position.x, 0f, e.transform.position.z) + d.normalized * min;
                }
            }
            float lim = Game.ArenaHalf - 0.4f;
            p.x = Mathf.Clamp(p.x, -lim, lim);
            p.z = Mathf.Clamp(p.z, -lim, lim);
            p.y = jumpY;
            transform.position = p;

            Pose(dt, mv.sqrMagnitude > 0.01f && !Airborne);
        }

        // Idle / walk / block poses, legs, shield and tint (attacks drive the sword and body themselves).
        // Shared by the local knight and by remote knights.
        void Pose(float dt, bool moving)
        {
            if (moving) walkPhase += dt * 13f * (whirl ? 0.5f : 1f);
            if (!IsAttacking && !Dead)
            {
                float e = 1f - Mathf.Exp(-14f * dt);
                swYaw = Mathf.Lerp(swYaw, IsBlocking ? 80f : RestYaw + (moving ? Mathf.Sin(walkPhase) * 6f : 0f), e);
                swPitch = Mathf.Lerp(swPitch, IsBlocking ? -55f : RestPitch + Mathf.Sin(Time.time * 2.2f) * 2f, e);
                swHand = Mathf.Lerp(swHand, RestHand, e);
                float bob = moving ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.07f : Mathf.Sin(Time.time * 2.2f) * 0.015f;
                rigHop = Mathf.Lerp(rigHop, bob, 1f - Mathf.Exp(-20f * dt));
                rigLean = Mathf.Lerp(rigLean, moving ? 6f : 0f, 1f - Mathf.Exp(-12f * dt));
                rigSpin = 0f;
            }
            float swing = moving && !Dead ? Mathf.Sin(walkPhase) * 32f : 0f;
            legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(swing, 0f, 0f), 1f - Mathf.Exp(-18f * dt));
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(-swing, 0f, 0f), 1f - Mathf.Exp(-18f * dt));

            // shield
            float sk = 1f - Mathf.Exp(-18f * dt);
            shieldPushK = Mathf.MoveTowards(shieldPushK, shieldPush ? 1f : 0f, dt * (shieldPush ? 14f : 6f));
            Vector3 sPos = IsBlocking ? ShieldBlockPos : ShieldRestPos;
            Quaternion sRot = IsBlocking ? Quaternion.Euler(-12f, 5f, 0f) : Quaternion.Euler(-18f, -72f, 0f);
            float sScale = IsBlocking ? 1.3f : 1f;
            if (shieldPushK > 0.001f)
            {
                sPos = Vector3.Lerp(sPos, new Vector3(0f, 1.15f, 1.05f), shieldPushK);
                sRot = Quaternion.Slerp(sRot, Quaternion.Euler(-8f, 0f, 0f), shieldPushK);
                sScale = Mathf.Lerp(sScale, 1.6f, shieldPushK);
                shieldNode.localPosition = sPos; shieldNode.localRotation = sRot; shieldNode.localScale = Vector3.one * sScale;
            }
            else
            {
                shieldNode.localPosition = Vector3.Lerp(shieldNode.localPosition, sPos, sk);
                shieldNode.localRotation = Quaternion.Slerp(shieldNode.localRotation, sRot, sk);
                shieldNode.localScale = Vector3.Lerp(shieldNode.localScale, Vector3.one * sScale, sk);
            }

            hurtTint = Mathf.Max(0f, hurtTint - dt / 0.3f);
            if (Glowing && !Dead) tint.Set(new Color(1f, 1f, 0.7f), 0.35f + 0.25f * Mathf.Sin(Time.time * 25f));
            else tint.Set(new Color(1f, 0.25f, 0.2f), hurtTint * 0.8f);
        }

        // ---------------------------------------------------------------- remote knight (another player)

        // State bits sent over the network.
        public const int NetBlocking = 1, NetDead = 2, NetAirborne = 4, NetGlow = 8;

        public int NetFlags
        {
            get { return (IsBlocking ? NetBlocking : 0) | (Dead ? NetDead : 0) | (Airborne ? NetAirborne : 0) | (Glowing ? NetGlow : 0); }
        }

        // The time stamp and facing that go out with this knight's state: his own for the local knight, the last
        // reported ones for a remote knight (the host passes other players' reports on unchanged).
        public float NetTime { get { return remote ? netTime : Time.unscaledTime; } }
        public float NetYaw { get { return remote ? netYaw : transform.eulerAngles.y; } }

        // Every frame: glide to the reported position and keep the idle / walk / block pose going.
        void RemoteTick(float dt)
        {
            Vector3 p; float yaw;
            if (interp.Sample(Time.unscaledTime, out p, out yaw))
            {
                Vector3 d = p - transform.position; d.y = 0f;
                if (dt > 0f) remoteSpeed = Mathf.Lerp(remoteSpeed, d.magnitude / dt, 0.3f);
                transform.position = p;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            Pose(dt, remoteSpeed > 1f && !Airborne && !Dead);
        }

        // A state report from that player's machine (sender time is his clock).
        public void ApplyNet(float senderTime, Vector3 pos, float yaw, int flags, float newHp, float newMaxHp, float newArmor, float newMaxArmor,
            int level, int weapon, int plus)
        {
            if (!remote) return;
            netPos = pos; netYaw = yaw; netTime = senderTime;
            interp.Push(senderTime, Time.unscaledTime, pos, yaw);
            bool wasDead = Dead, nowDead = (flags & NetDead) != 0;
            IsBlocking = (flags & NetBlocking) != 0 && !nowDead;
            Airborne = (flags & NetAirborne) != 0;
            netGlow = (flags & NetGlow) != 0;

            float before = hp + armor;
            hp = newHp; armor = newArmor; netMaxHp = newMaxHp; netMaxArmor = newMaxArmor;
            if (netSynced && !wasDead && hp + armor < before - 0.5f) RemoteHurt();

            if (netSynced && level > progress.level) LevelUpFx();
            progress.level = Mathf.Clamp(level, 1, Progress.MaxLevel);

            if (weapon >= 0 && weapon < Weapons.All.Length && (weapon != inventory.equipped || plus != inventory.plus[weapon]))
            {
                inventory.owned[weapon] = true;
                inventory.plus[weapon] = Mathf.Clamp(plus, 0, Weapons.MaxPlus);
                if (weapon != inventory.equipped) { inventory.equipped = weapon; RebuildSword(); }
            }

            if (nowDead && !wasDead) RemoteDown();
            else if (!nowDead && wasDead) RemoteRevive();
            netSynced = true;
        }

        // The other player lost health or armor: flash and a few sparks (the numbers are shown on his own screen).
        void RemoteHurt()
        {
            hurtTint = 1f;
            Vector3 chestPos = transform.position + new Vector3(0f, 1.4f, 0f);
            Fx.I.Burst(chestPos, 6, new Color(1f, 0.3f, 0.25f), 2f, 6f, Vector3.up, 0.9f, 0.3f, 8f, 0.1f);
        }

        void StopAct()
        {
            if (act != null) StopCoroutine(act);
            act = null;
            IsAttacking = false; whirl = false; shieldPush = false; lockFacing = false;
            rigSpin = 0f;
        }

        void RemoteDown()
        {
            StopAllCoroutines();
            StopAct();
            Dead = true;
            IsBlocking = false;
            StartCoroutine(FallRoutine());
        }

        void RemoteRevive()
        {
            StopAllCoroutines();
            StopAct();
            Dead = false;
            rigLean = 0f; rigHop = 0f;
            swYaw = RestYaw; swPitch = RestPitch; swHand = RestHand;
            interp.Snap(netPos, transform.eulerAngles.y);
            transform.position = netPos;
            Sfx.I.Play(Snd.Ready, Game.I.RemoteVolume(netPos));
            Fx.I.Ring(new Vector3(netPos.x, 0.06f, netPos.z), 0.4f, 3.5f, new Color(0.7f, 0.9f, 1f, 0.9f), 0.6f);
            if (Net.IsHost) Game.I.PushEnemiesFrom(spawnPoint);       // same courtesy the local knight gets
        }

        // Replays an attack / skill of the remote player for show. kind: 0..4 = sword attack, 10..13 = skill.
        // It starts one interpolation delay late, so it lines up with the delayed position on screen.
        public void PlayRemoteAct(int kind, float speed, Vector3 target)
        {
            if (!remote || Dead) return;
            bool skill = kind >= 10;
            int index = skill ? kind - 10 : kind;
            if (index < 0 || index >= (skill ? Skills.Length : Attacks.Length)) return;
            StopAct();
            slamTarget = target;
            LastAction = skill ? Skills[index].name : Attacks[index].name;
            ActionsSeen++;
            act = StartCoroutine(RemoteAct(skill, index, Mathf.Clamp(speed, MinSwingSpeed, MaxSwingSpeed)));
        }

        IEnumerator RemoteAct(bool skill, int index, float speed)
        {
            if (interp.delay > 0f) yield return new WaitForSeconds(interp.delay);
            if (!skill) yield return AttackRoutine(Attacks[index], speed);
            else
            {
                Sfx.I.Grunt(true, Volume);
                if (index == 0) yield return Whirlwind(Skills[0]);
                else if (index == 1) yield return DashThrust(Skills[1]);
                else if (index == 2) yield return ShieldBash(Skills[2]);
                else yield return GroundSlam(Skills[3]);
            }
            act = null;
        }

        // Other players' swings and grunts are quieter and fade with distance.
        float Volume { get { return remote ? Game.I.RemoteVolume(transform.position) : 1f; } }

        void LateUpdate()
        {
            rig.localPosition = new Vector3(0f, rigHop, 0f);
            if (!Dead) rig.localRotation = Quaternion.Euler(rigLean, rigSpin, 0f);
            swordPivot.localRotation = Quaternion.Euler(swPitch, swYaw, 0f);
            swordNode.localPosition = new Vector3(0f, 0f, swHand);
            armR.localPosition = new Vector3(0f, 0f, swHand * 0.5f);
            armR.localScale = new Vector3(0.17f, 0.17f, Mathf.Max(0.1f, swHand));
        }

        // ---------------------------------------------------------------- requests

        void Request(int req)
        {
            if (req >= SkillRequest) TrySkill(req - SkillRequest);
            else TryStartAttack(req);
        }

        // Used by SelfTest to trigger actions without input devices.
        public void DebugAttack(int index) { if (!Dead && !IsAttacking) TryStartAttack(index); }
        public bool DebugSkill(int index) { return !Dead && !IsAttacking && TrySkill(index); }

        void Deny(string what)
        {
            StaminaDeniedTime = Time.unscaledTime;
            Game.I.hud.Banner("Not enough stamina for " + what, new Color(1f, 0.5f, 0.4f));
        }

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
            float speed = SwingSpeed;
            float cost = SwingCost(a);
            if (stamina < cost) { Deny(a.name); return; }
            stamina -= cost;
            lastSpendTime = Time.time;
            if (fromCombo) comboIndex = (index + 1) % Attacks.Length;
            LastAction = a.name;
            ActionsSeen++;
            Game.I.hud.Action((index + 1) + "  " + a.name, a.fxColor);
            Net.SendAct(index, speed, Vector3.zero);
            StartCoroutine(AttackRoutine(a, speed));
        }

        bool TrySkill(int i)
        {
            SkillDef s = Skills[i];
            if (cd[i] > 0f)
            {
                Game.I.hud.Action(s.name + " not ready (" + cd[i].ToString("0.0") + "s)", new Color(1f, 0.6f, 0.5f));
                return false;
            }
            if (stamina < s.stamina) { Deny(s.name); return false; }
            stamina -= s.stamina;
            lastSpendTime = Time.time;
            cdMax[i] = cd[i] = s.cooldown / progress.SpeedMult(i);
            LastAction = s.name;
            ActionsSeen++;
            Game.I.hud.Action(s.key + "  " + s.name, s.color);
            Sfx.I.Grunt(true);
            if (i != 3) Net.SendAct(10 + i, 1f, Vector3.zero);      // the Ground Slam reports itself once it knows where it lands
            switch (i)
            {
                case 0: StartCoroutine(Whirlwind(s)); break;
                case 1: StartCoroutine(DashThrust(s)); break;
                case 2: StartCoroutine(ShieldBash(s)); break;
                default: StartCoroutine(GroundSlam(s)); break;
            }
            return true;
        }

        // ---------------------------------------------------------------- hits

        // Damages every enemy inside a sector. skill: -1 = basic attack, 0..3 = Q/E/R/F (picks the upgrade totals).
        // src is what the host is told: 0..4 = sword attack, 10..13 = skill. A remote knight's replay hits nothing.
        int HitArea(Vector3 origin, Vector3 dir, float reach, float halfArc, int skill, float baseDamage, float pierce,
            float knock, float stun, string source, int src, HashSet<Enemy> once = null)
        {
            if (remote) return 0;
            int n = 0;
            bool flesh = false, armored = false;
            Snd armorSnd = Snd.ArmorImpact;
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                if (i >= Enemy.All.Count) continue;
                Enemy e = Enemy.All[i];
                if (e.Dead) continue;
                Vector3 to = e.transform.position - origin; to.y = 0f;
                float d = to.magnitude;
                if (d - e.radius > reach) continue;
                if (halfArc < 179f && d > e.radius + 0.25f)
                {
                    float pad = Mathf.Asin(Mathf.Clamp01(e.radius / d)) * Mathf.Rad2Deg;
                    if (Vector3.Angle(dir, to) > halfArc + pad) continue;
                }
                if (once != null && !once.Add(e)) continue;
                bool crit;
                float dmg = RollDamage(skill, baseDamage, out crit);
                Game.I.HitEnemy(this, e, dmg, pierce, origin, crit, knock, stun, source, src);
                if (e.lastToArmor > e.lastToHp) { armored = true; armorSnd = e.ArmorSound; }   // the armor took most of it
                else flesh = true;
                n++;
            }
            LastHitCount = n;
            // one sound per kind for the whole swing, however many enemies it went through
            if (skill == 2) { if (n > 0) Sfx.I.Play(Snd.ArmorImpact, 1f, 0.8f); }              // the shield, not the blade
            else
            {
                if (flesh) Sfx.I.Play(Snd.Slice);
                if (armored) Sfx.I.Play(armorSnd);
            }
            return n;
        }

        // The whoosh of the equipped sword: heavier (slower) swords sound lower.
        void SwingSound(float volume = 1f)
        {
            Sfx.I.Play(Snd.Swing, volume * Volume, Mathf.Lerp(0.82f, 1.18f, Mathf.InverseLerp(0.7f, 1.4f, Weapon.speed)));
        }

        // Camera shake and hit-stop belong to the player who did it.
        void Juice(float shake, float shakeTime, float stop)
        {
            if (remote) return;
            Game.I.Shake(shake, shakeTime);
            if (stop > 0f) Game.I.HitStop(stop);
        }

        // Attack lunges and dashes move the local knight only; a remote one is moved by his reported position.
        void Push(Vector3 delta)
        {
            if (!remote) transform.position += delta;
        }

        Vector3 Ground(float y) { return new Vector3(transform.position.x, y, transform.position.z); }

        // ---------------------------------------------------------------- basic attacks

        IEnumerator AttackRoutine(AttackDef a, float speed)
        {
            IsAttacking = true;
            IsBlocking = false;
            lockFacing = true;
            Vector3 dir = transform.forward;

            // 1) wind-up
            float y0 = swYaw, p0 = swPitch, h0 = swHand, lean0 = rigLean, hop0 = rigHop;
            for (float t = 0; t < 1f; t += Time.deltaTime * speed / a.windupTime)
            {
                float e = 1f - (1f - t) * (1f - t);                       // ease out
                swYaw = Mathf.Lerp(y0, a.yawWind, e);
                swPitch = Mathf.Lerp(p0, a.pitchWind, e);
                swHand = Mathf.Lerp(h0, a.handWind, e);
                rigLean = Mathf.Lerp(lean0, a.leanWind, e);
                rigHop = Mathf.Lerp(hop0, a.hopWind, e);
                yield return null;
            }

            // 2) strike
            dir = transform.forward;
            SpawnAttackFx(a);
            SwingSound();
            Sfx.I.Grunt(a.damage >= 16f, Volume);                     // chop / spin / thrust always, light slashes sometimes
            if (a.lunge > 2.5f) Fx.I.Dust(transform.position, 5);
            bool hitDone = false;
            for (float t = 0; t < 1f; t += Time.deltaTime * speed / a.strikeTime)
            {
                float e = a.kind == AttackKind.Spin ? t : t * t;          // spin turns evenly, the rest accelerate
                swYaw = Mathf.Lerp(a.yawWind, a.yawStrike, e);
                swPitch = Mathf.Lerp(a.pitchWind, a.pitchStrike, e);
                swHand = Mathf.Lerp(a.handWind, a.handStrike, e);
                rigLean = Mathf.Lerp(a.leanWind, a.leanStrike, e);
                rigHop = Mathf.Lerp(a.hopWind, 0f, e) + Mathf.Sin(t * Mathf.PI) * a.hopStrike;
                if (a.kind == AttackKind.Spin) rigSpin = -360f * t;       // whole body turns once
                Push(dir * a.lunge * Time.deltaTime);

                if (!hitDone && t > (a.kind == AttackKind.Spin ? 0.2f : 0.35f)) { DoHit(a, dir); hitDone = true; }
                yield return null;
            }
            if (!hitDone) DoHit(a, dir);
            rigSpin = 0f;
            if (a.kind == AttackKind.Chop)   // overhead chop slams the ground
            {
                Vector3 g = Ground(0.05f) + dir * 2.2f;
                Fx.I.Dust(g, 10);
                Fx.I.Ring(g, 0.3f, 1.3f, new Color(1f, 0.95f, 0.75f, 0.7f), 0.25f);
                Juice(a.shake * 0.5f, 0.12f, 0f);
            }

            // 3) recover
            lockFacing = false;
            float ys = swYaw, ps = swPitch, hs = swHand;
            for (float t = 0; t < 1f; t += Time.deltaTime * speed / a.recoverTime)
            {
                float e = t * t * (3f - 2f * t);                          // smoothstep
                swYaw = Mathf.Lerp(ys, RestYaw, e);
                swPitch = Mathf.Lerp(ps, RestPitch, e);
                swHand = Mathf.Lerp(hs, RestHand, e);
                rigLean = Mathf.Lerp(a.leanStrike, 0f, e);
                rigHop = Mathf.Lerp(rigHop, 0f, e);
                if (queued >= 0 && t > 0.45f) break;                       // a buffered action cancels the tail
                yield return null;
            }
            IsAttacking = false;
            lastAttackEnd = Time.time;
        }

        void SpawnAttackFx(AttackDef a)
        {
            float yaw = transform.eulerAngles.y;
            Vector3 pos = Ground(1.05f);
            Color c = Color.Lerp(a.fxColor, Weapon.uiColor, Weapon.id == Weapons.Iron ? 0f : 0.45f); c.a = 0.9f;
            float reach = Reach(a);
            switch (a.kind)
            {
                case AttackKind.Thrust:
                    Fx.I.Streak(pos + transform.forward * 0.6f, yaw, reach - 0.3f, 0.45f, c, 0.2f);
                    Fx.I.Burst(pos + transform.forward * 1.2f, 7, new Color(0.75f, 0.92f, 1f), 6f, 13f, transform.forward, 0.15f, 0.18f, 0f, 0.08f);
                    break;
                case AttackKind.Spin:
                    Fx.I.Arc(pos, yaw, 1.1f, reach, 360f, 0f, c, 0.36f, 0.1f, -500f, transform);
                    Fx.I.Dust(transform.position, 10);
                    break;
                case AttackKind.Chop:
                    Fx.I.Streak(pos + transform.forward * 0.5f, yaw, reach - 0.2f, 0.8f, c, 0.22f);
                    break;
                default:
                    Fx.I.Arc(pos, yaw, 1.0f, reach, a.yawWind, a.yawStrike, c, 0.22f, 0.12f);
                    break;
            }
        }

        void DoHit(AttackDef a, Vector3 dir)
        {
            int n = HitArea(transform.position, dir, Reach(a), a.halfArc, -1, a.damage, a.pierce, a.knock, 0f, a.name, System.Array.IndexOf(Attacks, a));
            if (n == 0) return;
            Juice(a.shake, 0.16f, a.damage >= 16f ? 0.075f : 0.05f);
        }

        // ---------------------------------------------------------------- skills

        // Q: spin for 0.9 s (can still walk), three hits on everything around.
        IEnumerator Whirlwind(SkillDef s)
        {
            IsAttacking = true; whirl = true;
            int ticks = 0;
            Color c = s.color; c.a = 0.85f;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.9f)
            {
                rigSpin = -1080f * t;
                swYaw = 95f; swPitch = 0f; swHand = Mathf.Lerp(swHand, 0.65f, 0.3f);
                rigLean = 0f; rigHop = 0.05f;
                if (ticks < 3 && t >= 0.06f + ticks * 0.32f)
                {
                    ticks++;
                    SwingSound();
                    Fx.I.Arc(Ground(1.0f), Random.Range(0f, 360f), 1.2f, WhirlRadius, 360f, 0f, c, 0.3f, 0.08f, -700f, transform);
                    Fx.I.Dust(transform.position, 5);
                    if (HitArea(transform.position, transform.forward, WhirlRadius, 180f, 0, s.damage, 0f, 3f, 0f, s.name, 10) > 0)
                        Juice(0.14f, 0.14f, 0.04f);
                }
                yield return null;
            }
            rigSpin = 0f; whirl = false;
            IsAttacking = false;
            lastAttackEnd = Time.time;
        }

        // E: short wind-up, then a 6 m dash that pierces everything on the way. Invulnerable while dashing.
        IEnumerator DashThrust(SkillDef s)
        {
            IsAttacking = true; lockFacing = true;
            Vector3 dir = transform.forward;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.08f)
            {
                swYaw = Mathf.Lerp(swYaw, 4f, 0.5f); swPitch = Mathf.Lerp(swPitch, -3f, 0.5f); swHand = Mathf.Lerp(swHand, 0.05f, 0.5f);
                rigLean = Mathf.Lerp(rigLean, -10f, 0.5f);
                yield return null;
            }
            dir = transform.forward;
            if (!remote) invuln = true;
            Color c = s.color; c.a = 0.9f;
            Fx.I.Streak(Ground(1.0f), transform.eulerAngles.y, DashLength + 2.2f, 0.9f, c, 0.35f);
            Fx.I.Dust(transform.position, 8);
            SwingSound();
            var once = new HashSet<Enemy>();
            int total = 0;
            const float dur = 0.16f;
            for (float t = 0; t < 1f; t += Time.deltaTime / dur)
            {
                swYaw = 0f; swPitch = 0f; swHand = 1.05f; rigLean = 16f;
                Push(dir * (DashLength / dur) * Time.deltaTime);
                total += HitArea(transform.position + dir * 0.8f, dir, 1.5f, 180f, 1, s.damage, 0.5f, 5f, 0f, s.name, 11, once);
                yield return null;
            }
            LastHitCount = total;
            if (total > 0) Juice(0.2f, 0.18f, 0.06f);
            invuln = false;
            lockFacing = false;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.18f)
            {
                rigLean = Mathf.Lerp(16f, 0f, t);
                swHand = Mathf.Lerp(1.05f, RestHand, t);
                if (queued >= 0 && t > 0.4f) break;
                yield return null;
            }
            IsAttacking = false;
            lastAttackEnd = Time.time;
        }

        // R: slam the shield forward. Small damage, but stuns and throws back everything in a cone.
        IEnumerator ShieldBash(SkillDef s)
        {
            IsAttacking = true; lockFacing = true;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.09f)
            {
                rigLean = Mathf.Lerp(rigLean, -8f, 0.5f);
                yield return null;
            }
            Vector3 dir = transform.forward;
            shieldPush = true;
            Color c = s.color; c.a = 0.9f;
            Fx.I.Arc(Ground(1.0f), transform.eulerAngles.y, 0.7f, BashReach, -BashArc, BashArc, c, 0.25f, 0.2f);
            Fx.I.Arc(Ground(1.0f), transform.eulerAngles.y, 0.7f, BashReach, BashArc, -BashArc, c, 0.25f, 0.2f);
            bool hitDone = false;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.13f)
            {
                rigLean = 14f;
                Push(dir * 5f * Time.deltaTime);
                if (!hitDone && t > 0.3f)
                {
                    hitDone = true;
                    if (HitArea(transform.position, dir, BashReach, BashArc, 2, s.damage, 0f, 10f, BashStun, s.name, 12) > 0)
                        Juice(0.22f, 0.18f, 0.07f);
                }
                yield return null;
            }
            shieldPush = false;
            lockFacing = false;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.2f)
            {
                rigLean = Mathf.Lerp(14f, 0f, t);
                if (queued >= 0 && t > 0.5f) break;
                yield return null;
            }
            IsAttacking = false;
            lastAttackEnd = Time.time;
        }

        // F: leap to the cursor (max 7 m), invulnerable in the air, heavy area hit + short stun on landing.
        IEnumerator GroundSlam(SkillDef s)
        {
            IsAttacking = true; lockFacing = true;
            Vector3 from = Ground(0f);
            Vector3 to;
            if (remote) to = new Vector3(slamTarget.x, 0f, slamTarget.z);     // where the other player said he lands
            else
            {
                Vector3 delta = aimPoint - from; delta.y = 0f;
                delta = Vector3.ClampMagnitude(delta, SlamRange);
                to = from + delta;
                float lim = Game.ArenaHalf - 0.6f;
                to.x = Mathf.Clamp(to.x, -lim, lim); to.z = Mathf.Clamp(to.z, -lim, lim);
                Net.SendAct(13, 1f, to);
                Airborne = true; invuln = true;
            }

            Fx.I.Dust(from, 10);
            SwingSound(0.8f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.45f)
            {
                if (!remote)
                {
                    Vector3 p = Vector3.Lerp(from, to, t);
                    jumpY = Mathf.Sin(t * Mathf.PI) * 2.6f;
                    transform.position = new Vector3(p.x, jumpY, p.z);
                }
                swYaw = Mathf.Lerp(swYaw, 5f, 0.3f); swPitch = Mathf.Lerp(swPitch, -120f, 0.3f); swHand = 0.45f;
                rigLean = Mathf.Lerp(-15f, 25f, t);
                yield return null;
            }
            if (!remote)
            {
                jumpY = 0f;
                transform.position = to;
                Airborne = false;
            }
            swPitch = 45f; rigLean = 20f;

            Vector3 g = remote ? new Vector3(to.x, 0.06f, to.z) : Ground(0.06f);
            Color c = s.color; c.a = 0.9f;
            Fx.I.Ring(g, 0.6f, SlamRadius + 0.5f, c, 0.45f);
            Fx.I.Ring(g, 0.3f, SlamRadius * 0.7f, new Color(1f, 0.9f, 0.6f, 0.8f), 0.35f);
            Fx.I.Disc(g, SlamRadius * 0.5f, SlamRadius, new Color(1f, 0.8f, 0.5f, 0.45f), 0.25f);
            Fx.I.Dust(g, 26, 2f);
            Fx.I.Burst(g + Vector3.up * 0.2f, 14, new Color(0.45f, 0.36f, 0.25f), 3f, 9f, Vector3.up, 0.7f, 0.7f, 14f, 0.18f);
            HitArea(transform.position, transform.forward, SlamRadius, 180f, 3, s.damage, 0.2f, 8f, 0.7f, s.name, 13);
            Juice(0.45f, 0.3f, 0.09f);
            invuln = false;
            lockFacing = false;

            for (float t = 0; t < 1f; t += Time.deltaTime / 0.3f)
            {
                rigLean = Mathf.Lerp(20f, 0f, t);
                swPitch = Mathf.Lerp(45f, RestPitch, t);
                if (queued >= 0 && t > 0.5f) break;
                yield return null;
            }
            IsAttacking = false;
            lastAttackEnd = Time.time;
        }

        // ---------------------------------------------------------------- taking damage

        // Armor absorbs first. A raised shield facing the blow cuts it by 80% (+2% per BLOCK point) for 15 stamina.
        // Always runs on the machine of the player who owns this knight (the host forwards monster hits to it).
        public void TakeDamage(float damage, Vector3 from, string source, float knockback = 6f)
        {
            if (remote || Dead || Invulnerable) return;
            LastDamageSource = source;
            Vector3 toSrc = from - transform.position; toSrc.y = 0f;
            Vector3 srcDir = toSrc.sqrMagnitude > 0.0001f ? toSrc.normalized : transform.forward;
            Vector3 chestPos = transform.position + new Vector3(0f, 1.4f, 0f);
            bool blocked = false;

            if (IsBlocking && Vector3.Angle(transform.forward, srcDir) < 75f)
            {
                if (stamina >= BlockCost)
                {
                    blocked = true;
                    stamina -= BlockCost;
                    lastSpendTime = Time.time;
                    damage *= 1f - progress.BlockReduction;
                    Sfx.I.Play(Snd.Clash);
                    Game.I.hud.Popup(chestPos + new Vector3(0f, 1.2f, 0f), "BLOCK", new Color(1f, 0.9f, 0.45f), 26);
                    Fx.I.Burst(chestPos + srcDir * 0.6f, 12, new Color(1f, 0.92f, 0.6f), 3f, 8f, srcDir + Vector3.up * 0.4f, 0.7f, 0.25f, 5f, 0.1f);
                }
                else
                {
                    StaminaDeniedTime = Time.unscaledTime;
                    Game.I.hud.Popup(chestPos + new Vector3(0f, 1.2f, 0f), "GUARD BROKEN", new Color(1f, 0.5f, 0.4f), 24);
                }
            }

            float toArmor = Mathf.Min(armor, damage);
            armor -= toArmor;
            float toHp = damage - toArmor;
            hp = Mathf.Max(0f, hp - toHp);
            lastDamageTime = Time.time;

            if (toArmor >= 0.5f) Game.I.hud.Popup(chestPos + new Vector3(-0.4f, 0.6f, 0f), "-" + Mathf.RoundToInt(toArmor), new Color(0.65f, 0.82f, 1f), 24);
            if (toHp >= 0.5f) Game.I.hud.Popup(chestPos + new Vector3(0.4f, 0.9f, 0f), "-" + Mathf.RoundToInt(toHp), new Color(1f, 0.35f, 0.25f), 28);

            knockVel = -srcDir * (blocked ? knockback * 0.35f : knockback);
            if (!blocked)
            {
                hurtTint = 1f;
                if (toArmor >= 0.5f && toArmor >= toHp) Sfx.I.Play(Snd.ArmorImpact, 0.8f);     // the blow landed on his plate
                Fx.I.Burst(chestPos, 8, toHp > 0.5f ? new Color(1f, 0.3f, 0.25f) : new Color(0.7f, 0.85f, 1f), 2f, 6f, -srcDir + Vector3.up * 0.5f, 0.7f, 0.3f, 8f, 0.1f);
            }
            Game.I.Shake(blocked ? 0.08f : 0.18f, 0.18f);

            if (hp <= 0f)
            {
                StopAllCoroutines();
                StartCoroutine(DownRoutine());
            }
        }

        public string LastDamageSource { get; private set; }

        // Falling on the back (local and remote knights).
        IEnumerator FallRoutine()
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.4f)
            {
                rig.localRotation = Quaternion.Euler(-86f * t * t, 0f, 0f);
                rigHop = 0.2f * t;
                yield return null;
            }
        }

        // Down, wait, stand up again at the spawn point. Online the wait is longer (the others fight on meanwhile).
        IEnumerator DownRoutine()
        {
            Dead = true;
            IsAttacking = false; IsBlocking = false; Airborne = false;
            invuln = false; lockFacing = false; whirl = false; shieldPush = false;
            queued = -1; rigSpin = 0f; jumpY = 0f;
            bool online = Net.Multiplayer;
            Game.I.hud.Banner(online ? "You are down! Back in " + Game.ReviveDelayCoop.ToString("0") + " seconds..." : "Knight down! Reviving...", new Color(1f, 0.5f, 0.4f));
            yield return FallRoutine();
            yield return new WaitForSeconds(online ? Game.ReviveDelayCoop : Game.ReviveDelaySingle);
            Game.I.OnKnightRevive(this);
            hp = MaxHp; armor = MaxArmor; stamina = MaxStamina;
            rigLean = 0f; rigHop = 0f;
            swYaw = RestYaw; swPitch = RestPitch; swHand = RestHand;
            knockVel = Vector3.zero;
            transform.position = spawnPoint;
            invulnUntil = Time.time + 2.5f;
            Dead = false;
            Sfx.I.Play(Snd.Ready);
            Fx.I.Ring(Ground(0.06f), 0.4f, 3.5f, new Color(0.7f, 0.9f, 1f, 0.9f), 0.6f);
        }
    }
}

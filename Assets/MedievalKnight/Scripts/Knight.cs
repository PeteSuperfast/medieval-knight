using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // What a knight is asked to do in one frame. It comes from the keyboard / mouse (Knight.ReadInput) or from a
    // test script; the simulation (Knight.Simulate) never reads a device itself.
    public struct KnightInput
    {
        public Vector2 move;        // walk direction on the ground plane (x = right, y = world +Z)
        public Vector3 aim;         // world point the knight faces and leaps to
        public bool block;          // right mouse button held: shield up
        public bool run;            // Left Shift held
        public bool roll;           // Space pressed this frame
        public bool eatHeld;        // F held
        public bool attackHeld;    // left mouse button / J held (read even while a panel is open, so a press that began there is not a click)
    }

    // A player: top-down movement, health / armor / stamina, shield block, the three-swing sword chain
    // (diagonal, sweep, thrust). He always faces the mouse cursor.
    // Everything that belongs to one player lives on this object: level / upgrades / stat points (progress)
    // and the owned + equipped swords (inventory).
    // A knight is either local (remote == false: driven by ReadInput + Simulate on this machine) or remote
    // (another player's knight: position, pose flags and numbers come from the network, attacks are replayed
    // for show only and never hit anything here).
    public class Knight : MonoBehaviour
    {
        public const float BaseHp = 100f, HpPerLevel = 10f, BaseArmor = 60f, BaseStamina = 100f, StaminaRegen = 26f;
        public const float MinSwingSpeed = 0.4f, MaxSwingSpeed = 4f;
        const float MoveSpeed = 6.2f;
        public const float RunMult = 1.5f;                  // Left Shift: 6.2 -> 9.3 m/s, costs nothing
        // Space: roll (dodge). Stamina pays for the roll and for blocking: one roll = 25 (one tick on the stamina bar).
        public const float RollCost = 25f, RollCooldown = 0.8f, RollDistance = 6f, RollMoveTime = 0.40f, RollInvuln = 0.5f;
        public const float BlockStaminaCost = 10f, BlockCostPerDef = 0.04f;     // a blow on the raised shield costs 10 stamina, 4% less per DEF point
        public const int ParryLockPhases = 2;                                    // the first two phases of the parry motion (0.27 s) cannot be cut short
        const float RestYaw = 55f, RestPitch = -25f, RestHand = 0.36f;

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

        public int pid;                                     // player slot 0..3 (0 = single player / the host)
        public string playerName = "Knight";
        public bool remote;                                 // another player's knight, shown here from network state
        public Vector3 spawnPoint;
        public Progress progress = new Progress();
        public readonly Inventory inventory = new Inventory();
        public int souls;                                   // Soul points: not part of Progress, because dying resets that
        public readonly NetInterp interp = new NetInterp();
        public float hp = BaseHp, armor = BaseArmor, stamina = BaseStamina;
        public bool IsAttacking { get; private set; }      // busy with a swing
        public bool IsBlocking { get; private set; }
        public bool Dead { get; private set; }
        public bool Airborne { get; private set; }
        public bool Invulnerable { get { return invuln || Glowing || Rolling; } }
        // 0.5 s of invulnerability from the moment of a roll (the body rolls for the first 0.4 s)
        public bool Rolling { get { return Time.time < rollInvulnUntil; } }
        public float ReviveIn { get { return Dead ? Mathf.Max(0f, reviveAt - Time.time) : 0f; } }       // seconds until he stands up again
        public float RollReadyIn { get { return Mathf.Max(0f, rollReadyAt - Time.time); } }
        // just revived, or picking a level-up card online: cannot be hurt, and glows
        public bool Glowing { get { return remote ? netGlow : Time.time < invulnUntil || Time.time < protectUntil; } }
        public float StaminaDeniedTime { get; private set; }
        public string LastAction { get; private set; }
        public int LastHitCount { get; private set; }
        public float LastAttackStart { get; private set; }   // Time.time when the last sword attack began (test hook)
        public int ActionsSeen { get; private set; }       // swings / rolls started (local) or replayed (remote)
        public float MaxHp { get { return remote ? netMaxHp : BaseHp + HpPerLevel * (progress.level - 1) + progress.HpBonus; } }
        public float MaxArmor { get { return remote ? netMaxArmor : BaseArmor + progress.ArmorBonus; } }
        public float MaxStamina { get { return BaseStamina + progress.StaminaBonus; } }
        // Where the rules see this knight: a remote one is at his newest reported position, not the smoothed one on screen.
        public Vector3 Pos { get { return remote ? netPos : transform.position; } }
        public Vector3 Forward { get { return transform.forward; } }
        public WeaponDef Weapon { get { return inventory.Weapon; } }
        public Transform SwordModel { get { return swordModel; } }
        public string BodyKind { get { return body != null ? body.Kind : ""; } }          // "coded" (a "file" body comes with the .fbx work)
        public float BodyLean { get { return body != null ? body.rigLean : 0f; } }                 // test read-out: the body turn (a roll goes through 360)

        // --- combat numbers: the equipped weapon only (level-ups give stats, not damage) ---
        public float DamageMult() { return inventory.Damage; }
        public float CritChance() { return Mathf.Min(Progress.CritChanceCap, Progress.BaseCritChance + Weapon.critChance); }
        public float CritMult() { return Mathf.Min(Progress.CritMultCap, Progress.BaseCritMult + Weapon.critMult); }
        public float SwingSpeed { get { return Mathf.Clamp(Weapon.speed, MinSwingSpeed, MaxSwingSpeed); } }
        public float Reach(SwingDef a) { return a.reach * Weapon.reach; }

        // The one place where the knight's damage is rolled (weapon multiplier, crit chance, crit multiplier).
        public float RollDamage(float baseDamage, out bool isCrit)
        {
            float d = baseDamage * DamageMult();
            isCrit = Progress.ForceCrit == 2 || (Progress.ForceCrit == 0 && Random.value < CritChance());
            if (isCrit) d *= CritMult();
            return d;
        }

        // self-test hooks: aim at a world point / walk in a direction without real input devices
        public Vector3? aimOverride;
        public Vector2? moveOverride;
        public bool? attackHeldOverride;                    // the left button held (true) / up (false) without a device
        public bool? eatHeldOverride;                       // F held (true) / up (false) without a device
        CodedBody body;                                     // what he looks like (the pose numbers below live in it)
        Transform rig, swordNode, swordModel;
        float swYaw { get { return body.swYaw; } set { body.swYaw = value; } }
        float swPitch { get { return body.swPitch; } set { body.swPitch = value; } }
        float swHand { get { return body.swHand; } set { body.swHand = value; } }
        float rigLean { get { return body.rigLean; } set { body.rigLean = value; } }
        float rigHop { get { return body.rigHop; } set { body.rigHop = value; } }
        float rigSpin { get { return body.rigSpin; } set { body.rigSpin = value; } }
        float jumpY, hurtTint;
        float lastSpendTime = -10f, lastDamageTime = -10f, lastAttackEnd = -10f, invulnUntil, protectUntil;
        float rollInvulnUntil, rollMoveEnd, rollReadyAt, lastDodgeFx = -10f, lastRunDust, reviveAt;
        Vector3 rollDir;
        bool invuln, lockFacing;
        Vector3 knockVel, aimPoint;
        // phase 7: parry window (0.2 s from a fresh shield press, not again within 0.5 s), clash window (the strike of a light swing +-0.1 s)
        float parryUntil, lastPressAt = -10f, clashFrom, clashTo, shieldFlashUntil;
        public const float ParryWindow = 0.2f, ParryGap = 0.5f, ClashPad = 0.1f, ClashHalfAngle = 60f, ClashRange = 5.5f;
        public bool ParryOpen { get { return Time.time < parryUntil; } }
        public bool ClashOpen { get { return clashTo > 0f && Time.time >= clashFrom && Time.time <= clashTo; } }
        public int ParryCount { get; private set; }               // blows turned away by a parry (test counters)
        public int ClashCount { get; private set; }               // light blows traded in a clash
        public int HeavyBlockCount { get; private set; }          // heavy blows that came through a block / the parry window
        // swing chain: swingIndex = the swing the next click starts (0..2); queuedClick = one click kept for the next swing
        public readonly ActClock clock = new ActClock();
        int swingIndex;
        bool queuedClick, queueFromRecovery, leftWas;
        float queuedClickTime;
        Vector2 lastMove;                                    // the walk input of this frame

        // remote knights only
        Vector3 netPos;
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
            body = new CodedBody();
            body.Build(transform, pid);
            rig = body.Root;
            swordNode = body.SwordMount;
            swordModel = Weapons.BuildModel(swordNode, Weapon);       // the equipped sword; replaced by Equip()
            swordModel.name = Weapon.name;
            body.RebuildTint();

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
            body.ClearTint();
            if (swordModel != null)
            {
                swordModel.gameObject.SetActive(false);
                swordModel.SetParent(null);                 // Destroy is delayed; the new Tint must not see the old parts
                Destroy(swordModel.gameObject);
            }
            swordModel = Weapons.BuildModel(swordNode, Weapon);
            swordModel.name = Weapon.name;
            body.RebuildTint();
        }

        // Spends one stat point; the new maximum is filled in right away.
        public bool SpendPoint(Attr a)
        {
            if (!progress.Spend(a)) return false;
            if (a == Attr.Hp) hp = Mathf.Min(MaxHp, hp + Progress.HpPerPoint);
            else if (a == Attr.Def) armor = Mathf.Min(MaxArmor, armor + Progress.ArmorPerPoint);
            else if (a == Attr.Stamina) stamina = Mathf.Min(MaxStamina, stamina + Progress.StaminaPerPoint);     // (SPEED and RESISTANCE have no bar to fill)
            return true;
        }

        // F5: back to level 1, no upgrades, no stat points, only the starting sword.
        public void ResetAll()
        {
            inventory.Reset();
            ResetForRun(spawnPoint);
            RebuildSword();
        }

        // Everything except the weapon (the caller sets that): level 1, no upgrades, Soul 0, full bars, standing at "at".
        // Used by F5 (above) and by the Phase 1 loop at the start / end of every run.
        public void ResetForRun(Vector3 at)
        {
            CancelAction();
            progress = new Progress();
            souls = 0;                                                   // F5 clears Soul too (dying does not)
            hp = MaxHp; armor = MaxArmor; stamina = MaxStamina;
            Dead = false; IsBlocking = false; Airborne = false;
            swingIndex = 0; knockVel = Vector3.zero; hurtTint = 0f;
            rigLean = 0f; rigHop = 0f; jumpY = 0f;
            swYaw = RestYaw; swPitch = RestPitch; swHand = RestHand;
            invulnUntil = 0f; protectUntil = 0f;
            rollInvulnUntil = 0f; rollMoveEnd = 0f; rollReadyAt = 0f;
            transform.position = at;
            transform.rotation = Quaternion.identity;
        }

        // Phase 1 loop: the weapon in hand is the carried HELD one (inventory is only a one-weapon projection of it).
        // The model is rebuilt only when it is a different weapon.
        public void SetWeapon(int id, int plus)
        {
            inventory.SetSingle(id, plus);
            if (swordModel == null || swordModel.name != Weapon.name) RebuildSword();
        }

        // Stops whatever the knight is doing (swing, roll) and clears every flag those leave behind.
        // One place for the three callers: a roll that cancels a swing, death, restart.
        public void CancelAction()
        {
            StopAllCoroutines();
            ClearActionFlags();
        }

        void ClearActionFlags()
        {
            IsAttacking = false; lockFacing = false; invuln = false;
            queuedClick = false; rigSpin = 0f; eating = false;
            if (clock.act == Act.Roll) { rigLean = 0f; rigHop = 0f; }                // (a half-turned body would otherwise unwind through the floor)
            body.pivotY = 0f; body.ReleaseShield();
            clock.End();
            clashFrom = clashTo = 0f;
        }

        // Food: health only (not armor / stamina). Returns the health actually gained.
        public float Heal(float amount)
        {
            if (Dead || amount <= 0f) return 0f;
            float before = hp;
            hp = Mathf.Min(MaxHp, hp + amount);
            return hp - before;
        }

        // Dying: level, EXP, upgrade cards and stat points start over. Swords, Soul and the kill count stay.
        // Health / armor / stamina come back full for the new (level 1) maximums.
        public void ResetProgress()
        {
            int kills = progress.kills;
            progress = new Progress { kills = kills };
            hp = MaxHp; armor = MaxArmor; stamina = MaxStamina;
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
            if (Game.I.Paused) { leftWas = InputX.MouseHeld(0) || InputX.Held(KeyCode.J); return; }

            // --- resources ---
            if (!Dead)
            {
                if (Time.time - lastSpendTime > 0.6f)
                    stamina = Mathf.Min(MaxStamina, stamina + StaminaRegen * progress.StaminaRegenMult * dt);
                if (Time.time - lastDamageTime > 4f)
                    armor = Mathf.Min(MaxArmor, armor + 8f * dt);
            }

            // an open panel (online: the game is not paused) takes the combat keys and the mouse; walking still works
            bool canInput = !Dead && Time.unscaledTime >= Game.I.inputBlockUntil && !Game.I.PanelOpen;
            Simulate(ReadInput(canInput), canInput, dt);
        }

        // Online loop: a friend lies within reach, so F revives him instead of leaping (Ground Slam).
        bool ReviveInstead
        {
            get
            {
                Game g = Game.I;
                return g.Coop && g.run != null && g.run.team != null && g.run.team.ReviveCandidate != null;
            }
        }

        // The only place where the knight looks at the keyboard and mouse (or at the self-test's overrides).
        KnightInput ReadInput(bool canInput)
        {
            var inp = new KnightInput();
            if (!Dead) inp.move = moveOverride.HasValue ? moveOverride.Value : InputX.Move();
            inp.aim = aimOverride.HasValue ? aimOverride.Value : Game.I.AimPoint();
            inp.attackHeld = attackHeldOverride.HasValue ? attackHeldOverride.Value : InputX.MouseHeld(0) || InputX.Held(KeyCode.J);
            if (!canInput) return inp;
            inp.block = InputX.MouseHeld(1);
            inp.run = InputX.Held(KeyCode.LeftShift) || InputX.Held(KeyCode.RightShift);
            inp.roll = InputX.Down(KeyCode.Space);
            inp.eatHeld = eatHeldOverride.HasValue ? eatHeldOverride.Value : InputX.Held(KeyCode.F);
            return inp;
        }

        // One frame of the local knight: actions, facing, movement, pose.
        void Simulate(KnightInput inp, bool canInput, float dt)
        {
            Vector2 mv = inp.move;
            lastMove = mv;
            aimPoint = inp.aim;
            bool rolling = Time.time < rollMoveEnd;
            // left button: a click is the moment it goes down; a press that began while a panel was open never counts
            bool click = inp.attackHeld && !leftWas && canInput && !Dead;
            leftWas = inp.attackHeld;
            UpdateEating(inp, canInput, mv);
            bool wasBlocking = IsBlocking;
            IsBlocking = canInput && !IsAttacking && !Eating && inp.block && !rolling;                   // the shield drops during a roll
            if (IsBlocking && !wasBlocking)                                                      // a fresh press: 0.2 s parry window, unless pressed again within 0.5 s
            {
                float now = Time.time;
                if (now - lastPressAt >= ParryGap) { parryUntil = now + ParryWindow; shieldFlashUntil = parryUntil; }
                lastPressAt = now;
            }

            if (canInput && inp.roll) TryRoll(inp);
            rolling = Time.time < rollMoveEnd;
            if (canInput)
            {
                if (click && !inp.block) Click(rolling);                                     // the shield held at the press = no swing
                else if (!IsAttacking && queuedClick) { queuedClick = false; if (!inp.block) Click(rolling); }
            }
            else queuedClick = false;

            // --- facing: always towards the cursor, unless an attack has committed to a direction ---
            if (!Dead && !lockFacing)
            {
                Vector3 aim = inp.aim - transform.position;
                aim.y = 0f;
                if (aim.sqrMagnitude > 0.05f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(aim), 1100f * dt);
            }

            // --- movement on the XZ plane ---
            float speedMul = Eating ? 0f : IsAttacking ? 0.2f : IsBlocking ? 0.4f : 1f;
            if (clock.act == Act.Swing && !clock.done) speedMul = (clock.phase == 0 || clock.phase == 3) ? 0.3f : 0f;   // the strike and the follow-through are driven by the move
            else if (lockFacing && IsAttacking) speedMul = Airborne ? 0f : 0.15f;
            bool running = canInput && inp.run && mv.sqrMagnitude > 0.01f && !IsAttacking && !IsBlocking && !Airborne && !Dead && !rolling;
            if (running) speedMul *= RunMult;
            Running = running;
            Vector3 p = transform.position;
            p.y = 0f;
            if (rolling) p += rollDir * (RollDistance / RollMoveTime) * dt;      // the dash replaces walking
            else p += new Vector3(mv.x, 0f, mv.y) * MoveSpeed * speedMul * progress.MoveSpeedMult * dt;     // SPEED: walking and running only
            p += knockVel * dt;
            if (running && Time.time - lastRunDust > 0.22f) { lastRunDust = Time.time; Fx.I.Dust(transform.position, 2); }
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
            p = Obstacles.Push(p, 0.4f, Obstacles.Players);        // houses, walls, trees, rocks, the lake and the mountain
            float lim = Game.ArenaHalf - 0.4f;
            p.x = Mathf.Clamp(p.x, -lim, lim);
            p.z = Mathf.Clamp(p.z, -lim, lim);
            p.y = jumpY;
            transform.position = p;

            Pose(dt, (mv.sqrMagnitude > 0.01f || rolling) && !Airborne);
        }

        public bool Running { get; private set; }
        public int DodgeCount { get; private set; }               // blows that passed through a roll (test counter)
        public int ParryMotionCount { get; private set; }          // parry motions played (a parry of his own, or a friend's replayed here)

        // Space: roll 6 m in 0.4 s; cannot be hurt for 0.5 s. Costs 25 stamina and has a 0.8 s cooldown.
        // It cuts any swing (or the end of a parry motion) short; it cannot be used while leaping or in the first part of a parry motion.
        void TryRoll(KnightInput inp)
        {
            if (Dead || Airborne || Time.time < rollReadyAt) return;
            if (ParryLocked) return;
            if (stamina < RollCost - 0.001f) { Deny("Roll"); return; }
            CancelEat();
            if (IsAttacking || clock.act == Act.Parry) CancelAction();
            stamina -= RollCost;
            lastSpendTime = Time.time;
            rollReadyAt = Time.time + RollCooldown;
            rollMoveEnd = Time.time + RollMoveTime;
            swingIndex = 0; queuedClick = false;                                  // a roll starts the chain over
            rollInvulnUntil = Time.time + RollInvuln;
            knockVel = Vector3.zero;
            Vector3 dir = new Vector3(inp.move.x, 0f, inp.move.y);
            bool backwards = dir.sqrMagnitude < 0.01f;                            // no direction held: roll backwards
            if (backwards) dir = -transform.forward;
            rollDir = dir.normalized;
            Vector3 flat = transform.forward; flat.y = 0f;
            backwards |= Vector3.Dot(rollDir, flat) < -0.5f;
            if (!backwards) transform.rotation = Quaternion.LookRotation(rollDir);   // forwards and sideways: roll the way he runs
            LastAction = "Roll";
            ActionsSeen++;
            Net.SendAct(20, 1f, transform.position + rollDir * RollDistance);
            RollFx(rollDir);
            StartCoroutine(RollRoutine(backwards));
        }

        // The tumble: crouch, roll over (a full turn about the middle of the body), stand up. The clock is 20 / 60 / 20 % of 0.4 s.
        IEnumerator RollRoutine(bool backwards)
        {
            lockFacing = true;
            clock.Begin(Act.Roll, 0, KnightAnim.Roll, 1f);
            body.pivotY = 0.8f;
            PoseKey start = CurrentKey();
            yield return null;
            while (!clock.done)
            {
                clock.Tick(Time.deltaTime);
                PoseKey p = ActClock.Pose(KnightAnim.Roll, clock.phase, clock.t, start);
                if (backwards) p.lean = -p.lean;
                SetPose(p);
                yield return null;
            }
            rigLean = 0f; rigHop = 0f;                                             // (360 degrees is the same as 0: no unwinding)
            body.pivotY = 0f;
            lockFacing = false;
            clock.End();
        }

        // ---------------------------------------------------------------- eating (hold F)

        // Food is never eaten at once: he stands still and holds F (or presses C / clicks the food in the Carry window) for
        // 2.0 s, then the health comes. Interrupted (progress lost, the food stays) by a blow, a roll, a step, letting go of F,
        // a click or the shield, falling, or a panel. A friend lying within reach is revived instead (TeamRun).
        public const float EatReach = 1.2f;
        bool eating, eatByKey, eatLock, netEating;
        float eatStart, eatHurtMark;
        public float EatFlagSince { get; set; }                     // remote knight: when his eating flag went up (the host grants ground food after ~2 s)
        public int EatCompletedCount { get; private set; }         // test counter: meals finished
        public int EatCancelCount { get; private set; }            // test counter: meals interrupted
        public bool Eating { get { return remote ? netEating : eating; } }
        public float EatProgress
        {
            get
            {
                if (remote) return netEating ? Mathf.Clamp01((Time.unscaledTime - EatFlagSince) / KnightAnim.EatSeconds) : 0f;
                return eating ? Mathf.Clamp01((Time.unscaledTime - eatStart) / KnightAnim.EatSeconds) : 0f;
            }
        }

        // Is there something to eat right now? Loop: the first food in the carry. The v1 arena: a food on the ground within 1.2 m.
        public bool FoodAvailable(out Pickup ground)
        {
            ground = null;
            Game g = Game.I;
            if (g.Loop)
            {
                if (g.run == null || !g.run.InWorld) return false;
                Section s; int i;
                return g.run.PeekFood(out s, out i);
            }
            float best = EatReach * EatReach;
            for (int n = 0; n < Pickup.All.Count; n++)
            {
                Pickup p = Pickup.All[n];
                if (p.kind != PickupKind.Food || p.hidden || p.age < 0.35f || !p.gameObject.activeInHierarchy) continue;
                Vector3 d = p.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude <= best) { best = d.sqrMagnitude; ground = p; }
            }
            return ground != null;
        }

        // The hint "Hold F: Eat" is shown when this is true.
        public bool EatPromptOn
        {
            get
            {
                if (remote || Dead || eating || IsAttacking || IsBlocking || Rolling || Game.I.PanelOpen || hp >= MaxHp - 0.5f) return false;
                Pickup p;
                return FoodAvailable(out p);
            }
        }

        bool EatBlocked { get { return IsAttacking || IsBlocking || Rolling || ParryActive || Airborne || Dead; } }

        // F held / C / a click on the food in the Carry window. byKey: no need to keep F down.
        public bool TryBeginEat(bool byKey)
        {
            if (remote || eating || EatBlocked) return false;
            Game g = Game.I;
            Pickup ground;
            bool food = FoodAvailable(out ground);
            if (!food)
            {
                if (byKey && g.Loop) g.hud.Notice("No food to eat", new Color(1f, 0.85f, 0.4f));
                return false;
            }
            if (hp >= MaxHp - 0.5f)
            {
                g.hud.Popup(transform.position + new Vector3(0f, 2.7f, 0f), "HP full", new Color(0.75f, 0.75f, 0.78f), 16);
                g.hud.Notice("HP full", new Color(0.75f, 0.75f, 0.78f));
                g.fullHints++;
                return false;
            }
            eating = true; eatByKey = byKey; eatStart = Time.unscaledTime; eatHurtMark = LastHurtTime;
            LastAction = "Eat";
            ActionsSeen++;
            Net.SendAct(21, 1f, Vector3.zero);
            return true;
        }

        void CancelEat()
        {
            if (!eating) return;
            eating = false;
            EatCancelCount++;
        }

        void UpdateEating(KnightInput inp, bool canInput, Vector2 mv)
        {
            if (eatLock && !inp.eatHeld) eatLock = false;               // let go of F after a meal / an interruption before the next one
            if (eating)
            {
                bool hurt = LastHurtTime > eatHurtMark + 0.0001f;
                bool cancel = Game.I.PanelOpen || Dead || EatBlocked || inp.block || mv.sqrMagnitude > 0.01f || hurt || (!eatByKey && !inp.eatHeld) || ReviveInstead;
                if (cancel) { CancelEat(); if (inp.eatHeld) eatLock = true; }
                else if (Time.unscaledTime - eatStart >= KnightAnim.EatSeconds) FinishEat();
                return;
            }
            if (canInput && inp.eatHeld && !eatLock && mv.sqrMagnitude <= 0.01f && !ReviveInstead)
            {
                if (!TryBeginEat(false)) eatLock = true;                // nothing to eat / HP full: one hint per press, not every frame
            }
        }

        void FinishEat()
        {
            eating = false;
            eatLock = true;
            EatCompletedCount++;
            Game g = Game.I;
            if (g.Loop)
            {
                Section s; int i;
                if (g.run != null && g.run.PeekFood(out s, out i)) g.run.Eat(s, i);          // the one place that takes the item and heals
            }
            else if (Net.Authority)
            {
                Pickup p;
                if (FoodAvailable(out p)) g.EatGroundFood(this, p);
            }
            // a client in the v1 arena: the host sees the eating flag and sends the PickupTaken
        }

        void EatPose()
        {
            float t = EatProgress;
            PoseKey crouch = KnightAnim.Eat.keys[1], rest = KnightAnim.Eat.keys[0];
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 8f));
            PoseKey k = PoseKey.Lerp(rest, crouch, e);
            k.pitch += Mathf.Sin(Time.time * 9f) * 9f * e;              // bites
            SetPose(k);
        }

        // ---------------------------------------------------------------- parry motion

        // The parry motion (shield up, the blow knocked aside, arms a little wide, shield back) after a successful parry.
        // The first two phases cannot be cut short; after that a swing or a roll can.
        public bool ParryLocked { get { return clock.act == Act.Parry && !clock.done && clock.phase < ParryLockPhases; } }
        public bool ParryActive { get { return clock.act == Act.Parry && !clock.done; } }

        void StartParryMotion()
        {
            if (Dead || remote) return;
            Net.SendAct(22, 1f, Vector3.zero);
            StartCoroutine(ParryRoutine());
        }

        IEnumerator ParryRoutine()
        {
            ParryMotionCount++;
            ActDef d = KnightAnim.Parry;
            clock.Begin(Act.Parry, 0, d, 1f);
            PoseKey start = CurrentKey();
            ShieldKey sStart = body.CurrentShieldKey();
            bool sparks = false;
            yield return null;
            while (!clock.done)
            {
                clock.Tick(Time.deltaTime);
                SetPose(ActClock.Pose(d, clock.phase, clock.t, start));
                body.DriveShield(ActClock.ShieldPose(d, clock.phase, clock.t, sStart));
                if (!sparks && clock.phase >= 1)                                  // phase 2: the blow is knocked aside - gold sparks
                {
                    sparks = true;
                    Vector3 at = body.ShieldMount.position + transform.forward * 0.3f;
                    Fx.I.Burst(at, 12, new Color(0.961f, 0.761f, 0.353f), 3f, 8f, transform.forward + Vector3.up * 0.3f, 0.7f, 0.25f, 4f, 0.09f);
                }
                yield return null;
            }
            body.ReleaseShield();
            clock.End();
        }

        // A friend's parry motion, replayed for show (one interpolation delay late like every replayed move).
        IEnumerator RemoteParry()
        {
            if (interp.delay > 0f) yield return new WaitForSeconds(interp.delay);
            yield return ParryRoutine();
            act = null;
        }

        // A Roll replayed for a remote knight (the position glides on its own).
        IEnumerator RemoteRoll(bool backwards)
        {
            if (interp.delay > 0f) yield return new WaitForSeconds(interp.delay);
            yield return RollRoutine(backwards);
            act = null;
        }

        // ---------------------------------------------------------------- click: the swing chain

        // A click of the left button (the moment it goes down). The shield held at the press never gets here.
        //   idle: the next swing starts (back to swing 1 when the last one ended more than ChainWindow ago)
        //   in a swing: one click is kept; used when the follow-through ends (the recovery is skipped, the next wind-up is
        //   30% shorter), or, when it came during the recovery, when the recovery ends.
        void Click(bool rolling)
        {
            if (Dead || Airborne || rolling) return;
            if (eating) CancelEat();                                      // a swing ends the meal at once
            if (ParryLocked) return;                                      // the first part of a parry motion cannot be cut short
            if (clock.act == Act.Parry && !clock.done) CancelAction();    // the end of it can: the swing starts at once
            if (IsAttacking)
            {
                if (!queuedClick)
                {
                    queuedClick = true;
                    queuedClickTime = Time.time;
                    queueFromRecovery = clock.act == Act.Swing && !clock.done && clock.phase >= 3;
                }
                return;
            }
            if (Time.time - lastAttackEnd > KnightAnim.ChainWindow) swingIndex = 0;
            BeginSwing(false);
        }

        // Starts the swing that swingIndex points at (chain = it follows the last one at once).
        void BeginSwing(bool chain)
        {
            int index = Mathf.Clamp(swingIndex, 0, KnightAnim.Swings.Length - 1);
            swingIndex = (index + 1) % KnightAnim.Swings.Length;
            SwingDef d = KnightAnim.Swings[index];
            float speed = SwingSpeed;
            LastAction = d.name;
            LastAttackStart = Time.time;
            ActionsSeen++;
            Game.I.hud.Action((index + 1) + "  " + d.name, d.fx);
            Net.SendAct(index, speed, new Vector3(chain ? 1f : 0f, 0f, 0f));
            StartCoroutine(SwingRoutine(d, index, speed, chain ? KnightAnim.ChainPreScale : 1f));
        }

        void RollFx(Vector3 dir)
        {
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            Fx.I.Streak(Ground(0.9f), yaw, RollDistance, 0.7f, new Color(0.8f, 0.92f, 1f, 0.85f), 0.25f);
            Fx.I.Dust(transform.position, 8);
            Sfx.I.Play(Snd.Swing, 0.7f * Volume, 0.75f);
        }

        // Idle / walk / block poses, legs, shield (in the body) and tint (attacks drive the sword and body themselves).
        // Shared by the local knight and by remote knights.
        void Pose(float dt, bool moving)
        {
            bool eatDriven = Eating && !Dead;
            if (eatDriven) EatPose();
            body.Pose(dt, moving, eatDriven || IsAttacking || (!clock.done && (clock.act == Act.Roll || clock.act == Act.Parry)), Dead, IsBlocking);

            hurtTint = Mathf.Max(0f, hurtTint - dt / 0.3f);
            if (Glowing && !Dead) body.SetTint(new Color(1f, 1f, 0.7f), 0.35f + 0.25f * Mathf.Sin(Time.time * 25f));
            else if (!remote && Rolling && !Dead) body.SetTint(new Color(0.81f, 0.91f, 1f), 0.45f + 0.25f * Mathf.Sin(Time.time * 40f));
            else body.SetTint(new Color(1f, 0.25f, 0.2f), hurtTint * 0.8f);

            // the shield flashes pale blue while the parry window is open (a remote knight: for 0.2 s after his shield goes up)
            bool flashing = !Dead && IsBlocking && Time.time < shieldFlashUntil;
            if (flashing || shieldFlashOn)
            {
                shieldFlashOn = flashing;
                body.SetShieldTint(new Color(0.81f, 0.91f, 1f), flashing ? 0.75f : 0f);
            }
        }
        bool shieldFlashOn;

        // ---------------------------------------------------------------- remote knight (another player)

        // State bits sent over the network.
        public const int NetBlocking = 1, NetDead = 2, NetAirborne = 4, NetGlow = 8, NetFallen = 16, NetEating = 64;      // NetEating: holding food up (protocol 6). NetFallen: Dead + Fallen = really dead (the loop online). Bit 32 was NetCharging (protocol 5): never reuse it in protocol 6

        public int NetFlags
        {
            get { return (IsBlocking ? NetBlocking : 0) | (Dead ? NetDead : 0) | (Airborne ? NetAirborne : 0) | (Glowing ? NetGlow : 0) | (Fallen ? NetFallen : 0) | (Eating && !Dead ? NetEating : 0); }
        }

        // The loop online: who this knight is reviving right now (pid), 255 = nobody. Travels with the state so the others see it.
        public int RevTarget = 255;
        // The loop online: Dead is true for both. Down = hit 0 health, can still be revived for DownLeft seconds; Fallen = time ran out.
        public bool Fallen { get; private set; }
        public float DownLeft { get; private set; }
        public bool Down { get { return Dead && !Fallen; } }
        public float LastHurtTime { get; private set; }                  // when he last took a blow (an interrupted revive starts over)
        bool standRequested;                                             // somebody finished reviving him (the loop online)

        // The loop online: a friend held F next to him for 2 s. Only works while he is down (not fallen for good).
        public void Revive()
        {
            if (remote || !Dead || Fallen) return;
            standRequested = true;
        }

        // Back on his feet with 30% health and a 3 s shield, where he lay.
        void StandUpRevived()
        {
            standRequested = false;
            Dead = false;
            Fallen = false;
            DownLeft = 0f;
            hp = MaxHp * 0.3f;
            rigLean = 0f; rigHop = 0f;
            swYaw = RestYaw; swPitch = RestPitch; swHand = RestHand;
            knockVel = Vector3.zero;
            invulnUntil = 0f;
            Protect(3f);
            Sfx.I.Play(Snd.Ready);
            Fx.I.Ring(Ground(0.06f), 0.4f, 3.5f, new Color(0.7f, 1f, 0.7f, 0.9f), 0.6f);
            Fx.I.Burst(transform.position + Vector3.up * 1.0f, 18, new Color(0.7f, 1f, 0.75f), 2f, 6f, Vector3.up, 0.8f, 0.7f, 2f, 0.1f);
            Game.I.OnKnightRevived(this);
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
            if (Dead && !Fallen && DownLeft > 0f) DownLeft = Mathf.Max(0f, DownLeft - dt);
            Pose(dt, remoteSpeed > 1f && !Airborne && !Dead);
        }

        // A state report from that player's machine (sender time is his clock).
        public void ApplyNet(float senderTime, Vector3 pos, float yaw, int flags, float newHp, float newMaxHp, float newArmor, float newMaxArmor,
            int level, int weapon, int plus, int revTarget = 255)
        {
            if (!remote) return;
            RevTarget = revTarget;
            netPos = pos; netYaw = yaw; netTime = senderTime;
            interp.Push(senderTime, Time.unscaledTime, pos, yaw);
            bool wasDead = Dead, nowDead = (flags & NetDead) != 0;
            bool wasUp = IsBlocking;
            IsBlocking = (flags & NetBlocking) != 0 && !nowDead;
            if (IsBlocking && !wasUp) shieldFlashUntil = Time.time + ParryWindow;
            Airborne = (flags & NetAirborne) != 0;
            netGlow = (flags & NetGlow) != 0;
            bool nowEating = (flags & NetEating) != 0 && !nowDead;
            if (nowEating && !netEating) EatFlagSince = Time.unscaledTime;
            netEating = nowEating;

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

            bool nowFallen = nowDead && (flags & NetFallen) != 0;
            if (nowDead && !wasDead) RemoteDown();
            else if (!nowDead && wasDead) RemoteRevive();
            if (nowFallen && !Fallen) { Fallen = true; DownLeft = 0f; }
            else if (!nowDead && Fallen) Fallen = false;
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
            IsAttacking = false; lockFacing = false;
            rigSpin = 0f;
            if (clock.act == Act.Roll) { rigLean = 0f; rigHop = 0f; }
            body.pivotY = 0f; body.ReleaseShield();
            clock.End();
        }

        void RemoteDown()
        {
            StopAllCoroutines();
            StopAct();
            Dead = true;
            Fallen = false;
            DownLeft = Game.TestDownSeconds;                            // counted here too (the loop online) so everybody can show it
            IsBlocking = false;
            StartCoroutine(FallRoutine());
        }

        void RemoteRevive()
        {
            StopAllCoroutines();
            StopAct();
            Dead = false;
            Fallen = false;
            DownLeft = 0f;
            rigLean = 0f; rigHop = 0f;
            swYaw = RestYaw; swPitch = RestPitch; swHand = RestHand;
            interp.Snap(netPos, transform.eulerAngles.y);
            transform.position = netPos;
            Sfx.I.Play(Snd.Ready, Game.I.RemoteVolume(netPos));
            Fx.I.Ring(new Vector3(netPos.x, 0.06f, netPos.z), 0.4f, 3.5f, new Color(0.7f, 0.9f, 1f, 0.9f), 0.6f);
            if (Net.IsHost && !Game.I.Loop) Game.I.PushEnemiesFrom(spawnPoint);       // same courtesy the local knight gets (not in the loop: he stands where he lay)
        }

        // Replays a move of the remote player for show. kind: 0..2 = swing (target.x = 1: it chained straight on from the last one),
        // 20 = roll, 22 = parry motion. It starts one interpolation delay late, so it lines up with the delayed position on screen.
        // A kind this build does not know is dropped quietly.
        public void PlayRemoteAct(int kind, float speed, Vector3 target)
        {
            if (!remote || Dead) return;
            if (kind == 20)                                   // roll: the position glides on its own, only the show is replayed
            {
                StopAct();
                Vector3 dd = target - netPos; dd.y = 0f;
                bool backwards = dd.sqrMagnitude <= 0.01f || Vector3.Dot(dd.normalized, transform.forward) < -0.5f;
                if (dd.sqrMagnitude > 0.01f) RollFx(dd.normalized);
                LastAction = "Roll";
                ActionsSeen++;
                act = StartCoroutine(RemoteRoll(backwards));
                return;
            }
            if (kind == 21) { LastAction = "Eat"; ActionsSeen++; return; }       // eating: the pose follows his flag
            if (kind == 22)                                   // a parry motion
            {
                if (act != null) StopCoroutine(act);
                body.ReleaseShield();
                act = StartCoroutine(RemoteParry());
                return;
            }
            if (kind < 0 || kind >= KnightAnim.Swings.Length) return;
            StopAct();
            LastAction = KnightAnim.Swings[kind].name;
            ActionsSeen++;
            act = StartCoroutine(RemoteSwing(kind, Mathf.Clamp(speed, MinSwingSpeed, MaxSwingSpeed), target.x > 0.5f ? KnightAnim.ChainPreScale : 1f));
        }

        IEnumerator RemoteSwing(int index, float speed, float preScale)
        {
            if (interp.delay > 0f) yield return new WaitForSeconds(interp.delay);
            yield return SwingRoutine(KnightAnim.Swings[index], index, speed, preScale);
            act = null;
        }

        // Other players' swings and grunts are quieter and fade with distance.
        float Volume { get { return remote ? Game.I.RemoteVolume(transform.position) : 1f; } }

        void LateUpdate()
        {
            body.LateApply(Dead);
        }

        // Used by SelfTest to trigger actions without input devices.
        // Starts swing index (0..2) right away; the chain goes on from there (index + 1 next).
        public void DebugAttack(int index)
        {
            if (Dead || IsAttacking) return;
            swingIndex = Mathf.Clamp(index, 0, KnightAnim.Swings.Length - 1);
            BeginSwing(false);
        }
        public int NextSwing { get { return swingIndex; } }                       // test hook: the swing the next click starts
        public bool DebugRoll(Vector2 dir)                                          // test hook: Space with a direction held; true when the roll started
        {
            int a0 = ActionsSeen;
            if (!Dead) TryRoll(new KnightInput { move = dir });
            return ActionsSeen > a0;
        }

        void Deny(string what)
        {
            StaminaDeniedTime = Time.unscaledTime;
            Game.I.hud.Banner("Not enough stamina for " + what, new Color(1f, 0.5f, 0.4f));
            Sfx.I.Play(Snd.ArmorImpact, 0.35f, 0.7f);
        }


        // ---------------------------------------------------------------- hits

        // Damages every enemy inside a sector (or, with capWidth, a lane in front). A swing calls this every frame of its hit window with the
        // same "once" set, so nothing is hit twice. src is what the host is told: 0..2 = swing. A remote knight's replay hits nothing.
        int HitArea(Vector3 origin, Vector3 dir, float reach, float halfArc, float baseDamage, float pierce,
            float knock, float stun, string source, int src, HashSet<Enemy> once = null, float capWidth = 0f)
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
                if (capWidth > 0f)                                                    // a lane in front (the thrust): along dir 0..reach, across <= half the width
                {
                    float along = Vector3.Dot(to, dir);
                    if (along < -e.radius || along - e.radius > reach) continue;
                    float across = Mathf.Sqrt(Mathf.Max(0f, d * d - along * along));
                    if (across > capWidth * 0.5f + e.radius) continue;
                }
                else
                {
                    if (d - e.radius > reach) continue;
                    if (halfArc < 179f && d > e.radius + 0.25f)
                    {
                        float pad = Mathf.Asin(Mathf.Clamp01(e.radius / d)) * Mathf.Rad2Deg;
                        if (Vector3.Angle(dir, to) > halfArc + pad) continue;
                    }
                }
                if (once != null && !once.Add(e)) continue;                          // one hit per target per swing
                bool crit;
                float dmg = RollDamage(baseDamage, out crit);
                Game.I.HitEnemy(this, e, dmg, pierce, origin, crit, knock, stun, source, src);
                if (e.lastToArmor > e.lastToHp) { armored = true; armorSnd = e.ArmorSound; }   // the armor took most of it
                else flesh = true;
                n++;
            }
            // one sound per kind for the whole swing, however many enemies it went through
            if (flesh) Sfx.I.Play(Snd.Slice);
            if (armored) Sfx.I.Play(armorSnd);
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

        // ---------------------------------------------------------------- swings

        void SetPose(PoseKey k)
        {
            swYaw = k.yaw; swPitch = k.pitch; swHand = k.hand; rigLean = k.lean; rigHop = k.hop; rigSpin = k.twist;
        }

        PoseKey CurrentKey() { return new PoseKey(swYaw, swPitch, swHand, rigLean, rigHop, rigSpin); }

        // The three swings: wind-up, strike, follow-through, recovery. The ActClock (KnightAnim.cs) keeps the time; this
        // routine does what each phase means: facing, the pose, the lunge, the hitbox, the sounds, the click chain.
        // Remote knights run the same routine for show (their hits are skipped in HitArea).
        IEnumerator SwingRoutine(SwingDef d, int index, float speed, float preScale)
        {
            IsAttacking = true;
            IsBlocking = false;
            lockFacing = true;
            queueFromRecovery = false;
            if (!remote)                                                  // face the cursor at once and keep that heading for the whole swing
            {
                Vector3 toAim = aimPoint - transform.position; toAim.y = 0f;
                if (toAim.sqrMagnitude > 0.05f) transform.rotation = Quaternion.LookRotation(toAim);
            }
            clock.Begin(Act.Swing, index, d, speed, preScale);
            float hitFrom, hitTo;
            ActClock.HitWindow(d, speed, preScale, out hitFrom, out hitTo);
            float strikeFrom = ActClock.PhaseStart(d, 1, speed, preScale), strikeLen = ActClock.Duration(d, 1, speed, preScale);
            clashFrom = Time.time + strikeFrom - ClashPad;                 // the strike +-0.1 s can clash with a light blow
            clashTo = Time.time + strikeFrom + strikeLen + ClashPad;

            Vector3 dir = transform.forward; dir.y = 0f;
            var once = new HashSet<Enemy>();
            PoseKey start = CurrentKey();
            bool fxDone = false, juiced = false, chain = false;
            int total = 0;
            float lungeDone = 0f;
            SetPose(ActClock.Pose(d, 0, 0f, start));
            yield return null;
            while (!clock.done)
            {
                float prev = clock.elapsed;
                clock.Tick(Time.deltaTime);
                if (clock.phase >= 1 && !fxDone)
                {
                    fxDone = true;
                    dir = transform.forward; dir.y = 0f;
                    SwingFx(d, dir);
                    SwingSound();
                    Sfx.I.Grunt(index == 2, Volume);                       // the thrust always, the slashes sometimes
                }
                if (d.lunge > 0f && clock.elapsed > strikeFrom)            // the body steps forward over the strike
                {
                    float p = Mathf.Clamp01((clock.elapsed - strikeFrom) / strikeLen);
                    Push(dir * d.lunge * (p - lungeDone));
                    lungeDone = p;
                }
                if (!remote && clock.elapsed >= hitFrom && prev <= hitTo)  // (a slow frame cannot jump over the window)
                {
                    float baseDmg = KnightAnim.BaseDamage * d.damageMul;
                    int n = d.capsule
                        ? HitArea(transform.position, dir, d.length * Weapon.reach, 180f, baseDmg, 0f, d.knock, 0f, d.name, index, once, d.width)
                        : HitArea(transform.position, dir, Reach(d), d.halfArc, baseDmg, 0f, d.knock, 0f, d.name, index, once);
                    total += n;
                    if (n > 0 && !juiced) { juiced = true; Juice(d.shake, 0.16f, d.hitStop); }
                }
                SetPose(ActClock.Pose(d, clock.phase, clock.t, start));
                if (clock.phase >= 3 && queuedClick && !queueFromRecovery) { chain = true; break; }   // a click in the follow-through skips the recovery
                yield return null;
            }
            LastHitCount = total;
            if (chain)
            {
                queuedClick = false;
                BeginSwing(true);                                          // IsAttacking stays up: the next swing takes over at once
                yield break;
            }
            IsAttacking = false;
            lockFacing = false;
            clashFrom = clashTo = 0f;
            queueFromRecovery = false;
            lastAttackEnd = Time.time;
        }

        // The streak of the blade: a ground arc across the cone for the slashes, a lane for the thrust (#EEF3F8, 0.15 s).
        void SwingFx(SwingDef d, Vector3 dir)
        {
            float yaw = transform.eulerAngles.y;
            Vector3 pos = Ground(1.05f);
            Color c = d.fx; c.a = 0.9f;
            if (d.capsule)
            {
                float len = d.length * Weapon.reach;
                Fx.I.Streak(pos + dir * 0.6f, yaw, len - 0.6f, 0.45f, c, 0.15f);
                Fx.I.Burst(pos + dir * 1.2f, 7, new Color(0.75f, 0.92f, 1f), 6f, 13f, dir, 0.15f, 0.18f, 0f, 0.08f);
            }
            else Fx.I.Arc(pos, yaw, 1.0f, Reach(d), -d.halfArc, d.halfArc, c, 0.15f, 0.12f);
        }

        // ---------------------------------------------------------------- taking damage

        // Armor absorbs first. A raised shield facing the blow cuts it by 80% (+2% per BLOCK point) for free (no stamina).
        // Always runs on the machine of the player who owns this knight (the host forwards monster hits to it).
        // by != null: a monster's blow (phase 7) - it can be parried / clashed unless heavy. null (test button H, old tests) = as before.
        public void TakeDamage(float damage, Vector3 from, string source, float knockback = 6f, Enemy by = null, bool heavy = false)
        {
            if (remote || Dead) return;
            if (Rolling)
            {
                // the blow passes through: a small "DODGE" (not on every hit of a flurry)
                if (Time.time - lastDodgeFx > 0.3f)
                {
                    lastDodgeFx = Time.time;
                    DodgeCount++;
                    Vector3 cp = transform.position + new Vector3(0f, 2.6f, 0f);
                    Game.I.hud.Popup(cp, "DODGE", new Color(0.81f, 0.91f, 1f), 22);
                    Fx.I.Burst(transform.position + Vector3.up * 1.2f, 6, new Color(0.8f, 0.92f, 1f), 2f, 5f, Vector3.up, 0.9f, 0.3f, 0f, 0.08f);
                    Sfx.I.Play(Snd.Swing, 0.3f, 1.3f);
                }
                return;
            }
            if (Invulnerable) return;
            Vector3 toSrc = from - transform.position; toSrc.y = 0f;
            Vector3 srcDir = toSrc.sqrMagnitude > 0.0001f ? toSrc.normalized : transform.forward;
            Vector3 chestPos = transform.position + new Vector3(0f, 1.4f, 0f);
            bool facing = Vector3.Angle(transform.forward, srcDir) < 75f;

            // phase 7: parry (shield up within 0.2 s of the press) > clash (his light swing meets the monster's) > block > hit.
            // Neither touches health or armor; the monster is thrown back / stunned (the host does that: Game.ReportDeflect).
            if (by != null && !heavy)
            {
                bool parry = IsBlocking && facing && ParryOpen;
                bool clash = false;
                if (!parry && !IsBlocking && IsAttacking && ClashOpen)
                {
                    Vector3 toE = by.transform.position - transform.position; toE.y = 0f;
                    clash = toE.magnitude <= ClashRange && (toE.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, toE) <= ClashHalfAngle);
                }
                if (parry || clash)
                {
                    Deflected(parry, srcDir, chestPos, by);
                    return;
                }
            }

            LastDamageSource = source;
            LastHurtTime = Time.time;
            bool blocked = false;

            if (IsBlocking && facing)
            {
                // the shield costs stamina: 10 per blow, 4% less per DEF point. With too little left it does not hold: the blow lands in full.
                float cost = BlockStaminaCost * (1f - BlockCostPerDef * progress.Points(Attr.Def));
                if (stamina + 0.001f < cost)
                {
                    Game.I.hud.Popup(chestPos + new Vector3(0f, 1.2f, 0f), "GUARD BROKEN", new Color(1f, 0.5f, 0.4f), 26);
                    Sfx.I.Play(Snd.ArmorImpact, 0.5f, 0.7f);
                    StaminaDeniedTime = Time.unscaledTime;
                }
                else
                {
                    stamina -= cost;
                    lastSpendTime = Time.time;                                       // the stamina does not come back for 0.6 s
                    blocked = true;
                    damage *= 1f - progress.BlockReduction;
                    Sfx.I.Play(Snd.Clash);
                    if (heavy) { HeavyBlockCount++; Game.I.hud.Popup(chestPos + new Vector3(0f, 1.2f, 0f), "HEAVY", new Color(0.78f, 0.28f, 0.23f), 28); }
                    else Game.I.hud.Popup(chestPos + new Vector3(0f, 1.2f, 0f), "BLOCK", new Color(1f, 0.9f, 0.45f), 26);
                    Fx.I.Burst(chestPos + srcDir * 0.6f, 12, new Color(1f, 0.92f, 0.6f), 3f, 8f, srcDir + Vector3.up * 0.4f, 0.7f, 0.25f, 5f, 0.1f);
                }
            }

            damage *= progress.DamageTakenMult;                                      // RESISTANCE: after the shield, before armor and health
            float toArmor = Mathf.Min(armor, damage);
            armor -= toArmor;
            float toHp = damage - toArmor;
            hp = Mathf.Max(0f, hp - toHp);
            lastDamageTime = Time.time;

            if (toArmor >= 0.5f) Game.I.hud.Popup(chestPos + new Vector3(-0.4f, 0.6f, 0f), "-" + Mathf.RoundToInt(toArmor), new Color(0.65f, 0.82f, 1f), 24);
            if (toHp >= 0.5f) Game.I.hud.Popup(chestPos + new Vector3(0.4f, 0.9f, 0f), "-" + Mathf.RoundToInt(toHp), new Color(1f, 0.35f, 0.25f), 28);

            knockVel = -srcDir * (blocked ? knockback * 0.35f : knockback) * progress.KnockbackTakenMult;
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

        // A parry or a clash: feedback here, then the host is told so the monster flies back / is stunned.
        void Deflected(bool parry, Vector3 srcDir, Vector3 chestPos, Enemy by)
        {
            Vector3 mid = Vector3.Lerp(chestPos, by.Center, 0.5f);
            if (parry)
            {
                ParryCount++;
                StartParryMotion();
                Sfx.I.Play(Snd.Clash, 1f, 1.35f);
                Game.I.hud.Popup(chestPos + new Vector3(0f, 1.2f, 0f), "PARRY!", new Color(0.81f, 0.91f, 1f), 32);
                Fx.I.Burst(mid, 18, new Color(0.81f, 0.91f, 1f), 3f, 9f, srcDir + Vector3.up * 0.4f, 0.9f, 0.3f, 4f, 0.1f);
                Fx.I.Ring(new Vector3(mid.x, 0.1f, mid.z), 0.3f, 1.8f, new Color(0.81f, 0.91f, 1f, 0.9f), 0.25f);
                Game.I.Shake(0.16f, 0.12f);
                Game.I.HitStop(0.10f);
            }
            else
            {
                ClashCount++;
                clashTo = 0f;                                                        // one clash per swing
                knockVel = -srcDir * 10f;                                            // thrown back a little; the swing carries on
                Sfx.I.Play(Snd.Clash, 1f, 0.9f);
                Game.I.hud.Popup(chestPos + new Vector3(0f, 1.2f, 0f), "CLASH", Color.white, 28);
                Fx.I.Burst(mid, 14, new Color(0.96f, 0.76f, 0.35f), 3f, 9f, Vector3.up, 0.9f, 0.3f, 4f, 0.1f);
                Fx.I.Ring(new Vector3(mid.x, 0.1f, mid.z), 0.3f, 1.2f, new Color(0.96f, 0.76f, 0.35f, 0.9f), 0.25f);
                Game.I.Shake(0.12f, 0.15f);
                Game.I.HitStop(0.08f);
            }
            Game.I.ReportDeflect(this, by, parry);
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
            ClearActionFlags();                             // (not CancelAction: that would stop this very coroutine)
            IsBlocking = false; Airborne = false;
            jumpY = 0f; rollMoveEnd = 0f; rollInvulnUntil = 0f;
            bool online = Net.Multiplayer;
            bool coop = Game.I.Coop;
            reviveAt = Time.time + (online ? Game.ReviveDelayCoop : Game.ReviveDelaySingle);
            if (coop) { Fallen = false; standRequested = false; DownLeft = Game.TestDownSeconds; }
            Game.I.OnKnightDown(this);                      // YOU DIED screen (the numbers shown are the ones from before the reset)
            yield return FallRoutine();
            if (coop)
            {
                // the loop online: down for DownLeft seconds, a friend can revive him (F, 2 s); out of time = fallen for good
                // (he stays on the ground, Dead stays true, and he watches the others)
                while (DownLeft > 0f && !standRequested) { DownLeft -= Time.deltaTime; yield return null; }
                if (standRequested) { StandUpRevived(); yield break; }
                DownLeft = 0f;
                Fallen = true;
                Game.I.OnKnightFallen(this);
                yield break;
            }
            if (Game.I.Loop)                                // Phase 1 loop: dying ends the run, nobody stands up here
            {
                Game.I.OnLoopDeath();
                yield break;
            }
            yield return new WaitForSeconds(Mathf.Max(0f, reviveAt - Time.time));
            Game.I.OnKnightRevive(this);                    // back to level 1 (swords and Soul stay)
            hp = MaxHp; armor = MaxArmor; stamina = MaxStamina;
            rigLean = 0f; rigHop = 0f;
            swYaw = RestYaw; swPitch = RestPitch; swHand = RestHand;
            knockVel = Vector3.zero;
            transform.position = spawnPoint;
            Game.I.SnapCamera();
            invulnUntil = Time.time + 2.5f;
            Dead = false;
            Sfx.I.Play(Snd.Ready);
            Fx.I.Ring(Ground(0.06f), 0.4f, 3.5f, new Color(0.7f, 0.9f, 1f, 0.9f), 0.6f);
            Fx.I.Burst(transform.position + Vector3.up * 1.0f, 18, new Color(0.8f, 0.92f, 1f), 2f, 6f, Vector3.up, 0.8f, 0.7f, 2f, 0.1f);
        }
    }
}

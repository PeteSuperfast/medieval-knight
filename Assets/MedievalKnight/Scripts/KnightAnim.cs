using UnityEngine;

namespace MedievalKnight
{
    // What the body (coded or from a file) is asked to show. Pure data, no Unity scene objects: the times of every
    // move live here so the game logic, the pose and the self-test all read the same table.
    public enum Act { Idle, Walk, Run, Swing, Roll, Parry, Eat, Down }

    // One key pose of the coded body. yaw / pitch / hand = the sword arm (yaw 0 = straight ahead, +90 = to his right,
    // pitch < 0 = tip up, hand = how far the hand is out); lean = body pitch (+ = forwards), hop = body height,
    // twist = body yaw (+ = to his right).
    public struct PoseKey
    {
        public float yaw, pitch, hand, lean, hop, twist;
        public PoseKey(float yaw, float pitch, float hand, float lean, float hop, float twist)
        {
            this.yaw = yaw; this.pitch = pitch; this.hand = hand; this.lean = lean; this.hop = hop; this.twist = twist;
        }
        public static PoseKey Lerp(PoseKey a, PoseKey b, float t)
        {
            return new PoseKey(Mathf.Lerp(a.yaw, b.yaw, t), Mathf.Lerp(a.pitch, b.pitch, t), Mathf.Lerp(a.hand, b.hand, t),
                Mathf.Lerp(a.lean, b.lean, t), Mathf.Lerp(a.hop, b.hop, t), Mathf.Lerp(a.twist, b.twist, t));
        }
    }

    // What the body shows this frame.
    public struct BodyState
    {
        public Act act;
        public int index;          // which swing (0..2)
        public int phase;          // 0-based phase of the move
        public float t;            // 0..1 inside that phase
        public float speed;        // weapon speed (the swing times were divided by it already)
        public bool blocking, dead;
        public float moveSpeed;    // m/s: walk / run and the pace of the steps
    }

    // A timed move with phases. Times are seconds at weapon speed 1.0.
    public class ActDef
    {
        public string name;
        public float[] phaseTime;          // swing: wind-up, strike, follow-through, recovery
        public bool[] scaledBySpeed;       // is this phase divided by the weapon speed (recovery is not)
        public PoseKey[] keys;             // phase count + 1 key poses (the coded body blends between them)
        public Color fx = Color.white;

        public int Phases { get { return phaseTime.Length; } }
    }

    public class SwingDef : ActDef
    {
        public float damageMul = 1f;
        public float reach = 2.2f, halfArc = 50f;      // sector: reach in metres at weapon reach x1.0, half of the arc in degrees
        public bool capsule;                           // thrust: a lane in front instead of a sector
        public float width, length;                    // capsule: total width and length in metres at weapon reach x1.0
        public float lunge;                            // metres the body steps forward over the strike
        public float shake = 0.1f, hitStop = 0.05f, knock = 3.5f;
        public float clashPad = 0f;
    }

    // Everything about the timing of one move, evaluated from the time since its start (no coroutines, no frames).
    public class ActClock
    {
        public ActDef def;
        public Act act = Act.Idle;
        public int index;
        public float speed = 1f, preScale = 1f;
        public float elapsed;
        public int phase;
        public float t;
        public bool done = true;

        public void Begin(Act a, int idx, ActDef d, float weaponSpeed, float firstPhaseScale = 1f)
        {
            act = a; index = idx; def = d; speed = weaponSpeed; preScale = firstPhaseScale;
            elapsed = 0f; done = false;
            Locate();
        }

        public void End() { done = true; act = Act.Idle; def = null; }

        public void Tick(float dt)
        {
            if (done || def == null) return;
            elapsed += dt;
            Locate();
        }

        // Jumps to the start of a phase (a click during the follow-through skips the recovery).
        public void Skip(int toPhase)
        {
            if (done || def == null) return;
            float e = 0f;
            for (int i = 0; i < toPhase && i < def.Phases; i++) e += Duration(def, i, speed, preScale);
            elapsed = Mathf.Max(elapsed, e + 0.0001f);       // (a hair into the phase: float sums can land just short of its start)
            Locate();
        }

        void Locate()
        {
            Locate(def, speed, preScale, elapsed, out phase, out t);
            if (elapsed >= Total(def, speed, preScale)) done = true;
        }

        public float Total() { return Total(def, speed, preScale); }

        public BodyState State()
        {
            return new BodyState { act = act, index = index, phase = phase, t = t, speed = speed };
        }

        // ------------------------------------------------ pure functions (the self-test checks these without waiting for frames)

        public static float Duration(ActDef d, int phase, float speed, float firstPhaseScale = 1f)
        {
            float s = Mathf.Clamp(speed, Knight.MinSwingSpeed, Knight.MaxSwingSpeed);
            float v = d.phaseTime[phase];
            if (d.scaledBySpeed != null && phase < d.scaledBySpeed.Length && d.scaledBySpeed[phase]) v /= s;
            if (phase == 0) v *= firstPhaseScale;
            return v;
        }

        public static float Total(ActDef d, float speed, float firstPhaseScale = 1f)
        {
            if (d == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < d.Phases; i++) sum += Duration(d, i, speed, firstPhaseScale);
            return sum;
        }

        // The phase and the progress inside it after "elapsed" seconds. Past the end: the last phase at t = 1.
        public static void Locate(ActDef d, float speed, float firstPhaseScale, float elapsed, out int phase, out float t)
        {
            phase = 0; t = 0f;
            if (d == null) return;
            float e = Mathf.Max(0f, elapsed);
            for (int i = 0; i < d.Phases; i++)
            {
                float du = Duration(d, i, speed, firstPhaseScale);
                if (e < du || i == d.Phases - 1)
                {
                    phase = i;
                    t = du > 0.0001f ? Mathf.Clamp01(e / du) : 1f;
                    return;
                }
                e -= du;
            }
        }

        // Seconds from the start of the swing to a phase.
        public static float PhaseStart(ActDef d, int phase, float speed, float firstPhaseScale = 1f)
        {
            float s = 0f;
            for (int i = 0; i < phase; i++) s += Duration(d, i, speed, firstPhaseScale);
            return s;
        }

        // The hitbox of a swing is open from 25% to 85% of its strike phase: [from, to] in seconds since the start of the swing.
        public static void HitWindow(SwingDef d, float speed, float firstPhaseScale, out float from, out float to)
        {
            float s = PhaseStart(d, 1, speed, firstPhaseScale), du = Duration(d, 1, speed, firstPhaseScale);
            from = s + du * KnightAnim.HitFrom;
            to = s + du * KnightAnim.HitTo;
        }

        // The pose of the coded body at a point of a move: an ease per phase between the key poses.
        // start = the pose the body really had when the move began (the first phase blends from it instead of from the rest key).
        public static PoseKey Pose(ActDef d, int phase, float t, PoseKey? start = null)
        {
            if (d == null || d.keys == null || d.keys.Length < 2) return default(PoseKey);
            int p = Mathf.Clamp(phase, 0, d.keys.Length - 2);
            float e = Ease(d, p, t);
            PoseKey a = p == 0 && start.HasValue ? start.Value : d.keys[p];
            return PoseKey.Lerp(a, d.keys[p + 1], e);
        }

        static float Ease(ActDef d, int phase, float t)
        {
            t = Mathf.Clamp01(t);
            if (d is SwingDef)
            {
                if (phase == 0) return 1f - (1f - t) * (1f - t);     // wind-up: ease out
                if (phase == 1) return t * t;                        // strike: accelerates
                if (phase == 2) return 1f - (1f - t) * (1f - t);     // follow-through: slows into the hold
                return t * t * (3f - 2f * t);                        // recovery: smoothstep
            }
            return t * t * (3f - 2f * t);
        }
    }

    // The tables. Constants in code like the rest of the game (no config file): design.md section 3 / 4 / 5 / 6.
    public static class KnightAnim
    {
        public const float BaseDamage = 14f;               // damage of a swing at multiplier 1.0 (before the weapon) - decisions.md D4
        public const float HitFrom = 0.25f, HitTo = 0.85f; // the hitbox window inside the strike phase
        public const float ChainPreScale = 0.7f;           // a click during the follow-through makes the next wind-up 30% shorter
        public const float ChainWindow = 0.4f;             // no click within this time after the recovery = back to swing 1

        static readonly bool[] Scaled4 = { true, true, true, false };

        public static readonly SwingDef[] Swings =
        {
            // 1: diagonal, from the upper left down to the lower right
            new SwingDef { name = "Diagonal Slash", phaseTime = new[] { 0.20f, 0.12f, 0.15f, 0.25f }, scaledBySpeed = Scaled4,
                damageMul = 1.0f, reach = 2.2f, halfArc = 50f, lunge = 0.4f, shake = 0.10f, hitStop = 0.05f, knock = 3.5f,
                fx = new Color(0.933f, 0.953f, 0.973f),
                keys = new[]
                {
                    new PoseKey(55f, -25f, 0.36f, 0f, 0f, 0f),          // rest
                    new PoseKey(-55f, -100f, 0.30f, -8f, 0f, -22f),     // sword raised over the left shoulder
                    new PoseKey(85f, 40f, 0.52f, 14f, 0f, 24f),         // cut down to beside the right hip
                    new PoseKey(95f, 46f, 0.46f, 10f, 0f, 20f),         // carried through
                    new PoseKey(55f, -25f, 0.36f, 0f, 0f, 0f),
                } },
            // 2: sweep, left to right, the widest and the weakest
            new SwingDef { name = "Sweep Slash", phaseTime = new[] { 0.18f, 0.16f, 0.17f, 0.25f }, scaledBySpeed = Scaled4,
                damageMul = 0.9f, reach = 2.0f, halfArc = 75f, lunge = 0f, shake = 0.08f, hitStop = 0.05f, knock = 3.5f,
                fx = new Color(0.933f, 0.953f, 0.973f),
                keys = new[]
                {
                    new PoseKey(55f, -25f, 0.36f, 0f, 0f, 0f),
                    new PoseKey(-105f, -2f, 0.34f, -4f, 0f, -38f),      // sword drawn back to the left at waist height, body wound up
                    new PoseKey(115f, 4f, 0.56f, 8f, 0f, 36f),          // swept across to the right
                    new PoseKey(128f, 6f, 0.50f, 6f, 0f, 32f),
                    new PoseKey(55f, -25f, 0.36f, 0f, 0f, 0f),
                } },
            // 3: thrust, one step forward, the slowest to recover
            new SwingDef { name = "Thrust", phaseTime = new[] { 0.25f, 0.08f, 0.15f, 0.30f }, scaledBySpeed = Scaled4,
                damageMul = 1.2f, reach = 2.8f, halfArc = 0f, capsule = true, width = 0.8f, length = 2.8f, lunge = 1.0f,
                shake = 0.15f, hitStop = 0.06f, knock = 6f,
                fx = new Color(0.933f, 0.953f, 0.973f),
                keys = new[]
                {
                    new PoseKey(55f, -25f, 0.36f, 0f, 0f, 0f),
                    new PoseKey(32f, -4f, 0.02f, -10f, -0.14f, -18f),   // sword pulled to the hip, knees bent
                    new PoseKey(0f, 0f, 1.05f, 15f, -0.04f, 6f),        // driven forward
                    new PoseKey(0f, 0f, 1.0f, 12f, -0.04f, 4f),
                    new PoseKey(55f, -25f, 0.36f, 0f, 0f, 0f),
                } },
        };

        // Roll (Space): crouch, roll over, stand up. 0.4 s at 20 / 60 / 20 %.
        public static readonly ActDef Roll = new ActDef
        {
            name = "Roll", phaseTime = new[] { 0.08f, 0.24f, 0.08f }, scaledBySpeed = new[] { false, false, false },
            fx = new Color(0.8f, 0.92f, 1f),
            keys = new[]
            {
                new PoseKey(55f, -25f, 0.36f, 0f, 0f, 0f),
                new PoseKey(20f, -30f, 0.20f, 28f, -0.25f, 0f),         // tucked in
                new PoseKey(20f, -30f, 0.20f, 360f, -0.25f, 0f),        // over once (the sign flips for a backwards roll)
                new PoseKey(55f, -25f, 0.36f, 360f, 0f, 0f),            // up again
            },
        };

        // Parry (a blow turned away): shield up, the shield knocks it aside, arms a little out, shield pulled back.
        public static readonly ActDef Parry = new ActDef
        {
            name = "Parry", phaseTime = new[] { 0.15f, 0.12f, 0.25f, 0.20f }, scaledBySpeed = new[] { false, false, false, false },
            fx = new Color(0.961f, 0.761f, 0.353f),
            keys = new[]
            {
                new PoseKey(80f, -55f, 0.36f, 0f, 0f, 0f),
                new PoseKey(80f, -55f, 0.36f, -4f, 0f, 0f),             // shield up in front of the chest
                new PoseKey(60f, -40f, 0.50f, 8f, 0f, 14f),             // the shield pushes the blow to the side
                new PoseKey(100f, -30f, 0.44f, 4f, 0f, 10f),            // arms a little wide, holding
                new PoseKey(55f, -25f, 0.36f, 0f, 0f, 0f),
            },
        };

        // Eating (hold F): crouch, take the food up, eat. One phase of 2.0 s, the progress is the clock.
        public const float EatSeconds = 2.0f;
        public static readonly ActDef Eat = new ActDef
        {
            name = "Eat", phaseTime = new[] { EatSeconds }, scaledBySpeed = new[] { false },
            fx = new Color(0.553f, 0.690f, 0.290f),
            keys = new[]
            {
                new PoseKey(55f, -25f, 0.36f, 0f, 0f, 0f),
                new PoseKey(55f, -25f, 0.36f, 0f, 0f, 0f),
            },
        };

        // Weapon speed -> the speed used for the swing times.
        public static float SwingSpeedOf(float weaponSpeed) { return Mathf.Clamp(weaponSpeed, Knight.MinSwingSpeed, Knight.MaxSwingSpeed); }
    }
}

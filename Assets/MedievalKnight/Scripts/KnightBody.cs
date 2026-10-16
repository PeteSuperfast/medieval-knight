using UnityEngine;

namespace MedievalKnight
{
    // What a knight looks like. The Knight (game logic) never touches the pieces of the model itself: it asks its body.
    // There are two kinds: CodedBody (built from boxes and lathes in code - the classic knight, and the fallback) and, later,
    // a body made from a .fbx file with animation clips.
    public abstract class KnightBody
    {
        public Transform Root;             // "Rig": every piece hangs below it; it is the root of the hurt tint
        public Transform SwordMount;       // the weapon model is built under this node (local +Z = from the hand to the tip)
        public Transform ShieldMount;      // the shield node

        public abstract string Kind { get; }                       // "coded" or "file"
        public abstract void SetTint(Color c, float k);            // the whole body incl. sword and shield
        public abstract void SetShieldTint(Color c, float k);
        public abstract void RebuildTint();                        // call after the weapon model was swapped
    }

    // The shield, shared by every body kind.
    public static class KnightGear
    {
        public static readonly Vector3 ShieldRestPos = new Vector3(-0.5f, 1.05f, 0.12f), ShieldBlockPos = new Vector3(-0.05f, 1.12f, 0.55f);

        public static Transform BuildShield(Transform parent, Material plate, Material gold, Material darkSteel)
        {
            Transform shieldNode = Prims.Node(parent, "Shield", ShieldRestPos);
            Vector2[] l = { new Vector2(-0.36f, 0.42f), new Vector2(-0.36f, 0.05f), new Vector2(-0.25f, -0.3f), new Vector2(-0.02f, -0.56f) };
            Vector2[] r = { new Vector2(0.36f, 0.42f), new Vector2(0.36f, 0.05f), new Vector2(0.25f, -0.3f), new Vector2(0.02f, -0.56f) };
            Prims.Part(shieldNode, "Plate", Prims.Strip("shield", l, r, 0.08f), Vector3.zero, Vector3.one, plate);
            Prims.Box(shieldNode, "Top", new Vector3(0f, 0.42f, 0f), new Vector3(0.76f, 0.07f, 0.11f), gold);
            Prims.Box(shieldNode, "CrossV", new Vector3(0f, -0.04f, 0.03f), new Vector3(0.11f, 0.86f, 0.06f), gold);
            Prims.Box(shieldNode, "CrossH", new Vector3(0f, 0.14f, 0.03f), new Vector3(0.66f, 0.11f, 0.06f), gold);
            Prims.Box(shieldNode, "ArmL", new Vector3(0f, 0.05f, -0.16f), new Vector3(0.17f, 0.17f, 0.3f), darkSteel);
            return shieldNode;
        }
    }

    // The knight built from boxes and lathes (the classic model). Its pose is a handful of numbers: the sword arm
    // (swYaw / swPitch / swHand), the body (rigLean / rigHop / rigSpin) and the legs / shield (set by Pose).
    public class CodedBody : KnightBody
    {
        public const float RestYaw = 55f, RestPitch = -25f, RestHand = 0.36f;

        public float swYaw = RestYaw, swPitch = RestPitch, swHand = RestHand;
        public float rigLean, rigHop, rigSpin, walkPhase;

        Transform legL, legR, swordPivot, armR, shieldNode;
        Tint tint, shieldTint;

        public override string Kind { get { return "coded"; } }

        public void Build(Transform owner, int pid)
        {
            Material steel = Prims.Lit(new Color(0.74f, 0.77f, 0.82f), 0.7f, 0.75f);
            Material darkSteel = Prims.Lit(new Color(0.42f, 0.45f, 0.52f), 0.55f, 0.6f);
            Material blue = Prims.Lit(Knight.MainColors[pid & 3]);       // tabard + shield: the player's colour
            Material red = Prims.Lit(Knight.AccentColors[pid & 3]);      // cape + plume
            Material gold = Prims.Lit(new Color(0.96f, 0.78f, 0.22f), 0.7f, 0.8f);
            Material leather = Prims.Lit(new Color(0.35f, 0.22f, 0.12f));
            Material black = Prims.Lit(new Color(0.06f, 0.06f, 0.08f));

            Root = Prims.Node(owner, "Rig", Vector3.zero);
            Transform rig = Root;

            legL = Prims.Node(rig, "LegL", new Vector3(-0.17f, 0.75f, 0f));
            legR = Prims.Node(rig, "LegR", new Vector3(0.17f, 0.75f, 0f));
            foreach (Transform leg in new[] { legL, legR })
            {
                Prims.Box(leg, "Leg", new Vector3(0f, -0.36f, 0f), new Vector3(0.24f, 0.72f, 0.28f), darkSteel);
                Prims.Box(leg, "Boot", new Vector3(0f, -0.68f, 0.05f), new Vector3(0.27f, 0.14f, 0.38f), leather);
            }

            Transform chest = Prims.Node(rig, "Chest", new Vector3(0f, 0.75f, 0f));
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
            SwordMount = Prims.Node(swordPivot, "Sword", new Vector3(0f, 0f, RestHand));
            Prims.Part(SwordMount, "Gauntlet", Prims.Ball(6), Vector3.zero, new Vector3(0.24f, 0.24f, 0.24f), steel);

            // shield arm
            shieldNode = KnightGear.BuildShield(rig, blue, gold, darkSteel);
            ShieldMount = shieldNode;
            shieldTint = new Tint(shieldNode);
        }

        public override void SetTint(Color c, float k) { tint.Set(c, k); }
        public override void SetShieldTint(Color c, float k) { shieldTint.Set(c, k); }
        public override void RebuildTint() { tint = new Tint(Root); }
        public void ClearTint() { if (tint != null) tint.Set(Color.white, 0f); }

        public void ResetPose()
        {
            swYaw = RestYaw; swPitch = RestPitch; swHand = RestHand;
            rigLean = 0f; rigHop = 0f;
        }

        // Idle / walk / block poses, legs and shield (a swing or a roll drives the sword and the body itself while "attacking").
        // Shared by the local knight and by remote knights.
        public void Pose(float dt, bool moving, bool attacking, bool dead, bool blocking)
        {
            if (moving) walkPhase += dt * 13f;
            if (!attacking && !dead)
            {
                float e = 1f - Mathf.Exp(-14f * dt);
                swYaw = Mathf.Lerp(swYaw, blocking ? 80f : RestYaw + (moving ? Mathf.Sin(walkPhase) * 6f : 0f), e);
                swPitch = Mathf.Lerp(swPitch, blocking ? -55f : RestPitch + Mathf.Sin(Time.time * 2.2f) * 2f, e);
                swHand = Mathf.Lerp(swHand, RestHand, e);
                float bob = moving ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.07f : Mathf.Sin(Time.time * 2.2f) * 0.015f;
                rigHop = Mathf.Lerp(rigHop, bob, 1f - Mathf.Exp(-20f * dt));
                rigLean = Mathf.Lerp(rigLean, moving ? 6f : 0f, 1f - Mathf.Exp(-12f * dt));
                rigSpin = 0f;
            }
            float swing = moving && !dead ? Mathf.Sin(walkPhase) * 32f : 0f;
            legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(swing, 0f, 0f), 1f - Mathf.Exp(-18f * dt));
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(-swing, 0f, 0f), 1f - Mathf.Exp(-18f * dt));

            // shield
            float sk = 1f - Mathf.Exp(-18f * dt);
            Vector3 sPos = blocking ? KnightGear.ShieldBlockPos : KnightGear.ShieldRestPos;
            Quaternion sRot = blocking ? Quaternion.Euler(-12f, 5f, 0f) : Quaternion.Euler(-18f, -72f, 0f);
            float sScale = blocking ? 1.3f : 1f;
            shieldNode.localPosition = Vector3.Lerp(shieldNode.localPosition, sPos, sk);
            shieldNode.localRotation = Quaternion.Slerp(shieldNode.localRotation, sRot, sk);
            shieldNode.localScale = Vector3.Lerp(shieldNode.localScale, Vector3.one * sScale, sk);
        }

        // Writes the numbers into the transforms (after everything else moved this frame).
        public void LateApply(bool dead)
        {
            Root.localPosition = new Vector3(0f, rigHop, 0f);
            if (!dead) Root.localRotation = Quaternion.Euler(rigLean, rigSpin, 0f);
            swordPivot.localRotation = Quaternion.Euler(swPitch, swYaw, 0f);
            SwordMount.localPosition = new Vector3(0f, 0f, swHand);
            armR.localPosition = new Vector3(0f, 0f, swHand * 0.5f);
            armR.localScale = new Vector3(0.17f, 0.17f, Mathf.Max(0.1f, swHand));
        }
    }
}

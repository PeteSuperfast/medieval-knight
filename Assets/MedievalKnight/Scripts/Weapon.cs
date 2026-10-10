using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    public enum GuardStyle { Bar, Cup, Wide, Fang, Horns }

    // One sword type. The numbers multiply / add on top of the level-up totals in Progress:
    //   damage      x  on every sword hit (basic combo and the skills Q / E / F; Shield Bash uses the shield)
    //   speed       x  on the basic combo's swing speed (a slower sword also costs more stamina per swing)
    //   critChance  +  flat crit chance,  critMult + flat crit damage multiplier (same hits as "damage")
    //   reach       x  on the reach of the basic combo
    // The rest describes the low-poly model (see Weapons.BuildModel).
    public class WeaponDef
    {
        public int id;
        public string name, rarity, hint, dropHint;
        public float damage = 1f, speed = 1f, critChance = 0f, critMult = 0f, reach = 1f;
        public Color uiColor = Color.white;

        public float bladeLen = 1.25f, bladeWidth = 0.13f, bladeThick = 0.04f, tipLen = 0.22f, gripLen = 0.3f;
        public Color bladeColor = new Color(0.9f, 0.93f, 0.98f), guardColor = new Color(0.96f, 0.78f, 0.22f), gripColor = new Color(0.35f, 0.22f, 0.12f);
        public Color coreColor = Color.clear;       // a = 0: no glowing line down the blade
        public GuardStyle guard = GuardStyle.Bar;
        public float curve = 0f;                    // degrees the outer half of the blade bends sideways

        public string StatLine()
        {
            return "DMG x" + damage.ToString("0.00") + "   SPEED x" + speed.ToString("0.00")
                + "   CRIT +" + (critChance * 100f).ToString("0") + "% / +" + critMult.ToString("0.0") + "x   REACH x" + reach.ToString("0.00");
        }
    }

    // The sword table and the code that builds a sword model (held by the knight or lying on the ground).
    public static class Weapons
    {
        public const int Iron = 0, Rapier = 1, Greatsword = 2, Fang = 3, Boss = 4;
        public const int MaxPlus = 5;                   // a duplicate refines the sword: +1 .. +5
        public const float DamagePerPlus = 0.05f;       // each "+" adds 5% of the sword's damage
        public const float DuplicateExp = 15f;          // EXP for a duplicate of a sword that is already +5

        // drop chance per slime kind (green / blue / red) and the share of each sword in a slime drop
        public static readonly float[] SlimeDropChance = { 0.04f, 0.10f, 0.20f };
        public static readonly int[] SlimeDropIds = { Rapier, Greatsword, Fang };
        public static readonly float[] SlimeDropWeights = { 0.45f, 0.35f, 0.20f };
        public const float MinotaurDropChance = 1f;     // always the boss sword

        public static readonly WeaponDef[] All =
        {
            new WeaponDef { id = Iron, name = "Iron Sword", rarity = "Common", hint = "Balanced starter blade", dropHint = "Starting weapon",
                uiColor = new Color(0.88f, 0.9f, 0.94f) },

            new WeaponDef { id = Rapier, name = "Swift Rapier", rarity = "Uncommon", hint = "Fast and light, weak hits", dropHint = "Drops from slimes",
                damage = 0.75f, speed = 1.35f, critChance = 0.05f, reach = 1.05f, uiColor = new Color(0.45f, 0.95f, 0.8f),
                bladeLen = 1.4f, bladeWidth = 0.06f, bladeThick = 0.045f, tipLen = 0.26f, gripLen = 0.26f,
                bladeColor = new Color(0.8f, 0.97f, 0.95f), guardColor = new Color(0.25f, 0.75f, 0.68f), gripColor = new Color(0.12f, 0.14f, 0.2f),
                guard = GuardStyle.Cup },

            new WeaponDef { id = Greatsword, name = "Iron Greatsword", rarity = "Uncommon", hint = "Slow, heavy, long reach", dropHint = "Drops from slimes",
                damage = 1.7f, speed = 0.72f, reach = 1.25f, uiColor = new Color(0.55f, 0.7f, 1f),
                bladeLen = 1.75f, bladeWidth = 0.24f, bladeThick = 0.055f, tipLen = 0.3f, gripLen = 0.46f,
                bladeColor = new Color(0.55f, 0.6f, 0.7f), guardColor = new Color(0.3f, 0.32f, 0.38f), gripColor = new Color(0.25f, 0.13f, 0.08f),
                coreColor = new Color(0.25f, 0.27f, 0.33f, 1f), guard = GuardStyle.Wide },

            new WeaponDef { id = Fang, name = "Crimson Fang", rarity = "Rare", hint = "Short curved blade, deadly crits", dropHint = "Drops from slimes (rare)",
                damage = 0.9f, speed = 1.1f, critChance = 0.20f, critMult = 1.0f, reach = 0.9f, uiColor = new Color(1f, 0.4f, 0.45f),
                bladeLen = 1.0f, bladeWidth = 0.15f, bladeThick = 0.04f, tipLen = 0.3f, gripLen = 0.28f,
                bladeColor = new Color(0.88f, 0.2f, 0.24f), guardColor = new Color(0.12f, 0.1f, 0.12f), gripColor = new Color(0.4f, 0.08f, 0.1f),
                guard = GuardStyle.Fang, curve = 16f },

            new WeaponDef { id = Boss, name = "Horned King's Blade", rarity = "Legendary", hint = "The Minotaur's golden sword", dropHint = "Drops from the Minotaur",
                damage = 1.5f, speed = 1.1f, critChance = 0.10f, critMult = 0.5f, reach = 1.15f, uiColor = new Color(1f, 0.72f, 0.2f),
                bladeLen = 1.55f, bladeWidth = 0.17f, bladeThick = 0.05f, tipLen = 0.32f, gripLen = 0.34f,
                bladeColor = new Color(1f, 0.82f, 0.3f), guardColor = new Color(0.95f, 0.92f, 0.8f), gripColor = new Color(0.5f, 0.08f, 0.08f),
                coreColor = new Color(1f, 0.45f, 0.1f, 1f), guard = GuardStyle.Horns },
        };

        // Builds grip, pommel, guard and blade under "parent". Local +Z runs from the hand to the tip.
        public static Transform BuildModel(Transform parent, WeaponDef w)
        {
            Transform m = Prims.Node(parent, "Model", Vector3.zero);
            bool shiny = w.id != Fang;
            Material blade = Prims.Lit(w.bladeColor, 0.85f, shiny ? 0.9f : 0.55f);
            Material guard = Prims.Lit(w.guardColor, 0.7f, 0.8f);
            Material grip = Prims.Lit(w.gripColor);

            float gripBack = w.gripLen - 0.17f;                 // how far the grip sticks out behind the hand
            Prims.Box(m, "Grip", new Vector3(0f, 0f, 0.17f - w.gripLen * 0.5f), new Vector3(0.06f, 0.06f, w.gripLen), grip);
            float pom = w.guard == GuardStyle.Wide ? 0.17f : 0.13f;
            Prims.Part(m, "Pommel", Prims.Ball(6), new Vector3(0f, 0f, -gripBack - 0.03f), new Vector3(pom, pom, pom), guard);

            const float gz = 0.2f;                              // the guard sits here, the blade starts just after it
            switch (w.guard)
            {
                case GuardStyle.Cup:        // rapier: round bowl over the hand and a thin cross bar
                    Prims.Part(m, "Cup", Prims.Ball(8), new Vector3(0f, 0f, gz - 0.02f), new Vector3(0.3f, 0.3f, 0.16f), guard);
                    Prims.Box(m, "Quillon", new Vector3(0f, 0f, gz + 0.04f), new Vector3(0.4f, 0.035f, 0.035f), guard);
                    break;
                case GuardStyle.Wide:       // greatsword: long bar with blocks at both ends, then a bare ricasso
                    Prims.Box(m, "Guard", new Vector3(0f, 0f, gz), new Vector3(0.74f, 0.09f, 0.1f), guard);
                    for (int s = -1; s <= 1; s += 2)
                        Prims.Box(m, "GuardEnd", new Vector3(0.36f * s, 0f, gz + 0.04f), new Vector3(0.1f, 0.12f, 0.2f), guard);
                    Prims.Box(m, "Ricasso", new Vector3(0f, 0f, gz + 0.14f), new Vector3(w.bladeWidth * 0.62f, w.bladeThick + 0.03f, 0.2f), guard);
                    break;
                case GuardStyle.Fang:       // two short prongs swept towards the blade
                    for (int s = -1; s <= 1; s += 2)
                        Prims.Box(m, "Prong", new Vector3(0.13f * s, 0f, gz + 0.05f), new Vector3(0.3f, 0.07f, 0.08f), guard).Rot(0f, -38f * s, 0f);
                    Prims.Part(m, "Gem", Prims.Ball(6), new Vector3(0f, 0f, gz), new Vector3(0.12f, 0.14f, 0.12f), Prims.Glow(new Color(1f, 0.3f, 0.3f)));
                    break;
                case GuardStyle.Horns:      // a pair of bull horns curving up along the blade, with a red gem
                    Prims.Box(m, "Guard", new Vector3(0f, 0f, gz), new Vector3(0.36f, 0.1f, 0.11f), Prims.Lit(new Color(0.96f, 0.78f, 0.22f), 0.7f, 0.8f));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Transform h = Prims.Node(m, "Horn", new Vector3(0.16f * s, 0f, gz));
                        h.localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, -62f * s);    // cone axis Y -> out to the side
                        Prims.Part(h, "HornBase", Prims.Lathe("hornBase", 6, 0.5f, 0f, 0.34f, 1f, 0f, 1f), Vector3.zero, new Vector3(0.16f, 0.2f, 0.16f), guard);
                        Transform tip = Prims.Node(h, "TipPivot", new Vector3(0f, 0.19f, 0f));
                        tip.localRotation = Quaternion.Euler(0f, 0f, 52f * s);
                        Prims.Part(tip, "HornTip", Prims.Cone(6), Vector3.zero, new Vector3(0.11f, 0.3f, 0.11f), guard);
                    }
                    Prims.Part(m, "Gem", Prims.Ball(6), new Vector3(0f, 0f, gz), new Vector3(0.13f, 0.16f, 0.13f), Prims.Glow(new Color(1f, 0.2f, 0.15f)));
                    break;
                default:                    // plain cross guard
                    Prims.Box(m, "Guard", new Vector3(0f, 0f, gz), new Vector3(0.46f, 0.08f, 0.09f), guard);
                    break;
            }

            // blade: one straight piece, or two pieces with the outer one bent for a curved look
            float z0 = gz + 0.035f;
            Mesh tipMesh = Prims.Lathe("swordTip", 4, 0.5f, 0f, 0f, 1f);
            Transform end = m;
            float endZ = z0 + w.bladeLen;
            if (w.curve != 0f)
            {
                float half = w.bladeLen * 0.5f;
                Prims.Box(m, "Blade", new Vector3(0f, 0f, z0 + half * 0.5f), new Vector3(w.bladeWidth, w.bladeThick, half), blade);
                end = Prims.Node(m, "BladeBend", new Vector3(0f, 0f, z0 + half - 0.02f));
                end.localRotation = Quaternion.Euler(0f, w.curve, 0f);
                Prims.Box(end, "BladeOuter", new Vector3(0f, 0f, half * 0.5f), new Vector3(w.bladeWidth * 0.92f, w.bladeThick, half), blade);
                endZ = half;
            }
            else
            {
                Prims.Box(m, "Blade", new Vector3(0f, 0f, z0 + w.bladeLen * 0.5f), new Vector3(w.bladeWidth, w.bladeThick, w.bladeLen), blade);
                if (w.coreColor.a > 0f)
                {
                    Material core = w.id == Boss ? Prims.Glow(w.coreColor) : Prims.Lit(w.coreColor, 0.5f, 0.6f);
                    Prims.Box(m, "Fuller", new Vector3(0f, 0f, z0 + w.bladeLen * 0.47f), new Vector3(w.bladeWidth * 0.3f, w.bladeThick + 0.012f, w.bladeLen * 0.86f), core);
                }
            }
            Prims.Part(end, "Tip", tipMesh, new Vector3(0f, 0f, endZ - 0.005f), new Vector3(w.bladeWidth * (w.curve != 0f ? 0.92f : 1f), w.tipLen, w.bladeThick + 0.005f), blade).Rot(90f, 0f, 0f);
            return m;
        }

        // Which sword a slime drop is. roll = a uniform 0..1 number from the caller.
        public static int SlimeDrop(float roll)
        {
            float acc = 0f;
            for (int i = 0; i < SlimeDropIds.Length; i++)
            {
                acc += SlimeDropWeights[i];
                if (roll < acc) return SlimeDropIds[i];
            }
            return SlimeDropIds[SlimeDropIds.Length - 1];
        }
    }

    // What one knight owns: which swords, how refined each is, and the one equipped slot.
    // An instance belongs to a Knight (nothing here is static), so several players can each have one.
    public class Inventory
    {
        public readonly bool[] owned = new bool[Weapons.All.Length];
        public readonly int[] plus = new int[Weapons.All.Length];
        public int equipped = Weapons.Iron;

        public Inventory() { Reset(); }

        public void Reset()
        {
            for (int i = 0; i < owned.Length; i++) { owned[i] = false; plus[i] = 0; }
            owned[Weapons.Iron] = true;
            equipped = Weapons.Iron;
        }

        public WeaponDef Weapon { get { return Weapons.All[equipped]; } }
        public int OwnedCount { get { int n = 0; foreach (bool b in owned) if (b) n++; return n; } }

        // damage multiplier of a sword including its "+N" refinement
        public float DamageOf(int id) { return Weapons.All[id].damage * (1f + Weapons.DamagePerPlus * plus[id]); }
        public float Damage { get { return DamageOf(equipped); } }

        public string NameOf(int id) { return Weapons.All[id].name + (plus[id] > 0 ? " +" + plus[id] : ""); }

        // Returns 0 = new sword, 1 = duplicate refined it (+1), 2 = duplicate of a maxed sword (worth EXP instead).
        public int Add(int id)
        {
            if (!owned[id]) { owned[id] = true; return 0; }
            if (plus[id] < Weapons.MaxPlus) { plus[id]++; return 1; }
            return 2;
        }
    }

    // A sword lying in the arena: it floats, turns and glows in its rarity colour until a knight walks over it.
    public class Pickup : MonoBehaviour
    {
        public static readonly List<Pickup> All = new List<Pickup>();
        public const float CollectRadius = 1.25f, Lifetime = 90f;

        public int weaponId;
        public int netId;                 // the same number on every machine in an online game
        public int owner = -1;            // -1: anyone can take it; otherwise only the player with this pid (co-op boss sword)
        public bool hidden;               // somebody else's personal drop: it exists for the rules but is not shown here
        public float age;
        Transform spin, ring, beam;
        Color color;

        public static Pickup Spawn(int weaponId, Vector3 pos, Transform parent, int owner = -1, int netId = 0)
        {
            var go = new GameObject("Pickup " + Weapons.All[weaponId].name);
            go.transform.SetParent(parent, false);
            float lim = Game.ArenaHalf - 1f;
            go.transform.position = new Vector3(Mathf.Clamp(pos.x, -lim, lim), 0f, Mathf.Clamp(pos.z, -lim, lim));
            var p = go.AddComponent<Pickup>();
            p.owner = owner;
            p.netId = netId;
            p.hidden = owner >= 0 && owner != Net.LocalPid;
            p.Setup(weaponId);
            return p;
        }

        public static Pickup Find(int netId)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].netId == netId) return All[i];
            return null;
        }

        void Setup(int id)
        {
            weaponId = id;
            WeaponDef w = Weapons.All[id];
            color = w.uiColor;
            if (hidden) return;
            spin = Prims.Node(transform, "Spin", new Vector3(0f, 1.1f, 0f));
            Transform tilt = Prims.Node(spin, "Tilt", Vector3.zero);
            tilt.localRotation = Quaternion.Euler(-62f, 0f, 0f);                 // tip up, leaning so the top-down camera sees the blade
            Transform model = Weapons.BuildModel(tilt, w);
            model.localPosition = new Vector3(0f, 0f, -0.2f - w.bladeLen * 0.5f);   // turn around the middle of the sword
            model.localScale = Vector3.one * 1.25f;
            beam = Prims.Box(transform, "Beam", new Vector3(0f, 1.6f, 0f), new Vector3(0.07f, 3.2f, 0.07f), Prims.Glow(color), false);
            ring = Fx.I.MakeDecal(Prims.Arc(0.62f, 0.85f, 0f, 360f), "PickupRing");
            ring.gameObject.SetActive(true);
            Fx.I.Ring(new Vector3(transform.position.x, 0.06f, transform.position.z), 0.3f, 2.2f, new Color(color.r, color.g, color.b, 0.9f), 0.5f);
            Fx.I.Burst(transform.position + Vector3.up, 14, color, 2f, 6f, Vector3.up, 0.8f, 0.6f, 8f, 0.12f);
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
        void OnDestroy() { if (ring != null) Destroy(ring.gameObject); }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (hidden)
            {
                if (age >= Lifetime && Net.Authority) Destroy(gameObject);
                return;
            }
            spin.localRotation = Quaternion.Euler(0f, age * 110f, 0f);
            spin.localPosition = new Vector3(0f, 1.1f + Mathf.Sin(age * 2.6f) * 0.12f, 0f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * 5f);
            ring.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
            ring.localScale = Vector3.one * (1f + 0.12f * pulse);
            bool fading = age > Lifetime - 8f;                                     // blinks before it disappears
            bool show = !fading || Mathf.Sin(age * 18f) > -0.2f;
            Fx.I.SetDecal(ring, new Color(color.r, color.g, color.b, show ? 0.55f + 0.3f * pulse : 0.1f));
            if (beam.gameObject.activeSelf != show) beam.gameObject.SetActive(show);
            if (age >= Lifetime && Net.Authority) Destroy(gameObject);   // on a client the host's list removes it
        }
    }
}

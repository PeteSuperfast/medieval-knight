using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    public enum StatKind { Damage, Speed, Crit }
    public enum Attr { Hp, Block, Stamina }

    // One of the three cards offered on level-up.
    public class UpgradeOffer
    {
        public StatKind stat;
        public int target;        // 0 = all attacks, 1..4 = skill Q / E / R / F
        public float amount;      // the rolled "+Nx" (0.5 .. 2.0)
        public string title, line, detail;
    }

    // Level 1..25, EXP curve, and the upgrade totals that feed combat. Dying throws the whole Progress away
    // (Knight.ResetProgress): level, EXP, upgrade cards and stat points start again at level 1.
    //
    // "+Nx" is literal: it is added to a multiplier.
    //   Damage  +Nx : damage multiplier (starts x1.0) += N.                       capped at x12
    //   Speed   +Nx : attack-speed multiplier (starts x1.0) += N.                 capped at x4
    //                 basic attacks animate that much faster (max x3), skill cooldowns are divided by it.
    //   Crit    +Nx : crit damage multiplier (starts x1.5) += N  (cap x10) and
    //                 crit chance (starts 10%) += 5% per 1.0x     (cap 75%).
    // An upgrade is either for ALL attacks (rolls 0.4 .. 0.8) or for one skill only (rolls 0.8 .. 1.6);
    // a skill uses "all" + its own bonus.
    //
    // Every level-up also gives 1 stat point that the player puts into HP, BLOCK or STAMINA (max 10 each;
    // 24 points by level 25, so not everything can be maxed):
    //   HP       +15 max health
    //   BLOCK    +8 max armor, and a raised shield stops 1.5% more of a blow (80% -> at most 95%)
    //   STAMINA  +10 max stamina and +6% stamina regeneration (stamina is only spent by the Space dash)
    // One Progress belongs to one Knight; the equipped sword is combined with these totals in Knight.RollDamage.
    public class Progress
    {
        public const int MaxLevel = 25;
        public const float ExpBase = 40f, ExpGrowth = 1.18f;
        public const float DamageCap = 12f, SpeedCap = 4f, BasicSpeedCap = 3f, CritMultCap = 10f, CritChanceCap = 0.75f;
        public const float BaseCritChance = 0.10f, BaseCritMult = 1.5f, CritChancePerX = 0.05f;
        public const float AllRollMin = 0.4f, AllRollMax = 0.8f, SkillRollMin = 0.8f, SkillRollMax = 1.6f;
        public const int AttrCap = 10;
        public const float HpPerPoint = 15f, ArmorPerPoint = 8f, BaseBlock = 0.80f, BlockPerPoint = 0.015f, StaminaPerPoint = 10f, RegenPerPoint = 0.06f;

        public int level = 1;
        public float exp;                 // EXP inside the current level
        public float totalExp;
        public int pendingChoices;        // level-ups whose upgrade has not been picked yet
        public int kills;
        public readonly float[] dmg = new float[5], spd = new float[5], crit = new float[5];   // [0] = all, [1..4] = skills
        public int statPoints;            // unspent stat points (1 per level-up)
        public readonly int[] attr = new int[3];                                               // points in HP / BLOCK / STAMINA

        // EXP needed to go from "lvl" to "lvl + 1":  round(40 * 1.18^(lvl-1))  -> 40, 47, 56 ... 1800 at 24 > 25
        public static int Need(int lvl) { return Mathf.RoundToInt(ExpBase * Mathf.Pow(ExpGrowth, lvl - 1)); }

        public bool IsMax { get { return level >= MaxLevel; } }

        static float Sum(float[] a, int skill) { return a[0] + (skill >= 0 ? a[skill + 1] : 0f); }

        // skill: -1 = basic sword attack, 0..3 = Q / E / R / F
        public float DamageMult(int skill) { return Mathf.Min(DamageCap, 1f + Sum(dmg, skill)); }
        public float SpeedMult(int skill) { return Mathf.Min(SpeedCap, 1f + Sum(spd, skill)); }
        public float BasicSpeed() { return Mathf.Min(BasicSpeedCap, 1f + spd[0]); }
        public float CritMult(int skill) { return Mathf.Min(CritMultCap, BaseCritMult + Sum(crit, skill)); }
        public float CritChance(int skill) { return Mathf.Min(CritChanceCap, BaseCritChance + CritChancePerX * Sum(crit, skill)); }

        // Test hook: 0 = roll normally, 1 = never crit, 2 = always crit. (The roll itself is Knight.RollDamage.)
        public static int ForceCrit;

        // --- stat points ---
        public int Points(Attr a) { return attr[(int)a]; }
        public float HpBonus { get { return HpPerPoint * attr[(int)Attr.Hp]; } }
        public float ArmorBonus { get { return ArmorPerPoint * attr[(int)Attr.Block]; } }
        public float BlockReduction { get { return BaseBlock + BlockPerPoint * attr[(int)Attr.Block]; } }      // share of a blocked blow that is stopped
        public float StaminaBonus { get { return StaminaPerPoint * attr[(int)Attr.Stamina]; } }
        public float StaminaRegenMult { get { return 1f + RegenPerPoint * attr[(int)Attr.Stamina]; } }
        public bool CanSpend(Attr a) { return statPoints > 0 && attr[(int)a] < AttrCap; }
        public bool CanSpendAny { get { return CanSpend(Attr.Hp) || CanSpend(Attr.Block) || CanSpend(Attr.Stamina); } }

        public bool Spend(Attr a)
        {
            if (!CanSpend(a)) return false;
            attr[(int)a]++;
            statPoints--;
            return true;
        }

        public static string AttrName(Attr a) { return a == Attr.Hp ? "HP" : a == Attr.Block ? "BLOCK" : "STAMINA"; }

        // Returns how many levels were gained.
        public int AddExp(float amount)
        {
            if (IsMax || amount <= 0f) return 0;
            totalExp += amount;
            exp += amount;
            int gained = 0;
            while (!IsMax && exp >= Need(level))
            {
                exp -= Need(level);
                level++;
                pendingChoices++;
                statPoints++;
                gained++;
            }
            if (IsMax) exp = 0f;
            return gained;
        }

        float[] Arr(StatKind s) { return s == StatKind.Damage ? dmg : s == StatKind.Speed ? spd : crit; }

        // Current multiplier a card would change (what the HUD shows before -> after).
        public float Current(StatKind s, int target)
        {
            int skill = target - 1;
            return s == StatKind.Damage ? DamageMult(skill) : s == StatKind.Speed ? SpeedMult(skill) : CritMult(skill);
        }

        static float CapOf(StatKind s) { return s == StatKind.Damage ? DamageCap : s == StatKind.Speed ? SpeedCap : CritMultCap; }

        public void Apply(UpgradeOffer o)
        {
            Arr(o.stat)[o.target] += o.amount;
        }

        // Three distinct cards: one for all attacks and two for two different skills (when not capped).
        public UpgradeOffer[] MakeOffers()
        {
            var global = new List<UpgradeOffer>();
            var skill = new List<UpgradeOffer>();
            for (int s = 0; s < 3; s++)
            {
                for (int t = 0; t <= 4; t++)
                {
                    var kind = (StatKind)s;
                    if (Current(kind, t) >= CapOf(kind) - 0.001f) continue;
                    var o = new UpgradeOffer { stat = kind, target = t };
                    (t == 0 ? global : skill).Add(o);
                }
            }

            var picks = new List<UpgradeOffer>();
            if (global.Count > 0) picks.Add(Take(global, -1, null));
            while (picks.Count < 3 && skill.Count > 0)
            {
                // prefer a skill and a stat that are not on the table yet
                picks.Add(Take(skill, picks.Count, picks));
            }
            while (picks.Count < 3 && global.Count > 0) picks.Add(Take(global, -1, null));

            for (int i = picks.Count - 1; i > 0; i--)      // shuffle
            {
                int j = Random.Range(0, i + 1);
                var tmp = picks[i]; picks[i] = picks[j]; picks[j] = tmp;
            }

            foreach (var o in picks)
            {
                float lo = o.target == 0 ? AllRollMin : SkillRollMin, hi = o.target == 0 ? AllRollMax : SkillRollMax;
                o.amount = Mathf.Round(Random.Range(lo, hi) * 10f) / 10f;
                Describe(o);
            }
            return picks.ToArray();
        }

        static UpgradeOffer Take(List<UpgradeOffer> pool, int unused, List<UpgradeOffer> avoid)
        {
            int best = Random.Range(0, pool.Count);
            if (avoid != null)
            {
                for (int tries = 0; tries < 12; tries++)
                {
                    int i = Random.Range(0, pool.Count);
                    bool clash = false;
                    foreach (var a in avoid)
                        if (a.target == pool[i].target || (a.target != 0 && a.stat == pool[i].stat)) clash = true;
                    if (!clash) { best = i; break; }
                    bool sameSkill = false;
                    foreach (var a in avoid) if (a.target == pool[i].target) sameSkill = true;
                    if (!sameSkill) best = i;
                }
            }
            var o = pool[best];
            pool.RemoveAt(best);
            return o;
        }

        public static string StatName(StatKind s)
        {
            return s == StatKind.Damage ? "Damage" : s == StatKind.Speed ? "Attack Speed" : "Crit";
        }

        public static string TargetName(int target)
        {
            if (target == 0) return "ALL ATTACKS";
            SkillDef sk = Knight.Skills[target - 1];
            return sk.name.ToUpperInvariant() + "  [" + sk.key + "]";
        }

        void Describe(UpgradeOffer o)
        {
            int skill = o.target - 1;
            o.title = TargetName(o.target);
            o.line = StatName(o.stat) + "  +" + o.amount.ToString("0.0") + "x";
            float now = Current(o.stat, o.target);
            float after = Mathf.Min(CapOf(o.stat), now + o.amount);
            if (o.stat == StatKind.Crit)
            {
                float c0 = CritChance(skill) * 100f;
                float c1 = Mathf.Min(CritChanceCap, CritChance(skill) + CritChancePerX * o.amount) * 100f;
                o.detail = "Crit damage  x" + now.ToString("0.0") + " > x" + after.ToString("0.0")
                    + "\nCrit chance  " + c0.ToString("0") + "% > " + c1.ToString("0") + "%";
            }
            else if (o.stat == StatKind.Speed)
            {
                o.detail = "Speed  x" + now.ToString("0.0") + " > x" + after.ToString("0.0")
                    + (o.target == 0 ? "\nFaster sword, shorter cooldowns" : "\nShorter cooldown");
            }
            else
            {
                o.detail = "Damage  x" + now.ToString("0.0") + " > x" + after.ToString("0.0")
                    + (o.target == 0 ? "\nSword and every skill" : "\nThis skill only");
            }
        }
    }
}

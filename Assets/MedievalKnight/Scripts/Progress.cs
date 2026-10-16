using UnityEngine;

namespace MedievalKnight
{
    public enum Attr { Hp, Def, Stamina, Speed, Resist }

    // Level 1..25, EXP curve and the stat points. Damage, attack speed and crit come from the equipped weapon only
    // (Knight.RollDamage); a level-up gives one stat point that the player puts into one of five stats (max 10 each,
    // 24 points at level 25):
    //   HP          +20 max health (and the same much healed at once)
    //   DEF         +10 max armor, a raised shield stops 1.2% more of a blow (80% -> 92% at 10 points),
    //               and a blow on the shield costs 4% less stamina
    //   STAMINA     +15 max stamina (= rolls) and +5% stamina regeneration
    //   SPEED       +3% walking / running speed (not the roll, not the step of a swing)
    //   RESISTANCE  -2.5% of every blow taken (after the shield and before armor) and -5% knock-back
    // One Progress belongs to one Knight.
    public class Progress
    {
        public const int MaxLevel = 25;
        // EXP from level L to L+1 (index L-1): level 25 needs 16,410 EXP in total
        public static readonly int[] NeedTable =
        {
            30, 45, 65, 90, 120, 155, 195, 240, 290, 345, 405, 470,
            540, 620, 710, 810, 920, 1040, 1170, 1310, 1460, 1620, 1790, 1970
        };
        public const float CritMultCap = 10f, CritChanceCap = 0.75f;
        public const float BaseCritChance = 0.10f, BaseCritMult = 1.5f;
        public const int AttrCap = 10, AttrCount = 5;
        public const float HpPerPoint = 20f, ArmorPerPoint = 10f, BaseBlock = 0.80f, BlockPerPoint = 0.012f, StaminaPerPoint = 15f, RegenPerPoint = 0.05f;
        public const float SpeedPerPoint = 0.03f, ResistPerPoint = 0.025f, ResistKnockPerPoint = 0.05f;

        public int level = 1;
        public float exp;                 // EXP inside the current level
        public float totalExp;
        public int pendingChoices;        // level-ups whose stat point has not been spent yet
        public int kills;
        public int statPoints;            // unspent stat points (1 per level-up)
        public readonly int[] attr = new int[AttrCount];                                       // points in HP / DEF / STAMINA / SPEED / RESISTANCE

        // EXP needed to go from "lvl" to "lvl + 1" (NeedTable)
        public static int Need(int lvl) { return NeedTable[Mathf.Clamp(lvl - 1, 0, NeedTable.Length - 1)]; }

        public bool IsMax { get { return level >= MaxLevel; } }

        // Test hook: 0 = roll normally, 1 = never crit, 2 = always crit. (The roll itself is Knight.RollDamage.)
        public static int ForceCrit;

        // --- stat points ---
        public int Points(Attr a) { return attr[(int)a]; }
        public int PointsSpent { get { int n = 0; for (int i = 0; i < attr.Length; i++) n += attr[i]; return n; } }
        public float HpBonus { get { return HpPerPoint * attr[(int)Attr.Hp]; } }
        public float ArmorBonus { get { return ArmorPerPoint * attr[(int)Attr.Def]; } }
        public float BlockReduction { get { return BaseBlock + BlockPerPoint * attr[(int)Attr.Def]; } }      // share of a blocked blow that is stopped
        public float StaminaBonus { get { return StaminaPerPoint * attr[(int)Attr.Stamina]; } }
        public float StaminaRegenMult { get { return 1f + RegenPerPoint * attr[(int)Attr.Stamina]; } }
        public float MoveSpeedMult { get { return 1f + SpeedPerPoint * attr[(int)Attr.Speed]; } }
        public float DamageTakenMult { get { return 1f - ResistPerPoint * attr[(int)Attr.Resist]; } }
        public float KnockbackTakenMult { get { return 1f - ResistKnockPerPoint * attr[(int)Attr.Resist]; } }
        public bool CanSpend(Attr a) { return statPoints > 0 && attr[(int)a] < AttrCap; }
        public bool CanSpendAny { get { for (int i = 0; i < AttrCount; i++) if (CanSpend((Attr)i)) return true; return false; } }

        public bool Spend(Attr a)
        {
            if (!CanSpend(a)) return false;
            attr[(int)a]++;
            statPoints--;
            return true;
        }

        public static string AttrName(Attr a)
        {
            return a == Attr.Hp ? "HP" : a == Attr.Def ? "DEF" : a == Attr.Stamina ? "STAMINA" : a == Attr.Speed ? "SPEED" : "RESISTANCE";
        }

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
    }
}

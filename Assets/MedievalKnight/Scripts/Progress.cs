using UnityEngine;

namespace MedievalKnight
{
    public enum Attr { Hp, Block, Stamina }

    // Level 1..25, EXP curve and the stat points. Damage, attack speed and crit come from the equipped weapon only
    // (Knight.RollDamage); a level-up gives one stat point that the player puts into HP, BLOCK or STAMINA (max 10 each,
    // 24 points at level 25):
    //   HP       +20 max health
    //   BLOCK    +10 max armor, and a raised shield stops 1.2% more of a blow (80% -> 92% at 10 points)
    //   STAMINA  +15 max stamina (= rolls) and +5% stamina regeneration
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
        public const int AttrCap = 10;
        public const float HpPerPoint = 20f, ArmorPerPoint = 10f, BaseBlock = 0.80f, BlockPerPoint = 0.012f, StaminaPerPoint = 15f, RegenPerPoint = 0.05f;

        public int level = 1;
        public float exp;                 // EXP inside the current level
        public float totalExp;
        public int pendingChoices;        // level-ups whose stat point has not been spent yet
        public int kills;
        public int statPoints;            // unspent stat points (1 per level-up)
        public readonly int[] attr = new int[3];                                               // points in HP / BLOCK / STAMINA

        // EXP needed to go from "lvl" to "lvl + 1" (NeedTable)
        public static int Need(int lvl) { return NeedTable[Mathf.Clamp(lvl - 1, 0, NeedTable.Length - 1)]; }

        public bool IsMax { get { return level >= MaxLevel; } }

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
    }
}

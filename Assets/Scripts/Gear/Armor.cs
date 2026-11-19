using UnityEngine;

namespace Hunt.Player
{
    /// <summary>방어구 5부위. 모두 방어도를 주고, 부위마다 고유 효과가 하나씩 있다.</summary>
    public enum ArmorSlot { Head, Body, Legs, Hands, Feet }

    public class ArmorPiece
    {
        public readonly ArmorSlot slot;
        public Grade grade;
        public int upgrade;

        public ArmorPiece(ArmorSlot s) { slot = s; }

        public float Power => Grades.Mult(grade) * Grades.UpgradeMult(upgrade);

        /// <summary>방어도 (상의가 가장 높다).</summary>
        public float Defense => BaseDefense(slot) * Power;

        static float BaseDefense(ArmorSlot s)
        {
            switch (s)
            {
                case ArmorSlot.Body: return 8f;
                case ArmorSlot.Legs: return 5f;
                case ArmorSlot.Head: return 4f;
                default: return 3f;
            }
        }

        public string Name => $"{Grades.Name(grade)} {Armor.SlotName(slot)}" + (upgrade > 0 ? $" +{upgrade}" : "");

        /// <summary>부위 고유 효과 설명.</summary>
        public string EffectText
        {
            get
            {
                switch (slot)
                {
                    case ArmorSlot.Head: return $"역병 감염 -{(1f - Armor.InfectionMultFor(this)) * 100f:0}%";
                    case ArmorSlot.Body: return $"최대 생명 +{Armor.HpFor(this):0}";
                    case ArmorSlot.Legs: return $"스태미너 +{Armor.StaminaFor(this):0}";
                    case ArmorSlot.Hands: return $"장전 속도 +{(Armor.ReloadMultFor(this) - 1f) * 100f:0}%";
                    default: return $"이동 속도 +{(Armor.MoveMultFor(this) - 1f) * 100f:0}%";
                }
            }
        }
    }

    public class ArmorSet
    {
        public readonly ArmorPiece[] pieces =
        {
            new ArmorPiece(ArmorSlot.Head), new ArmorPiece(ArmorSlot.Body), new ArmorPiece(ArmorSlot.Legs),
            new ArmorPiece(ArmorSlot.Hands), new ArmorPiece(ArmorSlot.Feet),
        };

        public ArmorPiece this[ArmorSlot s] => pieces[(int)s];

        public float TotalDefense
        {
            get { float d = 0f; foreach (var p in pieces) d += p.Defense; return d; }
        }

        /// <summary>받는 피해 배율: 방어도 23(기본 세트)이면 약 19% 감소, 수확 체감.</summary>
        public float DamageTakenMult => 100f / (100f + TotalDefense);

        public float BonusHp => Armor.HpFor(this[ArmorSlot.Body]);
        public float BonusStamina => Armor.StaminaFor(this[ArmorSlot.Legs]);
        public float ReloadMult => Armor.ReloadMultFor(this[ArmorSlot.Hands]);
        public float MoveMult => Armor.MoveMultFor(this[ArmorSlot.Feet]);
        public float InfectionMult => Armor.InfectionMultFor(this[ArmorSlot.Head]);
    }

    public static class Armor
    {
        public static string SlotName(ArmorSlot s)
        {
            switch (s)
            {
                case ArmorSlot.Head: return "모자";
                case ArmorSlot.Body: return "상의";
                case ArmorSlot.Legs: return "하의";
                case ArmorSlot.Hands: return "장갑";
                default: return "신발";
            }
        }

        // 일반 +0 = 기본 효과의 하한, 등급·강화에 비례
        public static float HpFor(ArmorPiece p) => 10f * p.Power;
        public static float StaminaFor(ArmorPiece p) => 8f * p.Power;
        public static float ReloadMultFor(ArmorPiece p) => 1f + 0.03f * p.Power;
        public static float MoveMultFor(ArmorPiece p) => 1f + 0.02f * p.Power;
        public static float InfectionMultFor(ArmorPiece p) => 1f / (1f + 0.05f * p.Power);
    }
}

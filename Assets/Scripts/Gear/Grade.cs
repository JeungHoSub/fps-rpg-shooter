using UnityEngine;

namespace Hunt.Player
{
    /// <summary>장비 등급: 일반 → 고급 → 희귀 → 영웅 → 전설. 무기·방어구 공통.</summary>
    public enum Grade { Common, Fine, Rare, Heroic, Legendary }

    public static class Grades
    {
        public const int Max = (int)Grade.Legendary;
        public const int MaxUpgrade = 10;

        public static string Name(Grade g)
        {
            switch (g)
            {
                case Grade.Fine: return "고급";
                case Grade.Rare: return "희귀";
                case Grade.Heroic: return "영웅";
                case Grade.Legendary: return "전설";
                default: return "일반";
            }
        }

        /// <summary>등급 색 (한국풍: 한지 → 쑥 → 쪽빛 → 자주 → 주홍).</summary>
        public static Color Color(Grade g)
        {
            switch (g)
            {
                case Grade.Fine: return new Color(0.55f, 0.75f, 0.45f);
                case Grade.Rare: return new Color(0.4f, 0.6f, 0.95f);
                case Grade.Heroic: return new Color(0.72f, 0.45f, 0.85f);
                case Grade.Legendary: return new Color(0.95f, 0.45f, 0.25f);
                default: return new Color(0.82f, 0.79f, 0.72f);
            }
        }

        /// <summary>등급 배율: 일반 1.0, 고급 1.25, 희귀 1.55, 영웅 1.9, 전설 2.3.</summary>
        public static float Mult(Grade g) => new[] { 1f, 1.25f, 1.55f, 1.9f, 2.3f }[(int)g];

        /// <summary>강화 1단계당 +7%.</summary>
        public static float UpgradeMult(int level) => 1f + 0.07f * level;

        public static int UpgradeCost(Grade g, int level) => 40 + 30 * level + 25 * (int)g;
        public static int GradeUpCost(Grade g) => 150 + 150 * (int)g;
    }
}

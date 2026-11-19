using System.Collections.Generic;
using UnityEngine;

namespace Hunt.Stats
{
    /// <summary>
    /// 특성: 직업 대신 플레이어가 고르는 빌드 요소.
    /// 시작 시 1개, 이후 3레벨마다 무작위 3개 중 1개를 고른다 → 매 판 다른 성장 경로.
    /// </summary>
    public enum Trait
    {
        Bloodlust,      // 피의 갈증
        RunAndGun,      // 런앤건
        SteadyAim,      // 정조준
        ThickSkin,      // 두꺼운 피부
        MutantAffinity, // 변이 친화
        Scavenger,      // 수집가
        QuickHands,     // 빠른 손
        Heavyweight,    // 장사
    }

    public static class Traits
    {
        public const int PickEveryLevels = 3;

        public static readonly Trait[] All = (Trait[])System.Enum.GetValues(typeof(Trait));

        public static string Name(Trait t)
        {
            switch (t)
            {
                case Trait.Bloodlust: return "피의 갈증";
                case Trait.RunAndGun: return "런앤건";
                case Trait.SteadyAim: return "정조준";
                case Trait.ThickSkin: return "두꺼운 피부";
                case Trait.MutantAffinity: return "변이 친화";
                case Trait.Scavenger: return "수집가";
                case Trait.QuickHands: return "빠른 손";
                default: return "장사";
            }
        }

        public static string Describe(Trait t)
        {
            switch (t)
            {
                case Trait.Bloodlust: return "처치 시 HP 4 회복, 저지력 +30%";
                case Trait.RunAndGun: return "이동 중 탄퍼짐 페널티 없음";
                case Trait.SteadyAim: return "1초간 정지하면 약점 데미지 +50%";
                case Trait.ThickSkin: return "최대 HP +20%, 감염 축적 -15%";
                case Trait.MutantAffinity: return "변이 능력 비용 -25%, 지속시간 +25%";
                case Trait.Scavenger: return "골드 +30%, 파츠 드랍 확률 +40%";
                case Trait.QuickHands: return "재장전·무기 교체 속도 +25%";
                default: return "소지 무게 한도 +40%, 칼 피해 +25%";
            }
        }

        /// <summary>보유하지 않은 특성 중 무작위 n개.</summary>
        public static List<Trait> Offer(ICollection<Trait> owned, int n)
        {
            var pool = new List<Trait>();
            foreach (var t in All) if (!owned.Contains(t)) pool.Add(t);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            if (pool.Count > n) pool.RemoveRange(n, pool.Count - n);
            return pool;
        }
    }
}

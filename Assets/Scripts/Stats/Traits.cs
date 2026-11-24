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
        Demolitionist,  // 화약쟁이
        Herbalist,      // 약초꾼
        Headhunter,     // 머리 사냥꾼
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
                case Trait.RunAndGun: return "달리며 쏘기";
                case Trait.SteadyAim: return "숨 고르기";
                case Trait.ThickSkin: return "두꺼운 피부";
                case Trait.MutantAffinity: return "역병 친화";
                case Trait.Scavenger: return "넝마주이";
                case Trait.QuickHands: return "빠른 손";
                case Trait.Demolitionist: return "화약쟁이";
                case Trait.Herbalist: return "약초꾼";
                case Trait.Headhunter: return "머리 사냥꾼";
                default: return "장사";
            }
        }

        public static string Describe(Trait t)
        {
            switch (t)
            {
                case Trait.Bloodlust: return "망자를 쓰러뜨릴 때마다 생명 4 회복, 밀어내기 +30%";
                case Trait.RunAndGun: return "움직이며 쏴도 탄이 퍼지지 않는다";
                case Trait.SteadyAim: return "1초 멈춰 서면 약점 피해 +50%";
                case Trait.ThickSkin: return "최대 생명 +20%, 역병 쌓임 -15%";
                case Trait.MutantAffinity: return "역병 능력 비용 -25%, 지속 시간 +25%";
                case Trait.Scavenger: return "엽전 +30%, 부품 줍는 확률 +40%";
                case Trait.QuickHands: return "장전·무기 교체 25% 빨라짐";
                case Trait.Demolitionist: return "수류탄·지뢰·기관총·화염병 피해 +35%";
                case Trait.Herbalist: return "치료 키트 회복 +40%, 치유 부적 회복 +50%";
                case Trait.Headhunter: return "머리 피해 ×2 → ×2.6";
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

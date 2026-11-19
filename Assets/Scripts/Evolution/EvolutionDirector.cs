using System.Collections.Generic;
using Hunt.Combat;
using UnityEngine;

namespace Hunt.Evolution
{
    public enum WeakPointLocation { Front, Back, Shoulder }

    /// <summary>괴물 종 전체가 공유하는 현재 진화 상태. 다음 웨이브부터 스폰되는 괴물에 적용된다.</summary>
    public class EvolutionProfile
    {
        public int generation;
        public readonly float[] resist = new float[5];
        public int helmetLevel;                 // 머리 장갑 (헤드샷 대응)
        public WeakPointLocation weakPoint = WeakPointLocation.Front;
        public float legHpMult = 1f;            // 다리 강화 (다리 파괴 대응)
        public float knockbackResistBonus;      // 근거리 화력 대응
        public float speedBonus;                // 원거리 화력 대응
        public float defenseBonus;              // 지속 화력 대응

        public readonly List<string> history = new List<string>();

        public float RewardMultiplier => 1f + generation * 0.1f;
        public float PartLuck => generation * 0.15f;
    }

    /// <summary>
    /// A: 적응형 진화 시스템.
    /// 웨이브 동안 플레이어가 "어떤 속성 / 어떤 무기 / 어느 부위"로 피해를 줬는지 집계하고,
    /// 웨이브가 끝나면 가장 많이 당한 방식에 내성을 얻는다.
    /// </summary>
    public class EvolutionDirector : MonoBehaviour
    {
        public static EvolutionDirector Instance { get; private set; }

        public EvolutionProfile Profile { get; private set; } = new EvolutionProfile();

        readonly float[] byType = new float[5];
        readonly float[] byZone = new float[5];
        readonly float[] byClass = new float[System.Enum.GetValues(typeof(WeaponClass)).Length];
        float total;

        // 이번 웨이브 통계 (UI 표시용)
        public float Total => total;
        public float TypeShare(DamageType t) => total > 0 ? byType[(int)t] / total : 0f;
        public float ZoneShare(HitZone z) => total > 0 ? byZone[(int)z] / total : 0f;
        public float ClassShare(WeaponClass c) => total > 0 ? byClass[(int)c] / total : 0f;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Record(DamageType type, WeaponClass cls, HitZone zone, float amount)
        {
            if (amount <= 0) return;
            byType[(int)type] += amount;
            byZone[(int)zone] += amount;
            byClass[(int)cls] += amount;
            total += amount;
        }

        /// <summary>웨이브 종료 시 호출. 진화 내역 메시지를 반환한다.</summary>
        public List<string> Evolve()
        {
            var msgs = new List<string>();
            if (total < 500f) { ResetCounters(); return msgs; }

            var p = Profile;

            // 1) 속성 적응
            for (int i = 0; i < 5; i++)
            {
                float share = byType[i] / total;
                var t = (DamageType)i;
                if (share > 0.45f && p.resist[i] < 0.6f)
                {
                    p.resist[i] = Mathf.Min(0.6f, p.resist[i] + 0.2f);
                    msgs.Add($"{CombatNames.Of(t)} 내성 획득 → {Mathf.RoundToInt(p.resist[i] * 100)}%");
                }
                else if (share < 0.1f && p.resist[i] > 0f)
                {
                    p.resist[i] = Mathf.Max(0f, p.resist[i] - 0.1f); // 안 쓰는 속성 내성은 퇴화
                }
            }

            // 2) 부위 적응
            if (byZone[(int)HitZone.Head] / total > 0.3f && p.helmetLevel < 3)
            {
                p.helmetLevel++;
                msgs.Add($"골질 투구 생성 (Lv.{p.helmetLevel}) — 머리를 장갑이 덮는다");
            }
            if (byZone[(int)HitZone.WeakPoint] / total > 0.25f)
            {
                var prev = p.weakPoint;
                p.weakPoint = (WeakPointLocation)(((int)prev + 1 + Random.Range(0, 2)) % 3);
                msgs.Add($"약점 이동: {LocName(prev)} → {LocName(p.weakPoint)}");
            }
            if (byZone[(int)HitZone.Leg] / total > 0.2f)
            {
                p.legHpMult += 0.5f;
                msgs.Add("다리 근육 비대화 — 다리 파괴가 어려워진다");
            }

            // 3) 무기 거리 적응
            float close = (byClass[(int)WeaponClass.Shotgun] + byClass[(int)WeaponClass.SMG] + byClass[(int)WeaponClass.Pistol]) / total;
            float far = byClass[(int)WeaponClass.Sniper] / total;
            float sustained = byClass[(int)WeaponClass.LMG] / total;
            if (close > 0.5f)
            {
                p.knockbackResistBonus = Mathf.Min(0.6f, p.knockbackResistBonus + 0.15f);
                msgs.Add("가시 피부 — 넉백 저항 증가");
            }
            if (far > 0.4f)
            {
                p.speedBonus = Mathf.Min(0.6f, p.speedBonus + 0.15f);
                msgs.Add("질주 본능 — 이동 속도 증가");
            }
            if (sustained > 0.5f)
            {
                p.defenseBonus += 3f;
                msgs.Add("외피 경화 — 방어력 증가");
            }

            if (msgs.Count > 0)
            {
                p.generation++;
                foreach (var m in msgs) p.history.Add($"{p.generation}세대: {m}");
            }
            ResetCounters();
            return msgs;
        }

        public static string LocName(WeakPointLocation l)
        {
            switch (l)
            {
                case WeakPointLocation.Back: return "등";
                case WeakPointLocation.Shoulder: return "어깨";
                default: return "가슴";
            }
        }

        void ResetCounters()
        {
            System.Array.Clear(byType, 0, byType.Length);
            System.Array.Clear(byZone, 0, byZone.Length);
            System.Array.Clear(byClass, 0, byClass.Length);
            total = 0f;
        }
    }
}

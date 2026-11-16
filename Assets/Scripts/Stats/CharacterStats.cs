using System;
using System.Collections.Generic;
using Hunt.Data;
using UnityEngine;

namespace Hunt.Stats
{
    public enum StatType { STR, AGI, CTL, VIT, FOC, TEC }

    [Serializable]
    public class StatBlock
    {
        public float str, agi, ctl, vit, foc, tec;

        public StatBlock() { }

        public StatBlock(float str, float agi, float ctl, float vit, float foc, float tec)
        {
            this.str = str; this.agi = agi; this.ctl = ctl; this.vit = vit; this.foc = foc; this.tec = tec;
        }

        public float Get(StatType t)
        {
            switch (t)
            {
                case StatType.STR: return str;
                case StatType.AGI: return agi;
                case StatType.CTL: return ctl;
                case StatType.VIT: return vit;
                case StatType.FOC: return foc;
                default: return tec;
            }
        }
    }

    /// <summary>
    /// 캐릭터 스탯 = 기본치 + 레벨 성장치 + 투자 포인트.
    /// 모든 파생 수치(이동속도, 반동 등)는 여기서만 계산한다 → 밸런스 조정 지점이 한 곳.
    /// </summary>
    public class CharacterStats
    {
        public static readonly StatType[] All = (StatType[])Enum.GetValues(typeof(StatType));

        public readonly CharacterData data;
        public int Level { get; private set; } = 1;
        readonly int[] allocated = new int[6];
        public readonly HashSet<Trait> traits = new HashSet<Trait>();

        public CharacterStats(CharacterData data) { this.data = data; }

        public void SetLevel(int level) { Level = Mathf.Max(1, level); }
        public void Allocate(StatType t) { allocated[(int)t]++; }
        public void Deallocate(StatType t) { if (allocated[(int)t] > 0) allocated[(int)t]--; }
        public int Allocated(StatType t) => allocated[(int)t];
        public bool Has(Trait t) => traits.Contains(t);

        public float Get(StatType t) =>
            data.baseStats.Get(t) + Mathf.Floor(data.growthPerLevel.Get(t) * (Level - 1)) + allocated[(int)t];

        float STR => Get(StatType.STR);
        float AGI => Get(StatType.AGI);
        float CTL => Get(StatType.CTL);
        float VIT => Get(StatType.VIT);
        float FOC => Get(StatType.FOC);
        float TEC => Get(StatType.TEC);

        // ---- 힘 ----
        public float CarryCapacity => (10f + STR * 1.5f) * (Has(Trait.Heavyweight) ? 1.4f : 1f);
        public float KnockbackMult => (1f + STR * 0.01f) * (Has(Trait.Bloodlust) ? 1.3f : 1f);

        // ---- 민첩 ----
        public float MoveSpeed => 5f * (1f + AGI * 0.01f);
        public float ReloadSpeedMult => (1f + AGI * 0.015f) * (Has(Trait.QuickHands) ? 1.25f : 1f);
        public float SwapSpeedMult => (1f + AGI * 0.02f) * (Has(Trait.QuickHands) ? 1.25f : 1f);

        // ---- 제어 (C: 반동을 "없애는" 게 아니라 "예측 가능하게" 만든다) ----
        /// <summary>고정 반동 패턴의 크기. 최대 20%까지만 줄어든다 — 패턴 자체는 유지.</summary>
        public float RecoilPatternScale => 1f - Mathf.Min(0.2f, CTL * 0.005f);
        /// <summary>패턴 위에 얹히는 무작위 흔들림. 제어가 높을수록 반동이 "외울 수 있는" 형태가 된다.</summary>
        public float RecoilNoise => 1f / (1f + CTL * 0.08f);
        /// <summary>사격 중지 후 조준점이 돌아오는 속도.</summary>
        public float RecoilRecovery => 6f * (1f + CTL * 0.03f);
        public float SpreadMult => 1f / (1f + CTL * 0.02f);
        public float AdsSpeedMult => 1f + CTL * 0.015f;

        // ---- 체력 ----
        public float MaxHp => (100f + VIT * 10f) * (Has(Trait.ThickSkin) ? 1.2f : 1f);
        public float InfectionResistMult => 1f / (1f + VIT * 0.02f) * (Has(Trait.ThickSkin) ? 0.85f : 1f);

        // ---- 집중 ----
        public float CritChance => 0.05f + FOC * 0.005f;
        public float CritMultiplier => 1.5f + FOC * 0.01f;
        public float WeakPointBonus => FOC * 0.02f;

        // ---- 기술 (변이 능력 효율) ----
        public float AbilityCostMult => 1f / (1f + TEC * 0.02f) * (Has(Trait.MutantAffinity) ? 0.75f : 1f);
        public float AbilityDurationMult => (1f + TEC * 0.01f) * (Has(Trait.MutantAffinity) ? 1.25f : 1f);

        /// <summary>총 장비 무게가 허용치를 넘으면 0.5까지 감소하는 배율.</summary>
        public float WeightPenalty(float totalWeight)
        {
            float over = Mathf.Max(0f, totalWeight - CarryCapacity) / CarryCapacity;
            return 1f - Mathf.Min(0.5f, over);
        }
    }
}

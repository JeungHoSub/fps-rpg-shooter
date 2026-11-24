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
        /// <summary>화면에 보이고 포인트를 쓰는 능력치 (제어는 생명에 통합되어 빠짐).</summary>
        public static readonly StatType[] Visible = { StatType.STR, StatType.AGI, StatType.VIT, StatType.FOC, StatType.TEC };

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

        // ---- 몰빵 방지: 수확 체감 ----
        // 16까지는 1점 = 1, 16~28은 1점 = 0.5, 그 위는 1점 = 0.25.
        // 한 능력치에 몰아 넣을수록 손해이고, 고르게 올리면 '균형' 보너스를 받는다.
        public const float SoftCap1 = 16f, SoftCap2 = 28f;

        public static float Soft(float v)
        {
            if (v <= SoftCap1) return v;
            if (v <= SoftCap2) return SoftCap1 + (v - SoftCap1) * 0.5f;
            return SoftCap1 + (SoftCap2 - SoftCap1) * 0.5f + (v - SoftCap2) * 0.25f;
        }

        /// <summary>수확 체감이 적용된 실제 효과 값.</summary>
        public float Effective(StatType t) => Soft(Get(t));

        float STR => Effective(StatType.STR);
        float AGI => Effective(StatType.AGI);
        float CTL => Effective(StatType.VIT); // 제어는 없어지고 반동·탄퍼짐·조준 효과는 생명이 맡는다
        float VIT => Effective(StatType.VIT);
        float FOC => Effective(StatType.FOC);
        float TEC => Effective(StatType.TEC);

        public float Strength => STR;
        public float Agility => AGI;
        public float Vitality => VIT;
        public float Tech => TEC;

        /// <summary>가장 낮은 능력치 기준 '균형' 보너스: 기본(8)보다 1 높을 때마다 모든 피해 +1.5%.</summary>
        public float BalanceBonus
        {
            get
            {
                float min = float.MaxValue;
                foreach (var t in Visible) min = Mathf.Min(min, Get(t));
                return 1f + Mathf.Max(0f, min - 8f) * 0.015f;
            }
        }

        // ---- 힘: 공격력 ----
        /// <summary>총 피해 배율: 기본(8)보다 1 높을 때마다 +2.5% (수확 체감은 Effective에서).</summary>
        public float DamageMult => Mathf.Max(0.85f, 1f + (STR - 8f) * 0.025f);
        /// <summary>칼(근접) 피해 배율: 힘의 영향이 두 배.</summary>
        public float MeleeDamageMult => Mathf.Max(0.8f, 1f + (STR - 8f) * 0.05f) * (Has(Trait.Heavyweight) ? 1.25f : 1f);
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
        /// <summary>받는 피해: 기본보다 1 높을 때마다 약 1.2% 감소.</summary>
        public float DamageTakenMult => 1f / (1f + Mathf.Max(0f, VIT - 8f) * 0.012f);

        // ---- 집중 ----
        public float CritChance => 0.05f + FOC * 0.005f;
        public float CritMultiplier => 1.5f + FOC * 0.01f;
        public float WeakPointBonus => FOC * 0.02f;

        // ---- 기술 (변이 능력 효율) ----
        public float AbilityCostMult => 1f / (1f + TEC * 0.02f) * (Has(Trait.MutantAffinity) ? 0.75f : 1f);
        /// <summary>벽 관통 깊이(m): 총의 관통력 × 기술. 저격총(0.8)+기본 기술이면 약 0.7m (돌담 0.6m를 뚫음).</summary>
        public float WallPenetration(float weaponPenetration) => (0.1f + weaponPenetration * 0.75f) * Mathf.Max(0.7f, 1f + (TEC - 8f) * 0.03f);
        /// <summary>수류탄·지뢰·설치물 피해: 기본보다 1 높을 때마다 +3%.</summary>
        public float GadgetDamageMult => Mathf.Max(0.8f, 1f + (TEC - 8f) * 0.03f) * (Has(Trait.Demolitionist) ? 1.35f : 1f);
        public float AbilityDurationMult => (1f + TEC * 0.01f) * (Has(Trait.MutantAffinity) ? 1.25f : 1f);

        /// <summary>총 장비 무게가 허용치를 넘으면 0.5까지 감소하는 배율.</summary>
        public float WeightPenalty(float totalWeight)
        {
            float over = Mathf.Max(0f, totalWeight - CarryCapacity) / CarryCapacity;
            return 1f - Mathf.Min(0.5f, over);
        }
    }
}

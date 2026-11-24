using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Hunt.Weapons
{
    public enum PartSlot { Barrel, Magazine, Stock, Scope, Muzzle, Module }

    public enum Rarity { Common, Rare, Epic, Legendary }

    public enum AffixType
    {
        // 수치형
        DamagePct, FireRatePct, MagazinePct, ReloadSpeedPct, RecoilReductionPct,
        SpreadReductionPct, KnockbackPct, CritChancePct, WeakPointDamagePct, WeightReductionPct,
        // 속성 변환
        ConvertFire, ConvertIce, ConvertElectric, ConvertPoison,
        // 발동형
        PostReloadBurst,   // 재장전 후 첫 3발 데미지 +X%
        AmmoOnWeakPoint,   // 약점 명중 시 X% 확률로 탄 1발 회복
        ExplodeOnKill,     // 처치 시 X% 확률로 폭발
        // 총구 '소음기' 전용 (새 항목은 끝에 — 저장된 값이 밀리지 않게)
        QuietPct,          // 총성이 들리는 거리 -X%
    }

    [Serializable]
    public class Affix
    {
        public AffixType type;
        public float value;

        public Affix(AffixType type, float value) { this.type = type; this.value = value; }

        public string Describe()
        {
            int v = Mathf.RoundToInt(value);
            switch (type)
            {
                case AffixType.DamagePct: return $"데미지 +{v}%";
                case AffixType.FireRatePct: return $"연사 속도 +{v}%";
                case AffixType.MagazinePct: return $"탄창 용량 +{v}%";
                case AffixType.ReloadSpeedPct: return $"재장전 속도 +{v}%";
                case AffixType.RecoilReductionPct: return $"반동 -{v}%";
                case AffixType.SpreadReductionPct: return $"탄퍼짐 -{v}%";
                case AffixType.KnockbackPct: return $"저지력 +{v}%";
                case AffixType.CritChancePct: return $"치명타 확률 +{v}%";
                case AffixType.WeakPointDamagePct: return $"약점 데미지 +{v}%";
                case AffixType.WeightReductionPct: return $"무게 -{v}%";
                case AffixType.ConvertFire: return "탄환 속성: 화염 (지속 화상)";
                case AffixType.ConvertIce: return "탄환 속성: 냉기 (둔화)";
                case AffixType.ConvertElectric: return "탄환 속성: 전기 (연쇄)";
                case AffixType.ConvertPoison: return "탄환 속성: 독 (중독)";
                case AffixType.PostReloadBurst: return $"재장전 후 첫 3발 데미지 +{v}%";
                case AffixType.AmmoOnWeakPoint: return $"약점 명중 시 {v}% 확률로 탄 1발 회복";
                case AffixType.ExplodeOnKill: return $"처치 시 {v}% 확률로 폭발";
                case AffixType.QuietPct: return $"총성 -{v}% (망자가 덜 듣는다)";
                default: return type.ToString();
            }
        }
    }

    [Serializable]
    public class WeaponPart
    {
        public string name;
        public PartSlot slot;
        public Rarity rarity;
        public int itemLevel;
        public List<Affix> affixes = new List<Affix>();

        public static string SlotName(PartSlot s)
        {
            switch (s)
            {
                case PartSlot.Barrel: return "총열";
                case PartSlot.Magazine: return "탄창";
                case PartSlot.Stock: return "개머리판";
                case PartSlot.Scope: return "조준경";
                case PartSlot.Muzzle: return "총구";
                default: return "특수 모듈";
            }
        }

        public static string RarityName(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return "희귀";
                case Rarity.Epic: return "영웅";
                case Rarity.Legendary: return "전설";
                default: return "일반";
            }
        }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return new Color(0.35f, 0.6f, 1f);
                case Rarity.Epic: return new Color(0.75f, 0.4f, 1f);
                case Rarity.Legendary: return new Color(1f, 0.65f, 0.15f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        public string Title => $"[{RarityName(rarity)}] {name} Lv.{itemLevel}";
    }

    /// <summary>D: 랜덤 옵션 파츠 생성기 (핵앤슬래시 파밍).</summary>
    public static class PartGenerator
    {
        static readonly AffixType[] StatPool =
        {
            AffixType.DamagePct, AffixType.FireRatePct, AffixType.MagazinePct, AffixType.ReloadSpeedPct,
            AffixType.RecoilReductionPct, AffixType.SpreadReductionPct, AffixType.KnockbackPct,
            AffixType.CritChancePct, AffixType.WeakPointDamagePct, AffixType.WeightReductionPct,
        };

        static readonly AffixType[] ModulePool =
        {
            AffixType.ConvertFire, AffixType.ConvertIce, AffixType.ConvertElectric, AffixType.ConvertPoison,
            AffixType.PostReloadBurst, AffixType.AmmoOnWeakPoint, AffixType.ExplodeOnKill,
        };

        static readonly Dictionary<PartSlot, AffixType> SlotPrimary = new Dictionary<PartSlot, AffixType>
        {
            { PartSlot.Barrel, AffixType.DamagePct },
            { PartSlot.Magazine, AffixType.MagazinePct },
            { PartSlot.Stock, AffixType.RecoilReductionPct },
            { PartSlot.Scope, AffixType.WeakPointDamagePct },
            { PartSlot.Muzzle, AffixType.SpreadReductionPct },
        };

        static readonly Dictionary<PartSlot, string[]> Names = new Dictionary<PartSlot, string[]>
        {
            { PartSlot.Barrel, new[] { "강선 총열", "중량 총열", "단축 총열" } },
            { PartSlot.Magazine, new[] { "확장 탄창", "드럼 탄창", "퀵 탄창" } },
            { PartSlot.Stock, new[] { "전술 개머리판", "충격흡수 개머리판" } },
            { PartSlot.Scope, new[] { "홀로그램 조준경", "열상 조준경", "약점 분석 조준경" } },
            { PartSlot.Muzzle, new[] { "보정기", "소염기", "제퇴기", "소음기" } },
            { PartSlot.Module, new[] { "변이 코어", "실험체 모듈", "공명 장치" } },
        };

        public static Rarity RollRarity(float luck)
        {
            float r = Random.value / (1f + luck);
            if (r < 0.03f) return Rarity.Legendary;
            if (r < 0.12f) return Rarity.Epic;
            if (r < 0.38f) return Rarity.Rare;
            return Rarity.Common;
        }

        public static WeaponPart Generate(int level, float luck = 0f, Rarity? forced = null)
        {
            var slot = (PartSlot)Random.Range(0, 6);
            var rarity = forced ?? RollRarity(luck);
            var part = new WeaponPart
            {
                slot = slot,
                rarity = rarity,
                itemLevel = Mathf.Max(1, level),
            };
            var names = Names[slot];
            part.name = names[Random.Range(0, names.Length)];

            // 슬롯 고유 옵션
            if (slot == PartSlot.Module)
                part.affixes.Add(Roll(ModulePool[Random.Range(0, ModulePool.Length)], level, rarity));
            else if (part.name == "소음기")
                part.affixes.Add(Roll(AffixType.QuietPct, level, rarity)); // 소음기는 총성을 줄인다
            else
                part.affixes.Add(Roll(SlotPrimary[slot], level, rarity));

            // 등급에 따른 추가 옵션 (일반 0 / 희귀 1 / 영웅 2 / 전설 3)
            int extra = (int)rarity;
            int guard = 0;
            while (extra > 0 && guard++ < 50)
            {
                var t = StatPool[Random.Range(0, StatPool.Length)];
                if (part.affixes.Exists(a => a.type == t)) continue;
                part.affixes.Add(Roll(t, level, rarity));
                extra--;
            }
            return part;
        }

        static Affix Roll(AffixType t, int level, Rarity rarity)
        {
            float q = 1f + (int)rarity * 0.25f + level * 0.02f;
            float v;
            switch (t)
            {
                case AffixType.DamagePct: v = Random.Range(5f, 12f) * q; break;
                case AffixType.FireRatePct: v = Random.Range(4f, 9f) * q; break;
                case AffixType.MagazinePct: v = Random.Range(10f, 25f) * q; break;
                case AffixType.ReloadSpeedPct: v = Random.Range(8f, 18f) * q; break;
                case AffixType.RecoilReductionPct: v = Random.Range(8f, 16f) * q; break;
                case AffixType.SpreadReductionPct: v = Random.Range(8f, 16f) * q; break;
                case AffixType.KnockbackPct: v = Random.Range(10f, 25f) * q; break;
                case AffixType.QuietPct: v = Mathf.Min(60f, Random.Range(28f, 40f) * q); break;
                case AffixType.CritChancePct: v = Random.Range(2f, 5f) * q; break;
                case AffixType.WeakPointDamagePct: v = Random.Range(10f, 25f) * q; break;
                case AffixType.WeightReductionPct: v = Random.Range(8f, 18f) * q; break;
                case AffixType.PostReloadBurst: v = Random.Range(30f, 50f) * q; break;
                case AffixType.AmmoOnWeakPoint: v = Mathf.Min(90f, Random.Range(25f, 40f) * q); break;
                case AffixType.ExplodeOnKill: v = Mathf.Min(80f, Random.Range(12f, 22f) * q); break;
                default: v = 1f; break; // 속성 변환
            }
            return new Affix(t, v);
        }
    }
}

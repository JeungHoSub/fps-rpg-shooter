using System.Collections.Generic;
using Hunt.Combat;
using Hunt.Data;
using UnityEngine;

namespace Hunt.Weapons
{
    /// <summary>
    /// 플레이어가 실제로 들고 있는 무기 하나. (기본 데이터 + 강화 + 장착 파츠) → 최종 수치.
    /// 같은 "K-7 돌격소총"이라도 파츠에 따라 완전히 다른 총이 된다.
    /// </summary>
    public class WeaponInstance
    {
        public const int MaxUpgrade = 10;

        public readonly WeaponData data;
        public int upgradeLevel;
        public Hunt.Player.Grade grade;
        public readonly Dictionary<PartSlot, WeaponPart> parts = new Dictionary<PartSlot, WeaponPart>();

        public int ammoInMag;
        public int reserve;

        public WeaponInstance(WeaponData data)
        {
            this.data = data;
            ammoInMag = data.magazine;
            reserve = data.reserveAmmo;
        }

        public float Sum(AffixType t)
        {
            float s = 0f;
            foreach (var p in parts.Values)
                foreach (var a in p.affixes)
                    if (a.type == t) s += a.value;
            return s;
        }

        public bool Has(AffixType t)
        {
            foreach (var p in parts.Values)
                foreach (var a in p.affixes)
                    if (a.type == t) return true;
            return false;
        }

        /// <summary>파츠 장착. 기존 파츠가 있으면 반환(인벤토리로 돌려보내기 위함).</summary>
        public WeaponPart Equip(WeaponPart part)
        {
            parts.TryGetValue(part.slot, out var old);
            parts[part.slot] = part;
            ammoInMag = Mathf.Min(ammoInMag, MagazineSize);
            return old;
        }

        /// <summary>등급(일반~전설) × 강화(+7%/단계) × 부품.</summary>
        public float Damage => data.damage * Hunt.Player.Grades.Mult(grade) * Hunt.Player.Grades.UpgradeMult(upgradeLevel) * (1f + Sum(AffixType.DamagePct) / 100f) * MasteryMult;

        // ---- 숙련: 이 총으로 쓰러뜨린 망자 수 → 위력이 조금씩 오른다 ----
        public int kills;
        public static readonly int[] MasterySteps = { 30, 100, 250, 500 };
        public int Mastery { get { int m = 0; foreach (var st in MasterySteps) if (kills >= st) m++; return m; } }
        public float MasteryMult => 1f + 0.03f * Mastery + (Mastery >= 4 ? 0.03f : 0f); // 3%·6%·9%·15%
        /// <summary>다음 숙련까지 남은 처치 수 (끝이면 0).</summary>
        public int KillsToNextMastery { get { foreach (var st in MasterySteps) if (kills < st) return st - kills; return 0; } }
        public static string MasteryName(int m) => m == 0 ? "" : m == 1 ? "손에 익음" : m == 2 ? "능숙" : m == 3 ? "노련" : "달인";
        public float FireInterval => data.FireInterval / (1f + Sum(AffixType.FireRatePct) / 100f);
        public int MagazineSize => Mathf.Max(1, Mathf.RoundToInt(data.magazine * (1f + Sum(AffixType.MagazinePct) / 100f)));
        public float ReloadSpeedMult => 1f + Sum(AffixType.ReloadSpeedPct) / 100f;
        public float RecoilMult => Mathf.Max(0.3f, 1f - Sum(AffixType.RecoilReductionPct) / 100f);
        public float SpreadMult => Mathf.Max(0.3f, 1f - Sum(AffixType.SpreadReductionPct) / 100f);
        public float KnockbackMult => 1f + Sum(AffixType.KnockbackPct) / 100f;
        public float CritChanceBonus => Sum(AffixType.CritChancePct) / 100f;
        public float WeakPointBonus => Sum(AffixType.WeakPointDamagePct) / 100f;
        public float Weight => data.weight * Mathf.Max(0.4f, 1f - Sum(AffixType.WeightReductionPct) / 100f);
        public int MaxReserve => Mathf.RoundToInt(data.reserveAmmo * (1f + 0.1f * (int)grade));

        public DamageType DamageType
        {
            get
            {
                if (Has(AffixType.ConvertFire)) return DamageType.Fire;
                if (Has(AffixType.ConvertIce)) return DamageType.Ice;
                if (Has(AffixType.ConvertElectric)) return DamageType.Electric;
                if (Has(AffixType.ConvertPoison)) return DamageType.Poison;
                return data.damageType;
            }
        }

        public int UpgradeCost => Hunt.Player.Grades.UpgradeCost(grade, upgradeLevel) + 20 * data.tier;
        public int GradeUpCost => Hunt.Player.Grades.GradeUpCost(grade) + 60 * data.tier;

        public string DisplayName => (grade > 0 ? Hunt.Player.Grades.Name(grade) + " " : "") + (upgradeLevel > 0 ? $"{data.displayName} +{upgradeLevel}" : data.displayName);
    }

    /// <summary>
    /// C: 무기마다 고정된 반동 패턴. 같은 무기는 항상 같은 모양으로 튀므로 "외울 수 있다".
    /// 캐릭터의 제어(CTL) 스탯은 이 패턴 위의 무작위 노이즈를 줄여준다.
    /// </summary>
    public static class RecoilPattern
    {
        /// <returns>x = 좌우(도), y = 위(도)</returns>
        public static Vector2 Kick(WeaponData d, int shotIndex)
        {
            // 처음 몇 발은 수직으로 강하게, 이후 좌우로 S자를 그린다.
            float climb = d.recoilVertical * (shotIndex < 8 ? 1f + shotIndex * 0.12f : 0.6f);
            float sway = (Mathf.PerlinNoise(d.recoilSeed, shotIndex * 0.35f) * 2f - 1f)
                         * d.recoilHorizontal * Mathf.Min(1f + shotIndex * 0.15f, 2.5f);
            return new Vector2(sway, climb);
        }
    }
}

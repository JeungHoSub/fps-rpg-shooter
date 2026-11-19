using UnityEngine;

namespace Hunt.Combat
{
    public enum DamageType { Physical, Fire, Ice, Electric, Poison }

    public enum HitZone { Body, Head, WeakPoint, Armor, Leg }

    public enum WeaponClass { Pistol, SMG, Rifle, Shotgun, Sniper, LMG, Melee, Explosive, Flamethrower, Launcher }

    public static class CombatNames
    {
        public static string Of(DamageType t)
        {
            switch (t)
            {
                case DamageType.Fire: return "화염";
                case DamageType.Ice: return "냉기";
                case DamageType.Electric: return "전기";
                case DamageType.Poison: return "독";
                default: return "물리";
            }
        }

        public static Color ColorOf(DamageType t)
        {
            switch (t)
            {
                case DamageType.Fire: return new Color(1f, 0.45f, 0.1f);
                case DamageType.Ice: return new Color(0.5f, 0.85f, 1f);
                case DamageType.Electric: return new Color(1f, 1f, 0.35f);
                case DamageType.Poison: return new Color(0.5f, 1f, 0.3f);
                default: return Color.white;
            }
        }
    }

    /// <summary>한 발(펠릿)이 가진 피해 정보. 무기+캐릭터+버프가 모두 반영된 상태로 괴물에게 전달된다.</summary>
    public struct DamageInfo
    {
        public float baseDamage;
        public DamageType type;
        public WeaponClass weaponClass;
        public float critChance;
        public float critMultiplier;
        public float weakPointBonus;   // 약점 배율 추가치 (0.3 = +30%)
        public float penetration;      // 0~1, 방어력 무시 비율
        public float knockback;        // 이 펠릿의 저지력
        public float distance;
        public float falloffStart;
        public float falloffEnd;
        public float minFalloffMultiplier;
        public Vector3 hitPoint;
        public Vector3 direction;
    }

    public struct DamageResult
    {
        public float amount;
        public bool crit;
        public HitZone zone;
        public bool killed;
        public bool partBroken;
        public bool hit;
    }

    /// <summary>
    /// 최종 데미지 = 무기 데미지 × 부위 배율 × (1 - 속성 내성) × 거리 감쇠 × 치명타 − 방어력(관통 반영)
    /// </summary>
    public static class DamageCalculator
    {
        public const float HeadMultiplier = 2f;
        public const float WeakPointMultiplier = 3f;
        public const float LegMultiplier = 0.75f;

        public static float ZoneMultiplier(HitZone zone, float weakPointBonus)
        {
            switch (zone)
            {
                case HitZone.Head: return HeadMultiplier;
                case HitZone.WeakPoint: return WeakPointMultiplier * (1f + weakPointBonus);
                case HitZone.Leg: return LegMultiplier;
                default: return 1f;
            }
        }

        public static float Falloff(in DamageInfo info)
        {
            if (info.falloffEnd <= info.falloffStart) return 1f;
            float t = Mathf.InverseLerp(info.falloffStart, info.falloffEnd, info.distance);
            return Mathf.Lerp(1f, info.minFalloffMultiplier, t);
        }

        public static float Calculate(in DamageInfo info, HitZone zone, float resistance, float defense, out bool crit)
        {
            float dmg = info.baseDamage;
            dmg *= ZoneMultiplier(zone, info.weakPointBonus);
            dmg *= 1f - Mathf.Clamp(resistance, -1f, 0.9f);
            dmg *= Falloff(info);

            crit = Random.value < info.critChance;
            if (crit) dmg *= info.critMultiplier;

            float effectiveDefense = defense * (1f - Mathf.Clamp01(info.penetration));
            return Mathf.Max(dmg - effectiveDefense, dmg * 0.1f);
        }
    }
}

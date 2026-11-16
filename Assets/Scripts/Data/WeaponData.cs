using Hunt.Combat;
using Hunt.Stats;
using UnityEngine;

namespace Hunt.Data
{
    [CreateAssetMenu(menuName = "Hunt/Weapon", fileName = "Weapon")]
    public class WeaponData : ScriptableObject
    {
        public string id;
        public string displayName;
        public WeaponClass weaponClass;
        public DamageType damageType = DamageType.Physical;

        [Header("화력")]
        public float damage = 30f;
        public int pellets = 1;
        public float rpm = 600f;
        public bool automatic = true;
        public float penetration = 0.1f;
        [Tooltip("저지력: 괴물을 밀어내는 힘 (샷당 총합)")]
        public float knockback = 1f;

        [Header("탄약")]
        public int magazine = 30;
        public int reserveAmmo = 180;
        public float reloadTime = 2f;
        public float emptyReloadTime = 2.6f;

        [Header("반동 (고정 패턴 + 노이즈)")]
        public float recoilVertical = 1f;
        public float recoilHorizontal = 0.4f;
        public float recoilSeed = 1f;

        [Header("탄퍼짐 (도)")]
        public float spreadHip = 3f;
        public float spreadAds = 0.5f;
        public float spreadMovePenalty = 2f;

        [Header("사거리")]
        public float falloffStart = 20f;
        public float falloffEnd = 50f;
        public float minFalloff = 0.5f;

        [Header("조작")]
        public float weight = 4f;
        public float adsTime = 0.25f;
        public float adsFov = 55f;
        public Color viewColor = Color.gray;

        public float FireInterval => 60f / Mathf.Max(1f, rpm);
    }
}

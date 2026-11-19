using Hunt.Combat;
using Hunt.Monsters;
using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 소리 시스템: 총성·발소리가 반경 안의 망자를 불러 모은다.
    /// 빗소리가 짙을수록 소리가 덜 퍼진다 (최대 45% 감소).
    /// </summary>
    public static class Noise
    {
        static float level, levelTime;

        /// <summary>마지막 총구 화염 시각 (빛 감지용).</summary>
        public static float LastFlashTime = -10f;
        public static void MuzzleFlash() { LastFlashTime = Time.time; }

        /// <summary>비가 소리를 가리는 배율 (1 = 그대로, 0.55 = 폭우).</summary>
        public static float RainMask =>
            Atmosphere.Instance != null ? Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(0.4f, 1f, Atmosphere.Instance.RainIntensity)) : 1f;

        /// <summary>HUD 표시용: 최근 플레이어가 낸 소리 크기 (0~1, 서서히 잦아든다).</summary>
        public static float PlayerLoudness => level * Mathf.Exp(-(Time.time - levelTime) * 1.6f);

        public static float ShotRadius(WeaponClass c)
        {
            switch (c)
            {
                case WeaponClass.Pistol: return 28f;
                case WeaponClass.SMG: return 32f;
                case WeaponClass.Shotgun: return 40f;
                case WeaponClass.Sniper: return 50f;
                case WeaponClass.LMG: return 40f;
                default: return 32f;
            }
        }

        /// <summary>pos에서 radius(맑은 날 기준, m)만큼 들리는 소리를 낸다.</summary>
        public static void Emit(Vector3 pos, float radius, bool fromPlayer)
        {
            float r = radius * RainMask;
            if (fromPlayer)
            {
                float l = Mathf.Clamp01(r / 40f);
                if (l >= PlayerLoudness) { level = l; levelTime = Time.time; }
            }
            float r2 = r * r;
            var alive = Monster.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                var m = alive[i];
                if (m == null || m.IsDead) continue;
                Vector3 d = m.transform.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude <= r2) m.HearNoise(pos, fromPlayer);
            }
        }
    }
}

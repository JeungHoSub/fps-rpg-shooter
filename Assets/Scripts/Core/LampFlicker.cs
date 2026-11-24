using UnityEngine;

namespace Hunt.Core
{
    /// <summary>불빛 흔들림: 가로등(고장 나서 깜빡임), 창문 불빛(미세한 흔들림), 고분의 붉은 빛(맥동).</summary>
    public class LampFlicker : MonoBehaviour
    {
        public bool broken;  // 가끔 꺼졌다 켜짐
        public bool subtle;  // 촛불처럼 살짝
        public bool pulse;   // 숨쉬듯 천천히
        /// <summary>이 품질 이상일 때만 켠다 (저사양에서 조명 수 줄이기).</summary>
        public GfxLevel minLevel = GfxLevel.Low;

        Light lt;
        float baseIntensity, seed, offUntil, nextGlitch;

        void Start()
        {
            lt = GetComponent<Light>();
            if (lt != null) baseIntensity = lt.intensity;
            seed = Random.value * 100f;
            nextGlitch = Time.time + Random.Range(2f, 6f);
        }

        void Update()
        {
            if (lt == null) return;
            bool want = QualityManager.Level >= minLevel;
            if (lt.enabled != want) lt.enabled = want;
            if (!want) return;
            float t = Time.time;
            float k = 1f;
            if (pulse) k = 0.6f + 0.4f * Mathf.Sin(t * 1.3f + seed);
            else if (subtle) k = 0.85f + 0.15f * Mathf.PerlinNoise(seed, t * 3f);
            else k = 0.94f + 0.06f * Mathf.PerlinNoise(seed, t * 8f);

            if (broken)
            {
                if (t > nextGlitch)
                {
                    offUntil = t + Random.Range(0.05f, 0.6f);
                    nextGlitch = t + Random.Range(0.4f, 5f);
                }
                if (t < offUntil) k *= Random.value < 0.5f ? 0.05f : 0.4f;
            }
            // 보스가 가까우면 마을 불빛이 모두 떨린다 (Flashlight가 0.25초마다 잰 값)
            float bn = Hunt.Player.Flashlight.BossNear;
            if (bn > 0f && GameSettings.ShakeMult > 0f && Mathf.PerlinNoise(seed + t * (7f + bn * 12f), 1.7f) < 0.15f + bn * 0.2f) k *= 0.25f;
            lt.intensity = baseIntensity * k;
        }
    }
}

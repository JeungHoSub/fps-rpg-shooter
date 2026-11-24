using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 가까운 폭발 뒤 귀가 먹먹해진다: 듣는 쪽(카메라)에 저역 통과 필터를 잠깐 걸고 '삐—' 귀 울림을 낸다.
    /// 1.5~2초에 걸쳐 원래 소리로 돌아온다.
    /// </summary>
    public class EarRing : MonoBehaviour
    {
        AudioLowPassFilter filter;
        AudioSource ring; // 먹먹함 필터를 건너뛰는 귀 울림 전용 (안 그러면 '삐' 소리까지 먹먹해진다)
        float amount; // 0~1
        float lowHp;  // 생명이 낮을 때 먹먹함 (0~0.55)

        public static void Hit(float strength)
        {
            var gm = GameManager.Instance;
            var cam = gm != null ? gm.ActiveCamera : null;
            if (cam == null || strength <= 0.05f) return;
            var e = cam.GetComponent<EarRing>();
            if (e == null) e = cam.gameObject.AddComponent<EarRing>();
            bool fresh = e.amount < 0.3f;
            e.amount = Mathf.Clamp01(Mathf.Max(e.amount, strength));
            if (fresh && e.ring != null)
            {
                e.ring.volume = 0.35f + 0.5f * strength;
                e.ring.pitch = Random.Range(0.97f, 1.03f);
                e.ring.Play();
            }
        }

        void Awake()
        {
            filter = GetComponent<AudioLowPassFilter>();
            if (filter == null) filter = gameObject.AddComponent<AudioLowPassFilter>();
            filter.lowpassResonanceQ = 1f;
            ring = gameObject.AddComponent<AudioSource>();
            ring.playOnAwake = false;
            ring.spatialBlend = 0f;
            ring.bypassListenerEffects = true;
            ring.clip = SFX.EarRing;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) { filter.enabled = true; filter.cutoffFrequency = 1100f; return; } // 숨 고르는 동안 바깥 소리가 먹먹하게
            amount = Mathf.MoveTowards(amount, 0f, Time.deltaTime * 0.55f);
            // 생명이 20% 아래면 소리가 조금씩 멀어진다 (심장 소리와 함께 긴장감)
            var p = gm != null ? gm.Player : null;
            float low = 0f;
            if (p != null && !p.IsDead && p.MaxHp > 0f) low = Mathf.Clamp01((0.2f - p.Hp / p.MaxHp) / 0.2f) * 0.55f;
            // 숨을 참으면 바깥 소리가 살짝 멀어진다 (심장 소리만 또렷)
            if (p != null && !p.IsDead && p.Controller != null && p.Controller.HoldingBreath) low = Mathf.Max(low, 0.35f);
            lowHp = Mathf.MoveTowards(lowHp, low, Time.deltaTime * 0.8f);
            float k = Mathf.Max(amount, lowHp);
            // 22000Hz(평소) ↔ 700Hz(먹먹)
            filter.cutoffFrequency = Mathf.Lerp(22000f, 700f, k * k * (3f - 2f * k));
            filter.enabled = k > 0.001f;
        }
    }
}

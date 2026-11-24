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
            if (gm != null && gm.Paused) return;
            amount = Mathf.MoveTowards(amount, 0f, Time.deltaTime * 0.55f);
            // 22000Hz(평소) ↔ 700Hz(먹먹)
            filter.cutoffFrequency = Mathf.Lerp(22000f, 700f, amount * amount * (3f - 2f * amount));
            filter.enabled = amount > 0.001f;
        }
    }
}

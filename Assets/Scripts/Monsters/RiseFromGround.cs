using Hunt.Core;
using UnityEngine;

namespace Hunt.Monsters
{
    /// <summary>
    /// 망자가 땅속에서 기어 나온다: 모델(Visual)을 땅 밑에서 시작해 흔들리며 올라오게 하고, 흙덩이를 튀긴다.
    /// 그동안 몬스터는 제자리에 묶여 있다(Monster.Emerge가 stagger를 건다). 끝나면 스스로 지운다.
    /// </summary>
    public class RiseFromGround : MonoBehaviour
    {
        Vector3 baseLocal;
        float t, dur, depth, nextClod;

        public static void Begin(Transform pivot, float duration, float depth)
        {
            if (pivot == null) return;
            var r = pivot.gameObject.AddComponent<RiseFromGround>();
            r.baseLocal = pivot.localPosition;
            r.dur = duration;
            r.depth = depth;
            pivot.localPosition = r.baseLocal - Vector3.up * depth / Mathf.Max(0.01f, pivot.parent != null ? pivot.parent.lossyScale.y : 1f);
            r.Clods(5);
        }

        void Clods(int n)
        {
            Vector3 at = transform.parent != null ? transform.parent.position : transform.position;
            for (int i = 0; i < n; i++)
            {
                var v = Random.insideUnitSphere * 2.2f; v.y = Mathf.Abs(v.y) * 2f + 2.5f;
                float g = Random.Range(0.14f, 0.24f);
                MonsterGib.Chunk(null, at + Random.insideUnitSphere * 0.35f + Vector3.up * 0.1f, v, new Color(g * 1.1f, g * 0.92f, g * 0.7f), Random.Range(0.05f, 0.12f));
            }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            // 처음엔 버둥대며 천천히, 끝에 확 올라선다
            float rise = k < 0.7f ? Mathf.Lerp(0f, 0.55f, k / 0.7f) : Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.7f) / 0.3f));
            float shake = (1f - k) * 0.04f * Mathf.Sin(t * 37f);
            float s = transform.parent != null ? transform.parent.lossyScale.y : 1f;
            transform.localPosition = baseLocal + new Vector3(shake, -(1f - rise) * depth / Mathf.Max(0.01f, s), 0f);
            nextClod -= Time.deltaTime;
            if (nextClod <= 0f && k < 0.85f) { Clods(QualityManager.Level == GfxLevel.Low ? 1 : 2); nextClod = 0.35f; }
            if (k >= 1f)
            {
                transform.localPosition = baseLocal;
                Destroy(this);
            }
        }
    }
}

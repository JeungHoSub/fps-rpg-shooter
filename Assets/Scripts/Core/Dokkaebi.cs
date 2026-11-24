using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 도깨비불: 밤에 가끔(3~5분마다) 마을 바깥 먼 곳에서 푸르스름한 불덩이 대여섯이 줄지어 천천히 떠가다 사라진다.
    /// 아무 일도 하지 않는 분위기 연출 (빛 없이 단색 구슬만, 매우 가볍다).
    /// </summary>
    public class Dokkaebi : MonoBehaviour
    {
        static float next = 90f;
        Transform[] orbs;
        Vector3 dir;
        float t, life = 14f;

        /// <summary>Atmosphere가 매 프레임 부른다.</summary>
        public static void Tick(Transform parent, Camera cam, float dawn)
        {
            if (cam == null || dawn > 0.2f || Time.time < next) return;
            next = Time.time + Random.Range(180f, 300f);
            Vector2 d2 = Random.insideUnitCircle.normalized;
            float far = Mathf.Clamp(cam.farClipPlane * 0.7f, 45f, 90f);
            Vector3 at = new Vector3(d2.x, 0f, d2.y) * far;
            at.y = 1.4f;
            var go = new GameObject("도깨비불");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = at;
            var dk = go.AddComponent<Dokkaebi>();
            dk.dir = Vector3.Cross(Vector3.up, new Vector3(d2.x, 0f, d2.y)) * (Random.value < 0.5f ? 1f : -1f);
            dk.Build();
        }

        void Build()
        {
            int n = Random.Range(4, 7);
            orbs = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                var o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(o.GetComponent<Collider>());
                o.layer = 2;
                o.transform.SetParent(transform, false);
                o.transform.localPosition = -dir * i * 2.2f;
                o.transform.localScale = Vector3.one * 0.32f;
                var r = o.GetComponent<Renderer>();
                r.sharedMaterial = FX.UnlitMaterial(new Color(0.45f, 0.75f, 1f, 0.75f));
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                orbs[i] = o.transform;
            }
        }

        void Update()
        {
            t += Time.deltaTime;
            transform.position += dir * 0.9f * Time.deltaTime;
            float fade = Mathf.Clamp01(t / 2f) * Mathf.Clamp01((life - t) / 2f);
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] == null) continue;
                float bob = Mathf.Sin(t * 1.7f + i * 1.3f) * 0.35f;
                var lp = orbs[i].localPosition; lp.y = bob; orbs[i].localPosition = lp;
                float flick = 0.7f + 0.3f * Mathf.PerlinNoise(i * 3.3f, t * 3f);
                orbs[i].localScale = Vector3.one * 0.32f * fade * flick;
            }
            if (t >= life) Destroy(gameObject);
        }
    }
}

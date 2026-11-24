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

    /// <summary>혼불: 망자가 쓰러지면 푸르스름한 작은 불이 몸에서 빠져나와 흔들리며 떠오르다 사라진다 (1.6초).</summary>
    public class SoulWisp : MonoBehaviour
    {
        float t;
        Vector3 drift;

        public static void Spawn(Vector3 at)
        {
            if (QualityManager.Level == GfxLevel.Low && Random.value < 0.5f) return;
            var o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(o.GetComponent<Collider>());
            o.name = "혼불";
            o.layer = 2;
            if (Effects.Root != null) o.transform.SetParent(Effects.Root, true);
            o.transform.position = at;
            o.transform.localScale = Vector3.one * 0.15f;
            var r = o.GetComponent<Renderer>();
            r.sharedMaterial = FX.UnlitMaterial(new Color(0.55f, 0.8f, 1f, 0.7f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var w = o.AddComponent<SoulWisp>();
            w.drift = new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = t / 1.6f;
            transform.position += (Vector3.up * 1.1f + drift + new Vector3(Mathf.Sin(t * 6f) * 0.4f, 0f, 0f)) * Time.deltaTime;
            transform.localScale = Vector3.one * 0.15f * (1f - k) * (0.8f + 0.2f * Mathf.Sin(t * 20f));
            if (k >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>곡소리 물결: 바닥에 붉은 고리가 반경까지 퍼지며 옅어진다 (곡하는 망자가 부르는 범위를 보여 준다).</summary>
    public class WailRing : MonoBehaviour
    {
        float t, radius;
        Material mat;

        public static void Spawn(Vector3 at, float radius)
        {
            var tex = Hunt.UI.UITheme.DiamondHollow;
            if (tex == null) return;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(q.GetComponent<Collider>());
            q.name = "곡소리";
            q.layer = 2;
            if (Effects.Root != null) q.transform.SetParent(Effects.Root, true);
            q.transform.position = at + Vector3.up * 0.15f;
            q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var w = q.AddComponent<WailRing>();
            w.radius = radius;
            w.mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, color = new Color(1f, 0.3f, 0.25f, 0.8f) };
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = w.mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 1.4f);
            float size = Mathf.Lerp(1f, radius * 2.2f, 1f - (1f - k) * (1f - k));
            transform.localScale = new Vector3(size, size, 1f);
            mat.color = new Color(1f, 0.3f, 0.25f, 0.8f * (1f - k));
            if (k >= 1f) Destroy(gameObject);
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }
    }
}

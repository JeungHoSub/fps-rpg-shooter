using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 폭발 연출: 부풀었다 사라지는 불덩이 + 피어오르는 연기 + 흙덩이 + 잠깐 번쩍이는 빛 + 그을음 자국.
    /// 도형·단색 재질만 써서 내장 그래픽('하')에서도 가볍다. 재질 사본은 끝나면 지운다.
    /// </summary>
    public class ExplosionFx : MonoBehaviour
    {
        Transform fire;
        Material fireMat;
        Transform[] puffs;
        Vector3[] puffVel;
        Material smokeMat;
        Light flash;
        float t, radius;
        Color fireCol;
        const float Life = 2.2f;

        static Material baseMat;
        static Material BaseMat => baseMat != null ? baseMat : (baseMat = new Material(Shader.Find("Sprites/Default")) { name = "ExplosionFx" });

        public static void Spawn(Vector3 point, float radius, Color fireColor)
        {
            var go = new GameObject("폭발");
            if (Effects.Root != null) go.transform.SetParent(Effects.Root, true);
            go.transform.position = point;
            var fx = go.AddComponent<ExplosionFx>();
            fx.Build(radius, fireColor);
        }

        static Transform Ball(Transform parent, Material m)
        {
            var b = FX.LowSphere(); // 가벼운 공 (판정 없음)
            b.layer = 2;
            b.transform.SetParent(parent, false);
            var r = b.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return b.transform;
        }

        void Build(float r, Color c)
        {
            radius = r;
            fireCol = c;
            bool low = QualityManager.Level == GfxLevel.Low;

            fireMat = new Material(BaseMat) { color = c };
            fire = Ball(transform, fireMat);
            fire.localScale = Vector3.one * r * 0.3f;

            smokeMat = new Material(BaseMat) { color = new Color(0.16f, 0.15f, 0.14f, 0f) };
            int n = low ? 4 : 7;
            puffs = new Transform[n];
            puffVel = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                puffs[i] = Ball(transform, smokeMat);
                var dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.3f;
                puffs[i].localPosition = dir * r * 0.15f;
                puffs[i].localScale = Vector3.one * r * 0.25f;
                puffVel[i] = dir.normalized * r * Random.Range(0.25f, 0.5f) + Vector3.up * r * 0.25f;
            }

            if (!low)
            {
                flash = new GameObject("빛").AddComponent<Light>();
                flash.transform.SetParent(transform, false);
                flash.transform.localPosition = Vector3.up * 0.6f;
                flash.type = LightType.Point;
                flash.color = new Color(1f, 0.65f, 0.35f);
                flash.range = r * 3.5f;
                flash.intensity = 5f;
                flash.shadows = LightShadows.None;
            }

            // 흙덩이가 사방으로 튄다
            int chunks = low ? 5 : 9;
            for (int i = 0; i < chunks; i++)
            {
                var v = Random.insideUnitSphere * 7f; v.y = Mathf.Abs(v.y) * 1.4f + 4f;
                float g = Random.Range(0.18f, 0.3f);
                Hunt.Monsters.MonsterGib.Chunk(transform.parent, transform.position + Vector3.up * 0.2f, v,
                    new Color(g * 1.1f, g * 0.95f, g * 0.75f), Random.Range(0.06f, 0.14f));
            }

            FX.Scorch(transform.position, r * 0.55f);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            float dt = Time.deltaTime;
            t += dt;

            // 불덩이: 0.12초에 최대로 부풀고 0.35초에 꺼진다
            if (fire != null)
            {
                float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.12f), 2f);
                fire.localScale = Vector3.one * radius * Mathf.Lerp(0.3f, 1.25f, grow);
                float a = Mathf.Clamp01(1f - (t - 0.08f) / 0.27f);
                fireMat.color = new Color(Mathf.Lerp(0.6f, fireCol.r, a), Mathf.Lerp(0.2f, fireCol.g, a), fireCol.b * a, a);
                if (a <= 0f) { Destroy(fire.gameObject); fire = null; }
            }

            // 연기: 떠오르며 퍼지고 옅어진다
            float sa = t < 0.15f ? t / 0.15f * 0.75f : 0.75f * Mathf.Clamp01(1f - (t - 0.6f) / (Life - 0.6f));
            smokeMat.color = new Color(0.16f, 0.15f, 0.14f, sa);
            for (int i = 0; i < puffs.Length; i++)
            {
                puffVel[i] *= 1f - 2.2f * dt;
                puffVel[i] += Vector3.up * 0.9f * dt;
                puffs[i].localPosition += puffVel[i] * dt;
                puffs[i].localScale += Vector3.one * radius * 0.35f * dt;
            }

            if (flash != null)
            {
                flash.intensity = Mathf.Lerp(5f, 0f, t / 0.3f);
                if (t > 0.3f) { Destroy(flash.gameObject); flash = null; }
            }

            if (t >= Life) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (fireMat != null) Destroy(fireMat);
            if (smokeMat != null) Destroy(smokeMat);
        }
    }
}

using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 까마귀 떼: 큰 총소리가 나면(40초에 한 번까지) 근처 나무 쪽에서 까마귀 대여섯 마리가 까악거리며 날아오른다.
    /// 검은 납작 도형 두 장(날개)이 퍼덕이는 아주 가벼운 연출.
    /// </summary>
    public class Crows : MonoBehaviour
    {
        static float lastFlock = -100f;

        public static void Startle(Vector3 near)
        {
            if (Time.time - lastFlock < 40f || QualityManager.Emergency || QualityManager.Level == GfxLevel.Low && Random.value < 0.5f) return;
            lastFlock = Time.time;
            Vector2 off = Random.insideUnitCircle.normalized * Random.Range(12f, 22f);
            Vector3 at = near + new Vector3(off.x, 0f, off.y);
            at.y = Mathf.Max(near.y, 0f) + Random.Range(3.5f, 6f);
            var flock = new GameObject("까마귀 떼");
            flock.transform.position = at;
            if (Effects.Root != null) flock.transform.SetParent(Effects.Root, true);
            Vector3 away = (at - near); away.y = 0f; away = away.normalized;
            int n = Random.Range(4, 7);
            for (int i = 0; i < n; i++)
            {
                var b = new GameObject("까마귀").AddComponent<Crows>();
                b.transform.SetParent(flock.transform, false);
                b.transform.localPosition = Random.insideUnitSphere * 1.5f;
                b.vel = (away + Random.insideUnitSphere * 0.4f + Vector3.up * Random.Range(0.6f, 1.1f)).normalized * Random.Range(6f, 9f);
                b.Build();
            }
            Destroy(flock, 9f);
            SFX.PlayAt3D(SFX.Caw, at, 0.9f, Random.Range(0.9f, 1.1f));
            SFX.PlayAt3D(SFX.Caw, at + Vector3.right, 0.7f, Random.Range(1.05f, 1.25f));
        }

        Vector3 vel;
        Transform wingL, wingR;
        float phase;

        void Build()
        {
            var mat = FX.UnlitMaterial(new Color(0.03f, 0.03f, 0.035f));
            var body = FX.LowSphere(); // 가벼운 공 (판정 없음)
            body.layer = 2;
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(0.12f, 0.1f, 0.3f);
            body.GetComponent<Renderer>().sharedMaterial = mat;
            body.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wingL = Wing(mat, -1f);
            wingR = Wing(mat, 1f);
            phase = Random.value * 10f;
            transform.rotation = Quaternion.LookRotation(vel);
        }

        Transform Wing(Material mat, float side)
        {
            var pivot = new GameObject("날개").transform;
            pivot.SetParent(transform, false);
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(w.GetComponent<Collider>());
            w.layer = 2;
            w.transform.SetParent(pivot, false);
            w.transform.localPosition = new Vector3(side * 0.2f, 0f, 0f);
            w.transform.localScale = new Vector3(0.4f, 0.01f, 0.16f);
            w.GetComponent<Renderer>().sharedMaterial = mat;
            w.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return pivot;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            vel += Vector3.up * 1.2f * dt;          // 계속 높이 오른다
            transform.position += vel * dt;
            if (vel.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(vel), dt * 4f);
            float flap = Mathf.Sin((Time.time + phase) * 16f) * 50f;
            if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, flap);
            if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
        }
    }
}

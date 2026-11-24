using Hunt.Core;
using Hunt.Weapons;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>발차기 때 화면 아래에서 0.3초 동안 뻗었다 들어가는 장화 (1인칭 무기 층).</summary>
    public class KickLeg : MonoBehaviour
    {
        float t;

        public static void Show(Transform cam)
        {
            if (cam == null) return;
            var root = new GameObject("KickLeg");
            root.transform.SetParent(cam, false);
            root.layer = GunModelBuilder.ViewLayer;
            var dark = EnvMaterials.Plain(new Color(0.12f, 0.11f, 0.1f), 0.35f);   // 바지
            var boot = EnvMaterials.Plain(new Color(0.05f, 0.05f, 0.05f), 0.55f);  // 고무 장화
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0f, 0.25f), new Vector3(0.16f, 0.16f, 0.5f), dark);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.55f), new Vector3(0.17f, 0.2f, 0.16f), boot);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, -0.06f, 0.6f), new Vector3(0.17f, 0.06f, 0.26f), boot);
            root.AddComponent<KickLeg>().Update();
        }

        static void Part(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            Object.Destroy(g.GetComponent<Collider>());
            g.layer = GunModelBuilder.ViewLayer;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            t += Time.deltaTime / 0.32f;
            // 아래에서 쭉 뻗었다가 (0~0.35) 잠깐 머문 뒤 (~0.6) 빠진다
            float ext = t < 0.35f ? t / 0.35f : t < 0.6f ? 1f : Mathf.Max(0f, 1f - (t - 0.6f) / 0.4f);
            ext = 1f - (1f - ext) * (1f - ext);
            transform.localPosition = Vector3.Lerp(new Vector3(0.05f, -0.9f, -0.2f), new Vector3(0.05f, -0.42f, 0.2f), ext);
            transform.localRotation = Quaternion.Euler(Mathf.Lerp(-55f, -12f, ext), -6f, 0f);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// Poly Haven(CC0) 소품 모델: Assets/Resources/Props/{id}.fbx + {id}_diff / {id}_nor 텍스처.
    /// 모델 크기와 방향은 파일마다 달라서, 놓을 때 원하는 높이(m)에 맞춰 줄이고 바닥에 붙인다.
    /// 파일이 없으면 null → 호출하는 쪽이 기존 도형으로 대체한다.
    /// </summary>
    public static class PropModels
    {
        static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        public static void ClearCache() { mats.Clear(); }

        static GameObject Model(string id)
        {
            if (models.TryGetValue(id, out var m)) return m;
            m = Resources.Load<GameObject>("Props/" + id);
            models[id] = m;
            return m;
        }

        public static bool Has(string id) => Model(id) != null;

        /// <summary>비에 젖은 듯한 광택의 Standard 재질 (cutout = 잎사귀 투명 처리).</summary>
        public static Material Material(string id, Color tint, float gloss, bool cutout)
        {
            string key = id + "|" + ColorUtility.ToHtmlStringRGBA(tint) + "|" + gloss + "|" + cutout;
            if (mats.TryGetValue(key, out var mat) && mat != null) return mat;
            var shader = Shader.Find("Standard");
            mat = new Material(shader) { name = "Prop_" + id, color = tint };
            var diff = Resources.Load<Texture2D>("Props/" + id + "_diff");
            var nor = Resources.Load<Texture2D>("Props/" + id + "_nor");
            if (diff != null) mat.mainTexture = diff;
            if (nor != null)
            {
                mat.SetTexture("_BumpMap", nor);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Glossiness", gloss);
            mat.SetFloat("_Metallic", 0f);
            if (cutout)
            {
                mat.SetFloat("_Mode", 1f);
                mat.SetFloat("_Cutoff", 0.45f);
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = 2450;
            }
            mats[key] = mat;
            return mat;
        }

        /// <summary>
        /// 모델을 놓는다. height = 바닥부터 꼭대기까지 높이(m). 반환: 기준 오브젝트(바닥 중심이 pos).
        /// </summary>
        public static GameObject Place(string id, Transform parent, Vector3 localPos, float yaw, float height,
            bool collider = true, Color? tint = null, float gloss = 0.45f, bool cutout = false, bool shadows = true)
        {
            var src = Model(id);
            if (src == null) return null;

            var holder = new GameObject(id);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPos;
            holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var inst = Object.Instantiate(src);
            inst.name = "model";
            // 일단 원점·정방향에 두고 크기를 잰다
            inst.transform.SetParent(null, false);
            inst.transform.position = Vector3.zero;

            var rends = inst.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) { Object.Destroy(inst); return holder; }
            var mat = Material(id, tint ?? Color.white, gloss, cutout);
            Bounds b = rends[0].bounds;
            foreach (var r in rends)
            {
                b.Encapsulate(r.bounds);
                var arr = r.sharedMaterials;
                for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                r.sharedMaterials = arr;
                if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // 모델에 딸려 온 카메라·조명은 지운다
            foreach (var c in inst.GetComponentsInChildren<Camera>(true)) Object.Destroy(c.gameObject);
            foreach (var l in inst.GetComponentsInChildren<Light>(true)) Object.Destroy(l);

            float s = b.size.y > 0.0001f ? height / b.size.y : 1f;
            inst.transform.localScale *= s;
            inst.transform.position = new Vector3(-b.center.x * s, -b.min.y * s, -b.center.z * s);
            inst.transform.SetParent(holder.transform, false);

            if (collider)
            {
                var bc = holder.AddComponent<BoxCollider>();
                bc.size = new Vector3(b.size.x * s, height, b.size.z * s);
                bc.center = new Vector3(0f, height * 0.5f, 0f);
            }
            return holder;
        }
    }
}

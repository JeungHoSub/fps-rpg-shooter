using Hunt.Combat;
using Hunt.Core;
using UnityEngine;

namespace Hunt.Weapons
{
    /// <summary>
    /// 실사 총 모델 (Poly Haven CC0): 권총 = service_pistol, 저격총 = bolt_action_rifle_7_62.
    /// 모델마다 축 방향이 달라서, 방아쇠 위치로 총구 방향을, 슬라이드·조준경 위치로 위쪽을 자동으로 찾아 맞춘다.
    /// 모델 파일이 없으면 null → 도형 총으로 대체.
    /// </summary>
    public static class RealGunModel
    {
        struct Spec
        {
            public string id;
            public float length;      // 총 전체 길이 (m)
            public string trigger;    // 방아쇠 메시 이름 일부
            public string top;        // 위쪽 기준 (슬라이드 / 조준경)
            public string action;     // 쏠 때 움직이는 부품 (슬라이드 / 노리쇠)
            public string magazine;   // 장전 때 빠지는 탄창
            public string[] hide;     // 숨길 부품 (전시용 2번째 총, 낱개 총알 등)
            public string[] accessory;// 별도 텍스처(_acc)를 쓰는 부품
        }

        static Spec? SpecFor(WeaponClass c)
        {
            switch (c)
            {
                case WeaponClass.Pistol:
                    return new Spec
                    {
                        id = "service_pistol", length = 0.2f, trigger = "trigger_a", top = "slide_a", action = "slide_a",
                        magazine = "magazine_loaded", hide = new[] { "_b", "bullet", "magazine_empty" }, accessory = new string[0],
                    };
                case WeaponClass.Sniper:
                    return new Spec
                    {
                        id = "bolt_action_rifle_7_62", length = 1.12f, trigger = "trigger", top = "scope", action = "bolt_a",
                        magazine = null, hide = new[] { "bullet" }, accessory = new[] { "scope", "wrap" },
                    };
                default: return null;
            }
        }

        public static bool Has(WeaponClass c)
        {
            var s = SpecFor(c);
            return s.HasValue && PropModels.Has(s.Value.id);
        }

        /// <summary>
        /// root(총 기준점) 아래에 모델을 만든다. 기준점 = 방아쇠 약간 위 (기관부), +Z = 총구, +Y = 위.
        /// 반환값: 조준선 높이(sightHeight). 총구/탄창/작동부는 m에 채운다.
        /// </summary>
        public static bool Build(Transform root, WeaponClass c, GunModel m)
        {
            var sp = SpecFor(c);
            if (!sp.HasValue) return false;
            var spec = sp.Value;
            var src = Resources.Load<GameObject>("Props/" + spec.id);
            if (src == null) return false;

            var inst = Object.Instantiate(src);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            foreach (var cam in inst.GetComponentsInChildren<Camera>(true)) Object.Destroy(cam.gameObject);
            foreach (var l in inst.GetComponentsInChildren<Light>(true)) Object.Destroy(l);

            // 회색 총철 (사용자: 회색이 낫다) — 권총만 살짝 푸른 기를 더해 깔끔하게
            Color body = spec.id == "service_pistol" ? new Color(0.88f, 0.9f, 0.95f) : Color.white;
            var main = PropModels.Material(spec.id, body, 0.62f, false);
            var acc = PropModels.Material(spec.id + "_acc", new Color(0.85f, 0.86f, 0.9f), 0.45f, false);

            Renderer triggerR = null, topR = null, actionR = null, magR = null;
            Bounds b = default;
            bool any = false;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name.ToLowerInvariant();
                bool hidden = false;
                foreach (var h in spec.hide) if (n.Contains(h)) hidden = true;
                if (hidden) { r.gameObject.SetActive(false); continue; }

                bool isAcc = false;
                foreach (var a in spec.accessory) if (n.Contains(a)) isAcc = true;
                var arr = r.sharedMaterials;
                for (int i = 0; i < arr.Length; i++) arr[i] = isAcc ? acc : main;
                r.sharedMaterials = arr;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;

                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                if (triggerR == null && n.Contains(spec.trigger)) triggerR = r;
                if (topR == null && n.Contains(spec.top)) topR = r;
                if (actionR == null && spec.action != null && n.Contains(spec.action)) actionR = r;
                if (magR == null && spec.magazine != null && n.Contains(spec.magazine)) magR = r;
            }
            if (!any) { Object.Destroy(inst); return false; }

            // ---- 방향 찾기 (모델 공간) ----
            Vector3 size = b.size;
            Vector3 longAxis = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
            Vector3 trig = triggerR != null ? triggerR.bounds.center : b.center;
            // 방아쇠는 총 뒤쪽에 있다 → 반대쪽이 총구
            Vector3 muzzleDir = Vector3.Dot(trig - b.center, longAxis) > 0f ? -longAxis : longAxis;
            Vector3 upDir = Vector3.up;
            if (topR != null)
            {
                Vector3 u = Vector3.ProjectOnPlane(topR.bounds.center - trig, muzzleDir);
                upDir = Snap(u, muzzleDir);
            }
            else upDir = Snap(Vector3.ProjectOnPlane(Vector3.up, muzzleDir), muzzleDir);

            float s = spec.length / Mathf.Max(0.0001f, Vector3.Scale(size, Abs(longAxis)).magnitude);
            var toGun = Quaternion.Inverse(Quaternion.LookRotation(muzzleDir, upDir)); // 모델 → 총 공간

            // 기준점: 방아쇠에서 위로 조금 (기관부 높이)
            Vector3 origin = trig + upDir * (spec.length * (c == WeaponClass.Pistol ? 0.12f : 0.03f));

            var pivot = new GameObject("RealModel").transform;
            pivot.SetParent(root, false);
            pivot.localRotation = toGun;
            pivot.localScale = Vector3.one * s;
            inst.transform.SetParent(pivot, false);
            inst.transform.localPosition = -origin;

            // ---- 총구·조준선 (총 공간) ----
            Vector3 ToGun(Vector3 p) => toGun * ((p - origin) * s);
            Vector3 bMin = b.min, bMax = b.max;
            float maxZ = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bMin.x : bMax.x, (i & 2) == 0 ? bMin.y : bMax.y, (i & 4) == 0 ? bMin.z : bMax.z);
                var g = ToGun(corner);
                maxZ = Mathf.Max(maxZ, g.z);
                maxY = Mathf.Max(maxY, g.y);
            }
            float sight;
            if (c == WeaponClass.Sniper && topR != null) sight = ToGun(topR.bounds.center).y;          // 조준경 중심
            else if (topR != null) sight = ToGun(topR.bounds.max).y * 0.5f + maxY * 0.5f;          // 슬라이드 위
            else sight = maxY;
            m.sightHeight = Mathf.Max(0.02f, sight);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0f, c == WeaponClass.Pistol ? m.sightHeight * 0.55f : 0.02f, maxZ);
            m.muzzle = muzzle;

            // 슬라이드·노리쇠·탄창은 총 공간에 맞춘 부모 아래로 옮겨서 "뒤로/아래로" 움직임이 맞게
            if (actionR != null) m.action = Detach(actionR.transform, root, "Action");
            if (magR != null) m.magazine = Detach(magR.transform, root, "Magazine");
            if (m.action != null) m.actionRest = m.action.localPosition;
            if (c == WeaponClass.Sniper) m.actionTravel = 0.075f; // 노리쇠는 길게 당긴다
            if (m.magazine != null) m.magazineRest = m.magazine.localPosition;

            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = GunModelBuilder.ViewLayer;
            return true;
        }

        static Transform Detach(Transform part, Transform gunRoot, string name)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(gunRoot, false);
            holder.position = part.position;
            holder.rotation = gunRoot.rotation;
            part.SetParent(holder, true);
            return holder;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>v를 muzzle과 수직인 가장 가까운 주축으로 맞춘다.</summary>
        static Vector3 Snap(Vector3 v, Vector3 muzzle)
        {
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3 best = Vector3.up;
            float bestDot = float.MinValue;
            foreach (var a in axes)
            {
                if (Mathf.Abs(Vector3.Dot(a, muzzle)) > 0.5f) continue;
                float d = Vector3.Dot(a, v);
                if (d > bestDot) { bestDot = d; best = a; }
            }
            return best;
        }
    }
}

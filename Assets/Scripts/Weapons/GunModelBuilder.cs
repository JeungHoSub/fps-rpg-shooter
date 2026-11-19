using Hunt.Combat;
using UnityEngine;

namespace Hunt.Weapons
{
    /// <summary>조립된 1인칭 총 모델. 애니메이션에 필요한 부위 참조를 가진다.</summary>
    public class GunModel : MonoBehaviour
    {
        public Transform muzzle;
        public Transform magazine;
        public Vector3 magazineRest;
        public Transform action;          // 슬라이드 / 펌프 / 노리쇠 / 장전손잡이
        public Vector3 actionRest;
        public float actionTravel = 0.03f;
        public bool pumpAction;           // 샷건·저격총: 발사 후 느리게 왕복
        public float sightHeight = 0.06f; // 가늠자 선 높이 (조준 시 화면 중앙에 맞출 높이)
        public Vector3 hipOffset = new Vector3(0.12f, -0.13f, 0.24f);
        public float adsZ = 0.2f;
        public bool hideWhenScoped;       // 저격총: 완전 조준 시 스코프 화면으로 전환
        public float kickStrength = 0.5f;
    }

    /// <summary>
    /// 기본 도형으로 총을 조립한다. 아트가 들어오면 이 클래스만 프리팹 로드로 교체하면 된다.
    /// 좌표 기준: 원점 = 기관부 중앙, +Z = 총구 방향, 단위 m.
    /// </summary>
    public static class GunModelBuilder
    {
        public const int ViewLayer = 1; // TransparentFX: 무기 전용 카메라만 그린다

        static readonly Color Metal = new Color(0.11f, 0.11f, 0.12f);
        static readonly Color MetalLight = new Color(0.24f, 0.24f, 0.26f);

        public static GunModel Build(Transform parent, WeaponInstance w)
        {
            var root = new GameObject("Gun_" + w.data.id);
            root.transform.SetParent(parent, false);
            var m = root.AddComponent<GunModel>();
            var t = root.transform;
            Color furn = w.data.viewColor;
            float muzzleZ = 0.5f, muzzleY = 0.01f;
            Renderer stock = null, barrel = null;

            switch (w.data.weaponClass)
            {
                case WeaponClass.Pistol:
                    Box(t, new Vector3(0, -0.005f, 0.03f), new Vector3(0.03f, 0.03f, 0.17f), Metal);
                    m.action = Box(t, new Vector3(0, 0.025f, 0.035f), new Vector3(0.032f, 0.032f, 0.19f), MetalLight).transform;
                    m.actionTravel = 0.035f;
                    Box(t, new Vector3(0, -0.07f, -0.025f), new Vector3(0.03f, 0.11f, 0.045f), furn, -12f);
                    Box(t, new Vector3(0, 0.046f, 0.12f), new Vector3(0.006f, 0.012f, 0.006f), MetalLight);
                    Box(t, new Vector3(0, 0.046f, -0.05f), new Vector3(0.022f, 0.012f, 0.006f), MetalLight);
                    m.magazine = Box(t, new Vector3(0, -0.128f, -0.037f), new Vector3(0.026f, 0.02f, 0.04f), Metal).transform;
                    m.sightHeight = 0.05f;
                    muzzleZ = 0.13f; muzzleY = 0.025f;
                    m.hipOffset = new Vector3(0.11f, -0.11f, 0.28f); m.adsZ = 0.3f; m.kickStrength = 0.6f;
                    break;

                case WeaponClass.SMG:
                    Box(t, new Vector3(0, 0, 0.05f), new Vector3(0.05f, 0.07f, 0.32f), Metal);
                    Box(t, new Vector3(0, 0.04f, 0.05f), new Vector3(0.025f, 0.01f, 0.25f), MetalLight);
                    barrel = Cyl(t, new Vector3(0, 0.01f, 0.27f), 0.012f, 0.12f, Metal);
                    m.magazine = Box(t, new Vector3(0, -0.11f, 0.1f), new Vector3(0.028f, 0.16f, 0.04f), Metal).transform;
                    Box(t, new Vector3(0, -0.07f, -0.05f), new Vector3(0.035f, 0.1f, 0.045f), furn, -15f);
                    stock = Box(t, new Vector3(0, -0.005f, -0.2f), new Vector3(0.03f, 0.05f, 0.16f), furn);
                    Box(t, new Vector3(0, 0.052f, 0.17f), new Vector3(0.006f, 0.016f, 0.006f), MetalLight);
                    Box(t, new Vector3(0, 0.052f, -0.07f), new Vector3(0.02f, 0.016f, 0.006f), MetalLight);
                    m.action = Box(t, new Vector3(0.03f, 0.02f, 0f), new Vector3(0.012f, 0.015f, 0.03f), MetalLight).transform;
                    m.sightHeight = 0.058f;
                    muzzleZ = 0.33f;
                    m.hipOffset = new Vector3(0.12f, -0.12f, 0.24f); m.adsZ = 0.2f; m.kickStrength = 0.35f;
                    break;

                case WeaponClass.Shotgun:
                    Box(t, Vector3.zero, new Vector3(0.06f, 0.08f, 0.3f), Metal);
                    barrel = Cyl(t, new Vector3(0, 0.02f, 0.38f), 0.017f, 0.46f, Metal);
                    Cyl(t, new Vector3(0, -0.022f, 0.33f), 0.014f, 0.38f, Metal);
                    m.action = Box(t, new Vector3(0, -0.022f, 0.3f), new Vector3(0.055f, 0.05f, 0.16f), furn).transform;
                    m.actionTravel = 0.08f; m.pumpAction = true;
                    stock = Box(t, new Vector3(0, -0.035f, -0.3f), new Vector3(0.045f, 0.1f, 0.32f), furn, 5f);
                    Box(t, new Vector3(0, -0.07f, -0.1f), new Vector3(0.035f, 0.09f, 0.045f), furn, -20f);
                    Box(t, new Vector3(0, 0.044f, 0.6f), new Vector3(0.008f, 0.008f, 0.008f), MetalLight);
                    m.sightHeight = 0.044f;
                    muzzleZ = 0.61f; muzzleY = 0.02f;
                    m.hipOffset = new Vector3(0.12f, -0.12f, 0.22f); m.adsZ = 0.2f; m.kickStrength = 1.4f;
                    break;

                case WeaponClass.Sniper:
                    Box(t, Vector3.zero, new Vector3(0.06f, 0.075f, 0.38f), Metal);
                    barrel = Cyl(t, new Vector3(0, 0.01f, 0.48f), 0.013f, 0.6f, Metal);
                    stock = Box(t, new Vector3(0, -0.02f, -0.33f), new Vector3(0.05f, 0.11f, 0.32f), furn);
                    Box(t, new Vector3(0, 0.035f, -0.3f), new Vector3(0.04f, 0.03f, 0.2f), furn);
                    Box(t, new Vector3(0, -0.07f, -0.1f), new Vector3(0.035f, 0.1f, 0.045f), furn, -15f);
                    Cyl(t, new Vector3(0, 0.075f, 0.03f), 0.022f, 0.32f, Metal);
                    Cyl(t, new Vector3(0, 0.075f, 0.2f), 0.028f, 0.04f, MetalLight);
                    Box(t, new Vector3(0, 0.048f, -0.06f), new Vector3(0.02f, 0.03f, 0.02f), MetalLight);
                    Box(t, new Vector3(0, 0.048f, 0.1f), new Vector3(0.02f, 0.03f, 0.02f), MetalLight);
                    m.action = Box(t, new Vector3(0.04f, 0.015f, -0.05f), new Vector3(0.04f, 0.012f, 0.012f), MetalLight).transform;
                    m.actionTravel = 0.07f; m.pumpAction = true;
                    m.magazine = Box(t, new Vector3(0, -0.06f, 0.05f), new Vector3(0.03f, 0.05f, 0.08f), Metal).transform;
                    m.sightHeight = 0.075f; m.hideWhenScoped = true;
                    muzzleZ = 0.78f;
                    m.hipOffset = new Vector3(0.12f, -0.13f, 0.22f); m.adsZ = 0.1f; m.kickStrength = 1.6f;
                    break;

                case WeaponClass.LMG:
                    Box(t, Vector3.zero, new Vector3(0.08f, 0.1f, 0.42f), Metal);
                    Box(t, new Vector3(0, 0.06f, 0.02f), new Vector3(0.075f, 0.02f, 0.2f), MetalLight);
                    barrel = Cyl(t, new Vector3(0, 0f, 0.45f), 0.017f, 0.42f, Metal);
                    Cyl(t, new Vector3(0, 0f, 0.33f), 0.026f, 0.2f, furn);
                    m.magazine = Box(t, new Vector3(0, -0.11f, 0.04f), new Vector3(0.09f, 0.12f, 0.12f), furn).transform;
                    Box(t, new Vector3(0, 0.095f, 0.12f), new Vector3(0.02f, 0.02f, 0.12f), MetalLight);
                    Box(t, new Vector3(0, -0.08f, -0.12f), new Vector3(0.035f, 0.1f, 0.045f), furn, -15f);
                    stock = Box(t, new Vector3(0, -0.01f, -0.32f), new Vector3(0.05f, 0.1f, 0.24f), furn);
                    Box(t, new Vector3(0, 0.065f, 0.6f), new Vector3(0.008f, 0.03f, 0.008f), MetalLight);
                    Box(t, new Vector3(0, 0.077f, -0.12f), new Vector3(0.03f, 0.014f, 0.01f), MetalLight);
                    m.action = Box(t, new Vector3(0.045f, 0f, 0.05f), new Vector3(0.015f, 0.02f, 0.04f), MetalLight).transform;
                    m.sightHeight = 0.08f;
                    muzzleZ = 0.67f; muzzleY = 0f;
                    m.hipOffset = new Vector3(0.13f, -0.15f, 0.22f); m.adsZ = 0.18f; m.kickStrength = 0.45f;
                    break;

                case WeaponClass.Flamethrower:
                    // 연료통 두 개 + 굵은 관 + 점화구
                    Box(t, Vector3.zero, new Vector3(0.07f, 0.08f, 0.3f), Metal);
                    Cyl(t, new Vector3(0.05f, -0.07f, -0.02f), 0.045f, 0.3f, new Color(0.5f, 0.14f, 0.1f));
                    Cyl(t, new Vector3(-0.05f, -0.07f, -0.02f), 0.045f, 0.3f, new Color(0.5f, 0.14f, 0.1f));
                    barrel = Cyl(t, new Vector3(0, 0.01f, 0.32f), 0.022f, 0.36f, Metal);
                    Cyl(t, new Vector3(0, 0.01f, 0.5f), 0.032f, 0.05f, MetalLight);
                    Box(t, new Vector3(0, -0.07f, 0.12f), new Vector3(0.035f, 0.09f, 0.04f), furn, -15f);
                    stock = Box(t, new Vector3(0, -0.01f, -0.26f), new Vector3(0.045f, 0.08f, 0.2f), furn);
                    Box(t, new Vector3(0, 0.05f, 0.1f), new Vector3(0.01f, 0.025f, 0.01f), MetalLight);
                    m.sightHeight = 0.06f;
                    muzzleZ = 0.53f;
                    m.hipOffset = new Vector3(0.13f, -0.15f, 0.22f); m.adsZ = 0.2f; m.kickStrength = 0.1f;
                    break;

                case WeaponClass.Launcher:
                    // 굵고 짧은 발사관 + 나무 개머리 + 접이식 가늠자
                    barrel = Cyl(t, new Vector3(0, 0.01f, 0.18f), 0.04f, 0.5f, new Color(0.18f, 0.2f, 0.15f));
                    Cyl(t, new Vector3(0, 0.01f, 0.43f), 0.046f, 0.04f, Metal);
                    Box(t, new Vector3(0, -0.02f, -0.05f), new Vector3(0.06f, 0.07f, 0.16f), Metal);
                    Box(t, new Vector3(0, -0.08f, -0.03f), new Vector3(0.035f, 0.1f, 0.045f), furn, -15f);
                    stock = Box(t, new Vector3(0, -0.03f, -0.27f), new Vector3(0.05f, 0.1f, 0.26f), furn, 4f);
                    Box(t, new Vector3(0, 0.075f, 0.05f), new Vector3(0.03f, 0.04f, 0.006f), MetalLight);
                    m.action = Box(t, new Vector3(0, 0.055f, -0.05f), new Vector3(0.02f, 0.012f, 0.05f), MetalLight).transform;
                    m.actionTravel = 0.04f; m.pumpAction = true;
                    m.sightHeight = 0.09f;
                    muzzleZ = 0.45f;
                    m.hipOffset = new Vector3(0.13f, -0.14f, 0.24f); m.adsZ = 0.2f; m.kickStrength = 1.8f;
                    break;

                default: // Rifle
                    Box(t, Vector3.zero, new Vector3(0.055f, 0.08f, 0.36f), Metal);
                    Box(t, new Vector3(0, 0f, 0.32f), new Vector3(0.06f, 0.065f, 0.26f), furn);
                    barrel = Cyl(t, new Vector3(0, 0.01f, 0.5f), 0.011f, 0.16f, Metal);
                    Box(t, new Vector3(0, 0.045f, 0.05f), new Vector3(0.025f, 0.01f, 0.3f), MetalLight);
                    Box(t, new Vector3(0, 0.058f, 0.43f), new Vector3(0.008f, 0.03f, 0.008f), MetalLight);
                    Box(t, new Vector3(0, 0.058f, -0.12f), new Vector3(0.03f, 0.016f, 0.012f), MetalLight);
                    var mag = new GameObject("Magazine").transform;
                    mag.SetParent(t, false);
                    mag.localPosition = new Vector3(0, -0.05f, 0.1f);
                    Box(mag, new Vector3(0, -0.05f, 0f), new Vector3(0.03f, 0.1f, 0.06f), Metal, 8f);
                    Box(mag, new Vector3(0, -0.125f, 0.018f), new Vector3(0.03f, 0.07f, 0.055f), Metal, 20f);
                    m.magazine = mag;
                    Box(t, new Vector3(0, -0.075f, -0.06f), new Vector3(0.035f, 0.1f, 0.045f), furn, -15f);
                    stock = Box(t, new Vector3(0, -0.01f, -0.3f), new Vector3(0.045f, 0.09f, 0.24f), furn);
                    m.action = Box(t, new Vector3(0.033f, 0.015f, 0.02f), new Vector3(0.012f, 0.015f, 0.035f), MetalLight).transform;
                    m.sightHeight = 0.066f;
                    muzzleZ = 0.58f;
                    m.hipOffset = new Vector3(0.12f, -0.13f, 0.24f); m.adsZ = 0.2f; m.kickStrength = 0.5f;
                    break;
            }

            // ---- 장착 파츠가 모델에 보이도록 ----
            if (w.parts.TryGetValue(PartSlot.Scope, out var scope) && w.data.weaponClass != WeaponClass.Sniper)
            {
                // 홀로그램 조준경: 창틀 + 가운데 점. 조준선이 창 가운데로 올라간다.
                float sh = m.sightHeight;
                float z = 0.04f;
                Box(t, new Vector3(0, sh + 0.004f, z), new Vector3(0.034f, 0.01f, 0.05f), Metal);
                Box(t, new Vector3(0, sh + 0.046f, z + 0.015f), new Vector3(0.034f, 0.004f, 0.006f), Metal);
                Box(t, new Vector3(0.015f, sh + 0.027f, z + 0.015f), new Vector3(0.004f, 0.04f, 0.006f), Metal);
                Box(t, new Vector3(-0.015f, sh + 0.027f, z + 0.015f), new Vector3(0.004f, 0.04f, 0.006f), Metal);
                Box(t, new Vector3(0, sh + 0.027f, z + 0.03f), new Vector3(0.003f, 0.003f, 0.002f), WeaponPart.RarityColor(scope.rarity) * 1.5f, 0f, true);
                m.sightHeight = sh + 0.027f;
            }
            if (w.parts.ContainsKey(PartSlot.Muzzle))
            {
                Cyl(t, new Vector3(0, muzzleY, muzzleZ + 0.07f), 0.02f, 0.14f, Metal);
                muzzleZ += 0.14f;
            }
            if (w.parts.ContainsKey(PartSlot.Magazine) && m.magazine != null)
            {
                var s = m.magazine.localScale;
                m.magazine.localScale = new Vector3(s.x, s.y * 1.35f, s.z);
            }
            if (w.parts.TryGetValue(PartSlot.Stock, out var stockPart) && stock != null)
                SetColor(stock, Color.Lerp(stock.material.color, WeaponPart.RarityColor(stockPart.rarity), 0.5f));
            if (w.parts.TryGetValue(PartSlot.Barrel, out var barrelPart) && barrel != null)
                SetColor(barrel, Color.Lerp(Metal, WeaponPart.RarityColor(barrelPart.rarity), 0.35f));
            if (w.parts.ContainsKey(PartSlot.Module))
                Box(t, new Vector3(0.036f, 0f, 0.05f), new Vector3(0.012f, 0.03f, 0.06f), CombatNames.ColorOf(w.DamageType), 0f, true);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(t, false);
            muzzle.localPosition = new Vector3(0, muzzleY, muzzleZ);
            m.muzzle = muzzle;

            if (m.magazine != null) m.magazineRest = m.magazine.localPosition;
            if (m.action != null) m.actionRest = m.action.localPosition;

            SetLayerRecursive(root, ViewLayer);
            return m;
        }

        static Renderer Box(Transform parent, Vector3 pos, Vector3 size, Color c, float rotX = 0f, bool unlit = false)
        {
            return Prim(PrimitiveType.Cube, parent, pos, size, Quaternion.Euler(rotX, 0, 0), c, unlit);
        }

        /// <summary>Z축 방향 원통.</summary>
        static Renderer Cyl(Transform parent, Vector3 pos, float radius, float length, Color c)
        {
            return Prim(PrimitiveType.Cylinder, parent, pos, new Vector3(radius * 2f, length * 0.5f, radius * 2f), Quaternion.Euler(90f, 0, 0), c, false);
        }

        static Renderer Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Color c, bool unlit)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>()); // 같은 프레임 레이캐스트에 걸리지 않도록 즉시 제거
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (unlit) r.sharedMaterial = Core.FX.UnlitMaterial(c);
            else r.material.color = c;
            return r;
        }

        static void SetColor(Renderer r, Color c) { r.material.color = c; }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }
    }
}

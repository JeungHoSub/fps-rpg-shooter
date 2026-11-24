using Hunt.Core;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>손전등 (F). 어두운 밤의 주 광원. 시점보다 살짝 늦게 따라와 흔들린다.</summary>
    public class Flashlight : MonoBehaviour
    {
        Light spot;
        Transform cam;
        Quaternion lagRot;
        public bool IsOn => spot != null && spot.enabled;

        /// <summary>망자 감지용: 손전등이 켜져 있는지, 빛이 나가는 눈 위치.</summary>
        public static bool Active { get; private set; }
        public static Transform Eye { get; private set; }
        /// <summary>난이도 변형 '시야 감소': 손전등을 쓸 수 없음.</summary>
        public static bool Disabled;

        void OnDestroy() { if (Eye == cam) { Eye = null; Active = false; } BossNear = 0f; } // 판이 끝나면 마을 불빛 떨림도 멈춘다

        public void Init(Transform camera)
        {
            cam = camera;
            var go = new GameObject("Flashlight");
            go.transform.SetParent(camera, false);
            go.transform.localPosition = new Vector3(0.18f, -0.12f, 0.1f);
            spot = go.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.93f, 0.82f);
            spot.range = 32f;
            spot.spotAngle = 52f;
            spot.innerSpotAngle = 24f;
            spot.intensity = 2.6f;
            spot.shadows = LightShadows.Soft; // 하 품질에서는 전역 설정으로 그림자 꺼짐
            spot.shadowStrength = 0.85f;
            spot.renderMode = LightRenderMode.ForcePixel; // 조명 수를 줄여도 손전등은 항상 제대로
            lagRot = camera.rotation;
            Eye = camera;
            Active = spot.enabled;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (spot == null || gm == null) return;
            if (gm.AcceptsGameplayInput && GameInput.FlashlightDown)
            {
                if (Disabled && !spot.enabled) Hunt.UI.HUD.Notify("이번 밤은 손전등을 쓸 수 없다", new Color(1f, 0.6f, 0.4f));
                else
                {
                    spot.enabled = !spot.enabled;
                    SFX.Play(SFX.FlashlightClick, 0.6f);
                    Noise.Emit(cam.position, 3f, true);
                }
            }
            if (Disabled && spot.enabled) spot.enabled = false;
            Active = spot.enabled;
            // 빛줄기가 시점을 살짝 늦게 따라온다
            lagRot = Quaternion.Slerp(lagRot, cam.rotation, Time.deltaTime * 14f);
            spot.transform.rotation = lagRot;
            // 배터리가 약한 듯 아주 가끔 흔들림
            float k = 0.96f + 0.04f * Mathf.PerlinNoise(Time.time * 6f, 3.3f);
            // 보스가 25m 안이면 불빛이 떨린다 (가까울수록 자주, 꺼질 듯) — 0.25초마다 한 번만 찾는다
            if ((bossCheck -= Time.deltaTime) <= 0f)
            {
                bossCheck = 0.25f; bossNear = 0f;
                foreach (var m in Hunt.Monsters.Monster.Alive)
                {
                    if (m == null || m.IsDead || !m.IsBoss) continue;
                    float d = Vector3.Distance(m.transform.position, cam.position);
                    if (d < 25f) bossNear = Mathf.Max(bossNear, 1f - d / 25f);
                }
            }
            BossNear = bossNear;
            if (bossNear > 0f && Hunt.Core.GameSettings.ShakeMult > 0f) // 흔들림 끔이면 깜빡임도 끔
            {
                float n = Mathf.PerlinNoise(Time.time * (9f + bossNear * 14f), 7.1f);
                if (n < 0.18f + bossNear * 0.22f) k *= 0.15f + n; // 툭 꺼질 듯
            }
            spot.intensity = 2.6f * k;
        }

        float bossCheck, bossNear;
        /// <summary>보스가 얼마나 가까운가 0~1 (25m 밖 0). 가로등 떨림도 이 값을 쓴다.</summary>
        public static float BossNear;

    }
}

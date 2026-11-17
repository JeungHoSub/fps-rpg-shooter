using Hunt.Core;
using Hunt.Weapons;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>
    /// 1인칭 무기 연출: 반동 킥, 걸음 흔들림, 마우스 스웨이, 달리기 자세, 재장전/교체 동작,
    /// 펌프·노리쇠·슬라이드 동작, 총구 화염. 별도 무기 카메라(레이어 1)로 그려 벽에 파묻히지 않는다.
    /// </summary>
    public class WeaponViewModel : MonoBehaviour
    {
        PlayerCharacter player;
        GunModel model;
        int builtSignature;

        Vector3 swayPos;
        Vector2 swayRot;
        float bobT, bobBlend, sprintBlend;
        Vector3 kickPos, kickRot;
        float actionT = 1f, actionDuration = 0.1f;

        GameObject flash;
        Light flashLight;
        float flashTime;

        public Vector3 MuzzlePosition => model != null ? model.muzzle.position : transform.position;

        public void Init(PlayerCharacter p) { player = p; }

        static int Signature(WeaponInstance w)
        {
            int h = w.GetHashCode();
            foreach (var p in w.parts.Values) h += p.GetHashCode() * 31; // 순서 무관 합
            return h;
        }

        void EnsureModel()
        {
            var w = player.Weapons.Current;
            if (w == null) return;
            int sig = Signature(w);
            if (model != null && sig == builtSignature) return;
            // 교체 중에는 이전 총이 완전히 내려간 뒤(절반 지점)에 새 총으로 바꾼다
            if (model != null && player.Weapons.SwapPhase < 0.5f) return;

            if (model != null) Destroy(model.gameObject);
            model = GunModelBuilder.Build(transform, w);
            builtSignature = sig;
            actionT = 1f;
            CreateFlash();
        }

        void CreateFlash()
        {
            flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(flash.GetComponent<Collider>());
            flash.name = "MuzzleFlash";
            flash.transform.SetParent(model.muzzle, false);
            flash.transform.localPosition = new Vector3(0, 0, 0.05f);
            var r = flash.GetComponent<Renderer>();
            r.sharedMaterial = FX.UnlitMaterial(new Color(1f, 0.85f, 0.45f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flash.layer = GunModelBuilder.ViewLayer;
            flashLight = flash.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(1f, 0.7f, 0.35f);
            flashLight.range = 6f;
            flashLight.intensity = 3f;
            flash.SetActive(false);
        }

        public void OnFire(WeaponInstance w)
        {
            EnsureModel();
            if (model == null) return;
            float s = model.kickStrength * Mathf.Lerp(1f, 0.6f, player.Weapons.Aim);
            kickPos += new Vector3(0f, 0.008f * s, -0.05f * s);
            kickRot += new Vector3(-8.5f * s, Random.Range(-2f, 2f) * s, Random.Range(-4f, 4f) * s);

            flashTime = 0.045f;
            float big = w.data.weaponClass == Combat.WeaponClass.Shotgun || w.data.weaponClass == Combat.WeaponClass.Sniper ? 1.6f : 1f;
            flash.transform.localScale = new Vector3(Random.Range(0.05f, 0.08f), Random.Range(0.05f, 0.08f), Random.Range(0.1f, 0.16f)) * big;
            flash.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            actionT = 0f;
            actionDuration = model.pumpAction ? Mathf.Max(0.35f, w.FireInterval * 0.8f) : Mathf.Min(0.08f, w.FireInterval * 0.8f);
        }

        void LateUpdate()
        {
            if (player == null || player.Weapons == null || player.Weapons.Current == null) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.Paused) return;
            EnsureModel();
            if (model == null) return;

            float dt = Time.deltaTime;
            var wp = player.Weapons;
            var ctrl = player.Controller;
            float aim = Mathf.SmoothStep(0f, 1f, wp.Aim);
            bool input = gm.AcceptsGameplayInput;

            // 마우스 스웨이: 시점을 돌리면 총이 살짝 늦게 따라온다
            float mx = input ? Input.GetAxis("Mouse X") : 0f;
            float my = input ? Input.GetAxis("Mouse Y") : 0f;
            float swayK = 1f - aim * 0.85f;
            var targetRot = new Vector2(Mathf.Clamp(my * 2f, -5f, 5f), Mathf.Clamp(-mx * 2f, -5f, 5f)) * swayK;
            swayRot = Vector2.Lerp(swayRot, targetRot, dt * 8f);
            var targetPos = new Vector3(Mathf.Clamp(-mx * 0.004f, -0.02f, 0.02f), Mathf.Clamp(-my * 0.004f, -0.02f, 0.02f), 0f) * swayK;
            swayPos = Vector3.Lerp(swayPos, targetPos, dt * 8f);

            // 걸음 흔들림 / 달리기
            bool moving = ctrl.IsMoving && ctrl.IsGrounded;
            bool sprint = ctrl.IsSprinting && moving;
            if (moving) bobT += dt * (sprint ? 13f : 9f);
            bobBlend = Mathf.MoveTowards(bobBlend, moving ? (sprint ? 1.8f : 1f) * (1f - aim * 0.85f) : 0f, dt * 5f);
            Vector3 bob = new Vector3(Mathf.Sin(bobT) * 0.009f, -Mathf.Abs(Mathf.Cos(bobT)) * 0.008f, 0f) * bobBlend;
            Vector3 breathe = new Vector3(0f, Mathf.Sin(Time.time * 1.6f) * 0.0015f, 0f) * (1f - aim * 0.7f);
            sprintBlend = Mathf.MoveTowards(sprintBlend, sprint && !wp.IsReloading ? 1f : 0f, dt * 6f);

            // 반동 복귀 (스프링)
            kickPos = Vector3.Lerp(kickPos, Vector3.zero, dt * 14f);
            kickRot = Vector3.Lerp(kickRot, Vector3.zero, dt * 11f);

            // 기본 자세: 허리 사격 ↔ 조준 (가늠자 높이를 화면 중앙에)
            Vector3 ads = new Vector3(0f, -model.sightHeight, model.adsZ);
            Vector3 pos = Vector3.Lerp(model.hipOffset, ads, aim) + swayPos + bob + breathe + kickPos;
            Vector3 rot = new Vector3(swayRot.x, swayRot.y, 0f) + kickRot;

            pos += new Vector3(-0.05f, -0.04f, -0.03f) * sprintBlend;
            rot += new Vector3(12f, -35f, 12f) * sprintBlend;

            // 재장전: 총을 기울이고 탄창을 뺐다가 끼운다
            if (wp.IsReloading)
            {
                float p = wp.ReloadProgress;
                float k = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI);
                pos += new Vector3(-0.02f, -0.05f, 0f) * k;
                rot += new Vector3(18f, 10f, 32f) * k;
                if (model.magazine != null)
                {
                    float drop = p < 0.25f ? 0f : p < 0.55f ? Mathf.InverseLerp(0.25f, 0.45f, p) : p < 0.85f ? 1f - Mathf.InverseLerp(0.55f, 0.8f, p) : 0f;
                    model.magazine.localPosition = model.magazineRest + Vector3.down * 0.25f * drop;
                    model.magazine.gameObject.SetActive(drop < 0.98f);
                }
            }
            else if (model.magazine != null)
            {
                model.magazine.localPosition = model.magazineRest;
                model.magazine.gameObject.SetActive(true);
            }

            // 무기 교체: 내렸다가 올린다
            float sw = wp.SwapProgress;
            pos += Vector3.down * 0.25f * sw;
            rot.x += 40f * sw;

            // 슬라이드 / 펌프 / 노리쇠
            if (model.action != null)
            {
                actionT += dt / Mathf.Max(0.01f, actionDuration);
                float a = actionT >= 1f ? 0f : Mathf.Sin(Mathf.Clamp01(actionT) * Mathf.PI);
                model.action.localPosition = model.actionRest + Vector3.back * model.actionTravel * a;
            }

            transform.localPosition = pos;
            transform.localRotation = Quaternion.Euler(rot);

            // 저격총: 완전 조준 시 총을 숨기고 HUD 스코프로 전환
            model.gameObject.SetActive(!(model.hideWhenScoped && wp.Aim > 0.92f));

            flashTime -= dt;
            if (flash != null) flash.SetActive(flashTime > 0f);
        }

        public bool IsScoped => model != null && model.hideWhenScoped && player.Weapons.Aim > 0.92f;
    }
}

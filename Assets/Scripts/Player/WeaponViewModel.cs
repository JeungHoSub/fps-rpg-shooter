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

        static Material starMat;

        /// <summary>총구 불꽃 별 무늬 (6갈래 가시 + 밝은 심, 코드로 한 번 만든다).</summary>
        static Material StarMaterial()
        {
            if (starMat != null) return starMat;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "MuzzleStar" };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - S / 2f + 0.5f) / (S / 2f), dy = (y - S / 2f + 0.5f) / (S / 2f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float spike = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 3f)), 5f);         // 6갈래 (굵게)
                    float core = Mathf.Clamp01(1f - r * 2.4f);
                    float a = Mathf.Clamp01(core * 1.3f + spike * Mathf.Clamp01(1f - r) * 1.3f);
                    a *= Mathf.Clamp01((1f - r) * 3f);
                    float w = Mathf.Clamp01(core * 1.5f);                                   // 심은 하얗게, 끝은 주황
                    px[y * S + x] = new Color32(255, (byte)(170 + 85 * w), (byte)(70 + 160 * w), (byte)(255 * a));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            starMat = new Material(Shader.Find("Sprites/Default")) { name = "MuzzleStar", mainTexture = tex };
            return starMat;
        }

        void CreateFlash()
        {
            flash = FX.LowSphere();
            flash.name = "MuzzleFlash";
            flash.transform.SetParent(model.muzzle, false);
            flash.transform.localPosition = new Vector3(0, 0, 0.05f);
            var r = flash.GetComponent<Renderer>();
            r.sharedMaterial = FX.UnlitMaterial(new Color(1f, 0.85f, 0.45f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flash.layer = GunModelBuilder.ViewLayer;
            // 별 모양 불꽃 두 장: 정면(총구를 마주 보는 별) + 옆면(총열 따라 뻗는 혀) — 공 하나보다 총구 불꽃처럼 보이게
            var starMat = StarMaterial();
            for (int i = 0; i < 2; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                DestroyImmediate(q.GetComponent<Collider>());
                q.name = i == 0 ? "FlashFront" : "FlashSide";
                q.layer = GunModelBuilder.ViewLayer;
                q.transform.SetParent(flash.transform, false);
                // 부모(공)의 크기에 따라 함께 커진다 — 공보다 훨씬 넓게 퍼진 별
                q.transform.localPosition = i == 0 ? Vector3.zero : new Vector3(0f, 0f, 0.35f);
                q.transform.localRotation = i == 0 ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                q.transform.localScale = i == 0 ? new Vector3(4.2f, 4.2f, 1f) : new Vector3(3.4f, 1.6f, 1f);
                var qr = q.GetComponent<Renderer>();
                qr.sharedMaterial = starMat;
                qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                qr.receiveShadows = false;
            }
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
            if (player.Weapons.Mode != PlayerWeapons.Hand.Gun)
            {
                if (model != null) model.gameObject.SetActive(false);
                if (flash != null) flash.SetActive(false);
                DrawHeld(Time.deltaTime);
                return;
            }
            ShowHeld(null);
            EnsureModel();
            if (model == null) return;

            float dt = Time.deltaTime;
            var wp = player.Weapons;
            var ctrl = player.Controller;
            float aim = Mathf.SmoothStep(0f, 1f, wp.Aim);
            bool input = gm.AcceptsGameplayInput;

            // 마우스 스웨이: 시점을 돌리면 총이 살짝 늦게 따라온다
            float mx = input ? GameInput.LookX : 0f;
            float my = input ? GameInput.LookY : 0f;
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

        // ---------------------------------------------------------------- 칼 · 특수 기기
        GameObject machete, grenade, mine, turretKit;
        float heldBob;

        static Material Mat(Color c, float gloss, float metal) => EnvMaterials.Plain(c, gloss, metal);

        static GameObject Piece(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
        {
            var g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localRotation = rot ?? Quaternion.identity;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.layer = GunModelBuilder.ViewLayer;
            return g;
        }

        GameObject Root(string name)
        {
            var g = new GameObject(name);
            g.layer = GunModelBuilder.ViewLayer;
            g.transform.SetParent(transform, false);
            return g;
        }

        void BuildHeld()
        {
            if (machete != null) return;
            // 마체테: 손잡이(검은 고무) + 넓적한 칼날(녹슨 쇠) — 칼끝이 앞(+Z)
            machete = Root("Machete");
            var steel = Mat(new Color(0.55f, 0.55f, 0.53f), 0.8f, 0.85f);
            var edge = Mat(new Color(0.75f, 0.75f, 0.74f), 0.9f, 0.9f);
            var grip = Mat(new Color(0.06f, 0.06f, 0.06f), 0.3f, 0f);
            Piece(machete.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.06f), new Vector3(0.032f, 0.07f, 0.032f), grip, Quaternion.Euler(90f, 0f, 0f));
            Piece(machete.transform, PrimitiveType.Cube, new Vector3(0f, 0.012f, 0.14f), new Vector3(0.012f, 0.05f, 0.02f), steel);
            Piece(machete.transform, PrimitiveType.Cube, new Vector3(0f, 0.012f, 0.36f), new Vector3(0.006f, 0.055f, 0.42f), steel);
            Piece(machete.transform, PrimitiveType.Cube, new Vector3(0f, -0.016f, 0.37f), new Vector3(0.004f, 0.012f, 0.4f), edge);
            Piece(machete.transform, PrimitiveType.Cube, new Vector3(0f, 0.0f, 0.585f), new Vector3(0.006f, 0.04f, 0.05f), steel, Quaternion.Euler(-30f, 0f, 0f));

            grenade = Root("GrenadeHeld");
            if (PropModels.PlaceHeld("stick_grenade", grenade.transform, 0.36f, GunModelBuilder.ViewLayer, new Color(0.7f, 0.7f, 0.65f)) == null)
                Piece(grenade.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.08f, Gadgets.Dark);

            mine = Root("MineHeld");
            Piece(mine.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.26f, 0.03f, 0.26f), Mat(new Color(0.2f, 0.24f, 0.16f), 0.4f, 0.3f));
            Piece(mine.transform, PrimitiveType.Sphere, new Vector3(0.08f, 0.035f, 0f), Vector3.one * 0.02f, FX.UnlitMaterial(new Color(1f, 0.15f, 0.1f)));

            turretKit = Root("TurretHeld");
            Piece(turretKit.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.14f, 0.14f, 0.42f), Mat(new Color(0.22f, 0.24f, 0.2f), 0.55f, 0.6f));
            Piece(turretKit.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0.3f), new Vector3(0.045f, 0.12f, 0.045f), Gadgets.Dark, Quaternion.Euler(90f, 0f, 0f));
            ShowHeld(null);
        }

        void ShowHeld(GameObject which)
        {
            if (machete == null) return;
            machete.SetActive(which == machete);
            grenade.SetActive(which == grenade);
            mine.SetActive(which == mine);
            turretKit.SetActive(which == turretKit);
        }

        void DrawHeld(float dt)
        {
            BuildHeld();
            var wp = player.Weapons;
            var ctrl = player.Controller;
            GameObject show;
            if (wp.Mode == PlayerWeapons.Hand.Melee) show = machete;
            else show = wp.Gadget == GadgetType.Grenade || wp.Gadget == GadgetType.Molotov ? grenade : wp.Gadget == GadgetType.Mine ? mine : turretKit; // 화염병은 던지는 손 모양을 같이 쓴다
            if (wp.Mode == PlayerWeapons.Hand.Gadget && wp.GadgetCount[(int)wp.Gadget] <= 0) show = null;
            ShowHeld(show);

            bool moving = ctrl.IsMoving && ctrl.IsGrounded;
            if (moving) heldBob += dt * (ctrl.IsSprinting ? 13f : 9f);
            Vector3 bob = moving ? new Vector3(Mathf.Sin(heldBob) * 0.012f, -Mathf.Abs(Mathf.Cos(heldBob)) * 0.01f, 0f) : Vector3.zero;
            Vector3 pos = new Vector3(0.2f, -0.2f, 0.32f) + bob;
            Vector3 rot = new Vector3(-10f, -8f, -25f);

            float t = wp.MeleeSwing;
            if (t < 1f)
            {
                if (wp.Mode == PlayerWeapons.Hand.Melee)
                {
                    // 들어 올렸다가(0~0.3) 오른쪽 위에서 왼쪽 아래로 벤다(0.3~0.55), 돌아온다
                    if (t < 0.3f)
                    {
                        float k = t / 0.3f;
                        pos += new Vector3(0.06f, 0.12f, -0.05f) * k;
                        rot += new Vector3(-55f, 20f, -20f) * k;
                    }
                    else if (t < 0.55f)
                    {
                        float k = (t - 0.3f) / 0.25f;
                        pos += Vector3.Lerp(new Vector3(0.06f, 0.12f, -0.05f), new Vector3(-0.32f, -0.12f, 0.12f), k);
                        rot += Vector3.Lerp(new Vector3(-55f, 20f, -20f), new Vector3(50f, -60f, -60f), k);
                    }
                    else
                    {
                        float k = 1f - (t - 0.55f) / 0.45f;
                        pos += new Vector3(-0.32f, -0.12f, 0.12f) * k;
                        rot += new Vector3(50f, -60f, -60f) * k;
                    }
                }
                else
                {
                    // 던지기: 뒤로 젖혔다가 앞으로
                    float k = Mathf.Sin(t * Mathf.PI);
                    pos += new Vector3(0.05f, 0.15f, t < 0.4f ? -0.15f : 0.2f) * k;
                    rot += new Vector3(t < 0.4f ? -40f : 30f, 0f, 0f) * k;
                }
            }
            float sw = wp.SwapProgress;
            pos += Vector3.down * 0.25f * sw;
            rot.x += 40f * sw;
            transform.localPosition = pos;
            transform.localRotation = Quaternion.Euler(rot);
        }

        public bool IsScoped => model != null && model.hideWhenScoped && player.Weapons.Aim > 0.92f;
    }
}

using Hunt.Core;
using Hunt.Monsters;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>1인칭 이동 + 시점 + 반동 적용.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public const float EyeHeight = 1.65f;
        /// <summary>기본 시야각 — Esc 메뉴 '시야각' 설정.</summary>
        public static float BaseFov => GameSettings.Fov;

        public float jumpSpeed = 5.5f;

        public bool IsMoving { get; private set; }
        public bool IsGrounded => cc != null && cc.isGrounded;
        public float StationaryTime { get; private set; }
        public float WeightPenalty { get; private set; } = 1f;

        /// <summary>무기 쪽에서 설정: 조준 중 감도/이동 배율.</summary>
        public float LookMultiplier { get; set; } = 1f;
        public float MoveMultiplier { get; set; } = 1f;

        PlayerCharacter player;
        Camera cam;
        CharacterController cc;
        float yaw, pitch, verticalVel;
        Vector2 recoil; // x = 좌우, y = 위 (도)

        public void Init(PlayerCharacter p, Camera camera)
        {
            player = p;
            cam = camera;
            cc = GetComponent<CharacterController>();
            yaw = transform.eulerAngles.y;
        }

        public bool IsSprinting { get; private set; }

        // ---- 앉기 (Ctrl): 눈높이가 낮아지고 느려지지만, 탄이 모이고 반동이 줄고, 발소리·모습이 덜 드러난다 ----
        public const float CrouchEyeHeight = 1.05f;
        public bool IsCrouching { get; private set; }
        /// <summary>0 = 섬, 1 = 완전히 앉음 (부드럽게 바뀐다).</summary>
        public float CrouchBlend { get; private set; }
        /// <summary>앉아서 쏠 때 탄퍼짐 배율.</summary>
        public float CrouchSpreadMult => Mathf.Lerp(1f, 0.6f, CrouchBlend);
        /// <summary>앉아서 쏠 때 반동 배율.</summary>
        public float CrouchRecoilMult => Mathf.Lerp(1f, 0.65f, CrouchBlend);
        float shake, sprintBlock, stepTimer;
        bool wasGrounded = true;

        // ---- 스태미너: 달리기·점프·칼질에 쓰고, 쉬면 찬다. 바닥나면 25까지 회복해야 다시 달린다 ----
        public float Stamina { get; private set; } = 100f;
        public float MaxStamina => 100f + player.Stats.Agility * 1.5f + player.Armor.BonusStamina;
        public bool Exhausted { get; private set; }
        /// <summary>스태미너 회복제: 남은 시간 동안 소모 절반, 회복 두 배.</summary>
        public float BoostTime { get; private set; }
        float staminaRestTimer;

        public bool UseStamina(float amount)
        {
            if (Stamina < amount * 0.5f) return false;
            Stamina = Mathf.Max(0f, Stamina - amount * (BoostTime > 0f ? 0.5f : 1f));
            staminaRestTimer = 0.8f;
            if (Stamina <= 0.5f) Exhausted = true;
            return true;
        }

        public void RestoreStamina(float boostSeconds)
        {
            Stamina = MaxStamina;
            Exhausted = false;
            BoostTime = Mathf.Max(BoostTime, boostSeconds);
        }
        Vector2 punch, punchVel; // 사격 시 화면이 튀었다 돌아오는 반동 (조준점과 별개)

        /// <summary>시점 반동 (도). 스프링처럼 튀었다가 돌아온다.</summary>
        public void AddPunch(float pitchUp, float yaw) { punchVel += new Vector2(yaw, pitchUp) * 28f; }

        /// <summary>조준점 근처(7° 안, 45m 안, 보이는) 가장 가까운 망자.</summary>
        Monster FindAssistTarget(out Vector3 point)
        {
            point = Vector3.zero;
            Monster best = null;
            float bestAngle = player.Weapons.IsAiming ? 4f : 7f;
            Vector3 eye = cam.transform.position, fwd = cam.transform.forward;
            foreach (var m in Monster.Alive)
            {
                if (m == null || m.IsDead) continue;
                Vector3 p = m.transform.position + Vector3.up * 1.25f * m.Data.scale;
                Vector3 d = p - eye;
                float dist = d.magnitude;
                if (dist > 45f || dist < 0.5f) continue;
                float ang = Vector3.Angle(fwd, d);
                if (ang >= bestAngle) continue;
                if (Physics.Raycast(eye, d / dist, out var hit, dist, ~((1 << 2) | (1 << 1)), QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponent<MonsterPart>() == null && hit.distance < dist - 1f) continue; // 벽 뒤
                bestAngle = ang;
                best = m;
                point = p;
            }
            return best;
        }

        public void AddRecoil(Vector2 kick) { recoil += kick; }

        /// <summary>화면 흔들림 추가 (도 단위).</summary>
        public void AddShake(float degrees) { shake = Mathf.Min(shake + degrees, 4f); }

        /// <summary>밀려남 (보스 돌진·내려치기). 금방 잦아든다.</summary>
        public void Knock(Vector3 v) { knock += new Vector3(v.x, 0f, v.z); if (v.y > 0f && cc != null && cc.isGrounded) verticalVel = Mathf.Max(verticalVel, v.y); }
        Vector3 knock;

        /// <summary>사격 시 달리기를 잠시 끊는다.</summary>
        public void CancelSprint() { sprintBlock = 0.3f; IsSprinting = false; }

        public void RecoverRecoil(float dt, float rate)
        {
            recoil = Vector2.MoveTowards(recoil, Vector2.zero, rate * dt * (1f + recoil.magnitude * 0.5f));
        }

        // ---- 쓰러질 때: 시선이 천천히 땅으로 떨어지며 옆으로 기운다 (게임이 멈춰도 돌아가게 실제 시간 기준) ----
        float deathT;
        bool gunCamOff;

        void DeathCam()
        {
            if (cam == null) return;
            deathT += Time.unscaledDeltaTime;
            // 손에서 총이 떨어진다: 무기 전용 카메라를 끈다
            if (deathT > 0.25f && !gunCamOff)
            {
                gunCamOff = true; // 한 번만 찾는다 (매 프레임 검색·할당 방지)
                foreach (var c in cam.GetComponentsInChildren<Camera>())
                    if (c != cam && c.enabled) c.enabled = false;
            }
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(deathT / 1.8f));
            var lp = cam.transform.localPosition;
            cam.transform.localPosition = new Vector3(lp.x, Mathf.Lerp(EyeHeight, 0.28f, k), lp.z);
            // 무릎 꿇듯 앞으로 숙였다가 옆으로 쓰러진다
            float bow = Mathf.Sin(Mathf.Clamp01(deathT / 0.9f) * Mathf.PI) * 18f;
            cam.transform.localRotation = Quaternion.Euler(Mathf.Lerp(pitch, 12f, k) + bow * (1f - k), 0f, Mathf.Lerp(0f, 78f, k));
            if (cam.fieldOfView > BaseFov - 12f) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, BaseFov - 12f, Time.unscaledDeltaTime * 1.5f);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (player != null && player.IsDead) { DeathCam(); IsSprinting = false; return; }
            if (gm == null || !gm.IsWorldRunning || player == null) { IsSprinting = false; return; }
            float dt = Time.deltaTime;
            bool input = gm.AcceptsGameplayInput;

            // ---- 시점 ----
            if (input)
            {
                float sens = GameSettings.MouseSensitivity * LookMultiplier;
                bool assist = GameInput.Mobile && GameSettings.AimAssist;
                Vector3 aimPoint = Vector3.zero;
                Monster assistTarget = assist ? FindAssistTarget(out aimPoint) : null;
                if (assistTarget != null) sens *= 0.6f; // 망자 위에서는 시점이 '끈적'하게 (지나치기 어렵게)
                yaw += GameInput.LookX * sens;
                pitch -= GameInput.LookY * sens;
                if (assistTarget != null)
                {
                    // 조준 보조: 조준점이 망자 가슴 쪽으로 살짝 끌린다 (쏘는 중엔 더 강하게)
                    var look = Quaternion.LookRotation(aimPoint - cam.transform.position);
                    float k = (GameInput.Fire ? 5f : 2.2f) * dt;
                    yaw += Mathf.DeltaAngle(yaw, look.eulerAngles.y) * k;
                    pitch += Mathf.DeltaAngle(pitch, look.eulerAngles.x) * k;
                }
                pitch = Mathf.Clamp(pitch, -85f, 85f);
            }
            float jitter = player.Infection.AimJitter;
            float jx = jitter > 0f ? (Mathf.PerlinNoise(Time.time * 3f, 0f) * 2f - 1f) * jitter : 0f;
            float jy = jitter > 0f ? (Mathf.PerlinNoise(0f, Time.time * 3f) * 2f - 1f) * jitter : 0f;

            // 시점 반동 스프링
            punchVel -= punch * 260f * dt;
            punchVel *= Mathf.Exp(-14f * dt);
            punch += punchVel * dt;

            // 화면 흔들림 (사격·피격): 짧은 롤 + 미세 피치
            shake *= Mathf.Exp(-12f * dt);
            float roll = (Mathf.PerlinNoise(Time.time * 40f, 7f) * 2f - 1f) * shake;
            float shakePitch = (Mathf.PerlinNoise(3f, Time.time * 40f) * 2f - 1f) * shake * 0.5f;

            transform.rotation = Quaternion.Euler(0f, yaw + recoil.x + jx, 0f);
            cam.transform.localRotation = Quaternion.Euler(Mathf.Clamp(pitch - recoil.y - punch.y + jy + shakePitch, -89f, 89f), punch.x, roll);

            // ---- 이동 ----
            WeightPenalty = player.Stats.WeightPenalty(player.Weapons.TotalWeight);
            Vector3 wish = Vector3.zero;
            if (input)
            {
                wish = transform.right * GameInput.MoveX + transform.forward * GameInput.MoveY;
                if (wish.sqrMagnitude > 1f) wish.Normalize();
            }
            if (sprintBlock > 0f) sprintBlock -= dt;
            bool forward = input && GameInput.MoveY > 0.1f;
            // 앉기: 머리 위가 막혀 있으면 일어서지 못한다. 달리면 일어선다.
            bool wantCrouch = input && GameInput.Crouch && !(forward && GameInput.Sprint && !Exhausted);
            if (wantCrouch) IsCrouching = true;
            else if (IsCrouching && !Physics.SphereCast(transform.position + Vector3.up * 0.6f, cc.radius * 0.9f, Vector3.up, out _, 1.15f, ~((1 << 2) | (1 << 1)), QueryTriggerInteraction.Ignore))
                IsCrouching = false;
            CrouchBlend = Mathf.MoveTowards(CrouchBlend, IsCrouching ? 1f : 0f, dt * 6f);
            float h = Mathf.Lerp(1.8f, 1.2f, CrouchBlend);
            cc.height = h;
            cc.center = new Vector3(0f, h * 0.5f, 0f);
            var cp = cam.transform.localPosition;
            cam.transform.localPosition = new Vector3(cp.x, Mathf.Lerp(EyeHeight, CrouchEyeHeight, CrouchBlend), cp.z);

            bool sprint = forward && GameInput.Sprint && !IsCrouching && !player.Weapons.IsAiming && sprintBlock <= 0f && !Exhausted && !player.IsUsingItem;
            if (BoostTime > 0f) BoostTime -= dt;
            if (sprint) UseStamina(16f * dt);
            else
            {
                if (staminaRestTimer > 0f) staminaRestTimer -= dt;
                else Stamina = Mathf.Min(MaxStamina, Stamina + (22f + player.Stats.Vitality * 0.4f) * (BoostTime > 0f ? 2f : 1f) * dt);
            }
            if (Exhausted && Stamina >= 25f) Exhausted = false;
            IsSprinting = sprint;
            float speed = player.Stats.MoveSpeed * WeightPenalty * player.Infection.MoveMultiplier * MoveMultiplier * (sprint ? 1.45f : 1f)
                * player.Armor.MoveMult * (player.IsUsingItem ? 0.45f : 1f) * (Exhausted ? 0.85f : 1f) * GameRules.MoveMult * Mathf.Lerp(1f, 0.5f, CrouchBlend);

            if (cc.isGrounded)
            {
                // 착지: 떨어지던 속도만큼 쿵 소리 + 시선이 살짝 꺼졌다 올라옴 (발소리처럼 망자에게도 들린다)
                if (!wasGrounded && verticalVel < -3.5f)
                {
                    float k = Mathf.Clamp01((-verticalVel - 3.5f) / 6f);
                    SFX.Footstep("mud", 0.35f + 0.4f * k);
                    AddPunch(-1.2f - 2.5f * k, 0f);
                    Noise.Emit(transform.position, 6f + 6f * k, true);
                }
                verticalVel = -1f;
                if (input && GameInput.JumpDown && !IsCrouching && UseStamina(10f)) verticalVel = jumpSpeed;
            }
            else
            {
                verticalVel += Physics.gravity.y * dt;
            }

            wasGrounded = cc.isGrounded;
            cc.Move((wish * speed + Vector3.up * verticalVel + knock) * dt);
            knock *= Mathf.Exp(-5f * dt);

            IsMoving = wish.sqrMagnitude > 0.01f || !cc.isGrounded;

            // 발소리: 달리면 10m, 걸으면 4m (비가 오면 덜 들린다)
            if (cc.isGrounded && wish.sqrMagnitude > 0.01f)
            {
                stepTimer -= dt;
                if (stepTimer <= 0f)
                {
                    stepTimer = sprint ? 0.33f : IsCrouching ? 0.7f : 0.5f;
                    Noise.Emit(transform.position, sprint ? 10f : IsCrouching ? 1.5f : 4f, true); // 앉아 걸으면 거의 안 들린다
                    // 내 귀에 들리는 발소리: 툇마루·기단 위는 나무, 큰길은 돌, 나머지는 젖은 흙
                    string surf = "mud";
                    Vector3 fp = transform.position;
                    if (Physics.Raycast(fp + Vector3.up * 0.5f, Vector3.down, out var gh, 1.2f, ~((1 << 2) | (1 << 1)), QueryTriggerInteraction.Ignore) && gh.point.y > 0.12f)
                    {
                        // 밟은 물건의 재질 이름으로: 나무판 → 나무, 돌·시멘트·쇠 → 돌, 그 밖(논두렁 풀 등) → 흙
                        var gr = gh.collider.GetComponent<Renderer>();
                        string mn = gr != null && gr.sharedMaterial != null ? gr.sharedMaterial.name : "";
                        surf = mn.Contains("Wood") ? "wood" : (mn.Contains("Stone") || mn.Contains("Concrete") || mn.Contains("Rock") || mn.Contains("Metal") || mn.Contains("Road")) ? "stone" : "mud";
                    }
                    else if (Mathf.Abs(fp.x - VillageBuilder.RoadX(fp.z)) < 3f) surf = "stone";
                    SFX.Footstep(surf, sprint ? 0.45f : IsCrouching ? 0.12f : 0.28f);
                }
            }
            else stepTimer = 0f;
            StationaryTime = IsMoving ? 0f : StationaryTime + dt;
        }
    }
}

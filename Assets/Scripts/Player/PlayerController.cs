using Hunt.Core;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>1인칭 이동 + 시점 + 반동 적용.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public const float EyeHeight = 1.65f;
        public const float BaseFov = 75f;

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
        float shake, sprintBlock, stepTimer;

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

        public void AddRecoil(Vector2 kick) { recoil += kick; }

        /// <summary>화면 흔들림 추가 (도 단위).</summary>
        public void AddShake(float degrees) { shake = Mathf.Min(shake + degrees, 4f); }

        /// <summary>사격 시 달리기를 잠시 끊는다.</summary>
        public void CancelSprint() { sprintBlock = 0.3f; IsSprinting = false; }

        public void RecoverRecoil(float dt, float rate)
        {
            recoil = Vector2.MoveTowards(recoil, Vector2.zero, rate * dt * (1f + recoil.magnitude * 0.5f));
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsWorldRunning || player == null || player.IsDead) { IsSprinting = false; return; }
            float dt = Time.deltaTime;
            bool input = gm.AcceptsGameplayInput;

            // ---- 시점 ----
            if (input)
            {
                float sens = GameSettings.MouseSensitivity * LookMultiplier;
                yaw += Input.GetAxis("Mouse X") * sens;
                pitch -= Input.GetAxis("Mouse Y") * sens;
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
                wish = transform.right * Input.GetAxisRaw("Horizontal") + transform.forward * Input.GetAxisRaw("Vertical");
                if (wish.sqrMagnitude > 1f) wish.Normalize();
            }
            if (sprintBlock > 0f) sprintBlock -= dt;
            bool forward = input && Input.GetAxisRaw("Vertical") > 0.1f;
            bool sprint = forward && Input.GetKey(KeyCode.LeftShift) && !player.Weapons.IsAiming && sprintBlock <= 0f && !Exhausted && !player.IsUsingItem;
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
                * player.Armor.MoveMult * (player.IsUsingItem ? 0.45f : 1f) * (Exhausted ? 0.85f : 1f) * GameRules.MoveMult;

            if (cc.isGrounded)
            {
                verticalVel = -1f;
                if (input && Input.GetKeyDown(KeyCode.Space) && UseStamina(10f)) verticalVel = jumpSpeed;
            }
            else
            {
                verticalVel += Physics.gravity.y * dt;
            }

            cc.Move((wish * speed + Vector3.up * verticalVel) * dt);

            IsMoving = wish.sqrMagnitude > 0.01f || !cc.isGrounded;

            // 발소리: 달리면 10m, 걸으면 4m (비가 오면 덜 들린다)
            if (cc.isGrounded && wish.sqrMagnitude > 0.01f)
            {
                stepTimer -= dt;
                if (stepTimer <= 0f)
                {
                    stepTimer = sprint ? 0.33f : 0.5f;
                    Noise.Emit(transform.position, sprint ? 10f : 4f, true);
                }
            }
            else stepTimer = 0f;
            StationaryTime = IsMoving ? 0f : StationaryTime + dt;
        }
    }
}

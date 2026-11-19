using Hunt.Core;
using Hunt.UI;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>
    /// B: 감염 게이지 — 괴물의 힘을 빌린다.
    /// 피격 시 게이지가 쌓이고, 게이지를 소모해 변이 능력을 쓴다.
    /// 게이지가 높을수록 데미지가 오르지만(최대 +20%), 100%가 되면 "폭주" 상태가 된다.
    /// </summary>
    public class InfectionSystem : MonoBehaviour
    {
        public const float Max = 100f;
        public const float RageCost = 30f;
        public const float RegenCost = 25f;
        const float RageDuration = 6f;
        const float RegenDuration = 4f;
        const float RegenTotal = 50f;
        const float FrenzyDuration = 7f;

        public float Gauge { get; private set; }
        public bool IsFrenzy => frenzyTime > 0f;
        public bool IsRage => rageTime > 0f;
        public bool IsRegen => regenTime > 0f;
        public float FrenzyTime => frenzyTime;
        public float RageTime => rageTime;
        public int Antidotes { get; set; } = 1;

        float frenzyTime, rageTime, regenTime, sinceHit;
        PlayerCharacter player;

        public void Init(PlayerCharacter p) { player = p; }

        public float DamageMultiplier => (1f + Gauge / Max * 0.2f) * (IsRage ? 1.3f : 1f);
        public float MoveMultiplier => IsRage ? 1.2f : 1f;
        public float KnockbackMultiplier => IsRage ? 1.5f : 1f;
        /// <summary>폭주 중 조준 흔들림(도).</summary>
        public float AimJitter => IsFrenzy ? 2.5f : 0f;
        /// <summary>폭주 중 받는 피해 감소 (괴물에 가까워진 몸).</summary>
        public float DamageTakenMultiplier => IsFrenzy ? 0.6f : 1f;

        public float RageCostFinal => RageCost * player.Stats.AbilityCostMult;
        public float RegenCostFinal => RegenCost * player.Stats.AbilityCostMult;

        public void AddInfection(float amount)
        {
            if (IsFrenzy) return;
            Gauge = Mathf.Min(Max, Gauge + amount * player.Stats.InfectionResistMult * player.Armor.InfectionMult);
            sinceHit = 0f;
            if (Gauge >= Max) StartFrenzy();
        }

        void StartFrenzy()
        {
            frenzyTime = FrenzyDuration;
            rageTime = 0f;
            HUD.Notify("역병이 온몸을 덮쳤다 — 폭주! (H: 청심환)", new Color(0.8f, 0.2f, 1f));
        }

        void EndFrenzy(bool cured)
        {
            frenzyTime = 0f;
            Gauge = cured ? 20f : 45f;
            HUD.Notify(cured ? "청심환을 삼켰다 — 정신이 돌아온다" : "폭주가 가라앉았다", new Color(0.7f, 0.9f, 1f));
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsWorldRunning || player == null || player.IsDead) return;
            float dt = Time.deltaTime;

            if (gm.AcceptsGameplayInput)
            {
                if (GameInput.RageDown) TryRage();
                if (GameInput.RegenDown) TryRegen();
                if (GameInput.AntidoteDown) TryAntidote();
            }

            if (frenzyTime > 0f)
            {
                frenzyTime -= dt;
                if (frenzyTime <= 0f) EndFrenzy(false);
            }
            if (rageTime > 0f) rageTime -= dt;
            if (regenTime > 0f)
            {
                regenTime -= dt;
                player.Heal(RegenTotal / RegenDuration * dt);
            }

            // 오래 맞지 않으면 아주 천천히 자연 정화
            sinceHit += dt;
            if (!IsFrenzy && sinceHit > 6f) Gauge = Mathf.Max(0f, Gauge - 1.5f * dt);
        }

        void TryRage()
        {
            if (IsFrenzy || IsRage) return;
            if (Gauge < RageCostFinal) { HUD.Notify("역병 기운이 모자라다", Color.gray); return; }
            Gauge -= RageCostFinal;
            rageTime = RageDuration * player.Stats.AbilityDurationMult;
            HUD.Notify("광폭화 — 피가 끓는다 (피해 +30% · 이동 +20% · 저지력 +50%)", new Color(1f, 0.4f, 0.3f));
        }

        void TryRegen()
        {
            if (IsFrenzy || IsRegen) return;
            if (Gauge < RegenCostFinal) { HUD.Notify("역병 기운이 모자라다", Color.gray); return; }
            Gauge -= RegenCostFinal;
            regenTime = RegenDuration * player.Stats.AbilityDurationMult;
            HUD.Notify("재생 — 상처가 기어 붙는다", new Color(0.4f, 1f, 0.5f));
        }

        void TryAntidote()
        {
            if (Antidotes <= 0) { HUD.Notify("청심환이 없다", Color.gray); return; }
            if (!IsFrenzy && Gauge < 30f) return;
            Antidotes--;
            if (IsFrenzy) EndFrenzy(true);
            else Gauge = Mathf.Max(0f, Gauge - 40f);
        }

        public void ResetForNewGame()
        {
            Gauge = 0f; frenzyTime = rageTime = regenTime = 0f; Antidotes = 1;
        }
    }
}

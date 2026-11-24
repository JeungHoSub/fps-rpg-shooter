using System.Collections.Generic;
using Hunt.Core;
using Hunt.Data;
using Hunt.Evolution;
using Hunt.Monsters;
using Hunt.Stats;
using Hunt.UI;
using Hunt.Weapons;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>플레이어의 RPG 상태: 스탯, HP, 레벨/경험치, 골드, 파츠 인벤토리.</summary>
    public class PlayerCharacter : MonoBehaviour
    {
        public CharacterStats Stats { get; private set; }
        public float Hp { get; private set; }
        public bool IsDead { get; private set; }

        public int Level => Stats.Level;
        public int Xp { get; private set; }
        public int XpToNext => 60 + Level * 40;
        public int StatPoints { get; private set; }
        public int Gold { get; private set; }
        public int Kills { get; private set; }
        /// <summary>지금까지 받은 피해 합계 · 가장 긴 연속 처치 (새벽 기록 카드용).</summary>
        public float DamageTaken { get; private set; }
        public int BestStreak { get; set; }
        /// <summary>마지막으로 나를 문 망자 이름 (사망 화면용).</summary>
        public string LastAttacker { get; set; }
        public float HurtFlash { get; private set; }

        public readonly List<WeaponPart> Inventory = new List<WeaponPart>();
        /// <summary>무기고: 가진 모든 총 (장착 중인 것 포함). 계열마다 키워 두어야 망자 방어도를 넘는다.</summary>
        public readonly List<WeaponInstance> Arsenal = new List<WeaponInstance>();

        public WeaponInstance Owned(WeaponData d)
        {
            foreach (var w in Arsenal) if (w.data == d) return w;
            return null;
        }

        public PlayerController Controller { get; private set; }
        public PlayerWeapons Weapons { get; private set; }
        public InfectionSystem Infection { get; private set; }
        /// <summary>방어구 5부위.</summary>
        public ArmorSet Armor { get; private set; } = new ArmorSet();
        public float MaxHp => Stats.MaxHp * GameRules.HpMult + Armor.BonusHp;

        // 특성 선택 (3레벨마다 1회)
        public int PendingTraitPicks { get; private set; }
        public readonly List<Trait> TraitOffer = new List<Trait>();

        public void Init(CharacterData data, int[] startAllocation, Trait startTrait, List<WeaponData> loadout, Camera cam)
        {
            Stats = new CharacterStats(data);
            foreach (var t in CharacterStats.All)
                for (int i = 0; i < startAllocation[(int)t]; i++) Stats.Allocate(t);
            Stats.traits.Add(startTrait);
            Hp = MaxHp;

            Infection = gameObject.AddComponent<InfectionSystem>();
            Infection.Init(this);
            Controller = gameObject.AddComponent<PlayerController>();
            Controller.Init(this, cam);
            Weapons = gameObject.AddComponent<PlayerWeapons>();
            Weapons.Init(this, cam, loadout);
            Arsenal.AddRange(Weapons.Slots);
        }

        // ---- 소비 아이템: X 치료 키트 (2.5초 동안 감는다), C 스태미너 회복제 ----
        public const int MaxKits = 3, MaxDrinks = 3;
        public const float KitTime = 2.5f;
        public int Kits { get; set; } = 2;
        public int Drinks { get; set; } = 2;
        public bool IsUsingItem => kitTime > 0f;
        public float KitProgress => kitTime > 0f ? 1f - kitTime / KitTime : 0f;
        float kitTime;

        /// <summary>치료 키트 회복량: 최대 체력의 45% + 25.</summary>
        public float KitHeal => (MaxHp * 0.45f + 25f) * (Stats.Has(Trait.Herbalist) ? 1.4f : 1f);

        void Update()
        {
            TickFeedback(Time.deltaTime);
            if (HurtFlash > 0f) HurtFlash -= Time.deltaTime;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsWorldRunning || IsDead) return;

            if (kitTime > 0f)
            {
                kitTime -= Time.deltaTime;
                if (kitTime <= 0f)
                {
                    Heal(KitHeal);
                    HUD.Notify($"상처를 감쌌다 — 생명 +{KitHeal:0}", new Color(0.6f, 1f, 0.6f));
                }
            }
            if (!gm.AcceptsGameplayInput) return;
            if (GameInput.KitDown) UseKit();
            if (GameInput.DrinkDown) UseDrink();
        }

        void UseKit()
        {
            if (IsUsingItem) return;
            if (Kits <= 0) { HUD.Notify("치료 키트가 없다", new Color(1f, 0.6f, 0.4f)); return; }
            if (Hp >= MaxHp - 0.5f) { HUD.Notify("다친 곳이 없다", UITheme.BoneDim); return; }
            Kits--;
            kitTime = KitTime;
            SFX.Play(SFX.Bandage, 0.8f);
        }

        /// <summary>맞거나 무기를 쏘면 치료가 끊긴다 (키트는 돌려받지 못한다).</summary>
        public void InterruptItem()
        {
            if (kitTime <= 0f) return;
            kitTime = 0f;
            HUD.Notify("치료가 끊겼다", new Color(1f, 0.5f, 0.4f));
        }

        void UseDrink()
        {
            if (Drinks <= 0) { HUD.Notify("스태미너 회복제가 없다", new Color(1f, 0.6f, 0.4f)); return; }
            Drinks--;
            Controller.RestoreStamina(15f);
            SFX.Play(SFX.Drink, 0.8f);
            HUD.Notify("기운이 돈다 — 15초 동안 덜 지친다", new Color(0.6f, 0.9f, 1f));
        }

        /// <summary>밤을 넘기면 보급: 키트·회복제 1개씩, 특수 기기 채움.</summary>
        public void Resupply()
        {
            Kits = Mathf.Min(MaxKits, Kits + 1);
            Drinks = Mathf.Min(MaxDrinks, Drinks + 1);
            Weapons.ResupplyGadgets();
        }

        /// <summary>최근 맞은 방향 (월드 위치, 남은 표시 시간) — HUD 피격 방향 표시용.</summary>
        public readonly System.Collections.Generic.List<(Vector3 from, float time)> DamageFrom = new System.Collections.Generic.List<(Vector3, float)>();
        public int KillStreak { get; private set; }
        public float StreakTime { get; private set; }
        float streakWindow;

        public void TakeDamage(float dmg, float infection, Vector3 from)
        {
            if (IsDead) return;
            DamageFrom.Add((from, 1.2f));
            if (DamageFrom.Count > 6) DamageFrom.RemoveAt(0);
#if UNITY_ANDROID || UNITY_IOS
            if (dmg >= 12f && Application.isMobilePlatform) Handheld.Vibrate(); // 세게 맞으면 진동
#endif
            TakeDamage(dmg, infection);
        }

        public void TakeDamage(float dmg, float infection)
        {
            if (IsDead) return;
            float taken = dmg * Infection.DamageTakenMultiplier * Armor.DamageTakenMult * Stats.DamageTakenMult;
            Hp -= taken;
            DamageTaken += Mathf.Max(0f, taken);
            if (taken > MaxHp * 0.04f) GameHud.ScreenBlood(taken / Mathf.Max(1f, MaxHp)); // 세게 물리면 화면에 피가 튄다
            if (dmg > 3f) InterruptItem();
            HurtFlash = 0.25f;
            SFX.Play(SFX.PlayerHurt, 0.8f);
            Controller.AddShake(Mathf.Clamp(dmg * 0.08f, 0.5f, 3f));
            Infection.AddInfection(infection);
            if (Hp <= 0f)
            {
                Hp = 0f;
                IsDead = true;
                if (GameManager.Instance != null) GameManager.Instance.OnPlayerDied();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Hp = Mathf.Min(MaxHp, Hp + amount);
        }

        float heartTimer;

        void TickFeedback(float dt)
        {
            if (streakWindow > 0f) { streakWindow -= dt; if (streakWindow <= 0f) KillStreak = 0; }
            StreakTime += dt;
            for (int i = DamageFrom.Count - 1; i >= 0; i--)
            {
                var d = DamageFrom[i];
                d.time -= dt;
                if (d.time <= 0f) DamageFrom.RemoveAt(i); else DamageFrom[i] = d;
            }
            // 체력이 35% 아래면 심장 박동 (낮을수록 빠르게)
            float ratio = Stats != null && Stats.MaxHp > 0f ? Hp / Stats.MaxHp : 1f;
            if (!IsDead && ratio < 0.35f)
            {
                heartTimer -= dt;
                if (heartTimer <= 0f)
                {
                    SFX.Play(SFX.Heartbeat, Mathf.Lerp(0.9f, 0.45f, ratio / 0.35f), 0f);
                    heartTimer = Mathf.Lerp(0.55f, 1.1f, ratio / 0.35f);
                }
            }
        }

        public void OnKill(Monster m)
        {
            // 연속 처치: 2.5초 안에 다음 처치가 이어지면 콤보
            KillStreak = streakWindow > 0f ? KillStreak + 1 : 1;
            streakWindow = 2.5f;
            StreakTime = 0f;
            if (KillStreak >= 2) SFX.PlayPitched(SFX.KillTick, 0.7f, 1f + Mathf.Min(KillStreak - 1, 8) * 0.08f);
            Kills++;
            Achievements.Add("kills1000");
            if (KillStreak >= 10) Achievements.Unlock("streak10");
            if (KillStreak > BestStreak) BestStreak = KillStreak;
            var evo = EvolutionDirector.Instance != null ? EvolutionDirector.Instance.Profile : null;
            float reward = evo != null ? evo.RewardMultiplier : 1f;
            AddXp(Mathf.RoundToInt(m.Data.xp * reward));
            bool scavenger = Stats.Has(Trait.Scavenger);
            AddGold(Mathf.RoundToInt(m.Data.gold * reward * (scavenger ? 1.3f : 1f)));

            if (Stats.Has(Trait.Bloodlust)) Heal(4f);

            // 연속 처치 보상: 5연속마다 엽전 보너스, 10연속마다 치유 부적이 발밑에 떨어진다 (마구 쏘는 맛)
            if (KillStreak >= 5 && KillStreak % 5 == 0)
            {
                int bonus = 4 * KillStreak;
                AddGold(bonus);
                GameHud.OnPickup(HudIcons.Icon.Flame, new Color(1f, 0.8f, 0.4f), $"{KillStreak}연속 처치  엽전 +{bonus}");
                if (KillStreak % 10 == 0) Pickup.Spawn(PickupKind.Rune, transform.position + transform.forward * 1.2f, m.transform.parent);
            }

            // 보급품 드롭: 치료 키트 · 스태미너 회복제 · 탄창
            Pickup.RollDrops(m);

            // D: 파츠 드랍. 진화 세대가 높을수록 희귀 파츠 확률 증가.
            float luck = evo != null ? evo.PartLuck : 0f;
            if (Random.value < m.Data.partDropChance * (1f + luck) * (scavenger ? 1.4f : 1f))
            {
                Rarity? forced = m.IsBoss ? Rarity.Legendary : (Rarity?)null;
                var part = PartGenerator.Generate(Level + (m.Generation), luck, forced);
                Inventory.Add(part);
                HUD.Notify($"부품을 주웠다 — {part.Title}", WeaponPart.RarityColor(part.rarity));
            }
        }

        public void AddGold(int g) { Gold += g; }

        public bool SpendGold(int g)
        {
            if (Gold < g) return false;
            Gold -= g;
            return true;
        }

        public void AddXp(int xp)
        {
            Xp += xp;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                float hpRatio = Hp / MaxHp;
                Stats.SetLevel(Level + 1);
                StatPoints += 3;
                Hp = Mathf.Min(MaxHp, MaxHp * hpRatio + MaxHp * 0.2f);
                SFX.Play(SFX.LevelUp, 0.7f, 0f);
                GameHud.LevelUpAt = Time.unscaledTime; // 화면 연출
                HUD.Notify($"더 강해졌다 — Lv.{Level}  ·  단련 +3 (서낭당에서)", new Color(1f, 0.9f, 0.3f));
                if (Level % Traits.PickEveryLevels == 0)
                {
                    PendingTraitPicks++;
                    HUD.Notify("새 기질을 얻을 수 있다 (서낭당)", new Color(0.5f, 0.9f, 1f));
                }
            }
        }

        /// <summary>선택 대기 중이면 후보 3개를 준비해 둔다.</summary>
        public void EnsureTraitOffer()
        {
            if (PendingTraitPicks > 0 && TraitOffer.Count == 0)
                TraitOffer.AddRange(Traits.Offer(Stats.traits, 3));
        }

        public void PickTrait(Trait t)
        {
            if (PendingTraitPicks <= 0 || !TraitOffer.Contains(t)) return;
            float hpRatio = Hp / MaxHp;
            Stats.traits.Add(t);
            Hp = MaxHp * hpRatio;
            PendingTraitPicks--;
            TraitOffer.Clear();
            HUD.Notify($"새 기질 — {Traits.Name(t)}", new Color(0.5f, 0.9f, 1f));
        }

        /// <summary>남은 단련을 한 능력치에 전부 넣는다.</summary>
        public void SpendAllStatPoints(StatType t)
        {
            while (StatPoints > 0) SpendStatPoint(t);
        }

        public void SpendStatPoint(StatType t)
        {
            if (StatPoints <= 0) return;
            float hpRatio = Hp / MaxHp;
            StatPoints--;
            Stats.Allocate(t);
            Hp = MaxHp * hpRatio;
        }
    }
}

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
        public float HurtFlash { get; private set; }

        public readonly List<WeaponPart> Inventory = new List<WeaponPart>();

        public PlayerController Controller { get; private set; }
        public PlayerWeapons Weapons { get; private set; }
        public InfectionSystem Infection { get; private set; }

        // 특성 선택 (3레벨마다 1회)
        public int PendingTraitPicks { get; private set; }
        public readonly List<Trait> TraitOffer = new List<Trait>();

        public void Init(CharacterData data, int[] startAllocation, Trait startTrait, List<WeaponData> loadout, Camera cam)
        {
            Stats = new CharacterStats(data);
            foreach (var t in CharacterStats.All)
                for (int i = 0; i < startAllocation[(int)t]; i++) Stats.Allocate(t);
            Stats.traits.Add(startTrait);
            Hp = Stats.MaxHp;

            Infection = gameObject.AddComponent<InfectionSystem>();
            Infection.Init(this);
            Controller = gameObject.AddComponent<PlayerController>();
            Controller.Init(this, cam);
            Weapons = gameObject.AddComponent<PlayerWeapons>();
            Weapons.Init(this, cam, loadout);
        }

        void Update()
        {
            if (HurtFlash > 0f) HurtFlash -= Time.deltaTime;
        }

        public void TakeDamage(float dmg, float infection)
        {
            if (IsDead) return;
            Hp -= dmg * Infection.DamageTakenMultiplier;
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
            Hp = Mathf.Min(Stats.MaxHp, Hp + amount);
        }

        public void OnKill(Monster m)
        {
            Kills++;
            var evo = EvolutionDirector.Instance != null ? EvolutionDirector.Instance.Profile : null;
            float reward = evo != null ? evo.RewardMultiplier : 1f;
            AddXp(Mathf.RoundToInt(m.Data.xp * reward));
            bool scavenger = Stats.Has(Trait.Scavenger);
            AddGold(Mathf.RoundToInt(m.Data.gold * reward * (scavenger ? 1.3f : 1f)));

            if (Stats.Has(Trait.Bloodlust)) Heal(4f);

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
                float hpRatio = Hp / Stats.MaxHp;
                Stats.SetLevel(Level + 1);
                StatPoints += 3;
                Hp = Mathf.Min(Stats.MaxHp, Stats.MaxHp * hpRatio + Stats.MaxHp * 0.2f);
                SFX.Play(SFX.LevelUp, 0.7f, 0f);
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
            float hpRatio = Hp / Stats.MaxHp;
            Stats.traits.Add(t);
            Hp = Stats.MaxHp * hpRatio;
            PendingTraitPicks--;
            TraitOffer.Clear();
            HUD.Notify($"새 기질 — {Traits.Name(t)}", new Color(0.5f, 0.9f, 1f));
        }

        public void SpendStatPoint(StatType t)
        {
            if (StatPoints <= 0) return;
            float hpRatio = Hp / Stats.MaxHp;
            StatPoints--;
            Stats.Allocate(t);
            Hp = Stats.MaxHp * hpRatio;
        }
    }
}

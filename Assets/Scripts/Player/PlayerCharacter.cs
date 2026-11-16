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

        public void Init(CharacterData data, List<WeaponData> loadout, Camera cam)
        {
            Stats = new CharacterStats(data);
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
            AddGold(Mathf.RoundToInt(m.Data.gold * reward));

            if (Stats.data.passive == CharacterPassive.BruiserBloodlust) Heal(4f);

            // D: 파츠 드랍. 진화 세대가 높을수록 희귀 파츠 확률 증가.
            float luck = evo != null ? evo.PartLuck : 0f;
            if (Random.value < m.Data.partDropChance * (1f + luck))
            {
                Rarity? forced = m.IsBoss ? Rarity.Legendary : (Rarity?)null;
                var part = PartGenerator.Generate(Level + (m.Generation), luck, forced);
                Inventory.Add(part);
                HUD.Notify($"파츠 획득: {part.Title}", WeaponPart.RarityColor(part.rarity));
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
                HUD.Notify($"레벨 업! Lv.{Level} — 스탯 포인트 +3 (웨이브 사이 [Tab] 메뉴에서 분배)", new Color(1f, 0.9f, 0.3f));
            }
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

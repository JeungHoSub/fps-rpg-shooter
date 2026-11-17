using System.Collections.Generic;
using Hunt.Combat;
using Hunt.Stats;
using UnityEngine;

namespace Hunt.Data
{
    /// <summary>
    /// 코드로 정의한 기본 콘텐츠. 에디터에서 ScriptableObject 에셋(Create > Hunt > ...)을 만들어
    /// 교체할 수 있지만, 프로토타입은 이 값만으로 바로 플레이 가능하다.
    /// </summary>
    public static class DefaultContent
    {
        static List<WeaponData> weapons;
        static Dictionary<MonsterArchetype, MonsterData> monsters;

        public static List<WeaponData> Weapons { get { if (weapons == null) BuildWeapons(); return weapons; } }
        public static WeaponData Pistol => Weapons[0];

        public static MonsterData Monster(MonsterArchetype a)
        {
            if (monsters == null) BuildMonsters();
            return monsters[a];
        }

        // ------------------------------------------------------------------ 주인공
        static CharacterData hunter;

        /// <summary>단일 주인공. 모든 스탯이 균등하게 시작하고, 플레이어의 투자로 성격이 정해진다.</summary>
        public static CharacterData Hunter
        {
            get
            {
                if (hunter == null)
                {
                    hunter = ScriptableObject.CreateInstance<CharacterData>();
                    hunter.id = "hunter"; hunter.name = "hunter"; hunter.displayName = "헌터";
                    hunter.description = "홀로 남은 생존자. 괴물을 사냥하며 강해진다.";
                    hunter.baseStats = new StatBlock(8, 8, 8, 8, 8, 8);
                    hunter.growthPerLevel = new StatBlock(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f);
                    hunter.startingStatPoints = 6;
                }
                return hunter;
            }
        }

        // ------------------------------------------------------------------ 무기
        static void BuildWeapons()
        {
            weapons = new List<WeaponData>();

            var pistol = Weapon("pistol", "P9 권총", WeaponClass.Pistol, DamageType.Physical, new Color(0.35f, 0.35f, 0.4f));
            pistol.damage = 28; pistol.rpm = 400; pistol.automatic = false; pistol.magazine = 12; pistol.reserveAmmo = 999;
            pistol.reloadTime = 1.3f; pistol.emptyReloadTime = 1.6f; pistol.recoilVertical = 1.2f; pistol.recoilHorizontal = 0.3f;
            pistol.spreadHip = 1.8f; pistol.spreadAds = 0.4f; pistol.knockback = 0.8f; pistol.weight = 1.5f; pistol.adsFov = 62;
            pistol.falloffStart = 15; pistol.falloffEnd = 40; pistol.recoilSeed = 2.1f;
            weapons.Add(pistol);

            var ar = Weapon("ar", "K-7 돌격소총", WeaponClass.Rifle, DamageType.Physical, new Color(0.3f, 0.32f, 0.25f));
            ar.damage = 34; ar.rpm = 650; ar.magazine = 30; ar.reserveAmmo = 240; ar.reloadTime = 2.0f; ar.emptyReloadTime = 2.5f;
            ar.recoilVertical = 0.9f; ar.recoilHorizontal = 0.45f; ar.spreadHip = 3f; ar.spreadAds = 0.35f;
            ar.knockback = 1.2f; ar.weight = 4f; ar.penetration = 0.25f; ar.falloffStart = 30; ar.falloffEnd = 70; ar.recoilSeed = 4.7f;
            weapons.Add(ar);

            var smg = Weapon("smg", "볼트 SMG", WeaponClass.SMG, DamageType.Electric, new Color(0.25f, 0.3f, 0.5f));
            smg.damage = 20; smg.rpm = 900; smg.magazine = 35; smg.reserveAmmo = 280; smg.reloadTime = 1.6f; smg.emptyReloadTime = 2.0f;
            smg.recoilVertical = 0.55f; smg.recoilHorizontal = 0.5f; smg.spreadHip = 2.5f; smg.spreadAds = 0.8f; smg.spreadMovePenalty = 1f;
            smg.knockback = 0.6f; smg.weight = 2.5f; smg.falloffStart = 12; smg.falloffEnd = 35; smg.adsFov = 60; smg.recoilSeed = 8.3f;
            weapons.Add(smg);

            var sg = Weapon("shotgun", "M3 샷건", WeaponClass.Shotgun, DamageType.Physical, new Color(0.4f, 0.25f, 0.15f));
            sg.damage = 15; sg.pellets = 9; sg.rpm = 75; sg.automatic = false; sg.magazine = 8; sg.reserveAmmo = 64;
            sg.reloadTime = 2.8f; sg.emptyReloadTime = 3.2f; sg.recoilVertical = 4f; sg.recoilHorizontal = 0.8f;
            sg.spreadHip = 6f; sg.spreadAds = 4.5f; sg.knockback = 9f; sg.weight = 5f; sg.falloffStart = 6; sg.falloffEnd = 20;
            sg.minFalloff = 0.2f; sg.adsFov = 65; sg.recoilSeed = 1.3f;
            weapons.Add(sg);

            var sniper = Weapon("sniper", "AWR 저격총", WeaponClass.Sniper, DamageType.Physical, new Color(0.2f, 0.25f, 0.2f));
            sniper.damage = 160; sniper.rpm = 45; sniper.automatic = false; sniper.magazine = 5; sniper.reserveAmmo = 40;
            sniper.reloadTime = 3f; sniper.emptyReloadTime = 3.6f; sniper.recoilVertical = 6f; sniper.recoilHorizontal = 0.5f;
            sniper.spreadHip = 8f; sniper.spreadAds = 0.02f; sniper.knockback = 4f; sniper.weight = 6f; sniper.penetration = 0.8f;
            sniper.falloffStart = 80; sniper.falloffEnd = 200; sniper.adsFov = 20; sniper.adsTime = 0.4f; sniper.recoilSeed = 6.6f;
            weapons.Add(sniper);

            var lmg = Weapon("lmg", "인페르노 경기관총", WeaponClass.LMG, DamageType.Fire, new Color(0.45f, 0.2f, 0.1f));
            lmg.damage = 27; lmg.rpm = 750; lmg.magazine = 100; lmg.reserveAmmo = 300; lmg.reloadTime = 4.5f; lmg.emptyReloadTime = 5.2f;
            lmg.recoilVertical = 0.8f; lmg.recoilHorizontal = 0.7f; lmg.spreadHip = 4f; lmg.spreadAds = 1f; lmg.spreadMovePenalty = 4f;
            lmg.knockback = 2f; lmg.weight = 9f; lmg.penetration = 0.3f; lmg.adsTime = 0.45f; lmg.recoilSeed = 3.9f;
            weapons.Add(lmg);
        }

        static WeaponData Weapon(string id, string name, WeaponClass cls, DamageType type, Color color)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.id = id; w.name = id; w.displayName = name; w.weaponClass = cls; w.damageType = type; w.viewColor = color;
            return w;
        }

        // ------------------------------------------------------------------ 괴물
        static void BuildMonsters()
        {
            monsters = new Dictionary<MonsterArchetype, MonsterData>();

            var runner = MonsterDef("runner", "역병 주민", MonsterArchetype.Runner, new Color(0.55f, 0.6f, 0.5f));
            runner.maxHp = 90; runner.moveSpeed = 5.2f; runner.attackDamage = 8; runner.attackInterval = 0.9f;
            runner.infectionPerHit = 7; runner.xp = 10; runner.gold = 4; runner.partDropChance = 0.04f;
            monsters[runner.archetype] = runner;

            var brute = MonsterDef("brute", "장군 망자", MonsterArchetype.Brute, new Color(0.45f, 0.3f, 0.3f));
            brute.maxHp = 600; brute.defense = 6; brute.moveSpeed = 2.6f; brute.knockbackResist = 0.75f;
            brute.attackDamage = 25; brute.attackRange = 2.4f; brute.attackInterval = 1.6f; brute.infectionPerHit = 15;
            brute.scale = 1.6f; brute.xp = 45; brute.gold = 20; brute.partDropChance = 0.25f;
            brute.resistances = new float[] { 0.1f, 0f, -0.2f, 0f, 0f };
            monsters[brute.archetype] = brute;

            var spitter = MonsterDef("spitter", "토혈귀", MonsterArchetype.Spitter, new Color(0.4f, 0.55f, 0.2f));
            spitter.maxHp = 140; spitter.moveSpeed = 3.4f; spitter.attackDamage = 12; spitter.attackRange = 16f;
            spitter.attackInterval = 2.6f; spitter.infectionPerHit = 12; spitter.xp = 18; spitter.gold = 8;
            spitter.partDropChance = 0.08f; spitter.resistances = new float[] { 0f, -0.3f, 0f, 0f, 0.5f };
            monsters[spitter.archetype] = spitter;

            var boss = MonsterDef("boss", "역병 대감", MonsterArchetype.Boss, new Color(0.35f, 0.15f, 0.3f));
            boss.maxHp = 6000; boss.defense = 8; boss.moveSpeed = 2.8f; boss.knockbackResist = 0.95f;
            boss.attackDamage = 35; boss.attackRange = 3.6f; boss.attackInterval = 1.8f; boss.infectionPerHit = 20;
            boss.scale = 2.8f; boss.xp = 400; boss.gold = 200; boss.partDropChance = 1f;
            monsters[boss.archetype] = boss;
        }

        static MonsterData MonsterDef(string id, string name, MonsterArchetype a, Color color)
        {
            var m = ScriptableObject.CreateInstance<MonsterData>();
            m.id = id; m.name = id; m.displayName = name; m.archetype = a; m.color = color;
            return m;
        }
    }
}

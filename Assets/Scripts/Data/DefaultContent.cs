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

        static List<WeaponData> catalog;
        /// <summary>무기고 전체: 계열마다 1~3단계 (시작 무기 = 각 계열 1단계).</summary>
        public static List<WeaponData> Catalog { get { if (catalog == null) BuildCatalog(); return catalog; } }

        /// <summary>무기고에 보이는 계열 순서.</summary>
        public static readonly WeaponClass[] Families =
        {
            WeaponClass.Rifle, WeaponClass.SMG, WeaponClass.Shotgun, WeaponClass.Sniper,
            WeaponClass.LMG, WeaponClass.Flamethrower, WeaponClass.Launcher, WeaponClass.Pistol,
        };

        public static List<WeaponData> Family(WeaponClass c)
        {
            var l = new List<WeaponData>();
            foreach (var w in Catalog) if (w.weaponClass == c) l.Add(w);
            l.Sort((a, b) => a.tier.CompareTo(b.tier));
            return l;
        }

        /// <summary>
        /// 상위 단계 무기. 원칙: 연사가 빠르고 탄창이 클수록 한 발 피해가 낮다
        /// → 망자 방어도(한 발마다 깎임)가 오르면 한 방이 센 총·관통·폭발·화염이 유리해진다.
        /// </summary>
        static void BuildCatalog()
        {
            catalog = new List<WeaponData>(Weapons);
            foreach (var w in catalog) { w.tier = 1; w.price = 0; }

            // 권총
            var p2 = Clone(Weapons[0], "pistol2", "K5 권총", 2, 300);
            p2.damage = 36; p2.rpm = 380; p2.magazine = 13; p2.penetration = 0.2f; p2.recoilVertical = 1.4f;
            var p3 = Clone(Weapons[0], "revolver", "매그넘 리볼버", 3, 700);
            p3.damage = 95; p3.rpm = 140; p3.magazine = 6; p3.penetration = 0.55f; p3.reloadTime = 2.4f; p3.emptyReloadTime = 2.4f;
            p3.recoilVertical = 4.5f; p3.knockback = 3.5f; p3.weight = 2.2f;

            // 소총
            var r2 = Clone(Weapons[1], "k2c", "K2C 소총", 2, 450);
            r2.damage = 40; r2.rpm = 700; r2.penetration = 0.3f; r2.recoilVertical = 0.85f;
            var r3 = Clone(Weapons[1], "br762", "7.62 전투소총", 3, 900);
            r3.damage = 64; r3.rpm = 520; r3.magazine = 20; r3.penetration = 0.45f; r3.recoilVertical = 1.5f; r3.knockback = 2f; r3.weight = 5f;

            // 기관단총
            var s2 = Clone(Weapons[2], "ump", "UMP 기관단총", 2, 400);
            s2.damage = 27; s2.rpm = 720; s2.magazine = 30; s2.damageType = DamageType.Physical; s2.penetration = 0.15f;
            var s3 = Clone(Weapons[2], "vector", "벡터 기관단총", 3, 850);
            s3.damage = 23; s3.rpm = 1150; s3.magazine = 33; s3.damageType = DamageType.Physical; s3.recoilVertical = 0.45f;

            // 산탄총
            var g2 = Clone(Weapons[3], "autoshotgun", "자동 산탄총", 2, 500);
            g2.damage = 12; g2.rpm = 230; g2.automatic = true; g2.magazine = 10; g2.recoilVertical = 2.6f; g2.knockback = 7f;
            var g3 = Clone(Weapons[3], "doublebarrel", "쌍발 엽총", 3, 950);
            g3.damage = 26; g3.pellets = 10; g3.rpm = 140; g3.magazine = 2; g3.reloadTime = 2.2f; g3.emptyReloadTime = 2.2f;
            g3.knockback = 14f; g3.penetration = 0.3f; g3.recoilVertical = 6f; g3.weight = 4f;

            // 저격총
            var n2 = Clone(Weapons[4], "dmr", "반자동 지정사수총", 2, 550);
            n2.damage = 85; n2.rpm = 180; n2.magazine = 10; n2.penetration = 0.6f; n2.recoilVertical = 3f; n2.adsFov = 30;
            var n3 = Clone(Weapons[4], "antimat", "대물 저격총", 3, 1200);
            n3.damage = 330; n3.rpm = 35; n3.magazine = 5; n3.penetration = 0.95f; n3.recoilVertical = 9f; n3.knockback = 9f; n3.weight = 9f;

            // 경기관총
            var l2 = Clone(Weapons[5], "k3", "K3 경기관총", 2, 600);
            l2.damage = 30; l2.rpm = 800; l2.magazine = 150; l2.damageType = DamageType.Physical; l2.penetration = 0.3f;
            var l3 = Clone(Weapons[5], "hmg", "중기관총", 3, 1300);
            l3.damage = 50; l3.rpm = 550; l3.magazine = 100; l3.damageType = DamageType.Physical; l3.penetration = 0.5f;
            l3.recoilVertical = 1.3f; l3.weight = 12f; l3.knockback = 3f;

            // 화염방사기: 짧고 넓게, 방어도와 상관없이 타오른다 (화상 지속 피해)
            var f1 = Weapon("flamer", "화염방사기", WeaponClass.Flamethrower, DamageType.Fire, new Color(0.35f, 0.15f, 0.1f));
            f1.tier = 1; f1.price = 350;
            f1.damage = 9; f1.pellets = 3; f1.rpm = 1000; f1.magazine = 150; f1.reserveAmmo = 450; f1.reloadTime = 3f; f1.emptyReloadTime = 3.4f;
            f1.recoilVertical = 0.1f; f1.recoilHorizontal = 0.1f; f1.spreadHip = 7f; f1.spreadAds = 5f; f1.spreadMovePenalty = 0f;
            f1.knockback = 0.3f; f1.weight = 7f; f1.falloffStart = 5f; f1.falloffEnd = 11f; f1.minFalloff = 0.4f; f1.maxRange = 12f;
            f1.penetration = 1f; f1.adsFov = 68; f1.recoilSeed = 0.7f;
            var f2 = Clone(f1, "flamer2", "고열 화염방사기", 2, 800);
            f2.damage = 13; f2.maxRange = 15f; f2.falloffEnd = 14f; f2.magazine = 200;
            var f3 = Clone(f1, "napalm", "네이팜 분사기", 3, 1400);
            f3.damage = 18; f3.maxRange = 18f; f3.falloffEnd = 17f; f3.magazine = 200; f3.pellets = 4;

            // 유탄 · 로켓: 터지는 탄, 방어도 무시
            var m1 = Weapon("m79", "M79 유탄발사기", WeaponClass.Launcher, DamageType.Physical, new Color(0.4f, 0.3f, 0.18f));
            m1.tier = 1; m1.price = 400;
            m1.damage = 190; m1.rpm = 60; m1.automatic = false; m1.magazine = 1; m1.reserveAmmo = 20; m1.reloadTime = 1.8f; m1.emptyReloadTime = 1.8f;
            m1.recoilVertical = 5f; m1.spreadHip = 1.5f; m1.spreadAds = 0.5f; m1.knockback = 5f; m1.weight = 5f;
            m1.explosive = true; m1.blastRadius = 4.5f; m1.projectileSpeed = 38f; m1.adsFov = 60;
            var m2 = Clone(m1, "mgl", "연발 유탄발사기", 2, 900);
            m2.damage = 160; m2.rpm = 110; m2.magazine = 6; m2.reserveAmmo = 30; m2.reloadTime = 4f; m2.emptyReloadTime = 4.5f; m2.weight = 7f;
            var m3 = Clone(m1, "rocket", "로켓발사기 (미사일탄)", 3, 1500);
            m3.damage = 460; m3.rpm = 40; m3.reserveAmmo = 10; m3.reloadTime = 3f; m3.emptyReloadTime = 3f; m3.blastRadius = 6.5f;
            m3.projectileSpeed = 30f; m3.weight = 8f; m3.recoilVertical = 7f;

            catalog.AddRange(new[] { p2, p3, r2, r3, s2, s3, g2, g3, n2, n3, l2, l3, f1, f2, f3, m1, m2, m3 });
        }

        static WeaponData Clone(WeaponData src, string id, string name, int tier, int price)
        {
            var w = Object.Instantiate(src);
            w.id = id; w.name = id; w.displayName = name; w.tier = tier; w.price = price;
            w.recoilSeed = src.recoilSeed + tier * 1.7f;
            return w;
        }
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

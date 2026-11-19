using Hunt.Combat;
using Hunt.Data;
using Hunt.Stats;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>UI에 쓰는 이름·설명·문장(紋章) 글자·분위기 문구 모음.</summary>
    public static class GameText
    {
        public const string Title = "역병의 마을";
        public const string TitleSub = "PROJECT : HUNT";
        public const string Tagline = "비는 그치지 않고, 무덤은 열렸다.";

        // ------------------------------------------------------------------ 스탯
        public static string StatName(StatType t)
        {
            switch (t)
            {
                case StatType.STR: return "근력";
                case StatType.AGI: return "민첩";
                case StatType.CTL: return "제어";
                case StatType.VIT: return "생명";
                case StatType.FOC: return "집중";
                default: return "기술";
            }
        }

        /// <summary>도장에 새길 한글 한 글자.</summary>
        public static string StatGlyph(StatType t)
        {
            switch (t)
            {
                case StatType.STR: return "힘";
                case StatType.AGI: return "날";
                case StatType.CTL: return "손";
                case StatType.VIT: return "숨";
                case StatType.FOC: return "눈";
                default: return "꾀";
            }
        }

        public static string StatShort(StatType t)
        {
            switch (t)
            {
                case StatType.STR: return "무거운 무장을 견디고, 괴물을 밀어낸다";
                case StatType.AGI: return "빨리 달리고, 빨리 장전한다";
                case StatType.CTL: return "반동을 길들이고, 조준을 다잡는다";
                case StatType.VIT: return "더 오래 버티고, 역병에 덜 물든다";
                case StatType.FOC: return "급소를 꿰뚫는 눈";
                default: return "역병의 힘을 더 싸게, 더 오래 다룬다";
            }
        }

        /// <summary>해당 스탯이 바꾸는 실제 수치 (현재 스탯 기준).</summary>
        public static string StatEffects(StatType t, CharacterStats s)
        {
            switch (t)
            {
                case StatType.STR: return $"소지 무게 한도 {s.CarryCapacity:0.0}  ·  저지력 ×{s.KnockbackMult:0.00}";
                case StatType.AGI: return $"이동 {s.MoveSpeed:0.00}m/s  ·  재장전 ×{s.ReloadSpeedMult:0.00}  ·  교체 ×{s.SwapSpeedMult:0.00}";
                case StatType.CTL: return $"반동 무작위성 ×{s.RecoilNoise:0.00}  ·  회복 {s.RecoilRecovery:0.0}°/s  ·  탄퍼짐 ×{s.SpreadMult:0.00}";
                case StatType.VIT: return $"최대 생명 {s.MaxHp:0}  ·  역병 저항 ×{s.InfectionResistMult:0.00}";
                case StatType.FOC: return $"치명 {s.CritChance * 100:0.0}% ×{s.CritMultiplier:0.00}  ·  약점 +{s.WeakPointBonus * 100:0}%";
                default: return $"역병 능력 비용 ×{s.AbilityCostMult:0.00}  ·  지속 ×{s.AbilityDurationMult:0.00}";
            }
        }

        public static string StatTooltip(StatType t)
        {
            switch (t)
            {
                case StatType.STR: return "<b>근력</b>\n\n• 소지 무게 한도 +1.5 / 포인트\n• 저지력(넉백) +1% / 포인트\n\n무거운 경기관총·저격총을 들어도 느려지지 않는다.";
                case StatType.AGI: return "<b>민첩</b>\n\n• 이동 속도 +1% / 포인트\n• 재장전 속도 +1.5% / 포인트\n• 무기 교체 속도 +2% / 포인트";
                case StatType.CTL: return "<b>제어</b>\n\n• 반동의 무작위 흔들림 감소 (반동 모양은 유지 → 외울 수 있다)\n• 반동 회복 속도 +3% / 포인트\n• 탄퍼짐 감소, 조준 속도 증가";
                case StatType.VIT: return "<b>생명</b>\n\n• 최대 생명 +10 / 포인트\n• 역병 기운 축적 감소";
                case StatType.FOC: return "<b>집중</b>\n\n• 치명타 확률 +0.5% / 포인트\n• 치명타 배율 +1% / 포인트\n• 약점 피해 +2% / 포인트";
                default: return "<b>기술</b>\n\n• 역병 능력(광폭화·재생) 비용 감소\n• 역병 능력 지속시간 증가";
            }
        }

        // ------------------------------------------------------------------ 특성
        public static string TraitGlyph(Trait t)
        {
            switch (t)
            {
                case Trait.Bloodlust: return "피";
                case Trait.RunAndGun: return "발";
                case Trait.SteadyAim: return "숨";
                case Trait.ThickSkin: return "살";
                case Trait.MutantAffinity: return "병";
                case Trait.Scavenger: return "줍";
                case Trait.QuickHands: return "손";
                default: return "짐";
            }
        }

        public static string TraitFlavor(Trait t)
        {
            switch (t)
            {
                case Trait.Bloodlust: return "\"피 냄새가 짙어질수록 손이 덜 떨린다.\"";
                case Trait.RunAndGun: return "\"논두렁을 달리며 쏘는 법은 아버지께 배웠다.\"";
                case Trait.SteadyAim: return "\"숨을 멈추면 빗소리도 멎는다.\"";
                case Trait.ThickSkin: return "\"평생 밭일로 굳은 살갗이다. 쉽게 안 뚫린다.\"";
                case Trait.MutantAffinity: return "\"내 피에도 그것이 섞여 있다. 그렇다면 쓰겠다.\"";
                case Trait.Scavenger: return "\"죽은 이웃의 주머니를 뒤지는 건… 살기 위해서다.\"";
                case Trait.QuickHands: return "\"탄창은 손이 먼저 기억한다.\"";
                default: return "\"쌀가마니 두 개쯤이야.\"";
            }
        }

        // ------------------------------------------------------------------ 무기
        public static string WeaponClassName(WeaponClass c)
        {
            switch (c)
            {
                case WeaponClass.Pistol: return "권총";
                case WeaponClass.SMG: return "기관단총";
                case WeaponClass.Shotgun: return "산탄총";
                case WeaponClass.Sniper: return "저격총";
                case WeaponClass.LMG: return "경기관총";
                case WeaponClass.Melee: return "근접";
                case WeaponClass.Explosive: return "폭발물";
                case WeaponClass.Flamethrower: return "화염방사기";
                case WeaponClass.Launcher: return "유탄발사기";
                default: return "돌격소총";
            }
        }

        public static string WeaponFlavor(WeaponData w)
        {
            switch (w.id)
            {
                case "pistol": return "파출소 무기고에서 챙긴 마지막 권총.";
                case "ar": return "예비군 창고의 소총. 무엇이든 무난하게.";
                case "smg": return "전류가 흐르는 탄. 무리 사이로 번개가 튄다.";
                case "shotgun": return "멧돼지 사냥용. 가까이 온 것은 밀어낸다.";
                case "sniper": return "정자나무 위에서. 급소 하나면 충분하다.";
                case "lmg": return "불을 뿜는 쇳덩이. 무겁지만 멈추지 않는다.";
                default: return "";
            }
        }

        public static string ElementName(DamageType t) => CombatNames.Of(t);

        // ------------------------------------------------------------------ 추천 각인 (프리셋)
        public struct Preset
        {
            public string name, desc;
            public int[] alloc; // STR, AGI, CTL, VIT, FOC, TEC
        }

        public static readonly Preset[] Presets =
        {
            new Preset { name = "균형", desc = "모든 능력을 고르게", alloc = new[] { 1, 1, 1, 1, 1, 1 } },
            new Preset { name = "도살자", desc = "근력·생명 — 버티며 밀어낸다", alloc = new[] { 3, 0, 0, 3, 0, 0 } },
            new Preset { name = "사냥꾼", desc = "제어·집중 — 급소를 노린다", alloc = new[] { 0, 0, 3, 0, 3, 0 } },
            new Preset { name = "그림자", desc = "민첩·집중 — 치고 빠진다", alloc = new[] { 0, 4, 0, 0, 2, 0 } },
            new Preset { name = "역병체", desc = "기술·생명 — 역병의 힘을 쓴다", alloc = new[] { 0, 0, 0, 3, 0, 3 } },
        };
    }
}

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

        /// <summary>시작 화면 '새로 바뀐 점' (최근 업데이트, 위가 최신). 큰 변화가 있을 때마다 위에 추가.</summary>
        /// <summary>쓰러졌을 때 보여 주는 요령 한 줄 (무작위).</summary>
        public static readonly string[] DeathTips =
        {
            "다리를 쏘면 망자가 절뚝인다 — 빠른 놈부터 다리를 노려라",
            "Ctrl로 앉으면 탄이 모이고 반동이 줄어든다",
            "물기 직전 동작이 보이면 한 걸음 물러나라 — 헛물게 된다",
            "세게 맞혀 휘청이게 하면 물려던 공격이 끊긴다",
            "E로 발밑 보급품을 한꺼번에 줍는다 · 치유 부적은 다쳤을 때 먹어라",
            "손전등을 켜면 멀리 보이지만, 망자도 그 빛을 본다",
            "빗소리가 셀수록 발소리·총소리가 덜 퍼진다",
            "수류탄(5번)을 들면 날아갈 길이 보인다",
            "저격 조준경에서 Shift를 누르면 숨을 참아 조준이 멈춘다",
            "5연속 처치마다 엽전, 10연속이면 치유 부적이 떨어진다",
            "보스 위치는 화면에 '대' 표식으로 보인다",
            "배가 부푼 '터지는 망자'는 무리 한가운데에서 쏘아 터뜨려라 — 가까이 오면 위험하다",
            "서낭당 무기고의 가는 금은 지금 든 총 — ▲면 더 좋다",
        };

        public static readonly string[] WhatsNew =
        {
            "새 망자 '곡하는 망자'(셋째 밤~) — 발견하면 울부짖어 주변 망자를 부른다, 먼저 쏴라",
            "새 망자 '터지는 망자' — 죽으면 터져서 주변 망자까지 다치게 한다 (가까이서 잡지 말 것)",
            "망자가 땅을 헤치고 기어 나온다 · 물기 직전 동작이 보여 피할 수 있다",
            "보스를 쓰러뜨리면 슬로 모션 · 밤 시작 때 큰 제목",
            "저격 조준경 흔들림 + Shift 숨 참기 · 수류탄 길 미리 보기",
            "Esc 설정: 밝기 · 시야각",
            "E로 보급품 줍기 · 치유 부적(힐링룬) · 새 탄창 모양",
            "머리를 맞히면 갓이 날아간다 · 벽에 튀는 피",
            "다섯째 밤 최종 보스 '역병 대감' · 승리 화면",
            "폭발 연출 · 귀 울림 · 망자가 폭발에 밀려난다",
        };

        // ------------------------------------------------------------------ 스탯
        public static string StatName(StatType t)
        {
            switch (t)
            {
                case StatType.STR: return "힘";
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
                case StatType.STR: return "총과 칼이 더 아프게 박힌다";
                case StatType.AGI: return "빨리 달리고, 빨리 장전한다";
                case StatType.CTL: return "반동을 길들이고, 조준을 다잡는다";
                case StatType.VIT: return "버티는 몸, 흔들리지 않는 손";
                case StatType.FOC: return "급소를 꿰뚫는 눈";
                default: return "벽을 뚫는 탄, 터지는 도구, 역병의 힘을 다룬다";
            }
        }

        /// <summary>해당 스탯이 바꾸는 실제 수치 (현재 스탯 기준).</summary>
        public static string StatEffects(StatType t, CharacterStats s)
        {
            switch (t)
            {
                case StatType.STR: return $"총 피해 ×{s.DamageMult:0.00}  ·  칼 ×{s.MeleeDamageMult:0.00}  ·  무게 한도 {s.CarryCapacity:0.0}";
                case StatType.AGI: return $"이동 {s.MoveSpeed:0.00}m/s  ·  재장전 ×{s.ReloadSpeedMult:0.00}  ·  교체 ×{s.SwapSpeedMult:0.00}";
                case StatType.CTL: return $"반동 무작위성 ×{s.RecoilNoise:0.00}  ·  회복 {s.RecoilRecovery:0.0}°/s  ·  탄퍼짐 ×{s.SpreadMult:0.00}";
                case StatType.VIT: return $"생명 {s.MaxHp:0}  ·  받는 피해 ×{s.DamageTakenMult:0.00}  ·  반동 흔들림 ×{s.RecoilNoise:0.00}  ·  탄퍼짐 ×{s.SpreadMult:0.00}";
                case StatType.FOC: return $"치명 {s.CritChance * 100:0.0}% ×{s.CritMultiplier:0.00}  ·  약점 +{s.WeakPointBonus * 100:0}%";
                default: return $"벽 관통 ×{s.WallPenetration(1f) / 0.85f:0.00}  ·  수류탄·설치물 ×{s.GadgetDamageMult:0.00}  ·  역병 능력 비용 ×{s.AbilityCostMult:0.00}";
            }
        }

        public static string StatTooltip(StatType t)
        {
            switch (t)
            {
                case StatType.STR: return "<b>힘 — 공격력</b>\n\n• 총 피해 +2.5% / 포인트\n• 칼 피해 +5% / 포인트\n• 소지 무게 한도 +1.5, 저지력(넉백) +1% / 포인트\n\n높이 올릴수록 효율이 줄어든다 (16·28 이상).";
                case StatType.AGI: return "<b>민첩</b>\n\n• 이동 속도 +1% / 포인트\n• 재장전 속도 +1.5% / 포인트\n• 무기 교체 속도 +2% / 포인트";
                case StatType.CTL: return "<b>제어</b>\n\n• 반동의 무작위 흔들림 감소 (반동 모양은 유지 → 외울 수 있다)\n• 반동 회복 속도 +3% / 포인트\n• 탄퍼짐 감소, 조준 속도 증가";
                case StatType.VIT: return "<b>생명 — 버티는 몸, 흔들리지 않는 손</b>\n\n• 최대 생명 +10 / 포인트\n• 받는 피해 약 1.2% 감소 / 포인트\n• 역병 기운 축적 감소\n• 반동의 무작위 흔들림 감소 (반동 모양은 유지 → 외울 수 있다)\n• 반동 회복·조준 속도 증가, 탄퍼짐 감소";
                case StatType.FOC: return "<b>집중</b>\n\n• 치명타 확률 +0.5% / 포인트\n• 치명타 배율 +1% / 포인트\n• 약점 피해 +2% / 포인트";
                default: return "<b>기술 — 장비 다루기</b>\n\n• 벽 관통 깊이 +3% / 포인트 (총알이 더 두꺼운 벽을 뚫는다)\n• 수류탄·지뢰·설치형 기관총 피해 +3% / 포인트\n• 역병 능력(광폭화·재생) 비용 감소·지속 증가";
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
            new Preset { name = "균형", desc = "모든 능력을 고르게", alloc = new[] { 1, 1, 0, 2, 1, 1 } },
            new Preset { name = "도살자", desc = "힘·생명 — 버티며 밀어낸다", alloc = new[] { 3, 0, 0, 3, 0, 0 } },
            new Preset { name = "사냥꾼", desc = "생명·집중 — 흔들림 없이 급소를 노린다", alloc = new[] { 0, 0, 0, 3, 3, 0 } },
            new Preset { name = "그림자", desc = "민첩·집중 — 치고 빠진다", alloc = new[] { 0, 4, 0, 0, 2, 0 } },
            new Preset { name = "역병체", desc = "기술·생명 — 역병의 힘을 쓴다", alloc = new[] { 0, 0, 0, 3, 0, 3 } },
        };
    }
}

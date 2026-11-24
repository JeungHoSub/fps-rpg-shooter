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
        /// <summary>보급 상자에서 가끔 나오는 찢어진 쪽지 (마을에 무슨 일이 있었는지). 순서대로 하나씩.</summary>
        public static readonly string[] LoreNotes =
        {
            "장마가 스무 날째다. 뒷산 고분 봉분이 무너졌다는 말을 이장님이 했다. 아무도 올라가 보지 않았다.",
            "윗집 할매가 밤새 곡을 했다. 아침에 가 보니 할매는 없고, 마당에 맨발 자국만 고분 쪽으로 나 있었다.",
            "보건소에 전화가 안 된다. 읍내로 나가는 다리가 물에 잠겼다. 개들이 짖지 않는다. 그게 더 무섭다.",
            "갓 쓴 사람들을 봤다. 비 맞은 도포가 땅에 끌렸다. 우리 마을에 갓 쓰는 사람은 없다.",
            "총은 창고에 있던 사냥총이다. 아버지가 멧돼지 잡던 것. 탄이 서른 발. 아껴 써야 한다.",
            "서낭당 돌무더기 앞에서만은 그것들이 다가오지 않는다. 새벽이 오면 그늘로 숨는다.",
            "옛 족보에 적혀 있었다. 이 마을은 정축년 역병 때 한 번 비었다고. 그때 묻은 이들이 '대감'과 함께 고분에 있다고.",
            "대감은 죽지 않았다. 다섯째 밤마다 일어난다고 할아버지가 말했었다. 그 말을 웃어넘긴 게 후회된다.",
            "물린 자리가 검게 번진다. 청심환을 삼키니 조금 가라앉는다. 오래가지는 않을 것이다.",
            "누가 이걸 읽는다면 — 머리를 노려라. 그리고 날이 밝을 때까지 버텨라.",
        };

        /// <summary>쓰러졌을 때 보여 주는 요령 한 줄 (무작위).</summary>
        public static readonly string[] DeathTips =
        {
            "머리를 맞혀 쓰러뜨린 망자는 다시 일어나지 못한다",
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
            "칼(V)로 쓰러뜨리면 치유 부적이 잘 나온다 — 달려들며 휘두르면 한 걸음 파고든다",
            "M을 누르고 있으면 마을 지도 — 노란 점은 보급 상자",
            "배가 부푼 '터지는 망자'는 무리 한가운데에서 쏘아 터뜨려라 — 가까이 오면 위험하다",
            "서낭당 무기고의 가는 금은 지금 든 총 — ▲면 더 좋다",
        };

        public static readonly string[] WhatsNew =
        {
            "새 특수 기기 '화염병'(5번) — 깨지면 7초 동안 불바다, 길목을 막는다 (서낭당에서 살 수 있음)",
            "몸통만 맞고 쓰러진 망자는 가끔 다시 일어난다 — 머리를 노려라",
            "업적 10가지 — 시작 화면 오른쪽 위 '업적' (대감 처치·머리 100·10연속·쪽지 모두 등)",
            "보급 상자에서 가끔 '찢어진 쪽지' — 마을에 무슨 일이 있었는지 10장",
            "무기 숙련 — 한 총으로 망자를 많이 잡을수록 위력이 오른다 (30·100·250·500)",
            "M을 누르고 있으면 마을 지도 — 나·보급 상자·보스 위치",
            "보스가 나오면 북이 울린다 — 보스가 약해질수록 빨라진다 · 아래 나침반 띠",
            "밤마다 마을 곳곳에 보급 상자 3개 (노란 빛기둥) — E로 열면 탄창·치유 부적·수류탄",
            "달리다 Ctrl — 낮게 미끄러지기 (망자 사이를 빠져나갈 때)",
            "Esc 설정에 난이도(쉬움·보통·어려움) — 망자 체력·피해가 바뀐다",
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
                case Trait.Demolitionist: return "화";
                case Trait.Herbalist: return "약";
                case Trait.Headhunter: return "머";
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
                case Trait.Demolitionist: return "\"광산에서 남은 화약이 아직 창고에 있다.\"";
                case Trait.Herbalist: return "\"뒷산 약초는 내 손바닥 보듯 안다.\"";
                case Trait.Headhunter: return "\"망자는 머리를 날려야 눕는다. 할머니 말씀이다.\"";
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

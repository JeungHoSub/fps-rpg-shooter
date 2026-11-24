using Hunt.Core;
using Hunt.Player;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// 첫 밤 조작 안내: 왼쪽 위에 한 단계씩. 해 보면 넘어가고, 안 해도 잠시 뒤 넘어간다.
    /// 한 번 끝까지 보면 다시 나오지 않는다 (PlayerPrefs "hunt.tut.done"). 터치 조작에서는 띄우지 않는다.
    /// </summary>
    public class TutorialHints
    {
        struct Step { public string keys, text; public float maxTime; }

        static readonly Step[] Steps =
        {
            new Step { keys = "W A S D", text = "움직이기   ·   Shift 달리기   ·   Space 뛰기", maxTime = 14f },
            new Step { keys = "마우스 왼쪽", text = "쏘기   ·   오른쪽 버튼 조준", maxTime = 14f },
            new Step { keys = "R", text = "장전 — 탄창이 비면 노리쇠까지 당긴다", maxTime = 12f },
            new Step { keys = "F", text = "손전등 — 잘 보이지만, 망자도 불빛을 보고 온다", maxTime = 12f },
            new Step { keys = "Ctrl", text = "앉기 — 탄이 모이고 발소리가 줄어든다 · 큰 덤불에 앉으면 숨는다 (Esc에서 전환식으로)", maxTime = 12f },
            new Step { keys = "X  C", text = "치료 키트 · 스태미너 회복제 (망자가 떨어뜨리기도 한다)", maxTime = 10f },
            new Step { keys = "G  V  B", text = "수류탄 · 칼(달리며 누르면 발차기) · 돌 던지기(소리로 꾀기)   —   1~5 무기", maxTime = 10f },
            new Step { keys = "E", text = "발밑 보급품 줍기 · 보급 상자 열기 (근처에 없으면 재생 능력)", maxTime = 10f },
            new Step { keys = "M", text = "누르고 있으면 마을 지도 — 노란 빛기둥 보급 상자를 찾아라", maxTime = 10f },
            new Step { keys = "Q  E", text = "역병 능력: 광폭화 · 재생 (오른쪽 아래 구슬이 차면)", maxTime = 10f },
            new Step { keys = "Tab", text = "밤이 끝나면 서낭당에서 능력치·무기를 고른다", maxTime = 9f },
        };

        int step = -1;
        float stepTime, fade;
        bool done;
        Vector3 startPos;
        bool checkedPrefs;
        bool anyNight;
        /// <summary>Esc 메뉴 '조작 안내 다시 보기' — 다음 화면부터 처음부터 다시 (몇 번째 밤이든).</summary>
        public static bool Replay;

        bool Satisfied(int i, PlayerCharacter p)
        {
            switch (i)
            {
                case 0: return Vector3.Distance(p.transform.position, startPos) > 6f;
                case 1: return GameInput.FireDownRaw;
                case 2: return GameInput.ReloadDown;
                case 3: return GameInput.FlashlightDown;
                case 4: return GameInput.Crouch;
                case 5: return GameInput.KitDown || GameInput.DrinkDown;
                case 6: return GameInput.GrenadeDown || GameInput.MeleeDown;
                case 7: return GameInput.PickupDown;
                case 8: return GameInput.MapHeld;
                case 9: return GameInput.RageDown || GameInput.RegenDown;
                default: return false;
            }
        }

        public void Draw(GameManager gm, float W, float H)
        {
            if (Replay) { Replay = false; done = false; step = -1; checkedPrefs = true; anyNight = true; }
            if (done || GameInput.Mobile) return;
            if (!checkedPrefs)
            {
                checkedPrefs = true;
                try { done = PlayerPrefs.GetInt("hunt.tut.done", 0) == 1; } catch { }
                if (done) return;
            }
            var p = gm.Player;
            if (p == null || p.IsDead || gm.Waves == null || (gm.Waves.Wave != 1 && !anyNight) || gm.State != GameState.Playing) return;

            if (UIKit.IsRepaint)
            {
                float dt = Time.unscaledDeltaTime;
                if (step < 0) { step = 0; stepTime = 0f; startPos = p.transform.position; }
                stepTime += dt;
                bool ok = stepTime > 1.2f && Satisfied(step, p);
                if (ok || stepTime > Steps[step].maxTime)
                {
                    if (ok) SFX.Play(SFX.UiClick, 0.35f, 0f);
                    step++;
                    stepTime = 0f;
                    fade = 0f;
                    if (step >= Steps.Length)
                    {
                        done = true;
                        try { PlayerPrefs.SetInt("hunt.tut.done", 1); PlayerPrefs.Save(); } catch { }
                        HUD.Notify("조작 안내 끝 — 살아남아라", UITheme.GoldBright);
                        return;
                    }
                }
                fade = Mathf.MoveTowards(fade, 1f, dt * 3f);
            }
            if (step < 0 || step >= Steps.Length) return;

            var s = Steps[step];
            float a = fade;
            var r = new Rect(30, 150, 470, 64);
            UIKit.Fill(r, new Color(0.03f, 0.03f, 0.035f, 0.78f * a));
            UIKit.Fill(new Rect(r.x, r.y, 3, r.height), new Color(0.97f, 0.94f, 0.86f, a));
            // 키 상자 (한지색)
            float kw = Mathf.Max(44f, UIKit.TextWidth(s.keys, 16, false, true) + 18f);
            var kr = new Rect(r.x + 14, r.y + 14, kw, 36);
            UIKit.Fill(kr, new Color(0.86f, 0.82f, 0.72f, 0.95f * a));
            UIKit.Text(kr, s.keys, 16, new Color(0.06f, 0.05f, 0.05f, a), TextAnchor.MiddleCenter, bold: true);
            UIKit.Text(new Rect(kr.xMax + 12, r.y + 6, r.width - kw - 40, r.height - 12), s.text, 15, new Color(0.9f, 0.87f, 0.8f, a), TextAnchor.MiddleLeft, serif: true, wrap: true);
            // 진행 칸
            for (int i = 0; i < Steps.Length; i++)
                UIKit.Fill(new Rect(r.x + 14 + i * 14, r.yMax + 6, 10, 3), i <= step ? new Color(0.86f, 0.82f, 0.72f, a) : new Color(0.4f, 0.38f, 0.35f, 0.6f * a));
            // 남은 시간 막대
            UIKit.Fill(new Rect(r.x + 3, r.yMax - 2, (r.width - 3) * (1f - stepTime / s.maxTime), 2), new Color(0.6f, 0.56f, 0.48f, 0.6f * a));
        }
    }
}

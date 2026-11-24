using Hunt.Core;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>일시정지(설정 포함) / 사망 화면.</summary>
    public class EndScreens
    {
        float deathAppear;

        int tip;
        public void ResetDeath() { deathAppear = 0f; tip = Random.Range(0, 1000); }

        float pauseAppear, lastPauseDraw = -10f;

        public void DrawPause(GameManager gm, float W, float H)
        {
            // 열릴 때 살짝 아래에서 떠오르며 나타난다 (0.2초)
            if (Time.unscaledTime - lastPauseDraw > 0.25f) pauseAppear = 0f;
            lastPauseDraw = Time.unscaledTime;
            if (UIKit.IsRepaint) pauseAppear = Mathf.MoveTowards(pauseAppear, 1f, Time.unscaledDeltaTime * 5f);
            float pa = UIKit.EaseOut(pauseAppear);
            UIKit.Alpha = pa;
            var keep = GUI.matrix;
            GUI.matrix = keep * Matrix4x4.Translate(new Vector3(0f, (1f - pa) * 24f, 0f));
            try { DrawPauseInner(gm, W, H); }
            finally { GUI.matrix = keep; UIKit.Alpha = 1f; }
        }

        void DrawPauseInner(GameManager gm, float W, float H)
        {
            UIKit.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.01f, 0.015f, 0.7f));
            UIKit.Vignette(W, H, new Color(0, 0, 0, 0.9f));

            var r = new Rect(W / 2 - 320 - (GameInput.Mobile ? 0f : 200f), H / 2 - 450, 640, 900); // PC: 오른쪽 조작 표와 함께 가운데
            UIKit.Panel(r, "숨  을  고 른 다");

            float y = r.y + 90;
            if (UIKit.Button(new Rect(r.x + 90, y, r.width - 180, 62), "계속 사냥한다", 24, false, true, "Esc")) gm.TogglePause();
            y += 90;

            UIKit.Divider(new Rect(r.x + 60, y, r.width - 120, 10));
            y += 26;
            UIKit.Text(new Rect(r.x, y, r.width, 28), "설정", 20, UITheme.Gold, TextAnchor.MiddleCenter, serif: true, bold: true);
            y += 44;

            UIKit.Text(new Rect(r.x + 60, y, 160, 30), "마우스 감도", 18, UITheme.Bone, serif: true);
            GameSettings.MouseSensitivity = UIKit.Slider(new Rect(r.x + 220, y, r.width - 340, 30), GameSettings.MouseSensitivity, 0.2f, 6f, "sens");
            UIKit.Text(new Rect(r.xMax - 110, y, 60, 30), GameSettings.MouseSensitivity.ToString("0.0"), 18, UITheme.GoldBright, TextAnchor.MiddleRight, bold: true);
            y += 52;

            UIKit.Text(new Rect(r.x + 60, y, 160, 30), "소리 크기", 18, UITheme.Bone, serif: true);
            GameSettings.Volume = UIKit.Slider(new Rect(r.x + 220, y, r.width - 340, 30), GameSettings.Volume, 0f, 1f, "vol");
            UIKit.Text(new Rect(r.xMax - 110, y, 60, 30), $"{GameSettings.Volume * 100:0}%", 18, UITheme.GoldBright, TextAnchor.MiddleRight, bold: true);
            y += 52;

            UIKit.Text(new Rect(r.x + 60, y, 160, 30), "시야각", 18, UITheme.Bone, serif: true);
            GameSettings.Fov = UIKit.Slider(new Rect(r.x + 220, y, r.width - 340, 30), GameSettings.Fov, 65f, 95f, "fov");
            UIKit.Text(new Rect(r.xMax - 110, y, 60, 30), $"{GameSettings.Fov:0}°", 18, UITheme.GoldBright, TextAnchor.MiddleRight, bold: true);
            y += 52;

            UIKit.Text(new Rect(r.x + 60, y, 160, 30), "밝기", 18, UITheme.Bone, serif: true);
            GameSettings.Brightness = UIKit.Slider(new Rect(r.x + 220, y, r.width - 340, 30), GameSettings.Brightness, 0.6f, 1.8f, "bright");
            UIKit.Text(new Rect(r.xMax - 110, y, 60, 30), $"{GameSettings.Brightness * 100:0}%", 18, UITheme.GoldBright, TextAnchor.MiddleRight, bold: true);
            y += 54;

            // 그래픽 품질 (PC가 멈추거나 끊기면 '하')
            UIKit.Text(new Rect(r.x + 60, y + 4, 160, 30), "그래픽", 18, UITheme.Bone, serif: true);
            float bw = (r.width - 340) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var lvl = (GfxLevel)i;
                string label = lvl == GfxLevel.Low ? "하" : lvl == GfxLevel.Medium ? "중" : "상";
                if (UIKit.Button(new Rect(r.x + 220 + i * (bw + 6), y, bw, 38), label, 18, QualityManager.Level == lvl)) QualityManager.Set(lvl);
            }
            y += 46;
            Color fpsCol = QualityManager.Fps < 30f ? UITheme.BloodBright : QualityManager.Fps < 50f ? UITheme.Warn : UITheme.Good;
            UIKit.Text(new Rect(r.x + 60, y, r.width - 120, 22),
                $"현재 <color=#{UIKit.Hex(fpsCol)}>{QualityManager.Fps:0} FPS</color>   ·   {(QualityManager.AutoDetected ? "사양에 맞춰 자동 선택됨" : "직접 고름")}", 14, UITheme.BoneDim);
            y += 22;
            UIKit.Text(new Rect(r.x + 60, y, r.width - 120, 22), QualityManager.DeviceSummary, 13, UITheme.Ash);
            y += 36;

            // 터치 조작 설정
            float tw = (r.width - 120 - 12) / 3f;
            if (UIKit.Button(new Rect(r.x + 60, y, tw, 52), "자동 사격", 16, GameSettings.AutoFire, true, GameSettings.AutoFire ? "켜짐" : "꺼짐"))
                GameSettings.AutoFire = !GameSettings.AutoFire;
            if (UIKit.Button(new Rect(r.x + 66 + tw, y, tw, 52), "조준 보조", 16, GameSettings.AimAssist, true, GameSettings.AimAssist ? "켜짐" : "꺼짐"))
                GameSettings.AimAssist = !GameSettings.AimAssist;
            if (!Application.isMobilePlatform &&
                UIKit.Button(new Rect(r.x + 72 + tw * 2, y, tw, 52), "터치 미리보기", 16, GameSettings.ForceMobileControls, true, GameSettings.ForceMobileControls ? "켜짐" : "꺼짐"))
                GameSettings.ForceMobileControls = !GameSettings.ForceMobileControls;
            y += 70;

            UIKit.Divider(new Rect(r.x + 60, y, r.width - 120, 10));
            y += 28;
            if (UIKit.Button(new Rect(r.x + 90, y, r.width - 180, 56), "처음으로 돌아간다", 20, false, true, "진행 상황은 사라진다")) gm.Restart();

            // ---- 오른쪽: 조작 키 모음 (언제든 확인) ----
            if (!GameInput.Mobile)
            {
                var k = new Rect(r.xMax + 20, r.y, 380, r.height);
                if (k.xMax > W - 10) k.x = W - 10 - k.width;
                UIKit.Panel(k, "조  작");
                string[,] keys =
                {
                    { "W A S D", "움직이기" }, { "Shift", "달리기" }, { "Space", "뛰기" }, { "Ctrl", "앉기 (탄 모임·반동↓)" },
                    { "마우스 왼쪽", "쏘기" }, { "마우스 오른쪽", "조준" }, { "R", "장전" }, { "1 ~ 5", "무기 바꾸기" },
                    { "V", "칼" }, { "G", "수류탄" }, { "F", "손전등" }, { "X", "치료 키트" }, { "C", "스태미너 회복제" },
                    { "Q", "광폭화" }, { "E", "줍기 (가까이 있으면) · 재생" }, { "H", "청심환" }, { "Tab", "서낭당 (밤 사이)" }, { "Esc", "숨 고르기" },
                };
                float ky = k.y + 84;
                for (int i = 0; i < keys.GetLength(0); i++)
                {
                    float kw = Mathf.Max(40f, UIKit.TextWidth(keys[i, 0], 14, false, true) + 16f);
                    var kr = new Rect(k.x + 30, ky, kw, 28);
                    UIKit.Fill(kr, new Color(0.86f, 0.82f, 0.72f, 0.92f));
                    UIKit.Text(kr, keys[i, 0], 14, new Color(0.06f, 0.05f, 0.05f), TextAnchor.MiddleCenter, bold: true);
                    UIKit.Text(new Rect(kr.xMax + 12, ky, k.width - kw - 60, 28), keys[i, 1], 15, UITheme.Bone, serif: true);
                    ky += 36;
                }
                if (UIKit.Button(new Rect(k.x + 30, k.yMax - 84, k.width - 60, 52), "조작 안내 다시 보기", 17, false, true, "닫으면 왼쪽 위에 한 단계씩"))
                {
                    TutorialHints.Replay = true;
                    gm.TogglePause();
                }
            }
        }

        // 묘비에 새긴 듯한 한 줄 (죽을 때마다 하나)
        static readonly string[] Epitaphs =
        {
            "빗소리만 남았다. 마을은 또 하나의 망자를 얻었다.",
            "새벽은 끝내 오지 않았다.",
            "서낭당 돌무더기에 돌 하나가 더 얹혔다.",
            "누군가 이 이야기를 이어 가리라.",
            "젖은 흙이 마지막으로 따뜻했다.",
            "멀리서 징 소리가 한 번 울렸다.",
        };
        int epitaph;
        bool recordChecked, newWave, newKills;
        int bestWave, bestKills;

        public void DrawDeath(GameManager gm, float W, float H)
        {
            if (UIKit.IsRepaint)
            {
                if (deathAppear <= 0f) { epitaph = Random.Range(0, Epitaphs.Length); recordChecked = false; }
                // 쓰러지는 연출(약 1.8초)을 지켜본 뒤 천천히 떠오른다
                deathAppear = Mathf.MoveTowards(deathAppear, 1f, Time.unscaledDeltaTime * (deathAppear < 0.01f ? 0.012f : 0.55f));
            }
            var p = gm.Player;
            // 최고 기록 (한 번만 저장)
            if (!recordChecked && p != null && gm.Waves != null)
            {
                recordChecked = true;
                try
                {
                    bestWave = PlayerPrefs.GetInt("hunt.best.wave", 0);
                    bestKills = PlayerPrefs.GetInt("hunt.best.kills", 0);
                    newWave = gm.Waves.Wave > bestWave;
                    newKills = p.Kills > bestKills;
                    if (newWave) PlayerPrefs.SetInt("hunt.best.wave", gm.Waves.Wave);
                    if (newKills) PlayerPrefs.SetInt("hunt.best.kills", p.Kills);
                    PlayerPrefs.Save();
                }
                catch { }
            }
            float a = Mathf.SmoothStep(0f, 1f, deathAppear);

            // 붉게 번지는 피 + 가장자리 먹빛
            UIKit.Fill(new Rect(0, 0, W, H), new Color(0.12f, 0f, 0.01f, 0.55f * a));
            UIKit.Vignette(W, H, new Color(0, 0, 0, a));
            UIKit.Tex(new Rect(-W * 0.2f, H * 0.2f, W * 1.4f, H * 0.6f), UITheme.Glow, new Color(0.6f, 0f, 0.02f, 0.35f * a));

            UIKit.Alpha = a;
            float y = H / 2 - 250 + (1f - a) * 30f;
            // 붓 자국 위 제목
            UIKit.BrushTitle(new Rect(W / 2 - 420, y, 840, 120), "쓰러졌다", 84, new Color(0.03f, 0.02f, 0.02f, 0.9f), UITheme.BloodBright);
            UIKit.Text(new Rect(0, y + 122, W, 30), Epitaphs[epitaph], 20, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            if (p != null && !string.IsNullOrEmpty(p.LastAttacker))
                UIKit.Text(new Rect(0, y + 154, W, 26), $"— {p.LastAttacker}에게 물려 쓰러짐", 16, UITheme.Ash, TextAnchor.MiddleCenter, serif: true);
            UIKit.Divider(new Rect(W / 2 - 320, y + 188, 640, 12));

            if (p != null)
            {
                var r = new Rect(W / 2 - 330, y + 214, 660, 170);
                UIKit.Panel(r);
                Stat(new Rect(r.x + 20, r.y + 24, 150, 120), "버틴 밤", gm.Waves.Wave.ToString());
                Stat(new Rect(r.x + 175, r.y + 24, 150, 120), "단계", $"Lv.{p.Level}");
                Stat(new Rect(r.x + 330, r.y + 24, 150, 120), "쓰러뜨린 망자", p.Kills.ToString());
                Stat(new Rect(r.x + 485, r.y + 24, 150, 120), "역병의 적응", $"{gm.Evolution.Profile.generation}세대");
                // 새 기록이면 해당 칸 위에 붉은 낙관
                if (newWave) Seal(new Rect(r.x + 120, r.y + 6, 46, 46));
                if (newKills) Seal(new Rect(r.x + 430, r.y + 6, 46, 46));
                string best = $"지난 최고 — 밤 {Mathf.Max(bestWave, 0)} · 망자 {Mathf.Max(bestKills, 0)}";
                UIKit.Text(new Rect(r.x, r.yMax + 8, r.width, 22), best, 14, UITheme.Ash, TextAnchor.MiddleCenter);
            }

            // 요령 한 줄 (쓰러질 때마다 다른 것)
            UIKit.Text(new Rect(0, y + 506, W, 24), "요령 — " + GameText.DeathTips[tip % GameText.DeathTips.Length], 16, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            if (a > 0.6f && UIKit.Button(new Rect(W / 2 - 170, y + 424, 340, 70), "다시 사냥한다", 28, false, true, "R")) gm.Restart();
            UIKit.Alpha = 1f;
        }

        static void Seal(Rect r)
        {
            UIKit.Tex(r, UITheme.Seal, UITheme.Blood);
            UIKit.Text(r, "새\n기록", 13, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
        }

        static void Stat(Rect r, string label, string value)
        {
            UIKit.Text(new Rect(r.x, r.y, r.width, 60), value, 40, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Text(new Rect(r.x, r.y + 66, r.width, 26), label, 16, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
        }
    }
}

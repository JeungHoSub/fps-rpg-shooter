using Hunt.Core;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>일시정지(설정 포함) / 사망 화면.</summary>
    public class EndScreens
    {
        float deathAppear;

        public void ResetDeath() { deathAppear = 0f; }

        public void DrawPause(GameManager gm, float W, float H)
        {
            UIKit.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.01f, 0.015f, 0.7f));
            UIKit.Vignette(W, H, new Color(0, 0, 0, 0.9f));

            var r = new Rect(W / 2 - 300, H / 2 - 290, 600, 580);
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
            y += 70;

            UIKit.Divider(new Rect(r.x + 60, y, r.width - 120, 10));
            y += 28;
            if (UIKit.Button(new Rect(r.x + 90, y, r.width - 180, 56), "처음으로 돌아간다", 20, false, true, "진행 상황은 사라진다")) gm.Restart();
        }

        public void DrawDeath(GameManager gm, float W, float H)
        {
            if (UIKit.IsRepaint) deathAppear = Mathf.MoveTowards(deathAppear, 1f, Time.unscaledDeltaTime * 0.6f);
            float a = deathAppear;

            UIKit.Fill(new Rect(0, 0, W, H), new Color(0.12f, 0f, 0.01f, 0.55f * a));
            UIKit.Vignette(W, H, new Color(0, 0, 0, a));
            UIKit.Tex(new Rect(-W * 0.2f, H * 0.2f, W * 1.4f, H * 0.6f), UITheme.Glow, new Color(0.6f, 0f, 0.02f, 0.35f * a));

            UIKit.Alpha = a;
            float y = H / 2 - 230 + (1f - a) * 30f;
            UIKit.Text(new Rect(0, y, W, 110), "당신은 쓰러졌다", 84, UITheme.BloodBright, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Text(new Rect(0, y + 110, W, 34), "빗소리만 남았다. 마을은 또 하나의 망자를 얻었다.", 20, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            UIKit.Divider(new Rect(W / 2 - 320, y + 160, 640, 12));

            var p = gm.Player;
            if (p != null)
            {
                var r = new Rect(W / 2 - 330, y + 190, 660, 170);
                UIKit.Panel(r);
                Stat(new Rect(r.x + 20, r.y + 24, 150, 120), "버틴 밤", gm.Waves.Wave.ToString());
                Stat(new Rect(r.x + 175, r.y + 24, 150, 120), "단계", $"Lv.{p.Level}");
                Stat(new Rect(r.x + 330, r.y + 24, 150, 120), "쓰러뜨린 망자", p.Kills.ToString());
                Stat(new Rect(r.x + 485, r.y + 24, 150, 120), "역병의 적응", $"{gm.Evolution.Profile.generation}세대");
            }

            if (a > 0.6f && UIKit.Button(new Rect(W / 2 - 170, y + 400, 340, 70), "다시 사냥한다", 28, false, true, "R")) gm.Restart();
            UIKit.Alpha = 1f;
        }

        static void Stat(Rect r, string label, string value)
        {
            UIKit.Text(new Rect(r.x, r.y, r.width, 60), value, 40, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Text(new Rect(r.x, r.y + 66, r.width, 26), label, 16, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
        }
    }
}

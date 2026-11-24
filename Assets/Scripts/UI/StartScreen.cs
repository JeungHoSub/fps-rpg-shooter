using System;
using Hunt.Combat;
using Hunt.Core;
using Hunt.Data;
using Hunt.Stats;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// 시작 화면: Ⅰ 능력치 각인 → Ⅱ 특성 계약 → Ⅲ 무장 선택 → Ⅳ 출정.
    /// 오른쪽 "헌터 기록서"가 선택에 따라 실시간으로 바뀌고, 마우스를 올린 선택지의 변화량을 미리 보여준다.
    /// </summary>
    public class StartScreen
    {
        static readonly string[] StepNames = { "몸 만들기", "기질 고르기", "무기 챙기기", "길 나서기" };
        static readonly string[] StepNumerals = { "하나", "둘", "셋", "넷" };
        static readonly string[] StepHints =
        {
            "타고난 몸을 단련한다",
            "어떤 사람으로 버틸 것인가",
            "두 자루를 메고 간다",
            "빗속으로 걸어 나간다",
        };

        int step;
        float appear;            // 화면 등장 페이드
        float stepAppear = 1f;   // 단계 전환 페이드
        StatType? hoverStat;     // + 버튼 미리보기
        Trait? hoverTrait;
        readonly RadarChart radar = new RadarChart();
        float W, H;

        public void Reset()
        {
            step = 0;
            appear = 0f;
        }

        void GoTo(int s)
        {
            s = Mathf.Clamp(s, 0, 3);
            if (s == step) return;
            step = s;
            stepAppear = 0f;
        }

        bool StepDone(GameManager gm, int s)
        {
            switch (s)
            {
                case 0: return gm.StartPointsLeft == 0;
                case 1: return true;
                case 2: return gm.SelectedPrimaries.Count == GameManager.MaxPrimaries;
                default: return gm.StartPointsLeft == 0;
            }
        }

        public void Draw(GameManager gm, float w, float h)
        {
            W = w; H = h;
            if (UIKit.IsRepaint)
            {
                appear = Mathf.MoveTowards(appear, 1f, Time.unscaledDeltaTime * 1.2f);
                stepAppear = Mathf.MoveTowards(stepAppear, 1f, Time.unscaledDeltaTime * 4f);
            }
            hoverStat = null;
            hoverTrait = null;

            // ---- 배경: 어둡게 + 비네트 + 위쪽 핏빛 안개 ----
            UIKit.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.01f, 0.015f, 0.45f));
            UIKit.Tex(new Rect(-W * 0.2f, -H * 0.55f, W * 1.4f, H * 0.9f), UITheme.Glow, new Color(0.5f, 0.02f, 0.03f, 0.35f));
            UIKit.Vignette(W, H, new Color(0, 0, 0, 0.95f));
            DrawRain();

            // 처음 켰을 때 한 번: 제목 카드 (클릭·아무 키로 넘김)
            if (!introShown) { DrawIntroCard(); return; }

            UIKit.Alpha = appear;

            DrawTitle();
            DrawNav(gm);
            DrawSheet(gm);

            var content = new Rect(360, 200, W - 360 - 500, 690);
            UIKit.Alpha = appear * stepAppear;
            switch (step)
            {
                case 0: DrawStats(gm, content); break;
                case 1: DrawTraits(gm, content); break;
                case 2: DrawWeapons(gm, content); break;
                default: DrawDeploy(gm, content); break;
            }
            UIKit.Alpha = appear;
            DrawFooter(gm, content);
            DrawWhatsNew();
            UIKit.Alpha = 1f;

            // 키보드: Enter = 다음 / 출정
            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                e.Use();
                if (step < 3) GoTo(step + 1);
                else TryDeploy(gm);
            }
        }

        // ------------------------------------------------------------------ 새로 바뀐 점 (오른쪽 위 단추 → 펼침)
        bool whatsNewOpen, achOpen;
        float whatsNewT, achT;

        void DrawAchievements()
        {
            if (UIKit.IsRepaint) achT = Mathf.MoveTowards(achT, achOpen ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            if (achT <= 0.01f) return;
            float e = UIKit.EaseOut(achT);
            float keep = UIKit.Alpha;
            UIKit.Alpha = keep * e;
            var list = Achievements.All;
            var r = new Rect(W - 620, 66 + (1f - e) * 12f, 600, 70 + list.Length * 46);
            UIKit.Panel(r);
            UIKit.Text(new Rect(r.x, r.y + 14, r.width, 28), "업적", 22, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
            float y = r.y + 54;
            foreach (var d in list)
            {
                bool done = Achievements.Done(d);
                int prog = Achievements.Progress(d.id);
                var seal = new Rect(r.x + 24, y + 4, 34, 34);
                if (done) { UIKit.Tex(seal, UITheme.Seal, UITheme.Blood); UIKit.Text(seal, "완", 15, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true); }
                else UIKit.Outline(seal, new Color(0.4f, 0.38f, 0.34f), 1f);
                UIKit.Text(new Rect(r.x + 70, y, r.width - 200, 24), d.name, 17, done ? UITheme.GoldBright : UITheme.Bone, TextAnchor.MiddleLeft, serif: true, bold: true);
                UIKit.Text(new Rect(r.x + 70, y + 22, r.width - 200, 20), d.desc, 13, UITheme.BoneDim, TextAnchor.MiddleLeft);
                if (d.goal > 1 && !done)
                    UIKit.Text(new Rect(r.xMax - 130, y + 8, 110, 24), $"{prog} / {d.goal}", 15, UITheme.BoneDim, TextAnchor.MiddleRight);
                y += 46;
            }
            UIKit.Alpha = keep;
        }

        void DrawWhatsNew()
        {
            var btn = new Rect(W - 210, 18, 190, 40);
            if (UIKit.Button(btn, whatsNewOpen ? "닫기" : "새로 바뀐 점", 17, whatsNewOpen)) { whatsNewOpen = !whatsNewOpen; achOpen = false; }
            var abtn = new Rect(W - 410, 18, 190, 40);
            if (UIKit.Button(abtn, achOpen ? "닫기" : $"업적  {Achievements.DoneCount()} / {Achievements.All.Length}", 17, achOpen)) { achOpen = !achOpen; whatsNewOpen = false; }
            DrawAchievements();
            if (UIKit.IsRepaint) whatsNewT = Mathf.MoveTowards(whatsNewT, whatsNewOpen ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            if (whatsNewT <= 0.01f) return;
            float e = UIKit.EaseOut(whatsNewT);
            float keep = UIKit.Alpha;
            UIKit.Alpha = keep * e;
            var list = GameText.WhatsNew;
            var r = new Rect(W - 620, 66 + (1f - e) * 12f, 600, 70 + list.Length * 34);
            UIKit.Panel(r);
            UIKit.Text(new Rect(r.x, r.y + 14, r.width, 28), "새로 바뀐 점", 22, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
            float y = r.y + 54;
            for (int i = 0; i < list.Length; i++)
            {
                UIKit.Text(new Rect(r.x + 28, y, r.width - 56, 30), "·  " + list[i], 16, i == 0 ? UITheme.Bone : UITheme.BoneDim, TextAnchor.MiddleLeft, serif: true);
                y += 34;
            }
            UIKit.Alpha = keep;
        }

        // ------------------------------------------------------------------ 첫 제목 카드 · 빗줄기
        static bool introShown;
        float introT;
        bool gongPlayed;

        void DrawIntroCard()
        {
            if (UIKit.IsRepaint) introT += Time.unscaledDeltaTime;
            var e = Event.current;
            bool skip = introT > 0.6f && (e.type == EventType.MouseDown || e.type == EventType.KeyDown);
            if (skip) e.Use();
            // 0~1.2초 떠오름, 4.2초부터 사라짐, 5초에 끝
            float a = Mathf.Clamp01(introT / 1.2f) * Mathf.Clamp01((5f - introT) / 0.8f);
            UIKit.Alpha = a;
            float y = H / 2 - 120;
            UIKit.Text(new Rect(0, y - 40, W, 26), SpaceOut(GameText.TitleSub), 18, UITheme.GoldDim, TextAnchor.MiddleCenter, bold: true);
            UIKit.BrushTitle(new Rect(0, y, W, 130), GameText.Title, 110, new Color(0f, 0f, 0f, 0.9f), UITheme.GoldBright);
            float tw = UIKit.TextWidth(GameText.Title, 110, true);
            // 낙관은 조금 늦게 '찍힌다'
            float stamp = Mathf.Clamp01((introT - 1.3f) * 5f);
            if (stamp > 0f)
            {
                float sz = Mathf.Lerp(130f, 78f, stamp);
                UIKit.Alpha = a * stamp;
                UIKit.Emblem(new Rect(W / 2 + tw / 2 + 20 + (78 - sz) / 2, y + 26 + (78 - sz) / 2, sz, sz), "역병", UITheme.Blood, true);
                if (stamp >= 1f && !gongPlayed) { gongPlayed = true; SFX.Play(SFX.Gong, 0.5f, 0f); }
                UIKit.Alpha = a;
            }
            float tagA = Mathf.Clamp01((introT - 2f) / 0.8f);
            UIKit.Alpha = a * tagA;
            UIKit.Text(new Rect(0, y + 150, W, 34), GameText.Tagline, 22, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            UIKit.Alpha = a * Mathf.Clamp01((introT - 3f) / 0.5f) * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f));
            UIKit.Text(new Rect(0, H - 90, W, 26), "아무 키나 누르세요", 15, UITheme.Ash, TextAnchor.MiddleCenter, serif: true);
            UIKit.Alpha = 1f;
            if (introT >= 5f || skip) { introShown = true; appear = 0f; }
        }

        // 화면 앞을 스치는 옅은 빗줄기 (분위기)
        void DrawRain()
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < 70; i++)
            {
                float sx = Mathf.Repeat(i * 0.6180339f, 1f), sy = Mathf.Repeat(i * 0.4142135f, 1f);
                float spd = 0.8f + Mathf.Repeat(i * 0.7548776f, 1f) * 0.6f;
                float x = Mathf.Repeat(sx * W + t * 90f * spd, W);
                float y = Mathf.Repeat(sy * H + t * 1100f * spd, H + 60f) - 60f;
                float len = 30f + spd * 25f;
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(-6f, new Vector2(x, y));
                UIKit.Fill(new Rect(x, y, 1.2f, len), new Color(0.75f, 0.8f, 0.9f, 0.05f + 0.04f * spd));
                GUI.matrix = m;
            }
        }

        // ------------------------------------------------------------------ 제목
        void DrawTitle()
        {
            float y = 26 - (1f - appear) * 20f;
            UIKit.Text(new Rect(0, y, W, 24), SpaceOut(GameText.TitleSub), 16, UITheme.GoldDim, TextAnchor.MiddleCenter, bold: true);
            // 먹 붓질 위에 궁서체 제목, 옆에 붉은 낙관
            UIKit.BrushTitle(new Rect(0, y + 22, W, 80), GameText.Title, 70, new Color(0f, 0f, 0f, 0.85f), UITheme.GoldBright);
            float tw = UIKit.TextWidth(GameText.Title, 70, true);
            UIKit.Emblem(new Rect(W / 2 + tw / 2 + 14, y + 34, 58, 58), "역병", UITheme.Blood, true);
            UIKit.Text(new Rect(0, y + 100, W, 30), GameText.Tagline, 18, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            // 지난 최고 기록 (사망 화면에서 저장)
            int bw = 0, bk = 0;
            try { bw = PlayerPrefs.GetInt("hunt.best.wave", 0); bk = PlayerPrefs.GetInt("hunt.best.kills", 0); } catch { }
            if (bw > 0) UIKit.Text(new Rect(W - 330, y + 6, 300, 22), $"최고 기록  밤 {bw} · 망자 {bk}", 14, UITheme.Ash, TextAnchor.MiddleRight, serif: true);
            UIKit.Divider(new Rect(W / 2 - 360, y + 138, 720, 12));
        }

        static string SpaceOut(string s) => string.Join(" ", s.ToCharArray());

        // ------------------------------------------------------------------ 왼쪽 단계 안내
        void DrawNav(GameManager gm)
        {
            float x = 40, y = 200, w = 290;
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(x, y + i * 112, w, 100);
                bool cur = i == step;
                bool done = StepDone(gm, i) && i != 3;
                bool click = UIKit.HitArea(r, "nav" + i, out float hv);

                UIKit.NineSlice(r, cur ? UITheme.PanelHeader : UITheme.Panel, 12, new Color(1, 1, 1, cur ? 1f : 0.75f + hv * 0.25f));
                if (cur) UIKit.Tex(new Rect(r.x - 6, r.y + 10, 4, r.height - 20), UITheme.Pixel, UITheme.GoldBright);

                UIKit.Emblem(new Rect(r.x + 16, r.y + 22, 56, 56), StepNumerals[i], cur ? UITheme.GoldBright : UITheme.GoldDim, cur);
                UIKit.Text(new Rect(r.x + 88, r.y + 18, w - 100, 34), StepNames[i], 24, cur ? UITheme.GoldBright : Color.Lerp(UITheme.BoneDim, UITheme.Bone, hv), serif: true, bold: true);
                UIKit.Text(new Rect(r.x + 88, r.y + 54, w - 100, 26), StepHints[i], 15, UITheme.BoneDim);
                if (done) UIKit.Tex(new Rect(r.xMax - 30, r.y + 14, 16, 16), UITheme.Diamond, UITheme.Good);

                if (click) GoTo(i);
            }
        }

        // ------------------------------------------------------------------ 오른쪽 헌터 기록서
        void DrawSheet(GameManager gm)
        {
            var r = new Rect(W - 470, 200, 430, 760);
            UIKit.Panel(r, "사냥꾼 수첩");

            var s = gm.PreviewStats();
            CharacterStats p = null;
            if (hoverStat.HasValue && gm.StartPointsLeft > 0)
            {
                p = gm.PreviewStats();
                p.Allocate(hoverStat.Value);
            }
            else if (hoverTrait.HasValue && hoverTrait.Value != gm.StartTrait)
            {
                p = new CharacterStats(gm.Character);
                foreach (var t in CharacterStats.All)
                    for (int k = 0; k < gm.StartAllocation[(int)t]; k++) p.Allocate(t);
                p.traits.Add(hoverTrait.Value);
            }

            float y = r.y + 64;
            UIKit.Text(new Rect(r.x + 24, y, r.width - 48, 30), $"{gm.Character.displayName}  <size=16><color=#{UIKit.Hex(UITheme.BoneDim)}>· 1단계 · 홀로 남은 자</color></size>", 26, UITheme.Bone, serif: true, bold: true);
            y += 34;

            // 6각형 차트
            var vis = CharacterStats.Visible;
            var vals = new float[vis.Length];
            float[] prev = p != null ? new float[vis.Length] : null;
            var labels = new string[vis.Length];
            for (int i = 0; i < vis.Length; i++)
            {
                var t = vis[i];
                vals[i] = s.Get(t) / 16f;
                if (p != null) prev[i] = p.Get(t) / 16f;
                labels[i] = GameText.StatGlyph(t);
            }
            radar.Draw(new Rect(r.center.x - 115, y + 26, 230, 230), vals, prev, labels);
            y += 290;

            UIKit.Divider(new Rect(r.x + 30, y, r.width - 60, 10));
            y += 18;

            Line(r, ref y, "최대 생명", s.MaxHp, p?.MaxHp, "0");
            Line(r, ref y, "이동 속도", s.MoveSpeed, p?.MoveSpeed, "0.00");
            Line(r, ref y, "치명타", s.CritChance * 100f, p != null ? p.CritChance * 100f : (float?)null, "0.0", "%");
            Line(r, ref y, "약점 추가 피해", s.WeakPointBonus * 100f, p != null ? p.WeakPointBonus * 100f : (float?)null, "0", "%");
            Line(r, ref y, "반동 안정", 1f / s.RecoilNoise, p != null ? 1f / p.RecoilNoise : (float?)null, "0.00", "×");
            Line(r, ref y, "재장전 속도", s.ReloadSpeedMult, p?.ReloadSpeedMult, "0.00", "×");

            float weight = DefaultContent.Pistol.weight;
            foreach (var wd in gm.SelectedPrimaries) weight += wd.weight;
            float cap = (p ?? s).CarryCapacity;
            Color wc = weight > cap ? UITheme.Warn : UITheme.BoneDim;
            UIKit.Text(new Rect(r.x + 28, y, 200, 26), "무장 무게", 17, UITheme.BoneDim);
            UIKit.Text(new Rect(r.x + 200, y, r.width - 228, 26), $"{weight:0.0} / {cap:0.0}", 17, wc, TextAnchor.MiddleRight, bold: true);
            y += 34;

            UIKit.Divider(new Rect(r.x + 30, y, r.width - 60, 10));
            y += 18;
            UIKit.Text(new Rect(r.x + 28, y, r.width - 56, 26), $"기질  <color=#{UIKit.Hex(UITheme.GoldBright)}>{Traits.Name(gm.StartTrait)}</color>", 18, UITheme.BoneDim, serif: true);
            y += 28;
            string arms = "";
            foreach (var wd in gm.SelectedPrimaries) arms += (arms.Length > 0 ? " · " : "") + wd.displayName;
            UIKit.Text(new Rect(r.x + 28, y, r.width - 56, 26), $"무장  <color=#{UIKit.Hex(UITheme.Bone)}>{arms}</color>", 18, UITheme.BoneDim, serif: true);
        }

        void Line(Rect r, ref float y, string label, float v, float? pv, string fmt, string suffix = "")
        {
            UIKit.Text(new Rect(r.x + 28, y, 200, 26), label, 17, UITheme.BoneDim);
            string val = v.ToString(fmt) + suffix;
            if (pv.HasValue && Mathf.Abs(pv.Value - v) > 0.0001f)
            {
                float d = pv.Value - v;
                string col = UIKit.Hex(d > 0 ? UITheme.Good : UITheme.BloodBright);
                val = $"{pv.Value.ToString(fmt)}{suffix}  <size=14><color=#{col}>({(d > 0 ? "+" : "")}{d.ToString(fmt)})</color></size>";
            }
            UIKit.Text(new Rect(r.x + 200, y, r.width - 228, 26), val, 17, UITheme.Bone, TextAnchor.MiddleRight, bold: true);
            y += 30;
        }

        // ------------------------------------------------------------------ Ⅰ 능력치 각인
        void DrawStats(GameManager gm, Rect c)
        {
            UIKit.Panel(c, "하나 · 몸 만들기");
            float y = c.y + 70;

            // 남은 각인 포인트
            int total = gm.Character.startingStatPoints, left = gm.StartPointsLeft;
            UIKit.Text(new Rect(c.x + 30, y, 300, 34), "남은 단련", 20, UITheme.BoneDim, serif: true);
            for (int i = 0; i < total; i++)
            {
                var pr = new Rect(c.x + 140 + i * 30, y + 6, 22, 22);
                if (i < total - left) UIKit.Tex(pr, UITheme.Diamond, UITheme.BloodBright);
                else
                {
                    UIKit.Tex(new Rect(pr.x - 5, pr.y - 5, 32, 32), UITheme.Glow, new Color(1f, 0.8f, 0.4f, 0.35f + 0.2f * Mathf.Sin(Time.unscaledTime * 4f + i)));
                    UIKit.Tex(pr, UITheme.DiamondHollow, UITheme.GoldBright);
                }
            }
            UIKit.Text(new Rect(c.x + 140 + total * 30 + 10, y, 120, 34), $"{left} / {total}", 20, left > 0 ? UITheme.GoldBright : UITheme.Good, bold: true);

            // 추천 각인
            float bw = 118, bx = c.xMax - 30 - (GameText.Presets.Length + 1) * (bw + 8) + 8;
            for (int i = 0; i < GameText.Presets.Length; i++)
            {
                var pr = GameText.Presets[i];
                var br = new Rect(bx + i * (bw + 8), y - 4, bw, 42);
                if (UIKit.Button(br, pr.name, 18)) gm.ApplyStartPreset(pr.alloc);
                UIKit.TooltipIf(br, $"<b>추천: {pr.name}</b>\n{pr.desc}");
            }
            if (UIKit.Button(new Rect(bx + GameText.Presets.Length * (bw + 8), y - 4, bw, 42), "초기화", 18)) gm.ResetStartAllocation();
            y += 56;

            var s = gm.PreviewStats();
            float rowH = 88;
            foreach (var t in CharacterStats.Visible)
            {
                var row = new Rect(c.x + 20, y, c.width - 40, rowH - 8);
                float hv = UIKit.Hover(row, "statrow" + t, true, false);
                UIKit.Fill(row, new Color(1f, 0.8f, 0.5f, 0.03f + hv * 0.05f));
                UIKit.Tex(new Rect(row.x, row.yMax - 2, row.width, 2), UITheme.LineFade, new Color(0.5f, 0.38f, 0.2f, 0.4f));

                if (gm.StartAllocation[(int)t] > 0) UIKit.Fill(new Rect(row.x, row.y + 8, 3, row.height - 16), UITheme.GoldBright);
                UIKit.Text(new Rect(row.x + 18, row.y + 8, 180, 34), GameText.StatName(t), 26, UITheme.Bone, serif: true, bold: true);
                UIKit.Text(new Rect(row.x + 18, row.y + 44, 300, 24), GameText.StatShort(t), 15, UITheme.BoneDim);

                int baseVal = Mathf.RoundToInt(gm.Character.baseStats.Get(t));
                int alloc = gm.StartAllocation[(int)t];
                int preview = 0;

                // − · +1 · 전부 (먼저 처리해서 미리보기 표시)
                var all = new Rect(row.xMax - 70, row.y + 22, 62, 38);
                var plus = new Rect(all.x - 52, row.y + 22, 46, 38);
                var minus = new Rect(plus.x - 52, row.y + 22, 46, 38);
                if (UIKit.SmallButton(minus, "−", alloc > 0, 20)) gm.AdjustStartStat(t, -1);
                if (UIKit.SmallButton(plus, "+1", left > 0, 18)) gm.AdjustStartStat(t, +1);
                if (UIKit.SmallButton(all, "전부", left > 0, 16)) { int n = gm.StartPointsLeft; for (int k = 0; k < n; k++) gm.AdjustStartStat(t, +1); }
                if (plus.Contains(UIKit.Mouse) && left > 0) { hoverStat = t; preview = 1; }
                if (all.Contains(UIKit.Mouse) && left > 0) { hoverStat = t; preview = left; }

                float pipsX = row.x + 330, pipsW = minus.x - 100 - pipsX;
                UIKit.StatCells(new Rect(pipsX, row.y + 18, pipsW, 18), 20, baseVal, alloc, preview);
                UIKit.Text(new Rect(pipsX, row.y + 44, pipsW + 90, 24), GameText.StatEffects(t, s), 15, UITheme.BoneDim);

                float v = s.Get(t);
                UIKit.Text(new Rect(minus.x - 92, row.y + 10, 80, 60), v.ToString("0") + (preview > 0 ? $"<size=18><color=#{UIKit.Hex(UITheme.Good)}> +{preview}</color></size>" : ""),
                    38, alloc > 0 ? UITheme.GoldBright : UITheme.Bone, TextAnchor.MiddleRight, serif: true, bold: true);

                UIKit.TooltipIf(new Rect(row.x, row.y, 340, row.height), GameText.StatTooltip(t));
                y += rowH;
            }
        }

        // ------------------------------------------------------------------ Ⅱ 특성 계약
        void DrawTraits(GameManager gm, Rect c)
        {
            UIKit.Panel(c, "둘 · 기질 고르기");
            UIKit.Text(new Rect(c.x + 30, c.y + 66, c.width - 60, 30),
                $"하나를 고른다. 이후 {Traits.PickEveryLevels}레벨마다 셋 중 하나를 더 얻는다.", 17, UITheme.BoneDim, TextAnchor.MiddleCenter);

            var all = Traits.All;
            int cols = 4;
            float gap = 14;
            float cw = (c.width - 40 - gap * (cols - 1)) / cols;
            int rows = (all.Length + cols - 1) / cols;
            // 기질이 많아 세 줄이 되면 칸을 줄이고 짧은 글만 (패널 밖으로 넘치지 않게)
            float ch = Mathf.Min(270f, (c.height - 124f - gap * (rows - 1)) / rows);
            bool compact = ch < 240f;
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                int cx = i % cols, cy = i / cols;
                var r = new Rect(c.x + 20 + cx * (cw + gap), c.y + 110 + cy * (ch + gap), cw, ch);
                bool sel = gm.StartTrait == t;
                bool click = UIKit.HitArea(r, "trait" + t, out float hv);
                if (hv > 0.5f) hoverTrait = t;
                r.y -= hv * 5f;

                UIKit.NineSlice(r, sel ? UITheme.PanelHeader : UITheme.Panel, 12, Color.white);
                if (sel || hv > 0.01f)
                    UIKit.NineSlice(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), UITheme.Frame, 8, sel ? UITheme.GoldBright : new Color(1f, 0.8f, 0.5f, hv * 0.7f));

                if (compact)
                {
                    UIKit.Emblem(new Rect(r.center.x - 23, r.y + 10, 46, 46), GameText.TraitGlyph(t), sel ? UITheme.GoldBright : UITheme.Gold, sel);
                    UIKit.Text(new Rect(r.x + 10, r.y + 60, r.width - 20, 28), Traits.Name(t), 20, sel ? UITheme.GoldBright : UITheme.Bone, TextAnchor.MiddleCenter, serif: true, bold: true);
                    UIKit.Text(new Rect(r.x + 12, r.y + 90, r.width - 24, r.height - 96), Traits.Describe(t), 14, UITheme.Bone, TextAnchor.UpperCenter, wrap: true);
                    UIKit.TooltipIf(r, GameText.TraitFlavor(t));
                }
                else
                {
                    UIKit.Emblem(new Rect(r.center.x - 34, r.y + 20, 68, 68), GameText.TraitGlyph(t), sel ? UITheme.GoldBright : UITheme.Gold, sel);
                    UIKit.Text(new Rect(r.x + 10, r.y + 98, r.width - 20, 34), Traits.Name(t), 24, sel ? UITheme.GoldBright : UITheme.Bone, TextAnchor.MiddleCenter, serif: true, bold: true);
                    UIKit.Text(new Rect(r.x + 16, r.y + 136, r.width - 32, 50), Traits.Describe(t), 16, UITheme.Bone, TextAnchor.UpperCenter, wrap: true);
                    UIKit.Text(new Rect(r.x + 16, r.y + 194, r.width - 32, 64), GameText.TraitFlavor(t), 14, UITheme.BoneDim, TextAnchor.UpperCenter, serif: true, wrap: true);
                }
                if (sel)
                {
                    var seal = new Rect(r.xMax - 64, r.y + 12, 52, 52);
                    UIKit.Tex(seal, UITheme.Seal, UITheme.Blood);
                    UIKit.Text(seal, "택", 26, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true);
                }
                if (click) gm.StartTrait = t;
            }
        }

        // ------------------------------------------------------------------ Ⅲ 무장 선택
        void DrawWeapons(GameManager gm, Rect c)
        {
            UIKit.Panel(c, "셋 · 무기 챙기기");
            int count = gm.SelectedPrimaries.Count;
            UIKit.Text(new Rect(c.x + 30, c.y + 66, c.width - 60, 30),
                $"주무기 <color=#{UIKit.Hex(count == GameManager.MaxPrimaries ? UITheme.Good : UITheme.GoldBright)}>{count} / {GameManager.MaxPrimaries}</color>  ·  권총(P9)은 기본 지급", 18, UITheme.BoneDim, TextAnchor.MiddleCenter);

            var weapons = DefaultContent.Weapons;
            float y = c.y + 106;
            float rowH = (c.height - 180) / (weapons.Count - 1);
            for (int i = 1; i < weapons.Count; i++)
            {
                var w = weapons[i];
                var r = new Rect(c.x + 20, y, c.width - 40, rowH - 8);
                bool sel = gm.SelectedPrimaries.Contains(w);
                bool full = !sel && count >= GameManager.MaxPrimaries;
                bool click = UIKit.HitArea(r, "weapon" + w.id, out float hv);

                UIKit.NineSlice(r, sel ? UITheme.PanelHeader : UITheme.Slot, 10, new Color(1, 1, 1, full ? 0.6f : 1f));
                if (sel || hv > 0.01f)
                    UIKit.NineSlice(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), UITheme.Frame, 8, sel ? UITheme.GoldBright : new Color(1f, 0.8f, 0.5f, hv * 0.6f));

                // 선택 표시
                var mark = new Rect(r.x + 16, r.center.y - 14, 28, 28);
                UIKit.Tex(mark, sel ? UITheme.Diamond : UITheme.DiamondHollow, sel ? UITheme.GoldBright : UITheme.GoldDim);

                Color elem = CombatNames.ColorOf(w.damageType);
                UIKit.Text(new Rect(r.x + 60, r.y + 8, 300, 32), w.displayName, 24, sel ? UITheme.GoldBright : UITheme.Bone, serif: true, bold: true);
                UIKit.Text(new Rect(r.x + 60, r.y + 40, 300, 22),
                    $"{GameText.WeaponClassName(w.weaponClass)}  ·  <color=#{UIKit.Hex(elem)}>{GameText.ElementName(w.damageType)}</color>  ·  무게 {w.weight}", 15, UITheme.BoneDim);
                UIKit.Text(new Rect(r.x + 60, r.y + 62, 330, 22), GameText.WeaponFlavor(w), 14, UITheme.BoneDim, serif: true);

                // 성능 막대
                float dps = w.damage * w.pellets * w.rpm / 60f;
                float bx = r.x + 410, bw = (r.width - 430) / 3f - 14;
                StatBar(new Rect(bx, r.y + 12, bw, 14), "위력", w.damage * w.pellets / 160f);
                StatBar(new Rect(bx + bw + 14, r.y + 12, bw, 14), "초당 피해", dps / 400f);
                StatBar(new Rect(bx + (bw + 14) * 2, r.y + 12, bw, 14), "장탄", w.magazine / 100f);
                StatBar(new Rect(bx, r.y + 52, bw, 14), "안정", 1f - Mathf.Clamp01(w.recoilVertical / 6.5f));
                StatBar(new Rect(bx + bw + 14, r.y + 52, bw, 14), "사거리", Mathf.Sqrt(Mathf.Clamp01(w.falloffEnd / 200f)));
                StatBar(new Rect(bx + (bw + 14) * 2, r.y + 52, bw, 14), "저지력", Mathf.Sqrt(Mathf.Clamp01(w.knockback / 9f)));

                if (click && !full) gm.ToggleWeapon(w);
                else if (click && full) Hunt.UI.HUD.Notify("주무기는 둘까지. 먼저 하나를 내려놓아라.", UITheme.Warn);
                y += rowH;
            }

            // 무게
            var s = gm.PreviewStats();
            float weight = DefaultContent.Pistol.weight;
            foreach (var wd in gm.SelectedPrimaries) weight += wd.weight;
            float pen = s.WeightPenalty(weight);
            var wr = new Rect(c.x + 160, c.yMax - 54, c.width - 320, 18);
            UIKit.Text(new Rect(c.x + 30, wr.y - 6, 130, 30), "무장 무게", 18, UITheme.BoneDim, serif: true);
            UIKit.Bar(wr, weight / Mathf.Max(1f, s.CarryCapacity * 1.5f), pen < 1f ? UITheme.Warn : UITheme.Gold, $"{weight:0.0} / 한도 {s.CarryCapacity:0.0}", -1f, 14);
            UIKit.Tex(new Rect(wr.x + wr.width / 1.5f - 1, wr.y - 4, 2, wr.height + 8), UITheme.Pixel, UITheme.GoldBright);
            if (pen < 1f)
                UIKit.Text(new Rect(wr.xMax + 10, wr.y - 6, 150, 30), $"과적 −{Mathf.RoundToInt((1 - pen) * 100)}%", 17, UITheme.Warn, bold: true);
        }

        static void StatBar(Rect r, string label, float t)
        {
            UIKit.Text(new Rect(r.x, r.y - 2, r.width, 18), label, 13, UITheme.BoneDim);
            var b = new Rect(r.x, r.y + 18, r.width, 8);
            UIKit.Fill(b, new Color(0, 0, 0, 0.6f));
            UIKit.Tex(new Rect(b.x, b.y, b.width * Mathf.Clamp01(t), b.height), UITheme.BarFill, Color.Lerp(UITheme.Blood, UITheme.Gold, Mathf.Clamp01(t)));
        }

        // ------------------------------------------------------------------ Ⅳ 출정
        void DrawDeploy(GameManager gm, Rect c)
        {
            UIKit.Panel(c, "넷 · 길 나서기");
            float y = c.y + 90;
            UIKit.Text(new Rect(c.x, y, c.width, 50), "준비는 끝났다.", 40, UITheme.Bone, TextAnchor.MiddleCenter, serif: true, bold: true);
            y += 56;
            UIKit.Text(new Rect(c.x + 60, y, c.width - 120, 60),
                "비가 그치지 않는 마을. 무덤에서 나온 것들이 이웃의 얼굴로 걸어온다.\n해가 지면 그것들이 온다. 살아서 아침을 맞아라.", 18, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true, wrap: true);
            y += 90;
            UIKit.Divider(new Rect(c.center.x - 260, y, 520, 12));
            y += 34;

            var s = gm.PreviewStats();
            string stats = "";
            foreach (var t in CharacterStats.Visible)
            {
                int a = gm.StartAllocation[(int)t];
                stats += $"{GameText.StatName(t)} <color=#{UIKit.Hex(a > 0 ? UITheme.GoldBright : UITheme.Bone)}>{s.Get(t):0}</color>     ";
            }
            UIKit.Text(new Rect(c.x, y, c.width, 30), stats, 19, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            y += 44;
            UIKit.Text(new Rect(c.x, y, c.width, 30), $"기질 — <color=#{UIKit.Hex(UITheme.GoldBright)}>{Traits.Name(gm.StartTrait)}</color> : {Traits.Describe(gm.StartTrait)}", 19, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            y += 40;
            string arms = "";
            foreach (var wd in gm.SelectedPrimaries) arms += $"{wd.displayName}  ·  ";
            arms += DefaultContent.Pistol.displayName;
            UIKit.Text(new Rect(c.x, y, c.width, 30), $"무장 — <color=#{UIKit.Hex(UITheme.Bone)}>{arms}</color>", 19, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            y += 70;

            bool ready = gm.StartPointsLeft == 0;
            var br = new Rect(c.center.x - 170, y, 340, 84);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.5f);
            if (ready) UIKit.Tex(new Rect(br.x - 60, br.y - 40, br.width + 120, br.height + 80), UITheme.Glow, new Color(0.9f, 0.1f, 0.08f, 0.25f + 0.2f * pulse));
            if (UIKit.Button(br, "길 나서기", 36, false, ready, ready ? "Enter" : "단련 포인트를 모두 써라")) TryDeploy(gm);
        }

        void TryDeploy(GameManager gm)
        {
            if (gm.StartPointsLeft > 0)
            {
                Hunt.UI.HUD.Notify("단련 포인트를 모두 써야 길을 나설 수 있다.", UITheme.Warn);
                GoTo(0);
                return;
            }
            gm.StartGame();
        }

        // ------------------------------------------------------------------ 아래 버튼
        void DrawFooter(GameManager gm, Rect c)
        {
            float y = c.yMax + 16;
            if (step > 0 && UIKit.Button(new Rect(c.x, y, 200, 54), "◀  이전", 22)) GoTo(step - 1);
            if (step < 3)
            {
                bool done = StepDone(gm, step);
                if (UIKit.Button(new Rect(c.xMax - 220, y, 220, 54), "다음  ▶", 22, false, true, done ? null : StepWarn(step)))
                    GoTo(step + 1);
            }
            UIKit.Text(new Rect(c.x + 220, y, c.width - 460, 54),
                "WASD 이동 · Shift 달리기 · Ctrl 앉기(달리며 미끄러지기) · 좌클릭 사격 · 우클릭 조준 · R 장전 · F 손전등\nE 줍기·상자(없으면 재생) · M 지도 · G 수류탄 · V 칼 · Q 광폭화 · Tab 서낭당 · Esc 일시정지", 14, UITheme.Ash, TextAnchor.MiddleCenter);
            // 빠른 출정: 지난 판 설정 그대로(포인트를 다 썼으면), 처음이면 기본 설정으로
            if (step == 0)
            {
                bool prev = gm.StartPointsLeft == 0;
                if (UIKit.Button(new Rect(c.xMax - 220, y - 70, 220, 54), "빠른 출정", 20, false, true, prev ? "지난 설정 그대로" : "기본 설정으로"))
                {
                    if (!prev) gm.ApplyStartPreset(Hunt.UI.GameText.Presets[0].alloc);
                    TryDeploy(gm);
                }
            }
            UIKit.Text(new Rect(0, H - 30, W - 30, 24), $"그래픽 품질: {QualityManager.Name(QualityManager.Level)}{(QualityManager.AutoDetected ? " (내 PC에 맞춰 자동)" : "")}  ·  게임 중 Esc에서 바꿀 수 있다", 13, UITheme.Ash, TextAnchor.MiddleRight);
        }

        static string StepWarn(int s)
        {
            switch (s)
            {
                case 0: return "단련 포인트가 남았다";
                case 2: return "주무기를 더 고를 수 있다";
                default: return null;
            }
        }
    }
}

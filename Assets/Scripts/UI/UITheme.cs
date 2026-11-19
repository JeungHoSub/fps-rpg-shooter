using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// 한국풍 테마: 먹(검정) 바탕 + 한지(바랜 미색) 글씨·선 + 낙관(도장)의 붉은색만 포인트로.
    /// 제목은 궁서체, 장식은 창살 무늬와 먹점. 빨강·금 조합과 한자 장식은 쓰지 않는다.
    /// 이름(Gold, Blood…)은 기존 코드 호환을 위해 유지하고 값만 한지·낙관 색으로 바꿨다.
    /// 모든 텍스처는 코드로 생성한다.
    /// </summary>
    public static class UITheme
    {
        // ---- 팔레트 ----
        public static readonly Color Ink = new Color(0.05f, 0.05f, 0.055f);          // 먹
        public static readonly Color Gold = new Color(0.84f, 0.79f, 0.68f);          // 한지
        public static readonly Color GoldDim = new Color(0.46f, 0.43f, 0.38f);       // 바랜 한지
        public static readonly Color GoldBright = new Color(0.97f, 0.94f, 0.86f);    // 새 한지
        public static readonly Color Blood = new Color(0.6f, 0.13f, 0.1f);           // 낙관 인주
        public static readonly Color BloodBright = new Color(0.85f, 0.24f, 0.17f);
        public static readonly Color BloodDark = new Color(0.24f, 0.05f, 0.04f);
        public static readonly Color Bone = new Color(0.9f, 0.87f, 0.8f);
        public static readonly Color BoneDim = new Color(0.6f, 0.58f, 0.54f);
        public static readonly Color Ash = new Color(0.4f, 0.39f, 0.37f);
        public static readonly Color Indigo = new Color(0.2f, 0.26f, 0.36f);         // 쪽빛
        public static readonly Color Infect = new Color(0.5f, 0.18f, 0.7f);
        public static readonly Color InfectBright = new Color(0.82f, 0.45f, 1f);
        public static readonly Color Good = new Color(0.55f, 0.88f, 0.45f);
        public static readonly Color Warn = new Color(1f, 0.55f, 0.25f);

        public static Font Serif { get; private set; }
        public static Font Sans { get; private set; }

        public static Texture2D Pixel, Panel, PanelHeader, Frame, Button, ButtonHover, ButtonPress, ButtonSelected,
            Slot, Diamond, DiamondHollow, Circle, Orb, OrbRing, Vignette, Glow, BarFill, LineFade,
            Lattice, Brush, Seal, Paper;

        static readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();
        static bool ready;

        public static void Ensure()
        {
            if (ready && Panel != null) return;
            ready = true;
            styles.Clear();

            // 제목: 궁서체 (윈도우 기본 붓글씨체) → 없으면 바탕
            // 휴대폰에는 한글 OS 글꼴이 보장되지 않으므로 게임에 넣은 나눔 글꼴을 먼저 쓴다 (OFL)
            var bundledSerif = Resources.Load<Font>("Fonts/NanumMyeongjo-Bold");
            var bundledSans = Resources.Load<Font>("Fonts/NanumGothic-Regular");
            Serif = Application.isMobilePlatform && bundledSerif != null ? bundledSerif : TryFont(new[] { "Gungsuh", "궁서", "GungsuhChe", "궁서체", "Batang", "바탕", "Nanum Myeongjo", "Noto Serif KR", "Noto Serif CJK KR", "Malgun Gothic" }, 28);
            Sans = Application.isMobilePlatform && bundledSans != null ? bundledSans : TryFont(new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans KR", "Noto Sans CJK KR", "Apple SD Gothic Neo", "Arial" }, 20);

            Pixel = Make(2, 2, (x, y) => Color.white);
            var rnd = new System.Random(7);
            // 먹 바탕: 한지 결이 살짝 비치는 검정 + 두 줄 한지색 테두리 (모서리는 각지게)
            Panel = Framed(64, new Color(0.075f, 0.072f, 0.075f, 0.93f), new Color(0.045f, 0.043f, 0.047f, 0.93f), GoldDim, new Color(0.32f, 0.3f, 0.27f, 0.9f), 0.025f, rnd);
            PanelHeader = Framed(64, new Color(0.13f, 0.125f, 0.12f, 0.96f), new Color(0.09f, 0.087f, 0.085f, 0.96f), Gold, Color.clear, 0.02f, rnd);
            Frame = Framed(32, Color.clear, Color.clear, Color.white, Color.clear, 0f, rnd);
            Button = Framed(32, new Color(0.11f, 0.105f, 0.11f, 0.95f), new Color(0.07f, 0.068f, 0.072f, 0.95f), GoldDim, Color.clear, 0.02f, rnd);
            ButtonHover = Framed(32, new Color(0.22f, 0.21f, 0.2f, 0.97f), new Color(0.14f, 0.135f, 0.13f, 0.97f), Gold, Color.clear, 0.02f, rnd);
            ButtonPress = Framed(32, new Color(0.06f, 0.06f, 0.065f, 1f), new Color(0.1f, 0.1f, 0.1f, 1f), GoldBright, Color.clear, 0f, rnd);
            // 선택됨: 한지 바탕에 먹 테두리 (색이 뒤집힌다)
            ButtonSelected = Framed(32, new Color(0.9f, 0.86f, 0.76f, 0.98f), new Color(0.8f, 0.76f, 0.66f, 0.98f), new Color(0.15f, 0.13f, 0.12f), Color.clear, 0.04f, rnd);
            Slot = Framed(32, new Color(0.05f, 0.05f, 0.055f, 0.92f), new Color(0.09f, 0.088f, 0.09f, 0.92f), new Color(0.26f, 0.25f, 0.23f), Color.clear, 0.02f, rnd);

            // 먹점 / 고리
            Diamond = Make(32, 32, (x, y) => A(Color.white, Mathf.Clamp01(13.5f - Dist(x, y, 15.5f, 15.5f))));
            DiamondHollow = Make(32, 32, (x, y) => A(Color.white, Mathf.Clamp01(1.4f - Mathf.Abs(Dist(x, y, 15.5f, 15.5f) - 12f))));

            // 창살 무늬 (띠살문): 가로세로 살이 엇갈린 격자
            Lattice = Make(48, 48, (x, y) =>
            {
                bool border = x < 3 || y < 3 || x > 44 || y > 44;
                bool v = (x % 12) < 2;
                bool h = (y % 16) < 2 && (y / 16) % 1 == 0;
                return border || v || h ? Color.white : Color.clear;
            });

            // 붓질: 가로로 긴 먹 자국 (가장자리가 거칠게 번진다)
            Brush = Make(256, 64, (x, y) =>
            {
                float t = x / 255f;
                float half = 22f * Mathf.Sin(Mathf.Clamp01(t * 1.05f) * Mathf.PI) + 6f * Mathf.PerlinNoise(x * 0.05f, 3.1f);
                float d = Mathf.Abs(y - 31.5f - 5f * Mathf.Sin(t * 5f));
                float edge = Mathf.Clamp01((half - d) / 3f);
                float streak = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.02f, y * 0.6f);
                float dry = t > 0.82f ? Mathf.Clamp01((Mathf.PerlinNoise(x * 0.3f, y * 0.4f) - (t - 0.82f) * 3f) * 2f) : 1f; // 끝이 갈라짐
                return new Color(1f, 1f, 1f, edge * streak * dry);
            });

            // 낙관: 붉은 사각 도장 (테두리가 살짝 뭉개짐)
            Seal = Make(64, 64, (x, y) =>
            {
                float n = Mathf.PerlinNoise(x * 0.25f, y * 0.25f);
                bool outer = x > 2 && y > 2 && x < 61 && y < 61;
                bool rim = outer && (x < 8 || y < 8 || x > 55 || y > 55);
                float a = outer ? (rim ? 1f : 0.88f) : 0f;
                if (n < 0.28f) a *= 0.35f; // 인주가 덜 묻은 곳
                return new Color(1f, 1f, 1f, a);
            });

            // 한지 결 (아주 옅게 깔리는 섬유 무늬)
            Paper = Make(128, 128, (x, y) =>
            {
                float f = Mathf.PerlinNoise(x * 0.08f, y * 0.5f) * 0.6f + Mathf.PerlinNoise(x * 0.4f, y * 0.05f) * 0.4f;
                return new Color(1f, 1f, 1f, f * 0.08f);
            });

            Circle = Make(128, 128, (x, y) => A(Color.white, Mathf.Clamp01((63.5f - Dist(x, y, 63.5f, 63.5f)) * 0.8f)));
            Orb = Make(128, 128, (x, y) =>
            {
                float d = Dist(x, y, 63.5f, 63.5f) / 63.5f;
                float shade = Mathf.Lerp(1f, 0.35f, d * d);
                float hl = Mathf.Clamp01(1f - Dist(x, y, 45f, 88f) / 30f) * 0.5f; // 왼쪽 위 하이라이트
                float a = Mathf.Clamp01((1f - d) * 64f);
                float v = Mathf.Clamp01(shade + hl);
                return new Color(v, v, v, a);
            });
            OrbRing = Make(128, 128, (x, y) =>
            {
                float d = Dist(x, y, 63.5f, 63.5f);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 59f) / 4.5f);
                float inner = Mathf.Clamp01(1f - Mathf.Abs(d - 53.5f) / 1.2f) * 0.6f;
                float shine = 0.75f + 0.25f * Mathf.Sin(Mathf.Atan2(y - 63.5f, x - 63.5f) * 2f + 1f);
                Color c = Color.Lerp(GoldDim, GoldBright, shine);
                return new Color(c.r, c.g, c.b, Mathf.Max(ring, inner));
            });
            Vignette = Make(128, 128, (x, y) =>
            {
                float d = Dist(x, y, 63.5f, 63.5f) / 63.5f;
                return new Color(0, 0, 0, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.15f, d)));
            });
            Glow = Make(64, 64, (x, y) =>
            {
                float d = Mathf.Clamp01(Dist(x, y, 31.5f, 31.5f) / 31.5f);
                return A(Color.white, (1f - d) * (1f - d));
            });
            BarFill = Make(4, 32, (x, y) =>
            {
                float t = y / 31f;
                float v = Mathf.Lerp(0.62f, 1f, t) + (t > 0.78f ? 0.15f : 0f);
                return new Color(v, v, v, 1f);
            });
            LineFade = Make(128, 4, (x, y) =>
            {
                float t = Mathf.Abs(x - 63.5f) / 63.5f;
                return A(Color.white, Mathf.Clamp01(1f - t * t) * (y == 0 || y == 3 ? 0.4f : 1f));
            });
        }

        static Font TryFont(string[] names, int size)
        {
            try
            {
                var f = Font.CreateDynamicFontFromOSFont(names, size);
                if (f != null) return f;
            }
            catch (Exception) { /* 폰트 없음 → 기본 폰트 */ }
            return null;
        }

        /// <summary>
        /// 모서리를 사선으로 깎은 패널 텍스처. fillTop/fillBottom 세로 그라데이션 + 금빛 외곽선 + 안쪽 보조선.
        /// 9-slice(border)로 늘려 쓴다.
        /// </summary>
        static Texture2D Chamfered(int n, int cut, Color fillTop, Color fillBottom, Color border, Color innerLine)
        {
            bool Inside(int x, int y, int inset)
            {
                int lo = inset, hi = n - 1 - inset;
                if (x < lo || y < lo || x > hi || y > hi) return false;
                int c = cut;
                if (x - lo + y - lo < c) return false;
                if (hi - x + y - lo < c) return false;
                if (x - lo + hi - y < c) return false;
                if (hi - x + hi - y < c) return false;
                return true;
            }

            return Make(n, n, (x, y) =>
            {
                if (!Inside(x, y, 0)) return Color.clear;
                if (!Inside(x, y, 2)) return border;
                if (innerLine.a > 0f && Inside(x, y, 4) && !Inside(x, y, 5)) return innerLine;
                return Color.Lerp(fillBottom, fillTop, y / (float)(n - 1));
            });
        }

        /// <summary>각진 사각 패널: 세로 그라데이션 + 한지 결 + 1px 외곽선 + 안쪽 보조선(두 줄 테두리).</summary>
        static Texture2D Framed(int n, Color fillTop, Color fillBottom, Color border, Color innerLine, float grain, System.Random rnd)
        {
            return Make(n, n, (x, y) =>
            {
                if (x == 0 || y == 0 || x == n - 1 || y == n - 1) return border;
                if (innerLine.a > 0f && (x == 3 || y == 3 || x == n - 4 || y == n - 4) && x >= 3 && y >= 3 && x <= n - 4 && y <= n - 4) return innerLine;
                var c = Color.Lerp(fillBottom, fillTop, y / (float)(n - 1));
                if (c.a > 0f && grain > 0f) c *= 1f + ((float)rnd.NextDouble() - 0.5f) * grain * 2f;
                return c;
            });
        }

        static Texture2D Make(int w, int h, Func<int, int, Color> f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f(x, y);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static float Dist(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        /// <summary>흰색 글자 스타일 (색은 GUI.color로 입힌다). 크기·정렬·글꼴별로 캐시.</summary>
        public static GUIStyle Text(int size, TextAnchor anchor, bool serif, bool bold, bool wrap)
        {
            string key = $"{size}|{(int)anchor}|{serif}|{bold}|{wrap}";
            if (styles.TryGetValue(key, out var s)) return s;
            s = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = anchor,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = wrap,
                richText = true,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            var font = serif ? Serif : Sans;
            if (font != null) s.font = font;
            s.normal.textColor = Color.white;
            styles[key] = s;
            return s;
        }

        public static GUIStyle NineSlice(Texture2D tex, int border)
        {
            string key = "9|" + tex.GetInstanceID() + "|" + border;
            if (styles.TryGetValue(key, out var s)) return s;
            s = new GUIStyle { border = new RectOffset(border, border, border, border) };
            s.normal.background = tex;
            styles[key] = s;
            return s;
        }
    }
}

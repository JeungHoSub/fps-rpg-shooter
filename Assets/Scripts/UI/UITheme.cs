using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// 다크 판타지 호러 테마: 핏빛 검정 바탕, 금빛 테두리, 진홍 포인트, 세리프 제목.
    /// 모든 텍스처는 코드로 생성한다 (아트 에셋 없이 동작, 이후 이미지로 교체 가능).
    /// </summary>
    public static class UITheme
    {
        // ---- 팔레트 ----
        public static readonly Color Ink = new Color(0.03f, 0.02f, 0.025f);
        public static readonly Color Gold = new Color(0.86f, 0.68f, 0.36f);
        public static readonly Color GoldDim = new Color(0.52f, 0.39f, 0.2f);
        public static readonly Color GoldBright = new Color(1f, 0.87f, 0.58f);
        public static readonly Color Blood = new Color(0.62f, 0.06f, 0.08f);
        public static readonly Color BloodBright = new Color(0.92f, 0.16f, 0.16f);
        public static readonly Color BloodDark = new Color(0.24f, 0.02f, 0.04f);
        public static readonly Color Bone = new Color(0.92f, 0.87f, 0.77f);
        public static readonly Color BoneDim = new Color(0.64f, 0.58f, 0.5f);
        public static readonly Color Ash = new Color(0.42f, 0.38f, 0.36f);
        public static readonly Color Infect = new Color(0.5f, 0.18f, 0.7f);
        public static readonly Color InfectBright = new Color(0.82f, 0.45f, 1f);
        public static readonly Color Good = new Color(0.55f, 0.88f, 0.45f);
        public static readonly Color Warn = new Color(1f, 0.55f, 0.25f);

        public static Font Serif { get; private set; }
        public static Font Sans { get; private set; }

        public static Texture2D Pixel, Panel, PanelHeader, Frame, Button, ButtonHover, ButtonPress, ButtonSelected,
            Slot, Diamond, DiamondHollow, Circle, Orb, OrbRing, Vignette, Glow, BarFill, LineFade;

        static readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();
        static bool ready;

        public static void Ensure()
        {
            if (ready && Panel != null) return;
            ready = true;
            styles.Clear();

            Serif = TryFont(new[] { "Batang", "바탕", "BatangChe", "Gungsuh", "Noto Serif KR", "Noto Serif CJK KR", "Malgun Gothic" }, 28);
            Sans = TryFont(new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans KR", "Noto Sans CJK KR", "Apple SD Gothic Neo", "Arial" }, 20);

            Pixel = Make(2, 2, (x, y) => Color.white);
            Panel = Chamfered(48, 7, new Color(0.10f, 0.055f, 0.06f, 0.95f), new Color(0.045f, 0.03f, 0.035f, 0.95f), GoldDim, new Color(0.35f, 0.25f, 0.12f, 0.8f));
            PanelHeader = Chamfered(48, 7, new Color(0.32f, 0.04f, 0.06f, 0.97f), new Color(0.13f, 0.03f, 0.04f, 0.97f), Gold, new Color(0.6f, 0.45f, 0.2f, 0.6f));
            Frame = Chamfered(32, 5, Color.clear, Color.clear, Color.white, Color.clear);
            Button = Chamfered(32, 5, new Color(0.16f, 0.06f, 0.07f, 0.96f), new Color(0.07f, 0.035f, 0.04f, 0.96f), GoldDim, Color.clear);
            ButtonHover = Chamfered(32, 5, new Color(0.42f, 0.08f, 0.09f, 0.98f), new Color(0.18f, 0.04f, 0.05f, 0.98f), Gold, Color.clear);
            ButtonPress = Chamfered(32, 5, new Color(0.12f, 0.03f, 0.04f, 1f), new Color(0.3f, 0.05f, 0.06f, 1f), GoldBright, Color.clear);
            ButtonSelected = Chamfered(32, 5, new Color(0.5f, 0.36f, 0.14f, 0.98f), new Color(0.22f, 0.12f, 0.05f, 0.98f), GoldBright, Color.clear);
            Slot = Chamfered(32, 4, new Color(0.05f, 0.035f, 0.04f, 0.95f), new Color(0.11f, 0.07f, 0.07f, 0.95f), new Color(0.3f, 0.22f, 0.12f), Color.clear);

            Diamond = Make(32, 32, (x, y) => A(Color.white, 1f - Mathf.Clamp01((Mathf.Abs(x - 15.5f) + Mathf.Abs(y - 15.5f) - 14f))));
            DiamondHollow = Make(32, 32, (x, y) =>
            {
                float d = Mathf.Abs(x - 15.5f) + Mathf.Abs(y - 15.5f);
                return A(Color.white, Mathf.Clamp01(1f - Mathf.Abs(d - 12.5f) / 1.6f));
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

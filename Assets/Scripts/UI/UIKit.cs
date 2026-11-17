using System.Collections.Generic;
using Hunt.Core;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// 테마 위젯 모음 (IMGUI). 버튼 호버 애니메이션, 툴팁, 다이아몬드 눈금, 장식 구분선 등.
    /// 좌표는 HUD가 설정한 1080p 가상 해상도 기준.
    /// </summary>
    public static class UIKit
    {
        static readonly Dictionary<int, float> anim = new Dictionary<int, float>();
        static readonly HashSet<int> hoveredLastFrame = new HashSet<int>();
        static readonly HashSet<int> hoveredThisFrame = new HashSet<int>();
        static string tooltip;
        static Vector2 tooltipAt;

        public static bool IsRepaint => Event.current.type == EventType.Repaint;
        static float Dt => IsRepaint ? Mathf.Min(Time.unscaledDeltaTime, 0.05f) : 0f;
        public static Vector2 Mouse => Event.current.mousePosition;

        /// <summary>OnGUI 시작마다 호출.</summary>
        public static void BeginFrame()
        {
            tooltip = null;
            if (IsRepaint)
            {
                hoveredLastFrame.Clear();
                foreach (var id in hoveredThisFrame) hoveredLastFrame.Add(id);
                hoveredThisFrame.Clear();
            }
        }

        // ------------------------------------------------------------------ 기본 그리기
        public static void Fill(Rect r, Color c) => Tex(r, UITheme.Pixel, c);

        public static void Tex(Rect r, Texture tex, Color c)
        {
            if (!IsRepaint || tex == null) return;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, tex);
            GUI.color = old;
        }

        public static void NineSlice(Rect r, Texture2D tex, int border, Color tint)
        {
            if (!IsRepaint) return;
            var old = GUI.color;
            GUI.color = tint;
            UITheme.NineSlice(tex, border).Draw(r, false, false, false, false);
            GUI.color = old;
        }

        public static void Text(Rect r, string s, int size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft,
            bool serif = false, bool bold = false, bool wrap = false, bool shadow = true)
        {
            if (string.IsNullOrEmpty(s)) return;
            var st = UITheme.Text(size, anchor, serif, bold, wrap);
            var old = GUI.color;
            if (shadow)
            {
                GUI.color = new Color(0f, 0f, 0f, c.a * 0.85f);
                GUI.Label(new Rect(r.x + 1.5f, r.y + 2f, r.width, r.height), s, st);
            }
            GUI.color = c;
            GUI.Label(r, s, st);
            GUI.color = old;
        }

        public static float TextWidth(string s, int size, bool serif = false, bool bold = false)
        {
            return UITheme.Text(size, TextAnchor.MiddleLeft, serif, bold, false).CalcSize(new GUIContent(s)).x;
        }

        // ------------------------------------------------------------------ 패널 / 장식
        public static void Panel(Rect r, string title = null, float alpha = 1f)
        {
            NineSlice(r, UITheme.Panel, 12, new Color(1, 1, 1, alpha));
            if (title != null)
            {
                var h = new Rect(r.x + 14, r.y + 10, r.width - 28, 44);
                NineSlice(h, UITheme.PanelHeader, 12, new Color(1, 1, 1, alpha));
                Text(h, title, 24, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
                Ornament(new Vector2(h.x + 22, h.center.y), 10, UITheme.Gold);
                Ornament(new Vector2(h.xMax - 22, h.center.y), 10, UITheme.Gold);
            }
        }

        public static void Ornament(Vector2 c, float size, Color col)
        {
            Tex(new Rect(c.x - size, c.y - size, size * 2, size * 2), UITheme.Glow, new Color(col.r, col.g, col.b, 0.35f));
            Tex(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), UITheme.Diamond, col);
        }

        /// <summary>양 끝이 흐려지는 금색 선 + 가운데 마름모.</summary>
        public static void Divider(Rect r, Color? color = null)
        {
            var c = color ?? UITheme.GoldDim;
            Tex(new Rect(r.x, r.center.y - 1.5f, r.width, 3), UITheme.LineFade, c);
            Ornament(r.center, 7, c);
        }

        public static void Vignette(float W, float H, Color c)
        {
            Tex(new Rect(-W * 0.1f, -H * 0.1f, W * 1.2f, H * 1.2f), UITheme.Vignette, c);
        }

        /// <summary>마름모 안에 한 글자를 새긴 문장(紋章).</summary>
        public static void Emblem(Rect r, string glyph, Color c, bool filled = true)
        {
            Tex(new Rect(r.x - r.width * 0.3f, r.y - r.height * 0.3f, r.width * 1.6f, r.height * 1.6f), UITheme.Glow, new Color(c.r, c.g, c.b, 0.18f));
            Tex(r, UITheme.Diamond, filled ? new Color(c.r * 0.35f, c.g * 0.2f, c.b * 0.2f, 0.95f) : new Color(0, 0, 0, 0.6f));
            Tex(r, UITheme.DiamondHollow, c);
            Text(r, glyph, Mathf.RoundToInt(r.height * 0.36f), UITheme.Bone, TextAnchor.MiddleCenter, serif: true, bold: true);
        }

        // ------------------------------------------------------------------ 상호작용
        static int Id(Rect r, string key)
        {
            unchecked
            {
                int h = (int)(r.x * 73856093) ^ (int)(r.y * 19349663) ^ (int)(r.width * 83492791);
                return h * 31 + (key?.GetHashCode() ?? 0);
            }
        }

        /// <summary>호버 정도 0~1 (부드럽게 변함). 처음 올라갔을 때 작은 소리.</summary>
        public static float Hover(Rect r, string key, bool enabled = true, bool sound = true)
        {
            int id = Id(r, key);
            bool over = enabled && GUI.enabled && r.Contains(Mouse);
            if (over && IsRepaint)
            {
                hoveredThisFrame.Add(id);
                if (sound && !hoveredLastFrame.Contains(id)) SFX.Play(SFX.UiHover, 0.25f, 0.02f);
            }
            anim.TryGetValue(id, out float v);
            v = Mathf.MoveTowards(v, over ? 1f : 0f, Dt * 8f);
            anim[id] = v;
            return v;
        }

        static bool Clicked(Rect r, bool enabled)
        {
            if (!enabled) return false;
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                e.Use();
                SFX.Play(SFX.UiClick, 0.5f, 0.03f);
                return true;
            }
            return false;
        }

        /// <summary>테마 버튼. sub = 아래 작은 설명줄.</summary>
        public static bool Button(Rect r, string label, int size = 22, bool selected = false, bool enabled = true, string sub = null)
        {
            float h = Hover(r, label, enabled);
            bool pressed = enabled && r.Contains(Mouse) && Input.GetMouseButton(0);

            Texture2D bg = selected ? UITheme.ButtonSelected : pressed ? UITheme.ButtonPress : UITheme.Button;
            NineSlice(r, bg, 8, enabled ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.6f));
            if (!selected && h > 0.01f) NineSlice(r, UITheme.ButtonHover, 8, new Color(1, 1, 1, h));
            if (h > 0.01f)
                Tex(new Rect(r.x + 10, r.yMax - 4, r.width - 20, 3), UITheme.LineFade, new Color(1f, 0.85f, 0.5f, h));

            Color tc = !enabled ? UITheme.Ash : selected ? UITheme.Ink : Color.Lerp(UITheme.Bone, UITheme.GoldBright, h);
            bool shadow = !selected;
            float nudge = h * 2f;
            if (sub == null)
            {
                Text(new Rect(r.x, r.y - nudge * 0.5f, r.width, r.height), label, size, tc, TextAnchor.MiddleCenter, serif: true, bold: true, shadow: shadow);
            }
            else
            {
                Text(new Rect(r.x + 8, r.y + 6 - nudge * 0.5f, r.width - 16, r.height * 0.5f), label, size, tc, TextAnchor.MiddleCenter, serif: true, bold: true, shadow: shadow);
                Text(new Rect(r.x + 8, r.y + r.height * 0.5f, r.width - 16, r.height * 0.45f), sub, Mathf.RoundToInt(size * 0.62f),
                    selected ? new Color(0.2f, 0.1f, 0.05f) : UITheme.BoneDim, TextAnchor.UpperCenter, wrap: true, shadow: shadow);
            }
            return Clicked(r, enabled);
        }

        /// <summary>보이지 않는 클릭 영역 (카드 등 직접 그린 요소용). 호버 값도 반환.</summary>
        public static bool HitArea(Rect r, string key, out float hover, bool enabled = true)
        {
            hover = Hover(r, key, enabled);
            return Clicked(r, enabled);
        }

        /// <summary>작은 사각 +/- 버튼.</summary>
        public static bool IconButton(Rect r, string glyph, bool enabled)
        {
            float h = Hover(r, "icon" + glyph, enabled);
            NineSlice(r, h > 0.5f ? UITheme.ButtonHover : UITheme.Button, 6, enabled ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f));
            Text(r, glyph, Mathf.RoundToInt(r.height * 0.62f), enabled ? Color.Lerp(UITheme.Gold, UITheme.GoldBright, h) : UITheme.Ash,
                TextAnchor.MiddleCenter, bold: true);
            return Clicked(r, enabled);
        }

        public static float Slider(Rect r, float value, float min, float max, string key)
        {
            float t = Mathf.InverseLerp(min, max, value);
            var track = new Rect(r.x, r.center.y - 4, r.width, 8);
            Fill(track, new Color(0, 0, 0, 0.6f));
            Tex(new Rect(track.x, track.y, track.width * t, track.height), UITheme.BarFill, UITheme.Blood);
            float h = Hover(r, key, true, false);
            var knob = new Rect(track.x + track.width * t - 10, r.center.y - 10, 20, 20);
            Tex(knob, UITheme.Diamond, Color.Lerp(UITheme.Gold, UITheme.GoldBright, h));

            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && r.Contains(e.mousePosition))
            {
                t = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                e.Use();
            }
            return Mathf.Lerp(min, max, t);
        }

        // ------------------------------------------------------------------ 수치 표시
        /// <summary>
        /// 마름모 눈금. base개는 바랜 금, 그다음 extra개는 진홍(투자분), 그다음 preview개는 초록 테두리(미리보기).
        /// </summary>
        public static void Pips(Rect r, int max, int baseCount, int extra, int preview = 0, float size = 16f)
        {
            float step = Mathf.Min(size + 4, r.width / Mathf.Max(1, max));
            for (int i = 0; i < max; i++)
            {
                var p = new Rect(r.x + i * step, r.center.y - size / 2, size, size);
                if (i < baseCount) Tex(p, UITheme.Diamond, UITheme.Gold);
                else if (i < baseCount + extra)
                {
                    Tex(new Rect(p.x - 4, p.y - 4, p.width + 8, p.height + 8), UITheme.Glow, new Color(1f, 0.2f, 0.15f, 0.5f));
                    Tex(p, UITheme.Diamond, UITheme.BloodBright);
                }
                else if (i < baseCount + extra + preview) Tex(p, UITheme.DiamondHollow, UITheme.Good);
                else Tex(p, UITheme.DiamondHollow, new Color(0.4f, 0.3f, 0.2f, 0.6f));
            }
        }

        /// <summary>테두리 있는 게이지. trail ≥ 0이면 최근 잃은 양을 밝게 표시.</summary>
        public static void Bar(Rect r, float t, Color c, string label = null, float trail = -1f, int fontSize = 0)
        {
            t = Mathf.Clamp01(t);
            NineSlice(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), UITheme.Slot, 6, Color.white);
            Fill(r, new Color(0, 0, 0, 0.7f));
            if (trail > t) Tex(new Rect(r.x + r.width * t, r.y, r.width * (Mathf.Clamp01(trail) - t), r.height), UITheme.BarFill, new Color(1f, 0.9f, 0.7f, 0.8f));
            Tex(new Rect(r.x, r.y, r.width * t, r.height), UITheme.BarFill, c);
            if (!string.IsNullOrEmpty(label))
                Text(r, label, fontSize > 0 ? fontSize : Mathf.Clamp(Mathf.RoundToInt(r.height * 0.72f), 12, 22), UITheme.Bone, TextAnchor.MiddleCenter, bold: true);
        }

        /// <summary>
        /// 구슬 게이지 (아래에서 위로 차오름). 원 텍스처를 그룹으로 잘라 채움 높이를 표현.
        /// </summary>
        public static void Orb(Rect r, float t, Color fill, Color glow)
        {
            t = Mathf.Clamp01(t);
            Tex(new Rect(r.x - r.width * 0.25f, r.y - r.height * 0.25f, r.width * 1.5f, r.height * 1.5f), UITheme.Glow, new Color(glow.r, glow.g, glow.b, 0.25f));
            Tex(r, UITheme.Circle, new Color(0.02f, 0.01f, 0.015f, 0.92f));
            float fillH = r.height * t;
            if (fillH > 0.5f)
            {
                GUI.BeginGroup(new Rect(r.x, r.yMax - fillH, r.width, fillH));
                Tex(new Rect(0, fillH - r.height, r.width, r.height), UITheme.Orb, fill);
                // 수면의 출렁임
                float wave = Mathf.Sin(Time.unscaledTime * 2.2f) * 2f;
                Tex(new Rect(r.width * 0.12f, wave - 2, r.width * 0.76f, 5), UITheme.LineFade, new Color(1f, 1f, 1f, 0.35f));
                GUI.EndGroup();
            }
            Tex(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), UITheme.OrbRing, Color.white);
        }

        // ------------------------------------------------------------------ 툴팁
        public static void TooltipIf(Rect r, string text)
        {
            if (r.Contains(Mouse) && GUI.enabled)
            {
                tooltip = text;
                tooltipAt = Mouse;
            }
        }

        public static void DrawTooltip(float W, float H)
        {
            if (string.IsNullOrEmpty(tooltip)) return;
            var st = UITheme.Text(17, TextAnchor.UpperLeft, false, false, true);
            float w = 380f;
            float h = st.CalcHeight(new GUIContent(tooltip), w - 32) + 28;
            float x = tooltipAt.x + 22, y = tooltipAt.y + 18;
            if (x + w > W - 10) x = tooltipAt.x - w - 12;
            if (y + h > H - 10) y = H - h - 10;
            var r = new Rect(x, y, w, h);
            NineSlice(r, UITheme.Panel, 12, Color.white);
            Text(new Rect(r.x + 16, r.y + 14, w - 32, h - 28), tooltip, 17, UITheme.Bone, TextAnchor.UpperLeft, wrap: true);
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}

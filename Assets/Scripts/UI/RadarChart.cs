using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// 6각형 능력치 차트. CPU로 텍스처에 그려서 값이 바뀔 때만 다시 그린다.
    /// 진홍 = 현재 값, 초록 테두리 = 미리보기(투자 후) 값.
    /// </summary>
    public class RadarChart
    {
        const int N = 220;
        readonly Texture2D tex;
        readonly Color[] px = new Color[N * N];
        string lastKey;

        public RadarChart()
        {
            tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        /// <summary>values/preview: 0~1 정규화된 6개 값.</summary>
        public void Draw(Rect r, float[] values, float[] preview, string[] labels)
        {
            string key = Key(values) + "|" + (preview != null ? Key(preview) : "");
            if (key != lastKey)
            {
                lastKey = key;
                Render(values, preview);
            }
            UIKit.Tex(r, tex, Color.white);

            // 축 라벨
            float rad = r.width * 0.5f;
            for (int i = 0; i < 6; i++)
            {
                Vector2 d = Dir(i);
                var c = new Vector2(r.center.x + d.x * (rad + 18), r.center.y - d.y * (rad + 14));
                UIKit.Text(new Rect(c.x - 30, c.y - 14, 60, 28), labels[i], 17, UITheme.Gold, TextAnchor.MiddleCenter, serif: true, bold: true);
            }
        }

        static string Key(float[] v)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var x in v) sb.Append(Mathf.RoundToInt(x * 1000)).Append(',');
            return sb.ToString();
        }

        static Vector2 Dir(int i)
        {
            float a = Mathf.PI / 2f - i * Mathf.PI * 2f / 6f; // 위에서 시작, 시계 방향
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        void Render(float[] values, float[] preview)
        {
            float c = (N - 1) * 0.5f, R = N * 0.46f;
            var poly = new Vector2[6];
            var prev = new Vector2[6];
            var outer = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                outer[i] = new Vector2(c, c) + Dir(i) * R;
                poly[i] = new Vector2(c, c) + Dir(i) * R * Mathf.Clamp(values[i], 0.04f, 1f);
                if (preview != null) prev[i] = new Vector2(c, c) + Dir(i) * R * Mathf.Clamp(preview[i], 0.04f, 1f);
            }

            Color webCol = new Color(0.55f, 0.42f, 0.22f, 0.55f);
            Color fillCol = new Color(0.7f, 0.08f, 0.1f, 0.55f);
            Color edgeCol = new Color(1f, 0.35f, 0.3f, 1f);
            Color prevCol = new Color(0.55f, 0.9f, 0.45f, 1f);

            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2(x, y);
                    Color col = Color.clear;

                    // 바탕: 안쪽이 살짝 어두운 육각형
                    if (Inside(p, outer)) col = new Color(0.05f, 0.02f, 0.03f, 0.75f);

                    // 거미줄: 1/3, 2/3, 1 단계 육각형 + 축
                    float web = 99f;
                    for (int ring = 1; ring <= 3; ring++)
                    {
                        float s = ring / 3f;
                        for (int i = 0; i < 6; i++)
                        {
                            var a = new Vector2(c, c) + (outer[i] - new Vector2(c, c)) * s;
                            var b = new Vector2(c, c) + (outer[(i + 1) % 6] - new Vector2(c, c)) * s;
                            web = Mathf.Min(web, SegDist(p, a, b));
                        }
                    }
                    for (int i = 0; i < 6; i++) web = Mathf.Min(web, SegDist(p, new Vector2(c, c), outer[i]));
                    col = Over(col, webCol, Mathf.Clamp01(1.2f - web));

                    // 현재 값
                    if (Inside(p, poly)) col = Over(col, fillCol, 1f);
                    float edge = 99f;
                    for (int i = 0; i < 6; i++) edge = Mathf.Min(edge, SegDist(p, poly[i], poly[(i + 1) % 6]));
                    col = Over(col, edgeCol, Mathf.Clamp01(1.8f - edge));

                    // 미리보기
                    if (preview != null)
                    {
                        float pe = 99f;
                        for (int i = 0; i < 6; i++) pe = Mathf.Min(pe, SegDist(p, prev[i], prev[(i + 1) % 6]));
                        col = Over(col, prevCol, Mathf.Clamp01(1.6f - pe));
                    }

                    px[y * N + x] = col;
                }
            tex.SetPixels(px);
            tex.Apply();
        }

        static Color Over(Color dst, Color src, float a)
        {
            a *= src.a;
            if (a <= 0f) return dst;
            float outA = a + dst.a * (1f - a);
            if (outA <= 0f) return Color.clear;
            Color c = (src * a + dst * dst.a * (1f - a)) / outA;
            c.a = outA;
            return c;
        }

        static bool Inside(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}

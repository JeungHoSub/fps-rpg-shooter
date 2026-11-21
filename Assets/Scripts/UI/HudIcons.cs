using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// HUD 능력·보급 아이콘 (코드로 그리는 흰 실루엣, 색은 그릴 때 입힌다).
    /// 글자 한 자 대신 모양으로 바로 알아보게: 불꽃(광폭화) · 심장(재생) · 환약(청심환) · 구급함(키트) · 약병(회복제) · 웅크린 사람(앉기).
    /// 처음 쓸 때 만든다 (필드 초기화에서 Texture2D 생성 금지).
    /// </summary>
    public static class HudIcons
    {
        public enum Icon { Flame, Heart, Pill, Kit, Bottle, Crouch, Ammo, Helmet, Vest, Glove, Greave, Boot, Grenade, Mine, Turret }

        const int Size = 96;
        static readonly Dictionary<Icon, Texture2D> cache = new Dictionary<Icon, Texture2D>();

        public static Texture2D Get(Icon i)
        {
            if (cache.TryGetValue(i, out var t) && t != null) return t;
            t = Make(Shape(i));
            t.name = "HudIcon_" + i;
            cache[i] = t;
            return t;
        }

        /// <summary>점(-1~1, 위가 +y)이 모양 안이면 true.</summary>
        static Func<float, float, bool> Shape(Icon i)
        {
            switch (i)
            {
                case Icon.Flame:
                    return (x, y) =>
                    {
                        // 바깥 불꽃 (아래 둥글고 위로 갈수록 가늘게 휘는 물방울) − 안쪽 작은 불꽃
                        bool outer = Drop(x - 0.08f * Mathf.Max(0f, y) * Mathf.Max(0f, y), y, 0.62f, -0.25f, 0.95f);
                        bool inner = Drop(x + 0.04f, y + 0.18f, 0.3f, -0.4f, 0.35f);
                        return outer && !inner;
                    };
                case Icon.Heart:
                    return (x, y) =>
                    {
                        float hx = x * 1.25f, hy = (y + 0.12f) * 1.3f;
                        float a = hx * hx + hy * hy - 1f;
                        bool heart = a * a * a - hx * hx * hy * hy * hy <= 0f;
                        // 가운데 흰 십자 (빈칸)
                        bool cross = (Mathf.Abs(x) < 0.09f && Mathf.Abs(y - 0.02f) < 0.32f) || (Mathf.Abs(y - 0.02f) < 0.09f && Mathf.Abs(x) < 0.32f);
                        return heart && !cross;
                    };
                case Icon.Pill:
                    return (x, y) =>
                    {
                        // 금박 싼 환약: 동그라미 + 비스듬한 띠 (빈칸) + 위 반짝임 (빈칸)
                        float r = Mathf.Sqrt(x * x + y * y);
                        bool ball = r < 0.78f;
                        bool band = Mathf.Abs(x * 0.6f + y * 0.8f) < 0.06f;
                        bool shine = (x + 0.3f) * (x + 0.3f) + (y - 0.32f) * (y - 0.32f) < 0.016f;
                        return ball && !band && !shine;
                    };
                case Icon.Kit:
                    return (x, y) =>
                    {
                        // 손잡이 달린 구급함 + 십자 (빈칸)
                        bool box = RoundRect(x, y + 0.08f, 0.8f, 0.6f, 0.14f);
                        bool handle = RoundRect(x, y - 0.62f, 0.32f, 0.13f, 0.06f) && !RoundRect(x, y - 0.6f, 0.18f, 0.06f, 0.03f);
                        bool cross = (Mathf.Abs(x) < 0.1f && Mathf.Abs(y + 0.08f) < 0.36f) || (Mathf.Abs(y + 0.08f) < 0.1f && Mathf.Abs(x) < 0.36f);
                        return (box && !cross) || handle;
                    };
                case Icon.Bottle:
                    return (x, y) =>
                    {
                        // 약병: 둥근 몸통 + 목 + 마개, 몸통에 물결 (빈칸)
                        bool body = RoundRect(x, y + 0.25f, 0.48f, 0.58f, 0.2f);
                        bool neck = Mathf.Abs(x) < 0.17f && y > 0.3f && y < 0.66f;
                        bool cap = RoundRect(x, y - 0.75f, 0.24f, 0.12f, 0.04f);
                        bool label = Mathf.Abs(y + 0.2f + 0.06f * Mathf.Sin(x * 9f)) < 0.05f && Mathf.Abs(x) < 0.4f;
                        return ((body || neck) && !label) || cap;
                    };
                case Icon.Crouch:
                    return (x, y) =>
                    {
                        // 웅크린 사람: 머리 + 굽은 몸 + 접힌 다리
                        bool head = (x - 0.18f) * (x - 0.18f) + (y - 0.55f) * (y - 0.55f) < 0.04f;
                        bool torso = Seg(x, y, 0.05f, 0.3f, -0.25f, -0.1f, 0.13f);
                        bool thigh = Seg(x, y, -0.25f, -0.1f, 0.3f, -0.25f, 0.12f);
                        bool shin = Seg(x, y, 0.3f, -0.25f, 0.15f, -0.75f, 0.1f);
                        bool arm = Seg(x, y, 0.0f, 0.2f, 0.45f, 0.05f, 0.08f);
                        return head || torso || thigh || shin || arm;
                    };
                case Icon.Helmet:
                    // 둥근 철모 + 챙
                    return (x, y) => ((x * x + (y + 0.05f) * (y + 0.05f) < 0.5f && y > -0.05f) || RoundRect(x, y + 0.12f, 0.85f, 0.08f, 0.04f)) && !(y > -0.05f && y < 0.02f && Mathf.Abs(x) < 0.55f);
                case Icon.Vest:
                    // 조끼: 몸통 − 목·팔 구멍, 가운데 여밈선
                    return (x, y) => RoundRect(x, y + 0.05f, 0.62f, 0.78f, 0.12f)
                        && !((x * x + (y - 0.85f) * (y - 0.85f)) < 0.09f)
                        && !(Mathf.Abs(x) > 0.38f && y > 0.15f)
                        && !(Mathf.Abs(x) < 0.025f && y < 0.5f);
                case Icon.Glove:
                    return (x, y) =>
                    {
                        if (RoundRect(x, y + 0.3f, 0.42f, 0.42f, 0.14f)) return true;                 // 손바닥
                        for (int f = 0; f < 4; f++)
                            if (RoundRect(x - (-0.3f + f * 0.2f), y - 0.3f - (f == 1 || f == 2 ? 0.08f : 0f), 0.075f, 0.34f, 0.07f)) return true; // 손가락
                        return Seg(x, y, -0.4f, -0.2f, -0.72f, 0.12f, 0.1f);                           // 엄지
                    };
                case Icon.Greave:
                    // 두 다리 정강이받이
                    return (x, y) => (RoundRect(x + 0.28f, y, 0.2f, 0.82f, 0.12f) || RoundRect(x - 0.28f, y, 0.2f, 0.82f, 0.12f)) && Mathf.Abs(Mathf.Repeat(y * 3.2f, 1f) - 0.5f) < 0.42f;
                case Icon.Boot:
                    return (x, y) => RoundRect(x + 0.15f, y - 0.1f, 0.3f, 0.6f, 0.12f) || RoundRect(x - 0.15f, y + 0.55f, 0.62f, 0.2f, 0.12f);
                case Icon.Grenade:
                    return (x, y) =>
                    {
                        float ex = x / 0.55f, ey = (y + 0.18f) / 0.68f;
                        bool body = ex * ex + ey * ey < 1f && !(Mathf.Abs(Mathf.Repeat((y + 0.18f) * 3.5f, 1f) - 0.5f) < 0.05f);
                        bool top = RoundRect(x, y - 0.56f, 0.22f, 0.12f, 0.04f);
                        bool lever = Seg(x, y, 0.15f, 0.62f, 0.55f, 0.1f, 0.06f);
                        bool ring = Mathf.Abs(Mathf.Sqrt((x + 0.38f) * (x + 0.38f) + (y - 0.62f) * (y - 0.62f)) - 0.17f) < 0.045f;
                        return body || top || lever || ring;
                    };
                case Icon.Mine:
                    return (x, y) => RoundRect(x, y + 0.2f, 0.85f, 0.22f, 0.12f) || RoundRect(x, y - 0.12f, 0.25f, 0.12f, 0.05f) || (Mathf.Abs(x) < 0.04f && y > 0f && y < 0.45f);
                case Icon.Turret:
                    return (x, y) => Seg(x, y, 0f, 0.05f, -0.6f, -0.8f, 0.06f) || Seg(x, y, 0f, 0.05f, 0.6f, -0.8f, 0.06f) || Seg(x, y, 0f, 0.05f, 0f, -0.8f, 0.06f)
                        || RoundRect(x - 0.05f, y - 0.2f, 0.32f, 0.16f, 0.05f) || RoundRect(x - 0.55f, y - 0.22f, 0.4f, 0.05f, 0.03f);
                default:
                    return (x, y) =>
                    {
                        // 총알 세 발
                        for (int k = -1; k <= 1; k++)
                        {
                            float cx = k * 0.42f;
                            if (Mathf.Abs(x - cx) < 0.13f && y > -0.75f && y < 0.25f) return true;
                            float dx = (x - cx) / 0.13f, dy = (y - 0.25f) / 0.42f;
                            if (y >= 0.25f && dx * dx + dy * dy < 1f) return true;
                        }
                        return false;
                    };
            }
        }

        // 물방울: 아래는 원, 위는 꼭짓점으로 좁아진다
        static bool Drop(float x, float y, float r, float cy, float tipY)
        {
            float dy = y - cy;
            if (dy <= 0f) return x * x + dy * dy < r * r;
            if (y > tipY) return false;
            float t = dy / (tipY - cy);
            float half = r * Mathf.Sqrt(Mathf.Max(0f, 1f - t)) * (1f - t * 0.3f);
            return Mathf.Abs(x) < half;
        }

        static bool RoundRect(float x, float y, float hw, float hh, float rad)
        {
            float qx = Mathf.Abs(x) - (hw - rad), qy = Mathf.Abs(y) - (hh - rad);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return ox * ox + oy * oy <= rad * rad && Mathf.Max(qx, qy) <= rad;
        }

        static bool Seg(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01((px * vx + py * vy) / (vx * vx + vy * vy));
            float dx = px - vx * t, dy = py - vy * t;
            return dx * dx + dy * dy < r * r;
        }

        /// <summary>4×4 표본으로 가장자리를 부드럽게 (계단 현상 없이).</summary>
        static Texture2D Make(Func<float, float, bool> inside)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[Size * Size];
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float x = ((i + (sx + 0.5f) / 4f) / Size) * 2f - 1f;
                            float y = ((j + (sy + 0.5f) / 4f) / Size) * 2f - 1f;
                            if (inside(x * 1.08f, y * 1.08f)) hit++;
                        }
                    px[j * Size + i] = new Color32(255, 255, 255, (byte)(hit * 255 / 16));
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}

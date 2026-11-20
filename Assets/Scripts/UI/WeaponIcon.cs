using Hunt.Combat;
using Hunt.Player;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// HUD용 무기 실루엣: 계열마다 총열·몸통·개머리·탄창·손잡이 모양을 사각형 조각으로 그린다 (텍스처 없음).
    /// 좌표는 칸 안의 0~1 비율, 총구가 오른쪽.
    /// </summary>
    public static class WeaponIcon
    {
        struct P { public float x, y, w, h, rot; public P(float x, float y, float w, float h, float rot = 0f) { this.x = x; this.y = y; this.w = w; this.h = h; this.rot = rot; } }

        static P[] Shape(WeaponClass c)
        {
            switch (c)
            {
                case WeaponClass.Pistol: return new[]
                {
                    new P(0.25f, 0.30f, 0.55f, 0.16f), new P(0.27f, 0.45f, 0.45f, 0.08f),
                    new P(0.30f, 0.50f, 0.13f, 0.38f, -12f), new P(0.42f, 0.52f, 0.1f, 0.12f),
                };
                case WeaponClass.SMG: return new[]
                {
                    new P(0.22f, 0.32f, 0.46f, 0.18f), new P(0.68f, 0.37f, 0.18f, 0.06f), new P(0.48f, 0.48f, 0.07f, 0.36f),
                    new P(0.32f, 0.48f, 0.08f, 0.24f, -15f), new P(0.04f, 0.36f, 0.2f, 0.09f), new P(0.04f, 0.36f, 0.05f, 0.22f),
                };
                case WeaponClass.Shotgun: return new[]
                {
                    new P(0.22f, 0.36f, 0.24f, 0.17f), new P(0.44f, 0.36f, 0.52f, 0.07f), new P(0.44f, 0.44f, 0.46f, 0.06f),
                    new P(0.52f, 0.42f, 0.2f, 0.12f), new P(0.02f, 0.38f, 0.22f, 0.2f, 6f), new P(0.25f, 0.5f, 0.07f, 0.22f, -20f),
                };
                case WeaponClass.Sniper: return new[]
                {
                    new P(0.24f, 0.42f, 0.3f, 0.13f), new P(0.52f, 0.45f, 0.46f, 0.05f), new P(0.28f, 0.24f, 0.28f, 0.1f),
                    new P(0.25f, 0.26f, 0.04f, 0.14f), new P(0.53f, 0.26f, 0.04f, 0.14f), new P(0.0f, 0.43f, 0.26f, 0.18f, 3f),
                    new P(0.3f, 0.54f, 0.06f, 0.2f, -15f), new P(0.4f, 0.54f, 0.07f, 0.1f),
                };
                case WeaponClass.LMG: return new[]
                {
                    new P(0.2f, 0.32f, 0.38f, 0.2f), new P(0.56f, 0.38f, 0.42f, 0.07f), new P(0.58f, 0.34f, 0.14f, 0.15f),
                    new P(0.32f, 0.52f, 0.17f, 0.2f), new P(0.0f, 0.34f, 0.22f, 0.18f), new P(0.22f, 0.52f, 0.06f, 0.2f, -15f),
                    new P(0.82f, 0.44f, 0.02f, 0.26f, 20f), new P(0.86f, 0.44f, 0.02f, 0.26f, -20f),
                };
                case WeaponClass.Flamethrower: return new[]
                {
                    new P(0.25f, 0.3f, 0.4f, 0.16f), new P(0.62f, 0.33f, 0.3f, 0.09f), new P(0.9f, 0.3f, 0.06f, 0.15f),
                    new P(0.3f, 0.47f, 0.32f, 0.14f), new P(0.3f, 0.62f, 0.32f, 0.14f), new P(0.05f, 0.32f, 0.22f, 0.13f),
                    new P(0.4f, 0.46f, 0.06f, 0.3f, -15f),
                };
                case WeaponClass.Launcher: return new[]
                {
                    new P(0.3f, 0.3f, 0.6f, 0.22f), new P(0.88f, 0.28f, 0.06f, 0.26f), new P(0.02f, 0.38f, 0.3f, 0.18f, 4f),
                    new P(0.36f, 0.5f, 0.07f, 0.26f, -15f), new P(0.5f, 0.2f, 0.04f, 0.1f),
                };
                case WeaponClass.Melee: return new[]
                {
                    new P(0.02f, 0.46f, 0.26f, 0.12f), new P(0.27f, 0.42f, 0.04f, 0.2f), new P(0.3f, 0.4f, 0.6f, 0.15f, -3f),
                    new P(0.86f, 0.36f, 0.1f, 0.14f, 25f),
                };
                default: return new[] // 소총
                {
                    new P(0.22f, 0.36f, 0.38f, 0.16f), new P(0.58f, 0.38f, 0.22f, 0.12f), new P(0.8f, 0.41f, 0.17f, 0.05f),
                    new P(0.4f, 0.5f, 0.08f, 0.3f, 10f), new P(0.28f, 0.5f, 0.07f, 0.22f, -15f), new P(0.02f, 0.37f, 0.22f, 0.16f),
                    new P(0.3f, 0.29f, 0.24f, 0.06f), new P(0.86f, 0.3f, 0.02f, 0.1f),
                };
            }
        }

        // 새 총 모델의 옆모습을 미리 그려 둔 그림 (Resources/WeaponIcons/<무기 id>.png). 없으면 아래 도형 실루엣.
        static readonly System.Collections.Generic.Dictionary<string, Texture2D> pics = new System.Collections.Generic.Dictionary<string, Texture2D>();
        static readonly System.Collections.Generic.Dictionary<string, string> alias = new System.Collections.Generic.Dictionary<string, string>
        { { "flamer2", "flamer" }, { "napalm", "flamer" } };

        static Texture2D Picture(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (alias.TryGetValue(id, out var a)) id = a;
            if (!pics.TryGetValue(id, out var t)) { t = Resources.Load<Texture2D>("WeaponIcons/" + id); pics[id] = t; }
            return t;
        }

        /// <summary>무기마다 다른 그림 (모델 옆모습). 칸 비율에 맞춰 가운데 정렬.</summary>
        public static void Draw(Rect r, Hunt.Data.WeaponData d, Color col)
        {
            var t = d != null ? Picture(d.id) : null;
            if (t == null) { Draw(r, d != null ? d.weaponClass : WeaponClass.Rifle, col); return; }
            float aspect = t.width / (float)t.height;
            float w = r.width, h = w / aspect;
            if (h > r.height) { h = r.height; w = h * aspect; }
            UIKit.Tex(new Rect(r.center.x - w / 2f, r.center.y - h / 2f, w, h), t, col);
        }

        public static void Draw(Rect r, WeaponClass c, Color col)
        {
            foreach (var p in Shape(c)) Piece(r, p, col);
        }

        public static void DrawGadget(Rect r, GadgetType g, Color col)
        {
            switch (g)
            {
                case GadgetType.Grenade:
                    Piece(r, new P(0.12f, 0.44f, 0.5f, 0.12f), col);
                    Piece(r, new P(0.6f, 0.34f, 0.24f, 0.32f), col);
                    break;
                case GadgetType.Mine:
                    Piece(r, new P(0.2f, 0.5f, 0.6f, 0.16f), col);
                    Piece(r, new P(0.4f, 0.4f, 0.2f, 0.1f), col);
                    break;
                default:
                    Piece(r, new P(0.3f, 0.25f, 0.45f, 0.16f), col);
                    Piece(r, new P(0.72f, 0.29f, 0.22f, 0.06f), col);
                    Piece(r, new P(0.45f, 0.4f, 0.05f, 0.4f, 25f), col);
                    Piece(r, new P(0.5f, 0.4f, 0.05f, 0.4f, -25f), col);
                    break;
            }
        }

        static void Piece(Rect r, P p, Color col)
        {
            var pr = new Rect(r.x + p.x * r.width, r.y + p.y * r.height, p.w * r.width, p.h * r.height);
            if (Mathf.Abs(p.rot) < 0.01f) { UIKit.Fill(pr, col); return; }
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(p.rot, pr.center);
            UIKit.Fill(pr, col);
            GUI.matrix = m;
        }
    }
}

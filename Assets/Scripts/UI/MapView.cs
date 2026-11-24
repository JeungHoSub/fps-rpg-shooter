using Hunt.Core;
using Hunt.Monsters;
using Hunt.Player;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// 마을 지도 (M을 누르고 있는 동안): 마을을 지을 때 위에서 한 번 찍어 둔 그림 위에
    /// 나 · 보급 상자 · 보스 · 위치가 드러난 망자를 표시한다. 매 프레임 다시 찍지 않으니 가볍다.
    /// </summary>
    public static class MapView
    {
        static RenderTexture rt;
        static float extent = VillageBuilder.Half + 4f;

        /// <summary>마을을 위에서 내려다본 그림을 한 번 찍는다 (밤이라도 지도는 밝게).</summary>
        public static void Capture()
        {
            try
            {
                if (rt == null)
                {
                    rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32) { name = "VillageMap" };
                    rt.Create();
                }
                var go = new GameObject("MapCamera");
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.orthographicSize = extent;
                cam.aspect = 1f;
                cam.transform.position = new Vector3(0f, 150f, 0f);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 300f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.14f, 0.1f);
                cam.cullingMask = ~((1 << 2) | (1 << Hunt.Weapons.GunModelBuilder.ViewLayer)); // 효과·무기 화면 제외
                cam.targetTexture = rt;

                bool fog = RenderSettings.fog;
                Color amb = RenderSettings.ambientLight;
                RenderSettings.fog = false;
                RenderSettings.ambientLight = new Color(0.8f, 0.78f, 0.72f);
                cam.Render();
                RenderSettings.fog = fog;
                RenderSettings.ambientLight = amb;

                cam.targetTexture = null;
                Object.Destroy(go);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("지도 찍기 실패: " + e.Message);
            }
        }

        static Vector2 ToMap(Vector3 world, Rect r)
        {
            float u = Mathf.InverseLerp(-extent, extent, world.x);
            float v = Mathf.InverseLerp(-extent, extent, world.z);
            return new Vector2(r.x + u * r.width, r.y + (1f - v) * r.height);
        }

        public static void Draw(GameManager gm, float W, float H, float alpha)
        {
            if (rt == null || alpha <= 0.01f) return;
            float keep = UIKit.Alpha;
            UIKit.Alpha = keep * alpha;
            float size = Mathf.Min(H * 0.82f, W * 0.7f);
            var r = new Rect(W / 2 - size / 2, H / 2 - size / 2 + (1f - alpha) * 14f, size, size);
            UIKit.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.5f));
            UIKit.Panel(new Rect(r.x - 18, r.y - 52, r.width + 36, r.height + 90));
            UIKit.Text(new Rect(r.x, r.y - 46, r.width, 30), "마을 지도", 22, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Tex(r, rt, new Color(0.85f, 0.82f, 0.75f, 1f));
            UIKit.Outline(r, new Color(0.5f, 0.46f, 0.38f), 2f);

            // 지킴 밤: 지켜야 할 돌무더기
            if (gm.Waves != null && gm.Waves.Rule == Hunt.Waves.RoundRule.Defense && gm.State == GameState.Playing)
            {
                var dp = ToMap(gm.Waves.DefendPos, r);
                UIKit.Emblem(new Rect(dp.x - 15, dp.y - 15, 30, 30), "지", UITheme.GoldBright, true);
            }
            // 확성기 탑 (사이렌): 이번 밤에 썼으면 흐리게
            if (SirenTower.Instance != null)
            {
                var sp = ToMap(SirenTower.Instance.transform.position, r);
                UIKit.Emblem(new Rect(sp.x - 13, sp.y - 13, 26, 26), "탑", SirenTower.Instance.UsedThisNight ? new Color(0.45f, 0.43f, 0.4f) : new Color(1f, 0.7f, 0.35f), true);
            }
            // 다친 마을 사람
            foreach (var sv in Survivor.Active)
            {
                if (sv == null || sv.Done) continue;
                var svp = ToMap(sv.transform.position, r);
                UIKit.Emblem(new Rect(svp.x - 12, svp.y - 12, 24, 24), "사", new Color(0.9f, 0.88f, 0.75f), true);
            }
            // 숨을 덤불 (옅은 초록 동그라미)
            foreach (var b in Hiding.Spots)
            {
                var bp = ToMap(new Vector3(b.x, 0f, b.z), r);
                UIKit.Tex(new Rect(bp.x - 8, bp.y - 8, 16, 16), UITheme.Circle, new Color(0.45f, 0.75f, 0.4f, 0.55f));
            }
            // 이미 절한 장승 (찾은 것만 — 못 찾은 장승은 끝까지 숨어 있다)
            foreach (var j in Jangseung.Active)
            {
                if (j == null || !j.Blessed) continue;
                var jp = ToMap(j.transform.position, r);
                UIKit.Emblem(new Rect(jp.x - 12, jp.y - 12, 24, 24), "장", new Color(0.75f, 0.6f, 0.4f), true);
            }
            // 보급 상자
            foreach (var c in SupplyCrate.Active)
            {
                if (c == null || c.Opened) continue;
                var p = ToMap(c.transform.position, r);
                UIKit.Fill(new Rect(p.x - 6, p.y - 6, 12, 12), new Color(0f, 0f, 0f, 0.6f));
                UIKit.Fill(new Rect(p.x - 5, p.y - 5, 10, 10), new Color(1f, 0.8f, 0.3f));
            }
            // 망자: 보스는 '대', 위치가 드러난 망자는 붉은 점
            foreach (var m in Monster.Alive)
            {
                if (m == null || m.IsDead) continue;
                var p = ToMap(m.transform.position, r);
                if (m.IsBoss) UIKit.Emblem(new Rect(p.x - 14, p.y - 14, 28, 28), "대", UITheme.BloodBright, true);
                else if (m.IsBounty) UIKit.Tex(new Rect(p.x - 7, p.y - 7, 14, 14), UITheme.Circle, new Color(1f, 0.82f, 0.3f));
                else if (m.Revealed) UIKit.Tex(new Rect(p.x - 5, p.y - 5, 10, 10), UITheme.Circle, new Color(0.95f, 0.2f, 0.12f));
            }
            // 나: 흰 점 + 바라보는 쪽 짧은 선
            var pl = gm.Player;
            if (pl != null)
            {
                var p = ToMap(pl.transform.position, r);
                float yaw = pl.transform.eulerAngles.y;
                var m0 = GUI.matrix;
                GUI.matrix = m0 * Matrix4x4.Translate(new Vector3(p.x, p.y, 0f)) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, yaw)) * Matrix4x4.Translate(new Vector3(-p.x, -p.y, 0f));
                UIKit.Fill(new Rect(p.x - 1.5f, p.y - 20, 3, 18), new Color(1f, 0.95f, 0.85f));
                GUI.matrix = m0;
                UIKit.Tex(new Rect(p.x - 7, p.y - 7, 14, 14), UITheme.Circle, Color.white);
            }
            UIKit.Text(new Rect(r.x, r.yMax + 2, r.width, 18), "흰 점 = 나  ·  노란 네모 = 보급 상자  ·  노란 동그라미 = 현상금  ·  대 = 보스", 13, UITheme.BoneDim, TextAnchor.MiddleCenter);
            UIKit.Text(new Rect(r.x, r.yMax + 20, r.width, 18), "지 = 지킬 곳  ·  탑 = 사이렌(E)  ·  사 = 다친 사람  ·  붉은 점 = 망자  ·  초록 = 덤불", 13, UITheme.BoneDim, TextAnchor.MiddleCenter);
            UIKit.Alpha = keep;
        }
    }
}

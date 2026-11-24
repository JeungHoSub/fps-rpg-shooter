using System.Collections.Generic;
using Hunt.Core;
using Hunt.UI;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>
    /// 숨은 장승: 판마다 마을 가장자리 곳곳에 3개 (지도·나침반에 안 나온다 — 찾아다녀야 한다).
    /// 다가가 E로 절하면 이번 판 최대 생명 +8%. 나무 기둥에 부릅뜬 눈·드러낸 이빨, 검은 사모.
    /// </summary>
    public class Jangseung : MonoBehaviour
    {
        public const float Range = 2.6f;
        public static readonly List<Jangseung> Active = new List<Jangseung>();
        bool blessed;

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        /// <summary>마을 가장자리(중심에서 26~40m) 서로 떨어진 3곳에 세운다.</summary>
        public static void PlaceForRun(Transform parent)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = Vector3.zero;
                for (int k = 0; k < 24; k++)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f), rad = Random.Range(26f, 40f);
                    p = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                    bool far = true;
                    foreach (var o in Active) if (o != null && Vector3.Distance(o.transform.position, p) < 20f) far = false;
                    if (far) break;
                }
                p = Navigation.Ready ? Navigation.Snap(p, 6f) : p;
                var go = new GameObject("장승");
                if (parent != null) go.transform.SetParent(parent, true);
                go.transform.position = p;
                go.transform.rotation = Quaternion.LookRotation(new Vector3(-p.x, 0f, -p.z)); // 마을 안쪽을 본다
                go.AddComponent<Jangseung>().Build(i % 2 == 0);
            }
        }

        void Build(bool male)
        {
            var wood = EnvMaterials.Plain(new Color(0.36f, 0.27f, 0.19f), 0.25f);
            var black = EnvMaterials.Plain(new Color(0.05f, 0.05f, 0.05f), 0.4f);
            var white = EnvMaterials.Plain(new Color(0.85f, 0.82f, 0.74f), 0.3f);
            var red = EnvMaterials.Plain(new Color(0.55f, 0.12f, 0.08f), 0.3f);
            Part(PrimitiveType.Cylinder, new Vector3(0f, 1.15f, 0f), new Vector3(0.42f, 1.15f, 0.42f), wood, true);   // 몸통
            if (male)
            {
                Part(PrimitiveType.Cylinder, new Vector3(0f, 2.42f, 0f), new Vector3(0.5f, 0.12f, 0.5f), black, false);   // 사모(관)
                Part(PrimitiveType.Cylinder, new Vector3(0f, 2.62f, 0f), new Vector3(0.3f, 0.12f, 0.3f), black, false);
            }
            else Part(PrimitiveType.Sphere, new Vector3(0f, 2.38f, 0f), new Vector3(0.46f, 0.22f, 0.46f), black, false); // 족두리 같은 머리
            // 부릅뜬 눈 · 굵은 눈썹 · 주먹코 · 드러낸 이빨
            foreach (float sx in new[] { -0.1f, 0.1f })
            {
                Part(PrimitiveType.Sphere, new Vector3(sx, 2.08f, 0.19f), new Vector3(0.11f, 0.11f, 0.06f), white, false);
                Part(PrimitiveType.Sphere, new Vector3(sx, 2.08f, 0.22f), new Vector3(0.05f, 0.05f, 0.03f), black, false);
                Part(PrimitiveType.Cube, new Vector3(sx, 2.19f, 0.2f), new Vector3(0.14f, 0.035f, 0.04f), black, false);
            }
            Part(PrimitiveType.Sphere, new Vector3(0f, 1.94f, 0.22f), new Vector3(0.1f, 0.12f, 0.08f), wood, false);
            Part(PrimitiveType.Cube, new Vector3(0f, 1.78f, 0.2f), new Vector3(0.22f, 0.06f, 0.04f), white, false);
            // 몸통 붉은 글씨 띠 (천하대장군 / 지하여장군 자리)
            Part(PrimitiveType.Cube, new Vector3(0f, 1.1f, 0.2f), new Vector3(0.1f, 1.1f, 0.03f), red, false);
        }

        void Part(PrimitiveType t, Vector3 pos, Vector3 scale, Material m, bool collider)
        {
            var g = GameObject.CreatePrimitive(t);
            if (!collider) Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        /// <summary>플레이어 가까이(2.6m)에 아직 절하지 않은 장승.</summary>
        public static Jangseung NearestToPlayer()
        {
            var gm = GameManager.Instance;
            var p = gm != null ? gm.Player : null;
            if (p == null || p.IsDead) return null;
            foreach (var j in Active)
            {
                if (j == null || j.blessed) continue;
                Vector3 d = j.transform.position - p.transform.position; d.y = 0f;
                if (d.sqrMagnitude < Range * Range) return j;
            }
            return null;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (blessed || gm == null || !gm.AcceptsGameplayInput || !GameInput.PickupDown) return;
            if (!ReferenceEquals(NearestToPlayer(), this)) return;
            blessed = true;
            var p = gm.Player;
            float before = p.MaxHp;
            p.Blessings++;
            p.Heal(p.MaxHp - before + p.MaxHp * 0.15f);
            SFX.Play(SFX.Gong, 0.7f, 0f);
            HUD.Notify($"장승에게 절했다 — 이번 판 최대 생명 +8% (찾은 장승 {p.Blessings} / 3)", new Color(1f, 0.85f, 0.55f));
            if (p.Blessings >= 3) Achievements.Unlock("jangseung");
        }
    }
}

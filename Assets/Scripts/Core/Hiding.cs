using System.Collections.Generic;
using UnityEngine;

namespace Hunt.Core
{
    /// <summary>덤불 흔들림: 0.9초 동안 잦아드는 좌우 기울임.</summary>
    public class BushShake : MonoBehaviour
    {
        Quaternion baseRot;
        float t = 1f;
        bool init;

        public void Kick()
        {
            if (!init) { baseRot = transform.localRotation; init = true; }
            t = 0f;
            enabled = true;
        }

        void Update()
        {
            t += Time.deltaTime / 0.9f;
            float a = (1f - Mathf.Clamp01(t)) * 6f;
            transform.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 30f) * a, 0f, Mathf.Cos(t * 23f) * a);
            if (t >= 1f) { transform.localRotation = baseRot; enabled = false; }
        }
    }

    /// <summary>
    /// 숨을 곳 (덤불). 앉아서 덤불 안에 있으면 망자가 가까이 와야 알아챈다 (Monster.Sense).
    /// VillageBuilder가 덤불을 놓을 때 등록. 마을을 다시 지으면 Clear.
    /// </summary>
    public static class Hiding
    {
        static readonly List<Vector4> spots = new List<Vector4>(); // xyz = 중심, w = 반지름
        static readonly List<GameObject> visuals = new List<GameObject>(); // 같은 순서의 덤불 모델 (불타면 검게)

        public static IReadOnlyList<Vector4> Spots => spots;

        public static void Clear() { spots.Clear(); visuals.Clear(); paddies.Clear(); }

        // 물 댄 논 (첨벙: 발소리가 크고 걸음이 느리다)
        static readonly List<Rect> paddies = new List<Rect>();
        public static void AddPaddy(Vector3 center, float w, float d) { paddies.Add(new Rect(center.x - w * 0.5f, center.z - d * 0.5f, w, d)); }
        public static bool InPaddy(Vector3 p)
        {
            for (int i = 0; i < paddies.Count; i++) if (paddies[i].Contains(new Vector2(p.x, p.z))) return true;
            return false;
        }

        public static void Add(Vector3 center, float radius, GameObject visual = null)
        {
            spots.Add(new Vector4(center.x, center.y, center.z, radius));
            visuals.Add(visual);
        }

        /// <summary>불바다에 닿은 덤불은 타서 검게 그을리고 더는 숨을 수 없다 (이번 판 내내).</summary>
        public static void Burn(Vector3 at, float radius)
        {
            for (int i = spots.Count - 1; i >= 0; i--)
            {
                var s = spots[i];
                float dx = at.x - s.x, dz = at.z - s.z, r = radius + s.w * 0.6f;
                if (dx * dx + dz * dz > r * r) continue;
                var v = visuals[i];
                if (v != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_Color", new Color(0.07f, 0.06f, 0.05f));
                    foreach (var rd in v.GetComponentsInChildren<Renderer>()) rd.SetPropertyBlock(mpb);
                }
                // 그을음 + 불티, 속에 숨어 있던 매복 망자는 뛰쳐나온다
                var c = new Vector3(s.x, s.y, s.z);
                FX.Scorch(FX.FloorUnder(c + Vector3.up * 0.5f), s.w);
                Effects.Burst(c + Vector3.up * 0.6f, 0.9f, new Color(1f, 0.55f, 0.2f));
                foreach (var lm in Hunt.Monsters.Monster.Alive)
                    if (lm != null && lm.Lurking && (lm.transform.position - c).sqrMagnitude < (s.w + 0.8f) * (s.w + 0.8f)) lm.Disturb();
                spots.RemoveAt(i);
                visuals.RemoveAt(i);
                Hunt.UI.HUD.Notify("덤불이 타 버렸다 — 이제 숨을 수 없다", new Color(1f, 0.6f, 0.35f));
            }
        }

        /// <summary>누가 헤치고 지나가면 그 덤불이 잠깐 흔들린다 (어둠 속에서도 보이는 실마리).</summary>
        public static void Shake(Vector3 p)
        {
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                float dx = p.x - s.x, dz = p.z - s.z;
                if (dx * dx + dz * dz > s.w * s.w * 1.4f) continue;
                var v = visuals[i];
                if (v == null) return;
                var sh = v.GetComponent<BushShake>();
                if (sh == null) sh = v.AddComponent<BushShake>();
                sh.Kick();
                return;
            }
        }

        /// <summary>돌풍: 모든 덤불이 한꺼번에 흔들린다 (매복 덤불의 들썩임도 묻힌다).</summary>
        public static void ShakeAll()
        {
            foreach (var v in visuals)
            {
                if (v == null) continue;
                var sh = v.GetComponent<BushShake>();
                if (sh == null) sh = v.AddComponent<BushShake>();
                sh.Kick();
            }
        }

        /// <summary>이 위치가 덤불 안인가 (높이는 무시).</summary>
        public static bool InBush(Vector3 p)
        {
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                float dx = p.x - s.x, dz = p.z - s.z;
                if (dx * dx + dz * dz < s.w * s.w) return true;
            }
            return false;
        }
    }
}

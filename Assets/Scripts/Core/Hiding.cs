using System.Collections.Generic;
using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 숨을 곳 (덤불). 앉아서 덤불 안에 있으면 망자가 가까이 와야 알아챈다 (Monster.Sense).
    /// VillageBuilder가 덤불을 놓을 때 등록. 마을을 다시 지으면 Clear.
    /// </summary>
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

    public static class Hiding
    {
        static readonly List<Vector4> spots = new List<Vector4>(); // xyz = 중심, w = 반지름
        static readonly List<GameObject> visuals = new List<GameObject>(); // 같은 순서의 덤불 모델 (불타면 검게)

        public static IReadOnlyList<Vector4> Spots => spots;

        public static void Clear() { spots.Clear(); visuals.Clear(); }

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

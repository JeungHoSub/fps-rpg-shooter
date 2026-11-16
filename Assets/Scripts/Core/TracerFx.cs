using UnityEngine;

namespace Hunt.Core
{
    /// <summary>총구 → 탄착점으로 빠르게 날아가는 예광탄 선.</summary>
    public class TracerFx : MonoBehaviour
    {
        const float Speed = 320f;
        const float Length = 4f;

        Vector3 from, dir;
        float dist, traveled;
        LineRenderer lr;

        public void Init(Vector3 a, Vector3 b)
        {
            from = a;
            Vector3 d = b - a;
            dist = Mathf.Max(0.01f, d.magnitude);
            dir = d / dist;
            traveled = Mathf.Min(dist, Speed * 0.012f);
            lr = GetComponent<LineRenderer>();
            Apply();
        }

        void Update()
        {
            traveled += Speed * Time.deltaTime;
            if (traveled - Length >= dist) { Destroy(gameObject); return; }
            Apply();
        }

        void Apply()
        {
            float head = Mathf.Min(traveled, dist);
            float tail = Mathf.Clamp(traveled - Length, 0f, dist);
            lr.SetPosition(0, from + dir * head);
            lr.SetPosition(1, from + dir * tail);
        }
    }
}

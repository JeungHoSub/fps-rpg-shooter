using Hunt.Core;
using UnityEngine;

namespace Hunt.Monsters
{
    /// <summary>죽은 좀비: 마지막 자세 그대로 총알 방향으로 넘어진 뒤 땅속으로 가라앉아 사라진다.</summary>
    public class ZombieCorpse : MonoBehaviour
    {
        Vector3 axis;
        float t, unit;
        Quaternion startRot;
        Vector3 startPos, slide;

        public void Begin(Vector3 hitDir, float unit)
        {
            this.unit = unit;
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.0001f) hitDir = -transform.forward;
            hitDir.Normalize();
            axis = Vector3.Cross(Vector3.up, hitDir);
            startRot = transform.rotation;
            startPos = transform.position;
            slide = hitDir * 0.9f * unit;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            t += Time.deltaTime;
            // 0~0.45초: 넘어짐 (가속) → 살짝 튕김
            float fall = Mathf.Clamp01(t / 0.45f);
            float ang = Mathf.Lerp(0f, 88f, fall * fall);
            if (t > 0.45f) ang = 88f - Mathf.Sin(Mathf.Clamp01((t - 0.45f) / 0.25f) * Mathf.PI) * 6f;
            transform.rotation = Quaternion.AngleAxis(ang, axis) * startRot;
            var pos = startPos + slide * Mathf.Clamp01(t / 0.5f);
            // 3초 뒤 가라앉기
            if (t > 3f) pos.y -= (t - 3f) * 0.5f * unit;
            transform.position = pos;
            if (t > 5f) Destroy(gameObject);
        }
    }
}

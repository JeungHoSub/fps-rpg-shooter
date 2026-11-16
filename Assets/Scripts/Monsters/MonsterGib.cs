using Hunt.Core;
using UnityEngine;

namespace Hunt.Monsters
{
    /// <summary>괴물 사망 시 몸 조각이 총알 방향으로 흩어져 떨어진 뒤 사라진다.</summary>
    public class MonsterGib : MonoBehaviour
    {
        Vector3 velocity, spin, baseScale;
        float life, halfHeight;

        public static void Shatter(Monster m, Vector3 hitDir)
        {
            var parent = m.transform.parent;
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.01f) hitDir = Random.insideUnitSphere;
            hitDir.Normalize();
            float force = m.IsBoss ? 3f : 5f;

            var parts = m.GetComponentsInChildren<Renderer>();
            foreach (var r in parts)
            {
                var go = r.gameObject;
                go.layer = 2; // 사격 판정 제외
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var mp = go.GetComponent<MonsterPart>();
                if (mp != null) Destroy(mp);

                go.transform.SetParent(parent, true);
                var gib = go.AddComponent<MonsterGib>();
                gib.velocity = hitDir * Random.Range(1f, force) + Vector3.up * Random.Range(1.5f, 4f) + Random.insideUnitSphere * 1.5f;
                gib.spin = Random.insideUnitSphere * 400f;
                gib.life = Random.Range(1.8f, 2.6f);
                gib.baseScale = go.transform.localScale;
                gib.halfHeight = go.transform.lossyScale.y * 0.4f;
            }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            float dt = Time.deltaTime;

            velocity += Physics.gravity * dt;
            var p = transform.position + velocity * dt;
            if (p.y < halfHeight)
            {
                p.y = halfHeight;
                velocity = new Vector3(velocity.x * 0.5f, -velocity.y * 0.25f, velocity.z * 0.5f);
                spin *= 0.6f;
            }
            transform.position = p;
            transform.Rotate(spin * dt, Space.World);

            life -= dt;
            if (life < 0.5f) transform.localScale = baseScale * Mathf.Max(0f, life / 0.5f);
            if (life <= 0f) Destroy(gameObject);
        }
    }
}

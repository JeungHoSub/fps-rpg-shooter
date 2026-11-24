using Hunt.Core;
using UnityEngine;

namespace Hunt.Monsters
{
    /// <summary>괴물 사망 시 몸 조각이 총알 방향으로 흩어져 떨어진 뒤 사라진다.</summary>
    public class MonsterGib : MonoBehaviour
    {
        Vector3 velocity, spin, baseScale;
        float life, halfHeight, floorY; // 바닥 높이 (언덕진 땅: 처음에 한 번 잰다)

        /// <summary>살점 조각 하나 (머리·급소 처치 때 튀어 오른다).</summary>
        public static void Chunk(Transform parent, Vector3 pos, Vector3 vel, Color col, float size)
        {
            GameObject go;
            if (Random.value < 0.5f) { go = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(go.GetComponent<Collider>()); }
            else go = FX.LowSphere(); // 가벼운 공
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Random.rotation;
            go.transform.localScale = new Vector3(size, size * Random.Range(0.5f, 1f), size * Random.Range(0.6f, 1.2f));
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = EnvMaterials.Plain(col, 0.75f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var gib = go.AddComponent<MonsterGib>();
            gib.velocity = vel;
            gib.spin = Random.insideUnitSphere * 600f;
            gib.life = Random.Range(2.5f, 4f);
            gib.baseScale = go.transform.localScale;
            gib.halfHeight = size * 0.35f;
            gib.floorY = FX.FloorUnder(pos).y;
        }

        public static void Shatter(Monster m, Vector3 hitDir)
        {
            var parent = m.transform.parent;
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.01f) hitDir = Random.insideUnitSphere;
            hitDir.Normalize();
            float force = m.IsBoss ? 3f : 5f;
            float fy = FX.FloorUnder(m.transform.position).y;

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
                gib.floorY = fy;
            }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            float dt = Time.deltaTime;

            velocity += Physics.gravity * dt;
            var p = transform.position + velocity * dt;
            if (p.y < floorY + halfHeight)
            {
                p.y = floorY + halfHeight;
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

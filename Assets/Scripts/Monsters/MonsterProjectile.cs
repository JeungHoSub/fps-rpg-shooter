using Hunt.Core;
using Hunt.Player;
using UnityEngine;

namespace Hunt.Monsters
{
    /// <summary>스피터/보스의 산성 투사체. 물리 없이 거리 판정으로 처리한다.</summary>
    public class MonsterProjectile : MonoBehaviour
    {
        const float Speed = 16f;

        Vector3 velocity;
        PlayerCharacter target;
        float damage, infection, life = 5f;

        public static void Launch(Transform parent, Vector3 from, PlayerCharacter target, float damage, float infection)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "AcidSpit";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.35f;
            go.GetComponent<Renderer>().material.color = new Color(0.55f, 1f, 0.2f);

            var p = go.AddComponent<MonsterProjectile>();
            p.target = target;
            p.damage = damage;
            p.infection = infection;
            Vector3 aim = target.transform.position + Vector3.up * 1.2f;
            p.velocity = (aim - from).normalized * Speed + Vector3.up * 1.5f;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsWorldRunning) return;

            float dt = Time.deltaTime;
            velocity += Physics.gravity * 0.25f * dt;
            transform.position += velocity * dt;
            life -= dt;

            if (target != null)
            {
                Vector3 center = target.transform.position + Vector3.up * 1f;
                if ((transform.position - center).sqrMagnitude < 0.8f * 0.8f)
                {
                    target.TakeDamage(damage, infection, transform.position - velocity.normalized * 3f);
                    Destroy(gameObject);
                    return;
                }
            }
            if (life <= 0f || transform.position.y < 0f) Destroy(gameObject);
        }
    }
}

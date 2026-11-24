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
            var go = FX.LowSphere();
            go.name = "AcidSpit";
            go.transform.SetParent(parent, true);
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.35f;
            go.GetComponent<Renderer>().sharedMaterial = EnvMaterials.Emissive(new Color(0.55f, 1f, 0.2f), 1.2f); // 공유 재질 (침마다 재질이 새로 남지 않게), 어둠에서도 보이게 빛남

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
                    target.LastAttacker = "침 뱉는 망자";
                    target.DeathCause = "역병 침을 맞고 쓰러짐";
                    target.TakeDamage(damage, infection, transform.position - velocity.normalized * 3f);
                    Hunt.UI.GameHud.AcidFlash = 1f;
                    SFX.PlayAt(SFX.FleshHit, transform.position, 0.6f);
                    Destroy(gameObject);
                    return;
                }
            }
            if (life <= 0f) { Destroy(gameObject); return; }
            // 땅·벽에 닿으면 터지며 초록 얼룩 (전에는 땅을 뚫고 지나갔다)
            if (Physics.Raycast(transform.position, velocity.normalized, out var hit, velocity.magnitude * dt + 0.2f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<Monster>() == null && hit.collider.GetComponentInParent<PlayerCharacter>() == null)
            {
                Effects.Burst(hit.point, 0.3f, new Color(0.5f, 0.85f, 0.15f));
                SFX.PlayAt(SFX.FleshHit, hit.point, 0.35f);
                Destroy(gameObject);
                return;
            }
            if (transform.position.y < -1f) Destroy(gameObject);
        }
    }
}

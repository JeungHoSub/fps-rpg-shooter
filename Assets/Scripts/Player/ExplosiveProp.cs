using Hunt.Core;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>
    /// 쏘면 터지는 소품: LPG 가스통·드럼통. 몇 발 맞으면 '쉬익' 새다가 0.7초 뒤 터진다 (화염은 바로).
    /// 망자 무리를 유인해 터뜨리는 맛. 너무 가까우면 나도 다친다 (Gadgets.Explode).
    /// </summary>
    public class ExplosiveProp : MonoBehaviour
    {
        float hp, radius, damage, fuse = -1f;
        bool done;

        public void Setup(float hp, float radius, float damage)
        {
            this.hp = hp; this.radius = radius; this.damage = damage;
        }

        /// <summary>총알이 맞았을 때. fire = 화염(바로 붙는다).</summary>
        public void Hit(float amount, bool fire)
        {
            if (done || fuse >= 0f) return;
            hp -= fire ? amount * 3f : amount;
            if (hp > 0f) return;
            fuse = fire ? 0.25f : 0.7f;
            SFX.PlayAt3D(SFX.Swing, transform.position + Vector3.up * 0.6f, 0.9f, 0.45f); // 쉬익 새는 소리
            FX.ImpactWorld(transform.position + Vector3.up * 0.8f, Vector3.up);
        }

        void Update()
        {
            if (done || fuse < 0f) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            fuse -= Time.deltaTime;
            if (fuse > 0f) return;
            done = true;
            var owner = gm != null ? gm.Player : null;
            Gadgets.Explode(transform.position + Vector3.up * 0.6f, radius, damage, owner);
            Destroy(gameObject);
        }
    }
}

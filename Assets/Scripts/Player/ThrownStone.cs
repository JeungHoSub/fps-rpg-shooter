using Hunt.Core;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>
    /// 돌 던지기 (B): 떨어진 자리에서 '딱' 소리 → 근처 망자(14m)가 그쪽을 뒤지러 간다.
    /// 플레이어 소리로 치지 않아 쫓아오지는 않는다. 몰래 지나가거나 등 뒤 칼질용.
    /// </summary>
    public class ThrownStone : MonoBehaviour
    {
        public const float Cooldown = 4f;
        public static float ReadyAt;
        bool landed;
        float life = 6f;

        public static bool Ready => Time.time >= ReadyAt;

        public static void Throw(Transform cam)
        {
            if (!Ready || cam == null) return;
            ReadyAt = Time.time + Cooldown;
            var go = FX.LowSphere();
            go.name = "Stone";
            go.layer = 2;
            go.transform.position = cam.position + cam.forward * 0.5f - cam.up * 0.1f;
            go.transform.localScale = new Vector3(0.09f, 0.07f, 0.08f);
            go.GetComponent<Renderer>().sharedMaterial = EnvMaterials.Plain(new Color(0.35f, 0.34f, 0.32f), 0.3f);
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            var cc = cam.GetComponentInParent<CharacterController>();
            if (cc != null) Physics.IgnoreCollision(col, cc); // 던지자마자 내 몸에 맞지 않게
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            Vector3 v = cam.forward * 17f + Vector3.up * 3f;
#if UNITY_2023_3_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<ThrownStone>();
            SFX.Play(SFX.Swing, 0.35f, 0.1f);
        }

        void OnCollisionEnter(Collision c)
        {
            if (landed) return;
            landed = true;
            SFX.PlayAt(SFX.StoneClack, transform.position, 0.7f);
            Noise.Emit(transform.position, 14f, false);
            life = 2f;
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }
    }
}

using Hunt.Core;
using UnityEngine;

namespace Hunt.Monsters
{
    /// <summary>머리에 맞아 벗겨진 갓: 빙글빙글 날아갔다가 땅에 떨어져 잠시 남은 뒤 사라진다.</summary>
    public class HatFly : MonoBehaviour
    {
        Vector3 vel, spin;
        float life = 8f, groundY;
        bool landed;

        public void Launch(Vector3 v)
        {
            vel = v;
            spin = new Vector3(Random.Range(-500f, 500f), Random.Range(-300f, 300f), Random.Range(-500f, 500f));
            groundY = FX.FloorUnder(transform.position).y + 0.04f;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            float dt = Time.deltaTime;
            if (!landed)
            {
                vel += Physics.gravity * dt;
                vel *= 1f - 0.6f * dt; // 넓은 갓 테가 공기를 받아 천천히
                var p = transform.position + vel * dt;
                if (p.y <= groundY && vel.y < 0f)
                {
                    p.y = groundY;
                    if (vel.y < -3f) { vel = new Vector3(vel.x * 0.4f, -vel.y * 0.25f, vel.z * 0.4f); spin *= 0.4f; }
                    else
                    {
                        landed = true;
                        // 바닥에 납작하게 눕는다
                        var e = transform.eulerAngles;
                        transform.rotation = Quaternion.Euler(Random.Range(-12f, 12f), e.y, Random.Range(-12f, 12f));
                    }
                }
                transform.position = p;
                if (!landed) transform.Rotate(spin * dt, Space.World);
            }
            life -= dt;
            if (life < 1f) transform.localScale = Vector3.one * Mathf.Max(0f, life);
            if (life <= 0f) Destroy(gameObject);
        }
    }
}

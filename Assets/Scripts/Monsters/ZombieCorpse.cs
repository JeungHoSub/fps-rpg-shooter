using Hunt.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Hunt.Monsters
{
    /// <summary>죽은 좀비: 마지막 자세 그대로 총알 방향으로 넘어진 뒤 땅속으로 가라앉아 사라진다.</summary>
    public class ZombieCorpse : MonoBehaviour
    {
        Vector3 axis;
        float t, unit;
        Quaternion startRot;
        Vector3 startPos, slide;

        PlayableGraph graph;
        AnimationClipPlayable deathPlayer;
        float deathLength = -1f;

        public void Begin(Vector3 hitDir, float unit, AnimationClip deathClip = null, Animator animator = null)
        {
            this.unit = unit;
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.0001f) hitDir = -transform.forward;
            hitDir.Normalize();
            axis = Vector3.Cross(Vector3.up, hitDir);
            startRot = transform.rotation;
            startPos = transform.position;
            slide = hitDir * 0.9f * unit;

            // 실제 쓰러지는 동작 (Mixamo Zombie Death)
            if (deathClip != null && animator != null)
            {
                animator.enabled = true;
                graph = PlayableGraph.Create("ZombieDeath");
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                var output = AnimationPlayableOutput.Create(graph, "Death", animator);
                deathPlayer = AnimationClipPlayable.Create(graph, deathClip);
                deathPlayer.SetApplyFootIK(false);
                output.SetSourcePlayable(deathPlayer);
                graph.Play();
                deathLength = deathClip.length;
                slide = hitDir * 0.3f * unit;
            }
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            t += Time.deltaTime;
            if (deathLength > 0f)
            {
                // 동작 끝 프레임에서 멈춘 채 누워 있다가 가라앉는다
                if (graph.IsValid() && deathPlayer.GetTime() >= deathLength) { deathPlayer.SetTime(deathLength); deathPlayer.SetSpeed(0); }
                var p = startPos + slide * Mathf.Clamp01(t / 0.8f);
                float sinkAt = deathLength + 2f;
                if (t > sinkAt) p.y -= (t - sinkAt) * 0.5f * unit;
                transform.position = p;
                if (t > sinkAt + 2f) Destroy(gameObject);
                return;
            }
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

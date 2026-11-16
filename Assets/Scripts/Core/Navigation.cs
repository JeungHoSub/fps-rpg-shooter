using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hunt.Core
{
    /// <summary>
    /// 런타임 NavMesh. 아레나가 코드로 생성되므로 시작 시 직접 굽는다 (패키지 불필요, 기본 AI 모듈 사용).
    /// 괴물은 NavMeshAgent 대신 경로(코너)만 받아 자체 이동 로직(넉백·분리·경직)으로 움직인다.
    /// </summary>
    public static class Navigation
    {
        public const float AgentRadius = 0.45f;

        static NavMeshDataInstance instance;
        public static bool Ready { get; private set; }

        public static void Build(Transform arenaRoot)
        {
            Clear();

            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(arenaRoot, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = AgentRadius;
            settings.agentHeight = 2f;
            settings.agentClimb = 0.4f;
            settings.agentSlope = 45f;

            var bounds = new Bounds(Vector3.zero, new Vector3(110f, 20f, 110f));
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogWarning("[Hunt] NavMesh 생성 실패 — 괴물이 직선 추적으로 동작합니다.");
                return;
            }
            instance = NavMesh.AddNavMeshData(data);
            Ready = instance.valid;
        }

        public static void Clear()
        {
            if (instance.valid) instance.Remove();
            Ready = false;
        }

        /// <summary>가장 가까운 NavMesh 위 지점으로 보정 (y=0 지면 기준).</summary>
        public static Vector3 Snap(Vector3 p, float maxDistance = 4f)
        {
            if (Ready && NavMesh.SamplePosition(p, out var hit, maxDistance, NavMesh.AllAreas))
                p = hit.position;
            p.y = 0f;
            return p;
        }

        /// <returns>코너 개수 (0이면 경로 없음)</returns>
        public static int FindPath(Vector3 from, Vector3 to, NavMeshPath path, Vector3[] corners)
        {
            if (!Ready) return 0;
            if (!NavMesh.SamplePosition(from, out var a, 2f, NavMesh.AllAreas)) return 0;
            if (!NavMesh.SamplePosition(to, out var b, 4f, NavMesh.AllAreas)) return 0;
            if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)) return 0;
            if (path.status == NavMeshPathStatus.PathInvalid) return 0;
            return path.GetCornersNonAlloc(corners);
        }

        /// <summary>
        /// from → to 이동을 NavMesh 안으로 제한한다. 장애물에 막히면 벽면을 따라 미끄러진다.
        /// 넉백으로 기둥 속에 박히는 것을 막는다.
        /// </summary>
        public static Vector3 Constrain(Vector3 from, Vector3 to)
        {
            if (!Ready) return to;
            if (!NavMesh.SamplePosition(from, out var s, 1.5f, NavMesh.AllAreas)) return to;
            from = s.position;
            to.y = from.y;

            if (!NavMesh.Raycast(from, to, out var hit, NavMesh.AllAreas))
            {
                to.y = 0f;
                return to;
            }

            // 막힌 지점에서 벽면 방향으로 남은 이동량만큼 미끄러지기
            Vector3 remaining = to - hit.position;
            Vector3 normal = hit.normal;
            normal.y = 0f;
            Vector3 slide = normal.sqrMagnitude > 0.0001f ? remaining - Vector3.Project(remaining, normal.normalized) : Vector3.zero;
            Vector3 result = hit.position;
            if (slide.sqrMagnitude > 0.000001f)
            {
                Vector3 slideTo = hit.position + slide;
                result = NavMesh.Raycast(hit.position, slideTo, out var hit2, NavMesh.AllAreas) ? hit2.position : slideTo;
            }
            result.y = 0f;
            return result;
        }
    }
}

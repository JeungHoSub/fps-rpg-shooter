using System.Collections.Generic;
using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 숨을 곳 (덤불). 앉아서 덤불 안에 있으면 망자가 가까이 와야 알아챈다 (Monster.Sense).
    /// VillageBuilder가 덤불을 놓을 때 등록. 마을을 다시 지으면 Clear.
    /// </summary>
    public static class Hiding
    {
        static readonly List<Vector4> spots = new List<Vector4>(); // xyz = 중심, w = 반지름

        public static IReadOnlyList<Vector4> Spots => spots;

        public static void Clear() { spots.Clear(); }

        public static void Add(Vector3 center, float radius) { spots.Add(new Vector4(center.x, center.y, center.z, radius)); }

        /// <summary>이 위치가 덤불 안인가 (높이는 무시).</summary>
        public static bool InBush(Vector3 p)
        {
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                float dx = p.x - s.x, dz = p.z - s.z;
                if (dx * dx + dz * dz < s.w * s.w) return true;
            }
            return false;
        }
    }
}

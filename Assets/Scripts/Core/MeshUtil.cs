using System.Collections.Generic;
using UnityEngine;

namespace Hunt.Core
{
    public static class MeshUtil
    {
        /// <summary>
        /// 양면 메시: 뒷면은 점을 복제하고 감는 방향을 뒤집는다.
        /// (같은 점을 앞뒤가 공유하면 노멀이 상쇄되어 새까맣게 보인다)
        /// </summary>
        public static Mesh DoubleSided(string name, List<Vector3> v, List<Vector2> uv, List<int> frontTris)
        {
            int n = v.Count;
            var vv = new List<Vector3>(v); vv.AddRange(v);
            var uu = new List<Vector2>(uv); uu.AddRange(uv);
            var tt = new List<int>(frontTris);
            for (int i = 0; i < frontTris.Count; i += 3)
            {
                tt.Add(frontTris[i] + n);
                tt.Add(frontTris[i + 2] + n);
                tt.Add(frontTris[i + 1] + n);
            }
            var mesh = new Mesh { name = name };
            if (vv.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vv);
            mesh.SetUVs(0, uu);
            mesh.SetTriangles(tt, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

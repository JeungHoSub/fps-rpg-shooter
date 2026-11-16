using UnityEngine;

namespace Hunt.Core
{
    /// <summary>임시 이펙트 (탄도/폭발). 아트가 들어오면 파티클로 교체.</summary>
    public static class Effects
    {
        static Material lineMat;

        static Material LineMat
        {
            get
            {
                if (lineMat == null)
                {
                    var shader = Shader.Find("Sprites/Default");
                    lineMat = new Material(shader);
                }
                return lineMat;
            }
        }

        public static Transform Root;

        public static void Line(Vector3 a, Vector3 b, Color c, float width, float life)
        {
            var go = new GameObject("Tracer");
            if (Root != null) go.transform.SetParent(Root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = LineMat;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = width;
            lr.endWidth = width * 0.5f;
            lr.startColor = c;
            lr.endColor = new Color(c.r, c.g, c.b, 0.2f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Object.Destroy(go, life);
        }

        public static void Burst(Vector3 p, float radius, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Burst";
            Object.Destroy(go.GetComponent<Collider>());
            if (Root != null) go.transform.SetParent(Root, true);
            go.transform.position = p;
            go.transform.localScale = Vector3.one * radius * 2f;
            go.GetComponent<Renderer>().material.color = c;
            Object.Destroy(go, 0.12f);
        }
    }
}

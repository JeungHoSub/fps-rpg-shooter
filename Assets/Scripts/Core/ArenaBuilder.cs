using UnityEngine;

namespace Hunt.Core
{
    /// <summary>테스트용 100×100m 아레나. 레벨 디자인이 들어오면 씬으로 대체.</summary>
    public static class ArenaBuilder
    {
        public static void Build(Transform root)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            ground.GetComponent<Renderer>().material.color = new Color(0.22f, 0.24f, 0.22f);

            Color wall = new Color(0.3f, 0.3f, 0.33f);
            Box(root, new Vector3(0, 2.5f, 50), new Vector3(100, 5, 1), wall);
            Box(root, new Vector3(0, 2.5f, -50), new Vector3(100, 5, 1), wall);
            Box(root, new Vector3(50, 2.5f, 0), new Vector3(1, 5, 100), wall);
            Box(root, new Vector3(-50, 2.5f, 0), new Vector3(1, 5, 100), wall);

            // 엄폐물 / 기둥 (고정 시드 → 매번 같은 맵)
            var state = Random.state;
            Random.InitState(1234);
            Color cover = new Color(0.4f, 0.38f, 0.34f);
            for (int i = 0; i < 14; i++)
            {
                Vector2 p = Random.insideUnitCircle * 30f;
                if (p.magnitude < 8f) continue;
                bool pillar = Random.value < 0.4f;
                var size = pillar ? new Vector3(1.5f, 4f, 1.5f) : new Vector3(Random.Range(2f, 5f), 1.2f, Random.Range(0.8f, 1.5f));
                var b = Box(root, new Vector3(p.x, size.y / 2f, p.y), size, cover);
                b.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 180f), 0);
            }
            Random.state = state;

            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Sun");
                lightGo.transform.SetParent(root, false);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.1f;
                l.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.12f, 0.12f, 0.15f);
            RenderSettings.fogDensity = 0.015f;
        }

        static GameObject Box(Transform root, Vector3 pos, Vector3 size, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().material.color = c;
            return go;
        }
    }
}

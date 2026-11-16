using System.Collections.Generic;
using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 사격 이펙트: 파편 파티클, 불꽃, 예광탄, 탄흔.
    /// 파티클은 전역 ParticleSystem 2개에 Emit만 해서 오브젝트 생성 비용을 줄인다.
    /// </summary>
    public static class FX
    {
        static ParticleSystem debris;   // 먼지·피 (빌보드)
        static ParticleSystem sparks;   // 불꽃 (속도 방향으로 늘어남)
        static Transform root;
        static readonly List<Transform> holes = new List<Transform>();
        static int holeIndex;
        const int MaxHoles = 120;

        static Material unlitBase;
        static readonly Dictionary<Color, Material> unlitCache = new Dictionary<Color, Material>();

        /// <summary>조명 영향을 받지 않는 단색 머티리얼 (빛나는 표시용).</summary>
        public static Material UnlitMaterial(Color c)
        {
            if (unlitCache.TryGetValue(c, out var m) && m != null) return m;
            if (unlitBase == null) unlitBase = new Material(Shader.Find("Sprites/Default"));
            m = new Material(unlitBase) { color = c };
            unlitCache[c] = m;
            return m;
        }

        public static void Init(Transform worldRoot)
        {
            root = worldRoot;
            holes.Clear();
            holeIndex = 0;
            debris = MakeSystem("FX_Debris", 1.3f, ParticleSystemRenderMode.Billboard);
            sparks = MakeSystem("FX_Sparks", 0.7f, ParticleSystemRenderMode.Stretch);
        }

        static ParticleSystem MakeSystem(string name, float gravity, ParticleSystemRenderMode mode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = UnlitMaterial(Color.white);
            r.renderMode = mode;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                r.velocityScale = 0.025f;
                r.lengthScale = 1f;
            }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color col)
        {
            if (ps == null) return;
            var ep = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startSize = size,
                startLifetime = life,
                startColor = col,
            };
            ps.Emit(ep, 1);
        }

        /// <summary>벽·바닥 명중: 불꽃 + 먼지 + 탄흔.</summary>
        public static void ImpactWorld(Vector3 point, Vector3 normal)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector3 v = (normal + Random.insideUnitSphere * 0.8f).normalized * Random.Range(4f, 9f);
                Emit(sparks, point, v, Random.Range(0.02f, 0.035f), Random.Range(0.12f, 0.25f), new Color(1f, Random.Range(0.6f, 0.9f), 0.3f));
            }
            for (int i = 0; i < 4; i++)
            {
                Vector3 v = (normal + Random.insideUnitSphere * 0.6f).normalized * Random.Range(0.8f, 2f);
                Emit(debris, point + normal * 0.02f, v, Random.Range(0.08f, 0.16f), Random.Range(0.35f, 0.6f), new Color(0.55f, 0.52f, 0.47f, 0.8f));
            }
            BulletHole(point, normal);
        }

        /// <summary>괴물 명중: 체액 튀김. 약점은 주황색 진액, 장갑은 뼛조각.</summary>
        public static void ImpactFlesh(Vector3 point, Vector3 shotDir, Color color, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                Vector3 v = (shotDir * 0.6f + Random.insideUnitSphere).normalized * Random.Range(1.5f, 4.5f) + Vector3.up;
                Emit(debris, point, v, Random.Range(0.04f, 0.1f), Random.Range(0.3f, 0.6f), color * Random.Range(0.75f, 1.1f));
            }
        }

        public static void BloodBurst(Vector3 center, Color color, float scale)
        {
            int n = Mathf.RoundToInt(25 * scale);
            for (int i = 0; i < n; i++)
            {
                Vector3 v = Random.insideUnitSphere * 4f * scale + Vector3.up * 3f;
                Emit(debris, center + Random.insideUnitSphere * 0.3f * scale, v, Random.Range(0.06f, 0.18f) * scale, Random.Range(0.4f, 0.9f), color * Random.Range(0.7f, 1.1f));
            }
        }

        static void BulletHole(Vector3 point, Vector3 normal)
        {
            if (root == null) return;
            Transform h;
            if (holes.Count < MaxHoles)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = "BulletHole";
                go.transform.SetParent(root, false);
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = UnlitMaterial(new Color(0.05f, 0.05f, 0.05f));
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                h = go.transform;
                holes.Add(h);
            }
            else
            {
                h = holes[holeIndex];
                holeIndex = (holeIndex + 1) % MaxHoles;
                if (h == null) return;
            }
            h.position = point + normal * 0.004f;
            h.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            h.localScale = Vector3.one * Random.Range(0.05f, 0.08f);
        }

        /// <summary>총구 → 탄착점으로 날아가는 예광탄.</summary>
        public static void Tracer(Vector3 from, Vector3 to, Color color, float width)
        {
            var go = new GameObject("Tracer");
            if (root != null) go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = UnlitMaterial(Color.white);
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width * 0.3f;
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            var t = go.AddComponent<TracerFx>();
            t.Init(from, to);
        }
    }
}

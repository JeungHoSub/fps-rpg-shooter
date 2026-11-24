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
            scorches.Clear();
            scorchIndex = 0;
            splats.Clear();
            splatIndex = 0;
            holeIndex = 0;
            pools.Clear(); // 다시 시작하면 예전 피 웅덩이(파괴됨)를 버린다 — 안 그러면 웅덩이가 영영 안 생겼다
            poolIndex = 0;
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

        // ---- 피 웅덩이: 쓰러진 자리에 번져 나가는 검붉은 얼룩 (오래 남다가 옅어진다) ----
        static Texture2D poolTex;
        static Material poolMat;
        static readonly List<Transform> pools = new List<Transform>();
        static int poolIndex;
        const int MaxPools = 18;

        /// <summary>피 얼룩 무늬 텍스처 (화면 피 튀김 UI에서도 쓴다).</summary>
        public static Texture2D BloodTexture { get { PoolMaterial(); return poolTex; } }

        static Material PoolMaterial()
        {
            if (poolMat != null) return poolMat;
            if (poolTex == null)
            {
                // 둥글지만 가장자리가 울퉁불퉁한 얼룩 (128px, 코드로 생성)
                const int S = 128;
                poolTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "BloodPool" };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = (x - S / 2f) / (S / 2f), dy = (y - S / 2f) / (S / 2f);
                        float ang = Mathf.Atan2(dy, dx);
                        float edge = 0.72f + 0.12f * Mathf.Sin(ang * 5f + 1.3f) + 0.08f * Mathf.Sin(ang * 11f) + 0.1f * (Mathf.PerlinNoise(x * 0.06f, y * 0.06f) - 0.5f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01((edge - d) * 14f);
                        // 작은 튄 자국
                        if (a <= 0f && Mathf.PerlinNoise(x * 0.25f + 7f, y * 0.25f) > 0.78f && d < 0.98f) a = 0.8f;
                        float shade = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.1f, y * 0.1f + 3f);
                        px[y * S + x] = new Color32((byte)(255 * shade), (byte)(255 * shade), (byte)(255 * shade), (byte)(255 * a));
                    }
                poolTex.SetPixels32(px);
                poolTex.Apply(true, true);
            }
            poolMat = new Material(Shader.Find("Sprites/Default")) { name = "BloodPool", mainTexture = poolTex, color = new Color(0.22f, 0.015f, 0.02f, 0.9f) };
            poolMat.renderQueue = 2460;
            return poolMat;
        }

        static readonly RaycastHit[] floorHits = new RaycastHit[16];

        /// <summary>
        /// pos 바로 아래 땅(바닥·기단·논두렁)의 점. 망자 판정체·시체·플레이어·장식 없는 층은 무시한다.
        /// 땅을 못 찾으면 높이 0.
        /// </summary>
        public static Vector3 FloorUnder(Vector3 pos)
        {
            Vector3 from = new Vector3(pos.x, pos.y + 1.2f, pos.z);
            int n = Physics.RaycastNonAlloc(from, Vector3.down, floorHits, 4f, ~((1 << 2) | (1 << 1)), QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 result = new Vector3(pos.x, 0f, pos.z);
            for (int i = 0; i < n; i++)
            {
                var h = floorHits[i];
                if (h.collider == null || h.distance >= best) continue;
                if (h.normal.y < 0.6f) continue; // 벽·비탈 면 말고 바닥만
                if (h.collider.GetComponentInParent<Hunt.Monsters.Monster>() != null) continue;
                if (h.collider.GetComponent<Hunt.Monsters.MonsterPart>() != null) continue;
                if (h.collider is CharacterController) continue;
                if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue; // 굴러가는 수류탄 등
                best = h.distance;
                result = h.point;
            }
            return result;
        }

        public static void BloodPool(Vector3 pos, float size)
        {
            if (root == null || !GameSettings.Gore) return;
            Transform t;
            if (pools.Count < MaxPools)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = "BloodPool";
                go.layer = 2;
                go.transform.SetParent(root, false);
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = PoolMaterial();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.AddComponent<BloodPoolFx>();
                t = go.transform;
                pools.Add(t);
            }
            else
            {
                t = pools[poolIndex];
                poolIndex = (poolIndex + 1) % MaxPools;
                if (t == null) return;
            }
            // 바닥 높이에 맞춤 (논두렁·기단 위도). 망자 몸·플레이어는 건너뛴다 — 안 그러면 쓰러지는 망자 가슴에 맞아 피가 공중에 떴다
            Vector3 p = FloorUnder(pos);
            t.position = p + Vector3.up * (0.012f + Random.value * 0.006f);
            t.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            t.GetComponent<BloodPoolFx>().Begin(size * Random.Range(1.5f, 2.2f));
        }

        // ---- 그을음 자국 (폭발 자리) — 피 웅덩이 무늬를 검게 ----
        static Material scorchMat;
        static readonly List<Transform> scorches = new List<Transform>();
        static int scorchIndex;
        const int MaxScorch = 8;

        public static void Scorch(Vector3 pos, float size)
        {
            if (root == null) return;
            if (scorchMat == null)
            {
                scorchMat = new Material(PoolMaterial()) { name = "Scorch", color = new Color(0.04f, 0.035f, 0.03f, 0.85f) };
                scorchMat.renderQueue = 2459;
            }
            Transform t;
            if (scorches.Count < MaxScorch)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = "Scorch";
                go.layer = 2;
                go.transform.SetParent(root, false);
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = scorchMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.AddComponent<BloodPoolFx>();
                t = go.transform;
                scorches.Add(t);
            }
            else
            {
                t = scorches[scorchIndex];
                scorchIndex = (scorchIndex + 1) % MaxScorch;
                if (t == null) return;
            }
            Vector3 p = FloorUnder(pos);
            t.position = p + Vector3.up * 0.01f;
            t.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            t.GetComponent<BloodPoolFx>().Begin(size * 2f);
        }

        // ---- 뒤쪽 벽·바닥에 튄 피 (망자를 맞히면 총알 방향 뒤로) ----
        static Material splatMat;
        static readonly List<Transform> splats = new List<Transform>();
        static int splatIndex;
        const int MaxSplats = 14;
        static readonly RaycastHit[] splatHits = new RaycastHit[8];

        /// <summary>
        /// from에서 dir 방향 maxDist 안에 벽·바닥이 있으면 피 자국을 남긴다 (망자·플레이어는 건너뜀).
        /// 피 웅덩이 무늬를 작게, 면에 붙여서. 14개를 돌려 쓴다.
        /// </summary>
        public static void BloodSplat(Vector3 from, Vector3 dir, float maxDist, float size)
        {
            if (root == null || !GameSettings.Gore) return;
            int n = Physics.RaycastNonAlloc(from, dir, splatHits, maxDist, ~((1 << 2) | (1 << 1)), QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            RaycastHit found = default;
            for (int i = 0; i < n; i++)
            {
                var h = splatHits[i];
                if (h.collider == null || h.distance >= best) continue;
                if (h.collider.GetComponentInParent<Hunt.Monsters.Monster>() != null) continue;
                if (h.collider.GetComponent<Hunt.Monsters.MonsterPart>() != null) continue;
                if (h.collider is CharacterController) continue;
                if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;
                best = h.distance;
                found = h;
            }
            if (best == float.MaxValue) return;

            if (splatMat == null)
            {
                splatMat = new Material(PoolMaterial()) { name = "BloodSplat", color = new Color(0.32f, 0.02f, 0.03f, 0.85f) };
                splatMat.renderQueue = 2461;
            }
            Transform t;
            if (splats.Count < MaxSplats)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = "BloodSplat";
                go.layer = 2;
                go.transform.SetParent(root, false);
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = splatMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.AddComponent<BloodPoolFx>();
                t = go.transform;
                splats.Add(t);
            }
            else
            {
                t = splats[splatIndex];
                splatIndex = (splatIndex + 1) % MaxSplats;
                if (t == null) return;
            }
            t.position = found.point + found.normal * 0.006f;
            t.rotation = Quaternion.LookRotation(-found.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            // 멀수록 옅고 작게 튄다
            float k = 1f - best / maxDist;
            t.GetComponent<BloodPoolFx>().Begin(size * Mathf.Lerp(0.6f, 1.2f, k) * Random.Range(0.8f, 1.2f));
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
        /// <summary>탄피: 총 오른쪽 위로 튀어나가 떨어지는 놋쇠 조각.</summary>
        public static void Casing(Vector3 pos, Vector3 right, Vector3 up, bool shell)
        {
            Vector3 v = right * Random.Range(1.6f, 2.6f) + up * Random.Range(1.2f, 2.2f) + Random.insideUnitSphere * 0.4f;
            Color c = shell ? new Color(0.75f, 0.12f, 0.08f) : new Color(0.85f, 0.65f, 0.28f);
            Emit(debris, pos, v, shell ? 0.045f : 0.028f, Random.Range(0.6f, 0.9f), c);
        }

        /// <summary>총구 연기: 쏘고 나서 피어오르는 희미한 연기.</summary>
        public static void MuzzleSmoke(Vector3 pos, Vector3 fwd, float amount)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(2f * amount));
            for (int i = 0; i < n; i++)
                Emit(debris, pos + fwd * 0.05f, fwd * Random.Range(0.3f, 0.8f) + Vector3.up * Random.Range(0.4f, 0.9f) + Random.insideUnitSphere * 0.2f,
                    Random.Range(0.12f, 0.24f) * amount, Random.Range(0.5f, 0.9f), new Color(0.55f, 0.55f, 0.55f, 0.22f));
        }

        public static void ImpactFlesh(Vector3 point, Vector3 shotDir, Color color, int amount)
        {
            if (!GameSettings.Gore) color = new Color(0.35f, 0.33f, 0.3f, color.a);
            for (int i = 0; i < amount; i++)
            {
                Vector3 v = (shotDir * 0.6f + Random.insideUnitSphere).normalized * Random.Range(1.5f, 4.5f) + Vector3.up;
                Emit(debris, point, v, Random.Range(0.04f, 0.1f), Random.Range(0.3f, 0.6f), color * Random.Range(0.75f, 1.1f));
            }
        }

        public static void BloodBurst(Vector3 center, Color color, float scale)
        {
            if (!GameSettings.Gore) color = new Color(0.32f, 0.3f, 0.27f, color.a); // 피 표현 끔: 흙먼지
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

namespace Hunt.Core
{
    /// <summary>피 웅덩이가 1.5초 동안 번지고, 40초 뒤 천천히 옅어진다.</summary>
    public class BloodPoolFx : MonoBehaviour
    {
        float t, target;
        public void Begin(float size) { t = 0f; target = size; transform.localScale = Vector3.one * 0.05f; gameObject.SetActive(true); }

        void Update()
        {
            t += Time.deltaTime;
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 1.6f), 3f);
            float fade = Mathf.Clamp01((48f - t) / 8f);
            transform.localScale = Vector3.one * Mathf.Max(0.01f, target * grow * Mathf.Lerp(0.85f, 1f, fade));
            if (fade <= 0f) gameObject.SetActive(false);
        }
    }
}

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
            mist = MakeSystem("FX_Mist", -0.03f, ParticleSystemRenderMode.Billboard);
            // 연기·피 안개: 퍼지며 커진다
            var msol = mist.sizeOverLifetime;
            msol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.6f)));
            var mcol = mist.colorOverLifetime;
            mcol.enabled = true;
            var mg = new Gradient();
            mg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            mcol.color = mg;
            var mmain = mist.main;
            mmain.maxParticles = 400;
            mist.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftMaterial(true);
        }

        static Material flameMat;

        /// <summary>불꽃 한 혀: 아래는 희고 노란 심, 위로 갈수록 가늘고 주황·붉게 (물방울을 뒤집은 모양).</summary>
        public static Material FlameMaterial()
        {
            if (flameMat != null) return flameMat;
            const int TW = 32, TH = 64;
            var tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Flame" };
            var px = new Color32[TW * TH];
            for (int y = 0; y < TH; y++)
                for (int x = 0; x < TW; x++)
                {
                    float v = (y + 0.5f) / TH;                      // 0 아래 → 1 위
                    float u = Mathf.Abs((x + 0.5f) / TW - 0.5f) * 2f; // 0 가운데 → 1 가장자리
                    float width = Mathf.Lerp(0.95f, 0.05f, Mathf.Pow(v, 0.8f)) * Mathf.Clamp01(v * 6f + 0.35f);
                    float a = Mathf.Clamp01((width - u) / Mathf.Max(0.05f, width) * 2.2f) * Mathf.Clamp01((1f - v) * 3f);
                    float core = Mathf.Clamp01(1f - u / Mathf.Max(0.05f, width * 0.55f)) * Mathf.Clamp01(1f - v * 1.6f);
                    Color c = Color.Lerp(new Color(1f, 0.38f, 0.08f), new Color(1f, 0.75f, 0.25f), Mathf.Clamp01(1f - v * 1.3f));
                    c = Color.Lerp(c, new Color(1f, 0.95f, 0.75f), core);
                    px[y * TW + x] = new Color(c.r, c.g, c.b, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            flameMat = new Material(Shader.Find("Sprites/Default")) { name = "Flame", mainTexture = tex };
            return flameMat;
        }

        /// <summary>
        /// 빛 덩이: host의 단색 공은 숨기고(hideHost) 카메라를 보는 부드러운 빛 판 + 작은 밝은 심을 붙인다.
        /// 도깨비불·혼불처럼 '공'이 아니라 번지는 빛으로 보여야 하는 것.
        /// </summary>
        public static void MakeGlow(GameObject host, Color c, float size, bool hideHost = true)
        {
            var hr = host.GetComponent<Renderer>();
            if (hr != null && hideHost) hr.enabled = false;
            for (int i = 0; i < 2; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(q.GetComponent<Collider>());
                q.layer = host.layer;
                q.transform.SetParent(host.transform, false);
                q.transform.localScale = Vector3.one * (i == 0 ? size : size * 0.35f);
                var r = q.GetComponent<Renderer>();
                r.sharedMaterial = SoftMaterial(i == 0);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Color", i == 0 ? new Color(c.r, c.g, c.b, c.a * 0.8f) : Color.Lerp(c, Color.white, 0.6f));
                r.SetPropertyBlock(mpb);
                q.AddComponent<FaceCam>();
            }
        }

        /// <summary>판이 늘 카메라를 본다.</summary>
        public class FaceCam : MonoBehaviour
        {
            void LateUpdate()
            {
                var gm = GameManager.Instance;
                var cam = gm != null && gm.ActiveCamera != null ? gm.ActiveCamera : Camera.main;
                if (cam != null) transform.rotation = cam.transform.rotation;
            }
        }

        static ParticleSystem mist;     // 피 안개·연기 (부드럽게 커지며 사라짐)
        static Material softDotMat, softCloudMat;

        /// <summary>둥근 입자 재질: dot = 가운데가 진한 작은 방울, cloud = 아주 부드러운 구름.</summary>
        public static Material SoftMaterial(bool cloud)
        {
            var m = cloud ? softCloudMat : softDotMat;
            if (m != null) return m;
            const int S = 32;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = cloud ? "SoftCloud" : "SoftDot" };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - S / 2f + 0.5f) / (S / 2f), dy = (y - S / 2f + 0.5f) / (S / 2f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = cloud ? Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f)
                                    : Mathf.Clamp01((1f - r) * 3.5f);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            m = new Material(Shader.Find("Sprites/Default")) { name = tex.name, mainTexture = tex };
            if (cloud) softCloudMat = m; else softDotMat = m;
            return m;
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
            r.sharedMaterial = SoftMaterial(false); // 둥글고 가장자리가 부드러운 입자 (네모 입자는 싸구려처럼 보인다)
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

        static Mesh lowSphere;

        /// <summary>
        /// 가벼운 공 (꼭짓점 ~60개, 판정 없음): 효과용. 기본 Sphere(515개)는 묶어 그리기가 안 돼 내장 그래픽에 무겁다.
        /// </summary>
        public static GameObject LowSphere()
        {
            if (lowSphere == null)
            {
                const int seg = 8, rings = 6;
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                for (int r = 0; r <= rings; r++)
                {
                    float phi = Mathf.PI * r / rings;
                    for (int s = 0; s <= seg; s++)
                    {
                        float th = 2f * Mathf.PI * s / seg;
                        var p = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                        v.Add(p * 0.5f); n.Add(p); uv.Add(new Vector2((float)s / seg, 1f - (float)r / rings));
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int s = 0; s < seg; s++)
                    {
                        int a = r * (seg + 1) + s, b = a + seg + 1;
                        tri.Add(a); tri.Add(a + 1); tri.Add(b);
                        tri.Add(b); tri.Add(a + 1); tri.Add(b + 1);
                    }
                lowSphere = new Mesh { name = "LowSphere" };
                lowSphere.SetVertices(v); lowSphere.SetNormals(n); lowSphere.SetUVs(0, uv); lowSphere.SetTriangles(tri, 0);
                lowSphere.RecalculateBounds();
            }
            var go = new GameObject("Sphere");
            go.AddComponent<MeshFilter>().sharedMesh = lowSphere;
            go.AddComponent<MeshRenderer>();
            return go;
        }

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

        /// <summary>발밑 물 튐 (논물을 밟을 때).</summary>
        public static void Splash(Vector3 p, float strength)
        {
            if (debris == null) return;
            int n = Mathf.RoundToInt(5 * strength);
            for (int i = 0; i < n; i++)
            {
                Vector3 v = (Vector3.up * 1.4f + Random.insideUnitSphere * 0.9f).normalized * Random.Range(1.2f, 2.4f) * strength;
                Emit(debris, p + Vector3.up * 0.05f, v, Random.Range(0.025f, 0.05f), Random.Range(0.3f, 0.45f), new Color(0.4f, 0.42f, 0.42f, 0.7f));
            }
        }

        /// <summary>벽·바닥 명중: 불꽃 + 먼지 + 탄흔.</summary>
        public static void ImpactWorld(Vector3 point, Vector3 normal)
        {
            // 젖은 땅: 불꽃 대신 흙탕물이 위로 튄다
            bool ground = normal.y > 0.75f;
            if (ground)
            {
                for (int i = 0; i < 7; i++)
                {
                    Vector3 v = (Vector3.up * 1.6f + Random.insideUnitSphere * 0.7f).normalized * Random.Range(1.8f, 3.6f);
                    Emit(debris, point + Vector3.up * 0.03f, v, Random.Range(0.03f, 0.07f), Random.Range(0.35f, 0.55f), new Color(0.32f, 0.3f, 0.26f, 0.85f));
                }
                Emit(debris, point + Vector3.up * 0.05f, Vector3.up * 0.5f, 0.35f, 0.5f, new Color(0.45f, 0.44f, 0.42f, 0.35f)); // 물안개
                BulletHole(point, normal);
                return;
            }
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
                Emit(mist, pos + fwd * 0.05f, fwd * Random.Range(0.3f, 0.8f) + Vector3.up * Random.Range(0.3f, 0.7f) + Random.insideUnitSphere * 0.2f,
                    Random.Range(0.12f, 0.22f) * amount, Random.Range(0.7f, 1.2f), new Color(0.6f, 0.6f, 0.62f, 0.2f));
        }

        public static void ImpactFlesh(Vector3 point, Vector3 shotDir, Color color, int amount)
        {
            if (!GameSettings.Gore) color = new Color(0.35f, 0.33f, 0.3f, color.a);
            for (int i = 0; i < amount; i++)
            {
                Vector3 v = (shotDir * 0.6f + Random.insideUnitSphere).normalized * Random.Range(1.5f, 4.5f) + Vector3.up;
                Emit(debris, point, v, Random.Range(0.04f, 0.1f), Random.Range(0.3f, 0.6f), color * Random.Range(0.75f, 1.1f));
            }
            // 맞은 자리에서 확 퍼지는 피 안개 (총알이 나가는 쪽으로 조금 더)
            int puffs = amount >= 8 ? 3 : 2;
            for (int i = 0; i < puffs; i++)
            {
                Color mc = new Color(color.r * 0.8f, color.g * 0.6f, color.b * 0.6f, 0.45f);
                Emit(mist, point + shotDir * 0.05f, shotDir * Random.Range(0.6f, 1.6f) + Random.insideUnitSphere * 0.5f,
                    Random.Range(0.22f, 0.36f), Random.Range(0.3f, 0.45f), mc);
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

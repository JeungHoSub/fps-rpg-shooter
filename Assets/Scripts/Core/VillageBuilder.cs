using System.Collections.Generic;
using Hunt.UI;
using UnityEngine;
using K = Hunt.Core.EnvMaterials.Kind;

namespace Hunt.Core
{
    /// <summary>
    /// 비 오는 한국 시골 마을 (코드로 배치, 텍스처는 Poly Haven CC0).
    ///   서쪽: 농가(파란 함석지붕) · 기와집 · 초가집 · 마을회관과 확성기
    ///   동쪽: 물 댄 논과 논두렁, 곤포 사일리지
    ///   북쪽: 공동묘지와 무너져 열린 고분 (역병의 근원)
    ///   남쪽: 마을 어귀 장승 한 쌍 · 버스정류장 · 비닐하우스
    ///   가운데: 정자나무와 평상, 마을길, 전봇대와 가로등
    /// 플레이 영역은 ±48m. 바깥은 안개에 잠긴 산.
    /// </summary>
    public static class VillageBuilder
    {
        public const float Half = 44f; // 예전 48 → 좁혀서 망자와 더 자주 마주친다

        /// <summary>망자가 걸어 나오는 곳 (무덤, 논 끝, 마을 어귀, 산자락).</summary>
        public static readonly List<Vector3> SpawnAnchors = new List<Vector3>();

        static Transform root;

        public static Transform Build(Transform worldRoot)
        {
            EnvMaterials.ClearCache();
            root = new GameObject("Village").transform;
            root.SetParent(worldRoot, false);
            SpawnAnchors.Clear();
            Hiding.Clear();
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);

            Ground();
            Roads();
            Paddies();
            Houses();
            VillageHall();
            Graveyard();
            Entrance();
            Greenhouses();
            CenterTree();
            Utilities();
            Props();
            Dressing();
            Mountains();
            Boundary();

            SpawnAnchors.AddRange(new[]
            {
                new Vector3(16f, 0f, 33f), new Vector3(-8f, 0f, 40f), new Vector3(32f, 0f, 40f),   // 무덤
                new Vector3(41f, 0f, 12f), new Vector3(41f, 0f, -8f),                              // 논 끝
                new Vector3(0f, 0f, -42f), new Vector3(-20f, 0f, -41f),                            // 마을 어귀
                new Vector3(-41f, 0f, 30f), new Vector3(-41f, 0f, 2f), new Vector3(-40f, 0f, -30f), // 서쪽 산자락
            });
            return root;
        }

        // ================================================================ 기본 도구
        static GameObject Prim(PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Transform parent = null,
            Quaternion? rot = null, bool collider = true, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent != null ? parent : root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Box(Vector3 pos, Vector3 size, K kind, Transform parent = null, Quaternion? rot = null, bool collider = true, Color? tint = null)
        {
            var mat = EnvMaterials.Get(kind, new Vector2(Mathf.Max(size.x, size.z), size.y < 0.3f ? Mathf.Min(size.x, size.z) : size.y), tint);
            return Prim(PrimitiveType.Cube, pos, size, mat, parent, rot, collider);
        }

        /// <summary>Poly Haven 소품 모델을 놓는다. 모델 파일이 없으면 null (호출한 쪽이 도형으로 대체).</summary>
        static GameObject Model(string id, Vector3 pos, float yaw, float height, Transform parent = null, bool collider = true,
            Color? tint = null, float gloss = 0.45f, bool cutout = false)
        {
            return PropModels.Place(id, parent != null ? parent : root, pos, yaw, height, collider, tint, gloss, cutout, QualityManager.Shadows);
        }

        static Transform Group(string name, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        static void Label(Transform parent, Vector3 localPos, Quaternion localRot, string text, float size, Color color)
        {
            UITheme.Ensure();
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 96;
            tm.characterSize = size / 10f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var font = UITheme.Serif;
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        // ================================================================ 지형
        static void Ground()
        {
            // 플레이 영역 (진흙땅)
            var g = Prim(PrimitiveType.Plane, Vector3.zero, new Vector3(12f, 1f, 12f), EnvMaterials.Get(K.Mud, new Vector2(120f, 120f)));
            g.name = "Ground";
            // 판정은 평평한 바닥 그대로, 보이는 땅은 살짝 울퉁불퉁 + 가장자리는 언덕으로 솟는다 (네모난 경계가 안 보이게)
            g.GetComponent<Renderer>().enabled = false;
            RollingGround();
            // 바깥 들판 (판정 없음, 안개에 묻힘)
            Prim(PrimitiveType.Plane, new Vector3(0f, -0.03f, 0f), new Vector3(60f, 1f, 60f), EnvMaterials.Get(K.Grass, new Vector2(600f, 600f), new Color(0.3f, 0.32f, 0.26f)), collider: false);
            // 마른 풀밭 조각
            var rnd = new System.Random(5);
            for (int i = 0; i < 16; i++)
            {
                float x = (float)(rnd.NextDouble() * 90 - 45), z = (float)(rnd.NextDouble() * 90 - 45);
                if (Mathf.Abs(x) < 5f || (x > 8f && z > -18f && z < 26f)) continue; // 길·논 제외
                float s = (float)(rnd.NextDouble() * 6 + 4);
                Prim(PrimitiveType.Cylinder, new Vector3(x, 0.006f, z), new Vector3(s, 0.005f, s * 0.8f), EnvMaterials.Get(K.Grass, new Vector2(s, s)), collider: false, shadows: false);
            }
        }

        /// <summary>큰길이 굽이치는 정도: z 위치에서 길 가운데의 x.</summary>
        public static float RoadX(float z) => Mathf.Sin(z * 0.045f) * 1.3f + Mathf.Sin(z * 0.11f + 1.3f) * 0.4f;

        /// <summary>
        /// 보이는 땅: 1.5m 격자, 놀이 영역 안은 ±8cm 안팎의 굴곡(길 근처는 평평), 경계 너머는 언덕으로 솟는다.
        /// 판정은 평평한 Plane이 맡는다 (망자·플레이어 높이 계산이 단순하도록).
        /// </summary>
        static void RollingGround()
        {
            const float ext = 66f, step = 1.5f;
            int n = Mathf.RoundToInt(ext * 2f / step) + 1;
            var v = new Vector3[n * n];
            var uv = new Vector2[n * n];
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    float x = -ext + ix * step, z = -ext + iz * step;
                    float bump = (Mathf.PerlinNoise(x * 0.09f + 13f, z * 0.09f + 7f) - 0.5f) * 0.16f
                               + (Mathf.PerlinNoise(x * 0.35f, z * 0.35f) - 0.5f) * 0.05f;
                    float roadD = Mathf.Abs(x - RoadX(z));
                    bump *= Mathf.Clamp01((roadD - 3.5f) / 3f);              // 길은 평평
                    // 경계 너머: 둥근 사각 거리로 언덕 (울퉁불퉁한 능선)
                    float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - (Half + 0.5f) + (Mathf.PerlinNoise(x * 0.05f, z * 0.05f) - 0.5f) * 4f;
                    float hill = edge > 0f ? Mathf.Pow(edge, 1.5f) * 0.28f * (0.7f + Mathf.PerlinNoise(x * 0.07f + 3f, z * 0.07f) * 0.6f) : 0f;
                    v[iz * n + ix] = new Vector3(x, Mathf.Max(-0.02f, bump) + hill - 0.01f, z);
                    uv[iz * n + ix] = new Vector2(x / 100f, z / 100f);
                }
            var tris = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int iz = 0; iz < n - 1; iz++)
                for (int ix = 0; ix < n - 1; ix++)
                {
                    int a = iz * n + ix;
                    tris[t++] = a; tris[t++] = a + n; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = a + n; tris[t++] = a + n + 1;
                }
            var mesh = new Mesh { name = "RollingGround", vertices = v, uv = uv, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject("GroundVisual");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = EnvMaterials.Get(K.Mud, new Vector2(100f, 100f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>굽은 띠 모양 길 (점들을 부드럽게 이은 리본). 판정 없음 — 바닥 위에 얹힌 겉모습.</summary>
        static void Ribbon(string name, Vector3[] pts, float width, K kind, float y, Color? tint = null)
        {
            var path = new List<Vector3>();
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Length - 1, i + 2)];
                int seg = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / 1.5f));
                for (int k = 0; k < seg; k++)
                {
                    float u = k / (float)seg, u2 = u * u, u3 = u2 * u;
                    path.Add(0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3));
                }
            }
            path.Add(pts[pts.Length - 1]);
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            float dist = 0f;
            var rnd = new System.Random(name.GetHashCode());
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 fwd = (path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]);
                fwd.y = 0f;
                fwd.Normalize();
                Vector3 side = new Vector3(fwd.z, 0f, -fwd.x);
                if (i > 0) dist += Vector3.Distance(path[i], path[i - 1]);
                // 길가가 조금씩 들쭉날쭉
                float wl = width * 0.5f * (0.92f + (float)rnd.NextDouble() * 0.16f), wr = width * 0.5f * (0.92f + (float)rnd.NextDouble() * 0.16f);
                Vector3 c = new Vector3(path[i].x, y, path[i].z);
                v.Add(c - side * wl); v.Add(c + side * wr);
                uv.Add(new Vector2(-wl / 100f, dist / 100f)); uv.Add(new Vector2(wr / 100f, dist / 100f));
                if (i > 0)
                {
                    int a = v.Count - 4;
                    tri.Add(a); tri.Add(a + 2); tri.Add(a + 1);
                    tri.Add(a + 1); tri.Add(a + 2); tri.Add(a + 3);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = EnvMaterials.Get(kind, new Vector2(100f, 100f), tint);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void Roads()
        {
            // 남북 큰길 (금 간 콘크리트) — 곧은 띠가 아니라 살짝 굽이친다
            var main = new List<Vector3>();
            for (float z = -66f; z <= 66f; z += 6f) main.Add(new Vector3(RoadX(z), 0f, z));
            Ribbon("큰길", main.ToArray(), 6f, K.Road, 0.04f);
            // 서쪽 갈림길: 집들 사이로 휘어 들어간다
            Ribbon("서쪽길", new[]
            {
                new Vector3(RoadX(8f), 0f, 8f), new Vector3(-8f, 0f, 9.2f), new Vector3(-16f, 0f, 8.4f), new Vector3(-24f, 0f, 7f),
                new Vector3(-31f, 0f, 8.6f), new Vector3(-38f, 0f, 10.5f), new Vector3(-46f, 0f, 9.5f), new Vector3(-58f, 0f, 8f),
            }, 4f, K.Road, 0.035f);
            // 북쪽 묘지로 가는 흙길 (구불구불)
            Ribbon("묘지길", new[]
            {
                new Vector3(RoadX(22f) + 2.5f, 0f, 22f), new Vector3(7f, 0f, 26f), new Vector3(10.5f, 0f, 28.5f), new Vector3(12.5f, 0f, 30f),
                new Vector3(15.5f, 0f, 30.5f), new Vector3(19f, 0f, 30.8f),
            }, 2.6f, K.FarmSoil, 0.03f);
            // 논두렁 사이 흙길
            Ribbon("논길", new[]
            {
                new Vector3(RoadX(-20f) + 2.5f, 0f, -19.5f), new Vector3(14f, 0f, -19f), new Vector3(26f, 0f, -20.2f), new Vector3(38f, 0f, -19.4f), new Vector3(50f, 0f, -18f),
            }, 2f, K.FarmSoil, 0.03f);
            // 웅덩이 (가로등 빛이 비친다)
            var puddle = EnvMaterials.Plain(new Color(0.02f, 0.025f, 0.03f), 0.97f);
            var rnd = new System.Random(9);
            for (int i = 0; i < 14; i++)
            {
                float z = (float)(rnd.NextDouble() * 84 - 42);
                float x = RoadX(z) + (float)(rnd.NextDouble() * 4 - 2);
                Prim(PrimitiveType.Cylinder, new Vector3(x, 0.062f, z), new Vector3((float)(rnd.NextDouble() * 2 + 1.2f), 0.002f, (float)(rnd.NextDouble() * 1.5 + 0.8f)),
                    puddle, collider: false, shadows: false, rot: Quaternion.Euler(0f, (float)(rnd.NextDouble() * 180), 0f));
            }
        }

        static void Paddies()
        {
            var water = EnvMaterials.Plain(new Color(0.05f, 0.065f, 0.07f), 0.96f);
            var stubble = EnvMaterials.Plain(new Color(0.42f, 0.4f, 0.22f), 0.2f);
            var rnd = new System.Random(3);
            for (int ix = 0; ix < 3; ix++)
                for (int iz = 0; iz < 3; iz++)
                {
                    // 논 한 배미씩 조금씩 틀어지고 크기도 다르다 (반듯한 바둑판 대신)
                    var c = new Vector3(15f + ix * 11f + (float)(rnd.NextDouble() - 0.5) * 0.8f, 0f, -12f + iz * 12f + (float)(rnd.NextDouble() - 0.5) * 0.8f);
                    var pg = Group("논", c, (float)(rnd.NextDouble() * 6 - 3));
                    float pw = 10.2f + (float)rnd.NextDouble() * 0.6f, pd = 10.4f + (float)rnd.NextDouble() * 0.8f;
                    // 물 댄 논바닥
                    Prim(PrimitiveType.Cube, new Vector3(0, 0.03f, 0), new Vector3(pw, 0.06f, pd), water, pg);
                    Hiding.AddPaddy(c, pw, pd);
                    // 논두렁 (사방, 높이가 조금씩 다르다)
                    float bh = 0.26f + (float)rnd.NextDouble() * 0.1f;
                    Box(new Vector3(0, bh * 0.5f, pd * 0.5f + 0.3f), new Vector3(pw + 0.9f, bh, 0.6f), K.Grass, pg);
                    Box(new Vector3(0, bh * 0.5f, -pd * 0.5f - 0.3f), new Vector3(pw + 0.9f, bh, 0.6f), K.Grass, pg);
                    Box(new Vector3(pw * 0.5f + 0.3f, bh * 0.5f, 0), new Vector3(0.6f, bh, pd), K.Grass, pg);
                    Box(new Vector3(-pw * 0.5f - 0.3f, bh * 0.5f, 0), new Vector3(0.6f, bh, pd), K.Grass, pg);
                    // 벼 그루터기
                    for (int k = 0; k < 26; k++)
                    {
                        var p = c + new Vector3((float)(rnd.NextDouble() * 9.5 - 4.75), 0.18f, (float)(rnd.NextDouble() * 10 - 5));
                        Prim(PrimitiveType.Cube, p, new Vector3(0.08f, 0.3f, 0.08f), stubble, collider: false, shadows: false,
                            rot: Quaternion.Euler((float)(rnd.NextDouble() * 20 - 10), 0, (float)(rnd.NextDouble() * 20 - 10)));
                    }
                }
            // 곤포 사일리지 (논가의 하얀 비닐 볏짚 뭉치)
            var bale = EnvMaterials.Plain(new Color(0.85f, 0.86f, 0.84f), 0.75f);
            Vector3[] bales = { new Vector3(10f, 0.7f, 22f), new Vector3(12f, 0.7f, 23.5f), new Vector3(11f, 0.7f, 25.5f), new Vector3(39.5f, 0.7f, -23f), new Vector3(39f, 0.7f, -26f) };
            foreach (var b in bales)
                Prim(PrimitiveType.Cylinder, b, new Vector3(1.5f, 0.7f, 1.5f), bale, rot: Quaternion.Euler(0f, 0f, 90f));
        }

        // ================================================================ 건물
        enum Roof { Gable, Hip, Thatch, Flat }

        static Transform House(string name, Vector3 pos, float yaw, float w, float h, float d, K wall, Roof roof, K roofKind, Color? roofTint, bool litWindow)
        {
            var g = Group(name, pos, yaw);
            Box(new Vector3(0, h / 2f, 0), new Vector3(w, h, d), wall, g);
            // 기단 (시멘트/돌)
            Box(new Vector3(0, 0.15f, 0), new Vector3(w + 0.6f, 0.3f, d + 0.6f), wall == K.ClayWall ? K.StoneWall : K.Concrete, g);

            switch (roof)
            {
                case Roof.Gable:
                case Roof.Hip:
                {
                    bool hanok = roof == Roof.Hip;
                    float pitch = hanok ? 30f : 22f;
                    float overhang = hanok ? 1.0f : 0.5f;
                    float rise = Mathf.Tan(pitch * Mathf.Deg2Rad) * (d / 2f + overhang);
                    var mat = EnvMaterials.Get(roofKind, new Vector2(w + 2f, d), roofTint);
                    // 곡면 지붕: 기와집은 오목한 경사 + 들린 처마 귀, 농가는 곧은 함석
                    CurvedRoof(g, w, d, h, rise, overhang, hanok ? 0.4f : 0f, hanok ? 0.55f : 0f, mat);
                    // 박공 삼각벽
                    GableWall(g, w, d, h, rise * d / 2f / (d / 2f + overhang), wall);
                    // 용마루 (기와집은 양 끝이 솟는다)
                    var ridgeMat = EnvMaterials.Plain(new Color(0.12f, 0.12f, 0.13f), 0.6f);
                    Prim(PrimitiveType.Cube, new Vector3(0, h + rise + 0.08f, 0), new Vector3(w + 2f * overhang * 0.6f, 0.24f, 0.34f), ridgeMat, g, collider: false);
                    if (hanok)
                    {
                        foreach (var sx in new[] { -1f, 1f })
                            Prim(PrimitiveType.Cube, new Vector3(sx * (w / 2f + overhang * 0.55f), h + rise + 0.32f, 0), new Vector3(0.3f, 0.5f, 0.3f), ridgeMat, g,
                                Quaternion.Euler(0, 0, -sx * 22f), collider: false);
                        HanokFrame(g, w, d, h);
                    }
                    break;
                }
                case Roof.Thatch:
                    // 초가지붕: 두툼한 짚 처마 + 둥근 빵 모양 지붕 + 새끼줄 그물 + 용마름, 기둥·툇마루
                    ThatchRoof(g, w, d, h);
                    HanokFrame(g, w, d, h);
                    break;
                case Roof.Flat:
                    Box(new Vector3(0, h + 0.15f, 0), new Vector3(w + 0.4f, 0.3f, d + 0.4f), K.Concrete, g);
                    break;
            }

            // 문 (앞면 = -Z)
            Box(new Vector3(-w * 0.2f, 1.05f, -d / 2f - 0.04f), new Vector3(1.1f, 2.1f, 0.08f), K.WoodPlanks, g, collider: false);
            // 창문
            Material win = litWindow ? EnvMaterials.Emissive(new Color(1f, 0.68f, 0.35f), 1.6f) : EnvMaterials.Plain(new Color(0.02f, 0.025f, 0.03f), 0.95f);
            Prim(PrimitiveType.Cube, new Vector3(w * 0.22f, 1.5f, -d / 2f - 0.03f), new Vector3(1.4f, 0.9f, 0.05f), win, g, collider: false);
            Prim(PrimitiveType.Cube, new Vector3(w / 2f + 0.03f, 1.5f, 0f), new Vector3(0.05f, 0.9f, 1.4f), win, g, collider: false);
            if (litWindow)
            {
                var lg = new GameObject("WindowLight");
                lg.transform.SetParent(g, false);
                lg.transform.localPosition = new Vector3(w * 0.22f, 1.5f, -d / 2f - 1.2f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.65f, 0.35f);
                l.range = 6f;
                l.intensity = 1.2f;
                var wf = lg.AddComponent<LampFlicker>();
                wf.subtle = true;
                wf.minLevel = GfxLevel.High;
            }
            return g;
        }

        // ================================================================ 초가지붕
        /// <summary>
        /// 초가지붕: 둥근 사각 처마선에서 위로 둥글게 부풀어 용마루 선으로 모이는 '빵' 모양.
        /// 처마 끝은 짚단 두께(약 30cm)가 보이게 아래로 한 번 접는다. 그 위로 새끼줄 그물, 꼭대기엔 용마름.
        /// </summary>
        static void ThatchRoof(Transform g, float w, float d, float h)
        {
            const int N = 44, M = 9;
            float A = w / 2f + 0.65f, B = d / 2f + 0.65f;      // 처마 반폭 (벽 밖으로 65cm)
            float ridgeHalf = Mathf.Max(0.2f, A - B);           // 꼭대기 용마루 반길이
            float H = 1.9f + d * 0.06f;                         // 지붕 높이 (둥글게 부푼 짚)
            float eaveY = h + 0.05f;

            // 고리 j의 둘레 점 (둥근 사각, 위로 갈수록 용마루 선으로 모인다)
            Vector3 Ring(int i, float t, float drop, float shrink)
            {
                float th = i / (float)N * Mathf.PI * 2f;
                float k = Mathf.Pow(Mathf.Cos(t * Mathf.PI * 0.5f), 0.55f) * shrink;
                float ax = ridgeHalf + (A - ridgeHalf) * k, az = B * k;
                float c = Mathf.Cos(th), sn = Mathf.Sin(th);
                const float e = 2.6f; // 둥근 사각 정도
                float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / e) * ax;
                float z = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), 2f / e) * az;
                // 짚이 고르지 않게 (처마선이 조금씩 출렁)
                float wob = 1f + 0.025f * Mathf.Sin(th * 7f + t * 3f) * (1f - t);
                float y = eaveY + H * Mathf.Pow(Mathf.Sin(t * Mathf.PI * 0.5f), 0.85f) - drop;
                return new Vector3(x * wob, y, z * wob);
            }

            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            // 고리 0 = 처마 아래 짚단 밑면(안쪽·아래), 1 = 처마 끝, 2.. = 지붕면
            int rings = M + 2;
            for (int j = 0; j < rings; j++)
            {
                for (int i = 0; i <= N; i++)
                {
                    Vector3 p = j == 0 ? Ring(i % N, 0f, 0.32f, 0.93f) : Ring(i % N, (j - 1) / (float)M, 0f, 1f);
                    v.Add(p);
                    float arc = j == 0 ? -0.35f : (j - 1) / (float)M * (H + B);
                    uv.Add(new Vector2(i / (float)N * (2f * (A + B)) / 100f, arc / 100f));
                }
            }
            int row = N + 1;
            for (int j = 0; j < rings - 1; j++)
                for (int i = 0; i < N; i++)
                {
                    int a0 = j * row + i, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    tri.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
            var mesh = MeshUtil.DoubleSided("ThatchRoof", v, uv, tri);
            var go = new GameObject("초가지붕");
            go.transform.SetParent(g, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var rr = go.AddComponent<MeshRenderer>();
            rr.sharedMaterial = EnvMaterials.Get(K.Thatch, new Vector2(100f, 100f), new Color(0.6f, 0.54f, 0.42f));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;

            // 새끼줄 그물: 지붕면을 따라 세로줄 + 가로줄 두 가닥 (가는 띠, 면보다 2cm 위)
            var rv = new List<Vector3>();
            var ruv = new List<Vector2>();
            var rt = new List<int>();
            void Strip(System.Func<float, Vector3> f, int seg, float width)
            {
                int start = rv.Count;
                for (int k = 0; k <= seg; k++)
                {
                    float t = k / (float)seg;
                    Vector3 p = f(t), q = f(Mathf.Min(1f, t + 0.01f)), pr = f(Mathf.Max(0f, t - 0.01f));
                    Vector3 along = (q - pr).normalized;
                    Vector3 outward = new Vector3(p.x, 0f, p.z).normalized * 0.6f + Vector3.up;
                    Vector3 side = Vector3.Cross(along, outward).normalized * width * 0.5f;
                    Vector3 lift = outward.normalized * 0.025f;
                    rv.Add(p + lift - side); rv.Add(p + lift + side);
                    ruv.Add(new Vector2(0f, t)); ruv.Add(new Vector2(1f, t));
                    if (k > 0)
                    {
                        int b = start + (k - 1) * 2;
                        rt.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                    }
                }
            }
            Vector3 Surf(float i, float t)
            {
                // 둘레 각도 i(0~N)를 실수로 받아 지붕면 위 점
                int i0 = Mathf.FloorToInt(i) % N, i1 = (i0 + 1) % N;
                float f = i - Mathf.Floor(i);
                return Vector3.Lerp(Ring(i0, t, 0f, 1f), Ring(i1, t, 0f, 1f), f);
            }
            for (int k = 0; k < 16; k++)
            {
                float ii = k * N / 16f + 0.5f;
                Strip(t => Surf(ii, t * 0.97f), 10, 0.05f);
            }
            foreach (var tt in new[] { 0.3f, 0.62f })
                Strip(u => Surf(u * N, tt), N, 0.05f);
            var rope = new Mesh { name = "ThatchRope" };
            rope.SetVertices(rv);
            rope.SetUVs(0, ruv);
            rope.SetTriangles(rt, 0);
            rope.RecalculateNormals();
            var rgo = new GameObject("새끼줄");
            rgo.transform.SetParent(g, false);
            rgo.AddComponent<MeshFilter>().sharedMesh = rope;
            var ropeR = rgo.AddComponent<MeshRenderer>();
            ropeR.sharedMaterial = EnvMaterials.Plain(new Color(0.32f, 0.26f, 0.17f), 0.15f);
            ropeR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 용마름: 꼭대기에 엮어 덮은 짚 (어두운 굵은 원통)
            var cap = Prim(PrimitiveType.Cylinder, new Vector3(0f, eaveY + H + 0.06f, 0f), new Vector3(0.36f, ridgeHalf + 0.35f, 0.32f),
                EnvMaterials.Get(K.Thatch, new Vector2(2f, 1f), new Color(0.42f, 0.37f, 0.28f)), g, Quaternion.Euler(0f, 0f, 90f), collider: false);
            cap.name = "용마름";
        }

        // ================================================================ 한국 지붕 (곡면)
        /// <summary>
        /// 맞배지붕을 곡면 메시로. concave = 경사가 오목하게 휘는 정도(처마 쪽이 완만), lift = 처마 귀가 들리는 높이(m).
        /// 양면 메시 + 메시 충돌체 (총알이 지붕에 맞는다).
        /// </summary>
        static void CurvedRoof(Transform g, float w, float d, float h, float rise, float overhang, float concave, float lift, Material mat)
        {
            const int NX = 14, NT = 8;
            float L = w + overhang * 2f;
            float run = d / 2f + overhang;
            float ridgeY = h + rise * (d / 2f) / run + 0.1f; // 벽 위에서 처마까지 덮이도록
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            foreach (float side in new[] { -1f, 1f })
            {
                int baseIdx = v.Count;
                for (int it = 0; it <= NT; it++)
                {
                    float t = it / (float)NT;
                    float shape = (1f - concave) * t + concave * (2f * t - t * t); // 오목한 경사
                    for (int ix = 0; ix <= NX; ix++)
                    {
                        float sN = ix / (float)NX * 2f - 1f;
                        float corner = sN * sN * sN * sN;
                        float x = sN * L / 2f * (1f + 0.04f * t * corner);
                        float z = side * (t * run + 0.25f * lift * t * corner);    // 귀가 바깥으로 살짝 뻗음 (안허리)
                        float y = ridgeY - rise * shape + lift * t * t * corner;    // 귀가 들림 (앙곡)
                        v.Add(new Vector3(x, y, z));
                        uv.Add(new Vector2(x / 2.5f, t * run / 2.5f));
                    }
                }
                int row = NX + 1;
                for (int it = 0; it < NT; it++)
                    for (int ix = 0; ix < NX; ix++)
                    {
                        int a0 = baseIdx + it * row + ix, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                        // 바깥(위)을 보는 면만 넣고, 뒷면은 DoubleSided가 만든다
                        if (side > 0) tri.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                        else tri.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                    }
            }
            var mesh = MeshUtil.DoubleSided("Roof", v, uv, tri);
            var go = new GameObject("Roof");
            go.transform.SetParent(g, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>양 옆 삼각 박공벽.</summary>
        static void GableWall(Transform g, float w, float d, float h, float peak, K wall)
        {
            var mat = EnvMaterials.Get(wall, new Vector2(d, peak));
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * (w / 2f - 0.02f);
                var v = new List<Vector3> { new Vector3(x, h, -d / 2f), new Vector3(x, h, d / 2f), new Vector3(x, h + peak, 0f) };
                var uv = new List<Vector2> { new Vector2(0, 0), new Vector2(d / 3f, 0), new Vector2(d / 6f, peak / 3f) };
                var mesh = MeshUtil.DoubleSided("Gable", v, uv, new List<int> { 0, 1, 2 });
                var go = new GameObject("GableWall");
                go.transform.SetParent(g, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        /// <summary>한옥 뼈대: 둥근 기둥, 처마 밑 도리, 툇마루, 창호(문살) 문.</summary>
        static void HanokFrame(Transform g, float w, float d, float h)
        {
            var wood = EnvMaterials.Get(K.WoodPlanks, new Vector2(1f, h), new Color(0.45f, 0.36f, 0.28f));
            int cols = Mathf.Max(2, Mathf.RoundToInt(w / 2.5f));
            for (int i = 0; i <= cols; i++)
            {
                float x = -w / 2f + w * i / cols;
                foreach (float sz in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cylinder, new Vector3(x, h / 2f, sz * (d / 2f + 0.02f)), new Vector3(0.24f, h / 2f, 0.24f), wood, g);
            }
            // 처마 밑 도리
            foreach (float sz in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, new Vector3(0, h - 0.12f, sz * (d / 2f + 0.05f)), new Vector3(w + 0.4f, 0.22f, 0.22f), wood, g, collider: false);
            // 툇마루 (앞쪽 나무 마루)
            Prim(PrimitiveType.Cube, new Vector3(0, 0.45f, -d / 2f - 0.55f), new Vector3(w - 0.4f, 0.1f, 1.0f), EnvMaterials.Get(K.WoodPlanks, new Vector2(w, 1f), new Color(0.55f, 0.45f, 0.35f)), g);
            // 창호: 한지 바른 문 + 띠살
            var paper = EnvMaterials.Plain(new Color(0.75f, 0.7f, 0.58f), 0.1f);
            var bar = EnvMaterials.Plain(new Color(0.22f, 0.16f, 0.11f), 0.3f);
            for (int k = -1; k <= 1; k += 2)
            {
                var c = new Vector3(k * w * 0.18f, 1.25f, -d / 2f - 0.06f);
                Prim(PrimitiveType.Cube, c, new Vector3(1.0f, 1.7f, 0.03f), paper, g, collider: false);
                for (int i = 0; i < 5; i++)
                    Prim(PrimitiveType.Cube, c + new Vector3(-0.4f + i * 0.2f, 0, -0.02f), new Vector3(0.03f, 1.7f, 0.02f), bar, g, collider: false);
                for (int j = 0; j < 4; j++)
                    Prim(PrimitiveType.Cube, c + new Vector3(0, -0.6f + j * 0.4f, -0.02f), new Vector3(1.0f, 0.03f, 0.02f), bar, g, collider: false);
            }
        }

        static void Houses()
        {
            var blue = new Color(0.25f, 0.42f, 0.68f);
            var red = new Color(0.6f, 0.22f, 0.18f);
            House("농가_파란지붕", new Vector3(-20f, 0f, 24f), 0f, 9f, 3.0f, 7f, K.Plaster, Roof.Gable, K.MetalSheet, blue, true);
            House("농가_빨간지붕", new Vector3(-36f, 0f, 22f), 8f, 8f, 2.9f, 6.5f, K.Plaster, Roof.Gable, K.MetalSheet, red, false);
            var hanok = House("기와집", new Vector3(-24f, 0f, -19f), -4f, 10f, 2.9f, 6f, K.ClayWall, Roof.Hip, K.RoofTiles, null, false);
            House("초가집", new Vector3(-38f, 0f, -4f), 12f, 7f, 2.5f, 5.5f, K.ClayWall, Roof.Thatch, K.Thatch, null, false);

            // 기와집 돌담과 대문
            var lot = Group("돌담", new Vector3(-24f, 0f, -19f), -4f);
            Box(new Vector3(0, 0.6f, -6f), new Vector3(5f, 1.2f, 0.6f), K.StoneWall, lot);
            Box(new Vector3(-7f, 0.6f, -6f), new Vector3(4f, 1.2f, 0.6f), K.StoneWall, lot);
            Box(new Vector3(7.5f, 0.6f, -2f), new Vector3(0.6f, 1.2f, 8f), K.StoneWall, lot);
            Box(new Vector3(-8.5f, 0.6f, -2f), new Vector3(0.6f, 1.2f, 8f), K.StoneWall, lot);
            // 농가 사이 돌담
            Box(new Vector3(-28f, 0.6f, 18.5f), new Vector3(0.6f, 1.2f, 9f), K.StoneWall);
            Box(new Vector3(-28f, 0.6f, 30f), new Vector3(16f, 1.2f, 0.6f), K.StoneWall);

            // 창고 (함석)
            var shed = Group("창고", new Vector3(-37f, 0f, 35f), 0f);
            Box(new Vector3(0, 1.6f, 0), new Vector3(6f, 3.2f, 5f), K.MetalSheet, shed, tint: new Color(0.45f, 0.42f, 0.38f));
            Box(new Vector3(0, 3.3f, 0), new Vector3(6.6f, 0.15f, 5.6f), K.MetalSheet, shed, Quaternion.Euler(6f, 0, 0), tint: new Color(0.5f, 0.3f, 0.2f));
        }

        static void VillageHall()
        {
            var g = House("마을회관", new Vector3(-13f, 0f, -2f), -90f, 12f, 3.8f, 8f, K.Concrete, Roof.Flat, K.Concrete, null, true);
            // 간판
            Box(new Vector3(0f, 3.1f, -4.12f), new Vector3(4.2f, 0.8f, 0.1f), K.WoodPlanks, g, collider: false, tint: new Color(0.9f, 0.88f, 0.82f));
            Label(g, new Vector3(0f, 3.1f, -4.2f), Quaternion.identity, "마을회관", 0.55f, new Color(0.12f, 0.1f, 0.1f));

            // 확성기 탑
            var t = Group("확성기탑", new Vector3(-6f, 0f, -9f), 0f);
            t.gameObject.AddComponent<SirenTower>(); // E로 사이렌 (밤마다 한 번)
            Prim(PrimitiveType.Cylinder, new Vector3(0, 4f, 0), new Vector3(0.25f, 4f, 0.25f), EnvMaterials.Plain(new Color(0.5f, 0.5f, 0.48f), 0.5f, 0.4f), t);
            var horn = EnvMaterials.Plain(new Color(0.75f, 0.75f, 0.72f), 0.6f, 0.3f);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cylinder, Quaternion.Euler(0, i * 90f, 0) * new Vector3(0, 0, 0.35f) + new Vector3(0, 7.6f, 0), new Vector3(0.35f, 0.25f, 0.35f), horn, t,
                    Quaternion.Euler(0, i * 90f, 0) * Quaternion.Euler(80f, 0, 0), collider: false);
        }

        // ================================================================ 북쪽: 공동묘지와 열린 고분
        static void Graveyard()
        {
            var grass = EnvMaterials.Get(K.Grass, new Vector2(4f, 4f), new Color(0.38f, 0.42f, 0.32f));
            var rnd = new System.Random(13);
            for (int i = 0; i < 11; i++)
            {
                var p = new Vector3(-12f + i * 5.2f + (float)(rnd.NextDouble() * 2 - 1), 0f, 33f + (float)(rnd.NextDouble() * 6));
                if (p.x > 38f || Mathf.Abs(p.x - RoadX(p.z)) < 5f) continue; // 경계·큰길 피함
                if (Mathf.Abs(p.x - 22f) < 7.5f) continue; // 고분 자리
                var g = Group("무덤", p, (float)(rnd.NextDouble() * 30 - 15));
                Prim(PrimitiveType.Sphere, new Vector3(0, 0f, 0), new Vector3(3.6f, 1.7f, 4f), grass, g);
                Box(new Vector3(0, 0.45f, -2.6f), new Vector3(0.5f, 0.9f, 0.14f), K.Rock, g, tint: new Color(0.55f, 0.55f, 0.52f)); // 비석
                Box(new Vector3(0, 0.15f, -2.2f), new Vector3(1.0f, 0.3f, 0.6f), K.Rock, g);                                      // 상석
            }

            // 무너져 열린 조선 고분 — 붉은 빛이 새어 나온다
            var tomb = Group("열린고분", new Vector3(22f, 0f, 36.5f), 180f);
            Prim(PrimitiveType.Sphere, Vector3.zero, new Vector3(10f, 4.4f, 10f), grass, tomb);
            Box(new Vector3(0, 0.9f, 4.4f), new Vector3(2.4f, 1.8f, 1.6f), K.Rock, tomb, tint: new Color(0.12f, 0.1f, 0.1f));
            Prim(PrimitiveType.Cube, new Vector3(0, 0.85f, 5.15f), new Vector3(1.8f, 1.5f, 0.05f), EnvMaterials.Plain(Color.black, 0f), tomb, collider: false);
            // 깨진 석판
            for (int i = 0; i < 5; i++)
                Box(new Vector3(-2.5f + i * 1.3f, 0.12f, 6.5f + (i % 2) * 0.8f), new Vector3(1.1f, 0.2f, 0.7f), K.Rock, tomb, Quaternion.Euler(0, i * 37f, i % 2 == 0 ? 8f : -6f));
            // 부적 (붉은 띠)
            var talisman = EnvMaterials.Emissive(new Color(0.9f, 0.12f, 0.08f), 0.6f);
            for (int i = 0; i < 6; i++)
                Prim(PrimitiveType.Cube, new Vector3(-1.4f + i * 0.55f, 1.6f + (i % 3) * 0.12f, 5.25f), new Vector3(0.12f, 0.5f, 0.02f), talisman, tomb,
                    Quaternion.Euler(0, 0, (i - 2.5f) * 7f), collider: false);
            // 석등 한 쌍
            foreach (var sx in new[] { -3.2f, 3.2f })
            {
                Box(new Vector3(sx, 0.7f, 6f), new Vector3(0.45f, 1.4f, 0.45f), K.Rock, tomb);
                Box(new Vector3(sx, 1.6f, 6f), new Vector3(0.8f, 0.5f, 0.8f), K.Rock, tomb);
            }
            var lg = new GameObject("TombGlow");
            lg.transform.SetParent(tomb, false);
            lg.transform.localPosition = new Vector3(0, 1.2f, 6.2f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.9f, 0.1f, 0.06f);
            l.range = 9f;
            l.intensity = 2.2f;
            lg.AddComponent<LampFlicker>().pulse = true;
        }

        // ================================================================ 남쪽: 마을 어귀
        static void Entrance()
        {
            // 장승 한 쌍
            Jangseung(new Vector3(-4.4f, 0f, -38f), "천\n하\n대\n장\n군", true);
            Jangseung(new Vector3(4.4f, 0f, -38f), "지\n하\n여\n장\n군", false);

            // 버스정류장
            var bus = Group("버스정류장", new Vector3(7.5f, 0f, -28f), -90f);
            Box(new Vector3(0, 1.2f, 0.9f), new Vector3(3.2f, 2.4f, 0.12f), K.MetalSheet, bus, tint: new Color(0.55f, 0.55f, 0.52f));
            Box(new Vector3(0, 2.45f, 0.2f), new Vector3(3.6f, 0.1f, 1.8f), K.MetalSheet, bus, Quaternion.Euler(-5f, 0, 0), tint: new Color(0.35f, 0.45f, 0.6f));
            Box(new Vector3(0, 0.45f, 0.55f), new Vector3(2.6f, 0.08f, 0.45f), K.WoodPlanks, bus);
            Prim(PrimitiveType.Cylinder, new Vector3(-2.3f, 1.3f, -0.4f), new Vector3(0.08f, 1.3f, 0.08f), EnvMaterials.Plain(new Color(0.6f, 0.6f, 0.6f), 0.5f, 0.5f), bus);
            Box(new Vector3(-2.3f, 2.5f, -0.4f), new Vector3(0.7f, 0.5f, 0.05f), K.MetalSheet, bus, tint: new Color(0.25f, 0.4f, 0.75f), collider: false);
            Label(bus, new Vector3(0f, 2.05f, 0.82f), Quaternion.identity, "버 스 정 류 장", 0.28f, new Color(0.9f, 0.9f, 0.85f));
        }

        static void Jangseung(Vector3 pos, string text, bool male)
        {
            var g = Group("장승", pos, 0f);
            var wood = EnvMaterials.Get(K.Bark, new Vector2(1.5f, 3f), new Color(0.5f, 0.42f, 0.35f));
            Prim(PrimitiveType.Cylinder, new Vector3(0, 1.7f, 0), new Vector3(0.65f, 1.7f, 0.65f), wood, g);
            // 얼굴: 부릅뜬 눈, 주먹코, 이빨
            var white = EnvMaterials.Plain(new Color(0.85f, 0.82f, 0.75f), 0.2f);
            var black = EnvMaterials.Plain(new Color(0.05f, 0.04f, 0.04f), 0.2f);
            var red = EnvMaterials.Plain(new Color(0.55f, 0.08f, 0.06f), 0.3f);
            foreach (var sx in new[] { -0.14f, 0.14f })
            {
                Prim(PrimitiveType.Sphere, new Vector3(sx, 2.75f, -0.29f), new Vector3(0.18f, 0.18f, 0.08f), white, g, collider: false);
                Prim(PrimitiveType.Sphere, new Vector3(sx, 2.75f, -0.33f), new Vector3(0.08f, 0.08f, 0.04f), black, g, collider: false);
                Prim(PrimitiveType.Cube, new Vector3(sx, 2.92f, -0.3f), new Vector3(0.2f, 0.04f, 0.04f), black, g, Quaternion.Euler(0, 0, sx > 0 ? 15f : -15f), collider: false);
            }
            Prim(PrimitiveType.Sphere, new Vector3(0, 2.55f, -0.33f), new Vector3(0.16f, 0.14f, 0.12f), red, g, collider: false);
            Prim(PrimitiveType.Cube, new Vector3(0, 2.36f, -0.31f), new Vector3(0.3f, 0.07f, 0.04f), white, g, collider: false);
            if (male) Prim(PrimitiveType.Cylinder, new Vector3(0, 3.5f, 0), new Vector3(0.5f, 0.12f, 0.5f), black, g, collider: false); // 관모
            Label(g, new Vector3(0f, 1.25f, -0.34f), Quaternion.identity, text, 0.32f, new Color(0.15f, 0.08f, 0.06f));
        }

        static void Greenhouses()
        {
            var film = EnvMaterials.Transparent(new Color(0.75f, 0.8f, 0.85f, 0.28f), 0.85f);
            for (int i = 0; i < 3; i++)
            {
                var g = Group("비닐하우스", new Vector3(18f + i * 8f, 0f, -31f), 0f);
                Prim(PrimitiveType.Cylinder, new Vector3(0, 0f, 0), new Vector3(5.5f, 9f, 5.5f), film, g, Quaternion.Euler(90f, 0, 0), shadows: false);
                // 철골: 용마루 파이프 + 양쪽 끝 기둥
                var steel = EnvMaterials.Plain(new Color(0.55f, 0.55f, 0.55f), 0.6f, 0.6f);
                Prim(PrimitiveType.Cube, new Vector3(0, 2.76f, 0), new Vector3(0.07f, 0.07f, 18f), steel, g, collider: false);
                foreach (var ez in new[] { -8.9f, 8.9f })
                    foreach (var ex in new[] { -1.4f, 0f, 1.4f })
                        Prim(PrimitiveType.Cube, new Vector3(ex, 1.2f, ez), new Vector3(0.06f, 2.4f, 0.06f), steel, g, collider: false);
                if (i == 1)
                {
                    var lg = new GameObject("GreenhouseLight");
                    lg.transform.SetParent(g, false);
                    lg.transform.localPosition = new Vector3(0, 1.8f, 0);
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(0.75f, 0.95f, 0.8f);
                    l.range = 9f;
                    l.intensity = 0.9f;
                    lg.AddComponent<LampFlicker>().minLevel = GfxLevel.High;
                }
            }
        }

        // ================================================================ 가운데: 정자나무
        static void CenterTree()
        {
            var g = Group("정자나무", new Vector3(9f, 0f, 5f), 0f);
            var bark = EnvMaterials.Get(K.Bark, new Vector2(3f, 6f));
            Prim(PrimitiveType.Cylinder, new Vector3(0, 3f, 0), new Vector3(1.3f, 3f, 1.3f), bark, g);
            Prim(PrimitiveType.Cylinder, new Vector3(1.2f, 6.3f, 0.3f), new Vector3(0.5f, 1.8f, 0.5f), bark, g, Quaternion.Euler(0, 0, -35f), collider: false);
            Prim(PrimitiveType.Cylinder, new Vector3(-1.1f, 6.5f, -0.4f), new Vector3(0.45f, 1.7f, 0.45f), bark, g, Quaternion.Euler(10f, 0, 38f), collider: false);
            Prim(PrimitiveType.Cylinder, new Vector3(0.2f, 6.8f, 1.2f), new Vector3(0.4f, 1.6f, 0.4f), bark, g, Quaternion.Euler(35f, 0, 0), collider: false);
            var leaf = EnvMaterials.Plain(new Color(0.06f, 0.09f, 0.05f), 0.35f);
            var rnd = new System.Random(21);
            bool leafy = PropModels.Has("shrub_03");
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3((float)(rnd.NextDouble() * 6 - 3), 7.5f + (float)(rnd.NextDouble() * 2.5), (float)(rnd.NextDouble() * 6 - 3));
                float s = 3.5f + (float)rnd.NextDouble() * 2.5f;
                // 잎 모델이 있으면 도형 공은 속을 채우는 정도로 작게, 바깥은 잎사귀 덩어리로
                Prim(PrimitiveType.Sphere, p, new Vector3(s, s * 0.7f, s) * (leafy ? 0.6f : 1f), leaf, g, collider: false);
                if (leafy && i < 6)
                    Model("shrub_03", p - new Vector3(0f, s * 0.35f, 0f), (float)(rnd.NextDouble() * 360), s * 0.75f, g, false,
                        new Color(0.35f, 0.4f, 0.32f), 0.3f, true);
            }
            // 평상
            Box(new Vector3(2.6f, 0.45f, -2.2f), new Vector3(2.6f, 0.12f, 2f), K.WoodPlanks, g);
            foreach (var sx in new[] { 1.5f, 3.7f })
                foreach (var sz in new[] { -3.0f, -1.4f })
                    Box(new Vector3(sx, 0.2f, sz), new Vector3(0.12f, 0.4f, 0.12f), K.WoodPlanks, g, collider: false);
        }

        // ================================================================ 전봇대 · 가로등
        static void Utilities()
        {
            var poleMat = EnvMaterials.Get(K.Concrete, new Vector2(1f, 9f), new Color(0.55f, 0.55f, 0.52f));
            var wireMat = FX.UnlitMaterial(new Color(0.02f, 0.02f, 0.025f));
            Vector3? prevTop = null;
            for (int i = 0; i < 7; i++)
            {
                float z = -45f + i * 15f;
                if (Mathf.Abs(z) > Half - 2f) { prevTop = null; continue; } // 경계 밖 전봇대 생략
                var p = new Vector3(RoadX(z) + 4.6f, 0f, z); // 굽은 길을 따라
                Prim(PrimitiveType.Cylinder, p + new Vector3(0, 4.5f, 0), new Vector3(0.32f, 4.5f, 0.32f), poleMat);
                Prim(PrimitiveType.Cube, p + new Vector3(0, 8.4f, 0), new Vector3(1.8f, 0.12f, 0.12f), EnvMaterials.Plain(new Color(0.3f, 0.3f, 0.3f), 0.5f, 0.6f), collider: false);
                var top = p + new Vector3(0.8f, 8.5f, 0);
                if (prevTop.HasValue)
                {
                    for (int w = 0; w < 2; w++)
                    {
                        var off = new Vector3(-w * 1.6f, 0, 0);
                        Wire(prevTop.Value + off, top + off, wireMat);
                    }
                }
                prevTop = top;
            }

            // 가로등 (깜빡이는 것도 있다)
            Vector3[] lamps =
            {
                new Vector3(-3.6f, 0, -36f), new Vector3(-3.6f, 0, -18f), new Vector3(-3.6f, 0, 2f),
                new Vector3(-3.6f, 0, 20f), new Vector3(-3.6f, 0, 36f), new Vector3(-30f, 0, 11.4f), new Vector3(14f, 0, 32f),
            };
            for (int i = 0; i < lamps.Length; i++) if (i < 5) lamps[i].x += RoadX(lamps[i].z);
            for (int i = 0; i < lamps.Length; i++) StreetLamp(lamps[i], i == 1 || i == 5, i % 2 == 0 ? GfxLevel.Low : GfxLevel.Medium);
        }

        static void Wire(Vector3 a, Vector3 b, Material mat)
        {
            var go = new GameObject("Wire");
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.positionCount = 12;
            lr.startWidth = lr.endWidth = 0.035f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f;
                var p = Vector3.Lerp(a, b, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * 0.9f; // 처짐
                lr.SetPosition(i, p);
            }
        }

        static void StreetLamp(Vector3 p, bool flicker, GfxLevel minLevel)
        {
            var g = Group("가로등", p, 0f);
            Vector3 lightPos = new Vector3(1.35f, 5.3f, 0);
            var model = Model("street_lamp_02", Vector3.zero, 0f, 5.8f, g, collider: false, tint: new Color(0.7f, 0.7f, 0.7f), gloss: 0.6f);
            if (model != null)
            {
                // 기둥 판정만 따로, 불빛은 모델의 등갓 쪽(꼭대기에서 기둥 반대편)에
                var pole = g.gameObject.AddComponent<CapsuleCollider>();
                pole.center = new Vector3(0f, 2.9f, 0f); pole.height = 5.8f; pole.radius = 0.15f;
                var rs = model.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    Vector3 c = g.InverseTransformPoint(b.center);
                    Vector3 side = new Vector3(c.x, 0f, c.z);
                    lightPos = side.sqrMagnitude > 0.04f ? side * 2f + Vector3.up * 5.4f : new Vector3(0f, 5.4f, 0f);
                }
            }
            else
            {
                var metal = EnvMaterials.Plain(new Color(0.35f, 0.36f, 0.36f), 0.6f, 0.6f);
                Prim(PrimitiveType.Cylinder, new Vector3(0, 2.8f, 0), new Vector3(0.16f, 2.8f, 0.16f), metal, g);
                Prim(PrimitiveType.Cube, new Vector3(0.7f, 5.55f, 0), new Vector3(1.5f, 0.08f, 0.08f), metal, g, collider: false);
            }
            // 불 켜진 등갓 (모델 위에도 겹쳐 빛나 보이게)
            var headGlow = Prim(PrimitiveType.Cube, lightPos + new Vector3(0f, 0.15f, 0f), new Vector3(0.4f, 0.06f, 0.22f), EnvMaterials.Emissive(new Color(1f, 0.75f, 0.45f), 2.2f), g, collider: false, shadows: false);
            // 등갓 판정 (쏘면 깨진다)
            var headHit = new GameObject("LampHead");
            headHit.transform.SetParent(g, false);
            headHit.transform.localPosition = lightPos + new Vector3(0f, 0.1f, 0f);
            var hb = headHit.AddComponent<BoxCollider>();
            hb.size = new Vector3(0.7f, 0.35f, 0.5f);
            var lg = new GameObject("LampLight");
            lg.transform.SetParent(g, false);
            lg.transform.localPosition = lightPos;
            lg.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.72f, 0.42f);
            l.range = 15f;
            l.spotAngle = 110f;
            l.intensity = 3.2f;
            var f = lg.AddComponent<LampFlicker>();
            f.broken = flicker;
            f.minLevel = minLevel; // 저사양에서는 가로등 불빛을 절반만
            var sl = g.gameObject.AddComponent<ShootableLamp>();
            sl.lamp = l; sl.flicker = f; sl.glow = headGlow != null ? headGlow.GetComponent<Renderer>() : null;
        }

        // ================================================================ 소품
        static void Props()
        {
            // 파란 1톤 트럭 (전조등이 켜진 채 버려졌다)
            var t = Group("트럭", new Vector3(-9f, 0f, 16f), 160f);
            var body = EnvMaterials.Plain(new Color(0.15f, 0.3f, 0.62f), 0.7f, 0.3f);
            var dark = EnvMaterials.Plain(new Color(0.04f, 0.04f, 0.045f), 0.3f);
            Prim(PrimitiveType.Cube, new Vector3(0, 1.25f, 1.6f), new Vector3(1.75f, 1.5f, 1.4f), body, t);
            Prim(PrimitiveType.Cube, new Vector3(0, 1.45f, 2.31f), new Vector3(1.55f, 0.6f, 0.04f), EnvMaterials.Plain(new Color(0.02f, 0.03f, 0.04f), 0.95f), t, collider: false);
            Prim(PrimitiveType.Cube, new Vector3(0, 0.85f, -0.9f), new Vector3(1.8f, 0.5f, 3.4f), EnvMaterials.Plain(new Color(0.45f, 0.45f, 0.45f), 0.5f, 0.5f), t);
            foreach (var sx in new[] { -0.8f, 0.8f })
                foreach (var sz in new[] { 1.5f, -1.6f })
                    Prim(PrimitiveType.Cylinder, new Vector3(sx, 0.38f, sz), new Vector3(0.7f, 0.12f, 0.7f), dark, t, Quaternion.Euler(0, 0, 90f), collider: false);
            foreach (var sx in new[] { -0.6f, 0.6f })
            {
                Prim(PrimitiveType.Cube, new Vector3(sx, 0.9f, 2.31f), new Vector3(0.3f, 0.16f, 0.04f), EnvMaterials.Emissive(new Color(1f, 0.95f, 0.8f), 3f), t, collider: false);
            }
            var hl = new GameObject("Headlights");
            hl.transform.SetParent(t, false);
            hl.transform.localPosition = new Vector3(0, 0.9f, 2.4f);
            hl.transform.localRotation = Quaternion.Euler(8f, 0, 0);
            var l = hl.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.95f, 0.85f);
            l.range = 22f;
            l.spotAngle = 55f;
            l.intensity = 3.5f;
            l.shadows = LightShadows.Hard;
            hl.AddComponent<LampFlicker>().minLevel = GfxLevel.Medium;

            // 드럼통 · 장작더미 · 경운기 짐칸
            var rust = EnvMaterials.Plain(new Color(0.35f, 0.18f, 0.1f), 0.5f, 0.3f);
            Vector3[] drums = { new Vector3(-16f, 0.5f, 18f), new Vector3(-15.2f, 0.5f, 18.6f), new Vector3(-31f, 0.5f, -12f), new Vector3(-6.5f, 0.5f, -14f) };
            for (int i = 0; i < drums.Length; i++)
            {
                var d = drums[i];
                var barrel = Model("barrel_03", new Vector3(d.x, 0f, d.z), i * 70f, 0.95f, tint: new Color(0.8f, 0.7f, 0.65f), gloss: 0.55f);
                if (barrel == null) barrel = Prim(PrimitiveType.Cylinder, d, new Vector3(0.6f, 0.5f, 0.6f), rust);
                if (barrel != null) barrel.AddComponent<Hunt.Player.ExplosiveProp>().Setup(110f, 4.5f, 230f); // 쏘면 터지는 드럼통
            }
            Box(new Vector3(-33f, 0.5f, -10f), new Vector3(2.4f, 1f, 1.2f), K.WoodPlanks, tint: new Color(0.55f, 0.45f, 0.35f));
            Box(new Vector3(-40.5f, 0.6f, 28f), new Vector3(1.6f, 0.7f, 2.4f), K.MetalSheet, tint: new Color(0.3f, 0.45f, 0.3f));

            // 이끼 낀 바위
            var rnd = new System.Random(17);
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector3((float)(rnd.NextDouble() * 82 - 41), 0.2f, (float)(rnd.NextDouble() * 82 - 41));
                if (Mathf.Abs(p.x) < 6f) continue;
                float s = 0.7f + (float)rnd.NextDouble() * 1.4f;
                string rock = i % 2 == 0 ? "rock_09" : "moon_rock_02";
                if (Model(rock, new Vector3(p.x, -0.1f, p.z), (float)rnd.NextDouble() * 360f, s * 0.9f, tint: new Color(0.6f, 0.62f, 0.58f), gloss: 0.55f) == null)
                    Prim(PrimitiveType.Sphere, p, new Vector3(s * 1.3f, s * 0.8f, s), EnvMaterials.Get(K.Rock, new Vector2(s, s)));
            }
        }

        // ================================================================ 생활 소품 (Poly Haven 모델, 없으면 생략)
        static void Dressing()
        {
            // 장독대: 돌 단 위에 옹기 항아리
            void Jangdok(Vector3 at, float yaw, int seed)
            {
                if (!PropModels.Has("ceramic_pot")) return;
                var g = Group("장독대", at, yaw);
                Box(new Vector3(0f, 0.12f, 0f), new Vector3(2.6f, 0.24f, 1.6f), K.StoneWall, g, tint: new Color(0.55f, 0.55f, 0.52f));
                var r = new System.Random(seed);
                for (int i = 0; i < 7; i++)
                {
                    float x = -0.95f + (i % 4) * 0.62f + (float)r.NextDouble() * 0.08f;
                    float z = i < 4 ? -0.35f : 0.38f;
                    float h = i < 4 ? 0.55f + (float)r.NextDouble() * 0.12f : 0.8f + (float)r.NextDouble() * 0.15f; // 뒷줄은 큰 독
                    Model("ceramic_pot", new Vector3(x, 0.24f, z), (float)r.NextDouble() * 360f, h, g, tint: new Color(0.45f, 0.33f, 0.26f), gloss: 0.75f);
                }
            }
            Jangdok(new Vector3(-20.5f, 0f, -23.6f), -4f, 3);   // 기와집 마당
            Jangdok(new Vector3(-35.5f, 0f, -8.5f), 12f, 5);    // 초가집 옆
            Jangdok(new Vector3(-17f, 0f, 29.2f), 0f, 7);       // 농가 뒤

            // LPG 가스통 (농가 벽에 붙여서)
            Vector3[] lpg = { new Vector3(-14.9f, 0f, 22.5f), new Vector3(-14.9f, 0f, 23.3f), new Vector3(-31.6f, 0f, 20.2f), new Vector3(-7.4f, 0f, -0.5f) };
            for (int i = 0; i < lpg.Length; i++)
            {
                var tank = Model("small_lpg_tank", lpg[i], i * 40f, 1.25f, gloss: 0.6f);
                if (tank != null) tank.AddComponent<Hunt.Player.ExplosiveProp>().Setup(55f, 5.5f, 320f); // 쏘면 터지는 LPG 가스통
            }

            // 농가 플라스틱 상자 (쌓아 둔 것)
            void Crates(Vector3 at, float yaw, int n)
            {
                for (int i = 0; i < n; i++)
                    Model("plastic_crate_02", at + new Vector3((i / 3) * 0.62f, (i % 3) * 0.3f, 0f), yaw + (i % 2) * 6f, 0.3f, gloss: 0.5f);
            }
            Crates(new Vector3(11.5f, 0f, -24f), 0f, 6);   // 비닐하우스 앞
            Crates(new Vector3(-17.5f, 0f, 19f), 90f, 4); // 농가 마당
            Crates(new Vector3(-36f, 0f, 31.3f), 0f, 5);  // 창고 앞

            // 창고 앞 나무 상자 · 삽, 마을회관 쓰레기통, 평상 옆 의자, 초가집 화덕
            Model("old_military_crate", new Vector3(-40.2f, 0f, 31.5f), 10f, 0.55f);
            Model("old_military_crate", new Vector3(-40f, 0.55f, 31.6f), -5f, 0.55f);
            Model("rusted_spade_01", new Vector3(-33.2f, 0f, 31.8f), 30f, 1.1f, collider: false);
            Model("metal_trash_can", new Vector3(-6.8f, 0f, 2.8f), 0f, 0.95f, gloss: 0.6f);
            Model("metal_trash_can", new Vector3(-6.9f, 0f, 3.7f), 40f, 0.95f, gloss: 0.6f);
            Model("painted_wooden_bench", new Vector3(13.5f, 0f, 1.5f), 90f, 0.85f, tint: new Color(0.6f, 0.55f, 0.5f));
            Model("stone_fire_pit", new Vector3(-33f, 0f, -1.5f), 0f, 0.45f);

            // 덤불: 담장 밑, 집 모퉁이, 길가 (길 위는 피한다)
            if (PropModels.Has("shrub_03"))
            {
                var r = new System.Random(41);
                Vector3[] spots =
                {
                    new Vector3(-28.8f, 0f, 16f), new Vector3(-28.8f, 0f, 21f), new Vector3(-22f, 0f, 30.8f), new Vector3(-33f, 0f, 30.8f),
                    new Vector3(-17f, 0f, -25.6f), new Vector3(-31f, 0f, -25.6f), new Vector3(-33.2f, 0f, -16f), new Vector3(-15.8f, 0f, -14f),
                    new Vector3(-41f, 0f, -9f), new Vector3(-42f, 0f, 1f), new Vector3(8f, 0f, -20f), new Vector3(12f, 0f, 14f),
                    new Vector3(22f, 0f, 20f), new Vector3(-10f, 0f, 38f), new Vector3(28f, 0f, -24f), new Vector3(-12f, 0f, -38f),
                };
                for (int i = 0; i < spots.Length; i++)
                {
                    bool big = i % 3 != 2 || !PropModels.Has("shrub_sorrel_01");
                    var bushGo = Model(big ? "shrub_03" : "shrub_sorrel_01", spots[i], (float)r.NextDouble() * 360f, big ? 0.9f + (float)r.NextDouble() * 0.6f : 0.5f,
                        collider: false, tint: new Color(0.42f, 0.46f, 0.38f), gloss: 0.35f, cutout: true);
                    if (big) Hiding.Add(spots[i], 1.3f, bushGo); // 큰 덤불엔 앉아서 숨을 수 있다 (불타면 못 숨는다)
                }
            }
        }

        // ================================================================ 바깥: 산과 경계
        static void Mountains()
        {
            var mat = EnvMaterials.Plain(new Color(0.03f, 0.045f, 0.035f), 0.2f);
            var rnd = new System.Random(31);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.3f;
                float r = 110f + (float)rnd.NextDouble() * 50f;
                float w = 70f + (float)rnd.NextDouble() * 50f, h = 35f + (float)rnd.NextDouble() * 35f;
                Prim(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * r, -h * 0.25f, Mathf.Sin(a) * r), new Vector3(w, h, w * 0.8f), mat, collider: false, shadows: false);
            }
            // 경계를 따라 들쭉날쭉한 수풀 (네모난 보이지 않는 벽 대신 자연스러운 끝)
            if (PropModels.Has("shrub_03"))
            {
                int count = QualityManager.Level == GfxLevel.Low ? 26 : 44;
                for (int i = 0; i < count; i++)
                {
                    float a = i / (float)count * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.12f;
                    // 둥근 사각형 둘레 (경계 바로 안쪽)
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float k = Half / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.z));
                    Vector3 p = dir * Mathf.Min(k, Half * 1.25f) * (0.97f + (float)rnd.NextDouble() * 0.06f);
                    if (Mathf.Abs(p.x - RoadX(p.z)) < 5f) continue;                 // 큰길 끝은 열어 둔다
                    if (p.x < -40f && Mathf.Abs(p.z - 9f) < 4f) continue;            // 서쪽길 끝
                    Model("shrub_03", p, (float)rnd.NextDouble() * 360f, 1.6f + (float)rnd.NextDouble() * 1.4f,
                        collider: false, tint: new Color(0.3f, 0.36f, 0.28f), gloss: 0.3f, cutout: true);
                }
            }
            // 경계 근처 대숲/덤불 실루엣
            var bush = EnvMaterials.Plain(new Color(0.04f, 0.06f, 0.035f), 0.3f);
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                float r = 52f + (float)rnd.NextDouble() * 6f;
                float s = 3f + (float)rnd.NextDouble() * 4f;
                Prim(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * r, s * 0.3f, Mathf.Sin(a) * r), new Vector3(s * 1.4f, s, s * 1.4f), bush, collider: false);
            }
        }

        static void Boundary()
        {
            // 보이지 않는 벽 (플레이 영역)
            void Wall(Vector3 p, Vector3 s)
            {
                var go = new GameObject("Boundary");
                go.transform.SetParent(root, false);
                go.transform.localPosition = p;
                var c = go.AddComponent<BoxCollider>();
                c.size = s;
            }
            Wall(new Vector3(0, 3f, Half + 1f), new Vector3(100f, 6f, 1f));
            Wall(new Vector3(0, 3f, -Half - 1f), new Vector3(100f, 6f, 1f));
            Wall(new Vector3(Half + 1f, 3f, 0), new Vector3(1f, 6f, 100f));
            Wall(new Vector3(-Half - 1f, 3f, 0), new Vector3(1f, 6f, 100f));
        }
    }
}

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
        public const float Half = 48f;

        /// <summary>망자가 걸어 나오는 곳 (무덤, 논 끝, 마을 어귀, 산자락).</summary>
        public static readonly List<Vector3> SpawnAnchors = new List<Vector3>();

        static Transform root;

        public static Transform Build(Transform worldRoot)
        {
            EnvMaterials.ClearCache();
            root = new GameObject("Village").transform;
            root.SetParent(worldRoot, false);
            SpawnAnchors.Clear();
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
            Mountains();
            Boundary();

            SpawnAnchors.AddRange(new[]
            {
                new Vector3(18f, 0f, 40f), new Vector3(-8f, 0f, 44f), new Vector3(34f, 0f, 44f),   // 무덤
                new Vector3(45f, 0f, 12f), new Vector3(45f, 0f, -8f),                              // 논 끝
                new Vector3(0f, 0f, -46f), new Vector3(-20f, 0f, -45f),                            // 마을 어귀
                new Vector3(-45f, 0f, 34f), new Vector3(-45f, 0f, 2f), new Vector3(-44f, 0f, -30f), // 서쪽 산자락
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

        static void Roads()
        {
            // 남북 큰길 (금 간 콘크리트)
            Box(new Vector3(0f, 0.03f, 0f), new Vector3(6f, 0.06f, 100f), K.Road);
            // 서쪽 갈림길
            Box(new Vector3(-24f, 0.03f, 8f), new Vector3(48f, 0.06f, 4f), K.Road);
            // 북쪽 묘지로 가는 흙길
            Box(new Vector3(12f, 0.025f, 32f), new Vector3(3f, 0.05f, 16f), K.FarmSoil, rot: Quaternion.Euler(0f, 30f, 0f));
            // 웅덩이 (가로등 빛이 비친다)
            var puddle = EnvMaterials.Plain(new Color(0.02f, 0.025f, 0.03f), 0.97f);
            var rnd = new System.Random(9);
            for (int i = 0; i < 14; i++)
            {
                float z = (float)(rnd.NextDouble() * 90 - 45);
                float x = (float)(rnd.NextDouble() * 4 - 2);
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
                    var c = new Vector3(15f + ix * 11.5f, 0f, -12f + iz * 12f);
                    // 물 댄 논바닥
                    Prim(PrimitiveType.Cube, c + new Vector3(0, 0.03f, 0), new Vector3(10.6f, 0.06f, 11f), water);
                    // 논두렁 (사방)
                    Box(c + new Vector3(0, 0.15f, 5.75f), new Vector3(11.5f, 0.3f, 0.6f), K.Grass);
                    Box(c + new Vector3(0, 0.15f, -5.75f), new Vector3(11.5f, 0.3f, 0.6f), K.Grass);
                    Box(c + new Vector3(5.6f, 0.15f, 0), new Vector3(0.6f, 0.3f, 11f), K.Grass);
                    Box(c + new Vector3(-5.6f, 0.15f, 0), new Vector3(0.6f, 0.3f, 11f), K.Grass);
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
            Vector3[] bales = { new Vector3(10f, 0.7f, 22f), new Vector3(12f, 0.7f, 23.5f), new Vector3(11f, 0.7f, 25.5f), new Vector3(44f, 0.7f, -22f), new Vector3(41.5f, 0.7f, -23f) };
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
                    float pitch = roof == Roof.Hip ? 30f : 24f;
                    float half = (d / 2f + 0.7f) / Mathf.Cos(pitch * Mathf.Deg2Rad);
                    float rise = Mathf.Sin(pitch * Mathf.Deg2Rad) * half / 2f;
                    var mat = EnvMaterials.Get(roofKind, new Vector2(w + 1.2f, half), roofTint);
                    Prim(PrimitiveType.Cube, new Vector3(0, h + rise, d / 4f + 0.2f), new Vector3(w + 1.2f, 0.12f, half), mat, g, Quaternion.Euler(pitch, 0, 0));
                    Prim(PrimitiveType.Cube, new Vector3(0, h + rise, -d / 4f - 0.2f), new Vector3(w + 1.2f, 0.12f, half), mat, g, Quaternion.Euler(-pitch, 0, 0));
                    // 용마루
                    Prim(PrimitiveType.Cube, new Vector3(0, h + rise * 2f + 0.05f, 0), new Vector3(w + 1.3f, 0.22f, 0.32f), EnvMaterials.Plain(new Color(0.12f, 0.12f, 0.13f), 0.6f), g);
                    // 박공 삼각벽 대신 막음판
                    Box(new Vector3(w / 2f - 0.05f, h + rise * 0.8f, 0), new Vector3(0.1f, rise * 1.6f, d * 0.6f), wall, g);
                    Box(new Vector3(-w / 2f + 0.05f, h + rise * 0.8f, 0), new Vector3(0.1f, rise * 1.6f, d * 0.6f), wall, g);
                    if (roof == Roof.Hip)
                    {
                        // 기와집: 처마 끝이 살짝 들린 느낌 (모서리 추녀)
                        var dark = EnvMaterials.Plain(new Color(0.1f, 0.1f, 0.11f), 0.6f);
                        foreach (var sx in new[] { -1f, 1f })
                            foreach (var sz in new[] { -1f, 1f })
                                Prim(PrimitiveType.Cube, new Vector3(sx * (w / 2f + 0.5f), h + 0.35f, sz * (d / 2f + 0.55f)), new Vector3(0.5f, 0.14f, 0.5f), dark, g,
                                    Quaternion.Euler(sz * 18f, 0, -sx * 18f), collider: false);
                    }
                    break;
                }
                case Roof.Thatch:
                    // 초가지붕: 둥근 짚더미
                    Prim(PrimitiveType.Sphere, new Vector3(0, h + 0.1f, 0), new Vector3(w + 1.4f, 2.4f, d + 1.4f), EnvMaterials.Get(K.Thatch, new Vector2(w * 1.6f, d * 1.6f)), g);
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
                lg.AddComponent<LampFlicker>().subtle = true;
            }
            return g;
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
            var shed = Group("창고", new Vector3(-42f, 0f, 36f), 0f);
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
                var p = new Vector3(-12f + i * 5.2f + (float)(rnd.NextDouble() * 2 - 1), 0f, 38f + (float)(rnd.NextDouble() * 7));
                if (Mathf.Abs(p.x - 22f) < 7f) continue; // 고분 자리
                var g = Group("무덤", p, (float)(rnd.NextDouble() * 30 - 15));
                Prim(PrimitiveType.Sphere, new Vector3(0, 0f, 0), new Vector3(3.6f, 1.7f, 4f), grass, g);
                Box(new Vector3(0, 0.45f, -2.6f), new Vector3(0.5f, 0.9f, 0.14f), K.Rock, g, tint: new Color(0.55f, 0.55f, 0.52f)); // 비석
                Box(new Vector3(0, 0.15f, -2.2f), new Vector3(1.0f, 0.3f, 0.6f), K.Rock, g);                                      // 상석
            }

            // 무너져 열린 조선 고분 — 붉은 빛이 새어 나온다
            var tomb = Group("열린고분", new Vector3(22f, 0f, 41f), 180f);
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
            Jangseung(new Vector3(-4.4f, 0f, -40f), "天\n下\n大\n將\n軍", true);
            Jangseung(new Vector3(4.4f, 0f, -40f), "地\n下\n女\n將\n軍", false);

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
                var g = Group("비닐하우스", new Vector3(18f + i * 8f, 0f, -33f), 0f);
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
                    lg.AddComponent<LampFlicker>();
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
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3((float)(rnd.NextDouble() * 6 - 3), 7.5f + (float)(rnd.NextDouble() * 2.5), (float)(rnd.NextDouble() * 6 - 3));
                float s = 3.5f + (float)rnd.NextDouble() * 2.5f;
                Prim(PrimitiveType.Sphere, p, new Vector3(s, s * 0.7f, s), leaf, g, collider: false);
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
                var p = new Vector3(4.6f, 0f, z);
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
                new Vector3(-3.6f, 0, 20f), new Vector3(-3.6f, 0, 36f), new Vector3(-30f, 0, 10.5f), new Vector3(14f, 0, 32f),
            };
            for (int i = 0; i < lamps.Length; i++) StreetLamp(lamps[i], i == 1 || i == 5);
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

        static void StreetLamp(Vector3 p, bool flicker)
        {
            var g = Group("가로등", p, 0f);
            var metal = EnvMaterials.Plain(new Color(0.35f, 0.36f, 0.36f), 0.6f, 0.6f);
            Prim(PrimitiveType.Cylinder, new Vector3(0, 2.8f, 0), new Vector3(0.16f, 2.8f, 0.16f), metal, g);
            Prim(PrimitiveType.Cube, new Vector3(0.7f, 5.55f, 0), new Vector3(1.5f, 0.08f, 0.08f), metal, g, collider: false);
            Prim(PrimitiveType.Cube, new Vector3(1.35f, 5.48f, 0), new Vector3(0.45f, 0.12f, 0.25f), EnvMaterials.Emissive(new Color(1f, 0.75f, 0.45f), 2.2f), g, collider: false);
            var lg = new GameObject("LampLight");
            lg.transform.SetParent(g, false);
            lg.transform.localPosition = new Vector3(1.35f, 5.3f, 0);
            lg.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.72f, 0.42f);
            l.range = 15f;
            l.spotAngle = 110f;
            l.intensity = 3.2f;
            var f = lg.AddComponent<LampFlicker>();
            f.broken = flicker;
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

            // 드럼통 · 장작더미 · 경운기 짐칸
            var rust = EnvMaterials.Plain(new Color(0.35f, 0.18f, 0.1f), 0.5f, 0.3f);
            Vector3[] drums = { new Vector3(-16f, 0.5f, 18f), new Vector3(-15.2f, 0.5f, 18.6f), new Vector3(-31f, 0.5f, -12f), new Vector3(-6.5f, 0.5f, -14f) };
            foreach (var d in drums) Prim(PrimitiveType.Cylinder, d, new Vector3(0.6f, 0.5f, 0.6f), rust);
            Box(new Vector3(-33f, 0.5f, -10f), new Vector3(2.4f, 1f, 1.2f), K.WoodPlanks, tint: new Color(0.55f, 0.45f, 0.35f));
            Box(new Vector3(-44f, 0.6f, 30f), new Vector3(1.6f, 0.7f, 2.4f), K.MetalSheet, tint: new Color(0.3f, 0.45f, 0.3f));

            // 이끼 낀 바위
            var rnd = new System.Random(17);
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector3((float)(rnd.NextDouble() * 90 - 45), 0.2f, (float)(rnd.NextDouble() * 90 - 45));
                if (Mathf.Abs(p.x) < 6f) continue;
                float s = 0.7f + (float)rnd.NextDouble() * 1.4f;
                Prim(PrimitiveType.Sphere, p, new Vector3(s * 1.3f, s * 0.8f, s), EnvMaterials.Get(K.Rock, new Vector2(s, s)));
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

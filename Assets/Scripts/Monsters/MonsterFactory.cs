using Hunt.Core;
using System.Collections.Generic;
using Hunt.Combat;
using Hunt.Data;
using Hunt.Evolution;
using Hunt.Player;
using UnityEngine;

namespace Hunt.Monsters
{
    /// <summary>
    /// 괴물 외형 조립기. 지금은 기본 도형(프리미티브)으로 만들지만, 컨셉이 정해지면
    /// 이 클래스만 프리팹/모델 기반으로 교체하면 된다. (MonsterData·Monster 로직은 그대로)
    /// 모든 좌표는 scale=1 기준 키 약 2m 인체형.
    /// </summary>
    public static class MonsterFactory
    {
        public static readonly Color WeakPointColor = new Color(1f, 0.55f, 0.05f);
        static readonly Color BoneColor = new Color(0.85f, 0.8f, 0.65f);

        public static Monster Create(MonsterData d, Vector3 position, EvolutionProfile evo, float hpScale, PlayerCharacter target, Transform parent)
        {
            // 애니메이션 좀비 모델이 있으면 그것으로, 없으면 아래 도형 괴물로
            if (ZombieRig.Available) return CreateRigged(d, position, evo, hpScale, target, parent);

            var root = new GameObject($"{d.displayName}");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * d.scale;

            var m = root.AddComponent<Monster>();
            m.Init(d, hpScale, evo, target);

            Color skin = Color.Lerp(d.color, new Color(0.5f, 0.1f, 0.4f), Mathf.Min(0.5f, evo.generation * 0.08f)); // 진화할수록 보랏빛
            bool spitter = d.archetype == MonsterArchetype.Spitter;
            bool heavy = d.archetype == MonsterArchetype.Brute || d.archetype == MonsterArchetype.Boss;

            // 몸통 / 머리
            Part(PrimitiveType.Capsule, root.transform, new Vector3(0, 0.95f, 0), new Vector3(spitter ? 0.65f : 0.8f, 0.6f, spitter ? 0.65f : 0.8f), skin, m, HitZone.Body);
            var head = Part(PrimitiveType.Sphere, root.transform, new Vector3(0, 1.75f, 0.08f), Vector3.one * (spitter ? 0.6f : 0.45f), skin * 1.1f, m, HitZone.Head);
            m.SetHead(head.transform);

            // 눈 (판정 없음)
            // 어둠 속에서 붉게 빛나는 눈
            Deco(PrimitiveType.Sphere, root.transform, new Vector3(0.09f, 1.8f, 0.28f), Vector3.one * 0.08f, new Color(1f, 0.15f, 0.1f), true);
            Deco(PrimitiveType.Sphere, root.transform, new Vector3(-0.09f, 1.8f, 0.28f), Vector3.one * 0.08f, new Color(1f, 0.15f, 0.1f), true);

            // 팔 (몸통 판정)
            float armW = heavy ? 0.24f : 0.14f;
            var armL = Part(PrimitiveType.Cube, root.transform, new Vector3(0.5f, 1.1f, 0.25f), new Vector3(armW, 0.65f, armW), skin * 0.9f, m, HitZone.Body);
            var armR = Part(PrimitiveType.Cube, root.transform, new Vector3(-0.5f, 1.1f, 0.25f), new Vector3(armW, 0.65f, armW), skin * 0.9f, m, HitZone.Body);
            armL.transform.localRotation = Quaternion.Euler(55f, 0, 0);
            armR.transform.localRotation = Quaternion.Euler(55f, 0, 0);

            // 다리 (파괴 가능 → 이동속도 감소)
            float legHp = d.maxHp * hpScale * (d.archetype == MonsterArchetype.Boss ? 0.08f : 0.18f) * evo.legHpMult;
            Part(PrimitiveType.Cube, root.transform, new Vector3(0.2f, 0.3f, 0), new Vector3(0.2f, 0.6f, 0.22f), skin * 0.8f, m, HitZone.Leg, legHp);
            Part(PrimitiveType.Cube, root.transform, new Vector3(-0.2f, 0.3f, 0), new Vector3(0.2f, 0.6f, 0.22f), skin * 0.8f, m, HitZone.Leg, legHp);

            // 투구 (진화: 헤드샷 대응)
            if (evo.helmetLevel > 0)
            {
                float hs = (spitter ? 0.7f : 0.55f) * (1f + 0.05f * evo.helmetLevel);
                Part(PrimitiveType.Cube, root.transform, new Vector3(0, 1.77f, 0.08f), Vector3.one * hs, BoneColor, m, HitZone.Armor,
                    d.maxHp * hpScale * 0.12f * evo.helmetLevel);
            }

            // 전면 장갑판 (브루트/보스): 가슴 약점을 가린다
            if (heavy)
            {
                float armorHp = d.maxHp * hpScale * (d.archetype == MonsterArchetype.Boss ? 0.12f : 0.45f);
                Part(PrimitiveType.Cube, root.transform, new Vector3(0, 1.0f, 0.47f), new Vector3(0.95f, 0.85f, 0.14f), BoneColor * 0.9f, m, HitZone.Armor, armorHp);
            }

            // 약점
            if (d.archetype == MonsterArchetype.Boss)
            {
                var wps = new List<MonsterPart>
                {
                    WeakPoint(root.transform, m, WeakPointLocation.Back, 0.32f),
                    WeakPoint(root.transform, m, WeakPointLocation.Shoulder, 0.32f),
                    WeakPoint(root.transform, m, WeakPointLocation.Front, 0.32f), // 장갑판 뒤
                };
                m.SetBossWeakPoints(wps);
            }
            else
            {
                WeakPoint(root.transform, m, evo.weakPoint, d.archetype == MonsterArchetype.Runner ? 0.24f : 0.3f);
            }

            return m;
        }

        // ================================================================ 애니메이션 모델 좀비
        static readonly Color[] VillagerTints =
        {
            new Color(0.85f, 0.85f, 0.8f), new Color(0.72f, 0.78f, 0.9f), new Color(0.92f, 0.8f, 0.68f),
            new Color(0.7f, 0.76f, 0.64f), new Color(0.8f, 0.7f, 0.72f),
        };
        static readonly Color ArmorRed = new Color(0.38f, 0.07f, 0.06f);   // 두정갑 (조선 갑옷)
        static readonly Color Brass = new Color(0.75f, 0.58f, 0.25f);
        static readonly Color GatBlack = new Color(0.03f, 0.03f, 0.035f);

        static Color RigTint(MonsterData d, EvolutionProfile evo)
        {
            Color c;
            switch (d.archetype)
            {
                case MonsterArchetype.Brute: c = new Color(0.72f, 0.6f, 0.55f); break;
                case MonsterArchetype.Spitter: c = new Color(0.62f, 0.8f, 0.5f); break;
                case MonsterArchetype.Boss: c = new Color(0.62f, 0.48f, 0.62f); break;
                default:
                    c = VillagerTints[Random.Range(0, VillagerTints.Length)];
                    // 망자 유형마다 살빛이 달라 한눈에 구별된다 (단단한형 누렇게, 빠른형 창백하게, 광폭형 붉게…)
                    if (d.id != null && d.id.StartsWith("z_") && d.id != "z_basic") c = Color.Lerp(c, d.color * 1.35f, 0.6f);
                    break;
            }
            // 진화할수록 핏기 없이 보랏빛으로
            return Color.Lerp(c, new Color(0.55f, 0.4f, 0.65f), Mathf.Min(0.45f, evo.generation * 0.07f));
        }

        // ---------------------------------------------------------------- 킹덤풍 머리 장식
        static Material hatBlack, hatRed, hatNavy, hatCord;

        static Material HatMat(ref Material m, Color c, float gloss)
        {
            if (m == null) m = EnvMaterials.Plain(c, gloss, 0f);
            return m;
        }

        static void HatPiece(Transform head, PrimitiveType t, Vector3 worldPos, Quaternion rot, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(t);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = "Hat";
            g.layer = 2;
            g.transform.position = worldPos;
            g.transform.rotation = rot;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            g.transform.SetParent(head, true); // 머리뼈를 따라 움직인다
        }

        /// <summary>
        /// 조선 역병 망자: 주민은 갓·상투(망건), 장군 망자는 붉은 상모 달린 전립, 대감은 큰 갓과 갓끈.
        /// 현대 주민 망자는 맨머리. 모델 위에 덧씌우는 저폴리 도형이라 '하' 품질에서도 가볍다.
        /// </summary>
        static bool Headwear(MonsterData d, ZombieRig rig, Transform root)
        {
            if (rig.Head == null) return false;
            bool joseon = false;
            float u = rig.Unit; // 키(괴물 크기 포함) 기준 배율
            Vector3 top = rig.HeadTop != null ? rig.HeadTop.position : rig.Head.position + Vector3.up * 0.18f * u;
            Quaternion up = Quaternion.Euler(0f, root.eulerAngles.y, 0f);
            Vector3 fwd = up * Vector3.forward;
            var black = HatMat(ref hatBlack, new Color(0.03f, 0.03f, 0.035f), 0.55f);
            var cord = HatMat(ref hatCord, new Color(0.55f, 0.32f, 0.12f), 0.6f);

            void Gat(float size)
            {
                // 테(양태) + 대우(모자 꼭대기), 살짝 앞으로 기울여 쓴다
                Quaternion tilt = up * Quaternion.Euler(-6f, 0f, 0f);
                Vector3 brim = top - Vector3.up * 0.03f * u;
                HatPiece(rig.Head, PrimitiveType.Cylinder, brim, tilt, new Vector3(0.62f, 0.006f, 0.62f) * u * size, black);
                HatPiece(rig.Head, PrimitiveType.Cylinder, brim + tilt * Vector3.up * 0.085f * u * size, tilt, new Vector3(0.19f, 0.085f, 0.19f) * u * size, black);
                // 갓끈: 양 옆으로 늘어진 구슬끈
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 sideDir = up * Vector3.right * side;
                    HatPiece(rig.Head, PrimitiveType.Cylinder, top + sideDir * 0.085f * u - Vector3.up * 0.16f * u, up, new Vector3(0.012f, 0.12f, 0.012f) * u * size, cord);
                }
            }

            void Topknot()
            {
                // 상투 + 망건(이마 띠)
                HatPiece(rig.Head, PrimitiveType.Sphere, top + Vector3.up * 0.02f * u, up, new Vector3(0.07f, 0.09f, 0.07f) * u, black);
                HatPiece(rig.Head, PrimitiveType.Cylinder, top - Vector3.up * 0.07f * u, up, new Vector3(0.215f, 0.022f, 0.235f) * u, black);
            }

            switch (d.archetype)
            {
                case MonsterArchetype.Runner:
                {
                    float r = Random.value;
                    if (r < 0.4f) { Gat(1f); joseon = true; }
                    else if (r < 0.7f) { Topknot(); joseon = true; }
                    break;
                }
                case MonsterArchetype.Spitter:
                    if (Random.value < 0.6f) { Topknot(); joseon = true; }
                    break;
                case MonsterArchetype.Brute:
                {
                    // 전립: 검은 둥근 모자 + 넓은 테 + 붉은 상모 + 공작깃 대신 붉은 술
                    var navy = HatMat(ref hatNavy, new Color(0.05f, 0.06f, 0.09f), 0.45f);
                    var red = HatMat(ref hatRed, new Color(0.55f, 0.08f, 0.06f), 0.4f);
                    HatPiece(rig.Head, PrimitiveType.Sphere, top + Vector3.up * 0.02f * u, up, new Vector3(0.25f, 0.17f, 0.25f) * u, navy);
                    HatPiece(rig.Head, PrimitiveType.Cylinder, top - Vector3.up * 0.04f * u, up, new Vector3(0.45f, 0.008f, 0.45f) * u, navy);
                    HatPiece(rig.Head, PrimitiveType.Sphere, top + Vector3.up * 0.12f * u, up, new Vector3(0.07f, 0.1f, 0.07f) * u, red);
                    HatPiece(rig.Head, PrimitiveType.Cylinder, top + Vector3.up * 0.03f * u, up, new Vector3(0.255f, 0.02f, 0.255f) * u, red);
                    break;
                }
                case MonsterArchetype.Boss:
                    Gat(1.35f);
                    joseon = true;
                    break;
            }
            return joseon;
        }

        // ================================================================ 소복·도포 (킹덤풍 의상)
        static readonly Dictionary<int, Mesh> robeMeshes = new Dictionary<int, Mesh>();
        static readonly Dictionary<Color, Material> robeMats = new Dictionary<Color, Material>();

        /// <summary>
        /// 위가 좁고 아래가 퍼지는 천 통(양면). 높이 1, 윗반지름 1, 아랫반지름 flare. 모양마다 한 번만 만든다.
        /// </summary>
        static Mesh RobeMesh(float flare)
        {
            int key = Mathf.RoundToInt(flare * 100f);
            if (robeMeshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            const int N = 18;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            for (int ring = 0; ring <= 3; ring++)
            {
                float t = ring / 3f;
                float rad = Mathf.Lerp(1f, flare, t * t);         // 아래로 갈수록 퍼짐
                for (int i = 0; i <= N; i++)
                {
                    float a = i / (float)N * Mathf.PI * 2f;
                    float wob = 1f + 0.06f * Mathf.Sin(a * 5f + ring) * t; // 주름
                    v.Add(new Vector3(Mathf.Cos(a) * rad * wob, -t, Mathf.Sin(a) * rad * wob));
                    uv.Add(new Vector2(i / (float)N, 1f - t));
                }
            }
            int row = N + 1;
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < N; i++)
                {
                    int a0 = ring * row + i, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    tri.AddRange(new[] { a0, b0, a1, a1, b0, b1 });   // 바깥 (안쪽은 DoubleSided가 만든다)
                }
            mesh = MeshUtil.DoubleSided("Robe", v, uv, tri);
            robeMeshes[key] = mesh;
            return mesh;
        }

        static Material RobeMat(Color c)
        {
            if (robeMats.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(Shader.Find("Standard")) { name = "Robe", color = c };
            m.SetFloat("_Glossiness", 0.12f); // 비에 젖은 무명
            var tex = Resources.Load<Texture2D>("Env/clay_plaster_diff"); // 거친 천 결 대용
            if (tex != null) { m.mainTexture = tex; m.mainTextureScale = new Vector2(2f, 1f); }
            robeMats[c] = m;
            return m;
        }

        static void RobePiece(Transform bone, Vector3 topWorld, Quaternion yaw, float topRadius, float height, float flare, Color c)
        {
            var go = new GameObject("Robe");
            go.AddComponent<MeshFilter>().sharedMesh = RobeMesh(flare);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = RobeMat(c);
            r.shadowCastingMode = QualityManager.Shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.position = topWorld;
            go.transform.rotation = yaw;
            go.transform.localScale = new Vector3(topRadius, height, topRadius);
            go.transform.SetParent(bone, true);
        }

        /// <summary>조선 망자에게 소복(흰 무명) 또는 도포(쪽빛)를 입힌다. 저고리는 가슴에, 아랫도리는 엉덩이에 붙어 몸을 따라 흔들린다.</summary>
        static void Robe(MonsterData d, ZombieRig rig, Transform root)
        {
            if (rig.Hips == null || rig.Spine2 == null) return;
            float u = rig.Unit;
            var yaw = Quaternion.Euler(0f, root.eulerAngles.y, 0f);
            float footY = Mathf.Min(rig.LeftFoot != null ? rig.LeftFoot.position.y : root.position.y,
                                    rig.RightFoot != null ? rig.RightFoot.position.y : root.position.y);
            bool boss = d.archetype == MonsterArchetype.Boss;
            // 소복은 집마다 바랜 정도가 다르다 / 대감은 쪽빛 도포
            float dirt = Random.Range(0f, 0.18f);
            Color cloth = boss ? new Color(0.24f, 0.28f, 0.36f) : new Color(0.8f - dirt, 0.78f - dirt, 0.72f - dirt * 1.2f);

            // 저고리(윗옷): 목 아래 → 허리
            Vector3 neckBase = (rig.Neck != null ? rig.Neck.position : rig.Spine2.position + Vector3.up * 0.15f * u) - Vector3.up * 0.04f * u;
            float torso = Mathf.Max(0.2f * u, neckBase.y - rig.Hips.position.y);
            RobePiece(rig.Spine2, neckBase, yaw, 0.15f * u, torso + 0.06f * u, 1.15f, cloth); // 급소(가슴·등)가 천 밖으로 보이도록 몸에 붙게

            // 치마·바지 / 도포 자락: 허리 → 발목 근처 (대감은 땅까지)
            Vector3 waist = rig.Hips.position + Vector3.up * 0.06f * u;
            float legLen = Mathf.Max(0.3f * u, waist.y - footY - (boss ? 0.04f : 0.18f) * u);
            RobePiece(rig.Hips, waist, yaw, 0.2f * u, legLen, boss ? 1.9f : 1.6f, cloth * 0.96f);
        }

        static Monster CreateRigged(MonsterData d, Vector3 position, EvolutionProfile evo, float hpScale, PlayerCharacter target, Transform parent)
        {
            var root = new GameObject($"{d.displayName}");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var R = root.transform;

            var m = root.AddComponent<Monster>();
            m.Init(d, hpScale, evo, target);

            float height = 1.8f * d.scale * Random.Range(0.95f, 1.05f);
            var rig = ZombieRig.Create(R, height, RigTint(d, evo));
            m.SetRig(rig);
            string kind = d.id != null && d.id.StartsWith("z_") ? d.id.Substring(2) : "";
            // 유형별 체격: 부은·거구·돌덩이는 옆으로 퍼지고, 날랜·그림자·굶주린은 홀쭉하다
            float girth = kind == "tank" ? 1.15f : kind == "butcher" ? 1.1f : kind == "wall" ? 1.12f
                        : kind == "fast" ? 0.88f : kind == "stalker" ? 0.9f : kind == "gnaw" ? 0.92f : 1f;
            if (girth != 1f && rig.Pivot != null) rig.Pivot.localScale = Vector3.Scale(rig.Pivot.localScale, new Vector3(girth, 1f, girth));
            float u = rig.Unit;
            Vector3 up = Vector3.up;
            Transform neck = rig.Neck != null ? rig.Neck : rig.Spine2;
            if (Headwear(d, rig, R)) Robe(d, rig, R);

            // ---- 판정체 (보이지 않음): 머리 · 몸통 · 팔 · 다리 ----
            var headCenter = rig.HeadTop != null ? Vector3.Lerp(rig.Head.position, rig.HeadTop.position, 0.45f) : rig.Head.position + up * 0.1f * u;
            Volume(PrimitiveType.Sphere, m, HitZone.Head, rig.Head, headCenter, Quaternion.identity, Vector3.one * 0.3f * u);
            m.SetHead(rig.Head);
            Between(m, HitZone.Body, rig.Spine != null ? rig.Spine : rig.Hips, rig.Hips.position - up * 0.08f * u, neck.position, 0.42f * u, 0.3f * u, R);
            if (rig.LeftArm != null && rig.LeftHand != null) Between(m, HitZone.Body, rig.LeftArm, rig.LeftArm.position, rig.LeftHand.position, 0.13f * u, 0.13f * u, R);
            if (rig.RightArm != null && rig.RightHand != null) Between(m, HitZone.Body, rig.RightArm, rig.RightArm.position, rig.RightHand.position, 0.13f * u, 0.13f * u, R);
            float legHp = d.maxHp * hpScale * (d.archetype == MonsterArchetype.Boss ? 0.08f : 0.18f) * evo.legHpMult;
            if (rig.LeftUpLeg != null && rig.LeftFoot != null) Between(m, HitZone.Leg, rig.LeftUpLeg, rig.LeftUpLeg.position, rig.LeftFoot.position, 0.17f * u, 0.17f * u, R, legHp);
            if (rig.RightUpLeg != null && rig.RightFoot != null) Between(m, HitZone.Leg, rig.RightUpLeg, rig.RightUpLeg.position, rig.RightFoot.position, 0.17f * u, 0.17f * u, R, legHp);

            // ---- 어둠 속에서 빛나는 눈 ----
            var eyeBase = rig.Head.position + R.forward * 0.11f * u + up * 0.06f * u;
            // 눈빛도 유형마다 다르다 (멀리 어둠 속에서도 무엇이 오는지 알 수 있게)
            Color eye; float eyeSize = 0.035f;
            switch (kind)
            {
                case "frenzy": eye = new Color(1f, 0.05f, 0.02f); eyeSize = 0.05f; break;   // 미친 망자: 크고 새빨간 눈
                case "gnaw": eye = new Color(1f, 0.85f, 0.15f); break;                      // 굶주린: 누런 눈
                case "stalker": eye = new Color(0.75f, 0.9f, 1f); eyeSize = 0.04f; break;   // 그림자: 창백한 푸른 눈
                case "fast": eye = new Color(1f, 0.5f, 0.1f); break;                        // 날랜: 주황
                case "tank": eye = new Color(0.6f, 0.9f, 0.3f); break;                      // 부은: 병든 초록
                case "wall": eye = new Color(0.55f, 0.5f, 0.45f); eyeSize = 0.028f; break;  // 돌덩이: 흐릿
                case "bloat": eye = new Color(0.85f, 1f, 0.2f); break;                       // 터지는: 누런 초록
                default: eye = new Color(1f, 0.15f, 0.08f); break;
            }
            foreach (var sx in new[] { -0.045f, 0.045f })
                Cosmetic(PrimitiveType.Sphere, rig.Head, eyeBase + R.right * sx * u, Quaternion.identity, Vector3.one * eyeSize * u, eye, true);
            KindDressing(kind, rig, R, u);
            EliteDressing(d, rig, R, u);

            var chest = rig.Spine2.position;
            bool heavy = d.archetype == MonsterArchetype.Brute || d.archetype == MonsterArchetype.Boss;

            // ---- 장군 망자 / 역병 대감: 두정갑 앞판 (부수면 가슴 급소가 드러난다) ----
            if (heavy)
            {
                float armorHp = d.maxHp * hpScale * (d.archetype == MonsterArchetype.Boss ? 0.12f : 0.45f);
                var plate = Visible(PrimitiveType.Cube, m, HitZone.Armor, rig.Spine2, chest + R.forward * 0.2f * u - up * 0.05f * u, R.rotation,
                    new Vector3(0.5f, 0.5f, 0.08f) * u, ArmorRed, armorHp);
                for (int i = 0; i < 9; i++)
                {
                    var stud = chest + R.forward * 0.245f * u + R.right * ((i % 3) - 1) * 0.14f * u + up * (0.12f - (i / 3) * 0.13f) * u;
                    Cosmetic(PrimitiveType.Sphere, plate.transform, stud, Quaternion.identity, Vector3.one * 0.035f * u, Brass, false);
                }
            }
            // 장군 망자: 조선 투구 (장식)
            if (d.archetype == MonsterArchetype.Brute)
            {
                var top = rig.HeadTop != null ? rig.HeadTop.position : rig.Head.position + up * 0.2f * u;
                Cosmetic(PrimitiveType.Sphere, rig.Head, top - up * 0.06f * u, Quaternion.identity, new Vector3(0.3f, 0.2f, 0.32f) * u, new Color(0.2f, 0.18f, 0.16f), false);
                Cosmetic(PrimitiveType.Cylinder, rig.Head, top + up * 0.08f * u, Quaternion.identity, new Vector3(0.025f, 0.08f, 0.025f) * u, Brass, false);
                Cosmetic(PrimitiveType.Cylinder, rig.Head, top - up * 0.1f * u, Quaternion.identity, new Vector3(0.38f, 0.015f, 0.38f) * u, ArmorRed, false);
            }
            // 역병 대감: 갓
            if (d.archetype == MonsterArchetype.Boss)
            {
                var top = rig.HeadTop != null ? rig.HeadTop.position : rig.Head.position + up * 0.2f * u;
                Cosmetic(PrimitiveType.Cylinder, rig.Head, top - up * 0.02f * u, Quaternion.identity, new Vector3(0.75f, 0.006f, 0.75f) * u, GatBlack, false);
                Cosmetic(PrimitiveType.Cylinder, rig.Head, top + up * 0.08f * u, Quaternion.identity, new Vector3(0.26f, 0.1f, 0.26f) * u, GatBlack, false);
            }
            // 토혈귀: 부풀어 오른 배
            if (d.archetype == MonsterArchetype.Spitter && rig.Spine != null)
                Cosmetic(PrimitiveType.Sphere, rig.Spine, rig.Spine.position + R.forward * 0.12f * u, Quaternion.identity, new Vector3(0.42f, 0.4f, 0.36f) * u, new Color(0.35f, 0.42f, 0.25f), false);

            // ---- 투구 (진화: 헤드샷 대응) ----
            if (evo.helmetLevel > 0)
            {
                float hs = 0.36f * u * (1f + 0.05f * evo.helmetLevel);
                Visible(PrimitiveType.Sphere, m, HitZone.Armor, rig.Head, headCenter + up * 0.02f * u, R.rotation, new Vector3(hs, hs * 0.9f, hs * 1.05f), BoneColor,
                    d.maxHp * hpScale * 0.12f * evo.helmetLevel);
            }

            // ---- 급소 (빛남) ----
            if (d.archetype == MonsterArchetype.Boss)
            {
                m.SetBossWeakPoints(new List<MonsterPart>
                {
                    RigWeakPoint(m, rig, R, WeakPointLocation.Back, 0.2f * u),
                    RigWeakPoint(m, rig, R, WeakPointLocation.Shoulder, 0.2f * u),
                    RigWeakPoint(m, rig, R, WeakPointLocation.Front, 0.2f * u),
                });
            }
            else
            {
                RigWeakPoint(m, rig, R, evo.weakPoint, (d.archetype == MonsterArchetype.Runner ? 0.15f : 0.19f) * u);
            }
            return m;
        }

        /// <summary>
        /// 엘리트 겉모습 (판정 없음).
        /// 장군 망자: 두정갑 어깨받이·갑옷 치마·붉은 상모 술·오른손 환도.  토혈귀: 부푼 목, 입에서 흘러내리는 초록 진액, 등의 고름 혹.
        /// </summary>
        static void EliteDressing(MonsterData d, ZombieRig rig, Transform R, float u)
        {
            Vector3 up = Vector3.up;
            var chest = rig.Spine2 != null ? rig.Spine2.position : rig.Hips.position + up * 0.4f * u;
            if (d.archetype == MonsterArchetype.Brute)
            {
                // 어깨받이 (붉은 두정갑 + 놋쇠 징)
                foreach (var sx in new[] { -1f, 1f })
                {
                    var arm = sx < 0 ? rig.LeftArm : rig.RightArm;
                    var at = arm != null ? arm.position : chest + R.right * sx * 0.2f * u;
                    var parent = arm != null ? arm : rig.Spine2;
                    Cosmetic(PrimitiveType.Sphere, parent, at + up * 0.04f * u, R.rotation, new Vector3(0.2f, 0.09f, 0.2f) * u, ArmorRed, false);
                    Cosmetic(PrimitiveType.Sphere, parent, at + up * 0.09f * u, Quaternion.identity, Vector3.one * 0.03f * u, Brass, false);
                }
                // 갑옷 치마: 허리 둘레 판 여섯 장 (조금씩 벌어져 늘어짐)
                var hips = rig.Hips.position;
                for (int i = 0; i < 6; i++)
                {
                    float ang = (i / 6f) * 360f + 30f;
                    var rot = R.rotation * Quaternion.Euler(0f, ang, 0f);
                    var pos = hips + rot * Vector3.forward * 0.17f * u - up * 0.14f * u;
                    Cosmetic(PrimitiveType.Cube, rig.Hips, pos, rot * Quaternion.Euler(12f, 0f, 0f), new Vector3(0.15f, 0.3f, 0.025f) * u, i % 2 == 0 ? ArmorRed : ArmorRed * 0.8f, false);
                }
                // 투구 꼭대기 붉은 상모 술
                if (rig.HeadTop != null)
                    Cosmetic(PrimitiveType.Sphere, rig.Head, rig.HeadTop.position + up * 0.17f * u, Quaternion.identity, new Vector3(0.1f, 0.14f, 0.1f) * u, new Color(0.55f, 0.04f, 0.03f), false);
                // 오른손 환도 (녹슨 칼날 + 검은 자루)
                if (rig.RightHand != null)
                {
                    var hand = rig.RightHand.position;
                    Cosmetic(PrimitiveType.Cube, rig.RightHand, hand - up * 0.45f * u + R.forward * 0.05f * u, R.rotation * Quaternion.Euler(8f, 0f, 0f),
                        new Vector3(0.015f, 0.75f, 0.05f) * u, new Color(0.42f, 0.4f, 0.38f), false);
                    Cosmetic(PrimitiveType.Cylinder, rig.RightHand, hand, R.rotation, new Vector3(0.035f, 0.08f, 0.035f) * u, GatBlack, false);
                    Cosmetic(PrimitiveType.Cube, rig.RightHand, hand - up * 0.08f * u, R.rotation, new Vector3(0.1f, 0.012f, 0.08f) * u, Brass, false); // 코등이
                }
            }
            else if (d.archetype == MonsterArchetype.Spitter && rig.Head != null)
            {
                var neck = rig.Neck != null ? rig.Neck.position : rig.Head.position - up * 0.1f * u;
                // 부풀어 오른 목 (독주머니, 희미하게 빛남)
                Cosmetic(PrimitiveType.Sphere, rig.Neck != null ? rig.Neck : rig.Head, neck + R.forward * 0.05f * u, Quaternion.identity, new Vector3(0.2f, 0.16f, 0.18f) * u, new Color(0.45f, 0.75f, 0.15f), true);
                // 입에서 흘러내리는 진액 세 줄기
                var mouth = rig.Head.position + R.forward * 0.12f * u - up * 0.06f * u;
                for (int k = 0; k < 3; k++)
                    Cosmetic(PrimitiveType.Cylinder, rig.Head, mouth + R.right * (k - 1) * 0.025f * u - up * (0.05f + k * 0.02f) * u, Quaternion.identity,
                        new Vector3(0.012f, 0.05f + k * 0.025f, 0.012f) * u, new Color(0.4f, 0.65f, 0.1f), true);
                // 등의 고름 혹
                Cosmetic(PrimitiveType.Sphere, rig.Spine2, chest - R.forward * 0.14f * u + up * 0.05f * u, Quaternion.identity, new Vector3(0.16f, 0.14f, 0.12f) * u, new Color(0.5f, 0.55f, 0.25f), false);
                Cosmetic(PrimitiveType.Sphere, rig.Spine2, chest - R.forward * 0.12f * u + R.right * 0.1f * u - up * 0.08f * u, Quaternion.identity, new Vector3(0.1f, 0.09f, 0.08f) * u, new Color(0.55f, 0.5f, 0.22f), false);
            }
        }

        /// <summary>망자 유형별 덧붙임 (판정 없음, 겉모습만).</summary>
        static void KindDressing(string kind, ZombieRig rig, Transform R, float u)
        {
            Vector3 up = Vector3.up;
            var chest = rig.Spine2 != null ? rig.Spine2.position : rig.Hips.position + up * 0.4f * u;
            switch (kind)
            {
                case "tank":
                    // 부풀어 오른 배와 목덜미 혹
                    if (rig.Spine != null)
                        Cosmetic(PrimitiveType.Sphere, rig.Spine, rig.Spine.position + R.forward * 0.13f * u, Quaternion.identity, new Vector3(0.48f, 0.44f, 0.4f) * u, new Color(0.45f, 0.42f, 0.33f), false);
                    Cosmetic(PrimitiveType.Sphere, rig.Spine2, chest - R.forward * 0.1f * u + up * 0.12f * u, Quaternion.identity, new Vector3(0.3f, 0.22f, 0.26f) * u, new Color(0.42f, 0.38f, 0.3f), false);
                    break;
                case "wall":
                    // 어깨·등을 덮은 돌 같은 굳은살 판
                    foreach (var sx in new[] { -1f, 1f })
                        Cosmetic(PrimitiveType.Cube, rig.Spine2, chest + R.right * sx * 0.2f * u + up * 0.12f * u, R.rotation * Quaternion.Euler(0f, 0f, sx * 25f),
                            new Vector3(0.2f, 0.09f, 0.26f) * u, new Color(0.36f, 0.36f, 0.37f), false);
                    Cosmetic(PrimitiveType.Cube, rig.Spine2, chest - R.forward * 0.15f * u, R.rotation, new Vector3(0.4f, 0.42f, 0.08f) * u, new Color(0.33f, 0.33f, 0.34f), false);
                    break;
                case "butcher":
                    // 피 묻은 가죽 앞치마 + 오른손 식칼
                    Cosmetic(PrimitiveType.Cube, rig.Spine != null ? rig.Spine : rig.Hips, rig.Hips.position + R.forward * 0.16f * u + up * 0.05f * u, R.rotation,
                        new Vector3(0.42f, 0.62f, 0.03f) * u, new Color(0.28f, 0.13f, 0.1f), false);
                    if (rig.RightHand != null)
                    {
                        Cosmetic(PrimitiveType.Cube, rig.RightHand, rig.RightHand.position - up * 0.14f * u, R.rotation, new Vector3(0.02f, 0.24f, 0.12f) * u, new Color(0.5f, 0.5f, 0.52f), false);
                        Cosmetic(PrimitiveType.Cylinder, rig.RightHand, rig.RightHand.position, R.rotation, new Vector3(0.035f, 0.06f, 0.035f) * u, new Color(0.25f, 0.18f, 0.12f), false);
                    }
                    break;
                case "frenzy":
                    // 가슴과 입가의 핏자국
                    Cosmetic(PrimitiveType.Sphere, rig.Spine2, chest + R.forward * 0.15f * u - up * 0.05f * u, Quaternion.identity, new Vector3(0.22f, 0.3f, 0.06f) * u, new Color(0.35f, 0.02f, 0.02f), false);
                    Cosmetic(PrimitiveType.Sphere, rig.Head, rig.Head.position + R.forward * 0.12f * u - up * 0.04f * u, Quaternion.identity, new Vector3(0.1f, 0.06f, 0.04f) * u, new Color(0.4f, 0.02f, 0.02f), false);
                    break;
                case "bloat":
                    // 터질 듯 부푼 누런 배 + 군데군데 고름 혹 (빛나서 멀리서도 '저건 터진다'를 알게)
                    if (rig.Spine != null)
                    {
                        Cosmetic(PrimitiveType.Sphere, rig.Spine, rig.Spine.position + R.forward * 0.16f * u, Quaternion.identity, new Vector3(0.56f, 0.5f, 0.5f) * u, new Color(0.55f, 0.58f, 0.28f), false);
                        Cosmetic(PrimitiveType.Sphere, rig.Spine, rig.Spine.position + R.forward * 0.38f * u + R.right * 0.1f * u + up * 0.06f * u, Quaternion.identity, new Vector3(0.1f, 0.1f, 0.1f) * u, new Color(0.75f, 0.85f, 0.25f), true);
                        Cosmetic(PrimitiveType.Sphere, rig.Spine, rig.Spine.position + R.forward * 0.33f * u - R.right * 0.14f * u - up * 0.08f * u, Quaternion.identity, new Vector3(0.08f, 0.08f, 0.08f) * u, new Color(0.75f, 0.85f, 0.25f), true);
                    }
                    break;
                case "stalker":
                    // 앞으로 쏠린 해진 천 조각 (그림자처럼 어둡게)
                    Cosmetic(PrimitiveType.Cube, rig.Spine2, chest - R.forward * 0.12f * u - up * 0.15f * u, R.rotation * Quaternion.Euler(-15f, 0f, 0f),
                        new Vector3(0.38f, 0.7f, 0.02f) * u, new Color(0.06f, 0.06f, 0.07f), false);
                    break;
            }
        }

        static MonsterPart RigWeakPoint(Monster m, ZombieRig rig, Transform R, WeakPointLocation loc, float size)
        {
            var c = rig.Spine2.position;
            Vector3 p;
            switch (loc)
            {
                case WeakPointLocation.Back: p = c - R.forward * 0.15f * rig.Unit; break;
                case WeakPointLocation.Shoulder: p = c + R.right * 0.2f * rig.Unit + Vector3.up * 0.1f * rig.Unit; break;
                default: p = c + R.forward * 0.13f * rig.Unit - Vector3.up * 0.04f * rig.Unit; break;
            }
            return Visible(PrimitiveType.Sphere, m, HitZone.WeakPoint, rig.Spine2, p, Quaternion.identity, Vector3.one * size, WeakPointColor);
        }

        /// <summary>보이지 않는 판정체를 월드 좌표로 만든 뒤 뼈에 붙인다 (뼈를 따라 움직인다).</summary>
        static MonsterPart Volume(PrimitiveType type, Monster m, HitZone zone, Transform bone, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale, float hp = -1f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = zone + "_Hit";
            // 평소엔 안 보이고, F9(판정 보기)일 때만 반투명 빨강으로 보인다
            var dr = go.GetComponent<MeshRenderer>();
            dr.sharedMaterial = Hunt.Core.FX.UnlitMaterial(new Color(1f, 0.1f, 0.1f, 0.35f));
            dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dr.enabled = false;
            go.AddComponent<HitboxDebug>();
            go.transform.position = worldPos;
            go.transform.rotation = worldRot;
            go.transform.localScale = worldScale;
            go.transform.SetParent(bone, true);
            var part = go.AddComponent<MonsterPart>();
            part.Setup(m, zone, hp);
            return part;
        }

        static MonsterPart Visible(PrimitiveType type, Monster m, HitZone zone, Transform bone, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale, Color c, float hp = -1f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = zone.ToString();
            go.transform.position = worldPos;
            go.transform.rotation = worldRot;
            go.transform.localScale = worldScale;
            go.GetComponent<Renderer>().material.color = c;
            go.transform.SetParent(bone, true);
            var part = go.AddComponent<MonsterPart>();
            part.Setup(m, zone, hp);
            return part;
        }

        /// <summary>a→b 사이를 감싸는 상자 판정체 (팔·다리·몸통).</summary>
        static MonsterPart Between(Monster m, HitZone zone, Transform bone, Vector3 a, Vector3 b, float width, float depth, Transform R, float hp = -1f)
        {
            Vector3 axis = b - a;
            float len = axis.magnitude;
            if (len < 0.001f) { axis = Vector3.up; len = 0.1f; }
            Vector3 fwd = Vector3.ProjectOnPlane(R.forward, axis.normalized);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.ProjectOnPlane(R.right, axis.normalized);
            var rot = Quaternion.LookRotation(fwd.normalized, axis.normalized);
            return Volume(PrimitiveType.Cube, m, zone, bone, (a + b) * 0.5f, rot, new Vector3(width, len, depth), hp);
        }

        static void Cosmetic(PrimitiveType type, Transform parent, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale, Color c, bool glow)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = worldPos;
            go.transform.rotation = worldRot;
            go.transform.localScale = worldScale;
            var r = go.GetComponent<Renderer>();
            if (glow) r.sharedMaterial = Hunt.Core.FX.UnlitMaterial(c);
            else r.sharedMaterial = Hunt.Core.EnvMaterials.Plain(c, 0.45f);
            // 작은 장식(눈·징·진액·술 등)은 그림자를 만들지 않는다 — 망자 수십 마리 × 장식 십여 개의 그림자 계산을 줄임
            if (glow || worldScale.magnitude < 0.25f) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            go.transform.SetParent(parent, true);
        }

        static MonsterPart WeakPoint(Transform root, Monster m, WeakPointLocation loc, float size)
        {
            Vector3 p;
            switch (loc)
            {
                case WeakPointLocation.Back: p = new Vector3(0, 1.05f, -0.36f); break;
                case WeakPointLocation.Shoulder: p = new Vector3(0.3f, 1.4f, 0f); break;
                default: p = new Vector3(0, 1.0f, 0.34f); break;
            }
            return Part(PrimitiveType.Sphere, root, p, Vector3.one * size, WeakPointColor, m, HitZone.WeakPoint);
        }

        static MonsterPart Part(PrimitiveType type, Transform parent, Vector3 lpos, Vector3 lscale, Color c, Monster m, HitZone zone, float hp = -1f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = zone.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lpos;
            go.transform.localScale = lscale;
            go.GetComponent<Renderer>().material.color = c;
            var part = go.AddComponent<MonsterPart>();
            part.Setup(m, zone, hp);
            return part;
        }

        static void Deco(PrimitiveType type, Transform parent, Vector3 lpos, Vector3 lscale, Color c, bool glow = false)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lpos;
            go.transform.localScale = lscale;
            if (glow) go.GetComponent<Renderer>().sharedMaterial = Hunt.Core.FX.UnlitMaterial(c);
            else go.GetComponent<Renderer>().material.color = c;
        }
    }
}

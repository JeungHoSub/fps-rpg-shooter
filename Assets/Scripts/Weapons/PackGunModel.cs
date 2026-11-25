using System.Collections.Generic;
using System.Globalization;
using Hunt.Combat;
using UnityEngine;

namespace Hunt.Weapons
{
    /// <summary>
    /// 무기별 총 모델 (Resources/Guns/&lt;id&gt;.txt).
    /// - Quaternius Ultimate Gun Pack (CC0) · M4A1 (CC0, 실사 텍스처) · 화염방사기 (CC0, 실사 텍스처)
    /// 원본 OBJ/FBX를 미리 '총 공간'(+Z 총구, +Y 위, 원점 = 손잡이 위 기관부, m 단위)으로 돌려 둔 텍스트라,
    /// 유니티 가져오기 설정(축·크기)에 상관없이 항상 같은 방향으로 나온다.
    ///
    /// 형식: "gun 재질수 조준선높이 총구Z 총구Y 최저Y" / "m 이름 r g b 종류 금속 광택" / "p 부위" / "s 재질번호 삼각형수" / 정점 줄(위치·노멀·UV)
    /// </summary>
    public static class PackGunModel
    {
        // 같은 모델을 쓰는 무기
        static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            { "flamer2", "flamer" }, { "napalm", "flamer" },
        };

        static readonly Dictionary<string, TextAsset> found = new Dictionary<string, TextAsset>();
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        public static string ModelId(string weaponId) => Alias.TryGetValue(weaponId, out var a) ? a : weaponId;

        static TextAsset Asset(string weaponId)
        {
            string id = ModelId(weaponId);
            if (!found.TryGetValue(id, out var t))
            {
                t = Resources.Load<TextAsset>("Guns/" + id);
                found[id] = t;
            }
            return t;
        }

        // ---- 이진 모델 (Resources/GunsBin/<id>.bytes): 같은 점을 합쳐 정점이 절반 이하, 읽기도 훨씬 빠르다 ----
        // 한 번 만든 메시는 기억해 두고 다시 쓴다 (무기 교체·다시 시작 때 끊김 없음)
        class Cached
        {
            public float sight, muzzleZ, muzzleY;
            public readonly List<(string part, Mesh mesh, Material[] mats, Vector3 pivot)> parts = new List<(string, Mesh, Material[], Vector3)>();
        }
        static readonly Dictionary<string, Cached> cache = new Dictionary<string, Cached>();
        static readonly Dictionary<string, TextAsset> foundBin = new Dictionary<string, TextAsset>();

        static TextAsset BinAsset(string weaponId)
        {
            string id = ModelId(weaponId);
            if (!foundBin.TryGetValue(id, out var t)) { t = Resources.Load<TextAsset>("GunsBin/" + id); foundBin[id] = t; }
            return t;
        }

        public static bool Has(string weaponId) => BinAsset(weaponId) != null || Asset(weaponId) != null;

        static Cached LoadBinary(string id, byte[] bytes)
        {
            var c = new Cached();
            using (var br = new System.IO.BinaryReader(new System.IO.MemoryStream(bytes)))
            {
                if (new string(br.ReadChars(4)) != "HGN1") throw new System.Exception("형식이 다름");
                int nm = br.ReadInt32();
                c.sight = Mathf.Max(0.02f, br.ReadSingle()); c.muzzleZ = br.ReadSingle(); c.muzzleY = br.ReadSingle(); br.ReadSingle();
                var materials = new Material[nm];
                for (int i = 0; i < nm; i++)
                {
                    string name = System.Text.Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
                    var col = new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    byte kind = br.ReadByte();
                    float metal = br.ReadSingle(), gloss = br.ReadSingle();
                    materials[i] = GetMaterial(id, name, col, kind == 2 ? "tex" : kind == 1 ? "wood" : "metal", metal, gloss);
                }
                int partCount = br.ReadInt32();
                for (int pi = 0; pi < partCount; pi++)
                {
                    string part = System.Text.Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
                    int vc = br.ReadInt32();
                    var verts = new Vector3[vc]; var norms = new Vector3[vc]; var uvs = new Vector2[vc];
                    for (int i = 0; i < vc; i++)
                    {
                        verts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        norms[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        uvs[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                    }
                    int subCount = br.ReadInt32();
                    var subs = new int[subCount][];
                    var subMats = new Material[subCount];
                    for (int si = 0; si < subCount; si++)
                    {
                        subMats[si] = materials[br.ReadInt32()];
                        int n = br.ReadInt32();
                        var idx = new int[n];
                        for (int k = 0; k < n; k++) idx[k] = br.ReadInt32();
                        subs[si] = idx;
                    }
                    // 탄창·작동부는 자기 중심을 기준점으로
                    Vector3 pivot = Vector3.zero;
                    if (part != "body" && vc > 0)
                    {
                        var b = new Bounds(verts[0], Vector3.zero);
                        for (int i = 1; i < vc; i++) b.Encapsulate(verts[i]);
                        pivot = b.center;
                        for (int i = 0; i < vc; i++) verts[i] -= pivot;
                    }
                    var mesh = new Mesh { name = id + "_" + part };
                    if (vc > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = verts; mesh.normals = norms; mesh.uv = uvs;
                    mesh.subMeshCount = subCount;
                    for (int si = 0; si < subCount; si++) mesh.SetTriangles(subs[si], si);
                    mesh.RecalculateBounds();
                    if (subMats.Length > 0 && subMats[0].GetTexture("_BumpMap") != null) mesh.RecalculateTangents();
                    mesh.UploadMeshData(true); // CPU 사본 버림 (메모리 절약)
                    c.parts.Add((part, mesh, subMats, pivot));
                }
            }
            return c;
        }

        static void Instantiate(Cached c, Transform root, GunModel m)
        {
            m.sightHeight = c.sight;
            foreach (var (part, mesh, pmats, pivot) in c.parts)
            {
                var go = new GameObject(part);
                go.transform.SetParent(root, false);
                go.transform.localPosition = pivot;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = pmats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (part == "mag") { m.magazine = go.transform; m.magazineRest = pivot; }
                else if (part == "act") { m.action = go.transform; m.actionRest = pivot; }
            }
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0f, c.muzzleY, c.muzzleZ);
            m.muzzle = muzzle;
        }

        public static bool Build(Transform root, Hunt.Data.WeaponData d, GunModel m)
        {
            string id = ModelId(d.id);
            // 1) 이진 모델 (기억해 둔 메시 재사용)
            if (!cache.TryGetValue(id, out var cached) || cached == null)
            {
                var bin = BinAsset(d.id);
                if (bin != null)
                {
                    try { cached = LoadBinary(id, bin.bytes); cache[id] = cached; }
                    catch (System.Exception e) { Debug.LogWarning($"총 모델 {id} 이진 읽기 실패: {e.Message}"); cached = null; }
                }
            }
            if (cached != null)
            {
                Instantiate(cached, root, m);
                Presets(d.weaponClass, m); Tweak(id, m);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = GunModelBuilder.ViewLayer;
                return true;
            }

            // 2) 예전 텍스트 모델 (이진 파일이 없을 때)
            var asset = Asset(d.id);
            if (asset == null) return false;
            try
            {
                Parse(asset.text, id, root, d, m);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"총 모델 {id} 읽기 실패: {e.Message}");
                for (int i = root.childCount - 1; i >= 0; i--) Object.Destroy(root.GetChild(i).gameObject);
                return false;
            }
            Presets(d.weaponClass, m); Tweak(id, m);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = GunModelBuilder.ViewLayer;
            return true;
        }

        static void Parse(string text, string id, Transform root, Hunt.Data.WeaponData d, GunModel m)
        {
            var ci = CultureInfo.InvariantCulture;
            var lines = text.Split('\n');
            int li = 0;
            string[] head = lines[li++].Trim().Split(' ');
            int matCount = int.Parse(head[1], ci);
            m.sightHeight = Mathf.Max(0.02f, float.Parse(head[2], ci));
            float muzzleZ = float.Parse(head[3], ci), muzzleY = float.Parse(head[4], ci);

            var materials = new Material[matCount];
            for (int i = 0; i < matCount; i++)
            {
                var p = lines[li++].Trim().Split(' ');
                var col = new Color(float.Parse(p[2], ci), float.Parse(p[3], ci), float.Parse(p[4], ci));
                materials[i] = GetMaterial(id, p[1], col, p[5], float.Parse(p[6], ci), float.Parse(p[7], ci));
            }

            // 부위별 메시 (본체 / 탄창 / 작동부)
            while (li < lines.Length)
            {
                string l = lines[li].Trim();
                if (l.Length == 0) { li++; continue; }
                if (!l.StartsWith("p ")) { li++; continue; }
                string part = l.Substring(2);
                li++;
                var verts = new List<Vector3>();
                var norms = new List<Vector3>();
                var uvs = new List<Vector2>();
                var subTris = new List<List<int>>();
                var subMats = new List<Material>();
                while (li < lines.Length && lines[li].StartsWith("s "))
                {
                    var sp = lines[li].Trim().Split(' ');
                    int mi = int.Parse(sp[1], ci), triCount = int.Parse(sp[2], ci);
                    li++;
                    var tris = new List<int>(triCount * 3);
                    for (int k = 0; k < triCount * 3; k++)
                    {
                        var v = lines[li++].Split(' ');
                        tris.Add(verts.Count);
                        verts.Add(new Vector3(float.Parse(v[0], ci), float.Parse(v[1], ci), float.Parse(v[2], ci)));
                        norms.Add(new Vector3(float.Parse(v[3], ci), float.Parse(v[4], ci), float.Parse(v[5], ci)));
                        uvs.Add(new Vector2(float.Parse(v[6], ci), float.Parse(v[7], ci)));
                    }
                    subTris.Add(tris);
                    subMats.Add(materials[mi]);
                }
                if (verts.Count == 0) continue;

                var mesh = new Mesh { name = id + "_" + part };
                if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = subTris.Count;
                for (int s = 0; s < subTris.Count; s++) mesh.SetTriangles(subTris[s], s);
                mesh.RecalculateBounds();
                if (subMats.Count > 0 && subMats[0].GetTexture("_BumpMap") != null) mesh.RecalculateTangents();

                // 탄창·작동부는 자기 중심을 기준점으로 (빠지고 당기는 애니메이션용)
                Vector3 pivot = part == "body" ? Vector3.zero : mesh.bounds.center;
                if (pivot != Vector3.zero)
                {
                    for (int i = 0; i < verts.Count; i++) verts[i] -= pivot;
                    mesh.SetVertices(verts);
                    mesh.RecalculateBounds();
                }
                var go = new GameObject(part);
                go.transform.SetParent(root, false);
                go.transform.localPosition = pivot;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = subMats.ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;

                if (part == "mag") { m.magazine = go.transform; m.magazineRest = go.transform.localPosition; }
                else if (part == "act") { m.action = go.transform; m.actionRest = go.transform.localPosition; }
            }

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0f, muzzleY, muzzleZ);
            m.muzzle = muzzle;
        }

        static Shader gunShader;
        static Material GetMaterial(string id, string name, Color col, string kind, float metal, float gloss)
        {
            bool fancy = Hunt.Core.GameSettings.Grade; // '화면 색감'을 끄면 총 결 셰이더도 끈다 (혹시 분홍색 등 문제가 나면 끌 수 있게)
            string key = (kind == "tex" ? id : kind + "|" + ColorUtility.ToHtmlStringRGB(col)) + (fancy ? "|g" : "");
            if (mats.TryGetValue(key, out var mat) && mat != null) return mat;
            mat = new Material(Shader.Find("Standard")) { name = "Gun_" + (kind == "tex" ? id : name) };
            if (kind == "tex")
            {
                var diff = Resources.Load<Texture2D>("Guns/" + id + "_diff");
                var nor = Resources.Load<Texture2D>("Guns/" + id + "_nor");
                // 텍스처 총도 검게: 회색 기 빼고 진하게 (총마다 텍스처 밝기가 달라 따로 맞춤)
                float tint = id == "ar" ? 0.9f : 1f; // 회색 총철 그대로, M4만 아주 살짝 진하게
                mat.color = new Color(tint, tint, tint * 1.02f);
                if (diff != null) mat.mainTexture = diff;
                if (nor != null) { mat.SetTexture("_BumpMap", nor); mat.EnableKeyword("_NORMALMAP"); }
            }
            else if (kind == "wood")
            {
                // 단색 나무 → 나뭇결 텍스처를 옅게 입혀 실감 있게
                mat.color = col * 1.15f;
                var wood = Resources.Load<Texture2D>("Props/painted_wooden_bench_diff");
                if (wood != null) { mat.mainTexture = wood; mat.mainTextureScale = new Vector2(2f, 0.5f); mat.color = col * 1.4f + new Color(0.04f, 0.015f, 0f); } // 따뜻한 호두나무색 개머리
            }
            else
            {
                // 금속: 회색 총철 (사용자: "회색이 나아 보인다, 더 예쁘게"). 부품끼리 밝기 차이를 넓혀 입체감,
                // 살짝 푸른 기가 도는 총철색 + 금속광으로 밋밋한 회색 덩어리가 아니게
                float l = (col.r + col.g + col.b) / 3f;
                bool glass = name.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0;
                float v = Mathf.Lerp(0.11f, 0.38f, Mathf.InverseLerp(0.12f, 0.34f, l));
                Color gun = new Color(v * 0.96f, v, v * 1.07f);
                mat.color = glass ? new Color(0.03f, 0.05f, 0.08f) : Color.Lerp(gun, col * (v / Mathf.Max(0.01f, l)), 0.25f);
                metal = glass ? 0.9f : Mathf.Max(metal, 0.6f);
                gloss = glass ? 0.9f : Mathf.Max(gloss, 0.6f);
            }
            // UV 없는 팩 모델: 물체 공간 결 셰이더로 바꿔 총철 입자·나뭇결·달빛 테두리 (없거나 안 되면 Standard 그대로)
            if (kind != "tex" && !(name.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (gunShader == null) gunShader = Resources.Load<Shader>("Shaders/GunMetal");
                if (fancy && gunShader != null && gunShader.isSupported)
                {
                    var c0 = kind == "wood" ? col * 1.45f + new Color(0.05f, 0.02f, 0f) : mat.color;
                    mat.shader = gunShader;
                    mat.mainTexture = null;
                    mat.color = c0;
                    mat.SetFloat("_Wood", kind == "wood" ? 1f : 0f);
                    mat.SetFloat("_Grain", 0.6f);
                }
            }
            mat.SetFloat("_Metallic", metal);
            mat.SetFloat("_Glossiness", gloss);
            mats[key] = mat;
            return mat;
        }

        /// <summary>계열별 손 위치·조준 거리·반동 세기 (도형 총과 같은 값).</summary>
        // 덩치 큰 모델 (긴 탄창·높은 가늠자) → 화면을 덜 가리게 더 멀리·낮게·오른쪽으로
        static void Tweak(string id, GunModel m)
        {
            switch (id)
            {
                case "br762": m.hipOffset = new Vector3(0.155f, -0.175f, 0.3f); m.adsZ = 0.24f; break;
                case "hmg": m.hipOffset = new Vector3(0.15f, -0.18f, 0.28f); break;
                case "antimat": m.hipOffset = new Vector3(0.15f, -0.16f, 0.28f); break;
                case "flamer": m.hipOffset = new Vector3(0.15f, -0.17f, 0.26f); break;
            }
        }

        static void Presets(WeaponClass c, GunModel m)
        {
            switch (c)
            {
                case WeaponClass.Pistol:
                    m.hipOffset = new Vector3(0.11f, -0.11f, 0.28f); m.adsZ = 0.3f; m.kickStrength = 0.6f; break;
                case WeaponClass.SMG:
                    m.hipOffset = new Vector3(0.12f, -0.12f, 0.24f); m.adsZ = 0.2f; m.kickStrength = 0.35f; break;
                case WeaponClass.Shotgun:
                    m.hipOffset = new Vector3(0.12f, -0.12f, 0.22f); m.adsZ = 0.2f; m.kickStrength = 1.4f; break;
                case WeaponClass.Sniper:
                    m.hipOffset = new Vector3(0.12f, -0.13f, 0.22f); m.adsZ = 0.1f; m.kickStrength = 1.6f; m.hideWhenScoped = true; break;
                case WeaponClass.LMG:
                    m.hipOffset = new Vector3(0.13f, -0.15f, 0.22f); m.adsZ = 0.18f; m.kickStrength = 0.45f; break;
                case WeaponClass.Launcher:
                    m.hipOffset = new Vector3(0.13f, -0.14f, 0.2f); m.adsZ = 0.16f; m.kickStrength = 1.3f; break;
                case WeaponClass.Flamethrower:
                    m.hipOffset = new Vector3(0.14f, -0.16f, 0.2f); m.adsZ = 0.2f; m.kickStrength = 0.1f; break;
                default:
                    m.hipOffset = new Vector3(0.12f, -0.13f, 0.22f); m.adsZ = 0.18f; m.kickStrength = 0.5f; break;
            }
        }
    }
}

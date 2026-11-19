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

        static string ModelId(string weaponId) => Alias.TryGetValue(weaponId, out var a) ? a : weaponId;

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

        public static bool Has(string weaponId) => Asset(weaponId) != null;

        public static bool Build(Transform root, Hunt.Data.WeaponData d, GunModel m)
        {
            var asset = Asset(d.id);
            if (asset == null) return false;
            string id = ModelId(d.id);
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
            Presets(d.weaponClass, m);
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

        static Material GetMaterial(string id, string name, Color col, string kind, float metal, float gloss)
        {
            string key = kind == "tex" ? id : kind + "|" + ColorUtility.ToHtmlStringRGB(col);
            if (mats.TryGetValue(key, out var mat) && mat != null) return mat;
            mat = new Material(Shader.Find("Standard")) { name = "Gun_" + (kind == "tex" ? id : name) };
            if (kind == "tex")
            {
                var diff = Resources.Load<Texture2D>("Guns/" + id + "_diff");
                var nor = Resources.Load<Texture2D>("Guns/" + id + "_nor");
                mat.color = Color.white;
                if (diff != null) mat.mainTexture = diff;
                if (nor != null) { mat.SetTexture("_BumpMap", nor); mat.EnableKeyword("_NORMALMAP"); }
            }
            else if (kind == "wood")
            {
                // 단색 나무 → 나뭇결 텍스처를 옅게 입혀 실감 있게
                mat.color = col * 1.15f;
                var wood = Resources.Load<Texture2D>("Props/painted_wooden_bench_diff");
                if (wood != null) { mat.mainTexture = wood; mat.mainTextureScale = new Vector2(2f, 0.5f); mat.color = Color.Lerp(col, Color.white, 0.15f) * 1.6f; }
            }
            else
            {
                // 금속: 너무 새까맣지 않게 살짝 밝히고, 금속광
                mat.color = Color.Lerp(col, new Color(0.22f, 0.22f, 0.24f), 0.35f);
            }
            mat.SetFloat("_Metallic", metal);
            mat.SetFloat("_Glossiness", gloss);
            mats[key] = mat;
            return mat;
        }

        /// <summary>계열별 손 위치·조준 거리·반동 세기 (도형 총과 같은 값).</summary>
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

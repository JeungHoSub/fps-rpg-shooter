using System.Collections.Generic;
using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 마을 재질. Assets/Resources/Env 의 Poly Haven(CC0) 텍스처로 Standard 머티리얼을 만든다.
    /// 비에 젖은 느낌을 위해 광택(smoothness)을 높게 준다. 텍스처가 없으면 단색으로 대체.
    /// </summary>
    public static class EnvMaterials
    {
        public enum Kind
        {
            Mud, Road, FarmSoil, Grass, RoofTiles, Thatch, MetalSheet, Plaster, ClayWall,
            WoodPlanks, StoneWall, Concrete, Bark, Rock,
        }

        struct Spec
        {
            public string id;
            public Color tint;
            public float gloss;
            public float tileSize; // 텍스처 한 장이 덮는 미터
            public Spec(string id, Color tint, float gloss, float tileSize) { this.id = id; this.tint = tint; this.gloss = gloss; this.tileSize = tileSize; }
        }

        static readonly Dictionary<Kind, Spec> specs = new Dictionary<Kind, Spec>
        {
            { Kind.Mud, new Spec("brown_mud_02", new Color(0.55f, 0.5f, 0.45f), 0.62f, 4f) },
            { Kind.Road, new Spec("road_damaged", new Color(0.6f, 0.6f, 0.62f), 0.72f, 6f) },
            { Kind.FarmSoil, new Spec("farm_soil", new Color(0.45f, 0.4f, 0.35f), 0.55f, 4f) },
            { Kind.Grass, new Spec("withered_grass", new Color(0.55f, 0.58f, 0.45f), 0.35f, 3f) },
            { Kind.RoofTiles, new Spec("grey_roof_tiles", new Color(0.5f, 0.52f, 0.55f), 0.55f, 3f) },
            { Kind.Thatch, new Spec("reed_roof_03", new Color(0.55f, 0.5f, 0.4f), 0.25f, 3f) },
            { Kind.MetalSheet, new Spec("corrugated_iron_02", new Color(0.35f, 0.5f, 0.7f), 0.65f, 2.5f) },
            { Kind.Plaster, new Spec("grey_plaster", new Color(0.78f, 0.76f, 0.72f), 0.3f, 3f) },
            { Kind.ClayWall, new Spec("clay_plaster", new Color(0.65f, 0.55f, 0.45f), 0.25f, 3f) },
            { Kind.WoodPlanks, new Spec("dark_wooden_planks", new Color(0.6f, 0.55f, 0.5f), 0.4f, 2f) },
            { Kind.StoneWall, new Spec("stacked_stone_wall", new Color(0.6f, 0.6f, 0.58f), 0.5f, 2.5f) },
            { Kind.Concrete, new Spec("dirty_concrete", new Color(0.62f, 0.62f, 0.6f), 0.5f, 4f) },
            { Kind.Bark, new Spec("bark_brown_02", new Color(0.55f, 0.5f, 0.45f), 0.35f, 2f) },
            { Kind.Rock, new Spec("mossy_rock", new Color(0.5f, 0.52f, 0.48f), 0.45f, 3f) },
        };

        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
        static Shader standard;

        public static void ClearCache() { cache.Clear(); }

        static Texture2D Tex(string name)
        {
            if (texCache.TryGetValue(name, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>("Env/" + name);
            texCache[name] = t;
            return t;
        }

        static Shader Standard
        {
            get
            {
                if (standard == null) standard = Shader.Find("Standard");
                return standard;
            }
        }

        /// <summary>크기(m)에 맞춰 타일링된 재질. 같은 설정은 공유.</summary>
        public static Material Get(Kind kind, Vector2 sizeMeters, Color? tintOverride = null)
        {
            var sp = specs[kind];
            Vector2 tiling = new Vector2(Mathf.Max(0.25f, sizeMeters.x / sp.tileSize), Mathf.Max(0.25f, sizeMeters.y / sp.tileSize));
            tiling = new Vector2(Mathf.Round(tiling.x * 4f) / 4f, Mathf.Round(tiling.y * 4f) / 4f);
            Color tint = tintOverride ?? sp.tint;
            string key = $"{kind}|{tiling.x}|{tiling.y}|{ColorUtility.ToHtmlStringRGBA(tint)}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;

            m = new Material(Standard) { name = "Env_" + kind };
            m.color = tint;
            m.SetFloat("_Glossiness", sp.gloss);
            m.SetFloat("_Metallic", kind == Kind.MetalSheet ? 0.35f : 0f);
            var diff = Tex(sp.id + "_diff");
            if (diff != null)
            {
                m.mainTexture = diff;
                m.mainTextureScale = tiling;
            }
            var nor = Tex(sp.id + "_nor");
            if (nor != null)
            {
                m.SetTexture("_BumpMap", nor);
                m.SetTextureScale("_BumpMap", tiling);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            cache[key] = m;
            return m;
        }

        /// <summary>단색 재질 (텍스처 없음). wet=광택.</summary>
        public static Material Plain(Color c, float gloss = 0.4f, float metallic = 0f)
        {
            string key = $"plain|{ColorUtility.ToHtmlStringRGBA(c)}|{gloss}|{metallic}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Standard) { name = "Env_Plain" };
            m.color = c;
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Metallic", metallic);
            cache[key] = m;
            return m;
        }

        /// <summary>스스로 빛나는 재질 (창문 불빛, 가로등 갓).</summary>
        public static Material Emissive(Color c, float intensity)
        {
            string key = $"emis|{ColorUtility.ToHtmlStringRGBA(c)}|{intensity}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Standard) { name = "Env_Emissive" };
            m.color = c * 0.3f;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            cache[key] = m;
            return m;
        }

        /// <summary>반투명 재질 (비닐하우스).</summary>
        public static Material Transparent(Color c, float gloss = 0.8f)
        {
            string key = $"trans|{ColorUtility.ToHtmlStringRGBA(c)}|{gloss}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Standard) { name = "Env_Transparent" };
            m.color = c;
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Mode", 3f); // Transparent
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            cache[key] = m;
            return m;
        }
    }
}

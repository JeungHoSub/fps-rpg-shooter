using UnityEngine;

namespace Hunt.Core
{
    public enum GfxLevel { Low = 0, Medium = 1, High = 2 }

    /// <summary>
    /// 그래픽 품질. PC 사양으로 자동 선택하고, 프레임이 계속 30 아래로 떨어지면 한 단계씩 내린다.
    /// 프레임은 60으로 제한해 그래픽카드가 쉬지 않고 100%로 도는 것을 막는다.
    /// </summary>
    public static class QualityManager
    {
        const string Key = "hunt.gfx";
        public static GfxLevel Level { get; private set; } = GfxLevel.Medium;
        public static bool AutoDetected { get; private set; }
        public static string DeviceSummary { get; private set; } = "";

        public static event System.Action Changed;

        // ---- 단계별 수치 ----
        public static float RainRate => Level == GfxLevel.Low ? 0.15f : Level == GfxLevel.Medium ? 0.55f : 1f;
        public static float SplashRate => Level == GfxLevel.Low ? 0.08f : Level == GfxLevel.Medium ? 0.5f : 1f;
        public static bool Shadows => Level != GfxLevel.Low;
        public static bool SoftShadows => Level == GfxLevel.High;
        public static int MaxAliveMonsters => Level == GfxLevel.Low ? 10 : Level == GfxLevel.Medium ? 18 : 26;
        /// <summary>시야 거리 (안개가 가려주므로 저사양은 짧게).</summary>
        public static float FarClip => Level == GfxLevel.Low ? 70f : Level == GfxLevel.Medium ? 140f : 400f;
        public static bool ExtraLights => Level == GfxLevel.High;   // 창문·비닐하우스 불빛
        public static bool AllLampLights => Level != GfxLevel.Low;  // 가로등 모두 켜기
        public static int GibLimit => Level == GfxLevel.Low ? 6 : 14;

        public static void Init()
        {
            DeviceSummary = $"{SystemInfo.graphicsDeviceName} · VRAM {SystemInfo.graphicsMemorySize}MB · RAM {SystemInfo.systemMemorySize}MB · CPU {SystemInfo.processorCount}코어";
            int saved = -1;
            try { saved = PlayerPrefs.GetInt(Key, -1); } catch { }
            if (saved >= 0 && saved <= 2) { Level = (GfxLevel)saved; AutoDetected = false; }
            else { Level = Detect(); AutoDetected = true; }
            Apply();
        }

        /// <summary>내장 그래픽이나 VRAM/RAM이 적으면 '하', 넉넉하면 '상'.</summary>
        static GfxLevel Detect()
        {
            // 휴대폰: 기본 '하', 램 8GB 이상·8코어 고급 기기만 '중'
            if (Application.isMobilePlatform)
                return SystemInfo.systemMemorySize >= 7500 && SystemInfo.processorCount >= 8 ? GfxLevel.Medium : GfxLevel.Low;
            string gpu = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();
            int vram = SystemInfo.graphicsMemorySize;
            int ram = SystemInfo.systemMemorySize;
            bool integrated = gpu.Contains("intel") || gpu.Contains("uhd") || gpu.Contains("iris") || gpu.Contains("vega 8")
                              || gpu.Contains("radeon(tm) graphics") || gpu.Contains("radeon graphics") || gpu.Contains("microsoft basic");
            if (integrated || vram < 2500 || ram < 8000) return GfxLevel.Low;
            if (vram >= 6000 && ram >= 16000) return GfxLevel.High;
            return GfxLevel.Medium;
        }

        public static void Set(GfxLevel level, bool save = true)
        {
            Level = level;
            if (save) { try { PlayerPrefs.SetInt(Key, (int)level); } catch { } }
            AutoDetected = false;
            Apply();
        }

        static int nativeW, nativeH;

        static void Apply()
        {
            Application.targetFrameRate = 60;
            // 휴대폰: 렌더 해상도를 낮춰 발열·프레임을 잡는다 (UI도 같은 비율로 그려짐)
            if (Application.isMobilePlatform && !Application.isEditor)
            {
                if (nativeW == 0)
                {
                    nativeW = Mathf.Max(Screen.width, Screen.height);
                    nativeH = Mathf.Min(Screen.width, Screen.height);
                }
                float rs = Level == GfxLevel.Low ? 0.6f : Level == GfxLevel.Medium ? 0.75f : 0.9f;
                Screen.SetResolution(Mathf.RoundToInt(nativeW * rs), Mathf.RoundToInt(nativeH * rs), true);
            }
            QualitySettings.vSyncCount = 0; // 프레임 제한은 targetFrameRate로 (에디터에서도 동작)
            QualitySettings.pixelLightCount = Level == GfxLevel.Low ? 1 : Level == GfxLevel.Medium ? 4 : 8;
            QualitySettings.shadows = Level == GfxLevel.Low ? ShadowQuality.Disable : Level == GfxLevel.Medium ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowDistance = Level == GfxLevel.Low ? 15f : Level == GfxLevel.Medium ? 30f : 50f;
            QualitySettings.shadowCascades = Level == GfxLevel.High ? 2 : 1;
            QualitySettings.antiAliasing = Level == GfxLevel.High ? 2 : 0;
#if UNITY_2022_2_OR_NEWER
            QualitySettings.globalTextureMipmapLimit = Level == GfxLevel.Low ? 1 : 0; // 하: 텍스처 절반 해상도
#else
            QualitySettings.masterTextureLimit = Level == GfxLevel.Low ? 1 : 0;
#endif
            QualitySettings.softParticles = false;
            QualitySettings.skinWeights = Level == GfxLevel.High ? SkinWeights.FourBones : SkinWeights.TwoBones; // 좀비 뼈 계산 간소화
            Changed?.Invoke();
        }

        public static string Name(GfxLevel l) => l == GfxLevel.Low ? "하 (저사양)" : l == GfxLevel.Medium ? "중" : "상";

        // ---- 프레임 감시: 계속 느리면 자동으로 한 단계 내림 ----
        static float fpsAvg = 60f, slowTime;
        public static float Fps => fpsAvg;

        public static void Tick(float unscaledDt)
        {
            if (unscaledDt <= 0f) return;
            fpsAvg = Mathf.Lerp(fpsAvg, 1f / unscaledDt, 0.05f);
            if (fpsAvg < 28f && Level > GfxLevel.Low)
            {
                slowTime += unscaledDt;
                if (slowTime > 6f)
                {
                    slowTime = 0f;
                    Set(Level - 1, save: true);
                    UI.HUD.Notify($"프레임이 낮아 그래픽을 '{Name(Level)}'(으)로 낮췄다 (Esc에서 변경)", new Color(1f, 0.7f, 0.4f));
                }
            }
            else slowTime = Mathf.Max(0f, slowTime - unscaledDt);
        }
    }
}

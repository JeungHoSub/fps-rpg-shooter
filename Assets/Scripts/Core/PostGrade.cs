using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 화면 색감 + 가장자리 계단 줄이기(FXAA) (메인 카메라). Resources/Shaders/HuntGrade.shader 를 쓴다.
    /// 셰이더를 못 읽거나 이 그래픽에서 안 되면 스스로 꺼진다 (게임은 그대로).
    /// 비상 절약 모드나 Esc '색감 끔'이면 건너뛴다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostGrade : MonoBehaviour
    {
        Material mat;
        bool failed;

        void Awake()
        {
            var sh = Resources.Load<Shader>("Shaders/HuntGrade");
            if (sh == null || !sh.isSupported) { failed = true; enabled = false; return; }
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (failed || mat == null || QualityManager.Emergency || !GameSettings.Grade)
            {
                Graphics.Blit(src, dst);
                return;
            }
            float dawn = Atmosphere.Instance != null ? Atmosphere.Instance.Dawn : 0f;
            mat.SetFloat("_Sat", Mathf.Lerp(0.82f, 0.95f, dawn));                       // 밤은 빛이 바랜 색
            mat.SetFloat("_Contrast", 1.12f);
            mat.SetFloat("_Pivot", 0.28f);
            mat.SetFloat("_Vignette", 0.85f); // 모서리 약 60% 밝기 (밤이라 너무 어둡지 않게)
            mat.SetFloat("_Grain", QualityManager.Level == GfxLevel.Low ? 0.018f : 0.028f);
            mat.SetFloat("_Seed", Time.unscaledTime % 97f);
            mat.SetFloat("_Fxaa", QualitySettings.antiAliasing > 0 ? 0f : 1f); // MSAA가 없으면 가장자리 계단을 부드럽게
            // 어두운 곳: 푸르스름한 먹빛 / 밝은 곳(불빛·손전등): 따뜻한 등잔 빛
            mat.SetColor("_Shadow", Color.Lerp(new Color(0.88f, 0.97f, 1.1f), new Color(0.98f, 0.98f, 1.02f), dawn));
            mat.SetColor("_Highlight", Color.Lerp(new Color(1.08f, 1.0f, 0.9f), new Color(1.04f, 1.0f, 0.95f), dawn));
            Graphics.Blit(src, dst, mat);
        }
    }
}

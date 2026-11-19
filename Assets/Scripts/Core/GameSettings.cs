using UnityEngine;

namespace Hunt.Core
{
    /// <summary>플레이어 설정 (마우스 감도, 음량). PlayerPrefs에 저장.</summary>
    public static class GameSettings
    {
        const string SensKey = "hunt.sensitivity";
        const string VolKey = "hunt.volume";

        static float? sensitivity, volume;

        public static float MouseSensitivity
        {
            get { if (sensitivity == null) sensitivity = Load(SensKey, 2f); return sensitivity.Value; }
            set { sensitivity = Mathf.Clamp(value, 0.2f, 6f); Save(SensKey, sensitivity.Value); }
        }

        public static float Volume
        {
            get { if (volume == null) volume = Load(VolKey, 0.8f); return volume.Value; }
            set { volume = Mathf.Clamp01(value); AudioListener.volume = volume.Value; Save(VolKey, volume.Value); }
        }

        // ---- 모바일 ----
        const string AutoFireKey = "hunt.autofire", AssistKey = "hunt.aimassist", PreviewKey = "hunt.mobilepreview";
        static bool? autoFire, aimAssist, forceMobile;

        /// <summary>자동 사격: 조준점에 망자가 오면 알아서 쏜다 (터치 조작 기본값 켜짐).</summary>
        public static bool AutoFire
        {
            get { if (autoFire == null) autoFire = Load(AutoFireKey, 1f) > 0.5f; return autoFire.Value; }
            set { autoFire = value; Save(AutoFireKey, value ? 1f : 0f); }
        }

        /// <summary>조준 보조: 망자 근처에서 조준이 살짝 끌린다 (터치 조작에서만).</summary>
        public static bool AimAssist
        {
            get { if (aimAssist == null) aimAssist = Load(AssistKey, 1f) > 0.5f; return aimAssist.Value; }
            set { aimAssist = value; Save(AssistKey, value ? 1f : 0f); }
        }

        /// <summary>에디터·PC에서 터치 조작 화면을 마우스로 미리 써 보기.</summary>
        public static bool ForceMobileControls
        {
            // 앱이 목표라 유니티 에디터에서는 기본으로 터치 화면을 보여준다 (Esc 메뉴에서 끌 수 있음)
            get { if (forceMobile == null) forceMobile = Load(PreviewKey, Application.isEditor ? 1f : 0f) > 0.5f; return forceMobile.Value; }
            set { forceMobile = value; Save(PreviewKey, value ? 1f : 0f); }
        }

        public static void Apply() { AudioListener.volume = Volume; }

        static float Load(string key, float def)
        {
            try { return PlayerPrefs.GetFloat(key, def); } catch { return def; }
        }

        static void Save(string key, float v)
        {
            try { PlayerPrefs.SetFloat(key, v); } catch { /* 저장 실패는 무시 */ }
        }
    }
}

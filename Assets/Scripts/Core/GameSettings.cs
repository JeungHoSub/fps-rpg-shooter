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

        const string FovKey = "hunt.fov";
        static float? fov;

        /// <summary>시야각 (65~95, 기본 75). 넓히면 주변이 잘 보이지만 멀리 있는 망자가 작아진다.</summary>
        public static float Fov
        {
            get { if (fov == null) fov = Mathf.Clamp(Load(FovKey, 75f), 65f, 95f); return fov.Value; }
            set { fov = Mathf.Clamp(Mathf.Round(value), 65f, 95f); Save(FovKey, fov.Value); }
        }

        const string BrightKey = "hunt.brightness";
        static float? brightness;

        /// <summary>밝기 (0.6~1.8, 기본 1). 어두운 화면·모니터에서 망자가 안 보이면 올린다 (주변광·달빛에 곱함).</summary>
        public static float Brightness
        {
            get { if (brightness == null) brightness = Mathf.Clamp(Load(BrightKey, 1f), 0.6f, 1.8f); return brightness.Value; }
            set { brightness = Mathf.Clamp(value, 0.6f, 1.8f); Save(BrightKey, brightness.Value); }
        }

        const string DiffKey = "hunt.difficulty";
        static int? difficulty;

        /// <summary>난이도 0 쉬움 · 1 보통 · 2 어려움. 새로 나오는 망자의 체력·피해에 곱한다.</summary>
        public static int Difficulty
        {
            get { if (difficulty == null) difficulty = Mathf.Clamp(Mathf.RoundToInt(Load(DiffKey, 1f)), 0, 2); return difficulty.Value; }
            set { difficulty = Mathf.Clamp(value, 0, 2); Save(DiffKey, difficulty.Value); }
        }
        public static float DiffHp => Difficulty == 0 ? 0.75f : Difficulty == 2 ? 1.3f : 1f;
        public static float DiffDamage => Difficulty == 0 ? 0.65f : Difficulty == 2 ? 1.3f : 1f;
        public static string DifficultyName(int d) => d == 0 ? "쉬움" : d == 2 ? "어려움" : "보통";

        // ---- 모바일 ----
        // PreviewKey2: 예전 키에 '켜짐'이 저장돼 있어도 PC 기본(꺼짐)으로 시작하도록 새 키 사용
        const string AutoFireKey = "hunt.autofire", AssistKey = "hunt.aimassist", PreviewKey2 = "hunt.mobilepreview2";
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
            // 지금은 PC 방식으로 완성하는 단계 → 기본은 키보드·마우스 (Esc 메뉴 '터치 미리보기'로 켤 수 있음)
            get { if (forceMobile == null) forceMobile = Load(PreviewKey2, 0f) > 0.5f; return forceMobile.Value; }
            set { forceMobile = value; Save(PreviewKey2, value ? 1f : 0f); }
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

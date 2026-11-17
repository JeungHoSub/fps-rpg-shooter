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

using Hunt.UI;
using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 마을회관 확성기 탑: 밑에서 E → 사이렌이 울려 마을의 망자가 모두 탑 쪽을 뒤지러 간다 (쫓지는 않음).
    /// 밤마다 한 번. 탑 근처에 지뢰·기관총을 깔아 두거나, 반대편으로 빠져나갈 때 쓴다.
    /// </summary>
    public class SirenTower : MonoBehaviour
    {
        public const float Range = 3f;
        public static SirenTower Instance { get; private set; }
        int usedWave = -1;

        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; }

        int CurrentWave()
        {
            var gm = GameManager.Instance;
            return gm != null && gm.Waves != null ? gm.Waves.Wave : 0;
        }

        public bool UsedThisNight => usedWave == CurrentWave();

        /// <summary>플레이어가 탑 밑(3m)에 있고 이번 밤에 아직 안 울렸으면 그 탑.</summary>
        public static SirenTower NearPlayer()
        {
            var t = Instance;
            var gm = GameManager.Instance;
            if (t == null || gm == null || gm.Player == null || gm.Player.IsDead || gm.State != GameState.Playing) return null;
            Vector3 d = t.transform.position - gm.Player.transform.position; d.y = 0f;
            return d.sqrMagnitude < Range * Range ? t : null;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.AcceptsGameplayInput || !GameInput.PickupDown) return;
            if (!ReferenceEquals(NearPlayer(), this)) return;
            if (Hunt.Player.Pickup.AnyNearPlayer()) return; // 보급품 줍기가 먼저
            if (UsedThisNight) { HUD.Notify("사이렌은 밤마다 한 번 — 이미 울렸다", UITheme.BoneDim); return; }
            usedWave = CurrentWave();
            Vector3 top = transform.position + Vector3.up * 7.6f;
            SFX.PlayAt3D(SFX.Siren, top, 1f, 1f);
            SFX.Play(SFX.Siren, 0.35f, 0f); // 가까이서도 귀가 울리게
            Noise.Emit(transform.position, 110f, false); // 폭우에도 마을 끝까지 들린다
            HUD.Notify("사이렌이 울린다 — 망자들이 확성기 탑으로 몰려온다", new Color(1f, 0.7f, 0.35f));
        }
    }
}

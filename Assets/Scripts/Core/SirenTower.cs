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
        bool told;

        void OnEnable() { Instance = this; }

        void Start()
        {
            // 확성기 판정 (멀리서 쏴도 울린다): 꼭대기 상자
            var horn = new GameObject("SirenHorn");
            horn.transform.SetParent(transform, false);
            horn.transform.localPosition = Vector3.up * 7.6f;
            var bc = horn.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.3f, 0.6f, 1.3f);
        }

        /// <summary>확성기를 총으로 맞혔다: 밑에서 E를 누른 것처럼 울린다 (밤마다 한 번).</summary>
        public void ShotHorn()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            if (UsedThisNight) return;
            Ring();
            HUD.Notify("확성기를 쏘아 맞혔다 — 사이렌이 울린다", new Color(1f, 0.75f, 0.45f));
        }
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

        Renderer beacon;
        float beaconT;
        /// <summary>사이렌 뒤 붉은 등이 깜빡이는 중 (나침반에 '탑').</summary>
        public bool BeaconOn => beaconT > 0f;

        void Update()
        {
            // 사이렌 뒤 25초 동안 탑 꼭대기 붉은 등이 깜빡인다 (어디로 몰려가는지 보이게)
            if (beaconT > 0f)
            {
                beaconT -= Time.deltaTime;
                if (beacon != null) beacon.enabled = beaconT > 0f && Mathf.Repeat(Time.time, 0.8f) < 0.4f;
            }
            var gm = GameManager.Instance;
            // 처음 탑 가까이(14m) 오면 한 번 알려 준다
            if (!told && gm != null && gm.State == GameState.Playing && gm.Player != null
                && (gm.Player.transform.position - transform.position).sqrMagnitude < 14f * 14f)
            {
                told = true;
                HUD.Notify("확성기 탑 — 밑에서 E를 누르면 사이렌이 울려 망자들을 이쪽으로 꾄다 (밤마다 한 번)", new Color(1f, 0.75f, 0.45f));
            }
            if (gm == null || !gm.AcceptsGameplayInput || !GameInput.PickupDown) return;
            if (!ReferenceEquals(NearPlayer(), this)) return;
            if (Hunt.Player.Pickup.AnyNearPlayer()) return; // 보급품 줍기가 먼저
            if (UsedThisNight) { HUD.Notify("사이렌은 밤마다 한 번 — 이미 울렸다", UITheme.BoneDim); return; }
            Ring();
            HUD.Notify("사이렌이 울린다 — 망자들이 확성기 탑으로 몰려온다", new Color(1f, 0.7f, 0.35f));
        }

        void Ring()
        {
            usedWave = CurrentWave();
            Vector3 top = transform.position + Vector3.up * 7.6f;
            SFX.PlayAt3D(SFX.Siren, top, 1f, 1f);
            SFX.Play(SFX.Siren, 0.35f, 0f); // 가까이서도 귀가 울리게
            Noise.Emit(transform.position, 110f, false); // 폭우에도 마을 끝까지 들린다
            Crows.Startle(transform.position); // 까마귀가 날아오른다
            if (beacon == null)
            {
                var b = FX.LowSphere();
                b.name = "SirenBeacon";
                b.layer = 2;
                b.transform.SetParent(transform, false);
                b.transform.localPosition = Vector3.up * 8.1f;
                b.transform.localScale = Vector3.one * 0.35f;
                beacon = b.GetComponent<Renderer>();
                beacon.sharedMaterial = FX.UnlitMaterial(new Color(1f, 0.12f, 0.05f));
                beacon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            beaconT = 25f;
        }
    }
}

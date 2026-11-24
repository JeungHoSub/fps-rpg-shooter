using System.Collections.Generic;
using Hunt.Data;
using Hunt.Evolution;
using Hunt.Monsters;
using Hunt.Player;
using Hunt.Stats;
using Hunt.UI;
using Hunt.Waves;
using Hunt.Weapons;
using Hunt.Core;
using UnityEngine;

namespace Hunt.Core
{
    public enum GameState { CharacterSelect, Playing, Intermission, GameOver }

    /// <summary>게임 흐름: 캐릭터 선택 → 웨이브 → 정비(진화/성장) → 웨이브 … → 게임오버.</summary>
    public class GameManager : MonoBehaviour
    {
        public const float IntermissionDuration = 30f;
        public const int MaxPrimaries = 2;

        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public bool MenuOpen { get; set; }
        public bool Paused { get; private set; }
        public float IntermissionTime { get; private set; }
        public List<string> LastEvolution { get; private set; } = new List<string>();

        public bool IsWorldRunning => (State == GameState.Playing || State == GameState.Intermission) && !Paused;
        public bool AcceptsGameplayInput => IsWorldRunning && !MenuOpen;

        public PlayerCharacter Player { get; private set; }
        public WaveManager Waves { get; private set; }
        public EvolutionDirector Evolution { get; private set; }
        public Camera ActiveCamera { get; private set; }

        // 시작 화면: 직업 대신 스탯 분배 + 시작 특성 + 무기
        public CharacterData Character => DefaultContent.Hunter;
        public readonly int[] StartAllocation = new int[6];
        public Trait StartTrait = Trait.Bloodlust;
        public readonly List<WeaponData> SelectedPrimaries = new List<WeaponData>();

        public int StartPointsLeft
        {
            get
            {
                int used = 0;
                foreach (var v in StartAllocation) used += v;
                return Character.startingStatPoints - used;
            }
        }

        public void AdjustStartStat(StatType t, int delta)
        {
            int i = (int)t;
            if (delta > 0 && StartPointsLeft > 0) StartAllocation[i]++;
            else if (delta < 0 && StartAllocation[i] > 0) StartAllocation[i]--;
        }

        /// <summary>추천 각인 적용 (포인트 합이 시작 포인트를 넘으면 앞에서부터 잘린다).</summary>
        public void ApplyStartPreset(int[] alloc)
        {
            System.Array.Clear(StartAllocation, 0, StartAllocation.Length);
            int left = Character.startingStatPoints;
            for (int i = 0; i < StartAllocation.Length && i < alloc.Length; i++)
            {
                int v = Mathf.Min(alloc[i], left);
                StartAllocation[i] = v;
                left -= v;
            }
        }

        public void ResetStartAllocation() { System.Array.Clear(StartAllocation, 0, StartAllocation.Length); }

        readonly List<GameObject> displayMonsters = new List<GameObject>();

        /// <summary>시작 화면 배경: 빗속에 멈춰 선 망자들.</summary>
        void SpawnDisplayMonsters()
        {
            displayMonsters.Clear();
            var evo = new EvolutionProfile();
            var spots = new[]
            {
                (MonsterArchetype.Runner, new Vector3(-4f, 0f, 6f), 160f),
                (MonsterArchetype.Runner, new Vector3(3f, 0f, 9f), 200f),
                (MonsterArchetype.Brute, new Vector3(0f, 0f, 14f), 180f),
                (MonsterArchetype.Spitter, new Vector3(-9f, 0f, 12f), 140f),
                (MonsterArchetype.Runner, new Vector3(8f, 0f, 4f), 230f),
            };
            foreach (var (type, pos, yaw) in spots)
            {
                var m = MonsterFactory.Create(DefaultContent.Monster(type), Navigation.Snap(pos), evo, 1f, null, world);
                m.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                displayMonsters.Add(m.gameObject);
            }
        }

        void ClearDisplayMonsters()
        {
            foreach (var go in displayMonsters)
                if (go != null) Destroy(go);
            displayMonsters.Clear();
        }

        /// <summary>시작 화면 미리보기용 스탯 (분배·특성 반영).</summary>
        public CharacterStats PreviewStats()
        {
            var s = new CharacterStats(Character);
            foreach (var t in CharacterStats.All)
                for (int k = 0; k < StartAllocation[(int)t]; k++) s.Allocate(t);
            s.traits.Add(StartTrait);
            return s;
        }

        Transform world;

        void Awake()
        {
            Instance = this;
            SFX.Init(gameObject);
            GameSettings.Apply();
            QualityManager.Init();
            SelectedPrimaries.Add(DefaultContent.Weapons[1]); // 돌격소총
            SelectedPrimaries.Add(DefaultContent.Weapons[3]); // 샷건
            BuildWorld();
        }

        void OnDestroy()
        {
            Navigation.Clear();
            if (Instance == this) Instance = null;
        }

        void BuildWorld()
        {
            world = new GameObject("World").transform;
            Effects.Root = world;
            FX.Init(world);
            var arena = VillageBuilder.Build(world);
            Navigation.Build(arena);
            world.gameObject.AddComponent<Atmosphere>().Init(world);
            Hunt.UI.MapView.Capture(); // M 지도용으로 마을을 위에서 한 번 찍어 둔다
            Evolution = world.gameObject.AddComponent<EvolutionDirector>();
            Waves = world.gameObject.AddComponent<WaveManager>();
            LastEvolution = new List<string>();

            DisableForeignCameras();
            var menuCam = new GameObject("MenuCamera");
            menuCam.transform.SetParent(world, false);
            menuCam.AddComponent<MenuCameraOrbit>();
            ActiveCamera = menuCam.AddComponent<Camera>();
            ActiveCamera.backgroundColor = RenderSettings.fogColor;
            ActiveCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCam.AddComponent<AudioListener>();
            SpawnDisplayMonsters();

            State = GameState.CharacterSelect;
            MenuOpen = false;
        }

        void DisableForeignCameras()
        {
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (!c.transform.IsChildOf(world)) c.gameObject.SetActive(false);
        }

        public void ToggleWeapon(WeaponData w)
        {
            if (SelectedPrimaries.Contains(w)) { if (SelectedPrimaries.Count > 1) SelectedPrimaries.Remove(w); }
            else if (SelectedPrimaries.Count < MaxPrimaries) SelectedPrimaries.Add(w);
        }

        public void StartGame()
        {
            if (ActiveCamera != null) Destroy(ActiveCamera.gameObject);
            ClearDisplayMonsters();
            SFX.Play(SFX.Gong, 0.9f, 0f);

            var go = new GameObject("Player");
            go.layer = 2; // Ignore Raycast: 자기 몸에 총알이 맞지 않도록
            go.transform.SetParent(world, false);
            go.transform.position = new Vector3(0f, 0.1f, 0f);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.4f;

            var camGo = new GameObject("PlayerCamera");
            camGo.layer = 2;
            camGo.transform.SetParent(go.transform, false);
            camGo.transform.localPosition = new Vector3(0f, PlayerController.EyeHeight, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.fieldOfView = PlayerController.BaseFov;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<EarRing>(); // 폭발 귀 울림 · 생명이 낮을 때 먹먹함
            cam.cullingMask &= ~(1 << GunModelBuilder.ViewLayer);
            ActiveCamera = cam;

            // 1인칭 무기 전용 카메라: 무기가 벽에 파묻히지 않고, 조준 줌과 무관하게 같은 크기로 보인다
            var weaponCamGo = new GameObject("WeaponCamera");
            weaponCamGo.transform.SetParent(camGo.transform, false);
            var weaponCam = weaponCamGo.AddComponent<Camera>();
            weaponCam.clearFlags = CameraClearFlags.Depth;
            weaponCam.cullingMask = 1 << GunModelBuilder.ViewLayer;
            weaponCam.depth = cam.depth + 1;
            weaponCam.fieldOfView = 55f;
            weaponCam.nearClipPlane = 0.01f;
            weaponCam.farClipPlane = 5f;

            var loadout = new List<WeaponData>(SelectedPrimaries) { DefaultContent.Pistol };
            go.AddComponent<Flashlight>().Init(camGo.transform);
            Player = go.AddComponent<PlayerCharacter>();
            Player.Init(Character, StartAllocation, StartTrait, loadout, cam);

            Waves.Setup(world, Player);
            StartNextWave();
        }

        public void StartNextWave()
        {
            State = GameState.Playing;
            MenuOpen = false;
            Waves.StartWave(Waves.Wave + 1);
            HUD.Notify(Waves.IsBossWave ? $"{GameHud.NightName(Waves.Wave)} — 대감의 밤. 망자들이 마을에 흩어져 있다" : $"{GameHud.NightName(Waves.Wave)}이 왔다 — 망자들이 마을에 흩어져 있다", new Color(1f, 0.45f, 0.4f));
            if (Waves.Wave > 1) SFX.Play(SFX.Gong, 0.8f, 0f);
        }

        public void OnWaveCleared()
        {
            LastEvolution = Evolution.Evolve();
            State = GameState.Intermission;
            if (Player != null) Player.ClearCharms(); // 부적은 하룻밤만
            IntermissionTime = IntermissionDuration;
            MenuOpen = true;

            bool failed = Waves.Failed;
            float reward = failed ? 0f : Waves.RewardMult;
            GameRules.Reset();
            Flashlight.Disabled = false;
            Player.Weapons.RefillAmmo();
            foreach (var w in Player.Arsenal) { w.reserve = Mathf.RoundToInt(w.MaxReserve * GameRules.AmmoMult); w.ammoInMag = w.MagazineSize; }
            if (!failed) Player.Resupply();
            Resources.UnloadUnusedAssets(); // 죽은 망자들이 남긴 재질 정리 (메모리 누수 방지)
            Player.Heal(Player.MaxHp * 0.3f);
            Player.Infection.Antidotes = Mathf.Min(2, Player.Infection.Antidotes + 1);
            Player.AddGold(Mathf.RoundToInt((40 + 20 * Waves.Wave) * reward));

            if (!failed)
            {
                if (Waves.Wave >= 1) Achievements.Unlock("night1");
                if (Waves.Wave >= 10) Achievements.Unlock("night10");
                if (Waves.IsBossWave) Achievements.Unlock("daegam");
                if (Player.DamageTaken - Player.NightStartDamage < 10f)
                {
                    int bonus = 30 + 10 * Waves.Wave;
                    Player.AddGold(bonus);
                    HUD.Notify($"상처 없이 밤을 넘겼다 — 엽전 +{bonus}", new Color(0.7f, 1f, 0.75f));
                    Achievements.Unlock("flawless");
                }
            }
            if (!failed) HUD.Notify($"{GameHud.NightName(Waves.Wave)}을 버텼다 — 날이 밝는다" + (reward > 1f ? $"  (변고 보상 ×{reward:0.00})" : ""), new Color(0.5f, 1f, 0.6f));
            if (LastEvolution.Count > 0) HUD.Notify("망자들이 당신의 사냥법을 배웠다… (서낭당 · 역병 일지)", new Color(0.85f, 0.4f, 1f));
        }

        public void OnMonsterKilled(Monster m)
        {
            if (Player != null) Player.OnKill(m);
        }

        public void OnPlayerDied()
        {
            State = GameState.GameOver;
            MenuOpen = false;
        }

        float hitStopUntil;

        /// <summary>히트스톱: 아주 짧게 시간을 늦춰 타격을 강조한다.</summary>
        float slowStart = -10f, slowUntil, slowScale = 1f;

        /// <summary>슬로 모션 (보스 처치 등): 처음엔 scale까지 느려졌다가 끝으로 갈수록 원래 속도로 돌아온다.</summary>
        public void SlowMo(float seconds, float scale)
        {
            slowStart = Time.unscaledTime;
            slowUntil = Time.unscaledTime + seconds;
            slowScale = Mathf.Clamp(scale, 0.05f, 1f);
        }

        float SlowFactor()
        {
            float now = Time.unscaledTime;
            if (now >= slowUntil) return 1f;
            float k = Mathf.InverseLerp(slowStart, slowUntil, now);
            return Mathf.Lerp(slowScale, 1f, k * k);
        }

        public void HitStop(float seconds)
        {
            if (Paused) return;
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
        }

        public void TogglePause()
        {
            if (State != GameState.Playing && State != GameState.Intermission) return;
            Paused = !Paused;
            Time.timeScale = Paused ? 0f : 1f;
        }

        // 창을 벗어나거나(Alt+Tab) 휴대폰에서 앱이 뒤로 가면 자동 일시정지 (싸우다 죽지 않게)
        void OnApplicationFocus(bool focus)
        {
            if (!focus && !Application.isEditor) AutoPause();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) AutoPause();
        }

        void AutoPause()
        {
            if (Paused || State != GameState.Playing) return;
            TogglePause();
        }

        public void Restart()
        {
            Paused = false;
            Time.timeScale = 1f;
            Player = null;
            Monster.Alive.Clear();
            DamageNumbers.Clear();
            Hunt.UI.GameHud.ClearToasts();
            Navigation.Clear();
            Destroy(world.gameObject);
            BuildWorld();
        }

        void Update()
        {
            QualityManager.Tick(Time.unscaledDeltaTime);
            bool cursorFree = !AcceptsGameplayInput || GameInput.Mobile; // 터치 조작(미리보기 포함)은 커서를 잠그지 않는다
            if (Input.GetKeyDown(KeyCode.F9))
            {
                Hunt.Monsters.HitboxDebug.Show = !Hunt.Monsters.HitboxDebug.Show;
                HUD.Notify(Hunt.Monsters.HitboxDebug.Show ? "판정 보기: 켜짐 (F9)" : "판정 보기: 꺼짐", Color.white);
            }
            Cursor.lockState = cursorFree ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = cursorFree;

            if ((State == GameState.Playing || State == GameState.Intermission) && GameInput.PauseDown)
                TogglePause();
            if (Paused) return;
            Time.timeScale = Time.unscaledTime < hitStopUntil ? 0.06f : SlowFactor();

            switch (State)
            {
                case GameState.Intermission:
                    IntermissionTime -= Time.deltaTime;
                    if (GameInput.MenuDown) MenuOpen = !MenuOpen;
                    if (IntermissionTime <= 0f || GameInput.ConfirmDown)
                        StartNextWave();
                    break;
                case GameState.GameOver:
                    if (GameInput.RestartDown) Restart();
                    break;
            }
        }
    }
}

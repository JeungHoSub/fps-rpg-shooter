using System.Collections.Generic;
using Hunt.Data;
using Hunt.Evolution;
using Hunt.Monsters;
using Hunt.Player;
using Hunt.UI;
using Hunt.Waves;
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

        // 선택 화면
        public CharacterData SelectedCharacter;
        public readonly List<WeaponData> SelectedPrimaries = new List<WeaponData>();

        Transform world;

        void Awake()
        {
            Instance = this;
            SelectedCharacter = DefaultContent.Characters[0];
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
            var arena = ArenaBuilder.Build(world);
            Navigation.Build(arena);
            Evolution = world.gameObject.AddComponent<EvolutionDirector>();
            Waves = world.gameObject.AddComponent<WaveManager>();
            LastEvolution = new List<string>();

            DisableForeignCameras();
            var menuCam = new GameObject("MenuCamera");
            menuCam.transform.SetParent(world, false);
            menuCam.transform.position = new Vector3(0f, 18f, -38f);
            menuCam.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
            ActiveCamera = menuCam.AddComponent<Camera>();
            ActiveCamera.backgroundColor = RenderSettings.fogColor;
            ActiveCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCam.AddComponent<AudioListener>();

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

            var go = new GameObject("Player");
            go.layer = 2; // Ignore Raycast: 자기 몸에 총알이 맞지 않도록
            go.transform.SetParent(world, false);
            go.transform.position = new Vector3(0f, 0.1f, 0f);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

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
            ActiveCamera = cam;

            var loadout = new List<WeaponData>(SelectedPrimaries) { DefaultContent.Pistol };
            Player = go.AddComponent<PlayerCharacter>();
            Player.Init(SelectedCharacter, loadout, cam);

            Waves.Setup(world, Player);
            StartNextWave();
        }

        public void StartNextWave()
        {
            State = GameState.Playing;
            MenuOpen = false;
            Waves.StartWave(Waves.Wave + 1);
            HUD.Notify(Waves.IsBossWave ? $"웨이브 {Waves.Wave} — 보스 출현!" : $"웨이브 {Waves.Wave} 시작", new Color(1f, 0.5f, 0.4f));
        }

        public void OnWaveCleared()
        {
            LastEvolution = Evolution.Evolve();
            State = GameState.Intermission;
            IntermissionTime = IntermissionDuration;
            MenuOpen = true;

            Player.Weapons.RefillAmmo();
            Player.Heal(Player.Stats.MaxHp * 0.3f);
            Player.Infection.Antidotes = Mathf.Min(2, Player.Infection.Antidotes + 1);
            Player.AddGold(40 + 20 * Waves.Wave);

            HUD.Notify($"웨이브 {Waves.Wave} 클리어! 보급 완료", new Color(0.5f, 1f, 0.6f));
            if (LastEvolution.Count > 0) HUD.Notify("괴물들이 진화했다…", new Color(0.85f, 0.4f, 1f));
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

        public void Restart()
        {
            Paused = false;
            Time.timeScale = 1f;
            Player = null;
            Monster.Alive.Clear();
            DamageNumbers.Clear();
            Navigation.Clear();
            Destroy(world.gameObject);
            BuildWorld();
        }

        void Update()
        {
            bool cursorFree = !AcceptsGameplayInput;
            Cursor.lockState = cursorFree ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = cursorFree;

            if ((State == GameState.Playing || State == GameState.Intermission) && Input.GetKeyDown(KeyCode.Escape))
            {
                Paused = !Paused;
                Time.timeScale = Paused ? 0f : 1f;
            }
            if (Paused) return;

            switch (State)
            {
                case GameState.Intermission:
                    IntermissionTime -= Time.deltaTime;
                    if (Input.GetKeyDown(KeyCode.Tab)) MenuOpen = !MenuOpen;
                    if (IntermissionTime <= 0f || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                        StartNextWave();
                    break;
                case GameState.GameOver:
                    if (Input.GetKeyDown(KeyCode.R)) Restart();
                    break;
            }
        }
    }
}

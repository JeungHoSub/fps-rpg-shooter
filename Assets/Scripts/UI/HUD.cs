using System.Collections.Generic;
using Hunt.Core;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// UI 진입점 (IMGUI). 1080p 가상 해상도로 맞춘 뒤 상태별 화면을 그린다.
    /// 화면: 시작(StartScreen) · 인게임(GameHud) · 야영지(CampMenu) · 일시정지/사망(EndScreens).
    /// </summary>
    public class HUD : MonoBehaviour
    {
        const float RefHeight = 1080f;

        struct Note { public string text; public Color color; public float time; public float age; public float y; }
        static readonly List<Note> notes = new List<Note>();

        /// <summary>화면 위쪽 알림 (어디서든 호출).</summary>
        public static void Notify(string text, Color color)
        {
            for (int i = 0; i < notes.Count; i++)
                if (notes[i].text == text) { notes.RemoveAt(i); break; } // 같은 알림은 갱신
            notes.Add(new Note { text = text, color = color, time = 4f, y = -1f });
            if (notes.Count > 6) notes.RemoveAt(0);
        }

        // 화면 객체는 Awake에서 만든다 (필드 초기화 시점에는 유니티 오브젝트 생성이 금지됨)
        StartScreen start;
        GameHud hud;
        readonly TutorialHints tutorial = new TutorialHints();
        CampMenu camp;
        EndScreens ends;

        string uiError;
        float uiErrorTime;

        void Awake()
        {
            start = new StartScreen();
            hud = new GameHud();
            camp = new CampMenu();
            ends = new EndScreens();
        }

        GameState lastState = (GameState)(-1);
        float mapT; // M 지도 펼침 정도
        bool lastMenuOpen;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                var n = notes[i];
                n.time -= dt;
                n.age += dt;
                if (n.time <= 0f) notes.RemoveAt(i); else notes[i] = n;
            }
            DamageNumbers.Tick(Time.deltaTime);
        }

        void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (start == null) Awake();

            float scale = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float W = Screen.width / scale, H = RefHeight;

            try
            {
                DrawScreens(gm, W, H, scale);
            }
            catch (ExitGUIException) { throw; }
            catch (System.Exception ex)
            {
                // UI 오류가 나도 게임은 계속된다. 첫 오류만 콘솔에 자세히 남긴다.
                if (uiError == null) Debug.LogException(ex);
                uiError = ex.GetType().Name + ": " + ex.Message;
                uiErrorTime = Time.unscaledTime;
            }
            DrawErrorFallback(gm, W);
        }

        void DrawErrorFallback(GameManager gm, float W)
        {
            if (uiError == null || Time.unscaledTime - uiErrorTime > 3f) return;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.height / RefHeight, Screen.height / RefHeight, 1f));
            GUI.color = Color.white;
            GUI.Box(new Rect(20, 20, 760, 70), "");
            GUI.Label(new Rect(32, 26, 740, 60), "<color=#ff8080><b>UI 오류</b></color> — 이 문구를 캡처해서 보내 주세요\n" + uiError, new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16, wordWrap = true });
            if (gm.State == GameState.CharacterSelect && GUI.Button(new Rect(W / 2 - 150, 980, 300, 60), "비상 출정 (기본 설정으로 시작)"))
            {
                gm.ApplyStartPreset(Hunt.UI.GameText.Presets[0].alloc);
                gm.StartGame();
            }
        }

        void DrawScreens(GameManager gm, float W, float H, float scale)
        {
            UITheme.Ensure();
            UIKit.BeginFrame();

            // 화면 전환 감지
            if (gm.State != lastState)
            {
                if (gm.State == GameState.CharacterSelect) start.Reset();
                if (gm.State == GameState.GameOver) ends.ResetDeath();
                lastState = gm.State;
            }
            if (gm.MenuOpen && !lastMenuOpen) camp.OnOpen();
            lastMenuOpen = gm.MenuOpen;

            switch (gm.State)
            {
                case GameState.CharacterSelect:
                    start.Draw(gm, W, H);
                    break;
                case GameState.Playing:
                case GameState.Intermission:
                    hud.Draw(gm, W, H, scale);
                    if (!gm.Paused && !gm.MenuOpen) tutorial.Draw(gm, W, H); // 첫 밤 조작 안내
                    if (UIKit.IsRepaint) mapT = Mathf.MoveTowards(mapT, GameInput.MapHeld && !gm.Paused && !gm.MenuOpen ? 1f : 0f, Time.unscaledDeltaTime * 8f);
                    MapView.Draw(gm, W, H, UIKit.EaseOut(mapT));
                    if (!gm.Paused) hud.DawnInput(gm); // 새벽 기록 카드가 떠 있으면 클릭을 먼저 받는다
                    if (gm.State == GameState.Intermission && gm.MenuOpen && !gm.Paused) camp.Draw(gm, W, H);
                    if (!gm.Paused) hud.DrawDawnCard(gm);
                    break;
                case GameState.GameOver:
                    hud.Draw(gm, W, H, scale);
                    ends.DrawDeath(gm, W, H);
                    break;
            }
            if (gm.Paused) ends.DrawPause(gm, W, H);

            DrawNotes(W);
            DrawFps(gm);
            TryScreenshot();
            UIKit.DrawTooltip(W, H);
        }

        static bool showFps;

        /// <summary>F12: 바탕화면 '역병의마을_사진' 폴더에 화면을 저장한다 (게임 화면을 보내 줄 때).</summary>
        static void TryScreenshot()
        {
            if (Event.current.type != EventType.KeyDown || Event.current.keyCode != KeyCode.F12 || GameInput.Mobile) return;
            Event.current.Use();
            try
            {
                string dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory), "역병의마을_사진");
                System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir, "사진_" + System.DateTime.Now.ToString("MMdd_HHmmss") + ".png");
                ScreenCapture.CaptureScreenshot(file);
                Notify("사진을 찍었다 — 바탕화면 '역병의마을_사진' 폴더", new Color(0.8f, 0.9f, 1f));
                SFX.Play(SFX.FlashlightClick, 0.6f);
            }
            catch (System.Exception e) { Notify("사진 저장 실패: " + e.Message, new Color(1f, 0.5f, 0.4f)); }
        }

        /// <summary>F3: 왼쪽 위에 FPS·품질 (내장 그래픽에서 끊김 확인용).</summary>
        static void DrawFps(GameManager gm)
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F3 && !GameInput.Mobile) { showFps = !showFps; Event.current.Use(); }
            if (!showFps) return;
            float fps = QualityManager.Fps;
            Color c = fps < 30f ? UITheme.BloodBright : fps < 50f ? UITheme.Warn : UITheme.Good;
            string q = QualityManager.Level == GfxLevel.Low ? "하" : QualityManager.Level == GfxLevel.Medium ? "중" : "상";
            UIKit.Fill(new Rect(8, 8, 190, 30), new Color(0f, 0f, 0f, 0.6f));
            UIKit.Text(new Rect(16, 8, 180, 30), $"<color=#{UIKit.Hex(c)}><b>{fps:0} FPS</b></color>   그래픽 {q}  ·  망자 {Hunt.Monsters.Monster.Alive.Count}", 14, UITheme.Bone);
        }

        static void DrawNotes(float W)
        {
            float y = 178;
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                var n = notes[i];
                float a = Mathf.Clamp01(n.time) * Mathf.Clamp01(n.age * 6f);
                float slide = (1f - UIKit.EaseOut(n.age * 5f)) * -14f;
                // 자리 이동도 부드럽게 (새 알림이 오거나 하나가 사라질 때 줄이 미끄러진다)
                if (n.y < 0f) n.y = y;
                else if (UIKit.IsRepaint) n.y = Mathf.Lerp(n.y, y, 1f - Mathf.Exp(-14f * Mathf.Min(Time.unscaledDeltaTime, 0.05f)));
                notes[i] = n;
                var r = new Rect(W / 2 - 420, n.y + slide, 840, 34);
                UIKit.Tex(new Rect(r.x, r.y + 2, r.width, r.height - 4), UITheme.LineFade, new Color(0f, 0f, 0f, 0.55f * a));
                var c = n.color;
                c.a = a;
                UIKit.Text(r, n.text, 21, c, TextAnchor.MiddleCenter, serif: true, bold: true);
                y += 36;
            }
        }
    }
}

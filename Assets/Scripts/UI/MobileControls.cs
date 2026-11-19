using System.Collections.Generic;
using Hunt.Core;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// 휴대폰 터치 조작. 매 프레임 가장 먼저 터치를 읽어 GameInput에 채우고(Update), 버튼을 그린다(OnGUI).
    ///  - 왼쪽 아무 곳: 손가락을 댄 곳이 조이스틱 중심 (끝까지 밀면 달리기)
    ///  - 오른쪽 빈 곳: 드래그 = 시점
    ///  - 사격 버튼은 누른 채로 끌면 시점도 돌아간다 (모바일 FPS 표준)
    /// 에디터에서는 '모바일 조작 미리보기'를 켜면 마우스 한 손가락으로 시험할 수 있다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class MobileControls : MonoBehaviour
    {
        const float RefHeight = 1080f;
        const float StickRadius = 120f;
        const float LookGain = 0.11f; // 화면 픽셀 → 마우스 축과 비슷한 크기

        enum Id { Fire, Aim, Reload, Jump, Swap, Grenade, Melee, Flash, Kit, Drink, Rage, Regen, Antidote, Pause, Menu }

        struct Btn
        {
            public Id id;
            public Vector2 c;
            public float r;
            public string label;
        }

        class TouchOwner
        {
            public string kind; // "stick", "look", "btn"
            public Id btn;
            public Vector2 origin, last;
        }

        readonly Dictionary<int, TouchOwner> owners = new Dictionary<int, TouchOwner>();
        readonly List<Btn> buttons = new List<Btn>();
        readonly HashSet<Id> held = new HashSet<Id>();
        float W, H, scale;
        Vector2 stickOrigin, stickPos;
        bool stickActive, aimToggle, mouseWasDown;

        bool Active
        {
            get
            {
                var gm = GameManager.Instance;
                return GameInput.Mobile && gm != null && (gm.State == GameState.Playing || gm.State == GameState.Intermission);
            }
        }

        void Layout()
        {
            scale = Screen.height / RefHeight;
            W = Screen.width / scale;
            H = RefHeight;
            buttons.Clear();
            var gm = GameManager.Instance;
            if (gm == null) return;

            buttons.Add(new Btn { id = Id.Pause, c = new Vector2(64, 64), r = 40, label = "Ⅱ" });
            if (gm.State == GameState.Intermission)
                buttons.Add(new Btn { id = Id.Menu, c = gm.MenuOpen ? new Vector2(W - 80, 64) : new Vector2(W / 2f, 170), r = gm.MenuOpen ? 44 : 58, label = gm.MenuOpen ? "닫기" : "서낭당" });
            if (!gm.AcceptsGameplayInput) return;

            // 오른손: 사격·조준·장전·점프·교체·수류탄·칼
            buttons.Add(new Btn { id = Id.Fire, c = new Vector2(W - 210, H - 330), r = 108, label = "사격" });
            buttons.Add(new Btn { id = Id.Aim, c = new Vector2(W - 430, H - 250), r = 62, label = "조준" });
            buttons.Add(new Btn { id = Id.Reload, c = new Vector2(W - 175, H - 565), r = 54, label = "장전" });
            buttons.Add(new Btn { id = Id.Jump, c = new Vector2(W - 380, H - 455), r = 52, label = "점프" });
            buttons.Add(new Btn { id = Id.Swap, c = new Vector2(W - 72, H - 470), r = 48, label = "교체" });
            buttons.Add(new Btn { id = Id.Grenade, c = new Vector2(W - 560, H - 395), r = 50, label = "투척" });
            buttons.Add(new Btn { id = Id.Melee, c = new Vector2(W - 610, H - 245), r = 48, label = "칼" });
            // 위쪽: 손전등·키트·회복제
            buttons.Add(new Btn { id = Id.Flash, c = new Vector2(W - 90, 290), r = 46, label = "등" });
            buttons.Add(new Btn { id = Id.Kit, c = new Vector2(W - 205, 290), r = 42, label = "키트" });
            buttons.Add(new Btn { id = Id.Drink, c = new Vector2(W - 315, 290), r = 42, label = "회복" });
            // 왼쪽 가장자리: 역병의 힘
            buttons.Add(new Btn { id = Id.Rage, c = new Vector2(70, 420), r = 44, label = "광폭" });
            buttons.Add(new Btn { id = Id.Regen, c = new Vector2(70, 530), r = 44, label = "재생" });
            buttons.Add(new Btn { id = Id.Antidote, c = new Vector2(70, 640), r = 44, label = "청심" });
        }

        void Update()
        {
            GameInput.ClearTouchFrame();
            if (!Active)
            {
                owners.Clear();
                held.Clear();
                stickActive = false;
                GameInput.ClearTouchHeld();
                return;
            }
            Layout();

            // 터치 모으기 (에디터 미리보기: 마우스를 손가락 하나로)
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    Handle(t.fingerId, ToGui(t.position), t.phase);
                }
            }
            else if (!Application.isMobilePlatform)
            {
                bool down = Input.GetMouseButton(0);
                var p = ToGui(Input.mousePosition);
                if (down && !mouseWasDown) Handle(-1, p, TouchPhase.Began);
                else if (down) Handle(-1, p, TouchPhase.Moved);
                else if (mouseWasDown) Handle(-1, p, TouchPhase.Ended);
                mouseWasDown = down;
            }

            // 결과를 GameInput으로
            GameInput.touchFire = held.Contains(Id.Fire);
            GameInput.touchAim = aimToggle;
            if (stickActive)
            {
                Vector2 v = (stickPos - stickOrigin) / StickRadius;
                if (v.magnitude > 1f) v.Normalize();
                GameInput.touchMove = new Vector2(v.x, -v.y); // 화면 위쪽 = 앞
                GameInput.touchSprint = v.magnitude > 0.92f && -v.y > 0.6f;
            }
            else
            {
                GameInput.touchMove = Vector2.zero;
                GameInput.touchSprint = false;
            }
        }

        Vector2 ToGui(Vector2 screen) => new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);

        void Handle(int finger, Vector2 p, TouchPhase phase)
        {
            if (phase == TouchPhase.Began)
            {
                // 1) 버튼
                foreach (var b in buttons)
                {
                    if ((p - b.c).sqrMagnitude <= b.r * b.r * 1.25f)
                    {
                        owners[finger] = new TouchOwner { kind = "btn", btn = b.id, last = p };
                        held.Add(b.id);
                        Press(b.id);
                        return;
                    }
                }
                // 2) 왼쪽 = 조이스틱, 오른쪽 = 시점
                if (p.x < W * 0.42f && !stickActive)
                {
                    owners[finger] = new TouchOwner { kind = "stick" };
                    stickActive = true;
                    stickOrigin = stickPos = p;
                }
                else owners[finger] = new TouchOwner { kind = "look", last = p };
                return;
            }

            if (!owners.TryGetValue(finger, out var o)) return;
            bool ended = phase == TouchPhase.Ended || phase == TouchPhase.Canceled;

            if (o.kind == "stick")
            {
                stickPos = p;
                // 조이스틱 중심이 손가락을 따라온다 (너무 멀어지면)
                Vector2 d = stickPos - stickOrigin;
                if (d.magnitude > StickRadius * 1.4f) stickOrigin = stickPos - d.normalized * StickRadius * 1.4f;
                if (ended) stickActive = false;
            }
            else if (o.kind == "look" || (o.kind == "btn" && o.btn == Id.Fire))
            {
                Vector2 delta = p - o.last;
                GameInput.touchLook += new Vector2(delta.x, -delta.y) * LookGain;
                o.last = p;
            }

            if (ended)
            {
                if (o.kind == "btn") held.Remove(o.btn);
                owners.Remove(finger);
            }
        }

        void Press(Id id)
        {
            switch (id)
            {
                case Id.Fire: GameInput.touchFireDown = true; break;
                case Id.Aim: aimToggle = !aimToggle; break;
                case Id.Reload: GameInput.touchReload = true; break;
                case Id.Jump: GameInput.touchJump = true; break;
                case Id.Swap: GameInput.touchSwap = true; aimToggle = false; break;
                case Id.Grenade: GameInput.touchGrenade = true; break;
                case Id.Melee: GameInput.touchMelee = true; break;
                case Id.Flash: GameInput.touchFlash = true; break;
                case Id.Kit: GameInput.touchKit = true; break;
                case Id.Drink: GameInput.touchDrink = true; break;
                case Id.Rage: GameInput.touchRage = true; break;
                case Id.Regen: GameInput.touchRegen = true; break;
                case Id.Antidote: GameInput.touchAntidote = true; break;
                case Id.Pause: GameInput.touchPause = true; break;
                case Id.Menu: GameInput.touchMenu = true; break;
            }
        }

        // ------------------------------------------------------------------ 그리기
        void OnGUI()
        {
            if (!Active) return;
            GUI.depth = -10; // HUD 위에
            UITheme.Ensure();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            if (buttons.Count == 0) Layout();

            // 조이스틱
            var gm = GameManager.Instance;
            if (gm != null && gm.AcceptsGameplayInput)
            {
                Vector2 o = stickActive ? stickOrigin : new Vector2(250, H - 250);
                float a = stickActive ? 0.55f : 0.22f;
                UIKit.Tex(new Rect(o.x - StickRadius, o.y - StickRadius, StickRadius * 2, StickRadius * 2), UITheme.DiamondHollow, new Color(0.9f, 0.87f, 0.8f, a));
                Vector2 k = stickActive ? stickOrigin + Vector2.ClampMagnitude(stickPos - stickOrigin, StickRadius) : o;
                UIKit.Tex(new Rect(k.x - 46, k.y - 46, 92, 92), UITheme.Diamond, new Color(0.9f, 0.87f, 0.8f, a + 0.1f));
                if (!stickActive) UIKit.Text(new Rect(o.x - 80, o.y + StickRadius + 4, 160, 26), "이동", 18, new Color(0.9f, 0.87f, 0.8f, 0.4f), TextAnchor.MiddleCenter);
            }

            foreach (var b in buttons)
            {
                bool on = held.Contains(b.id) || (b.id == Id.Aim && aimToggle) || (b.id == Id.Fire && GameInput.AutoTarget);
                bool fire = b.id == Id.Fire;
                var r = new Rect(b.c.x - b.r, b.c.y - b.r, b.r * 2, b.r * 2);
                Color fill = fire ? new Color(0.6f, 0.13f, 0.1f, on ? 0.85f : 0.55f) : new Color(0.05f, 0.05f, 0.06f, on ? 0.8f : 0.45f);
                UIKit.Tex(r, UITheme.Circle, fill);
                UIKit.Tex(r, UITheme.DiamondHollow, new Color(0.9f, 0.87f, 0.8f, on ? 0.95f : 0.5f));
                UIKit.Text(r, b.label, Mathf.RoundToInt(Mathf.Clamp(b.r * 0.42f, 16f, 34f)), new Color(0.95f, 0.92f, 0.85f, on ? 1f : 0.8f),
                    TextAnchor.MiddleCenter, serif: fire, bold: true);
            }
            if (GameSettings.AutoFire)
                UIKit.Text(new Rect(W - 330, H - 205, 240, 24), "자동 사격", 15, new Color(0.9f, 0.87f, 0.8f, 0.45f), TextAnchor.MiddleCenter);
        }
    }
}

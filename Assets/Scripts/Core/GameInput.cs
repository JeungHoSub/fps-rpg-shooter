using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 모든 조작 입력의 단일 창구. PC에서는 키보드·마우스, 휴대폰에서는 터치(MobileControls)가 같은 값을 채운다.
    /// 게임 코드는 Input.* 대신 여기만 읽는다.
    /// </summary>
    public static class GameInput
    {
        /// <summary>터치 조작을 쓰는가 (휴대폰, 또는 에디터의 '모바일 조작 미리보기').</summary>
        public static bool Mobile => Application.isMobilePlatform || GameSettings.ForceMobileControls;

        // ---- 터치 조작이 매 프레임 채우는 값 (MobileControls) ----
        internal static Vector2 touchMove, touchLook;
        internal static bool touchFire, touchFireDown, touchAim, touchSprint;
        internal static bool touchReload, touchJump, touchSwap, touchFlash, touchGrenade, touchMelee;
        internal static bool touchKit, touchDrink, touchRage, touchRegen, touchAntidote, touchPause, touchMenu;

        internal static void ClearTouchFrame()
        {
            touchLook = Vector2.zero;
            touchFireDown = touchReload = touchJump = touchSwap = touchFlash = touchGrenade = touchMelee = false;
            touchKit = touchDrink = touchRage = touchRegen = touchAntidote = touchPause = touchMenu = false;
        }

        internal static void ClearTouchHeld()
        {
            touchMove = Vector2.zero;
            touchFire = touchSprint = false;
        }

        /// <summary>자동 사격: 조준점에 망자가 있으면 PlayerWeapons가 매 프레임 켠다.</summary>
        public static bool AutoTarget;

        // ---- 이동 / 시점 ----
        public static float MoveX => Mobile ? touchMove.x : Input.GetAxisRaw("Horizontal");
        public static float MoveY => Mobile ? touchMove.y : Input.GetAxisRaw("Vertical");
        public static float LookX => Mobile ? touchLook.x : Input.GetAxis("Mouse X");
        public static float LookY => Mobile ? touchLook.y : Input.GetAxis("Mouse Y");
        public static bool Sprint => Mobile ? touchSprint : Input.GetKey(KeyCode.LeftShift);
        public static bool JumpDown => Mobile ? touchJump : Input.GetKeyDown(KeyCode.Space);

        // ---- 사격 ----
        public static bool Fire => (Mobile ? touchFire : Input.GetMouseButton(0)) || AutoTarget;
        public static bool FireDown => FireDownRaw || AutoTarget;
        /// <summary>자동 사격을 뺀 실제 '누름' (빈 탄창 딸깍 소리 등).</summary>
        public static bool FireDownRaw => Mobile ? touchFireDown : Input.GetMouseButtonDown(0);
        public static bool Aim => Mobile ? touchAim : Input.GetMouseButton(1);
        public static bool ReloadDown => Mobile ? touchReload : Input.GetKeyDown(KeyCode.R);

        // ---- 무기 ----
        public static bool SlotDown(int i) => !Mobile && Input.GetKeyDown(KeyCode.Alpha1 + i);
        /// <summary>다음 무기(아래로 굴림 = 음수). 휴대폰은 '교체' 버튼.</summary>
        public static float Scroll => Mobile ? (touchSwap ? -1f : 0f) : Input.GetAxis("Mouse ScrollWheel");
        public static bool MeleeDown => Mobile ? touchMelee : Input.GetKeyDown(KeyCode.V);
        public static bool GrenadeDown => Mobile ? touchGrenade : Input.GetKeyDown(KeyCode.G);

        // ---- 도구 / 능력 ----
        public static bool FlashlightDown => Mobile ? touchFlash : Input.GetKeyDown(KeyCode.F);
        public static bool KitDown => Mobile ? touchKit : Input.GetKeyDown(KeyCode.X);
        public static bool DrinkDown => Mobile ? touchDrink : Input.GetKeyDown(KeyCode.C);
        public static bool RageDown => Mobile ? touchRage : Input.GetKeyDown(KeyCode.Q);
        public static bool RegenDown => Mobile ? touchRegen : Input.GetKeyDown(KeyCode.E);
        public static bool AntidoteDown => Mobile ? touchAntidote : Input.GetKeyDown(KeyCode.H);

        // ---- 메뉴 ----
        public static bool PauseDown => Input.GetKeyDown(KeyCode.Escape) || (Mobile && touchPause); // 안드로이드 뒤로가기 = Escape
        public static bool MenuDown => Mobile ? touchMenu : Input.GetKeyDown(KeyCode.Tab);
        public static bool ConfirmDown => !Mobile && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter));
        public static bool RestartDown => !Mobile && Input.GetKeyDown(KeyCode.R);
    }
}

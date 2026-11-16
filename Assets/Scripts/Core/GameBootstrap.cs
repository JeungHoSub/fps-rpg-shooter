using Hunt.UI;
using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 아무 씬에서나 Play를 누르면 게임이 자동으로 구성된다 (씬/프리팹 없이 코드만으로 동작).
    /// 직접 만든 씬에서 자동 구성을 끄려면 씬에 "NoHuntBootstrap" 이름의 오브젝트를 두면 된다.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameObject.Find("NoHuntBootstrap") != null) return;
            if (Object.FindFirstObjectByType<GameManager>() != null) return;

            var go = new GameObject("HUNT_Game");
            go.AddComponent<GameManager>();
            go.AddComponent<HUD>();
        }
    }
}

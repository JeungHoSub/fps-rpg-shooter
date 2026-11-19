using Hunt.Core;
using UnityEngine;

namespace Hunt.Monsters
{
    /// <summary>F9: 망자의 피격 판정체를 반투명 빨강으로 보여준다 (총이 안 맞을 때 원인 확인용).</summary>
    public class HitboxDebug : MonoBehaviour
    {
        public static bool Show;
        Renderer r;

        void Start() { r = GetComponent<Renderer>(); }

        void Update()
        {
            if (r != null && r.enabled != Show) r.enabled = Show;
        }
    }
}

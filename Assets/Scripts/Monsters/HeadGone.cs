using UnityEngine;

namespace Hunt.Monsters
{
    /// <summary>
    /// 머리로 쓰러뜨린 망자: 머리 뼈 크기를 0으로 (애니메이션이 크기를 되돌려도 매 프레임 다시 0).
    /// 머리에 붙은 갓·상투도 같이 사라진다. 피 표현을 끄면 쓰지 않는다 (Monster.Die).
    /// </summary>
    public class HeadGone : MonoBehaviour
    {
        public static void Apply(Transform headBone)
        {
            if (headBone == null || headBone.GetComponent<HeadGone>() != null) return;
            headBone.gameObject.AddComponent<HeadGone>();
            headBone.localScale = Vector3.zero;
        }

        void LateUpdate() { transform.localScale = Vector3.zero; }
    }
}

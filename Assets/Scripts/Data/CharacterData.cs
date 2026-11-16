using Hunt.Stats;
using UnityEngine;

namespace Hunt.Data
{
    /// <summary>
    /// 주인공 기본 데이터. 직업 구분 없이 한 명이며, 빌드는 스탯 투자 + 특성 선택으로 만든다.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunt/Character", fileName = "Character")]
    public class CharacterData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public StatBlock baseStats = new StatBlock();
        public StatBlock growthPerLevel = new StatBlock();
        [Tooltip("시작 화면에서 자유롭게 분배하는 포인트")]
        public int startingStatPoints = 6;
    }
}

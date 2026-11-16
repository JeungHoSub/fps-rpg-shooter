using Hunt.Combat;
using Hunt.Stats;
using UnityEngine;

namespace Hunt.Data
{
    public enum CharacterPassive
    {
        None,
        BruiserBloodlust,   // 처치 시 HP 회복, 넉백 +30%
        ScoutRunAndGun,     // 이동 중 탄퍼짐 페널티 없음
        MarksmanSteady,     // 1초 정지 시 약점 데미지 +50%
    }
    [CreateAssetMenu(menuName = "Hunt/Character", fileName = "Character")]
    public class CharacterData : ScriptableObject
    {
        public string id;
        public string displayName;
        public string role;
        [TextArea] public string description;
        public StatBlock baseStats = new StatBlock();
        public StatBlock growthPerLevel = new StatBlock();
        public CharacterPassive passive;
        [TextArea] public string passiveDescription;
    }
}

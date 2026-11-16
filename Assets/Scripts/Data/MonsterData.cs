using Hunt.Combat;
using Hunt.Stats;
using UnityEngine;

namespace Hunt.Data
{
    public enum MonsterArchetype { Runner, Brute, Spitter, Boss }
    /// <summary>
    /// 괴물 "행동/수치" 데이터. 외형은 MonsterFactory가 담당 → 컨셉이 바뀌어도 이 데이터는 유지.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunt/Monster", fileName = "Monster")]
    public class MonsterData : ScriptableObject
    {
        public string id;
        public string displayName;
        public MonsterArchetype archetype;

        public float maxHp = 100f;
        public float defense = 0f;
        public float moveSpeed = 4f;
        [Range(0, 1)] public float knockbackResist = 0f;

        public float attackDamage = 10f;
        public float attackRange = 1.6f;
        public float attackInterval = 1f;
        public float infectionPerHit = 8f;

        [Tooltip("물리/화염/냉기/전기/독 순서의 기본 내성")]
        public float[] resistances = new float[5];

        public float scale = 1f;
        public Color color = Color.gray;

        public int xp = 10;
        public int gold = 5;
        [Range(0, 1)] public float partDropChance = 0.05f;
    }
}

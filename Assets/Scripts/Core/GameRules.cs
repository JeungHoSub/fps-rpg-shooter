namespace Hunt.Core
{
    /// <summary>이번 밤의 난이도 변형 배율 (WaveManager가 밤마다 정한다). 기본값 1 = 변형 없음.</summary>
    public static class GameRules
    {
        public static float AmmoMult = 1f;    // 탄창·예비탄
        public static float DamageMult = 1f;  // 내가 주는 피해
        public static float HpMult = 1f;      // 최대 체력
        public static float TimeMult = 1f;    // 제한 시간
        public static float MoveMult = 1f;    // 이동 속도

        public static void Reset()
        {
            AmmoMult = DamageMult = HpMult = TimeMult = MoveMult = 1f;
        }
    }
}

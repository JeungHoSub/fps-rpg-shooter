using System.Collections.Generic;
using Hunt.Core;
using Hunt.Data;
using Hunt.Evolution;
using Hunt.Monsters;
using Hunt.Player;
using Hunt.UI;
using UnityEngine;

namespace Hunt.Waves
{
    public enum RoundStage { Normal, Elite, Boss, Cleared }

    /// <summary>밤마다 바뀌는 룰.</summary>
    public enum RoundRule
    {
        Hunt,     // 기본: 기본 → 엘리트 → 보스, 보스 처치 시 클리어
        Defense,  // 디펜스: 제한 시간 동안 지킴터(서낭당 돌무더기)를 지킨다
        Mission,  // 미션: 제한 시간 안에 모든 망자 처치
    }

    /// <summary>난이도 변형 (셋째 밤부터 무작위, 밤이 깊을수록 여러 개).</summary>
    public enum Modifier { LowAmmo, WeakDamage, LowHp, ShortTime, Slow, Dark }

    /// <summary>
    /// 라운드(밤) 기본 룰: 기본 망자 → 엘리트 망자 → 보스 망자. <b>보스를 잡으면 클리어.</b>
    /// 기본 망자는 시작 때 마을 곳곳에 흩어져 서성이고, 소리·빛에 반응해 달려든다.
    /// 일정 시간이 지나면 남은 망자가 모두 냄새를 맡고 몰려온다.
    /// 5번째 밤마다 '대감의 밤' (더 강한 보스).
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public const int BossEvery = 5;

        public static WaveManager Instance { get; private set; }

        public int Wave { get; private set; }
        public bool Spawning { get; private set; }
        public RoundStage Stage { get; private set; }
        public int RemainingToSpawn => queue.Count;
        public int AliveCount => Monster.Alive.Count;
        /// <summary>5번째 밤마다 대감(큰 보스).</summary>
        public bool IsBossWave => Wave > 0 && Wave % BossEvery == 0;
        public Monster Boss { get; private set; }

        public RoundRule Rule { get; private set; }
        public readonly List<Modifier> Modifiers = new List<Modifier>();
        /// <summary>디펜스·미션 남은 시간.</summary>
        public float TimeLeft { get; private set; }
        public float TimeLimit { get; private set; }
        public bool Timed => Rule != RoundRule.Hunt;
        public float DefendHp { get; private set; }
        public const float DefendMaxHp = 300f;
        public Vector3 DefendPos => new Vector3(2.5f, 0f, -3f);
        /// <summary>이번 밤 실패 (보상 없음).</summary>
        public bool Failed { get; private set; }
        GameObject defendMarker;

        public static string RuleName(RoundRule r)
        {
            switch (r)
            {
                case RoundRule.Defense: return "지킴";
                case RoundRule.Mission: return "소탕";
                default: return "사냥";
            }
        }

        public static string RuleGoal(RoundRule r)
        {
            switch (r)
            {
                case RoundRule.Defense: return "날이 밝을 때까지 서낭당 돌무더기를 지켜라";
                case RoundRule.Mission: return "시간 안에 망자를 모두 쓰러뜨려라";
                default: return "기본 망자 → 엘리트 → 보스, 보스를 쓰러뜨려라";
            }
        }

        public static string ModifierName(Modifier m)
        {
            switch (m)
            {
                case Modifier.LowAmmo: return "탄약 부족 (예비탄 -40%)";
                case Modifier.WeakDamage: return "녹슨 총 (피해 -20%)";
                case Modifier.LowHp: return "병든 몸 (최대 생명 -25%)";
                case Modifier.ShortTime: return "짧은 밤 (제한 시간 -25%)";
                case Modifier.Slow: return "진흙탕 (이동 -15%)";
                default: return "칠흑 (손전등 금지)";
            }
        }
        /// <summary>몰려오기까지 남은 시간 (0이면 이미 전부 몰려옴).</summary>
        public float RushIn => Mathf.Max(0f, rushTimer);
        public bool Rushing => Monster.GlobalRush;

        readonly Queue<MonsterArchetype> queue = new Queue<MonsterArchetype>();
        float spawnTimer, stragglerTime, rushTimer, stageClock;
        bool defenseBossSent;
        bool bossSpawned;
        /// <summary>다음 '대감의 그림자'의 성질 (1~4). 밤이 끝날 때 새로 정해 서낭당에서 미리 보여 준다.</summary>
        public int NextBossAffix { get; private set; } // 0이면 StartWave에서 정한다 (필드 초기화에서 Random 금지)
        float drumClock;
        int drumStep;
        Vector3 bossLastPos;
        Transform root;
        PlayerCharacter player;

        void Awake() { Instance = this; }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Monster.GlobalRush = false;
            Monster.DefendTarget = null;
            GameRules.Reset();
            Flashlight.Disabled = false;
        }

        public void Setup(Transform worldRoot, PlayerCharacter p)
        {
            root = worldRoot;
            player = p;
        }

        public static string StageName(RoundStage s)
        {
            switch (s)
            {
                case RoundStage.Normal: return "기본 망자";
                case RoundStage.Elite: return "엘리트 망자";
                case RoundStage.Boss: return "보스 망자";
                default: return "";
            }
        }

        /// <summary>몰려오기까지의 시간: 밤이 깊어질수록 짧아진다.</summary>
        float RushDelay => Mathf.Max(30f, 60f - Wave * 4f);

        public void StartWave(int n)
        {
            Wave = n;
            if (NextBossAffix == 0) NextBossAffix = Random.Range(1, 5);
            Boss = null;
            bossSpawned = false;
            defenseBossSent = false;
            Failed = false;
            Monster.GlobalRush = false;
            Monster.DefendTarget = null;
            ChooseRule();
            if (Atmosphere.Instance != null) Atmosphere.Instance.SetWeather(n, IsBossWave); // 밤마다 날씨가 바뀐다
            BeginStage(RoundStage.Normal);
            Spawning = true;
            if (Rule == RoundRule.Defense) SetupDefense();
            if (Timed) TimeLeft = TimeLimit = (Rule == RoundRule.Defense ? 120f : 150f + n * 4f) * GameRules.TimeMult;

            // 보급 상자 3개 (지난 밤 남은 상자는 치운다)
            SupplyCrate.PlaceForNight(root, player != null ? player.transform.position : Vector3.zero, 3);
            Survivor.PlaceForNight(root, player != null ? player.transform.position : Vector3.zero, n); // 다친 마을 사람 (둘째 밤부터 40%)
            // 셋째 밤부터: 밤마다 한 번 망자 떼가 마을을 가로질러 지나간다 (숨거나 싸우거나)
            hordeDone = n < 3 || Rule == RoundRule.Defense;
            hordeJudged = true; horde.Clear();
            hordeAt = Time.time + Random.Range(35f, 75f);
            if (n <= 2) HUD.Notify("마을 곳곳에 보급 상자 3개 — 노란 빛기둥을 찾아 E로 연다 (아래 나침반에 노란 점)", new Color(1f, 0.85f, 0.45f));

            // 기본 망자 절반은 처음부터 마을 곳곳에 서성이고 있다
            int scatter = Mathf.Min(queue.Count, Mathf.Max(3, QualityManager.MaxAliveMonsters - 2));
            Monster bountyPick = null;
            for (int i = 0; i < scatter; i++)
            {
                var sm = Spawn(queue.Dequeue(), ScatterPoint(), placed: true);
                if (sm != null && (bountyPick == null || Random.value < 0.3f)) bountyPick = sm;
            }
            // 둘째 밤부터: 큰 덤불 속 매복 망자 1~2 (가까이 가면 튀어나온다). 플레이어 15m 안 덤불은 피한다
            if (n >= 2)
            {
                int lurkers = n >= 4 ? 2 : 1, placedL = 0;
                var spots = Hiding.Spots;
                for (int tries = 0; tries < 12 && placedL < lurkers && spots.Count > 0; tries++)
                {
                    var b = spots[Random.Range(0, spots.Count)];
                    var bp = new Vector3(b.x, 0f, b.z);
                    if (player != null && (bp - player.transform.position).sqrMagnitude < 15f * 15f) continue;
                    var lm = Spawn(MonsterArchetype.Runner, bp, placed: true);
                    if (lm == null) continue;
                    lm.Lurk();
                    placedL++;
                }
                if (n == 2 && placedL > 0) HUD.Notify("덤불 속에서 무언가 숨죽이고 있다… (손전등으로 비춰 보라)", new Color(0.85f, 0.75f, 0.6f));
            }
            // 현상금 망자 하나 (머리 위 노란 등불)
            if (bountyPick != null)
            {
                bountyPick.MakeBounty();
                if (n <= 3) HUD.Notify("오늘 밤 현상금 망자가 있다 — 머리 위 노란 등불, 들키면 도망친다. 쫓아가 쓰러뜨리면 엽전과 부적", new Color(1f, 0.85f, 0.45f));
            }
        }

        /// <summary>룰과 난이도 변형을 고른다. 첫째 밤과 5번째 밤(대감)은 사냥.</summary>
        /// <summary>n번째 밤의 룰 (서낭당에서 다음 밤 미리 보기에도 쓴다).</summary>
        public static RoundRule RuleFor(int n)
        {
            if (n == 1 || (n > 0 && n % BossEvery == 0)) return RoundRule.Hunt;
            return n % 3 == 2 ? RoundRule.Defense : n % 3 == 0 ? RoundRule.Mission : RoundRule.Hunt;
        }

        void ChooseRule()
        {
            int n = Wave;
            Rule = RuleFor(n);

            Modifiers.Clear();
            GameRules.Reset();
            Flashlight.Disabled = false;
            int count = n < 3 ? 0 : n < 6 ? 1 : n < 10 ? 2 : 3;
            var pool = new List<Modifier>((Modifier[])System.Enum.GetValues(typeof(Modifier)));
            if (Rule == RoundRule.Hunt) pool.Remove(Modifier.ShortTime);
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                var m = pool[Random.Range(0, pool.Count)];
                pool.Remove(m);
                Modifiers.Add(m);
                switch (m)
                {
                    case Modifier.LowAmmo: GameRules.AmmoMult = 0.6f; break;
                    case Modifier.WeakDamage: GameRules.DamageMult = 0.8f; break;
                    case Modifier.LowHp: GameRules.HpMult = 0.75f; break;
                    case Modifier.ShortTime: GameRules.TimeMult = 0.75f; break;
                    case Modifier.Slow: GameRules.MoveMult = 0.85f; break;
                    case Modifier.Dark: Flashlight.Disabled = true; break;
                }
            }
            if (player != null)
            {
                foreach (var w in player.Arsenal) w.reserve = Mathf.Min(w.reserve, Mathf.RoundToInt(w.MaxReserve * GameRules.AmmoMult));
                player.Heal(0f); // 최대 생명 변화 반영
            }
            HUD.Notify($"오늘 밤 — {RuleName(Rule)}: {RuleGoal(Rule)}", new Color(1f, 0.8f, 0.55f));
            foreach (var m in Modifiers) HUD.Notify("변고 — " + ModifierName(m), new Color(1f, 0.5f, 0.4f));
        }

        /// <summary>보상 배율: 변고 하나마다 +25%.</summary>
        public float RewardMult => 1f + 0.25f * Modifiers.Count;

        void SetupDefense()
        {
            DefendHp = DefendMaxHp;
            Monster.DefendTarget = DefendPos;
            if (defendMarker != null) Destroy(defendMarker);
            defendMarker = new GameObject("지킴터");
            defendMarker.transform.SetParent(root, false);
            defendMarker.transform.position = DefendPos;
            // 서낭당 돌무더기 + 금줄 + 등불
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (i < 5 ? 0.55f : 0f) + Vector3.up * (i < 5 ? 0f : 0.5f);
                if (PropModels.Place("moon_rock_02", defendMarker.transform, p, i * 70f, i < 5 ? 0.55f : 0.5f, false, new Color(0.6f, 0.6f, 0.58f)) == null)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(g.GetComponent<Collider>());
                    g.transform.SetParent(defendMarker.transform, false);
                    g.transform.localPosition = p + Vector3.up * 0.25f;
                    g.transform.localScale = Vector3.one * 0.55f;
                    g.GetComponent<Renderer>().sharedMaterial = EnvMaterials.Get(EnvMaterials.Kind.Rock, Vector2.one);
                }
            }
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pole.GetComponent<Collider>());
            pole.transform.SetParent(defendMarker.transform, false);
            pole.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            pole.transform.localScale = new Vector3(0.08f, 0.6f, 0.08f);
            pole.GetComponent<Renderer>().sharedMaterial = EnvMaterials.Get(EnvMaterials.Kind.WoodPlanks, Vector2.one);
            var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(rope.GetComponent<Collider>());
            rope.transform.SetParent(defendMarker.transform, false);
            rope.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            rope.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f);
            rope.GetComponent<Renderer>().sharedMaterial = EnvMaterials.Plain(new Color(0.75f, 0.68f, 0.5f), 0.2f);
            var lg = new GameObject("등불");
            lg.transform.SetParent(defendMarker.transform, false);
            lg.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.55f, 0.3f);
            l.range = 9f;
            l.intensity = 2.2f;
        }

        public void DamageDefendPoint(float dmg)
        {
            if (Rule != RoundRule.Defense || Stage == RoundStage.Cleared) return;
            float before = DefendHp / DefendMaxHp;
            DefendHp = Mathf.Max(0f, DefendHp - dmg);
            SFX.PlayAt(SFX.FleshHit, DefendPos, 0.5f);
            // 돌무더기가 반·4분의 1 아래로 내려가면 경고 (어디서 공격받는지 모를 때 돌아가라고)
            float now = DefendHp / DefendMaxHp;
            if (before > 0.5f && now <= 0.5f) { HUD.Notify("돌무더기가 반쯤 무너졌다 — 서낭당으로 돌아가라 (나침반 '지')", new Color(1f, 0.55f, 0.35f)); SFX.Play(SFX.Gong, 0.5f, 0f); }
            if (before > 0.25f && now <= 0.25f) { HUD.Notify("돌무더기가 곧 무너진다!", new Color(1f, 0.3f, 0.25f)); SFX.Play(SFX.Gong, 0.7f, 0f); }
            if (DefendHp <= 0f) FailRound("서낭당 돌무더기가 무너졌다");
        }

        void FailRound(string why)
        {
            Failed = true;
            HUD.Notify($"{why} — 이번 밤은 실패. 보상이 없다", new Color(1f, 0.35f, 0.35f));
            ClearRound(GameManager.Instance);
        }

        void BeginStage(RoundStage s)
        {
            Stage = s;
            queue.Clear();
            int n = Wave;
            switch (s)
            {
                case RoundStage.Normal:
                    for (int i = 0; i < 8 + n * 3; i++) queue.Enqueue(MonsterArchetype.Runner);
                    break;
                case RoundStage.Elite:
                {
                    var list = new List<MonsterArchetype>();
                    int brutes = 1 + n / 2;
                    int spitters = 1 + Mathf.Min(6, n / 2);
                    for (int i = 0; i < brutes; i++) list.Add(MonsterArchetype.Brute);
                    for (int i = 0; i < spitters; i++) list.Add(MonsterArchetype.Spitter);
                    for (int i = 0; i < 2 + n; i++) list.Add(MonsterArchetype.Runner);
                    for (int i = list.Count - 1; i > 0; i--)
                    {
                        int j = Random.Range(0, i + 1);
                        (list[i], list[j]) = (list[j], list[i]);
                    }
                    foreach (var a in list) queue.Enqueue(a);
                    HUD.Notify("엘리트 망자가 나타났다 — 장군 망자와 토혈귀", new Color(1f, 0.6f, 0.35f));
                    break;
                }
                case RoundStage.Boss:
                    queue.Enqueue(MonsterArchetype.Boss);
                    for (int i = 0; i < 2 + n / 2; i++) queue.Enqueue(MonsterArchetype.Runner);
                    HUD.Notify(IsBossWave ? "최종 보스 역병 대감이 깨어났다 — 대감을 쓰러뜨려라" : "보스 '대감의 그림자'가 나타났다 — 쓰러뜨리면 밤이 끝난다", new Color(1f, 0.35f, 0.35f));
                    SFX.Play(SFX.Gong, 0.9f, 0f);
                    break;
            }
            spawnTimer = 1f;
            stragglerTime = 0f;
            stageClock = 0f;
            rushTimer = Rule == RoundRule.Mission ? 20f : RushDelay;
            Monster.GlobalRush = false;
        }

        bool hordeDone = true, hordeJudged = true;
        float hordeAt;
        readonly List<Monster> horde = new List<Monster>();

        /// <summary>망자 떼가 나오고 45초 뒤: 한 마리도 나를 알아채지 못했으면 '몰래 보냈다' 보상.</summary>
        void JudgeHorde()
        {
            hordeJudged = true;
            if (horde.Count == 0 || Monster.GlobalRush || player == null || player.IsDead) return;
            foreach (var m in horde) if (m != null && m.EverAlerted) return;
            int gold = 30 + 5 * Wave;
            player.AddGold(gold);
            HUD.Notify($"망자 떼를 들키지 않고 보냈다 — 엽전 +{gold}", new Color(0.75f, 0.9f, 0.65f));
            SFX.PlayPitched(SFX.LevelUp, 0.5f, 0.9f);
        }
        /// <summary>망자 떼가 나온 곳과 때 (나침반에 10초 동안 '떼').</summary>
        public static Vector3 HordeFrom;
        public static float HordeTime = -100f;

        /// <summary>망자 떼: 마을 한쪽 끝에서 나와 반대쪽 끝으로 지나간다 (소리를 쫓듯 뒤지며 걷는다 — 들키면 덮친다).</summary>
        void SpawnHorde()
        {
            float h = VillageBuilder.Half - 5f;
            bool alongX = Random.value < 0.5f;
            float side = Random.value < 0.5f ? -1f : 1f;
            float lane = Random.Range(-h * 0.6f, h * 0.6f);
            Vector3 from = alongX ? new Vector3(-h * side, 0f, lane) : new Vector3(lane, 0f, -h * side);
            Vector3 to = alongX ? new Vector3(h * side, 0f, lane + Random.Range(-8f, 8f)) : new Vector3(lane + Random.Range(-8f, 8f), 0f, h * side);
            horde.Clear(); hordeJudged = false;
            int room = QualityManager.MaxAliveMonsters + 3 - Monster.Alive.Count;
            int count = Mathf.Clamp(Mathf.Min(4 + Wave / 3, room), 2, 6);
            for (int i = 0; i < count; i++)
            {
                Vector2 off = Random.insideUnitCircle * 3f;
                // 여섯째 밤부터 떼를 거구 망자(Brute) 하나가 이끈다
                var arch = i == 0 && Wave >= 6 ? MonsterArchetype.Brute : MonsterArchetype.Runner;
                var m = Spawn(arch, from + new Vector3(off.x, 0f, off.y), placed: true);
                if (m == null) continue;
                horde.Add(m);
                m.HearNoise(to + new Vector3(off.x, 0f, off.y) * 2f, false);
            }
            SFX.PlayPitched(SFX.Drum, 0.6f, 0.6f);
            SFX.PlayAt3D(SFX.ZombieMoan, from, 1f, 0.8f);
            HordeFrom = from; HordeTime = Time.time;
            Crows.Startle(from); // 그쪽 까마귀가 날아오른다
            string dir = Mathf.Abs(from.x) > Mathf.Abs(from.z) ? (from.x > 0 ? "동쪽" : "서쪽") : (from.z > 0 ? "북쪽" : "남쪽");
            HUD.Notify($"{dir}에서 망자 떼가 마을을 가로지른다 — 숨거나, 맞서거나", new Color(1f, 0.55f, 0.4f));
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (!Spawning || gm == null || !gm.IsWorldRunning) return;
            float dt = Time.deltaTime;

            if (!hordeDone && Time.time > hordeAt && !Monster.GlobalRush) { hordeDone = true; SpawnHorde(); }
            if (hordeDone && !hordeJudged && Time.time > HordeTime + 45f) JudgeHorde();

            // 시간이 지나면 모두 몰려온다
            if (!Monster.GlobalRush)
            {
                rushTimer -= dt;
                if (rushTimer <= 0f)
                {
                    Monster.GlobalRush = true;
                    GameHud.RushAt = Time.unscaledTime;                // 화면 가장자리 붉게 한 번
                    SFX.PlayPitched(SFX.Drum, 0.8f, 0.75f);            // 쿵 — 몰려온다
                    HUD.Notify("망자들이 냄새를 맡았다 — 모두 몰려온다", new Color(1f, 0.35f, 0.35f));
                }
            }

            if (Boss != null && !Boss.IsDead)
            {
                bossLastPos = Boss.transform.position;
                // 보스가 살아 있는 동안 북이 울린다: 둥 · 둥 · 둥둥 — 보스 생명이 줄수록 빨라진다
                drumClock -= dt;
                if (drumClock <= 0f)
                {
                    float hp = Mathf.Clamp01(Boss.Hp / Boss.MaxHp);
                    float beat = Mathf.Lerp(0.42f, 0.75f, hp);
                    bool accent = drumStep % 4 == 0;
                    SFX.PlayPitched(SFX.Drum, (accent ? 0.55f : 0.38f) * (IsBossWave ? 1f : 0.8f), accent ? 0.9f : 1.05f);
                    drumStep++;
                    drumClock = drumStep % 4 == 3 ? beat * 0.5f : beat; // 마지막 두 번은 붙여 친다
                }
            }
            else { drumClock = 0.6f; drumStep = 0; }
            spawnTimer -= dt;
            // 보스는 망자 수 제한과 상관없이 바로 나온다 (예전엔 남은 망자가 많으면 안 나올 수 있었다)
            bool bossNext = queue.Count > 0 && queue.Peek() == MonsterArchetype.Boss;
            if (spawnTimer <= 0f && queue.Count > 0 && (bossNext || Monster.Alive.Count < QualityManager.MaxAliveMonsters))
            {
                var a = queue.Dequeue();
                var m = Spawn(a, a == MonsterArchetype.Boss ? BossPoint() : (Vector3?)null, placed: false);
                if (a == MonsterArchetype.Boss && m != null)
                {
                    Boss = m;
                    bossSpawned = true;
                    m.Revealed = true; // 보스 위치는 항상 화면에 표시
                    m.Alert(999f);
                    if (!IsBossWave) m.SetBossAffix(NextBossAffix); // 매 밤 보스는 성질이 하나씩 다르다 (서낭당에서 미리 알려 준 것)
                    if (IsBossWave)
                    {
                        // 최종 보스 대감: 몸에서 붉은 기운이 일렁인다 (멀리서도 '저게 대감'이라고 알게)
                        var aura = new GameObject("대감의 기운").AddComponent<Light>();
                        aura.transform.SetParent(m.transform, false);
                        aura.transform.localPosition = Vector3.up * 1.6f;
                        aura.type = LightType.Point;
                        aura.color = new Color(1f, 0.18f, 0.12f);
                        aura.range = 7f;
                        aura.intensity = 2.2f;
                        aura.shadows = LightShadows.None;
                        aura.gameObject.AddComponent<Hunt.Core.LampFlicker>().pulse = true; // 숨쉬듯 일렁임 ('하'에서도 보스 1개라 부담 적음)
                    }
                }
                spawnTimer = Mathf.Max(0.3f, 1.2f - Wave * 0.05f);
            }

            // 사라진(파괴된) 항목 정리 — 보이지 않는 '남은 망자' 방지
            Monster.Alive.RemoveAll(m => m == null || m.IsDead);

            // 마지막 몇 마리가 멀리서 헤매면: 위치를 표시하고 플레이어 근처로 끌어온다
            if (queue.Count == 0 && Monster.Alive.Count > 0 && Monster.Alive.Count <= 5)
            {
                stragglerTime += dt;
                if (stragglerTime > 8f && player != null)
                {
                    foreach (var m in Monster.Alive)
                    {
                        m.Revealed = true;
                        m.Alert(10f);
                        if (Vector3.Distance(m.transform.position, player.transform.position) > 22f)
                        {
                            var off = Random.insideUnitCircle.normalized * 16f;
                            m.Recall(Navigation.Snap(player.transform.position + new Vector3(off.x, 0f, off.y)));
                        }
                    }
                    stragglerTime = 0f;
                }
            }
            else stragglerTime = 0f;

            // 디펜스·미션: 시간
            if (Timed && Stage != RoundStage.Cleared)
            {
                float prevT = TimeLeft;
                TimeLeft -= dt;
                // 남은 시간 경고: 소탕은 서두르라고, 지킴은 조금만 더 버티라고
                if (prevT > 30f && TimeLeft <= 30f)
                    HUD.Notify(Rule == RoundRule.Mission ? "30초 남았다 — 남은 망자를 서둘러 찾아라 (M 지도)" : "30초만 더 버텨라 — 곧 날이 밝는다", new Color(1f, 0.75f, 0.4f));
                if (prevT > 10f && TimeLeft <= 10f && Rule == RoundRule.Mission) { HUD.Notify("10초!", new Color(1f, 0.35f, 0.3f)); SFX.Play(SFX.Gong, 0.5f, 0f); }
                if (TimeLeft <= 0f)
                {
                    if (Rule == RoundRule.Defense) { HUD.Notify("날이 밝았다 — 끝까지 지켜냈다", new Color(0.5f, 1f, 0.6f)); ClearRound(gm); }
                    else FailRound("시간이 다 됐다");
                    return;
                }
            }

            // 디펜스: 끝날 때까지 계속 밀려온다 / 미션: 정해진 수를 모두 잡으면 클리어
            stageClock += dt;
            if (Rule == RoundRule.Defense)
            {
                // 밤이 절반쯤 지나면 보스가 돌무더기를 노리고 온다. 쓰러뜨리면 바로 클리어
                if (!defenseBossSent && TimeLeft < TimeLimit * 0.55f)
                {
                    defenseBossSent = true;
                    Stage = RoundStage.Boss;
                    var rest = queue.ToArray();
                    queue.Clear();
                    queue.Enqueue(MonsterArchetype.Boss);
                    foreach (var r in rest) queue.Enqueue(r);
                    spawnTimer = 0f;
                    HUD.Notify("보스 '대감의 그림자'가 돌무더기를 노린다 — 쓰러뜨리면 밤이 끝난다", new Color(1f, 0.35f, 0.35f));
                    SFX.Play(SFX.Gong, 0.9f, 0f);
                }
                if (Stage == RoundStage.Boss && Boss != null && Boss.IsDead) { ClearRound(gm); return; }
                if (queue.Count == 0 && Monster.Alive.Count < QualityManager.MaxAliveMonsters - 2)
                    queue.Enqueue(Random.value < 0.15f + Wave * 0.02f ? (Random.value < 0.6f ? MonsterArchetype.Brute : MonsterArchetype.Spitter) : MonsterArchetype.Runner);
                Monster.GlobalRush = true;
                return;
            }
            if (Rule == RoundRule.Mission)
            {
                // 소탕: 기본 → 엘리트 → 보스, 보스까지 모두 잡으면 클리어
                if (Stage == RoundStage.Normal && queue.Count == 0 && Monster.Alive.Count <= 2) BeginStage(RoundStage.Elite);
                else if (Stage == RoundStage.Elite && queue.Count == 0 && (Monster.Alive.Count == 0 || stageClock > 75f)) BeginStage(RoundStage.Boss);
                else if (Stage == RoundStage.Boss && Boss != null && Boss.IsDead) ClearRound(gm);
                else if (Stage == RoundStage.Boss && queue.Count == 0 && Boss == null && stageClock > 5f) ClearRound(gm);
                return;
            }

            switch (Stage)
            {
                case RoundStage.Normal:
                    // 기본 망자를 거의 다 잡으면 엘리트가 온다
                    if (queue.Count == 0 && Monster.Alive.Count <= 2) BeginStage(RoundStage.Elite);
                    break;
                case RoundStage.Elite:
                    // 엘리트를 다 잡거나, 75초가 지나면 (어딘가 끼인 엘리트가 있어도) 보스가 나온다
                    if (queue.Count == 0 && (CountElites() == 0 || stageClock > 75f)) BeginStage(RoundStage.Boss);
                    break;
                case RoundStage.Boss:
                    if (Boss != null && Boss.IsDead) ClearRound(gm);
                    else if (queue.Count == 0 && Boss == null && stageClock > 5f) ClearRound(gm); // 안전장치 (보스 생성 실패)
                    break;
            }
        }

        int CountElites()
        {
            int c = 0;
            foreach (var m in Monster.Alive)
                if (m != null && !m.IsDead && m.Data.archetype != MonsterArchetype.Runner) c++;
            return c;
        }

        /// <summary>보스가 쓰러지면 남은 망자도 함께 무너진다.</summary>
        void ClearRound(GameManager gm)
        {
            // 보스를 쓰러뜨려 끝났나 (보스 오브젝트는 죽으면 곧 사라지므로 '나왔었다' 표시로 판단)
            bool bossKill = bossSpawned && (Boss == null || Boss.IsDead);
            bossSpawned = false;
            Stage = RoundStage.Cleared;
            Spawning = false;
            queue.Clear();
            Monster.GlobalRush = false;
            Monster.DefendTarget = null;
            if (defendMarker != null) Destroy(defendMarker);
            var rest = new List<Monster>(Monster.Alive);
            foreach (var m in rest)
                if (m != null && !m.IsDead) m.Collapse();
            Monster.Alive.Clear();
            Vector3 bossAt = bossLastPos;
            Boss = null;
            NextBossAffix = Random.Range(1, 5); // 다음 밤 보스 성질
            if (bossKill) StartCoroutine(BossFallThenDawn(gm, bossAt));
            else gm.OnWaveCleared();
        }

        static bool toldRevive;

        /// <summary>쓰러진 망자가 4초 뒤 그 자리에서 다시 일어난다 (체력 절반). 그 사이 밤이 끝나면 없던 일.</summary>
        public void Revive(MonsterData d, Vector3 at) { StartCoroutine(ReviveLater(d, at)); }

        System.Collections.IEnumerator ReviveLater(MonsterData d, Vector3 at)
        {
            yield return new WaitForSeconds(4f);
            if (!Spawning || Stage == RoundStage.Cleared) yield break;
            var evo = EvolutionDirector.Instance != null ? EvolutionDirector.Instance.Profile : new EvolutionProfile();
            float hpScale = (1f + 0.12f * (Wave - 1)) * 0.5f * GameSettings.DiffHp;
            var m = MonsterFactory.Create(d, Navigation.Snap(at, 2f), evo, hpScale, player, root);
            if (m == null) yield break;
            m.revenant = true;
            m.MarkRevenantLook(); // 검푸른 살빛 — 한 번 죽었다 일어난 놈
            m.AddDefense(DefenseBonus(MonsterArchetype.Runner));
            m.DamageScale = Mathf.Min(1.6f, 1f + 0.05f * (Wave - 1)) * GameSettings.DiffDamage;
            m.Alert(12f);
            m.Emerge(1.1f); // 땅에서 몸을 일으킨다
            if (!toldRevive) { toldRevive = true; HUD.Notify("쓰러졌던 망자가 다시 일어난다 — 머리를 맞혀 쓰러뜨리면 다시 못 일어난다", new Color(1f, 0.55f, 0.4f)); }
        }

        /// <summary>보스를 쓰러뜨리면: 잠깐 느려지며 쓰러지는 걸 보여 준 뒤(2.5초) 날이 밝는다.</summary>
        System.Collections.IEnumerator BossFallThenDawn(GameManager gm, Vector3 at)
        {
            gm.SlowMo(1.6f, 0.3f);
            if (gm.Player != null) gm.Player.Controller.AddShake(IsBossWave ? 4f : 2.5f);
            FX.BloodBurst(at + Vector3.up * 1.6f, new Color(0.4f, 0.04f, 0.06f), IsBossWave ? 2.4f : 1.6f);
            SFX.Play(SFX.Gong, 1f, 0f);
            HUD.Notify(IsBossWave ? "역병 대감이 쓰러졌다" : "보스가 쓰러졌다 — 남은 망자들이 무너진다", new Color(1f, 0.85f, 0.5f));
            yield return new WaitForSecondsRealtime(2.5f);
            if (gm != null && gm.State == GameState.Playing) gm.OnWaveCleared(); // 그 사이 죽었으면 넘기지 않는다
        }

        public Monster Spawn(MonsterArchetype a, Vector3? at) => Spawn(a, at, false);

        Monster Spawn(MonsterArchetype a, Vector3? at, bool placed)
        {
            // 기본 망자는 유형(기본·단단·빠름·광폭…)을 섞는다
            var data = a == MonsterArchetype.Runner ? DefaultContent.RandomZombie(Wave) : DefaultContent.Monster(a);
            float hpScale = 1f + 0.12f * (Wave - 1);
            // 매 밤 보스가 나온다: 평소엔 약한 보스, 대감의 밤엔 온전한 대감
            if (a == MonsterArchetype.Boss)
                hpScale = IsBossWave ? 1f + 0.4f * (Wave / BossEvery - 1) : 0.28f + 0.07f * (Wave - 1);
            hpScale *= GameSettings.DiffHp; // Esc 난이도
            var evo = EvolutionDirector.Instance != null ? EvolutionDirector.Instance.Profile : new EvolutionProfile();
            Vector3 pos = Navigation.Snap(at ?? SpawnPoint());
            var m = MonsterFactory.Create(data, pos, evo, hpScale, player, root);
            if (m != null) m.AddDefense(DefenseBonus(a));
            // 밤이 깊을수록 망자도 더 아프게 문다 (밤마다 +5%, 최대 +60%)
            if (m != null) m.DamageScale = Mathf.Min(1.6f, 1f + 0.05f * (Wave - 1)) * GameSettings.DiffDamage;
            if (m != null && !placed && a != MonsterArchetype.Runner) m.Alert(4f); // 엘리트·보스는 찾아서 온다
            // 무덤·논 끝에서 새로 나오는 망자는 땅을 헤치고 기어 나온다 (보스는 더 천천히)
            if (m != null && !placed) m.Emerge(a == MonsterArchetype.Boss ? 2.4f : Random.Range(1.3f, 1.8f));
            return m;
        }

        /// <summary>
        /// 밤마다 오르는 방어도 (한 발 피해에서 그대로 빠진다).
        /// 셋째 밤쯤부터 연사가 빠른 총은 효율이 떨어지고, 한 방이 센 총·관통·폭발·화염이 필요해진다.
        /// </summary>
        public float DefenseBonus(MonsterArchetype a)
        {
            float k = a == MonsterArchetype.Runner ? 1.4f : a == MonsterArchetype.Boss ? 3f : 2.2f;
            // 완만하게 오르다 10번째 밤쯤 멈춘다 (예전엔 끝없이 올라 기본 총·산탄총이 쓸모없어졌다)
            return Mathf.Min(Mathf.Max(0, Wave - 1) * 0.85f, 9f) * k;
        }

        /// <summary>보스: 플레이어에서 22~30m 떨어진 곳 (너무 멀어 찾아 헤매지 않게).</summary>
        Vector3 BossPoint()
        {
            Vector3 c = player != null ? player.transform.position : Vector3.zero;
            for (int i = 0; i < 10; i++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(22f, 30f);
                Vector3 p = c + new Vector3(r.x, 0f, r.y);
                if (Mathf.Abs(p.x) < VillageBuilder.Half - 4f && Mathf.Abs(p.z) < VillageBuilder.Half - 4f) return p;
            }
            return Vector3.zero;
        }

        /// <summary>마을 안 아무 곳 (플레이어와 18~38m 사이 — 너무 멀리 흩어지지 않게).</summary>
        Vector3 ScatterPoint()
        {
            Vector3 p = Vector3.zero;
            for (int i = 0; i < 16; i++)
            {
                p = new Vector3(Random.Range(-38f, 38f), 0f, Random.Range(-38f, 38f));
                if (player == null) break;
                float d = Vector3.Distance(p, player.transform.position);
                if (d > 18f && d < 38f) break;
            }
            return p;
        }

        /// <summary>망자는 무덤·논 끝·마을 어귀·산자락에서 걸어 나온다 (플레이어와 가까운 곳은 피한다).</summary>
        Vector3 SpawnPoint()
        {
            var anchors = VillageBuilder.SpawnAnchors;
            Vector3 p = Vector3.zero;
            for (int i = 0; i < 12; i++)
            {
                if (anchors.Count > 0)
                {
                    var a = anchors[Random.Range(0, anchors.Count)];
                    Vector2 off = Random.insideUnitCircle * 3.5f;
                    p = a + new Vector3(off.x, 0f, off.y);
                }
                else
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    p = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Random.Range(34f, 41f);
                }
                if (player == null || Vector3.Distance(p, player.transform.position) > 20f) break;
            }
            return p;
        }
    }
}

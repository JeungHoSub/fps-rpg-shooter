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

        /// <summary>몰려오기까지 남은 시간 (0이면 이미 전부 몰려옴).</summary>
        public float RushIn => Mathf.Max(0f, rushTimer);
        public bool Rushing => Monster.GlobalRush;

        readonly Queue<MonsterArchetype> queue = new Queue<MonsterArchetype>();
        float spawnTimer, stragglerTime, rushTimer;
        Transform root;
        PlayerCharacter player;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; Monster.GlobalRush = false; }

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
        float RushDelay => Mathf.Max(45f, 100f - Wave * 5f);

        public void StartWave(int n)
        {
            Wave = n;
            Boss = null;
            Monster.GlobalRush = false;
            BeginStage(RoundStage.Normal);
            Spawning = true;

            // 기본 망자 절반은 처음부터 마을 곳곳에 서성이고 있다
            int scatter = Mathf.Min(queue.Count, Mathf.Max(3, QualityManager.MaxAliveMonsters - 2));
            for (int i = 0; i < scatter; i++) Spawn(queue.Dequeue(), ScatterPoint(), placed: true);
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
                    HUD.Notify(IsBossWave ? "역병 대감이 깨어났다 — 대감을 쓰러뜨려라" : "보스 망자가 나타났다 — 쓰러뜨리면 밤이 끝난다", new Color(1f, 0.35f, 0.35f));
                    SFX.Play(SFX.Gong, 0.9f, 0f);
                    break;
            }
            spawnTimer = 1f;
            stragglerTime = 0f;
            rushTimer = RushDelay;
            Monster.GlobalRush = false;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (!Spawning || gm == null || !gm.IsWorldRunning) return;
            float dt = Time.deltaTime;

            // 시간이 지나면 모두 몰려온다
            if (!Monster.GlobalRush)
            {
                rushTimer -= dt;
                if (rushTimer <= 0f)
                {
                    Monster.GlobalRush = true;
                    HUD.Notify("망자들이 냄새를 맡았다 — 모두 몰려온다", new Color(1f, 0.35f, 0.35f));
                }
            }

            spawnTimer -= dt;
            if (spawnTimer <= 0f && queue.Count > 0 && Monster.Alive.Count < QualityManager.MaxAliveMonsters)
            {
                var a = queue.Dequeue();
                var m = Spawn(a, null, placed: false);
                if (a == MonsterArchetype.Boss) Boss = m;
                spawnTimer = Mathf.Max(0.3f, 1.2f - Wave * 0.05f);
            }

            // 사라진(파괴된) 항목 정리 — 보이지 않는 '남은 망자' 방지
            Monster.Alive.RemoveAll(m => m == null || m.IsDead);

            // 마지막 몇 마리가 멀리서 헤매면: 위치를 표시하고 플레이어 근처로 끌어온다
            if (queue.Count == 0 && Monster.Alive.Count > 0 && Monster.Alive.Count <= 3)
            {
                stragglerTime += dt;
                if (stragglerTime > 12f && player != null)
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

            switch (Stage)
            {
                case RoundStage.Normal:
                    // 기본 망자를 거의 다 잡으면 엘리트가 온다
                    if (queue.Count == 0 && Monster.Alive.Count <= 2) BeginStage(RoundStage.Elite);
                    break;
                case RoundStage.Elite:
                    if (queue.Count == 0 && CountElites() == 0) BeginStage(RoundStage.Boss);
                    break;
                case RoundStage.Boss:
                    if (Boss != null && Boss.IsDead) ClearRound(gm);
                    else if (queue.Count == 0 && Boss == null) ClearRound(gm); // 안전장치
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
            Stage = RoundStage.Cleared;
            Spawning = false;
            queue.Clear();
            Monster.GlobalRush = false;
            var rest = new List<Monster>(Monster.Alive);
            foreach (var m in rest)
                if (m != null && !m.IsDead) m.Collapse();
            Monster.Alive.Clear();
            Boss = null;
            gm.OnWaveCleared();
        }

        public Monster Spawn(MonsterArchetype a, Vector3? at) => Spawn(a, at, false);

        Monster Spawn(MonsterArchetype a, Vector3? at, bool placed)
        {
            var data = DefaultContent.Monster(a);
            float hpScale = 1f + 0.12f * (Wave - 1);
            // 매 밤 보스가 나온다: 평소엔 약한 보스, 대감의 밤엔 온전한 대감
            if (a == MonsterArchetype.Boss)
                hpScale = IsBossWave ? 1f + 0.4f * (Wave / BossEvery - 1) : 0.28f + 0.07f * (Wave - 1);
            var evo = EvolutionDirector.Instance != null ? EvolutionDirector.Instance.Profile : new EvolutionProfile();
            Vector3 pos = Navigation.Snap(at ?? SpawnPoint());
            var m = MonsterFactory.Create(data, pos, evo, hpScale, player, root);
            if (m != null && !placed && a != MonsterArchetype.Runner) m.Alert(4f); // 엘리트·보스는 찾아서 온다
            return m;
        }

        /// <summary>마을 안 아무 곳 (플레이어와 20m 이상 떨어진 곳).</summary>
        Vector3 ScatterPoint()
        {
            Vector3 p = Vector3.zero;
            for (int i = 0; i < 16; i++)
            {
                p = new Vector3(Random.Range(-42f, 42f), 0f, Random.Range(-42f, 42f));
                if (player == null || Vector3.Distance(p, player.transform.position) > 20f) break;
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
                    p = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Random.Range(38f, 45f);
                }
                if (player == null || Vector3.Distance(p, player.transform.position) > 20f) break;
            }
            return p;
        }
    }
}

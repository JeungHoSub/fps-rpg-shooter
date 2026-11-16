using System.Collections.Generic;
using Hunt.Core;
using Hunt.Data;
using Hunt.Evolution;
using Hunt.Monsters;
using Hunt.Player;
using UnityEngine;

namespace Hunt.Waves
{
    /// <summary>웨이브 구성 / 스폰. 5웨이브마다 보스.</summary>
    public class WaveManager : MonoBehaviour
    {
        public const int BossEvery = 5;
        const int MaxAlive = 28;

        public static WaveManager Instance { get; private set; }

        public int Wave { get; private set; }
        public bool Spawning { get; private set; }
        public int RemainingToSpawn => queue.Count;
        public int AliveCount => Monster.Alive.Count;
        public bool IsBossWave => Wave > 0 && Wave % BossEvery == 0;

        readonly Queue<MonsterArchetype> queue = new Queue<MonsterArchetype>();
        float spawnTimer;
        Transform root;
        PlayerCharacter player;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Setup(Transform worldRoot, PlayerCharacter p)
        {
            root = worldRoot;
            player = p;
        }

        public void StartWave(int n)
        {
            Wave = n;
            queue.Clear();
            var list = new List<MonsterArchetype>();

            bool boss = n % BossEvery == 0;
            int runners = boss ? 4 + n : 6 + n * 3;
            int brutes = n >= 2 ? 1 + (n - 2) / 2 : 0;
            int spitters = n >= 3 ? Mathf.Min(8, n - 2) : 0;
            if (boss) { brutes /= 2; spitters /= 2; }

            for (int i = 0; i < runners; i++) list.Add(MonsterArchetype.Runner);
            for (int i = 0; i < brutes; i++) list.Add(MonsterArchetype.Brute);
            for (int i = 0; i < spitters; i++) list.Add(MonsterArchetype.Spitter);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            if (boss) list.Insert(Mathf.Min(3, list.Count), MonsterArchetype.Boss);

            foreach (var a in list) queue.Enqueue(a);
            spawnTimer = 1f;
            Spawning = true;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (!Spawning || gm == null || !gm.IsWorldRunning) return;

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f && queue.Count > 0 && Monster.Alive.Count < MaxAlive)
            {
                Spawn(queue.Dequeue(), null);
                spawnTimer = Mathf.Max(0.2f, 0.7f - Wave * 0.03f);
            }

            if (queue.Count == 0 && Monster.Alive.Count == 0)
            {
                Spawning = false;
                gm.OnWaveCleared();
            }
        }

        public Monster Spawn(MonsterArchetype a, Vector3? at)
        {
            var data = DefaultContent.Monster(a);
            float hpScale = 1f + 0.12f * (Wave - 1);
            if (a == MonsterArchetype.Boss) hpScale = 1f + 0.4f * (Wave / BossEvery - 1);
            var evo = EvolutionDirector.Instance != null ? EvolutionDirector.Instance.Profile : new EvolutionProfile();
            Vector3 pos = at ?? SpawnPoint();
            pos.y = 0f;
            return MonsterFactory.Create(data, pos, evo, hpScale, player, root);
        }

        Vector3 SpawnPoint()
        {
            Vector3 p = Vector3.zero;
            for (int i = 0; i < 10; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                p = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Random.Range(38f, 45f);
                if (player == null || Vector3.Distance(p, player.transform.position) > 20f) break;
            }
            return p;
        }
    }
}

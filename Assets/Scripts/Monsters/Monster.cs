using System.Collections.Generic;
using Hunt.Combat;
using Hunt.Core;
using Hunt.Data;
using Hunt.Evolution;
using Hunt.Player;
using Hunt.UI;
using Hunt.Waves;
using UnityEngine;
using UnityEngine.AI;

namespace Hunt.Monsters
{
    public class Monster : MonoBehaviour
    {
        public static readonly List<Monster> Alive = new List<Monster>();

        public MonsterData Data { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsBoss => Data != null && Data.archetype == MonsterArchetype.Boss;
        public int Generation { get; private set; }
        public int BossPhase { get; private set; }

        readonly float[] resist = new float[5];
        float defense;
        float speed;
        float knockbackResist;
        float legSlow = 1f;

        Vector3 knockVel;
        float stagger;
        float attackTimer;
        float spitTimer;

        // 상태이상
        float burnDps, burnTime, poisonDps, poisonTime, slowTime, dotTick;

        PlayerCharacter target;
        Transform head;

        // 길찾기
        readonly NavMeshPath navPath = new NavMeshPath();
        readonly Vector3[] corners = new Vector3[32];
        int cornerCount, cornerIdx;
        float repathTimer;

        // 보스: 활성 약점이 페이즈마다 이동
        readonly List<MonsterPart> bossWeakPoints = new List<MonsterPart>();
        int activeWeak;

        public float Resistance(DamageType t) => resist[(int)t];

        public void Init(MonsterData d, float hpScale, EvolutionProfile evo, PlayerCharacter target)
        {
            Data = d;
            MaxHp = Hp = d.maxHp * hpScale;
            for (int i = 0; i < 5; i++)
                resist[i] = (d.resistances != null && i < d.resistances.Length ? d.resistances[i] : 0f) + evo.resist[i];
            defense = d.defense + evo.defenseBonus;
            speed = d.moveSpeed * (1f + evo.speedBonus) * Random.Range(0.9f, 1.1f);
            knockbackResist = Mathf.Clamp01(d.knockbackResist + evo.knockbackResistBonus);
            Generation = evo.generation;
            this.target = target;
            attackTimer = Random.Range(0.2f, d.attackInterval);
            spitTimer = 3f;
            Alive.Add(this);
        }

        public void SetHead(Transform t) { head = t; }

        public void SetBossWeakPoints(List<MonsterPart> wps)
        {
            bossWeakPoints.Clear();
            bossWeakPoints.AddRange(wps);
            activeWeak = 0;
            RefreshBossWeakPoints();
        }

        void OnDestroy() { Alive.Remove(this); }

        void Update()
        {
            if (IsDead || target == null) return;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsWorldRunning) return;

            float dt = Time.deltaTime;
            TickStatus(dt);
            if (IsDead) return;

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : transform.forward;
            Vector3 pathDir = PathDirection(target.transform.position, dir, dt);

            float phaseSpeed = IsBoss ? 1f + BossPhase * 0.2f : 1f;
            float spd = speed * legSlow * (slowTime > 0f ? 0.55f : 1f) * phaseSpeed;

            Vector3 move = Vector3.zero;
            if (Data.archetype == MonsterArchetype.Spitter)
            {
                if (dist > 12f) move = pathDir;
                else if (dist < 7f) move = -dir;
            }
            else if (dist > Data.attackRange)
            {
                move = pathDir;
            }
            if (stagger > 0f) { stagger -= dt; move = Vector3.zero; }

            // 멀리 있을 땐 가는 방향을, 가까우면 플레이어를 바라본다
            Vector3 faceDir = move.sqrMagnitude > 0.01f && dist > Data.attackRange + 1.5f ? move : dir;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), dt * 8f);

            // 무리 간 분리
            Vector3 sep = Vector3.zero;
            foreach (var o in Alive)
            {
                if (o == this || o.IsDead) continue;
                Vector3 d = transform.position - o.transform.position;
                d.y = 0f;
                float m = d.magnitude;
                float minD = 0.55f * (Data.scale + o.Data.scale);
                if (m < minD && m > 0.001f) sep += d / m * (minD - m);
            }

            Vector3 pos = transform.position + (move * spd + sep * 4f + knockVel) * dt;
            pos.x = Mathf.Clamp(pos.x, -47f, 47f);
            pos.z = Mathf.Clamp(pos.z, -47f, 47f);
            pos.y = 0f;
            transform.position = Navigation.Constrain(transform.position, pos);
            knockVel *= Mathf.Exp(-6f * dt);

            // 공격
            attackTimer -= dt;
            if (attackTimer <= 0f)
            {
                if (Data.archetype == MonsterArchetype.Spitter)
                {
                    if (dist < Data.attackRange) { Spit(); attackTimer = Data.attackInterval; }
                }
                else if (dist <= Data.attackRange + 0.4f)
                {
                    target.TakeDamage(Data.attackDamage, Data.infectionPerHit);
                    attackTimer = Data.attackInterval;
                }
            }

            // 보스 3페이즈: 원거리 공격 추가
            if (IsBoss && BossPhase >= 2)
            {
                spitTimer -= dt;
                if (spitTimer <= 0f && dist > 5f)
                {
                    Spit();
                    spitTimer = 1.6f;
                }
            }
        }

        /// <summary>NavMesh 경로의 다음 코너 방향. 경로가 없거나 끝에 도달하면 직선 방향을 쓴다.</summary>
        Vector3 PathDirection(Vector3 targetPos, Vector3 fallback, float dt)
        {
            if (!Navigation.Ready) return fallback;

            repathTimer -= dt;
            if (repathTimer <= 0f)
            {
                repathTimer = 0.25f + Random.value * 0.2f; // 괴물마다 갱신 타이밍 분산
                cornerCount = Navigation.FindPath(transform.position, targetPos, navPath, corners);
                cornerIdx = 1;
            }

            while (cornerIdx < cornerCount)
            {
                Vector3 d = corners[cornerIdx] - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.35f * 0.35f) return d.normalized;
                cornerIdx++;
            }
            return fallback;
        }

        void Spit()
        {
            Vector3 from = head != null ? head.position + transform.forward * 0.4f : transform.position + Vector3.up * 1.6f;
            MonsterProjectile.Launch(transform.parent, from, target, Data.attackDamage, Data.infectionPerHit);
        }

        /// <summary>플레이어의 탄환이 이 괴물의 부위에 명중했을 때.</summary>
        public DamageResult ApplyHit(MonsterPart part, in DamageInfo info)
        {
            if (IsDead) return default;

            HitZone zone = part.zone;
            HitZone calcZone = zone == HitZone.Armor ? HitZone.Body : zone;
            float dmg = DamageCalculator.Calculate(info, calcZone, resist[(int)info.type], defense, out bool crit);

            var result = new DamageResult { hit = true, crit = crit, zone = zone };
            float hpDmg = dmg;

            if (part.breakable)
            {
                if (part.TakePartDamage(dmg))
                {
                    result.partBroken = true;
                    OnPartBroken(part);
                }
                if (zone == HitZone.Armor) hpDmg = dmg * 0.2f; // 장갑은 대부분 흡수
            }
            else
            {
                part.TakePartDamage(0f); // 피격 플래시
            }

            // 저지력 → 넉백
            Vector3 kdir = info.direction;
            kdir.y = 0f;
            float kb = info.knockback * (1f - knockbackResist);
            knockVel += kdir.normalized * kb * 1.5f;
            if (kb > 3f) stagger = Mathf.Max(stagger, 0.25f);

            ApplyStatus(info.type, hpDmg);
            if (EvolutionDirector.Instance != null)
                EvolutionDirector.Instance.Record(info.type, info.weaponClass, zone, hpDmg);
            DamageNumbers.Spawn(info.hitPoint, hpDmg, crit, zone, info.type);

            result.amount = hpDmg;
            result.killed = TakeRawDamage(hpDmg);

            if (info.type == DamageType.Electric && !result.killed) Chain(hpDmg * 0.3f);
            return result;
        }

        /// <summary>폭발/지속피해/연쇄 등 부위 판정 없는 피해. 처치 시 true.</summary>
        public bool ApplyDirectDamage(float amount, DamageType type, bool showNumber = true)
        {
            if (IsDead) return false;
            amount *= 1f - Mathf.Clamp(resist[(int)type], -1f, 0.9f);
            if (showNumber) DamageNumbers.Spawn(transform.position + Vector3.up * 1.5f * Data.scale, amount, false, HitZone.Body, type);
            return TakeRawDamage(amount);
        }

        bool TakeRawDamage(float amount)
        {
            Hp -= amount;
            if (IsBoss) CheckBossPhase();
            if (Hp <= 0f)
            {
                Die();
                return true;
            }
            return false;
        }

        void OnPartBroken(MonsterPart part)
        {
            if (part.zone == HitZone.Leg)
            {
                legSlow = Mathf.Min(legSlow, 0.45f);
                part.transform.localScale = new Vector3(part.transform.localScale.x, part.transform.localScale.y * 0.5f, part.transform.localScale.z);
                part.SetBaseColor(new Color(0.45f, 0.05f, 0.05f));
                HUD.Notify("다리 파괴! 이동 속도 감소", new Color(1f, 0.7f, 0.3f));
            }
            else if (part.zone == HitZone.Armor)
            {
                part.gameObject.SetActive(false);
                HUD.Notify("장갑 파괴! 약점 노출", new Color(1f, 0.7f, 0.3f));
            }
        }

        void ApplyStatus(DamageType type, float dmg)
        {
            switch (type)
            {
                case DamageType.Fire: burnDps = Mathf.Max(burnDps, dmg * 0.25f); burnTime = 3f; break;
                case DamageType.Poison: poisonDps = Mathf.Max(poisonDps, dmg * 0.3f); poisonTime = 4f; break;
                case DamageType.Ice: slowTime = 1.5f; break;
            }
        }

        void TickStatus(float dt)
        {
            if (slowTime > 0f) slowTime -= dt;
            if (burnTime <= 0f && poisonTime <= 0f) return;

            burnTime -= dt;
            poisonTime -= dt;
            if (burnTime <= 0f) burnDps = 0f;
            if (poisonTime <= 0f) poisonDps = 0f;

            dotTick -= dt;
            if (dotTick > 0f) return;
            dotTick = 0.5f;
            if (burnDps > 0f && ApplyDirectDamage(burnDps * 0.5f, DamageType.Fire)) return;
            if (poisonDps > 0f) ApplyDirectDamage(poisonDps * 0.5f, DamageType.Poison);
        }

        void Chain(float dmg)
        {
            Monster best = null;
            float bestD = 6f;
            foreach (var o in Alive)
            {
                if (o == this || o.IsDead) continue;
                float d = Vector3.Distance(o.transform.position, transform.position);
                if (d < bestD) { bestD = d; best = o; }
            }
            if (best == null) return;
            Effects.Line(transform.position + Vector3.up, best.transform.position + Vector3.up, CombatNames.ColorOf(DamageType.Electric), 0.05f, 0.08f);
            best.ApplyDirectDamage(dmg, DamageType.Electric);
        }

        void CheckBossPhase()
        {
            int phase = Hp < MaxHp * 0.33f ? 2 : Hp < MaxHp * 0.66f ? 1 : 0;
            if (phase <= BossPhase) return;
            BossPhase = phase;
            if (bossWeakPoints.Count > 1)
            {
                activeWeak = (activeWeak + 1) % bossWeakPoints.Count;
                RefreshBossWeakPoints();
            }
            HUD.Notify(phase == 1 ? "보스 2페이즈: 약점이 이동했다! 부하 소환!" : "보스 3페이즈: 광폭화! 산성 투사체!", new Color(1f, 0.35f, 0.35f));
            if (WaveManager.Instance != null)
                for (int i = 0; i < 3 + phase * 2; i++)
                    WaveManager.Instance.Spawn(MonsterArchetype.Runner, transform.position + Random.insideUnitSphere.Flat() * 4f);
        }

        void RefreshBossWeakPoints()
        {
            for (int i = 0; i < bossWeakPoints.Count; i++)
            {
                var wp = bossWeakPoints[i];
                bool on = i == activeWeak;
                var col = wp.GetComponent<Collider>();
                if (col != null) col.enabled = on;
                wp.SetBaseColor(on ? MonsterFactory.WeakPointColor : Data.color * 0.8f);
            }
        }

        void Die()
        {
            if (IsDead) return;
            IsDead = true;
            Alive.Remove(this);
            if (GameManager.Instance != null) GameManager.Instance.OnMonsterKilled(this);
            Destroy(gameObject);
        }
    }

    public static class VectorExt
    {
        public static Vector3 Flat(this Vector3 v) { v.y = 0f; return v; }
    }
}

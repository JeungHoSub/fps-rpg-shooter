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
        float stuckClock;
        Vector3 stuckCheckPos;
        /// <summary>마지막 남은 망자: HUD에 위치 표시.</summary>
        public bool Revealed { get; set; }

        /// <summary>멀리서 헤매는 망자를 지정 위치로 옮긴다.</summary>
        public void Recall(Vector3 pos)
        {
            transform.position = pos;
            knockVel = Vector3.zero;
            repathTimer = 0f;
        }
        /// <summary>밤이 깊어질수록 커지는 공격력 배율 (WaveManager가 정한다).</summary>
        public float DamageScale { get; set; } = 1f;
        /// <summary>목소리 높낮이: 몸집이 클수록 낮고 굵게, 작을수록 높고 날카롭게 (망자마다 조금씩 다르게).</summary>
        float voicePitch = -1f;
        public float VoicePitch
        {
            get
            {
                if (voicePitch < 0f) voicePitch = Mathf.Clamp(1.12f - (Data != null ? Data.scale - 1f : 0f) * 0.55f, 0.6f, 1.25f) * Random.Range(0.92f, 1.08f);
                return voicePitch;
            }
        }
        public bool IsBoss => Data != null && Data.archetype == MonsterArchetype.Boss;
        public int Generation { get; private set; }
        public int BossPhase { get; private set; }

        readonly float[] resist = new float[5];
        float defense;
        float speed;
        float knockbackResist;
        float legSlow = 1f;

        Vector3 knockVel;
        Vector3 lastHitDir;
        float stagger;
        float attackTimer;
        float windup; // 물기 직전 남은 시간 (0이면 물려고 하지 않는 중)
        float spitTimer;

        // 상태이상
        float burnDps, burnTime, poisonDps, poisonTime, slowTime, dotTick;

        PlayerCharacter target;
        Transform head;

        // 감지: 소리를 듣거나 가까이서 보면 쫓아오고, 아니면 소리 난 곳을 찾아 어슬렁거린다
        // 어두우면 가까이 와야 알아챈다. 손전등을 켜면 내 모습이 드러난다.
        const float SightDark = 7f, SightLit = 12f;
        const float BeamRange = 32f, BeamCos = 0.87f;  // 손전등 빛줄기 안 (약 30도)
        const float FlashRange = 26f;                  // 총구 화염 (비가 와도 보인다)
        /// <summary>라운드 제한 시간이 지나 모두 몰려오는 중 (WaveManager가 설정).</summary>
        public static bool GlobalRush;
        /// <summary>디펜스 룰: 지켜야 할 곳. 설정되면 플레이어가 멀리 있을 때 이곳으로 몰려가 부순다.</summary>
        public static Vector3? DefendTarget;
        float alertTime;                  // > 0 이면 플레이어를 쫓는 중
        float senseTimer;
        Vector3 home;
        bool hasInvestigate;
        Vector3 investigatePos, wanderGoal;
        float wanderTimer;

        public bool IsAlerted => alertTime > 0f || IsBoss || GlobalRush;

        public void Alert(float seconds) { alertTime = Mathf.Max(alertTime, seconds); }

        /// <summary>라운드가 오를수록 단단해진다: 한 발마다 깎는 방어도.</summary>
        public float Defense => defense;
        public void AddDefense(float d) { defense += d; }

        /// <summary>보스가 쓰러질 때 함께 무너진다.</summary>
        public void Collapse() { Die(); }

        /// <summary>소리를 들었다. 플레이어가 낸 소리면 잠시 쫓아오고, 그 뒤엔 그 자리를 뒤진다.</summary>
        public void HearNoise(Vector3 pos, bool fromPlayer)
        {
            if (IsDead) return;
            investigatePos = pos;
            hasInvestigate = true;
            if (fromPlayer) alertTime = Mathf.Max(alertTime, 6f);
        }

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
            home = transform.position;
            wanderGoal = home;
            senseTimer = Random.value * 0.25f;
            Alive.Add(this);
        }

        public void SetHead(Transform t) { head = t; }

        ZombieRig rig;
        bool wasAlerted;
        float growlTimer = 1f;
        float stepClock;
        static float lastZombieStep;
        public void SetRig(ZombieRig r) { rig = r; }

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
            if (bountyLamp != null) bountyLamp.localPosition = Vector3.up * (2.35f * Data.scale + Mathf.Sin(Time.time * 3f) * 0.08f);
            if (IsBoss) TickBossAffix(dt, target != null ? Vector3.Distance(transform.position, target.transform.position) : 99f);
            if (IsDead) return;

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : transform.forward;

            if (alertTime > 0f) alertTime -= dt;
            senseTimer -= dt;
            if (senseTimer <= 0f) { senseTimer = 0.25f; Sense(dist); }
            bool alerted = IsAlerted;
            // 처음 발견하면 멈춰 서서 비명을 지른 뒤 달려든다
            if (alerted && !wasAlerted && rig != null && dist > 6f && !IsBoss)
            {
                float t = rig.PlayScream();
                SFX.PlayAt3D(SFX.ZombieScream, transform.position + Vector3.up * 1.6f, 1f, VoicePitch);
                if (t > 0f) stagger = Mathf.Max(stagger, Mathf.Min(t, 1.6f));
                if (Data.id == "z_shriek") Wail();
            }
            wasAlerted = alerted;

            // 가까이 다가온 망자는 그르렁거린다 (위치가 들리는 3D 소리 → 뒤에서 오는 것도 알 수 있음)
            if (alerted && dist < 9f)
            {
                growlTimer -= dt;
                if (growlTimer <= 0f)
                {
                    SFX.PlayAt3D(SFX.Growl, transform.position + Vector3.up * 1.5f, Mathf.Lerp(0.9f, 0.45f, dist / 9f), VoicePitch);
                    growlTimer = Random.Range(2.2f, 4.2f);
                }
            }

            float phaseSpeed = IsBoss ? 1f + BossPhase * 0.2f : 1f;
            float spd = speed * legSlow * (slowTime > 0f ? 0.55f : 1f) * phaseSpeed;

            Vector3 move = Vector3.zero;
            if (!alerted)
            {
                // 모르는 채 서성임: 소리 난 곳을 뒤지거나, 제자리 근처를 어슬렁
                spd *= hasInvestigate ? 0.55f : 0.3f;
                Vector3 goal = hasInvestigate ? investigatePos : wanderGoal;
                wanderTimer -= dt;
                Vector3 g = goal - transform.position;
                g.y = 0f;
                if (g.sqrMagnitude < 2f * 2f || (!hasInvestigate && wanderTimer <= 0f))
                {
                    hasInvestigate = false;
                    // 소리를 뒤진 자리를 새 거처로 삼는다
                    if (g.sqrMagnitude < 2f * 2f && goal == investigatePos) home = investigatePos;
                    Vector2 off = Random.insideUnitCircle * 5f;
                    wanderGoal = home + new Vector3(off.x, 0f, off.y);
                    wanderTimer = Random.Range(5f, 10f);
                    goal = wanderGoal;
                    g = goal - transform.position;
                    g.y = 0f;
                }
                move = PathDirection(goal, g.sqrMagnitude > 0.01f ? g.normalized : transform.forward, dt);
            }
            else if (Data.archetype == MonsterArchetype.Spitter)
            {
                if (dist > 12f) move = PathDirection(target.transform.position, dir, dt);
                else if (dist < 7f) move = -dir;
            }
            else if (DefendTarget.HasValue && dist > 9f)
            {
                // 디펜스: 지킴터로 간다
                Vector3 tp = DefendTarget.Value - transform.position;
                tp.y = 0f;
                if (tp.magnitude > 2.2f) move = PathDirection(DefendTarget.Value, tp.normalized, dt);
                else if (attackTimer <= 0f)
                {
                    if (WaveManager.Instance != null) WaveManager.Instance.DamageDefendPoint(Data.attackDamage * DamageScale * (IsBoss ? 3f : 1f));
                    if (rig != null) rig.PlayBite();
                    attackTimer = Data.attackInterval;
                }
                dir = tp.sqrMagnitude > 0.01f ? tp.normalized : dir;
            }
            else if (dist > Data.attackRange)
            {
                move = PathDirection(target.transform.position, dir, dt);
            }
            if (stagger > 0f) { stagger -= dt; move = Vector3.zero; }
            if (IsBoss && alerted) BossPattern(dt, dist, dir, ref move, ref spd);

            // 멀리 있을 땐 가는 방향을, 가까우면 플레이어를 바라본다
            Vector3 faceDir = move.sqrMagnitude > 0.01f && dist > Data.attackRange + 1.5f ? move : dir;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), dt * 8f);

            if (rig != null)
                rig.SetLocomotion(move.sqrMagnitude > 0.01f ? spd : 0f, legSlow < 1f, alerted && Data.archetype == MonsterArchetype.Runner && Data.moveSpeed > 4f && dist > 3f);

            // 가까이 오는 망자의 발소리 (10m 안, 움직일 때) — 등 뒤에서 다가오는 것도 들린다. 전체에서 0.09초에 한 번까지만
            if (dist < 10f && move.sqrMagnitude > 0.01f && stagger <= 0f)
            {
                stepClock -= dt;
                if (stepClock <= 0f)
                {
                    stepClock = Mathf.Clamp(1.6f / Mathf.Max(0.5f, spd), 0.22f, 0.75f);
                    if (Time.time - lastZombieStep > 0.09f)
                    {
                        lastZombieStep = Time.time;
                        SFX.PlayAt3D(SFX.ZombieStep, transform.position + Vector3.up * 0.1f, Mathf.Lerp(0.75f, 0.25f, dist / 10f) * Mathf.Clamp(Data.scale, 0.8f, 1.6f), VoicePitch * 0.85f);
                    }
                }
            }

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
            float lim = VillageBuilder.Half - 1f;
            pos.x = Mathf.Clamp(pos.x, -lim, lim);
            pos.z = Mathf.Clamp(pos.z, -lim, lim);
            pos.y = 0f;
            transform.position = Navigation.Constrain(transform.position, pos);

            // 끼임 탈출: 움직이려는데 3초 동안 거의 못 갔거나, 벽·물체 속에 있으면 가까운 빈 곳으로
            stuckClock += dt;
            if (stuckClock >= 3f)
            {
                bool wantsToMove = move.sqrMagnitude > 0.01f && stagger <= 0f;
                float moved = Vector3.Distance(transform.position, stuckCheckPos);
                if ((wantsToMove && moved < 0.4f) || Navigation.IsBlocked(transform.position))
                {
                    Vector3 toward = target != null ? (target.transform.position - transform.position).normalized * 2.5f : Vector3.zero;
                    transform.position = Navigation.GroundPoint(transform.position + toward, 10f);
                    knockVel = Vector3.zero;
                    repathTimer = 0f;
                }
                stuckCheckPos = transform.position;
                stuckClock = 0f;
            }
            knockVel *= Mathf.Exp(-6f * dt);

            // 공격
            attackTimer -= dt;
            if (windup > 0f)
            {
                // 물기 직전: 세게 맞아 휘청이면 공격이 끊긴다 (쏴서 막는 맛)
                if (stagger > 0.15f) { windup = 0f; attackTimer = 0.35f; }
                else
                {
                    windup -= dt;
                    if (windup <= 0f)
                    {
                        // 그 사이 물러났으면 헛물기
                        if (dist <= Data.attackRange + 0.9f)
                        {
                            target.LastAttacker = Data.displayName;
                            target.TakeDamage(Data.attackDamage * DamageScale, Data.infectionPerHit, transform.position);
                        }
                        else SFX.PlayAt3D(SFX.Swing, transform.position + Vector3.up * 1.3f, 0.5f, 0.8f);
                        attackTimer = Data.attackInterval;
                    }
                }
            }
            else if (attackTimer <= 0f && alerted && bossState == 0)
            {
                if (Data.archetype == MonsterArchetype.Spitter)
                {
                    if (dist < Data.attackRange) { Spit(); attackTimer = Data.attackInterval; }
                }
                else if (dist <= Data.attackRange + 0.4f)
                {
                    // 물기 동작을 먼저 시작하고, 잠깐 뒤(빠른 유형일수록 짧게) 실제로 문다 — 보고 피할 수 있게
                    float speedUp = Mathf.Clamp(0.9f / Mathf.Max(0.1f, Data.attackInterval), 0.7f, 2.4f);
                    windup = Mathf.Clamp(0.3f / speedUp, 0.14f, 0.32f);
                    if (rig != null) rig.PlayBite(1.3f * speedUp); // 공격이 빠른 유형은 동작도 빠르게
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

        /// <summary>시야·빛 감지 (0.25초마다). 소리는 Noise가 따로 알려준다.</summary>
        void Sense(float dist)
        {
            bool lit = Flashlight.Active || ShootableLamp.PlayerLit(); // 손전등을 켰거나 가로등 불빛 아래면 멀리서 보인다
            // 앉아 있으면 어둠 속에서 덜 눈에 띈다
            bool low = target != null && target.Controller != null && target.Controller.IsCrouching;
            if (dist < (lit ? SightLit : SightDark) * (low ? 0.7f : 1f)) { Alert(3f); return; }
            if (IsAlerted && alertTime > 1f) return;

            var eye = Flashlight.Eye;
            if (eye == null) return;
            Vector3 me = transform.position + Vector3.up * 1.5f * Data.scale;

            // 손전등 빛줄기에 비치면 빛을 따라 온다
            if (lit && dist < BeamRange)
            {
                Vector3 toMe = (me - eye.position).normalized;
                if (Vector3.Dot(eye.forward, toMe) > BeamCos && CanSee(eye.position, me)) { Alert(6f); return; }
            }
            // 손전등이 켜져 있으면 멀리서도 불빛이 보인다 (빛줄기 밖이어도 절반 거리)
            if (lit && dist < BeamRange * 0.5f && CanSee(eye.position, me)) { Alert(5f); return; }
            // 총구 화염
            if (Time.time - Noise.LastFlashTime < 0.3f && dist < FlashRange && CanSee(eye.position, me)) Alert(6f);
        }

        static bool CanSee(Vector3 from, Vector3 to)
        {
            if (!Physics.Linecast(from, to, out RaycastHit hit, ~(1 << 2), QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.GetComponent<MonsterPart>() != null; // 망자 몸에 막힌 건 시야로 친다
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
            if (rig != null) rig.PlayBite();
            Vector3 from = head != null ? head.position + transform.forward * 0.4f : transform.position + Vector3.up * 1.6f;
            MonsterProjectile.Launch(transform.parent, from, target, Data.attackDamage * DamageScale, Data.infectionPerHit);
        }

        /// <summary>플레이어의 탄환이 이 괴물의 부위에 명중했을 때.</summary>
        public DamageResult ApplyHit(MonsterPart part, in DamageInfo info)
        {
            if (IsDead) return default;

            HitZone zone = part.zone;
            lastZone = zone; directKill = false;
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

            // 맞은 방향 + 세기(길이 1~3): 산탄총·센 한 방으로 죽으면 시체가 더 멀리 날아간다
            lastHitDir = info.direction.normalized * (1f + Mathf.Clamp(info.knockback * (1f - knockbackResist) / 3f, 0f, 2f));
            alertTime = Mathf.Max(alertTime, 8f); // 맞으면 확실히 알아챈다

            // 저지력 → 넉백
            Vector3 kdir = info.direction;
            kdir.y = 0f;
            float kb = info.knockback * (1f - knockbackResist);
            // 타격감: 맞을 때마다 확실히 뒤로 밀린다 (무거운 놈은 덜)
            knockVel += kdir.normalized * (kb * 2.2f + 0.5f * (1f - knockbackResist));
            if (knockVel.magnitude > 9f) knockVel = knockVel.normalized * 9f;
            if (kb > 1.2f) stagger = Mathf.Max(stagger, Mathf.Min(0.35f, 0.08f + kb * 0.05f));
            if (rig != null)
            {
                rig.Flinch(info.direction, Mathf.Clamp01(kb / 4f + (crit ? 0.3f : 0f) + (zone == HitZone.WeakPoint ? 0.2f : 0f)), zone == HitZone.Head);
                if (kb > 2f || crit || zone == HitZone.Head) rig.PlayHit();
            }

            ApplyStatus(info.type, hpDmg);
            if (EvolutionDirector.Instance != null)
                EvolutionDirector.Instance.Record(info.type, info.weaponClass, zone, hpDmg);
            DamageNumbers.Spawn(info.hitPoint, hpDmg, crit, zone, info.type);

            result.amount = hpDmg;
            result.killed = TakeRawDamage(hpDmg);

            if (info.type == DamageType.Electric && !result.killed) Chain(hpDmg * 0.3f);
            return result;
        }

        /// <summary>땅속에서 기어 나오기 (그동안 제자리). 모델이 있는 망자만.</summary>
        public void Emerge(float seconds)
        {
            if (rig == null || rig.Pivot == null) return;
            stagger = Mathf.Max(stagger, seconds);
            RiseFromGround.Begin(rig.Pivot, seconds, 1.7f * Data.scale);
            SFX.PlayAt3D(SFX.ZombieMoan, transform.position, 0.7f, VoicePitch);
        }

        /// <summary>터지는 망자: 죽으면 누런 고름이 터져 3.2m 안의 망자·플레이어를 다치게 한다 (일부러 무리 속에서 터뜨리는 맛).</summary>
        void BloatBurst()
        {
            Vector3 at = transform.position + Vector3.up * 1.0f * Data.scale;
            const float radius = 3.2f;
            ExplosionFx.Spawn(at, 1.6f, new Color(0.7f, 0.85f, 0.25f));
            FX.BloodBurst(at, new Color(0.55f, 0.6f, 0.15f), 1.8f);
            SFX.PlayAt(SFX.Explosion, at, 0.6f);
            SFX.PlayAt(SFX.FleshHit, at, 1f);
            Noise.Emit(at, 25f, true);
            float baseDmg = 55f + Data.maxHp * 0.6f;
            int burstKills = 0;
            foreach (var m in Alive.ToArray())
            {
                if (m == null || m == this || m.IsDead) continue;
                float d = Vector3.Distance(at, m.transform.position + Vector3.up);
                if (d > radius) continue;
                float k = Mathf.Lerp(1f, 0.4f, d / radius);
                if (m.ApplyDirectDamage(baseDmg * k, DamageType.Physical)) burstKills++;
                m.Blast(at, k * 0.8f);
            }
            if (burstKills >= 5) Achievements.Unlock("bloat5");
            var p = target;
            if (p != null && !p.IsDead)
            {
                float d = Vector3.Distance(at, p.transform.position + Vector3.up);
                if (d < radius)
                {
                    float k = 1f - d / radius;
                    p.LastAttacker = Data.displayName;
                    p.TakeDamage(Data.attackDamage * DamageScale * (0.6f + 1.4f * k), Data.infectionPerHit * 2f, at);
                    p.Controller.AddShake(2f * k + 0.5f);
                }
            }
        }

        /// <summary>곡하는 망자: 처음 발견하면 길게 곡을 해 28m 안 망자를 모두 깨워 플레이어 쪽으로 부른다.</summary>
        void Wail()
        {
            stagger = Mathf.Max(stagger, 2.2f); // 곡하는 동안은 멈춰 있다 — 그 사이 잡으면 된다
            SFX.PlayAt3D(SFX.ZombieScream, transform.position + Vector3.up * 1.6f, 1f, VoicePitch * 0.75f);
            SFX.PlayAt3D(SFX.ZombieMoan, transform.position + Vector3.up * 1.6f, 1f, VoicePitch * 0.6f);
            int called = 0;
            foreach (var m in Alive)
            {
                if (m == null || m == this || m.IsDead) continue;
                if ((m.transform.position - transform.position).sqrMagnitude > 28f * 28f) continue;
                m.Alert(20f);
                called++;
            }
            Noise.Emit(transform.position, 28f, true);
            if (called > 0) HUD.Notify($"곡하는 망자가 울부짖는다 — 망자 {called}이(가) 몰려온다", new Color(1f, 0.55f, 0.4f));
        }

        // ---- 현상금 망자: 머리 위에 노란 등불, 쓰러뜨리면 엽전·치유 부적 ----
        public bool IsBounty { get; private set; }
        Transform bountyLamp;

        public void MakeBounty()
        {
            IsBounty = true;
            var l = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(l.GetComponent<Collider>());
            l.layer = 2;
            l.transform.SetParent(transform, false);
            l.transform.localPosition = Vector3.up * 2.35f * Data.scale;
            l.transform.localScale = Vector3.one * 0.22f;
            var r = l.GetComponent<Renderer>();
            r.sharedMaterial = FX.UnlitMaterial(new Color(1f, 0.82f, 0.3f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bountyLamp = l.transform;
            if (QualityManager.Level != GfxLevel.Low)
            {
                var lt = l.AddComponent<Light>();
                lt.type = LightType.Point; lt.color = new Color(1f, 0.75f, 0.3f); lt.range = 4f; lt.intensity = 1.6f; lt.shadows = LightShadows.None;
            }
        }

        // ---- 매 밤 보스('대감의 그림자')의 성질: 0 없음 · 1 날랜 · 2 단단한 · 3 부르는 · 4 불타는 ----
        public int BossAffix { get; private set; }
        float affixClock;
        public static string AffixName(int a) => a == 1 ? "날랜" : a == 2 ? "단단한" : a == 3 ? "부르는" : a == 4 ? "불타는" : "";
        public static string AffixDesc(int a) => a == 1 ? "아주 빠르게 쫓아온다" : a == 2 ? "체력이 훨씬 많다" : a == 3 ? "15초마다 망자 둘을 땅에서 불러낸다" : a == 4 ? "몸에 불이 붙어 가까이 있으면 뜨겁다 (불에 안 탄다)" : "";

        public void SetBossAffix(int a)
        {
            BossAffix = a;
            if (a == 1) speed *= 1.45f;
            if (a == 2) { MaxHp *= 1.6f; Hp = MaxHp; }
            if (a == 4) { burnTime = 9999f; burnDps = 0f; } // 몸에 불꽃 (피해 없이 보이기만)
            affixClock = 8f;
        }

        void TickBossAffix(float dt, float dist)
        {
            if (BossAffix == 0 || !IsAlerted) return;
            affixClock -= dt;
            if (BossAffix == 3 && affixClock <= 0f)
            {
                affixClock = 15f;
                if (WaveManager.Instance != null)
                    for (int i = 0; i < 2; i++) WaveManager.Instance.Spawn(MonsterArchetype.Runner, transform.position + Random.insideUnitSphere.Flat() * 4f);
                SFX.PlayAt3D(SFX.ZombieScream, transform.position + Vector3.up * 2f, 1f, VoicePitch * 0.7f);
            }
            if (BossAffix == 4 && affixClock <= 0f)
            {
                affixClock = 0.5f;
                if (target != null && !target.IsDead && dist < 3.2f) { target.LastAttacker = "불타는 대감의 그림자"; target.TakeDamage(4f * DamageScale, 0f, transform.position); }
            }
        }

        bool hatGone;
        HitZone lastZone = HitZone.Body;
        bool directKill;
        /// <summary>한 번 다시 일어난 망자 (두 번은 안 일어난다).</summary>
        public bool revenant;

        /// <summary>머리를 맞으면 갓·상투 장식이 총알 방향으로 날아간다 (한 번만).</summary>
        public void KnockOffHat(Vector3 dir)
        {
            if (hatGone || rig == null || rig.Head == null) return;
            var pieces = new System.Collections.Generic.List<Transform>();
            foreach (Transform c in rig.Head.GetComponentsInChildren<Transform>(true))
                if (c != rig.Head && c.name == "Hat") pieces.Add(c);
            if (pieces.Count == 0) return;
            hatGone = true;
            Vector3 center = Vector3.zero;
            foreach (var t in pieces) center += t.position;
            center /= pieces.Count;
            var holder = new GameObject("날아간 갓").transform;
            holder.SetParent(transform.parent, true);
            holder.position = center;
            foreach (var t in pieces) t.SetParent(holder, true);
            dir.y = 0f;
            holder.gameObject.AddComponent<HatFly>().Launch(dir.normalized * Random.Range(2.5f, 4f) + Vector3.up * Random.Range(3.5f, 5f));
        }

        /// <summary>폭발 등으로 밀려남: 가까울수록 세게, 무거운 놈(보스·거구)은 덜. strength 0~1.</summary>
        public void Blast(Vector3 from, float strength)
        {
            if (IsDead) return;
            Vector3 dir = transform.position - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;
            float k = Mathf.Clamp01(strength) * (1f - knockbackResist) * (IsBoss ? 0.3f : 1f);
            knockVel += dir.normalized * 9f * k;
            if (knockVel.magnitude > 11f) knockVel = knockVel.normalized * 11f;
            stagger = Mathf.Max(stagger, 0.45f * k);
            lastHitDir = dir.normalized * (1f + 2f * Mathf.Clamp01(strength)); // 폭발: 세게 날아간다
            if (rig != null) rig.Flinch(dir.normalized, Mathf.Clamp01(0.4f + k), false);
        }

        /// <summary>폭발/지속피해/연쇄 등 부위 판정 없는 피해. 처치 시 true.</summary>
        public bool ApplyDirectDamage(float amount, DamageType type, bool showNumber = true)
        {
            if (IsDead) return false;
            directKill = true; // 폭발·불 등으로 죽으면 다시 일어나지 않는다
            amount *= 1f - Mathf.Clamp(resist[(int)type], -1f, 0.9f);
            alertTime = Mathf.Max(alertTime, 8f);
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
                if (rig == null)
                {
                    part.transform.localScale = new Vector3(part.transform.localScale.x, part.transform.localScale.y * 0.5f, part.transform.localScale.z);
                    part.SetBaseColor(new Color(0.45f, 0.05f, 0.05f));
                }
                HUD.Notify("다리를 꺾었다 — 망자가 절뚝인다", new Color(1f, 0.7f, 0.3f));
            }
            else if (part.zone == HitZone.Armor)
            {
                part.gameObject.SetActive(false);
                HUD.Notify("갑주가 부서졌다 — 급소가 드러났다", new Color(1f, 0.7f, 0.3f));
            }
        }

        void ApplyStatus(DamageType type, float dmg)
        {
            switch (type)
            {
                case DamageType.Fire: if (BossAffix == 4) break; burnDps = Mathf.Max(burnDps, dmg * 0.25f); burnTime = Mathf.Max(burnTime, 3f); break; // 불타는 보스는 불에 안 탄다
                case DamageType.Poison: poisonDps = Mathf.Max(poisonDps, dmg * 0.3f); poisonTime = 4f; break;
                case DamageType.Ice: slowTime = 1.5f; break;
            }
        }

        /// <summary>불을 붙인다 (화염병 불바다 등): 3초 동안 타오른다.</summary>
        public void Ignite(float dps) { if (BossAffix == 4) return; burnDps = Mathf.Max(burnDps, dps); burnTime = Mathf.Max(burnTime, 3f); }

        // 타는 동안 몸에 붙는 불꽃 (도형 3개, 처음 탈 때 만든다)
        Transform[] burnFx;

        void UpdateBurnFx()
        {
            bool on = burnTime > 0f && !IsDead;
            if (!on && burnFx == null) return;
            if (burnFx == null)
            {
                burnFx = new Transform[3];
                for (int i = 0; i < 3; i++)
                {
                    var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(f.GetComponent<Collider>());
                    f.layer = 2;
                    f.transform.SetParent(transform, false);
                    f.transform.localPosition = new Vector3((i - 1) * 0.18f, (0.7f + i * 0.35f) * Data.scale, 0.05f);
                    var r = f.GetComponent<Renderer>();
                    r.sharedMaterial = FX.UnlitMaterial(i == 1 ? new Color(1f, 0.8f, 0.3f, 0.7f) : new Color(1f, 0.4f, 0.1f, 0.6f));
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    burnFx[i] = f.transform;
                }
            }
            for (int i = 0; i < burnFx.Length; i++)
            {
                if (burnFx[i] == null) continue;
                if (burnFx[i].gameObject.activeSelf != on) burnFx[i].gameObject.SetActive(on);
                if (!on) continue;
                float h = (0.25f + 0.3f * Mathf.PerlinNoise(i * 2.3f, Time.time * 5f)) * Data.scale;
                burnFx[i].localScale = new Vector3(0.22f * Data.scale, h, 0.22f * Data.scale);
            }
        }

        void TickStatus(float dt)
        {
            UpdateBurnFx();
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

        // ---- 보스 공격 패턴: 돌진 · 땅 내려치기 (예고 동작이 있어 피할 수 있다) ----
        int bossState;          // 0 평소, 1 돌진 준비, 2 돌진 중, 3 내려치기 준비
        float bossT, bossCd = 6f;
        Vector3 chargeDir;
        bool chargeHit;

        void BossPattern(float dt, float dist, Vector3 dir, ref Vector3 move, ref float spd)
        {
            // 매 밤 나오는 약한 보스는 덜 자주·덜 아프게 (5번째 밤 대감만 온전한 힘)
            bool fullBoss = WaveManager.Instance == null || WaveManager.Instance.IsBossWave;
            float pDmg = fullBoss ? 1f : 0.6f;
            switch (bossState)
            {
                case 0:
                    bossCd -= dt;
                    if (bossCd > 0f) return;
                    if (dist < 4.5f)
                    {
                        // 가까우면: 땅 내려치기
                        bossState = 3; bossT = 0.75f;
                        if (rig != null) rig.PlayBite(0.9f);
                        SFX.PlayAt3D(SFX.ZombieScream, transform.position + Vector3.up * 2f, 1f, VoicePitch * 0.8f);
                    }
                    else if (dist > 7f && dist < 28f)
                    {
                        // 멀면: 포효한 뒤 돌진
                        bossState = 1; bossT = 1.0f;
                        if (rig != null) rig.PlayScream();
                        SFX.PlayAt3D(SFX.ZombieScream, transform.position + Vector3.up * 2f, 1f, VoicePitch * 0.75f);
                        HUD.Notify("대감이 몸을 낮춘다 — 돌진이 온다, 옆으로 피하라", new Color(1f, 0.4f, 0.3f));
                    }
                    else bossCd = 1f;
                    return;
                case 1: // 돌진 준비: 멈춰서 플레이어를 노려본다
                    move = Vector3.zero;
                    chargeDir = dir;
                    bossT -= dt;
                    if (bossT <= 0f) { bossState = 2; bossT = 1.3f; chargeHit = false; }
                    return;
                case 2: // 돌진: 정한 방향으로 곧게 (도중에 방향을 못 튼다)
                    move = chargeDir;
                    spd = speed * 3.4f;
                    bossT -= dt;
                    if (!chargeHit && dist < 2.4f * Data.scale * 0.6f + 1f)
                    {
                        chargeHit = true;
                        target.LastAttacker = Data.displayName;
                        target.TakeDamage(Data.attackDamage * DamageScale * 1.4f * pDmg, Data.infectionPerHit, transform.position);
                        target.Controller.Knock(chargeDir * 14f + Vector3.up * 4f);
                        target.Controller.AddShake(3f);
                        bossT = Mathf.Min(bossT, 0.25f);
                    }
                    if (bossT <= 0f) { bossState = 0; bossCd = Random.Range(7f, 11f) - BossPhase * 1.5f + (fullBoss ? 0f : 5f); stagger = 0.8f; }
                    return;
                case 3: // 내려치기 준비 → 둘레 4.5m 충격
                    move = Vector3.zero;
                    bossT -= dt;
                    if (bossT <= 0f)
                    {
                        bossState = 0;
                        bossCd = Random.Range(5f, 8f) - BossPhase + (fullBoss ? 0f : 4f);
                        SFX.PlayAt(SFX.Explosion, transform.position, 0.8f);
                        FX.BloodBurst(transform.position + Vector3.up * 0.2f, new Color(0.3f, 0.25f, 0.2f), 1.6f); // 흙탕물 튀김
                        Vector3 to = target.transform.position - transform.position; to.y = 0f;
                        if (to.magnitude < 4.5f)
                        {
                            target.LastAttacker = Data.displayName;
                            target.TakeDamage(Data.attackDamage * DamageScale * 1.2f * pDmg, Data.infectionPerHit, transform.position);
                            target.Controller.Knock(to.normalized * 10f + Vector3.up * 3f);
                        }
                        target.Controller.AddShake(Mathf.Lerp(3f, 0.8f, to.magnitude / 15f));
                    }
                    return;
            }
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
            HUD.Notify(phase == 1 ? "대감이 울부짖는다 — 급소가 옮겨가고 망자들이 몰려온다" : "대감이 미쳐 날뛴다 — 검은 피를 토한다", new Color(1f, 0.35f, 0.35f));
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
            FX.BloodBurst(transform.position + Vector3.up * Data.scale, new Color(0.4f, 0.05f, 0.08f), Data.scale);
            if (Data.id == "z_bloat") BloatBurst();
            FX.BloodPool(transform.position, Data.scale); // 쓰러진 자리에 피가 번진다
            // 머리를 맞히지 않고 쓰러뜨린 평범한 망자는 가끔(12%) 4초 뒤 다시 일어난다 — "망자는 머리를 날려야 눕는다"
            if (!IsBoss && !directKill && lastZone != HitZone.Head && Data.archetype == MonsterArchetype.Runner && !revenant
                && (Data.id == "z_basic" || Data.id == "z_tank" || Data.id == "z_gnaw") && Random.value < 0.12f && WaveManager.Instance != null)
                WaveManager.Instance.Revive(Data, transform.position);
            if (rig != null && rig.Pivot != null)
            {
                // 판정체·급소 제거 후 그 자세로 쓰러진다
                foreach (var part in rig.Pivot.GetComponentsInChildren<MonsterPart>(true))
                {
                    var c = part.GetComponent<Collider>();
                    if (c != null) c.enabled = false;
                    part.gameObject.layer = 2;
                    if (part.zone == HitZone.WeakPoint) part.gameObject.SetActive(false);
                    Destroy(part);
                }
                rig.Die(lastHitDir, transform.parent);
            }
            else MonsterGib.Shatter(this, lastHitDir);
            Destroy(gameObject);
        }
    }

    public static class VectorExt
    {
        public static Vector3 Flat(this Vector3 v) { v.y = 0f; return v; }
    }
}

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
                if (voicePitch < 0f)
                {
                    voicePitch = Mathf.Clamp(1.12f - (Data != null ? Data.scale - 1f : 0f) * 0.55f, 0.6f, 1.25f) * Random.Range(0.92f, 1.08f);
                    // 유형마다 목소리 색: 터지는 망자는 꾸르륵 낮게, 곡하는 망자는 가늘고 높게, 날랜·미친 망자는 조금 높게
                    string id = Data != null ? Data.id : "";
                    if (id == "z_bloat") voicePitch *= 0.72f;
                    else if (id == "z_shriek") voicePitch *= 1.3f;
                    else if (id == "z_fast" || id == "z_frenzy") voicePitch *= 1.1f;
                    else if (id == "z_butcher" || id == "z_wall") voicePitch *= 0.85f;
                }
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
        /// <summary>다리가 꺾여 절뚝이는 중 (칼 마무리 대상).</summary>
        public bool Crippled => legSlow < 1f;

        Vector3 knockVel;
        Vector3 lastHitDir;
        float stagger;
        float attackTimer;
        float windup; // 물기 직전 남은 시간 (0이면 물려고 하지 않는 중)
        float spitTimer;

        // 상태이상
        float burnDps, burnTime, poisonDps, poisonTime, slowTime, dotTick;
        float hobble; // 다리에 맞으면 잠깐 휘청 (보스는 짧게)

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
            // 돌·폭죽·사이렌 소리에 고개를 돌린다: 머리 위 '?' (꾀기가 먹혔다는 표시, 3초에 한 번)
            if (!fromPlayer && !IsAlerted && !lurking && Time.time - LostAt > 3f) LostAt = Time.time;
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

            // 매복: 덤불 속 땅바닥에 엎드려 있다가 4.5m 안에 오면 튀어나온다
            if (lurking)
            {
                if (dist < 4.5f || GlobalRush || Revealed || hpLostWhileLurking) WakeLurk();
                else
                {
                    // 12m 안에 오면 가끔 덤불이 미세하게 들썩인다 (손전등 없이도 눈치챌 수 있는 실마리)
                    lurkTwitch -= dt;
                    if (lurkTwitch <= 0f) { lurkTwitch = Random.Range(2.5f, 5f); if (dist < 12f && Random.value < 0.4f) Hiding.Shake(transform.position); }
                    // 손전등으로 비추면 덤불 속에서 붉은 눈 두 개가 번뜩인다 (15m 안, 빛줄기 안)
                    bool lit = false;
                    var eye = Flashlight.Eye;
                    if (Flashlight.Active && eye != null && dist < 15f)
                    {
                        Vector3 toMe = (transform.position + Vector3.up * 0.7f - eye.position).normalized;
                        lit = Vector3.Dot(eye.forward, toMe) > 0.9f;
                    }
                    if (!lit && Atmosphere.Flash > 0.5f && dist < 22f) lit = true; // 번개가 번쩍이면 덤불 속 눈이 순간 비친다
                    if (lurkEyes != null)
                    {
                        if (lurkEyes.activeSelf != lit) lurkEyes.SetActive(lit);
                        if (lit && !eyesGrowled) { eyesGrowled = true; SFX.PlayAt3D(SFX.Growl, transform.position + Vector3.up * 0.6f, 0.5f, VoicePitch * 0.8f); } // 처음 비췄을 때 낮게 으르렁
                        // 잎에 가리지 않게 덤불 바깥쪽(플레이어 쪽) 가장자리에 두고 플레이어를 본다
                        if (lit)
                        {
                            lurkEyes.transform.position = transform.position + Vector3.up * 0.75f + dir * 1.0f;
                            lurkEyes.transform.rotation = Quaternion.LookRotation(dir);
                        }
                    }
                    return;
                }
            }

            if (alertTime > 0f) alertTime -= dt;
            senseTimer -= dt;
            if (senseTimer <= 0f)
            {
                senseTimer = 0.25f; Sense(dist);
                // 덤불을 헤치고 지나가면 바스락 (25m 안만) — 어둠 속에서 다가오는 걸 귀로 안다
                bool bush = dist < 25f && Hiding.InBush(transform.position);
                if (bush && !inBush) { SFX.PlayAt3D(SFX.Rustle, transform.position + Vector3.up * 0.6f, 0.7f, Random.Range(0.85f, 1.05f)); Hiding.Shake(transform.position); }
                if (bush && burnTime > 0f) Hiding.Burn(transform.position, 0.5f); // 불붙은 망자가 덤불에 들어가면 덤불도 탄다
                inBush = bush;
            }
            bool alerted = IsAlerted;
            // 처음 발견하면 멈춰 서서 비명을 지른 뒤 달려든다
            if (alerted && !wasAlerted && rig != null && dist > 6f && !IsBoss)
            {
                float t = rig.PlayScream();
                SFX.PlayAt3D(SFX.ZombieScream, transform.position + Vector3.up * 1.6f, 1f, VoicePitch);
                if (t > 0f) stagger = Mathf.Max(stagger, Mathf.Min(t, 1.6f));
                if (Data.id == "z_shriek") Wail();
            }
            if (alerted && !wasAlerted) { SpottedAt = Time.time; EverAlerted = true; } // 알아챈 순간 (머리 위 '!')
            // 놓쳤다: 마지막으로 본 자리로 가서 뒤진다 (머리 위 '?')
            if (!alerted && wasAlerted && target != null)
            {
                investigatePos = target.transform.position; hasInvestigate = true;
                // 덤불에 숨어서 놓쳤다면 정확한 자리 대신 그 언저리(3~5m)를 뒤진다
                if (target.Controller != null && target.Controller.IsCrouching && Hiding.InBush(target.transform.position))
                {
                    Vector2 off = Random.insideUnitCircle.normalized * Random.Range(3f, 5f);
                    investigatePos += new Vector3(off.x, 0f, off.y);
                }
                LostAt = Time.time;
            }
            wasAlerted = alerted;

            // 가까이 다가온 망자는 그르렁거린다 (위치가 들리는 3D 소리 → 뒤에서 오는 것도 알 수 있음)
            if (alerted && dist < 9f)
            {
                growlTimer -= dt;
                if (growlTimer <= 0f)
                {
                    // 벽 너머면 소리가 줄어든다 (어디 있는지 귀로 가늠하게)
                    bool walled = Flashlight.Eye != null && !CanSee(Flashlight.Eye.position, transform.position + Vector3.up * 1.5f * Data.scale);
                    SFX.PlayAt3D(SFX.Growl, transform.position + Vector3.up * 1.5f, Mathf.Lerp(0.9f, 0.45f, dist / 9f) * (walled ? 0.55f : 1f), VoicePitch * (walled ? 0.92f : 1f));
                    growlTimer = Random.Range(2.2f, 4.2f);
                }
            }

            float phaseSpeed = IsBoss ? 1f + BossPhase * 0.2f : 1f;
            float spd = speed * legSlow * (slowTime > 0f ? 0.55f : 1f) * (hobble > 0f ? 0.6f : 1f) * phaseSpeed
                * (!IsBoss && Hiding.InPaddy(transform.position) ? 0.85f : 1f); // 논물에 발이 빠진다

            Vector3 move = Vector3.zero;
            if (!alerted)
            {
                // 뒤지러 온 자리에서 잠깐 멈춰 두리번 (좌우로 고개를 돌린다)
                if (lookAround > 0f && !hasInvestigate)
                {
                    lookAround -= dt;
                    transform.Rotate(0f, Mathf.Sin(lookAround * 2.6f) * 70f * dt, 0f);
                }
                else
                {
                    // 모르는 채 서성임: 소리 난 곳을 뒤지거나, 제자리 근처를 어슬렁
                    spd *= hasInvestigate ? (InHorde ? 0.8f : 0.55f) : 0.3f; // 떼는 바삐 걷는다
                    Vector3 goal = hasInvestigate ? investigatePos : wanderGoal;
                    wanderTimer -= dt;
                    Vector3 g = goal - transform.position;
                    g.y = 0f;
                    if (g.sqrMagnitude < 2f * 2f || (!hasInvestigate && wanderTimer <= 0f))
                    {
                        hasInvestigate = false;
                        // 소리를 뒤진 자리를 새 거처로 삼는다
                        if (g.sqrMagnitude < 2f * 2f && goal == investigatePos)
                        {
                            home = investigatePos;
                            lookAround = Random.Range(1.8f, 3f); // 그 자리에서 두리번거린다
                            // 뒤지러 온 자리에서 킁킁 — 그르렁 (25m 안이면 들린다)
                            if (dist < 25f && Random.value < 0.5f) SFX.PlayAt3D(SFX.ZombieMoan, transform.position + Vector3.up * 1.5f, 0.5f, VoicePitch * 0.95f);
                        }
                        Vector2 off = Random.insideUnitCircle * 5f;
                        wanderGoal = home + new Vector3(off.x, 0f, off.y);
                        wanderTimer = Random.Range(5f, 10f);
                        goal = wanderGoal;
                        g = goal - transform.position;
                        g.y = 0f;
                    }
                    move = PathDirection(goal, g.sqrMagnitude > 0.01f ? g.normalized : transform.forward, dt);
                }
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
            // 현상금 망자는 알아채면 도망친다 (16m 안) — 쫓아가 잡는 맛. 구석에 몰리면(가까우면) 덤빈다
            if (IsBounty && alerted && dist < 16f && dist > 2.5f && target != null)
            {
                Vector3 away = transform.position - target.transform.position; away.y = 0f;
                if (away.sqrMagnitude > 0.01f)
                {
                    Vector3 fleeTo = transform.position + away.normalized * 10f;
                    // 근처(20m)에 나보다 먼 쪽 큰 덤불이 있으면 그리로 숨으러 간다 (등불은 덤불 위로 비친다)
                    if (bountyBushSearch <= 0f)
                    {
                        bountyBushSearch = 1f; bountyBush = null;
                        float bestB = 20f * 20f;
                        foreach (var b in Hiding.Spots)
                        {
                            var bp = new Vector3(b.x, transform.position.y, b.z);
                            float dMe = (bp - transform.position).sqrMagnitude;
                            if (dMe < bestB && (bp - target.transform.position).sqrMagnitude > (transform.position - target.transform.position).sqrMagnitude)
                            { bestB = dMe; bountyBush = bp; }
                        }
                    }
                    bountyBushSearch -= dt;
                    if (bountyBush.HasValue)
                    {
                        fleeTo = bountyBush.Value;
                        if ((fleeTo - transform.position).sqrMagnitude < 1.2f * 1.2f && dist > 9f && rig != null)
                        {
                            alertTime = 0f; Lurk(); // 덤불 속에 엎드렸다 — 가까이 가면 다시 튀어나온다
                            HUD.Notify("현상금 망자가 덤불 속에 숨었다 — 등불을 찾아라", new Color(1f, 0.85f, 0.45f));
                            return;
                        }
                    }
                    move = PathDirection(fleeTo, away.normalized, dt);
                    spd *= 1.15f;
                }
            }
            if (stagger > 0f) { stagger -= dt; move = Vector3.zero; }
            if (IsBoss && alerted) BossPattern(dt, dist, dir, ref move, ref spd);

            // 멀리 있을 땐 가는 방향을, 가까우면 플레이어를 바라본다
            Vector3 faceDir = move.sqrMagnitude > 0.01f && (dist > Data.attackRange + 1.5f || !alerted) ? move : dir; // 모르는 망자는 가까이 가도 제 갈 길을 본다
            faceDir.y = 0f;
            // 나를 모르는 채 멈춰 있을 땐(두리번·비틀) 나를 쳐다보지 않는다 — 등 뒤로 다가갈 수 있게
            bool idleUnaware = !alerted && move.sqrMagnitude <= 0.01f;
            if (faceDir.sqrMagnitude > 0.0001f && !idleUnaware)
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
                        bool wet = Hiding.InPaddy(transform.position); // 논물을 밟으면 더 크게 첨벙
                        bool walledStep = Flashlight.Eye != null && !CanSee(Flashlight.Eye.position, transform.position + Vector3.up * 0.8f); // 벽 너머면 작게
                        SFX.PlayAt3D(SFX.ZombieStep, transform.position + Vector3.up * 0.1f, Mathf.Lerp(0.75f, 0.25f, dist / 10f) * Mathf.Clamp(Data.scale, 0.8f, 1.6f) * (wet ? 1.4f : 1f) * (walledStep ? 0.6f : 1f), VoicePitch * (wet ? 0.75f : 0.85f));
                        if (wet && dist < 20f && QualityManager.Level != GfxLevel.Low && !QualityManager.Emergency) FX.Splash(transform.position, 0.8f); // '하' 품질은 소리만
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
            // 앉아서 덤불 속이면 코앞(어둠 2.5m)에 와야 안다. 손전등을 켜면 소용없다
            bool hidden = low && !lit && Hiding.InBush(target.transform.position);
            // 그림자 망자(추적형)는 냄새로 찾는다: 덤불에 숨어도 앉은 사람 보듯 본다
            if (hidden && Data.id == "z_stalker")
            {
                hidden = false;
                if (dist < 12f && !sniffWarned && !IsAlerted)
                {
                    sniffWarned = true;
                    SFX.PlayAt3D(SFX.Growl, transform.position + Vector3.up * 1.4f, 0.6f, VoicePitch * 0.7f);
                    HUD.Notify("킁킁… 그림자 망자가 냄새를 맡는다 — 덤불도 소용없다", new Color(1f, 0.55f, 0.45f));
                }
            }
            // 어슬렁대는 망자는 등 뒤(110° 밖)를 잘 못 본다 → 몰래 다가가 칼질할 수 있다
            float back = 1f;
            if (!IsAlerted && target != null)
            {
                Vector3 tp = target.transform.position - transform.position; tp.y = 0f;
                if (tp.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, tp) > 110f) back = 0.45f;
            }
            float lowMult = low ? (target.CharmHide ? 0.5f : 0.7f) : 1f; // 은신부: 앉으면 더 안 띈다
            float hideR = hidden && target.Controller.HoldingBreath ? 1.4f : 2.5f; // 숨을 참으면 더 가까이 와야 안다
            float sightThr = Mathf.Max(hidden ? hideR : 2f, (hidden ? hideR : (lit ? SightLit : SightDark) * lowMult * Atmosphere.SightMult) * back);
            // 눈치챌락 말락 (알아채는 거리의 1.6배 안): 머리 위 '…' — 물러설 때를 알게
            Suspicion = IsAlerted ? 0f : Mathf.Clamp01((sightThr * 1.6f - dist) / (sightThr * 0.6f));
            // 쫓던 중이라도 6m 밖에서 내가 덤불에 숨으면 곧 놓친다 (정확한 자리는 모른 채 주변을 뒤진다)
            if (hidden && IsAlerted && !GlobalRush && !IsBoss && dist > 6f && alertTime > 1.5f) alertTime = 1.5f;
            // 벽·집 뒤에 있으면 못 본다 (2.5m 안은 숨소리·기척으로 안다)
            bool blocked = false;
            if (dist < sightThr && dist > 2.5f && Flashlight.Eye != null)
                blocked = !CanSee(Flashlight.Eye.position, transform.position + Vector3.up * 1.5f * Data.scale);
            if (blocked) Suspicion *= 0.5f;
            if (dist < sightThr && !blocked) { Alert(3f); Suspicion = 0f; return; }
            // 쓰러진 동료를 보면 그 자리를 뒤지러 간다 ('?')
            if (!IsAlerted && !hasInvestigate)
                for (int bi = recentBodies.Count - 1; bi >= 0; bi--)
                {
                    var b = recentBodies[bi];
                    if (Time.time - b.w > 25f) { recentBodies.RemoveAt(bi); continue; }
                    if (b.w <= seenBodyAt) continue; // 이미 본 시체는 다시 안 뒤진다
                    var bp = new Vector3(b.x, b.y, b.z);
                    if ((bp - transform.position).sqrMagnitude < 6f * 6f && (bp - transform.position).sqrMagnitude > 1f
                        && CanSee(transform.position + Vector3.up * 1.5f * Data.scale, bp + Vector3.up * 0.4f))
                    {
                        HearNoise(bp, false);
                        seenBodyAt = b.w;
                        break;
                    }
                }
            if (hidden && !IsAlerted)
            {
                if (dist < 3.5f && !passedHidden)
                {
                    passedHidden = true; Achievements.Add("hide10"); target.RunHidden++;
                    SFX.PlayAt3D(SFX.Growl, transform.position + Vector3.up * 1.4f, 0.4f, VoicePitch * 0.8f); // 바로 옆에서 킁킁 — 숨죽여라
                }
                return; // 덤불 속은 시야선으로도 안 보인다
            }
            if (IsAlerted && alertTime > 1f) return;

            var eye = Flashlight.Eye;
            if (eye == null) return;
            Vector3 me = transform.position + Vector3.up * 1.5f * Data.scale;

            // 손전등 빛줄기에 비치면 빛을 따라 온다
            if (lit && dist < BeamRange * Atmosphere.SightMult)
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
                if (part != null && part.transform.parent != null && zone != HitZone.Head)
                    rig.BoneJerk(part.transform.parent, info.direction, Mathf.Clamp01(0.35f + kb / 4f + (crit ? 0.3f : 0f)) * (IsBoss ? 0.4f : 1f)); // 맞은 팔·다리가 홱
                if (kb > 2f || crit || zone == HitZone.Head) rig.PlayHit();
            }

            if (zone == HitZone.Leg) hobble = Mathf.Max(hobble, IsBoss ? 0.2f : 0.55f);
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
                    p.DeathCause = "터진 망자의 고름을 뒤집어쓰고 쓰러짐";
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
            Noise.Emit(transform.position, 28f, false); // 플레이어가 낸 소리가 아니다 (소리 막대·까마귀에 안 잡히게)
            WailRing.Spawn(transform.position, 28f); // 부르는 범위가 바닥에 물결로
            if (called > 0) HUD.Notify($"곡하는 망자가 울부짖는다 — 망자 {called}이(가) 몰려온다", new Color(1f, 0.55f, 0.4f));
        }

        // ---- 현상금 망자: 머리 위에 노란 등불, 쓰러뜨리면 엽전·치유 부적 ----
        public bool IsBounty { get; private set; }
        Transform bountyLamp;

        public void MakeBounty()
        {
            IsBounty = true;
            var l = FX.LowSphere(); // 가벼운 공 (판정 없음)
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
        /// <summary>다시 일어난 망자: 살빛을 검푸르게 (한눈에 알아보게).</summary>
        public void MarkRevenantLook() { if (rig != null) rig.SetTint(new Color(0.45f, 0.5f, 0.62f)); }

        /// <summary>소리 없는 처치 등: 다시 일어나지 않는다.</summary>
        public bool NoRevive;
        /// <summary>나를 알아챈 시각 (HUD가 잠깐 머리 위에 '!'를 띄운다).</summary>
        public float SpottedAt = -100f;
        /// <summary>쫓다가 놓친 순간 (머리 위 '?').</summary>
        public float LostAt = -100f;
        /// <summary>이 망자가 한 번이라도 나를 알아챘나 (망자 떼 몰래 보내기 판정).</summary>
        public bool EverAlerted { get; private set; }
        /// <summary>0~1: 나를 알아채기 직전인 정도 (HUD '…').</summary>
        public float Suspicion { get; private set; }
        bool inBush, passedHidden, sniffWarned;
        float lookAround, bountyBushSearch, seenBodyAt = -1f;
        /// <summary>망자 떼의 일원 (뒤지러 갈 때 더 빨리 걷는다).</summary>
        public bool InHorde;
        Vector3? bountyBush;
        bool lurking, hpLostWhileLurking;
        Vector3 lurkBase;
        GameObject lurkEyes;
        Renderer[] lurkHidden;
        bool eyesGrowled;
        float lurkTwitch = 3f;
        public bool Lurking => lurking;

        /// <summary>덤불 속 매복으로 만든다 (모델이 있는 망자만): 몸을 땅 밑에 숨기고 기다린다.</summary>
        public void Lurk()
        {
            if (rig == null || rig.Pivot == null) return;
            lurking = true;
            lurkBase = rig.Pivot.localPosition;
            float s = Mathf.Max(0.01f, transform.lossyScale.y);
            rig.Pivot.localPosition = lurkBase - Vector3.up * (2.2f * Data.scale / s);
            // 땅 밑에 있는 동안은 그리지 않는다 (피부 변형 계산도 쉰다 — 내장 그래픽 절약)
            lurkHidden = rig.Pivot.GetComponentsInChildren<Renderer>();
            foreach (var r0 in lurkHidden) if (r0 != null) r0.enabled = false;
            // 손전등에만 보이는 눈 (평소엔 꺼 둠)
            lurkEyes = new GameObject("LurkEyes");
            lurkEyes.transform.SetParent(transform, false);
            lurkEyes.transform.localPosition = Vector3.up * 0.75f / s;
            foreach (float sx in new[] { -0.09f, 0.09f })
            {
                var e = FX.LowSphere();
                e.layer = 2;
                e.transform.SetParent(lurkEyes.transform, false);
                e.transform.localPosition = new Vector3(sx / s, 0f, 0f);
                e.transform.localScale = Vector3.one * 0.07f / s;
                var r = e.GetComponent<Renderer>();
                r.sharedMaterial = FX.UnlitMaterial(new Color(1f, 0.15f, 0.08f));
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            lurkEyes.SetActive(false);
        }

        /// <summary>덤불을 건드렸다 (돌·폭죽이 떨어짐): 매복 중이면 다음 프레임에 튀어나온다.</summary>
        public void Disturb() { if (lurking) hpLostWhileLurking = true; }

        void WakeLurk()
        {
            lurking = false;
            if (lurkEyes != null) Destroy(lurkEyes);
            if (lurkHidden != null) { foreach (var r0 in lurkHidden) if (r0 != null) r0.enabled = true; lurkHidden = null; }
            if (rig != null && rig.Pivot != null) rig.Pivot.localPosition = lurkBase;
            Emerge(0.55f); // 덤불 속에서 확 솟구친다
            Hiding.Shake(transform.position);
            Alert(10f);
            SpottedAt = Time.time;
            SFX.PlayAt3D(SFX.ZombieScream, transform.position + Vector3.up * 1.4f, 1f, VoicePitch * 1.05f);
            SFX.PlayAt3D(SFX.Rustle, transform.position + Vector3.up * 0.6f, 1f, 0.8f);
            if (target != null && Vector3.Distance(target.transform.position, transform.position) < 8f)
            {
                HUD.Notify("덤불 속에 망자가 숨어 있었다!", new Color(1f, 0.4f, 0.3f));
                if (target.Controller != null) target.Controller.AddShake(1.2f);
            }
        }

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
        /// <summary>발차기 등으로 잠깐 비틀거리게 (보스는 짧게).</summary>
        public void Stagger(float seconds)
        {
            if (IsDead) return;
            stagger = Mathf.Max(stagger, IsBoss ? seconds * 0.25f : seconds * (1f - knockbackResist * 0.5f));
            if (rig != null) rig.PlayHit();
        }

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
            if (lurking) hpLostWhileLurking = true; // 폭발 등에 맞으면 튀어나온다
            Hp -= amount;
            if (IsBoss) CheckBossPhase();
            if (Hp <= 0f)
            {
                if (lurking)
                {
                    // 튀어나오기 전에 잡았다 (수류탄·화염병으로 덤불째)
                    Achievements.Unlock("spotlurk");
                    HUD.Notify("덤불 속 매복 망자를 먼저 잡았다", new Color(0.75f, 0.9f, 0.6f));
                    lurking = false;
                    if (lurkEyes != null) Destroy(lurkEyes);
                    if (lurkHidden != null) { foreach (var r0 in lurkHidden) if (r0 != null) r0.enabled = true; lurkHidden = null; }
                    if (rig != null && rig.Pivot != null) rig.Pivot.localPosition = lurkBase;
                }
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
                    var f = GameObject.CreatePrimitive(PrimitiveType.Quad); // 불꽃 혀 모양 판
                    Destroy(f.GetComponent<Collider>());
                    f.layer = 2;
                    f.transform.SetParent(transform, false);
                    f.transform.localPosition = new Vector3((i - 1) * 0.18f, (0.7f + i * 0.35f) * Data.scale, 0.05f);
                    var r = f.GetComponent<Renderer>();
                    r.sharedMaterial = FX.FlameMaterial();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    burnFx[i] = f.transform;
                }
            }
            for (int i = 0; i < burnFx.Length; i++)
            {
                if (burnFx[i] == null) continue;
                if (burnFx[i].gameObject.activeSelf != on) burnFx[i].gameObject.SetActive(on);
                if (!on) continue;
                float h = (0.25f + 0.3f * Mathf.PerlinNoise(i * 2.3f, Time.time * 5f)) * Data.scale * 1.7f;
                burnFx[i].localScale = new Vector3(0.36f * Data.scale, h, 1f);
                var cam = GameManager.Instance != null ? GameManager.Instance.ActiveCamera : null;
                if (cam != null)
                {
                    Vector3 to = burnFx[i].position - cam.transform.position; to.y = 0f;
                    if (to.sqrMagnitude > 0.0001f) burnFx[i].rotation = Quaternion.LookRotation(to);
                }
            }
        }

        void TickStatus(float dt)
        {
            UpdateBurnFx();
            if (slowTime > 0f) slowTime -= dt;
            if (hobble > 0f) hobble -= dt;
            if (burnTime <= 0f && poisonTime <= 0f) return;

            burnTime -= dt;
            poisonTime -= dt;
            if (burnTime <= 0f) burnDps = 0f;
            if (poisonTime <= 0f) poisonDps = 0f;

            dotTick -= dt;
            if (dotTick > 0f) return;
            dotTick = 0.5f;
            // 불이 옮겨붙는다: 타는 망자 1.6m 안의 망자에게 (조금 약하게)
            if (burnDps > 0.5f && burnTime > 0.5f && BossAffix != 4)
                foreach (var nb in Alive)
                {
                    if (nb == null || nb == this || nb.IsDead || nb.burnTime > 0f) continue;
                    if ((nb.transform.position - transform.position).sqrMagnitude < 1.6f * 1.6f) { nb.Ignite(burnDps * 0.7f); break; }
                }
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
            if (Atmosphere.Instance != null) Atmosphere.Instance.StrikeNow(); // 성나는 순간 하늘이 갈라진다
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

        // 최근 쓰러진 자리 (25초): 지나가던 망자가 6m 안에서 보면 뒤지러 온다
        static readonly List<Vector4> recentBodies = new List<Vector4>(); // xyz = 자리, w = 쓰러진 때

        void Die()
        {
            if (IsDead) return;
            IsDead = true;
            if (recentBodies.Count >= 12) recentBodies.RemoveAt(0);
            recentBodies.Add(new Vector4(transform.position.x, transform.position.y, transform.position.z, Time.time));
            Alive.Remove(this);
            if (GameManager.Instance != null) GameManager.Instance.OnMonsterKilled(this);
            FX.BloodBurst(transform.position + Vector3.up * Data.scale, new Color(0.4f, 0.05f, 0.08f), Data.scale);
            if (Data.id == "z_bloat") BloatBurst();
            SoulWisp.Spawn(transform.position + Vector3.up * 1.2f * Data.scale); // 혼불이 빠져나간다
            FX.BloodPool(transform.position, Data.scale); // 쓰러진 자리에 피가 번진다
            // 머리를 맞히지 않고 쓰러뜨린 평범한 망자는 가끔(12%) 4초 뒤 다시 일어난다 — "망자는 머리를 날려야 눕는다"
            if (!IsBoss && !directKill && !NoRevive && lastZone != HitZone.Head && Data.archetype == MonsterArchetype.Runner && !revenant
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
                // 머리로 쓰러뜨리면 머리가 터져 나간다 (보스 제외, 피 표현 켰을 때)
                if (lastZone == HitZone.Head && !directKill && !IsBoss && GameSettings.Gore && rig.Head != null)
                {
                    Vector3 hp = rig.Head.position;
                    HeadGone.Apply(rig.Head);
                    FX.BloodBurst(hp, new Color(0.42f, 0.03f, 0.05f), 1.1f * Data.scale);
                    for (int i = 0; i < (QualityManager.Level == GfxLevel.Low ? 3 : 6); i++)
                        MonsterGib.Chunk(null, hp + Random.insideUnitSphere * 0.1f,
                            lastHitDir.normalized * Random.Range(1f, 3f) + Vector3.up * Random.Range(2f, 4f) + Random.insideUnitSphere * 1.5f,
                            Color.Lerp(new Color(0.32f, 0.03f, 0.04f), new Color(0.55f, 0.45f, 0.4f), Random.value * 0.35f), Random.Range(0.05f, 0.1f));
                }
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

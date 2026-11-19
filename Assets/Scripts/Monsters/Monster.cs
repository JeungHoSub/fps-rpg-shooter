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
                if (t > 0f) stagger = Mathf.Max(stagger, Mathf.Min(t, 1.6f));
            }
            wasAlerted = alerted;

            // 가까이 다가온 망자는 그르렁거린다 (위치가 들리는 3D 소리 → 뒤에서 오는 것도 알 수 있음)
            if (alerted && dist < 9f)
            {
                growlTimer -= dt;
                if (growlTimer <= 0f)
                {
                    SFX.PlayAt(SFX.Growl, transform.position + Vector3.up * 1.5f, Mathf.Lerp(0.9f, 0.45f, dist / 9f));
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
                    if (WaveManager.Instance != null) WaveManager.Instance.DamageDefendPoint(Data.attackDamage * (IsBoss ? 3f : 1f));
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

            // 멀리 있을 땐 가는 방향을, 가까우면 플레이어를 바라본다
            Vector3 faceDir = move.sqrMagnitude > 0.01f && dist > Data.attackRange + 1.5f ? move : dir;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), dt * 8f);

            if (rig != null)
                rig.SetLocomotion(move.sqrMagnitude > 0.01f ? spd : 0f, legSlow < 1f, alerted && Data.archetype == MonsterArchetype.Runner && Data.moveSpeed > 4f && dist > 3f);

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
            if (attackTimer <= 0f && alerted)
            {
                if (Data.archetype == MonsterArchetype.Spitter)
                {
                    if (dist < Data.attackRange) { Spit(); attackTimer = Data.attackInterval; }
                }
                else if (dist <= Data.attackRange + 0.4f)
                {
                    target.TakeDamage(Data.attackDamage, Data.infectionPerHit, transform.position);
                    if (rig != null) rig.PlayBite(1.3f * Mathf.Clamp(0.9f / Mathf.Max(0.1f, Data.attackInterval), 0.7f, 2.4f)); // 공격이 빠른 유형은 동작도 빠르게
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

        /// <summary>시야·빛 감지 (0.25초마다). 소리는 Noise가 따로 알려준다.</summary>
        void Sense(float dist)
        {
            bool lit = Flashlight.Active;
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

            lastHitDir = info.direction;
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

        /// <summary>폭발/지속피해/연쇄 등 부위 판정 없는 피해. 처치 시 true.</summary>
        public bool ApplyDirectDamage(float amount, DamageType type, bool showNumber = true)
        {
            if (IsDead) return false;
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

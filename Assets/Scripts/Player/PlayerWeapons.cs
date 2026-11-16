using System.Collections.Generic;
using Hunt.Combat;
using Hunt.Core;
using Hunt.Data;
using Hunt.Monsters;
using Hunt.Stats;
using Hunt.Weapons;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>사격 / 재장전 / 조준 / 무기 교체 / 반동. 캐릭터 스탯과 파츠 옵션이 모두 여기서 합쳐진다.</summary>
    public class PlayerWeapons : MonoBehaviour
    {
        // 플레이어(2: Ignore Raycast)와 1인칭 무기(1: TransparentFX) 레이어는 사격 판정에서 제외
        const int RaycastMask = ~((1 << 2) | (1 << GunModelBuilder.ViewLayer));

        public readonly List<WeaponInstance> Slots = new List<WeaponInstance>();
        public int CurrentIndex { get; private set; }
        public WeaponInstance Current => Slots.Count > 0 ? Slots[CurrentIndex] : null;

        public bool IsReloading => reloadTime > 0f;
        public float ReloadProgress => reloadTotal > 0f ? 1f - reloadTime / reloadTotal : 0f;
        public float SwapProgress => swapTotal > 0f && swapTime > 0f ? Mathf.Sin(swapTime / swapTotal * Mathf.PI) : 0f;
        /// <summary>교체 진행도 0→1 (0.5에서 총이 가장 아래).</summary>
        public float SwapPhase => swapTotal > 0f && swapTime > 0f ? 1f - swapTime / swapTotal : 1f;
        public bool IsAiming => aim > 0.5f;
        public float Aim => aim;
        public bool IsScoped => viewModel != null && viewModel.IsScoped;
        public float CurrentSpread { get; private set; }
        public float HitMarker { get; private set; }
        public bool LastHitWeak { get; private set; }
        public bool LastHitKill { get; private set; }
        public int ShotIndex => shotIndex;

        public float TotalWeight
        {
            get
            {
                float w = 0f;
                foreach (var s in Slots) w += s.Weight;
                return w;
            }
        }

        PlayerCharacter player;
        Camera cam;
        WeaponViewModel viewModel;

        float fireCooldown, reloadTime, reloadTotal, swapTime, swapTotal, aim, lastShotTime = -10f;
        int shotIndex, postReloadShots, reloadStage;

        public void Init(PlayerCharacter p, Camera camera, List<WeaponData> loadout)
        {
            player = p;
            cam = camera;
            foreach (var w in loadout) Slots.Add(new WeaponInstance(w));

            var holder = new GameObject("WeaponHolder");
            holder.layer = GunModelBuilder.ViewLayer;
            holder.transform.SetParent(cam.transform, false);
            viewModel = holder.AddComponent<WeaponViewModel>();
            viewModel.Init(p);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsWorldRunning || player == null || player.IsDead || Current == null) return;
            float dt = Time.deltaTime;
            bool input = gm.AcceptsGameplayInput;
            var w = Current;
            var stats = player.Stats;
            var ctrl = player.Controller;

            fireCooldown -= dt;
            if (HitMarker > 0f) HitMarker -= dt;

            // ---- 무기 교체 ----
            if (input)
            {
                for (int i = 0; i < Slots.Count && i < 9; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Swap(i);
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll > 0.01f) Swap((CurrentIndex + Slots.Count - 1) % Slots.Count);
                else if (scroll < -0.01f) Swap((CurrentIndex + 1) % Slots.Count);
            }
            if (swapTime > 0f) swapTime -= dt;

            // ---- 재장전 (진행도에 맞춰 탄창 빼는/끼우는 소리) ----
            if (reloadTime > 0f)
            {
                reloadTime -= dt;
                float p = ReloadProgress;
                if (reloadStage == 0 && p > 0.3f) { SFX.Play(SFX.MagOut, 0.7f); reloadStage = 1; }
                if (reloadStage == 1 && p > 0.72f) { SFX.Play(SFX.MagIn, 0.8f); reloadStage = 2; }
                if (reloadTime <= 0f) FinishReload(w);
            }
            else if (input && Input.GetKeyDown(KeyCode.R)) StartReload(w);

            // ---- 조준 (ADS): 조준 속도 = 무기 기본 × 제어 스탯 × 무게 페널티 ----
            bool wantAim = input && Input.GetMouseButton(1) && !IsReloading && !ctrl.IsSprinting;
            float adsSpeed = stats.AdsSpeedMult * ctrl.WeightPenalty / Mathf.Max(0.05f, w.data.adsTime);
            aim = Mathf.MoveTowards(aim, wantAim ? 1f : 0f, adsSpeed * dt);
            cam.fieldOfView = Mathf.Lerp(PlayerController.BaseFov, w.data.adsFov, aim);
            ctrl.LookMultiplier = Mathf.Lerp(1f, w.data.adsFov / PlayerController.BaseFov, aim);
            ctrl.MoveMultiplier = Mathf.Lerp(1f, 0.6f, aim);

            // ---- 탄퍼짐 ----
            CurrentSpread = ComputeSpread(w);

            // ---- 사격 ----
            bool trigger = input && (w.data.automatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0));
            if (trigger && fireCooldown <= 0f && swapTime <= 0f && !IsReloading)
            {
                if (ctrl.IsSprinting) ctrl.CancelSprint();
                if (w.ammoInMag > 0) Fire(w);
                else if (w.reserve > 0) StartReload(w);
                else if (Input.GetMouseButtonDown(0)) SFX.Play(SFX.Empty, 0.8f);
            }

            // 사격을 멈추면 반동 회복, 반동 패턴 인덱스 리셋
            if (Time.time - lastShotTime > Mathf.Max(0.12f, w.FireInterval * 1.5f))
            {
                ctrl.RecoverRecoil(dt, stats.RecoilRecovery);
                if (Time.time - lastShotTime > 0.35f) shotIndex = 0;
            }
        }

        float ComputeSpread(WeaponInstance w)
        {
            var stats = player.Stats;
            float s = Mathf.Lerp(w.data.spreadHip, w.data.spreadAds, aim);
            bool runGun = stats.Has(Trait.RunAndGun);
            if (player.Controller.IsMoving && !runGun) s += w.data.spreadMovePenalty;
            s += Mathf.Min(shotIndex, 10) * 0.08f * (1f - aim * 0.7f); // 연사 블룸
            s *= stats.SpreadMult * w.SpreadMult;
            s += player.Infection.IsFrenzy ? 3f : 0f;
            return s;
        }

        void Swap(int index)
        {
            if (index == CurrentIndex || index < 0 || index >= Slots.Count) return;
            CurrentIndex = index;
            reloadTime = 0f;
            reloadTotal = 0f;
            shotIndex = 0;
            swapTotal = swapTime = 0.45f / player.Stats.SwapSpeedMult;
            SFX.Play(SFX.Swap, 0.6f);
        }

        void StartReload(WeaponInstance w)
        {
            if (IsReloading || w.ammoInMag >= w.MagazineSize || w.reserve <= 0) return;
            float baseTime = w.ammoInMag == 0 ? w.data.emptyReloadTime : w.data.reloadTime;
            reloadTotal = reloadTime = baseTime / (player.Stats.ReloadSpeedMult * w.ReloadSpeedMult);
            reloadStage = 0;
        }

        void FinishReload(WeaponInstance w)
        {
            int need = w.MagazineSize - w.ammoInMag;
            int take = Mathf.Min(need, w.reserve);
            w.ammoInMag += take;
            w.reserve -= take;
            reloadTime = reloadTotal = 0f;
            SFX.Play(SFX.Rack, 0.8f);
            if (w.Has(AffixType.PostReloadBurst)) postReloadShots = 3;
        }

        void Fire(WeaponInstance w)
        {
            var stats = player.Stats;
            var inf = player.Infection;
            fireCooldown = w.FireInterval;
            w.ammoInMag--;
            lastShotTime = Time.time;

            float burst = 1f;
            if (postReloadShots > 0)
            {
                burst += w.Sum(AffixType.PostReloadBurst) / 100f;
                postReloadShots--;
            }

            float weakBonus = stats.WeakPointBonus + w.WeakPointBonus;
            if (stats.Has(Trait.SteadyAim) && player.Controller.StationaryTime >= 1f) weakBonus += 0.5f;
            float kbMult = stats.KnockbackMult * w.KnockbackMult * inf.KnockbackMultiplier;

            var info = new DamageInfo
            {
                baseDamage = w.Damage * inf.DamageMultiplier * burst,
                type = w.DamageType,
                weaponClass = w.data.weaponClass,
                critChance = stats.CritChance + w.CritChanceBonus,
                critMultiplier = stats.CritMultiplier,
                weakPointBonus = weakBonus,
                penetration = w.data.penetration,
                knockback = w.data.knockback * kbMult / Mathf.Max(1, w.data.pellets),
                falloffStart = w.data.falloffStart,
                falloffEnd = w.data.falloffEnd,
                minFalloffMultiplier = w.data.minFalloff,
            };

            // 연출: 총 모델 반동 / 총성 / 화면 흔들림
            viewModel.OnFire(w);
            SFX.Play(SFX.Shot(w.data.weaponClass), 0.85f);
            float shake = w.data.weaponClass == WeaponClass.Shotgun || w.data.weaponClass == WeaponClass.Sniper ? 1.2f : 0.35f;
            player.Controller.AddShake(shake * Mathf.Lerp(1f, 0.5f, aim));

            Vector3 origin = cam.transform.position;
            Vector3 muzzle = viewModel.MuzzlePosition;
            Color tracer = w.DamageType == DamageType.Physical ? new Color(1f, 0.85f, 0.5f) : CombatNames.ColorOf(w.DamageType);
            int pellets = Mathf.Max(1, w.data.pellets);
            float tracerWidth = pellets > 1 ? 0.012f : 0.025f;

            bool anyHit = false, anyWeak = false, anyKill = false;
            Vector3 killPos = Vector3.zero, hurtPos = Vector3.zero;

            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = SpreadDirection(cam.transform, CurrentSpread);
                Vector3 end = origin + dir * 150f;
                if (Physics.Raycast(origin, dir, out RaycastHit hit, 300f, RaycastMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    var part = hit.collider.GetComponent<MonsterPart>();
                    if (part != null)
                    {
                        info.distance = hit.distance;
                        info.hitPoint = hit.point;
                        info.direction = dir;
                        var res = part.ReceiveHit(info);
                        if (res.hit)
                        {
                            anyHit = true;
                            hurtPos = hit.point;
                            anyWeak |= res.zone == HitZone.WeakPoint || res.zone == HitZone.Head;
                            if (res.killed) { anyKill = true; killPos = hit.point; }
                            FX.ImpactFlesh(hit.point, dir, FleshColor(res.zone), res.crit ? 10 : 5);
                        }
                        OnHitResult(w, res, info, hit.point);
                    }
                    else
                    {
                        FX.ImpactWorld(hit.point, hit.normal);
                    }
                }
                // 산탄은 일부 펠릿만 예광탄 표시
                if (pellets == 1 || i % 3 == 0) FX.Tracer(muzzle, end, tracer, tracerWidth);
            }

            if (anyKill) { SFX.Play(SFX.KillTick, 0.6f, 0f); SFX.PlayAt(SFX.MonsterDie, killPos, 0.9f); }
            else if (anyHit)
            {
                SFX.Play(anyWeak ? SFX.WeakTick : SFX.HitTick, 0.45f, 0f);
                SFX.MonsterHurtAt(hurtPos);
            }

            // C: 고정 패턴 × (제어 스탯 + 파츠) + 제어 스탯에 반비례하는 노이즈
            Vector2 pattern = RecoilPattern.Kick(w.data, shotIndex);
            float aimDamp = Mathf.Lerp(1f, 0.75f, aim);
            Vector2 noise = Random.insideUnitCircle * pattern.magnitude * 0.6f * stats.RecoilNoise;
            player.Controller.AddRecoil((pattern * stats.RecoilPatternScale + noise) * w.RecoilMult * aimDamp);
            shotIndex++;

            if (w.ammoInMag == 0 && w.reserve > 0) StartReload(w);
        }

        static Color FleshColor(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.WeakPoint: return new Color(1f, 0.6f, 0.1f);
                case HitZone.Armor: return new Color(0.85f, 0.8f, 0.65f);
                default: return new Color(0.45f, 0.05f, 0.08f);
            }
        }

        void OnHitResult(WeaponInstance w, DamageResult res, DamageInfo info, Vector3 point)
        {
            if (!res.hit) return;
            HitMarker = 0.12f;
            LastHitWeak = res.zone == HitZone.WeakPoint || res.zone == HitZone.Head;
            LastHitKill = res.killed;

            if (res.zone == HitZone.WeakPoint && w.Has(AffixType.AmmoOnWeakPoint) && Random.value < w.Sum(AffixType.AmmoOnWeakPoint) / 100f)
                w.ammoInMag = Mathf.Min(w.MagazineSize, w.ammoInMag + 1);

            if (res.killed && w.Has(AffixType.ExplodeOnKill) && Random.value < w.Sum(AffixType.ExplodeOnKill) / 100f)
                Explode(point, w.Damage * 2f * Mathf.Max(1, w.data.pellets) * 0.5f, info.type);
        }

        void Explode(Vector3 point, float damage, DamageType type)
        {
            Effects.Burst(point, 3f, CombatNames.ColorOf(type == DamageType.Physical ? DamageType.Fire : type));
            SFX.PlayAt(SFX.Shot(WeaponClass.Shotgun), point, 1f);
            var hits = Physics.OverlapSphere(point, 3f, RaycastMask, QueryTriggerInteraction.Ignore);
            var done = new HashSet<Monster>();
            foreach (var h in hits)
            {
                var part = h.GetComponent<MonsterPart>();
                if (part == null || part.owner == null || !done.Add(part.owner)) continue;
                part.owner.ApplyDirectDamage(damage, type);
            }
        }

        static Vector3 SpreadDirection(Transform t, float spreadDeg)
        {
            Vector2 r = Random.insideUnitCircle * Mathf.Tan(spreadDeg * Mathf.Deg2Rad);
            return (t.forward + t.right * r.x + t.up * r.y).normalized;
        }

        public void RefillAmmo()
        {
            foreach (var s in Slots)
            {
                s.reserve = s.MaxReserve;
                s.ammoInMag = s.MagazineSize;
            }
        }
    }
}

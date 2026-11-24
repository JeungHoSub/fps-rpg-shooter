using System.Collections.Generic;
using Hunt.Combat;
using Hunt.Core;
using Hunt.Data;
using Hunt.Monsters;
using Hunt.Stats;
using Hunt.UI;
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

        // ---- 슬롯: 1·2 주무기, 3 권총, 4 근접(칼), 5 특수 기기 ----
        public enum Hand { Gun, Melee, Gadget }
        public Hand Mode { get; private set; } = Hand.Gun;
        /// <summary>HUD용: 지금 손에 든 슬롯 번호 (0~4).</summary>
        public int ActiveKey => Mode == Hand.Melee ? 3 : Mode == Hand.Gadget ? 4 : (CurrentIndex == Slots.Count - 1 ? 2 : CurrentIndex);
        public int PrimaryCount => Mathf.Max(0, Slots.Count - 1);
        public GadgetType Gadget { get; private set; }
        public readonly int[] GadgetCount = { 3, 2, 1, 2 };
        /// <summary>칼 휘두름 진행도 0→1 (1 = 쉬는 중).</summary>
        public float MeleeSwing => meleeT;
        public const string MeleeName = "마체테";
        float meleeCooldown, meleeT = 1f, gadgetCooldown;
        bool meleePending;
        Hand returnMode = Hand.Gun;
        bool quickMelee;

        public float MeleeDamage => (70f + player.Level * 4f) * player.Stats.MeleeDamageMult * GameRules.DamageMult;
        /// <summary>화염병 불바다 초당 피해 (기술·기질 '화약쟁이' 반영은 GadgetDamageMult로).</summary>
        public float MolotovDps => (45f + player.Level * 6f) * player.Stats.GadgetDamageMult * GameRules.DamageMult;
        public float GrenadeDamage => (220f + player.Level * 25f) * (1f + player.Stats.Tech * 0.02f) * GameRules.DamageMult;
        public float MineDamage => (300f + player.Level * 30f) * (1f + player.Stats.Tech * 0.02f) * GameRules.DamageMult;
        public float TurretDamage => (14f + player.Level * 1.5f) * (1f + player.Stats.Tech * 0.02f) * GameRules.DamageMult;

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
            if (player != null && player.IsDead) { UpdateGrenadeArc(false); UpdatePlacePreview(false); }
            if (gm == null || !gm.IsWorldRunning || player == null || player.IsDead || Current == null) return;
            float dt = Time.deltaTime;
            bool input = gm.AcceptsGameplayInput;
            Hunt.Combat.DamageCalculator.PlayerHeadBonus = player.Stats.Has(Hunt.Stats.Trait.Headhunter) ? 0.6f : 0f; // 기질 '머리 사냥꾼'
            // 자동 사격 (터치 조작): 조준점에 망자가 있으면 방아쇠를 당긴 것으로 친다
            GameInput.AutoTarget = input && GameInput.Mobile && GameSettings.AutoFire && Mode == Hand.Gun
                                   && !player.IsUsingItem && AutoFireTarget();
            var w = Current;
            var stats = player.Stats;
            var ctrl = player.Controller;

            fireCooldown -= dt;
            if (HitMarker > 0f) HitMarker -= dt;

            // ---- 무기 교체 ----
            if (input && !player.IsUsingItem)
            {
                if (GameInput.SlotDown(0)) SelectKey(0);
                if (GameInput.SlotDown(1)) SelectKey(1);
                if (GameInput.SlotDown(2)) SelectKey(2);
                if (GameInput.SlotDown(3)) SelectKey(3);
                if (GameInput.SlotDown(4)) SelectKey(4);
                float scroll = GameInput.Scroll;
                if (scroll > 0.01f) CycleKey(-1);
                else if (scroll < -0.01f) CycleKey(1);
                // V: 빠른 칼질, G: 빠른 수류탄
                if (GameInput.MeleeDown && Mode != Hand.Melee && meleeCooldown <= 0f)
                {
                    returnMode = Mode;
                    Mode = Hand.Melee;
                    quickMelee = true;
                    StartSwing();
                }
                if (GameInput.GrenadeDown) QuickGrenade();
            }
            if (swapTime > 0f) swapTime -= dt;
            if (meleeCooldown > 0f) meleeCooldown -= dt;
            if (gadgetCooldown > 0f) gadgetCooldown -= dt;
            UpdateMelee(dt);

            bool gadgetReady = input && Mode == Hand.Gadget && GadgetCount[(int)Gadget] > 0 && !player.IsUsingItem;
            UpdateGrenadeArc(gadgetReady && (Gadget == GadgetType.Grenade || Gadget == GadgetType.Molotov));
            UpdatePlacePreview(gadgetReady && (Gadget == GadgetType.Mine || Gadget == GadgetType.Turret));
            if (Mode != Hand.Gun)
            {
                aim = Mathf.MoveTowards(aim, 0f, dt * 6f);
                cam.fieldOfView = Mathf.Lerp(PlayerController.BaseFov, w.data.adsFov, aim);
                ctrl.LookMultiplier = 1f;
                ctrl.MoveMultiplier = 1f;
                CurrentSpread = 0f;
                bool click = input && GameInput.FireDown && swapTime <= 0f && !player.IsUsingItem;
                if (click && Mode == Hand.Melee) StartSwing();
                bool throwable = Gadget == GadgetType.Grenade || Gadget == GadgetType.Molotov;
                if (Mode == Hand.Gadget && throwable && !GameInput.Mobile)
                {
                    // 던지는 기기: 누르고 있으면 힘을 모으고(1초), 떼면 던진다 — 오래 누를수록 멀리
                    if (click && gadgetCooldown <= 0f && GadgetCount[(int)Gadget] > 0) { charging = true; ThrowCharge = 0f; }
                    if (charging)
                    {
                        ThrowCharge = Mathf.MoveTowards(ThrowCharge, 1f, dt);
                        if (!GameInput.Fire || !input) { charging = false; if (input) UseGadget(Gadget); ThrowCharge = 0f; }
                    }
                }
                else if (click && Mode == Hand.Gadget) UseGadget(Gadget);
                if (Mode != Hand.Gadget) { charging = false; ThrowCharge = 0f; }
                return;
            }

            // ---- 재장전 (진행도에 맞춰 탄창 빼는/끼우는 소리) ----
            if (reloadTime > 0f && shellReload)
            {
                // 산탄총: 한 발씩 밀어 넣는다 (쏘면 그 자리에서 멈춘다)
                reloadTime -= dt;
                shellTimer -= dt;
                if (shellTimer <= 0f)
                {
                    if (w.ammoInMag < w.MagazineSize && w.reserve > 0)
                    {
                        w.ammoInMag++;
                        w.reserve--;
                        SFX.PlayPitched(SFX.ShellIn, 0.75f, 0.9f);
                        shellTimer = shellInterval;
                    }
                    if (w.ammoInMag >= w.MagazineSize || w.reserve <= 0)
                    {
                        if (reloadFromEmpty) SFX.Play(SFX.Rack, 0.85f); // 펌프를 당겨 약실에 넣는다
                        reloadTime = reloadTotal = 0f;
                        shellReload = false;
                    }
                }
            }
            else if (reloadTime > 0f)
            {
                // 탄창식: 탄창 분리 → 새 탄창 삽입 → (빈 탄창이었으면) 노리쇠/슬라이드 당김
                reloadTime -= dt;
                float p = ReloadProgress;
                float pitch = ReloadPitch(w.data.weaponClass);
                float inAt = reloadFromEmpty ? 0.62f : 0.72f;
                if (reloadStage == 0 && p > 0.25f) { SFX.PlayPitched(SFX.MagOut, 0.7f, pitch); reloadStage = 1; }
                if (reloadStage == 1 && p > inAt) { SFX.PlayPitched(SFX.MagIn, 0.85f, pitch); reloadStage = 2; }
                if (reloadStage == 2 && reloadFromEmpty && p > 0.86f) { SFX.PlayPitched(SFX.Bolt, 0.85f, pitch); reloadStage = 3; }
                if (reloadTime <= 0f) FinishReload(w);
            }
            else if (input && GameInput.ReloadDown) StartReload(w);

            // ---- 조준 (ADS): 조준 속도 = 무기 기본 × 제어 스탯 × 무게 페널티 ----
            bool wantAim = input && GameInput.Aim && !IsReloading && !ctrl.IsSprinting;
            float adsSpeed = stats.AdsSpeedMult * ctrl.WeightPenalty / Mathf.Max(0.05f, w.data.adsTime);
            aim = Mathf.MoveTowards(aim, wantAim ? 1f : 0f, adsSpeed * dt);
            cam.fieldOfView = Mathf.Lerp(PlayerController.BaseFov, w.data.adsFov, aim);
            ctrl.LookMultiplier = Mathf.Lerp(1f, w.data.adsFov / PlayerController.BaseFov * GameSettings.AdsSensitivity, aim); // Esc '조준 감도'
            ctrl.MoveMultiplier = Mathf.Lerp(1f, 0.6f, aim);

            // ---- 탄퍼짐 ----
            CurrentSpread = ComputeSpread(w);

            // ---- 사격 ----
            bool trigger = input && !player.IsUsingItem && (w.data.automatic ? GameInput.Fire : GameInput.FireDown);
            // 한 발씩 넣는 중이면 쏠 수 있다 (넣은 만큼만)
            if (trigger && shellReload && w.ammoInMag > 0) { shellReload = false; reloadTime = reloadTotal = 0f; }
            if (trigger && fireCooldown <= 0f && swapTime <= 0f && !IsReloading)
            {
                if (ctrl.IsSprinting) ctrl.CancelSprint();
                if (w.ammoInMag > 0) Fire(w);
                else if (w.reserve > 0) StartReload(w);
                else if (GameInput.FireDownRaw)
                {
                    SFX.Play(SFX.Empty, 0.8f);
                    // 탄이 다 떨어졌으면 탄이 남은 다른 총으로 바꿔 든다
                    for (int i = 0; i < Slots.Count; i++)
                    {
                        var o = Slots[i];
                        if (o == null || o == w || (o.ammoInMag <= 0 && o.reserve <= 0)) continue;
                        HUD.Notify($"{w.data.displayName} 탄이 떨어졌다 — {o.data.displayName}(으)로", new Color(1f, 0.75f, 0.45f));
                        Swap(i);
                        break;
                    }
                }
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
            s *= stats.SpreadMult * w.SpreadMult * player.Controller.CrouchSpreadMult * (player.CharmAim ? 0.75f : 1f); // 앉으면 탄이 모인다 · 명중부
            s += player.Infection.IsFrenzy ? 3f : 0f;
            return s;
        }

        void SelectKey(int key)
        {
            if (key <= 1)
            {
                if (key < PrimaryCount) Swap(key);
            }
            else if (key == 2) Swap(Slots.Count - 1);
            else if (key == 3) SetMode(Hand.Melee);
            else
            {
                // 5번을 다시 누르면 다음 기기로
                if (Mode == Hand.Gadget) NextGadget();
                else SetMode(Hand.Gadget);
            }
        }

        void CycleKey(int dir)
        {
            int k = ActiveKey;
            for (int i = 0; i < 5; i++)
            {
                k = (k + dir + 5) % 5;
                if (k == 1 && PrimaryCount < 2) continue;
                SelectKey(k);
                return;
            }
        }

        void SetMode(Hand m)
        {
            if (Mode == m) return;
            Mode = m;
            quickMelee = false;
            reloadTime = reloadTotal = 0f;
            swapTotal = swapTime = 0.4f / player.Stats.SwapSpeedMult;
            SFX.Play(SFX.Swap, 0.6f);
            if (m == Hand.Gadget && GadgetCount[(int)Gadget] <= 0) NextGadget();
        }

        void NextGadget()
        {
            for (int i = 1; i <= Gadgets.Count; i++)
            {
                var g = (GadgetType)(((int)Gadget + i) % Gadgets.Count);
                if (GadgetCount[(int)g] > 0 || i == Gadgets.Count) { Gadget = g; break; }
            }
            SFX.Play(SFX.Swap, 0.4f);
        }

        /// <summary>서낭당에서 장착 교체. slot 0·1 = 주무기, 2 = 권총 자리.</summary>
        public void EquipSlot(int slot, WeaponInstance w)
        {
            int existing = Slots.IndexOf(w);
            if (slot < 2 && slot >= PrimaryCount)
            {
                if (existing >= 0) return;
                Slots.Insert(PrimaryCount, w);
            }
            else
            {
                int idx = slot >= 2 ? Slots.Count - 1 : slot;
                if (existing == idx) return;
                if (existing >= 0) Slots[existing] = Slots[idx]; // 주무기 1·2 자리 바꿈
                Slots[idx] = w;
            }
            w.ammoInMag = w.MagazineSize;
            CurrentIndex = Mathf.Clamp(CurrentIndex, 0, Slots.Count - 1);
            reloadTime = reloadTotal = 0f;
        }

        public void ResupplyGadgets()
        {
            for (int i = 0; i < Gadgets.Count; i++)
                GadgetCount[i] = Mathf.Min(Gadgets.MaxCarry((GadgetType)i), GadgetCount[i] + 1);
        }

        // ---- 근접 ----
        void StartSwing()
        {
            if (meleeCooldown > 0f) return;
            if (!player.Controller.UseStamina(8f)) { SFX.Play(SFX.Empty, 0.5f); return; }
            meleeCooldown = 0.55f / player.Stats.SwapSpeedMult;
            meleeT = 0f;
            meleePending = true;
            player.InterruptItem();
            SFX.Play(SFX.Swing, 0.7f, 0.1f);
            // 휘두르기 시작할 때 나를 못 알아챈 망자들 (칼 소리에 알아채기 전 상태로 등 뒤 처치를 판정)
            swingUnaware.Clear();
            foreach (var um in Monster.Alive)
                if (um != null && !um.IsDead && !um.IsAlerted && (um.transform.position - player.transform.position).sqrMagnitude < 16f) swingUnaware.Add(um);
            Noise.Emit(player.transform.position, 2f, true); // 칼은 조용하다
            // 앞쪽 3.2m 안(시야 35° 안)에 망자가 있으면 그쪽으로 한 걸음 파고든다 — 칼이 허공을 가르지 않게
            Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Monster lungeTo = null;
            float bestD = 3.2f;
            foreach (var m in Monster.Alive)
            {
                if (m == null || m.IsDead) continue;
                Vector3 to = m.transform.position - player.transform.position; to.y = 0f;
                float d = to.magnitude;
                if (d < 1.4f || d > bestD) continue; // 이미 닿는 거리면 그대로
                if (Vector3.Angle(fwd, to) > 35f) continue;
                bestD = d; lungeTo = m;
            }
            if (lungeTo != null)
            {
                Vector3 to = lungeTo.transform.position - player.transform.position; to.y = 0f;
                player.Controller.Knock(to.normalized * Mathf.Clamp((bestD - 1.1f) * 5f, 3f, 10f));
            }
        }

        void UpdateMelee(float dt)
        {
            if (meleeT >= 1f) return;
            meleeT = Mathf.Min(1f, meleeT + dt / 0.42f);
            if (meleePending && meleeT > 0.38f) { meleePending = false; MeleeHit(); }
            if (meleeT >= 1f && quickMelee) { quickMelee = false; Mode = returnMode; swapTotal = swapTime = 0.2f; }
        }

        readonly HashSet<Monster> swingUnaware = new HashSet<Monster>();

        int impactFrame;
        /// <summary>벽·땅 명중 소리 (한 프레임 한 번 — 산탄 여러 알이 겹치지 않게). 단단한 면은 가끔 도탄.</summary>
        void ImpactSound(Vector3 p, Vector3 n)
        {
            if (impactFrame == Time.frameCount) return;
            impactFrame = Time.frameCount;
            if (n.y > 0.75f) SFX.PlayAt(SFX.MudHit, p, 0.5f);
            else if (Random.value < 0.14f) SFX.PlayAt(SFX.Ricochet, p, 0.35f);
        }

        void MeleeHit()
        {
            Vector3 center = cam.transform.position + cam.transform.forward * 1.2f;
            var hits = Physics.OverlapSphere(center, 1.1f, RaycastMask, QueryTriggerInteraction.Ignore);
            var best = new Dictionary<Monster, MonsterPart>();
            foreach (var h in hits)
            {
                var part = h.GetComponent<MonsterPart>();
                if (part == null || part.owner == null || part.owner.IsDead) continue;
                if (!best.TryGetValue(part.owner, out var cur) ||
                    (h.ClosestPoint(center) - center).sqrMagnitude < (cur.GetComponent<Collider>().ClosestPoint(center) - center).sqrMagnitude)
                    best[part.owner] = part;
            }
            int n = 0;
            bool kill = false;
            foreach (var kv in best)
            {
                if (n++ >= 2) break; // 한 번에 둘까지
                var part = kv.Value;
                Vector3 hp = part.GetComponent<Collider>().ClosestPoint(center);
                // 소리 없는 처치: 나를 못 알아챈 망자를 등 뒤(70° 안)에서 베면 한 번에 쓰러진다 (보스·엘리트 제외)
                var mo = part.owner;
                Vector3 toMo = mo.transform.position - player.transform.position; toMo.y = 0f;
                bool backstab = swingUnaware.Contains(mo) && !mo.IsBoss && mo.Data.archetype == Hunt.Data.MonsterArchetype.Runner
                                && toMo.sqrMagnitude > 0.01f && Vector3.Angle(mo.transform.forward, toMo) < 70f;
                if (backstab) mo.NoRevive = true;
                // 마무리: 다리 꺾인 망자는 칼 한 번에 (보스 제외) — 다리 쏘고 베는 흐름
                bool finisher = !backstab && mo.Crippled && !mo.IsBoss;
                var info = new DamageInfo
                {
                    baseDamage = backstab ? Mathf.Max(MeleeDamage * 12f, mo.MaxHp * 3f) : finisher ? Mathf.Max(MeleeDamage * 6f, mo.MaxHp * 1.6f) : MeleeDamage, type = DamageType.Physical, weaponClass = WeaponClass.Melee,
                    critChance = player.Stats.CritChance, critMultiplier = player.Stats.CritMultiplier,
                    weakPointBonus = player.Stats.WeakPointBonus, penetration = 0.5f, knockback = 3.5f * player.Stats.KnockbackMult,
                    distance = 1f, falloffStart = 100f, falloffEnd = 200f, minFalloffMultiplier = 1f,
                    hitPoint = hp, direction = cam.transform.forward,
                };
                var res = part.ReceiveHit(info);
                if (!res.hit) continue;
                kill |= res.killed;
                if (res.killed) player.RunMelee++;
                if (backstab && res.killed)
                {
                    HUD.Notify("소리 없이 쓰러뜨렸다", new Color(0.75f, 0.85f, 1f));
                    Achievements.Add("silent10");
                    player.RunSilent++;
                }
                else if (finisher && res.killed)
                {
                    HUD.Notify("절뚝이는 망자를 마무리했다", new Color(1f, 0.75f, 0.4f));
                    player.Controller.AddShake(0.5f);
                }
                // 칼로 쓰러뜨리면 35% 확률로 치유 부적 (가까이 붙는 위험의 보상)
                if (res.killed && Random.value < 0.35f)
                    Pickup.Spawn(PickupKind.Rune, part.owner.transform.position + Vector3.up * 0.2f, part.owner.transform.parent);
                HitMarker = 0.12f;
                LastHitWeak = res.zone == HitZone.WeakPoint || res.zone == HitZone.Head;
                LastHitKill = res.killed;
                FX.ImpactFlesh(hp, cam.transform.forward, FleshColor(res.zone), 8);
            }
            if (best.Count > 0)
            {
                SFX.Play(SFX.Slash, 0.9f, 0.08f);
                player.Controller.AddShake(0.6f);
                Noise.Emit(player.transform.position, 4f, true);
                if (kill) GameManager.Instance.HitStop(0.07f);
            }
        }

        // ---- 던지기 힘 (누르고 있는 동안 0→1) ----
        bool charging;
        /// <summary>던질 힘 0~1 (HUD 막대). 모으지 않을 때 던지면 중간 힘.</summary>
        public float ThrowCharge { get; private set; }
        public bool ChargingThrow => charging;
        /// <summary>던지는 속도: 힘 0 = 가까이(9) · 1 = 멀리(19), 모으지 않으면 15.</summary>
        float ThrowSpeed => charging ? Mathf.Lerp(9f, 19f, ThrowCharge) : 15f;

        // ---- 수류탄 던질 길 미리 보기 (5번 수류탄을 들고 있을 때) ----
        LineRenderer arcLine;
        Transform arcRing;
        static readonly Vector3[] arcPts = new Vector3[48];

        void UpdateGrenadeArc(bool show)
        {
            if (!show)
            {
                if (arcLine != null && arcLine.enabled) { arcLine.enabled = false; arcRing.gameObject.SetActive(false); }
                return;
            }
            if (arcLine == null)
            {
                var go = new GameObject("수류탄 길");
                go.layer = 2;
                arcLine = go.AddComponent<LineRenderer>();
                arcLine.sharedMaterial = FX.UnlitMaterial(Color.white);
                arcLine.useWorldSpace = true;
                arcLine.startWidth = 0.035f;
                arcLine.endWidth = 0.02f;
                arcLine.startColor = new Color(1f, 0.85f, 0.55f, 0.15f);
                arcLine.endColor = new Color(1f, 0.6f, 0.35f, 0.8f);
                arcLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arcLine.receiveShadows = false;
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(ring.GetComponent<Collider>());
                ring.layer = 2;
                var rr = ring.GetComponent<Renderer>();
                rr.sharedMaterial = FX.UnlitMaterial(new Color(1f, 0.35f, 0.2f, 0.16f));
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arcRing = ring.transform;
            }
            arcLine.enabled = true;
            // ThrownGrenade.Throw와 같은 시작점·속도·끌림(0.2)으로 길을 따라가 본다
            var t = cam.transform;
            Vector3 pos = t.position + t.forward * 0.5f + t.right * 0.15f;
            Vector3 vel = t.forward * ThrowSpeed + Vector3.up * 3.5f;
            const float step = 0.05f;
            int n = 0;
            arcPts[n++] = pos;
            bool landed = false;
            Vector3 land = pos;
            int mask = ~((1 << 2) | (1 << 1));
            for (int i = 0; i < arcPts.Length - 1 && !landed; i++)
            {
                vel += Physics.gravity * step;
                vel *= 1f - 0.2f * step;
                Vector3 next = pos + vel * step;
                if (Physics.Linecast(pos, next, out var hit, mask, QueryTriggerInteraction.Ignore) && !(hit.collider is CharacterController))
                {
                    next = hit.point;
                    landed = true;
                    land = hit.point;
                }
                arcPts[n++] = next;
                pos = next;
            }
            arcLine.positionCount = n;
            arcLine.SetPositions(arcPts);
            // 터질 곳: 폭발 범위(6m)를 옅은 원으로
            arcRing.gameObject.SetActive(landed);
            if (landed)
            {
                Vector3 g = FX.FloorUnder(land + Vector3.up * 0.3f);
                arcRing.position = g + Vector3.up * 0.03f;
                arcRing.localScale = new Vector3(12f * 0.6f, 0.005f, 12f * 0.6f); // 피해가 큰 안쪽(약 3.6m)만
            }
        }

        // ---- 지뢰·기관총 놓을 자리 미리 보기 ----
        Transform placeMark;

        void UpdatePlacePreview(bool show)
        {
            if (!show) { if (placeMark != null && placeMark.gameObject.activeSelf) placeMark.gameObject.SetActive(false); return; }
            if (placeMark == null)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(ring.GetComponent<Collider>());
                ring.name = "놓을 자리";
                ring.layer = 2;
                var rr = ring.GetComponent<Renderer>();
                rr.sharedMaterial = FX.UnlitMaterial(new Color(0.55f, 1f, 0.6f, 0.22f));
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                placeMark = ring.transform;
            }
            placeMark.gameObject.SetActive(true);
            // UseGadget과 같은 자리: 지뢰 1.1m 앞(작동 범위 1.6m), 기관총 1.6m 앞
            Vector3 flatFwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            bool mine = Gadget == GadgetType.Mine;
            Vector3 at = player.transform.position + flatFwd * (mine ? 1.1f : 1.6f);
            at = FX.FloorUnder(at + Vector3.up * 0.5f);
            placeMark.position = at + Vector3.up * 0.03f;
            float r = mine ? 1.6f : 0.6f;
            float pulse = 1f + 0.04f * Mathf.Sin(Time.time * 6f);
            placeMark.localScale = new Vector3(r * 2f * pulse, 0.004f, r * 2f * pulse);
        }

        void OnDestroy()
        {
            if (placeMark != null) Destroy(placeMark.gameObject);
            if (arcLine != null) Destroy(arcLine.gameObject);
            if (arcRing != null) Destroy(arcRing.gameObject);
        }

        // ---- 특수 기기 ----
        void QuickGrenade()
        {
            if (gadgetCooldown > 0f || GadgetCount[(int)GadgetType.Grenade] <= 0) return;
            UseGadget(GadgetType.Grenade);
        }

        void UseGadget(GadgetType g)
        {
            if (gadgetCooldown > 0f) return;
            if (GadgetCount[(int)g] <= 0) { HUD.Notify($"{Gadgets.Name(g)}이(가) 없다", new Color(1f, 0.6f, 0.4f)); return; }
            GadgetCount[(int)g]--;
            gadgetCooldown = 0.8f;
            player.InterruptItem();
            var t = cam.transform;
            Vector3 flatFwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            switch (g)
            {
                case GadgetType.Grenade:
                    ThrownGrenade.Throw(t.position + t.forward * 0.5f + t.right * 0.15f, t.forward * ThrowSpeed + Vector3.up * 3.5f, player, GrenadeDamage);
                    meleeT = 0f; // 던지는 팔 동작 재사용
                    break;
                case GadgetType.Molotov:
                    ThrownMolotov.Throw(t.position + t.forward * 0.5f + t.right * 0.15f, t.forward * ThrowSpeed + Vector3.up * 3.5f, player, MolotovDps);
                    meleeT = 0f;
                    break;
                case GadgetType.Mine:
                    LandMine.Plant(player.transform.position + flatFwd * 1.1f, player, MineDamage);
                    break;
                case GadgetType.Turret:
                    SentryTurret.Deploy(player.transform.position + flatFwd * 1.6f, t.eulerAngles.y, TurretDamage);
                    break;
            }
            if (Mode == Hand.Gadget && GadgetCount[(int)g] <= 0) NextGadget();
        }

        bool AutoFireTarget()
        {
            var t = cam.transform;
            return Physics.Raycast(t.position, t.forward, out var hit, 60f, RaycastMask, QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponent<MonsterPart>() != null;
        }

        void Swap(int index)
        {
            if (index < 0 || index >= Slots.Count) return;
            if (Mode != Hand.Gun) { Mode = Hand.Gun; quickMelee = false; if (index == CurrentIndex) { swapTotal = swapTime = 0.4f / player.Stats.SwapSpeedMult; SFX.Play(SFX.Swap, 0.6f); return; } }
            if (index == CurrentIndex) return;
            CurrentIndex = index;
            reloadTime = 0f;
            reloadTotal = 0f;
            shotIndex = 0;
            swapTotal = swapTime = 0.45f / player.Stats.SwapSpeedMult;
            SFX.Play(SFX.Swap, 0.6f);
        }

        bool shellReload, reloadFromEmpty;
        float shellTimer, shellInterval;

        static float ReloadPitch(WeaponClass c)
        {
            switch (c)
            {
                case WeaponClass.Pistol: return 1.25f;
                case WeaponClass.SMG: return 1.12f;
                case WeaponClass.Sniper: return 0.9f;
                case WeaponClass.LMG: return 0.78f;
                case WeaponClass.Launcher: return 0.72f;
                case WeaponClass.Flamethrower: return 0.65f;
                default: return 1f;
            }
        }

        void StartReload(WeaponInstance w)
        {
            if (IsReloading || w.ammoInMag >= w.MagazineSize || w.reserve <= 0) return;
            float speed = player.Stats.ReloadSpeedMult * w.ReloadSpeedMult * player.Armor.ReloadMult;
            reloadFromEmpty = w.ammoInMag == 0;
            shellReload = w.data.weaponClass == WeaponClass.Shotgun && w.data.magazine > 2;
            if (shellReload)
            {
                int need = Mathf.Min(w.MagazineSize - w.ammoInMag, w.reserve);
                shellInterval = w.data.reloadTime / 6f / speed;
                shellTimer = 0.35f / speed; // 총을 기울이는 시간
                reloadTotal = reloadTime = shellTimer + shellInterval * need + (reloadFromEmpty ? 0.4f : 0f) + 0.05f;
            }
            else
            {
                float baseTime = reloadFromEmpty ? w.data.emptyReloadTime : w.data.reloadTime;
                reloadTotal = reloadTime = baseTime / speed;
            }
            reloadStage = 0;
            Noise.Emit(player.transform.position, 6f, true); // 탄창 소리
        }

        void FinishReload(WeaponInstance w)
        {
            int need = w.MagazineSize - w.ammoInMag;
            int take = Mathf.Min(need, w.reserve);
            w.ammoInMag += take;
            w.reserve -= take;
            reloadTime = reloadTotal = 0f;
            if (w.Has(AffixType.PostReloadBurst)) postReloadShots = 3;
        }

        void Fire(WeaponInstance w)
        {
            var stats = player.Stats;
            var inf = player.Infection;
            fireCooldown = w.FireInterval;
            w.ammoInMag--;
            player.InterruptItem();
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
                baseDamage = w.Damage * inf.DamageMultiplier * burst * GameRules.DamageMult * stats.BalanceBonus * stats.DamageMult,
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
            // 탄피·총구 연기 (화염방사기·발사기 제외)
            var wc = w.data.weaponClass;
            if (wc != WeaponClass.Flamethrower && wc != WeaponClass.Launcher && wc != WeaponClass.Melee)
            {
                var ct = cam.transform;
                Vector3 mz = viewModel.MuzzlePosition;
                if (wc != WeaponClass.Shotgun && wc != WeaponClass.Sniper) FX.Casing(mz - ct.forward * 0.35f + ct.right * 0.03f, ct.right, ct.up, false);
                FX.MuzzleSmoke(mz, ct.forward, wc == WeaponClass.Shotgun || wc == WeaponClass.Sniper ? 2.2f : 0.7f);
            }
            SFX.PlayShot(w.data, 0.9f); // 실총 녹음 + 저음 층
            // 탄창이 바닥나 간다: 남은 20% 안이면 쏠 때마다 '틱' (남을수록 높게) — 화면을 안 봐도 장전 때를 안다
            if (w.MagazineSize >= 6 && wc != WeaponClass.Flamethrower)
            {
                int low = Mathf.Max(2, Mathf.CeilToInt(w.MagazineSize * 0.2f));
                if (w.ammoInMag < low) SFX.PlayPitched(SFX.LowAmmoTick, 0.25f, 1f + (low - w.ammoInMag) * 0.08f);
            }
            // 탄피 떨어지는 소리 (펌프·볼트식은 장전 동작 때)
            bool manual = (w.data.weaponClass == WeaponClass.Shotgun && !w.data.automatic) || (w.data.weaponClass == WeaponClass.Sniper && w.data.rpm < 100f);
            if (w.data.weaponClass != WeaponClass.Flamethrower && w.data.weaponClass != WeaponClass.Launcher && !manual)
                SFX.Play(SFX.Casing, 0.18f, 0.2f);
            Noise.Emit(player.transform.position, Noise.ShotRadius(w.data.weaponClass), true);
            Noise.MuzzleFlash();
            float shake = w.data.weaponClass == WeaponClass.Shotgun || w.data.weaponClass == WeaponClass.Sniper || w.data.weaponClass == WeaponClass.Launcher ? 1.4f
                : w.data.weaponClass == WeaponClass.Flamethrower ? 0.1f : 0.45f;
            shake *= 1f + (w.data.tier - 1) * 0.25f; // 큰 구경일수록 더 흔들린다
            player.Controller.AddShake(shake * Mathf.Lerp(1f, 0.5f, aim));
            player.Controller.AddPunch(Punch(w.data.weaponClass) * Mathf.Lerp(1f, 0.6f, aim) * player.Controller.CrouchRecoilMult, Random.Range(-0.35f, 0.35f) * Punch(w.data.weaponClass));

            Vector3 origin = cam.transform.position;
            Vector3 muzzle = viewModel.MuzzlePosition;
            Color tracer = w.DamageType == DamageType.Physical ? new Color(1f, 0.85f, 0.5f) : CombatNames.ColorOf(w.DamageType);
            int pellets = Mathf.Max(1, w.data.pellets);
            float tracerWidth = pellets > 1 ? 0.012f : 0.025f;

            bool anyHit = false, anyWeak = false, anyKill = false, anyArmor = false, anyEliteKill = false;
            Vector3 killPos = Vector3.zero, hurtPos = Vector3.zero;

            // 유탄·로켓: 날아가서 터지는 탄
            if (w.data.explosive)
            {
                Vector3 d0 = SpreadDirection(cam.transform, CurrentSpread);
                LauncherShell.Fire(muzzle, d0 * w.data.projectileSpeed, player, info.baseDamage, w.data.blastRadius, w.data.tier >= 3);
                pellets = 0;
            }
            bool flame = w.data.weaponClass == WeaponClass.Flamethrower;
            float range = Mathf.Min(300f, w.data.maxRange);

            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = SpreadDirection(cam.transform, CurrentSpread);
                Vector3 end = origin + dir * Mathf.Min(150f, range);
                // 총알 궤적: 벽은 두께를 재서 관통력 안이면 뚫고(피해 감소), 관통력 높은 총은 망자도 뚫고 다음 망자까지
                Vector3 rayFrom = origin;
                float left = range, dmgScale = 1f;
                float wallPower = flame ? 0f : stats.WallPenetration(w.data.penetration);
                float pelletBase = info.baseDamage;
                int monstersHit = 0;
                Monster lastMonster = null;
                for (int seg = 0; seg < 8 && left > 0.1f; seg++)
                {
                    if (!Physics.Raycast(rayFrom, dir, out RaycastHit hit, left, RaycastMask, QueryTriggerInteraction.Ignore))
                    {
                        end = rayFrom + dir * Mathf.Min(left, 150f);
                        break;
                    }
                    end = hit.point;
                    left -= hit.distance;
                    var part = hit.collider.GetComponent<MonsterPart>();
                    if (part != null)
                    {
                        if (part.owner == lastMonster) { rayFrom = hit.point + dir * 0.05f; left -= 0.05f; continue; } // 같은 망자의 다른 판정체
                        info.baseDamage = pelletBase * dmgScale;
                        info.distance = range - left;
                        info.hitPoint = hit.point;
                        info.direction = dir;
                        var res = part.ReceiveHit(info);
                        if (res.hit)
                        {
                            anyHit = true;
                            hurtPos = hit.point;
                            anyWeak |= res.zone == HitZone.WeakPoint || res.zone == HitZone.Head;
                            anyArmor |= res.zone == HitZone.Armor;
                            if (res.killed)
                            {
                                anyKill = true;
                                if (!part.owner.IsBoss && part.owner.Data.archetype != Hunt.Data.MonsterArchetype.Runner) anyEliteKill = true;
                                killPos = hit.point;
                                // 머리·급소로 끝내면 크게 터진다
                                if (res.zone == HitZone.Head || res.zone == HitZone.WeakPoint)
                                {
                                    FX.BloodBurst(hit.point, res.zone == HitZone.WeakPoint ? new Color(1f, 0.55f, 0.1f) : new Color(0.45f, 0.04f, 0.06f), 0.7f);
                                    // 살점이 총알 방향으로 튀어 오른다 (타격감)
                                    int n = !GameSettings.Gore ? 0 : QualityManager.Level == GfxLevel.Low ? 3 : 6; // 피 표현 끔이면 살점 없음
                                    for (int g = 0; g < n; g++)
                                        Hunt.Monsters.MonsterGib.Chunk(null, hit.point + Random.insideUnitSphere * 0.08f,
                                            dir * Random.Range(1.5f, 4.5f) + Vector3.up * Random.Range(2f, 4.5f) + Random.insideUnitSphere * 1.5f,
                                            Color.Lerp(new Color(0.3f, 0.03f, 0.04f), new Color(0.5f, 0.38f, 0.32f), Random.value * 0.4f), Random.Range(0.05f, 0.11f));
                                }
                            }
                            if (res.zone == HitZone.Head && (res.killed || Random.value < 0.3f)) part.owner.KnockOffHat(dir); // 갓이 날아간다
                            FX.ImpactFlesh(hit.point, dir, FleshColor(res.zone), res.crit ? 10 : 5);
                            // 뒤쪽 벽·바닥에 피가 튄다 (처치·머리·치명타는 항상, 그 외 가끔 — 너무 많지 않게)
                            if (res.killed || res.zone == HitZone.Head || res.crit || Random.value < 0.25f)
                                FX.BloodSplat(hit.point + dir * 0.35f, (dir + Vector3.down * 0.25f).normalized, 2.8f, res.killed ? 0.75f : 0.5f);
                        }
                        OnHitResult(w, res, info, hit.point);
                        lastMonster = part.owner;
                        monstersHit++;
                        if (monstersHit >= 2 || w.data.penetration < 0.6f) break;
                        dmgScale *= 0.55f;
                        rayFrom = hit.point + dir * 0.05f;
                        left -= 0.05f;
                        continue;
                    }

                    if (hit.collider.name == "LampHead") { var lampHit = hit.collider.GetComponentInParent<ShootableLamp>(); if (lampHit != null) lampHit.Hit(hit.point); }
                    var boom = hit.collider.GetComponentInParent<ExplosiveProp>();
                    if (boom != null) boom.Hit(w.Damage * dmgScale, flame);
                    if (!flame) { FX.ImpactWorld(hit.point, hit.normal); ImpactSound(hit.point, hit.normal); }
                    if (flame || wallPower <= 0.02f) break;
                    // 벽 두께: 관통 깊이만큼 앞에서 거꾸로 쏴서 빠져나오는 면을 찾는다 (없으면 너무 두꺼움)
                    float probe = wallPower + 0.05f;
                    if (!hit.collider.Raycast(new Ray(hit.point + dir * probe, -dir), out RaycastHit exit, probe)) break;
                    float thick = Mathf.Max(0.01f, probe - exit.distance);
                    if (thick > wallPower) break;
                    wallPower -= thick;
                    dmgScale *= Mathf.Clamp(1f - thick * 1.4f, 0.3f, 0.9f);
                    FX.ImpactWorld(exit.point, exit.normal); // 뒷면 탄흔
                    rayFrom = exit.point + dir * 0.02f;
                    left -= thick + 0.02f;
                }
                info.baseDamage = pelletBase;
                // 화염: 굵고 짧은 불줄기 / 산탄은 일부 펠릿만 예광탄 표시
                if (flame) { if (i == 0) FX.Tracer(muzzle, Vector3.Lerp(muzzle, end, Random.Range(0.6f, 1f)), new Color(1f, Random.Range(0.35f, 0.6f), 0.1f), Random.Range(0.12f, 0.22f)); }
                else if (pellets == 1 || i % 3 == 0) FX.Tracer(muzzle, end, tracer, tracerWidth);
            }

            if (anyHit) SFX.Play(SFX.HitFor(w.data.weaponClass), pellets > 1 ? 0.8f : 0.6f, 0.08f);
            if (anyArmor) { SFX.PlayPitched(SFX.Casing, 0.7f, 0.8f); SFX.PlayPitched(SFX.HitTick, 0.35f, 1.9f); } // 갑옷에 튕기는 '팅'
            if (anyKill)
            {
                SFX.Play(SFX.KillTick, 0.6f, 0f);
                SFX.PlayAt(SFX.MonsterDie, killPos, 0.9f);
                bool eliteKill = anyEliteKill;
                GameManager.Instance.HitStop(eliteKill ? 0.13f : 0.06f); // 처치 순간 멈칫 (엘리트는 더 길게)
                if (eliteKill) player.Controller.AddShake(1.4f);
            }
            else if (anyHit)
            {
                if (anyWeak && w.data.weaponClass == WeaponClass.Sniper) GameManager.Instance.HitStop(0.04f);
                SFX.Play(anyWeak ? SFX.WeakTick : SFX.HitTick, 0.45f, 0f);
                SFX.MonsterHurtAt(hurtPos);
            }

            // C: 고정 패턴 × (제어 스탯 + 파츠) + 제어 스탯에 반비례하는 노이즈
            Vector2 pattern = RecoilPattern.Kick(w.data, shotIndex);
            float aimDamp = Mathf.Lerp(1f, 0.75f, aim);
            Vector2 noise = Random.insideUnitCircle * pattern.magnitude * 0.6f * stats.RecoilNoise;
            player.Controller.AddRecoil((pattern * stats.RecoilPatternScale + noise) * w.RecoilMult * aimDamp * player.Controller.CrouchRecoilMult); // 앉으면 반동 감소
            shotIndex++;

            if (w.ammoInMag == 0 && w.reserve > 0) StartReload(w);
        }

        /// <summary>무기별 시점 반동 크기 (도).</summary>
        static float Punch(WeaponClass c)
        {
            switch (c)
            {
                case WeaponClass.Pistol: return 1.3f;
                case WeaponClass.SMG: return 0.55f;
                case WeaponClass.Shotgun: return 3.4f;
                case WeaponClass.Sniper: return 4.2f;
                case WeaponClass.LMG: return 0.75f;
                case WeaponClass.Flamethrower: return 0.08f;
                case WeaponClass.Launcher: return 3.6f;
                default: return 0.9f;
            }
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
            if (res.killed && res.zone == HitZone.Head) { Achievements.Add("head100"); player.RunHeadKills++; }
            if (res.killed && w != null)
            {
                int before = w.Mastery;
                w.kills++;
                if (w.Mastery >= 4) Achievements.Unlock("master");
                if (w.Mastery > before)
                {
                    HUD.Notify($"{w.data.displayName} 숙련 — {WeaponInstance.MasteryName(w.Mastery)}  (위력 +{(w.MasteryMult - 1f) * 100f:0}%)", new Color(1f, 0.85f, 0.45f));
                    SFX.PlayPitched(SFX.LevelUp, 0.5f, 1.2f);
                }
            }

            if (res.zone == HitZone.WeakPoint && w.Has(AffixType.AmmoOnWeakPoint) && Random.value < w.Sum(AffixType.AmmoOnWeakPoint) / 100f)
                w.ammoInMag = Mathf.Min(w.MagazineSize, w.ammoInMag + 1);

            if (res.killed && w.Has(AffixType.ExplodeOnKill) && Random.value < w.Sum(AffixType.ExplodeOnKill) / 100f)
                Explode(point, w.Damage * 2f * Mathf.Max(1, w.data.pellets) * 0.5f, info.type);
        }

        void Explode(Vector3 point, float damage, DamageType type)
        {
            Effects.Burst(point, 3f, CombatNames.ColorOf(type == DamageType.Physical ? DamageType.Fire : type));
            SFX.PlayAt(SFX.Shot(WeaponClass.Shotgun), point, 1f);
            Noise.Emit(point, 35f, true);
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
                s.reserve = Mathf.RoundToInt(s.MaxReserve * GameRules.AmmoMult);
                s.ammoInMag = s.MagazineSize;
            }
        }
    }
}

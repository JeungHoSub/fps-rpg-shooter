using System.Collections.Generic;
using Hunt.Combat;
using Hunt.Core;
using Hunt.Monsters;
using Hunt.Weapons;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>5번 슬롯 특수 기기.</summary>
    public enum GadgetType { Grenade, Mine, Turret, Molotov }

    public static class Gadgets
    {
        public const int Count = 4;

        public static string Name(GadgetType t)
        {
            switch (t)
            {
                case GadgetType.Grenade: return "투척 수류탄";
                case GadgetType.Mine: return "설치형 지뢰";
                case GadgetType.Molotov: return "화염병";
                default: return "설치형 기관총";
            }
        }

        public static string Desc(GadgetType t)
        {
            switch (t)
            {
                case GadgetType.Grenade: return "던지면 2.5초 뒤 터진다 (반경 6m)";
                case GadgetType.Mine: return "발밑에 묻는다. 망자가 밟으면 터진다";
                case GadgetType.Molotov: return "깨지면 7초쯤 불바다 (3m, 비에 빨리 꺼짐) · 덤불도 태운다";
                default: return "30초 동안 가까운 망자를 쏜다. 총성이 망자를 끌어들인다";
            }
        }

        public static int MaxCarry(GadgetType t) => t == GadgetType.Grenade ? 4 : t == GadgetType.Mine ? 3 : t == GadgetType.Molotov ? 3 : 1;

        /// <summary>폭발: 반경 안 망자에게 거리 비례 피해, 너무 가까우면 나도 다친다.</summary>
        public static void Explode(Vector3 point, float radius, float damage, PlayerCharacter owner)
        {
            if (owner != null) damage *= owner.Stats.GadgetDamageMult; // 기술
            ExplosionFx.Spawn(point, radius * 0.6f, new Color(1f, 0.55f, 0.2f));
            FX.ImpactWorld(point, Vector3.up);
            SFX.PlayAt(SFX.Explosion, point, 1f);
            Noise.Emit(point, 55f, true);
            Noise.MuzzleFlash();

            var hits = Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore);
            var done = new HashSet<Monster>();
            foreach (var h in hits)
            {
                // 근처 가스통·드럼통도 불붙어 연쇄로 터진다
                var prop = h.GetComponentInParent<ExplosiveProp>();
                if (prop != null) prop.Hit(9999f, true);
                var part = h.GetComponent<MonsterPart>();
                if (part == null || part.owner == null || part.owner.IsDead || !done.Add(part.owner)) continue;
                float d = Vector3.Distance(point, part.owner.transform.position);
                float k = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(d / radius));
                part.owner.ApplyDirectDamage(damage * k, DamageType.Physical);
                part.owner.Blast(point, k); // 폭발에 밀려 휘청인다
            }
            if (owner != null && !owner.IsDead)
            {
                float pd = Vector3.Distance(point, owner.transform.position + Vector3.up);
                if (pd < radius * 0.6f) { owner.DeathCause = "제 폭발에 휘말려 쓰러짐"; owner.TakeDamage(damage * 0.12f * (1f - pd / (radius * 0.6f)), 0f); }
                if (pd < radius * 3f) owner.Controller.AddShake(Mathf.Lerp(3f, 0.5f, pd / (radius * 3f)));
                if (pd < radius * 1.6f) EarRing.Hit(1f - pd / (radius * 1.6f)); // 가까우면 귀가 먹먹
            }
        }

        public static Material Dark => EnvMaterials.Plain(new Color(0.12f, 0.13f, 0.11f), 0.5f, 0.6f);
    }

    /// <summary>던진 수류탄: 굴러가다 터진다.</summary>
    public class ThrownGrenade : MonoBehaviour
    {
        /// <summary>날아가거나 굴러다니는 수류탄들 (HUD가 가까우면 경고).</summary>
        public static readonly List<ThrownGrenade> Live = new List<ThrownGrenade>();
        void OnEnable() { Live.Add(this); }
        void OnDisable() { Live.Remove(this); }
        public float Fuse => fuse;
        float fuse = 2.5f;
        PlayerCharacter owner;
        float damage;
        Renderer spark;      // 깜빡이는 불씨 (터질 때가 가까울수록 빨라진다)
        float blinkClock;
        bool sparkOn;

        public static void Throw(Vector3 from, Vector3 velocity, PlayerCharacter owner, float damage)
        {
            var go = new GameObject("Grenade");
            go.layer = 2;
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(velocity);
            var vis = PropModels.PlaceHeld("stick_grenade", go.transform, 0.36f, 2, new Color(0.7f, 0.7f, 0.65f));
            if (vis == null)
            {
                var s = FX.LowSphere(); // 가벼운 공 (판정 없음)
                s.transform.SetParent(go.transform, false);
                s.transform.localScale = Vector3.one * 0.12f;
                s.GetComponent<Renderer>().sharedMaterial = Gadgets.Dark;
                s.layer = 2;
            }
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.07f;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.6f;
#if UNITY_2023_3_OR_NEWER
            rb.linearDamping = 0.2f;
            rb.angularDamping = 1.5f;
            rb.linearVelocity = velocity;
#else
            rb.drag = 0.2f;
            rb.angularDrag = 1.5f;
            rb.velocity = velocity;
#endif
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var g = go.AddComponent<ThrownGrenade>();
            g.owner = owner;
            g.damage = damage;
            var sp = FX.LowSphere(); // 가벼운 공 (판정 없음)
            sp.layer = 2;
            sp.transform.SetParent(go.transform, false);
            sp.transform.localScale = Vector3.one * 0.06f;
            g.spark = sp.GetComponent<Renderer>();
            g.spark.sharedMaterial = FX.UnlitMaterial(new Color(1f, 0.25f, 0.1f));
            g.spark.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.spark.enabled = false;
            FX.MakeGlow(sp, new Color(1f, 0.35f, 0.1f, 0.8f), 5f, false); // 불씨 빛 (불씨와 함께 깜빡인다)
            foreach (Transform c in sp.transform) c.gameObject.SetActive(false);
            SFX.Play(SFX.Swap, 0.5f);
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.relativeVelocity.magnitude > 2f) SFX.PlayAt(SFX.MagIn, transform.position, 0.35f);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            fuse -= Time.deltaTime;
            if (spark != null)
            {
                // 깜빡임 간격: 처음 0.45초 → 터지기 직전 0.08초, 켜질 때마다 째깍
                float interval = Mathf.Lerp(0.08f, 0.45f, Mathf.Clamp01(fuse / 2.5f));
                blinkClock -= Time.deltaTime;
                if (blinkClock <= 0f)
                {
                    sparkOn = !sparkOn;
                    blinkClock = sparkOn ? 0.05f : interval;
                    spark.enabled = sparkOn;
                    foreach (Transform c in spark.transform) c.gameObject.SetActive(sparkOn);
                    if (sparkOn) SFX.PlayAt(SFX.HitTick, transform.position, 0.25f);
                }
            }
            if (fuse > 0f) return;
            Gadgets.Explode(transform.position, 6f, damage, owner);
            Destroy(gameObject);
        }
    }

    /// <summary>화염병: 던지면 처음 부딪히는 곳에서 깨져 불바다(FireZone)가 된다.</summary>
    public class ThrownMolotov : MonoBehaviour
    {
        PlayerCharacter owner;
        float damage, life = 4f;
        bool broken;

        public static void Throw(Vector3 from, Vector3 velocity, PlayerCharacter owner, float damage)
        {
            var go = new GameObject("Molotov");
            go.layer = 2;
            go.transform.position = from;
            // 병 + 불붙은 심지
            var bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(bottle.GetComponent<Collider>());
            bottle.layer = 2;
            bottle.transform.SetParent(go.transform, false);
            bottle.transform.localScale = new Vector3(0.08f, 0.1f, 0.08f);
            bottle.GetComponent<Renderer>().sharedMaterial = EnvMaterials.Plain(new Color(0.22f, 0.36f, 0.2f), 0.92f, 0.1f); // 반들거리는 초록 유리 소주병
            var wick = FX.LowSphere(); // 가벼운 공 (판정 없음)
            wick.layer = 2;
            wick.transform.SetParent(go.transform, false);
            wick.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            wick.transform.localScale = Vector3.one * 0.06f;
            wick.GetComponent<Renderer>().sharedMaterial = FX.UnlitMaterial(new Color(1f, 0.6f, 0.15f));
            FX.MakeGlow(wick, new Color(1f, 0.55f, 0.15f, 0.8f), 6f, true); // 심지 불: 번지는 불빛
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.06f;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
#if UNITY_2023_3_OR_NEWER
            rb.linearVelocity = velocity;
#else
            rb.velocity = velocity;
#endif
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var m = go.AddComponent<ThrownMolotov>();
            m.owner = owner;
            m.damage = damage;
            SFX.Play(SFX.Swing, 0.5f, 0.1f);
        }

        void OnCollisionEnter(Collision c)
        {
            if (broken || c.collider is CharacterController) return;
            Break(c.contacts.Length > 0 ? c.contacts[0].point : transform.position);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            life -= Time.deltaTime;
            if (life <= 0f && !broken) Break(transform.position);
        }

        void Break(Vector3 at)
        {
            broken = true;
            SFX.PlayAt(SFX.Casing, at, 1f);           // 유리 깨지는 소리 (짧고 높게)
            SFX.PlayAt(SFX.Explosion, at, 0.35f);     // 확 붙는 소리
            FireZone.Spawn(FX.FloorUnder(at + Vector3.up * 0.4f), 3f, 7f, damage, owner);
            Destroy(gameObject);
        }
    }

    /// <summary>불바다: radius 안 망자에게 0.4초마다 불 피해, 나도 들어가면 조금 뜨겁다. 일렁이는 불꽃 도형 + 빛.</summary>
    public class FireZone : MonoBehaviour
    {
        float radius, life, maxLife, dps, tick;
        int kills;
        float crackleClock;
        PlayerCharacter owner;
        Transform[] flames;
        Light glow;

        public static void Spawn(Vector3 at, float radius, float seconds, float dps, PlayerCharacter owner)
        {
            var go = new GameObject("불바다");
            if (Effects.Root != null) go.transform.SetParent(Effects.Root, true);
            go.transform.position = at;
            var z = go.AddComponent<FireZone>();
            // 빗줄기가 셀수록 불이 빨리 꺼진다 (폭우면 70%)
            float rain = Atmosphere.Instance != null ? Atmosphere.Instance.RainIntensity : 0.5f;
            seconds *= Mathf.Lerp(1.05f, 0.7f, Mathf.InverseLerp(0.3f, 1f, rain));
            z.radius = radius; z.life = z.maxLife = seconds; z.dps = dps; z.owner = owner;
            z.Build();
            Noise.Emit(at, 18f, true);
            FX.Scorch(at, radius * 0.7f);
            Hiding.Burn(at, radius);
        }

        void Build()
        {
            bool low = QualityManager.Level == GfxLevel.Low;
            int n = low ? 6 : 11;
            flames = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                // 불꽃 혀 모양 판 (카메라 쪽으로 돌아선다) — 단색 공보다 진짜 불처럼
                var f = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(f.GetComponent<Collider>());
                f.layer = 2;
                f.transform.SetParent(transform, false);
                Vector2 p = Random.insideUnitCircle * radius * 0.85f;
                f.transform.localPosition = new Vector3(p.x, 0.25f, p.y);
                var r = f.GetComponent<Renderer>();
                r.sharedMaterial = FX.FlameMaterial();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                flames[i] = f.transform;
            }
            if (!low)
            {
                glow = new GameObject("불빛").AddComponent<Light>();
                glow.transform.SetParent(transform, false);
                glow.transform.localPosition = Vector3.up * 1f;
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.55f, 0.2f);
                glow.range = radius * 3f;
                glow.intensity = 2.5f;
                glow.shadows = LightShadows.None;
            }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            float dt = Time.deltaTime;
            life -= dt;
            float fade = Mathf.Clamp01(life / 1.2f) * Mathf.Clamp01((maxLife - life) / 0.25f);
            float t = Time.time;
            var camT = gm != null && gm.ActiveCamera != null ? gm.ActiveCamera.transform : null;
            for (int i = 0; i < flames.Length; i++)
            {
                if (flames[i] == null) continue;
                float h = (0.5f + 0.5f * Mathf.PerlinNoise(i * 3.1f, t * 4f)) * 1.1f * fade * 1.35f;
                float wv = (0.55f + 0.15f * Mathf.PerlinNoise(t * 5f, i * 1.7f)) * fade;
                flames[i].localScale = new Vector3(wv, h, 1f);
                var lp = flames[i].localPosition; lp.y = h * 0.45f; flames[i].localPosition = lp;
                if (camT != null)
                {
                    Vector3 to = flames[i].position - camT.position; to.y = 0f;
                    if (to.sqrMagnitude > 0.0001f) flames[i].rotation = Quaternion.LookRotation(to) * Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(i, t * 3f) - 0.5f) * 14f);
                }
            }
            if (glow != null) glow.intensity = (2f + Mathf.PerlinNoise(t * 6f, 1f)) * fade;
            crackleClock -= dt;
            if (crackleClock <= 0f && life > 0.5f) { crackleClock = 0.5f; SFX.PlayAt3D(SFX.Crackle, transform.position + Vector3.up * 0.5f, 0.8f, Random.Range(0.9f, 1.1f)); }
            tick -= dt;
            if (tick <= 0f && life > 0f)
            {
                tick = 0.4f;
                float r2 = radius * radius;
                foreach (var h in Physics.OverlapSphere(transform.position, radius, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    var prop = h.GetComponentInParent<ExplosiveProp>();
                    if (prop != null) prop.Hit(dps * 0.4f, true); // 불바다 옆 가스통도 달아오른다
                }
                foreach (var m in Monster.Alive.ToArray())
                {
                    if (m == null || m.IsDead) continue;
                    Vector3 d = m.transform.position - transform.position; d.y = 0f;
                    if (d.sqrMagnitude > r2) continue;
                    if (m.ApplyDirectDamage(dps * 0.4f, DamageType.Fire) && ++kills >= 4) Achievements.Unlock("molotov");
                    m.Ignite(dps * 0.35f); // 불바다를 빠져나가도 잠깐 더 탄다
                    m.Alert(5f);
                }
                if (owner != null && !owner.IsDead)
                {
                    Vector3 d = owner.transform.position - transform.position; d.y = 0f;
                    if (d.sqrMagnitude < r2 * 0.6f) { owner.DeathCause = "불길 속에서 쓰러짐"; owner.TakeDamage(3f, 0f); } // 나도 뜨겁다
                }
            }
            if (life <= 0f) Destroy(gameObject);
        }
    }

    /// <summary>설치형 지뢰: 1초 뒤 작동, 망자가 1.6m 안에 들어오면 터진다.</summary>
    public class LandMine : MonoBehaviour
    {
        float armIn = 1f, blink;
        PlayerCharacter owner;
        float damage;
        Renderer led;

        public static void Plant(Vector3 at, PlayerCharacter owner, float damage)
        {
            var go = new GameObject("Mine");
            go.transform.position = Navigation.Snap(at, 2f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            body.transform.localScale = new Vector3(0.32f, 0.04f, 0.32f);
            body.GetComponent<Renderer>().sharedMaterial = EnvMaterials.Plain(new Color(0.2f, 0.24f, 0.16f), 0.4f, 0.3f);
            var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(top.GetComponent<Collider>());
            top.transform.SetParent(go.transform, false);
            top.transform.localPosition = new Vector3(0f, 0.085f, 0f);
            top.transform.localScale = new Vector3(0.1f, 0.012f, 0.1f);
            top.GetComponent<Renderer>().sharedMaterial = Gadgets.Dark;
            var l = FX.LowSphere(); // 가벼운 공 (판정 없음)
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0.1f, 0.09f, 0f);
            l.transform.localScale = Vector3.one * 0.025f;
            var m = go.AddComponent<LandMine>();
            m.led = l.GetComponent<Renderer>();
            m.led.sharedMaterial = FX.UnlitMaterial(new Color(1f, 0.15f, 0.1f));
            m.owner = owner;
            m.damage = damage;
            SFX.PlayAt(SFX.MagIn, go.transform.position, 0.6f);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsWorldRunning) return;
            float dt = Time.deltaTime;
            blink += dt;
            if (led != null) led.enabled = armIn > 0f || Mathf.Repeat(blink, 1.2f) < 0.12f;
            if (armIn > 0f) { armIn -= dt; return; }
            foreach (var mo in Monster.Alive)
            {
                if (mo == null || mo.IsDead) continue;
                Vector3 d = mo.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < 1.6f * 1.6f)
                {
                    Gadgets.Explode(transform.position + Vector3.up * 0.3f, 4.5f, damage, owner);
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }

    /// <summary>설치형 기관총: 가까운 망자를 자동으로 쏜다. 총성은 망자를 그쪽으로 끌어들인다 (미끼).</summary>
    public class SentryTurret : MonoBehaviour
    {
        float life = 30f, fireTimer, seekTimer;
        int ammo = 240;
        public const float MaxLife = 30f;
        public const int MaxAmmo = 240;
        /// <summary>깔려 있는 기관총들 (HUD가 머리 위에 남은 시간·탄을 그린다).</summary>
        public static readonly System.Collections.Generic.List<SentryTurret> Active = new System.Collections.Generic.List<SentryTurret>();
        public float Life => life;
        public int Ammo => ammo;

        /// <summary>플레이어 2m 안의 기관총 (E로 거두기).</summary>
        public static SentryTurret NearestToPlayer()
        {
            var gm = GameManager.Instance;
            var p = gm != null ? gm.Player : null;
            if (p == null || p.IsDead) return null;
            foreach (var t in Active)
            {
                if (t == null) continue;
                Vector3 d = t.transform.position - p.transform.position; d.y = 0f;
                if (d.sqrMagnitude < 4f && MaxLife - t.life > 2f) return t; // 막 깐 기관총은 거두지 않는다
            }
            return null;
        }
        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }
        float damage;
        Monster target;
        Transform head, muzzle;

        public static void Deploy(Vector3 at, float yaw, float damage)
        {
            var go = new GameObject("Sentry");
            go.transform.position = Navigation.Snap(at, 2f);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var metal = EnvMaterials.Plain(new Color(0.22f, 0.24f, 0.2f), 0.55f, 0.6f);
            for (int i = 0; i < 3; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(leg.GetComponent<Collider>());
                leg.transform.SetParent(go.transform, false);
                var dir = Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward;
                leg.transform.localPosition = dir * 0.22f + Vector3.up * 0.32f;
                leg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (Vector3.up * 0.65f - dir * 0.45f).normalized);
                leg.transform.localScale = new Vector3(0.035f, 0.36f, 0.035f);
                leg.GetComponent<Renderer>().sharedMaterial = metal;
            }
            var h = new GameObject("Head").transform;
            h.SetParent(go.transform, false);
            h.localPosition = new Vector3(0f, 0.72f, 0f);
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(h, false);
            box.transform.localScale = new Vector3(0.16f, 0.16f, 0.5f);
            box.GetComponent<Renderer>().sharedMaterial = metal;
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(barrel.GetComponent<Collider>());
            barrel.transform.SetParent(h, false);
            barrel.transform.localPosition = new Vector3(0f, 0.02f, 0.5f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.05f, 0.3f, 0.05f);
            barrel.GetComponent<Renderer>().sharedMaterial = Gadgets.Dark;
            var mz = new GameObject("Muzzle").transform;
            mz.SetParent(h, false);
            mz.localPosition = new Vector3(0f, 0.02f, 0.82f);
            var ammoBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(ammoBox.GetComponent<Collider>());
            ammoBox.transform.SetParent(h, false);
            ammoBox.transform.localPosition = new Vector3(0.13f, -0.04f, 0.05f);
            ammoBox.transform.localScale = new Vector3(0.1f, 0.14f, 0.22f);
            ammoBox.GetComponent<Renderer>().sharedMaterial = EnvMaterials.Plain(new Color(0.3f, 0.32f, 0.22f), 0.4f);
            var t = go.AddComponent<SentryTurret>();
            t.head = h;
            t.muzzle = mz;
            t.damage = damage;
            SFX.PlayAt(SFX.Rack, go.transform.position, 0.8f);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsWorldRunning) return;
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0f || ammo <= 0) { FX.ImpactWorld(head.position, Vector3.up); Destroy(gameObject); return; }
            // E: 가까이서 거둬 다시 가방에 넣는다 (반 넘게 남았을 때만 — 아니면 고장 나서 못 쓴다)
            if (gm.AcceptsGameplayInput && GameInput.PickupDown && MaxLife - life > 2f && !Pickup.AnyNearPlayer() && ReferenceEquals(NearestToPlayer(), this))
            {
                var pl = gm.Player;
                int gi = (int)GadgetType.Turret;
                if (life > MaxLife * 0.5f && ammo > MaxAmmo / 2 && pl.Weapons.GadgetCount[gi] < Gadgets.MaxCarry(GadgetType.Turret))
                {
                    pl.Weapons.GadgetCount[gi]++;
                    Hunt.UI.HUD.Notify("설치형 기관총을 거뒀다", new Color(0.8f, 0.9f, 1f));
                    SFX.Play(SFX.Rack, 0.7f);
                    Destroy(gameObject);
                    return;
                }
                Hunt.UI.HUD.Notify("너무 오래 써서 거둘 수 없다 (반 넘게 남았을 때만)", new Color(0.7f, 0.68f, 0.64f));
            }

            seekTimer -= dt;
            if (seekTimer <= 0f || target == null || target.IsDead)
            {
                seekTimer = 0.3f;
                target = null;
                float best = 24f * 24f;
                foreach (var m in Monster.Alive)
                {
                    if (m == null || m.IsDead) continue;
                    float d = (m.transform.position - transform.position).sqrMagnitude;
                    if (d < best && Visible(m)) { best = d; target = m; }
                }
            }
            if (target == null) return;

            Vector3 aimAt = target.transform.position + Vector3.up * 1.2f * target.Data.scale;
            Vector3 dir = aimAt - head.position;
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(dir), dt * 10f);
            if (Vector3.Angle(head.forward, dir) > 8f) return;

            fireTimer -= dt;
            if (fireTimer > 0f) return;
            fireTimer = 0.11f;
            ammo--;
            Vector3 shot = (dir.normalized + Random.insideUnitSphere * 0.03f).normalized;
            Vector3 end = muzzle.position + shot * 40f;
            if (Physics.Raycast(muzzle.position, shot, out RaycastHit hit, 40f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var part = hit.collider.GetComponent<MonsterPart>();
                if (part != null && part.owner != null)
                {
                    var info = new DamageInfo
                    {
                        baseDamage = damage, type = DamageType.Physical, weaponClass = WeaponClass.LMG, critChance = 0.03f, critMultiplier = 1.5f,
                        penetration = 0.15f, knockback = 0.4f, distance = hit.distance, falloffStart = 20f, falloffEnd = 40f,
                        minFalloffMultiplier = 0.6f, hitPoint = hit.point, direction = shot,
                    };
                    part.ReceiveHit(info);
                    FX.ImpactFlesh(hit.point, shot, new Color(0.45f, 0.05f, 0.08f), 4);
                }
                else FX.ImpactWorld(hit.point, hit.normal);
            }
            FX.Tracer(muzzle.position, end, new Color(1f, 0.85f, 0.5f), 0.02f);
            SFX.PlayAt(SFX.Shot(WeaponClass.LMG), muzzle.position, 0.7f);
            // 기관총 소리는 망자를 이쪽으로 부른다 (플레이어 위치는 모른다)
            Noise.Emit(transform.position, 35f, false);
        }

        bool Visible(Monster m)
        {
            Vector3 to = m.transform.position + Vector3.up * 1.2f * m.Data.scale;
            if (!Physics.Linecast(head.position, to, out RaycastHit hit, ~(1 << 2), QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.GetComponent<MonsterPart>() != null;
        }
    }

    /// <summary>유탄·로켓 탄: 직선에 가깝게 날아가다 무엇에든 닿으면 터진다.</summary>
    public class LauncherShell : MonoBehaviour
    {
        Vector3 vel;
        PlayerCharacter owner;
        float damage, radius, life = 6f;
        bool rocket;

        public static void Fire(Vector3 from, Vector3 velocity, PlayerCharacter owner, float damage, float radius, bool rocket)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(go.GetComponent<Collider>());
            go.name = rocket ? "Rocket" : "Grenade40";
            go.layer = 2;
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(velocity) * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = rocket ? new Vector3(0.09f, 0.3f, 0.09f) : new Vector3(0.05f, 0.05f, 0.05f);
            go.GetComponent<Renderer>().sharedMaterial = Gadgets.Dark;
            var s = go.AddComponent<LauncherShell>();
            s.vel = velocity; s.owner = owner; s.damage = damage; s.radius = radius; s.rocket = rocket;
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = rocket ? 0.5f : 0.2f;
            tr.startWidth = rocket ? 0.18f : 0.05f;
            tr.endWidth = 0f;
            tr.sharedMaterial = FX.BeamMaterial(); // 가장자리가 번지는 꼬리
            tr.textureMode = LineTextureMode.Stretch;
            tr.startColor = rocket ? new Color(1f, 0.75f, 0.4f, 0.9f) : new Color(0.7f, 0.7f, 0.7f, 0.5f);
            tr.endColor = new Color(tr.startColor.r, tr.startColor.g, tr.startColor.b, 0f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        float puffClock;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            float dt = Time.deltaTime;
            life -= dt;
            if (!rocket) vel += Physics.gravity * 0.35f * dt; // 유탄은 살짝 떨어진다
            Vector3 step = vel * dt;
            bool hitSomething = Physics.Raycast(transform.position, step.normalized, out RaycastHit hit, step.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore);
            if (life <= 0f || hitSomething)
            {
                Vector3 at = hitSomething ? hit.point - step.normalized * 0.2f : transform.position;
                Gadgets.Explode(at, radius, damage, owner);
                Destroy(gameObject);
                return;
            }
            transform.position += step;
            transform.rotation = Quaternion.LookRotation(vel) * Quaternion.Euler(90f, 0f, 0f);
            // 로켓은 뒤로 회색 연기를 길게 남긴다
            puffClock -= dt;
            if (puffClock <= 0f)
            {
                puffClock = rocket ? 0.025f : 0.06f;
                FX.Puff(transform.position - vel.normalized * 0.2f, rocket ? 0.3f : 0.15f, rocket ? new Color(0.55f, 0.53f, 0.5f, 0.4f) : new Color(0.6f, 0.6f, 0.6f, 0.2f));
            }
        }
    }
}

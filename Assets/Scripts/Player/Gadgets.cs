using System.Collections.Generic;
using Hunt.Combat;
using Hunt.Core;
using Hunt.Monsters;
using Hunt.Weapons;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>5번 슬롯 특수 기기.</summary>
    public enum GadgetType { Grenade, Mine, Turret }

    public static class Gadgets
    {
        public const int Count = 3;

        public static string Name(GadgetType t)
        {
            switch (t)
            {
                case GadgetType.Grenade: return "투척 수류탄";
                case GadgetType.Mine: return "설치형 지뢰";
                default: return "설치형 기관총";
            }
        }

        public static string Desc(GadgetType t)
        {
            switch (t)
            {
                case GadgetType.Grenade: return "던지면 2.5초 뒤 터진다 (반경 6m)";
                case GadgetType.Mine: return "발밑에 묻는다. 망자가 밟으면 터진다";
                default: return "30초 동안 가까운 망자를 쏜다. 총성이 망자를 끌어들인다";
            }
        }

        public static int MaxCarry(GadgetType t) => t == GadgetType.Grenade ? 4 : t == GadgetType.Mine ? 3 : 1;

        /// <summary>폭발: 반경 안 망자에게 거리 비례 피해, 너무 가까우면 나도 다친다.</summary>
        public static void Explode(Vector3 point, float radius, float damage, PlayerCharacter owner)
        {
            if (owner != null) damage *= owner.Stats.GadgetDamageMult; // 기술
            Effects.Burst(point, radius * 0.6f, new Color(1f, 0.55f, 0.2f));
            FX.ImpactWorld(point, Vector3.up);
            SFX.PlayAt(SFX.Explosion, point, 1f);
            Noise.Emit(point, 55f, true);
            Noise.MuzzleFlash();

            var hits = Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore);
            var done = new HashSet<Monster>();
            foreach (var h in hits)
            {
                var part = h.GetComponent<MonsterPart>();
                if (part == null || part.owner == null || part.owner.IsDead || !done.Add(part.owner)) continue;
                float d = Vector3.Distance(point, part.owner.transform.position);
                float k = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(d / radius));
                part.owner.ApplyDirectDamage(damage * k, DamageType.Physical);
            }
            if (owner != null && !owner.IsDead)
            {
                float pd = Vector3.Distance(point, owner.transform.position + Vector3.up);
                if (pd < radius * 0.6f) owner.TakeDamage(damage * 0.12f * (1f - pd / (radius * 0.6f)), 0f);
                if (pd < radius * 3f) owner.Controller.AddShake(Mathf.Lerp(3f, 0.5f, pd / (radius * 3f)));
            }
        }

        public static Material Dark => EnvMaterials.Plain(new Color(0.12f, 0.13f, 0.11f), 0.5f, 0.6f);
    }

    /// <summary>던진 수류탄: 굴러가다 터진다.</summary>
    public class ThrownGrenade : MonoBehaviour
    {
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
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(s.GetComponent<Collider>());
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
            var sp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sp.GetComponent<Collider>());
            sp.layer = 2;
            sp.transform.SetParent(go.transform, false);
            sp.transform.localScale = Vector3.one * 0.06f;
            g.spark = sp.GetComponent<Renderer>();
            g.spark.sharedMaterial = FX.UnlitMaterial(new Color(1f, 0.25f, 0.1f));
            g.spark.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.spark.enabled = false;
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
                    if (sparkOn) SFX.PlayAt(SFX.HitTick, transform.position, 0.25f);
                }
            }
            if (fuse > 0f) return;
            Gadgets.Explode(transform.position, 6f, damage, owner);
            Destroy(gameObject);
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
            var l = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(l.GetComponent<Collider>());
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
        TrailRenderer trail;

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
            tr.sharedMaterial = FX.UnlitMaterial(rocket ? new Color(1f, 0.7f, 0.4f) : new Color(0.6f, 0.6f, 0.6f));
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

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
        }
    }
}

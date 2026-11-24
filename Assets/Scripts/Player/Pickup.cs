using Hunt.Core;
using Hunt.Monsters;
using Hunt.UI;
using UnityEngine;

namespace Hunt.Player
{
    public enum PickupKind { Kit, Drink, Ammo, Rune }

    /// <summary>
    /// 망자가 떨어뜨리는 보급품: 치료 키트 · 스태미너 회복제 · 탄창 · 치유 부적(힐링룬, 먹으면 바로 회복).
    /// 바닥에서 빛나며 천천히 돌고, 가까이 가면 빨려 들어와 자동으로 줍는다. 35초 뒤 사라진다.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        PickupKind kind;
        float life = 35f, bobSeed;
        Transform visual;
        Vector3 basePos;
        bool toldFull;
        float age; // 떨어진 뒤 지난 시간 (튀어 오르는 연출) // 가득 찬 상태로 밟으면 한 번만 알려 준다

        public static Color ColorOf(PickupKind k) =>
            k == PickupKind.Kit ? new Color(1f, 0.3f, 0.25f) : k == PickupKind.Drink ? new Color(0.35f, 0.75f, 1f)
            : k == PickupKind.Rune ? new Color(0.4f, 1f, 0.55f) : new Color(1f, 0.78f, 0.3f);

        public static string NameOf(PickupKind k) => k == PickupKind.Kit ? "치료 키트" : k == PickupKind.Drink ? "스태미너 회복제" : k == PickupKind.Rune ? "치유 부적" : "탄창";

        /// <summary>치유 부적 하나가 바로 채워 주는 생명 (최대 생명의 비율).</summary>
        public const float RuneHeal = 0.15f;

        /// <summary>처치 보상 판정. 유형이 단단할수록 · 보스는 반드시 떨어뜨린다.</summary>
        public static void RollDrops(Monster m)
        {
            if (m == null) return;
            Vector3 at = m.transform.position;
            Transform parent = m.transform.parent;
            if (m.IsBoss)
            {
                Spawn(PickupKind.Kit, at + Vector3.right * 0.8f, parent);
                Spawn(PickupKind.Drink, at - Vector3.right * 0.8f, parent);
                Spawn(PickupKind.Ammo, at + Vector3.forward * 0.8f, parent);
                Spawn(PickupKind.Ammo, at - Vector3.forward * 0.8f, parent);
                for (int i = 0; i < 3; i++)
                    Spawn(PickupKind.Rune, at + Quaternion.Euler(0f, 45f + i * 120f, 0f) * Vector3.forward * 1.3f, parent);
                return;
            }
            float tough = Mathf.Clamp(m.MaxHp / 90f, 0.5f, 6f); // 단단한 망자일수록 잘 떨어뜨린다
            float mult = Mathf.Lerp(1f, 2.2f, (tough - 0.5f) / 5.5f);
            // 사용자 요청: 잡으면 힐링룬·탄창이 잘 나오게 — 부적 약 20%, 탄창 약 22% (따로 굴려서 둘 다 나올 수도)
            if (Random.value < 0.2f * mult) Spawn(PickupKind.Rune, at + RandomOffset(), parent);
            if (Random.value < 0.22f * mult) Spawn(PickupKind.Ammo, at + RandomOffset(), parent);
            float r = Random.value;
            float pKit = 0.035f * mult, pDrink = 0.045f * mult;
            if (r < pKit) Spawn(PickupKind.Kit, at + RandomOffset(), parent);
            else if (r < pKit + pDrink) Spawn(PickupKind.Drink, at + RandomOffset(), parent);
        }

        static Vector3 RandomOffset() { var c = Random.insideUnitCircle * 0.7f; return new Vector3(c.x, 0f, c.y); }

        public static Pickup Spawn(PickupKind k, Vector3 at, Transform parent)
        {
            var go = new GameObject("보급_" + NameOf(k));
            if (parent != null) go.transform.SetParent(parent, false);
            at.y = 0f;
            go.transform.position = Navigation.Ready ? Navigation.GroundPoint(at, 3f) : at;
            var p = go.AddComponent<Pickup>();
            p.kind = k;
            p.Build();
            return p;
        }

        void Build()
        {
            Color c = ColorOf(kind);
            basePos = transform.position;
            bobSeed = Random.value * 10f;

            visual = new GameObject("모양").transform;
            visual.SetParent(transform, false);
            switch (kind)
            {
                case PickupKind.Kit: BuildKit(c); break;
                case PickupKind.Drink: BuildBottle(c); break;
                case PickupKind.Rune: BuildRune(c); break;
                default: BuildMagazine(); break;
            }

            // 어둠 속에서도 보이게 아래에서 위로 옅은 빛기둥
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(beam.GetComponent<Collider>());
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            beam.transform.localScale = new Vector3(0.05f, 0.9f, 0.05f);
            var br = beam.GetComponent<Renderer>();
            br.sharedMaterial = FX.UnlitMaterial(new Color(c.r, c.g, c.b, 0.28f));
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 작은 빛: '하'에서는 없음, 동시에 4개까지만 (보급품이 많이 떨어져도 조명 부담 제한)
            if (QualityManager.Level != GfxLevel.Low && activeLights < 4)
            {
                activeLights++;
                hasLight = true;
                var l = new GameObject("빛").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.transform.localPosition = Vector3.up * 0.4f;
                l.type = LightType.Point;
                l.color = c;
                l.range = 2.5f;
                l.intensity = 1.2f;
                l.shadows = LightShadows.None;
            }
        }

        // ---- 모양 만들기 (도형 몇 개 + 단색/빛나는 재질, 가볍게) ----
        Transform Part(PrimitiveType t, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(t);
            Destroy(go.GetComponent<Collider>());
            go.layer = 2;
            go.transform.SetParent(visual, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void BuildKit(Color c)
        {
            Part(PrimitiveType.Cube, Vector3.zero, new Vector3(0.34f, 0.2f, 0.26f), FX.UnlitMaterial(c * 0.85f));
            var white = FX.UnlitMaterial(new Color(0.95f, 0.92f, 0.85f));
            Part(PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.03f, 0.06f), white);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(0.06f, 0.03f, 0.2f), white);
        }

        void BuildBottle(Color c)
        {
            Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.12f, 0.15f, 0.12f), FX.UnlitMaterial(c * 0.85f));
            Part(PrimitiveType.Cylinder, new Vector3(0f, 0.19f, 0f), new Vector3(0.05f, 0.05f, 0.05f), FX.UnlitMaterial(new Color(0.85f, 0.8f, 0.7f)));
        }

        /// <summary>탄창: 살짝 휜 검은 탄창 하나 + 위로 보이는 놋쇠 탄알 (도형 8개 — 많이 떨어져도 가볍게).</summary>
        void BuildMagazine()
        {
            var body = EnvMaterials.Plain(new Color(0.16f, 0.17f, 0.19f), 0.6f, 0.55f);
            var brass = FX.UnlitMaterial(new Color(0.95f, 0.72f, 0.3f));
            var tip = FX.UnlitMaterial(new Color(0.8f, 0.45f, 0.25f));
            for (int k = 0; k < 1; k++)
            {
                float x = 0f;
                float tilt = 4f;
                // 아래로 갈수록 앞으로 휘는 탄창: 세 마디
                for (int seg = 0; seg < 3; seg++)
                {
                    float y = -seg * 0.085f;
                    float z = seg * seg * 0.012f;
                    Part(PrimitiveType.Cube, new Vector3(x, y, z), new Vector3(0.06f, 0.09f, 0.13f), body, new Vector3(-seg * 7f + tilt, 0f, 0f));
                }
                Part(PrimitiveType.Cube, new Vector3(x, -0.25f, 0.045f), new Vector3(0.07f, 0.025f, 0.15f), body, new Vector3(-18f, 0f, 0f)); // 바닥판
                // 맨 위 탄알 두 발 (놋쇠 + 탄두)
                for (int b = 0; b < 2; b++)
                {
                    float bx = x + (b == 0 ? -0.012f : 0.012f), bz = b == 0 ? -0.015f : 0.02f;
                    Part(PrimitiveType.Cylinder, new Vector3(bx, 0.055f, bz), new Vector3(0.022f, 0.035f, 0.022f), brass, new Vector3(90f, 0f, 0f));
                    Part(PrimitiveType.Sphere, new Vector3(bx, 0.055f, bz + 0.045f), new Vector3(0.02f, 0.02f, 0.03f), tip);
                }
            }
        }

        /// <summary>치유 부적(힐링룬): 빛나는 옥빛 구슬 + 그 둘레를 도는 가는 고리 두 개.</summary>
        void BuildRune(Color c)
        {
            Part(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.16f, FX.UnlitMaterial(Color.Lerp(c, Color.white, 0.35f)));
            Part(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.26f, FX.UnlitMaterial(new Color(c.r, c.g, c.b, 0.22f))); // 은은한 빛무리
            runeRingA = Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.36f, 0.004f, 0.36f), FX.UnlitMaterial(new Color(c.r, c.g, c.b, 0.55f)), new Vector3(70f, 0f, 0f));
            runeRingB = Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.3f, 0.004f, 0.3f), FX.UnlitMaterial(new Color(c.r, c.g, c.b, 0.45f)), new Vector3(-60f, 40f, 0f));
        }

        Transform runeRingA, runeRingB;
        static int activeLights;
        bool hasLight;

        void OnDestroy() { if (hasLight) activeLights = Mathf.Max(0, activeLights - 1); }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsWorldRunning) return;
            float dt = Time.deltaTime;
            // 새벽(정비 시간): 남은 보급품이 사라지지 않고 멀리서도 끌려온다
            bool dawn = gm.State == GameState.Intermission;
            if (!dawn) life -= dt;
            if (life <= 0f) { Destroy(gameObject); return; }
            if (visual != null)
            {
                age += dt;
                // 떨어질 때: 톡 튀어 올랐다가 내려앉으며 커진다 (0.45초)
                float pop = Mathf.Clamp01(age / 0.45f);
                float hop = Mathf.Sin(pop * Mathf.PI) * 0.55f * (1f - pop * 0.4f);
                float grow = pop < 1f ? Mathf.LerpUnclamped(0.2f, 1f, 1f - (1f - pop) * (1f - pop) * (1f - 2.2f * pop)) : 1f;
                visual.localScale = Vector3.one * grow;
                visual.localPosition = Vector3.up * ((kind == PickupKind.Rune ? 0.55f : 0.3f) + hop + Mathf.Sin(Time.time * 2.5f + bobSeed) * (kind == PickupKind.Rune ? 0.1f : 0.06f));
                visual.Rotate(0f, 90f * dt, 0f, Space.World);
                if (runeRingA != null) runeRingA.Rotate(0f, 0f, 160f * dt, Space.Self);
                if (runeRingB != null) runeRingB.Rotate(0f, 0f, -120f * dt, Space.Self);
                // 사라지기 직전 깜빡임
                visual.gameObject.SetActive(life > 5f || Mathf.Repeat(life, 0.4f) > 0.15f);
            }

            var p = gm.Player;
            if (p == null || p.IsDead) return;
            Vector3 to = p.transform.position - transform.position;
            to.y = 0f;
            float d = to.magnitude;
            bool can = CanTake(p);
            if (!can && d < 1.1f && !toldFull && kind != PickupKind.Rune) // 부적은 많이 떨어져서 '가득' 알림이 시끄럽지 않게 생략
            {
                toldFull = true;
                string full = kind == PickupKind.Rune ? "생명이 가득 차 있다 — 다치면 치유 부적을 주우세요"
                            : kind == PickupKind.Kit ? $"치료 키트 가득 참 (최대 {PlayerCharacter.MaxKits}) — 쓰고 나서 주우세요"
                            : kind == PickupKind.Drink ? $"회복제 가득 참 (최대 {PlayerCharacter.MaxDrinks}) — 쓰고 나서 주우세요"
                            : "예비 탄약 가득 참";
                GameHud.OnPickup(kind == PickupKind.Kit ? HudIcons.Icon.Kit : kind == PickupKind.Drink ? HudIcons.Icon.Bottle : kind == PickupKind.Rune ? HudIcons.Icon.Heart : HudIcons.Icon.Ammo,
                                 new Color(0.6f, 0.58f, 0.54f), full, false);
            }
            else if (d > 3f) toldFull = false;
            float reach = dawn ? 30f : 4f;
            if (d < reach && can)
            {
                // 자석처럼 끌려온다
                float speed = dawn ? 9f + (reach - d) * 0.6f : 3f + (4f - d) * 3f;
                transform.position = Vector3.MoveTowards(transform.position, new Vector3(p.transform.position.x, basePos.y, p.transform.position.z), speed * dt);
                if (d < 0.9f) { Take(p); Destroy(gameObject); }
            }
        }

        bool CanTake(PlayerCharacter p)
        {
            switch (kind)
            {
                case PickupKind.Kit: return p.Kits < PlayerCharacter.MaxKits;
                case PickupKind.Drink: return p.Drinks < PlayerCharacter.MaxDrinks;
                case PickupKind.Rune: return p.Hp < p.MaxHp - 0.5f;
                default:
                    foreach (var w in p.Weapons.Slots)
                        if (w != null && w.reserve < w.MaxReserve) return true;
                    return false;
            }
        }

        void Take(PlayerCharacter p)
        {
            Color c = ColorOf(kind);
            switch (kind)
            {
                case PickupKind.Kit:
                    p.Kits++;
                    GameHud.OnPickup(HudIcons.Icon.Kit, c, $"치료 키트 +1  (지닌 {p.Kits})");
                    break;
                case PickupKind.Rune:
                {
                    float before = p.Hp;
                    p.Heal(p.MaxHp * RuneHeal);
                    GameHud.OnPickup(HudIcons.Icon.Heart, c, $"치유 부적  생명 +{p.Hp - before:0}");
                    SFX.PlayPitched(SFX.LevelUp, 0.35f, 1.35f); // 맑은 방울 소리
                    return;
                }
                case PickupKind.Drink:
                    p.Drinks++;
                    GameHud.OnPickup(HudIcons.Icon.Bottle, c, $"회복제 +1  (지닌 {p.Drinks})");
                    break;
                default:
                {
                    // 들고 있는 총 우선, 탄창 1개 분량 (예비탄 한도까지)
                    var w = p.Weapons.Current;
                    if (w == null || w.reserve >= w.MaxReserve)
                        foreach (var s in p.Weapons.Slots)
                            if (s != null && s.reserve < s.MaxReserve) { w = s; break; }
                    if (w == null) break;
                    int add = Mathf.Max(w.data.magazine, 6);
                    int before = w.reserve;
                    w.reserve = Mathf.Min(w.MaxReserve, w.reserve + add);
                    GameHud.OnPickup(HudIcons.Icon.Ammo, c, $"{w.data.displayName} 탄 +{w.reserve - before}");
                    break;
                }
            }
            // 종류마다 다른 줍는 소리: 키트는 천 스치는 소리, 회복제는 병 찰랑, 탄창은 철컥
            if (kind == PickupKind.Kit) { SFX.PlayPitched(SFX.Bandage, 0.45f, 1.6f); SFX.Play(SFX.UiClick, 0.5f); }
            else if (kind == PickupKind.Drink) SFX.PlayPitched(SFX.Casing, 0.7f, 0.55f);
            else SFX.Play(SFX.MagIn, 0.8f, 0.05f);
            SFX.PlayPitched(SFX.LevelUp, 0.12f, 1.8f); // 아주 작게 '얻었다' 방울
        }
    }
}

using Hunt.Core;
using Hunt.Monsters;
using Hunt.UI;
using UnityEngine;

namespace Hunt.Player
{
    public enum PickupKind { Kit, Drink, Ammo, Rune, Pill, Gear }

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
            : k == PickupKind.Rune ? new Color(1f, 0.86f, 0.45f) : k == PickupKind.Gear ? new Color(0.45f, 0.65f, 1f) : k == PickupKind.Pill ? new Color(0.85f, 0.55f, 1f) : new Color(1f, 0.78f, 0.3f);

        public static string NameOf(PickupKind k) => k == PickupKind.Kit ? "치료 키트" : k == PickupKind.Drink ? "스태미너 회복제" : k == PickupKind.Rune ? "치유 부적" : k == PickupKind.Pill ? "청심환" : k == PickupKind.Gear ? "장비 꾸러미" : "탄약";

        public static HudIcons.Icon IconOf(PickupKind k) =>
            k == PickupKind.Kit ? HudIcons.Icon.Kit : k == PickupKind.Drink ? HudIcons.Icon.Bottle : k == PickupKind.Rune ? HudIcons.Icon.Heart
            : k == PickupKind.Pill ? HudIcons.Icon.Pill : k == PickupKind.Gear ? HudIcons.Icon.Vest : HudIcons.Icon.Ammo;

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
                Spawn(PickupKind.Gear, at + Vector3.up * 0.1f, parent);
                for (int i = 0; i < 3; i++)
                    Spawn(PickupKind.Rune, at + Quaternion.Euler(0f, 45f + i * 120f, 0f) * Vector3.forward * 1.3f, parent);
                return;
            }
            float tough = Mathf.Clamp(m.MaxHp / 90f, 0.5f, 6f); // 단단한 망자일수록 잘 떨어뜨린다
            float mult = Mathf.Lerp(1f, 2.2f, (tough - 0.5f) / 5.5f);
            // 사용자 요청: 잡으면 힐링룬·탄창이 잘 나오게 — 부적 약 20%, 탄창 약 22% (따로 굴려서 둘 다 나올 수도)
            var gmH = GameManager.Instance;
            float hpNeed = gmH != null && gmH.Player != null && gmH.Player.Hp < gmH.Player.MaxHp * 0.35f ? 1.8f : 1f; // 많이 다쳤으면 부적이 더 잘 나온다
            if (Random.value < 0.2f * mult * hpNeed) Spawn(PickupKind.Rune, at + RandomOffset(), parent);
            // 탄약이 바닥나 가면 탄창이 더 잘 나온다 (예비탄 합계 30% 아래면 2배)
            float ammoNeed = 1f;
            var gmA = GameManager.Instance;
            if (gmA != null && gmA.Player != null)
            {
                int have = 0, max = 0;
                foreach (var w in gmA.Player.Weapons.Slots) if (w != null) { have += w.reserve; max += w.MaxReserve; }
                if (max > 0 && have < max * 0.3f) ammoNeed = 2f;
            }
            if (Random.value < 0.22f * mult * ammoNeed) Spawn(PickupKind.Ammo, at + RandomOffset(), parent);
            // 장비 꾸러미: 단단한 망자(거구·침쟁이 등) 약 22%, 보통 망자 2%
            bool tough2 = m.Data != null && m.Data.archetype != Hunt.Data.MonsterArchetype.Runner;
            if (Random.value < (tough2 ? 0.22f : 0.02f)) Spawn(PickupKind.Gear, at + RandomOffset(), parent);
            float r = Random.value;
            float pKit = 0.035f * mult, pDrink = 0.045f * mult;
            if (Random.value < 0.03f * mult) Spawn(PickupKind.Pill, at + RandomOffset(), parent); // 드물게 청심환 (역병 기운을 씻는다, H)
            if (r < pKit) Spawn(PickupKind.Kit, at + RandomOffset(), parent);
            else if (r < pKit + pDrink) Spawn(PickupKind.Drink, at + RandomOffset(), parent);
        }

        // ---- E로 줍기 ----
        public const float GrabRange = 5f;
        public static System.Collections.Generic.List<Pickup> All => all;
        public PickupKind Kind => kind;
        public bool Takeable => GameManager.Instance != null && GameManager.Instance.Player != null && CanTake(GameManager.Instance.Player);
        static readonly System.Collections.Generic.List<Pickup> all = new System.Collections.Generic.List<Pickup>();
        bool grabbed; // E로 집어서 거리와 상관없이 날아오는 중

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        /// <summary>플레이어 3.5m 안에 보급품이 있나 (E를 줍기로 쓸지 재생으로 쓸지).</summary>
        /// <summary>E 한 번에 집을 수 있는 보급품 수.</summary>
        public static int CountTakeableNear()
        {
            var gm = GameManager.Instance;
            var p = gm != null ? gm.Player : null;
            if (p == null || p.IsDead) return 0;
            int n = 0;
            float bd = GrabRange * GrabRange;
            foreach (var k in all)
            {
                if (k == null || k.grabbed || !k.CanTake(p)) continue;
                Vector3 d = k.transform.position - p.transform.position; d.y = 0f;
                if (d.sqrMagnitude < bd) n++;
            }
            return n;
        }

        public static bool AnyNearPlayer()
        {
            // 주울 수 있는 것만 (가득 차서 못 줍는 부적 때문에 E 재생이 막히지 않게)
            var gm = GameManager.Instance;
            var p = gm != null ? gm.Player : null;
            if (p == null || p.IsDead) return false;
            float bd = GrabRange * GrabRange;
            foreach (var k in all)
            {
                if (k == null || k.grabbed || !k.CanTake(p)) continue;
                Vector3 d = k.transform.position - p.transform.position; d.y = 0f;
                if (d.sqrMagnitude < bd) return true;
            }
            return false;
        }

        public static Pickup NearestToPlayer()
        {
            var gm = GameManager.Instance;
            var p = gm != null ? gm.Player : null;
            if (p == null || p.IsDead) return null;
            Pickup best = null;
            float bd = GrabRange * GrabRange;
            foreach (var k in all)
            {
                if (k == null || k.grabbed) continue;
                Vector3 d = k.transform.position - p.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = k; }
            }
            return best;
        }

        /// <summary>HUD 안내용: 가장 가까운 보급품 이름과 지금 주울 수 있는지.</summary>
        public static bool NearestInfo(out string name, out bool canTake)
        {
            var k = NearestToPlayer();
            name = k != null ? NameOf(k.kind) : null;
            canTake = k != null && k.CanTake(GameManager.Instance.Player);
            return k != null;
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
                case PickupKind.Pill: BuildPill(); break;
                case PickupKind.Gear: BuildBundle(); break;
                default: BuildMagazine(); break;
            }

            // 바닥에 은은한 빛 고리 (조명이 없는 '하' 품질에서도 어디 떨어졌는지 보이게)
            var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(ring.GetComponent<Collider>());
            ring.layer = 2;
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.up * 0.03f;
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = Vector3.one * 1.1f;
            var rr = ring.GetComponent<Renderer>();
            rr.sharedMaterial = GlowMaterial(c);
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rr.receiveShadows = false;

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

        /// <summary>탄약: 국방색 철제 탄약통 (손잡이·잠금쇠·노란 글씨 띠) + 뚜껑 위 놋쇠 탄알. 멀리서도 '탄약'으로 읽힌다.</summary>
        void BuildMagazine()
        {
            var olive = EnvMaterials.Plain(new Color(0.24f, 0.27f, 0.18f), 0.45f, 0.35f);
            var dark = EnvMaterials.Plain(new Color(0.12f, 0.13f, 0.12f), 0.5f, 0.6f);
            var stripe = FX.UnlitMaterial(new Color(0.85f, 0.7f, 0.25f));
            var brass = EnvMaterials.Plain(new Color(0.85f, 0.62f, 0.28f), 0.8f, 0.9f);
            Part(PrimitiveType.Cube, Vector3.zero, new Vector3(0.3f, 0.17f, 0.13f), olive);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.093f, 0f), new Vector3(0.315f, 0.02f, 0.14f), dark);          // 뚜껑 테
            Part(PrimitiveType.Cube, new Vector3(0f, 0.125f, 0f), new Vector3(0.12f, 0.015f, 0.025f), dark);         // 손잡이
            Part(PrimitiveType.Cube, new Vector3(-0.055f, 0.11f, 0f), new Vector3(0.012f, 0.03f, 0.02f), dark);
            Part(PrimitiveType.Cube, new Vector3(0.055f, 0.11f, 0f), new Vector3(0.012f, 0.03f, 0.02f), dark);
            Part(PrimitiveType.Cube, new Vector3(0.155f, 0.03f, 0f), new Vector3(0.015f, 0.06f, 0.05f), dark);       // 잠금쇠
            Part(PrimitiveType.Cube, new Vector3(0f, -0.01f, 0.066f), new Vector3(0.2f, 0.035f, 0.002f), stripe);    // 글씨 띠 (앞)
            Part(PrimitiveType.Cube, new Vector3(0f, -0.01f, -0.066f), new Vector3(0.2f, 0.035f, 0.002f), stripe);   // (뒤)
            for (int b = 0; b < 3; b++)
            {
                float bx = -0.07f + b * 0.035f;
                Part(PrimitiveType.Cylinder, new Vector3(bx, 0.13f, 0.035f), new Vector3(0.018f, 0.03f, 0.018f), brass, new Vector3(0f, 0f, 90f - b * 6f));
            }
        }

        /// <summary>장비 꾸러미: 쪽빛 보자기로 싸맨 보따리 + 위로 묶은 매듭.</summary>
        void BuildBundle()
        {
            var cloth = EnvMaterials.Plain(new Color(0.18f, 0.27f, 0.5f), 0.15f, 0f);
            var knot = EnvMaterials.Plain(new Color(0.22f, 0.33f, 0.6f), 0.15f, 0f);
            var trim = FX.UnlitMaterial(new Color(0.85f, 0.8f, 0.68f));
            Part(PrimitiveType.Sphere, Vector3.zero, new Vector3(0.36f, 0.24f, 0.32f), cloth);
            Part(PrimitiveType.Sphere, new Vector3(0f, 0.13f, 0f), new Vector3(0.11f, 0.08f, 0.09f), knot);
            Part(PrimitiveType.Cube, new Vector3(-0.06f, 0.17f, 0f), new Vector3(0.09f, 0.02f, 0.05f), knot, new Vector3(0f, 0f, 35f));
            Part(PrimitiveType.Cube, new Vector3(0.06f, 0.17f, 0f), new Vector3(0.09f, 0.02f, 0.05f), knot, new Vector3(0f, 0f, -35f));
            Part(PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.37f, 0.012f, 0.012f), trim, new Vector3(0f, 25f, 0f));
        }

        /// <summary>청심환: 금박 싼 동그란 환약 + 비단 끈.</summary>
        void BuildPill()
        {
            Part(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.14f, FX.UnlitMaterial(new Color(0.95f, 0.78f, 0.35f)));
            Part(PrimitiveType.Cube, Vector3.zero, new Vector3(0.16f, 0.025f, 0.03f), FX.UnlitMaterial(new Color(0.7f, 0.25f, 0.6f)), new Vector3(0f, 0f, 35f));
        }

        /// <summary>치유 부적: 누런 한지에 붉은 글씨를 쓴 부적 한 장이 은은한 빛무리 앞에서 하늘거린다.</summary>
        void BuildRune(Color c)
        {
            if (talismanMat == null)
            {
                const int TW = 32, TH = 64;
                var tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Talisman" };
                var px = new Color32[TW * TH];
                var rnd = new System.Random(77);
                for (int y = 0; y < TH; y++)
                    for (int x = 0; x < TW; x++)
                    {
                        float n = (float)rnd.NextDouble() * 0.06f;
                        Color col = new Color(0.93f - n, 0.8f - n, 0.42f - n);
                        bool border = x == 2 || x == TW - 3 || y == 2 || y == TH - 3;
                        // 세로 붓글씨 획 (가운데 굵은 줄 + 가로 획 몇 개 + 아래 동그라미)
                        bool stroke = (Mathf.Abs(x - TW / 2) <= 1 && y > 10 && y < 54)
                                   || ((y == 46 || y == 47 || y == 36 || y == 26 || y == 27) && x > 8 && x < TW - 8)
                                   || (Mathf.Abs(Mathf.Sqrt((x - 16f) * (x - 16f) + (y - 15f) * (y - 15f)) - 6f) < 1.1f);
                        if (border || stroke) col = new Color(0.72f, 0.12f, 0.1f);
                        px[y * TW + x] = col;
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                tex.filterMode = FilterMode.Point;
                talismanMat = FX.UnlitMaterial(Color.white);
                talismanMat = new Material(talismanMat) { mainTexture = tex, name = "Talisman" };
            }
            // 앞뒤로 한 장씩 (양면)
            Part(PrimitiveType.Quad, Vector3.zero, new Vector3(0.16f, 0.32f, 1f), talismanMat);
            Part(PrimitiveType.Quad, Vector3.zero, new Vector3(0.16f, 0.32f, 1f), talismanMat, new Vector3(0f, 180f, 0f));
            var halo = Part(PrimitiveType.Quad, new Vector3(0f, 0f, 0.01f), Vector3.one * 0.55f, GlowMaterial(c));
            halo.gameObject.AddComponent<FaceCamera>();
        }

        static Material talismanMat;

        /// <summary>빛무리 판이 항상 카메라를 보게.</summary>
        class FaceCamera : MonoBehaviour
        {
            void LateUpdate()
            {
                var cam = Camera.main;
                if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            }
        }

        Transform runeRingA, runeRingB;
        static Texture2D glowTex;
        static readonly System.Collections.Generic.Dictionary<Color, Material> glowMats = new System.Collections.Generic.Dictionary<Color, Material>();

        /// <summary>바닥 빛 고리 재질 (색마다 하나, 텍스처는 처음 쓸 때 만든다).</summary>
        static Material GlowMaterial(Color c)
        {
            if (glowMats.TryGetValue(c, out var m) && m != null) return m;
            if (glowTex == null)
            {
                const int S = 64;
                glowTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "PickupGlow" };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = (x - S / 2f + 0.5f) / (S / 2f), dy = (y - S / 2f + 0.5f) / (S / 2f);
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float ringA = Mathf.Clamp01(1f - Mathf.Abs(r - 0.7f) * 9f) * 0.9f;   // 고리
                        float fill = Mathf.Clamp01(1f - r) * 0.35f;                          // 안쪽 은은하게
                        px[y * S + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(Mathf.Max(ringA, fill))));
                    }
                glowTex.SetPixels32(px);
                glowTex.Apply(false, true);
            }
            m = new Material(Shader.Find("Sprites/Default")) { name = "PickupGlow", mainTexture = glowTex, color = new Color(c.r, c.g, c.b, 0.55f) };
            glowMats[c] = m;
            return m;
        }
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
                // 빨려 들어올 때 몸 가까이에서 작아지며 쏙 들어간다
                var pl = gm.Player;
                float suck = 1f;
                if (pl != null)
                {
                    Vector3 dd = pl.transform.position - transform.position; dd.y = 0f;
                    float dist = dd.magnitude;
                    if (dist < 1.6f && (grabbed || CanTake(pl))) suck = Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(0.9f, 1.6f, dist));
                }
                visual.localScale = Vector3.one * grow * suck;
                visual.localPosition = Vector3.up * ((kind == PickupKind.Rune ? 0.55f : 0.3f) + hop + Mathf.Sin(Time.time * 2.5f + bobSeed) * (kind == PickupKind.Rune ? 0.1f : 0.06f));
                if (kind == PickupKind.Rune) visual.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.7f + bobSeed) * 8f, Time.time * 40f + bobSeed * 30f, Mathf.Sin(Time.time * 2.3f + bobSeed) * 10f);
                else visual.Rotate(0f, 90f * dt, 0f, Space.World);
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
            // E: 3.5m 안의 보급품을 한꺼번에 집는다 (주울 수 없으면 왜 안 되는지 알려 준다)
            if (!grabbed && d < GrabRange && GameInput.PickupDown && gm.AcceptsGameplayInput)
            {
                if (can) grabbed = true;
                else
                {
                    string why = kind == PickupKind.Rune ? "생명이 가득하다 — 다쳤을 때 치유 부적을 주우세요"
                               : kind == PickupKind.Ammo ? "탄약도 수류탄도 가득하다"
                               : $"{NameOf(kind)} 가득 참 — 쓰고 나서 주우세요";
                    GameHud.OnPickup(kind == PickupKind.Kit ? HudIcons.Icon.Kit : kind == PickupKind.Drink ? HudIcons.Icon.Bottle : kind == PickupKind.Rune ? HudIcons.Icon.Heart : kind == PickupKind.Pill ? HudIcons.Icon.Pill : HudIcons.Icon.Ammo,
                                     new Color(0.6f, 0.58f, 0.54f), why, false);
                    toldFull = true;
                }
            }
            if (grabbed && can)
            {
                // 손으로 확 끌어당기듯 빠르게 날아온다
                transform.position = Vector3.MoveTowards(transform.position, new Vector3(p.transform.position.x, basePos.y, p.transform.position.z), 14f * dt);
                if (d < 0.9f) { Take(p); Destroy(gameObject); }
                return;
            }
            grabbed = false;
            if (!can && d < 1.1f && !toldFull && kind != PickupKind.Rune) // 부적은 많이 떨어져서 '가득' 알림이 시끄럽지 않게 생략
            {
                toldFull = true;
                string full = kind == PickupKind.Rune ? "생명이 가득 차 있다 — 다치면 치유 부적을 주우세요"
                            : kind == PickupKind.Kit ? $"치료 키트 가득 참 (최대 {PlayerCharacter.MaxKits}) — 쓰고 나서 주우세요"
                            : kind == PickupKind.Drink ? $"회복제 가득 참 (최대 {PlayerCharacter.MaxDrinks}) — 쓰고 나서 주우세요"
                            : "탄약도 수류탄도 가득 참";
                GameHud.OnPickup(kind == PickupKind.Kit ? HudIcons.Icon.Kit : kind == PickupKind.Drink ? HudIcons.Icon.Bottle : kind == PickupKind.Rune ? HudIcons.Icon.Heart : kind == PickupKind.Pill ? HudIcons.Icon.Pill : HudIcons.Icon.Ammo,
                                 new Color(0.6f, 0.58f, 0.54f), full, false);
            }
            else if (d > 3f) toldFull = false;
            float reach = dawn ? 30f : 2.5f; // 밟으면 줍고, 멀리 있는 건 E (5m)
            if (d < reach && can)
            {
                // 자석처럼 끌려온다
                float speed = dawn ? 9f + (reach - d) * 0.6f : 5f + (reach - d) * 4f;
                transform.position = Vector3.MoveTowards(transform.position, new Vector3(p.transform.position.x, basePos.y, p.transform.position.z), speed * dt);
                if (d < 0.9f) { Take(p); Destroy(gameObject); }
            }
        }

        /// <summary>장비 꾸러미: 방어구 한 부위 강화 +1 또는 든 총 강화 +1 (꽉 찼으면 등급 올림, 그것도 끝이면 엽전).</summary>
        static void TakeGear(PlayerCharacter p)
        {
            Color c = ColorOf(PickupKind.Gear);
            var w = p.Weapons.Current;
            bool gun = w != null && Random.value < 0.45f;
            if (gun && w.upgradeLevel < Hunt.Weapons.WeaponInstance.MaxUpgrade)
            {
                w.upgradeLevel++;
                GameHud.OnPickup(HudIcons.Icon.Ammo, c, $"장비 꾸러미 — {w.data.displayName} 강화 +{w.upgradeLevel}");
                return;
            }
            // 방어구: 가장 약한 부위부터
            Hunt.Player.ArmorPiece best = null;
            foreach (var pc in p.Armor.pieces)
                if (pc.upgrade < Grades.MaxUpgrade && (best == null || pc.Power < best.Power)) best = pc;
            if (best != null)
            {
                best.upgrade++;
                p.Heal(0f);
                var icon = best.slot == ArmorSlot.Head ? HudIcons.Icon.Helmet : best.slot == ArmorSlot.Body ? HudIcons.Icon.Vest : best.slot == ArmorSlot.Hands ? HudIcons.Icon.Glove : best.slot == ArmorSlot.Legs ? HudIcons.Icon.Greave : HudIcons.Icon.Boot;
                GameHud.OnPickup(icon, Grades.Color(best.grade), $"장비 꾸러미 — {best.Name}");
                return;
            }
            p.AddGold(120);
            GameHud.OnPickup(HudIcons.Icon.Vest, c, "장비 꾸러미 — 모두 다 강화돼 엽전 120");
        }

        bool CanTake(PlayerCharacter p)
        {
            switch (kind)
            {
                case PickupKind.Kit: return p.Kits < PlayerCharacter.MaxKits;
                case PickupKind.Drink: return p.Drinks < PlayerCharacter.MaxDrinks;
                case PickupKind.Rune: return p.Hp < p.MaxHp - 0.5f;
                case PickupKind.Pill: return p.Infection.Antidotes < 3;
                case PickupKind.Gear: return true;
                default:
                    foreach (var w in p.Weapons.Slots)
                        if (w != null && w.reserve < w.MaxReserve) return true;
                    // 탄약이 다 가득하면 수류탄 한 개로 바꿔 준다 (탄창이 쓸모없이 남지 않게)
                    return p.Weapons.GadgetCount[(int)GadgetType.Grenade] < Gadgets.MaxCarry(GadgetType.Grenade);
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
                    p.Heal(p.MaxHp * RuneHeal * (p.Stats.Has(Hunt.Stats.Trait.Herbalist) ? 1.5f : 1f));
                    GameHud.OnPickup(HudIcons.Icon.Heart, c, $"치유 부적  생명 +{p.Hp - before:0}");
                    SFX.PlayPitched(SFX.LevelUp, 0.35f, 1.35f); // 맑은 방울 소리
                    GameHud.HealFlash = 1f;
                    return;
                }
                case PickupKind.Pill:
                    p.Infection.Antidotes++;
                    GameHud.OnPickup(HudIcons.Icon.Pill, c, $"청심환 +1  (지닌 {p.Infection.Antidotes}, H로 먹는다)");
                    break;
                case PickupKind.Gear:
                    TakeGear(p);
                    SFX.PlayPitched(SFX.Bandage, 0.4f, 1.3f);
                    SFX.PlayPitched(SFX.LevelUp, 0.5f, 1.1f);
                    return;
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
                    if (w == null || w.reserve >= w.MaxReserve)
                    {
                        int gi = (int)GadgetType.Grenade;
                        p.Weapons.GadgetCount[gi] = Mathf.Min(Gadgets.MaxCarry(GadgetType.Grenade), p.Weapons.GadgetCount[gi] + 1);
                        GameHud.OnPickup(HudIcons.Icon.Grenade, c, $"탄약이 가득해 수류탄으로  +1  (지닌 {p.Weapons.GadgetCount[gi]})");
                        break;
                    }
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

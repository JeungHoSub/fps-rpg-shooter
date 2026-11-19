using Hunt.Core;
using Hunt.Monsters;
using Hunt.UI;
using UnityEngine;

namespace Hunt.Player
{
    public enum PickupKind { Kit, Drink, Ammo }

    /// <summary>
    /// 망자가 떨어뜨리는 보급품: 치료 키트 · 스태미너 회복제 · 탄창.
    /// 바닥에서 빛나며 천천히 돌고, 가까이 가면 빨려 들어와 자동으로 줍는다. 35초 뒤 사라진다.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        PickupKind kind;
        float life = 35f, bobSeed;
        Transform visual;
        Vector3 basePos;

        public static Color ColorOf(PickupKind k) =>
            k == PickupKind.Kit ? new Color(1f, 0.3f, 0.25f) : k == PickupKind.Drink ? new Color(0.35f, 0.75f, 1f) : new Color(1f, 0.78f, 0.3f);

        public static string NameOf(PickupKind k) => k == PickupKind.Kit ? "치료 키트" : k == PickupKind.Drink ? "스태미너 회복제" : "탄창";

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
                return;
            }
            float tough = Mathf.Clamp(m.MaxHp / 90f, 0.5f, 6f); // 단단한 망자일수록 잘 떨어뜨린다
            float mult = Mathf.Lerp(1f, 2.2f, (tough - 0.5f) / 5.5f);
            float r = Random.value;
            float pAmmo = 0.11f * mult, pKit = 0.035f * mult, pDrink = 0.045f * mult;
            if (r < pKit) Spawn(PickupKind.Kit, at, parent);
            else if (r < pKit + pDrink) Spawn(PickupKind.Drink, at, parent);
            else if (r < pKit + pDrink + pAmmo) Spawn(PickupKind.Ammo, at, parent);
        }

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
            // 상자(키트) · 병(회복제) · 탄창(납작한 직육면체)
            Vector3 size = kind == PickupKind.Kit ? new Vector3(0.34f, 0.2f, 0.26f)
                         : kind == PickupKind.Drink ? new Vector3(0.12f, 0.3f, 0.12f)
                         : new Vector3(0.1f, 0.26f, 0.2f);
            var body = GameObject.CreatePrimitive(kind == PickupKind.Drink ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(visual, false);
            body.transform.localScale = kind == PickupKind.Drink ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
            var mr = body.GetComponent<Renderer>();
            mr.sharedMaterial = FX.UnlitMaterial(c * 0.85f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (kind == PickupKind.Kit)
            {
                // 흰 십자
                foreach (var s in new[] { new Vector3(0.2f, 0.03f, 0.06f), new Vector3(0.06f, 0.03f, 0.2f) })
                {
                    var x = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(x.GetComponent<Collider>());
                    x.transform.SetParent(visual, false);
                    x.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    x.transform.localScale = s;
                    x.GetComponent<Renderer>().sharedMaterial = FX.UnlitMaterial(new Color(0.95f, 0.92f, 0.85f));
                }
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

            if (QualityManager.Level != GfxLevel.Low)
            {
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

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsWorldRunning) return;
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0f) { Destroy(gameObject); return; }
            if (visual != null)
            {
                visual.localPosition = Vector3.up * (0.3f + Mathf.Sin(Time.time * 2.5f + bobSeed) * 0.06f);
                visual.Rotate(0f, 90f * dt, 0f, Space.World);
                // 사라지기 직전 깜빡임
                visual.gameObject.SetActive(life > 5f || Mathf.Repeat(life, 0.4f) > 0.15f);
            }

            var p = gm.Player;
            if (p == null || p.IsDead) return;
            Vector3 to = p.transform.position - transform.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d < 4f && CanTake(p))
            {
                // 자석처럼 끌려온다
                transform.position = Vector3.MoveTowards(transform.position, new Vector3(p.transform.position.x, basePos.y, p.transform.position.z), (3f + (4f - d) * 3f) * dt);
                if (d < 0.9f) { Take(p); Destroy(gameObject); }
            }
        }

        bool CanTake(PlayerCharacter p)
        {
            switch (kind)
            {
                case PickupKind.Kit: return p.Kits < PlayerCharacter.MaxKits;
                case PickupKind.Drink: return p.Drinks < PlayerCharacter.MaxDrinks;
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
                    HUD.Notify("치료 키트를 주웠다 (X로 사용)", c);
                    break;
                case PickupKind.Drink:
                    p.Drinks++;
                    HUD.Notify("스태미너 회복제를 주웠다 (C로 사용)", c);
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
                    HUD.Notify($"탄창을 주웠다 — {w.data.displayName} +{w.reserve - before}발", c);
                    break;
                }
            }
            SFX.Play(SFX.MagIn, 0.8f, 0.05f);
        }
    }
}

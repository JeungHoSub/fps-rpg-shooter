using System.Collections.Generic;
using Hunt.Core;
using Hunt.UI;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>
    /// 보급 상자: 밤마다 마을 곳곳에 3개. 다가가 E를 누르면 뚜껑이 튀어 오르며 탄창·치유 부적(가끔 수류탄)이 쏟아진다.
    /// 노란 빛기둥으로 멀리서도 보인다. 다음 밤이 오면 남은 상자는 치운다.
    /// </summary>
    public class SupplyCrate : MonoBehaviour
    {
        public const float OpenRange = 2.4f;
        public static readonly List<SupplyCrate> Active = new List<SupplyCrate>();
        /// <summary>이번 밤 놓은 수 · 연 수 (새벽 기록 카드용).</summary>
        public static int PlacedThisNight, OpenedThisNight;

        bool opened;
        public bool Opened => opened;
        Transform lid, beam;
        float openT;

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        /// <summary>지난 밤 상자를 치우고 플레이어에서 14~34m 떨어진 곳에 n개를 놓는다.</summary>
        public static void PlaceForNight(Transform parent, Vector3 player, int n)
        {
            foreach (var c in Active.ToArray()) if (c != null) Destroy(c.gameObject);
            PlacedThisNight = n;
            OpenedThisNight = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Vector3.zero;
                for (int k = 0; k < 20; k++)
                {
                    p = new Vector3(Random.Range(-36f, 36f), 0f, Random.Range(-36f, 36f));
                    float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(player.x, 0f, player.z));
                    bool farFromOthers = true;
                    foreach (var c in Active) if (c != null && Vector3.Distance(c.transform.position, p) < 12f) farFromOthers = false;
                    if (d > 14f && d < 34f && farFromOthers) break;
                }
                p = Navigation.Ready ? Navigation.Snap(p, 6f) : p;
                Create(parent, p);
            }
        }

        static void Create(Transform parent, Vector3 at)
        {
            var go = new GameObject("보급 상자");
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var crate = go.AddComponent<SupplyCrate>();
            crate.Build();
        }

        void Build()
        {
            // 몸통: 실사 군용 상자 모델 (없으면 나무 상자 도형)
            var model = PropModels.Place("old_military_crate", transform, Vector3.zero, 0f, 0.5f, true);
            if (model == null)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.transform.SetParent(transform, false);
                box.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                box.transform.localScale = new Vector3(0.8f, 0.5f, 0.5f);
                box.GetComponent<Renderer>().sharedMaterial = EnvMaterials.Plain(new Color(0.3f, 0.33f, 0.22f), 0.3f);
            }
            // 뚜껑 (열 때 튀어 오른다)
            var l = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(l.GetComponent<Collider>());
            l.layer = 2;
            l.transform.SetParent(transform, false);
            l.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            l.transform.localScale = new Vector3(0.82f, 0.05f, 0.52f);
            l.GetComponent<Renderer>().sharedMaterial = EnvMaterials.Plain(new Color(0.26f, 0.29f, 0.19f), 0.3f);
            lid = l.transform;
            // 노란 빛줄기: 바닥에서 위로 갈수록 옅어지는 부드러운 빛 (늘 나를 향해 돌아선 판 하나 — 딱딱한 원기둥 대신)
            var b = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(b.GetComponent<Collider>());
            b.layer = 2;
            b.transform.SetParent(transform, false);
            b.transform.localPosition = new Vector3(0f, 3f, 0f);
            b.transform.localScale = new Vector3(0.7f, 6f, 1f);
            var br = b.GetComponent<Renderer>();
            br.sharedMaterial = BeamMaterial();
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            br.receiveShadows = false;
            beam = b.transform;
        }

        static Material beamMat;

        /// <summary>빛줄기 재질: 가운데 밝고 양옆으로 번지며, 위로 갈수록 사라진다.</summary>
        static Material BeamMaterial()
        {
            if (beamMat != null) return beamMat;
            const int W = 32, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / W * 2f - 1f);          // 가운데 0 → 가장자리 1
                    float v = (y + 0.5f) / H;                                // 아래 0 → 위 1
                    float across = Mathf.Pow(1f - u, 2.2f);
                    float up = Mathf.Pow(1f - v, 1.6f) * Mathf.Clamp01(v * 12f); // 바닥에서 살짝 올라가 시작, 위로 옅어짐
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(255 * across * up));
                }
            tex.SetPixels32(px);
            tex.Apply();
            beamMat = new Material(Shader.Find("Sprites/Default")) { name = "CrateBeam", mainTexture = tex, color = new Color(1f, 0.82f, 0.4f, 0.55f) };
            return beamMat;
        }

        void LateUpdate()
        {
            // 빛줄기 판이 늘 나를 향하게 (세로축으로만 돈다)
            if (beam == null) return;
            var gm = GameManager.Instance;
            var cam = gm != null ? gm.ActiveCamera : null;
            if (cam == null) return;
            Vector3 to = beam.position - cam.transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.001f) beam.rotation = Quaternion.LookRotation(to);
        }

        /// <summary>플레이어 가까이(2.4m)에 열 수 있는 상자가 있으면 그것.</summary>
        public static SupplyCrate NearestToPlayer()
        {
            var gm = GameManager.Instance;
            var p = gm != null ? gm.Player : null;
            if (p == null || p.IsDead) return null;
            SupplyCrate best = null;
            float bd = OpenRange * OpenRange;
            foreach (var c in Active)
            {
                if (c == null || c.opened) continue;
                Vector3 d = c.transform.position - p.transform.position; d.y = 0f;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = c; }
            }
            return best;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsWorldRunning) return;
            float dt = Time.deltaTime;
            if (!opened)
            {
                if (beam != null) beam.localScale = new Vector3(0.7f + 0.12f * Mathf.Sin(Time.time * 2.4f), 6f, 1f); // 은은하게 숨 쉰다
                if (gm.AcceptsGameplayInput && GameInput.PickupDown && ReferenceEquals(NearestToPlayer(), this)) Open(gm.Player);
                return;
            }
            // 뚜껑: 튀어 올라 빙글 돌다 옆에 떨어진다
            openT += dt;
            if (lid != null && openT < 1.2f)
            {
                float k = openT / 1.2f;
                lid.localPosition = new Vector3(0.6f * k, 0.52f + Mathf.Sin(k * Mathf.PI) * 0.9f - 0.45f * k, 0f);
                lid.localRotation = Quaternion.Euler(0f, 0f, -110f * k);
            }
            if (beam != null) { beam.localScale = Vector3.Lerp(beam.localScale, new Vector3(0f, 6f, 1f), dt * 4f); if (openT > 1.5f) { Destroy(beam.gameObject); beam = null; } }
        }

        void Open(PlayerCharacter p)
        {
            opened = true;
            openT = 0f;
            OpenedThisNight++;
            Achievements.Add("crates20");
            p.RunCrates++;
            SFX.PlayAt(SFX.Rack, transform.position, 0.8f);
            if (p.Controller != null) p.Controller.AddPunch(-2.5f, 0f); // 몸을 숙여 뚜껑을 젖힌다
            SFX.PlayPitched(SFX.LevelUp, 0.3f, 1.1f);
            Vector3 c = transform.position;
            // 앞쪽(플레이어 쪽)으로 쏟아진다
            Vector3 toP = p.transform.position - c; toP.y = 0f; toP = toP.sqrMagnitude > 0.01f ? toP.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, toP);
            Pickup.Spawn(PickupKind.Ammo, c + toP * 0.9f + side * 0.5f, transform.parent);
            Pickup.Spawn(PickupKind.Ammo, c + toP * 0.9f - side * 0.5f, transform.parent);
            Pickup.Spawn(PickupKind.Rune, c + toP * 1.3f, transform.parent);
            // 가끔 수류탄 한 개 더
            if (Random.value < 0.45f)
            {
                int gi = (int)GadgetType.Grenade;
                if (p.Weapons.GadgetCount[gi] < Gadgets.MaxCarry(GadgetType.Grenade))
                {
                    p.Weapons.GadgetCount[gi]++;
                    GameHud.OnPickup(HudIcons.Icon.Grenade, new Color(1f, 0.8f, 0.4f), $"보급 상자에서 수류탄 +1  (지닌 {p.Weapons.GadgetCount[gi]})");
                }
            }
            // 가끔 폭죽 (B로 던져 망자를 꾄다)
            if (Random.value < 0.35f && p.Firecrackers < PlayerCharacter.MaxFirecrackers)
            {
                p.Firecrackers++;
                GameHud.OnPickup(HudIcons.Icon.Flame, new Color(1f, 0.4f, 0.3f), $"보급 상자에서 폭죽 +1  (B로 던진다, 지닌 {p.Firecrackers})");
            }
            HUD.Notify("보급 상자를 열었다", new Color(1f, 0.85f, 0.5f));
            // 함정: 둘째 밤부터 18% — 상자 주변 땅에서 망자 셋이 기어 나온다 (보급품은 그대로)
            var wm = Hunt.Waves.WaveManager.Instance;
            if (wm != null && wm.Wave >= 2 && wm.Spawning && Random.value < 0.18f)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 off = Quaternion.Euler(0f, i * 120f + Random.Range(-20f, 20f), 0f) * Vector3.forward * Random.Range(3.5f, 5f);
                    wm.Spawn(Hunt.Data.MonsterArchetype.Runner, c + off);
                }
                HUD.Notify("함정이다! 상자 주변 땅에서 망자들이 기어 나온다", new Color(1f, 0.35f, 0.3f));
                SFX.PlayPitched(SFX.Drum, 0.7f, 0.8f);
            }
            // 가끔 찢어진 쪽지 (아직 안 읽은 다음 장)
            if (Random.value < 0.4f) GameHud.ShowNextLoreNote();
            Noise.Emit(c, 6f, true);
        }
    }
}

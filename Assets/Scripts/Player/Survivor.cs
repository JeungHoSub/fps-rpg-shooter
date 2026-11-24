using System.Collections.Generic;
using Hunt.Core;
using Hunt.Monsters;
using Hunt.UI;
using UnityEngine;

namespace Hunt.Player
{
    /// <summary>
    /// 다친 마을 사람: 둘째 밤부터 40% 확률로 마을 어딘가에 웅크리고 있다.
    /// 다가가 E로 치료 키트를 하나 내주면 엽전·폭죽·쪽지를 받는다. 망자들이 모두 몰려오기 전에 못 찾으면 잃는다.
    /// </summary>
    public class Survivor : MonoBehaviour
    {
        public const float Range = 2.6f;
        public static readonly List<Survivor> Active = new List<Survivor>();
        bool saved, lost, called, warned;
        float rushT;
        float shiver, leaveT;
        Transform body;

        public bool Saved => saved;

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        /// <summary>밤마다 지난 사람은 치우고, 둘째 밤부터 40% 확률로 한 명 (플레이어 16~34m).</summary>
        public static void PlaceForNight(Transform parent, Vector3 player, int night)
        {
            foreach (var s in Active.ToArray()) if (s != null) Destroy(s.gameObject);
            if (night < 2 || Random.value > 0.4f) return;
            Vector3 p = Vector3.zero;
            for (int k = 0; k < 20; k++)
            {
                p = new Vector3(Random.Range(-34f, 34f), 0f, Random.Range(-34f, 34f));
                float d = Vector3.Distance(p, new Vector3(player.x, 0f, player.z));
                if (d > 16f && d < 34f) break;
            }
            p = Navigation.Ready ? Navigation.Snap(p, 6f) : p;
            var go = new GameObject("다친 마을 사람");
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.AddComponent<Survivor>().Build();
            HUD.Notify("어디선가 사람 목소리가 들린다 — 다친 마을 사람이 있다 (치료 키트를 가져가 E)", new Color(0.9f, 0.85f, 0.7f));
        }

        void Build()
        {
            var cloth = EnvMaterials.Plain(new Color(0.62f, 0.6f, 0.55f), 0.2f);   // 바랜 작업복
            var pants = EnvMaterials.Plain(new Color(0.18f, 0.2f, 0.24f), 0.2f);
            var skin = EnvMaterials.Plain(new Color(0.62f, 0.48f, 0.38f), 0.3f);
            var hair = EnvMaterials.Plain(new Color(0.08f, 0.07f, 0.07f), 0.3f);
            var blood = EnvMaterials.Plain(new Color(0.35f, 0.03f, 0.03f), 0.5f);
            body = new GameObject("몸").transform;
            body.SetParent(transform, false);
            // 벽에 기대 웅크려 앉은 모습: 몸통·머리·무릎·팔
            Part(PrimitiveType.Capsule, new Vector3(0f, 0.55f, -0.1f), new Vector3(0.42f, 0.38f, 0.3f), Quaternion.Euler(-15f, 0f, 0f), cloth, true);
            Part(PrimitiveType.Sphere, new Vector3(0f, 1.02f, -0.02f), new Vector3(0.24f, 0.27f, 0.25f), Quaternion.identity, skin, false);
            Part(PrimitiveType.Sphere, new Vector3(0f, 1.1f, -0.05f), new Vector3(0.25f, 0.18f, 0.26f), Quaternion.identity, hair, false);
            foreach (float sx in new[] { -0.12f, 0.12f })
            {
                Part(PrimitiveType.Capsule, new Vector3(sx, 0.32f, 0.2f), new Vector3(0.15f, 0.25f, 0.15f), Quaternion.Euler(70f, 0f, 0f), pants, false); // 허벅지
                Part(PrimitiveType.Capsule, new Vector3(sx, 0.2f, 0.42f), new Vector3(0.13f, 0.2f, 0.13f), Quaternion.Euler(-20f, 0f, 0f), pants, false); // 정강이
                Part(PrimitiveType.Capsule, new Vector3(sx * 1.6f, 0.6f, 0.12f), new Vector3(0.11f, 0.22f, 0.11f), Quaternion.Euler(60f, 0f, sx > 0 ? -20f : 20f), cloth, false); // 무릎 감싼 팔
            }
            Part(PrimitiveType.Cube, new Vector3(0.1f, 0.5f, 0.05f), new Vector3(0.16f, 0.12f, 0.02f), Quaternion.identity, blood, false); // 옆구리 상처
        }

        void Part(PrimitiveType t, Vector3 pos, Vector3 scale, Quaternion rot, Material m, bool collider)
        {
            var g = GameObject.CreatePrimitive(t);
            if (!collider) Destroy(g.GetComponent<Collider>());
            else g.layer = 2; // 총알이 맞지 않게
            g.transform.SetParent(body, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        /// <summary>플레이어 가까이(2.6m)에 아직 못 구한 사람.</summary>
        public static Survivor NearestToPlayer()
        {
            var gm = GameManager.Instance;
            var p = gm != null ? gm.Player : null;
            if (p == null || p.IsDead) return null;
            foreach (var s in Active)
            {
                if (s == null || s.saved || s.lost) continue;
                Vector3 d = s.transform.position - p.transform.position; d.y = 0f;
                if (d.sqrMagnitude < Range * Range) return s;
            }
            return null;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (saved || lost)
            {
                // 구했으면 일어나 서낭당 쪽으로 사라지고, 잃었으면 쓰러진 채 남는다
                if (saved)
                {
                    leaveT += Time.deltaTime;
                    body.localScale = Vector3.one * Mathf.Clamp01(1f - (leaveT - 1.5f) / 0.8f);
                    if (leaveT > 2.4f) Destroy(gameObject);
                }
                return;
            }
            if (!gm.IsWorldRunning) return;
            // 덜덜 떤다
            shiver += Time.deltaTime;
            body.localRotation = Quaternion.Euler(Mathf.Sin(shiver * 23f) * 1.2f, 0f, Mathf.Sin(shiver * 17f) * 1.2f);
            var p = gm.Player;
            if (p == null) return;
            float dist = Vector3.Distance(p.transform.position, transform.position);
            if (!called && dist < 12f)
            {
                called = true;
                HUD.Notify("\"거기 누구 있소… 살려 주시오…\"", new Color(0.9f, 0.85f, 0.7f));
            }
            // 망자들이 모두 몰려오고 20초 안에 곁에 가지 못하면 먼저 당한다
            if (Monster.GlobalRush && !warned) { warned = true; HUD.Notify("망자들이 몰려온다 — 다친 마을 사람이 위험하다 (20초)", new Color(1f, 0.6f, 0.45f)); }
            if (Monster.GlobalRush && dist > 10f) rushT += Time.deltaTime;
            if (rushT > 20f)
            {
                lost = true;
                FX.BloodBurst(transform.position + Vector3.up * 0.7f, new Color(0.4f, 0.03f, 0.04f), 1f);
                SFX.PlayAt3D(SFX.ZombieScream, transform.position + Vector3.up, 0.9f, 1.5f); // 비명
                body.localRotation = Quaternion.Euler(0f, 0f, 80f);
                HUD.Notify("멀리서 비명이 들렸다 — 다친 마을 사람을 잃었다", new Color(1f, 0.45f, 0.4f));
                return;
            }
            if (!gm.AcceptsGameplayInput || !GameInput.PickupDown || !ReferenceEquals(NearestToPlayer(), this)) return;
            if (Pickup.AnyNearPlayer()) return; // 보급품 줍기가 먼저
            if (p.Kits <= 0) { HUD.Notify("치료 키트가 있어야 상처를 감아 줄 수 있다 (X 키트, 서낭당에서 산다)", UITheme.BoneDim); return; }
            p.Kits--;
            saved = true;
            int wave = gm.Waves != null ? gm.Waves.Wave : 1;
            int gold = 60 + 12 * wave;
            p.AddGold(gold);
            string extra = "";
            if (p.Firecrackers < PlayerCharacter.MaxFirecrackers) { p.Firecrackers++; extra = " · 폭죽 +1"; }
            SFX.Play(SFX.Bandage, 0.8f);
            SFX.PlayPitched(SFX.LevelUp, 0.6f, 1f);
            HUD.Notify($"\"고맙소… 이거라도 받으시오.\"  엽전 +{gold}{extra}", new Color(1f, 0.85f, 0.5f));
            GameHud.ShowNextLoreNote();
            Achievements.Add("rescue5");
        }
    }
}

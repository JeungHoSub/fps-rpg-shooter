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
        bool saved, lost, called, warned, trap;
        float rushT, sobClock = 2f;
        float shiver, leaveT;
        Transform body;
        Renderer glow;

        public bool Saved => saved;
        /// <summary>구했거나 잃었다 (지도·나침반에서 지운다).</summary>
        public bool Done => saved || lost;
        /// <summary>새벽 기록용: 이번 밤 다친 사람이 있었나 / 구했나 / 잃었나.</summary>
        public static bool PresentThisNight, SavedThisNight, LostThisNight;

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        /// <summary>밤마다 지난 사람은 치우고, 둘째 밤부터 40% 확률로 한 명 (플레이어 16~34m).</summary>
        public static void PlaceForNight(Transform parent, Vector3 player, int night)
        {
            foreach (var s in Active.ToArray()) if (s != null) Destroy(s.gameObject);
            PresentThisNight = SavedThisNight = LostThisNight = false;
            if (night < 2 || Random.value > 0.4f) return;
            PresentThisNight = true;
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
            var sv = go.AddComponent<Survivor>();
            sv.trap = night >= 4 && Random.value < 0.15f; // 넷째 밤부터 가끔: 사람이 아니다
            sv.Build();
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
            // 곁에 놓인 꺼져 가는 휴대폰 불빛 (어둠 속에서 찾는 실마리, 조명은 안 씀)
            var phone = FX.LowSphere();
            phone.layer = 2;
            phone.transform.SetParent(transform, false);
            phone.transform.localPosition = new Vector3(0.45f, 0.04f, 0.3f);
            phone.transform.localScale = new Vector3(0.09f, 0.02f, 0.16f);
            glow = phone.GetComponent<Renderer>();
            glow.sharedMaterial = FX.UnlitMaterial(new Color(0.75f, 0.85f, 1f));
            glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
                // 구했으면 일어나 절뚝이며 어둠 속으로 멀어지고, 잃었으면 쓰러진 채 남는다
                if (saved)
                {
                    leaveT += Time.deltaTime;
                    if (leaveT > 0.8f)
                    {
                        var pl = gm.Player;
                        Vector3 away = pl != null ? transform.position - pl.transform.position : transform.forward;
                        away.y = 0f;
                        if (away.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(away);
                        transform.position += transform.forward * 2.2f * Time.deltaTime;
                        body.localPosition = new Vector3(0f, 0.35f + Mathf.Abs(Mathf.Sin(leaveT * 7f)) * 0.06f, 0f); // 일어서서 절뚝
                    }
                    body.localScale = Vector3.one * Mathf.Clamp01(1f - (leaveT - 3.2f) / 0.6f);
                    if (leaveT > 3.8f) Destroy(gameObject);
                }
                return;
            }
            if (!gm.IsWorldRunning) return;
            // 덜덜 떤다 · 휴대폰 불빛이 깜빡
            shiver += Time.deltaTime;
            if (glow != null) glow.enabled = Mathf.Repeat(shiver, 2.4f) < 2.0f;
            body.localRotation = Quaternion.Euler(Mathf.Sin(shiver * 23f) * 1.2f, 0f, Mathf.Sin(shiver * 17f) * 1.2f);
            var p = gm.Player;
            if (p == null) return;
            float dist = Vector3.Distance(p.transform.position, transform.position);
            // 가까이(25m) 오면 가끔 흐느끼는 소리가 그쪽에서 들린다 — 찾는 실마리
            sobClock -= Time.deltaTime;
            if (sobClock <= 0f)
            {
                sobClock = Random.Range(5f, 8f);
                if (dist < 25f) SFX.PlayAt3D(SFX.Sob, transform.position + Vector3.up * 0.9f, 0.6f, trap ? Random.Range(0.82f, 0.88f) : Random.Range(0.95f, 1.08f)); // 함정이면 목소리가 어딘가 낮다
            }
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
                LostThisNight = true;
                FX.BloodBurst(transform.position + Vector3.up * 0.7f, new Color(0.4f, 0.03f, 0.04f), 1f);
                SFX.PlayAt3D(SFX.ZombieScream, transform.position + Vector3.up, 0.9f, 1.5f); // 비명
                body.localRotation = Quaternion.Euler(0f, 0f, 80f);
                HUD.Notify("멀리서 비명이 들렸다 — 다친 마을 사람을 잃었다", new Color(1f, 0.45f, 0.4f));
                return;
            }
            if (!gm.AcceptsGameplayInput || !GameInput.PickupDown || !ReferenceEquals(NearestToPlayer(), this)) return;
            if (Pickup.AnyNearPlayer()) return; // 보급품 줍기가 먼저
            if (trap)
            {
                // 사람이 아니었다: 웅크린 몸이 일어서며 덤벼든다 (키트는 그대로)
                lost = true;
                var wm = Hunt.Waves.WaveManager.Instance;
                if (wm != null)
                {
                    var z = wm.Spawn(Hunt.Data.MonsterArchetype.Runner, transform.position);
                    if (z != null) z.Alert(10f);
                }
                SFX.PlayAt3D(SFX.ZombieScream, transform.position + Vector3.up, 1f, 0.9f);
                if (p.Controller != null) p.Controller.AddShake(1.5f);
                HUD.Notify("사람이 아니었다!", new Color(1f, 0.35f, 0.3f));
                PresentThisNight = false; // 새벽 기록에 '다친 사람'으로 남기지 않는다
                Destroy(gameObject);
                return;
            }
            if (p.Kits <= 0) { HUD.Notify("치료 키트가 있어야 상처를 감아 줄 수 있다 (X 키트, 서낭당에서 산다)", UITheme.BoneDim); return; }
            p.Kits--;
            saved = true;
            SavedThisNight = true;
            p.RunRescued++;
            int wave = gm.Waves != null ? gm.Waves.Wave : 1;
            int gold = 60 + 12 * wave;
            p.AddGold(gold);
            string extra = "";
            if (p.Firecrackers < PlayerCharacter.MaxFirecrackers) { p.Firecrackers++; extra = " · 폭죽 +1"; }
            SFX.Play(SFX.Bandage, 0.8f);
            if (p.Controller != null) p.Controller.AddPunch(-3f, 0f); // 몸을 숙여 상처를 감는다
            SFX.PlayPitched(SFX.LevelUp, 0.6f, 1f);
            HUD.Notify($"\"고맙소… 이거라도 받으시오.\"  엽전 +{gold}{extra}", new Color(1f, 0.85f, 0.5f));
            GameHud.ShowNextLoreNote();
            Achievements.Add("rescue5");
        }
    }
}

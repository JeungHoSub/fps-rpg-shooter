using Hunt.Combat;
using Hunt.Core;
using Hunt.Monsters;
using Hunt.Player;
using Hunt.Waves;
using Hunt.Weapons;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// 인게임 HUD: 생명 구슬(진홍) · 역병 구슬(보라) · 역병 능력 슬롯 · 탄약 · 경험치 · 밤 표식 · 보스 체력.
    /// </summary>
    public class GameHud
    {
        float W, H;
        float hpTrail = 1f;
        float bossTrail = 1f;
        float introTime;
        static Texture2D scopeMask;

        // ---- 보급품 주움 알림 (능력 칸 위로 떠오른다) ----
        struct PickupToast { public HudIcons.Icon icon; public Color color; public string text; public float time; }
        static readonly System.Collections.Generic.List<PickupToast> toasts = new System.Collections.Generic.List<PickupToast>();
        /// <summary>키트·회복제 칸이 반짝이는 남은 시간.</summary>
        static float kitFlash, drinkFlash;

        /// <param name="gained">false면 알림만 (가득 참 등) — 칸을 반짝이지 않는다.</param>
        public static void OnPickup(HudIcons.Icon icon, Color color, string text, bool gained = true)
        {
            toasts.Add(new PickupToast { icon = icon, color = color, text = text, time = Time.unscaledTime });
            if (toasts.Count > 4) toasts.RemoveAt(0);
            if (!gained) return;
            if (icon == HudIcons.Icon.Kit) kitFlash = 1f;
            if (icon == HudIcons.Icon.Bottle) drinkFlash = 1f;
        }

        /// <summary>새 판 시작 시 지난 판 알림을 지운다.</summary>
        public static void ClearToasts() { toasts.Clear(); kitFlash = drinkFlash = 0f; }

        void DrawPickups(float dt)
        {
            kitFlash = Mathf.Max(0f, kitFlash - dt * 1.5f);
            drinkFlash = Mathf.Max(0f, drinkFlash - dt * 1.5f);
            bool mob = GameInput.Mobile; // 터치: 왼쪽 위에서 아래로 쌓음 (조이스틱·능력 버튼 자리 피함)
            float baseY = mob ? 190f : H - 230;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - toasts[i].time;
                if (age > 2.6f) { toasts.RemoveAt(i); continue; }
            }
            for (int i = 0; i < toasts.Count; i++)
            {
                var t = toasts[i];
                float age = Time.unscaledTime - t.time;
                float a = Mathf.Clamp01(age * 6f) * Mathf.Clamp01((2.6f - age) * 2f);
                float y = mob ? baseY + (toasts.Count - 1 - i) * 34 : baseY - (toasts.Count - 1 - i) * 34 - age * 10f;
                var r = new Rect(mob ? 30 : 262, y, 300, 30);
                UIKit.Fill(r, new Color(0.03f, 0.03f, 0.03f, 0.6f * a));
                UIKit.Fill(new Rect(r.x, r.y, 3, r.height), new Color(t.color.r, t.color.g, t.color.b, a));
                UIKit.Tex(new Rect(r.x + 8, r.y + 3, 24, 24), HudIcons.Get(t.icon), new Color(t.color.r, t.color.g, t.color.b, a));
                UIKit.Text(new Rect(r.x + 40, r.y, r.width - 44, r.height), t.text, 15, new Color(0.9f, 0.87f, 0.8f, a), serif: true);
            }
        }

        public static string NightName(int n)
        {
            string[] ord = { "첫째", "둘째", "셋째", "넷째", "다섯째", "여섯째", "일곱째", "여덟째", "아홉째", "열째" };
            return n >= 1 && n <= ord.Length ? ord[n - 1] + " 밤" : $"{n}번째 밤";
        }

        public void Draw(GameManager gm, float w, float h, float scale)
        {
            W = w; H = h;
            var p = gm.Player;
            if (p == null) return;
            var cam = gm.ActiveCamera;
            float dt = UIKit.IsRepaint ? Time.unscaledDeltaTime : 0f;

            DrawDamageNumbers(cam, scale);

            // ---- 화면 효과 ----
            float hpRatio = p.Hp / p.MaxHp;
            if (hpRatio < 0.35f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (hpRatio < 0.15f ? 7f : 4f));
                UIKit.Vignette(W, H, new Color(0.55f, 0f, 0f, (0.35f - hpRatio) * 2.2f * (0.6f + 0.4f * pulse)));
            }
            if (p.HurtFlash > 0f) UIKit.Vignette(W, H, new Color(0.8f, 0f, 0f, p.HurtFlash * 2.5f));
            if (p.Infection.IsFrenzy)
            {
                UIKit.Vignette(W, H, new Color(0.35f, 0f, 0.5f, 0.85f));
                UIKit.Fill(new Rect(0, 0, W, H), new Color(0.25f, 0f, 0.3f, 0.12f + Mathf.PingPong(Time.time, 0.5f) * 0.15f));
            }

            if (p.Weapons.IsScoped) DrawScope();
            if (gm.State != GameState.GameOver && !gm.MenuOpen) DrawCrosshair(p, cam);
            if (gm.State == GameState.Playing && !gm.MenuOpen) DrawAmmoWarning(p);

            // ---- 생명 구슬 ----
            // 잃은 생명은 잠시 남았다가 천천히 빠진다
            hpTrail = Mathf.Max(hpRatio, Mathf.MoveTowards(hpTrail, hpRatio, dt * 0.6f));

            var hpOrb = new Rect(36, H - 226, 190, 190);
            // 잃은 양 꼬리 (밝은 금빛) → 그 위에 현재 생명
            UIKit.Orb(hpOrb, hpTrail, new Color(1f, 0.75f, 0.55f), UITheme.BloodBright);
            DrawOrbFill(hpOrb, hpRatio, new Color(0.8f, 0.07f, 0.08f));
            UIKit.Text(new Rect(hpOrb.x, hpOrb.center.y - 22, hpOrb.width, 30), $"{p.Hp:0}", 30, UITheme.Bone, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Text(new Rect(hpOrb.x, hpOrb.center.y + 6, hpOrb.width, 22), $"/ {p.MaxHp:0}", 16, UITheme.BoneDim, TextAnchor.MiddleCenter);
            UIKit.Text(new Rect(hpOrb.x, hpOrb.y - 34, hpOrb.width, 28), "생  명", 18, UITheme.Gold, TextAnchor.MiddleCenter, serif: true, bold: true);
            // 레벨 문장
            UIKit.Emblem(new Rect(hpOrb.xMax - 46, hpOrb.y - 14, 60, 60), p.Level.ToString(), UITheme.GoldBright, true);

            // ---- 역병 구슬 ----
            var inf = p.Infection;
            var infOrb = new Rect(W - 226, H - 226, 190, 190);
            Color infFill = inf.IsFrenzy ? Color.Lerp(UITheme.Infect, UITheme.InfectBright, Mathf.PingPong(Time.time * 3f, 1f)) : UITheme.Infect;
            UIKit.Orb(infOrb, inf.Gauge / InfectionSystem.Max, infFill, UITheme.InfectBright);
            // 능력 비용 눈금
            CostTick(infOrb, inf.RegenCostFinal / InfectionSystem.Max, UITheme.Good);
            CostTick(infOrb, inf.RageCostFinal / InfectionSystem.Max, UITheme.BloodBright);
            UIKit.Text(new Rect(infOrb.x, infOrb.center.y - 22, infOrb.width, 30), inf.IsFrenzy ? $"폭주 {inf.FrenzyTime:0.0}" : $"{inf.Gauge:0}%", 28, UITheme.Bone, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Text(new Rect(infOrb.x, infOrb.center.y + 6, infOrb.width, 22), $"피해 +{inf.Gauge / InfectionSystem.Max * 20f:0}%", 15, UITheme.BoneDim, TextAnchor.MiddleCenter);
            UIKit.Text(new Rect(infOrb.x, infOrb.y - 34, infOrb.width, 28), "역  병", 18, UITheme.InfectBright, TextAnchor.MiddleCenter, serif: true, bold: true);

            // ---- 역병 능력 슬롯 ---- (터치 조작에선 화면 버튼이 같은 일을 하므로 겹치지 않게 숨김)
            if (!GameInput.Mobile)
            {
                float sx = 262, sy = H - 112, ss = 76;
                AbilitySlot(new Rect(sx, sy, ss, ss), "Q", HudIcons.Icon.Flame, new Color(1f, 0.45f, 0.3f), "광폭화", inf.IsRage, inf.RageTime / 6f, inf.Gauge >= inf.RageCostFinal && !inf.IsFrenzy, $"{inf.RageCostFinal:0}",
                    "<b>광폭화</b>\n역병 기운을 태워 6초간\n• 피해 +30%  • 이동 +20%  • 저지력 +50%");
                AbilitySlot(new Rect(sx + ss + 14, sy, ss, ss), "E", HudIcons.Icon.Heart, new Color(0.6f, 0.9f, 0.55f), "재생", inf.IsRegen, inf.IsRegen ? 1f : 0f, inf.Gauge >= inf.RegenCostFinal && !inf.IsFrenzy, $"{inf.RegenCostFinal:0}",
                    "<b>재생</b>\n역병 기운으로 4초간 생명 50 회복");
                AbilitySlot(new Rect(sx + (ss + 14) * 2, sy, ss, ss), "H", HudIcons.Icon.Pill, new Color(0.95f, 0.85f, 0.55f), "청심환", false, 0f, inf.Antidotes > 0, $"×{inf.Antidotes}",
                    "<b>청심환</b>\n폭주를 즉시 멈추거나 역병 기운을 40 낮춘다.\n밤을 넘길 때마다 하나씩 얻는다 (최대 2).");

                if (kitFlash > 0f) UIKit.Tex(new Rect(sx + (ss + 14) * 3 + 14 - 18, sy - 18, ss + 36, ss + 36), UITheme.Glow, new Color(1f, 0.45f, 0.35f, kitFlash * 0.7f));
                if (drinkFlash > 0f) UIKit.Tex(new Rect(sx + (ss + 14) * 4 + 14 - 18, sy - 18, ss + 36, ss + 36), UITheme.Glow, new Color(0.45f, 0.78f, 1f, drinkFlash * 0.7f));
                AbilitySlot(new Rect(sx + (ss + 14) * 3 + 14, sy, ss, ss), "X", HudIcons.Icon.Kit, new Color(1f, 0.42f, 0.36f), "치료 키트", p.IsUsingItem, p.KitProgress, p.Kits > 0, $"×{p.Kits}",
                    $"<b>치료 키트</b>\n2.5초 동안 상처를 감아 생명 {p.KitHeal:0} 회복.\n감는 동안 느려지고, 맞거나 쏘면 끊긴다.");
                AbilitySlot(new Rect(sx + (ss + 14) * 4 + 14, sy, ss, ss), "C", HudIcons.Icon.Bottle, new Color(0.45f, 0.78f, 1f), "회복제", p.Controller.BoostTime > 0f, p.Controller.BoostTime / 15f, p.Drinks > 0, $"×{p.Drinks}",
                    "<b>스태미너 회복제</b>\n기운을 다 채우고 15초 동안 덜 지친다.");
            }

            // ---- 스태미너 (생명 구슬 아래) ----
            var ctl = p.Controller;
            Color stc = ctl.Exhausted ? UITheme.BloodBright : new Color(0.55f, 0.75f, 0.45f);
            UIKit.Bar(new Rect(hpOrb.x + 10, hpOrb.yMax + 14, hpOrb.width - 20, 8), ctl.Stamina / ctl.MaxStamina, stc);
            UIKit.Text(new Rect(hpOrb.x, hpOrb.yMax + 22, hpOrb.width, 16), ctl.Exhausted ? "지쳤다" : "기 운", 12, ctl.Exhausted ? UITheme.BloodBright : UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            // 앉기 표시: 앉아 있으면 생명 구슬 옆에 웅크린 모양 + 효과 요약
            if (ctl.CrouchBlend > 0.05f)
            {
                var cr = GameInput.Mobile ? new Rect(30, 130, 40, 40) : new Rect(262, H - 172, 40, 40); // 능력 칸 줄 바로 위 (터치: 왼쪽 위)
                UIKit.Tex(cr, HudIcons.Get(HudIcons.Icon.Crouch), new Color(0.86f, 0.82f, 0.72f, ctl.CrouchBlend));
                UIKit.Text(new Rect(cr.xMax + 4, cr.y + 4, 140, 18), "앉음", 14, new Color(0.86f, 0.82f, 0.72f, ctl.CrouchBlend), serif: true, bold: true);
                UIKit.Text(new Rect(cr.xMax + 4, cr.y + 22, 160, 16), "탄퍼짐·반동 ↓  소리 ↓", 11, new Color(0.6f, 0.58f, 0.54f, ctl.CrouchBlend));
            }

            // ---- 치료 중 ----
            if (p.IsUsingItem)
            {
                var kr = new Rect(W / 2 - 150, H / 2 + 70, 300, 10);
                UIKit.Bar(kr, p.KitProgress, new Color(0.6f, 0.9f, 0.55f));
                UIKit.Text(new Rect(kr.x, kr.y + 12, kr.width, 22), "상처를 감는 중…", 15, UITheme.Bone, TextAnchor.MiddleCenter, serif: true);
            }

            // ---- 탄약 / 무기 ----
            DrawWeapon(p);

            // ---- 경험치 ----
            var xr = new Rect(W / 2 - 380, H - 30, 760, 10);
            UIKit.Bar(xr, (float)p.Xp / p.XpToNext, UITheme.Gold);
            UIKit.Text(new Rect(xr.x, xr.y - 26, xr.width, 22),
                $"Lv.{p.Level}   ·   {p.Xp} / {p.XpToNext}" + (p.StatPoints > 0 || p.PendingTraitPicks > 0 ? $"   ·   <color=#{UIKit.Hex(UITheme.GoldBright)}>새 단련 {p.StatPoints}{(p.PendingTraitPicks > 0 ? " · 새 기질" : "")} — 서낭당에서</color>" : ""),
                14, UITheme.BoneDim, TextAnchor.MiddleCenter);

            DrawNoise(gm);

            // ---- 위쪽: 밤 표식 / 남은 망자 ----
            DrawNightPlaque(gm);

            // ---- 오른쪽 위: 엽전 / 처치 ----
            UIKit.Text(new Rect(W - 330, 18, 300, 28), $"엽전 <color=#{UIKit.Hex(UITheme.GoldBright)}>{p.Gold}</color>     처치 <color=#{UIKit.Hex(UITheme.Bone)}>{p.Kills}</color>", 18, UITheme.BoneDim, TextAnchor.MiddleRight, serif: true);

            DrawBoss(dt);
            DrawIntro(gm, dt);
            DrawDamageDirections(p, cam);
            DrawStreak(p);
            DrawStragglers(cam, scale);
            DrawPickups(dt);
            DrawTargetTag(cam);
        }

        /// <summary>조준점 아래 탄약 경고: 적으면 'R 장전', 탄창이 비면 강하게, 예비탄까지 없으면 무기 바꾸라고.</summary>
        void DrawAmmoWarning(PlayerCharacter p)
        {
            var wp = p.Weapons;
            var w = wp.Current;
            if (w == null || wp.IsReloading || wp.Mode != PlayerWeapons.Hand.Gun || GameInput.Mobile) return;
            int mag = w.MagazineSize;
            if (mag <= 0) return;
            string msg; Color c;
            if (w.ammoInMag == 0 && w.reserve == 0) { msg = "탄이 떨어졌다 — 다른 무기(1~5)나 칼(V)"; c = UITheme.BloodBright; }
            else if (w.ammoInMag == 0) { msg = "장전 — R"; c = UITheme.BloodBright; }
            else if (w.ammoInMag <= Mathf.Max(1, mag * 0.25f)) { msg = "R 장전"; c = UITheme.Warn; }
            else return;
            float pulse = w.ammoInMag == 0 && w.reserve == 0 ? 1f : 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 7f);
            UIKit.Text(new Rect(W / 2 - 200, H / 2 + 46, 400, 24), msg, 16, new Color(c.r, c.g, c.b, pulse), TextAnchor.MiddleCenter, serif: true, bold: true);
        }

        // ---- 조준한 망자 이름표 (유형 이름 + 체력) ----
        Monster tagTarget;
        float tagTime;

        void DrawTargetTag(Camera cam)
        {
            if (cam == null) return;
            if (UIKit.IsRepaint)
            {
                var ray = new Ray(cam.transform.position, cam.transform.forward);
                if (Physics.Raycast(ray, out var hit, 40f, ~((1 << 2) | (1 << 1)), QueryTriggerInteraction.Ignore))
                {
                    var part = hit.collider.GetComponent<MonsterPart>();
                    if (part != null && part.owner != null && !part.owner.IsDead) { tagTarget = part.owner; tagTime = 1.2f; }
                }
                tagTime -= Time.unscaledDeltaTime;
            }
            if (tagTarget == null || tagTarget.IsDead || tagTime <= 0f || tagTarget.IsBoss) return;
            var m = tagTarget;
            Vector3 head = m.transform.position + Vector3.up * (2.05f * m.Data.scale);
            Vector3 sp = cam.WorldToScreenPoint(head);
            if (sp.z <= 0f) return;
            float k = W / Screen.width;
            var c = new Vector2(sp.x * k, (Screen.height - sp.y) * k);
            float a = Mathf.Clamp01(tagTime * 2f);
            var r = new Rect(c.x - 80, c.y - 34, 160, 34);
            UIKit.Text(new Rect(r.x - 40, r.y, r.width + 80, 18), m.Data.displayName, 15, new Color(0.9f, 0.87f, 0.8f, a), TextAnchor.MiddleCenter, serif: true, bold: true);
            var br = new Rect(r.x + 20, r.y + 21, r.width - 40, 6);
            UIKit.Fill(new Rect(br.x - 1, br.y - 1, br.width + 2, br.height + 2), new Color(0f, 0f, 0f, 0.7f * a));
            UIKit.Fill(new Rect(br.x, br.y, br.width * Mathf.Clamp01(m.Hp / m.MaxHp), br.height), new Color(0.75f, 0.1f, 0.08f, a));
        }

        /// <summary>맞은 방향을 화면 가장자리에 붉게 (휴대폰에서 뒤에서 맞아도 알 수 있게).</summary>
        void DrawDamageDirections(PlayerCharacter p, Camera cam)
        {
            if (cam == null || p.DamageFrom.Count == 0) return;
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) return;
            foreach (var (from, time) in p.DamageFrom)
            {
                Vector3 d = from - p.transform.position; d.y = 0f;
                if (d.sqrMagnitude < 0.01f) continue;
                float a = Vector3.SignedAngle(fwd, d, Vector3.up) * Mathf.Deg2Rad;
                var pos = new Vector2(W / 2f + Mathf.Sin(a) * W * 0.36f, H / 2f - Mathf.Cos(a) * H * 0.36f);
                float alpha = Mathf.Clamp01(time / 1.2f);
                UIKit.Tex(new Rect(pos.x - 130, pos.y - 130, 260, 260), UITheme.Glow, new Color(0.9f, 0.05f, 0.03f, 0.75f * alpha));
                UIKit.Tex(new Rect(pos.x - 16, pos.y - 16, 32, 32), UITheme.Diamond, new Color(1f, 0.25f, 0.2f, alpha));
            }
        }

        /// <summary>연속 처치: "3연속!" 이 튀어나왔다 사라진다.</summary>
        void DrawStreak(PlayerCharacter p)
        {
            if (p.KillStreak < 2 || p.StreakTime > 1.4f) return;
            float t = p.StreakTime;
            float pop = 1f + Mathf.Max(0f, 0.12f - t) * 4f;
            float alpha = Mathf.Clamp01((1.4f - t) / 0.5f);
            int size = Mathf.RoundToInt(Mathf.Min(44f + p.KillStreak * 4f, 76f) * pop);
            Color c = p.KillStreak >= 5 ? new Color(1f, 0.35f, 0.25f, alpha) : new Color(0.97f, 0.94f, 0.86f, alpha);
            UIKit.Text(new Rect(0, H * 0.27f - size, W, size * 2f), $"{p.KillStreak}연속!", size, c, TextAnchor.MiddleCenter, serif: true, bold: true);
        }

        /// <summary>마지막 남은 망자 위치 표시 (화면 밖이면 가장자리에 화살표처럼).</summary>
        void DrawStragglers(Camera cam, float scale)
        {
            if (cam == null) return;
            // 디펜스: 지킴터 위치
            var wv = GameManager.Instance != null ? GameManager.Instance.Waves : null;
            if (wv != null && wv.Rule == RoundRule.Defense && wv.Stage != RoundStage.Cleared)
            {
                Vector3 dp = cam.WorldToScreenPoint(wv.DefendPos + Vector3.up * 2.5f);
                float dx = dp.x / scale, dy = (Screen.height - dp.y) / scale;
                if (dp.z < 0f) { dx = W - dx; dy = H - 60f; }
                dx = Mathf.Clamp(dx, 50f, W - 50f);
                dy = Mathf.Clamp(dy, 50f, H - 50f);
                UIKit.Emblem(new Rect(dx - 24, dy - 24, 48, 48), "지", UITheme.Gold, false);
                UIKit.Text(new Rect(dx - 50, dy + 24, 100, 20), $"{wv.DefendHp / WaveManager.DefendMaxHp * 100f:0}%", 14, UITheme.Bone, TextAnchor.MiddleCenter);
            }
            foreach (var m in Monster.Alive)
            {
                if (m == null || !m.Revealed) continue;
                Vector3 sp = cam.WorldToScreenPoint(m.transform.position + Vector3.up * 1.2f);
                bool behind = sp.z < 0f;
                float x = sp.x / scale, y = (Screen.height - sp.y) / scale;
                if (behind) { x = W - x; y = H - 40f; }
                bool off = behind || x < 30 || x > W - 30 || y < 30 || y > H - 30;
                x = Mathf.Clamp(x, 40f, W - 40f);
                y = Mathf.Clamp(y, 40f, H - 40f);
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 5f);
                UIKit.Emblem(new Rect(x - 22, y - 22, 44, 44), "망", UITheme.Blood, true);
                if (!off)
                {
                    float d = Vector3.Distance(cam.transform.position, m.transform.position);
                    UIKit.Text(new Rect(x - 40, y + 20, 80, 20), $"{d:0}m", 14, new Color(1f, 1f, 1f, pulse), TextAnchor.MiddleCenter);
                }
            }
        }

        /// <summary>첫째 밤: 목표와 조작 안내 (25초 뒤 사라짐).</summary>
        void DrawIntro(GameManager gm, float dt)
        {
            if (gm.Waves.Wave != 1) { introTime = 0f; return; }
            if (gm.State != GameState.Playing) return;
            introTime += dt;
            float a = Mathf.Clamp01(introTime * 1.5f) * Mathf.Clamp01((26f - introTime) / 2f);
            if (a <= 0f) return;
            UIKit.Alpha = a;
            var r = new Rect(30, 270, 430, 400);
            UIKit.Panel(r, "첫 째  밤");
            UIKit.Text(new Rect(r.x + 24, r.y + 66, r.width - 48, 60),
                "기본 망자 → 엘리트 망자 → <color=#" + UIKit.Hex(UITheme.BloodBright) + ">보스 망자</color>.\n보스를 쓰러뜨리면 밤이 끝난다.", 17, UITheme.Bone, TextAnchor.UpperLeft, serif: true, wrap: true);
            string keys =
                "<b>F</b>   손전등 (켜면 망자가 빛을 보고 온다)\n" +
                "총성·달리기·장전 소리에 망자가 모인다\n" +
                "<b>우클릭</b>   조준  ·  <b>R</b> 재장전\n" +
                "<b>Shift</b>   달리기 (기운 소모)  ·  <b>1 2</b> 주무기  <b>3</b> 권총\n" +
                "<b>4</b> 마체테 (<b>V</b> 빠른 칼질)  ·  <b>5</b> 특수 기기 (<b>G</b> 수류탄)\n" +
                "<b>X</b>   치료 키트  ·  <b>C</b> 스태미너 회복제\n" +
                "<b>Q / E</b>   역병의 힘 (맞으면 차오른다)\n" +
                "<b>H</b>   청심환 (폭주 해제)\n" +
                "주황빛 <color=#" + UIKit.Hex(new Color(1f, 0.6f, 0.1f)) + ">급소</color>를 노려라 — 피해 3배";
            UIKit.Text(new Rect(r.x + 24, r.y + 136, r.width - 48, 250), keys, 16, UITheme.BoneDim, TextAnchor.UpperLeft, wrap: true);
            UIKit.Alpha = 1f;
        }

        // ------------------------------------------------------------------ 부품
        void DrawOrbFill(Rect r, float t, Color c)
        {
            t = Mathf.Clamp01(t);
            float fillH = r.height * t;
            if (fillH <= 0.5f) return;
            GUI.BeginGroup(new Rect(r.x, r.yMax - fillH, r.width, fillH));
            UIKit.Tex(new Rect(0, fillH - r.height, r.width, r.height), UITheme.Orb, c);
            float wave = Mathf.Sin(Time.unscaledTime * 2.2f) * 2f;
            UIKit.Tex(new Rect(r.width * 0.12f, wave - 2, r.width * 0.76f, 5), UITheme.LineFade, new Color(1f, 0.8f, 0.8f, 0.4f));
            GUI.EndGroup();
            UIKit.Tex(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), UITheme.OrbRing, Color.white);
        }

        static void CostTick(Rect orb, float t, Color c)
        {
            float y = orb.yMax - orb.height * Mathf.Clamp01(t);
            UIKit.Tex(new Rect(orb.x - 14, y - 5, 10, 10), UITheme.Diamond, c);
        }

        /// <summary>
        /// 능력·보급 칸: 먹색 네모 + 그림 아이콘 + 왼쪽 위 단축키 + 오른쪽 아래 비용/개수 + 아래 이름.
        /// 쓰는 중이면 아래에서 차오르는 색 + 테두리 빛, 못 쓰면 흐리게.
        /// </summary>
        void AbilitySlot(Rect r, string key, HudIcons.Icon icon, Color tint, string name, bool active, float activeT, bool usable, string cost, string tip)
        {
            bool lit = usable || active;
            float pulse = active ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
            if (active) UIKit.Tex(new Rect(r.x - 16, r.y - 16, r.width + 32, r.height + 32), UITheme.Glow, new Color(tint.r, tint.g, tint.b, 0.35f + 0.2f * pulse));
            // 바탕: 먹
            UIKit.Fill(r, new Color(0.035f, 0.033f, 0.035f, 0.88f));
            UIKit.Fill(new Rect(r.x, r.y, r.width, r.height * 0.45f), new Color(1f, 1f, 1f, 0.025f)); // 위쪽 살짝 밝게
            if (active)
            {
                float fh = (r.height - 4) * Mathf.Clamp01(activeT);
                UIKit.Fill(new Rect(r.x + 2, r.yMax - 2 - fh, r.width - 4, fh), new Color(tint.r, tint.g, tint.b, 0.22f));
            }
            // 아이콘
            var ir = new Rect(r.center.x - r.width * 0.3f, r.center.y - r.height * 0.3f, r.width * 0.6f, r.height * 0.6f);
            Color ic = lit ? tint : new Color(0.42f, 0.4f, 0.38f, 0.8f);
            UIKit.Tex(new Rect(ir.x + 2, ir.y + 3, ir.width, ir.height), HudIcons.Get(icon), new Color(0f, 0f, 0f, 0.6f)); // 그림자
            UIKit.Tex(ir, HudIcons.Get(icon), ic);
            // 단축키 (왼쪽 위 작은 한지 칸)
            var kr = new Rect(r.x + 3, r.y + 3, 20, 20);
            UIKit.Fill(kr, lit ? new Color(0.86f, 0.82f, 0.72f, 0.92f) : new Color(0.35f, 0.34f, 0.32f, 0.9f));
            UIKit.Text(kr, key, 13, new Color(0.06f, 0.05f, 0.05f), TextAnchor.MiddleCenter, bold: true);
            // 비용·개수
            UIKit.Text(new Rect(r.x, r.yMax - 22, r.width - 5, 20), cost, 15, usable ? UITheme.Bone : UITheme.BloodBright, TextAnchor.MiddleRight, bold: true);
            // 테두리
            UIKit.Outline(r, active ? Color.Lerp(tint, Color.white, pulse * 0.3f) : lit ? new Color(0.62f, 0.58f, 0.5f, 0.85f) : new Color(0.3f, 0.29f, 0.27f, 0.8f), active ? 2f : 1f);
            UIKit.Text(new Rect(r.x - 8, r.yMax + 3, r.width + 16, 18), name, 13, lit ? UITheme.BoneDim : UITheme.Ash, TextAnchor.MiddleCenter, serif: true);
            UIKit.TooltipIf(r, tip);
        }

        void DrawWeapon(PlayerCharacter p)
        {
            var wp = p.Weapons;
            var w = wp.Current;
            float right = W - 262;
            var card = new Rect(right - 440, H - 214, 440, 166);
            UIKit.NineSlice(card, UITheme.Slot, 10, new Color(1, 1, 1, 0.85f));
            var icon = new Rect(card.x + 14, card.y + 12, 190, 74);

            if (wp.Mode == PlayerWeapons.Hand.Melee)
            {
                WeaponIcon.Draw(icon, WeaponClass.Melee, UITheme.Bone);
                UIKit.Text(new Rect(card.x + 210, card.y + 10, card.width - 224, 34), PlayerWeapons.MeleeName, 26, UITheme.Bone, TextAnchor.MiddleRight, serif: true, bold: true);
                UIKit.Text(new Rect(card.x + 210, card.y + 44, card.width - 224, 22), $"근접  ·  위력 {wp.MeleeDamage:0}  ·  소리 거의 없음", 14, UITheme.BoneDim, TextAnchor.MiddleRight);
                UIKit.Text(new Rect(card.x + 14, card.y + 96, card.width - 28, 30), "좌클릭 베기  ·  한 번에 둘까지  ·  스태미너 8", 16, UITheme.BoneDim, TextAnchor.MiddleRight, serif: true);
                StaminaHint(p, card);
            }
            else if (wp.Mode == PlayerWeapons.Hand.Gadget)
            {
                var g = wp.Gadget;
                int n = wp.GadgetCount[(int)g];
                WeaponIcon.DrawGadget(icon, g, n > 0 ? UITheme.Bone : UITheme.Ash);
                UIKit.Text(new Rect(card.x + 210, card.y + 10, card.width - 224, 34), Gadgets.Name(g), 24, UITheme.Bone, TextAnchor.MiddleRight, serif: true, bold: true);
                UIKit.Text(new Rect(card.x + 210, card.y + 44, card.width - 224, 22), "5번을 다시 누르면 다음 기기", 14, UITheme.BoneDim, TextAnchor.MiddleRight);
                UIKit.Text(new Rect(card.x + 14, card.y + 74, card.width - 28, 60), $"<size=52><b>{n}</b></size><size=22><color=#{UIKit.Hex(UITheme.BoneDim)}>  /  {Gadgets.MaxCarry(g)}</color></size>", 52, n > 0 ? UITheme.Bone : UITheme.BloodBright, TextAnchor.MiddleRight, serif: true);
                UIKit.Text(new Rect(card.x + 14, card.yMax - 30, card.width - 28, 22), Gadgets.Desc(g), 13, UITheme.Ash, TextAnchor.MiddleRight);
            }
            else
            {
                Color gc = Grades.Color(w.grade);
                WeaponIcon.Draw(icon, w.data, Color.Lerp(UITheme.Bone, gc, 0.55f));
                Color elem = CombatNames.ColorOf(w.DamageType);
                UIKit.Text(new Rect(card.x + 210, card.y + 8, card.width - 224, 34), w.data.displayName + (w.upgradeLevel > 0 ? $" +{w.upgradeLevel}" : ""), 24, UITheme.Bone, TextAnchor.MiddleRight, serif: true, bold: true);
                UIKit.Text(new Rect(card.x + 210, card.y + 40, card.width - 224, 22),
                    $"<color=#{UIKit.Hex(gc)}>{Grades.Name(w.grade)}</color>  ·  {GameText.WeaponClassName(w.data.weaponClass)} {w.data.tier}단  ·  <color=#{UIKit.Hex(elem)}>{GameText.ElementName(w.DamageType)}</color>" +
                    (p.Controller.WeightPenalty < 1f ? $"  ·  <color=#{UIKit.Hex(UITheme.Warn)}>과적</color>" : ""),
                    14, UITheme.BoneDim, TextAnchor.MiddleRight);

                bool low = w.ammoInMag <= w.MagazineSize * 0.25f;
                Color ac = w.ammoInMag == 0 ? UITheme.BloodBright : low ? UITheme.Warn : UITheme.Bone;
                string reload = wp.IsReloading ? $"<size=18><color=#{UIKit.Hex(UITheme.Gold)}>{(w.ammoInMag == 0 ? "빈 탄창 교체" : "탄창 교체")}</color></size>   " : "";
                UIKit.Text(new Rect(card.x + 14, card.y + 66, card.width - 28, 62),
                    $"{reload}<size=54><b>{w.ammoInMag}</b></size><size=22><color=#{UIKit.Hex(UITheme.BoneDim)}>  /  {w.reserve}</color></size>", 54, ac, TextAnchor.MiddleRight, serif: true);
                if (wp.IsReloading) UIKit.Bar(new Rect(card.x + 20, card.y + 96, 170, 6), wp.ReloadProgress, UITheme.Gold);

                // 탄알: 탄피 + 탄두 모양 (최대 40발 표시)
                int mag = w.MagazineSize;
                int ticks = Mathf.Min(mag, 40);
                float tw = Mathf.Min(8f, (card.width - 40) / ticks - 2f), tg = 2f;
                float tx = card.xMax - 16 - ticks * (tw + tg);
                int full = Mathf.CeilToInt(w.ammoInMag * ticks / (float)mag);
                for (int i = 0; i < ticks; i++)
                {
                    bool f = i < full;
                    var br = new Rect(tx + i * (tw + tg), card.yMax - 30, tw, 16);
                    Color bc = f ? new Color(0.78f, 0.62f, 0.32f, 0.95f) : new Color(1, 1, 1, 0.1f);
                    UIKit.Fill(new Rect(br.x, br.y + 5, br.width, br.height - 5), bc);                       // 탄피
                    UIKit.Fill(new Rect(br.x + br.width * 0.15f, br.y, br.width * 0.7f, 6), f ? new Color(0.55f, 0.42f, 0.3f, 0.95f) : bc); // 탄두
                }
            }

            // ---- 슬롯 5칸: 1·2 주무기  3 권총  4 칼  5 기기 ----
            float sw = 84, sh = 40, gap = 6;
            float sx0 = right - 5 * (sw + gap) + gap;
            for (int k = 0; k < 5; k++)
            {
                var r = new Rect(sx0 + k * (sw + gap), H - 44, sw, sh);
                bool cur = wp.ActiveKey == k;
                bool exists = k != 1 || wp.PrimaryCount >= 2;
                UIKit.NineSlice(r, cur ? UITheme.PanelHeader : UITheme.Slot, 6, new Color(1, 1, 1, exists ? (cur ? 1f : 0.75f) : 0.3f));
                Color ic = cur ? UITheme.GoldBright : UITheme.BoneDim;
                var ir = new Rect(r.x + 18, r.y + 4, r.width - 22, r.height - 8);
                if (k <= 2 && exists)
                {
                    var sl = wp.Slots[k == 2 ? wp.Slots.Count - 1 : k];
                    WeaponIcon.Draw(ir, sl.data, ic);
                }
                else if (k == 3) WeaponIcon.Draw(ir, WeaponClass.Melee, ic);
                else if (k == 4)
                {
                    WeaponIcon.DrawGadget(ir, wp.Gadget, ic);
                    int total = 0; foreach (var c in wp.GadgetCount) total += c;
                    UIKit.Text(new Rect(r.x, r.y, r.width - 4, r.height), total.ToString(), 12, UITheme.BoneDim, TextAnchor.LowerRight);
                }
                UIKit.Text(new Rect(r.x + 4, r.y + 2, 16, 16), (k + 1).ToString(), 12, cur ? UITheme.GoldBright : UITheme.Ash, TextAnchor.UpperLeft, bold: true);
            }
        }

        void StaminaHint(PlayerCharacter p, Rect card)
        {
            var c = p.Controller;
            UIKit.Bar(new Rect(card.x + 20, card.yMax - 24, card.width - 40, 6), c.Stamina / c.MaxStamina, new Color(0.55f, 0.75f, 0.45f));
        }

        /// <summary>내가 낸 소리 크기와 빗소리 가림. 소리가 크면 망자가 몰려온다.</summary>
        void DrawNoise(GameManager gm)
        {
            if (gm.State == GameState.Intermission) return;
            float loud = Noise.PlayerLoudness;
            var r = new Rect(W / 2 - 110, H - 92, 220, 6);
            Color c = Color.Lerp(UITheme.GoldDim, UITheme.BloodBright, Mathf.InverseLerp(0.5f, 1f, loud));
            UIKit.Bar(r, loud, c);
            float mask = Noise.RainMask;
            string rain = mask < 0.75f ? "  ·  빗소리가 소리를 덮는다" : mask < 0.95f ? "  ·  빗소리" : "";
            string light = Flashlight.Disabled ? "  ·  손전등 금지" : Flashlight.Active ? $"  ·  <color=#{UIKit.Hex(UITheme.GoldBright)}>손전등 켜짐 — 망자가 빛을 본다</color>" : "  ·  손전등 꺼짐 (F)";
            UIKit.Text(new Rect(r.x - 160, r.y - 22, r.width + 320, 20), "소 리" + rain + light, 13, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
        }

        void DrawNightPlaque(GameManager gm)
        {
            var waves = gm.Waves;
            var r = new Rect(W / 2 - 240, 12, 480, 70);
            UIKit.NineSlice(r, UITheme.Panel, 12, new Color(1, 1, 1, 0.92f));
            UIKit.Ornament(new Vector2(r.x + 24, r.center.y), 9, UITheme.Gold);
            UIKit.Ornament(new Vector2(r.xMax - 24, r.center.y), 9, UITheme.Gold);

            if (gm.State == GameState.Intermission)
            {
                UIKit.Text(new Rect(r.x, r.y + 6, r.width, 34), "날이 밝았다", 24, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
                UIKit.Text(new Rect(r.x, r.y + 38, r.width, 24), $"다음 밤까지 {gm.IntermissionTime:0}초  ·  Tab 서낭당  ·  Enter 밤을 맞는다", 15, UITheme.BoneDim, TextAnchor.MiddleCenter);
            }
            else
            {
                string night = NightName(waves.Wave) + (waves.IsBossWave ? "  —  대감의 밤" : "");
                UIKit.Text(new Rect(r.x, r.y + 6, r.width, 34), night, 24, waves.IsBossWave ? UITheme.BloodBright : UITheme.Bone, TextAnchor.MiddleCenter, serif: true, bold: true);
                // 단계: 기본 → 엘리트 → 보스 (현재 단계만 밝게)
                string Step(RoundStage st)
                {
                    string n = WaveManager.StageName(st);
                    if (st == waves.Stage) return $"<color=#{UIKit.Hex(st == RoundStage.Boss ? UITheme.BloodBright : UITheme.GoldBright)}>{n}</color>";
                    return st < waves.Stage ? $"<color=#{UIKit.Hex(UITheme.Ash)}>{n}</color>" : n;
                }
                string sub;
                if (waves.Rule == RoundRule.Defense)
                    sub = $"<color=#{UIKit.Hex(UITheme.GoldBright)}>지킴</color>  ·  날이 밝기까지 {waves.TimeLeft:0}초  ·  돌무더기 {waves.DefendHp / WaveManager.DefendMaxHp * 100f:0}%";
                else if (waves.Rule == RoundRule.Mission)
                    sub = $"<color=#{UIKit.Hex(UITheme.GoldBright)}>소탕</color>  ·  남은 시간 <color=#{UIKit.Hex(waves.TimeLeft < 30f ? UITheme.BloodBright : UITheme.Bone)}>{waves.TimeLeft:0}초</color>  ·  남은 망자 {waves.AliveCount + waves.RemainingToSpawn}";
                else
                    sub = $"{Step(RoundStage.Normal)}  ›  {Step(RoundStage.Elite)}  ›  {Step(RoundStage.Boss)}    ·    남은 {waves.AliveCount + waves.RemainingToSpawn}";
                UIKit.Text(new Rect(r.x, r.y + 38, r.width, 24), sub, 15, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
                // 몰려오기까지 남은 시간
                if (waves.Modifiers.Count > 0)
                {
                    string mods = "변고: ";
                    for (int i = 0; i < waves.Modifiers.Count; i++) mods += (i > 0 ? " · " : "") + WaveManager.ModifierName(waves.Modifiers[i]);
                    UIKit.Text(new Rect(r.x - 100, r.yMax + 44, r.width + 200, 20), mods, 13, UITheme.BloodBright, TextAnchor.MiddleCenter, serif: true);
                }
                if (waves.Rule == RoundRule.Defense)
                    UIKit.Bar(new Rect(r.x + 60, r.yMax - 8, r.width - 120, 5), waves.DefendHp / WaveManager.DefendMaxHp, UITheme.Gold);
                string rush = waves.Rushing ? $"<color=#{UIKit.Hex(UITheme.BloodBright)}>망자들이 모두 몰려온다</color>" : $"망자들이 냄새를 맡기까지  {waves.RushIn:0}초";
                UIKit.Text(new Rect(r.x, r.yMax + 24, r.width, 20), rush, 14, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            }
            var evo = gm.Evolution.Profile;
            if (evo.generation > 0)
                UIKit.Text(new Rect(r.x, r.yMax + 2, r.width, 22), $"역병의 적응 · {evo.generation}세대", 15, UITheme.InfectBright, TextAnchor.MiddleCenter, serif: true);
        }

        void DrawBoss(float dt)
        {
            Monster boss = null;
            foreach (var m in Monster.Alive) if (m.IsBoss) { boss = m; break; }
            if (boss == null) { bossTrail = 1f; return; }

            float t = boss.Hp / boss.MaxHp;
            bossTrail = Mathf.Max(t, Mathf.MoveTowards(bossTrail, t, dt * 0.25f));
            var r = new Rect(W / 2 - 430, 128, 860, 22);
            UIKit.Text(new Rect(r.x, r.y - 34, r.width, 30), boss.Data.displayName, 24, UITheme.BloodBright, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Bar(r, t, new Color(0.6f, 0.05f, 0.1f), null, bossTrail);
            for (int i = 1; i <= 2; i++)
                UIKit.Tex(new Rect(r.x + r.width * (i / 3f) - 7, r.y + 4, 14, 14), UITheme.Diamond, i <= boss.BossPhase ? UITheme.Ash : UITheme.GoldBright);
            for (int i = 0; i < 3; i++)
                UIKit.Tex(new Rect(r.center.x - 40 + i * 30, r.yMax + 8, 16, 16), i <= boss.BossPhase ? UITheme.Diamond : UITheme.DiamondHollow, UITheme.BloodBright);
        }

        void DrawCrosshair(PlayerCharacter p, Camera cam)
        {
            float cx = W / 2, cy = H / 2;
            float alpha = 1f - Mathf.Clamp01((p.Weapons.Aim - 0.3f) / 0.4f);
            if (alpha > 0.01f)
            {
                float fov = cam != null ? cam.fieldOfView : 75f;
                float gap = Mathf.Tan(p.Weapons.CurrentSpread * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * (H / 2f);
                gap = Mathf.Clamp(gap, 4f, 300f);
                Color c = new Color(0.95f, 0.88f, 0.75f, alpha);
                Color sh = new Color(0, 0, 0, alpha * 0.6f);
                float len = 12;
                Rect[] lines =
                {
                    new Rect(cx - 1, cy - gap - len, 2, len), new Rect(cx - 1, cy + gap, 2, len),
                    new Rect(cx - gap - len, cy - 1, len, 2), new Rect(cx + gap, cy - 1, len, 2),
                };
                foreach (var l in lines) UIKit.Fill(new Rect(l.x - 1, l.y - 1, l.width + 2, l.height + 2), sh);
                foreach (var l in lines) UIKit.Fill(l, c);
                UIKit.Tex(new Rect(cx - 3, cy - 3, 6, 6), UITheme.Diamond, c);
            }

            if (p.Weapons.HitMarker > 0f)
            {
                Color hc = p.Weapons.LastHitKill ? UITheme.BloodBright : p.Weapons.LastHitWeak ? new Color(1f, 0.6f, 0.1f) : Color.white;
                float s = 10 + (0.12f - p.Weapons.HitMarker) * 60f;
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i % 2 == 0 ? -1 : 1), sy = (i < 2 ? -1 : 1);
                    UIKit.Tex(new Rect(cx + sx * s - 4, cy + sy * s - 4, 8, 8), UITheme.Diamond, hc);
                }
            }
            if (p.Weapons.IsReloading)
            {
                var r = new Rect(cx - 70, cy + 44, 140, 6);
                UIKit.Fill(r, new Color(0, 0, 0, 0.6f));
                UIKit.Tex(new Rect(r.x, r.y, r.width * p.Weapons.ReloadProgress, r.height), UITheme.BarFill, UITheme.Gold);
            }
        }

        void DrawDamageNumbers(Camera cam, float scale)
        {
            if (cam == null) return;
            foreach (var e in DamageNumbers.Entries)
            {
                Vector3 sp = cam.WorldToScreenPoint(e.pos);
                if (sp.z <= 0f) continue;
                float x = sp.x / scale, y = (Screen.height - sp.y) / scale;
                float a = 1f - e.t / DamageNumbers.Life;
                Color c = e.zone == HitZone.WeakPoint ? new Color(1f, 0.55f, 0.1f)
                    : e.zone == HitZone.Head ? new Color(1f, 0.92f, 0.45f)
                    : e.zone == HitZone.Armor ? new Color(0.65f, 0.62f, 0.58f)
                    : e.type != DamageType.Physical ? CombatNames.ColorOf(e.type) : UITheme.Bone;
                c.a = a;
                float pop = e.t < 0.08f ? 1f + (0.08f - e.t) * 6f : 1f;
                int size = Mathf.RoundToInt((e.crit ? 32 : e.zone == HitZone.WeakPoint ? 27 : 21) * pop);
                string s = Mathf.RoundToInt(e.amount) + (e.crit ? "!" : "");
                UIKit.Text(new Rect(x - 80, y - 25, 160, 50), s, size, c, TextAnchor.MiddleCenter, serif: true, bold: e.crit || e.zone == HitZone.WeakPoint);
            }
        }

        /// <summary>저격 스코프 화면: 원형 시야 + 십자선, 나머지는 검게.</summary>
        void DrawScope()
        {
            if (scopeMask == null)
            {
                const int N = 512;
                scopeMask = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[N * N];
                float r = N * 0.5f;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Mathf.Sqrt((x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f)) / r;
                        px[y * N + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01((d - 0.94f) / 0.06f) * 255));
                    }
                scopeMask.SetPixels32(px);
                scopeMask.Apply();
            }
            float size = H, x0 = W / 2 - size / 2;
            UIKit.Fill(new Rect(0, 0, x0 + 1, H), Color.black);
            UIKit.Fill(new Rect(x0 + size - 1, 0, W - x0 - size + 1, H), Color.black);
            UIKit.Tex(new Rect(x0, 0, size, size), scopeMask, Color.white);
            Color line = new Color(0, 0, 0, 0.9f);
            float cx = W / 2, cy = H / 2;
            UIKit.Fill(new Rect(x0, cy - 1, size, 2), line);
            UIKit.Fill(new Rect(cx - 1, 0, 2, H), line);
            UIKit.Fill(new Rect(x0 + size * 0.2f, cy - 3, size * 0.25f, 6), line);
            UIKit.Fill(new Rect(x0 + size * 0.55f, cy - 3, size * 0.25f, 6), line);
            UIKit.Fill(new Rect(cx - 3, cy + size * 0.08f, 6, size * 0.35f), line);
            UIKit.Tex(new Rect(cx - 3, cy - 3, 6, 6), UITheme.Diamond, UITheme.BloodBright);
        }
    }
}

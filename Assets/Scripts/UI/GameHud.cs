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

            // ---- 역병 능력 슬롯 ----
            float sx = 262, sy = H - 112, ss = 76;
            AbilitySlot(new Rect(sx, sy, ss, ss), "Q", "광", "광폭화", inf.IsRage, inf.RageTime / 6f, inf.Gauge >= inf.RageCostFinal && !inf.IsFrenzy, $"{inf.RageCostFinal:0}",
                "<b>광폭화</b>\n역병 기운을 태워 6초간\n• 피해 +30%  • 이동 +20%  • 저지력 +50%");
            AbilitySlot(new Rect(sx + ss + 14, sy, ss, ss), "E", "생", "재생", inf.IsRegen, inf.IsRegen ? 1f : 0f, inf.Gauge >= inf.RegenCostFinal && !inf.IsFrenzy, $"{inf.RegenCostFinal:0}",
                "<b>재생</b>\n역병 기운으로 4초간 생명 50 회복");
            AbilitySlot(new Rect(sx + (ss + 14) * 2, sy, ss, ss), "H", "약", "청심환", false, 0f, inf.Antidotes > 0, $"×{inf.Antidotes}",
                "<b>청심환</b>\n폭주를 즉시 멈추거나 역병 기운을 40 낮춘다.\n밤을 넘길 때마다 하나씩 얻는다 (최대 2).");

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
            DrawStragglers(cam, scale);
        }

        /// <summary>마지막 남은 망자 위치 표시 (화면 밖이면 가장자리에 화살표처럼).</summary>
        void DrawStragglers(Camera cam, float scale)
        {
            if (cam == null) return;
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
            var r = new Rect(30, 300, 400, 330);
            UIKit.Panel(r, "첫 째  밤");
            UIKit.Text(new Rect(r.x + 24, r.y + 66, r.width - 48, 60),
                "기본 망자 → 엘리트 망자 → <color=#" + UIKit.Hex(UITheme.BloodBright) + ">보스 망자</color>.\n보스를 쓰러뜨리면 밤이 끝난다.", 17, UITheme.Bone, TextAnchor.UpperLeft, serif: true, wrap: true);
            string keys =
                "<b>F</b>   손전등 (켜면 망자가 빛을 보고 온다)\n" +
                "총성·달리기·장전 소리에 망자가 모인다\n" +
                "<b>우클릭</b>   조준  ·  <b>R</b> 재장전\n" +
                "<b>Shift</b>   달리기  ·  <b>1 2 3</b> 무기\n" +
                "<b>Q / E</b>   역병의 힘 (맞으면 차오른다)\n" +
                "<b>H</b>   청심환 (폭주 해제)\n" +
                "주황빛 <color=#" + UIKit.Hex(new Color(1f, 0.6f, 0.1f)) + ">급소</color>를 노려라 — 피해 3배";
            UIKit.Text(new Rect(r.x + 24, r.y + 136, r.width - 48, 180), keys, 16, UITheme.BoneDim, TextAnchor.UpperLeft, wrap: true);
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

        void AbilitySlot(Rect r, string key, string glyph, string name, bool active, float activeT, bool usable, string cost, string tip)
        {
            UIKit.NineSlice(r, UITheme.Slot, 8, Color.white);
            if (active)
            {
                UIKit.Tex(new Rect(r.x - 14, r.y - 14, r.width + 28, r.height + 28), UITheme.Glow, new Color(1f, 0.3f, 0.2f, 0.5f));
                float fh = (r.height - 8) * Mathf.Clamp01(activeT);
                UIKit.Fill(new Rect(r.x + 4, r.yMax - 4 - fh, r.width - 8, fh), new Color(0.7f, 0.1f, 0.1f, 0.45f));
            }
            UIKit.Text(new Rect(r.x, r.y + 4, r.width, r.height - 20), glyph, 34, usable || active ? UITheme.GoldBright : UITheme.Ash, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Text(new Rect(r.x + 6, r.y + 2, 20, 20), key, 14, UITheme.Bone, bold: true);
            UIKit.Text(new Rect(r.x, r.yMax - 22, r.width - 6, 20), cost, 14, usable ? UITheme.Bone : UITheme.BloodBright, TextAnchor.MiddleRight, bold: true);
            UIKit.Text(new Rect(r.x, r.yMax + 2, r.width, 20), name, 14, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            if (!usable && !active) UIKit.Fill(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), new Color(0, 0, 0, 0.45f));
            UIKit.NineSlice(r, UITheme.Frame, 6, active ? UITheme.BloodBright : new Color(0.55f, 0.42f, 0.22f, 0.9f));
            UIKit.TooltipIf(r, tip);
        }

        void DrawWeapon(PlayerCharacter p)
        {
            var wp = p.Weapons;
            var w = wp.Current;
            float right = W - 262;

            Color elem = CombatNames.ColorOf(w.DamageType);
            UIKit.Text(new Rect(right - 420, H - 190, 420, 34), w.DisplayName, 26, UITheme.Bone, TextAnchor.MiddleRight, serif: true, bold: true);
            UIKit.Text(new Rect(right - 420, H - 160, 420, 24),
                $"{GameText.WeaponClassName(w.data.weaponClass)}  ·  <color=#{UIKit.Hex(elem)}>{GameText.ElementName(w.DamageType)}</color>" +
                (p.Controller.WeightPenalty < 1f ? $"  ·  <color=#{UIKit.Hex(UITheme.Warn)}>과적 −{Mathf.RoundToInt((1 - p.Controller.WeightPenalty) * 100)}%</color>" : ""),
                15, UITheme.BoneDim, TextAnchor.MiddleRight);

            bool low = w.ammoInMag <= w.MagazineSize * 0.25f;
            Color ac = w.ammoInMag == 0 ? UITheme.BloodBright : low ? UITheme.Warn : UITheme.Bone;
            string reload = wp.IsReloading ? $"<size=20><color=#{UIKit.Hex(UITheme.Gold)}>장전 중</color></size>   " : "";
            UIKit.Text(new Rect(right - 420, H - 136, 420, 70),
                $"{reload}<size=60><b>{w.ammoInMag}</b></size><size=24><color=#{UIKit.Hex(UITheme.BoneDim)}>  /  {w.reserve}</color></size>", 60, ac, TextAnchor.MiddleRight, serif: true);

            // 탄창 눈금 (최대 40칸)
            int mag = w.MagazineSize;
            int ticks = Mathf.Min(mag, 40);
            float tw = 7f, tg = 3f;
            float tx = right - ticks * (tw + tg);
            for (int i = 0; i < ticks; i++)
            {
                bool full = i < Mathf.CeilToInt(w.ammoInMag * ticks / (float)mag);
                UIKit.Fill(new Rect(tx + i * (tw + tg), H - 62, tw, 12), full ? new Color(ac.r, ac.g, ac.b, 0.9f) : new Color(1, 1, 1, 0.12f));
            }

            // 무기 슬롯
            float slotW = 132, gx = right - wp.Slots.Count * (slotW + 6) + 6;
            for (int i = 0; i < wp.Slots.Count; i++)
            {
                var r = new Rect(gx + i * (slotW + 6), H - 42, slotW, 28);
                bool cur = i == wp.CurrentIndex;
                UIKit.NineSlice(r, cur ? UITheme.PanelHeader : UITheme.Slot, 6, new Color(1, 1, 1, cur ? 1f : 0.75f));
                UIKit.Text(r, $"{i + 1}  {wp.Slots[i].data.displayName}", 14, cur ? UITheme.GoldBright : UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true, bold: cur);
            }
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
                string sub = $"{Step(RoundStage.Normal)}  ›  {Step(RoundStage.Elite)}  ›  {Step(RoundStage.Boss)}    ·    남은 {waves.AliveCount + waves.RemainingToSpawn}";
                UIKit.Text(new Rect(r.x, r.y + 38, r.width, 24), sub, 15, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
                // 몰려오기까지 남은 시간
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

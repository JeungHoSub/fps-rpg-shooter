using Hunt.Combat;
using Hunt.Core;
using Hunt.Data;
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
        // ---- 찢어진 쪽지 (보급 상자) ----
        static string noteText;
        static int noteIndex;
        static float noteShown = -100f;
        const string LoreKey = "hunt.lore.read";

        /// <summary>아직 안 읽은 다음 쪽지를 왼쪽에 8초 띄운다. 다 읽었으면 아무것도 안 한다.</summary>
        public static void ShowNextLoreNote()
        {
            int read = 0;
            try { read = PlayerPrefs.GetInt(LoreKey, 0); } catch { }
            if (read >= GameText.LoreNotes.Length) return;
            noteIndex = read;
            noteText = GameText.LoreNotes[read];
            noteShown = Time.unscaledTime;
            try { PlayerPrefs.SetInt(LoreKey, read + 1); PlayerPrefs.Save(); } catch { }
            if (read + 1 >= GameText.LoreNotes.Length) Achievements.Unlock("lore");
            SFX.PlayPitched(SFX.Swing, 0.3f, 1.6f); // 종이 펼치는 소리
        }

        void DrawLoreNote()
        {
            float age = Time.unscaledTime - noteShown;
            if (noteText == null || age > 9f) return;
            float a = UIKit.EaseOut(age * 3f) * Mathf.Clamp01((9f - age) / 0.8f);
            float keep = UIKit.Alpha;
            UIKit.Alpha = keep * a;
            var r = new Rect(36 - (1f - a) * 20f, H * 0.36f, 440, 190);
            UIKit.Tex(r, UITheme.Paper, new Color(0.92f, 0.86f, 0.72f, 0.95f));
            UIKit.Text(new Rect(r.x + 22, r.y + 14, r.width - 44, 24), $"찢어진 쪽지  {noteIndex + 1} / {GameText.LoreNotes.Length}", 15, new Color(0.35f, 0.12f, 0.08f), TextAnchor.MiddleLeft, serif: true, bold: true, shadow: false);
            UIKit.Text(new Rect(r.x + 22, r.y + 44, r.width - 44, r.height - 56), noteText, 17, new Color(0.12f, 0.1f, 0.08f), TextAnchor.UpperLeft, serif: true, wrap: true, shadow: false);
            UIKit.Alpha = keep;
        }

        // ---- 화면에 튄 피: 세게 물리면 화면 가장자리에 핏자국이 튀었다가 천천히 흘러내리며 옅어진다 ----
        struct Splat { public Vector2 pos; public float size, rot, life, max; }
        static readonly System.Collections.Generic.List<Splat> splats = new System.Collections.Generic.List<Splat>();

        /// <summary>피해 크기(생명 비율)에 맞춰 핏자국 1~3개.</summary>
        public static void ScreenBlood(float hpFraction)
        {
            if (!GameSettings.Gore) return;
            int n = hpFraction > 0.15f ? 3 : hpFraction > 0.07f ? 2 : 1;
            for (int i = 0; i < n && splats.Count < 8; i++)
            {
                // 가장자리 쪽 (가운데 조준을 가리지 않게)
                float ang = Random.value * Mathf.PI * 2f;
                var pos = new Vector2(0.5f + Mathf.Cos(ang) * Random.Range(0.36f, 0.48f), 0.5f + Mathf.Sin(ang) * Random.Range(0.3f, 0.45f));
                float life = Random.Range(2.5f, 4f);
                splats.Add(new Splat { pos = pos, size = Random.Range(160f, 300f) * (0.8f + hpFraction * 3f), rot = Random.Range(0f, 360f), life = life, max = life });
            }
        }

        void DrawScreenBlood(float dt)
        {
            if (splats.Count == 0) return;
            var tex = FX.BloodTexture;
            for (int i = splats.Count - 1; i >= 0; i--)
            {
                var sp = splats[i];
                if (dt > 0f) { sp.life -= dt; sp.pos.y += dt * 0.006f; }
                if (sp.life <= 0f) { splats.RemoveAt(i); continue; }
                splats[i] = sp;
                float a = Mathf.Clamp01(sp.life / sp.max * 1.6f) * 0.55f;
                float cx = sp.pos.x * W, cy = sp.pos.y * H;
                var keep = GUI.matrix;
                GUI.matrix = keep * Matrix4x4.Translate(new Vector3(cx, cy, 0f)) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, sp.rot)) * Matrix4x4.Translate(new Vector3(-cx, -cy, 0f));
                UIKit.Tex(new Rect(cx - sp.size / 2f, cy - sp.size / 2f, sp.size, sp.size), tex, new Color(0.45f, 0.02f, 0.03f, a));
                GUI.matrix = keep;
            }
        }

        /// <summary>망자들이 모두 몰려오기 시작한 시각 (붉은 가장자리 한 번).</summary>
        public static float RushAt = -100f;

        // ---- 혼잣말 (자막만): 주인공이 중얼거린다 ----
        static string mutter;
        static float mutterAt = -100f;

        public static void Mutter(string text) { mutter = text; mutterAt = Time.unscaledTime; }

        void DrawMutter()
        {
            float age = Time.unscaledTime - mutterAt;
            if (mutter == null || age > 3.6f) return;
            float a = Mathf.Clamp01(age * 4f) * Mathf.Clamp01((3.6f - age) / 0.6f);
            float keep = UIKit.Alpha;
            UIKit.Alpha = keep * a;
            UIKit.Text(new Rect(0, H * 0.66f, W, 30), "“" + mutter + "”", 19, new Color(0.88f, 0.85f, 0.78f), TextAnchor.MiddleCenter, serif: true);
            UIKit.Alpha = keep;
        }

        bool lowHpMuttered;

        /// <summary>마지막으로 단계가 오른 시각 (금빛 연출).</summary>
        public static float LevelUpAt = -100f;

        void DrawLevelUp(PlayerCharacter p)
        {
            float age = Time.unscaledTime - LevelUpAt;
            if (age > 2.2f) return;
            float a = Mathf.Clamp01(1f - age / 2.2f);
            UIKit.Vignette(W, H, new Color(1f, 0.82f, 0.35f, 0.35f * a * a));
            float pop = 1f + Mathf.Max(0f, 0.25f - age) * 2f;
            var r = new Rect(W / 2 - 200, H - 205 - UIKit.EaseOut(age * 2f) * 24f, 400, 50); // 나침반 띠 위
            float keep = UIKit.Alpha;
            UIKit.Alpha = keep * a;
            UIKit.Text(r, $"단계 상승  Lv.{p.Level}", Mathf.RoundToInt(30 * pop), UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Alpha = keep;
        }

        /// <summary>치유 순간 화면 가장자리 옥빛 (0~1).</summary>
        public static float HealFlash;
        /// <summary>역병 침을 맞았을 때 화면 가장자리 누런 초록 (MonsterProjectile).</summary>
        public static float AcidFlash;
        /// <summary>회복제를 마신 순간 화면 가장자리 푸른빛 (0~1).</summary>
        public static float DrinkFlash;

        public static void OnPickup(HudIcons.Icon icon, Color color, string text, bool gained = true)
        {
            toasts.Add(new PickupToast { icon = icon, color = color, text = text, time = Time.unscaledTime });
            if (toasts.Count > 4) toasts.RemoveAt(0);
            if (!gained) return;
            if (icon == HudIcons.Icon.Kit) kitFlash = 1f;
            if (icon == HudIcons.Icon.Bottle) drinkFlash = 1f;
        }

        /// <summary>새 판 시작 시 지난 판 알림을 지운다.</summary>
        public static void ClearToasts() { toasts.Clear(); kitFlash = drinkFlash = 0f; splats.Clear(); HealFlash = 0f; AcidFlash = 0f; }

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
            TrackNight(gm, p);
            DrawNightBanner(gm);
            DrawLoreNote();
            if (gm.State == GameState.Playing && !gm.MenuOpen) NightTask.Draw(p, W);

            // ---- 화면 효과 ----
            float hpRatio = p.Hp / p.MaxHp;
            if (hpRatio < 0.35f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (hpRatio < 0.15f ? 7f : 4f));
                UIKit.Vignette(W, H, new Color(0.55f, 0f, 0f, (0.35f - hpRatio) * 2.2f * (0.6f + 0.4f * pulse)));
            }
            if (p.HurtFlash > 0f) UIKit.Vignette(W, H, new Color(0.8f, 0f, 0f, p.HurtFlash * 2.5f));
            DrawScreenBlood(dt);
            DrawMutter();
            // 생명이 처음 25% 아래로 떨어지면 한마디 (밤마다 한 번)
            if (gm.State == GameState.Playing && p.Hp / p.MaxHp < 0.25f && !lowHpMuttered)
            {
                lowHpMuttered = true;
                string[] lines = { "피가… 너무 많이 난다", "여기서 쓰러지면 안 돼", "키트… 키트가 있었지(X)", "숨이 넘어갈 것 같다" };
                Mutter(lines[Random.Range(0, lines.Length)]);
            }
            float rushAge = Time.unscaledTime - RushAt;
            if (rushAge < 1.4f) UIKit.Vignette(W, H, new Color(0.6f, 0f, 0f, 0.55f * (1f - rushAge / 1.4f) * (0.7f + 0.3f * Mathf.Sin(rushAge * 18f))));
            DrawLevelUp(p);
            // 치유 부적을 먹으면 가장자리가 잠깐 옥빛으로
            if (DrinkFlash > 0f)
            {
                UIKit.Vignette(W, H, new Color(0.3f, 0.6f, 1f, DrinkFlash * 0.5f));
                DrinkFlash = Mathf.Max(0f, DrinkFlash - dt * 1.6f);
            }
            if (HealFlash > 0f)
            {
                UIKit.Vignette(W, H, new Color(0.25f, 0.9f, 0.45f, HealFlash * 0.55f));
                HealFlash = Mathf.Max(0f, HealFlash - dt * 1.8f);
            }
            // 덤불 속에 숨어 있으면 화면 가장자리가 잎 그늘처럼 어둡고 푸르스름
            bool hiddenNow = !Flashlight.Active && p.Controller != null && p.Controller.IsCrouching && Hiding.InBush(p.transform.position);
            float leaf = UIKit.Smooth("hud.leaf", hiddenNow ? 1f : 0f, 4f);
            if (leaf > 0.01f) UIKit.Vignette(W, H, new Color(0.03f, 0.08f, 0.03f, 0.75f * leaf));
            if (AcidFlash > 0f)
            {
                UIKit.Vignette(W, H, new Color(0.5f, 0.85f, 0.1f, AcidFlash * 0.6f));
                AcidFlash = Mathf.Max(0f, AcidFlash - dt * 1.1f);
            }
            if (p.Infection.IsFrenzy)
            {
                UIKit.Vignette(W, H, new Color(0.35f, 0f, 0.5f, 0.85f));
                UIKit.Fill(new Rect(0, 0, W, H), new Color(0.25f, 0f, 0.3f, 0.12f + Mathf.PingPong(Time.time, 0.5f) * 0.15f));
            }

            DrawLensRain(gm, cam, dt);
            if (p.Weapons.IsScoped) DrawScope(p);
            if (gm.State != GameState.GameOver && !gm.MenuOpen) DrawCrosshair(p, cam);
            if (gm.State == GameState.Playing && !gm.MenuOpen) DrawAmmoWarning(p);
            if (p.Weapons.ChargingThrow)
            {
                // 던지기 힘 막대 (조준점 아래)
                var tr = new Rect(W / 2 - 80, H / 2 + 60, 160, 6);
                UIKit.Bar(tr, p.Weapons.ThrowCharge, Color.Lerp(UITheme.Gold, UITheme.BloodBright, p.Weapons.ThrowCharge));
                UIKit.Text(new Rect(tr.x - 40, tr.y - 22, tr.width + 80, 20), "힘 모으는 중 — 떼면 던진다", 13, UITheme.Bone, TextAnchor.MiddleCenter, serif: true);
            }
            if (!gm.MenuOpen && !GameInput.Mobile) DrawPickupPrompt();

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
            // 경험치는 툭 튀지 않고 차오른다 (레벨이 오르면 비웠다가 다시)
            float xpT = (float)p.Xp / p.XpToNext;
            float xpShown = UIKit.Smooth("hud.xp", xpT, 6f);
            if (xpShown > xpT + 0.3f) { UIKit.SnapSmooth("hud.xp", 0f); xpShown = 0f; }
            UIKit.Bar(xr, xpShown, UITheme.Gold);
            UIKit.Text(new Rect(xr.x, xr.y - 26, xr.width, 22),
                $"Lv.{p.Level}   ·   {p.Xp} / {p.XpToNext}" + (p.StatPoints > 0 || p.PendingTraitPicks > 0 ? $"   ·   <color=#{UIKit.Hex(UITheme.GoldBright)}>새 단련 {p.StatPoints}{(p.PendingTraitPicks > 0 ? " · 새 기질" : "")} — 서낭당에서</color>" : ""),
                14, UITheme.BoneDim, TextAnchor.MiddleCenter);

            DrawNoise(gm);
            if (gm.State == GameState.Playing) DrawCompass(cam);

            // ---- 위쪽: 밤 표식 / 남은 망자 ----
            DrawNightPlaque(gm);

            // ---- 오른쪽 위: 엽전 / 처치 ----
            UIKit.Text(new Rect(W - 330, 18, 300, 28), $"엽전 <color=#{UIKit.Hex(UITheme.GoldBright)}>{p.Gold}</color>     처치 <color=#{UIKit.Hex(UITheme.Bone)}>{p.Kills}</color>", 18, UITheme.BoneDim, TextAnchor.MiddleRight, serif: true);

            DrawBoss(dt);
            DrawIntro(gm, dt);
            DrawDamageDirections(p, cam);
            if (gm.State == GameState.Playing) DrawBehindWarning(p, cam);
            DrawStreak(p);
            DrawStragglers(cam, scale);
            DrawPickups(dt);
            DrawTargetTag(cam);
            DrawTurretTags(cam);
            DrawGrenadeWarnings(p, cam);
            DrawSpotted(cam);
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

        // ---- 화면에 맺히는 빗방울: 하늘을 올려다보면 맺히고 천천히 흘러내린다 ----
        struct Drop { public float x, y, r, life, max, slide; }
        readonly System.Collections.Generic.List<Drop> drops = new System.Collections.Generic.List<Drop>();
        float dropClock;

        void DrawLensRain(GameManager gm, Camera cam, float dt)
        {
            var atm = Atmosphere.Instance;
            if (cam == null || atm == null || gm.State != GameState.Playing || QualityManager.Emergency) { drops.Clear(); return; }
            float up = Mathf.Clamp01((-Mathf.DeltaAngle(0f, cam.transform.eulerAngles.x) - 5f) / 40f); // 위로 볼수록
            float rate = atm.RainIntensity * (0.4f + 4f * up); // 초당 맺히는 수
            if (dt > 0f)
            {
                dropClock += dt * rate;
                while (dropClock > 1f && drops.Count < 18)
                {
                    dropClock -= 1f;
                    float life = Random.Range(1.6f, 3.2f);
                    drops.Add(new Drop { x = Random.Range(0.05f, 0.95f) * W, y = Random.Range(0.05f, 0.8f) * H, r = Random.Range(10f, 26f), life = life, max = life, slide = Random.Range(8f, 40f) });
                }
                if (drops.Count >= 18) dropClock = 0f;
            }
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                if (dt > 0f) { d.life -= dt; d.y += d.slide * dt; }
                if (d.life <= 0f) { drops.RemoveAt(i); continue; }
                drops[i] = d;
                float a = Mathf.Clamp01(d.life / d.max * 1.5f) * 0.22f;
                // 투명한 물방울: 옅은 테두리 + 아래쪽 밝은 반사
                UIKit.Tex(new Rect(d.x - d.r, d.y - d.r, d.r * 2f, d.r * 2f), UITheme.Circle, new Color(0.75f, 0.8f, 0.9f, a * 0.5f));
                UIKit.Tex(new Rect(d.x - d.r * 0.35f, d.y + d.r * 0.2f, d.r * 0.7f, d.r * 0.45f), UITheme.Circle, new Color(1f, 1f, 1f, a));
            }
        }

        /// <summary>
        /// 아래 가운데 나침반 띠: 앞쪽 ±75°를 보여 준다. 북·동·남·서 + 보급 상자(노랑)·보스(대)·위치가 드러난 망자(붉은 점).
        /// </summary>
        void DrawCompass(Camera cam)
        {
            if (cam == null) return;
            const float span = 75f;
            // 오른쪽 무기 칸(W-702부터)과 겹치지 않게 폭을 줄인다 (좁은 화면비)
            float half = Mathf.Clamp(W / 2f - 712f, 140f, 260f);
            var r = new Rect(W / 2 - half, H - 160, half * 2f, 26);
            UIKit.Tex(r, UITheme.LineFade, new Color(0f, 0f, 0f, 0.5f));
            float yaw = cam.transform.eulerAngles.y;
            float X(float bearing) => r.center.x + Mathf.DeltaAngle(yaw, bearing) / span * (r.width / 2f);
            bool In(float bearing) => Mathf.Abs(Mathf.DeltaAngle(yaw, bearing)) < span;
            // 눈금 15°마다
            for (int a = 0; a < 360; a += 15)
            {
                if (!In(a)) continue;
                float x = X(a);
                float edge = 1f - Mathf.Abs(Mathf.DeltaAngle(yaw, a)) / span;
                bool major = a % 90 == 0;
                UIKit.Fill(new Rect(x - 0.5f, r.y + (major ? 2 : 8), 1f, major ? 10 : 5), new Color(0.9f, 0.86f, 0.78f, 0.5f * edge));
                if (major)
                {
                    string n = a == 0 ? "북" : a == 90 ? "동" : a == 180 ? "남" : "서";
                    UIKit.Text(new Rect(x - 20, r.y + 9, 40, 18), n, 14, new Color(0.95f, 0.9f, 0.8f, edge), TextAnchor.MiddleCenter, serif: true, bold: true);
                }
            }
            Vector3 me = cam.transform.position;
            float Bearing(Vector3 p) { Vector3 d = p - me; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
            // 보급 상자
            foreach (var c in SupplyCrate.Active)
            {
                if (c == null || !c.isActiveAndEnabled || c.Opened) continue;
                float b = Bearing(c.transform.position);
                float x = In(b) ? X(b) : (Mathf.DeltaAngle(yaw, b) > 0 ? r.xMax : r.x);
                UIKit.Fill(new Rect(x - 4, r.y - 6, 8, 8), new Color(1f, 0.8f, 0.3f, In(b) ? 0.95f : 0.4f));
            }
            // 지킴 밤: 돌무더기 방향
            var gmw = GameManager.Instance != null ? GameManager.Instance.Waves : null;
            if (gmw != null && gmw.Rule == RoundRule.Defense)
            {
                float b = Bearing(gmw.DefendPos);
                bool inside = In(b);
                float x = inside ? X(b) : (Mathf.DeltaAngle(yaw, b) > 0 ? r.xMax : r.x);
                UIKit.Text(new Rect(x - 14, r.y - 24, 28, 22), "지", 16, new Color(1f, 0.85f, 0.45f, inside ? 1f : 0.6f), TextAnchor.MiddleCenter, serif: true, bold: true);
            }
            // 드러난 망자·보스·현상금
            foreach (var m in Monster.Alive)
            {
                if (m == null || m.IsDead || !(m.Revealed || m.IsBoss || m.IsBounty)) continue;
                if (m.IsBounty && !m.Revealed)
                {
                    float bb = Bearing(m.transform.position);
                    bool ins = In(bb);
                    float bx = ins ? X(bb) : (Mathf.DeltaAngle(yaw, bb) > 0 ? r.xMax : r.x);
                    UIKit.Tex(new Rect(bx - 6, r.y - 8, 12, 12), UITheme.Circle, new Color(1f, 0.82f, 0.3f, ins ? 1f : 0.45f));
                    continue;
                }
                float b = Bearing(m.transform.position);
                bool inside = In(b);
                float x = inside ? X(b) : (Mathf.DeltaAngle(yaw, b) > 0 ? r.xMax : r.x);
                if (m.IsBoss)
                    UIKit.Text(new Rect(x - 14, r.y - 24, 28, 22), "대", 17, new Color(1f, 0.3f, 0.25f, inside ? 1f : 0.6f), TextAnchor.MiddleCenter, serif: true, bold: true);
                else
                    UIKit.Tex(new Rect(x - 4, r.y - 6, 8, 8), UITheme.Circle, new Color(0.9f, 0.15f, 0.1f, inside ? 0.9f : 0.4f));
            }
            // 가운데 표시
            UIKit.Fill(new Rect(r.center.x - 1, r.y - 2, 2, r.height + 4), new Color(1f, 0.9f, 0.7f, 0.7f));
        }

        /// <summary>망자가 나를 알아챈 순간 머리 위에 붉은 '!' (1.2초) — 들켰다는 걸 바로 알게.</summary>
        void DrawSpotted(Camera cam)
        {
            if (cam == null || GlobalRushActive()) return;
            float k = W / Screen.width;
            foreach (var m in Monster.Alive)
            {
                if (m == null || m.IsDead || m.IsBoss) continue;
                float age = Time.time - m.SpottedAt;
                bool lost = false;
                if (age > 1.2f || age < 0f)
                {
                    age = Time.time - m.LostAt; lost = true; // 놓친 망자: 옅은 '?'
                    if (age > 1.2f || age < 0f || m.IsAlerted) continue;
                }
                Vector3 wp = m.transform.position + Vector3.up * (2.2f * m.Data.scale);
                if ((wp - cam.transform.position).sqrMagnitude > 35f * 35f) continue;
                Vector3 sp = cam.WorldToScreenPoint(wp);
                if (sp.z <= 0f) continue;
                float a = Mathf.Clamp01((1.2f - age) / 0.4f);
                float pop = 1f + Mathf.Max(0f, 0.15f - age) * 3f;
                var c = new Vector2(sp.x * k, (Screen.height - sp.y) * k - age * 10f);
                UIKit.Text(new Rect(c.x - 20, c.y - 30, 40, 40), lost ? "?" : "!", Mathf.RoundToInt(30 * pop), lost ? new Color(0.9f, 0.85f, 0.6f, a * 0.85f) : new Color(1f, 0.3f, 0.2f, a), TextAnchor.MiddleCenter, bold: true);
            }
        }

        static bool GlobalRushActive() => Monster.GlobalRush; // 모두 몰려올 땐 너무 많아 생략

        /// <summary>내 발 근처(7m)에 터지기 직전 수류탄이 있으면 그 위에 붉은 '!' — 화면 밖이면 가장자리에.</summary>
        void DrawGrenadeWarnings(PlayerCharacter p, Camera cam)
        {
            if (cam == null || ThrownGrenade.Live.Count == 0) return;
            float k = W / Screen.width;
            foreach (var g in ThrownGrenade.Live)
            {
                if (g == null || g.Fuse > 1.8f) continue; // 막 던진 건 날아가는 중이라 표시하지 않는다
                float dist = Vector3.Distance(g.transform.position, p.transform.position);
                if (dist > 7f) continue;
                Vector3 sp = cam.WorldToScreenPoint(g.transform.position + Vector3.up * 0.3f);
                bool behind = sp.z < 0f;
                float x = sp.x * k, y = (Screen.height - sp.y) * k;
                if (behind) { x = W - x; y = H - 80f; }
                x = Mathf.Clamp(x, 50f, W - 50f); y = Mathf.Clamp(y, 50f, H - 50f);
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * (g.Fuse < 1f ? 22f : 12f));
                UIKit.Tex(new Rect(x - 26, y - 26, 52, 52), UITheme.Circle, new Color(0f, 0f, 0f, 0.55f));
                UIKit.Text(new Rect(x - 26, y - 26, 52, 52), "!", 34, new Color(1f, 0.3f, 0.2f, pulse), TextAnchor.MiddleCenter, bold: true);
                UIKit.Text(new Rect(x - 60, y + 24, 120, 20), $"수류탄 {dist:0}m", 13, new Color(1f, 0.8f, 0.7f, pulse), TextAnchor.MiddleCenter);
            }
        }

        /// <summary>깔아 둔 기관총 머리 위: 남은 시간 막대 + 남은 탄 (30m 안, 화면에 보일 때만).</summary>
        void DrawTurretTags(Camera cam)
        {
            if (cam == null || SentryTurret.Active.Count == 0) return;
            float k = W / Screen.width;
            foreach (var t in SentryTurret.Active)
            {
                if (t == null) continue;
                Vector3 wp = t.transform.position + Vector3.up * 1.15f;
                float dist = Vector3.Distance(cam.transform.position, wp);
                if (dist > 30f) continue;
                Vector3 sp = cam.WorldToScreenPoint(wp);
                if (sp.z <= 0f) continue;
                var c = new Vector2(sp.x * k, (Screen.height - sp.y) * k);
                float a = Mathf.Clamp01((30f - dist) / 6f);
                float lifeT = Mathf.Clamp01(t.Life / SentryTurret.MaxLife);
                var br = new Rect(c.x - 40, c.y, 80, 5);
                UIKit.Fill(new Rect(br.x - 1, br.y - 1, br.width + 2, br.height + 2), new Color(0f, 0f, 0f, 0.7f * a));
                UIKit.Fill(new Rect(br.x, br.y, br.width * lifeT, br.height), new Color(0.55f, 0.9f, 0.55f, a));
                UIKit.Text(new Rect(c.x - 70, c.y - 22, 140, 20), $"기관총  {t.Life:0}초  ·  탄 {t.Ammo}", 13, new Color(0.9f, 0.87f, 0.8f, a), TextAnchor.MiddleCenter, serif: true);
            }
        }

        bool aimOnMonster; // 조준점이 망자 위에 있나 (조준선이 붉게)
        readonly System.Collections.Generic.HashSet<string> seenThisRun = new System.Collections.Generic.HashSet<string>();

        /// <summary>처음 보는 망자 유형을 조준하면 한 번 이름과 특징을 알려 준다 (판이 바뀌어도 기억).</summary>
        void FirstSight(Monster m)
        {
            string id = m.Data.id;
            if (string.IsNullOrEmpty(id) || !id.StartsWith("z_") || !seenThisRun.Add(id)) return;
            string key = "hunt.seen." + id;
            try { if (PlayerPrefs.GetInt(key, 0) == 1) return; PlayerPrefs.SetInt(key, 1); } catch { return; }
            foreach (var k in DefaultContent.ZombieKinds)
                if ("z_" + k.id == id)
                {
                    if (k.id == "basic") return;
                    HUD.Notify($"처음 보는 망자 — {k.name}  ·  {k.desc}", new Color(0.95f, 0.8f, 0.55f));
                    return;
                }
        }

        void DrawTargetTag(Camera cam)
        {
            if (cam == null) return;
            if (UIKit.IsRepaint)
            {
                var ray = new Ray(cam.transform.position, cam.transform.forward);
                aimOnMonster = false;
                if (Physics.Raycast(ray, out var hit, 40f, ~((1 << 2) | (1 << 1)), QueryTriggerInteraction.Ignore))
                {
                    var part = hit.collider.GetComponent<MonsterPart>();
                    if (part != null && part.owner != null && !part.owner.IsDead)
                    {
                        tagTarget = part.owner; tagTime = 1.2f; aimOnMonster = true;
                        FirstSight(part.owner);
                    }
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
            UIKit.Text(new Rect(r.x - 40, r.y, r.width + 80, 18), (m.IsBoss ? BossName(GameManager.Instance != null && GameManager.Instance.Waves != null && GameManager.Instance.Waves.IsBossWave) : m.Data.displayName), 15, new Color(0.9f, 0.87f, 0.8f, a), TextAnchor.MiddleCenter, serif: true, bold: true);
            var br = new Rect(r.x + 20, r.y + 21, r.width - 40, 6);
            UIKit.Fill(new Rect(br.x - 1, br.y - 1, br.width + 2, br.height + 2), new Color(0f, 0f, 0f, 0.7f * a));
            UIKit.Fill(new Rect(br.x, br.y, br.width * Mathf.Clamp01(m.Hp / m.MaxHp), br.height), new Color(0.75f, 0.1f, 0.08f, a));
            // 알아챘나: 어슬렁(등 뒤로 다가가 칼로 한 번에) / 쫓는 중
            if (!m.IsBoss)
                UIKit.Text(new Rect(r.x - 40, r.y + 28, r.width + 80, 16), m.Crippled ? "절뚝임 — V 칼로 마무리" : m.IsAlerted ? "쫓는 중" : "어슬렁 — 눈치 못 챔",
                    12, m.Crippled ? new Color(1f, 0.75f, 0.4f, a * 0.95f) : m.IsAlerted ? new Color(1f, 0.45f, 0.35f, a * 0.9f) : new Color(0.7f, 0.8f, 0.95f, a * 0.9f), TextAnchor.MiddleCenter, serif: true);
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

        /// <summary>
        /// 등 뒤 경고: 시야 밖(앞쪽 70° 밖) 4.5m 안에 망자가 다가오면 그쪽 가장자리가 옅게 붉어진다 — 물리기 전에 돌아볼 수 있게.
        /// </summary>
        void DrawBehindWarning(PlayerCharacter p, Camera cam)
        {
            if (cam == null || !UIKit.IsRepaint) return;
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) return;
            foreach (var m in Monster.Alive)
            {
                if (m == null || m.IsDead || !m.IsAlerted) continue;
                Vector3 d = m.transform.position - p.transform.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist > 4.5f || dist < 0.01f) continue;
                float ang = Vector3.SignedAngle(fwd, d, Vector3.up);
                if (Mathf.Abs(ang) < 70f) continue; // 보이는 놈은 표시하지 않는다
                float a = ang * Mathf.Deg2Rad;
                var pos = new Vector2(W / 2f + Mathf.Sin(a) * W * 0.46f, H / 2f - Mathf.Cos(a) * H * 0.44f);
                float k = Mathf.Clamp01((4.5f - dist) / 3f);
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f);
                UIKit.Tex(new Rect(pos.x - 170, pos.y - 170, 340, 340), UITheme.Glow, new Color(0.85f, 0.04f, 0.02f, 0.45f * k * pulse));
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
            // 길게 이어지면 이름이 붙는다
            string title = p.KillStreak >= 12 ? "저승사자" : p.KillStreak >= 8 ? "아수라" : p.KillStreak >= 5 ? "피바람" : null;
            if (title != null)
                UIKit.Text(new Rect(0, H * 0.27f + size * 0.75f, W, 30), title, 22, new Color(1f, 0.85f, 0.6f, alpha), TextAnchor.MiddleCenter, serif: true, bold: true);
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
                if (m.IsBoss)
                {
                    // 보스는 크게 '대'로, 거리도 항상 (화면 밖이어도 어느 쪽인지 바로 알게)
                    float sz = 56f + 6f * pulse;
                    UIKit.Tex(new Rect(x - sz, y - sz, sz * 2f, sz * 2f), UITheme.Glow, new Color(0.8f, 0.05f, 0.05f, 0.35f * pulse));
                    UIKit.Emblem(new Rect(x - sz / 2f, y - sz / 2f, sz, sz), "대", UITheme.BloodBright, true);
                    float d = Vector3.Distance(cam.transform.position, m.transform.position);
                    UIKit.Text(new Rect(x - 50, y + sz / 2f, 100, 22), $"{d:0}m", 16, new Color(1f, 0.85f, 0.75f, 1f), TextAnchor.MiddleCenter, bold: true);
                    continue;
                }
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
                "총성·달리기 소리에 망자가 모인다 · 가로등 불빛도 눈에 띈다\n" +
                "<b>E</b>   줍기 · 보급 상자(노란 빛기둥) 열기\n" +
                "<b>M</b>   마을 지도 · 아래 나침반에 상자·보스 방향\n" +
                "<b>V</b>   칼 — 못 알아챈 망자 등 뒤면 한 번에\n" +
                "<b>5</b>   특수 기기 (<b>G</b> 수류탄 · 누르고 있으면 멀리)\n" +
                "<b>X</b> 치료 키트 · <b>C</b> 회복제 · <b>H</b> 청심환\n" +
                "머리를 맞혀 쓰러뜨려야 다시 못 일어난다\n" +
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
            // 무기를 바꾸면 카드 내용이 아래에서 살짝 올라오며 바뀐다
            object cur = wp.Mode == PlayerWeapons.Hand.Gun ? (object)w : wp.Mode;
            if (!ReferenceEquals(cur, lastCardItem) && !(cur is PlayerWeapons.Hand h0 && lastCardItem is PlayerWeapons.Hand h1 && h0 == h1))
            {
                lastCardItem = cur;
                cardAppear = 0f;
            }
            if (UIKit.IsRepaint) cardAppear = Mathf.MoveTowards(cardAppear, 1f, Time.unscaledDeltaTime * 6f);
            // 쏠 때마다 탄 숫자가 톡 튄다
            if (w != null && wp.Mode == PlayerWeapons.Hand.Gun)
            {
                if (w.ammoInMag < lastAmmo && ReferenceEquals(w, lastAmmoWeapon)) ammoPunch = 1f;
                lastAmmo = w.ammoInMag;
                lastAmmoWeapon = w;
            }
            if (UIKit.IsRepaint) ammoPunch = Mathf.MoveTowards(ammoPunch, 0f, Time.unscaledDeltaTime * 9f);
            float ce = UIKit.EaseOut(cardAppear);
            var card = new Rect(right - 440, H - 214, 440, 166);
            UIKit.NineSlice(card, UITheme.Slot, 10, new Color(1, 1, 1, 0.85f));
            float keepAlpha = UIKit.Alpha;
            cardBaseAlpha = keepAlpha;
            UIKit.Alpha = keepAlpha * Mathf.Lerp(0.2f, 1f, ce);
            card.y += (1f - ce) * 10f;
            try { DrawWeaponCard(p, wp, w, card); }
            finally { UIKit.Alpha = keepAlpha; }
        }

        object lastCardItem;
        float cardAppear = 1f, ammoPunch;
        int lastAmmo;
        object lastAmmoWeapon;

        float cardBaseAlpha = 1f;

        void DrawWeaponCard(PlayerCharacter p, PlayerWeapons wp, WeaponInstance w, Rect card)
        {
            float right = W - 262;
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
                    (w.Mastery > 0 ? $"  ·  <color=#{UIKit.Hex(UITheme.GoldBright)}>{WeaponInstance.MasteryName(w.Mastery)}</color>" : "") +
                    (p.Controller.WeightPenalty < 1f ? $"  ·  <color=#{UIKit.Hex(UITheme.Warn)}>과적</color>" : ""),
                    14, UITheme.BoneDim, TextAnchor.MiddleRight);

                bool low = w.ammoInMag <= w.MagazineSize * 0.25f;
                Color ac = w.ammoInMag == 0 ? UITheme.BloodBright : low ? UITheme.Warn : UITheme.Bone;
                string reload = wp.IsReloading ? $"<size=18><color=#{UIKit.Hex(UITheme.Gold)}>{(w.ammoInMag == 0 ? "빈 탄창 교체" : "탄창 교체")}</color></size>   " : "";
                UIKit.Text(new Rect(card.x + 14, card.y + 66, card.width - 28, 62),
                    $"{reload}<size={54 + Mathf.RoundToInt(ammoPunch * 6f)}><b>{w.ammoInMag}</b></size><size=22><color=#{UIKit.Hex(UITheme.BoneDim)}>  /  {w.reserve}</color></size>", 54, ac, TextAnchor.MiddleRight, serif: true);
                if (wp.IsReloading) UIKit.Bar(new Rect(card.x + 20, card.y + 96, 170, 6), wp.ReloadProgress, UITheme.Gold);
                else if (w.KillsToNextMastery > 0)
                {
                    // 숙련 진행: 다음 단계까지 얼마나 남았나 (가는 금빛 선)
                    int prevStep = w.Mastery > 0 ? WeaponInstance.MasterySteps[w.Mastery - 1] : 0;
                    int nextStep = WeaponInstance.MasterySteps[w.Mastery];
                    float mt = Mathf.InverseLerp(prevStep, nextStep, w.kills);
                    var mr = new Rect(card.x + 20, card.y + 98, 170, 3);
                    UIKit.Fill(mr, new Color(0f, 0f, 0f, 0.5f));
                    UIKit.Fill(new Rect(mr.x, mr.y, mr.width * mt, mr.height), new Color(0.95f, 0.8f, 0.45f, 0.8f));
                    UIKit.TooltipIf(new Rect(mr.x, mr.y - 6, mr.width, 14), $"숙련: 다음 단계까지 {w.KillsToNextMastery}마리");
                }

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

            UIKit.Alpha = cardBaseAlpha; // 슬롯 줄은 전환 효과 없이 그대로
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
            float sightK = Atmosphere.SightMult;
            if (sightK < 0.8f) rain += "  ·  안개(덜 보임)";
            else if (sightK > 1.05f) rain += "  ·  달빛(잘 보임)";
            string light = Flashlight.Disabled ? "  ·  손전등 금지" : Flashlight.Active ? $"  ·  <color=#{UIKit.Hex(UITheme.GoldBright)}>손전등 켜짐 — 망자가 빛을 본다</color>" : "  ·  손전등 꺼짐 (F)";
            UIKit.Text(new Rect(r.x - 160, r.y - 22, r.width + 320, 20), "소 리" + rain + light, 13, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            // 둘째 줄: 가로등 불빛 · 쫓는 망자 수 · 부적 (있을 때만)
            string st = "";
            if (!Flashlight.Active && ShootableLamp.PlayerLit()) st += $"<color=#{UIKit.Hex(UITheme.GoldBright)}>가로등 불빛 아래 — 눈에 띈다</color>";
            var hp0 = gm.Player;
            if (hp0 != null && !Flashlight.Active && hp0.Controller != null && hp0.Controller.IsCrouching && Hiding.InBush(hp0.transform.position))
                st += (st.Length > 0 ? "  ·  " : "") + $"<color=#{UIKit.Hex(new Color(0.6f, 0.85f, 0.55f))}>덤불 속 — 숨었다</color>";
            int chasing = 0;
            foreach (var cm in Monster.Alive) if (cm != null && !cm.IsDead && cm.IsAlerted) chasing++;
            if (chasing > 0) st += (st.Length > 0 ? "  ·  " : "") + $"<color=#{UIKit.Hex(chasing >= 6 ? UITheme.BloodBright : UITheme.Warn)}>쫓는 망자 {chasing}</color>";
            var pc = gm.Player;
            if (pc != null && (pc.CharmGuard || pc.CharmSwift || pc.CharmAim))
                st += (st.Length > 0 ? "  ·  " : "") + $"<color=#{UIKit.Hex(new Color(0.95f, 0.85f, 0.6f))}>부적: {(pc.CharmGuard ? "호신 " : "")}{(pc.CharmSwift ? "축지 " : "")}{(pc.CharmAim ? "명중" : "")}</color>";
            if (st.Length > 0) UIKit.Text(new Rect(r.x - 160, r.y - 42, r.width + 320, 20), st, 13, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
        }

        // ---- 새벽 기록 카드: 그 밤에 한 일을 날이 밝을 때 잠깐 보여 준다 ----
        GameState nightLastState = GameState.CharacterSelect;
        int nightKills0, nightGold0, nightLevel0;
        float nightDmg0, nightStart;
        string[] dawnLines;
        bool dawnFinal;
        int dawnWave;
        PlayerCharacter nightPlayer;
        float dawnShown = -100f;

        void TrackNight(GameManager gm, PlayerCharacter p)
        {
            if (!ReferenceEquals(p, nightPlayer)) { nightPlayer = p; nightLastState = GameState.CharacterSelect; dawnLines = null; } // 새 판
            if (gm.State == nightLastState) return;
            if (gm.State == GameState.Playing)
            {
                nightKills0 = p.Kills; nightGold0 = p.Gold; nightLevel0 = p.Level;
                nightDmg0 = p.DamageTaken; nightStart = Time.time;
                p.BestStreak = 0;
                p.BountyThisNight = false;
                p.NightStartDamage = p.DamageTaken;
                NightTask.Begin(p, gm.Waves != null ? gm.Waves.Wave : 1);
                lowHpMuttered = false;
                string[] nightLines = { "또 밤이 왔다", "비가 그치질 않는구나", "오늘 밤만 넘기자", "고분 쪽에서 또 소리가 난다" };
                Mutter(nightLines[Random.Range(0, nightLines.Length)]);
                dawnShown = -100f;
                nightBanner = Time.unscaledTime; // 밤이 시작될 때 큰 제목
            }
            else if (gm.State == GameState.Intermission && nightLastState == GameState.Playing)
            {
                float secs = Time.time - nightStart;
                int lv = p.Level - nightLevel0;
                dawnFinal = gm.Waves != null && gm.Waves.IsBossWave && !gm.Waves.Failed;
                dawnWave = gm.Waves != null ? gm.Waves.Wave : 0;
                dawnLines = new[]
                {
                    $"쓰러뜨린 망자  <b>{p.Kills - nightKills0}</b>",
                    $"벌어들인 엽전  <b>{Mathf.Max(0, p.Gold - nightGold0)}</b>",
                    $"버틴 시간  <b>{(int)(secs / 60)}분 {(int)(secs % 60):00}초</b>",
                    $"받은 상처  <b>{p.DamageTaken - nightDmg0:0}</b>",
                    p.BestStreak >= 3 ? $"가장 긴 연속 처치  <b>{p.BestStreak}</b>" : null,
                    lv > 0 ? $"단계 상승  <b>+{lv}</b>" : null,
                    $"현상금 망자  <b>{(p.BountyThisNight ? "잡았다" : "놓쳤다")}</b>",
                    NightTask.Summary,
                    SupplyCrate.PlacedThisNight > 0 ? $"연 보급 상자  <b>{SupplyCrate.OpenedThisNight} / {SupplyCrate.PlacedThisNight}</b>" : null,
                };
                dawnShown = Time.unscaledTime;
                NightTask.End();
                string[] dawnLines2 = { "살았다…", "날이 밝는다", "서낭당에서 숨 좀 돌리자" };
                Mutter(dawnLines2[Random.Range(0, dawnLines2.Length)]);
            }
            nightLastState = gm.State;
        }

        float nightBanner = -100f;

        /// <summary>밤이 시작되면 화면 가운데 위쪽에 붓글씨로 '셋째 밤' + 이번 밤 룰·목표·변고 (3.5초).</summary>
        void DrawNightBanner(GameManager gm)
        {
            float age = Time.unscaledTime - nightBanner;
            if (age > 4f || gm.State != GameState.Playing || gm.Waves == null) return;
            float a = UIKit.EaseOut(age * 2.5f) * Mathf.Clamp01((4f - age) / 0.9f);
            float keep = UIKit.Alpha;
            UIKit.Alpha = keep * a;
            var w = gm.Waves;
            float y = H * 0.24f + (1f - UIKit.EaseOut(age * 2f)) * 14f;
            bool final = w.IsBossWave;
            UIKit.BrushTitle(new Rect(W / 2 - 340, y, 680, 104), NightName(w.Wave) + (final ? "  —  대감의 밤" : ""), final ? 58 : 64,
                new Color(0.03f, 0.02f, 0.02f, 0.88f), final ? UITheme.BloodBright : UITheme.Bone);
            string wx = Atmosphere.Instance != null ? Atmosphere.WeatherName(Atmosphere.Instance.Current) + "  ·  " : "";
            string sub = $"{wx}{WaveManager.RuleName(w.Rule)}  ·  {WaveManager.RuleGoal(w.Rule)}";
            UIKit.Text(new Rect(0, y + 106, W, 28), sub, 20, UITheme.Bone, TextAnchor.MiddleCenter, serif: true, bold: true);
            if (w.Modifiers.Count > 0)
            {
                string mods = "변고: ";
                for (int i = 0; i < w.Modifiers.Count; i++) mods += (i > 0 ? " · " : "") + WaveManager.ModifierName(w.Modifiers[i]);
                UIKit.Text(new Rect(0, y + 136, W, 24), mods, 17, UITheme.BloodBright, TextAnchor.MiddleCenter, serif: true);
            }
            UIKit.Alpha = keep;
        }

        const float DawnLife = 9f;
        bool DawnActive(GameManager gm) => dawnLines != null && gm.State == GameState.Intermission && Time.unscaledTime - dawnShown < DawnLife;

        /// <summary>새벽 카드가 떠 있으면 클릭으로 닫는다 (서낭당 버튼이 같이 눌리지 않게 먼저 호출).</summary>
        public void DawnInput(GameManager gm)
        {
            if (!DawnActive(gm)) return;
            var e = Event.current;
            float age = Time.unscaledTime - dawnShown;
            if (age < 0.5f) { if (e.type == EventType.MouseDown) e.Use(); return; } // 막 떴을 때 잘못 눌러 닫히지 않게
            if (e.type == EventType.MouseDown) // Enter는 '밤을 맞는다'라서 쓰지 않는다
            {
                dawnShown = Time.unscaledTime - DawnLife + 0.35f; // 0.35초 동안 사라진다
                e.Use();
            }
        }

        /// <summary>날이 밝으면 서낭당 위에 '지난 밤의 기록'을 띄운다 (9초 또는 클릭하면 닫힘).</summary>
        public void DrawDawnCard(GameManager gm)
        {
            if (!DawnActive(gm)) return;
            float age = Time.unscaledTime - dawnShown;
            float a = UIKit.EaseOut(age * 3f) * Mathf.Clamp01((DawnLife - age) / 0.35f);
            int n = 0;
            foreach (var l in dawnLines) if (l != null) n++;
            float extra = dawnFinal ? 96f : 0f;
            var r = new Rect(W / 2 - 260, H / 2 - (110 + n * 30 + extra) / 2 + (1f - a) * 16f, 520, 110 + n * 30 + extra);
            float keep = UIKit.Alpha;
            UIKit.Alpha = keep * a;
            UIKit.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.45f));
            UIKit.Panel(r);
            float y = r.y + 16;
            if (dawnFinal)
            {
                UIKit.BrushTitle(new Rect(r.x + 20, y, r.width - 40, 60), "마을을 지켜냈다", 40, new Color(0.03f, 0.02f, 0.02f, 0.9f), UITheme.GoldBright);
                UIKit.Text(new Rect(r.x, y + 60, r.width, 26), dawnWave <= WaveManager.BossEvery ? "역병 대감이 쓰러졌다 — 이제 끝없는 밤이 이어진다"
                    : dawnWave == WaveManager.BossEvery * 2 ? "고분이 다시 닫혔다 — 장마 뒤 첫 맑은 아침이 온다" : "역병 대감이 또 쓰러졌다", 17, UITheme.Bone, TextAnchor.MiddleCenter, serif: true);
                // 붉은 낙관
                var seal = new Rect(r.xMax - 70, r.y + 14, 50, 50);
                UIKit.Tex(seal, UITheme.Seal, UITheme.Blood);
                UIKit.Text(seal, "승", 24, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
                y += extra;
            }
            UIKit.Text(new Rect(r.x, y, r.width, 30), $"{NightName(dawnWave)}의 기록", 22, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
            y += 40;
            int i = 0;
            foreach (var l in dawnLines)
            {
                if (l == null) continue;
                float la = UIKit.EaseOut((age - 0.25f - i * 0.12f) * 4f); // 한 줄씩 차례로 떠오른다
                UIKit.Alpha = keep * a * la;
                UIKit.Text(new Rect(r.x + 40 + (1f - la) * 10f, y, r.width - 80, 28), l, 18, UITheme.Bone, TextAnchor.MiddleCenter, serif: true);
                y += 30; i++;
            }
            UIKit.Alpha = keep * a * 0.8f;
            UIKit.Text(new Rect(r.x, r.yMax - 34, r.width, 22), "클릭하면 서낭당으로", 14, UITheme.Ash, TextAnchor.MiddleCenter, serif: true);
            UIKit.Alpha = keep;
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
                if (!waves.IsBossWave)
                {
                    int left = WaveManager.BossEvery - waves.Wave % WaveManager.BossEvery;
                    sub += $"    ·    <color=#{UIKit.Hex(UITheme.BloodBright)}>대감의 밤까지 {left}밤</color>";
                }
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
            if (boss == null) { bossTrail = 1f; lastBoss = null; return; }

            var gm = GameManager.Instance;
            bool finalBoss = gm != null && gm.Waves != null && gm.Waves.IsBossWave;
            string bossName = BossName(finalBoss) + (boss.BossAffix > 0 ? " — " + Monster.AffixName(boss.BossAffix) : "");
            if (!ReferenceEquals(boss, lastBoss))
            {
                // 보스 등장: 화면 가운데 큰 제목 + 흔들림
                lastBoss = boss;
                bossIntro = Time.unscaledTime;
                Mutter(finalBoss ? "저게… 할아버지가 말한 대감인가" : "저놈이 이 밤의 우두머리다");
                // 징은 단계가 바뀔 때(WaveManager) 이미 울린다 — 여기선 흔들림만
                if (gm != null && gm.Player != null) gm.Player.Controller.AddShake(finalBoss ? 3f : 1.5f);
            }
            DrawBossIntro(bossName, finalBoss);

            float t = boss.Hp / boss.MaxHp;
            bossTrail = Mathf.Max(t, Mathf.MoveTowards(bossTrail, t, dt * 0.25f));
            var r = new Rect(W / 2 - 430, 128, 860, 22);
            UIKit.Text(new Rect(r.x, r.y - 34, r.width, 30), bossName, 24, UITheme.BloodBright, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Bar(r, t, new Color(0.6f, 0.05f, 0.1f), null, bossTrail);
            for (int i = 1; i <= 2; i++)
                UIKit.Tex(new Rect(r.x + r.width * (i / 3f) - 7, r.y + 4, 14, 14), UITheme.Diamond, i <= boss.BossPhase ? UITheme.Ash : UITheme.GoldBright);
            for (int i = 0; i < 3; i++)
                UIKit.Tex(new Rect(r.center.x - 40 + i * 30, r.yMax + 8, 16, 16), i <= boss.BossPhase ? UITheme.Diamond : UITheme.DiamondHollow, UITheme.BloodBright);
        }

        /// <summary>발밑 보급품 안내: [E] 줍기 / 가득 차서 못 줍는 이유.</summary>
        void DrawPickupPrompt()
        {
            bool near = Pickup.NearestInfo(out string name, out bool can);
            if (!near && SupplyCrate.NearestToPlayer() != null) { near = true; name = "보급 상자 열기"; can = true; }
            if (!near && Jangseung.NearestToPlayer() != null) { near = true; name = "장승에게 절하기"; can = true; }
            if (!near && SentryTurret.NearestToPlayer() != null) { near = true; name = "기관총 거두기"; can = true; }
            var siren = SirenTower.NearPlayer();
            if (!near && siren != null) { near = true; name = siren.UsedThisNight ? "사이렌 (이번 밤은 이미 울렸다)" : "사이렌 울리기 — 망자를 이쪽으로 꾄다"; can = true; }
            float a = UIKit.Smooth("hud.pickprompt", near ? 1f : 0f, 12f);
            if (a < 0.02f) return;
            if (near) { lastPromptName = name; lastPromptCan = can; }
            float keep = UIKit.Alpha;
            UIKit.Alpha = keep * a;
            var r = new Rect(W / 2 - 200, H / 2 + 92, 400, 34);
            UIKit.Tex(r, UITheme.LineFade, new Color(0f, 0f, 0f, 0.6f));
            string text = lastPromptCan
                ? (lastPromptName == "보급 상자 열기" || lastPromptName == "장승에게 절하기" || lastPromptName == "기관총 거두기" || lastPromptName.StartsWith("사이렌") ? $"<color=#{UIKit.Hex(UITheme.GoldBright)}><b>[E]</b></color>  {lastPromptName}"
                   : $"<color=#{UIKit.Hex(UITheme.GoldBright)}><b>[E]</b></color>  줍기  ·  {lastPromptName}")
                : $"{lastPromptName}  ·  <color=#{UIKit.Hex(UITheme.Ash)}>{(lastPromptName == Pickup.NameOf(PickupKind.Rune) ? "생명이 가득해 못 줍는다" : "가득 차서 못 줍는다")}</color>";
            UIKit.Text(r, text, 18, UITheme.Bone, TextAnchor.MiddleCenter, serif: true);
            UIKit.Alpha = keep;
        }

        string lastPromptName;
        bool lastPromptCan;

        Monster lastBoss;
        float bossIntro = -100f;

        /// <summary>매 밤 나오는 약한 보스와 다섯째 밤의 최종 보스(대감)를 이름으로 구분.</summary>
        public static string BossName(bool finalBoss) => finalBoss ? "역병 대감" : "대감의 그림자";

        void DrawBossIntro(string name, bool finalBoss)
        {
            float age = Time.unscaledTime - bossIntro;
            if (age > 4f) return;
            float a = UIKit.EaseOut(age * 3f) * Mathf.Clamp01((4f - age) / 0.8f);
            float keep = UIKit.Alpha;
            UIKit.Alpha = keep * a;
            float y = H * 0.3f - (1f - UIKit.EaseOut(age * 2.5f)) * 20f;
            UIKit.BrushTitle(new Rect(W / 2 - 360, y, 720, 110), name, finalBoss ? 72 : 60, new Color(0.03f, 0.02f, 0.02f, 0.9f), UITheme.BloodBright);
            var bm = lastBoss;
            string sub = finalBoss ? "최종 보스  ·  쓰러뜨리면 마을을 지켜낸다"
                : "보스  ·  쓰러뜨리면 이 밤이 끝난다" + (bm != null && bm.BossAffix > 0 ? "  ·  " + Monster.AffixDesc(bm.BossAffix) : "  ·  위치가 표시된다");
            UIKit.Text(new Rect(0, y + 112, W, 30), sub, 20, UITheme.Bone, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Alpha = keep;
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
                gap = UIKit.Smooth("hud.crossgap", gap, 22f); // 쏘거나 조준할 때 조준선이 툭 튀지 않고 벌어졌다 모인다
                // 망자 위에 조준점이 올라가면 조준선이 붉게 물든다
                float onM = UIKit.Smooth("hud.aimon", aimOnMonster ? 1f : 0f, 16f);
                Color c = Color.Lerp(new Color(0.95f, 0.88f, 0.75f, alpha), new Color(1f, 0.32f, 0.25f, alpha), onM);
                Color sh = new Color(0, 0, 0, alpha * 0.6f);
                int style = GameSettings.Crosshair;
                if (style == 0)
                {
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
                else if (style == 1)
                {
                    // 점: 가운데 점 하나 (탄퍼짐은 점 둘레의 옅은 원으로)
                    UIKit.Tex(new Rect(cx - 5, cy - 5, 10, 10), UITheme.Diamond, sh);
                    UIKit.Tex(new Rect(cx - 3.5f, cy - 3.5f, 7, 7), UITheme.Diamond, c);
                    float rr = gap + 4f;
                    UIKit.Tex(new Rect(cx - rr, cy - rr, rr * 2f, rr * 2f), UITheme.DiamondHollow, new Color(c.r, c.g, c.b, c.a * 0.25f));
                }
                else
                {
                    // 원: 탄퍼짐 크기의 고리 + 가운데 점
                    float rr = gap + 8f;
                    UIKit.Tex(new Rect(cx - rr - 1, cy - rr - 1, rr * 2f + 2, rr * 2f + 2), UITheme.DiamondHollow, sh);
                    UIKit.Tex(new Rect(cx - rr, cy - rr, rr * 2f, rr * 2f), UITheme.DiamondHollow, c);
                    UIKit.Tex(new Rect(cx - 2.5f, cy - 2.5f, 5, 5), UITheme.Diamond, c);
                }
            }

            if (p.Weapons.HitMarker > 0f)
            {
                // X자 짧은 선 4개: 맞히면 흰색, 급소 주황, 처치는 붉고 크게 — 처음 크게 튀었다가 오므라들며 사라진다
                bool killMark = p.Weapons.LastHitKill;
                Color hc = killMark ? UITheme.BloodBright : p.Weapons.LastHitWeak ? new Color(1f, 0.62f, 0.15f) : Color.white;
                float k = Mathf.Clamp01(p.Weapons.HitMarker / 0.12f);           // 1 → 0
                hc.a = Mathf.Clamp01(k * 1.6f);
                float inner = (killMark ? 9f : 7f) + (1f - k) * 3f;
                float len = (killMark ? 13f : 9f) * (0.7f + 0.3f * k) + (1f - UIKit.EaseOut((1f - k) * 3f)) * 4f;
                float thick = killMark ? 3f : 2f;
                var keep = GUI.matrix;
                for (int i = 0; i < 4; i++)
                {
                    float ang = 45f + i * 90f;
                    // 화면 배율(가상 1080p)이 걸린 행렬 안에서 조준점 중심으로 회전
                    GUI.matrix = keep * Matrix4x4.Translate(new Vector3(cx, cy, 0f)) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, ang)) * Matrix4x4.Translate(new Vector3(-cx, -cy, 0f));
                    var line = new Rect(cx + inner, cy - thick / 2f, len, thick);
                    UIKit.Fill(new Rect(line.x - 1, line.y - 1, line.width + 2, line.height + 2), new Color(0f, 0f, 0f, hc.a * 0.55f));
                    UIKit.Fill(line, hc);
                }
                GUI.matrix = keep;
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
                if (e.gold)
                {
                    float ga = 1f - e.t / DamageNumbers.Life;
                    UIKit.Text(new Rect(x - 80, y - 60, 160, 30), $"+{Mathf.RoundToInt(e.amount)} 엽전", 16, new Color(0.98f, 0.82f, 0.4f, ga), TextAnchor.MiddleCenter, serif: true, bold: true);
                    continue;
                }
                float u = e.t / DamageNumbers.Life;
                float a = 1f - u * u; // 끝에 가서 빠르게 사라진다
                // 숫자마다 살짝 옆으로 흩어진다 (겹쳐서 안 읽히지 않게)
                float side = (Mathf.Repeat(e.pos.x * 13.7f + e.pos.z * 7.3f, 1f) - 0.5f) * 60f * UIKit.EaseOut(u * 2f);
                x += side;
                Color c = e.zone == HitZone.WeakPoint ? new Color(1f, 0.55f, 0.1f)
                    : e.zone == HitZone.Head ? new Color(1f, 0.92f, 0.45f)
                    : e.zone == HitZone.Armor ? new Color(0.65f, 0.62f, 0.58f)
                    : e.type != DamageType.Physical ? CombatNames.ColorOf(e.type) : UITheme.Bone;
                c.a = a;
                // 치명타는 크게 튀었다가 줄어든다
                float pop = e.crit ? (e.t < 0.14f ? 1f + (0.14f - e.t) * 5f : 1f) : (e.t < 0.08f ? 1f + (0.08f - e.t) * 6f : 1f);
                int size = Mathf.RoundToInt((e.crit ? 34 : e.zone == HitZone.WeakPoint ? 27 : 21) * pop);
                string s = Mathf.RoundToInt(e.amount) + (e.crit ? "!" : "");
                UIKit.Text(new Rect(x - 80, y - 25, 160, 50), s, size, c, TextAnchor.MiddleCenter, serif: true, bold: e.crit || e.zone == HitZone.WeakPoint, shadow: true);
                if (e.zone == HitZone.Head && e.t < 0.5f)
                    UIKit.Text(new Rect(x - 60, y - 44, 120, 22), "머리", 14, new Color(1f, 0.92f, 0.6f, a), TextAnchor.MiddleCenter, serif: true, bold: true, shadow: true);
            }
        }

        /// <summary>저격 스코프 화면: 원형 시야 + 십자선, 나머지는 검게.</summary>
        void DrawScope(PlayerCharacter p)
        {
            DrawScopeMask();
            if (GameInput.Mobile) return;
            // 숨 참기 막대 (조준경 아래)
            var c = p.Controller;
            float t = c.BreathHold / PlayerController.BreathMax;
            var r = new Rect(W / 2 - 90, H - 120, 180, 6);
            UIKit.Bar(r, t, c.HoldingBreath ? new Color(0.75f, 0.85f, 0.95f) : new Color(0.5f, 0.55f, 0.6f));
            string label = c.HoldingBreath ? "숨을 참는 중" : t < 0.3f ? "숨이 가쁘다" : "Shift  숨 참기 (조준이 멈춘다)";
            UIKit.Text(new Rect(r.x - 100, r.y - 24, r.width + 200, 20), label, 14, UITheme.Bone, TextAnchor.MiddleCenter, serif: true);
        }

        void DrawScopeMask()
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

using Hunt.Combat;
using Hunt.Core;
using Hunt.Data;
using Hunt.Evolution;
using Hunt.Player;
using Hunt.Stats;
using Hunt.Weapons;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// 야영지 (밤과 밤 사이): 능력치·계약 / 무기·파츠 / 역병 연구.
    /// </summary>
    public class CampMenu
    {
        enum Tab { Stats, Armory, Arms, Gear, Research }
        Tab tab = Tab.Stats;
        int weaponIdx;
        Vector2 invScroll, historyScroll, notesScroll;
        bool showNotes;
        int rightView; // 역병 일지 오른쪽: 0 기록 · 1 쪽지 · 2 망자 도감
        StatType? hoverStat;
        readonly RadarChart radar = new RadarChart();
        float appear, tabAppear = 1f;

        public void OnOpen() { appear = 0f; }

        public void Draw(GameManager gm, float W, float H)
        {
            var p = gm.Player;
            if (p == null) return;
            if (UIKit.IsRepaint) appear = Mathf.MoveTowards(appear, 1f, Time.unscaledDeltaTime * 4f);
            float ease = UIKit.EaseOut(appear);
            UIKit.Alpha = ease;
            hoverStat = null;

            UIKit.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.01f, 0.015f, 0.6f));
            UIKit.Vignette(W, H, new Color(0, 0, 0, 0.8f));

            float pw = Mathf.Min(1560f, W - 60f);
            var win = new Rect(W / 2 - pw / 2, 96 + (1f - ease) * 26f, pw, 900);
            UIKit.Panel(win);

            // 제목
            UIKit.BrushTitle(new Rect(win.x, win.y + 12, win.width, 48), "서낭당", 38, new Color(0f, 0f, 0f, 0.7f), UITheme.GoldBright);
            UIKit.Text(new Rect(win.x, win.y + 56, win.width, 24), $"날이 밝았다. 망자들은 그늘로 숨었다. 서낭당 돌무더기 앞에서 숨을 고른다.  ·  다음 밤까지 {gm.IntermissionTime:0}초" + (p.Blessings > 0 ? $"  ·  장승의 축복 {p.Blessings}/3" : ""), 16, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            UIKit.Divider(new Rect(win.x + 200, win.y + 86, win.width - 400, 10));

            // 탭
            string[] names =
            {
                p.StatPoints > 0 || p.PendingTraitPicks > 0 ? "몸 · 기질  ●" : "몸 · 기질",
                "무기고",
                "부품",
                "방어구 · 보급",
                gm.LastEvolution.Count > 0 ? "역병 일지  ●" : "역병 일지",
            };
            float tw = Mathf.Min(240, (win.width - 80) / 5f - 10);
            float tx = win.center.x - (tw * 5 + 40) / 2;
            for (int i = 0; i < 5; i++)
                if (UIKit.Button(new Rect(tx + i * (tw + 10), win.y + 104, tw, 48), names[i], 20, (int)tab == i) && (int)tab != i) { tab = (Tab)i; tabAppear = 0f; }
            // 선택된 탭 아래 밑줄이 옆으로 미끄러져 따라간다
            float ux = UIKit.Smooth("camp.tabline", tx + (int)tab * (tw + 10), 14f);
            UIKit.Tex(new Rect(ux + 14, win.y + 154, tw - 28, 3), UITheme.LineFade, new Color(1f, 0.85f, 0.5f, 0.9f));

            // 탭을 바꾸면 내용이 살짝 옆에서 들어오며 나타난다 (0.18초)
            if (UIKit.IsRepaint) tabAppear = Mathf.MoveTowards(tabAppear, 1f, Time.unscaledDeltaTime * 5.5f);
            float te = UIKit.EaseOut(tabAppear);
            UIKit.Alpha = ease * te;
            var content = new Rect(win.x + 26 + (1f - te) * 14f, win.y + 168, win.width - 52, win.height - 260);
            switch (tab)
            {
                case Tab.Stats: DrawStats(p, content); break;
                case Tab.Armory: DrawArmory(gm, p, content); break;
                case Tab.Arms: DrawArms(p, content); break;
                case Tab.Gear: DrawGear(p, content); break;
                default: DrawResearch(gm, content); break;
            }

            UIKit.Alpha = ease;
            // 아래
            float by = win.yMax - 78;
            // 남은 단련·기질이 있으면 단추 아래에 알려 준다 (그냥 지나치지 않게)
            string remind = p.StatPoints > 0 && p.PendingTraitPicks > 0 ? $"단련 {p.StatPoints} · 기질 고르기가 남았다"
                          : p.StatPoints > 0 ? $"단련 포인트 {p.StatPoints} 남음 — '몸 · 기질' 탭"
                          : p.PendingTraitPicks > 0 ? "새 기질을 고를 수 있다 — '몸 · 기질' 탭" : "Enter";
            if (UIKit.Button(new Rect(win.center.x - 190, by, 380, 60), "밤을 맞이한다", 26, false, true, remind)) gm.StartNextWave();
            UIKit.Text(new Rect(win.x + 30, by, 400, 30), "Tab — 서낭당을 떠나 마을을 둘러본다", 15, UITheme.Ash);
            // 다음 밤 미리 보기
            int nextN = gm.Waves.Wave + 1;
            var nextRule = Hunt.Waves.WaveManager.RuleFor(nextN);
            bool nextBoss = nextN % Hunt.Waves.WaveManager.BossEvery == 0;
            UIKit.Text(new Rect(win.x + 30, by + 28, 520, 30),
                $"다음 밤: {GameHud.NightName(nextN)}  ·  {Hunt.Waves.WaveManager.RuleName(nextRule)}" + (nextBoss ? $"  ·  <color=#{UIKit.Hex(UITheme.BloodBright)}>대감의 밤</color>"
                    : $"  ·  보스 <color=#{UIKit.Hex(UITheme.BloodBright)}>{Hunt.Monsters.Monster.AffixName(gm.Waves.NextBossAffix)}</color> 그림자 ({Hunt.Monsters.Monster.AffixDesc(gm.Waves.NextBossAffix)})"),
                15, UITheme.BoneDim, serif: true);
            // 다음 밤에 생길 수 있는 일 (매복·망자 떼·정전·다친 사람)
            string ev = "";
            if (nextN >= 2) ev += $"덤불 매복 {(nextN >= 4 ? 2 : 1)}";
            if (nextN >= 3 && nextRule != Hunt.Waves.RoundRule.Defense) ev += (ev.Length > 0 ? " · " : "") + "망자 떼";
            if (nextN >= 3) ev += (ev.Length > 0 ? " · " : "") + "벼락 정전";
            if (nextN >= 2) ev += (ev.Length > 0 ? " · " : "") + (nextN >= 4 ? "다친 사람(가끔, 가짜일 수도)" : "다친 사람(가끔)");
            if (ev.Length > 0) UIKit.Text(new Rect(win.x + 30, by + 50, 520, 22), "있을 수 있는 일: " + ev, 13, UITheme.Ash, serif: true);
            UIKit.Text(new Rect(win.xMax - 430, by, 400, 60), $"엽전 <color=#{UIKit.Hex(UITheme.GoldBright)}>{Mathf.RoundToInt(UIKit.Smooth("camp.gold", p.Gold, 9f))}</color>   ·   청심환 ×{p.Infection.Antidotes}", 18, UITheme.BoneDim, TextAnchor.MiddleRight, serif: true);
            UIKit.Alpha = 1f;
        }

        // ------------------------------------------------------------------ 능력치 · 계약
        void DrawStats(PlayerCharacter p, Rect c)
        {
            var s = p.Stats;
            float lw = c.width * 0.56f;
            var left = new Rect(c.x, c.y, lw, c.height);
            var right = new Rect(c.x + lw + 20, c.y, c.width - lw - 20, c.height);

            UIKit.NineSlice(left, UITheme.Slot, 10, Color.white);
            UIKit.Text(new Rect(left.x + 24, left.y + 12, 400, 34), $"<b>Lv.{p.Level}</b>  단련", 22, UITheme.Bone, serif: true);
            UIKit.Text(new Rect(left.xMax - 300, left.y + 12, 276, 34), $"남은 단련  <color=#{UIKit.Hex(p.StatPoints > 0 ? UITheme.GoldBright : UITheme.Ash)}>{p.StatPoints}</color>", 22, UITheme.BoneDim, TextAnchor.MiddleRight, serif: true);

            float y = left.y + 58;
            float rowH = (left.height - 70) / CharacterStats.Visible.Length;
            foreach (var t in CharacterStats.Visible)
            {
                var row = new Rect(left.x + 14, y, left.width - 28, rowH - 6);
                float hv = UIKit.Hover(row, "cstat" + t, true, false);
                UIKit.Fill(row, new Color(1f, 0.95f, 0.85f, 0.02f + hv * 0.04f));
                UIKit.Tex(new Rect(row.x, row.yMax, row.width, 1), UITheme.LineFade, new Color(0.6f, 0.56f, 0.48f, 0.35f));
                // 투자한 능력치는 왼쪽에 세로 먹줄
                if (s.Allocated(t) > 0) UIKit.Fill(new Rect(row.x, row.y + 6, 3, row.height - 12), UITheme.GoldBright);

                UIKit.Text(new Rect(row.x + 16, row.y + 4, 120, 34), GameText.StatName(t), 24, UITheme.Bone, serif: true, bold: true);
                UIKit.Text(new Rect(row.x + 16, row.y + 36, 150, 22), GameText.StatShort(t), 13, UITheme.Ash);

                // 버튼: +1 · 전부
                bool can = p.StatPoints > 0;
                var all = new Rect(row.xMax - 66, row.center.y - 18, 58, 36);
                var plus = new Rect(all.x - 50, row.center.y - 18, 44, 36);
                if (UIKit.SmallButton(plus, "+1", can, 18)) p.SpendStatPoint(t);
                if (UIKit.SmallButton(all, "전부", can, 16)) p.SpendAllStatPoints(t);
                int preview = 0;
                if (can && plus.Contains(UIKit.Mouse)) { hoverStat = t; preview = 1; }
                if (can && all.Contains(UIKit.Mouse)) { hoverStat = t; preview = p.StatPoints; }
                UIKit.TooltipIf(all, $"남은 단련 {p.StatPoints}점을 모두 {GameText.StatName(t)}에 넣는다");

                // 눈금 + 효과
                float gx = row.x + 170, gw = plus.x - 80 - gx;
                int val = Mathf.RoundToInt(s.Get(t)), alloc = s.Allocated(t);
                int cells = Mathf.Max(24, val + preview);
                UIKit.StatCells(new Rect(gx, row.y + 12, gw, 16), cells, val - alloc, alloc, preview);
                UIKit.Text(new Rect(gx, row.y + 34, gw + 70, 24), GameText.StatEffects(t, s), 14, UITheme.BoneDim);

                UIKit.Text(new Rect(plus.x - 74, row.y, 64, row.height), val.ToString("0") + (preview > 0 ? $"<size=16><color=#{UIKit.Hex(UITheme.Good)}> +{preview}</color></size>" : ""),
                    30, alloc > 0 ? UITheme.GoldBright : UITheme.Bone, TextAnchor.MiddleRight, serif: true, bold: true);
                UIKit.TooltipIf(new Rect(row.x, row.y, 170, row.height), GameText.StatTooltip(t));
                y += rowH;
            }

            // 오른쪽: 차트 + 계약
            UIKit.NineSlice(right, UITheme.Slot, 10, Color.white);
            var vis = CharacterStats.Visible;
            var vals = new float[vis.Length];
            float[] prev = hoverStat.HasValue ? new float[vis.Length] : null;
            var labels = new string[vis.Length];
            float maxV = 12f;
            foreach (var t in vis) maxV = Mathf.Max(maxV, s.Get(t) + 2f);
            for (int i = 0; i < vis.Length; i++)
            {
                var t = vis[i];
                vals[i] = s.Get(t) / maxV;
                if (prev != null) prev[i] = (s.Get(t) + (hoverStat.Value == t ? 1 : 0)) / maxV;
                labels[i] = GameText.StatGlyph(t);
            }
            radar.Draw(new Rect(right.center.x - 100, right.y + 46, 200, 200), vals, prev, labels);

            float ty = right.y + 290;
            UIKit.Divider(new Rect(right.x + 20, ty, right.width - 40, 10));
            ty += 16;
            UIKit.Text(new Rect(right.x + 20, ty, right.width - 40, 28), "지닌 기질", 20, UITheme.Gold, TextAnchor.MiddleCenter, serif: true, bold: true);
            ty += 34;
            int n = s.traits.Count, k = 0;
            float ex = right.center.x - n * 56 / 2f;
            foreach (var t in s.traits)
            {
                var er = new Rect(ex + k * 56 + 6, ty, 44, 44);
                UIKit.Emblem(er, GameText.TraitGlyph(t), UITheme.GoldBright);
                UIKit.TooltipIf(er, $"<b>{Traits.Name(t)}</b>\n{Traits.Describe(t)}\n\n<color=#{UIKit.Hex(UITheme.BoneDim)}>{GameText.TraitFlavor(t)}</color>");
                k++;
            }
            ty += 62;

            p.EnsureTraitOffer();
            if (p.PendingTraitPicks > 0)
            {
                UIKit.Text(new Rect(right.x + 20, ty, right.width - 40, 28), $"새 기질을 얻을 수 있다  ({p.PendingTraitPicks})", 18, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
                ty += 34;
                float cw = (right.width - 40 - 20) / 3f;
                for (int i = 0; i < p.TraitOffer.Count; i++)
                {
                    var t = p.TraitOffer[i];
                    var r = new Rect(right.x + 20 + i * (cw + 10), ty, cw, right.yMax - ty - 16);
                    bool click = UIKit.HitArea(r, "offer" + t, out float hv);
                    // 숫자키 1·2·3으로도 고른다
                    if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Alpha1 + i) { click = true; Event.current.Use(); }
                    r.y -= hv * 6f;
                    // 한지 카드: 먹 바탕 + 한지색 테두리, 고르려 하면 밝아진다
                    UIKit.Tex(new Rect(r.x - 10, r.y - 10, r.width + 20, r.height + 20), UITheme.Glow, new Color(1f, 0.92f, 0.75f, 0.06f + hv * 0.18f));
                    UIKit.Fill(r, new Color(0.055f, 0.052f, 0.055f, 0.97f));
                    UIKit.Fill(new Rect(r.x, r.y, r.width, 4), Color.Lerp(new Color(0.6f, 0.56f, 0.48f), UITheme.GoldBright, hv));
                    UIKit.Outline(r, Color.Lerp(new Color(0.45f, 0.42f, 0.37f), UITheme.GoldBright, hv), 1f + hv);
                    // 오른쪽 위 낙관 (붉은 도장)
                    UIKit.Emblem(new Rect(r.xMax - 50, r.y + 8, 44, 44), GameText.TraitGlyph(t), UITheme.Gold);
                    // 번호
                    UIKit.Text(new Rect(r.x + 12, r.y + 12, 30, 22), (i + 1).ToString(), 15, UITheme.Ash, bold: true);
                    // 이름
                    UIKit.Text(new Rect(r.x + 12, r.y + 40, r.width - 24, 32), Traits.Name(t), 22, UITheme.Bone, TextAnchor.MiddleLeft, serif: true, bold: true);
                    UIKit.Fill(new Rect(r.x + 12, r.y + 76, r.width - 24, 1), new Color(0.6f, 0.56f, 0.48f, 0.4f));
                    // 효과: 쉼표마다 줄을 나눠 한눈에
                    string eff = "";
                    foreach (var part in Traits.Describe(t).Split(new[] { ", " }, System.StringSplitOptions.RemoveEmptyEntries)) eff += "· " + part + "\n";
                    UIKit.Text(new Rect(r.x + 12, r.y + 84, r.width - 24, r.height - 150), eff, 15, UITheme.GoldBright, TextAnchor.UpperLeft, wrap: true);
                    // 한 줄 이야기
                    UIKit.Text(new Rect(r.x + 12, r.yMax - 64, r.width - 24, 56), GameText.TraitFlavor(t), 13, UITheme.Ash, TextAnchor.LowerLeft, serif: true, wrap: true);
                    if (click) { p.PickTrait(t); break; }
                }
            }
            else
            {
                int next = (p.Level / Traits.PickEveryLevels + 1) * Traits.PickEveryLevels;
                UIKit.Text(new Rect(right.x + 20, ty, right.width - 40, 28), $"다음 기질 — Lv.{next}", 17, UITheme.Ash, TextAnchor.MiddleCenter, serif: true);
            }
        }

        // ------------------------------------------------------------------ 무기 · 파츠
        static string SlotGlyph(PartSlot s)
        {
            switch (s)
            {
                case PartSlot.Barrel: return "총";
                case PartSlot.Magazine: return "탄";
                case PartSlot.Stock: return "받";
                case PartSlot.Scope: return "눈";
                case PartSlot.Muzzle: return "구";
                default: return "부";
            }
        }

        static string PartText(WeaponPart part)
        {
            string hex = UIKit.Hex(WeaponPart.RarityColor(part.rarity));
            string s = $"<size=19><b><color=#{hex}>{part.name}</color></b></size>\n<color=#{UIKit.Hex(UITheme.BoneDim)}>{WeaponPart.RarityName(part.rarity)} {WeaponPart.SlotName(part.slot)} · Lv.{part.itemLevel}</color>\n";
            foreach (var a in part.affixes) s += $"\n◆ {a.Describe()}";
            return s;
        }

        void ItemSlot(Rect r, WeaponPart part, string emptyGlyph, out bool click, out float hover)
        {
            click = UIKit.HitArea(r, "item" + (part != null ? part.GetHashCode().ToString() : emptyGlyph + r.x), out hover);
            UIKit.NineSlice(r, UITheme.Slot, 8, Color.white);
            if (part != null)
            {
                Color rc = WeaponPart.RarityColor(part.rarity);
                UIKit.Tex(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), UITheme.Glow, new Color(rc.r, rc.g, rc.b, 0.25f + hover * 0.2f));
                UIKit.Text(r, SlotGlyph(part.slot), Mathf.RoundToInt(r.height * 0.42f), rc, TextAnchor.MiddleCenter, serif: true, bold: true);
                UIKit.NineSlice(r, UITheme.Frame, 6, new Color(rc.r, rc.g, rc.b, 0.6f + hover * 0.4f));
            }
            else
            {
                UIKit.Text(r, emptyGlyph, Mathf.RoundToInt(r.height * 0.36f), new Color(0.35f, 0.28f, 0.2f, 0.7f), TextAnchor.MiddleCenter, serif: true);
                if (hover > 0.01f) UIKit.NineSlice(r, UITheme.Frame, 6, new Color(1f, 0.8f, 0.5f, hover * 0.5f));
            }
        }

        void DrawArms(PlayerCharacter p, Rect c)
        {
            var slots = p.Weapons.Slots;
            weaponIdx = Mathf.Clamp(weaponIdx, 0, slots.Count - 1);
            var w = slots[weaponIdx];

            // ---- 왼쪽: 무기 선택 + 상세 ----
            var left = new Rect(c.x, c.y, 420, c.height);
            UIKit.NineSlice(left, UITheme.Slot, 10, Color.white);
            for (int i = 0; i < slots.Count; i++)
            {
                var sw = slots[i];
                if (UIKit.Button(new Rect(left.x + 16, left.y + 16 + i * 64, left.width - 32, 56), sw.DisplayName, 20, i == weaponIdx, true,
                        $"{GameText.WeaponClassName(sw.data.weaponClass)} · {GameText.ElementName(sw.DamageType)}"))
                    weaponIdx = i;
            }
            float y = left.y + 16 + slots.Count * 64 + 10;
            UIKit.Divider(new Rect(left.x + 20, y, left.width - 40, 10));
            y += 20;
            string hexE = UIKit.Hex(CombatNames.ColorOf(w.DamageType));
            string info =
                $"위력        {w.Damage:0.0}{(w.data.pellets > 1 ? " × " + w.data.pellets : "")}   <color=#{hexE}>{GameText.ElementName(w.DamageType)}</color>\n" +
                $"연사        {60f / w.FireInterval:0} RPM\n" +
                $"장탄        {w.MagazineSize}   (재장전 ×{w.ReloadSpeedMult:0.00})\n" +
                $"반동        ×{w.RecoilMult:0.00}    탄퍼짐 ×{w.SpreadMult:0.00}\n" +
                $"저지력      ×{w.KnockbackMult:0.00}\n" +
                $"치명 +{w.CritChanceBonus * 100:0}%    약점 +{w.WeakPointBonus * 100:0}%\n" +
                $"무게        {w.Weight:0.0}";
            UIKit.Text(new Rect(left.x + 28, y, left.width - 56, 190), info, 17, UITheme.Bone, TextAnchor.UpperLeft, wrap: true);
            y += 200;
            UIKit.Text(new Rect(left.x + 16, y, left.width - 32, 62), "강화와 등급은 '무기고'에서", 16, UITheme.Ash, TextAnchor.MiddleCenter, serif: true);

            // ---- 가운데: 부착 파츠 ----
            var mid = new Rect(left.xMax + 18, c.y, 400, c.height);
            UIKit.NineSlice(mid, UITheme.Slot, 10, Color.white);
            UIKit.Text(new Rect(mid.x, mid.y + 12, mid.width, 30), $"{w.data.displayName} 부착물", 20, UITheme.Gold, TextAnchor.MiddleCenter, serif: true, bold: true);
            var all = (PartSlot[])System.Enum.GetValues(typeof(PartSlot));
            float cell = 112, gap = 18;
            float gx = mid.center.x - (cell * 3 + gap * 2) / 2;
            for (int i = 0; i < all.Length; i++)
            {
                var slot = all[i];
                var r = new Rect(gx + (i % 3) * (cell + gap), mid.y + 64 + (i / 3) * (cell + 52), cell, cell);
                w.parts.TryGetValue(slot, out var part);
                ItemSlot(r, part, SlotGlyph(slot), out bool click, out _);
                UIKit.Text(new Rect(r.x - 10, r.yMax + 4, r.width + 20, 22), WeaponPart.SlotName(slot), 15, part != null ? UITheme.Bone : UITheme.Ash, TextAnchor.MiddleCenter, serif: true);
                if (part != null)
                {
                    UIKit.TooltipIf(r, PartText(part) + $"\n\n<color=#{UIKit.Hex(UITheme.Ash)}>[클릭] 떼어내기</color>");
                    if (click)
                    {
                        w.parts.Remove(slot);
                        p.Inventory.Add(part);
                    }
                }
                else UIKit.TooltipIf(r, $"<b>{WeaponPart.SlotName(slot)}</b>\n비어 있다. 오른쪽 짐에서 골라 단다.");
            }
            UIKit.Text(new Rect(mid.x + 20, mid.yMax - 90, mid.width - 40, 80),
                "부착물은 총의 모양에도 드러난다.\n부적(부)은 탄환에 기운을 싣는다.", 14, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true, wrap: true);

            // ---- 오른쪽: 짐 (인벤토리) ----
            var right = new Rect(mid.xMax + 18, c.y, c.xMax - mid.xMax - 18, c.height);
            UIKit.NineSlice(right, UITheme.Slot, 10, Color.white);
            UIKit.Text(new Rect(right.x, right.y + 12, right.width, 30), $"짐  ({p.Inventory.Count})", 20, UITheme.Gold, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Text(new Rect(right.x, right.y + 40, right.width, 22), "클릭: 장착   ·   Shift + 클릭: 분해(엽전)", 14, UITheme.Ash, TextAnchor.MiddleCenter);

            float icell = 86, igap = 10;
            int cols = Mathf.Max(1, Mathf.FloorToInt((right.width - 30 + igap) / (icell + igap)));
            int rows = Mathf.CeilToInt(p.Inventory.Count / (float)cols);
            var view = new Rect(right.x + 15, right.y + 72, right.width - 24, right.height - 86);
            var inner = new Rect(0, 0, view.width - 18, Mathf.Max(view.height, rows * (icell + igap)));
            invScroll = GUI.BeginScrollView(view, invScroll, inner, GUIStyle.none, GUI.skin.verticalScrollbar);
            if (p.Inventory.Count == 0)
                UIKit.Text(new Rect(0, 40, inner.width, 60), "짐이 비었다.\n망자를 쓰러뜨리면 부품이 떨어진다.", 16, UITheme.Ash, TextAnchor.MiddleCenter, serif: true, wrap: true);
            for (int i = 0; i < p.Inventory.Count; i++)
            {
                var part = p.Inventory[i];
                var r = new Rect((i % cols) * (icell + igap), (i / cols) * (icell + igap), icell, icell);
                ItemSlot(r, part, "", out bool click, out _);
                int sell = 15 * (1 + (int)part.rarity) * (1 + (int)part.rarity) + part.itemLevel * 2;
                w.parts.TryGetValue(part.slot, out var equipped);
                string tip = PartText(part);
                if (equipped != null) tip += $"\n\n<color=#{UIKit.Hex(UITheme.Ash)}>— 지금 달린 것 —</color>\n" + PartText(equipped);
                tip += $"\n\n<color=#{UIKit.Hex(UITheme.Ash)}>[클릭] {w.data.displayName}에 장착   [Shift+클릭] 분해 +{sell} 엽전</color>";
                UIKit.TooltipIf(r, tip);
                if (click)
                {
                    p.Inventory.RemoveAt(i);
                    if (Event.current.shift)
                    {
                        p.AddGold(sell);
                        SFX.Play(SFX.MagOut, 0.8f);
                    }
                    else
                    {
                        var old = w.Equip(part);
                        if (old != null) p.Inventory.Add(old);
                        SFX.Play(SFX.MagIn, 0.8f);
                    }
                    break;
                }
            }
            GUI.EndScrollView();
        }

        // ------------------------------------------------------------------ 무기고: 계열 · 단계 · 등급 · 강화
        WeaponData armorySel;
        Vector2 armoryScroll;

        static string GearLine(Grade g, int up) => $"<color=#{UIKit.Hex(Grades.Color(g))}>{Grades.Name(g)}</color>" + (up > 0 ? $" +{up}" : "");

        void DrawArmory(GameManager gm, PlayerCharacter p, Rect c)
        {
            if (armorySel == null) armorySel = p.Weapons.Current != null ? p.Weapons.Current.data : DefaultContent.Catalog[0];
            float lw = c.width * 0.58f;
            var left = new Rect(c.x, c.y, lw, c.height);
            var right = new Rect(c.x + lw + 20, c.y, c.width - lw - 20, c.height);
            UIKit.NineSlice(left, UITheme.Slot, 10, Color.white);
            UIKit.NineSlice(right, UITheme.Slot, 10, Color.white);

            // 망자 방어도 안내
            float def = gm.Waves != null ? gm.Waves.DefenseBonus(Data.MonsterArchetype.Runner) : 0f;
            float defNext = 1.4f * Mathf.Min(Mathf.Max(0, (gm.Waves != null ? gm.Waves.Wave : 0)) * 0.85f, 9f); // WaveManager.DefenseBonus와 같은 식 (다음 밤)
            UIKit.Text(new Rect(left.x + 20, left.y + 10, left.width - 40, 26),
                $"다음 밤 망자 방어도  <color=#{UIKit.Hex(UITheme.BloodBright)}>{defNext:0}</color>  (엘리트 ×1.6, 보스 ×2.1, 열 번째 밤쯤 멈춤)  ·  한 발마다 그만큼 깎인다 — 연사가 빠른 총은 점점 약해진다",
                14, UITheme.BoneDim, TextAnchor.MiddleLeft, wrap: true);

            var view = new Rect(left.x + 12, left.y + 44, left.width - 20, left.height - 56);
            float rowH = 112f;
            var inner = new Rect(0, 0, view.width - 18, DefaultContent.Families.Length * rowH);
            armoryScroll = GUI.BeginScrollView(view, armoryScroll, inner, GUIStyle.none, GUI.skin.verticalScrollbar);
            for (int f = 0; f < DefaultContent.Families.Length; f++)
            {
                var fam = DefaultContent.Families[f];
                var list = DefaultContent.Family(fam);
                var row = new Rect(0, f * rowH, inner.width, rowH - 8);
                UIKit.Tex(new Rect(row.x, row.yMax + 2, row.width, 2), UITheme.LineFade, new Color(0.5f, 0.38f, 0.2f, 0.3f));
                UIKit.Text(new Rect(row.x + 8, row.y, 120, row.height), GameText.WeaponClassName(fam), 20, UITheme.Gold, TextAnchor.MiddleLeft, serif: true, bold: true);
                float cw = (row.width - 140) / 3f;
                for (int t = 0; t < 3; t++)
                {
                    var cell = new Rect(row.x + 132 + t * cw, row.y + 4, cw - 8, row.height - 8);
                    if (t >= list.Count) continue;
                    var d = list[t];
                    var own = p.Owned(d);
                    bool equipped = own != null && p.Weapons.Slots.Contains(own);
                    string sub = own != null ? (equipped ? "장착 중 · " : "보유 · ") + Grades.Name(own.grade) + (own.upgradeLevel > 0 ? $" +{own.upgradeLevel}" : "")
                        : $"엽전 {d.price}";
                    // 칸: 무기 그림 + 이름 + 상태 (고른 칸은 한지색 테두리)
                    bool selCell = armorySel == d;
                    bool clicked = UIKit.HitArea(cell, "arm" + d.id, out float hv);
                    UIKit.Fill(cell, new Color(0.04f, 0.038f, 0.04f, 0.9f));
                    if (hv > 0f) UIKit.Fill(cell, new Color(1f, 0.95f, 0.85f, 0.05f * hv));
                    UIKit.Outline(cell, selCell ? UITheme.GoldBright : equipped ? new Color(0.62f, 0.58f, 0.5f) : new Color(0.3f, 0.29f, 0.27f, 0.9f), selCell ? 2f : 1f);
                    Color ic = own != null ? (equipped ? UITheme.GoldBright : UITheme.Bone) : new Color(0.45f, 0.43f, 0.4f);
                    WeaponIcon.Draw(new Rect(cell.x + 8, cell.y + 4, cell.width - 16, cell.height * 0.5f), d, ic);
                    UIKit.Text(new Rect(cell.x + 6, cell.y + cell.height * 0.52f, cell.width - 12, 20), $"{t + 1}단  {d.displayName}", 14, own != null ? UITheme.Bone : UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true, bold: true);
                    UIKit.Text(new Rect(cell.x + 6, cell.y + cell.height * 0.52f + 20, cell.width - 12, 18), sub, 12,
                        equipped ? UITheme.GoldBright : own != null ? UITheme.BoneDim : (p.Gold >= d.price ? UITheme.Good : UITheme.Ash), TextAnchor.MiddleCenter);
                    if (clicked) armorySel = d;
                }
            }
            GUI.EndScrollView();

            // ---- 오른쪽: 고른 무기 ----
            var sel = armorySel;
            var inst = p.Owned(sel);
            float y = right.y + 16;
            UIKit.Text(new Rect(right.x + 20, y, right.width - 40, 34), sel.displayName, 26, inst != null ? Grades.Color(inst.grade) : UITheme.Bone, TextAnchor.MiddleLeft, serif: true, bold: true);
            y += 36;
            UIKit.Text(new Rect(right.x + 20, y, right.width - 40, 24),
                $"{GameText.WeaponClassName(sel.weaponClass)} {sel.tier}단  ·  {GameText.ElementName(sel.damageType)}" + (inst != null ? "  ·  " + GearLine(inst.grade, inst.upgradeLevel) : "")
                + (inst != null ? $"  ·  숙련 {(inst.Mastery > 0 ? WeaponInstance.MasteryName(inst.Mastery) : "처음")} (처치 {inst.kills}{(inst.KillsToNextMastery > 0 ? $", 다음까지 {inst.KillsToNextMastery}" : "")})" : ""), 16, UITheme.BoneDim);
            y += 30;
            // 큰 그림
            WeaponIcon.Draw(new Rect(right.x + 30, y, right.width - 60, 96), sel, inst != null ? UITheme.GoldBright : UITheme.Bone);
            y += 104;
            float dmg = inst != null ? inst.Damage : sel.damage;
            float rpm = sel.rpm;
            float dps = dmg * Mathf.Max(1, sel.pellets) * rpm / 60f;
            float effHit = Mathf.Max(dmg - defNext * (1f - Mathf.Clamp01(sel.penetration)), dmg * 0.1f);
            if (sel.explosive || sel.weaponClass == WeaponClass.Flamethrower) effHit = dmg; // 폭발·화염은 방어도 무시
            float effDps = effHit * Mathf.Max(1, sel.pellets) * rpm / 60f;
            // 능력 막대: 전체 무기 중 가장 센 값 기준 (한눈에 비교)
            float maxHit = 1f, maxRpm = 1f, maxMag = 1f, maxW = 1f;
            foreach (var cd in DefaultContent.Catalog)
            {
                maxHit = Mathf.Max(maxHit, cd.damage * Mathf.Max(1, cd.pellets));
                maxRpm = Mathf.Max(maxRpm, cd.rpm);
                maxMag = Mathf.Max(maxMag, cd.magazine);
                maxW = Mathf.Max(maxW, cd.weight);
            }
            // 지금 손에 든 총과 비교: 막대 위 가는 금(지금 총 값) — 더 높으면 막대 끝이 밝게
            var curW = p.Weapons.Current;
            var cur = curW != null && curW.data != sel ? curW.data : null;
            void StatBar(string label, float t, string val, Color col, float cmp = -1f, bool lowerBetter = false)
            {
                UIKit.Text(new Rect(right.x + 24, y, 90, 22), label, 15, UITheme.BoneDim, serif: true);
                var br = new Rect(right.x + 112, y + 7, right.width - 112 - 110, 8);
                UIKit.Fill(br, new Color(0f, 0f, 0f, 0.6f));
                UIKit.Fill(new Rect(br.x, br.y, br.width * Mathf.Clamp01(t), br.height), col);
                if (cmp >= 0f)
                {
                    float cx = br.x + br.width * Mathf.Clamp01(cmp);
                    UIKit.Fill(new Rect(cx - 1f, br.y - 4, 2f, br.height + 8), new Color(0.95f, 0.92f, 0.85f, 0.9f));
                    bool better = lowerBetter ? t < cmp - 0.01f : t > cmp + 0.01f;
                    bool worse = lowerBetter ? t > cmp + 0.01f : t < cmp - 0.01f;
                    if (better || worse)
                        UIKit.Text(new Rect(br.xMax + 62, y, 40, 22), better ? "▲" : "▼", 13, better ? UITheme.Good : UITheme.BloodBright, TextAnchor.MiddleLeft, bold: true);
                }
                UIKit.Text(new Rect(br.xMax + 8, y, 100, 22), val, 15, UITheme.Bone, TextAnchor.MiddleLeft, bold: true);
                y += 26;
            }
            Color barC = new Color(0.86f, 0.82f, 0.72f);
            StatBar("한 발 위력", Mathf.Sqrt(dmg * Mathf.Max(1, sel.pellets) / maxHit), $"{dmg:0}{(sel.pellets > 1 ? " ×" + sel.pellets : "")}", barC,
                cur != null ? Mathf.Sqrt(curW.Damage * Mathf.Max(1, cur.pellets) / maxHit) : -1f);
            StatBar("연사", sel.rpm / maxRpm, $"{sel.rpm:0}", barC, cur != null ? cur.rpm / maxRpm : -1f);
            StatBar("관통", Mathf.Clamp01(sel.penetration), $"{sel.penetration * 100:0}%", barC, cur != null ? Mathf.Clamp01(cur.penetration) : -1f);
            StatBar("탄창", Mathf.Sqrt(sel.magazine / maxMag), $"{sel.magazine}", barC, cur != null ? Mathf.Sqrt(cur.magazine / maxMag) : -1f);
            StatBar("무게", sel.weight / maxW, $"{sel.weight:0.0}", new Color(0.6f, 0.58f, 0.54f), cur != null ? cur.weight / maxW : -1f, lowerBetter: true);
            if (cur != null)
            {
                UIKit.Text(new Rect(right.x + 24, y, right.width - 48, 18), $"가는 금 = 지금 든 {cur.displayName}", 12, UITheme.Ash, serif: true);
                y += 20;
            }
            y += 4;
            string extra = (sel.explosive ? $"폭발 반경 {sel.blastRadius:0.0}m   " : "") + (sel.maxRange < 100f ? $"사거리 {sel.maxRange:0}m" : "");
            if (extra.Length > 0) { UIKit.Text(new Rect(right.x + 24, y, right.width - 48, 20), extra, 14, UITheme.BoneDim); y += 22; }
            UIKit.Text(new Rect(right.x + 24, y, right.width - 48, 22), $"초당 피해 {dps:0}   →   다음 밤 실제 " +
                $"<color=#{UIKit.Hex(effDps < dps * 0.6f ? UITheme.BloodBright : UITheme.GoldBright)}>{effDps:0}</color>  (방어도 적용)", 16, UITheme.Bone, serif: true);
            y += 30;
            string flavor = GameText.WeaponFlavor(sel);
            if (!string.IsNullOrEmpty(flavor)) { UIKit.Text(new Rect(right.x + 24, y, right.width - 48, 24), flavor, 14, UITheme.Ash, serif: true); y += 28; }

            float bw = right.width - 40;
            if (inst == null)
            {
                bool can = p.Gold >= sel.price;
                if (UIKit.Button(new Rect(right.x + 20, y, bw, 58), "사들인다", 22, false, can, $"엽전 {sel.price}"))
                {
                    if (p.SpendGold(sel.price))
                    {
                        var w = new WeaponInstance(sel);
                        p.Arsenal.Add(w);
                        SFX.Play(SFX.Rack, 0.9f, 0f);
                        HUD.Notify($"{sel.displayName}을(를) 손에 넣었다", UITheme.GoldBright);
                    }
                }
                return;
            }

            bool isPistol = sel.weaponClass == WeaponClass.Pistol;
            bool equippedNow = p.Weapons.Slots.Contains(inst);
            if (isPistol)
            {
                if (UIKit.Button(new Rect(right.x + 20, y, bw, 50), equippedNow ? "3번에 장착 중" : "3번(보조)에 장착", 19, equippedNow, !equippedNow))
                    p.Weapons.EquipSlot(2, inst);
                y += 58;
            }
            else
            {
                float hw = (bw - 10) / 2f;
                for (int k = 0; k < 2; k++)
                {
                    bool here = p.Weapons.PrimaryCount > k && p.Weapons.Slots[k] == inst;
                    if (UIKit.Button(new Rect(right.x + 20 + k * (hw + 10), y, hw, 50), here ? $"{k + 1}번에 장착 중" : $"{k + 1}번에 장착", 19, here, !here))
                        p.Weapons.EquipSlot(k, inst);
                }
                y += 58;
            }

            bool maxUp = inst.upgradeLevel >= Grades.MaxUpgrade;
            if (UIKit.Button(new Rect(right.x + 20, y, bw, 56), maxUp ? "더는 벼릴 수 없다" : $"강화  +{inst.upgradeLevel + 1}", 20, false, !maxUp && p.Gold >= inst.UpgradeCost,
                    maxUp ? null : $"엽전 {inst.UpgradeCost}  ·  위력 +7%"))
            {
                if (p.SpendGold(inst.UpgradeCost)) { inst.upgradeLevel++; SFX.Play(SFX.Rack, 0.9f, 0f); }
            }
            y += 64;
            bool maxG = (int)inst.grade >= Grades.Max;
            bool needUp = inst.upgradeLevel < 5;
            string gsub = maxG ? null : needUp ? "강화 +5 이상 필요" : $"엽전 {inst.GradeUpCost}  ·  {Grades.Name(inst.grade + 1)} (위력 ×{Grades.Mult(inst.grade + 1) / Grades.Mult(inst.grade):0.00}, 예비탄 +10%)";
            if (UIKit.Button(new Rect(right.x + 20, y, bw, 56), maxG ? "전설 등급" : "등급 올리기", 20, false, !maxG && !needUp && p.Gold >= inst.GradeUpCost, gsub))
            {
                if (p.SpendGold(inst.GradeUpCost))
                {
                    inst.grade++;
                    inst.upgradeLevel = Mathf.Max(0, inst.upgradeLevel - 5); // 등급을 올리면 강화 단계 일부를 소모
                    SFX.Play(SFX.LevelUp, 0.8f, 0f);
                    HUD.Notify($"{sel.displayName} — {Grades.Name(inst.grade)} 등급", Grades.Color(inst.grade));
                }
            }
        }

        // ------------------------------------------------------------------ 방어구 · 보급
        void DrawGear(PlayerCharacter p, Rect c)
        {
            float lw = c.width * 0.58f;
            var left = new Rect(c.x, c.y, lw, c.height);
            var right = new Rect(c.x + lw + 20, c.y, c.width - lw - 20, c.height);
            UIKit.NineSlice(left, UITheme.Slot, 10, Color.white);
            UIKit.NineSlice(right, UITheme.Slot, 10, Color.white);

            var set = p.Armor;
            UIKit.Text(new Rect(left.x + 20, left.y + 12, left.width - 40, 30),
                $"방어구  ·  총 방어도 {set.TotalDefense:0}  ·  받는 피해 -{(1f - set.DamageTakenMult) * 100f:0}%", 20, UITheme.Gold, serif: true, bold: true);
            float rowH = (left.height - 60) / 5f;
            for (int i = 0; i < 5; i++)
            {
                var piece = set.pieces[i];
                var row = new Rect(left.x + 14, left.y + 52 + i * rowH, left.width - 28, rowH - 8);
                UIKit.Tex(new Rect(row.x, row.yMax + 2, row.width, 2), UITheme.LineFade, new Color(0.5f, 0.38f, 0.2f, 0.3f));
                // 부위 그림 (등급 색 테두리)
                var ib = new Rect(row.x + 4, row.center.y - 30, 60, 60);
                UIKit.Fill(ib, new Color(0.03f, 0.03f, 0.03f, 0.85f));
                UIKit.Outline(ib, Grades.Color(piece.grade), 1.5f);
                var icon = i == 0 ? HudIcons.Icon.Helmet : i == 1 ? HudIcons.Icon.Vest : i == 2 ? HudIcons.Icon.Greave : i == 3 ? HudIcons.Icon.Glove : HudIcons.Icon.Boot;
                UIKit.Tex(new Rect(ib.x + 10, ib.y + 10, 40, 40), HudIcons.Get(icon), UITheme.Bone);
                UIKit.Text(new Rect(row.x + 78, row.y + 4, 280, 30), piece.Name, 20, Grades.Color(piece.grade), serif: true, bold: true);
                UIKit.Text(new Rect(row.x + 78, row.y + 32, 320, 22), $"방어도 {piece.Defense:0.0}  ·  {piece.EffectText}", 14, UITheme.BoneDim);
                // 강화 단계 눈금 (+0 ~ +10)
                UIKit.StatCells(new Rect(row.x + 78, row.y + 58, 190, 8), Grades.MaxUpgrade, 0, piece.upgrade, 0);
                int upCost = Grades.UpgradeCost(piece.grade, piece.upgrade);
                int gCost = Grades.GradeUpCost(piece.grade);
                bool maxUp = piece.upgrade >= Grades.MaxUpgrade;
                bool maxG = (int)piece.grade >= Grades.Max;
                var b1 = new Rect(row.xMax - 380, row.y + 8, 180, row.height - 16);
                var b2 = new Rect(row.xMax - 190, row.y + 8, 180, row.height - 16);
                if (UIKit.Button(b1, maxUp ? "강화 끝" : $"강화 +{piece.upgrade + 1}", 17, false, !maxUp && p.Gold >= upCost, maxUp ? null : $"엽전 {upCost}"))
                    if (p.SpendGold(upCost)) { float r = p.Hp / p.MaxHp; piece.upgrade++; p.Heal(0f); SFX.Play(SFX.MagIn, 0.8f); }
                bool needUp = piece.upgrade < 5;
                if (UIKit.Button(b2, maxG ? "전설" : "등급 올리기", 17, false, !maxG && !needUp && p.Gold >= gCost, maxG ? null : needUp ? "강화 +5 필요" : $"엽전 {gCost}"))
                    if (p.SpendGold(gCost)) { piece.grade++; piece.upgrade = Mathf.Max(0, piece.upgrade - 5); SFX.Play(SFX.LevelUp, 0.8f, 0f); }
            }

            // ---- 보급: 소비 아이템 · 특수 기기 ----
            UIKit.Text(new Rect(right.x, right.y + 12, right.width, 30), "보급", 22, UITheme.Gold, TextAnchor.MiddleCenter, serif: true, bold: true);
            float y = right.y + 54;
            const int buyRows = 3 + Gadgets.Count + 5; // 키트·회복제·폭죽 + 기기 + 부적 4 + 향
            float rowStep = Mathf.Min(74f, (right.height - 54f - 64f) / buyRows);
            float buyH = rowStep - 6f;
            void Buy(HudIcons.Icon icon, Color tint, string name, string desc, int have, int max, int price, System.Action give)
            {
                // 보급 칸: 그림 + 이름·설명 + 개수 칸 + 가격 (누르면 산다)
                var r = new Rect(right.x + 16, y, right.width - 32, buyH);
                bool full = have >= max;
                bool can = !full && p.Gold >= price;
                bool click = UIKit.HitArea(r, "buy" + name, out float hv, can);
                UIKit.Fill(r, new Color(0.04f, 0.038f, 0.04f, 0.9f));
                if (hv > 0f) UIKit.Fill(r, new Color(1f, 0.95f, 0.85f, 0.06f * hv));
                UIKit.Outline(r, can ? Color.Lerp(new Color(0.45f, 0.42f, 0.37f), UITheme.GoldBright, hv) : new Color(0.3f, 0.29f, 0.27f, 0.8f));
                float ic = Mathf.Min(40f, buyH - 16f);
                UIKit.Tex(new Rect(r.x + 12, r.y + (buyH - ic) / 2f, ic, ic), HudIcons.Get(icon), can || full ? tint : new Color(0.45f, 0.43f, 0.4f));
                UIKit.Text(new Rect(r.x + 64, r.y + 3, r.width - 170, 24), name, 16, UITheme.Bone, serif: true, bold: true);
                UIKit.Text(new Rect(r.x + 64, r.y + buyH * 0.48f, r.width - 170, 20), desc, 12, UITheme.BoneDim);
                UIKit.StatCells(new Rect(r.xMax - 100, r.y + 12, 88, 10), max, 0, have, 0);
                UIKit.Text(new Rect(r.xMax - 110, r.y + 30, 98, 26), full ? "가득" : $"엽전 {price}", 15, full ? UITheme.Ash : p.Gold >= price ? UITheme.Good : UITheme.BloodBright, TextAnchor.MiddleRight, bold: true);
                if (click && p.SpendGold(price)) { give(); SFX.Play(SFX.MagIn, 0.8f); }
                y += rowStep;
            }
            Buy(HudIcons.Icon.Kit, new Color(1f, 0.42f, 0.36f), "치료 키트 (X)", "2.5초 동안 감는다", p.Kits, PlayerCharacter.MaxKits, 60, () => p.Kits++);
            Buy(HudIcons.Icon.Bottle, new Color(0.45f, 0.78f, 1f), "스태미너 회복제 (C)", "기운 즉시 회복", p.Drinks, PlayerCharacter.MaxDrinks, 40, () => p.Drinks++);
            Buy(HudIcons.Icon.Flame, new Color(1f, 0.35f, 0.25f), "폭죽 (B)", "4초 동안 타닥타닥 — 30m 망자를 그쪽으로 꾄다", p.Firecrackers, PlayerCharacter.MaxFirecrackers, 25, () => p.Firecrackers++);
            var gc = p.Weapons.GadgetCount;
            for (int i = 0; i < Gadgets.Count; i++)
            {
                var g = (GadgetType)i;
                int price = g == GadgetType.Grenade ? 50 : g == GadgetType.Mine ? 70 : g == GadgetType.Molotov ? 55 : 160;
                int idx = i;
                var gi = g == GadgetType.Grenade ? HudIcons.Icon.Grenade : g == GadgetType.Mine ? HudIcons.Icon.Mine : g == GadgetType.Molotov ? HudIcons.Icon.Flame : HudIcons.Icon.Turret;
                Buy(gi, UITheme.Bone, Gadgets.Name(g) + (g == GadgetType.Grenade ? " (5 / G)" : " (5)"), Gadgets.Desc(g), gc[i], Gadgets.MaxCarry(g), price, () => gc[idx]++);
            }
            // 부적: 다음 밤 하루만 듣는다 (밤이 끝나면 사라짐)
            Buy(HudIcons.Icon.Helmet, new Color(0.95f, 0.85f, 0.6f), "호신부 — 다음 밤", "받는 피해 -20% (하룻밤)", p.CharmGuard ? 1 : 0, 1, 80, () => p.CharmGuard = true);
            Buy(HudIcons.Icon.Boot, new Color(0.95f, 0.85f, 0.6f), "축지부 — 다음 밤", "이동 속도 +12% (하룻밤)", p.CharmSwift ? 1 : 0, 1, 60, () => p.CharmSwift = true);
            Buy(HudIcons.Icon.Ammo, new Color(0.95f, 0.85f, 0.6f), "명중부 — 다음 밤", "탄퍼짐 -25% (하룻밤)", p.CharmAim ? 1 : 0, 1, 70, () => p.CharmAim = true);
            Buy(HudIcons.Icon.Crouch, new Color(0.95f, 0.85f, 0.6f), "은신부 — 다음 밤", "앉아 있으면 망자 눈에 더 안 띈다 (하룻밤)", p.CharmHide ? 1 : 0, 1, 50, () => p.CharmHide = true);
            Buy(HudIcons.Icon.Flame, new Color(0.8f, 0.75f, 0.65f), "쑥 향 — 다음 밤", "냄새를 감춘다: 망자들이 몰려오기까지 +30초", p.CharmIncense ? 1 : 0, 1, 45, () => p.CharmIncense = true);
            UIKit.Text(new Rect(right.x + 20, right.yMax - 56, right.width - 40, 50), "밤을 넘길 때마다 키트·회복제·특수 기기가 하나씩 보급된다. 부적은 다음 밤 하루만 듣는다.", 13, UITheme.Ash, TextAnchor.MiddleCenter, serif: true, wrap: true);
        }

        // ------------------------------------------------------------------ 역병 연구
        void DrawResearch(GameManager gm, Rect c)
        {
            var evo = gm.Evolution.Profile;
            float lw = c.width * 0.55f;
            var left = new Rect(c.x, c.y, lw, c.height);
            var right = new Rect(c.x + lw + 20, c.y, c.width - lw - 20, c.height);
            UIKit.NineSlice(left, UITheme.Slot, 10, Color.white);
            UIKit.NineSlice(right, UITheme.Slot, 10, Color.white);

            float y = left.y + 16;
            UIKit.Text(new Rect(left.x + 24, y, left.width - 48, 34), $"망자들의 적응 — {evo.generation}세대", 24, UITheme.InfectBright, serif: true, bold: true);
            y += 38;
            UIKit.Text(new Rect(left.x + 24, y, left.width - 48, 50),
                "망자들은 지난밤 가장 많이 당한 방식을 기억한다. 같은 사냥법만 고집하면 점점 통하지 않는다. 대신 적응한 망자일수록 더 많은 것을 떨군다.", 15, UITheme.BoneDim, TextAnchor.UpperLeft, wrap: true);
            y += 66;

            if (gm.LastEvolution.Count > 0)
            {
                UIKit.Text(new Rect(left.x + 24, y, 400, 28), "지난밤의 변화", 19, UITheme.GoldBright, serif: true, bold: true);
                y += 30;
                foreach (var m in gm.LastEvolution)
                {
                    UIKit.Tex(new Rect(left.x + 30, y + 7, 12, 12), UITheme.Diamond, UITheme.InfectBright);
                    UIKit.Text(new Rect(left.x + 52, y, left.width - 80, 26), m, 17, UITheme.Bone);
                    y += 28;
                }
            }
            else
            {
                UIKit.Text(new Rect(left.x + 24, y, left.width - 48, 26), "지난밤에는 아무것도 변하지 않았다.", 17, UITheme.Ash, serif: true);
                y += 30;
            }
            y += 14;
            UIKit.Divider(new Rect(left.x + 30, y, left.width - 60, 10));
            y += 22;

            UIKit.Text(new Rect(left.x + 24, y, 400, 28), "기운별 내성", 19, UITheme.Gold, serif: true, bold: true);
            y += 34;
            for (int i = 0; i < 5; i++)
            {
                var t = (DamageType)i;
                UIKit.Text(new Rect(left.x + 30, y, 90, 24), GameText.ElementName(t), 17, CombatNames.ColorOf(t), serif: true, bold: true);
                UIKit.Bar(new Rect(left.x + 120, y + 4, left.width - 260, 16), evo.resist[i] / 0.6f, CombatNames.ColorOf(t) * 0.8f);
                UIKit.Text(new Rect(left.xMax - 120, y, 90, 24), $"{evo.resist[i] * 100:0}%", 17, UITheme.Bone, TextAnchor.MiddleRight, bold: true);
                y += 32;
            }
            y += 10;
            string misc =
                $"약점 위치  ·  {EvolutionDirector.LocName(evo.weakPoint)}\n" +
                $"투구  ·  {evo.helmetLevel}단계       다리 강화  ·  ×{evo.legHpMult:0.0}\n" +
                $"넉백 저항 +{evo.knockbackResistBonus * 100:0}%    속도 +{evo.speedBonus * 100:0}%    방어 +{evo.defenseBonus:0}\n" +
                $"전리품 ×{evo.RewardMultiplier:0.0}    부품 행운 +{evo.PartLuck * 100:0}%";
            UIKit.Text(new Rect(left.x + 30, y, left.width - 60, 110), misc, 16, UITheme.BoneDim, TextAnchor.UpperLeft, wrap: true);

            // 오른쪽: 기록 / 찢어진 쪽지 (단추로 바꿔 본다)
            int notesRead = 0;
            try { notesRead = Mathf.Clamp(PlayerPrefs.GetInt("hunt.lore.read", 0), 0, GameText.LoreNotes.Length); } catch { }
            float hb = (right.width - 60) / 3f;
            if (UIKit.Button(new Rect(right.x + 20, right.y + 14, hb, 38), "기록", 16, rightView == 0)) rightView = 0;
            if (UIKit.Button(new Rect(right.x + 30 + hb, right.y + 14, hb, 38), $"쪽지 {notesRead}/{GameText.LoreNotes.Length}", 16, rightView == 1)) rightView = 1;
            if (UIKit.Button(new Rect(right.x + 40 + hb * 2f, right.y + 14, hb, 38), "망자 도감", 16, rightView == 2)) rightView = 2;
            showNotes = rightView == 1;
            var view = new Rect(right.x + 20, right.y + 64, right.width - 30, right.height - 80);
            if (rightView == 2)
            {
                // 망자 도감: 본 적 있는 유형은 이름·특징·쓰러뜨린 수, 아직 못 본 유형은 '???'
                var kinds = DefaultContent.ZombieKinds;
                float rowH = 64f;
                string[,] habits =
                {
                    { "등 뒤", "어슬렁대는 망자는 등 뒤를 잘 못 본다. 앉아 다가가 칼(V)로 한 번에." },
                    { "뒤지기", "소리 난 곳·놓친 자리를 뒤진다 (머리 위 '?'). 돌·폭죽·사이렌으로 꾄다." },
                    { "매복", "둘째 밤부터 큰 덤불 속에 엎드려 있다. 손전등·번개에 붉은 눈, 돌을 던지거나 쏘면 튀어나온다." },
                    { "망자 떼", "셋째 밤부터 마을을 가로지른다. 들키지 않고 보내면 엽전." },
                };
                int hn = habits.GetLength(0);
                var inn = new Rect(0, 0, view.width - 18, Mathf.Max(view.height, kinds.Length * rowH + 40 + hn * 52));
                notesScroll = GUI.BeginScrollView(view, notesScroll, inn, GUIStyle.none, GUI.skin.verticalScrollbar);
                // 망자의 버릇 (유형 목록 아래)
                float hy = kinds.Length * rowH + 8;
                UIKit.Text(new Rect(0, hy, inn.width, 26), "망자의 버릇", 17, UITheme.Gold, TextAnchor.MiddleCenter, serif: true, bold: true);
                for (int i = 0; i < hn; i++)
                {
                    var hr = new Rect(0, hy + 32 + i * 52, inn.width - 6, 46);
                    UIKit.Fill(hr, new Color(0.04f, 0.038f, 0.04f, 0.7f));
                    UIKit.Text(new Rect(hr.x + 12, hr.y, 90, hr.height), habits[i, 0], 15, UITheme.Bone, TextAnchor.MiddleLeft, serif: true, bold: true);
                    UIKit.Text(new Rect(hr.x + 104, hr.y + 2, hr.width - 112, hr.height - 4), habits[i, 1], 13, UITheme.BoneDim, TextAnchor.MiddleLeft, wrap: true);
                }
                for (int i = 0; i < kinds.Length; i++)
                {
                    var k = kinds[i];
                    int kills = 0; bool seen = k.id == "basic";
                    try { kills = PlayerPrefs.GetInt("hunt.kills.z_" + k.id, 0); seen |= kills > 0 || PlayerPrefs.GetInt("hunt.seen.z_" + k.id, 0) == 1; } catch { }
                    var rr = new Rect(0, i * rowH, inn.width - 6, rowH - 6);
                    UIKit.Fill(rr, new Color(0.04f, 0.038f, 0.04f, 0.85f));
                    UIKit.Fill(new Rect(rr.x, rr.y, 6, rr.height), seen ? new Color(k.tint.r, k.tint.g, k.tint.b, 1f) : new Color(0.2f, 0.2f, 0.2f));
                    UIKit.Text(new Rect(rr.x + 16, rr.y + 4, rr.width - 140, 26), seen ? k.name : "???", 17, seen ? UITheme.Bone : UITheme.Ash, serif: true, bold: true);
                    UIKit.Text(new Rect(rr.x + 16, rr.y + 30, rr.width - 140, 24), seen ? k.desc : GameHud.NightName(k.fromWave) + "부터 나온다", 13, UITheme.BoneDim, wrap: true);
                    UIKit.Text(new Rect(rr.xMax - 120, rr.y + 4, 110, 26), seen ? $"{kills}마리" : "", 16, UITheme.GoldBright, TextAnchor.MiddleRight, bold: true);
                }
                GUI.EndScrollView();
                return;
            }
            if (showNotes)
            {
                float rowH = 92f;
                var inn = new Rect(0, 0, view.width - 18, Mathf.Max(view.height, notesRead * rowH));
                notesScroll = GUI.BeginScrollView(view, notesScroll, inn, GUIStyle.none, GUI.skin.verticalScrollbar);
                if (notesRead == 0)
                    UIKit.Text(new Rect(0, 20, inn.width, 60), "아직 주운 쪽지가 없다.\n보급 상자를 열면 가끔 나온다.", 16, UITheme.Ash, TextAnchor.MiddleCenter, serif: true, wrap: true);
                for (int i = 0; i < notesRead; i++)
                {
                    var nr = new Rect(0, i * rowH, inn.width - 6, rowH - 8);
                    UIKit.Tex(nr, UITheme.Paper, new Color(0.9f, 0.84f, 0.7f, 0.9f));
                    UIKit.Text(new Rect(nr.x + 12, nr.y + 6, 60, 20), $"{i + 1}", 13, new Color(0.4f, 0.12f, 0.08f), serif: true, bold: true, shadow: false);
                    UIKit.Text(new Rect(nr.x + 36, nr.y + 6, nr.width - 48, nr.height - 12), GameText.LoreNotes[i], 14, new Color(0.12f, 0.1f, 0.08f), TextAnchor.UpperLeft, serif: true, wrap: true, shadow: false);
                }
                GUI.EndScrollView();
                return;
            }
            var inner = new Rect(0, 0, view.width - 18, Mathf.Max(view.height, evo.history.Count * 30));
            historyScroll = GUI.BeginScrollView(view, historyScroll, inner, GUIStyle.none, GUI.skin.verticalScrollbar);
            if (evo.history.Count == 0)
                UIKit.Text(new Rect(0, 20, inner.width, 40), "아직 기록이 없다.", 16, UITheme.Ash, TextAnchor.MiddleCenter, serif: true);
            for (int i = 0; i < evo.history.Count; i++)
                UIKit.Text(new Rect(0, i * 30, inner.width, 28), evo.history[i], 16, UITheme.Bone, serif: true);
            GUI.EndScrollView();
        }
    }
}

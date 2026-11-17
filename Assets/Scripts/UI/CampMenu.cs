using Hunt.Combat;
using Hunt.Core;
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
        enum Tab { Stats, Arms, Research }
        Tab tab = Tab.Stats;
        int weaponIdx;
        Vector2 invScroll, historyScroll;
        StatType? hoverStat;
        readonly RadarChart radar = new RadarChart();
        float appear;

        public void OnOpen() { appear = 0f; }

        public void Draw(GameManager gm, float W, float H)
        {
            var p = gm.Player;
            if (p == null) return;
            if (UIKit.IsRepaint) appear = Mathf.MoveTowards(appear, 1f, Time.unscaledDeltaTime * 4f);
            UIKit.Alpha = appear;
            hoverStat = null;

            UIKit.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.01f, 0.015f, 0.6f));
            UIKit.Vignette(W, H, new Color(0, 0, 0, 0.8f));

            float pw = Mathf.Min(1560f, W - 60f);
            var win = new Rect(W / 2 - pw / 2, 96 + (1f - appear) * 20f, pw, 900);
            UIKit.Panel(win);

            // 제목
            UIKit.Text(new Rect(win.x, win.y + 14, win.width, 46), "야  영  지", 34, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
            UIKit.Text(new Rect(win.x, win.y + 56, win.width, 24), $"날이 밝았다. 망자들은 그늘로 숨었다.  ·  다음 밤까지 {gm.IntermissionTime:0}초", 16, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true);
            UIKit.Divider(new Rect(win.x + 200, win.y + 86, win.width - 400, 10));

            // 탭
            string[] names =
            {
                p.StatPoints > 0 || p.PendingTraitPicks > 0 ? "능력치 · 계약  ◆" : "능력치 · 계약",
                "무기 · 파츠",
                gm.LastEvolution.Count > 0 ? "역병 연구  ◆" : "역병 연구",
            };
            float tw = 260;
            float tx = win.center.x - (tw * 3 + 20) / 2;
            for (int i = 0; i < 3; i++)
                if (UIKit.Button(new Rect(tx + i * (tw + 10), win.y + 104, tw, 48), names[i], 20, (int)tab == i)) tab = (Tab)i;

            var content = new Rect(win.x + 26, win.y + 168, win.width - 52, win.height - 260);
            switch (tab)
            {
                case Tab.Stats: DrawStats(p, content); break;
                case Tab.Arms: DrawArms(p, content); break;
                default: DrawResearch(gm, content); break;
            }

            // 아래
            float by = win.yMax - 78;
            if (UIKit.Button(new Rect(win.center.x - 190, by, 380, 60), "밤을 맞이한다", 26, false, true, "Enter")) gm.StartNextWave();
            UIKit.Text(new Rect(win.x + 30, by, 400, 60), "Tab — 야영지를 닫고 마을을 둘러본다", 15, UITheme.Ash);
            UIKit.Text(new Rect(win.xMax - 430, by, 400, 60), $"엽전 <color=#{UIKit.Hex(UITheme.GoldBright)}>{p.Gold}</color>   ·   청심환 ×{p.Infection.Antidotes}", 18, UITheme.BoneDim, TextAnchor.MiddleRight, serif: true);
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
            UIKit.Text(new Rect(left.x + 24, left.y + 12, 400, 34), $"<b>Lv.{p.Level}</b>  각인", 22, UITheme.Bone, serif: true);
            UIKit.Text(new Rect(left.xMax - 300, left.y + 12, 276, 34), $"남은 각인  <color=#{UIKit.Hex(p.StatPoints > 0 ? UITheme.GoldBright : UITheme.Ash)}>{p.StatPoints}</color>", 22, UITheme.BoneDim, TextAnchor.MiddleRight, serif: true);

            float y = left.y + 58;
            float rowH = (left.height - 70) / 6f;
            foreach (var t in CharacterStats.All)
            {
                var row = new Rect(left.x + 14, y, left.width - 28, rowH - 6);
                UIKit.Tex(new Rect(row.x, row.yMax, row.width, 2), UITheme.LineFade, new Color(0.5f, 0.38f, 0.2f, 0.35f));
                UIKit.Emblem(new Rect(row.x + 8, row.center.y - 24, 48, 48), GameText.StatGlyph(t), UITheme.Gold);
                UIKit.Text(new Rect(row.x + 70, row.y + 4, 150, 30), GameText.StatName(t), 22, UITheme.Bone, serif: true, bold: true);
                UIKit.Text(new Rect(row.x + 70, row.y + 34, row.width - 200, 22), GameText.StatEffects(t, s), 14, UITheme.BoneDim);

                var plus = new Rect(row.xMax - 52, row.center.y - 20, 40, 40);
                if (UIKit.IconButton(plus, "+", p.StatPoints > 0)) p.SpendStatPoint(t);
                if (plus.Contains(UIKit.Mouse) && p.StatPoints > 0) hoverStat = t;

                UIKit.Text(new Rect(row.xMax - 140, row.y, 76, row.height), s.Get(t).ToString("0"), 32, s.Allocated(t) > 0 ? UITheme.GoldBright : UITheme.Bone, TextAnchor.MiddleRight, serif: true, bold: true);
                UIKit.TooltipIf(new Rect(row.x, row.y, 260, row.height), GameText.StatTooltip(t));
                y += rowH;
            }

            // 오른쪽: 차트 + 계약
            UIKit.NineSlice(right, UITheme.Slot, 10, Color.white);
            var vals = new float[6];
            float[] prev = hoverStat.HasValue ? new float[6] : null;
            var labels = new string[6];
            float maxV = 12f;
            foreach (var t in CharacterStats.All) maxV = Mathf.Max(maxV, s.Get(t) + 2f);
            for (int i = 0; i < 6; i++)
            {
                var t = (StatType)i;
                vals[i] = s.Get(t) / maxV;
                if (prev != null) prev[i] = (s.Get(t) + (hoverStat.Value == t ? 1 : 0)) / maxV;
                labels[i] = GameText.StatGlyph(t);
            }
            radar.Draw(new Rect(right.center.x - 100, right.y + 46, 200, 200), vals, prev, labels);

            float ty = right.y + 290;
            UIKit.Divider(new Rect(right.x + 20, ty, right.width - 40, 10));
            ty += 16;
            UIKit.Text(new Rect(right.x + 20, ty, right.width - 40, 28), "맺은 계약", 20, UITheme.Gold, TextAnchor.MiddleCenter, serif: true, bold: true);
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
                UIKit.Text(new Rect(right.x + 20, ty, right.width - 40, 28), $"새 계약을 맺을 수 있다  ({p.PendingTraitPicks})", 18, UITheme.GoldBright, TextAnchor.MiddleCenter, serif: true, bold: true);
                ty += 34;
                float cw = (right.width - 40 - 20) / 3f;
                for (int i = 0; i < p.TraitOffer.Count; i++)
                {
                    var t = p.TraitOffer[i];
                    var r = new Rect(right.x + 20 + i * (cw + 10), ty, cw, right.yMax - ty - 16);
                    bool click = UIKit.HitArea(r, "offer" + t, out float hv);
                    r.y -= hv * 4f;
                    UIKit.NineSlice(r, hv > 0.5f ? UITheme.PanelHeader : UITheme.Panel, 12, Color.white);
                    UIKit.NineSlice(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), UITheme.Frame, 8, new Color(1f, 0.8f, 0.45f, 0.3f + hv * 0.6f));
                    UIKit.Emblem(new Rect(r.center.x - 26, r.y + 14, 52, 52), GameText.TraitGlyph(t), UITheme.Gold);
                    UIKit.Text(new Rect(r.x + 8, r.y + 72, r.width - 16, 28), Traits.Name(t), 19, UITheme.Bone, TextAnchor.MiddleCenter, serif: true, bold: true);
                    UIKit.Text(new Rect(r.x + 10, r.y + 102, r.width - 20, r.height - 110), Traits.Describe(t), 14, UITheme.BoneDim, TextAnchor.UpperCenter, wrap: true);
                    if (click) { p.PickTrait(t); break; }
                }
            }
            else
            {
                int next = (p.Level / Traits.PickEveryLevels + 1) * Traits.PickEveryLevels;
                UIKit.Text(new Rect(right.x + 20, ty, right.width - 40, 28), $"다음 계약 — Lv.{next}", 17, UITheme.Ash, TextAnchor.MiddleCenter, serif: true);
            }
        }

        // ------------------------------------------------------------------ 무기 · 파츠
        static string SlotGlyph(PartSlot s)
        {
            switch (s)
            {
                case PartSlot.Barrel: return "銃";
                case PartSlot.Magazine: return "彈";
                case PartSlot.Stock: return "托";
                case PartSlot.Scope: return "照";
                case PartSlot.Muzzle: return "口";
                default: return "符";
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
            bool canUp = w.upgradeLevel < WeaponInstance.MaxUpgrade && p.Gold >= w.UpgradeCost;
            string upLabel = w.upgradeLevel >= WeaponInstance.MaxUpgrade ? "더는 벼릴 수 없다" : $"벼리기  +{w.upgradeLevel + 1}";
            if (UIKit.Button(new Rect(left.x + 16, y, left.width - 32, 62), upLabel, 22, false, canUp,
                    w.upgradeLevel >= WeaponInstance.MaxUpgrade ? null : $"엽전 {w.UpgradeCost}  ·  위력 +8%"))
            {
                if (p.SpendGold(w.UpgradeCost))
                {
                    w.upgradeLevel++;
                    SFX.Play(SFX.Rack, 0.9f, 0f);
                }
            }

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
                "부착물은 총의 모양에도 드러난다.\n특수 모듈(符)은 탄환에 오행의 기운을 싣는다.", 14, UITheme.BoneDim, TextAnchor.MiddleCenter, serif: true, wrap: true);

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

            // 기록
            UIKit.Text(new Rect(right.x, right.y + 16, right.width, 30), "역병 일지", 22, UITheme.Gold, TextAnchor.MiddleCenter, serif: true, bold: true);
            var view = new Rect(right.x + 20, right.y + 60, right.width - 30, right.height - 76);
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

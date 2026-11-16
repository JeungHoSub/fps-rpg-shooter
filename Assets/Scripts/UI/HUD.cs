using System.Collections.Generic;
using Hunt.Combat;
using Hunt.Core;
using Hunt.Data;
using Hunt.Evolution;
using Hunt.Monsters;
using Hunt.Player;
using Hunt.Stats;
using Hunt.Weapons;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// 프로토타입 UI (IMGUI). 프리팹/캔버스 없이 동작하도록 OnGUI로 그린다.
    /// 정식 UI는 이후 UGUI/UI Toolkit으로 교체.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        const float RefHeight = 1080f;

        struct Note { public string text; public Color color; public float time; }
        static readonly List<Note> notes = new List<Note>();

        public static void Notify(string text, Color color)
        {
            notes.Add(new Note { text = text, color = color, time = 4f });
            if (notes.Count > 7) notes.RemoveAt(0);
        }

        enum Tab { Stats, Weapons, Evolution }
        Tab tab = Tab.Stats;
        int weaponTab;
        Vector2 invScroll, evoScroll;

        float W, H, scale;
        GUIStyle label, labelC, labelR, big, title, small, box, button;

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                var n = notes[i];
                n.time -= dt;
                if (n.time <= 0f) notes.RemoveAt(i); else notes[i] = n;
            }
            DamageNumbers.Tick(dt);
        }

        void InitStyles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
            label.normal.textColor = Color.white;
            labelC = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            labelR = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
            small = new GUIStyle(label) { fontSize = 16 };
            big = new GUIStyle(labelC) { fontSize = 34, fontStyle = FontStyle.Bold };
            title = new GUIStyle(labelC) { fontSize = 64, fontStyle = FontStyle.Bold };
            box = new GUIStyle(GUI.skin.box);
            button = new GUIStyle(GUI.skin.button) { fontSize = 18, richText = true, wordWrap = true };
        }

        void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            InitStyles();

            scale = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            W = Screen.width / scale;
            H = RefHeight;

            switch (gm.State)
            {
                case GameState.CharacterSelect:
                    DrawSelect(gm);
                    break;
                case GameState.Playing:
                case GameState.Intermission:
                    DrawHud(gm);
                    if (gm.State == GameState.Intermission) DrawIntermission(gm);
                    break;
                case GameState.GameOver:
                    DrawHud(gm);
                    DrawGameOver(gm);
                    break;
            }
            if (gm.Paused)
            {
                Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
                GUI.Label(new Rect(0, H / 2 - 40, W, 80), "일시정지 — Esc로 재개", big);
            }
            DrawNotes();
        }

        // ================================================================ 시작 화면 (직업 없음: 스탯 분배 + 시작 특성 + 무기)
        void DrawSelect(GameManager gm)
        {
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
            GUI.Label(new Rect(0, 40, W, 90), "PROJECT: HUNT", title);
            GUI.Label(new Rect(0, 125, W, 40), $"{gm.Character.displayName} — {gm.Character.description}", labelC);

            var preview = gm.PreviewStats();

            // 스탯 분배
            var lp = new Rect(W / 2 - 620, 175, 600, 420);
            Fill(lp, new Color(0.1f, 0.1f, 0.12f, 0.85f));
            GUI.Label(new Rect(lp.x + 20, lp.y + 12, 560, 34), $"<b>시작 스탯 분배</b>   남은 포인트 <color=#ffd966>{gm.StartPointsLeft}</color>", label);
            float y = lp.y + 60;
            foreach (var t in CharacterStats.All)
            {
                float v = preview.Get(t);
                GUI.Label(new Rect(lp.x + 20, y, 80, 30), $"<b>{StatName(t)}</b>", label);
                Fill(new Rect(lp.x + 100, y + 10, 200, 12), new Color(1, 1, 1, 0.1f));
                Fill(new Rect(lp.x + 100, y + 10, 200 * Mathf.Clamp01(v / 20f), 12), StatColor(t));
                GUI.Label(new Rect(lp.x + 310, y, 40, 30), v.ToString("0"), label);
                GUI.enabled = gm.StartAllocation[(int)t] > 0;
                if (GUI.Button(new Rect(lp.x + 355, y + 2, 34, 28), "-", button)) gm.AdjustStartStat(t, -1);
                GUI.enabled = gm.StartPointsLeft > 0;
                if (GUI.Button(new Rect(lp.x + 393, y + 2, 34, 28), "+", button)) gm.AdjustStartStat(t, +1);
                GUI.enabled = true;
                GUI.Label(new Rect(lp.x + 440, y, 150, 30), $"<color=#999>{StatDesc(t)}</color>", new GUIStyle(small) { fontSize = 13, wordWrap = true });
                y += 56;
            }

            // 시작 특성
            var rp = new Rect(W / 2 + 20, 175, 600, 420);
            Fill(rp, new Color(0.1f, 0.1f, 0.12f, 0.85f));
            GUI.Label(new Rect(rp.x + 20, rp.y + 12, 560, 34), $"<b>시작 특성</b> (1개)   <color=#aaa>이후 {Traits.PickEveryLevels}레벨마다 하나씩 추가</color>", label);
            var all = Traits.All;
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                var r = new Rect(rp.x + 20 + (i % 2) * 285, rp.y + 60 + (i / 2) * 88, 275, 80);
                GUI.color = gm.StartTrait == t ? new Color(0.5f, 0.9f, 1f) : Color.white;
                if (GUI.Button(r, TraitText(t), button)) gm.StartTrait = t;
            }
            GUI.color = Color.white;

            // 무기 선택
            float wy = 620;
            GUI.Label(new Rect(0, wy, W, 30), $"주무기 선택 (최대 {GameManager.MaxPrimaries}개) + 권총 기본 지급", labelC);
            var weapons = DefaultContent.Weapons;
            float bw = 210, bh = 120;
            int count = weapons.Count - 1;
            float bx0 = W / 2 - (count * bw + (count - 1) * 12) / 2;
            for (int i = 1; i < weapons.Count; i++)
            {
                var w = weapons[i];
                bool on = gm.SelectedPrimaries.Contains(w);
                var r = new Rect(bx0 + (i - 1) * (bw + 12), wy + 40, bw, bh);
                GUI.color = on ? new Color(0.6f, 1f, 0.7f) : Color.white;
                string txt = $"<b>{w.displayName}</b>\n<size=14>{w.damage}{(w.pellets > 1 ? "×" + w.pellets : "")} 데미지 · {w.rpm} RPM\n" +
                             $"탄창 {w.magazine} · 무게 {w.weight}\n<color=#{ColorUtility.ToHtmlStringRGB(CombatNames.ColorOf(w.damageType))}>{CombatNames.Of(w.damageType)}</color></size>";
                if (GUI.Button(r, txt, button)) gm.ToggleWeapon(w);
                GUI.color = Color.white;
            }

            float weight = DefaultContent.Pistol.weight;
            foreach (var w in gm.SelectedPrimaries) weight += w.weight;
            float pen = preview.WeightPenalty(weight);
            string wcol = pen < 1f ? "#ff7777" : "#88ff88";
            GUI.Label(new Rect(0, wy + 170, W, 30),
                $"장비 무게 <color={wcol}>{weight:0.0} / {preview.CarryCapacity:0.0}</color>" + (pen < 1f ? $"  (과적: 이동·조준 속도 {Mathf.RoundToInt((1 - pen) * 100)}% 감소)" : ""), labelC);

            GUI.enabled = gm.StartPointsLeft == 0;
            if (GUI.Button(new Rect(W / 2 - 150, wy + 215, 300, 64), gm.StartPointsLeft == 0 ? "<size=28><b>출격</b></size>" : "<size=20>스탯 포인트를 모두 분배하세요</size>", button)) gm.StartGame();
            GUI.enabled = true;

            GUI.Label(new Rect(0, H - 70, W, 60),
                "WASD 이동 · Shift 달리기 · Space 점프 · 좌클릭 사격 · 우클릭 조준 · R 재장전 · 1/2/3 무기 교체\n" +
                "Q 변이:광폭화 · E 변이:재생 · H 해독제 · Tab 정비 메뉴(웨이브 사이) · Enter 다음 웨이브 · Esc 일시정지", Wrap(new GUIStyle(small) { alignment = TextAnchor.MiddleCenter }));
        }

        // ================================================================ 인게임 HUD
        void DrawHud(GameManager gm)
        {
            var p = gm.Player;
            if (p == null) return;
            var cam = gm.ActiveCamera;

            DrawDamageNumbers(cam);

            if (p.HurtFlash > 0f) Fill(new Rect(0, 0, W, H), new Color(0.8f, 0f, 0f, p.HurtFlash * 0.8f));
            if (p.Infection.IsFrenzy) Fill(new Rect(0, 0, W, H), new Color(0.5f, 0f, 0.6f, 0.18f + Mathf.PingPong(Time.time, 0.5f) * 0.2f));

            var w = p.Weapons.Current;

            // 조준선: 실제 탄퍼짐 각도를 화면 픽셀로 변환
            if (gm.State != GameState.GameOver && !gm.MenuOpen)
            {
                float fov = cam != null ? cam.fieldOfView : 75f;
                float gapPx = Mathf.Tan(p.Weapons.CurrentSpread * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * (H / 2f);
                gapPx = Mathf.Clamp(gapPx, 3f, 300f);
                Color cc = p.Weapons.IsAiming && w.data.weaponClass == WeaponClass.Sniper ? new Color(1, 0.3f, 0.3f) : new Color(0.6f, 1f, 0.6f);
                float cx = W / 2, cy = H / 2, len = 12;
                Fill(new Rect(cx - 1, cy - gapPx - len, 2, len), cc);
                Fill(new Rect(cx - 1, cy + gapPx, 2, len), cc);
                Fill(new Rect(cx - gapPx - len, cy - 1, len, 2), cc);
                Fill(new Rect(cx + gapPx, cy - 1, len, 2), cc);
                Fill(new Rect(cx - 1, cy - 1, 2, 2), cc);

                if (p.Weapons.HitMarker > 0f)
                {
                    Color hc = p.Weapons.LastHitKill ? Color.red : p.Weapons.LastHitWeak ? new Color(1f, 0.6f, 0.1f) : Color.white;
                    GUI.Label(new Rect(cx - 30, cy - 30, 60, 60), "<size=34>X</size>", new GUIStyle(labelC) { normal = { textColor = hc } });
                }
                if (p.Weapons.IsReloading)
                {
                    Fill(new Rect(cx - 60, cy + 40, 120, 6), new Color(0, 0, 0, 0.5f));
                    Fill(new Rect(cx - 60, cy + 40, 120 * p.Weapons.ReloadProgress, 6), Color.white);
                }
            }

            // 좌하단: HP / 감염
            float bx = 30, by = H - 150;
            GUI.Label(new Rect(bx, by - 34, 400, 30), $"<b>{p.Stats.data.displayName}</b>  Lv.{p.Level}", label);
            Bar(new Rect(bx, by, 380, 26), p.Hp / p.Stats.MaxHp, new Color(0.85f, 0.2f, 0.2f), $"HP {p.Hp:0} / {p.Stats.MaxHp:0}");

            var inf = p.Infection;
            Color infCol = inf.IsFrenzy ? new Color(0.9f, 0.2f, 1f) : new Color(0.55f, 0.2f, 0.8f);
            Bar(new Rect(bx, by + 34, 380, 20), inf.Gauge / InfectionSystem.Max, infCol,
                inf.IsFrenzy ? $"폭주! {inf.FrenzyTime:0.0}s" : $"감염 {inf.Gauge:0}%  (데미지 +{inf.Gauge / InfectionSystem.Max * 20f:0}%)");
            // 능력 비용 눈금
            Fill(new Rect(bx + 380 * inf.RegenCostFinal / 100f, by + 34, 2, 20), new Color(0.4f, 1f, 0.5f));
            Fill(new Rect(bx + 380 * inf.RageCostFinal / 100f, by + 34, 2, 20), new Color(1f, 0.4f, 0.3f));

            string rage = inf.IsRage ? $"<color=#ff6655>광폭화 {inf.RageTime:0.0}s</color>" : $"[Q] 광폭화 {inf.RageCostFinal:0}";
            string regen = inf.IsRegen ? "<color=#66ff88>재생 중</color>" : $"[E] 재생 {inf.RegenCostFinal:0}";
            GUI.Label(new Rect(bx, by + 60, 520, 26), $"{rage}   {regen}   [H] 해독제 ×{inf.Antidotes}", small);

            Bar(new Rect(bx, by + 92, 380, 10), (float)p.Xp / p.XpToNext, new Color(1f, 0.85f, 0.3f), "");
            GUI.Label(new Rect(bx, by + 104, 500, 26), $"EXP {p.Xp}/{p.XpToNext}   골드 {p.Gold}   처치 {p.Kills}" + (p.StatPoints > 0 ? $"   <color=#ffd966>스탯 포인트 {p.StatPoints}</color>" : ""), small);

            // 우하단: 탄약 / 무기 목록
            float rx = W - 430;
            GUI.Label(new Rect(rx, H - 150, 400, 40), $"<size=24><b>{w.DisplayName}</b></size>", labelR);
            string ammoCol = w.ammoInMag <= w.MagazineSize * 0.25f ? "#ff6666" : "#ffffff";
            GUI.Label(new Rect(rx, H - 112, 400, 56), $"<size=46><color={ammoCol}><b>{w.ammoInMag}</b></color></size><size=24> / {w.reserve}</size>", labelR);
            var wp = p.Weapons;
            string list = "";
            for (int i = 0; i < wp.Slots.Count; i++)
                list += (i == wp.CurrentIndex ? "<b><color=#ffd966>" : "<color=#999>") + $"[{i + 1}] {wp.Slots[i].data.displayName}" + (i == wp.CurrentIndex ? "</color></b>" : "</color>") + "   ";
            GUI.Label(new Rect(rx - 300, H - 52, 700, 30), list, new GUIStyle(small) { alignment = TextAnchor.MiddleRight, richText = true });
            string typeHex = ColorUtility.ToHtmlStringRGB(CombatNames.ColorOf(w.DamageType));
            float wpen = p.Controller.WeightPenalty;
            GUI.Label(new Rect(rx, H - 190, 400, 30), $"<color=#{typeHex}>{CombatNames.Of(w.DamageType)}</color>" + (wpen < 1f ? $"  <color=#ff7777>과적 -{Mathf.RoundToInt((1 - wpen) * 100)}%</color>" : ""), new GUIStyle(small) { alignment = TextAnchor.MiddleRight });

            // 상단: 웨이브 정보
            var waves = gm.Waves;
            string waveTxt = gm.State == GameState.Intermission
                ? $"정비 시간 {gm.IntermissionTime:0}s  —  Enter: 다음 웨이브 · Tab: 메뉴"
                : $"웨이브 {waves.Wave}{(waves.IsBossWave ? " (보스)" : "")}   남은 괴물 {waves.AliveCount + waves.RemainingToSpawn}";
            GUI.Label(new Rect(0, 16, W, 36), $"<size=26><b>{waveTxt}</b></size>", labelC);
            var evo = gm.Evolution.Profile;
            if (evo.generation > 0)
                GUI.Label(new Rect(0, 52, W, 28), $"<color=#cc77ff>진화 {evo.generation}세대</color>", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            // 보스 체력
            Monster boss = null;
            foreach (var m in Monster.Alive) if (m.IsBoss) { boss = m; break; }
            if (boss != null)
            {
                var r = new Rect(W / 2 - 400, 90, 800, 24);
                Bar(r, boss.Hp / boss.MaxHp, new Color(0.7f, 0.15f, 0.5f), $"{boss.Data.displayName}  —  페이즈 {boss.BossPhase + 1}/3");
                Fill(new Rect(r.x + r.width * 0.33f, r.y, 2, r.height), Color.black);
                Fill(new Rect(r.x + r.width * 0.66f, r.y, 2, r.height), Color.black);
            }
        }

        void DrawDamageNumbers(Camera cam)
        {
            if (cam == null) return;
            foreach (var e in DamageNumbers.Entries)
            {
                Vector3 sp = cam.WorldToScreenPoint(e.pos);
                if (sp.z <= 0f) continue;
                float x = sp.x / scale, y = (Screen.height - sp.y) / scale;
                float a = 1f - e.t / DamageNumbers.Life;
                Color c = e.zone == HitZone.WeakPoint ? new Color(1f, 0.55f, 0.1f)
                    : e.zone == HitZone.Head ? new Color(1f, 0.95f, 0.3f)
                    : e.zone == HitZone.Armor ? new Color(0.6f, 0.6f, 0.6f)
                    : e.type != DamageType.Physical ? CombatNames.ColorOf(e.type) : Color.white;
                c.a = a;
                int size = e.crit ? 30 : e.zone == HitZone.WeakPoint ? 26 : 20;
                var st = new GUIStyle(labelC) { fontSize = size, fontStyle = e.crit ? FontStyle.Bold : FontStyle.Normal };
                st.normal.textColor = new Color(0, 0, 0, a * 0.8f);
                string s = Mathf.RoundToInt(e.amount) + (e.crit ? "!" : "");
                GUI.Label(new Rect(x - 59, y - 19, 120, 40), s, st);
                st.normal.textColor = c;
                GUI.Label(new Rect(x - 60, y - 20, 120, 40), s, st);
            }
        }

        // ================================================================ 정비 메뉴 (웨이브 사이)
        void DrawIntermission(GameManager gm)
        {
            if (!gm.MenuOpen) return;
            var p = gm.Player;
            float pw = Mathf.Min(1300, W - 80), ph = 760;
            var panel = new Rect(W / 2 - pw / 2, 130, pw, ph);
            Fill(panel, new Color(0.06f, 0.06f, 0.08f, 0.94f));

            bool pending = p.StatPoints > 0 || p.PendingTraitPicks > 0;
            string[] names = { pending ? "스탯 · 특성 (!)" : "스탯 · 특성", "무기 · 파츠", "진화 정보" };
            for (int i = 0; i < 3; i++)
            {
                GUI.color = (int)tab == i ? new Color(1f, 0.9f, 0.5f) : Color.white;
                if (GUI.Button(new Rect(panel.x + 20 + i * 210, panel.y + 16, 200, 44), names[i], button)) tab = (Tab)i;
            }
            GUI.color = Color.white;
            if (GUI.Button(new Rect(panel.xMax - 260, panel.y + 16, 240, 44), $"다음 웨이브 (Enter) {gm.IntermissionTime:0}s", button)) gm.StartNextWave();

            var content = new Rect(panel.x + 20, panel.y + 80, panel.width - 40, panel.height - 100);
            switch (tab)
            {
                case Tab.Stats: DrawStatsTab(p, content); break;
                case Tab.Weapons: DrawWeaponsTab(p, content); break;
                case Tab.Evolution: DrawEvolutionTab(gm, content); break;
            }
        }

        void DrawStatsTab(PlayerCharacter p, Rect r)
        {
            var s = p.Stats;
            GUI.Label(new Rect(r.x, r.y, 600, 30), $"<b>Lv.{p.Level} {s.data.displayName}</b>   남은 포인트: <color=#ffd966>{p.StatPoints}</color>", label);
            float y = r.y + 50;
            foreach (var t in CharacterStats.All)
            {
                GUI.Label(new Rect(r.x, y, 140, 36), $"<b>{StatName(t)}</b>", label);
                GUI.Label(new Rect(r.x + 140, y, 60, 36), s.Get(t).ToString("0"), label);
                GUI.enabled = p.StatPoints > 0;
                if (GUI.Button(new Rect(r.x + 200, y + 2, 40, 32), "+", button)) p.SpendStatPoint(t);
                GUI.enabled = true;
                GUI.Label(new Rect(r.x + 260, y, 360, 36), $"<color=#aaa>{StatDesc(t)}</color>", small);
                y += 46;
            }

            float x2 = r.x + 660;
            GUI.Label(new Rect(x2, r.y, 560, 30), "<b>파생 능력치</b>", label);
            string d =
                $"최대 HP            {s.MaxHp:0}\n" +
                $"이동 속도          {s.MoveSpeed:0.00} m/s\n" +
                $"소지 무게          {p.Weapons.TotalWeight:0.0} / {s.CarryCapacity:0.0}\n" +
                $"재장전 속도        ×{s.ReloadSpeedMult:0.00}\n" +
                $"반동 패턴 크기     ×{s.RecoilPatternScale:0.00}\n" +
                $"반동 무작위성      ×{s.RecoilNoise:0.00}  (낮을수록 예측 가능)\n" +
                $"반동 회복          {s.RecoilRecovery:0.0}°/s\n" +
                $"탄퍼짐             ×{s.SpreadMult:0.00}\n" +
                $"치명타             {s.CritChance * 100:0.0}%  ×{s.CritMultiplier:0.00}\n" +
                $"약점 추가 배율     +{s.WeakPointBonus * 100:0}%\n" +
                $"감염 저항          ×{s.InfectionResistMult:0.00}\n" +
                $"변이 비용          ×{s.AbilityCostMult:0.00}   지속 ×{s.AbilityDurationMult:0.00}";
            GUI.Label(new Rect(x2, r.y + 40, 580, 320), d, Wrap(small));

            // 특성
            float ty = r.y + 360;
            string owned = "";
            foreach (var t in s.traits) owned += (owned.Length > 0 ? ", " : "") + Traits.Name(t);
            GUI.Label(new Rect(x2, ty, 580, 50), $"<b>보유 특성</b>  <color=#7fd8ff>{owned}</color>", Wrap(small));
            ty += 50;

            p.EnsureTraitOffer();
            if (p.PendingTraitPicks > 0)
            {
                GUI.Label(new Rect(x2, ty, 580, 30), $"<color=#ffd966><b>새 특성 선택</b> ({p.PendingTraitPicks}회 남음)</color>", label);
                ty += 34;
                for (int i = 0; i < p.TraitOffer.Count; i++)
                {
                    var t = p.TraitOffer[i];
                    if (GUI.Button(new Rect(x2 + i * 190, ty, 180, 100), TraitText(t), button))
                    {
                        p.PickTrait(t);
                        break;
                    }
                }
            }
            else
            {
                int next = (p.Level / Traits.PickEveryLevels + 1) * Traits.PickEveryLevels;
                GUI.Label(new Rect(x2, ty, 580, 30), $"<color=#888>다음 특성 선택: Lv.{next}</color>", small);
            }
        }

        static string TraitText(Trait t) => $"<b>{Traits.Name(t)}</b>\n<size=14>{Traits.Describe(t)}</size>";

        void DrawWeaponsTab(PlayerCharacter p, Rect r)
        {
            var slots = p.Weapons.Slots;
            weaponTab = Mathf.Clamp(weaponTab, 0, slots.Count - 1);
            for (int i = 0; i < slots.Count; i++)
            {
                GUI.color = weaponTab == i ? new Color(0.6f, 1f, 0.7f) : Color.white;
                if (GUI.Button(new Rect(r.x + i * 250, r.y, 240, 40), slots[i].DisplayName, button)) weaponTab = i;
            }
            GUI.color = Color.white;
            var w = slots[weaponTab];

            // 무기 상세
            float y = r.y + 56;
            string typeHex = ColorUtility.ToHtmlStringRGB(CombatNames.ColorOf(w.DamageType));
            string info =
                $"데미지 {w.Damage:0.0}{(w.data.pellets > 1 ? " ×" + w.data.pellets : "")}   <color=#{typeHex}>{CombatNames.Of(w.DamageType)}</color>\n" +
                $"연사 {60f / w.FireInterval:0} RPM   탄창 {w.MagazineSize}   재장전 ×{w.ReloadSpeedMult:0.00}\n" +
                $"반동 ×{w.RecoilMult:0.00}   탄퍼짐 ×{w.SpreadMult:0.00}   저지력 ×{w.KnockbackMult:0.00}\n" +
                $"치명타 +{w.CritChanceBonus * 100:0}%   약점 +{w.WeakPointBonus * 100:0}%   무게 {w.Weight:0.0}";
            GUI.Label(new Rect(r.x, y, 600, 120), info, Wrap(small));

            GUI.enabled = w.upgradeLevel < WeaponInstance.MaxUpgrade && p.Gold >= w.UpgradeCost;
            if (GUI.Button(new Rect(r.x, y + 120, 300, 40), w.upgradeLevel >= WeaponInstance.MaxUpgrade ? "최대 강화" : $"강화 +{w.upgradeLevel + 1}  ({w.UpgradeCost} 골드)", button))
            {
                if (p.SpendGold(w.UpgradeCost)) w.upgradeLevel++;
            }
            GUI.enabled = true;
            GUI.Label(new Rect(r.x + 320, y + 125, 300, 30), $"보유 골드 {p.Gold}", small);

            // 장착 파츠
            y += 180;
            GUI.Label(new Rect(r.x, y, 600, 30), "<b>장착 파츠</b>", label);
            y += 34;
            foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
            {
                w.parts.TryGetValue(slot, out var part);
                string txt = part == null ? "<color=#666>— 비어 있음 —</color>" : PartText(part, true);
                GUI.Label(new Rect(r.x, y, 110, 60), $"<color=#aaa>{WeaponPart.SlotName(slot)}</color>", small);
                GUI.Label(new Rect(r.x + 110, y, 500, 60), txt, Wrap(small));
                if (part != null && GUI.Button(new Rect(r.x + 560, y, 50, 26), "해제", button))
                {
                    w.parts.Remove(slot);
                    p.Inventory.Add(part);
                }
                y += Mathf.Max(30, 20 * (part != null ? part.affixes.Count + 1 : 1)) + 4;
            }

            // 인벤토리
            float ix = r.x + 650, iw = r.width - 650;
            GUI.Label(new Rect(ix, r.y + 56, iw, 30), $"<b>파츠 인벤토리</b> ({p.Inventory.Count})", label);
            var view = new Rect(ix, r.y + 90, iw, r.height - 100);
            float rowH = 118;
            invScroll = GUI.BeginScrollView(view, invScroll, new Rect(0, 0, iw - 20, Mathf.Max(view.height, p.Inventory.Count * rowH)));
            for (int i = 0; i < p.Inventory.Count; i++)
            {
                var part = p.Inventory[i];
                var row = new Rect(0, i * rowH, iw - 24, rowH - 6);
                Fill(row, new Color(1, 1, 1, 0.05f));
                GUI.Label(new Rect(row.x + 8, row.y + 4, row.width - 120, rowH - 10), $"<color=#aaa>{WeaponPart.SlotName(part.slot)}</color> " + PartText(part, false), Wrap(small));
                if (GUI.Button(new Rect(row.xMax - 100, row.y + 8, 92, 36), "장착", button))
                {
                    p.Inventory.RemoveAt(i);
                    var old = w.Equip(part);
                    if (old != null) p.Inventory.Add(old);
                    break;
                }
                int sell = 15 * (1 + (int)part.rarity) * (1 + (int)part.rarity) + part.itemLevel * 2;
                if (GUI.Button(new Rect(row.xMax - 100, row.y + 52, 92, 36), $"분해 +{sell}", button))
                {
                    p.Inventory.RemoveAt(i);
                    p.AddGold(sell);
                    break;
                }
            }
            GUI.EndScrollView();
        }

        void DrawEvolutionTab(GameManager gm, Rect r)
        {
            var evo = gm.Evolution.Profile;
            GUI.Label(new Rect(r.x, r.y, 600, 30), $"<b>괴물 진화 상태 — {evo.generation}세대</b>", label);
            GUI.Label(new Rect(r.x, r.y + 30, 1100, 50),
                "<color=#aaa>괴물은 지난 웨이브에서 가장 많이 당한 방식(속성 / 부위 / 사거리)에 적응한다. 같은 전략만 반복하면 점점 불리해진다. 대신 진화한 괴물은 보상이 늘어난다.</color>", Wrap(small));

            float y = r.y + 90;
            if (gm.LastEvolution.Count > 0)
            {
                GUI.Label(new Rect(r.x, y, 600, 30), "<color=#cc77ff><b>이번 진화</b></color>", label);
                y += 32;
                foreach (var m in gm.LastEvolution) { GUI.Label(new Rect(r.x + 10, y, 600, 26), "• " + m, small); y += 26; }
            }
            else
            {
                GUI.Label(new Rect(r.x, y, 600, 30), "이번 웨이브에는 진화가 일어나지 않았다.", small);
                y += 30;
            }

            y += 20;
            GUI.Label(new Rect(r.x, y, 600, 30), "<b>현재 내성</b>", label);
            y += 32;
            for (int i = 0; i < 5; i++)
            {
                var t = (DamageType)i;
                GUI.Label(new Rect(r.x, y, 100, 24), CombatNames.Of(t), small);
                Bar(new Rect(r.x + 100, y + 4, 300, 16), evo.resist[i] / 0.6f, CombatNames.ColorOf(t), $"{evo.resist[i] * 100:0}%");
                y += 26;
            }
            y += 10;
            string misc =
                $"약점 위치: {EvolutionDirector.LocName(evo.weakPoint)}\n" +
                $"투구 레벨: {evo.helmetLevel}   다리 강화: ×{evo.legHpMult:0.0}\n" +
                $"넉백 저항 +{evo.knockbackResistBonus * 100:0}%   이동속도 +{evo.speedBonus * 100:0}%   방어력 +{evo.defenseBonus:0}\n" +
                $"보상 배율 ×{evo.RewardMultiplier:0.0}   파츠 행운 +{evo.PartLuck * 100:0}%";
            GUI.Label(new Rect(r.x, y, 600, 120), misc, Wrap(small));

            float hx = r.x + 650;
            GUI.Label(new Rect(hx, r.y + 90, 500, 30), "<b>진화 기록</b>", label);
            var view = new Rect(hx, r.y + 124, r.width - 650, r.height - 140);
            evoScroll = GUI.BeginScrollView(view, evoScroll, new Rect(0, 0, view.width - 20, Mathf.Max(view.height, evo.history.Count * 26)));
            for (int i = 0; i < evo.history.Count; i++)
                GUI.Label(new Rect(0, i * 26, view.width - 20, 26), evo.history[i], small);
            GUI.EndScrollView();
        }

        // ================================================================ 게임 오버
        void DrawGameOver(GameManager gm)
        {
            Fill(new Rect(0, 0, W, H), new Color(0.2f, 0f, 0f, 0.6f));
            GUI.Label(new Rect(0, H / 2 - 160, W, 90), "사망", title);
            var p = gm.Player;
            string s = p != null
                ? $"도달 웨이브 {gm.Waves.Wave}   ·   Lv.{p.Level}   ·   처치 {p.Kills}   ·   괴물 진화 {gm.Evolution.Profile.generation}세대"
                : "";
            GUI.Label(new Rect(0, H / 2 - 50, W, 40), s, labelC);
            GUI.Label(new Rect(0, H / 2 + 10, W, 40), "R — 다시 시작", big);
        }

        // ================================================================ 공용
        void DrawNotes()
        {
            float y = 140;
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                var n = notes[i];
                var c = n.color;
                c.a = Mathf.Clamp01(n.time);
                var st = new GUIStyle(labelC) { fontSize = 22 };
                st.normal.textColor = new Color(0, 0, 0, c.a);
                GUI.Label(new Rect(1, y + 1, W, 30), n.text, st);
                st.normal.textColor = c;
                GUI.Label(new Rect(0, y, W, 30), n.text, st);
                y += 30;
            }
        }

        static string PartText(WeaponPart part, bool compact)
        {
            string hex = ColorUtility.ToHtmlStringRGB(WeaponPart.RarityColor(part.rarity));
            string s = $"<color=#{hex}><b>{part.Title}</b></color>";
            foreach (var a in part.affixes) s += "\n  " + a.Describe();
            return s;
        }

        void Bar(Rect r, float t, Color c, string text)
        {
            Fill(r, new Color(0, 0, 0, 0.55f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), c);
            if (!string.IsNullOrEmpty(text))
                GUI.Label(r, text, new GUIStyle(labelC) { fontSize = Mathf.Max(12, Mathf.RoundToInt(r.height * 0.7f)) });
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static GUIStyle Wrap(GUIStyle s) => new GUIStyle(s) { wordWrap = true };

        static string StatName(StatType t)
        {
            switch (t)
            {
                case StatType.STR: return "힘";
                case StatType.AGI: return "민첩";
                case StatType.CTL: return "제어";
                case StatType.VIT: return "체력";
                case StatType.FOC: return "집중";
                default: return "기술";
            }
        }

        static string StatDesc(StatType t)
        {
            switch (t)
            {
                case StatType.STR: return "소지 무게, 저지력";
                case StatType.AGI: return "이동·재장전·교체 속도";
                case StatType.CTL: return "반동 예측성·회복, 탄퍼짐, 조준 속도";
                case StatType.VIT: return "최대 HP, 감염 저항";
                case StatType.FOC: return "치명타, 약점 데미지";
                default: return "변이 능력 비용·지속시간";
            }
        }

        static Color StatColor(StatType t)
        {
            switch (t)
            {
                case StatType.STR: return new Color(0.9f, 0.4f, 0.3f);
                case StatType.AGI: return new Color(0.4f, 0.9f, 0.5f);
                case StatType.CTL: return new Color(0.4f, 0.7f, 1f);
                case StatType.VIT: return new Color(1f, 0.6f, 0.6f);
                case StatType.FOC: return new Color(1f, 0.85f, 0.3f);
                default: return new Color(0.75f, 0.5f, 1f);
            }
        }
    }
}

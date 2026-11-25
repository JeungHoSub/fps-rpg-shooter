using Hunt.Core;
using Hunt.Player;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>
    /// 밤마다 의뢰 하나: 머리로 N마리 · 칼로 3마리 · 보급 상자 모두 · 현상금 망자. 이루면 바로 엽전.
    /// 오른쪽 위에 작게 진행을 보여 준다. GameHud가 밤 시작에 Begin, 매 프레임 Draw.
    /// </summary>
    public static class NightTask
    {
        enum Kind { Head, Melee, Crates, Bounty, Silent, Hidden, Rescue }
        static Kind kind;
        static int goal, baseline, reward;
        static bool active, done;
        static float doneAt = -100f, changedAt;
        static int lastProg = -1;

        public static void Begin(PlayerCharacter p, int wave)
        {
            kind = (Kind)Random.Range(0, 6);
            if (Survivor.PresentThisNight && Random.value < 0.6f) kind = Kind.Rescue; // 다친 사람이 있는 밤엔 자주 '구하라'
            done = false; active = true;
            reward = 40 + 15 * wave;
            switch (kind)
            {
                case Kind.Head: goal = 6 + wave; baseline = p.RunHeadKills; break;
                case Kind.Melee: goal = 3; baseline = p.RunMelee; break;
                case Kind.Crates: goal = 3; baseline = p.RunCrates; break;
                case Kind.Silent: goal = 2; baseline = p.RunSilent; reward += 30; break;
                case Kind.Hidden: goal = 3; baseline = p.RunHidden; reward += 20; break;
                case Kind.Rescue: goal = 1; baseline = 0; reward += 25; break;
                default: goal = 1; baseline = 0; break;
            }
        }

        public static void End() { active = false; }
        /// <summary>새벽 기록용: 이번 밤 의뢰 이름과 이뤘는지.</summary>
        public static string Summary => goal > 0 ? $"의뢰 ({Text})  <b>{(done ? "이뤘다" : "못 이뤘다")}</b>" : null;

        static int Progress(PlayerCharacter p)
        {
            switch (kind)
            {
                case Kind.Head: return p.RunHeadKills - baseline;
                case Kind.Melee: return p.RunMelee - baseline;
                case Kind.Crates: return p.RunCrates - baseline;
                case Kind.Silent: return p.RunSilent - baseline;
                case Kind.Hidden: return p.RunHidden - baseline;
                case Kind.Rescue: return Survivor.SavedThisNight ? 1 : 0;
                default: return p.BountyThisNight ? 1 : 0;
            }
        }

        static string Text => kind == Kind.Head ? $"머리를 맞혀 {goal}마리" : kind == Kind.Melee ? "칼로 3마리" : kind == Kind.Crates ? "보급 상자 셋 모두 열기"
                            : kind == Kind.Silent ? "소리 없이(등 뒤 칼) 2마리" : kind == Kind.Hidden ? "덤불에 숨어 3마리 보내기" : kind == Kind.Rescue ? "다친 마을 사람 구하기" : "현상금 망자 쓰러뜨리기";

        public static void Draw(PlayerCharacter p, float W)
        {
            if (!active || p == null) return;
            int prog = Mathf.Min(goal, Progress(p));
            if (!done && prog >= goal)
            {
                done = true; doneAt = Time.unscaledTime;
                p.AddGold(reward);
                HUD.Notify($"의뢰를 이뤘다 — {Text}  ·  엽전 +{reward}", new Color(1f, 0.85f, 0.45f));
                SFX.PlayPitched(SFX.LevelUp, 0.6f, 1f);
            }
            float a = done ? Mathf.Clamp01(1f - (Time.unscaledTime - doneAt - 4f) / 1f) : 1f; // 이룬 뒤 4초 있다가 사라짐
            // 화면을 덜 어지럽게: 진행이 바뀐 뒤 8초가 지나면 옅게 (다시 바뀌면 또렷하게)
            if (prog != lastProg) { lastProg = prog; changedAt = Time.unscaledTime; }
            if (!done) a *= Mathf.Lerp(1f, 0.4f, Mathf.Clamp01((Time.unscaledTime - changedAt - 8f) / 1.5f));
            if (a <= 0f) return;
            float keep = UIKit.Alpha;
            UIKit.Alpha = keep * a;
            var r = new Rect(W - 330, 214, 310, 54);
            UIKit.Fill(r, new Color(0.02f, 0.02f, 0.02f, 0.6f));
            UIKit.Outline(r, done ? UITheme.GoldBright : new Color(0.45f, 0.42f, 0.36f), 1f);
            UIKit.Text(new Rect(r.x + 12, r.y + 4, r.width - 24, 22), done ? "의뢰 완료" : $"오늘 밤 의뢰  ·  엽전 {reward}", 13, done ? UITheme.GoldBright : UITheme.BoneDim, serif: true, bold: true);
            UIKit.Text(new Rect(r.x + 12, r.y + 26, r.width - 24, 22), $"{Text}   <b>{prog} / {goal}</b>", 15, UITheme.Bone, serif: true);
            UIKit.Alpha = keep;
        }
    }
}

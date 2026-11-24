using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 업적 (판이 끝나도 남는다, PlayerPrefs). 달성하면 위쪽 알림 + 징. 시작 화면 '업적' 단추에서 본다.
    /// </summary>
    public static class Achievements
    {
        public struct Def { public string id, name, desc; public int goal; }

        public static readonly Def[] All =
        {
            new Def { id = "night1",   name = "첫 새벽",       desc = "첫째 밤을 버틴다",                     goal = 1 },
            new Def { id = "daegam",   name = "대감을 눕히다", desc = "다섯째 밤 역병 대감을 쓰러뜨린다",      goal = 1 },
            new Def { id = "night10",  name = "열 밤",         desc = "열째 밤까지 버틴다",                   goal = 1 },
            new Def { id = "streak10", name = "피바람",        desc = "10연속 처치",                          goal = 1 },
            new Def { id = "head100",  name = "머리 사냥",     desc = "머리를 맞혀 망자 100마리를 쓰러뜨린다", goal = 100 },
            new Def { id = "kills1000",name = "천 망자",       desc = "망자 1000마리를 쓰러뜨린다 (모든 판 합계)", goal = 1000 },
            new Def { id = "crates20", name = "보급병",        desc = "보급 상자 20개를 연다",                goal = 20 },
            new Def { id = "lore",     name = "마을의 기억",   desc = "찢어진 쪽지를 모두 읽는다",            goal = 1 },
            new Def { id = "master",   name = "달인",          desc = "무기 하나를 '달인'까지 숙련한다",       goal = 1 },
            new Def { id = "bloat5",   name = "연쇄 폭발",     desc = "터지는 망자 하나로 망자 5마리를 함께 쓰러뜨린다", goal = 1 },
        };

        static string Key(string id) => "hunt.ach." + id;

        public static int Progress(string id) { try { return PlayerPrefs.GetInt(Key(id), 0); } catch { return 0; } }
        public static bool Done(Def d) => Progress(d.id) >= d.goal;

        public static int DoneCount()
        {
            int n = 0;
            foreach (var d in All) if (Done(d)) n++;
            return n;
        }

        /// <summary>진행을 amount만큼 올린다 (목표에 닿는 순간 한 번 알린다).</summary>
        public static void Add(string id, int amount = 1)
        {
            Def def = default; bool found = false;
            foreach (var d in All) if (d.id == id) { def = d; found = true; break; }
            if (!found) return;
            int before = Progress(id);
            if (before >= def.goal) return;
            int now = Mathf.Min(def.goal, before + amount);
            try { PlayerPrefs.SetInt(Key(id), now); if (now >= def.goal) PlayerPrefs.Save(); } catch { }
            if (now >= def.goal)
            {
                Hunt.UI.HUD.Notify($"업적 — {def.name}  ·  {def.desc}", new Color(1f, 0.85f, 0.45f));
                SFX.Play(SFX.Gong, 0.45f, 0f);
            }
        }

        /// <summary>한 번에 달성 (목표 1짜리).</summary>
        public static void Unlock(string id) => Add(id, 1000000);
    }
}

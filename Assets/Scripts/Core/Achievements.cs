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
            new Def { id = "jangseung", name = "장승의 축복",  desc = "한 판에 숨은 장승 셋을 모두 찾아 절한다",  goal = 1 },
            new Def { id = "silent10", name = "그림자 칼",     desc = "못 알아챈 망자를 등 뒤에서 칼로 10마리 쓰러뜨린다", goal = 10 },
            new Def { id = "flawless", name = "상처 없는 새벽", desc = "한 밤을 거의 다치지 않고 버틴다 (받은 상처 10 미만)", goal = 1 },
            new Def { id = "bounty10", name = "현상금 사냥꾼", desc = "현상금 망자 10마리를 쓰러뜨린다",        goal = 10 },
            new Def { id = "molotov",  name = "불바다",        desc = "화염병 불바다 하나로 망자 4마리를 태운다", goal = 1 },
            new Def { id = "horde3",   name = "그림자처럼",    desc = "망자 떼를 들키지 않고 세 번 보낸다", goal = 3 },
            new Def { id = "rescue5",  name = "사람을 살리다", desc = "다친 마을 사람 다섯을 구한다", goal = 5 },
            new Def { id = "spotlurk", name = "간파",          desc = "덤불 속 매복 망자를 튀어나오기 전에 쓰러뜨린다", goal = 1 },
            new Def { id = "hide10",   name = "숨바꼭질",      desc = "덤불에 숨은 채 망자 10마리를 코앞(3.5m)으로 지나 보낸다", goal = 10 },
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
                Hunt.UI.HUD.ShowAchievement(def.name, def.desc);
                SFX.Play(SFX.Gong, 0.45f, 0f);
            }
        }

        /// <summary>한 번에 달성 (목표 1짜리).</summary>
        public static void Unlock(string id) => Add(id, 1000000);
    }
}

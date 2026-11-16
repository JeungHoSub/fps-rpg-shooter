using System.Collections.Generic;
using Hunt.Combat;
using UnityEngine;

namespace Hunt.UI
{
    /// <summary>떠오르는 데미지 숫자 데이터. 그리기는 HUD가 담당.</summary>
    public static class DamageNumbers
    {
        public const float Life = 0.8f;

        public struct Entry
        {
            public Vector3 pos;
            public float amount;
            public bool crit;
            public HitZone zone;
            public DamageType type;
            public float t;
        }

        public static readonly List<Entry> Entries = new List<Entry>();

        public static void Spawn(Vector3 pos, float amount, bool crit, HitZone zone, DamageType type)
        {
            if (amount < 0.5f) return;
            if (Entries.Count > 80) Entries.RemoveAt(0);
            pos += Random.insideUnitSphere * 0.15f;
            Entries.Add(new Entry { pos = pos, amount = amount, crit = crit, zone = zone, type = type, t = 0f });
        }

        public static void Tick(float dt)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                var e = Entries[i];
                e.t += dt;
                e.pos += Vector3.up * dt * 1.2f;
                if (e.t >= Life) Entries.RemoveAt(i);
                else Entries[i] = e;
            }
        }

        public static void Clear() { Entries.Clear(); }
    }
}

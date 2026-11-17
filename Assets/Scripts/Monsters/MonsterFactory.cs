using System.Collections.Generic;
using Hunt.Combat;
using Hunt.Data;
using Hunt.Evolution;
using Hunt.Player;
using UnityEngine;

namespace Hunt.Monsters
{
    /// <summary>
    /// 괴물 외형 조립기. 지금은 기본 도형(프리미티브)으로 만들지만, 컨셉이 정해지면
    /// 이 클래스만 프리팹/모델 기반으로 교체하면 된다. (MonsterData·Monster 로직은 그대로)
    /// 모든 좌표는 scale=1 기준 키 약 2m 인체형.
    /// </summary>
    public static class MonsterFactory
    {
        public static readonly Color WeakPointColor = new Color(1f, 0.55f, 0.05f);
        static readonly Color BoneColor = new Color(0.85f, 0.8f, 0.65f);

        public static Monster Create(MonsterData d, Vector3 position, EvolutionProfile evo, float hpScale, PlayerCharacter target, Transform parent)
        {
            var root = new GameObject($"{d.displayName}");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * d.scale;

            var m = root.AddComponent<Monster>();
            m.Init(d, hpScale, evo, target);

            Color skin = Color.Lerp(d.color, new Color(0.5f, 0.1f, 0.4f), Mathf.Min(0.5f, evo.generation * 0.08f)); // 진화할수록 보랏빛
            bool spitter = d.archetype == MonsterArchetype.Spitter;
            bool heavy = d.archetype == MonsterArchetype.Brute || d.archetype == MonsterArchetype.Boss;

            // 몸통 / 머리
            Part(PrimitiveType.Capsule, root.transform, new Vector3(0, 0.95f, 0), new Vector3(spitter ? 0.65f : 0.8f, 0.6f, spitter ? 0.65f : 0.8f), skin, m, HitZone.Body);
            var head = Part(PrimitiveType.Sphere, root.transform, new Vector3(0, 1.75f, 0.08f), Vector3.one * (spitter ? 0.6f : 0.45f), skin * 1.1f, m, HitZone.Head);
            m.SetHead(head.transform);

            // 눈 (판정 없음)
            // 어둠 속에서 붉게 빛나는 눈
            Deco(PrimitiveType.Sphere, root.transform, new Vector3(0.09f, 1.8f, 0.28f), Vector3.one * 0.08f, new Color(1f, 0.15f, 0.1f), true);
            Deco(PrimitiveType.Sphere, root.transform, new Vector3(-0.09f, 1.8f, 0.28f), Vector3.one * 0.08f, new Color(1f, 0.15f, 0.1f), true);

            // 팔 (몸통 판정)
            float armW = heavy ? 0.24f : 0.14f;
            var armL = Part(PrimitiveType.Cube, root.transform, new Vector3(0.5f, 1.1f, 0.25f), new Vector3(armW, 0.65f, armW), skin * 0.9f, m, HitZone.Body);
            var armR = Part(PrimitiveType.Cube, root.transform, new Vector3(-0.5f, 1.1f, 0.25f), new Vector3(armW, 0.65f, armW), skin * 0.9f, m, HitZone.Body);
            armL.transform.localRotation = Quaternion.Euler(55f, 0, 0);
            armR.transform.localRotation = Quaternion.Euler(55f, 0, 0);

            // 다리 (파괴 가능 → 이동속도 감소)
            float legHp = d.maxHp * hpScale * (d.archetype == MonsterArchetype.Boss ? 0.08f : 0.18f) * evo.legHpMult;
            Part(PrimitiveType.Cube, root.transform, new Vector3(0.2f, 0.3f, 0), new Vector3(0.2f, 0.6f, 0.22f), skin * 0.8f, m, HitZone.Leg, legHp);
            Part(PrimitiveType.Cube, root.transform, new Vector3(-0.2f, 0.3f, 0), new Vector3(0.2f, 0.6f, 0.22f), skin * 0.8f, m, HitZone.Leg, legHp);

            // 투구 (진화: 헤드샷 대응)
            if (evo.helmetLevel > 0)
            {
                float hs = (spitter ? 0.7f : 0.55f) * (1f + 0.05f * evo.helmetLevel);
                Part(PrimitiveType.Cube, root.transform, new Vector3(0, 1.77f, 0.08f), Vector3.one * hs, BoneColor, m, HitZone.Armor,
                    d.maxHp * hpScale * 0.12f * evo.helmetLevel);
            }

            // 전면 장갑판 (브루트/보스): 가슴 약점을 가린다
            if (heavy)
            {
                float armorHp = d.maxHp * hpScale * (d.archetype == MonsterArchetype.Boss ? 0.12f : 0.45f);
                Part(PrimitiveType.Cube, root.transform, new Vector3(0, 1.0f, 0.47f), new Vector3(0.95f, 0.85f, 0.14f), BoneColor * 0.9f, m, HitZone.Armor, armorHp);
            }

            // 약점
            if (d.archetype == MonsterArchetype.Boss)
            {
                var wps = new List<MonsterPart>
                {
                    WeakPoint(root.transform, m, WeakPointLocation.Back, 0.32f),
                    WeakPoint(root.transform, m, WeakPointLocation.Shoulder, 0.32f),
                    WeakPoint(root.transform, m, WeakPointLocation.Front, 0.32f), // 장갑판 뒤
                };
                m.SetBossWeakPoints(wps);
            }
            else
            {
                WeakPoint(root.transform, m, evo.weakPoint, d.archetype == MonsterArchetype.Runner ? 0.24f : 0.3f);
            }

            return m;
        }

        static MonsterPart WeakPoint(Transform root, Monster m, WeakPointLocation loc, float size)
        {
            Vector3 p;
            switch (loc)
            {
                case WeakPointLocation.Back: p = new Vector3(0, 1.05f, -0.36f); break;
                case WeakPointLocation.Shoulder: p = new Vector3(0.3f, 1.4f, 0f); break;
                default: p = new Vector3(0, 1.0f, 0.34f); break;
            }
            return Part(PrimitiveType.Sphere, root, p, Vector3.one * size, WeakPointColor, m, HitZone.WeakPoint);
        }

        static MonsterPart Part(PrimitiveType type, Transform parent, Vector3 lpos, Vector3 lscale, Color c, Monster m, HitZone zone, float hp = -1f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = zone.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lpos;
            go.transform.localScale = lscale;
            go.GetComponent<Renderer>().material.color = c;
            var part = go.AddComponent<MonsterPart>();
            part.Setup(m, zone, hp);
            return part;
        }

        static void Deco(PrimitiveType type, Transform parent, Vector3 lpos, Vector3 lscale, Color c, bool glow = false)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lpos;
            go.transform.localScale = lscale;
            if (glow) go.GetComponent<Renderer>().sharedMaterial = Hunt.Core.FX.UnlitMaterial(c);
            else go.GetComponent<Renderer>().material.color = c;
        }
    }
}

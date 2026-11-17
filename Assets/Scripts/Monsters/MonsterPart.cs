using Hunt.Combat;
using UnityEngine;

namespace Hunt.Monsters
{
    /// <summary>
    /// 괴물의 피격 부위 하나 (콜라이더 단위).
    /// E: 장갑/다리/투구는 별도 내구도를 가지며 파괴할 수 있다. 장갑이 부서지면 뒤에 있던 약점이 드러난다.
    /// </summary>
    public class MonsterPart : MonoBehaviour
    {
        public Monster owner;
        public HitZone zone;
        public bool breakable;
        public float partHp;
        public float partMaxHp;
        public bool broken;

        Renderer rend;
        Collider col;
        Color baseColor;
        float flash;

        public void Setup(Monster owner, HitZone zone, float hp)
        {
            this.owner = owner;
            this.zone = zone;
            breakable = hp > 0f;
            partHp = partMaxHp = hp;
            rend = GetComponent<Renderer>();
            col = GetComponent<Collider>();
            if (rend != null) baseColor = rend.material.color;
            if (rend != null && zone == HitZone.WeakPoint) rend.material.EnableKeyword("_EMISSION"); // 어둠 속에서도 보이는 급소
        }

        public void SetBaseColor(Color c)
        {
            baseColor = c;
            if (rend != null) rend.material.color = c;
        }

        public DamageResult ReceiveHit(in DamageInfo info)
        {
            if (owner == null) return default;
            return owner.ApplyHit(this, info);
        }

        /// <returns>이번 피해로 파괴되었으면 true</returns>
        public bool TakePartDamage(float dmg)
        {
            flash = 0.06f;
            if (!breakable || broken) return false;
            partHp -= dmg;
            if (partHp <= 0f)
            {
                broken = true;
                return true;
            }
            return false;
        }

        void Update()
        {
            if (rend == null) return;
            if (flash > 0f)
            {
                flash -= Time.deltaTime;
                rend.material.color = flash > 0f ? Color.white : CurrentColor();
            }
            else if (zone == HitZone.WeakPoint && (col == null || col.enabled))
            {
                var c = CurrentColor();
                rend.material.color = c;
                rend.material.SetColor("_EmissionColor", c * 0.9f);
            }
            else if (zone == HitZone.WeakPoint)
            {
                rend.material.SetColor("_EmissionColor", Color.black); // 보스의 닫힌 급소
            }
        }

        Color CurrentColor()
        {
            // 약점은 맥동하듯 빛나서 멀리서도 눈에 띈다
            if (zone == HitZone.WeakPoint) return baseColor * (1f + 0.35f * Mathf.Sin(Time.time * 6f));
            if (!breakable || broken) return baseColor;
            // 내구도가 낮을수록 붉게 금이 간 느낌
            float t = 1f - Mathf.Clamp01(partHp / partMaxHp);
            return Color.Lerp(baseColor, new Color(0.6f, 0.1f, 0.1f), t * 0.7f);
        }
    }
}

using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 쏘면 꺼지는 가로등: 등갓을 맞히면 유리가 깨지며 불이 꺼진다. 어둠 속에 숨으려면 쏴서 끄면 된다
    /// (망자는 빛 속의 사람을 더 멀리서 본다). 한 번 꺼지면 그 판에서는 다시 안 켜진다.
    /// </summary>
    public class ShootableLamp : MonoBehaviour
    {
        public Light lamp;
        public LampFlicker flicker;
        public Renderer glow;
        bool broken;
        static readonly System.Collections.Generic.List<ShootableLamp> all = new System.Collections.Generic.List<ShootableLamp>();
        static int litFrame = -1;
        static bool litCache;

        Material litMat;
        bool shownDark;

        // 정전 동안 등갓 유리도 어둡게 (불빛만 꺼지고 전구가 빛나면 어색하다)
        void Update()
        {
            if (broken || glow == null) return;
            bool dark = LampFlicker.Blackout;
            if (dark == shownDark) return;
            shownDark = dark;
            if (dark) { litMat = glow.sharedMaterial; glow.sharedMaterial = EnvMaterials.Plain(new Color(0.1f, 0.09f, 0.08f), 0.6f); }
            else if (litMat != null) glow.sharedMaterial = litMat;
        }

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        /// <summary>플레이어가 켜진 가로등 불빛(6.5m) 아래 서 있나 (망자가 더 멀리서 본다). 한 프레임에 한 번만 계산.</summary>
        public static bool PlayerLit()
        {
            if (litFrame == Time.frameCount) return litCache;
            litFrame = Time.frameCount;
            litCache = false;
            if (LampFlicker.Blackout) return false; // 정전
            var gm = GameManager.Instance;
            var p = gm != null ? gm.Player : null;
            if (p == null) return false;
            Vector3 pp = p.transform.position;
            foreach (var l in all)
            {
                if (l == null || l.broken || l.lamp == null || !l.lamp.enabled) continue;
                Vector3 d = l.transform.position - pp; d.y = 0f;
                if (d.sqrMagnitude < 6.5f * 6.5f) { litCache = true; break; }
            }
            return litCache;
        }

        public void Hit(Vector3 point)
        {
            if (broken) return;
            broken = true;
            if (flicker != null) Destroy(flicker); // 깜빡임 스크립트가 다시 켜지 않게
            if (lamp != null) lamp.enabled = false;
            if (glow != null) glow.sharedMaterial = EnvMaterials.Plain(new Color(0.08f, 0.07f, 0.06f), 0.6f);
            FX.ImpactWorld(point, Vector3.down);
            FX.ImpactWorld(point, Vector3.down);
            SFX.PlayAt3D(SFX.Casing, point, 1f, 0.6f);      // 유리 깨지는 소리
            SFX.PlayAt3D(SFX.HitTick, point, 0.8f, 0.5f);
            Hunt.UI.HUD.Notify("가로등을 깨뜨렸다 — 이 근처는 어둠에 묻힌다", new Color(0.75f, 0.8f, 0.9f));
        }
    }
}

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

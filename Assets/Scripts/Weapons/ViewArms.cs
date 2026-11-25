using System.Collections.Generic;
using Hunt.Core;
using UnityEngine;

namespace Hunt.Weapons
{
    /// <summary>
    /// 1인칭 총을 쥔 두 손 (가죽 장갑 + 작업복 소매). 오른손은 손잡이를 감싸고 검지는 방아쇠울,
    /// 왼손은 총몸 아래를 받친다 (권총은 두 손으로 감싸 쥔다). 장전 때 왼손이 탄창을 뽑고 새 탄창을 끼우며
    /// 빈 총이면 노리쇠를 당긴다. 도형 20여 개 · 그림자 없음 (내장 그래픽 기준 가볍게).
    /// 손 위치는 모델별 측면도(Tools/GunSource)에서 잰 값. 표에 없는 총은 손 없이 그대로.
    /// </summary>
    public class ViewArms : MonoBehaviour
    {
        // (손잡이 z, y) · (앞손 z, y — 총몸 아랫면) · 권총 여부
        static readonly Dictionary<string, (float gz, float gy, float fz, float fy, bool pistol)> Poses =
            new Dictionary<string, (float, float, float, float, bool)>
        {
            { "ar",          (-0.12f, -0.07f, 0.17f, -0.005f, false) },
            { "k2c",         (-0.10f, -0.07f, 0.17f,  0.0f,   false) },
            { "br762",       (-0.10f, -0.10f, 0.24f,  0.01f,  false) },
            { "smg",         (-0.10f, -0.08f, 0.14f,  0.04f,  false) },
            { "ump",         ( 0.03f, -0.08f, 0.24f, -0.03f,  false) },
            { "vector",      (-0.04f, -0.08f, 0.18f, -0.02f,  false) },
            { "shotgun",     (-0.10f, -0.06f, 0.32f, -0.06f,  false) },
            { "autoshotgun", (-0.10f, -0.07f, 0.36f, -0.05f,  false) },
            { "doublebarrel",(-0.08f, -0.06f, 0.15f, -0.03f,  false) },
            { "dmr",         (-0.12f, -0.08f, 0.20f, -0.02f,  false) },
            { "antimat",     (-0.12f, -0.11f, 0.16f, -0.03f,  false) },
            { "lmg",         (-0.08f, -0.12f, 0.20f,  0.0f,   false) },
            { "k3",          ( 0.00f, -0.10f, 0.22f,  0.0f,   false) },
            { "hmg",         (-0.13f, -0.15f, 0.20f, -0.02f,  false) },
            { "m79",         (-0.08f, -0.07f, 0.15f, -0.03f,  false) },
            { "mgl",         (-0.02f, -0.06f, 0.20f, -0.04f,  false) },
            { "rocket",      ( 0.00f, -0.08f, 0.20f, -0.05f,  false) },
            { "flamer",      (-0.15f, -0.10f, 0.08f, -0.05f,  false) },
            { "pistol2",     (-0.04f, -0.05f, 0f, 0f, true) },
            { "revolver",    (-0.06f, -0.06f, 0f, 0f, true) },
        };

        public static bool Has(string weaponId) => Poses.ContainsKey(PackGunModel.ModelId(weaponId));

        Transform rHand, lHand, rFore, lFore, rUpper, lUpper;
        Vector3 grip, foreRest, rElbow, lElbowOff, lPos;
        bool pistol;
        GunModel model;

        static Material glove, sleeve, cuff;

        public static ViewArms Attach(GunModel m, string weaponId)
        {
            if (m == null || !Poses.TryGetValue(PackGunModel.ModelId(weaponId), out var p)) return null;
            var a = m.gameObject.AddComponent<ViewArms>();
            a.Build(m, p.gz, p.gy, p.fz, p.fy, p.pistol);
            return a;
        }

        static void Mats()
        {
            if (glove != null) return;
            glove = EnvMaterials.Plain(new Color(0.09f, 0.08f, 0.07f), 0.35f, 0f);   // 낡은 검은 가죽 장갑
            sleeve = EnvMaterials.Plain(new Color(0.16f, 0.15f, 0.11f), 0.08f, 0f);  // 물 먹은 국방색 작업복
            cuff = EnvMaterials.Plain(new Color(0.12f, 0.11f, 0.09f), 0.1f, 0f);
        }

        Transform Piece(Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler, bool capsule)
        {
            GameObject g = capsule ? GameObject.CreatePrimitive(PrimitiveType.Capsule) : FX.LowSphere();
            var col = g.GetComponent<Collider>();
            if (col != null) Destroy(col);
            g.layer = GunModelBuilder.ViewLayer;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return g.transform;
        }

        Transform Group(string name, Vector3 pos, Vector3 euler, float mirror = 1f)
        {
            var t = new GameObject(name).transform;
            t.gameObject.layer = GunModelBuilder.ViewLayer;
            t.SetParent(transform, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(euler);
            t.localScale = new Vector3(mirror, 1f, 1f);
            return t;
        }

        /// <summary>손잡이를 쥔 손 (손잡이 축 = 위, 손가락은 앞쪽을 감싼다).</summary>
        void GripHand(Transform h, bool trigger)
        {
            Piece(h, new Vector3(0.024f, 0.0f, -0.008f), new Vector3(0.036f, 0.085f, 0.07f), glove, Vector3.zero, false);    // 손바닥·손등
            Piece(h, new Vector3(0.034f, -0.004f, -0.026f), new Vector3(0.04f, 0.07f, 0.055f), glove, Vector3.zero, false);  // 손등 두께
            for (int i = 0; i < 3; i++)                                                                                        // 감싼 손가락 셋
                Piece(h, new Vector3(0.004f, 0.006f - i * 0.022f, 0.028f), new Vector3(0.021f, 0.026f, 0.021f), glove, new Vector3(0f, 0f, 90f), true);
            Piece(h, new Vector3(-0.022f, -0.016f, 0.016f), new Vector3(0.024f, 0.06f, 0.026f), glove, Vector3.zero, false); // 손가락 끝 (왼쪽 면)
            if (trigger) Piece(h, new Vector3(0.014f, 0.042f, 0.038f), new Vector3(0.017f, 0.024f, 0.017f), glove, new Vector3(90f, 0f, 0f), true); // 검지 → 방아쇠울
            Piece(h, new Vector3(-0.02f, 0.036f, 0.0f), new Vector3(0.019f, 0.03f, 0.019f), glove, new Vector3(75f, 0f, 0f), true);   // 엄지 (왼쪽 면)
            Piece(h, new Vector3(0.036f, -0.03f, -0.058f), new Vector3(0.05f, 0.03f, 0.05f), cuff, new Vector3(60f, 0f, 0f), true);    // 손목 소맷부리
        }

        /// <summary>총몸 아래를 받친 손 (손바닥 위로, 손가락은 왼쪽 면을 감싸고 엄지는 오른쪽).</summary>
        void SupportHand(Transform h)
        {
            Piece(h, new Vector3(-0.004f, -0.024f, 0f), new Vector3(0.07f, 0.032f, 0.09f), glove, Vector3.zero, false);
            for (int i = 0; i < 3; i++)
                Piece(h, new Vector3(-0.032f, -0.006f, -0.024f + i * 0.024f), new Vector3(0.02f, 0.022f, 0.02f), glove, new Vector3(0f, 0f, -15f), true);
            Piece(h, new Vector3(0.03f, -0.01f, -0.012f), new Vector3(0.019f, 0.03f, 0.019f), glove, new Vector3(80f, 0f, 0f), true);
            Piece(h, new Vector3(-0.012f, -0.04f, -0.06f), new Vector3(0.05f, 0.03f, 0.05f), cuff, new Vector3(60f, 0f, 0f), true);
        }

        void Build(GunModel m, float gz, float gy, float fz, float fy, bool isPistol)
        {
            Mats();
            model = m;
            pistol = isPistol;
            grip = new Vector3(0f, gy, gz);
            rHand = Group("오른손", grip, new Vector3(18f, 0f, 0f));
            GripHand(rHand, true);
            rElbow = grip + new Vector3(0.13f, -0.17f, -0.27f);
            if (isPistol)
            {
                // 왼손: 오른손을 마주 감싼다 (좌우 뒤집은 손)
                foreRest = grip + new Vector3(-0.01f, -0.012f, 0.01f);
                lHand = Group("왼손", foreRest, new Vector3(18f, 0f, -6f), -1f);
                GripHand(lHand, false);
                lElbowOff = new Vector3(-0.16f, -0.18f, -0.25f);
            }
            else
            {
                foreRest = new Vector3(0f, fy, fz);
                lHand = Group("왼손", foreRest, Vector3.zero);
                SupportHand(lHand);
                lElbowOff = new Vector3(-0.16f, -0.21f, -0.2f);
            }
            lPos = foreRest;
            rFore = Piece(transform, Vector3.zero, Vector3.one, sleeve, Vector3.zero, true);
            lFore = Piece(transform, Vector3.zero, Vector3.one, sleeve, Vector3.zero, true);
            rUpper = Piece(transform, Vector3.zero, Vector3.one, sleeve, Vector3.zero, true);
            lUpper = Piece(transform, Vector3.zero, Vector3.one, sleeve, Vector3.zero, true);
            LayArm(rFore, rUpper, grip + rHand.localRotation * new Vector3(0.036f, -0.03f, -0.06f), rElbow, new Vector3(0.06f, -0.12f, -0.25f), 0.034f);
            LayLeft();
        }

        /// <summary>소매: 손목 → 팔꿈치 (아래팔), 팔꿈치 → 어깨 쪽 (윗팔, 화면 밖으로).</summary>
        static void LayArm(Transform fore, Transform upper, Vector3 wrist, Vector3 elbow, Vector3 upperDir, float r)
        {
            Seg(fore, wrist, elbow, r);
            Seg(upper, elbow, elbow + upperDir, r * 1.25f);
        }

        static void Seg(Transform t, Vector3 a, Vector3 b, float r)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.001f) return;
            t.localPosition = (a + b) * 0.5f;
            t.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
            t.localScale = new Vector3(r * 2f, len * 0.5f + r, r * 2f);
        }

        void LayLeft()
        {
            lHand.localPosition = lPos;
            Vector3 wrist = lPos + (pistol ? lHand.localRotation * new Vector3(-0.036f, -0.03f, -0.06f) : new Vector3(-0.012f, -0.04f, -0.06f));
            Vector3 elbow = foreRest + lElbowOff + (lPos - foreRest) * 0.5f;
            LayArm(lFore, lUpper, wrist, elbow, new Vector3(-0.07f, -0.12f, -0.24f), 0.032f);
        }

        /// <summary>
        /// 장전 중 왼손: 탄창으로 가서 뽑고 → 아래로 새 탄창 가지러 → 새 탄창과 함께 올라와 끼움 → (빈 총) 노리쇠 → 제자리.
        /// 산탄총은 한 발씩 아래에서 집어 장전구로.
        /// </summary>
        public void Tick(float dt, bool reloading, bool shell, float p, float inAt, bool empty)
        {
            Vector3 target = foreRest;
            Vector3 magPos = model.magazine != null ? transform.InverseTransformPoint(model.magazine.position) + new Vector3(-0.01f, -0.05f, 0f)
                                                    : grip + new Vector3(0f, -0.06f, pistol ? 0.02f : 0.12f);
            Vector3 magHome = model.magazine != null ? model.magazineRest + new Vector3(-0.01f, -0.05f, 0f) : magPos;
            Vector3 away = magHome + new Vector3(-0.05f, -0.3f, -0.08f);
            if (reloading && shell)
            {
                float c = Mathf.PingPong(Time.time * 2.6f, 1f);
                target = Vector3.Lerp(grip + new Vector3(-0.03f, -0.2f, 0.02f), grip + new Vector3(0f, -0.05f, 0.08f), Mathf.SmoothStep(0f, 1f, c));
            }
            else if (reloading)
            {
                if (p < 0.12f) target = Vector3.Lerp(foreRest, magHome, Mathf.SmoothStep(0f, 1f, p / 0.12f));
                else if (p < 0.3f) target = magPos;
                else if (p < inAt - 0.14f) target = away;
                else if (p < inAt) target = model.magazine != null && model.magazine.gameObject.activeSelf ? magPos : Vector3.Lerp(away, magHome, Mathf.InverseLerp(inAt - 0.14f, inAt, p));
                else if (empty && p > inAt + 0.06f && p < 0.9f)
                {
                    Vector3 act = model.action != null ? transform.InverseTransformPoint(model.action.position) + new Vector3(-0.04f, 0f, 0f)
                                                       : grip + new Vector3(-0.045f, 0.06f, 0.04f);
                    target = act;
                }
                else if (p < inAt + 0.06f) target = magHome;
            }
            float follow = reloading ? 22f : 12f;
            lPos = Vector3.Lerp(lPos, target, 1f - Mathf.Exp(-follow * dt));
            LayLeft();
        }
    }
}

using Hunt.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Hunt.Monsters
{
    /// <summary>
    /// 애니메이션 좀비 모델 (Quaternius "Animated Zombie", CC0).
    /// - Animator Controller 에셋 없이 Playables로 걷기/달리기/기어가기/물기/대기를 섞어 재생
    /// - 맞으면 상체가 뒤로 젖혀지는 피격 반응(척추 뼈를 직접 비튼다)
    /// - 죽으면 그 자세로 뒤로 쓰러져 땅에 가라앉는다
    /// 모델이 없거나 불러오지 못하면 Available=false → 기존 도형 괴물로 대체.
    /// </summary>
    public class ZombieRig : MonoBehaviour
    {
        public enum Anim { Idle, Walk, Run, Bite, Crawl, Hit, Scream, Death }
        const int AnimCount = 8;
        const string ResPath = "Zombies/ZombieSmooth";       // Quaternius (대체용)
        const string MixamoModel = "Mixamo/Romero";           // Mixamo 실사 망자
        const string MixamoAnims = "Mixamo/Anims/Zombie_";

        static GameObject prefab;
        static AnimationClip[] clips;
        static bool loaded;
        public static bool IsMixamo { get; private set; }

        static AnimationClip FirstClip(string path)
        {
            foreach (var c in Resources.LoadAll<AnimationClip>(path))
                if (c != null && !c.name.StartsWith("__preview__")) return c;
            return null;
        }

        static void LoadMixamo()
        {
            var p = Resources.Load<GameObject>(MixamoModel);
            if (p == null) return;
            var cl = new AnimationClip[AnimCount];
            cl[(int)Anim.Idle] = FirstClip(MixamoAnims + "Idle");
            cl[(int)Anim.Walk] = FirstClip(MixamoAnims + "Walk");
            cl[(int)Anim.Run] = FirstClip(MixamoAnims + "Run");
            cl[(int)Anim.Bite] = FirstClip(MixamoAnims + "Attack");
            cl[(int)Anim.Crawl] = FirstClip(MixamoAnims + "Crawl");
            cl[(int)Anim.Hit] = FirstClip(MixamoAnims + "Hit");
            cl[(int)Anim.Scream] = FirstClip(MixamoAnims + "Scream");
            cl[(int)Anim.Death] = FirstClip(MixamoAnims + "Death");
            if (cl[(int)Anim.Walk] == null) { Debug.LogWarning("[Hunt] Mixamo 동작을 찾지 못해 기본 좀비를 씁니다."); return; }
            prefab = p;
            clips = cl;
            IsMixamo = true;
        }

        public static bool Available
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    LoadMixamo();
                    if (IsMixamo) return true;
                    prefab = Resources.Load<GameObject>(ResPath);
                    clips = new AnimationClip[AnimCount];
                    foreach (var c in Resources.LoadAll<AnimationClip>(ResPath))
                    {
                        if (c == null || c.name.StartsWith("__preview__")) continue;
                        string n = c.name.ToLowerInvariant();
                        if (n.Contains("idle")) clips[(int)Anim.Idle] = c;
                        else if (n.Contains("walk")) clips[(int)Anim.Walk] = c;
                        else if (n.Contains("run")) clips[(int)Anim.Run] = c;
                        else if (n.Contains("bite")) clips[(int)Anim.Bite] = c;
                        else if (n.Contains("crawl")) clips[(int)Anim.Crawl] = c;
                    }
                    if (prefab == null) Debug.LogWarning("[Hunt] 좀비 모델을 찾지 못해 도형 괴물로 대체합니다: Resources/" + ResPath);
                }
                return prefab != null && clips[(int)Anim.Walk] != null;
            }
        }

        // ---- 뼈 ----
        public Transform Pivot { get; private set; }
        public Transform Model { get; private set; }
        public Transform Hips, Spine, Spine2, Neck, Head, HeadTop;
        public Transform LeftArm, LeftHand, RightArm, RightHand;
        public Transform LeftUpLeg, LeftFoot, RightUpLeg, RightFoot;
        public float Unit { get; private set; } = 1f; // 키 1.85m 기준 배율

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] players = new AnimationClipPlayable[AnimCount];
        readonly float[] weights = new float[AnimCount];
        Anim locomotion = Anim.Idle;
        float locomotionSpeed = 1f;
        bool biting;
        Anim oneShot = Anim.Bite;
        float oneShotSpeed = 1.3f;
        float flinch, flinchVel, headJerk;
        Vector3 flinchAxis = Vector3.right;
        float flashT;
        Color tint = Color.white;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        bool dead;

        /// <summary>괴물 루트 아래에 모델을 만들고 키·방향·발 높이를 맞춘다.</summary>
        public static ZombieRig Create(Transform monsterRoot, float targetHeight, Color tint)
        {
            if (!Available) return null;
            var pivot = new GameObject("Visual").transform;
            pivot.SetParent(monsterRoot, false);
            var model = Instantiate(prefab, pivot);
            model.name = "ZombieModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var rig = monsterRoot.gameObject.AddComponent<ZombieRig>();
            rig.Pivot = pivot;
            rig.Model = model.transform;
            rig.tint = tint;
            rig.FindBones();

            // 키 맞추기 (기본 자세 기준)
            float footY = Mathf.Min(Y(rig.LeftFoot, monsterRoot), Y(rig.RightFoot, monsterRoot));
            float headY = rig.HeadTop != null ? rig.HeadTop.position.y : rig.Head.position.y + 0.15f;
            float height = Mathf.Max(0.01f, headY - footY);
            float s = targetHeight / height;
            pivot.localScale = Vector3.one * s;
            rig.Unit = targetHeight / 1.85f;

            // 방향 맞추기: 발끝이 괴물의 앞(+Z)을 보도록
            if (rig.LeftFoot != null && rig.RightFoot != null)
            {
                Vector3 toe = ToeDir(rig.LeftFoot) + ToeDir(rig.RightFoot);
                toe.y = 0f;
                if (toe.sqrMagnitude > 0.0001f)
                {
                    float ang = Vector3.SignedAngle(toe.normalized, monsterRoot.forward, Vector3.up);
                    pivot.rotation = Quaternion.AngleAxis(ang, Vector3.up) * pivot.rotation;
                }
            }
            // 발이 땅에 닿게
            footY = Mathf.Min(Y(rig.LeftFoot, monsterRoot), Y(rig.RightFoot, monsterRoot));
            pivot.position += Vector3.up * (monsterRoot.position.y - footY + 0.02f * rig.Unit);

            rig.SetupGraph(model);
            rig.renderers = model.GetComponentsInChildren<Renderer>();
            foreach (var r in rig.renderers)
            {
                r.shadowCastingMode = QualityManager.Shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }
            rig.mpb = new MaterialPropertyBlock();
            rig.ApplyTint(0f);
            return rig;
        }

        static float Y(Transform t, Transform fallback) => t != null ? t.position.y : fallback.position.y;

        static Vector3 ToeDir(Transform foot)
        {
            // 발 → 발끝 방향 (자식 중 가장 먼 끝점)
            Transform tip = foot;
            float best = 0f;
            foreach (var c in foot.GetComponentsInChildren<Transform>())
            {
                float d = (c.position - foot.position).sqrMagnitude;
                if (d > best) { best = d; tip = c; }
            }
            return tip.position - foot.position;
        }

        void FindBones()
        {
            foreach (var t in Model.GetComponentsInChildren<Transform>())
            {
                // Mixamo 접두어 제거: "mixamorig:Hips", "mixamorig5_Hips" → "Hips"
                string n = System.Text.RegularExpressions.Regex.Replace(t.name, @"^.*mixamorig\d*[:_]", "");
                int bar = n.LastIndexOf(':');
                if (bar >= 0) n = n.Substring(bar + 1);
                switch (n)
                {
                    case "Hips": Hips = t; break;
                    case "Spine": Spine = t; break;
                    case "Spine2": Spine2 = t; break;
                    case "Neck": Neck = t; break;
                    case "Head": Head = t; break;
                    case "HeadTop_End": HeadTop = t; break;
                    case "LeftArm": LeftArm = t; break;
                    case "LeftHand": LeftHand = t; break;
                    case "RightArm": RightArm = t; break;
                    case "RightHand": RightHand = t; break;
                    case "LeftUpLeg": LeftUpLeg = t; break;
                    case "LeftFoot": LeftFoot = t; break;
                    case "RightUpLeg": RightUpLeg = t; break;
                    case "RightFoot": RightFoot = t; break;
                }
            }
            if (Spine2 == null) Spine2 = Spine;
            if (Head == null) Head = Spine2 != null ? Spine2 : Model;
            if (Hips == null) Hips = Model;
        }

        void SetupGraph(GameObject model)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            graph = PlayableGraph.Create("Zombie");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
            mixer = AnimationMixerPlayable.Create(graph, AnimCount);
            output.SetSourcePlayable(mixer);
            for (int i = 0; i < AnimCount; i++)
            {
                var clip = clips[i] ?? clips[(int)Anim.Walk];
                players[i] = AnimationClipPlayable.Create(graph, clip);
                players[i].SetApplyFootIK(false);
                players[i].SetTime(Random.Range(0f, clip.length)); // 다 같이 똑같이 걷지 않도록
                graph.Connect(players[i], 0, mixer, i);
            }
            weights[(int)Anim.Idle] = 1f;
            for (int i = 0; i < AnimCount; i++) mixer.SetInputWeight(i, weights[i]);
            graph.Play();
        }

        // ------------------------------------------------------------------ 외부 호출
        /// <summary>이동 상태. speed = 실제 이동 속도(m/s).</summary>
        public void SetLocomotion(float speed, bool legsBroken, bool fast)
        {
            if (dead) return;
            if (legsBroken) { locomotion = Anim.Crawl; locomotionSpeed = Mathf.Clamp(speed / (0.9f * Unit), 0.5f, 1.6f); }
            else if (speed < 0.15f) { locomotion = Anim.Idle; locomotionSpeed = 1f; }
            else if (fast && clips[(int)Anim.Run] != null) { locomotion = Anim.Run; locomotionSpeed = Mathf.Clamp(speed / (4.5f * Unit), 0.6f, 1.8f); }
            else { locomotion = Anim.Walk; locomotionSpeed = Mathf.Clamp(speed / (1.4f * Unit), 0.6f, 2.2f); }
        }

        public void PlayBite() { PlayOnce(Anim.Bite, 1.3f, true); }

        /// <summary>강하게 맞았을 때 비틀거림.</summary>
        public void PlayHit() { PlayOnce(Anim.Hit, 1.4f, false); }

        /// <summary>처음 발견했을 때 비명. 재생 시간(초)을 돌려준다.</summary>
        public float PlayScream()
        {
            if (!PlayOnce(Anim.Scream, 1.2f, false)) return 0f;
            return clips[(int)Anim.Scream].length / 1.2f * 0.8f;
        }

        bool PlayOnce(Anim a, float speed, bool interrupt)
        {
            if (dead || clips[(int)a] == null) return false;
            if (biting && !interrupt) return false; // 다른 단발 동작 중이면 무시
            biting = true;
            oneShot = a;
            oneShotSpeed = speed;
            players[(int)a].SetTime(0);
            return true;
        }

        /// <summary>피격 반응: 상체가 맞은 방향으로 젖혀진다. strength 0~1.</summary>
        public void Flinch(Vector3 hitDir, float strength, bool head)
        {
            if (dead) return;
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.0001f) hitDir = -transform.forward;
            flinchAxis = Vector3.Cross(Vector3.up, hitDir.normalized); // 총알 방향으로 몸이 넘어가는 축
            flinchVel += Mathf.Lerp(250f, 900f, Mathf.Clamp01(strength));
            if (head) headJerk = Mathf.Min(headJerk + 25f + strength * 20f, 45f);
            flashT = 0.07f;
        }

        public void Die(Vector3 hitDir, Transform parent)
        {
            if (dead) return;
            dead = true;
            if (graph.IsValid()) graph.Destroy();
            var anim = Model.GetComponent<Animator>();
            if (anim != null) anim.enabled = false;
            Pivot.SetParent(parent, true);
            var corpse = Pivot.gameObject.AddComponent<ZombieCorpse>();
            corpse.Begin(hitDir, Unit, clips[(int)Anim.Death], anim);
            Pivot = null;
        }

        // ------------------------------------------------------------------ 갱신
        void Update()
        {
            if (dead || !graph.IsValid()) return;
            float dt = Time.deltaTime;

            Anim target = biting ? oneShot : locomotion;
            for (int i = 0; i < AnimCount; i++)
            {
                weights[i] = Mathf.MoveTowards(weights[i], i == (int)target ? 1f : 0f, dt * (biting ? 10f : 6f));
                mixer.SetInputWeight(i, weights[i]);
            }

            // 반복 재생 (FBX 클립은 반복 설정이 없을 수 있어 직접 돌린다)
            for (int i = 0; i < AnimCount; i++)
            {
                var clip = clips[i] ?? clips[(int)Anim.Walk];
                if (i == (int)Anim.Bite || i == (int)Anim.Hit || i == (int)Anim.Scream || i == (int)Anim.Death) continue;
                players[i].SetSpeed(i == (int)locomotion ? locomotionSpeed : 1f);
                double t = players[i].GetTime();
                if (t > clip.length) players[i].SetTime(t % clip.length);
            }
            if (biting)
            {
                var c = clips[(int)oneShot];
                players[(int)oneShot].SetSpeed(oneShotSpeed);
                if (c == null || players[(int)oneShot].GetTime() >= c.length * 0.95f) biting = false;
            }

            if (flashT > 0f) { flashT -= dt; ApplyTint(Mathf.Clamp01(flashT / 0.07f)); }
        }

        void LateUpdate()
        {
            if (dead) return;
            float dt = Time.deltaTime;
            // 스프링: 확 젖혀졌다가 돌아온다
            flinchVel -= flinch * 180f * dt;
            flinchVel *= Mathf.Exp(-10f * dt);
            flinch += flinchVel * dt;
            flinch = Mathf.Clamp(flinch, -10f, 40f);
            headJerk = Mathf.MoveTowards(headJerk, 0f, dt * 120f);

            if (Spine != null && Mathf.Abs(flinch) > 0.01f)
                Spine.rotation = Quaternion.AngleAxis(flinch * 0.6f, flinchAxis) * Spine.rotation;
            if (Spine2 != null && Mathf.Abs(flinch) > 0.01f)
                Spine2.rotation = Quaternion.AngleAxis(flinch * 0.4f, flinchAxis) * Spine2.rotation;
            if (Head != null && headJerk > 0.01f)
                Head.rotation = Quaternion.AngleAxis(headJerk, flinchAxis) * Head.rotation;
        }

        void ApplyTint(float flash)
        {
            if (renderers == null) return;
            mpb.SetColor("_Color", Color.Lerp(tint, new Color(2.2f, 2.2f, 2.2f), flash));
            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(mpb);
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}

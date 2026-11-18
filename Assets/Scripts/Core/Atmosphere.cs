using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 비 내리는 밤: 어두운 달빛, 짙은 안개, 카메라를 따라다니는 빗줄기와 물튀김, 빗소리, 천둥·번개.
    /// RainIntensity(0~1)는 이후 "빗소리가 소음을 덮는" 시스템에서 쓴다.
    /// </summary>
    public class Atmosphere : MonoBehaviour
    {
        public static Atmosphere Instance { get; private set; }

        public float RainIntensity { get; private set; } = 0.7f;

        static readonly Color FogColor = new Color(0.035f, 0.042f, 0.05f);
        const float BaseFog = 0.032f;

        Light moon;
        ParticleSystem rain, splash;
        AudioSource rainAudio;
        float baseMoon = 0.22f;
        float nextThunder, flash, thunderDelay = -1f;
        float rainTarget = 0.7f, nextRainShift;
        Color baseAmbient;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Init(Transform root)
        {
            // ---- 빛 / 안개 ----
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && !l.transform.IsChildOf(root)) l.gameObject.SetActive(false);

            var moonGo = new GameObject("Moon");
            moonGo.transform.SetParent(root, false);
            moonGo.transform.rotation = Quaternion.Euler(38f, -140f, 0f);
            moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 0.85f);
            moon.intensity = baseMoon;
            moon.shadows = LightShadows.Soft; // 실제 그림자 품질은 QualityManager가 전역으로 정한다
            moon.shadowStrength = 0.6f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            baseAmbient = new Color(0.075f, 0.085f, 0.105f);
            RenderSettings.ambientLight = baseAmbient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = BaseFog;
            RenderSettings.skybox = null;
            RenderSettings.sun = moon;

            // ---- 비 ----
            rain = MakeRain(root);
            splash = MakeSplash(root);

            // ---- 빗소리 (2D 루프) ----
            rainAudio = gameObject.AddComponent<AudioSource>();
            rainAudio.clip = SFX.RainLoop;
            rainAudio.loop = true;
            rainAudio.spatialBlend = 0f;
            rainAudio.volume = 0.5f;
            rainAudio.Play();

            nextThunder = Time.time + Random.Range(8f, 18f);
            nextRainShift = Time.time + 20f;
        }

        public static Color Fog => FogColor;

        ParticleSystem MakeRain(Transform root)
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 5f;
            main.maxParticles = 9000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.startColor = new Color(0.65f, 0.7f, 0.8f, 0.35f);
            main.gravityModifier = 0f;

            var em = ps.emission;
            em.rateOverTime = 5000f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 1f, 44f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            vel.y = new ParticleSystem.MinMaxCurve(-26f, -22f);
            vel.z = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FX.UnlitMaterial(Color.white);
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.045f;
            r.lengthScale = 1f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        ParticleSystem MakeSplash(Transform root)
        {
            var go = new GameObject("RainSplash");
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 2000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.8f;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FX.UnlitMaterial(Color.white);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var cam = gm != null ? gm.ActiveCamera : null;
            float dt = Time.unscaledDeltaTime;

            // 빗줄기는 카메라 위에서 쏟아진다
            if (cam != null && rain != null)
            {
                Vector3 c = cam.transform.position;
                rain.transform.position = new Vector3(c.x, c.y + 14f, c.z) + cam.transform.forward * 8f;

                // 물튀김: 카메라 주변 지면에 무작위로
                int count = Mathf.RoundToInt(120f * RainIntensity * dt * 10f * QualityManager.SplashRate);
                for (int i = 0; i < count; i++)
                {
                    Vector2 off = Random.insideUnitCircle * 14f;
                    var from = new Vector3(c.x + off.x, c.y + 12f, c.z + off.y);
                    if (Physics.Raycast(from, Vector3.down, out var hit, 30f, ~(1 << 2 | 1 << 1), QueryTriggerInteraction.Ignore))
                    {
                        var ep = new ParticleSystem.EmitParams
                        {
                            position = hit.point + Vector3.up * 0.02f,
                            velocity = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.8f, 1.6f), Random.Range(-0.4f, 0.4f)),
                            startSize = Random.Range(0.025f, 0.05f),
                            startLifetime = Random.Range(0.12f, 0.22f),
                            startColor = new Color(0.7f, 0.75f, 0.85f, 0.45f),
                        };
                        splash.Emit(ep, 1);
                    }
                }
            }

            // 빗줄기 세기 변화 (가끔 거세졌다 잦아든다)
            if (Time.time > nextRainShift)
            {
                rainTarget = Random.value < 0.35f ? Random.Range(0.85f, 1f) : Random.Range(0.45f, 0.75f);
                nextRainShift = Time.time + Random.Range(25f, 50f);
            }
            RainIntensity = Mathf.MoveTowards(RainIntensity, rainTarget, dt * 0.05f);
            if (rain != null)
            {
                var em = rain.emission;
                em.rateOverTime = Mathf.Lerp(2000f, 7000f, RainIntensity) * QualityManager.RainRate;
            }
            if (rainAudio != null) rainAudio.volume = Mathf.Lerp(0.3f, 0.75f, RainIntensity);

            // 천둥·번개
            if (Time.time > nextThunder)
            {
                flash = 1f;
                thunderDelay = Random.Range(0.6f, 2.5f);
                nextThunder = Time.time + Random.Range(25f, 60f);
            }
            if (thunderDelay > 0f)
            {
                thunderDelay -= dt;
                if (thunderDelay <= 0f) SFX.Play(SFX.Thunder, 0.85f, 0.08f);
            }
            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - dt * 3f);
                // 두 번 번쩍
                float f = flash > 0.7f ? 1f : flash > 0.55f ? 0.1f : flash > 0.4f ? 0.8f : flash * 0.5f;
                if (moon != null) moon.intensity = baseMoon + f * 2.4f;
                RenderSettings.ambientLight = baseAmbient + new Color(0.35f, 0.4f, 0.5f) * f;
                RenderSettings.fogColor = FogColor + new Color(0.12f, 0.14f, 0.18f) * f;
            }
            else if (moon != null)
            {
                moon.intensity = baseMoon;
                RenderSettings.ambientLight = baseAmbient;
                RenderSettings.fogColor = FogColor;
            }
            if (cam != null)
            {
                cam.backgroundColor = RenderSettings.fogColor;
                cam.farClipPlane = QualityManager.FarClip;
            }
        }
    }
}

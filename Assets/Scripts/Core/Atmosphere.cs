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

        // ---- 서낭당(밤과 밤 사이): 날이 밝는다 ----
        /// <summary>0 = 밤, 1 = 새벽. 밤을 버티면 천천히 밝아지고, 다음 밤이 오면 다시 어두워진다.</summary>
        public float Dawn { get; private set; }
        static readonly Color DawnAmbient = new Color(0.2f, 0.22f, 0.27f);
        static readonly Color DawnFog = new Color(0.32f, 0.34f, 0.38f);
        static readonly Color SunColor = new Color(1f, 0.82f, 0.66f);
        static readonly Color MoonColor = new Color(0.55f, 0.65f, 0.85f);
        bool dawnBell;

        // ---- 밤마다 바뀌는 날씨 ----
        public enum Weather { Drizzle, Downpour, Fog, Storm, Clearing }
        public Weather Current { get; private set; } = Weather.Drizzle;
        float rainLo = 0.45f, rainHi = 0.75f, fogMult = 1f, fogNow = 1f, thunderLo = 25f, thunderHi = 60f, moonMult = 1f;

        public static string WeatherName(Weather w)
        {
            switch (w)
            {
                case Weather.Downpour: return "장대비 — 빗소리가 발소리를 덮는다";
                case Weather.Fog: return "짙은 안개 — 열 걸음 앞도 흐리다";
                case Weather.Storm: return "뇌우 — 번개가 잦다";
                case Weather.Clearing: return "비가 잦아든다 — 달빛이 비친다";
                default: return "보슬비";
            }
        }

        /// <summary>밤이 시작될 때 날씨를 고른다 (첫 밤은 보슬비, 대감의 밤은 뇌우).</summary>
        bool redMoon; // 대감의 밤: 핏빛 달

        public void SetWeather(int night, bool bossNight)
        {
            redMoon = bossNight;
            Weather w;
            if (night <= 1) w = Weather.Drizzle;
            else if (bossNight) w = Weather.Storm;
            else
            {
                float r = Random.value;
                w = r < 0.28f ? Weather.Downpour : r < 0.5f ? Weather.Fog : r < 0.68f ? Weather.Storm : r < 0.82f ? Weather.Clearing : Weather.Drizzle;
            }
            Current = w;
            switch (w)
            {
                case Weather.Downpour: rainLo = 0.85f; rainHi = 1f; fogMult = 1.25f; thunderLo = 20f; thunderHi = 45f; moonMult = 0.8f; break;
                case Weather.Fog: rainLo = 0.25f; rainHi = 0.45f; fogMult = 1.9f; thunderLo = 60f; thunderHi = 120f; moonMult = 0.7f; break;
                case Weather.Storm: rainLo = 0.7f; rainHi = 1f; fogMult = 1.1f; thunderLo = 7f; thunderHi = 16f; moonMult = 0.85f; break;
                case Weather.Clearing: rainLo = 0.1f; rainHi = 0.3f; fogMult = 0.7f; thunderLo = 90f; thunderHi = 180f; moonMult = 1.6f; break;
                default: rainLo = 0.45f; rainHi = 0.75f; fogMult = 1f; thunderLo = 25f; thunderHi = 60f; moonMult = 1f; break;
            }
            rainTarget = Random.Range(rainLo, rainHi);
            nextRainShift = Time.time + Random.Range(20f, 40f);
            nextThunder = Time.time + Random.Range(thunderLo * 0.3f, thunderLo);
            if (night > 1) UI.HUD.Notify("날씨 — " + WeatherName(w), new Color(0.7f, 0.8f, 0.95f));
        }

        static readonly Color FogColor = new Color(0.035f, 0.042f, 0.05f);
        const float BaseFog = 0.032f;

        Light moon;
        ParticleSystem rain, splash;
        AudioSource rainAudio;
        float baseMoon = 0.22f;
        float nextThunder, flash, thunderDelay = -1f;
        float rainTarget = 0.7f, nextRainShift, nextDistant = 20f;
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
                bool surge = Random.value < 0.25f;
                rainTarget = surge ? Mathf.Min(1f, rainHi + 0.15f) : Random.Range(rainLo, rainHi); // 가끔 잠깐 거세진다
                var gmr = GameManager.Instance;
                if (surge && rainTarget > RainIntensity + 0.1f && gmr != null && gmr.State == GameState.Playing && Dawn < 0.1f)
                    Hunt.UI.HUD.Notify("소나기가 쏟아진다 — 빗소리가 발소리·총소리를 덮는다", new Color(0.65f, 0.8f, 1f));
                nextRainShift = Time.time + Random.Range(25f, 50f);
            }
            RainIntensity = Mathf.MoveTowards(RainIntensity, rainTarget, dt * 0.05f);
            if (rain != null)
            {
                var em = rain.emission;
                em.rateOverTime = Mathf.Lerp(2000f, 7000f, RainIntensity) * QualityManager.RainRate * Mathf.Lerp(1f, 0.12f, Dawn); // 새벽엔 빗방울만
            }
            // 새벽: 서낭당에 있는 동안 밝아진다
            bool day = gm != null && gm.State == GameState.Intermission;
            Dawn = Mathf.MoveTowards(Dawn, day ? 1f : 0f, dt * (day ? 0.12f : 0.4f));
            if (day && Dawn > 0.5f && !dawnBell) { dawnBell = true; SFX.Play(SFX.Gong, 0.35f, 0f); } // 멀리서 새벽 종
            if (!day) dawnBell = false;
            if (moon != null)
            {
                // 달은 서쪽 높이 → 해는 동쪽 낮게
                moon.transform.rotation = Quaternion.Slerp(Quaternion.Euler(38f, -140f, 0f), Quaternion.Euler(14f, 70f, 0f), Dawn);
                moon.color = Color.Lerp(MoonColor, SunColor, Dawn);
            }
            if (rainAudio != null) rainAudio.volume = Mathf.Lerp(0.3f, 0.75f, RainIntensity) * Mathf.Lerp(1f, 0.35f, Dawn);
            // 안개는 날씨에 맞춰 천천히 짙어지거나 걷힌다
            fogNow = Mathf.MoveTowards(fogNow, fogMult, dt * 0.08f);
            RenderSettings.fogDensity = BaseFog * fogNow * Mathf.Lerp(1f, 0.55f, Dawn);

            // 멀리서 들려오는 소리: 어딘가의 그르렁, 개 짖는 듯한 울음, 징 — 위치가 들리는 3D (긴장감)
            if (gm != null && gm.State == GameState.Playing && cam != null && Time.time > nextDistant)
            {
                nextDistant = Time.time + Random.Range(14f, 28f);
                Vector2 r2 = Random.insideUnitCircle.normalized * Random.Range(25f, 40f);
                Vector3 at = cam.transform.position + new Vector3(r2.x, 2f, r2.y);
                float roll = Random.value;
                if (roll < 0.45f) SFX.PlayAt3D(SFX.ZombieMoan, at, 0.9f, Random.Range(0.8f, 1.05f));
                else if (roll < 0.62f) SFX.PlayAt3D(SFX.Knock, at, 0.8f, Random.Range(0.85f, 1.1f)); // 어딘가에서 문을 두드린다
                else if (roll < 0.85f) SFX.PlayAt(SFX.MonsterDie, at, 0.7f);
                else SFX.PlayAt(SFX.Gong, at, 0.35f);
            }

            // 천둥·번개
            if (Time.time > nextThunder && Dawn < 0.3f) // 새벽엔 천둥이 멎는다
            {
                flash = 1f;
                thunderDelay = Random.Range(0.6f, 2.5f);
                SpawnBolt(cam); // 먼 하늘에 번개 줄기
                nextThunder = Time.time + Random.Range(thunderLo, thunderHi);
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
                if (moon != null) moon.intensity = Mathf.Lerp(baseMoon * moonMult, 0.75f, Dawn) + f * 2.4f;
                RenderSettings.ambientLight = (Color.Lerp(baseAmbient, DawnAmbient, Dawn) + new Color(0.35f, 0.4f, 0.5f) * f) * GameSettings.Brightness;
                RenderSettings.fogColor = Color.Lerp(FogColor, DawnFog, Dawn) + new Color(0.12f, 0.14f, 0.18f) * f;
            }
            else if (moon != null)
            {
                moon.intensity = Mathf.Lerp(baseMoon * moonMult, 0.75f, Dawn) * Mathf.Sqrt(GameSettings.Brightness);
                RenderSettings.ambientLight = Color.Lerp(baseAmbient, DawnAmbient, Dawn) * GameSettings.Brightness; // Esc '밝기'
                RenderSettings.fogColor = Color.Lerp(FogColor, DawnFog, Dawn);
            }
            if (cam != null)
            {
                cam.backgroundColor = RenderSettings.fogColor;
                cam.farClipPlane = QualityManager.FarClip;
                UpdateMoonDisc(cam);
            }
        }

        /// <summary>번개 줄기: 먼 하늘에서 땅으로 지그재그로 떨어지는 선 (0.25초). 천둥이 늦게 올수록 멀리.</summary>
        void SpawnBolt(Camera cam)
        {
            if (cam == null) return;
            float dist = cam.farClipPlane * 0.8f;
            Vector2 d2 = Random.insideUnitCircle.normalized;
            Vector3 baseP = cam.transform.position + new Vector3(d2.x, 0f, d2.y) * dist;
            baseP.y = 0f;
            var go = new GameObject("번개");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = FX.UnlitMaterial(Color.white);
            lr.useWorldSpace = true;
            const int n = 9;
            lr.positionCount = n;
            float top = dist * 0.55f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 p = baseP + Vector3.up * Mathf.Lerp(top, 0f, t);
                if (i > 0 && i < n - 1) p += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * dist * 0.035f;
                lr.SetPosition(i, p);
            }
            lr.startWidth = dist * 0.006f;
            lr.endWidth = dist * 0.003f;
            lr.startColor = new Color(0.85f, 0.9f, 1f, 0.95f);
            lr.endColor = new Color(0.75f, 0.82f, 1f, 0.7f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Destroy(go, 0.25f);
        }

        // ---- 하늘의 달: 비구름 사이로 흐릿하게 비친다 (빛 방향과 같은 쪽, 늘 멀리) ----
        Transform moonDisc;
        Material moonMat;

        void UpdateMoonDisc(Camera cam)
        {
            if (moon == null) return;
            if (moonDisc == null)
            {
                const int S = 128;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "MoonDisc" };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = (x - S / 2f + 0.5f) / (S / 2f), dy = (y - S / 2f + 0.5f) / (S / 2f);
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float disc = Mathf.Clamp01((0.42f - r) * 40f);                       // 달 몸
                        float halo = Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r) * 0.35f;   // 둘레 빛무리
                        float spots = 0.82f + 0.18f * Mathf.PerlinNoise(x * 0.09f + 3f, y * 0.09f); // 얼룩
                        float a = Mathf.Max(disc, halo);
                        float v = disc > 0f ? spots : 1f;
                        px[y * S + x] = new Color32((byte)(225 * v), (byte)(232 * v), (byte)(245 * v), (byte)(255 * a));
                    }
                tex.SetPixels32(px);
                tex.Apply(true, true);
                moonMat = new Material(Shader.Find("Sprites/Default")) { name = "MoonDisc", mainTexture = tex };
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "MoonDisc";
                q.layer = 2;
                q.transform.SetParent(transform, false);
                var r2 = q.GetComponent<Renderer>();
                r2.sharedMaterial = moonMat;
                r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r2.receiveShadows = false;
                moonDisc = q.transform;
            }
            float dist = cam.farClipPlane * 0.85f;
            Vector3 dir = -moon.transform.forward;              // 달빛이 오는 쪽
            if (dir.y < 0.15f) dir = (dir + Vector3.up * 0.4f).normalized;
            moonDisc.position = cam.transform.position + dir * dist;
            moonDisc.rotation = Quaternion.LookRotation(moonDisc.position - cam.transform.position);
            moonDisc.localScale = Vector3.one * dist * 0.16f;
            // 구름이 지나가며 흐려졌다 밝아진다 · 날씨(맑아짐은 또렷, 폭우·안개는 거의 안 보임) · 새벽엔 사라짐
            float cloud = Mathf.Lerp(0.35f, 1f, Mathf.PerlinNoise(Time.time * 0.05f, 7.3f));
            float a2 = Mathf.Clamp01((moonMult - 0.6f) * 1.1f) * cloud * (1f - Dawn) * (1f - RainIntensity * 0.45f);
            if (redMoon) a2 = Mathf.Max(a2, 0.75f * cloud * (1f - Dawn)); // 대감의 밤엔 폭풍 속에서도 핏빛 달이 또렷하다
            moonMat.color = redMoon ? new Color(1f, 0.35f, 0.28f, a2) : new Color(1f, 1f, 1f, a2);
        }
    }
}

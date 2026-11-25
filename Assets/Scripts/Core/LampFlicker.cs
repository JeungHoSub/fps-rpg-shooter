using UnityEngine;

namespace Hunt.Core
{
    /// <summary>불빛 흔들림: 가로등(고장 나서 깜빡임), 창문 불빛(미세한 흔들림), 고분의 붉은 빛(맥동).</summary>
    public class LampFlicker : MonoBehaviour
    {
        public bool broken;  // 가끔 꺼졌다 켜짐
        public bool subtle;  // 촛불처럼 살짝
        public bool pulse;   // 숨쉬듯 천천히
        /// <summary>이 품질 이상일 때만 켠다 (저사양에서 조명 수 줄이기).</summary>
        public GfxLevel minLevel = GfxLevel.Low;

        /// <summary>정전 시간 (Atmosphere가 벼락 때 정한다). ShootableLamp.PlayerLit도 이 동안은 거짓.</summary>
        public static float BlackoutFrom = -100f, BlackoutUntil = -100f;
        public static bool Blackout => Time.time > BlackoutFrom && Time.time < BlackoutUntil;

        Light lt;
        float baseIntensity, seed, offUntil, nextGlitch;

        // 빛무리 (빗속에서 불빛 둘레가 번져 보이는 둥근 빛). 실제 조명을 끈 저사양에서도 보여서 '하' 품질도 밤 불빛이 산다
        Transform halo;
        Renderer haloR;
        MaterialPropertyBlock haloMpb;
        Color haloCol;
        float haloSize;
        static Material haloMat;
        static Texture2D haloTex;

        void Start()
        {
            lt = GetComponent<Light>();
            if (lt != null) baseIntensity = lt.intensity;
            seed = Random.value * 100f;
            nextGlitch = Time.time + Random.Range(2f, 6f);
            if (lt != null) MakeHalo();
        }

        void MakeHalo()
        {
            if (haloTex == null)
            {
                const int N = 64;
                haloTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - r);
                        a = a * a * (0.35f + 0.65f * a); // 가운데 진하고 바깥으로 부드럽게
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                haloTex.SetPixels32(px);
                haloTex.Apply();
            }
            if (haloMat == null) haloMat = new Material(Shader.Find("Sprites/Default")) { name = "LampHalo", mainTexture = haloTex };
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Halo";
            q.layer = 2;
            halo = q.transform;
            halo.SetParent(transform, false);
            haloR = q.GetComponent<Renderer>();
            haloR.sharedMaterial = haloMat;
            haloR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            haloR.receiveShadows = false;
            haloMpb = new MaterialPropertyBlock();
            haloCol = lt.color;
            haloSize = Mathf.Clamp(lt.range * 0.16f, 0.8f, 2.4f);
        }

        void LateUpdate()
        {
            if (halo == null) return;
            var gm = GameManager.Instance;
            var cam = gm != null ? gm.ActiveCamera : null;
            if (cam == null) return;
            halo.rotation = cam.transform.rotation; // 늘 나를 본다
            float d = Vector3.Distance(cam.transform.position, halo.position);
            halo.localScale = Vector3.one * haloSize / Mathf.Max(0.01f, transform.lossyScale.x);
            // 멀수록 안개에 묻혀 옅게, 아주 가까우면 눈부시지 않게 옅게
            float fog = Mathf.Exp(-RenderSettings.fogDensity * d * 1.2f);
            float nearFade = Mathf.Clamp01((d - 0.8f) / 2f);
            float a = 0.38f * haloK * fog * nearFade;
            haloR.enabled = a > 0.01f;
            if (!haloR.enabled) return;
            haloMpb.SetColor("_Color", new Color(haloCol.r, haloCol.g, haloCol.b, a));
            haloR.SetPropertyBlock(haloMpb);
        }

        float haloK = 1f;

        void Update()
        {
            if (lt == null) return;
            bool want = QualityManager.Level >= minLevel;
            if (lt.enabled != want) lt.enabled = want;
            float t = Time.time;
            float k = 1f;
            if (pulse) k = 0.6f + 0.4f * Mathf.Sin(t * 1.3f + seed);
            else if (subtle) k = 0.85f + 0.15f * Mathf.PerlinNoise(seed, t * 3f);
            else k = 0.94f + 0.06f * Mathf.PerlinNoise(seed, t * 8f);

            if (broken)
            {
                if (t > nextGlitch)
                {
                    offUntil = t + Random.Range(0.05f, 0.6f);
                    nextGlitch = t + Random.Range(0.4f, 5f);
                }
                if (t < offUntil) k *= Random.value < 0.5f ? 0.05f : 0.4f;
            }
            // 보스가 가까우면 마을 불빛이 모두 떨린다 (Flashlight가 0.25초마다 잰 값)
            float bn = Hunt.Player.Flashlight.BossNear;
            if (bn > 0f && GameSettings.ShakeMult > 0f && Mathf.PerlinNoise(seed + t * (7f + bn * 12f), 1.7f) < 0.15f + bn * 0.2f) k *= 0.25f;
            // 정전: 벼락 뒤 20초 동안 전깃불이 꺼진다 (촛불·등잔은 그대로, 켜질 때 몇 번 깜빡)
            if (!subtle && !pulse && t > BlackoutFrom && t < BlackoutUntil) k = t > BlackoutUntil - 1.2f && Mathf.PerlinNoise(seed, t * 20f) > 0.5f ? k * 0.6f : 0f;
            haloK = k;
            if (want) lt.intensity = baseIntensity * k;
        }
    }
}

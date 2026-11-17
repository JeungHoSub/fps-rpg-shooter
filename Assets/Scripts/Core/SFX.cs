using System;
using System.Collections.Generic;
using Hunt.Combat;
using UnityEngine;

namespace Hunt.Core
{
    /// <summary>
    /// 코드로 합성하는 임시 사운드 (외부 에셋 없음). 정식 사운드가 들어오면 클립만 교체하면 된다.
    /// 플레이어 소리는 2D, 괴물 소리는 3D 위치 재생.
    /// </summary>
    public static class SFX
    {
        const int Rate = 44100;
        const int Voices = 10;

        static readonly AudioSource[] voices = new AudioSource[Voices];
        static int nextVoice;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static float lastHurt;

        public static void Init(GameObject host)
        {
            if (voices[0] != null) return;
            for (int i = 0; i < Voices; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                voices[i] = s;
            }
        }

        public static void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0.04f)
        {
            if (clip == null || voices[0] == null) return;
            var s = voices[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            s.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
            s.volume = volume;
            s.clip = clip;
            s.Play();
        }

        public static void PlayAt(AudioClip clip, Vector3 pos, float volume = 1f)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, pos, volume);
        }

        /// <summary>괴물 피격음은 너무 잦지 않게 제한.</summary>
        public static void MonsterHurtAt(Vector3 pos)
        {
            if (Time.time - lastHurt < 0.09f) return;
            lastHurt = Time.time;
            PlayAt(MonsterHurt, pos, 0.7f);
        }

        // ------------------------------------------------------------------ 클립
        public static AudioClip Shot(WeaponClass c) => Get("shot_" + c, () => MakeShot(c));
        public static AudioClip Empty => Get("empty", () => MakeClick(0.05f, 2600f, 0.5f));
        public static AudioClip MagOut => Get("magout", () => MakeClick(0.09f, 900f, 0.8f));
        public static AudioClip MagIn => Get("magin", () => MakeClick(0.12f, 1300f, 1f));
        public static AudioClip Rack => Get("rack", () => MakeRack());
        public static AudioClip Swap => Get("swap", () => MakeClick(0.07f, 700f, 0.6f));
        public static AudioClip HitTick => Get("hit", () => MakeTone(0.045f, 1500f, 1500f, 0.5f));
        public static AudioClip WeakTick => Get("weak", () => MakeTone(0.06f, 2300f, 2600f, 0.6f));
        public static AudioClip KillTick => Get("kill", () => MakeKill());
        public static AudioClip MonsterHurt => Get("mhurt", () => MakeGrowl(0.25f, 70f, 1f));
        public static AudioClip MonsterDie => Get("mdie", () => MakeGrowl(0.7f, 55f, 0.6f));
        public static AudioClip PlayerHurt => Get("phurt", () => MakeThud());
        public static AudioClip UiHover => Get("uihover", () => MakeTone(0.035f, 900f, 1100f, 0.18f));
        public static AudioClip UiClick => Get("uiclick", () => MakeClick(0.06f, 520f, 0.45f));
        /// <summary>출정·웨이브 시작: 낮게 울리는 징 소리.</summary>
        public static AudioClip Gong => Get("gong", () => MakeGong());
        public static AudioClip LevelUp => Get("levelup", () => MakeChime());

        public static AudioClip RainLoop => Get("rain", () => MakeRain());
        public static AudioClip Thunder => Get("thunder", () => MakeThunder());
        public static AudioClip FlashlightClick => Get("flash", () => MakeClick(0.04f, 3000f, 0.35f));

        /// <summary>빗소리 루프 (6초, 이음매 없이 반복). 넓은 대역 쏴— 소리 + 지붕·웅덩이에 떨어지는 빗방울.</summary>
        static AudioClip MakeRain()
        {
            int n = Rate * 6;
            var d = new float[n];
            var rnd = new System.Random(77);
            float b0 = 0, b1 = 0, b2 = 0, hp = 0, prev = 0;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                // 핑크 노이즈 근사
                b0 = 0.99765f * b0 + white * 0.099046f;
                b1 = 0.963f * b1 + white * 0.2965164f;
                b2 = 0.57f * b2 + white * 1.0526913f;
                float pink = (b0 + b1 + b2 + white * 0.1848f) * 0.18f;
                // 고역만 살짝 (쏴—)
                hp = 0.92f * (hp + white - prev);
                prev = white;
                d[i] = pink * 0.55f + hp * 0.06f;
            }
            // 빗방울 톡톡
            for (int k = 0; k < 2600; k++)
            {
                int at = rnd.Next(n);
                float amp = (float)rnd.NextDouble() * 0.25f + 0.05f;
                float freq = 1800f + (float)rnd.NextDouble() * 3500f;
                int len = 500;
                for (int j = 0; j < len && at + j < n; j++)
                {
                    float t = (float)j / Rate;
                    d[at + j] += Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 900f) * amp;
                }
            }
            // 처음과 끝을 겹쳐 이음매 제거
            int fade = Rate / 2;
            for (int i = 0; i < fade; i++)
            {
                float t = (float)i / fade;
                d[i] = d[i] * t + d[n - fade + i] * (1f - t);
            }
            var trimmed = new float[n - fade];
            System.Array.Copy(d, trimmed, trimmed.Length);
            for (int i = 0; i < trimmed.Length; i++) trimmed[i] = SoftClip(trimmed[i] * 1.2f);
            return Clip("rain", trimmed);
        }

        /// <summary>천둥: 멀리서 우르릉 굴러오는 저음.</summary>
        static AudioClip MakeThunder()
        {
            int n = (int)(Rate * 5f);
            var d = new float[n];
            var rnd = new System.Random(99);
            float f1 = 0f, f2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f1 += 0.02f * (noise - f1);
                f2 += 0.004f * (f1 - f2);
                float env = Mathf.Clamp01(t * 3f) * Mathf.Exp(-t * 0.8f) * (0.7f + 0.3f * Mathf.Sin(t * 7f + Mathf.Sin(t * 2.3f) * 3f));
                float crack = t < 0.3f ? noise * Mathf.Exp(-t * 12f) * 0.25f : 0f;
                d[i] = SoftClip((f1 * 18f + f2 * 40f) * env + crack);
            }
            return Clip("thunder", d);
        }

        static AudioClip MakeGong()
        {
            int n = (int)(Rate * 2.4f);
            var d = new float[n];
            var rnd = new System.Random(41);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float env = Mathf.Exp(-t * 1.6f);
                float beat = 1f + 0.25f * Mathf.Sin(2f * Mathf.PI * 3.1f * t); // 징 특유의 맥놀이
                float s = Mathf.Sin(2f * Mathf.PI * 92f * t) * 0.7f
                          + Mathf.Sin(2f * Mathf.PI * 138f * t) * 0.35f
                          + Mathf.Sin(2f * Mathf.PI * 231f * t) * 0.2f * Mathf.Exp(-t * 2f)
                          + noise * Mathf.Exp(-t * 60f) * 0.4f;
                d[i] = SoftClip(s * env * beat * 1.2f) * Mathf.Clamp01(t * 400f);
            }
            return Clip("gong", d);
        }

        static AudioClip MakeChime()
        {
            int n = (int)(Rate * 0.9f);
            var d = new float[n];
            float[] notes = { 523f, 659f, 784f, 1046f };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float s = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float st = k * 0.09f;
                    if (t < st) continue;
                    float lt = t - st;
                    s += Mathf.Sin(2f * Mathf.PI * notes[k] * lt) * Mathf.Exp(-lt * 4f) * 0.3f;
                }
                d[i] = s;
            }
            return Clip("chime", d);
        }

        static AudioClip Get(string key, Func<AudioClip> make)
        {
            if (!cache.TryGetValue(key, out var c) || c == null)
            {
                c = make();
                c.name = key;
                cache[key] = c;
            }
            return c;
        }

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static float SoftClip(float x) => x / (1f + Mathf.Abs(x));

        /// <summary>총성 = 초반 날카로운 파열음 + 저역 필터 노이즈 몸통 + 저음 쿵 + 잔향 꼬리.</summary>
        static AudioClip MakeShot(WeaponClass c)
        {
            float len, decay, thumpHz, thumpDecay, lp, crack, tail;
            switch (c)
            {
                case WeaponClass.Pistol: len = 0.35f; decay = 22f; thumpHz = 120f; thumpDecay = 20f; lp = 0.35f; crack = 0.7f; tail = 0.12f; break;
                case WeaponClass.SMG: len = 0.25f; decay = 30f; thumpHz = 150f; thumpDecay = 26f; lp = 0.45f; crack = 0.55f; tail = 0.08f; break;
                case WeaponClass.Shotgun: len = 0.7f; decay = 9f; thumpHz = 60f; thumpDecay = 8f; lp = 0.22f; crack = 0.9f; tail = 0.2f; break;
                case WeaponClass.Sniper: len = 1.1f; decay = 7f; thumpHz = 48f; thumpDecay = 5f; lp = 0.28f; crack = 1.1f; tail = 0.25f; break;
                case WeaponClass.LMG: len = 0.35f; decay = 19f; thumpHz = 85f; thumpDecay = 15f; lp = 0.33f; crack = 0.8f; tail = 0.12f; break;
                default: len = 0.4f; decay = 17f; thumpHz = 95f; thumpDecay = 14f; lp = 0.38f; crack = 0.85f; tail = 0.14f; break;
            }
            int n = (int)(Rate * len);
            var d = new float[n];
            var rnd = new System.Random(1000 + (int)c);
            float f1 = 0f, f2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f1 += lp * (noise - f1);
                f2 += 0.05f * (noise - f2);
                float s = noise * Mathf.Exp(-t * 140f) * crack
                          + f1 * Mathf.Exp(-t * decay) * 1.6f
                          + Mathf.Sin(2f * Mathf.PI * thumpHz * t * (1f - t * 0.6f)) * Mathf.Exp(-t * thumpDecay) * 0.9f
                          + f2 * Mathf.Exp(-t * 4f) * tail * 6f;
                float attack = Mathf.Clamp01(t * 2000f);
                d[i] = SoftClip(s * 1.4f) * attack * 0.9f;
            }
            return Clip("shot", d);
        }

        static AudioClip MakeClick(float len, float hz, float amp)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            var rnd = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float click = noise * Mathf.Exp(-t * 300f) + Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * 80f) * 0.6f;
                float second = t > len * 0.45f ? noise * Mathf.Exp(-(t - len * 0.45f) * 350f) * 0.7f : 0f;
                d[i] = SoftClip((click + second) * amp);
            }
            return Clip("click", d);
        }

        static AudioClip MakeRack()
        {
            int n = (int)(Rate * 0.25f);
            var d = new float[n];
            var rnd = new System.Random(11);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float a = noise * Mathf.Exp(-t * 250f);
                float b = t > 0.11f ? noise * Mathf.Exp(-(t - 0.11f) * 220f) * 1.2f + Mathf.Sin(2f * Mathf.PI * 1100f * t) * Mathf.Exp(-(t - 0.11f) * 60f) * 0.5f : 0f;
                d[i] = SoftClip(a + b);
            }
            return Clip("rack", d);
        }

        static AudioClip MakeTone(float len, float hzStart, float hzEnd, float amp)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float hz = Mathf.Lerp(hzStart, hzEnd, t / len);
                phase += 2f * Mathf.PI * hz / Rate;
                d[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 50f) * amp;
            }
            return Clip("tone", d);
        }

        static AudioClip MakeKill()
        {
            int n = (int)(Rate * 0.2f);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float hz = t < 0.07f ? 1200f : 1800f;
                phase += 2f * Mathf.PI * hz / Rate;
                float env = t < 0.07f ? Mathf.Exp(-t * 30f) : Mathf.Exp(-(t - 0.07f) * 25f);
                d[i] = Mathf.Sin(phase) * env * 0.5f;
            }
            return Clip("kill", d);
        }

        /// <summary>괴물 울음: 저역 노이즈를 낮은 주파수로 진폭 변조.</summary>
        static AudioClip MakeGrowl(float len, float modHz, float pitchDrop)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            var rnd = new System.Random(23);
            float f = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f += 0.08f * (noise - f);
                float hz = modHz * (1f - t / len * pitchDrop * 0.5f);
                phase += 2f * Mathf.PI * hz / Rate;
                float env = Mathf.Clamp01(t * 30f) * Mathf.Clamp01((len - t) * 6f);
                d[i] = SoftClip(f * 6f * (0.6f + 0.4f * Mathf.Sin(phase)) * env);
            }
            return Clip("growl", d);
        }

        static AudioClip MakeThud()
        {
            int n = (int)(Rate * 0.25f);
            var d = new float[n];
            var rnd = new System.Random(31);
            float f = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f += 0.1f * (noise - f);
                d[i] = SoftClip((Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.8f + f * 3f) * Mathf.Exp(-t * 18f));
            }
            return Clip("thud", d);
        }
    }
}

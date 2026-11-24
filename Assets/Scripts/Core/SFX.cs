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
        // 총성 전용 음성 (연사해도 다른 소리를 밀어내지 않게)
        const int GunVoices = 10;
        static readonly AudioSource[] gunVoices = new AudioSource[GunVoices];
        static int nextGun;
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
            for (int i = 0; i < GunVoices; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.priority = 0; // 총성은 가장 먼저
                gunVoices[i] = s;
            }
        }

        // ------------------------------------------------------------------ 실제 녹음 총성
        // Resources/Sounds/Guns/<이름>_1..3.ogg — The Free Firearm Sound Library (CC0, 실총 녹음)
        // 무기 id → (녹음 이름, 음높이). 음높이를 낮추면 더 묵직한 총이 된다.
        static readonly Dictionary<string, (string file, float pitch, float vol)> GunRec = new Dictionary<string, (string, float, float)>
        {
            { "pistol",      ("ppq", 1.0f, 0.8f) },
            { "pistol2",     ("m1911", 1.0f, 0.85f) },
            { "revolver",    ("sw642", 0.82f, 1f) },
            { "ar",          ("ar15", 1.0f, 0.85f) },
            { "k2c",         ("ak47", 1.04f, 0.85f) },
            { "br762",       ("sks", 0.92f, 0.95f) },
            { "smg",         ("m45", 1.06f, 0.75f) },
            { "ump",         ("m45", 0.94f, 0.8f) },
            { "vector",      ("ppsh", 1.08f, 0.75f) },
            { "shotgun",     ("nova", 1.0f, 1f) },
            { "autoshotgun", ("model12", 1.05f, 0.95f) },
            { "doublebarrel",("cd", 0.9f, 1f) },
            { "sniper",      ("tikka", 1.0f, 1f) },
            { "dmr",         ("savage", 1.0f, 0.95f) },
            { "antimat",     ("m1917", 0.72f, 1f) },
            { "lmg",         ("ak47", 0.9f, 0.85f) },
            { "k3",          ("ar15", 0.88f, 0.85f) },
            { "hmg",         ("mosin", 0.85f, 0.8f) },
        };
        static readonly Dictionary<string, AudioClip[]> recCache = new Dictionary<string, AudioClip[]>();

        static AudioClip[] LoadRecorded(string folder, string name, int max)
        {
            string key = folder + "/" + name;
            if (recCache.TryGetValue(key, out var arr)) return arr;
            var list = new List<AudioClip>();
            for (int i = 1; i <= max; i++)
            {
                var c = Resources.Load<AudioClip>($"Sounds/{folder}/{name}_{i}");
                if (c != null) list.Add(c);
            }
            if (list.Count == 0) { var single = Resources.Load<AudioClip>($"Sounds/{folder}/{name}"); if (single != null) list.Add(single); }
            arr = list.ToArray();
            recCache[key] = arr;
            return arr;
        }

        static AudioClip Mech(string name, Func<AudioClip> fallback)
        {
            var a = LoadRecorded("Mech", name, 1);
            return a.Length > 0 ? a[0] : Get(name, fallback);
        }

        /// <summary>
        /// 총 한 발: 실총 녹음(있으면, 무작위 테이크) + 아주 낮은 '쿵' 층(가슴을 치는 저음) 으로 타격감을 더한다.
        /// 녹음이 없으면 예전 합성음.
        /// </summary>
        public static void PlayShot(Hunt.Data.WeaponData d, float volume)
        {
            if (gunVoices[0] == null || d == null) return;
            AudioClip clip = null;
            float pitch = 1f, vol = volume;
            if (GunRec.TryGetValue(d.id, out var rec))
            {
                var takes = LoadRecorded("Guns", rec.file, 3);
                if (takes.Length > 0)
                {
                    clip = takes[UnityEngine.Random.Range(0, takes.Length)];
                    pitch = rec.pitch;
                    vol *= rec.vol;
                }
            }
            if (clip == null) clip = ShotFor(d);
            var s = gunVoices[nextGun];
            nextGun = (nextGun + 1) % GunVoices;
            s.pitch = pitch * (1f + UnityEngine.Random.Range(-0.035f, 0.035f));
            s.volume = vol;
            s.clip = clip;
            s.Play();
            Echo(clip, pitch, vol, d.weaponClass);

            // 저음 층: 무거운 총일수록 크게
            float thump;
            switch (d.weaponClass)
            {
                case WeaponClass.Shotgun: thump = 0.75f; break;
                case WeaponClass.Sniper: thump = 0.8f; break;
                case WeaponClass.LMG: thump = 0.45f; break;
                case WeaponClass.Rifle: thump = 0.4f; break;
                case WeaponClass.SMG: thump = 0.25f; break;
                case WeaponClass.Pistol: thump = d.id == "revolver" ? 0.6f : 0.3f; break;
                default: thump = 0f; break;
            }
            if (thump > 0f)
            {
                var t = gunVoices[nextGun];
                nextGun = (nextGun + 1) % GunVoices;
                t.pitch = (d.weaponClass == WeaponClass.Sniper || d.weaponClass == WeaponClass.Shotgun ? 0.85f : 1f) * (1f + UnityEngine.Random.Range(-0.05f, 0.05f));
                t.volume = volume * thump;
                t.clip = Thump;
                t.Play();
            }
        }

        // ---- 총성 메아리: 산과 집에 부딪혀 0.25초쯤 뒤 낮고 흐리게 되돌아온다 (연사 땐 0.3초에 한 번만) ----
        static AudioSource echo;
        static float lastEcho = -10f;

        static void Echo(AudioClip clip, float pitch, float vol, WeaponClass c)
        {
            if (Time.unscaledTime - lastEcho < 0.3f || gunVoices[0] == null || c == WeaponClass.Flamethrower) return;
            lastEcho = Time.unscaledTime;
            if (echo == null)
            {
                var go = new GameObject("GunEcho");
                go.transform.SetParent(gunVoices[0].transform, false);
                echo = go.AddComponent<AudioSource>();
                echo.playOnAwake = false;
                echo.spatialBlend = 0f;
                var lp = go.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = 1500f; // 멀리서 되돌아온 소리처럼 흐리게
            }
            float heavy = c == WeaponClass.Sniper || c == WeaponClass.Shotgun || c == WeaponClass.Launcher ? 1.4f : 1f;
            echo.clip = clip;
            echo.pitch = pitch * 0.94f;
            echo.volume = vol * 0.22f * heavy;
            echo.PlayDelayed(UnityEngine.Random.Range(0.22f, 0.32f));
        }

        /// <summary>가슴을 치는 저음 (55→38Hz로 떨어지는 짧은 사인 + 부드러운 클립).</summary>
        static AudioClip Thump => Get("thump", () =>
        {
            int n = (int)(Rate * 0.22f);
            var d = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float hz = Mathf.Lerp(62f, 36f, Mathf.Clamp01(t / 0.18f));
                ph += 2f * Mathf.PI * hz / Rate;
                float env = (1f - Mathf.Exp(-t * 900f)) * Mathf.Exp(-t * 16f);
                d[i] = SoftClip(Mathf.Sin(ph) * env * 1.8f);
            }
            return Clip("thump", d);
        });

        /// <summary>가까이 다가온 망자의 그르렁 (낮고 길게).</summary>
        public static AudioClip Growl => Rec("growl", 10, () => Get("growl_near", () => MakeGrowl(0.9f, 38f, 0.25f)));

        /// <summary>심장 박동 한 번 (쿵-쿵).</summary>
        public static AudioClip Heartbeat => Get("heart", () =>
        {
            int n = (int)(Rate * 0.45f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float b1 = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 28f);
                float t2 = t - 0.16f;
                float b2 = t2 > 0f ? Mathf.Sin(2f * Mathf.PI * 48f * t2) * Mathf.Exp(-t2 * 32f) * 0.7f : 0f;
                d[i] = SoftClip((b1 + b2) * 1.6f);
            }
            return Clip("heart", d);
        });

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

        /// <summary>음높이를 정해서 재생 (같은 소리를 무기마다 다르게).</summary>
        public static void PlayPitched(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || voices[0] == null) return;
            var s = voices[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            s.pitch = pitch * (1f + UnityEngine.Random.Range(-0.03f, 0.03f));
            s.volume = volume;
            s.clip = clip;
            s.Play();
        }

        public static void PlayAt(AudioClip clip, Vector3 pos, float volume = 1f)
        {
            if (clip == null) return;
            var a = Grab(volume);
            if (a == null) return;
            a.transform.position = pos;
            a.clip = clip; a.volume = volume; a.pitch = 1f;
            a.minDistance = 1f; a.maxDistance = 500f; // PlayClipAtPoint와 같은 퍼짐
            a.Play();
        }

        // ---- 3D 소리 묶음 (성능): 소리마다 오브젝트를 만들고 지우는 대신 22개를 돌려 쓴다 ----
        const int PoolSize = 22;
        static AudioSource[] pool;
        static GameObject poolRoot;

        /// <summary>쉬는 소리 칸을 하나 준다. 모두 울리는 중이면 가장 작게(거의 끝나 가는) 울리는 칸을 끊고 준다. 새 소리가 그보다 작으면 null.</summary>
        static AudioSource Grab(float volume)
        {
            if (poolRoot == null)
            {
                poolRoot = new GameObject("SFX_Pool");
                UnityEngine.Object.DontDestroyOnLoad(poolRoot);
                pool = new AudioSource[PoolSize];
                for (int i = 0; i < PoolSize; i++)
                {
                    var go = new GameObject("SFX3D");
                    go.transform.SetParent(poolRoot.transform, false);
                    var s = go.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.spatialBlend = 1f;
                    s.rolloffMode = AudioRolloffMode.Logarithmic;
                    s.dopplerLevel = 0f;
                    pool[i] = s;
                }
            }
            AudioSource weakest = null;
            float weakVal = float.MaxValue;
            for (int i = 0; i < PoolSize; i++)
            {
                var s = pool[i];
                if (s == null) continue;
                if (!s.isPlaying) return s;
                float left = s.clip != null && s.clip.length > 0f ? 1f - s.time / s.clip.length : 0f;
                float v = s.volume * (0.3f + 0.7f * left);
                if (v < weakVal) { weakVal = v; weakest = s; }
            }
            if (weakest == null || weakVal > volume) return null;
            weakest.Stop();
            return weakest;
        }

        /// <summary>괴물 피격음은 너무 잦지 않게 제한.</summary>
        public static void MonsterHurtAt(Vector3 pos)
        {
            if (Time.time - lastHurt < 0.09f) return;
            lastHurt = Time.time;
            PlayAt(MonsterHurt, pos, 0.7f);
        }

        // ------------------------------------------------------------------ 클립
        public static AudioClip Shot(WeaponClass c) => Get("shot_" + c, () => MakeShot(c, 0, 1));

        /// <summary>무기마다 고유한 총성: 계열 기본음 + 단계(구경)·이름에 따른 변주.</summary>
        public static AudioClip ShotFor(Hunt.Data.WeaponData d)
        {
            if (d.weaponClass == WeaponClass.Flamethrower) return Get("shot_flame", () => MakeFlame());
            if (d.weaponClass == WeaponClass.Launcher) return d.tier >= 3 ? Get("shot_rocket", () => MakeRocket()) : Get("shot_tube", () => MakeTube());
            return Get("shot_" + d.id, () => MakeShot(d.weaponClass, Mathf.Abs(d.id.GetHashCode()) % 997, d.tier));
        }

        /// <summary>계열마다 다른 타격음 (산탄 = 둔탁하고 큼, 저격 = 날카로운 관통음, 기관단총 = 가볍게).</summary>
        public static AudioClip HitFor(WeaponClass c)
        {
            switch (c)
            {
                case WeaponClass.Shotgun: return Get("hit_sg", () => MakeFleshVariant(0.22f, 95f, 2.8f, 0.25f, 66));
                case WeaponClass.Sniper: return Get("hit_sn", () => MakeFleshVariant(0.2f, 170f, 1.6f, 0.9f, 67));
                case WeaponClass.SMG: return Get("hit_smg", () => MakeFleshVariant(0.1f, 210f, 1.6f, 0.35f, 68));
                case WeaponClass.LMG: return Get("hit_lmg", () => MakeFleshVariant(0.15f, 120f, 2.2f, 0.4f, 69));
                case WeaponClass.Pistol: return Get("hit_p", () => MakeFleshVariant(0.13f, 150f, 1.9f, 0.4f, 70));
                case WeaponClass.Flamethrower: return Get("hit_fl", () => MakeWhoosh(0.18f, 0.35f));
                default: return FleshHit;
            }
        }

        /// <summary>탄피가 바닥에 떨어지는 소리.</summary>
        public static AudioClip Casing => Get("casing", () => MakeTone(0.06f, 4200f, 3800f, 0.12f));
        public static AudioClip ShellIn => Mech("shellin", () => MakeClick(0.08f, 1700f, 0.7f));
        public static AudioClip Bolt => Mech("bolt", () => MakeRack());

        static AudioClip MakeFleshVariant(float len, float thumpHz, float body, float crack, int seed)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            var rnd = new System.Random(seed);
            float f = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f += 0.18f * (noise - f);
                float thump = Mathf.Sin(2f * Mathf.PI * (thumpHz - t * 400f) * t) * Mathf.Exp(-t * 30f);
                d[i] = SoftClip((thump * 1.1f + f * body * Mathf.Exp(-t * 45f) + noise * Mathf.Exp(-t * 300f) * crack) * 1.3f);
            }
            return Clip("fleshv", d);
        }

        static AudioClip MakeFlame()
        {
            int n = (int)(Rate * 0.3f);
            var d = new float[n];
            var rnd = new System.Random(4242);
            float f = 0f, g = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f += 0.08f * (noise - f);
                g += 0.4f * (noise - g);
                float env = Mathf.Clamp01(t * 60f) * Mathf.Clamp01((0.3f - t) * 12f);
                d[i] = SoftClip((f * 2.4f + g * 0.25f * Mathf.PerlinNoise(t * 80f, 0f)) * env);
            }
            return Clip("flame", d);
        }

        static AudioClip MakeTube()
        {
            int n = (int)(Rate * 0.5f);
            var d = new float[n];
            var rnd = new System.Random(5151);
            float f = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f += 0.1f * (noise - f);
                float pop = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 12f) * 1.3f;
                d[i] = SoftClip((pop + f * Mathf.Exp(-t * 20f) * 1.5f + noise * Mathf.Exp(-t * 200f) * 0.4f) * 1.2f);
            }
            return Clip("tube", d);
        }

        static AudioClip MakeRocket()
        {
            int n = (int)(Rate * 1.2f);
            var d = new float[n];
            var rnd = new System.Random(6161);
            float f = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f += 0.2f * (noise - f);
                float launch = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 8f);
                float hiss = f * Mathf.Exp(-t * 2.2f) * 1.6f;
                d[i] = SoftClip((launch + hiss + noise * Mathf.Exp(-t * 90f) * 0.8f) * 1.3f);
            }
            return Clip("rocket", d);
        }
        public static AudioClip Empty => Get("empty", () => MakeClick(0.05f, 2600f, 0.5f));
        /// <summary>던진 돌이 '딱'.</summary>
        /// <summary>폭죽 '타닥'.</summary>
        public static AudioClip Pop => Get("pop", () => MakeClick(0.09f, 1800f, 1f));
        public static AudioClip StoneClack => Get("stoneclack", () => MakeClick(0.06f, 1100f, 0.9f));
        /// <summary>도탄 (돌·쇠에 맞고 튕기는 '피웅').</summary>
        public static AudioClip Ricochet => Get("ricochet", () => MakeTone(0.32f, 4200f, 1500f, 0.16f));
        /// <summary>젖은 땅에 박히는 '퍽'.</summary>
        public static AudioClip MudHit => Get("mudhit", () => MakeClick(0.07f, 380f, 0.5f));
        /// <summary>탄창이 바닥날 때 쏠 때마다 나는 가벼운 '틱' (갈수록 높아짐).</summary>
        public static AudioClip LowAmmoTick => Get("lowammo", () => MakeTone(0.035f, 3200f, 3000f, 0.35f));
        public static AudioClip MagOut => Mech("magout", () => MakeClick(0.09f, 900f, 0.8f));
        public static AudioClip MagIn => Mech("magin", () => MakeClick(0.12f, 1300f, 1f));
        public static AudioClip Rack => Mech("rack", () => MakeRack());
        public static AudioClip Swap => Get("swap", () => MakeClick(0.07f, 700f, 0.6f));
        public static AudioClip HitTick => Get("hit", () => MakeTone(0.045f, 1500f, 1500f, 0.5f));
        public static AudioClip WeakTick => Get("weak", () => MakeTone(0.06f, 2300f, 2600f, 0.6f));
        public static AudioClip KillTick => Get("kill", () => MakeKill());
        public static AudioClip MonsterHurt => Rec("hurt", 6, () => Get("mhurt", () => MakeGrowl(0.25f, 70f, 1f)));
        public static AudioClip MonsterDie => Rec("die", 6, () => Get("mdie", () => MakeGrowl(0.7f, 55f, 0.6f)));
        /// <summary>망자가 처음 플레이어를 알아챘을 때의 비명.</summary>
        public static AudioClip ZombieScream => Rec("scream", 10, () => Get("mdie", () => MakeGrowl(0.7f, 55f, 0.6f)));
        /// <summary>멀리서 들리는 길게 끄는 신음 (분위기).</summary>
        public static AudioClip ZombieMoan => Rec("moan", 9, () => Growl);

        /// <summary>
        /// 실제 녹음된 망자 소리 (Resources/Sounds/Zombie/&lt;이름&gt;_N.ogg, CC0) 중 무작위 하나. 없으면 합성음.
        /// 같은 소리가 연달아 나오지 않게 직전 것은 피한다.
        /// </summary>
        static readonly Dictionary<string, int> lastPick = new Dictionary<string, int>();
        static AudioClip Rec(string name, int max, Func<AudioClip> fallback)
        {
            var arr = LoadRecorded("Zombie", name, max);
            if (arr.Length == 0) return fallback();
            int i = UnityEngine.Random.Range(0, arr.Length);
            if (arr.Length > 1 && lastPick.TryGetValue(name, out var last) && last == i) i = (i + 1) % arr.Length;
            lastPick[name] = i;
            return arr[i];
        }

        /// <summary>망자 발소리 (젖은 흙 녹음 중 무작위) — 3D 재생용 클립.</summary>
        public static AudioClip ZombieStep
        {
            get { var a = LoadRecorded("Steps", "mud", 3); return a.Length > 0 ? a[UnityEngine.Random.Range(0, a.Length)] : null; }
        }

        /// <summary>내 발소리: 바닥에 따라 진흙(mud)·돌길(stone)·나무(wood). Resources/Sounds/Steps (CC0).</summary>
        public static void Footstep(string surface, float volume)
        {
            var arr = LoadRecorded("Steps", surface, 3);
            if (arr.Length == 0 || voices[0] == null) return;
            PlayPitched(arr[UnityEngine.Random.Range(0, arr.Length)], volume, UnityEngine.Random.Range(0.9f, 1.08f));
        }

        /// <summary>위치가 들리는 3D 소리 + 음높이 (몸집이 큰 망자는 낮고 굵게).</summary>
        public static void PlayAt3D(AudioClip clip, Vector3 pos, float volume, float pitch)
        {
            if (clip == null) return;
            var a = Grab(volume);
            if (a == null) return;
            a.transform.position = pos;
            a.clip = clip;
            a.volume = volume;
            a.pitch = pitch;
            a.minDistance = 2.5f;
            a.maxDistance = 40f;
            a.Play();
        }
        public static AudioClip PlayerHurt => Get("phurt", () => MakeThud());
        public static AudioClip UiHover => Get("uihover", () => MakeTone(0.035f, 900f, 1100f, 0.18f));
        public static AudioClip UiClick => Get("uiclick", () => MakeClick(0.06f, 520f, 0.45f));
        /// <summary>출정·웨이브 시작: 낮게 울리는 징 소리.</summary>
        public static AudioClip Gong => Get("gong", () => MakeGong());
        public static AudioClip LevelUp => Get("levelup", () => MakeChime());

        public static AudioClip RainLoop => Get("rain", () => MakeRain());
        /// <summary>살에 박히는 둔탁한 소리 (타격감).</summary>
        public static AudioClip FleshHit => Get("flesh", () => MakeFlesh());

        static AudioClip MakeFlesh()
        {
            int n = (int)(Rate * 0.16f);
            var d = new float[n];
            var rnd = new System.Random(55);
            float f = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f += 0.18f * (noise - f);
                float thump = Mathf.Sin(2f * Mathf.PI * (140f - t * 400f) * t) * Mathf.Exp(-t * 30f);
                d[i] = SoftClip((thump * 1.1f + f * 2.2f * Mathf.Exp(-t * 45f) + noise * Mathf.Exp(-t * 300f) * 0.3f) * 1.3f);
            }
            return Clip("flesh", d);
        }
        public static AudioClip Thunder => Get("thunder", () => MakeThunder());
        public static AudioClip Explosion => Get("explosion", () => MakeBoom());
        /// <summary>칼 휘두르는 바람 소리.</summary>
        public static AudioClip Swing => Get("swing", () => MakeWhoosh(0.28f, 0.5f));
        /// <summary>칼이 살을 베는 소리.</summary>
        public static AudioClip Slash => Get("slash", () => MakeSlash());
        /// <summary>붕대를 감는 부스럭 소리.</summary>
        public static AudioClip Bandage => Get("bandage", () => MakeWhoosh(0.9f, 0.25f, 9f));
        /// <summary>덤불 헤치는 소리.</summary>
        public static AudioClip Rustle => Get("rustle", () => MakeWhoosh(0.5f, 0.3f, 14f));
        public static AudioClip Drink => Get("drink", () => MakeDrink());

        static AudioClip MakeBoom()
        {
            int n = (int)(Rate * 2.2f);
            var d = new float[n];
            var rnd = new System.Random(303);
            float f1 = 0f, f2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f1 += 0.12f * (noise - f1);
                f2 += 0.02f * (noise - f2);
                float s = noise * Mathf.Exp(-t * 60f) * 0.9f
                          + f1 * Mathf.Exp(-t * 5f) * 2.2f
                          + Mathf.Sin(2f * Mathf.PI * 38f * t * (1f - t * 0.3f)) * Mathf.Exp(-t * 3.5f) * 1.2f
                          + f2 * Mathf.Exp(-t * 1.4f) * 7f;
                d[i] = SoftClip(s * 1.6f) * Mathf.Clamp01(t * 1500f);
            }
            return Clip("boom", d);
        }

        /// <summary>불 타닥거리는 소리: 낮은 '쉬익' 위에 잔가지 튀는 딱딱 소리 (0.6초).</summary>
        public static AudioClip Crackle => Get("crackle", () =>
        {
            const float len = 0.6f;
            int n = (int)(Rate * len);
            var d = new float[n];
            var rnd = new System.Random(91);
            float f = 0f;
            for (int i = 0; i < n; i++)
            {
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f += 0.08f * (noise - f);
                d[i] = f * 0.5f;                                           // 낮게 타오르는 소리
                if (rnd.NextDouble() < 0.0016) // 가끔 딱!
                    for (int j = 0; j < 220 && i + j < n; j++) d[i + j] += (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-j / 40f) * 0.7f;
            }
            float env = 0f;
            for (int i = 0; i < n; i++) { env = Mathf.Min(1f, Mathf.Min(i, n - i) / (0.05f * Rate)); d[i] *= env; }
            return Clip("crackle", d);
        });

        /// <summary>멀리서 문 두드리는 소리: 나무문을 쿵·쿵·쿵 (간격이 조금씩 어긋나 사람 손 같다).</summary>
        public static AudioClip Knock => Get("knock", () =>
        {
            const float len = 1.3f;
            int n = (int)(Rate * len);
            var d = new float[n];
            float[] hits = { 0.05f, 0.42f, 0.71f };
            var rnd = new System.Random(5);
            foreach (float h0 in hits)
            {
                int s0 = (int)(h0 * Rate);
                float ph = 0f;
                for (int i = 0; i < (int)(0.18f * Rate) && s0 + i < n; i++)
                {
                    float t = (float)i / Rate;
                    ph += 2f * Mathf.PI * (140f - t * 200f) / Rate;
                    float body = Mathf.Sin(ph) * Mathf.Exp(-t * 30f);
                    float click = (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 160f) * 0.6f;
                    d[s0 + i] += (body * 0.8f + click) * 0.8f;
                }
            }
            return Clip("knock", d);
        });

        /// <summary>민방위 사이렌: 오르내리는 울음 (5초). 확성기 탑.</summary>
        public static AudioClip Siren => Get("siren", () =>
        {
            const float len = 5f;
            int n = (int)(Rate * len);
            var d = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float sweep = 0.5f - 0.5f * Mathf.Cos(t / len * Mathf.PI * 4f); // 두 번 올라갔다 내려옴
                float hz = 380f + 420f * sweep;
                ph += 2f * Mathf.PI * hz / Rate;
                float env = Mathf.Clamp01(t * 3f) * Mathf.Clamp01((len - t) * 2f);
                // 확성기처럼 거친 소리: 사인 + 홀수 배음
                float v = Mathf.Sin(ph) + 0.35f * Mathf.Sin(ph * 3f) + 0.15f * Mathf.Sin(ph * 5f);
                d[i] = v * 0.32f * env;
            }
            return Clip("siren", d);
        });

        /// <summary>까마귀 '까악' 두 번 (거친 톱니파 + 떨림).</summary>
        public static AudioClip Caw => Get("caw", () =>
        {
            const float len = 0.75f;
            int n = (int)(Rate * len);
            var d = new float[n];
            var rnd = new System.Random(31);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float local = t < 0.3f ? t : t - 0.38f;           // 두 번 운다
                if (t >= 0.3f && t < 0.38f) { d[i] = 0f; continue; }
                float env = Mathf.Sin(Mathf.Clamp01(local / 0.3f) * Mathf.PI);
                float hz = 520f + 140f * Mathf.Sin(local * 60f) - local * 300f;
                ph += hz / Rate;
                float saw = (ph % 1f) * 2f - 1f;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                d[i] = (saw * 0.55f + noise * 0.25f) * env * 0.5f;
            }
            return Clip("caw", d);
        });

        /// <summary>북(큰북) 한 번: 낮게 울리며 음이 살짝 떨어지는 '둥' + 가죽 치는 소리.</summary>
        public static AudioClip Drum => Get("drum", () =>
        {
            const float len = 0.9f;
            int n = (int)(Rate * len);
            var d = new float[n];
            var rnd = new System.Random(77);
            float ph = 0f, f = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float hz = 62f + 38f * Mathf.Exp(-t * 18f);       // 처음 높았다가 낮게 가라앉는 울림
                ph += 2f * Mathf.PI * hz / Rate;
                float body = Mathf.Sin(ph) * Mathf.Exp(-t * 4.2f);
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f += 0.25f * (noise - f);
                float skin = f * Mathf.Exp(-t * 40f) * 0.8f;       // 북채가 가죽을 치는 순간
                d[i] = Mathf.Clamp(body * 0.95f + skin, -1f, 1f);
            }
            return Clip("drum", d);
        });

        /// <summary>지쳐서 헐떡이는 숨: 들숨(짧고 높게) + 날숨(길고 낮게) 한 번.</summary>
        public static AudioClip Breath => Get("breath", () =>
        {
            const float len = 0.95f;
            int n = (int)(Rate * len);
            var d = new float[n];
            var rnd = new System.Random(4242);
            float f = 0f, f2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                bool inhale = t < 0.32f;
                float u = inhale ? t / 0.32f : (t - 0.38f) / (len - 0.38f);
                float env = t >= 0.32f && t < 0.38f ? 0f : Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * (inhale ? 0.7f : 1f);
                float cut = inhale ? 0.35f : 0.16f; // 들숨은 쉭(밝게), 날숨은 후(어둡게)
                f += cut * (noise - f);
                f2 += 0.5f * (f - f2);
                d[i] = (f - f2 * 0.6f) * env * 0.9f;
            }
            return Clip("breath", d);
        });

        static AudioClip MakeWhoosh(float len, float amp, float rustle = 0f)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            var rnd = new System.Random(808 + (int)(len * 100));
            float f = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float u = t / len;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float cut = Mathf.Lerp(0.05f, 0.5f, Mathf.Sin(u * Mathf.PI));
                f += cut * (noise - f);
                float env = Mathf.Sin(u * Mathf.PI);
                if (rustle > 0f) env *= 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(t * rustle * Mathf.PI));
                d[i] = f * env * amp * 1.6f;
            }
            return Clip("whoosh", d);
        }

        static AudioClip MakeSlash()
        {
            int n = (int)(Rate * 0.22f);
            var d = new float[n];
            var rnd = new System.Random(919);
            float f = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                f += 0.25f * (noise - f);
                float thump = Mathf.Sin(2f * Mathf.PI * (180f - t * 500f) * t) * Mathf.Exp(-t * 28f);
                float wet = f * Mathf.Exp(-t * 18f) * 1.8f;
                d[i] = SoftClip((thump + wet + noise * Mathf.Exp(-t * 200f) * 0.5f) * 1.3f);
            }
            return Clip("slash", d);
        }

        static AudioClip MakeDrink()
        {
            int n = (int)(Rate * 0.9f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float k = Mathf.Repeat(t, 0.22f);
                float hz = 260f + 180f * (k / 0.22f);
                d[i] = Mathf.Sin(2f * Mathf.PI * hz * k) * Mathf.Exp(-k * 18f) * 0.5f;
            }
            return Clip("drink", d);
        }
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
        static AudioClip MakeShot(WeaponClass c, int variant, int tier)
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
            // 변주: 단계가 높을수록 구경이 커서 저음·여운이 깊다, 이름마다 음색이 조금씩 다르다
            if (variant != 0 || tier > 1)
            {
                float v = (variant % 100) / 100f;
                float big = 1f + (tier - 1) * 0.22f;
                thumpHz *= Mathf.Lerp(0.85f, 1.15f, v) / big;
                thumpDecay /= big;
                decay *= Mathf.Lerp(0.85f, 1.2f, 1f - v) / Mathf.Sqrt(big);
                crack *= Mathf.Lerp(0.85f, 1.15f, v) * big;
                tail *= big;
                lp *= Mathf.Lerp(0.85f, 1.2f, v);
                len *= Mathf.Sqrt(big);
            }
            int n = (int)(Rate * len);
            var d = new float[n];
            var rnd = new System.Random(1000 + (int)c + variant);
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

        /// <summary>폭발 뒤 귀 울림: 높은 '삐—' 소리가 서서히 사라진다.</summary>
        public static AudioClip EarRing => Get("earring", () =>
        {
            const float len = 2.4f;
            int n = (int)(Rate * len);
            var d = new float[n];
            float ph = 0f, ph2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                ph += 2f * Mathf.PI * 3600f / Rate;
                ph2 += 2f * Mathf.PI * 3640f / Rate; // 살짝 맥놀이
                float env = Mathf.Clamp01(t / 0.08f) * Mathf.Pow(1f - t / len, 1.6f);
                d[i] = (Mathf.Sin(ph) * 0.6f + Mathf.Sin(ph2) * 0.4f) * env * 0.22f;
            }
            return Clip("earring", d);
        });

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

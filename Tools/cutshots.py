import sys, os, subprocess, numpy as np, soundfile as sf
LIB = sys.argv[1]; REL = sys.argv[2]; OUT = sys.argv[3]
# 이름: (파일, 자를 길이 초)
SHOTS = {
  "ppq":     ("Walther PPQ/X_39P.wav", 0.75),
  "m1911":   ("1911/A_42P.wav", 0.8),
  "sw642":   ("Smith & Wesson 642/V_27P.wav", 0.9),
  "ar15":    ("AR-15/D_32P.wav", 0.8),
  "ak47":    ("AK-47/C_28P.wav", 0.8),
  "sks":     ("SKS/U_14P.wav", 0.9),
  "m45":     ("Carl Gustav M45/G_31P.wav", 0.55),
  "ppsh":    ("PPSh/P_30P.wav", 0.5),
  "nova":    ("Nova/O_21P.wav", 1.1),
  "model12": ("Model 12/K_22P.wav", 1.0),
  "cd":      ("CD/H_21P.wav", 1.1),
  "tikka":   ("Tikka/W_29P.wav", 1.4),
  "savage":  ("Savage 10 .300 Blackout/T_27P.wav", 1.1),
  "m1917":   ("1917/B_24P.wav", 1.6),
  "mosin":   ("Mosin Nagant/M_21P.wav", 1.3),
}
def onsets(m, sr, peak):
    w = int(sr*0.01); env = np.array([m[i:i+w].max() for i in range(0, len(m)-w, w)])
    res = []; last = -1e9
    for i, e in enumerate(env):
        t = i*0.01
        if e > peak*0.35 and t - last > 2.0:
            # 정확한 시작: 이 창 안에서 처음으로 peak*0.2를 넘는 샘플
            seg = m[i*w:(i+1)*w]
            j = int(np.argmax(seg > peak*0.2))
            res.append((i*w + j)/sr); last = t
    return res
def write(y, sr, path):
    tmp = path + ".tmp.wav"
    sf.write(tmp, y, sr, subtype="FLOAT")
    subprocess.run(["ffmpeg","-v","error","-y","-i",tmp,"-ar","44100","-c:a","libvorbis","-q:a","5",path], check=True)
    os.remove(tmp)
def shape(y, sr, length, fade_frac=0.55):
    n = min(len(y), int(sr*length))
    y = y[:n].copy()
    f0 = int(n*(1-fade_frac)); k = np.linspace(0,1,n-f0)
    y[f0:] *= ((1-k)**2)[:,None]
    y[:int(sr*0.001)] *= np.linspace(0,1,int(sr*0.001))[:,None]
    pk = np.abs(y).max()
    return y/pk*0.85 if pk>0 else y
for name,(f,length) in SHOTS.items():
    x, sr = sf.read(os.path.join(LIB,f), always_2d=True)
    m = np.abs(x).max(axis=1)
    ons = onsets(m, sr, m.max())[:3]
    for k,t in enumerate(ons):
        s = max(0,int((t-0.003)*sr))
        y = shape(x[s:], sr, length)
        write(y, sr, os.path.join(OUT,"Guns",f"{name}_{k+1}.ogg"))
    print(name, len(ons), ["%.2f"%t for t in ons])
MECH = {
  "magout":  ("assaultriflereload1_0.wav", 0.18, 0.46),
  "magin":   ("assaultriflereload1_0.wav", 0.98, 1.18),
  "magin2":  ("clipload1.wav", 0.0, 0.26),
  "bolt":    ("gunreload1.wav", 1.20, 1.56),
  "rack":    ("shotguncock_0.wav", 0.0, 0.47),
  "shellin": ("singlebullet1.wav", 0.0, 0.30),
}
for name,(f,a,b) in MECH.items():
    x, sr = sf.read(os.path.join(REL,f), always_2d=True)
    y = x[int(a*sr):int(b*sr)].copy()
    n=len(y); f0=int(n*0.7); y[f0:] *= np.linspace(1,0,n-f0)[:,None]
    y[:int(sr*0.002)] *= np.linspace(0,1,int(sr*0.002))[:,None]
    pk=np.abs(y).max(); y = y/pk*0.9
    write(y, sr, os.path.join(OUT,"Mech",name+".ogg"))
    print(name, "%.2fs"%(n/sr))

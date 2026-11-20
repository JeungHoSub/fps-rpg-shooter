# Resources/Guns/<id>.txt → 옆모습 아이콘 PNG (흰 바탕색 + 면 방향 음영, 투명 배경)
import sys, os, glob, numpy as np
from PIL import Image, ImageDraw
SRC, OUT = sys.argv[1], sys.argv[2]
W, H, SS = 320, 120, 4
for path in sorted(glob.glob(os.path.join(SRC, '*.txt'))):
    tris = []
    lines = open(path).read().split('\n'); i = 1
    nm = int(lines[0].split()[1]); i += nm
    while i < len(lines):
        l = lines[i].strip()
        if l.startswith('s '):
            cnt = int(l.split()[2]); i += 1
            for k in range(cnt):
                vs = [list(map(float, lines[i + j].split()[:6])) for j in range(3)]; i += 3
                tris.append(vs)
        else: i += 1
    if not tris: continue
    P = np.array([[v[:3] for v in t] for t in tris])
    zs, ys = P[:, :, 2], P[:, :, 1]
    zmin, zmax, ymin, ymax = zs.min(), zs.max(), ys.min(), ys.max()
    sc = min((W - 16) / (zmax - zmin), (H - 12) / (ymax - ymin)) * SS
    ox = (W * SS - (zmax - zmin) * sc) / 2; oy = (H * SS - (ymax - ymin) * sc) / 2
    img = Image.new('RGBA', (W * SS, H * SS), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    order = np.argsort(P[:, :, 0].mean(1))  # 먼 쪽부터
    light = np.array([0.55, 0.75, 0.35]); light /= np.linalg.norm(light)
    for ti in order:
        t = P[ti]
        n = np.cross(t[1] - t[0], t[2] - t[0]); ln = np.linalg.norm(n)
        if ln < 1e-12: continue
        n /= ln
        b = 0.62 + 0.38 * abs(n @ light)
        g = int(255 * b)
        pts = [(ox + (v[2] - zmin) * sc, H * SS - (oy + (v[1] - ymin) * sc)) for v in t]
        d.polygon(pts, fill=(g, g, g, 255))
    img = img.resize((W, H), Image.LANCZOS)
    name = os.path.splitext(os.path.basename(path))[0]
    img.save(os.path.join(OUT, name + '.png'))
    print(name)

# Guns/<id>.txt → GunsBin/<id>.bytes : 같은 점(위치·노멀·UV) 합치기 + 이진 저장
import sys, os, glob, struct
SRC, OUT = sys.argv[1], sys.argv[2]
KIND = {'metal': 0, 'wood': 1, 'tex': 2}
def wstr(f, s):
    b = s.encode('utf-8'); f.write(struct.pack('<i', len(b))); f.write(b)
for path in sorted(glob.glob(os.path.join(SRC, '*.txt'))):
    L = open(path, encoding='utf-8').read().split('\n')
    head = L[0].split(); nm = int(head[1])
    mats = [L[1 + i].split() for i in range(nm)]
    i = 1 + nm; parts = []
    while i < len(L):
        l = L[i].strip()
        if l.startswith('p '):
            name = l[2:]; i += 1; subs = []
            while i < len(L) and L[i].startswith('s '):
                _, mi, cnt = L[i].split(); i += 1
                subs.append((int(mi), [tuple(round(float(x), 4) for x in L[i + k].split()) for k in range(int(cnt) * 3)]))
                i += int(cnt) * 3
            parts.append((name, subs))
        else: i += 1
    before = after = 0
    with open(os.path.join(OUT, os.path.basename(path)[:-4] + '.bytes'), 'wb') as f:
        f.write(b'HGN1'); f.write(struct.pack('<i4f', nm, *[float(x) for x in head[2:6]]))
        for m in mats:
            wstr(f, m[1]); f.write(struct.pack('<3fB2f', float(m[2]), float(m[3]), float(m[4]), KIND.get(m[5], 0), float(m[6]), float(m[7])))
        f.write(struct.pack('<i', len(parts)))
        for name, subs in parts:
            index = {}; verts = []; sidx = []
            for mi, vs in subs:
                idx = []
                for v in vs:
                    j = index.get(v)
                    if j is None: j = len(verts); index[v] = j; verts.append(v)
                    idx.append(j)
                sidx.append((mi, idx)); before += len(vs)
            after += len(verts)
            wstr(f, name); f.write(struct.pack('<i', len(verts)))
            for v in verts: f.write(struct.pack('<8f', *v))
            f.write(struct.pack('<i', len(sidx)))
            for mi, idx in sidx:
                f.write(struct.pack('<ii', mi, len(idx))); f.write(struct.pack(f'<{len(idx)}i', *idx))
    print(os.path.basename(path), before, '->', after, 'verts', os.path.getsize(os.path.join(OUT, os.path.basename(path)[:-4] + '.bytes')) // 1024, 'KB')

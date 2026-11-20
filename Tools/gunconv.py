# OBJ → 게임용 총 메시 텍스트(.txt). +Z 총구, +Y 위, 원점 = 손잡이 위, 길이 맞춤.
import sys, os, numpy as np
def parse_obj(path):
    V=[];VT=[];VN=[]; faces=[]  # (mat, group, [(vi,ti,ni)...])
    mats={}; cur_m='default'; cur_g=''
    base=os.path.dirname(path)
    for line in open(path, encoding='utf-8', errors='ignore'):
        p=line.split()
        if not p: continue
        if p[0]=='v': V.append([float(x) for x in p[1:4]])
        elif p[0]=='vt': VT.append([float(x) for x in p[1:3]])
        elif p[0]=='vn': VN.append([float(x) for x in p[1:4]])
        elif p[0] in ('o','g'): cur_g=' '.join(p[1:])
        elif p[0]=='usemtl': cur_m=p[1] if len(p)>1 else 'default'
        elif p[0]=='mtllib':
            mp=os.path.join(base,' '.join(p[1:]))
            if os.path.exists(mp):
                name=None
                for l in open(mp,encoding='utf-8',errors='ignore'):
                    q=l.split()
                    if not q: continue
                    if q[0]=='newmtl': name=q[1]; mats[name]=[0.6,0.6,0.6]
                    elif q[0]=='Kd' and name: mats[name]=[float(x) for x in q[1:4]]
        elif p[0]=='f':
            idx=[]
            for t in p[1:]:
                a=t.split('/')
                vi=int(a[0]); ti=int(a[1]) if len(a)>1 and a[1] else 0; ni=int(a[2]) if len(a)>2 and a[2] else 0
                idx.append((vi-1 if vi>0 else len(V)+vi, ti-1 if ti>0 else (len(VT)+ti if ti<0 else -1), ni-1 if ni>0 else (len(VN)+ni if ni<0 else -1)))
            for k in range(1,len(idx)-1): faces.append((cur_m,cur_g,[idx[0],idx[k],idx[k+1]]))
    return np.array(V),np.array(VT) if VT else None,np.array(VN) if VN else None,faces,mats

def orient(V, flip_muzzle=False, flip_up=False):
    ext=V.max(0)-V.min(0); L=int(np.argmax(ext))
    rest=[i for i in range(3) if i!=L]; U=rest[0] if ext[rest[0]]>=ext[rest[1]] else rest[1]
    W=[i for i in rest if i!=U][0]
    lo,hi=V[:,L].min(),V[:,L].max(); span=hi-lo
    def slice_h(a,b):
        m=(V[:,L]>=a)&(V[:,L]<=b); s=V[m,U]; return (s.max()-s.min()) if len(s) else 1e9, (s.mean() if len(s) else 0)
    hlo,clo=slice_h(lo,lo+span*0.1); hhi,chi=slice_h(hi-span*0.1,hi)
    msign = 1 if hhi<hlo else -1          # 가는 쪽이 총구
    if flip_muzzle: msign=-msign
    barrelU = chi if msign>0 else clo
    usign = 1 if barrelU > (V[:,U].max()+V[:,U].min())/2 else -1   # 총열은 위쪽
    if flip_up: usign=-usign
    R=np.zeros((3,3)); R[2,L]=msign; R[1,U]=usign
    # x = y × z (오른손) → 왼손 Unity: x = cross(up, fwd) 로 맞춤
    x=np.cross(R[1],R[2]); R[0]=x
    return R, barrelU*usign

def convert(src, out, length, grip_frac, mag_group=None, action_group=None, flip_muzzle=False, flip_up=False, tex=None, metal=0.55, gloss=0.45, preview=None, drop_groups=(), fat=1.0, pca=False):
    V,VT,VN,faces,mats=parse_obj(src)
    if drop_groups: faces=[f for f in faces if not any(d in f[1] for d in drop_groups)]
    used=sorted({i for f in faces for (i,_,_) in f[2]})
    if pca:
        # 비스듬히 놓인 모델: 가장 긴 주축을 가장 가까운 좌표축에 맞춰 돌린다
        C=V[used]-V[used].mean(0); w,E=np.linalg.eigh(C.T@C); e=E[:,-1]
        ax=np.zeros(3); ax[np.argmax(abs(e))]=np.sign(e[np.argmax(abs(e))])
        v=np.cross(e,ax); c=float(e@ax); sv=np.linalg.norm(v)
        if sv>1e-6:
            K=np.array([[0,-v[2],v[1]],[v[2],0,-v[0]],[-v[1],v[0],0]])
            Rp=np.eye(3)+K+K@K*((1-c)/sv**2)
            V=V@Rp.T
            if VN is not None: VN=VN@Rp.T
    R,barrelY=orient(V[used],flip_muzzle,flip_up)
    P=V@R.T
    Pu=P[used]
    zmin,zmax=Pu[:,2].min(),Pu[:,2].max(); s=length/(zmax-zmin)
    oz=zmin+(zmax-zmin)*grip_frac
    # 원점 높이: 원점 근처 단면의 위쪽에서 15% 아래 (기관부)
    near=np.abs(Pu[:,2]-oz)<(zmax-zmin)*0.08
    top=Pu[near,1].max(); bot=Pu[near,1].min()
    oy=top-(top-bot)*0.3
    P=(P-np.array([0,oy,oz]))*s
    N=VN@R.T if VN is not None else None
    if fat!=1.0:
        # 총열(원점 앞쪽)을 굵게: 총구 단면 중심을 기준으로 x·y를 키운다 → 유탄발사기 통
        Pu2=P[used]; zf=Pu2[:,2].max(); L2=zf-Pu2[:,2].min()
        mz=Pu2[Pu2[:,2]>zf-L2*0.05]; cy=(mz[:,1].max()+mz[:,1].min())/2
        k=np.clip((P[:,2]-0.05*length)/(0.1*length),0,1)
        sc=1+(fat-1)*k
        P[:,0]*=sc; P[:,1]=cy+(P[:,1]-cy)*sc
    # 조준선: 원점 앞뒤 20% 구간 위쪽 최고점
    Pu=P[used]; near=np.abs(Pu[:,2])<length*0.2
    sight=float(Pu[near,1].max())
    zfront=float(Pu[:,2].max())
    mz=Pu[Pu[:,2]>zfront-length*0.05]; muzzleY=float((mz[:,1].max()+mz[:,1].min())/2)
    # 출력: 재질별 묶음, 정점은 면마다 펼침 (평면 음영 유지)
    mnames=sorted({f[0] for f in faces})
    lines=[f"gun {len(mnames)} {sight:.4f} {zfront:.4f} {muzzleY:.4f} {float(Pu[:,1].min()):.4f}"]
    for mn in mnames:
        kd=mats.get(mn,[0.6,0.6,0.6]); g=[min(1,max(0,c))**(1/2.2) for c in kd]
        low=mn.lower(); kind='wood' if ('wood' in low or 'brown' in low) else 'metal'
        if tex: kind='tex'
        lines.append(f"m {mn.replace(' ','_')} {g[0]:.3f} {g[1]:.3f} {g[2]:.3f} {kind} {metal if kind!='wood' else 0:.2f} {gloss if kind!='wood' else 0.2:.2f}")
    parts={'body':[], 'mag':[], 'act':[]}
    for f in faces:
        part='mag' if mag_group and mag_group in f[1] else 'act' if action_group and action_group in f[1] else 'body'
        parts[part].append(f)
    for pname,fl in parts.items():
        if not fl: continue
        lines.append(f"p {pname}")
        for mi,mn in enumerate(mnames):
            sub=[f for f in fl if f[0]==mn]
            if not sub: continue
            lines.append(f"s {mi} {len(sub)}")
            for f in sub:
                a,b,c=[P[i] for (i,_,_) in f[2]]
                fn=np.cross(b-a,c-a); fn=fn/ (np.linalg.norm(fn)+1e-9)
                # 오른손→왼손 변환 후 앞면이 바깥을 보게 감는 순서 뒤집음
                for (vi,ti,ni) in (f[2][0],f[2][2],f[2][1]):
                    p=P[vi]; n=(N[ni] if (N is not None and ni>=0) else fn)
                    uv=VT[ti] if (VT is not None and ti>=0) else (0,0)
                    # Unity는 x를 뒤집어 왼손 좌표 → x 부호 반전
                    lines.append(f"{-p[0]:.4f} {p[1]:.4f} {p[2]:.4f} {-n[0]:.3f} {n[1]:.3f} {n[2]:.3f} {uv[0]:.4f} {uv[1]:.4f}")
    open(out,'w').write('\n'.join(lines)+'\n')
    if preview:
        import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
        fig,ax=plt.subplots(figsize=(6,3)); Q=P[used]
        ax.scatter(Q[:,2],Q[:,1],s=0.3,c='k'); ax.plot([0],[0],'r+',ms=12); ax.axhline(sight,color='g',lw=0.5)
        ax.plot([zfront],[muzzleY],'bo'); ax.set_aspect('equal'); ax.set_title(os.path.basename(out)); fig.savefig(preview,dpi=70); plt.close(fig)
    print(os.path.basename(out), f"tris={len(faces)} sight={sight:.3f} front={zfront:.3f}", os.path.getsize(out)//1024,"KB")

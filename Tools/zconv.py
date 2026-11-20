import sys, os, subprocess, numpy as np, soundfile as sf
OUT=sys.argv[1]; base=os.getcwd()
V='x_Voice_Effects_Zombie-Skeleton-Monster_Human_Male/Voice Effects Zombie-Skeleton-Monster Human Male/'
groups={
 'growl':[f'x_zombies/zombies/zombie-{i}.wav' for i in (1,2,4,8,9,10,12,15,19,23)],
 'hurt':['x_zombies/zombies/zombie-24.wav','x_zombies/zombies/zombie-11.wav','x_zombies/zombies/zombie-5.wav','x_zombies/zombies/zombie-6.wav','x_zombies/zombies/zombie-13.wav','x_zombienoises/fastzombie1.ogg'],
 'scream':[V+f'zombieYell{i}.wav' for i in range(1,11)],
 'die':[V+f'zombieDeath{i}.wav' for i in range(1,5)]+['x_zombies/zombies/zombie-17.wav','x_zombies/zombies/zombie-16.wav'],
 'moan':[f'x_undead-moans/undead-{i}.ogg' for i in range(1,5)]+[f'x_zombiemoans_-_wav/ZombieMoans - Track {t} - ZombieMoan{i}.wav' for t,i in ((5,1),(6,2),(7,3))]+['x_zombienoises/zombienoise1.ogg','x_zombienoises/zombienoise2.ogg'],
}
for g,files in groups.items():
    for k,f in enumerate(files):
        x,sr=sf.read(f,always_2d=True); m=x.mean(1)
        a=np.abs(m); th=a.max()*0.04
        idx=np.where(a>th)[0]; s,e=max(0,idx[0]-int(sr*0.01)),min(len(m),idx[-1]+int(sr*0.05))
        y=m[s:e]; n=len(y); fo=min(n,int(sr*0.08)); y[-fo:]*=np.linspace(1,0,fo)
        y=y/np.abs(y).max()*0.85
        tmp=os.path.join(OUT,'_t.wav'); sf.write(tmp,y,sr,subtype='FLOAT')
        subprocess.run(['ffmpeg','-v','error','-y','-i',tmp,'-ac','1','-ar','44100','-c:a','libvorbis','-q:a','4',os.path.join(OUT,f'{g}_{k+1}.ogg')],check=True); os.remove(tmp)
    print(g,len(files))

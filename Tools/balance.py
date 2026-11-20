import math
W={ # id: dmg, pellets, rpm, pen, price
'pistol':(28,1,400,0,0),'pistol2':(36,1,380,.2,300),'revolver':(95,1,140,.55,700),
'ar':(34,1,650,.25,0),'k2c':(40,1,700,.3,450),'br762':(64,1,520,.45,900),
'smg':(20,1,900,0,0),'ump':(27,1,720,.15,400),'vector':(23,1,1150,0,850),
'shotgun':(15,9,75,0,0),'autoshotgun':(12,9,230,0,500),'doublebarrel':(26,10,140,.3,950),
'sniper':(160,1,45,.8,0),'dmr':(85,1,180,.6,550),'antimat':(330,1,35,.95,1200),
'lmg':(27,1,750,.3,0),'k3':(30,1,800,.3,600),'hmg':(50,1,550,.5,1300)}
K={'basic':(1,1,1,1),'tank':(2.4,1.8,.62,1.6),'fast':(.5,.6,1.5,.6),'frenzy':(.75,1.6,1.3,.8),'gnaw':(.8,.55,1.1,.35),'wall':(3.6,.8,.5,1.3),'butcher':(1.8,2.6,.85,1.9),'stalker':(1,.9,1.7,1.1)}
def ttk(w,kind,wave,pellets_hit=0.7):
    d,p,rpm,pen,_=W[w]; hp=90*K[kind][0]*(1+0.12*(wave-1)); df=max(0,wave-1)*1.4
    per=max(d-df*(1-pen),d*0.1)*(p*pellets_hit if p>1 else 1)
    shots=math.ceil(hp/per); return shots, (shots-1)*60/rpm
print("== 기본형 망자 몸통 사격: 발수 / 처치시간(초)")
print("무기".ljust(13)+"".join(f"밤{n}".rjust(12) for n in (1,3,5,8,12)))
for w in W:
    print(w.ljust(13)+"".join(("%d/%.2f"%ttk(w,'basic',n)).rjust(12) for n in (1,3,5,8,12)))
print("\n== 밤5, 무기별 유형 처치 시간(초)")
print("무기".ljust(13)+"".join(k.rjust(9) for k in K))
for w in ('pistol','ar','smg','shotgun','sniper','lmg','br762','vector','hmg'):
    print(w.ljust(13)+"".join(("%.2f"%ttk(w,k,5)[1]).rjust(9) for k in K))
print("\n== 망자가 플레이어(생명 180)를 죽이는 시간(초, 혼자 붙었을 때)")
for k,(h,dm,sp,ai) in K.items():
    dps=8*dm/(0.9*ai); print(k.ljust(9),"초당 %.1f  → %.1f초, 이동 %.1fm/s"%(dps,180/dps,5.2*sp))

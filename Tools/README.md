# 도구 (게임 빌드에는 포함되지 않음)

- `gunconv.py` — OBJ 총 모델 → 총 공간 텍스트 (`GunSource/<id>.txt`). 옵션: `fat`(총열 굵게), `pca`(비스듬한 모델 바로 세우기), `flip_up`.
- `gunbin.py GunSource Assets/Resources/GunsBin` — 텍스트 → 이진 `.bytes` (같은 점 합치기). 게임은 `GunsBin`을 읽는다 (`Weapons/PackGunModel.cs`).
- `iconrender.py GunSource Assets/Resources/WeaponIcons` — 옆모습 무기 아이콘 PNG.
- `cutshots.py` — Free Firearm Sound Library에서 한 발씩 잘라 `Sounds/Guns`, 장전음 `Sounds/Mech`.
- `zconv.py` — 망자 소리 묶음 → `Sounds/Zombie` (growl·hurt·scream·die·moan).
- `balance.py` — 무기·망자 처치 시간 표 (`Docs/BALANCE.md`).
필요: python3, numpy, soundfile, pillow, matplotlib, ffmpeg, assimp (FBX→OBJ).

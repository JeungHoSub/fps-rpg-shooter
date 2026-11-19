# 에셋 출처

## 텍스처 — Poly Haven (CC0, 출처 표기 의무 없음 · 상업 이용 가능)
https://polyhaven.com

`Assets/Resources/Env/` (1K, 색 + 노멀맵)

| 용도 | 에셋 |
|---|---|
| 진흙땅 | brown_mud_02 |
| 마을길 | road_damaged |
| 논흙/흙길 | farm_soil |
| 마른 풀 | withered_grass |
| 기와 | grey_roof_tiles |
| 초가지붕 | reed_roof_03 |
| 함석 | corrugated_iron_02 |
| 회벽 | grey_plaster |
| 흙벽 | clay_plaster |
| 나무판 | dark_wooden_planks |
| 돌담 | stacked_stone_wall |
| 콘크리트 | dirty_concrete |
| 나무껍질 | bark_brown_02 |
| 이끼 바위 | mossy_rock |

## 사운드
모든 효과음(총성, 빗소리, 천둥, 징, UI 등)은 코드로 합성 (`Assets/Scripts/Core/SFX.cs`).

## 좀비 모델 — Quaternius "Animated Zombie" (CC0)
https://quaternius.com/packs/animatedzombie.html

`Assets/Resources/Zombies/ZombieSmooth.fbx`, `ZombieTexture.png`
애니메이션: Idle, Walk, Run, Crawl, Bite

## 실사 망자 — Mixamo (Adobe, 무료 · 게임 내 사용 가능)
https://www.mixamo.com — 사용자 계정으로 내려받음

`Assets/Resources/Mixamo/Romero.dae` (텍스처 4K → 1K JPG로 축소)
동작 (`Mixamo/Anims/`): Zombie Idle(Twitching) · Walking · Run · Attack(Swipe) · Death · Reaction Hit(Stumble Back) · Scream · Crawl

## 소품 모델 — Poly Haven (CC0)
https://polyhaven.com/models — FBX 1K + diffuse/normal 1K (잎사귀는 알파를 합쳐 PNG)

`Assets/Resources/Props/`: ceramic_pot(장독대 옹기) · small_lpg_tank · barrel_03 · plastic_crate_02 · old_military_crate ·
rock_09 · moon_rock_02 · shrub_03 · shrub_sorrel_01 · street_lamp_02 · painted_wooden_bench · stone_fire_pit ·
metal_trash_can · rusted_spade_01 · machete(근접 무기) · stick_grenade(수류탄)

## 1인칭 총 — Poly Haven (CC0)
`Assets/Resources/Props/` service_pistol (권총), bolt_action_rifle_7_62 (저격총, 조준경·감개는 _acc 텍스처)

## 글꼴 — 나눔고딕·나눔명조 (SIL Open Font License, Google Fonts)
`Assets/Resources/Fonts/` — 휴대폰에서 한글이 깨지지 않도록 게임에 포함 (라이선스 전문: OFL-Nanum.txt)

## 총소리 (실제 녹음, CC0)
- **The Free Firearm Sound Library** — Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney (Still North Media). CC0.
  출처: https://opengameart.org/content/the-free-firearm-sound-library
  → `Assets/Resources/Sounds/Guns/` (한 발씩 잘라 44.1kHz OGG로 변환, 무기마다 음높이 조정은 `Core/SFX.cs` `GunRec`)
- 장전 소리 (CC0, OpenGameArt): gun-reload-sounds, gun-reload-sound-effects (singlebullet, clipload)
  → `Assets/Resources/Sounds/Mech/` (magout·magin·bolt·rack·shellin)

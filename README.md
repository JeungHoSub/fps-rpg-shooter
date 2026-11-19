# 역병의 마을 (가제) — PROJECT: HUNT

비가 그치지 않는 한국 시골 마을. 조선 때 봉인된 역병이 깨어나 마을 사람들과 무덤 속 옛 망자가 함께 걸어 나온다.
1인칭 **싱글플레이** PvE 슈터 + RPG 성장. 직업 없이 주인공 한 명이 스탯 각인과 특성 계약으로 빌드를 만든다.

- 세계관/분위기: [Docs/CONCEPT_KR_HORROR.md](Docs/CONCEPT_KR_HORROR.md)
- 시스템 기획 초안: [Docs/GAME_DESIGN_DRAFT.md](Docs/GAME_DESIGN_DRAFT.md)

## 실행 방법

1. Unity Hub → **Add project from disk** → 이 폴더 선택 (Unity 6 기준, 2021.3.18 이상이면 동작)
2. 프로젝트가 열리면 아무 씬(빈 씬도 가능)에서 **Play**
3. 시작 화면에서 **壹 능력치 각인 → 貳 특성 계약 → 參 무장 선택 → 肆 출정** 순서로 진행 (Enter로 다음 단계)

씬과 프리팹 없이 코드만으로 아레나, 플레이어, 괴물, UI를 모두 만든다 (`GameBootstrap`).
직접 만든 씬에서 자동 구성을 끄려면 씬에 `NoHuntBootstrap` 이라는 이름의 빈 오브젝트를 두면 된다.

> 처음 열면 Unity가 `.meta` 파일과 `ProjectSettings/*.asset`을 생성한다. 이 파일들도 커밋할 것.
> 입력은 구 Input Manager를 쓴다. 신규 Input System 패키지를 설치하면 Player Settings → Active Input Handling을 **Both**로 바꿔야 한다.

## 조작

| 키 | 동작 |
|---|---|
| WASD / Shift / Space | 이동 / 달리기 / 점프 |
| F | 손전등 켜기/끄기 |
| 좌클릭 / 우클릭 | 사격 / 조준(ADS) |
| R | 재장전 |
| 1·2 / 3 / 4 / 5, 휠 | 주무기 / 권총 / 마체테 / 특수 기기(5를 다시 누르면 다음 기기) |
| V / G | 빠른 칼질 / 빠른 수류탄 |
| X / C | 치료 키트 / 스태미너 회복제 |
| Q | 변이: 광폭화 (감염 게이지 소모) |
| E | 변이: 재생 (감염 게이지 소모) |
| H | 청심환 (폭주 해제) |
| Tab | 야영지 열기/닫기 (밤과 밤 사이) |
| Enter | 다음 밤 바로 맞이하기 |
| Esc | 일시정지 (마우스 감도·소리 크기 설정) |

## 구현된 차별화 요소 (A–E)

| | 시스템 | 어디에 있나 | 플레이에서 보이는 것 |
|---|---|---|---|
| **A** | 적응형 진화 | `Evolution/EvolutionDirector.cs` | 웨이브마다 플레이어가 준 피해를 속성·부위·무기 종류별로 집계함. 많이 당한 방식에 내성, 투구, 약점 이동, 넉백 저항, 속도, 방어력으로 적응. 진화 세대가 오를수록 보상과 파츠 드랍 확률 증가. 정비 메뉴의 "진화 정보" 탭에서 확인 |
| **B** | 감염 게이지 | `Player/InfectionSystem.cs` | 괴물에게 맞으면 게이지가 쌓이고 데미지가 최대 +20%까지 오름. Q·E로 게이지를 소모해 변이 능력을 씀. 100%가 되면 7초간 **폭주**(조준 흔들림·탄퍼짐 증가, 받는 피해 감소). H 해독제로 해제 |
| **C** | 외울 수 있는 반동 | `Weapons/WeaponInstance.cs` (`RecoilPattern`), `Stats/CharacterStats.cs` | 무기마다 고정 반동 패턴이 있음. 제어(CTL) 스탯은 패턴을 최대 20%까지만 줄이고, 주로 **무작위 노이즈**를 줄여 반동을 예측 가능하게 만듦. 실력과 스탯이 서로를 대체하지 않음 |
| **D** | 파츠 + 랜덤 옵션 | `Weapons/WeaponPart.cs` | 6개 슬롯(총열/탄창/개머리판/조준경/총구/모듈), 4개 등급, 수치형·속성변환·발동형 옵션(재장전 후 버스트, 약점 명중 시 탄 회복, 처치 시 폭발). 정비 메뉴에서 장착·분해·강화 |
| **E** | 부위 파괴 & 동적 약점 | `Monsters/MonsterPart.cs`, `MonsterFactory.cs`, `Monster.cs` | 브루트·보스의 전면 장갑판이 가슴 약점을 가림. 장갑을 부수면 약점이 드러남. 다리를 파괴하면 이동속도 감소. 보스는 페이즈마다 약점 위치가 바뀌고 부하를 소환함 |

## RPG 요소

- **직업 없음 — 주인공 1명(헌터)**: 모든 스탯 8로 시작. 시작 시 포인트 6개를 직접 분배하고, 레벨업마다 +3 포인트
- **특성 8종** (`Stats/Traits.cs`): 시작 시 1개, 이후 3레벨마다 무작위 3개 중 1개 선택 → 매 판 다른 빌드
  - 피의 갈증(처치 회복·저지력) · 런앤건(이동 사격) · 정조준(정지 시 약점 +50%) · 두꺼운 피부(HP·감염 저항)
  - 변이 친화(변이 능력 강화) · 수집가(골드·파츠) · 빠른 손(재장전·교체) · 장사(무게 한도 +40%)
- **스탯 6종**: 힘(무게·저지력) · 민첩(이동·재장전) · 제어(반동·탄퍼짐·조준) · 체력(HP·감염 저항) · 집중(치명타·약점) · 기술(변이 능력)
- **무게 시스템**: 장비 무게가 힘 기반 허용치를 넘으면 이동속도와 조준 속도가 최대 50%까지 감소
- **무기 계열 8종 × 1~3단계**: 소총 · 기관단총 · 산탄총 · 저격총 · 경기관총 · 화염방사기 · 유탄/로켓 · 권총 (+ 근접 마체테, 특수 기기 3종)
  - 등급(일반→고급→희귀→영웅→전설)과 강화(+10), 서낭당 '무기고'에서 구매·장착·강화
  - 연사가 빠르고 탄창이 클수록 한 발 피해가 낮고, **망자 방어도가 밤마다 올라** 한 발이 센 총·관통·폭발·화염이 필요해짐
- **방어구 5부위**: 모자(감염 저항) · 상의(생명) · 하의(스태미너) · 장갑(장전) · 신발(이동), 등급·강화
- **라운드 룰**: 사냥(기본→엘리트→보스 처치) · 지킴(120초 지키기) · 소탕(제한 시간 전멸), 셋째 밤부터 변고(탄약·피해·체력·시간·이동·손전등 제한)
- **능력치 수확 체감**: 16·28을 넘으면 효율이 줄고, 가장 낮은 능력치 기준 균형 보너스
- **속성 상태이상**: 화염(화상) · 냉기(둔화) · 전기(연쇄) · 독(중독)
- **성장**: 처치 경험치 → 레벨업 시 스탯 포인트 +3, 골드로 무기 강화(+10까지)

### 데미지 공식

```
최종 = 무기 데미지(강화·파츠 반영) × 감염/광폭화 배율
     × 부위 배율 (몸 1.0 / 다리 0.75 / 머리 2.0 / 약점 3.0×(1+집중·파츠·패시브))
     × (1 − 속성 내성) × 거리 감쇠 × 치명타
     − 방어력 × (1 − 관통력)          (최소 10%는 들어감)
```

## 코드 구조

```
Assets/Scripts/
  Core/       GameBootstrap, GameManager(게임 흐름), ArenaBuilder, Navigation(런타임 NavMesh), Effects
  Combat/     DamageType·HitZone·DamageInfo, DamageCalculator
  Stats/      CharacterStats (모든 파생 수치 계산을 한 곳에서), Traits (특성)
  Data/       CharacterData·WeaponData·MonsterData (ScriptableObject), DefaultContent(기본값)
  Weapons/    WeaponInstance, RecoilPattern, WeaponPart·PartGenerator
  Player/     PlayerCharacter(RPG 상태), PlayerController(이동·시점), PlayerWeapons(사격), InfectionSystem
  Monsters/   Monster(AI·피격), MonsterPart(부위), MonsterFactory(외형), MonsterProjectile
  Evolution/  EvolutionDirector, EvolutionProfile
  Waves/      WaveManager
  UI/         HUD(진입점), StartScreen, GameHud, CampMenu(야영지), EndScreens,
              UITheme(먹·한지·진홍·금 테마, 코드 생성 텍스처), UIKit(위젯), RadarChart, GameText(문구)
```

- **컨셉 교체 대비**: 괴물 외형은 `MonsterFactory` 한 곳에서만 만든다. 컨셉이 정해지면 여기만 모델/프리팹으로 바꾸면 되고, `MonsterData`와 `Monster` 로직은 그대로 쓴다.
- **길찾기**: 아레나를 만든 직후 NavMesh를 런타임에 굽는다(추가 패키지 없음). 괴물은 NavMeshAgent를 쓰지 않고 경로의 코너만 받아 자체 이동 로직(넉백·분리·경직)으로 움직인다. 넉백으로 밀려도 장애물을 뚫지 않고 벽면을 따라 미끄러진다.
- **밸런스 데이터**: 지금은 `DefaultContent.cs`에 코드로 들어 있다. `Create > Hunt > Character/Weapon/Monster`로 ScriptableObject 에셋을 만들어 에디터에서 조정할 수 있다.

## 다음 단계 후보

- [ ] 실제 모델·애니메이션·사운드·파티클
- [x] 테마 UI 전면 개편 (IMGUI 기반, 코드 생성 텍스처)
- [x] 비 오는 밤 분위기 (빗줄기·물튀김·빗소리·천둥번개·안개·손전등·가로등)
- [x] 시골 마을 맵 (Poly Haven CC0 텍스처, 출처: Docs/CREDITS.md)
- [x] 애니메이션 좀비 (Quaternius CC0) + 피격 반응·넉백·히트스톱
- [x] 한국풍 UI (먹·한지·낙관, 궁서체)
- [x] 그래픽 품질 하/중/상 (사양 자동 감지, 60프레임 제한)
- [x] 소리 시스템 (총성·발소리에 망자가 모이고, 빗소리가 소리를 가림)
- [ ] 오행, 밤낮 순환 (컨셉 문서 7장)
- [x] NavMesh 기반 괴물 길찾기 (런타임 굽기, `Core/Navigation.cs`)
- [ ] 저장/불러오기 (레벨, 파츠 인벤토리)
- [ ] 특성 추가 / 특성 간 시너지
- [ ] 솔로용 동료 대체 요소 (예: 설치형 포탑·드론을 장비로)
- [ ] 차별화 F·G (거점 운영, 매치 내 퍽) / 속성 콤보

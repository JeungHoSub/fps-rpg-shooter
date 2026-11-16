# PROJECT: HUNT (가제)

1인칭 협동 PvE 슈터 + RPG 성장. 카스온라인 좀비 모드처럼 웨이브로 몰려오는 괴물을 사냥한다.
기획 초안: [Docs/GAME_DESIGN_DRAFT.md](Docs/GAME_DESIGN_DRAFT.md)

## 실행 방법

1. Unity Hub → **Add project from disk** → 이 폴더 선택 (Unity 6 기준, 2021.3.18 이상이면 동작)
2. 프로젝트가 열리면 아무 씬(빈 씬도 가능)에서 **Play**
3. 캐릭터와 주무기 2개를 고르고 **출격**

씬과 프리팹 없이 코드만으로 아레나, 플레이어, 괴물, UI를 모두 만든다 (`GameBootstrap`).
직접 만든 씬에서 자동 구성을 끄려면 씬에 `NoHuntBootstrap` 이라는 이름의 빈 오브젝트를 두면 된다.

> 처음 열면 Unity가 `.meta` 파일과 `ProjectSettings/*.asset`을 생성한다. 이 파일들도 커밋할 것.
> 입력은 구 Input Manager를 쓴다. 신규 Input System 패키지를 설치하면 Player Settings → Active Input Handling을 **Both**로 바꿔야 한다.

## 조작

| 키 | 동작 |
|---|---|
| WASD / Shift / Space | 이동 / 달리기 / 점프 |
| 좌클릭 / 우클릭 | 사격 / 조준(ADS) |
| R | 재장전 |
| 1·2·3, 휠 | 무기 교체 |
| Q | 변이: 광폭화 (감염 게이지 소모) |
| E | 변이: 재생 (감염 게이지 소모) |
| H | 해독제 (폭주 해제) |
| Tab | 정비 메뉴 열기/닫기 (웨이브 사이) |
| Enter | 다음 웨이브 바로 시작 |
| Esc | 일시정지 |

## 구현된 차별화 요소 (A–E)

| | 시스템 | 어디에 있나 | 플레이에서 보이는 것 |
|---|---|---|---|
| **A** | 적응형 진화 | `Evolution/EvolutionDirector.cs` | 웨이브마다 팀이 준 피해를 속성·부위·무기 종류별로 집계함. 많이 당한 방식에 내성, 투구, 약점 이동, 넉백 저항, 속도, 방어력으로 적응. 진화 세대가 오를수록 보상과 파츠 드랍 확률 증가. 정비 메뉴의 "진화 정보" 탭에서 확인 |
| **B** | 감염 게이지 | `Player/InfectionSystem.cs` | 괴물에게 맞으면 게이지가 쌓이고 데미지가 최대 +20%까지 오름. Q·E로 게이지를 소모해 변이 능력을 씀. 100%가 되면 7초간 **폭주**(조준 흔들림·탄퍼짐 증가, 받는 피해 감소). H 해독제로 해제 |
| **C** | 외울 수 있는 반동 | `Weapons/WeaponInstance.cs` (`RecoilPattern`), `Stats/CharacterStats.cs` | 무기마다 고정 반동 패턴이 있음. 제어(CTL) 스탯은 패턴을 최대 20%까지만 줄이고, 주로 **무작위 노이즈**를 줄여 반동을 예측 가능하게 만듦. 실력과 스탯이 서로를 대체하지 않음 |
| **D** | 파츠 + 랜덤 옵션 | `Weapons/WeaponPart.cs` | 6개 슬롯(총열/탄창/개머리판/조준경/총구/모듈), 4개 등급, 수치형·속성변환·발동형 옵션(재장전 후 버스트, 약점 명중 시 탄 회복, 처치 시 폭발). 정비 메뉴에서 장착·분해·강화 |
| **E** | 부위 파괴 & 동적 약점 | `Monsters/MonsterPart.cs`, `MonsterFactory.cs`, `Monster.cs` | 브루트·보스의 전면 장갑판이 가슴 약점을 가림. 장갑을 부수면 약점이 드러남. 다리를 파괴하면 이동속도 감소. 보스는 페이즈마다 약점 위치가 바뀌고 부하를 소환함 |

## RPG 요소

- **캐릭터 3종**: 브루저(탱커) · 스카우트(기동 딜러) · 마크스맨(저격 딜러). 기본 스탯, 레벨당 성장치, 고유 패시브가 다름
- **스탯 6종**: 힘(무게·저지력) · 민첩(이동·재장전) · 제어(반동·탄퍼짐·조준) · 체력(HP·감염 저항) · 집중(치명타·약점) · 기술(변이 능력)
- **무게 시스템**: 장비 무게가 힘 기반 허용치를 넘으면 이동속도와 조준 속도가 최대 50%까지 감소
- **무기 6종**: 권총 / 돌격소총 / 볼트 SMG(전기) / 샷건 / 저격총 / 인페르노 경기관총(화염). 데미지·연사·탄창·반동·탄퍼짐·사거리 감쇠·관통·저지력·무게가 각각 다름
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
  Stats/      CharacterStats (모든 파생 수치 계산을 한 곳에서)
  Data/       CharacterData·WeaponData·MonsterData (ScriptableObject), DefaultContent(기본값)
  Weapons/    WeaponInstance, RecoilPattern, WeaponPart·PartGenerator
  Player/     PlayerCharacter(RPG 상태), PlayerController(이동·시점), PlayerWeapons(사격), InfectionSystem
  Monsters/   Monster(AI·피격), MonsterPart(부위), MonsterFactory(외형), MonsterProjectile
  Evolution/  EvolutionDirector, EvolutionProfile
  Waves/      WaveManager
  UI/         HUD(IMGUI), DamageNumbers
```

- **컨셉 교체 대비**: 괴물 외형은 `MonsterFactory` 한 곳에서만 만든다. 컨셉이 정해지면 여기만 모델/프리팹으로 바꾸면 되고, `MonsterData`와 `Monster` 로직은 그대로 쓴다.
- **길찾기**: 아레나를 만든 직후 NavMesh를 런타임에 굽는다(추가 패키지 없음). 괴물은 NavMeshAgent를 쓰지 않고 경로의 코너만 받아 자체 이동 로직(넉백·분리·경직)으로 움직인다. 넉백으로 밀려도 장애물을 뚫지 않고 벽면을 따라 미끄러진다.
- **밸런스 데이터**: 지금은 `DefaultContent.cs`에 코드로 들어 있다. `Create > Hunt > Character/Weapon/Monster`로 ScriptableObject 에셋을 만들어 에디터에서 조정할 수 있다.

## 다음 단계 후보

- [ ] 실제 모델·애니메이션·사운드·파티클
- [ ] UGUI/UI Toolkit으로 정식 UI
- [x] NavMesh 기반 괴물 길찾기 (런타임 굽기, `Core/Navigation.cs`)
- [ ] 저장/불러오기 (레벨, 파츠 인벤토리)
- [ ] 멀티플레이 협동 (Netcode for GameObjects 등)
- [ ] 엔지니어·메딕 캐릭터, 액티브 스킬
- [ ] 차별화 F·G·H (거점 운영, 매치 내 퍽, 속성 콤보)

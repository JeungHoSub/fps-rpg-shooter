# 역병의 마을 (PROJECT: HUNT) — 작업 안내

Unity 1인칭 싱글플레이 좀비 슈터 + RPG. **비 오는 밤의 한국 시골, 현대 시골 + 조선 역병.**
상세 컨셉: `Docs/CONCEPT_KR_HORROR.md` · 시스템 기획: `Docs/GAME_DESIGN_DRAFT.md` · 에셋 출처: `Docs/CREDITS.md`

## 핵심 사실
- 씬·프리팹 없음. `Core/GameBootstrap`이 Play 시 전부 코드로 생성 (마을 `VillageBuilder`, 날씨 `Atmosphere`).
- 렌더 파이프라인: Built-in (Standard 셰이더). 입력: 구 Input Manager. UI: IMGUI (`UI/HUD.cs` 진입점).
- 에셋은 `Assets/Resources/` (Env = Poly Haven 텍스처, Zombies = Quaternius 좀비 FBX). 좀비 모델 없으면 도형 괴물로 대체.
- 좀비 애니메이션은 Animator Controller 없이 Playables (`Monsters/ZombieRig.cs`). 피격 판정체는 뼈에 부착 (`MonsterFactory.CreateRigged`).
- **MonoBehaviour 필드 초기화에서 Texture2D 등 유니티 오브젝트 생성 금지** (과거 시작 화면 먹통 원인). Awake/지연 생성.
- 그래픽 품질 하/중/상: `Core/QualityManager.cs` (사양 자동 감지, 60fps 제한, 저프레임 자동 하향).

## 사용자 환경
- Windows, Unity 6.6, **Intel HD Graphics 630 (내장 그래픽)**, i5-7500, RAM 16GB → '하' 품질 기준으로 성능 판단.
- 사용자는 비개발자. 업데이트는 GitHub ZIP + PowerShell robocopy로 바탕화면 `fps-rpg-shooter`에 덮어씀 (삭제된 파일은 남음).
- 여기(클라우드)에서는 유니티 실행 불가 → 변경 후 컴파일만 확인 가능:
  dotnet SDK + NuGet `UnityEngine.Modules 2021.3.33` 참조로 `Assets/Scripts/**/*.cs` 빌드 (2022+ API는 `#if UNITY_2022_2_OR_NEWER`).

## UI 톤 (한국풍)
먹 바탕 + 한지색 글씨 + 붉은색은 낙관(도장)에만. 제목 궁서체. 한자·금색·마름모 쓰지 않음 (중국풍 피드백 있었음).

## 남은 로드맵 → `Docs/SPEC_V04.md` (사용자 요구 12항목, Phase 1~7)
- 한 세션에 Phase 하나씩. 끝나면 사용자 확인 후 다음 Phase.
- 완료: **소리 시스템** — `Core/Noise.cs` (총성 28~50m·발소리 4/10m, 폭우 시 최대 45% 감소), `Monster` 감지 상태(9m 시야·피격·소리 → 추적, 아니면 어슬렁, 45초 뒤 냄새 추적), HUD '소 리' 막대
- 완료: 마지막 남은 망자가 안 보이던 문제 → 남은 3마리 이하 12초 후 위치 표시 + 근처로 끌어옴
- 완료 (2026-11-19): **Phase 1** — 빛 감지(`Monster.Sense`: 어둠 7m/손전등 12m, 빛줄기 32m·30°, 총구 화염 26m, 시야선 검사), 손전등 F(켜고 끌 때 HUD 표시, `Flashlight.Disabled`=시야 감소 변형용),
  라운드 기본 룰(`WaveManager.RoundStage` 기본→엘리트→보스, **보스 처치 시 클리어**·남은 망자 붕괴, 매 밤 보스·5번째 밤은 대감), 기본 망자 마을 곳곳 배치·제자리 배회, 단계마다 45~95초 뒤 전원 돌진(`Monster.GlobalRush`)
- 완료 (2026-11-19, 여러 Phase 묶음 — 사용자 요청 "여러 단계 묶어서"):
  - Phase 2: 슬롯 5개(`PlayerWeapons.Hand` Gun/Melee/Gadget, 1·2·3·4·5, V 칼질, G 수류탄), `Player/Gadgets.cs`(수류탄·지뢰·설치형 기관총·유탄), X 키트/C 회복제(`PlayerCharacter`), 스태미너(`PlayerController`)
  - Phase 3: `DefaultContent.Catalog` 8계열×3단계(화염방사기·유탄·로켓 추가), `Gear/Grade.cs` 등급·강화, `Gear/Armor.cs` 방어구 5부위, 망자 방어도 `WaveManager.DefenseBonus`, 서낭당 '무기고'·'방어구·보급' 탭
  - Phase 4: `RoundRule` 사냥/지킴/소탕, `Modifier` 변고 6종 → `Core/GameRules.cs` 배율
  - Phase 5: `CharacterStats.Soft` 수확 체감(16·28), `BalanceBonus`
  - Phase 6: `SFX.ShotFor`(무기별 총성), `HitFor`(계열별 타격음), 탄창식 장전(분리→삽입→빈 탄창이면 노리쇠), 산탄총 한 발씩 장전
  - 그래픽: Poly Haven 소품 모델 `Core/PropModels.cs` + `Resources/Props/`(장독대·LPG·드럼통·상자·바위·덤불·가로등 등), 망자 갓·상투·전립 `MonsterFactory.Headwear`, 무기 HUD 실루엣 `UI/WeaponIcon.cs`
- 다음 할 일: 사용자 플레이 확인 → 밸런스 조정. 남은 그래픽: 건물(한옥·초가) 모델(무료 한국풍 모델 드묾), 망자 소복·도포 의상, 1인칭 총 모델 사실화
- 환경 '2번째'에 Poly Haven 호스트(api.polyhaven.com, dl.polyhaven.org) 허용됨. API는 User-Agent 필요.
- **그래픽 결정됨 (2026-11-18): Mixamo 실사 망자 + 분위기형 배경.**
  - **완료 (2026-11-18):** Mixamo API(사용자 토큰, 환경 네트워크 시크릿)로 `Romero` + 동작 8개를 Collada(DAE)로 받아 `Assets/Resources/Mixamo/`에 넣음.
    FBX는 4K 텍스처 내장으로 110MB라 DAE로 받고 텍스처를 1K JPG로 줄임. 뼈 접두어 `mixamorig5_` (캐릭터마다 달라서 동작은 캐릭터별로 받아야 함).
    `ZombieRig`: Mixamo 우선 로드 → 없으면 Quaternius. 비명(발견 시)·비틀거림(강타)·죽는 동작 재생. Romero 약 2.5만 정점 → '하'는 동시 10마리, 2본 스키닝.
    받는 스크립트 예시: Mixamo API `/api/v1/animations/export` (X-Api-Key: mixamo2) → `/characters/{id}/monitor` 폴링 → job_result URL 다운로드.
  - 갓·도포·상투는 모델 위에 덧입힘(킹덤풍). 배경은 각진 도형을 둥글게·자연스럽게 다듬되 HD 630 '하' 품질 유지.

## 작업 습관
- 요청은 작게, 수정 후 컴파일 확인 → 커밋 → main 푸시. 커밋 메시지는 한국어.

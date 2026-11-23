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

## 진행 순서 (2026-11-19 사용자 결정): **PC 방식으로 게임을 끝까지 완성 → 마지막에 모바일 조작으로 맞춤**
- 지금은 키보드·마우스 기준으로 작업. 모바일 코드는 유지하되 PC 기본값은 꺼짐 (`GameSettings.ForceMobileControls` 기본 false).
- 모바일 단계에서 할 일 (사용자와 논의됨, 미구현): 달리기 고정(조이스틱 위로 밀면 계속 달림), 자이로 조준, 왼쪽 사격 버튼, 조이스틱 크기·투명도 설정.

## 모바일 앱 (최종 목표는 앱. 핵심 = 마구 쏘는 쾌감·타격감·긴장감, 기능 추가보다 잘 돌아가는 것)
- 입력은 전부 `Core/GameInput` 경유 (Input.* 직접 호출 금지). PC=키보드·마우스, 휴대폰=`UI/MobileControls` 터치(실행 순서 -200).
- 자동 사격(`GameInput.AutoTarget`, 조준점 레이캐스트) · 조준 보조(`PlayerController.FindAssistTarget`) · 설정은 `GameSettings`(Esc 메뉴에서 토글).
- 에디터 시험: 메뉴 Hunt → 모바일 조작 미리보기 (마우스 = 손가락 하나). 터치 모드는 커서 잠금 안 함.
- 휴대폰: 품질 기본 '하'(고급기 '중'), 렌더 해상도 60/75/90%, 가로 고정, 화면 꺼짐 방지, 글꼴은 `Resources/Fonts` 나눔(OFL).
- 빌드: `Assets/Editor/HuntBuild.cs` 메뉴 Hunt → 안드로이드 APK 만들기 / USB로 바로 설치. 빈 씬 `Assets/Scenes/Main.unity` 자동 생성,
  IL2CPP+ARM64, minSdk 24, Standard·Sprites 셰이더 항상 포함 + `Resources/ShaderKeep` 키워드 재질(런타임 재질 분홍색 방지).
- 에디터 코드 컴파일 확인: NuGet `Unity3D.UnityEditor 2018.1.6-f1` 참조 추가 (`UnityEditor 2019.3.2.1` 패키지는 비어 있음).

- 긴장·타격 피드백 (2026-11-19): 피격 방향 표시(`PlayerCharacter.DamageFrom`, `GameHud.DrawDamageDirections`), 9m 안 망자 3D 그르렁, 저체력 심장 박동,
  연속 처치 문구·높아지는 처치음, 휴대폰 진동, 탄피·총구 연기, 머리/급소 처치 폭발, 멀리서 들리는 공포음(`Atmosphere`), 비상 절약 모드(`QualityManager.Emergency`).

## 능력치 (2026-11-19 사용자 확정, 5개)
힘(피해·무게·밀어내기) · 민첩(이동·장전·교체·스태미너) · 생명(체력·받는 피해·역병 저항 + 반동·탄퍼짐·조준 = 옛 '제어') · 집중(치명·약점) · 기술(벽 관통·수류탄/설치물·역병 능력).
`StatType.CTL`은 코드에 남아 있지만 화면·포인트에서 제외 (`CharacterStats.Visible`), 제어 공식은 생명 값을 쓴다. 벽 관통: `PlayerWeapons` 총알 궤적 + `CharacterStats.WallPenetration`.

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
- 완료 (2026-11-19 오후): 1인칭 실사 총(권총·저격총, `Weapons/RealGunModel.cs`, Poly Haven), 망자 소복·도포(`MonsterFactory.Robe`, 갓·상투 쓴 조선 망자만),
  기와집 곡면 지붕(오목 경사·들린 처마 귀·용마루 끝)·박공벽·한옥 기둥/도리/툇마루/창호(`VillageBuilder.CurvedRoof`, `HanokFrame`). 양면 메시는 `Core/MeshUtil.DoubleSided`(노멀 상쇄 방지).
- 완료 (2026-11-19 저녁, 사용자 피드백 12항목 중):
  - 보스 수정: 지킴(밤 절반 지나면)·소탕(엘리트 뒤)에도 보스, 엘리트 75초 강제 전환, 보스는 수 제한 무시·22~30m 근처·위치 표시 (`WaveManager`)
  - 망자 유형 8종 `DefaultContent.ZombieKinds` (기본·단단·빠름·광폭·물어뜯기·철벽·거구·추적, 체력/피해/이동/공격간격 배율), 살빛 구분
  - 처치 드롭 `Player/Pickup.cs` (키트·회복제·탄창, 자석 줍기), 앉기 Ctrl (`PlayerController.IsCrouching`, 탄퍼짐 ×0.6·반동 ×0.65)
  - 능력치 [+1]/[전부] 버튼 + 칸 눈금 `UIKit.StatCells`/`SmallButton`
  - 실총 녹음 총성 `Resources/Sounds/Guns` (Free Firearm Sound Library CC0) + `SFX.PlayShot`(녹음+저음층), 장전음 `Sounds/Mech`
  - 총 모델 `Resources/GunsBin/<id>.bytes`(원본 `Tools/GunSource/*.txt`) + `Weapons/PackGunModel.cs` (Quaternius 팩·M4A1·화염방사기, OpenGameArt CC0). 변환 = OBJ → 총 공간 텍스트
- **자동 진행 (사용자 요청 2026-11-19): "그만"이라고 할 때까지 예약 작업(3시간마다, trigger `trig_01CsfMcvJFCBg6SvvCs8uaSu`)으로 계속 업그레이드.**
  매번: git pull → 작게 수정 → 컴파일 확인 → 한국어 커밋 → main 푸시 → 이 목록 갱신 → 바뀐 점을 짧게 보고(미리보기 이미지 있으면 보냄).
  클라우드에선 게임 화면 캡처 불가 (유니티 실행 불가).
  **사용자 피드백이 오면 그게 최우선.** "모바일 시작" → 휴대폰 단계(`Docs/MOBILE_READINESS.md` 할 일), "그만" → 예약 작업 삭제.
  - 완료된 큰 작업 (2026-11-19~23): 맵(굽은 길 `VillageBuilder.Ribbon`/`RoadX`·`RollingGround`·경계 수풀·`Half` 44) · 초가집 `ThatchRoof` ·
    능력 칸 `UI/HudIcons.cs`·`GameHud.AbilitySlot` · 기질 카드 · 보급품 알림 `GameHud.OnPickup` · 앉기 표시 ·
    유탄/로켓 모델(RPG7) · 무기 아이콘 `Resources/WeaponIcons`(전부, `WeaponIcon.Draw(Rect, WeaponData, Color)`) · 무기고·방어구 탭 ·
    망자 유형 외형 `MonsterFactory.KindDressing`·엘리트 `EliteDressing`·조준 이름표 `GameHud.DrawTargetTag` · 밸런스 `Docs/BALANCE.md`(`Monster.DamageScale`) ·
    망자 소리 `Sounds/Zombie`(`SFX.Rec`·`PlayAt3D`·`Monster.VoicePitch`) · 발소리 `Sounds/Steps`(`SFX.Footstep`, 망자 발소리) · 착지 ·
    게임오버 `PlayerController.DeathCam`·최고 기록 `hunt.best.*` · 시작 제목 카드 `StartScreen.DrawIntroCard` · 첫 밤 조작 안내 `UI/TutorialHints.cs`(Esc에서 다시 보기) ·
    피 웅덩이 `FX.BloodPool`·살점 `MonsterGib.Chunk` · 날씨 `Atmosphere.SetWeather` · 서낭당 새벽 `Atmosphere.Dawn` · 보스 돌진·내려치기 `Monster.BossPattern`(약한 보스 ×0.6) ·
    총 모델 이진화 `Resources/GunsBin` + 메시 캐시 · 보급품 가득 참 알림 `Pickup.toldFull`·종류별 줍는 소리·떨어질 때 튀어 오름 · 탄약 경고 `GameHud.DrawAmmoWarning` · 터치 앉기 버튼 · 작은 장식 그림자 끔 · 사망 화면 카메라 검색 1회로 · 새 판에 지난 알림 지움 `GameHud.ClearToasts`.
  - 보류: 망자 모델 다양화 (Mixamo — 사용자 토큰 다시 필요, 토큰은 하루쯤 지나면 만료).
  - 이후 작은 후보: 사용자 피드백 반영 → 성능(실기기 확인 필요) · 버그 점검 · 소리/연출 다듬기.
- 환경 '2번째'에 Poly Haven 호스트(api.polyhaven.com, dl.polyhaven.org) 허용됨. API는 User-Agent 필요.
- **그래픽 결정됨 (2026-11-18): Mixamo 실사 망자 + 분위기형 배경.**
  - **완료 (2026-11-18):** Mixamo API(사용자 토큰, 환경 네트워크 시크릿)로 `Romero` + 동작 8개를 Collada(DAE)로 받아 `Assets/Resources/Mixamo/`에 넣음.
    FBX는 4K 텍스처 내장으로 110MB라 DAE로 받고 텍스처를 1K JPG로 줄임. 뼈 접두어 `mixamorig5_` (캐릭터마다 달라서 동작은 캐릭터별로 받아야 함).
    `ZombieRig`: Mixamo 우선 로드 → 없으면 Quaternius. 비명(발견 시)·비틀거림(강타)·죽는 동작 재생. Romero 약 2.5만 정점 → '하'는 동시 10마리, 2본 스키닝.
    받는 스크립트 예시: Mixamo API `/api/v1/animations/export` (X-Api-Key: mixamo2) → `/characters/{id}/monitor` 폴링 → job_result URL 다운로드.
  - 갓·도포·상투는 모델 위에 덧입힘(킹덤풍). 배경은 각진 도형을 둥글게·자연스럽게 다듬되 HD 630 '하' 품질 유지.

## 작업 습관
- 요청은 작게, 수정 후 컴파일 확인 → 커밋 → main 푸시. 커밋 메시지는 한국어.

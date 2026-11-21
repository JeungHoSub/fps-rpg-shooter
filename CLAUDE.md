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
- **자동 진행 (사용자 요청 2026-11-19): 사용자가 그만하라고 할 때까지 예약 작업으로 계속 업그레이드.** 남은 큐(순서대로):
  1. ~~맵 덜 딱딱하게~~ 완료(굽은 길 `VillageBuilder.Ribbon`/`RoadX`, 울퉁불퉁 땅+가장자리 언덕 `RollingGround`(보이기만, 판정은 평평), 틀어진 논, 경계 수풀). 영역 축소 완료(`VillageBuilder.Half` 48→44, 무덤·고분·창고·곤포·장승·비닐하우스·스폰 앵커 안쪽으로, 망자 제한도 Half 기준) 2. 능력 칸 완료(`UI/HudIcons.cs` 그림 아이콘·먹 바탕 칸 `GameHud.AbilitySlot`), 기질 카드 완료(한지 카드·낙관·효과 줄나눔·숫자키 1~3, 기질 이름/설명 한국어화) 3. 앉기 표시·보급품 주움 알림(`GameHud.OnPickup`) 완료
  4. 유탄·로켓발사기 완료(RPG7 CC0 + Quaternius 산탄총 총열 굵게, 변환기 `fat`·`pca` 옵션 = scratchpad tools/gunconv.py) 5. 유형별 외형 완료(`MonsterFactory.KindDressing`: 체격 폭·눈빛 색·배/굳은살/앞치마+식칼/핏자국/천, 조준 이름표 `GameHud.DrawTargetTag`) 6. 초가집 완료(`VillageBuilder.ThatchRoof`: 둥근 빵 모양 곡면·두툼한 처마·새끼줄 그물·용마름 + HanokFrame) 7. 밸런스 완료(`Docs/BALANCE.md`: 방어도 상한, 산탄총·기관단총 관통, 망자 공격력 밤마다 +5% `Monster.DamageScale`, 그림자 망자 감속)
  다음 큐: 8. 무기 아이콘 완료(`Resources/WeaponIcons/<id>.png` = 총 텍스트 옆모습 렌더, `WeaponIcon.Draw(Rect, WeaponData, Color)`, 권총은 도형 유지) 9. 망자 소리 완료(`Sounds/Zombie` CC0 녹음 41개, `SFX.Rec`·`PlayAt3D`, 몸집별 목소리 높낮이 `Monster.VoicePitch`, 발견 비명·멀리 신음) 10. 무기고 완료(칸마다 무기 그림·상태색, 오른쪽 큰 그림+능력 막대, 방어도 안내 새 식) — 방어구 탭은 그대로
  11. 게임오버 완료(쓰러지는 시점 `PlayerController.DeathCam`, 붓 제목·묘비 문구·'○○에게 물려 쓰러짐' `PlayerCharacter.LastAttacker`·최고 기록 PlayerPrefs `hunt.best.*`·새 기록 낙관), 시작 화면 완료(첫 실행 제목 카드 `StartScreen.DrawIntroCard`·낙관 찍힘+징·앞 빗줄기·최고 기록) 12. 성능 1차 완료(총 모델 이진화 `Resources/GunsBin/*.bytes` + 메시 캐시: 읽기 41ms→0.2~7ms·정점 절반, 원본 텍스트·변환 도구는 `Tools/`) 13. 망자 모델 다양화(Mixamo 다른 캐릭터 — 사용자 토큰 필요하면 요청)
  이후 후보: 14. 방어구 탭 다듬기 15. 사망 연출 완료(피 웅덩이 `FX.BloodPool`/`BloodPoolFx` 최대 18개·48초, 머리·급소 처치 살점 `MonsterGib.Chunk`) — 13번은 Mixamo 토큰 대기 중이라 건너뜀 16. 날씨 완료(`Atmosphere.SetWeather`: 보슬비·장대비·짙은 안개·뇌우·비 잦아듦, 첫 밤 보슬비·대감의 밤 뇌우, 비 세기·안개 농도·천둥 간격·달빛 배율) 17. 튜토리얼 안내(첫 밤 키 설명)
  - 매번: 컴파일 확인 → 커밋 → 푸시 → 바뀐 점 보고. 클라우드에선 게임 화면 캡처 불가 (유니티 실행 불가) — 모델/UI 미리보기 이미지만 가능.
- 다음 할 일: 사용자 플레이 확인 → 밸런스 조정. 남은 그래픽: 소총·기관단총·산탄총·경기관총 실사 모델(Quaternius Ultimate Gun 팩 — 구글 드라이브 다운로드 한도로 보류, 나중에 재시도), 초가집 사실화
- 환경 '2번째'에 Poly Haven 호스트(api.polyhaven.com, dl.polyhaven.org) 허용됨. API는 User-Agent 필요.
- **그래픽 결정됨 (2026-11-18): Mixamo 실사 망자 + 분위기형 배경.**
  - **완료 (2026-11-18):** Mixamo API(사용자 토큰, 환경 네트워크 시크릿)로 `Romero` + 동작 8개를 Collada(DAE)로 받아 `Assets/Resources/Mixamo/`에 넣음.
    FBX는 4K 텍스처 내장으로 110MB라 DAE로 받고 텍스처를 1K JPG로 줄임. 뼈 접두어 `mixamorig5_` (캐릭터마다 달라서 동작은 캐릭터별로 받아야 함).
    `ZombieRig`: Mixamo 우선 로드 → 없으면 Quaternius. 비명(발견 시)·비틀거림(강타)·죽는 동작 재생. Romero 약 2.5만 정점 → '하'는 동시 10마리, 2본 스키닝.
    받는 스크립트 예시: Mixamo API `/api/v1/animations/export` (X-Api-Key: mixamo2) → `/characters/{id}/monitor` 폴링 → job_result URL 다운로드.
  - 갓·도포·상투는 모델 위에 덧입힘(킹덤풍). 배경은 각진 도형을 둥글게·자연스럽게 다듬되 HD 630 '하' 품질 유지.

## 작업 습관
- 요청은 작게, 수정 후 컴파일 확인 → 커밋 → main 푸시. 커밋 메시지는 한국어.

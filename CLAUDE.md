# 역병의 마을 (PROJECT: HUNT) — 작업 안내

Unity 1인칭 싱글플레이 좀비 슈터 + RPG. **비 오는 밤의 한국 시골, 현대 시골 + 조선 역병.**
상세 컨셉: `Docs/CONCEPT_KR_HORROR.md` · 시스템 기획: `Docs/GAME_DESIGN_DRAFT.md` · 에셋 출처: `Docs/CREDITS.md`

## 핵심 사실
- 씬·프리팹 없음. `Core/GameBootstrap`이 Play 시 전부 코드로 생성 (마을 `VillageBuilder`, 날씨 `Atmosphere`).
- 렌더 파이프라인: Built-in (Standard 셰이더). 입력: 구 Input Manager. UI: IMGUI (`UI/HUD.cs` 진입점).
- 에셋은 `Assets/Resources/` (Env = Poly Haven 텍스처, Zombies = Quaternius 좀비 FBX). 좀비 모델 없으면 도형 괴물로 대체.
- 좀비 애니메이션은 Animator Controller 없이 Playables (`Monsters/ZombieRig.cs`). 피격 판정체는 뼈에 부착 (`MonsterFactory.CreateRigged`).
- **MonoBehaviour 필드 초기화에서 Texture2D 등 유니티 오브젝트 생성 금지** (과거 시작 화면 먹통 원인). Awake/지연 생성. `Random.*`·`Time.*` 같은 유니티 API 호출도 필드 초기화에서 금지(예외 남).
- 그래픽 품질 하/중/상: `Core/QualityManager.cs` (사양 자동 감지, 60fps 제한, 저프레임 자동 하향).

## 사용자 환경
- Windows, Unity 6.6, **Intel HD Graphics 630 (내장 그래픽)**, i5-7500, RAM 16GB → '하' 품질 기준으로 성능 판단.
- 사용자는 비개발자. 업데이트는 GitHub ZIP + PowerShell robocopy로 바탕화면 `fps-rpg-shooter`에 덮어씀 (삭제된 파일은 남음). 명령 문구: `Docs/UPDATE_POWERSHELL.txt`.
- 여기(클라우드)에서는 유니티 실행 불가 → 변경 후 컴파일만 확인 가능:
  dotnet SDK + NuGet `UnityEngine.Modules 2021.3.33` 참조로 `Assets/Scripts/**/*.cs` 빌드 (2022+ API는 `#if UNITY_2022_2_OR_NEWER`).
  **주의 (2026-11-25 사용자 유니티 오류)**: 프로젝트는 `Packages/manifest.json`에 켜진 모듈만 쓸 수 있다 (ScreenCapture 모듈이 없어 F12 사진에서 CS0103 → 모듈 추가로 고침).
  컴파일 확인은 그 모듈 DLL만 참조하는 scratchpad `modcheck` 로 한다 (`ship.sh`가 사용). 새 모듈 API(ImageConversion·Terrain·Video 등)를 쓰려면 manifest에 모듈을 먼저 추가.

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
- **자동 진행 (사용자 요청 2026-11-19): "그만"이라고 할 때까지 예약 작업(2026-11-24부터 1시간마다 매시 21분, trigger `trig_01CsfMcvJFCBg6SvvCs8uaSu`)으로 계속 업그레이드.**
  매번: git pull → 작게 수정 → 컴파일 확인 → 한국어 커밋 → main 푸시 → 이 목록 갱신 → 바뀐 점을 짧게 보고(미리보기 이미지 있으면 보냄).
  클라우드에선 게임 화면 캡처 불가 (유니티 실행 불가).
  **작업 방식 (2026-11-24 사용자 요청 "멈추지 말고 계속, 1시간마다 피드백")**: 한 대화 턴 안에서 쉬지 않고 작은 항목을 계속 작업, 약 1시간마다 진행 보고(SendUserFile/PushNotification). 예약 작업(매시 21분)은 멈췄을 때 다시 이어 주는 안전장치.
  **보고 방식 (2026-11-24 사용자 요청): "많이 작업한 다음에 알려줘, 너무 자주 묻지 말고"** → 한 번에 2~4개 묶어 작업, 보고는 묶어서 한 번(1시간마다), 선택지 질문 나열 금지.
  **사용자 피드백이 오면 그게 최우선.** "모바일 시작" → 휴대폰 단계(`Docs/MOBILE_READINESS.md` 할 일), "그만" → 예약 작업 삭제.
  - 완료된 큰 작업 (2026-11-19~23): 맵(굽은 길 `VillageBuilder.Ribbon`/`RoadX`·`RollingGround`·경계 수풀·`Half` 44) · 초가집 `ThatchRoof` ·
    능력 칸 `UI/HudIcons.cs`·`GameHud.AbilitySlot` · 기질 카드 · 보급품 알림 `GameHud.OnPickup` · 앉기 표시 ·
    유탄/로켓 모델(RPG7) · 무기 아이콘 `Resources/WeaponIcons`(전부, `WeaponIcon.Draw(Rect, WeaponData, Color)`) · 무기고·방어구 탭 ·
    망자 유형 외형 `MonsterFactory.KindDressing`·엘리트 `EliteDressing`·조준 이름표 `GameHud.DrawTargetTag` · 밸런스 `Docs/BALANCE.md`(`Monster.DamageScale`) ·
    망자 소리 `Sounds/Zombie`(`SFX.Rec`·`PlayAt3D`·`Monster.VoicePitch`) · 발소리 `Sounds/Steps`(`SFX.Footstep`, 망자 발소리) · 착지 ·
    게임오버 `PlayerController.DeathCam`·최고 기록 `hunt.best.*` · 시작 제목 카드 `StartScreen.DrawIntroCard` · 첫 밤 조작 안내 `UI/TutorialHints.cs`(Esc에서 다시 보기) ·
    피 웅덩이 `FX.BloodPool`·살점 `MonsterGib.Chunk` · 날씨 `Atmosphere.SetWeather` · 서낭당 새벽 `Atmosphere.Dawn` · 보스 돌진·내려치기 `Monster.BossPattern`(약한 보스 ×0.6) ·
    총 모델 이진화 `Resources/GunsBin` + 메시 캐시 · 보급품 가득 참 알림 `Pickup.toldFull`·종류별 줍는 소리·떨어질 때 튀어 오름·새벽엔 30m에서 모여듦 · 탄약 경고 `GameHud.DrawAmmoWarning` · 터치 앉기 버튼 · 작은 장식 그림자 끔 · 사망 화면 카메라 검색 1회로 · 새 판에 지난 알림 지움 `GameHud.ClearToasts` · 창 벗어나면 자동 일시정지 `GameManager.AutoPause` · 수류탄 불씨 깜빡임·째깍 `ThrownGrenade.spark` ·
    시야각 설정 `GameSettings.Fov` · 폭발 연출 `Core/ExplosionFx.cs`·그을음 `FX.Scorch`·밀려남 `Monster.Blast`·귀 울림 `Core/EarRing.cs` · 가라앉는 시체 그림자 끔 ·
    **사용자 피드백 (2026-11-24) "UI 매끄럽게 · 총은 까매야지" → 다시 "회색이 나아, 더 예쁘게"** → 총은 회색 총철(밝기 차이↑·푸른 기·금속광·호두나무 개머리, `PackGunModel.GetMaterial`·`RealGunModel` 색조) · UI 움직임 `UIKit.Smooth`/`EaseOut`(일시정지 등장·서낭당 탭 밑줄/전환·엽전·경험치·알림 줄·무기 칸 전환·탄 숫자 튐·조준선) · 새벽 기록 카드 `GameHud.DrawDawnCard`(서낭당 위 창, 클릭 닫힘 `DawnInput`, 대감 처치 시 '마을을 지켜냈다') · X자 적중 표시 ·
    **피드백 "죽으면 피가 화면 중간에 떠 있다 · 최종보스 안 나오나"** → 피 웅덩이 바닥 찾기 `FX.FloorUnder`(망자 판정 건너뜀) · 보스 등장 큰 이름 `GameHud.DrawBossIntro`, 매 밤 '대감의 그림자'/다섯째 밤 최종 '역병 대감'(`GameHud.BossName`, 붉은 기운 빛), 밤 표식 '대감의 밤까지 N밤' ·
    **요청 "몹 잡으면 힐링룬·탄창"** → `PickupKind.Rune` 치유 부적(즉시 생명 15%, ~20%, 보스 3개, 옥빛 화면 `GameHud.HealFlash`) · 탄창 모양(휜 탄창+탄알)·확률 ~22% · 보급품 빛 동시 4개 제한 ·
    **요청 "E로 둘 다 먹게"** → E = 3.5m 안 보급품 줍기(`Pickup.GrabRange`, `GameInput.PickupDown`), 없으면 재생(`GameInput.RegenDown`이 `Pickup.AnyNearPlayer` 확인) · 발밑 안내 `GameHud.DrawPickupPrompt`. 자동 자석 줍기도 유지(가득 차면 안 끌림 → 안내 문구) · 뒤쪽 벽 피 자국 `FX.BloodSplat` · 보급품 빨려 들어올 때 작아짐 · 머리 맞으면 갓 날아감 `Monster.KnockOffHat`/`HatFly` · 탄약 가득하면 탄창→수류탄 · 망자 조준 시 조준선 붉게 (`GameHud.aimOnMonster`).
  - 2026-11-24 연속 작업 (05:25~): 밝기 `GameSettings.Brightness` · 난이도 `GameSettings.Difficulty`(Esc) · 보스 '대' 표식·나침반 띠 `GameHud.DrawCompass` · 지도 M `UI/MapView.cs` ·
    수류탄 길·설치 자리 미리 보기 `PlayerWeapons.UpdateGrenadeArc/UpdatePlacePreview` · 기관총 남은 시간 `SentryTurret.Active` · 헐떡임 `SFX.Breath` · 보스 처치 슬로 `GameManager.SlowMo`+`WaveManager.BossFallThenDawn` ·
    밤 시작 제목 `GameHud.DrawNightBanner` · 땅에서 기어 나옴 `Monsters/RiseFromGround.cs` · 피해 숫자 다듬기 · 조준경 흔들림+Shift 숨 참기 `PlayerController.BreathHold` · 연속 처치 보상 ·
    공격 예고 `Monster.windup` · 저체력 먹먹 `EarRing.lowHp` · 무기고 비교 금 · 새로 바뀐 점 `GameText.WhatsNew` · 칼 파고들기 · 화면 빗방울 `GameHud.DrawLensRain` · 사망 요령 `GameText.DeathTips` ·
    새 망자 `bloat`(터짐, `Monster.BloatBurst`)·`shriek`(곡, `Monster.Wail`) · 새 기질 화약쟁이·약초꾼·머리 사냥꾼(`DamageCalculator.PlayerHeadBonus`) · 미끄러지기 `PlayerController.IsSliding` ·
    보급 상자 `Player/SupplyCrate.cs` · 보스 북 `SFX.Drum` · 무기 숙련 `WeaponInstance.kills/Mastery` · 찢어진 쪽지 `GameText.LoreNotes`(`GameHud.ShowNextLoreNote`, 역병 일지에서 다시 읽기) ·
    업적 `Core/Achievements.cs` · 처음 보는 망자 알림 `GameHud.FirstSight` · 화면 피 `GameHud.ScreenBlood` · 피 표현 끄기 `GameSettings.Gore` · 까마귀 `Core/Crows.cs` · 조준 감도 `GameSettings.AdsSensitivity` ·
    탄 떨어지면 자동 교체 · 다시 일어나는 망자 `WaveManager.Revive`(머리 처치 제외) · 다음 밤 미리 보기 `WaveManager.RuleFor` · 화염병 `GadgetType.Molotov`/`ThrownMolotov`/`FireZone` · 불붙은 망자 `Monster.Ignite`.
    10:22~: 조준점 모양 · 단계 상승 연출 · 매 밤 보스 성질 `Monster.SetBossAffix` · 현상금 망자 `Monster.MakeBounty` · F3 FPS · 등 뒤 경고 `GameHud.DrawBehindWarning` · 조작 안내 E·M ·
    터지는 소품 `Player/ExplosiveProp.cs` · F12 사진 · 업적 카드 `HUD.ShowAchievement` · 상처 없는 새벽 · 쏘면 꺼지는 가로등 `Core/ShootableLamp.cs`(+`PlayerLit` 시야). 수치 정리 `Docs/SYSTEMS_NEW.md`.
    던지기 힘 `PlayerWeapons.ThrowCharge` · 총성 메아리 `SFX.Echo` · 별 총구 불꽃 `WeaponViewModel.StarMaterial` · 망자 도감(역병 일지) · 빠른 출정 · 하늘의 달·번개 `Atmosphere.UpdateMoonDisc/SpawnBolt` ·
    소리 없는 처치(등 뒤 칼, `swingUnaware`) · 이름표 알아챔 · 보급품 바닥 고리 · 지킴/소탕 경고 · 돌진 연출 `GameHud.RushAt` · 갑옷 '팅' · 판 통계(사망 화면) · 흔들림 설정 `GameSettings.Shake` ·
    부적 `PlayerCharacter.CharmGuard/Swift/Aim` · 숨은 장승 `Player/Jangseung.cs`(`Blessings`, 풍경 소리) · 혼잣말 `GameHud.Mutter` · 툴팁 페이드 · 핏빛 달 · 문 두드림 `SFX.Knock` ·
    기관총 거두기 `SentryTurret.NearestToPlayer` · 수류탄 경고 `ThrownGrenade.Live` · 청심환 `PickupKind.Pill` · 숙련 진행 선 · 도깨비불 `Core/Dokkaebi.cs` · 상자 함정 · 현상금 도망 ·
    밤 제목 날씨 · 사망 슬로 · 일시정지 먹먹 · 쫓는 망자 수(소리 막대 둘째 줄) · 회복제·키트 화면 빛 · 보급 도움(탄·부적) · 빗소리 크기 `GameSettings.AmbientVolume` · 들킴 '!' `Monster.SpottedAt` ·
    연속 처치 이름 · 열째 밤 문구 · 혼불 `SoulWisp` · 곡소리 물결 `WailRing` · 처치 엽전 숫자 · 곧장 다시 `GameManager.QuickRestart` · 장승 지도 표시 · 불바다 소리 `SFX.Crackle` ·
    비상 모드 연출 끔 · 최근 5판 `hunt.runs` · 보스 단계 번개 `Atmosphere.StrikeNow` · 밤 의뢰 `UI/NightTask.cs` · 엘리트 처치 멈칫 · 다음 보스 성질 예고 `WaveManager.NextBossAffix`.
    11:00~: 연출용 구 `FX.LowSphere`(성능) · 침 초록 번짐 `GameHud.AcidFlash` · 다리 휘청 `Monster.hobble`·칼 마무리 `Monster.Crippled` · 탄창 바닥 '틱' `SFX.LowAmmoTick` ·
    보스 근처 손전등 떨림 · 덤불 숨기 `Core/Hiding.cs`(앉기+손전등 끔, 지도 표시, 바스락) · 땅 명중 흙탕물·도탄음 `PlayerWeapons.ImpactSound` ·
    놓친 망자 '?' 수색 `Monster.LostAt` · 등 뒤 시야 ×0.45 · 돌 던지기 B `Player/ThrownStone.cs`(Noise fromPlayer=false → 뒤지기만).
    15:20~: 머리 처치 화면 당김 `PlayerWeapons.fovKick` · 망자 덤불 바스락 · 기습 ×1.5 · 날씨 시야 `Atmosphere.SightMult` · 보스 근처 마을 불빛 떨림 `Flashlight.BossNear` ·
    빈 탄창 떨어짐 · 조각 바닥 높이 `MonsterGib.floorY` · 확성기 탑 사이렌 `Core/SirenTower.cs`(E, 밤마다 한 번, Noise 110m) · 업적 hide10 · 의뢰 Hidden(`RunHidden`) · 덤불 잎 그늘 화면. 수치 `Docs/SYSTEMS_NEW.md` '몰래 하기'.
    발차기 `PlayerWeapons.Kick`(달리며 V, `KickLeg` 장화, `Monster.Stagger`) · 폭죽 `PlayerCharacter.Firecrackers`(`ThrownStone` firecracker) · 앉기 전환식 `GameSettings.CrouchToggle` ·
    3D 소리 22칸 풀 `SFX.Grab` · 싸우는 중 IMGUI Layout 생략 `HUD.useGUILayout` · 소음기 `AffixType.QuietPct`(`WeaponInstance.NoiseMult`) ·
    매복 망자 `Monster.Lurk`(덤불, 손전등에 붉은 눈, 업적 spotlurk) · 머리 처치 머리 터짐 `Monsters/HeadGone.cs` · 사이렌 붉은 등.
    정전 `LampFlicker.Blackout` · 다친 마을 사람 `Player/Survivor.cs`(지도·나침반 '사', 의뢰 Rescue, 업적 rescue5) · 사망 사인 `PlayerCharacter.DeathCause` ·
    소음기 모양 `GunModelBuilder.AddSuppressor` · 알아채기 직전 '…' `Monster.Suspicion` · 꾀인 망자 '?' (`HearNoise` fromPlayer=false). 15:56 보고함.
    16:00~: 망자 떼 `WaveManager.SpawnHorde`(셋째 밤부터, 나침반 '떼', 몰래 보내면 엽전 `JudgeHorde`) · 덤불 불타기 `Hiding.Burn` · 덤불 흔들림 `BushShake` ·
    보스 안개 · 서낭당 '있을 수 있는 일' · 이름표 '등 뒤 V' · Esc PC줄: 앉기 방식·머리 위 표시 `GameSettings.Marks` · 다친 사람 흐느낌 `SFX.Sob`.
    떼: 6밤 거구가 이끎·7밤 두 번·업적 horde3 · 매복 덤불 들썩임 · 숨었을 때 옆에서 킁킁 · 은신부 `PlayerCharacter.CharmHide` · 사망 화면 '숨어 보냄' · 조작 표 Ctrl·E 갱신.
    16:21~: 구한 사람 새벽 선물(`GameManager.OnWaveCleared`) · 발차기 멈칫 · 덤불 숨 참기(Shift, `PlayerController.HoldingBreath`, 막대·먹먹) · 쫓길 때 낮은 울림 `SFX.ChaseDrone`(Atmosphere) ·
    번개에 매복 눈 `Atmosphere.Flash` · 새벽 기록 망자 떼 `WaveManager.HordeResult` · 판 통계 '구한 사람' · 코앞/칼 처치 화면 피.
    그림자 망자 냄새(덤불 무시) · 시작 화면 요령 줄 · 명사수 슬로(머리 셋) · 확성기 쏘기 `SirenTower.ShotHorn` · 매복 안 그림(성능) · 떼 상한 +1 ·
    덤불 떠보기 `Monster.Disturb`(돌·폭죽·사격) · 가짜 다친 사람(`Survivor.trap`, 쏘기 `OnShot`) · 쑥 향 `CharmIncense` · 장승·상자·구출 시점 숙임 · 혼잣말(덤불·둘러싸임). 16:40 보고함.
    17:21~: 다친 사람 안내 키트 수 · 뒤진 자리 두리번 `Monster.lookAround` · **고침: 모르는 망자가 멈추거나 3m 안이면 나를 돌아보던 것** (faceDir).
    새 밤 떼 표시 초기화 · 쪽지 3장 · 밤 제목 징조 · 서낭당 보급 촘촘 칸(`compact`, 12칸) · 쫓기다 6m 밖 덤불 숨기 → 놓치고 언저리 수색 · 앉아 장전 3m · 떼 코앞 생성 방지. 17:30 보고함.
    18:21~: 망자 시야 벽 검사(`Sense` blocked, 2.5m 안은 앎) · 돌풍 `SFX.Wind`(`Noise.GustUntil` ×0.8, `Hiding.ShakeAll`, 소리 막대 표시) · 떼 가까우면 땅울림(`hordeThump`).
    19:21~: 벽 너머 그르렁 작게 · 현상금 망자 덤불 숨기(`bountyBush` → `Lurk`) · 떼 함께 알아챔(`packAlerted`) · 둘째 밤 몰래 하기 안내(`hunt.tut.stealth`) · `HUD.Notify(text, color, seconds)`. 20:22 보고함. 떼 바삐 걷기 `Monster.InHorde`.
    21:21~: 물 댄 논 `Hiding.InPaddy`(발소리 ×1.6, 걸음 ×0.85, 망자도 느림, 소리 막대 표시) · 물 튐 `FX.Splash`(나·망자 첨벙). 22:25 보고함.
    23:21~: 벽 너머 망자 발소리 작게. 00:21~: 시체 발견 → 뒤지기 (`Monster.recentBodies`, `seenBodyAt`). 01:21~: 도감 버릇 7줄. 02:21~ 점검(경고 0) · 03:21 하 품질 망자 물 튐 생략.
    **피드백 (2026-11-25) "아직도 게임이 조잡해"** → 화면 색감 `Core/PostGrade.cs` + `Resources/Shaders/HuntGrade.shader`(채도·푸른 그늘/따뜻한 빛·대비·비네트·입자·FXAA, `GameSettings.Grade`, Esc PC줄 셋째 칸) · 소리 막대 위 글씨 최소화(중요 상태 3개까지) · 알림 4줄·17px · 불빛 빛무리 `LampFlicker.MakeHalo` · 하늘 돔 `Atmosphere.UpdateSkyDome`.
    **피드백 (2026-11-25) 총·보스·보급품** → 보스 안 잡아도 밤 끝나던 문제 고침(`WaveManager.BossDown`·`RetryBoss`, 시간 끝나면 보스 등장) · 실제 탄창 교체 동작(`WeaponViewModel` reloadVisStage, 빈 총 노리쇠) · 타격음 `SFX.MakeImpact`(3가지)·적중 '틱' `MakeMarker` · AK형 br762 자세 `PackGunModel.Tweak` · 총마다 다른 총성(`GunRec` mix 합성층) · 장비 꾸러미 `PickupKind.Gear`(`TakeGear`) · 탄약통·한지 부적 모양 · 보급품 이름표 `GameHud.DrawPickupLabels` · E 5m / 밟으면 2.5m · 총 재질 셰이더 `Resources/Shaders/GunMetal.shader`(UV 없는 팩 총, 물체 공간 결) · 1인칭 손 `Weapons/ViewArms.cs`(모델별 손잡이·앞손 위치 표, 장전 때 왼손이 탄창 따라감, 실사 권총·저격총 `AttachReal`, 칼 `HeldHand`) · 못 줍는 보급품이 E 재생 막던 것 고침(`Pickup.AnyNearPlayer`).
    **"더 업그레이드" (2026-11-25 오전)** → 옆걸음 총 기울기·벽에 붙으면 총 거둠(`WeaponViewModel` wallNear) · 망자 맞은 뼈 홱(`ZombieRig.BoneJerk`) · 보급품 '[E] 모두 줍기 (N)' · 수류탄 쥔 손 · 입자 둥글게 `FX.SoftMaterial`(피·먼지·비·빗방울 튐) · 피 안개·총구 연기 `FX.mist` · 폭발 구름 판(`ExplosionFx` Q) · 불꽃 혀 `FX.FlameMaterial`(불바다·타는 망자) · `Effects.Burst` 빛 덩이 · 총구 불꽃 심 빛 판 · `FX.MakeGlow`(도깨비불·혼불·독침) · 탄흔 텍스처 `FX.HoleMaterial` · 벽 먼지 mist · 예광탄·번개 `FX.BeamMaterial` · 키트=구급낭·회복제=갈색 드링크 병 · 현상금 등불·매복 눈·수류탄 불씨 빛무리 · 빈 탄창 달그락 `magClatterAt` · 성능: 손가락·보급품·망자 장식 공 → `FX.LowSphere` · 조준경(`GameHud.DrawScopeMask` 그늘 고리 밀림 `scopeOff`·밀 점).
  - 2026-11-24 05:55~10:20 작업 서버 재시작으로 멈춤 → 10:22 재개. 커밋 전 컴파일 확인은 scratchpad `ship.sh "메시지"` (오류면 커밋 안 함).
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
- 눈에 띄는 변화를 넣으면 시작 화면 '새로 바뀐 점' 목록 `GameText.WhatsNew` 맨 위에 한 줄 추가 (9~10줄 유지).
- 요청은 작게, 수정 후 컴파일 확인 → 커밋 → main 푸시. 커밋 메시지는 한국어.

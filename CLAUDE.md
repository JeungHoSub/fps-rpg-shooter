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

## 남은 로드맵
1. 사용자 확인 대기: 좀비 모델 방향/키, '하' 품질 프레임, 한국풍 UI 인상, 소리 시스템 손맛(감지 거리·45초 냄새 추적)
2. ~~소리 시스템~~ 완료: `Core/Noise.cs` (총성 28~50m·발소리 4/10m, 폭우 시 최대 45% 감소), `Monster` 감지 상태(9m 시야·피격·소리 → 추적, 아니면 어슬렁), HUD '소 리' 막대
3. 오행 속성 (물리/화염/냉기/전기/독 → 금/화/수/목/토, 상극·상생)
4. 밤낮 순환 (낮 탐색·파밍, 밤 웨이브)
5. (선택) 실사 좀비: 사용자가 Mixamo에서 받으면 교체

## 작업 습관
- 요청은 작게, 수정 후 컴파일 확인 → 커밋 → main 푸시. 커밋 메시지는 한국어.

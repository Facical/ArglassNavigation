# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 커뮤니케이션

- 사용자와 대화할 때 항상 한국어를 사용할 것.

## 프로젝트 개요

스마트 글래스(XReal Air2 Ultra)와 스마트폰(XReal Beam Pro)을 활용한 하이브리드 실내 내비게이션 HCI 연구 프로젝트. **2가지 조건(Glass Only / Hybrid)**을 단일 조건 직접 선택 방식으로 24명 대상 비교 실험. **Route B 단일 경로 + 미션 세트 2개(Set1/Set2)**로 within-subjects 설계 유지. 실험자가 메인 화면에서 참가자 ID, 미션 세트, 조건을 선택하여 단일 조건만 실행. 미션 기반 길찾기 태스크와 4종 불확실성 트리거를 통해 교차검증 행동을 유도. 문서는 주로 한국어로 작성.

## 저장소 구조

```
ARglasses/
├── ARNavExperiment/   # Unity 프로젝트 (단일 씬, C# 63개)
├── analysis/          # Python 분석 스크립트 4개 (→ analysis/output/)
├── data/raw/          # 실험 CSV 로그 (디바이스에서 수집)
├── docs/              # 실험 설계, 프로토콜, 설문지 등
└── tools/             # adb 유틸리티, 도면 추출 (pull_glass_captures.sh, pull_diagnostics.sh, extract_floorplan.py)
```

Git LFS 필수 (`.aar`, `.exe` 파일 추적):
```bash
git lfs install
git lfs pull
```

## Unity 프로젝트 (`ARNavExperiment/`)

- **Unity 버전**: 2022.3.62f2 (LTS)
- **빌드 대상**: Android (XReal Beam Pro)
- **Android 패키지명**: `com.KIT_HCI.ARNavExperiment`
- **단일 씬**: `Assets/Scenes/MainExperiment.unity`
- **주요 패키지**: XREAL SDK 3.1.0 (로컬, `LocalPackages/com.xreal.xr/`), AR Foundation 5.1.5, XR Hands 1.4.1, XR Interaction Toolkit 2.5.4, InputSystem 1.7.0, TextMesh Pro 3.0.6
- **네임스페이스**: `ARNavExperiment.*` — Domain.Events, Application, Core, Mission, Navigation, Presentation.Glass/BeamPro/Experimenter/Mapping/Shared, Logging, Utils, DebugTools, EditorTools. 폴더→네임스페이스 불일치: `Debug/`→`.DebugTools`, `Editor/`→`.EditorTools`
- **어셈블리 구조**: 4개 커스텀 `.asmdef` — `ARNav.Domain`(순수 C#, noEngineReferences), `ARNav.Runtime`(Scripts/ 루트), `ARNav.Debug`(Debug/), `ARNav.Editor`(Editor/ + UnityEditor). XREAL SDK 자체 asmdef: `Unity.XR.XREAL`(`LocalPackages/com.xreal.xr/`)

### 필수 초기 셋업 순서 (신규 환경)

**이 순서를 어기면 핸드트래킹/터치 입력이 완전 불능**:

1. Unity Hub에서 프로젝트 열기 (2022.3.62f2)
2. **XRI Starter Assets 샘플 Import** — Package Manager → XR Interaction Toolkit → Samples → Starter Assets
3. **`XREAL > Setup Hand Tracking`** 실행 — SDK가 Hand Ray 프리팹 및 InputAction 바인딩 생성
4. **`ARNav > Master Setup > Full Setup`** 실행 — 10단계 자동 구성:
   1. SceneSetupTool — 메인 씬 오브젝트 생성 (ExperimenterCanvas 포함)
   2. XROriginSetupTool — XR Origin (XREAL) + 핸드트래킹 구성
   3. MissionDataGenerator — 미션/POI 데이터 SO 생성
   4. MissionWiringTool — 미션 데이터를 씬에 와이어링
   5. InfoCardDataGenerator — 정보 카드 & 비교 데이터 SO 생성
   6. WaypointDataGenerator — 웨이포인트 경로 구성
   7. FlowUISetupTool.Cleanup — 기존 Flow UI 정리
   8. FlowUISetupTool.Setup — 실험 Flow UI 생성 (ExperimenterCanvas에 배치)
   9. SceneWiringTool — 씬 참조 자동 와이어링
   10. KoreanFontSetup — 한국어 폰트(AppleGothic SDF) 적용
5. **`ARNav > Master Setup > Build & Validate`** — Android 빌드 설정 자동 검증

Full Setup 내부 **순서 의존성**: Step 2(XROrigin) 완료 후 Step 9(SceneWiring) 필수, Step 7(Cleanup) 후 Step 8(FlowUI) 필수. 개별 도구는 `ARNav` 메뉴에서 단독 실행 가능 (스크립트: `Assets/Scripts/Editor/`).

**APK 빌드**: CLI 빌드 스크립트 없음. 에디터 `File > Build Settings > Build` 사용.

**테스트/CI**: 자동화 테스트 및 CI/CD 파이프라인 없음. 검증은 에디터 플레이 모드와 Android 실기기 테스트로 수행.

### 조건부 컴파일

| 플랫폼 | Define 심볼 |
|---------|------------|
| **Android** | `ENABLE_INPUT_SYSTEM;ENABLE_LEGACY_INPUT_MANAGER;XR_HANDS;XR_ARFOUNDATION;XR_INTERACTION` |
| **Standalone** | `ENABLE_INPUT_SYSTEM;ENABLE_LEGACY_INPUT_MANAGER;XR_INTERACTION` |

주요 분기: `#if XR_ARFOUNDATION`, `#if XR_HANDS`, `#if !UNITY_EDITOR`, `#if ENABLE_INPUT_SYSTEM`, `#if XR_INTERACTION`. 에디터에서는 XR 기능이 시뮬레이션 fallback으로 동작. 복합 가드(`#if XR_INTERACTION && !UNITY_EDITOR` 등)도 HandTrackingManager 등에서 사용됨.

### Android 빌드 필수 설정

| 설정 | 필수 값 |
|------|---------|
| Graphics API | OpenGLES3 전용 (Vulkan 금지) |
| Architecture | ARM64 |
| Minimum API Level | 31+ (Android 12) |
| Scripting Backend | IL2CPP |
| Color Space | Linear |
| XR Plugin | XREAL 활성화 |

### XREAL Plugin Settings (`Edit > Project Settings > XR Plug-in Management > XREAL`)

| 설정 | 필수 값 | 비고 |
|------|---------|------|
| Stereo Rendering | Multi-view | Multi-pass보다 성능 우수 |
| Tracking Type | 6DOF | Air2 Ultra 전용 |
| Input Source | ControllerAndHands | 핸드트래킹 + 가상 컨트롤러 동시 |
| Multi Resume | 활성화 | Beam Pro 듀얼 스크린 필수 |
| VSync | Don't Sync | SDK 권장 |
| Write Permission | External (SDCard) | CSV 로그 저장 필수 |

### 에디터 테스트 모드

`ARNav > Master Setup > Editor Test Mode`로 활성화. 키바인딩:

WASD(이동), Shift(달리기), 우클릭 드래그(시점), N(상태전환), M(미션시작), J(웨이포인트 텔레포트), B(BeamPro 토글), R(녹화 토글)

## C# 스크립트 아키텍처

**핵심 패턴**: 싱글턴(`Instance` + `?.Invoke()`), C# event/Action\<T\> 통신, ScriptableObject 데이터(`Assets/Data/`), 상태머신(switch), 조건부 컴파일 플랫폼 분기

### 운영 모드 (AppModeSelector)

1. **매핑 모드**: SpatialAnchorManager로 웨이포인트 앵커 생성/저장 → `anchor_mapping.json`
2. **실험 모드**: 아래 상태머신 참조. GlassOnly 조건 전환 시 핸드트래킹(Hand Ray) 자동 활성화, Hybrid 전환 시 비활성화 (ConditionController가 처리)

### 실험 상태머신 (2중 구조)

**ExperimentManager** (외부, 6상태): `Idle → Relocalization → Setup → Running → Survey → Complete`

단일 조건 실행 방식: 실험자가 메인 화면(AppModeSelector)에서 PID, Route, Condition을 직접 선택 → 해당 조건 1회만 실행.

**MissionManager** (내부, Running 동안 반복): `Idle → Briefing → Navigation → Arrival → Verification → ConfidenceRating → DifficultyRating → Scored`

미션 타입: A_DirectionVerify, B_AmbiguousDecision, C_InfoIntegration (미션 세트당 5개: A1→B1→A2→B2→C1). Set1: T2+T3 트리거, Set2: T1+T4 트리거

### 카운터밸런싱 규칙

- **조건/미션 세트**: 실험자가 메인 화면에서 직접 선택 (Glass Only / Hybrid, Set1 / Set2)
- **카운터밸런싱 4그룹** (24명 = 6명/그룹): G1: Glass+Set1→Hybrid+Set2, G2: Glass+Set2→Hybrid+Set1, G3: Hybrid+Set1→Glass+Set2, G4: Hybrid+Set2→Glass+Set1
- **CounterbalanceConfig.asset**: 참조용으로 유지 (레거시). 실제 선택은 AppModeSelector UI에서 수행

### DDD 아키텍처 (Onion Architecture)

프로젝트는 DDD(Domain-Driven Design) 기반 Onion Architecture로 구성. 의존성 규칙: Domain → Application → Infrastructure/Presentation (바깥→안쪽 참조 금지).

#### 어셈블리 구조 (.asmdef)

| Assembly | 폴더 | 참조 허용 | 설명 |
|----------|------|----------|------|
| **ARNav.Domain** | `Domain/` | 없음 | 순수 C#, `noEngineReferences: true`, 도메인 이벤트/인터페이스/값 객체 |
| **ARNav.Runtime** | `Scripts/` (루트) | Domain + SDK | Application, Core, Mission, Navigation, Presentation, Logging, Utils |
| **ARNav.Debug** | `Debug/` | Domain, Runtime | 에디터 테스트/디버그 도구 |
| **ARNav.Editor** | `Editor/` | 전체 + UnityEditor | 씬 자동 구성, 빌드 검증 |

#### 도메인 이벤트 버스 (DomainEventBus)

`Application/DomainEventBus.cs` — 싱글턴, DontDestroyOnLoad. 모든 크로스 레이어 통신은 도메인 이벤트(`IDomainEvent` readonly struct)로 수행.

```
Publish/Subscribe 패턴:
1. 발행자: DomainEventBus.Instance?.Publish(new SomeEvent(...))
2. 구독자: OnEnable()에서 Subscribe, OnDisable()에서 Unsubscribe
3. ObservationService: 모든 이벤트 → EventLogger.LogEvent() 변환 (CSV 호환성 유지)
```

#### 도메인 이벤트 목록

| 이벤트 파일 | 이벤트 | 발행자 |
|------------|--------|--------|
| **ExperimentEvents** | SessionInitialized(pid,cond,missionSet), ExperimentStateChanged, ConditionChanged, RouteStarted(missionSet), SurveyStarted, ExperimentCompleted | ExperimentManager, ConditionController |
| **MissionEvents** | MissionStarted/Arrived/Completed, VerificationAnswered, ConfidenceRated, DifficultyRated, BriefingForced, ArrivalForced, MissionForceSkipped, AllMissionsCompleted | MissionManager |
| **NavigationEvents** | WaypointReached, TriggerActivated/Deactivated, ArrowShown/Hidden/Offset, WaypointFallbackUsed, WaypointLateAnchorBound | WaypointManager, TriggerController, ARArrowRenderer |
| **SpatialEvents** | RelocalizationStarted/Progress/Completed, AnchorLateRecovered, AnchorSaved, AnchorDiagnostics | SpatialAnchorManager |
| **ObservationEvents** | DeviceScreenChanged, BeamTabSwitched, BeamInfoCardToggled, BeamPOI/Comparison/MissionRef/MapZoomed, GlassCaptureStateChanged | BeamPro UI, DeviceStateTracker |

#### Application 레이어 서비스

| 서비스 | 역할 |
|--------|------|
| **DomainEventBus** | 중앙 이벤트 버스 (Publish/Subscribe) |
| **ObservationService** | 도메인 이벤트 → EventLogger 변환 |
| **NavigationService** | TriggerController 래퍼 (트리거 활성화/비활성화) |
| **BeamProCoordinator** | MissionStarted 구독 → BeamPro 허브 데이터 로드 |
| **ExperimentAdvancer** | AllMissionsCompleted 구독 → ExperimentManager.AdvanceState() |

### 레이어 구조

| 레이어 | 주요 역할 | 핵심 클래스 |
|--------|----------|------------|
| **Domain** | 이벤트, 인터페이스, 값 객체 | IDomainEvent, MissionResult, 도메인 이벤트 structs |
| **Application** (5) | 이벤트 버스, 오케스트레이터 | DomainEventBus, ObservationService, NavigationService, BeamProCoordinator, ExperimentAdvancer |
| **Core** (7) | 상태머신, 조건 전환, 세션, 앵커, 핸드트래킹, 다국어 | ExperimentManager, ConditionController, SpatialAnchorManager, HandTrackingManager |
| **Navigation** (3) | 경로, AR 화살표, 불확실성 트리거 | WaypointManager, ARArrowRenderer, TriggerController |
| **Mission** (5) | 미션 FSM, SO 데이터 | MissionManager, MissionData, POIData, InfoCardData |
| **Presentation/Glass** (7) | 글래스 전용 UI | GlassCanvasController, ExperimentHUD, MissionBriefingUI, VerificationUI, ConfidenceRatingUI, DifficultyRatingUI |
| **Presentation/BeamPro** (9) | 3탭 정보 허브 | BeamProHubController, InteractiveMapController, InfoCardManager, BeamProCanvasController |
| **Presentation/Experimenter** (3) | 실험자 UI | ExperimenterHUD, ExperimentFlowUI, RelocalizationUI |
| **Presentation/Mapping** (4) | 매핑 모드 UI | MappingModeUI, MappingGlassOverlay, MappingMiniMap, MappingAnchorVisualizer |
| **Presentation/Shared** (4) | 공통 UI | AppModeSelector, FloorPlanMapBase, PanelFader, BeamProUIAdapter |
| **Logging** (3) | CSV 이벤트 로거 | EventLogger, DeviceStateTracker, HeadTracker |
| **Debug** (4) | 에디터 이동, 글래스 캡처 | EditorPlayerController, GlassViewCapture, HandJointVisualizer |
| **Utils** (2) | CSV 유틸, 카운터밸런싱 | CSVWriter, CounterbalanceConfig |
| **Editor** (13) | 씬 자동 구성, 빌드 검증 | MasterSetupTool, SceneWiringTool, SceneSetupTool |

### 싱글턴 (13개)

DontDestroyOnLoad 적용: **ExperimentManager**, **EventLogger** (세션 수명). 나머지(ConditionController, SpatialAnchorManager, HandTrackingManager, WaypointManager, TriggerController, MissionManager, BeamProHubController, DeviceStateTracker, HeadTracker, GlassViewCapture, **LocalizationManager**)는 씬 종속.

초기화 순서: Awake(ExperimentManager, EventLogger) → Start(SpatialAnchorManager.LoadMapping, ConditionController.BeamPro탭숨김) → 지연 초기화(HandTrackingManager: 1초 대기 + 3회 재시도)

### 3-Canvas 아키텍처

| Canvas | Render Mode | sortOrder | 역할 |
|--------|------------|-----------|------|
| **ExperimentCanvas** | WorldSpace (head-locked) | 0 | 글래스 표시 전용 (GlassCanvasController) |
| **BeamProCanvas** | ScreenSpaceOverlay ↔ WorldSpace | 1 | 3탭 정보 허브 (GlassOnly 시 WorldSpace로 전환/잠금) |
| **ExperimenterCanvas** | ScreenSpaceOverlay | 10 | 실험자 HUD + 플로우 제어 (ExperimentFlowUI) |

### 앵커 데이터 흐름

**매핑**: MappingModeUI → SpatialAnchorManager.CreateAndSaveAnchor() → 품질 관찰(최대 8초, GOOD 즉시 저장) → `anchor_mapping.json` 저장

**재인식**: SpatialAnchorManager.LoadAllAnchors() → GUID별 0.5초 폴링(30초 타임아웃) → OnRelocalizationDetailedProgress → 완료 시 OnRelocalizationCompleteWithRate

**백그라운드 복구**: 실패 앵커 → StartBackgroundReanchoring() → 5초 루프 + TryRemap(15초마다) → OnAnchorLateRecovered → WaypointManager가 fallback→anchor 전환

**Fallback 패턴**: `Waypoint.Position` = `anchorTransform ?? fallbackPosition` — 재인식 실패 시에도 미터 수준 정확도로 내비게이션 지속

**Heading 자동 보정**: 앵커 2+개 인식 시 `AutoCalibrateFromAnchors()` — 가장 먼 앵커 쌍의 SLAM/도면 yaw 차이로 좌표계 회전 오프셋 자동 계산. `BindAnchorTransforms()` + `OnAnchorLateRecovered()` 에서 트리거. Route B 시작점에 WP00 보정앵커 배치 (9개 WP). ExperimenterHUD ±5° 미세 조정 버튼 유지

## 다국어(Localization) 패턴

- **LocalizationManager**: 싱글턴, `Language.EN`/`KO` enum, `PlayerPrefs("ARNav_Language")` 영속, `OnLanguageChanged` (Action\<Language\>) 이벤트. `Get(string key)` 정적 메서드로 현재 언어에 맞는 문자열 반환
- **LocalizationTable**: 정적 `Dictionary<string, (string en, string ko)>`, 키 패턴 `"category.item"` (예: `"appmode.title"`, `"reloc.scanning"`), 누락 키 → `[key]` 반환
- **SO 다국어 패턴**: 영문 필드 + `_Ko` 접미사 필드 쌍 → `Get*()` 헬퍼가 `LocalizationManager.Instance.CurrentLanguage` 체크하여 적절한 언어 반환

## 코드 수정 시 공동 변경 규칙

아래 항목을 변경할 때는 **반드시** 연관 파일을 함께 수정:

| 변경 대상 | 연쇄 수정 필요 파일 |
|-----------|-------------------|
| **GameObject 이름** | SceneWiringTool 재실행 필수 (이름 기반 리플렉션 와이어링, 경고 없이 실패) |
| **웨이포인트 ID/fallbackPosition** | WaypointDataGenerator, MappingModeUI, MappingMiniMap, DebugToolsSetup, EditorPlayerController |
| **이벤트 시그니처** 변경 | "이벤트 통신 흐름" 섹션의 구독자 전체 확인 |
| **ExperimentState enum** 추가 | ExperimentManager (6상태: Idle/Relocalization/Setup/Running/Survey/Complete), ExperimentFlowUI, ExperimentHUD, ExperimenterHUD |
| **MissionState enum** 추가 | MissionManager + 해당 상태의 UI 패널 |
| **ScriptableObject 필드** 추가 | 해당 SO의 Editor Generator + Inspector 사용처 |
| **BeamPro 탭** 추가/제거 | BeamProHubController.tabPanels/tabButtons 배열 + SceneWiringTool 와이어링 순서 |
| **Define Symbol** 변경 | ProjectSettings + 해당 `#if` 가드 사용 스크립트 전체 |
| **UI 텍스트** 추가/변경 | `LocalizationTable.cs`에 키 등록 필수 + 해당 UI 스크립트에서 `LocalizationManager.Get("key")` 사용 |
| **SO 텍스트 필드** 추가 | 해당 SO 클래스에 `_Ko` 필드 + `Get*()` 헬퍼 추가 + Editor Generator에서 한국어 데이터 설정 |
| **도메인 이벤트** 추가 | `Domain/Events/*.cs`에 struct 추가 + `ObservationService`에 Subscribe/Handler 추가 (CSV 로그 호환) |
| **EventLogger.LogEvent()** 직접 호출 금지 | `DomainEventBus.Publish()` 사용 → ObservationService가 EventLogger 위임 |
| **Application 서비스** 추가 | SceneSetupTool에 GameObject 생성 추가 + 필요 시 SceneWiringTool에 와이어링 추가 |
| **Presentation 클래스 이동** | 네임스페이스 변경 + SceneSetupTool/SceneWiringTool의 타입 참조 업데이트 |

## 알려진 주의사항

### CRITICAL (빌드/입력 불능)

- **XR_INTERACTION define**: Editor 스크립트(Assembly-CSharp-Editor)는 XREAL SDK asmdef의 versionDefines를 받지 못함. ProjectSettings의 Scripting Define Symbols에 수동 추가 필수. **누락 시 EventSystem에 입력 모듈 0개 → 터치 완전 불능**
- **InputActionManager 필수**: XRI Starter Assets 미설치 시 InputAction이 자동 Enable 안 됨 → 핸드트래킹/터치 불능. "필수 초기 셋업 순서" 참조
- **XRUIInputModule.enableBuiltinActionsAsFallback**: true 필수. 미설정 시 핸드트래킹 초기화 타이밍에 따라 터치가 간헐적으로만 작동
- **ENABLE_INPUT_SYSTEM define**: 수동 추가됨. InputSystem 패키지 재설치 시 덮어씌워질 수 있음

### HIGH (기능 장애)

- **SceneWiringTool 리플렉션**: GameObject 이름 기반 `SerializedObject` 참조 자동 와이어링. **오브젝트 이름 변경 시 경고 없이 와이어링 실패** → `ARNav > Wire Scene References` 재실행
- **XREAL SDK asmdef**: Unity.XR.Hands 참조 필수, URP 참조 제거. SDK 업그레이드 시 `LocalPackages/com.xreal.xr/Marker~/nr_plugins.json` 확인
- **ConditionController**: Start()에서 BeamPro 탭 초기 숨김 필수. 누락 시 GlassOnly에서 Beam Pro 탭 노출
- **BeamProCanvasController 캐싱**: GlassOnly↔Hybrid 전환 시 원래 ScreenSpace 설정을 캐시/복원. 캐시 이전에 설정을 변경하면 복원 시 드리프트 발생

### 일반 참고

- **TrackedPoseDriver**: `positionInput`/`rotationInput` 사용 (`positionAction` 아님)
- **TMP 한국어**: AppleGothic SDF 필수. Full Setup이 자동 적용
- **TMP_Dropdown**: Template에 CanvasGroup 컴포넌트 필수 (없으면 NullRef)
- **NotificationListener**: XREAL SDK 프리팹(`Packages/com.xreal.xr/Runtime/Prefabs/`). Full Setup이 자동 배치. 배터리/온도/SLAM/네이티브 에러 경고 처리
- **매핑 품질 관찰**: `CreateAndSaveAnchor` 시 앵커별 `GetAnchorQuality(trackableId)` + 최대 8초 관찰. GOOD 도달 시 즉시 저장

## 이벤트 통신 흐름

### 도메인 이벤트 버스 (DomainEventBus — 주요 통신 채널)

모든 로깅/크로스 레이어 통신은 `DomainEventBus.Instance?.Publish()` 사용. `ObservationService`가 구독하여 `EventLogger`에 위임.

**실험 이벤트:** SessionInitialized, ExperimentStateChanged, ConditionChanged, RouteStarted, SurveyStarted, ExperimentCompleted
**미션 이벤트:** MissionStarted → BeamProCoordinator 구독 (BeamPro 데이터 로드). AllMissionsCompleted → ExperimentAdvancer 구독 (실험 전진)
**내비게이션 이벤트:** WaypointReached, TriggerActivated/Deactivated, ArrowShown/Hidden
**공간 이벤트:** RelocalizationStarted/Progress/Completed, AnchorLateRecovered, AnchorDiagnostics
**관찰 이벤트:** DeviceScreenChanged, BeamTabSwitched, BeamInfoCardToggled 등

### C# event/Action 통신 (기존 유지)

**상태 전환:**
- `ExperimentManager.OnStateChanged` → ExperimentFlowUI, ExperimentHUD, ExperimenterHUD
- `ConditionController.OnConditionChanged` → BeamProHubController, BeamProCanvasController, HandTrackingManager

**미션/내비게이션:**
- `WaypointManager.OnWaypointReached` → MissionManager
- `MissionManager.OnMissionStarted/OnMissionCompleted` — Action\<MissionData\>

**앵커/매핑:**
- `SpatialAnchorManager.OnRelocalizationDetailedProgress/CompleteWithRate` → RelocalizationUI
- `SpatialAnchorManager.OnAnchorLateRecovered` → WaypointManager
- `SpatialAnchorManager.OnAnchorSaved` → MappingAnchorVisualizer
- `SpatialAnchorManager.OnMappingQualityUpdate` → MappingGlassOverlay

**입력/디바이스/다국어/디버그:**
- `HandTrackingManager.OnHandTrackingStateChanged`, `DeviceStateTracker.OnBeamProScreenOn/Off`
- `LocalizationManager.OnLanguageChanged` → UI 전체
- `GlassViewCapture.OnRecordingStateChanged` → ExperimenterHUD

## 도구 및 분석 스크립트

### 디바이스 도구

```bash
# 글래스 캡처 파일을 디바이스에서 로컬로 전송 (adb 필요)
./tools/pull_glass_captures.sh              # 기본 디바이스 → debug_captures/
./tools/pull_glass_captures.sh 192.168.0.5  # 무선 adb → debug_captures/

# 앵커 진단 로그 + 이벤트 CSV를 디바이스에서 로컬로 전송
./tools/pull_diagnostics.sh                 # → data/diagnostics/ + data/raw/

# 도면 이미지에서 미니맵 배경 추출 (OpenCV 필요: pip3 install opencv-python numpy)
python3 tools/extract_floorplan.py
```

### 분석 스크립트

```bash
pip3 install numpy pandas scipy matplotlib pingouin  # 의존성
python3 analysis/analyze_device_switching.py
python3 analysis/analyze_trust_performance.py
python3 analysis/analyze_verification.py
python3 analysis/analyze_triggers.py
```

`data/raw/` 비어 있으면 데모 데이터 자동 생성. 결과: `analysis/output/`.

> **한국어 폰트**: 분석 스크립트가 `matplotlib.rcParams["font.family"] = "AppleGothic"`을 사용. macOS 외 환경에서는 `NanumGothic` 등 한국어 폰트로 변경 필요.

## 데이터

### 글래스 뷰 캡처 (GlassViewCapture)

앱 시작 시 자동 녹화 → 앱 종료 시 자동 저장. 디버그/검증용 글래스 시점 영상.

- **디바이스 경로**: `/storage/emulated/0/Android/data/com.KIT_HCI.ARNavExperiment/files/glass_captures/`
- **파일명 패턴**: `glass_{participantId}_{yyyyMMdd_HHmmss}_seg{nn}.mp4`
- **로컬 전송**: `./tools/pull_glass_captures.sh` → `debug_captures/`
- **Claude에게 요청 시**: `adb pull` 후 `data/glass_captures/`에 저장, `open`으로 재생. DCIM 녹화가 아님에 주의.

### 이벤트 로그 CSV

파일명: `{pid}_{condition}_{missionSet}_{timestamp}.csv` (15개 컬럼: timestamp, participant_id, condition, event_type, waypoint_id, head_rotation_x/y/z, device_active, confidence_rating, mission_id, difficulty_rating, verification_correct, beam_content_type, extra_data)

- **디바이스**: `/storage/emulated/0/Android/data/com.KIT_HCI.ARNavExperiment/files/data/raw/`
- **로컬 분석**: `data/raw/`
- **설문 데이터**: `data/surveys/` (pre_survey.csv, nasa_tlx.csv, trust_scale.csv, post_survey.csv) — 실험 후 수동 생성 필요

### ScriptableObject 데이터 에셋 (`Assets/Data/`)

| 디렉토리 | 내용 | 생성 도구 |
|----------|------|----------|
| `Missions/` | Set1/Set2 × 5미션 (A1,B1,A2,B2,C1) | MissionDataGenerator |
| `POIs/RouteB/` | 장소 정보 (room_bXXX, stairs_main 등) | MissionDataGenerator |
| `InfoCards/Set1/`, `InfoCards/Set2/` | 정보 카드 5개씩 | InfoCardDataGenerator |
| `Routes/` | 경로 데이터 | WaypointDataGenerator |
| `FloorPlan/` | KIT B1F 도면 (SVG/PNG) | 수동 |
| `CounterbalanceConfig.asset` | 카운터밸런싱 설정 | ExperimentConfigTool |

### 앵커 매핑 (`anchor_mapping.json`)

`Application.persistentDataPath/anchor_mapping.json`에 저장. 구조: `{ createdAt, routeA: { waypoints: [] }, routeB: { waypoints: [{ waypointId, anchorGuid, radius, locationName }] } }`. Route B만 사용 (routeA는 하위 호환성을 위해 빈 상태로 유지). 매핑 모드에서 생성, 재인식 시 로드.

## 주요 문서

- `docs/실험_설계_v2.md` — 실험 설계
- `docs/구현_로드맵.md` — 구현 로드맵
- `docs/RQ_정제.md` — 연구 질문/가설
- `docs/실험_프로토콜.md` — 실험 프로토콜
- `docs/데이터_포맷_명세.md` — 이벤트 로그 스키마
- `docs/설문지/` — 설문 도구
- `docs/웨이포인트_매핑_가이드.md` — 현장 매핑 절차, 웨이포인트 ID/좌표/트리거 위치
- `docs/연구실_발표_자료.md` — 연구실 발표용 요약 자료

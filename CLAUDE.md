# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 커뮤니케이션

- 사용자와 대화할 때 항상 한국어를 사용할 것.

## 프로젝트 개요

스마트 글래스(XReal Air2 Ultra)와 스마트폰(XReal Beam Pro)을 활용한 하이브리드 실내 내비게이션 HCI 연구 프로젝트. **2가지 조건(Glass Only / Hybrid)**을 피험자 내 설계(within-subjects design)로 24명 대상 비교 실험. 미션 기반 길찾기 태스크(방향 안내+검증, 모호한 의사결정, 정보 통합)와 4종 불확실성 트리거를 통해 교차검증 행동을 유도. 문서는 주로 한국어로 작성되어 있음.

## 저장소 셋업

Git LFS 필수 (XREAL SDK `.aar` 파일 추적):
```bash
git lfs install
git lfs pull
```

## Unity 프로젝트 (`ARNavExperiment/`)

- **Unity 버전**: 2022.3.62f2 (LTS)
- **빌드 대상**: Android (XReal Beam Pro)
- **단일 씬**: `Assets/Scenes/MainExperiment.unity`
- **주요 패키지**: XREAL SDK (로컬, `LocalPackages/com.xreal.xr/`), AR Foundation 5.1.5, XR Hands 1.4.1, XR Interaction Toolkit 2.5.4, TextMesh Pro
- **네임스페이스**: `ARNavExperiment.*` — 폴더 구조와 일치 (예: `ARNavExperiment.Core`, `ARNavExperiment.Mission`, `ARNavExperiment.Navigation`, `ARNavExperiment.BeamPro`, `ARNavExperiment.Logging`, `ARNavExperiment.Utils`, `ARNavExperiment.DebugTools`, `ARNavExperiment.EditorTools`)

### 조건부 컴파일

스크립트 define 심볼 (`ProjectSettings/ProjectSettings.asset`에서 설정):

| 플랫폼 | Define 심볼 |
|---------|------------|
| **Android** | `ENABLE_INPUT_SYSTEM;ENABLE_LEGACY_INPUT_MANAGER;XR_HANDS` |
| **Standalone** | `ENABLE_INPUT_SYSTEM;ENABLE_LEGACY_INPUT_MANAGER` |

코드에서 사용하는 주요 분기:
- `#if XR_ARFOUNDATION` — AR Foundation 기능 (자동 정의됨, 패키지 설치 시)
- `#if XR_HANDS` — 핸드트래킹 (Android 빌드에서만 활성)
- `#if !UNITY_EDITOR` — 에디터 제외, 디바이스 전용 코드 (예: WorldSpace 캔버스 전환, TrackedPoseDriver)
- `#if ENABLE_INPUT_SYSTEM` — Input System 활성화

에디터에서는 XR 기능이 시뮬레이션 fallback으로 동작하고, 디바이스 빌드에서만 실제 XR 기능이 활성화됨.

### 에디터 셋업 및 빌드

Unity 에디터 메뉴 `ARNav > Master Setup`:
- **Full Setup** — 씬 오브젝트, 참조 와이어링, XR Origin, UI 패널, 미션 데이터를 10단계로 일괄 생성/구성 (`MasterSetupTool.cs`)
- **Build & Validate** — Android 빌드 설정, 씬 포함, 참조 누락 등을 자동 검증
- **Editor Test Mode** — `EditorPlayerController`를 씬에 추가

### 에디터 테스트 모드 키바인딩

- **WASD**: 이동, **마우스 우클릭 드래그**: 회전
- **N**: 실험 상태 전환 (Advance State)
- **M**: 다음 미션 시작
- **J**: 다음 웨이포인트로 텔레포트
- **B**: BeamPro 화면 토글

## C# 스크립트 아키텍처

**패턴**: 핵심 매니저 클래스들은 **싱글턴**(`Instance`)으로 구현. 컴포넌트 간 통신은 **C# event/Action<T>**를 사용하여 느슨한 결합. 데이터는 **ScriptableObject** (`Assets/Data/`)로 정의.

### 두 가지 운영 모드

`AppModeSelector`에서 앱 시작 시 선택:
1. **매핑 모드**: `MappingModeUI` + `SpatialAnchorManager`로 웨이포인트 위치에 Spatial Anchor 생성/저장. `MappingGlassOverlay`가 글래스 HUD, `MappingAnchorVisualizer`가 3D 마커 표시.
2. **실험 모드**: 아래 실험 흐름 참조.

### 실험 흐름 (상태머신 2중 구조)

**ExperimentManager** (전체 실험 상태):
```
Idle → Relocalization → Setup → Practice → Condition1 → Survey1 → Condition2 → Survey2 → PostSurvey → Complete
```
`TransitionTo()` / `AdvanceState()`로 전환. `ExperimentFlowUI`가 `OnStateChanged` 이벤트를 구독하여 단계별 패널 전환.

**MissionManager** (미션별 상태):
```
Idle → Briefing → Navigation → Arrival → Verification → ConfidenceRating → DifficultyRating → Scored → (다음 미션 or 상태 전환)
```
`WaypointManager.OnWaypointReached` 이벤트로 도착 감지. 트리거 활성화/해제, BeamPro 데이터 로드를 미션 상태에 맞춰 제어.

### 레이어별 스크립트 역할

| 레이어 | 핵심 스크립트 | 역할 |
|--------|-------------|------|
| **Core** | `ExperimentManager`, `ConditionController`, `ParticipantSession`, `SpatialAnchorManager` | 실험 상태머신, 조건(GlassOnly/Hybrid) 전환, 세션 관리, 앵커 관리 |
| **Navigation** | `WaypointManager`, `ARArrowRenderer`, `TriggerController` | 경로/웨이포인트 거리 체크, 글래스 AR 화살표 (view-locked), 4종 불확실성 트리거(T1~T4) |
| **Mission** | `MissionManager`, `MissionData`, `VerificationUI` | 미션 상태머신, ScriptableObject 미션 정의 (A/B/C 타입), 검증 질문 UI |
| **UI** | `ExperimentFlowUI`, `ExperimentHUD`, `ExperimenterHUD`, `GlassCanvasController` | 실험 단계 패널, 글래스 참가자 HUD, BeamPro 실험자 제어판, WorldSpace 캔버스 |
| **BeamPro** | `BeamProHubController`, `InteractiveMapController`, `InfoCardManager` | 3탭(지도/정보카드/미션참조) 정보 허브, 조건별 잠금/해제 |
| **Logging** | `EventLogger`, `DeviceStateTracker`, `HeadTracker` | 중앙 CSV 로거, BeamPro 화면 on/off 감지, 머리 회전 10Hz 추적 |
| **Editor** | `MasterSetupTool`, `SceneSetupTool`, `SceneWiringTool`, `FlowUISetupTool` | 씬 자동 구성, 참조 와이어링, UI 패널 생성, 빌드 검증 |

### 3-Canvas 아키텍처

- **ExperimentCanvas** (글래스용, sortOrder=0): `GlassCanvasController`가 디바이스에서 WorldSpace로 전환하여 head-locked 렌더링. `TrackedDeviceGraphicRaycaster`로 핸드트래킹 인터랙션. 참가자에게 표시 전용 (버튼 없음).
- **BeamProCanvas** (Beam Pro 콘텐츠, sortOrder=1): `BeamProHubController`가 관리. 3탭 정보 허브(지도/정보카드/미션참조). `ConditionController`가 GlassOnly 조건 시 잠금 화면 표시.
- **ExperimenterCanvas** (Beam Pro 실험자 제어, sortOrder=10): `ExperimenterHUD`가 관리. 실험 상태 표시 + Advance/NextMission 버튼. `ExperimentFlowUI`가 단계별 패널 전환.

### 데이터 저장 경로 (디바이스)

- **앵커 매핑**: `Application.persistentDataPath/anchor_mapping.json` — SpatialAnchorManager가 생성/관리
- **앵커 바이너리**: `Application.persistentDataPath/AnchorMaps/` — XR 앵커 저장소
- **이벤트 로그**: `Application.persistentDataPath/data/raw/` — EventLogger CSV 출력

### Waypoint 구조

`WaypointManager`의 `Waypoint`에는 `fallbackPosition`(Vector3)과 `anchorTransform`(Transform) 두 위치 정보가 있음. `Position` 프로퍼티가 anchorTransform 우선, fallback 보조로 해석. 에디터에서는 항상 fallbackPosition 사용.

### Relocalization 실패 처리

`SpatialAnchorManager`가 앵커 재인식 결과를 `AnchorRelocState` (Pending/Tracking/TimedOut/LoadFailed)로 분류하여 정확하게 추적.

**카운터/프로퍼티**: `SuccessfulAnchorCount`, `TimedOutAnchorCount`, `FailedAnchorCount`, `RelocalizationSuccessRate` (0~1), `FallbackWaypoints` (실패 wpId 리스트)

**이벤트 흐름**:
1. `OnRelocalizationDetailedProgress(wpId, state, success, timedOut, total)` — 개별 앵커 처리 시마다
2. `OnRelocalizationCompleteWithRate(float)` — 전체 완료 시
3. `OnAnchorLateRecovered(wpId, Transform)` — 백그라운드 재인식 성공 시

**RelocalizationUI 분기**:
- 100% 성공 → 1.5초 후 자동 `AdvanceState()`
- <100% → `resultPanel` 표시 (경고 + 실패 WP 목록), 실험자가 "계속 진행" 또는 "재시도" 선택

**복구 메커니즘**:
- `RetryFailedAnchors()` — 실패/타임아웃 앵커만 카운터 보정 후 재로드
- `StartBackgroundReanchoring()` — 5초 간격으로 타임아웃 앵커의 Tracking 전환 폴링, 성공 시 `OnAnchorLateRecovered` 발행
- `WaypointManager.OnAnchorLateRecovered()` — 활성 경로의 `anchorTransform` 즉시 교체 (런타임 핫스왑)

**ExperimenterHUD**: `anchorStatusText`가 실시간으로 `"⚠ Fallback: {n} WP"` 또는 `"Anchors: OK"` 표시

**관련 이벤트 로그**: `RELOCALIZATION_COMPLETE`, `RELOCALIZATION_RETRY`, `RELOCALIZATION_PROCEED_PARTIAL`, `WAYPOINT_FALLBACK_USED`, `WAYPOINT_LATE_ANCHOR_BOUND`

## 분석 스크립트 실행

```bash
python3 analysis/analyze_device_switching.py
python3 analysis/analyze_trust_performance.py
python3 analysis/analyze_verification.py
python3 analysis/analyze_triggers.py
```

4개 스크립트 모두 `data/raw/`가 비어 있으면 데모 데이터를 자동 생성함. 결과물(CSV 요약 + PNG 그래프)은 `analysis/output/`에 저장됨.

**Python 의존성** (requirements.txt 없음): numpy, pandas, scipy, matplotlib, pingouin (선택)

```bash
pip3 install numpy pandas scipy matplotlib pingouin
```

- 통계: 2조건 비교 → paired t-test, 비모수 시 Wilcoxon signed-rank (scipy)
- 시각화: matplotlib (한국어 폰트 AppleGothic 사용)

## 데이터

이벤트 로그 CSV는 `data/raw/`에 저장. 파일명: `P{id}_{condition}_{route}_{timestamp}.csv`. 15개 컬럼: `timestamp, participant_id, condition, event_type, waypoint_id, head_rotation_x/y/z, device_active, confidence_rating, mission_id, difficulty_rating, verification_correct, beam_content_type, extra_data`.

## 알려진 주의사항

- **XREAL SDK asmdef**: `Unity.XR.Hands` 참조가 포함되어야 핸드트래킹 동작. URP 참조는 제거해야 함.
- **InputSystem define**: `ENABLE_INPUT_SYSTEM`이 ProjectSettings에 수동 추가되어 있음. 패키지 관리자에서 InputSystem 재설치 시 덮어씌워질 수 있음.
- **TrackedPoseDriver**: `positionInput`/`rotationInput` 사용 (`positionAction`이 아님).
- **TMP 한국어**: AppleGothic SDF 폰트 필수. `ARNav > Master Setup > Full Setup`이 자동 적용.
- **TMP_Dropdown**: Template 오브젝트에 `CanvasGroup` 컴포넌트 필수 (없으면 NullRef).
- **Waypoint 리네임**: `fallbackPosition` 필드명 변경 시 `WaypointGizmoDrawer`, `WaypointDataGenerator`, `EditorPlayerController` 모두 함께 업데이트 필요.
- **ConditionController 초기화**: `Start()`에서 BeamPro 탭을 초기 숨김 처리해야 GlassOnly 조건에서 노출 방지.

## 주요 문서

- `docs/실험_설계_v2.md` — 재설계된 실험 설계 (미션 기반 길찾기, 2조건, 4종 트리거)
- `docs/구현_로드맵.md` — Unity 앱 아키텍처 및 전체 시스템 구현 로드맵
- `docs/RQ_정제.md` — 연구 질문, 가설, 이론적 프레임워크
- `docs/실험_프로토콜.md` — 전체 실험 프로토콜 및 참가자 스크립트
- `docs/데이터_포맷_명세.md` — 이벤트 로그 CSV 스키마 및 이벤트 타입 정의
- `docs/설문지/` — 설문 도구 (사전 설문, NASA-TLX 한국어판, 신뢰 척도, 확신도 척도)
- `핵심논문_비교표_및_Gap분석.md` — 핵심 논문 비교표 및 연구 갭 분석

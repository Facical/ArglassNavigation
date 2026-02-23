# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 커뮤니케이션

- 사용자와 대화할 때 항상 한국어를 사용할 것.

## 프로젝트 개요

스마트 글래스(XReal Air2 Ultra)와 스마트폰(XReal Beam Pro)을 활용한 하이브리드 실내 내비게이션 HCI 연구 프로젝트. **2가지 조건(Glass Only / Hybrid)**을 피험자 내 설계(within-subjects design)로 24명 대상 비교 실험. 미션 기반 길찾기 태스크(방향 안내+검증, 모호한 의사결정, 정보 통합)와 4종 불확실성 트리거를 통해 교차검증 행동을 유도. 문서는 주로 한국어로 작성되어 있음.

## Unity 프로젝트 (`ARNavExperiment/`)

- **Unity 버전**: 2022.3.62f2 (LTS)
- **빌드 대상**: Android (XReal Beam Pro)
- **단일 씬**: `Assets/Scenes/MainExperiment.unity`
- **주요 패키지**: XREAL SDK (로컬, `LocalPackages/com.xreal.xr/`), AR Foundation 5.1.5, XR Hands 1.4.1, XR Interaction Toolkit 2.5.4, TextMesh Pro
- **조건부 컴파일**: `#if XR_ARFOUNDATION`, `#if XR_HANDS` — 에디터에서는 시뮬레이션 fallback, 디바이스에서만 실제 XR 기능 활성화. `#if !UNITY_EDITOR`로 에디터/빌드 분기.

### 에디터 셋업 및 빌드

Unity 에디터 메뉴 `ARNav > Master Setup > Full Setup`으로 씬 오브젝트, 참조 와이어링, XR Origin, UI 패널, 미션 데이터를 일괄 생성/구성. `MasterSetupTool.cs`가 10단계를 순차 실행.

빌드 검증: `ARNav > Master Setup > Build & Validate` — Android 빌드 설정, 씬 포함, 참조 누락 등을 자동 검증.

### 에디터 테스트 모드

`ARNav > Master Setup > Editor Test Mode`로 활성화. `EditorPlayerController`가 씬에 추가됨.
- **WASD**: 이동, **마우스 우클릭 드래그**: 회전
- **N**: 실험 상태 전환 (Advance State)
- **M**: 다음 미션 시작
- **J**: 다음 웨이포인트로 텔레포트
- **B**: BeamPro 화면 토글

## C# 스크립트 아키텍처

**패턴**: 핵심 매니저 클래스들은 **싱글턴**(`Instance`)으로 구현. 컴포넌트 간 통신은 **C# event/Action<T>**를 사용하여 느슨한 결합. 데이터는 **ScriptableObject** (`Assets/Data/`)로 정의. 네임스페이스: `ARNavExperiment.*`.

### 두 가지 운영 모드

`AppModeSelector`에서 앱 시작 시 선택:
1. **매핑 모드**: `MappingModeUI` + `SpatialAnchorManager`로 웨이포인트 위치에 Spatial Anchor 생성/저장 (JSON). `MappingGlassOverlay`가 글래스 HUD, `MappingAnchorVisualizer`가 3D 마커 표시.
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

### 듀얼 캔버스 구조

- **ExperimentCanvas** (글래스용): `GlassCanvasController`가 디바이스에서 WorldSpace로 전환하여 head-locked 렌더링. `TrackedDeviceGraphicRaycaster`로 핸드트래킹 인터랙션. 참가자에게 표시 전용 (버튼 없음).
- **ExperimenterCanvas** (Beam Pro용): `BeamProHubController`가 관리. 실험자 조작 버튼 + 정보 허브(지도/정보카드/미션참조). `ConditionController`가 GlassOnly 조건 시 잠금 화면 표시.

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

## 주요 문서

- `docs/실험_설계_v2.md` — 재설계된 실험 설계 (미션 기반 길찾기, 2조건, 4종 트리거)
- `docs/구현_로드맵.md` — Unity 앱 아키텍처 및 전체 시스템 구현 로드맵
- `docs/RQ_정제.md` — 연구 질문, 가설, 이론적 프레임워크
- `docs/실험_프로토콜.md` — 전체 실험 프로토콜 및 참가자 스크립트
- `docs/데이터_포맷_명세.md` — 이벤트 로그 CSV 스키마 및 이벤트 타입 정의
- `docs/설문지/` — 설문 도구 (사전 설문, NASA-TLX 한국어판, 신뢰 척도, 확신도 척도)
- `핵심논문_비교표_및_Gap분석.md` — 핵심 논문 비교표 및 연구 갭 분석

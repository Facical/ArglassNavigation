# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 커뮤니케이션

- 사용자와 대화할 때 항상 한국어를 사용할 것.

## 프로젝트 개요

스마트 글래스(XReal Air2 Ultra)와 스마트폰(XReal Beam Pro)을 활용한 하이브리드 실내 내비게이션 HCI 연구 프로젝트. **2가지 조건(Glass Only / Hybrid)**을 피험자 내 설계(within-subjects design)로 24명 대상 비교 실험. 미션 기반 길찾기 태스크(방향 안내+검증, 모호한 의사결정, 정보 통합)와 4종 불확실성 트리거를 통해 교차검증 행동을 유도. 문서는 주로 한국어로 작성되어 있음.

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

## 아키텍처

**3개 레이어 구조:**

1. **실험 프로토콜** (`docs/`): 2조건 × 2경로, AB/BA 카운터밸런싱 (2그룹 × 12명), 참가자당 55분, 단일 층(지하 1층). 미션 타입 3종(A: 방향+검증, B: 모호한 의사결정, C: 정보 통합), 불확실성 트리거 4종(T1: 안내 열화, T2: 정보 불일치, T3: 해상도 부족, T4: 안내 부재).

2. **데이터 수집** (`data/`): 이벤트 로그 CSV. 스키마: `timestamp, participant_id, condition, event_type, waypoint_id, head_rotation_x/y/z, device_active, confidence_rating, mission_id, trigger_type, extra_data`. 파일 명명 규칙: `P{id}_{condition}_{route}_{timestamp}.csv`.

3. **분석** (`analysis/`): Python 스크립트 4개:
   - `analyze_device_switching.py` — 기기 전환 빈도, 지속시간, 일시정지/완료시간, 교차검증 지수(CVI)
   - `analyze_trust_performance.py` — NASA-TLX 작업부하, 시스템 신뢰 척도, 확신도 궤적, 확신도 보정(calibration)
   - `analyze_verification.py` — 미션 정확도, 검증 행동 분류, 미션 타입별 소요시간
   - `analyze_triggers.py` — 트리거별 반응 시간, 오방향 선택률, 확신도 하락, 기기 전환률
   - 통계: 2조건 비교 → paired t-test, 비모수 시 Wilcoxon signed-rank (scipy)
   - 시각화: matplotlib (한국어 폰트 AppleGothic 사용)

## 주요 문서

- `docs/실험_설계_v2.md` — 재설계된 실험 설계 (미션 기반 길찾기, 2조건, 4종 트리거)
- `docs/구현_로드맵.md` — Unity 앱 아키텍처 및 전체 시스템 구현 로드맵
- `docs/RQ_정제.md` — 연구 질문, 가설, 이론적 프레임워크
- `docs/실험_프로토콜.md` — 전체 실험 프로토콜 및 참가자 스크립트
- `docs/데이터_포맷_명세.md` — 이벤트 로그 CSV 스키마 및 이벤트 타입 정의
- `docs/설문지/` — 설문 도구 (사전 설문, NASA-TLX 한국어판, 신뢰 척도, 확신도 척도)
- `핵심논문_비교표_및_Gap분석.md` — 핵심 논문 비교표 및 연구 갭 분석

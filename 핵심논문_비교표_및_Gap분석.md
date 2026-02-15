# 핵심 논문 비교표 및 연구 Gap 분석

## 연구 Gap 한 문장 요약

> **"스마트글래스와 스마트폰을 '동시에 상호보완적으로' 사용하는 하이브리드 실내 내비게이션 시스템은 기존 연구에 존재하지 않으며, 특히 불확실성 상황에서의 기기 전환(Device Switching) 행동과 신뢰 보정(Trust Calibration)을 다룬 연구는 전무하다."**

---

## 검색 결과 요약

| 검색 키워드 | 직접 경쟁 논문 수 |
|---|---|
| "hybrid interaction smart glasses navigation" | **0편** |
| "cross-device interaction AR navigation" | 0편 (크로스 디바이스 연구는 있으나 내비게이션 맥락 아님) |
| "smart glasses smartphone wayfinding" | 0편 (비교 연구만 존재, 동시 사용 아님) |
| "device switching uncertainty navigation" | 0편 (자동화 신뢰 분야에만 존재) |

**결론: 글래스+폰 하이브리드 내비게이션을 직접 연구한 논문은 0편**

---

## 핵심 논문 15편 비교표

### A. HMD vs HHD 비교 연구 (기기 "대안"으로 비교, 동시 사용 아님)

| # | 논문 | 년도 | 학회 | 핵심 내용 | 한계/Gap |
|---|------|------|------|-----------|----------|
| 1 | **Rehman & Cao** - Augmented-Reality-Based Indoor Navigation: A Comparative Analysis of Handheld Devices Versus Google Glass | 2017 | IEEE Trans. HMS | 스마트폰 vs Google Glass AR 내비게이션 비교. Glass가 낮은 작업부하, 유사한 수행시간. 둘 다 종이지도보다 우수하나 경로/지도 기억력은 저하 | **두 기기를 대안으로만 비교, 동시 사용(hybrid) 미연구** |
| 2 | **Qiu et al.** - Use of augmented reality in human wayfinding: a systematic review | 2025 | Virtual Reality | 88편 체계적 리뷰. AR이 실내 길찾기 성능 향상(75%), 인지부하 감소(92%), 인지지도 향상(85%). HMD가 HHD보다 인지부하 낮음 | **하이브리드/다중 기기 연구 분류 자체가 없음. 개인화·적응형 가이던스 부재 지적** |

### B. HMD+HHD 하이브리드 인터랙션 (내비게이션 아닌 다른 태스크)

| # | 논문 | 년도 | 학회 | 핵심 내용 | 한계/Gap |
|---|------|------|------|-----------|----------|
| 3 | **Zhu & Grossman** - BISHARE: Exploring Bidirectional Interactions Between Smartphones and Head-Mounted AR | 2020 | CHI | 스마트폰↔AR HMD 간 양방향 인터랙션 디자인 공간 제시. 정보/제어가 양 기기 간 흐르는 프로토타입 | **내비게이션 맥락 아님. 이동 중 사용 미고려** |
| 4 | **Lee et al.** - Interaction Techniques for HMD-HHD Hybrid AR Systems | 2013 | IEEE ISMAR | HMD+HHD 하이브리드 AR 인터랙션 기법 제안 (크로스 디바이스 정보 공유, 상황 적응형 시각화) | **내비게이션 미적용. 정적 환경에서의 기술 제안만** |
| 5 | **AReading** - Understanding Trade-offs between Enhanced Legibility and Display Switching Costs in Hybrid AR Interfaces | 2025 | CHI | 광학 시스루 HMD + 스마트폰 하이브리드 인터페이스의 **디스플레이 전환 비용** 정량화 (읽기 과제) | **읽기 태스크만 다룸. 보행 중 전환, 불확실성 트리거 없음** |
| 6 | **Enhancing Reading on AR HMDs Using Smartphones as Assistive Displays** | 2023 | IEEE VR | 스마트폰을 AR HMD 보조 디스플레이로 활용. 하이브리드가 HMD 단독보다 낮은 작업부하 | **읽기 태스크. 공간 이동 맥락 없음** |

### C. 크로스 디바이스 & 적응형 MR 프레임워크

| # | 논문 | 년도 | 학회 | 핵심 내용 | 한계/Gap |
|---|------|------|------|-----------|----------|
| 7 | **Speicher et al.** - XD-AR: Cross-Device AR Application Development | 2018 | ACM EICS | 핸드헬드·헤드웜·프로젝티브 AR 간 입출력 통합 프레임워크. 30명 AR 디자이너 설문 기반 | **기술 프레임워크만 제시. 사용자 실험/내비게이션 평가 없음** |
| 8 | **Lindlbauer et al.** - Context-Aware Online Adaptation of Mixed Reality Interfaces | 2019 | UIST | 인지부하·태스크·환경 기반 MR 앱 자동 전환 최적화. 실시간 맥락인식 UI 조절 | **내비게이션 미적용. 신뢰·불확실성은 전환 트리거로 미포함** |
| 9 | **Grubert et al.** - Challenges in Mobile Multi-Device Ecosystems | 2016 | mUX Journal | HMD·스마트워치·스마트폰·태블릿 생태계 전문가 설문. 주요 챌린지 식별 | **챌린지 식별만. 실험적 검증 없음** |

### D. AR 실내 내비게이션 시스템 (단일 기기)

| # | 논문 | 년도 | 학회 | 핵심 내용 | 한계/Gap |
|---|------|------|------|-----------|----------|
| 10 | **Putra et al.** - Adaptive AR Navigation: Real-Time Mapping Using Node Placement and Marker Localization (NavARNode) | 2025 | Information (MDPI) | ARCore+Unity+A*+NavMesh+QR코드 기반 모바일 AR 실내 내비. HARUS 81.98점. 노드 기반 실시간 맵핑 | **스마트폰 단독. 장시간 폰 들기 피로 문제 직접 보고** |
| 11 | **Sharin et al.** - GoMap: Combining step counting with AR for indoor map locator | 2023 | IJEECS | 가속도계 기반 보행 수 측정 + AR 마커로 실내 위치 추정. 종이지도 대비 탐색시간 300% 단축 | **스마트폰 단독. 인프라 불필요는 장점이나 글래스 적용 미고려** |
| 12 | **NavMarkAR** - Landmark-based AR Wayfinding for Older Adults | 2023 | arXiv | 스마트글래스 전용 랜드마크 기반 AR 길찾기. 고령자 공간학습 강화 | **글래스 단독. 보조 기기 없음** |

### E. 신뢰·불확실성·인지 관련

| # | 논문 | 년도 | 학회 | 핵심 내용 | 한계/Gap |
|---|------|------|------|-----------|----------|
| 13 | **Trust Calibration in AR Decision Support Systems** | 2024 | IEEE Conference | AR 의사결정 지원에서 시스템 성능·과정·목적이 신뢰 보정에 미치는 영향 | **내비게이션 아닌 작업 환경. 다중 기기 미고려** |
| 14 | **Lee & Moray** - Effects of Errors on System Trust and Control Allocation in Route Planning | 2004 | IJHCS | 자동화 오류가 시스템 신뢰 저하 → 수동 제어 전환 유발. 높은 복잡성에서 더 강한 효과 | **전통적(비-AR) 경로 계획. 기기 전환이 아닌 자동화 수준 전환** |
| 15 | **Guevara et al.** - Mitigation of Attentional Tunneling using Spatial Auditory Display | 2018 | NASA TM | 공간 오디오가 주목 터널링 완화 (놓침률 7.4%→2.1%). 고부하에서도 효과 유지 | **비행 환경. 보행 내비게이션 미적용이나, 다중모달 인터랙션 설계 근거** |

---

## 연구 Gap 상세 분석

### Gap이 존재하는 3가지 축의 교차점

```
           하이브리드 기기 사용
          (글래스 + 폰 동시 사용)
                  /\
                 /  \
                / ★  \         ← 이 교차점에 기존 연구 없음
               / GAP  \
              /________\
  실내 내비게이션         신뢰/불확실성 기반
  (길찾기 태스크)         기기 전환 행동
```

### 구체적으로 비어있는 연구 영역 5가지

| # | 비어있는 영역 | 가장 가까운 기존 연구 | 차이점 |
|---|---|---|---|
| 1 | **글래스+폰 동시 사용 내비게이션 시스템** | Rehman & Cao (2017) | 두 기기를 A/B 비교만 함. 동시 사용 미연구 |
| 2 | **불확실성 기반 기기 전환 행동** | Lee & Moray (2004) | 자동화 수준 전환이지 기기 전환 아님. AR 미적용 |
| 3 | **다중 기기 내비게이션의 신뢰 보정 모델** | Trust Calibration in AR (2024) | 의사결정 지원 맥락. 내비게이션·다중기기 아님 |
| 4 | **내비게이션에서의 디스플레이 전환 비용** | AReading (CHI 2025) | 읽기 태스크에서만 측정. 보행+방향결정 미포함 |
| 5 | **내비게이션 맥락의 상호보완적 기기 역할 디자인** | BISHARE (CHI 2020) | 양방향 인터랙션 기법만 제시. 내비게이션 미적용 |

---

## 보유 PDF 논문의 연구 관련성 정리

| PDF 파일 | 본 연구와의 관계 |
|---|---|
| Distributed Cognition (List, 2006) | **이론적 프레임워크.** 인지 과업의 기기 간 분배, 집합(aggregation) 프로토콜의 중요성 → 하이브리드 시스템의 이론적 근거 |
| GoMap (Sharin et al., 2023) | **기술적 참조.** 보행수 측정+AR 마커 기반 실내 측위. 폰 들기 피로 문제 → 글래스 필요성 근거 |
| NavARNode (Putra et al., 2025) | **기술 스택 참조.** ARCore+Unity+A*+NavMesh 아키텍처. HARUS 평가. 폰 피로 문제 재확인 |
| Attentional Tunneling (Guevara et al., 2018) | **다중모달 설계 근거.** 공간 오디오로 주목 터널링 완화 → 글래스의 청각 피드백 설계에 활용 |
| AR Wayfinding Systematic Review (Qiu et al., 2025) | **전체 연구 맥락.** AR 내비게이션 최신 동향 88편 리뷰. 하이브리드 연구 부재 확인. 적응형 가이던스 필요성 지적 |

---

## 추천: 추가로 확보해야 할 논문

| 논문 | 이유 | 출처 |
|---|---|---|
| BISHARE (Zhu & Grossman, 2020) | 가장 가까운 하이브리드 인터랙션 연구 | CHI 2020, ACM DL |
| AReading (2025) | 디스플레이 전환 비용 정량화 | CHI 2025, ACM DL |
| Lee et al. (2013) - HMD-HHD Interaction | 하이브리드 AR 인터랙션 기법 원형 | IEEE ISMAR 2013 |
| Lindlbauer et al. (2019) - Context-Aware MR | 맥락인식 적응형 UI 최적화 | UIST 2019, ACM DL |
| Rehman & Cao (2017) | 가장 직접적인 HMD vs HHD 비교 | IEEE Trans. HMS |
| Vereschak et al. (2023) - Trust Calibration Survey | 자동화 시스템 신뢰 보정 최신 종합 리뷰 | CHI 2023, ACM DL |

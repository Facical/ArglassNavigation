# Related Work 정리

> **최종 업데이트**: 2026-03-14
> **목적**: 논문 Related Work 섹션 작성을 위한 선행 연구 체계적 정리
> **총 논문 수**: 50편 (기존 15편 + 신규 35편)

---

## 연구 Gap 요약

> **"스마트글래스와 스마트폰을 '동시에 상호보완적으로' 사용하는 하이브리드 실내 내비게이션 시스템은 기존 연구에 존재하지 않으며, 특히 불확실성 상황에서의 기기 전환(Device Switching) 행동과 신뢰 보정(Trust Calibration)을 다룬 연구는 전무하다."**

### Gap 시각화

```
                    하이브리드 기기 사용
                 (글래스 + 폰 동시 사용)
                        /\
                       /  \
           BISHARE(20)/    \AReading(25)
            Bang(23) / ★★★ \ MultiFi(15)
                    / 본 연구 \
                   /  (GAP)   \
                  /____________\
    실내 내비게이션              신뢰/불확실성 기반
    (길찾기 태스크)              기기 전환 행동
   NavMarkAR(24)            Lee&See(04)
   Rehman&Cao(17)           Rittenberg(24)
   Xu et al.(24)            Schwarz(22,23)
```

- **하이브리드 + 내비게이션**: BISHARE, AReading 등이 근접하나 모두 비내비게이션 태스크(읽기, 조작)
- **하이브리드 + 신뢰/불확실성**: 완전 공백
- **내비게이션 + 신뢰**: 자동화 신뢰 연구는 있으나 AR 내비게이션 미적용
- **3축 교차점**: 연구 0편 → 본 연구의 독자적 기여

---

## 논문 섹션 추천 구조

| 소섹션 | 내용 | 해당 논문 |
|--------|------|-----------|
| **2.1** AR-based Indoor Navigation | 단일 디바이스 AR 내비게이션 + 체계적 리뷰 | #1-#14 |
| **2.2** Hybrid and Cross-Device AR Interaction | HMD+스마트폰 하이브리드 인터랙션 | #15-#23, #47-#50 |
| **2.3** Display Switching Cost in AR | 디스플레이 전환의 인지적 비용 | #24-#28 |
| **2.4** Trust Calibration and Uncertainty in Navigation | 신뢰 보정 이론 + 내비게이션 불확실성 | #29-#38 |
| **2.5** Distributed Cognition and Cognitive Load | 이론적 프레임워크 + 교차검증 행동 | #39-#46 |

---

## 1. AR 실내 내비게이션 (단일 디바이스)

### 1-1. HMD vs HHD 비교 연구

#### #1. Rehman & Cao (2017) — HMD vs HHD AR 내비게이션 비교

- **제목**: Augmented-Reality-Based Indoor Navigation: A Comparative Analysis of Handheld Devices Versus Google Glass
- **연도/학회**: 2017 / IEEE Transactions on Human-Machine Systems
- **디바이스**: Google Glass vs 스마트폰
- **핵심 발견**: Glass가 낮은 작업부하, 유사한 수행시간. 둘 다 종이지도보다 우수하나 경로/지도 기억력 저하
- **본 연구 관련성**: HMD vs HHD 비교의 원형 연구. **두 기기를 대안으로만 비교, 동시 사용(hybrid) 미연구**
- **Gap**: 동시 사용이 아닌 A/B 비교만 수행

#### #2. Neeson et al. (2025) — 핸드헬드 vs OST HMD 실내 내비게이션

- **제목**: Comparing handheld monoscopic and head-mounted stereoscopic optical see-through augmented reality indoor navigational aids across age and gender identity
- **연도/학회**: 2025 / Virtual Reality (Springer)
- **디바이스**: OST HMD vs 핸드헬드
- **핵심 발견**: 76명 참가자. HMD가 성능 향상, 핸드헬드는 오히려 성능 저하. 연령-성별 간 유의한 상호작용
- **본 연구 관련성**: OST HMD(글래스 타입)의 우위 실증. **XReal Air2 Ultra와 유사 폼팩터**

### 1-2. AR 실내 내비게이션 사용자 연구

#### #3. Qiu et al. — NavMarkAR (2024)

- **제목**: NavMarkAR: A Landmark-based Augmented Reality (AR) Wayfinding System for Enhancing Older Adults' Spatial Learning
- **연도/학회**: 2024 / Advanced Engineering Informatics (Elsevier)
- **디바이스**: HoloLens 2
- **핵심 발견**: 6명 사용성 + 32명 심층 연구. 더 빠른 과제 완료 + 정확한 인지 지도. 고령자는 단순 방향 큐 선호 — 복잡한 보조가 오히려 인지 부담
- **본 연구 관련성**: 랜드마크 기반 설계 참조. 글래스 단독의 한계(복잡한 보조 = 인지 부담) 근거

#### #4. Xu et al. (2024) — AR 자기중심 시각 단서로 실내 길찾기 개선

- **제목**: Improving indoor wayfinding with AR-enabled egocentric cues: A comparative study
- **연도/학회**: 2024 / Advanced Engineering Informatics, Vol. 59
- **디바이스**: HoloLens 2
- **핵심 발견**: 31명, 4조건(종이지도 / 2D AR 경로 / 3D 미니맵 / AR 자기중심 맵) 비교. 자기중심 관점이 효율 향상, 인지부하 감소, 공간 인식 강화
- **본 연구 관련성**: **자기중심적 AR 화살표 설계의 이론적 근거**

#### #5. Putra et al. — NavARNode (2025)

- **제목**: Adaptive AR Navigation: Real-Time Mapping Using Node Placement and Marker Localization
- **연도/학회**: 2025 / Information (MDPI)
- **디바이스**: 스마트폰 (ARCore + Unity)
- **핵심 발견**: A* + NavMesh + QR코드 기반 모바일 AR 실내 내비. HARUS 81.98점. **장시간 폰 들기 피로 문제 직접 보고**
- **본 연구 관련성**: 스마트폰 단독의 한계 → 글래스 필요성 근거

#### #6. Liu et al. (2025) — 병원 환경 AR 실내 길찾기

- **제목**: Augmented Reality Indoor Wayfinding in Hospital Environments: An Empirical Study on Navigation Efficiency, User Experience, and Cognitive Load
- **연도/학회**: 2025 / arXiv:2601.00001
- **디바이스**: 스마트폰
- **핵심 발견**: 32명, AR vs 종이지도. AR이 빠르고 정확, 불안/작업부하 감소. 단, 종이지도 사용자가 공간 기억 우월
- **본 연구 관련성**: 실시간 효율 vs 장기 공간학습 트레이드오프. 하이브리드가 이를 해결할 가능성

#### #7. Kumaran et al. (CHI 2023) — 광역 AR에서의 내비게이션 보조

- **제목**: The Impact of Navigation Aids on Search Performance and Object Recall in Wide-Area Augmented Reality
- **연도/학회**: 2023 / CHI 2023
- **디바이스**: AR HMD
- **핵심 발견**: 24명, 3종 AR 내비 보조(화면 나침반, 화면 레이더, 월드 고정 수직 화살표) 비교. **월드 고정 화살표 최고 선호.** 물리→가상 주의 전환(attention shifting) 발견
- **본 연구 관련성**: AR 화살표 설계 직접 참조. 주의 전환 문제 제기

#### #8. Sharin et al. — GoMap (2023)

- **제목**: GoMap: Combining step counting with AR for indoor map locator
- **연도/학회**: 2023 / IJEECS
- **디바이스**: 스마트폰
- **핵심 발견**: 가속도계 + AR 마커로 실내 위치 추정. 종이지도 대비 탐색시간 300% 단축
- **본 연구 관련성**: 인프라 불필요 장점이나 글래스 적용 미고려

#### #9. Rasch et al. (CHI 2025) — AR 보행 중 이중 과제

- **제목**: AR You on Track? Investigating Effects of Augmented Reality Anchoring on Dual-Task Performance While Walking
- **연도/학회**: 2025 / CHI 2025
- **디바이스**: AR HMD
- **핵심 발견**: 26명. 보행 중 시각 과제 수행. 머리 앵커링이 보행에 최소 영향 + 정확한 상호작용. 손 앵커링은 작업부하 증가
- **본 연구 관련성**: 보행 중 AR 디스플레이 앵커링 설계 참조

#### #10. Team-Based SAR with AR (2024) — 협업 수색 구조

- **제목**: Augmented Reality in Team-based Search and Rescue: Exploring Spatial Perspectives for Enhanced Navigation and Collaboration
- **연도/학회**: 2024 / Safety Science
- **디바이스**: AR HMD
- **핵심 발견**: 64명. 자기중심적 큐는 다중 에이전트 환경에서 제한적, 타자중심적 큐가 팀 성과 향상
- **본 연구 관련성**: Beam Pro 지도(타자중심적 뷰)의 보완적 가치 근거

### 1-3. 체계적 리뷰 / 서베이

#### #11. Qiu et al. (2025) — AR 인간 길찾기 체계적 리뷰

- **제목**: Use of augmented reality in human wayfinding: a systematic review
- **연도/학회**: 2025 / Virtual Reality (Springer)
- **범위**: 88편 분석
- **핵심 발견**: AR이 길찾기 성능 향상(75%), 인지부하 감소(92%), 인지지도 향상(85%). HMD가 HHD보다 인지부하 낮음
- **본 연구 관련성**: **하이브리드/다중기기 연구 분류 자체가 없음.** 적응형 가이던스 부재 지적

#### #12. ACM Computing Surveys (2025) — AR 보행 내비게이션 체계적 리뷰

- **제목**: A Systematic Review of the Use of Augmented Reality in Pedestrian Navigation
- **연도/학회**: 2025 / ACM Computing Surveys
- **범위**: 79편 분석
- **핵심 발견**: HMD가 핸드헬드보다 작업부하 감소 + 공간학습 촉진. 2020년 이후 HMD 연구 급증

#### #13. Zhou et al. (2025) — XR 기반 시각 강화 단서와 공간 내비게이션

- **제목**: XR-based visual enhancing cues for human spatial navigation: A systematic literature review
- **연도/학회**: 2025 / Ergonomics (ScienceDirect)
- **범위**: 2014-2025년 논문
- **핵심 발견**: XR 시각 강화 단서(VEC)가 "4번째 공간 인지 단서"로 기여

#### #14. Stefanidi et al. (2024) — 취약 도로 사용자를 위한 AR 리뷰

- **제목**: Augmented Reality on the Move: A Systematic Literature Review for Vulnerable Road Users
- **연도/학회**: 2024 / Proceedings of the ACM on HCI, Vol. 8
- **핵심 발견**: HMD로의 전환 추세, 기술 제약으로 인한 실험실 연구 우위

---

## 2. XREAL/Nreal 디바이스 사용 연구 (동일 디바이스군)

> **핵심 발견**: XREAL/Nreal을 사용한 **실내 내비게이션 사용자 연구는 0편.** 본 연구가 XREAL Air2 Ultra를 활용한 최초의 실내 내비게이션 HCI 실험이 됨.

#### #D1. YOLOv8 XR 보행 보조 시스템 (2025)

- **제목**:  
- **연도/학회**: 2025 / MDPI Electronics, Vol. 14(3), Art. 425
- **디바이스**: **Xreal Light** + Android 스마트폰 (Unity 기반)
- **핵심 발견**: YOLOv8n으로 보도/횡단보도/장애물 실시간 인식. 시야 9구역 분할 위험도 평가. 평균 처리 시간 583ms
- **본 연구 관련성**: **동일 디바이스 계열.** 스마트폰 테더링 방식 유사. 단, 시각장애인 보행 보조(내비게이션 아님)

#### #D2. Faulhaber et al. (2022) — 스마트 글래스 주변 시각 알림

- **제목**: Evaluation of Priority-Dependent Notifications for Smart Glasses Based on Peripheral Visual Cues
- **연도/학회**: 2022 / i-com (De Gruyter)
- **디바이스**: **Nreal Light**
- **핵심 발견**: 24명, 3단계 우선순위 아이콘 기반 알림 평가. 낮은 우선순위 = 긴 반응시간 + 최소 방해
- **본 연구 관련성**: **동일 디바이스.** 글래스 주변시야 알림 설계 참조. HCI 평가 방법론 참고

#### #D3. Cooks & Aros (2024) — 우주비행사 AR 디바이스 성능 평가

- **제목**: Beyond Gravity: Exploring the Use of Augmented Reality Devices for Enhanced Astronaut Performance
- **연도/학회**: 2024 / Embry-Riddle Aeronautical University
- **디바이스**: **XReal Air 2 Pro**, Meta Quest 3, 태블릿
- **핵심 발견**: 3개 디바이스로 ISS 도면 판독/영상 품질 비교. SUS/UEQ-Short 측정
- **본 연구 관련성**: **동일 세대 디바이스.** 도면 판독 태스크에서의 XReal 사용성 데이터

#### #D4. Heuristic Evaluation of Next-Gen AR Glasses (2025)

- **제목**: A Heuristic Evaluation of the Next Generation of AR Glasses Across Four Use Cases
- **연도/학회**: 2025 / Preprints.org
- **디바이스**: **XREAL Air** 포함 다수 (Viture, Rokid, RayNeo, Google Android)
- **핵심 발견**: 차세대 AR 글래스의 사용성 휴리스틱 평가. 4개 도메인(훈련/스포츠/접근성/소비자)

---

## 3. HMD+스마트폰 하이브리드 인터랙션 (핵심 차별화 영역)

> **내비게이션 맥락에서의 하이브리드 사용은 전무.** 이것이 본 연구의 핵심 기여.

### 3-1. 핵심 하이브리드 인터랙션 연구

#### #15. Zhu & Grossman — BISHARE (CHI 2020) ★★★

- **제목**: BISHARE: Exploring Bidirectional Interactions Between Smartphones and Head-Mounted Augmented Reality
- **연도/학회**: 2020 / CHI 2020
- **핵심 발견**: HMD-스마트폰 양방향 인터랙션 디자인 스페이스 제안. HMD→스마트폰, 스마트폰→HMD 양방향 탐색. 12명 사용자 연구
- **본 연구 관련성**: **가장 가까운 선행 연구.** "2D on phone, 3D/spatial on HMD" 원칙 → 본 연구의 WHERE(글래스)/WHAT(Beam Pro) 역할 분리에 직접 적용
- **Gap**: 내비게이션 미적용, 이동 중 사용 미고려

#### #16. AReading (CHI 2025) ★★★

- **제목**: AReading with Smartphones: Understanding the Trade-offs between Enhanced Legibility and Display Switching Costs in Hybrid AR Interfaces
- **연도/학회**: 2025 / CHI 2025
- **핵심 발견**: 24명, OST-HMD + 스마트폰 하이브리드 읽기 태스크. 가독성 향상 + 정신적/신체적 부담 감소. 단, 디스플레이 간 거리↑ 시 switching cost가 이점 상쇄
- **본 연구 관련성**: **디스플레이 전환 비용 정량화.** 보행 중 전환 측정에 직접 참조
- **Gap**: 읽기 태스크만. 보행 + 방향 결정 미포함

#### #17. Bang & Woo — AR HMD + 스마트폰 보조 (IEEE VR 2023) ★★

- **제목**: Enhancing the Reading Experience on AR HMDs by Using Smartphones as Assistive Displays
- **연도/학회**: 2023 / IEEE VR 2023
- **핵심 발견**: HMD 저해상도/좁은 FOV 문제를 스마트폰 보조로 보완. 하이브리드가 낮은 task load + 가독성 향상 + 시각 피로 감소
- **본 연구 관련성**: **스마트폰 보조 디스플레이 효과 실증.** Beam Pro 정보 허브의 이론적 정당성
- **Gap**: 읽기 태스크. 공간 이동 맥락 없음

#### #18. Budhiraja, Lee & Billinghurst — HHD+HMD (ISMAR 2013)

- **제목**: Using a HHD with a HMD for Mobile AR Interaction
- **연도/학회**: 2013 / IEEE ISMAR
- **핵심 발견**: HMD+HHD 4가지 인터랙션 기법 비교. HMD로 AR 시청, 터치스크린 HHD로 상호작용하는 상보적 역할
- **본 연구 관련성**: 초기 하이브리드 연구의 원형. 크로스디바이스 정보 공유 개념
- **Gap**: 정적 환경에서의 기술 제안만. 내비게이션 미적용

### 3-2. 하이브리드 UI 서베이 & 프레임워크

#### #19. Hubenschmid et al. — Hybrid User Interfaces Survey (2025) ★★

- **제목**: Hybrid User Interfaces: Past, Present, and Future of Complementary Cross-Device Interaction in Mixed Reality
- **연도/학회**: 2025 / arXiv:2509.05491
- **핵심 발견**: HUI(하이브리드 사용자 인터페이스)에 대한 체계적 서베이 + 택소노미. 이질적 디바이스의 상호보완적 역할 결합. 30년 연구 통합
- **본 연구 관련성**: 본 연구의 이론적 위치 설정에 핵심. "Complementary Cross-Device Interaction" 개념

#### #20. Brudy et al. — Cross-Device Taxonomy (CHI 2019)

- **제목**: Cross-Device Taxonomy: Survey, Opportunities and Challenges of Interactions Spanning Across Multiple Devices
- **연도/학회**: 2019 / CHI 2019
- **핵심 발견**: 510편 분석. 6차원(시간, 구성, 관계, 규모, 역동성, 공간) 크로스디바이스 택소노미
- **본 연구 관련성**: 본 연구의 위치를 택소노미 내에서 정의하는 프레임워크

### 3-3. 스마트폰을 AR 보조 디바이스로 활용

#### #21. Grubert et al. — MultiFi (CHI 2015) ★★

- **제목**: MultiFi: Multi-Fidelity Interaction with Displays On and Around the Body
- **연도/학회**: 2015 / CHI 2015
- **핵심 발견**: HMD + 스마트워치 + 태블릿 다중 디스플레이 결합. 디바이스 간 충실도 차이를 극복하는 위젯 분산 방식
- **본 연구 관련성**: **다중 디바이스 충실도 설계 원칙.** 글래스(저충실도 방향) ↔ Beam Pro(고충실도 정보) 설계 근거

#### #22. Screen Augmentation (CHI 2024 LBW)

- **제목**: Screen Augmentation Technique Using AR Glasses and Smartphone without External Sensors
- **연도/학회**: 2024 / CHI 2024 Extended Abstracts
- **핵심 발견**: 외부 센서 없이 AR 글래스 + 스마트폰으로 화면 확장. 관심 영역 고해상도 보기
- **본 연구 관련성**: 기술적 참조. 글래스+스마트폰 조합의 실현 가능성

#### #23. Knierim et al. — SmARtphone Controller (2021)

- **제목**: The SmARtphone Controller
- **연도/학회**: 2021 / i-com: Journal of Interactive Media
- **핵심 발견**: 24명, 스마트폰 기반 AR 컨트롤러가 미드에어 제스처 대비 더 빠르고 정확 + 낮은 task load
- **본 연구 관련성**: **스마트폰의 물리적 터치 인터페이스 우위 근거**

#### #24. Cross-Device Vocabulary Learning (2025)

- **제목**: A Cross-Device Interaction with the Smartphone and HMD for Vocabulary Learning
- **연도/학회**: 2025 / Springer LNCS
- **핵심 발견**: AR HMD로 맥락 정보 + 스마트폰으로 효율적 텍스트 입력. 16명 평가
- **본 연구 관련성**: 역할 분리(HMD: 맥락, Phone: 정밀 입력/정보)가 본 연구의 WHERE/WHAT 분리와 유사

#### #25. Ren et al. — Window Management AR+Smartphone (2022)

- **제목**: Design and Evaluation of Window Management Operations in AR Headset+Smartphone Interface
- **연도/학회**: 2022 / Virtual Reality & Intelligent Hardware
- **핵심 발견**: AR 헤드셋 + 스마트폰 = 넓은 디스플레이 + 정밀 터치 입력 동시 제공. 윈도우 관리 인터랙션 어휘 제안
- **본 연구 관련성**: AR+스마트폰의 상호보완적 강점 실증

### 3-4. 크로스디바이스 인터랙션 프레임워크

#### #26. Speicher et al. — XD-AR Framework (2018)

- **제목**: XD-AR: Cross-Device AR Application Development
- **연도/학회**: 2018 / ACM EICS
- **핵심 발견**: 핸드헬드/헤드웜/프로젝티브 AR 통합 프레임워크. 30명 AR 디자이너 설문
- **Gap**: 기술 프레임워크만. 사용자 실험/내비게이션 평가 없음

#### #27. Lindlbauer et al. — Context-Aware MR Adaptation (UIST 2019)

- **제목**: Context-Aware Online Adaptation of Mixed Reality Interfaces
- **연도/학회**: 2019 / UIST 2019
- **핵심 발견**: 인지부하/태스크/환경 기반 MR 앱 자동 전환 최적화. 실시간 맥락인식 UI 조절
- **본 연구 관련성**: 맥락인식 적응형 UI. 트리거 기반 정보 제공의 설계 근거
- **Gap**: 내비게이션 미적용. 신뢰/불확실성은 전환 트리거로 미포함

#### #28. Grubert et al. — Challenges in Multi-Device Ecosystems (2016)

- **제목**: Challenges in Mobile Multi-Device Ecosystems
- **연도/학회**: 2016 / mUX Journal
- **핵심 발견**: 다중 디바이스 생태계 핵심 과제. **split attention(근거리/원거리 동시 초점 불가)** 문제 규명
- **본 연구 관련성**: 하이브리드 조건의 잠재적 단점(split attention) 인지

---

## 4. 디스플레이 전환 비용 (Display Switching Cost)

> 하이브리드 조건에서 글래스 ↔ Beam Pro 간 시선 전환의 인지적 비용을 이해하기 위한 기반.

#### #29. Gabbard et al. (2018) — AR 컨텍스트 스위칭 & 초점 거리 ★★

- **제목**: Effects of AR Display Context Switching and Focal Distance Switching on Human Performance
- **연도/학회**: 2018 / IEEE TVCG, 25(6):2228-2241
- **핵심 발견**: OST AR에서 실제/가상 전환(context switching)과 초점 거리 전환(focal distance switching) 모두 성능 유의하게 저하. 반복 시 시각 피로 꾸준히 증가
- **본 연구 관련성**: **글래스→Beam Pro 전환의 초점 거리 변화 비용 근거**

#### #30. Gabbard et al. (2020) — AR Haploscope 재현 연구

- **제목**: Impact of AR Display Context Switching and Focal Distance Switching on Human Performance: Replication on an AR Haploscope
- **연도/학회**: 2020 / IEEE VR 2020
- **핵심 발견**: #29의 재현 연구. "transient focal blur" 현상 규명 — 눈이 아직 초점을 맞추는 동안 발생하는 일시적 흐림

#### #31. Baumeister et al. (2017) — AR 디스플레이의 인지 비용

- **제목**: Cognitive Cost of Using Augmented Reality Displays
- **연도/학회**: 2017 / IEEE TVCG, 23(11):2378-2388
- **핵심 발견**: 3종 AR 디스플레이(공간 AR, HoloLens OST, Gear VR VST) 비교. 공간 AR이 최고 성능 + 최저 인지 부하. **제한된 FOV가 인지부하 증가 요인**
- **본 연구 관련성**: OST 글래스의 FOV 제약이 Beam Pro 보조 필요성을 높이는 근거

#### #32. Rashid et al. (2012) — 디스플레이 전환 비용 비교

- **제목**: The Cost of Display Switching: A Comparison of Mobile, Large Display and Hybrid UI Configurations
- **연도/학회**: 2012 / AVI 2012, pp.99-106
- **핵심 발견**: 모바일 + 대형 디스플레이 + 하이브리드 비교. **하이브리드가 모든 태스크에서 최악 또는 동등 최악.** 모바일 제어 + 대형 디스플레이가 지도 검색에서 최상
- **본 연구 관련성**: 하이브리드의 잠재적 단점 인지. **전환 비용 vs 정보 이점의 트레이드오프 검증 필요**

#### #33. Bai et al. — Heads-Up Multitasker (CHI 2024) ★★

- **제목**: Heads-Up Multitasker: Simulating Attention Switching On Optical Head-Mounted Displays
- **연도/학회**: 2024 / CHI 2024
- **핵심 발견**: OHMD 보행 중 디지털↔물리 주의 전환 시뮬레이션 모델 최초 제안. 계층적 강화학습 기반 주의 배분 최적화. 주의 전환, 읽기/보행 속도 변화, 읽기 재개 등 핵심 행동 재현
- **본 연구 관련성**: **보행 중 주의 전환의 인지 모델.** 이중 과제(보행+정보 탐색) 설계 근거

---

## 5. 신뢰 보정 (Trust Calibration) & 불확실성

> 본 연구의 H1(신뢰 보정 가설)과 4종 트리거 설계의 이론적 기반.

### 5-1. 신뢰 보정 이론

#### #34. Lee & See (2004) — Trust in Automation ★★★

- **제목**: Trust in Automation: Designing for Appropriate Reliance
- **연도/학회**: 2004 / Human Factors, 46(1), 50-80
- **핵심 발견**: 신뢰 = "불확실성과 취약성 하에서 에이전트가 목표 달성을 도울 것이라는 태도." 신뢰의 3가지 기반: 성능(performance), 과정(process), 목적(purpose). 폐쇄 루프 모델
- **본 연구 관련성**: **핵심 이론 프레임워크.** 트리거가 성능 기반 신뢰를 위반하는 메커니즘 설명

#### #35. Chiou & Lee (2023) — Trusting Automation 업데이트 ★★

- **제목**: Trusting Automation: Designing for Responsivity and Resilience
- **연도/학회**: 2023 / Human Factors, 65(1), 137-165
- **핵심 발견**: Lee & See (2004) 업데이트. 보정보다 **반응성(responsivity)** 통한 "신뢰 과정(process of trusting)" 지원이 더 중요. situation-semiotics-strategy-sequence 관계 프레임워크
- **본 연구 관련성**: 하이브리드 조건의 Beam Pro가 "반응적 대안 정보원"으로 신뢰 회복 지원

#### #36. Vereschak et al. (CHI 2023) — 신뢰 보정 측정 서베이 ★★

- **제목**: Measuring and Understanding Trust Calibrations for Automated Systems: A Survey of the State-Of-The-Art and Future Directions
- **연도/학회**: 2023 / CHI 2023
- **핵심 발견**: 1000→96편 검토. 신뢰 보정의 측정/개입/결과 체계적 분석. 실험 프로토콜에 신뢰 정의가 통합되지 않는 경우가 많아 결과 과대해석 위험
- **본 연구 관련성**: **신뢰 보정 측정 방법론 참조.** 확신도-정확도 상관(calibration index) 설계 근거

#### #37. Yeh & Wickens (2001) — AR 디스플레이 신호와 신뢰 보정

- **제목**: Display Signaling in Augmented Reality: Effects of Cue Reliability and Image Realism on Attention Allocation and Trust Calibration
- **연도/학회**: 2001 / Human Factors, 43(3), 355-365
- **핵심 발견**: AR 큐 신뢰성 100% vs 75% 조작. 낮은 신뢰성 → 큐잉 이점 + 주의 터널링 동시 감소. 이미지 사실감↑ → 큐 의존↑
- **본 연구 관련성**: **AR 화살표 신뢰성 조작(트리거)의 직접적 선행 연구**

#### #38. Rittenberg et al. (2024) — 변동 신뢰성에서의 신뢰 ★

- **제목**: Trust with Increasing and Decreasing Reliability
- **연도/학회**: 2024 / Human Factors
- **핵심 발견**: 신뢰는 항상 시스템 신뢰성을 추적하지 않음. **저신뢰 경험 후 신뢰 회복 어려움.** 자기 확신이 증가하면 자동화 신뢰성의 신뢰 영향↑
- **본 연구 관련성**: **트리거 경험 후 Glass Only에서의 신뢰 회복 지연 예측 근거**

#### #39. Lee & Moray (2004) — 오류와 시스템 신뢰

- **제목**: Effects of Errors on System Trust and Control Allocation in Route Planning
- **연도/학회**: 2004 / IJHCS
- **핵심 발견**: 자동화 오류 → 신뢰 저하 → 수동 제어 전환. 높은 복잡성에서 더 강한 효과
- **본 연구 관련성**: **트리거(=시스템 오류)가 기기 전환(=수동 제어)을 유발하는 메커니즘.** 단, 전통적(비-AR) 경로 계획

#### #40. Parasuraman & Manzey (2010) — 자동화 안주 & 편향

- **제목**: Complacency and Bias in Human Use of Automation: An Attentional Integration
- **연도/학회**: 2010 / Human Factors, 52(3), 381-410
- **핵심 발견**: 자동화 안주 + 편향 통합 모델. 다중 과제 부하에서 안주 발생. 훈련이나 지시로 방지 불가
- **본 연구 관련성**: Glass Only에서 화살표에 대한 과의존(automation bias) 가능성 예측

#### #41. Trust Calibration in AR-DSS (IEEE, 2024)

- **제목**: Trust Calibration in Augmented Reality-based Decision Support Systems
- **연도/학회**: 2024 / IEEE Conference
- **핵심 발견**: AR 의사결정 지원에서 보정된 신뢰가 시스템 의존도와 향후 사용 의도에 긍정적 영향
- **본 연구 관련성**: AR 맥락의 신뢰 보정 실증
- **Gap**: 내비게이션 아닌 작업 환경, 다중 기기 미고려

### 5-2. 내비게이션 불확실성

#### #42. Schwarz et al. (2022) — EEG로 길찾기 불확실성 식별 ★

- **제목**: Identifying Uncertainty States during Wayfinding in Indoor Environments: An EEG Classification Study
- **연도/학회**: 2022 / Advanced Engineering Informatics
- **핵심 발견**: 30명, VR 병원 환경. EEG theta/alpha/beta 대역으로 불확실성 상태 식별. ROC-AUC 0.70
- **본 연구 관련성**: **길찾기 불확실성의 인지신경과학적 기반.** 트리거 설계의 생태적 타당성 근거

#### #43. Schwarz et al. (2023) — 내비게이션 불확실성 연속 측정 ★

- **제목**: Real-time Continuous Perceived Uncertainty Annotation for Spatial Navigation Studies in Buildings
- **연도/학회**: 2023 / Journal of Building Engineering
- **핵심 발견**: 실시간 + 사후 불확실성 연속 측정법 개발. **마지막 유용한 표지판 이후 경과 시간과 불확실성 상관.** 경로 선택(route-choice) vs 경로 확인(affirm on-route) 두 유형 구분
- **본 연구 관련성**: **T4(안내 부재) 트리거의 직접적 근거.** 확신도 측정 방법론 참조

### 5-3. 신뢰 위반 & 복구

#### #44. Sebo et al. (HRI 2019) — 신뢰 위반과 복구

- **제목**: "I Don't Believe You": Investigating the Effects of Robot Trust Violation and Repair
- **연도/학회**: 2019 / HRI 2019
- **핵심 발견**: **Model Update가 가장 효과적인 신뢰 복구 전략.** 복원된 신뢰가 위반 이전 수준 초과
- **본 연구 관련성**: Beam Pro의 교차검증 정보가 "model update"로 기능 → 신뢰 회복 촉진

#### #45. Trust Repair in AI Decision-Making (FAccT 2024)

- **제목**: Trust Development and Repair in AI-Assisted Decision Making
- **연도/학회**: 2024 / FAccT 2024
- **핵심 발견**: 정확도 개선이 부분적이지만 완전하지 않은 신뢰 회복. **추가 접근(교차검증 등)이 필요**
- **본 연구 관련성**: 단일 정보원 의존의 한계 → 교차검증 행동의 합리적 근거

---

## 6. 분산 인지 & 인지 부하 (이론적 프레임워크)

### 6-1. 분산 인지 이론

#### #46. Hutchins (1995) — Cognition in the Wild ★★★

- **제목**: Cognition in the Wild
- **연도/출판**: 1995 / MIT Press
- **핵심 발견**: 인지 과정이 (1) 사회적 구성원 간, (2) 내부-외부 구조 간, (3) 시간에 걸쳐 분산. 해군 함정 항해팀 사례
- **본 연구 관련성**: **핵심 이론.** 글래스(WHERE) + Beam Pro(WHAT)로의 인지 분산 근거

#### #47. Hollan, Hutchins & Kirsh (2000) — 분산 인지와 HCI ★★

- **제목**: Distributed Cognition: Toward a New Foundation for Human-Computer Interaction Research
- **연도/학회**: 2000 / ACM TOCHI, 7(2), 174-196
- **핵심 발견**: 분산 인지를 HCI 기반으로 제안. 내부-외부 자원 간 **조율(coordination)** 에 초점
- **본 연구 관련성**: **다중 디바이스 HCI의 이론적 틀.** 기기 전환 = 조율 행동으로 해석

### 6-2. 인지 부하 & 다중 자원 이론

#### #48. Wickens (2002) — Multiple Resources Theory ★★

- **제목**: Multiple Resources and Performance Prediction
- **연도/학회**: 2002 / Theoretical Issues in Ergonomics Science, 3(2), 159-177
- **핵심 발견**: 4차원 모델: 처리 단계(지각/인지 vs 반응), 감각 양식(청각 vs 시각), 코드(언어 vs 공간), 시각 채널(초점 vs 주변). 서로 다른 자원 사용 시 간섭↓
- **본 연구 관련성**: **글래스(공간/시각/주변) + Beam Pro(언어/시각/초점)가 서로 다른 인지 자원 사용 → 간섭 최소화**

#### #49. Buchner et al. (2022) — AR 인지 부하 체계적 리뷰

- **제목**: The Impact of Augmented Reality on Cognitive Load and Performance: A Systematic Review
- **연도/학회**: 2022 / Journal of Computer Assisted Learning, 38(1), 285-303
- **범위**: 58편 분석
- **핵심 발견**: AR이 인지적으로 덜 부담 + 높은 성과. **AR 글래스 자체가 불필요한 인지부하 증가 가능**
- **본 연구 관련성**: 글래스의 잠재적 인지부하 증가를 Beam Pro가 보완하는 설계 근거

#### #50. Suzuki et al. (2024) — AR 인지 부하의 생리학적 측정

- **제목**: Measuring Cognitive Load in Augmented Reality with Physiological Methods: A Systematic Review
- **연도/학회**: 2024 / Journal of Computer Assisted Learning, 40(2), 375-393
- **범위**: 23편 분석
- **핵심 발견**: **시선 추적(eye-tracking)이 가장 일관적 인지부하 지표.** 다중 방법 접근 필수
- **본 연구 관련성**: 향후 연구에서 시선 추적 도입 시 방법론 참조

### 6-3. 정보 탐색 & 교차검증 행동

#### #51. Pirolli & Card (1999) — Information Foraging Theory ★

- **제목**: Information Foraging
- **연도/학회**: 1999 / Psychological Review, 106(4), 643-675
- **핵심 발견**: 정보 냄새(information scent) 개념. 시간 대비 정보 획득률 최대화. 정보원/패치/냄새/식단의 4가지 핵심 개념
- **본 연구 관련성**: 사용자가 Beam Pro를 참조하는 시점 예측. **트리거 = 정보 냄새 강화**

#### #52. Guevara et al. (2018) — 주목 터널링 완화

- **제목**: Mitigation of Attentional Tunneling using Spatial Auditory Display
- **연도/학회**: 2018 / NASA TM
- **핵심 발견**: 공간 오디오로 주목 터널링 완화 (놓침률 7.4%→2.1%). 고부하에서도 효과 유지
- **본 연구 관련성**: 다중모달 인터랙션으로 단일 채널 과의존 방지. 하이브리드의 설계 철학 근거

---

## 구체적 Gap 분석 (업데이트, 5가지)

| # | 비어있는 영역 | 가장 가까운 기존 연구 | 차이점 |
|---|---|---|---|
| 1 | **글래스+폰 동시 사용 내비게이션** | BISHARE (CHI 2020), AReading (CHI 2025), Bang & Woo (IEEE VR 2023) | 모두 비내비게이션 태스크(읽기, 조작). 보행 중 이동+방향결정 미포함 |
| 2 | **불확실성 기반 기기 전환 행동** | Lee & Moray (2004), Schwarz et al. (2022, 2023) | 자동화 수준 전환 또는 불확실성 측정만. AR 다중기기 전환 미연구 |
| 3 | **다중 기기 내비게이션의 신뢰 보정** | Trust in AR-DSS (2024), Vereschak (CHI 2023) | 의사결정 지원 맥락. 내비게이션/다중기기 아님 |
| 4 | **보행 중 디스플레이 전환 비용** | AReading (CHI 2025), Gabbard et al. (2018) | 정적 읽기 태스크만. 보행+방향결정 미포함 |
| 5 | **소비자급 OST AR 글래스 실내 내비게이션** | 대부분 HoloLens/Magic Leap 사용 | XREAL 계열 디바이스로 실내 내비게이션 연구 0편 |

---

## 추가 확보 추천 논문

| 논문 | 이유 | 출처 |
|---|---|---|
| Vereschak et al. (CHI 2023) | 신뢰 보정 측정 방법론 서베이 | ACM DL |
| Chiou & Lee (2023) | Lee & See 프레임워크 업데이트 | Human Factors |
| Hubenschmid et al. (2025) | HUI 종합 서베이 | arXiv |
| Rittenberg et al. (2024) | 변동 신뢰성에서의 신뢰 비대칭 | Human Factors |
| Schwarz et al. (2023) | 불확실성 연속 측정법 | J. Building Eng. |
| Bai et al. (CHI 2024) | OHMD 주의 전환 모델 | ACM DL |
| Gabbard et al. (2018) | AR 컨텍스트 스위칭 비용 | IEEE TVCG |
| Rashid et al. (2012) | 디스플레이 전환 비용 비교 | ACM DL |
| Neeson et al. (2025) | OST HMD vs 핸드헬드 내비게이션 | Springer |

---

## 참고 문헌 목록 (BibTeX용 키 포맷)

```
[1]  Rehman2017_HMDvsHHD
[2]  Neeson2025_OSTnavigation
[3]  Qiu2024_NavMarkAR
[4]  Xu2024_EgocentricCues
[5]  Putra2025_NavARNode
[6]  Liu2025_HospitalWayfinding
[7]  Kumaran2023_NavigationAids_CHI
[8]  Sharin2023_GoMap
[9]  Rasch2025_DualTaskWalking_CHI
[10] TeamSAR2024_CollaborativeAR
[11] Qiu2025_WayfindingReview
[12] ACMSurveys2025_PedestrianNav
[13] Zhou2025_XRVisualCues
[14] Stefanidi2024_VulnerableRoadUsers
[D1] YOLOv8_XrealLight2025
[D2] Faulhaber2022_NrealNotifications
[D3] Cooks2024_AstronautAR
[D4] HeuristicEval2025_ARGlasses
[15] Zhu2020_BISHARE_CHI
[16] AReading2025_CHI
[17] Bang2023_SmartphoneAssistive_IEEEVR
[18] Budhiraja2013_HHDwithHMD_ISMAR
[19] Hubenschmid2025_HybridUI_Survey
[20] Brudy2019_CrossDeviceTaxonomy_CHI
[21] Grubert2015_MultiFi_CHI
[22] ScreenAugmentation2024_CHI_LBW
[23] Knierim2021_SmARtphoneController
[24] CrossDeviceVocab2025
[25] Ren2022_WindowManagement
[26] Speicher2018_XDAR
[27] Lindlbauer2019_ContextAwareMR_UIST
[28] Grubert2016_MultiDeviceChallenges
[29] Gabbard2018_ContextSwitching_TVCG
[30] Gabbard2020_Replication_IEEEVR
[31] Baumeister2017_CognitiveCostAR_TVCG
[32] Rashid2012_DisplaySwitching_AVI
[33] Bai2024_HeadsUpMultitasker_CHI
[34] Lee2004_TrustInAutomation
[35] Chiou2023_TrustingAutomation
[36] Vereschak2023_TrustCalibrationSurvey_CHI
[37] Yeh2001_DisplaySignalingAR
[38] Rittenberg2024_TrustReliability
[39] Lee2004_ErrorsTrustControl
[40] Parasuraman2010_ComplacencyBias
[41] TrustCalibrationARDSS2024
[42] Schwarz2022_EEGWayfinding
[43] Schwarz2023_UncertaintyAnnotation
[44] Sebo2019_TrustViolationRepair_HRI
[45] TrustRepairAI2024_FAccT
[46] Hutchins1995_CognitionInTheWild
[47] Hollan2000_DistributedCognition_TOCHI
[48] Wickens2002_MultipleResources
[49] Buchner2022_ARCognitiveLoad_Review
[50] Suzuki2024_PhysiologicalCogLoad
[51] Pirolli1999_InformationForaging
[52] Guevara2018_AttentionalTunneling_NASA
```

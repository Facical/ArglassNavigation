using System;
using System.Collections.Generic;
using UnityEngine;
using ARNavExperiment.Core;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;
using ARNavExperiment.Utils;

namespace ARNavExperiment.Navigation
{
    [Serializable]
    public class Waypoint
    {
        public string waypointId;
        public Vector3 fallbackPosition; // 에디터 테스트용 / 앵커 없을 때
        public float radius = 2f;
        public string locationName;

        /// <summary>런타임에 SpatialAnchorManager가 설정</summary>
        [NonSerialized] public Transform anchorTransform;

        /// <summary>앵커가 있으면 앵커 위치, 없으면 fallback 좌표</summary>
        public Vector3 Position => anchorTransform != null
            ? anchorTransform.position
            : fallbackPosition;
    }

    [Serializable]
    public class RouteData
    {
        public string routeId;
        public List<Waypoint> waypoints = new List<Waypoint>();
    }

    public class WaypointManager : MonoBehaviour
    {
        public static WaypointManager Instance { get; private set; }

        [Header("Routes")]
        [SerializeField] private RouteData routeB;

        public int CurrentWaypointIndex { get; private set; }
        public Waypoint CurrentWaypoint => activeRoute?.waypoints[CurrentWaypointIndex];
        public Waypoint NextWaypoint => HasNextWaypoint ? activeRoute.waypoints[CurrentWaypointIndex + 1] : null;
        public bool HasNextWaypoint => activeRoute != null && CurrentWaypointIndex < activeRoute.waypoints.Count - 1;

        /// <summary>현재 경로에서 fallback 사용 중인 웨이포인트 수</summary>
        public int FallbackWaypointCount
        {
            get
            {
                if (activeRoute == null) return 0;
                int count = 0;
                foreach (var wp in activeRoute.waypoints)
                {
                    if (wp.anchorTransform == null)
                        count++;
                }
                return count;
            }
        }

        [Header("Heading Fallback")]
        [SerializeField] private float fallbackHeadingOffset = 0f;

        [Header("Fallback Arrival")]
        [SerializeField] private float fallbackRadiusMultiplier = 0.8f;

        [Header("Heading Persistence")]
        [Tooltip("false(기본): 세션마다 heading을 새로 계산. true: PlayerPrefs에 저장하여 다음 세션에 재사용")]
        [SerializeField] private bool persistHeadingAcrossSessions = false;

        private RouteData activeRoute;
        private Transform playerTransform;
        private float lastSnapshotTime;
        private ARArrowRenderer cachedArrowRenderer;
        private float headingCalibrationOffset;
        private string headingSource = "none";

        // SLAM→도면 좌표 변환 파라미터
        private bool hasMapCalibration;
        private float mapCalibCos = 1f, mapCalibSin = 0f;
        private Vector2 mapCalibOffset;
        private string calibrationSource = "none";

        // Image Tracking에서 주입된 가상 앵커 쌍 (Phase 2)
        private List<(Vector2 slamXZ, Vector2 fallbackXZ)> injectedPairs;

        // 2점 수동 보정용 필드
        private bool manualCalibFirstPointRecorded;
        private Vector2 manualCalibSlamXZ_1;
        private Vector2 manualCalibFallbackXZ_1;

        /// <summary>현재 heading 보정 오프셋 (도 단위)</summary>
        public float HeadingCalibrationOffset => headingCalibrationOffset;

        /// <summary>현재 heading 보정 소스 ("auto", "fallback", "manual_2pt", "image_tracking", "none")</summary>
        public string HeadingSource => headingSource;

        /// <summary>맵 보정 활성화 여부</summary>
        public bool HasMapCalibration => hasMapCalibration;

        /// <summary>맵 보정 소스 ("none", "auto_zero_anchor", "anchor_1", "anchor_N", "manual", "injected")</summary>
        public string CalibrationSource => calibrationSource;

        public event Action<Waypoint> OnWaypointReached;
        public event Action OnRouteComplete;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null)
                anchorMgr.OnAnchorLateRecovered += OnAnchorLateRecovered;
        }

        private void OnDisable()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null)
                anchorMgr.OnAnchorLateRecovered -= OnAnchorLateRecovered;
        }

        public void LoadRoute(string routeId)
        {
            // 세션간 heading 오염 차단 — 이전 세션의 stored heading 삭제
            PlayerPrefs.DeleteKey("ARNav_HeadingOffset");
            PlayerPrefs.Save();

            // Route B 고정 — 단일 경로 사용
            activeRoute = routeB;
            CurrentWaypointIndex = 0;

            var cam = XRCameraHelper.GetCamera();
            if (cam != null)
                playerTransform = cam.transform;
            else
                Debug.LogWarning("[WaypointManager] LoadRoute: 카메라를 찾을 수 없어 playerTransform 미설정");

            // SpatialAnchorManager에서 앵커 Transform 바인딩
            BindAnchorTransforms(routeId);

            // Heading 보정: auto → fallback (stored 분기 제거)
            bool autoCalibrated = AutoCalibrateFromAnchors();
            if (autoCalibrated)
            {
                headingSource = "auto";
            }
            else
            {
                headingCalibrationOffset = fallbackHeadingOffset;
                headingSource = "fallback";
            }

            // heading 확정 후 맵 보정 1회만 계산
            ComputeMapCalibration();
            Debug.Log($"[WaypointManager] Heading offset applied: {headingCalibrationOffset:F1}° (source={headingSource})");
            DomainEventBus.Instance?.Publish(new HeadingCalibrationApplied(headingSource, headingCalibrationOffset));

            // 바인딩 요약 도메인 이벤트 발행
            PublishRouteBindingSummary(routeId);

            // 스냅샷 타이머 초기화
            lastSnapshotTime = Time.time;
            cachedArrowRenderer = FindObjectOfType<ARArrowRenderer>();

            // 앵커 바인딩 후 가장 가까운 웨이포인트를 시작 인덱스로 설정
            int nearestIdx = FindNearestWaypointIndex();
            if (nearestIdx > 0)
            {
                CurrentWaypointIndex = nearestIdx;
                Debug.Log($"[WaypointManager] Start index adjusted to {nearestIdx} " +
                    $"({activeRoute.waypoints[nearestIdx].waypointId}) — nearest to player position");
            }

            Debug.Log($"[WaypointManager] Route {routeId} loaded with {activeRoute.waypoints.Count} waypoints, " +
                $"starting at index {CurrentWaypointIndex}");
        }

        /// <summary>
        /// 플레이어 위치에서 가장 가까운 웨이포인트의 인덱스를 반환합니다.
        /// 앵커 바인딩 WP는 SLAM 직접 비교, 보정 가능 시 fallback WP도 변환 후 비교.
        /// </summary>
        private int FindNearestWaypointIndex()
        {
            if (activeRoute == null || playerTransform == null) return 0;

            float minDist = float.MaxValue;
            int nearestIdx = 0;
            bool hasAnyCandidate = false;
            var playerXZ = new Vector2(playerTransform.position.x, playerTransform.position.z);

            for (int i = 0; i < activeRoute.waypoints.Count; i++)
            {
                var wp = activeRoute.waypoints[i];
                float dist;

                if (wp.anchorTransform != null)
                {
                    dist = Vector3.Distance(
                        new Vector3(playerTransform.position.x, 0, playerTransform.position.z),
                        new Vector3(wp.Position.x, 0, wp.Position.z));
                }
                else if (hasMapCalibration)
                {
                    var playerFloorPlan = SlamToFloorPlan(playerXZ);
                    var wpXZ = new Vector2(wp.fallbackPosition.x, wp.fallbackPosition.z);
                    dist = Vector2.Distance(playerFloorPlan, wpXZ);
                }
                else
                {
                    continue; // 보정 불가 → 거리 비교 불가
                }

                hasAnyCandidate = true;
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestIdx = i;
                }
            }

            if (!hasAnyCandidate)
            {
                Debug.LogWarning("[WaypointManager] No distance-comparable waypoints — using index 0");
                return 0;
            }

            Debug.Log($"[WaypointManager] Nearest waypoint: [{nearestIdx}] " +
                $"{activeRoute.waypoints[nearestIdx].waypointId} (dist={minDist:F1}m)");
            return nearestIdx;
        }

        /// <summary>
        /// 각 웨이포인트에 SpatialAnchorManager의 앵커 Transform을 바인딩합니다.
        /// 앵커가 없는 웨이포인트는 fallbackPosition을 사용 + 경고 로그.
        /// </summary>
        private void BindAnchorTransforms(string routeId)
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr == null)
            {
                Debug.LogWarning("[WaypointManager] SpatialAnchorManager.Instance가 null — 모든 웨이포인트 fallback 사용");
                return;
            }

            Debug.Log($"[WaypointManager] BindAnchorTransforms 시작 — Route={routeId}, " +
                $"IsRelocalized={anchorMgr.IsRelocalized}, " +
                $"SuccessCount={anchorMgr.SuccessfulAnchorCount}/{anchorMgr.TotalAnchorCount}");

            int boundCount = 0;
            int fallbackCount = 0;

            foreach (var wp in activeRoute.waypoints)
            {
                string anchorKey = $"{routeId}_{wp.waypointId}";
                var anchorTransform = anchorMgr.GetAnchorTransform(anchorKey);
                if (anchorTransform != null)
                {
                    wp.anchorTransform = anchorTransform;
                    boundCount++;
                    Debug.Log($"[WaypointManager] {anchorKey}: 앵커 바인딩 완료 (pos={anchorTransform.position})");
                }
                else
                {
                    fallbackCount++;
                    Debug.LogWarning($"[WaypointManager] {anchorKey}: fallback 좌표 사용 ({wp.fallbackPosition}) — SLAM 좌표계와 불일치 가능");
                    DomainEventBus.Instance?.Publish(new WaypointFallbackUsed(
                        wp.waypointId, wp.fallbackPosition.ToString()));
                }
            }

            Debug.Log($"[WaypointManager] 바인딩 결과: 앵커={boundCount}, fallback={fallbackCount}, 전체={activeRoute.waypoints.Count}");

            if (fallbackCount > 0)
            {
                Debug.LogWarning($"[WaypointManager] ⚠ {fallbackCount}개 웨이포인트 fallback 사용 — " +
                    "화살표 방향이 부정확할 수 있음. 앵커 재매핑 권장.");
            }

        }

        private void PublishRouteBindingSummary(string routeId)
        {
            if (activeRoute == null) return;

            int anchorBound = 0;
            int fallbackUsed = 0;
            var details = new System.Text.StringBuilder("[");

            for (int i = 0; i < activeRoute.waypoints.Count; i++)
            {
                var wp = activeRoute.waypoints[i];
                bool bound = wp.anchorTransform != null;
                if (bound) anchorBound++;
                else fallbackUsed++;

                if (i > 0) details.Append(",");
                details.Append($"{{\"id\":\"{wp.waypointId}\",\"bound\":{bound.ToString().ToLower()},\"pos\":\"{wp.Position:F2}\"}}");
            }
            details.Append("]");

            DomainEventBus.Instance?.Publish(new RouteBindingSummary(
                routeId, activeRoute.waypoints.Count, anchorBound, fallbackUsed, details.ToString()));
        }

        /// <summary>특정 웨이포인트가 fallback 사용 중인지 확인</summary>
        public bool IsUsingFallback(string waypointId)
        {
            if (activeRoute == null) return false;
            foreach (var wp in activeRoute.waypoints)
            {
                if (wp.waypointId == waypointId)
                    return wp.anchorTransform == null;
            }
            return false;
        }

        /// <summary>백그라운드 재인식으로 뒤늦게 앵커가 복구되었을 때</summary>
        private void OnAnchorLateRecovered(string waypointId, Transform anchorTransform)
        {
            if (activeRoute == null) return;

            foreach (var wp in activeRoute.waypoints)
            {
                string anchorKey = $"{activeRoute.routeId}_{wp.waypointId}";
                if (anchorKey == waypointId)
                {
                    Vector3 oldPos = wp.Position;
                    wp.anchorTransform = anchorTransform;
                    Vector3 newPos = wp.Position;
                    float drift = Vector3.Distance(oldPos, newPos);

                    Debug.Log($"[WaypointManager] 런타임 앵커 교체: {waypointId}, 드리프트={drift:F2}m");
                    DomainEventBus.Instance?.Publish(new WaypointLateAnchorBound(
                        waypointId, drift, oldPos.ToString(), newPos.ToString()));

                    // 앵커 2+개 확보 시 자동 heading 보정
                    if (AutoCalibrateFromAnchors())
                    {
                        headingSource = "auto";
                        Debug.Log($"[WaypointManager] Late recovery auto-cal: offset={headingCalibrationOffset:F1}° (source=auto)");
                        DomainEventBus.Instance?.Publish(new HeadingCalibrationApplied(headingSource, headingCalibrationOffset));
                    }
                    // 맵 보정 재계산 (앵커 추가로 정밀도 향상)
                    ComputeMapCalibration();
                    break;
                }
            }
        }

        /// <summary>웨이포인트까지의 XZ 평면 거리를 계산합니다. 앵커→SLAM 직접, fallback→SLAM→도면 변환.</summary>
        private float ComputeDistanceToWaypoint(Waypoint wp, out float arrivalRadius)
        {
            if (wp.anchorTransform != null)
            {
                var wpPos = wp.anchorTransform.position;
                arrivalRadius = wp.radius;
                return Vector3.Distance(
                    new Vector3(playerTransform.position.x, 0, playerTransform.position.z),
                    new Vector3(wpPos.x, 0, wpPos.z));
            }
            else if (hasMapCalibration)
            {
                var playerXZ = new Vector2(playerTransform.position.x, playerTransform.position.z);
                var playerFloorPlan = SlamToFloorPlan(playerXZ);
                var wpXZ = new Vector2(wp.fallbackPosition.x, wp.fallbackPosition.z);
                arrivalRadius = wp.radius * fallbackRadiusMultiplier;
                return Vector2.Distance(playerFloorPlan, wpXZ);
            }
            else
            {
                arrivalRadius = wp.radius;
                return float.MaxValue;
            }
        }

        /// <summary>현재 미션의 타겟 웨이포인트인지 확인합니다.</summary>
        private bool IsMissionTarget(string waypointId)
        {
            var mm = Mission.MissionManager.Instance;
            if (mm == null || mm.CurrentMission == null) return false;
            if (mm.CurrentState != Mission.MissionState.Navigation) return false;
            return mm.CurrentMission.targetWaypointId == waypointId;
        }

        /// <summary>
        /// 현재 WP 이후의 모든 WP를 스캔하여, 이미 반경 내에 들어온 WP로 건너뜁니다.
        /// 미션 타겟 WP를 우선, 아니면 반경 내 가장 가까운 WP로 건너뜁니다.
        /// </summary>
        private void TryLookAheadReach()
        {
            int targetIdx = -1;
            int nearestReachedIdx = -1;
            float nearestReachedDist = float.MaxValue;

            for (int i = CurrentWaypointIndex + 1; i < activeRoute.waypoints.Count; i++)
            {
                var wp = activeRoute.waypoints[i];
                float dist = ComputeDistanceToWaypoint(wp, out float radius);
                if (dist > radius) continue;

                if (IsMissionTarget(wp.waypointId))
                {
                    targetIdx = i;
                    break;
                }
                if (dist < nearestReachedDist)
                {
                    nearestReachedIdx = i;
                    nearestReachedDist = dist;
                }
            }

            int skipToIdx = targetIdx >= 0 ? targetIdx : nearestReachedIdx;
            if (skipToIdx < 0) return;

            Debug.Log($"[WaypointManager] Look-ahead skip: index {CurrentWaypointIndex}→{skipToIdx} " +
                $"({activeRoute.waypoints[skipToIdx].waypointId})");

            // 건너뛴 WP들은 도메인 이벤트만 발행 (MissionManager에는 전달하지 않음)
            for (int i = CurrentWaypointIndex; i < skipToIdx; i++)
            {
                var skipped = activeRoute.waypoints[i];
                DomainEventBus.Instance?.Publish(new WaypointFallbackUsed(
                    skipped.waypointId, "look_ahead_skip"));
            }

            CurrentWaypointIndex = skipToIdx;
            ReachWaypoint(activeRoute.waypoints[skipToIdx]);
        }

        private void Update()
        {
            if (activeRoute == null || playerTransform == null) return;
            if (CurrentWaypointIndex >= activeRoute.waypoints.Count) return;

            var wp = activeRoute.waypoints[CurrentWaypointIndex];
            float dist = ComputeDistanceToWaypoint(wp, out float arrivalRadius);

            Debug.Log($"[ArrivalCheck] wp={wp.waypointId}, dist={dist:F2}, radius={arrivalRadius:F2}, " +
                $"anchor={(wp.anchorTransform != null)}, heading={headingCalibrationOffset:F1}, " +
                $"source={headingSource}, calib={calibrationSource}");

            if (dist <= arrivalRadius)
            {
                ReachWaypoint(wp);
                return;
            }

            // 현재 WP 미도달 — 이후 WP 중 이미 도달한 것이 있는지 스캔
            TryLookAheadReach();

            // 5초 주기 진단 스냅샷 (보정 상태 + 거리 정보)
            if (Time.time - lastSnapshotTime >= 5f)
            {
                lastSnapshotTime = Time.time;
                bool arrowVisible = cachedArrowRenderer != null && cachedArrowRenderer.IsVisible;
                string routeId = activeRoute?.routeId ?? "";
                string condition = Core.ExperimentManager.Instance?.ActiveCondition ?? "";

                DomainEventBus.Instance?.Publish(new NavigationStateSnapshot(
                    wp.waypointId,
                    wp.anchorTransform != null,
                    playerTransform.position.ToString("F2"),
                    wp.Position.ToString("F2"),
                    dist,
                    arrowVisible,
                    routeId,
                    condition,
                    hasMapCalibration,
                    calibrationSource));
            }
        }

        private void ReachWaypoint(Waypoint wp)
        {
            bool isTarget = IsMissionTarget(wp.waypointId);
            DomainEventBus.Instance?.Publish(new WaypointReached(wp.waypointId, isTarget));
            OnWaypointReached?.Invoke(wp);
            Debug.Log($"[WaypointManager] Reached {wp.waypointId} ({wp.locationName})" +
                $"{(isTarget ? " [TARGET]" : "")}");

            CurrentWaypointIndex++;
            if (CurrentWaypointIndex >= activeRoute.waypoints.Count)
            {
                OnRouteComplete?.Invoke();
                Debug.Log("[WaypointManager] Route complete");
            }
        }

        public Vector3 GetDirectionToNext()
        {
            if (playerTransform == null || CurrentWaypoint == null)
                return Vector3.forward;

            var target = CurrentWaypointIndex < activeRoute.waypoints.Count
                ? activeRoute.waypoints[CurrentWaypointIndex]
                : activeRoute.waypoints[activeRoute.waypoints.Count - 1];

            if (target.anchorTransform != null)
            {
                // 앵커 바인딩 있음 → 정확한 SLAM 좌표 기반 방향
                return (target.Position - playerTransform.position).normalized;
            }
            else
            {
                // Fallback: 절대 좌표 대신 이전 WP→현재 WP 의 상대 방향만 사용.
                // fallback 좌표는 SLAM 좌표계와 무관하므로,
                // playerTransform.position과 직접 연산하면 90° 이상 오차 발생.
                Vector3 from;
                if (CurrentWaypointIndex > 0)
                {
                    from = activeRoute.waypoints[CurrentWaypointIndex - 1].fallbackPosition;
                }
                else
                {
                    // 첫 웨이포인트: 경로 시작점(origin) 기준
                    from = Vector3.zero;
                }
                Vector3 to = target.fallbackPosition;
                Vector3 relativeDir = to - from;
                relativeDir.y = 0f; // 수평 방향만
                if (relativeDir.sqrMagnitude < 0.001f)
                    return Vector3.forward;
                // Heading 보정 오프셋 적용 (도면→SLAM 좌표계 회전)
                relativeDir = Quaternion.Euler(0, headingCalibrationOffset, 0) * relativeDir;
                return relativeDir.normalized;
            }
        }

        /// <summary>웨이포인트 ID로 현재 경로에서 위치를 조회한다.</summary>
        public Vector3? GetWaypointPosition(string waypointId)
        {
            if (activeRoute == null) return null;
            foreach (var wp in activeRoute.waypoints)
                if (wp.waypointId == waypointId) return wp.Position;
            return null;
        }

        public float GetDistanceToNext()
        {
            if (playerTransform == null || activeRoute == null || CurrentWaypointIndex >= activeRoute.waypoints.Count)
                return 0f;

            var wp = activeRoute.waypoints[CurrentWaypointIndex];
            if (wp.anchorTransform != null)
            {
                return Vector3.Distance(playerTransform.position, wp.anchorTransform.position);
            }
            else if (hasMapCalibration)
            {
                var playerXZ = new Vector2(playerTransform.position.x, playerTransform.position.z);
                var playerFloorPlan = SlamToFloorPlan(playerXZ);
                var wpXZ = new Vector2(wp.fallbackPosition.x, wp.fallbackPosition.z);
                return Vector2.Distance(playerFloorPlan, wpXZ);
            }
            else
            {
                return -1f; // 보정 불가
            }
        }

        /// <summary>
        /// 앵커 바인딩된 웨이포인트 쌍으로 SLAM→도면 좌표 변환(회전+평행이동)을 계산합니다.
        /// 0개: 보정 불가, 1개: heading+단일점 평행이동, 2개+: 최소자승법.
        /// </summary>
        private void ComputeMapCalibration()
        {
            var pairs = GetAllCalibratedAnchorPairs();
            if (pairs.Count == 0)
            {
                // 앵커 0개 → 카메라 시작 위치를 시작 WP의 fallback 좌표에 매핑
                if (playerTransform == null || activeRoute == null || activeRoute.waypoints.Count == 0)
                {
                    hasMapCalibration = false;
                    calibrationSource = "none";
                    Debug.LogWarning("[WaypointManager] Zero-anchor calibration failed: playerTransform or route unavailable");
                    return;
                }

                // WP01(실제 시작점)이 있으면 사용, 아니면 WP00
                int startIdx = activeRoute.waypoints.Count > 1 ? 1 : 0;
                var startWP = activeRoute.waypoints[startIdx];
                var cameraXZ = new Vector2(playerTransform.position.x, playerTransform.position.z);
                var fallbackXZ = new Vector2(startWP.fallbackPosition.x, startWP.fallbackPosition.z);

                float theta = -headingCalibrationOffset * Mathf.Deg2Rad;
                mapCalibCos = Mathf.Cos(theta);
                mapCalibSin = Mathf.Sin(theta);
                float rotX = cameraXZ.x * mapCalibCos - cameraXZ.y * mapCalibSin;
                float rotY = cameraXZ.x * mapCalibSin + cameraXZ.y * mapCalibCos;
                mapCalibOffset = fallbackXZ - new Vector2(rotX, rotY);

                hasMapCalibration = true;
                calibrationSource = "auto_zero_anchor";
                Debug.Log($"[WaypointManager] Zero-anchor auto-calibration: camera({cameraXZ}) → " +
                    $"WP{startIdx}({fallbackXZ}), heading={headingCalibrationOffset:F1}°");
                return;
            }

            if (pairs.Count == 1)
            {
                // heading 오프셋으로 회전 + 단일 앵커로 평행이동
                float theta = -headingCalibrationOffset * Mathf.Deg2Rad;
                mapCalibCos = Mathf.Cos(theta);
                mapCalibSin = Mathf.Sin(theta);

                var (slamXZ, fallbackXZ) = pairs[0];
                float rotX = slamXZ.x * mapCalibCos - slamXZ.y * mapCalibSin;
                float rotY = slamXZ.x * mapCalibSin + slamXZ.y * mapCalibCos;
                mapCalibOffset = fallbackXZ - new Vector2(rotX, rotY);
                calibrationSource = "anchor_1";
            }
            else
            {
                // 최소자승법 (InteractiveMapController.ComputeLeastSquaresTransform과 동일)
                int n = pairs.Count;
                Vector2 centroidS = Vector2.zero, centroidF = Vector2.zero;
                for (int i = 0; i < n; i++)
                {
                    centroidS += pairs[i].slamXZ;
                    centroidF += pairs[i].fallbackXZ;
                }
                centroidS /= n;
                centroidF /= n;

                float sumCross = 0f, sumDot = 0f;
                for (int i = 0; i < n; i++)
                {
                    var ds = pairs[i].slamXZ - centroidS;
                    var df = pairs[i].fallbackXZ - centroidF;
                    sumCross += ds.x * df.y - ds.y * df.x;
                    sumDot += ds.x * df.x + ds.y * df.y;
                }

                float theta = Mathf.Atan2(sumCross, sumDot);
                mapCalibCos = Mathf.Cos(theta);
                mapCalibSin = Mathf.Sin(theta);

                var rotatedCentroidS = new Vector2(
                    centroidS.x * mapCalibCos - centroidS.y * mapCalibSin,
                    centroidS.x * mapCalibSin + centroidS.y * mapCalibCos);
                mapCalibOffset = centroidF - rotatedCentroidS;
                calibrationSource = $"anchor_{n}";
            }

            hasMapCalibration = true;
            Debug.Log($"[WaypointManager] MapCalibration computed: cos={mapCalibCos:F3}, sin={mapCalibSin:F3}, " +
                $"offset={mapCalibOffset}, source={calibrationSource}, pairs={pairs.Count}");
        }

        /// <summary>SLAM XZ 좌표를 도면 XZ 좌표로 변환합니다.</summary>
        private Vector2 SlamToFloorPlan(Vector2 slamXZ)
        {
            float rx = slamXZ.x * mapCalibCos - slamXZ.y * mapCalibSin;
            float ry = slamXZ.x * mapCalibSin + slamXZ.y * mapCalibCos;
            return new Vector2(rx, ry) + mapCalibOffset;
        }

        /// <summary>persistHeadingAcrossSessions가 true일 때만 heading을 PlayerPrefs에 저장합니다.</summary>
        private void SaveHeadingIfAllowed()
        {
            if (!persistHeadingAcrossSessions) return;
            PlayerPrefs.SetFloat("ARNav_HeadingOffset", headingCalibrationOffset);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 앵커 2+개의 (fallbackPos, anchorPos) 쌍으로부터 yaw 오프셋을 자동 계산합니다.
        /// BindAnchorTransforms() 및 OnAnchorLateRecovered()에서 호출됩니다.
        /// </summary>
        private bool AutoCalibrateFromAnchors()
        {
            if (activeRoute == null) return false;

            // 앵커 바인딩된 웨이포인트 쌍 수집
            var anchored = new List<(Vector3 fallback, Vector3 slam)>();
            foreach (var wp in activeRoute.waypoints)
            {
                if (wp.anchorTransform != null)
                    anchored.Add((wp.fallbackPosition, wp.anchorTransform.position));
            }
            if (anchored.Count < 2) return false;

            // 가장 먼 쌍을 선택 (정밀도 향상 — 가까운 쌍은 노이즈에 취약)
            float maxDist = 0f;
            int bestI = 0, bestJ = 1;
            for (int i = 0; i < anchored.Count; i++)
                for (int j = i + 1; j < anchored.Count; j++)
                {
                    float d = Vector3.Distance(anchored[i].slam, anchored[j].slam);
                    if (d > maxDist) { maxDist = d; bestI = i; bestJ = j; }
                }

            // SLAM 벡터와 도면 벡터 비교
            Vector3 slamDir = anchored[bestJ].slam - anchored[bestI].slam;
            Vector3 mapDir = anchored[bestJ].fallback - anchored[bestI].fallback;
            slamDir.y = 0; mapDir.y = 0;
            if (slamDir.sqrMagnitude < 0.01f || mapDir.sqrMagnitude < 0.01f) return false;

            float slamYaw = Mathf.Atan2(slamDir.x, slamDir.z) * Mathf.Rad2Deg;
            float mapYaw = Mathf.Atan2(mapDir.x, mapDir.z) * Mathf.Rad2Deg;
            headingCalibrationOffset = slamYaw - mapYaw;

            SaveHeadingIfAllowed();
            Debug.Log($"[WaypointManager] Auto-calibrated heading: offset={headingCalibrationOffset:F1}° " +
                $"(slam={slamYaw:F1}°, map={mapYaw:F1}°, dist={maxDist:F1}m, pairs={anchored.Count})");
            return true;
        }

        /// <summary>앵커 바인딩된 웨이포인트 1개의 (SLAM xz, fallback xz) 쌍을 반환합니다.</summary>
        public (Vector2 slamXZ, Vector2 fallbackXZ)? GetCalibratedAnchorPair()
        {
            if (activeRoute == null) return null;
            foreach (var wp in activeRoute.waypoints)
            {
                if (wp.anchorTransform != null)
                {
                    var slam = wp.anchorTransform.position;
                    var fb = wp.fallbackPosition;
                    return (new Vector2(slam.x, slam.z), new Vector2(fb.x, fb.z));
                }
            }
            return null;
        }

        /// <summary>앵커 바인딩된 모든 웨이포인트의 (SLAM xz, fallback xz) 쌍 목록을 반환합니다.
        /// Image Tracking에서 주입된 쌍도 포함합니다.</summary>
        public List<(Vector2 slamXZ, Vector2 fallbackXZ)> GetAllCalibratedAnchorPairs()
        {
            var pairs = new List<(Vector2 slamXZ, Vector2 fallbackXZ)>();
            if (activeRoute == null) return pairs;
            foreach (var wp in activeRoute.waypoints)
            {
                if (wp.anchorTransform != null)
                {
                    var slam = wp.anchorTransform.position;
                    var fb = wp.fallbackPosition;
                    pairs.Add((new Vector2(slam.x, slam.z), new Vector2(fb.x, fb.z)));
                }
            }

            // Image Tracking에서 주입된 가상 앵커 쌍 추가
            if (injectedPairs != null)
                pairs.AddRange(injectedPairs);

            return pairs;
        }

        /// <summary>heading 오프셋을 delta 도만큼 미세 조정합니다.</summary>
        public void AdjustHeadingOffset(float delta)
        {
            headingCalibrationOffset += delta;
            SaveHeadingIfAllowed();

            Debug.Log($"[WaypointManager] Heading adjusted: offset={headingCalibrationOffset:F1}° (delta={delta:+0.0;-0.0}°)");
        }

        /// <summary>
        /// 현재 카메라 위치를 현재 목표 WP의 fallback 좌표에 수동 매핑합니다.
        /// 1회차: 1점 보정 즉시 적용 + 1번 점 기록.
        /// 2회차(다른 WP에서): 2점으로 heading까지 자동 계산.
        /// </summary>
        public void ManualCalibrateAtCurrentWaypoint()
        {
            if (activeRoute == null || playerTransform == null) return;
            if (CurrentWaypointIndex >= activeRoute.waypoints.Count) return;

            var wp = activeRoute.waypoints[CurrentWaypointIndex];
            var cameraXZ = new Vector2(playerTransform.position.x, playerTransform.position.z);
            var fallbackXZ = new Vector2(wp.fallbackPosition.x, wp.fallbackPosition.z);

            if (!manualCalibFirstPointRecorded)
            {
                // 1회차: 1번 점 기록
                manualCalibSlamXZ_1 = cameraXZ;
                manualCalibFallbackXZ_1 = fallbackXZ;
                manualCalibFirstPointRecorded = true;

                // 즉시 1점 보정도 적용 (거리 표시가 바로 동작하도록)
                ApplySinglePointCalibration(cameraXZ, fallbackXZ, wp.waypointId);

                Debug.Log($"[WaypointManager] Manual calibration point 1 at {wp.waypointId}: " +
                    $"slam({cameraXZ}), fallback({fallbackXZ}) — walk to next WP and press again for heading");
                return;
            }

            // 2회차: 2번 점에서 heading 계산
            manualCalibFirstPointRecorded = false;

            Vector2 slamDelta = cameraXZ - manualCalibSlamXZ_1;
            Vector2 floorDelta = fallbackXZ - manualCalibFallbackXZ_1;

            if (slamDelta.sqrMagnitude < 0.5f || floorDelta.sqrMagnitude < 0.5f)
            {
                Debug.LogWarning("[WaypointManager] Manual calibration: insufficient movement — single-point only");
                ApplySinglePointCalibration(cameraXZ, fallbackXZ, wp.waypointId);
                return;
            }

            // AutoCalibrateFromAnchors와 동일한 atan2(x, z) yaw 공식
            float slamYaw = Mathf.Atan2(slamDelta.x, slamDelta.y) * Mathf.Rad2Deg;
            float floorYaw = Mathf.Atan2(floorDelta.x, floorDelta.y) * Mathf.Rad2Deg;
            headingCalibrationOffset = slamYaw - floorYaw;
            headingSource = "manual_2pt";

            SaveHeadingIfAllowed();

            // heading 변경 후 맵 보정 재계산
            ComputeMapCalibration();

            Debug.Log($"[WaypointManager] Manual 2-point heading: {headingCalibrationOffset:F1}° " +
                $"(slamYaw={slamYaw:F1}°, floorYaw={floorYaw:F1}°)");
            DomainEventBus.Instance?.Publish(new HeadingCalibrationApplied("manual_2pt", headingCalibrationOffset));
            DomainEventBus.Instance?.Publish(new ManualCalibrationApplied(
                wp.waypointId, cameraXZ.ToString(), fallbackXZ.ToString(), headingCalibrationOffset));
        }

        private void ApplySinglePointCalibration(Vector2 cameraXZ, Vector2 fallbackXZ, string wpId)
        {
            float theta = -headingCalibrationOffset * Mathf.Deg2Rad;
            mapCalibCos = Mathf.Cos(theta);
            mapCalibSin = Mathf.Sin(theta);
            float rotX = cameraXZ.x * mapCalibCos - cameraXZ.y * mapCalibSin;
            float rotY = cameraXZ.x * mapCalibSin + cameraXZ.y * mapCalibCos;
            mapCalibOffset = fallbackXZ - new Vector2(rotX, rotY);

            hasMapCalibration = true;
            calibrationSource = "manual";

            DomainEventBus.Instance?.Publish(new ManualCalibrationApplied(
                wpId, cameraXZ.ToString(), fallbackXZ.ToString(), headingCalibrationOffset));
            Debug.Log($"[WaypointManager] Single-point manual calibration at {wpId}: " +
                $"camera({cameraXZ}) → fallback({fallbackXZ}), heading={headingCalibrationOffset:F1}°");
        }

        /// <summary>
        /// Image Tracking에서 감지된 마커 쌍을 주입하여 맵 보정을 갱신합니다.
        /// 기존 앵커 쌍에 추가되어 ComputeMapCalibration()에서 함께 처리됩니다.
        /// </summary>
        public void InjectCalibratedPairs(List<(Vector2 slamXZ, Vector2 fallbackXZ)> pairs)
        {
            injectedPairs = pairs;
            calibrationSource = "injected";
            ComputeMapCalibration();
        }

        /// <summary>
        /// Heading offset을 외부에서 직접 설정합니다 (Image Tracking 마커 방향 기반).
        /// </summary>
        public void SetHeadingCalibrationOffset(float offset)
        {
            headingCalibrationOffset = offset;
            headingSource = "image_tracking";
            SaveHeadingIfAllowed();
            Debug.Log($"[WaypointManager] Heading set by Image Tracking: {offset:F1}°");
        }
    }
}

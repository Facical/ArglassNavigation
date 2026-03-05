using System;
using System.Collections.Generic;
using UnityEngine;
using ARNavExperiment.Core;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;

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
        [SerializeField] private RouteData routeA;
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

        private RouteData activeRoute;
        private Transform playerTransform;

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
            activeRoute = routeId == "A" ? routeA : routeB;
            CurrentWaypointIndex = 0;

            if (Camera.main != null)
                playerTransform = Camera.main.transform;

            // SpatialAnchorManager에서 앵커 Transform 바인딩
            BindAnchorTransforms(routeId);

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
        /// 앵커가 바인딩된 웨이포인트만 고려합니다 (fallback 좌표는 SLAM과 무관).
        /// </summary>
        private int FindNearestWaypointIndex()
        {
            if (activeRoute == null || playerTransform == null) return 0;

            float minDist = float.MaxValue;
            int nearestIdx = 0;
            bool hasAnyAnchor = false;

            for (int i = 0; i < activeRoute.waypoints.Count; i++)
            {
                var wp = activeRoute.waypoints[i];
                // 앵커 바인딩된 웨이포인트만 거리 계산 (fallback은 SLAM 좌표계와 무관)
                if (wp.anchorTransform == null) continue;

                hasAnyAnchor = true;
                float dist = Vector3.Distance(
                    new Vector3(playerTransform.position.x, 0, playerTransform.position.z),
                    new Vector3(wp.Position.x, 0, wp.Position.z));

                if (dist < minDist)
                {
                    minDist = dist;
                    nearestIdx = i;
                }
            }

            if (!hasAnyAnchor)
            {
                Debug.LogWarning("[WaypointManager] No anchored waypoints — using index 0");
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
                var anchorTransform = anchorMgr.GetAnchorTransform(wp.waypointId);
                if (anchorTransform != null)
                {
                    wp.anchorTransform = anchorTransform;
                    boundCount++;
                    Debug.Log($"[WaypointManager] {wp.waypointId}: 앵커 바인딩 완료 (pos={anchorTransform.position})");
                }
                else
                {
                    fallbackCount++;
                    Debug.LogWarning($"[WaypointManager] {wp.waypointId}: fallback 좌표 사용 ({wp.fallbackPosition}) — SLAM 좌표계와 불일치 가능");
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
                if (wp.waypointId == waypointId)
                {
                    Vector3 oldPos = wp.Position;
                    wp.anchorTransform = anchorTransform;
                    Vector3 newPos = wp.Position;
                    float drift = Vector3.Distance(oldPos, newPos);

                    Debug.Log($"[WaypointManager] 런타임 앵커 교체: {waypointId}, 드리프트={drift:F2}m");
                    DomainEventBus.Instance?.Publish(new WaypointLateAnchorBound(
                        waypointId, drift, oldPos.ToString(), newPos.ToString()));
                    break;
                }
            }
        }

        private void Update()
        {
            if (activeRoute == null || playerTransform == null) return;
            if (CurrentWaypointIndex >= activeRoute.waypoints.Count) return;

            var wp = activeRoute.waypoints[CurrentWaypointIndex];
            var wpPos = wp.Position;
            float dist = Vector3.Distance(
                new Vector3(playerTransform.position.x, 0, playerTransform.position.z),
                new Vector3(wpPos.x, 0, wpPos.z));

            if (dist <= wp.radius)
            {
                ReachWaypoint(wp);
            }
        }

        private void ReachWaypoint(Waypoint wp)
        {
            DomainEventBus.Instance?.Publish(new WaypointReached(wp.waypointId, false));
            OnWaypointReached?.Invoke(wp);
            Debug.Log($"[WaypointManager] Reached {wp.waypointId} ({wp.locationName})");

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
            if (playerTransform == null || CurrentWaypointIndex >= activeRoute.waypoints.Count)
                return 0f;
            return Vector3.Distance(playerTransform.position, activeRoute.waypoints[CurrentWaypointIndex].Position);
        }
    }
}

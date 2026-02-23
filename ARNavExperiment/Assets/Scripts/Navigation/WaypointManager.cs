using System;
using System.Collections.Generic;
using UnityEngine;
using ARNavExperiment.Core;
using ARNavExperiment.Logging;

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

            Debug.Log($"[WaypointManager] Route {routeId} loaded with {activeRoute.waypoints.Count} waypoints");
        }

        /// <summary>
        /// 각 웨이포인트에 SpatialAnchorManager의 앵커 Transform을 바인딩합니다.
        /// 앵커가 없는 웨이포인트는 fallbackPosition을 사용 + 경고 로그.
        /// </summary>
        private void BindAnchorTransforms(string routeId)
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr == null || !anchorMgr.IsRelocalized) return;

            int boundCount = 0;
            int fallbackCount = 0;

            foreach (var wp in activeRoute.waypoints)
            {
                var anchorTransform = anchorMgr.GetAnchorTransform(wp.waypointId);
                if (anchorTransform != null)
                {
                    wp.anchorTransform = anchorTransform;
                    boundCount++;
                    Debug.Log($"[WaypointManager] {wp.waypointId}: 앵커 바인딩 완료");
                }
                else
                {
                    fallbackCount++;
                    Debug.LogWarning($"[WaypointManager] {wp.waypointId}: fallback 좌표 사용 ({wp.fallbackPosition}) — 수 미터 오차 가능");
                    EventLogger.Instance?.LogEvent("WAYPOINT_FALLBACK_USED",
                        waypointId: wp.waypointId,
                        extraData: $"{{\"fallback_position\":\"{wp.fallbackPosition}\"}}");
                }
            }

            Debug.Log($"[WaypointManager] 바인딩 결과: 앵커={boundCount}, fallback={fallbackCount}, 전체={activeRoute.waypoints.Count}");
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
                    EventLogger.Instance?.LogEvent("WAYPOINT_LATE_ANCHOR_BOUND",
                        waypointId: waypointId,
                        extraData: $"{{\"drift_m\":{drift:F2},\"old_pos\":\"{oldPos}\",\"new_pos\":\"{newPos}\"}}");
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
            EventLogger.Instance?.LogEvent("WAYPOINT_REACHED", waypointId: wp.waypointId);
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

            return (target.Position - playerTransform.position).normalized;
        }

        public float GetDistanceToNext()
        {
            if (playerTransform == null || CurrentWaypointIndex >= activeRoute.waypoints.Count)
                return 0f;
            return Vector3.Distance(playerTransform.position, activeRoute.waypoints[CurrentWaypointIndex].Position);
        }
    }
}

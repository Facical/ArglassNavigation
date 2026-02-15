using System.Collections.Generic;
using UnityEngine;
using ARNavExperiment.Logging;

namespace ARNavExperiment.Navigation
{
    [System.Serializable]
    public class Waypoint
    {
        public string waypointId;
        public Vector3 position;
        public float radius = 2f;
        public string locationName;
    }

    [System.Serializable]
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

        private RouteData activeRoute;
        private Transform playerTransform;

        public event System.Action<Waypoint> OnWaypointReached;
        public event System.Action OnRouteComplete;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void LoadRoute(string routeId)
        {
            activeRoute = routeId == "A" ? routeA : routeB;
            CurrentWaypointIndex = 0;

            if (Camera.main != null)
                playerTransform = Camera.main.transform;

            Debug.Log($"[WaypointManager] Route {routeId} loaded with {activeRoute.waypoints.Count} waypoints");
        }

        private void Update()
        {
            if (activeRoute == null || playerTransform == null) return;
            if (CurrentWaypointIndex >= activeRoute.waypoints.Count) return;

            var wp = activeRoute.waypoints[CurrentWaypointIndex];
            float dist = Vector3.Distance(
                new Vector3(playerTransform.position.x, 0, playerTransform.position.z),
                new Vector3(wp.position.x, 0, wp.position.z));

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

            return (target.position - playerTransform.position).normalized;
        }

        public float GetDistanceToNext()
        {
            if (playerTransform == null || CurrentWaypointIndex >= activeRoute.waypoints.Count)
                return 0f;
            return Vector3.Distance(playerTransform.position, activeRoute.waypoints[CurrentWaypointIndex].position);
        }
    }
}

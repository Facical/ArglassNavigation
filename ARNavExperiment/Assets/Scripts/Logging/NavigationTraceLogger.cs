using System;
using UnityEngine;
using ARNavExperiment.Utils;
using ARNavExperiment.Navigation;
using ARNavExperiment.Mission;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;

namespace ARNavExperiment.Logging
{
    /// <summary>
    /// 2Hz 위치/내비 상태 trace 로거 + 이동 정지 감지.
    /// </summary>
    public class NavigationTraceLogger : MonoBehaviour
    {
        public static NavigationTraceLogger Instance { get; private set; }

        private CSVWriter writer;
        [SerializeField] private float sampleRate = 2f;
        private float nextSampleTime;
        private Vector3 previousPosition;
        private bool hasPreviousPosition;

        // 정지 감지
        [SerializeField] private float pauseSpeedThreshold = 0.15f;
        [SerializeField] private float pauseMinDuration = 2f;
        private float lowSpeedStartTime;
        private bool isPaused;

        private static readonly string[] Headers = {
            "timestamp", "session_id", "mission_id",
            "current_wp_index", "current_wp_id", "target_wp_id",
            "player_x", "player_y", "player_z",
            "target_x", "target_y", "target_z",
            "distance_m", "speed_ms",
            "anchor_bound", "is_fallback", "has_map_calib", "calib_source",
            "heading_offset_deg", "arrow_visible", "beam_on", "trigger_id"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void StartLogging()
        {
            var el = EventLogger.Instance;
            if (el == null || string.IsNullOrEmpty(el.SessionFilePrefix)) return;

            string path = System.IO.Path.Combine(el.SessionDirectory,
                $"{el.SessionFilePrefix}_nav_trace.csv");
            writer = new CSVWriter(path, Headers);
            hasPreviousPosition = false;
            isPaused = false;
            Debug.Log($"[NavigationTraceLogger] Started: {path}");
        }

        private void Update()
        {
            if (writer == null || Time.time < nextSampleTime) return;
            float dt = 1f / sampleRate;
            nextSampleTime = Time.time + dt;

            var wm = WaypointManager.Instance;
            if (wm == null) return;

            var ht = HeadTracker.Instance;
            Vector3 playerPos = ht != null ? ht.CurrentPosition : Vector3.zero;

            float speed = 0f;
            if (hasPreviousPosition)
            {
                speed = Vector3.Distance(playerPos, previousPosition) / dt;
            }
            previousPosition = playerPos;
            hasPreviousPosition = true;

            // 정지 감지
            UpdatePauseDetection(speed, wm, playerPos);

            var currentWP = wm.CurrentWaypoint;
            if (currentWP == null) return;

            string missionId = MissionManager.Instance?.CurrentMission?.missionId ?? "";
            Vector3 targetPos = currentWP.Position;
            // 좌표계 일치 거리 계산: SLAM→도면 변환 후 비교 (fallback 모드 대응)
            float distance = wm.GetDistanceToNext();
            bool anchorBound = currentWP.anchorTransform != null;
            bool isFallback = !anchorBound;
            bool hasMapCalib = wm.HasMapCalibration;
            string calibSource = wm.CalibrationSource ?? "none";
            float headingOffset = wm.HeadingCalibrationOffset;

            bool arrowVisible = false;
            var arrowRenderer = GetArrowRenderer();
            if (arrowRenderer != null) arrowVisible = arrowRenderer.IsVisible;

            bool beamOn = DeviceStateTracker.Instance?.IsBeamProActive ?? false;
            string triggerId = TriggerController.Instance?.ActiveTriggerId ?? "";

            // target WP (미션 타겟)
            string targetWpId = MissionManager.Instance?.CurrentMission?.targetWaypointId ?? "";

            writer.WriteRow(
                DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                EventLogger.Instance?.ParticipantId ?? "",
                missionId,
                wm.CurrentWaypointIndex.ToString(),
                currentWP.waypointId ?? "",
                targetWpId,
                playerPos.x.ToString("F2"),
                playerPos.y.ToString("F2"),
                playerPos.z.ToString("F2"),
                targetPos.x.ToString("F2"),
                targetPos.y.ToString("F2"),
                targetPos.z.ToString("F2"),
                distance.ToString("F2"),
                speed.ToString("F2"),
                anchorBound.ToString().ToLower(),
                isFallback.ToString().ToLower(),
                hasMapCalib.ToString().ToLower(),
                calibSource,
                headingOffset.ToString("F1"),
                arrowVisible.ToString().ToLower(),
                beamOn.ToString().ToLower(),
                triggerId
            );
        }

        private void UpdatePauseDetection(float speed, WaypointManager wm, Vector3 playerPos)
        {
            if (speed < pauseSpeedThreshold)
            {
                if (!isPaused)
                {
                    if (lowSpeedStartTime == 0f)
                        lowSpeedStartTime = Time.time;
                    else if (Time.time - lowSpeedStartTime >= pauseMinDuration)
                    {
                        isPaused = true;
                        string wpId = wm.CurrentWaypoint?.waypointId ?? "";
                        DomainEventBus.Instance?.Publish(new MovementPaused(
                            wpId, playerPos.ToString("F2")));
                    }
                }
            }
            else
            {
                if (isPaused)
                {
                    float pauseDuration = Time.time - lowSpeedStartTime;
                    string wpId = wm.CurrentWaypoint?.waypointId ?? "";
                    DomainEventBus.Instance?.Publish(new MovementResumed(
                        wpId, pauseDuration, playerPos.ToString("F2")));
                }
                isPaused = false;
                lowSpeedStartTime = 0f;
            }
        }

        private ARArrowRenderer cachedArrow;
        private ARArrowRenderer GetArrowRenderer()
        {
            if (cachedArrow == null)
                cachedArrow = FindObjectOfType<ARArrowRenderer>();
            return cachedArrow;
        }

        public void StopLogging()
        {
            writer?.Dispose();
            writer = null;
        }

        private void OnDestroy() => writer?.Dispose();
    }
}

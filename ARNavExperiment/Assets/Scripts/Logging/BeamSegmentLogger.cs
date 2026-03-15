using System;
using UnityEngine;
using ARNavExperiment.Utils;
using ARNavExperiment.Navigation;
using ARNavExperiment.Mission;

namespace ARNavExperiment.Logging
{
    /// <summary>
    /// Beam Pro 사용 세그먼트 로거.
    /// ON→OFF 세그먼트 완료 시 한 행 기록.
    /// ObservationService에서 호출.
    /// </summary>
    public class BeamSegmentLogger : MonoBehaviour
    {
        public static BeamSegmentLogger Instance { get; private set; }

        private CSVWriter writer;
        private int segmentId;

        // 현재 세그먼트 추적
        private bool segmentActive;
        private float segmentStartTime;
        private string segmentMissionId;
        private string segmentStartWp;
        private string segmentActiveTrigger;
        private string primaryTab;
        private int poiViewCount;
        private int infoCardOpenCount;
        private int comparisonCount;
        private int missionRefCount;
        private int zoomCount;
        private float mapViewStartTime;
        private float mapViewDuration;
        private bool isOnMapTab;

        private static readonly string[] Headers = {
            "segment_id", "on_ts", "off_ts", "duration_s",
            "mission_id", "start_wp", "end_wp", "trigger_active",
            "primary_tab", "poi_view_count", "info_card_open_count",
            "comparison_count", "mission_ref_count", "zoom_count",
            "map_view_duration_s"
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
                $"{el.SessionFilePrefix}_beam_segments.csv");
            writer = new CSVWriter(path, Headers);
            segmentId = 0;
            segmentActive = false;
            Debug.Log($"[BeamSegmentLogger] Started: {path}");
        }

        public void OnBeamScreenOn()
        {
            if (writer == null) return;

            segmentActive = true;
            segmentId++;
            segmentStartTime = Time.time;
            segmentMissionId = MissionManager.Instance?.CurrentMission?.missionId ?? "";
            segmentStartWp = WaypointManager.Instance?.CurrentWaypoint?.waypointId ?? "";
            segmentActiveTrigger = TriggerController.Instance?.ActiveTriggerId ?? "";
            primaryTab = "";
            poiViewCount = 0;
            infoCardOpenCount = 0;
            comparisonCount = 0;
            missionRefCount = 0;
            zoomCount = 0;
            mapViewDuration = 0f;
            isOnMapTab = false;
        }

        public void OnBeamScreenOff()
        {
            if (writer == null || !segmentActive) return;

            if (isOnMapTab)
                mapViewDuration += Time.time - mapViewStartTime;

            string endWp = WaypointManager.Instance?.CurrentWaypoint?.waypointId ?? "";
            float duration = Time.time - segmentStartTime;
            string onTs = DateTime.Now.AddSeconds(-duration).ToString("yyyy-MM-ddTHH:mm:ss.fff");
            string offTs = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff");

            writer.WriteRow(
                segmentId.ToString(),
                onTs,
                offTs,
                duration.ToString("F1"),
                segmentMissionId,
                segmentStartWp,
                endWp,
                segmentActiveTrigger,
                primaryTab,
                poiViewCount.ToString(),
                infoCardOpenCount.ToString(),
                comparisonCount.ToString(),
                missionRefCount.ToString(),
                zoomCount.ToString(),
                mapViewDuration.ToString("F1")
            );

            segmentActive = false;
        }

        public void OnTabSwitch(string tabName)
        {
            if (!segmentActive) return;

            // 맵 탭 시간 추적
            if (isOnMapTab)
                mapViewDuration += Time.time - mapViewStartTime;

            isOnMapTab = tabName.ToLower().Contains("map");
            if (isOnMapTab)
                mapViewStartTime = Time.time;

            // 첫 번째 탭 전환이 primary
            if (string.IsNullOrEmpty(primaryTab))
                primaryTab = tabName;
        }

        public void OnPOIViewed() { if (segmentActive) poiViewCount++; }
        public void OnInfoCardOpened() { if (segmentActive) infoCardOpenCount++; }
        public void OnComparisonViewed() { if (segmentActive) comparisonCount++; }
        public void OnMissionRefViewed() { if (segmentActive) missionRefCount++; }
        public void OnMapZoomed() { if (segmentActive) zoomCount++; }

        public void StopLogging()
        {
            // 미완료 세그먼트 flush
            if (segmentActive) OnBeamScreenOff();
            writer?.Dispose();
            writer = null;
        }

        private void OnDestroy()
        {
            if (segmentActive && writer != null) OnBeamScreenOff();
            writer?.Dispose();
        }
    }
}

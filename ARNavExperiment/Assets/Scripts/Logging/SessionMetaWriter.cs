using System;
using System.IO;
using UnityEngine;
using ARNavExperiment.Core;
using ARNavExperiment.Navigation;
using ARNavExperiment.Mission;

namespace ARNavExperiment.Logging
{
    /// <summary>
    /// 세션 메타데이터(시작 시) + 요약(종료 시) JSON 작성.
    /// </summary>
    public class SessionMetaWriter : MonoBehaviour
    {
        public static SessionMetaWriter Instance { get; private set; }

        private float sessionStartTime;
        private int pauseCount;
        private float totalPauseDuration;
        private float totalBeamTime;
        private int beamSegmentCount;
        private int forcedEventsCount;

        // 외부에서 증가
        public void RecordPause(float duration) { pauseCount++; totalPauseDuration += duration; }
        public void RecordBeamSegment(float duration) { beamSegmentCount++; totalBeamTime += duration; }
        public void RecordForcedEvent() { forcedEventsCount++; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void WriteSessionMeta()
        {
            var el = EventLogger.Instance;
            if (el == null || string.IsNullOrEmpty(el.SessionFilePrefix)) return;

            sessionStartTime = Time.time;
            pauseCount = 0;
            totalPauseDuration = 0f;
            totalBeamTime = 0f;
            beamSegmentCount = 0;
            forcedEventsCount = 0;

            string path = Path.Combine(el.SessionDirectory,
                $"{el.SessionFilePrefix}_meta.json");

            var meta = new SessionMeta
            {
                participant_id = el.ParticipantId,
                condition = el.CurrentCondition,
                mission_set = el.MissionSet,
                device_model = SystemInfo.deviceModel,
                android_version = GetAndroidVersion(),
                app_version = UnityEngine.Application.version,
                xreal_sdk_version = "3.1.0",
                build_time = GetBuildTime(),
                route_version = "RouteB",
                mapping_version = GetMappingVersion(),
                start_time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")
            };

            string json = JsonUtility.ToJson(meta, true);
            File.WriteAllText(path, json);
            Debug.Log($"[SessionMetaWriter] Meta written: {path}");
        }

        public void WriteSessionSummary()
        {
            var el = EventLogger.Instance;
            if (el == null || string.IsNullOrEmpty(el.SessionFilePrefix)) return;

            string path = Path.Combine(el.SessionDirectory,
                $"{el.SessionFilePrefix}_summary.json");

            float totalDuration = Time.time - sessionStartTime;
            var wm = WaypointManager.Instance;
            int totalWaypoints = wm?.CurrentWaypointIndex ?? 0;
            int fallbackCount = wm?.FallbackWaypointCount ?? 0;
            float relocRate = SpatialAnchorManager.Instance?.RelocalizationSuccessRate ?? 0f;

            // 미션 정확도
            var mm = MissionManager.Instance;
            float missionAccuracy = 0f;
            int missionCount = mm?.CurrentMissionIndex ?? 0;
            // missionAccuracy는 이벤트 CSV에서 사후 계산

            // 총 이동 거리는 NavigationTraceLogger에서 사후 계산

            var summary = new SessionSummary
            {
                end_time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                total_duration_s = totalDuration,
                total_waypoints = totalWaypoints,
                fallback_count = fallbackCount,
                beam_total_time_s = totalBeamTime,
                beam_segment_count = beamSegmentCount,
                relocalization_success_rate = relocRate,
                forced_events_count = forcedEventsCount,
                pause_count = pauseCount,
                total_pause_duration_s = totalPauseDuration,
                mission_count = missionCount
            };

            string json = JsonUtility.ToJson(summary, true);
            File.WriteAllText(path, json);
            Debug.Log($"[SessionMetaWriter] Summary written: {path}");
        }

        private string GetAndroidVersion()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var build = new AndroidJavaClass("android.os.Build$VERSION"))
                    return build.GetStatic<string>("RELEASE");
            }
            catch { return "unknown"; }
#else
            return "editor";
#endif
        }

        private string GetBuildTime()
        {
            // APK 빌드 시간 (빌드 스크립트에서 설정 가능)
            return UnityEngine.Application.buildGUID;
        }

        private string GetMappingVersion()
        {
            string mappingPath = Path.Combine(
                UnityEngine.Application.persistentDataPath, "anchor_mapping.json");
            if (File.Exists(mappingPath))
            {
                var info = new FileInfo(mappingPath);
                return info.LastWriteTime.ToString("yyyy-MM-dd");
            }
            return "none";
        }

        [Serializable]
        private class SessionMeta
        {
            public string participant_id;
            public string condition;
            public string mission_set;
            public string device_model;
            public string android_version;
            public string app_version;
            public string xreal_sdk_version;
            public string build_time;
            public string route_version;
            public string mapping_version;
            public string start_time;
        }

        [Serializable]
        private class SessionSummary
        {
            public string end_time;
            public float total_duration_s;
            public int total_waypoints;
            public int fallback_count;
            public float beam_total_time_s;
            public int beam_segment_count;
            public float relocalization_success_rate;
            public int forced_events_count;
            public int pause_count;
            public float total_pause_duration_s;
            public int mission_count;
        }
    }
}

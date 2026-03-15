using System;
using System.IO;
using UnityEngine;
using ARNavExperiment.Utils;

namespace ARNavExperiment.Logging
{
    public class EventLogger : MonoBehaviour
    {
        public static EventLogger Instance { get; private set; }

        private CSVWriter csvWriter;
        private string participantId;
        private string currentCondition;
        private string currentMissionId;
        private string sessionId;
        private string missionSet;

        public bool IsReady => csvWriter != null;

        /// <summary>현재 세션의 파일명 prefix (sidecar 로거용)</summary>
        public string SessionFilePrefix { get; private set; }

        /// <summary>현재 세션 데이터 디렉토리</summary>
        public string SessionDirectory { get; private set; }

        public string ParticipantId => participantId;
        public string CurrentCondition => currentCondition;
        public string MissionSet => missionSet;

        private static readonly string[] Headers = {
            "timestamp", "participant_id", "condition", "event_type",
            "waypoint_id", "head_rotation_x", "head_rotation_y", "head_rotation_z",
            "device_active", "confidence_rating", "mission_id",
            "difficulty_rating", "verification_correct", "beam_content_type",
            "session_id", "mission_set", "trigger_id", "trigger_type",
            "cause", "duration_s", "distance_m", "anchor_bound", "arrow_visible",
            "extra_data"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void StartSession(string participantId, string condition, string missionSet)
        {
            this.participantId = participantId;
            this.currentCondition = condition;
            this.missionSet = missionSet;

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            sessionId = $"{participantId}_{timestamp}";
            SessionFilePrefix = $"{participantId}_{condition}_{missionSet}_{timestamp}";
            SessionDirectory = Path.Combine(UnityEngine.Application.persistentDataPath, "data", "raw");

            string fileName = $"{SessionFilePrefix}.csv";
            string path = Path.Combine(SessionDirectory, fileName);

            csvWriter = new CSVWriter(path, Headers);
            Debug.Log($"[EventLogger] Session started: {path}");
        }

        public void SetCondition(string condition) => currentCondition = condition;
        public void SetMissionId(string missionId) => currentMissionId = missionId;

        public void LogEvent(string eventType, string waypointId = "",
            Vector3? headRotation = null, string deviceActive = "",
            int? confidenceRating = null, int? difficultyRating = null,
            bool? verificationCorrect = null, string beamContentType = "",
            string triggerId = "", string triggerType = "",
            string cause = "", float? durationS = null,
            float? distanceM = null, bool? anchorBound = null,
            bool? arrowVisible = null,
            string extraData = "{}")
        {
            if (csvWriter == null) return;

            var rot = headRotation ?? GetHeadRotation();
            csvWriter.WriteRow(
                DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                participantId,
                currentCondition,
                eventType,
                waypointId,
                rot.x.ToString("F1"),
                rot.y.ToString("F1"),
                rot.z.ToString("F1"),
                deviceActive,
                confidenceRating.HasValue ? confidenceRating.Value.ToString() : "",
                currentMissionId ?? "",
                difficultyRating.HasValue ? difficultyRating.Value.ToString() : "",
                verificationCorrect.HasValue ? verificationCorrect.Value.ToString().ToLower() : "",
                beamContentType,
                sessionId ?? "",
                missionSet ?? "",
                triggerId,
                triggerType,
                cause,
                durationS.HasValue ? durationS.Value.ToString("F1") : "",
                distanceM.HasValue ? distanceM.Value.ToString("F2") : "",
                anchorBound.HasValue ? anchorBound.Value.ToString().ToLower() : "",
                arrowVisible.HasValue ? arrowVisible.Value.ToString().ToLower() : "",
                extraData
            );
        }

        private Vector3 GetHeadRotation()
        {
            if (HeadTracker.Instance != null)
                return HeadTracker.Instance.CurrentRotation;
            return Vector3.zero;
        }

        public void EndSession()
        {
            csvWriter?.Dispose();
            csvWriter = null;
            Debug.Log("[EventLogger] Session ended");
        }

        private void OnDestroy()
        {
            csvWriter?.Dispose();
        }
    }
}

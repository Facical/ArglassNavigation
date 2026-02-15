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

        private static readonly string[] Headers = {
            "timestamp", "participant_id", "condition", "event_type",
            "waypoint_id", "head_rotation_x", "head_rotation_y", "head_rotation_z",
            "device_active", "confidence_rating", "mission_id",
            "difficulty_rating", "verification_correct", "beam_content_type", "extra_data"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void StartSession(string participantId, string condition, string route)
        {
            this.participantId = participantId;
            this.currentCondition = condition;

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{participantId}_{condition}_{route}_{timestamp}.csv";
            string path = Path.Combine(Application.persistentDataPath, "data", "raw", fileName);

            csvWriter = new CSVWriter(path, Headers);
            Debug.Log($"[EventLogger] Session started: {path}");
        }

        public void SetCondition(string condition) => currentCondition = condition;
        public void SetMissionId(string missionId) => currentMissionId = missionId;

        public void LogEvent(string eventType, string waypointId = "",
            Vector3? headRotation = null, string deviceActive = "",
            int? confidenceRating = null, int? difficultyRating = null,
            bool? verificationCorrect = null, string beamContentType = "",
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

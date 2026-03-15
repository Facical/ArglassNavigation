using System;
using UnityEngine;
using ARNavExperiment.Utils;
using ARNavExperiment.Core;

namespace ARNavExperiment.Logging
{
    /// <summary>
    /// 앵커별 재인식 상세 CSV 로거.
    /// SpatialAnchorManager에서 RecordAnchorProcessed 시점에 행 기록.
    /// </summary>
    public class AnchorRelocLogger : MonoBehaviour
    {
        public static AnchorRelocLogger Instance { get; private set; }

        private CSVWriter writer;

        private static readonly string[] Headers = {
            "timestamp", "session_id", "waypoint_id", "anchor_guid",
            "is_critical", "state", "elapsed_s", "slam_reason",
            "remap_attempt", "file_exists", "in_sdk"
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
                $"{el.SessionFilePrefix}_anchor_reloc.csv");
            writer = new CSVWriter(path, Headers);
            Debug.Log($"[AnchorRelocLogger] Started: {path}");
        }

        public void LogAnchorResult(string waypointId, string anchorGuid,
            bool isCritical, AnchorRelocState state, float elapsedS,
            string slamReason = "", int remapAttempt = 0,
            bool fileExists = true, bool inSdk = true)
        {
            if (writer == null) return;

            writer.WriteRow(
                DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                EventLogger.Instance?.ParticipantId ?? "",
                waypointId,
                anchorGuid,
                isCritical.ToString().ToLower(),
                state.ToString(),
                elapsedS.ToString("F1"),
                slamReason,
                remapAttempt.ToString(),
                fileExists.ToString().ToLower(),
                inSdk.ToString().ToLower()
            );
        }

        /// <summary>WaitForAnchorTracking 루프 내 5초 주기 SLAM 상태 중간 기록</summary>
        public void LogAnchorProgress(string waypointId, string anchorGuid,
            bool isCritical, float elapsedS, string slamReason)
        {
            if (writer == null) return;

            writer.WriteRow(
                DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                EventLogger.Instance?.ParticipantId ?? "",
                waypointId,
                anchorGuid,
                isCritical.ToString().ToLower(),
                "InProgress",
                elapsedS.ToString("F1"),
                slamReason,
                "0",
                "",
                ""
            );
        }

        public void StopLogging()
        {
            writer?.Dispose();
            writer = null;
        }

        private void OnDestroy() => writer?.Dispose();
    }
}

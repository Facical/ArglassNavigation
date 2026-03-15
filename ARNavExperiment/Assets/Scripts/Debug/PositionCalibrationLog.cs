using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ARNavExperiment.Core;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;
using ARNavExperiment.Navigation;
using ARNavExperiment.Mission;
using ARNavExperiment.Utils;

namespace ARNavExperiment.DebugTools
{
    /// <summary>
    /// 도착선언(ForceArrival) 및 자동 도착(WaypointReached) 시 SLAM 좌표를 기록하여
    /// fallback 좌표 정밀도 개선에 활용할 수 있는 디버그 로그.
    /// JSON 파일로 저장: Application.persistentDataPath/calibration/position_log_{timestamp}.json
    /// </summary>
    public class PositionCalibrationLog : MonoBehaviour
    {
        public static PositionCalibrationLog Instance { get; private set; }

        [Serializable]
        private class SessionInfo
        {
            public string recordedAt;
            public float headingOffset;
            public string calibrationSource;
            public string participantId;
            public string condition;
        }

        [Serializable]
        private class PositionSample
        {
            public string waypointId;
            public string missionId;
            public string source; // "force_arrival", "waypoint_reached"
            public float[] slamPosition;
            public float[] fallbackPosition;
            public float[] calibratedPosition;
            public string timestamp;
        }

        [Serializable]
        private class PositionLog
        {
            public SessionInfo sessionInfo;
            public List<PositionSample> samples = new List<PositionSample>();
        }

        private PositionLog log;
        private bool hasData;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            log = new PositionLog();
            DomainEventBus.Instance?.Subscribe<ArrivalForced>(OnArrivalForced);
            DomainEventBus.Instance?.Subscribe<WaypointReached>(OnWaypointReached);
        }

        private void OnDestroy()
        {
            DomainEventBus.Instance?.Unsubscribe<ArrivalForced>(OnArrivalForced);
            DomainEventBus.Instance?.Unsubscribe<WaypointReached>(OnWaypointReached);
        }

        private void OnArrivalForced(ArrivalForced evt)
        {
            RecordPosition(evt.MissionId, "force_arrival");
        }

        private void OnWaypointReached(WaypointReached evt)
        {
            if (!evt.IsTarget) return; // 미션 타겟 도착만 기록
            string missionId = MissionManager.Instance?.CurrentMission?.missionId ?? "";
            RecordPosition(missionId, "waypoint_reached");
        }

        private void RecordPosition(string missionId, string source)
        {
            var cam = XRCameraHelper.GetCamera();
            if (cam == null) return;

            var wpm = WaypointManager.Instance;
            if (wpm == null) return;

            var slamPos = cam.transform.position;
            string waypointId = wpm.CurrentWaypoint?.waypointId ?? "unknown";
            var fallbackPos = wpm.CurrentWaypoint?.fallbackPosition ?? Vector3.zero;

            // calibratedPosition: 현재 heading 보정이 적용된 경우에만 기록
            float[] calibrated = null;

            var sample = new PositionSample
            {
                waypointId = waypointId,
                missionId = missionId,
                source = source,
                slamPosition = new float[] { slamPos.x, slamPos.y, slamPos.z },
                fallbackPosition = new float[] { fallbackPos.x, fallbackPos.y, fallbackPos.z },
                calibratedPosition = calibrated,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            log.samples.Add(sample);
            hasData = true;

            Debug.Log($"[PositionCal] {waypointId}: slam=({slamPos.x:F2}, {slamPos.y:F2}, {slamPos.z:F2}) " +
                $"fallback=({fallbackPos.x:F0},{fallbackPos.y:F0},{fallbackPos.z:F0}) " +
                $"heading={wpm.HeadingCalibrationOffset:F1}\u00b0 source={source}");
        }

        private void OnApplicationQuit()
        {
            if (!hasData) return;
            SaveLog();
        }

        private void SaveLog()
        {
            // 세션 정보 채우기
            var wpm = WaypointManager.Instance;
            var em = ExperimentManager.Instance;

            log.sessionInfo = new SessionInfo
            {
                recordedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                headingOffset = wpm?.HeadingCalibrationOffset ?? 0f,
                calibrationSource = wpm?.CalibrationSource ?? "unknown",
                participantId = em?.session?.participantId ?? "unknown",
                condition = em?.ActiveCondition ?? "unknown"
            };

            string dir = Path.Combine(UnityEngine.Application.persistentDataPath, "calibration");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(dir, $"position_log_{timestamp}.json");

            string json = JsonUtility.ToJson(log, true);
            File.WriteAllText(filePath, json);

            Debug.Log($"[PositionCal] Saved {log.samples.Count} samples to {filePath}");
        }
    }
}

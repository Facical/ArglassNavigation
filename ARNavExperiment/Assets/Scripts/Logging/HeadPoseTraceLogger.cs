using System;
using UnityEngine;
using ARNavExperiment.Utils;
using ARNavExperiment.Navigation;
using ARNavExperiment.Mission;

namespace ARNavExperiment.Logging
{
    /// <summary>
    /// 10Hz 머리 회전/각속도 trace 로거.
    /// HeadTracker와 동기화된 샘플링.
    /// </summary>
    public class HeadPoseTraceLogger : MonoBehaviour
    {
        public static HeadPoseTraceLogger Instance { get; private set; }

        private CSVWriter writer;
        [SerializeField] private float sampleRate = 10f;
        private float nextSampleTime;

        private static readonly string[] Headers = {
            "timestamp", "session_id", "participant_id", "condition",
            "mission_id", "waypoint_id",
            "yaw", "pitch", "roll", "angular_velocity_yaw",
            "beam_on", "arrow_visible", "trigger_id"
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
                $"{el.SessionFilePrefix}_head_pose.csv");
            writer = new CSVWriter(path, Headers);
            Debug.Log($"[HeadPoseTraceLogger] Started: {path}");
        }

        private void Update()
        {
            if (writer == null || Time.time < nextSampleTime) return;
            nextSampleTime = Time.time + (1f / sampleRate);

            var ht = HeadTracker.Instance;
            if (ht == null) return;

            var el = EventLogger.Instance;
            var rot = ht.CurrentRotation;

            string missionId = MissionManager.Instance?.CurrentMission?.missionId ?? "";
            string wpId = WaypointManager.Instance?.CurrentWaypoint?.waypointId ?? "";
            bool beamOn = DeviceStateTracker.Instance?.IsBeamProActive ?? false;
            bool arrowVisible = false;
            string triggerId = "";

            var arrowRenderer = GetArrowRenderer();
            if (arrowRenderer != null) arrowVisible = arrowRenderer.IsVisible;

            var triggerCtrl = TriggerController.Instance;
            if (triggerCtrl != null) triggerId = triggerCtrl.ActiveTriggerId ?? "";

            writer.WriteRow(
                DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                el?.SessionFilePrefix ?? "",
                el?.ParticipantId ?? "",
                el?.CurrentCondition ?? "",
                missionId,
                wpId,
                rot.y.ToString("F1"), // yaw
                rot.x.ToString("F1"), // pitch
                rot.z.ToString("F1"), // roll
                ht.AngularVelocityYaw.ToString("F1"),
                beamOn.ToString().ToLower(),
                arrowVisible.ToString().ToLower(),
                triggerId
            );
        }

        private ARArrowRenderer cachedArrow;
        private ARArrowRenderer GetArrowRenderer()
        {
            if (cachedArrow == null || !cachedArrow.isActiveAndEnabled)
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

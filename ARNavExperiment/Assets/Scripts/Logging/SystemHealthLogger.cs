using System;
using UnityEngine;
using UnityEngine.Profiling;
using ARNavExperiment.Utils;
using ARNavExperiment.Core;

namespace ARNavExperiment.Logging
{
    /// <summary>
    /// 1Hz 시스템 상태 로거 (fps, 배터리, 메모리, SLAM).
    /// </summary>
    public class SystemHealthLogger : MonoBehaviour
    {
        public static SystemHealthLogger Instance { get; private set; }

        private CSVWriter writer;
        private float nextSampleTime;

        private static readonly string[] Headers = {
            "timestamp", "fps", "frame_time_ms", "battery_level",
            "battery_charging", "thermal_status", "memory_mb",
            "tracking_state", "slam_reason"
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
                $"{el.SessionFilePrefix}_system_health.csv");
            writer = new CSVWriter(path, Headers);
            Debug.Log($"[SystemHealthLogger] Started: {path}");
        }

        private void Update()
        {
            if (writer == null || Time.time < nextSampleTime) return;
            nextSampleTime = Time.time + 1f; // 1Hz

            float fps = 1f / Time.deltaTime;
            float frameTimeMs = Time.deltaTime * 1000f;
            float batteryLevel = SystemInfo.batteryLevel;
            string batteryCharging = SystemInfo.batteryStatus.ToString();
            string thermalStatus = GetThermalStatus();
            long memoryBytes = Profiler.GetTotalAllocatedMemoryLong();
            float memoryMb = memoryBytes / (1024f * 1024f);

            string trackingState = "unknown";
            string slamReason = "";

            var sam = SpatialAnchorManager.Instance;
            if (sam != null)
            {
                var status = sam.GetSlamTrackingStatus();
                trackingState = status.isReady ? "ready" : "not_ready";
                slamReason = status.reasonKey ?? "";
            }

            writer.WriteRow(
                DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                fps.ToString("F1"),
                frameTimeMs.ToString("F1"),
                batteryLevel.ToString("F2"),
                batteryCharging,
                thermalStatus,
                memoryMb.ToString("F1"),
                trackingState,
                slamReason
            );
        }

        private string GetThermalStatus()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var powerMgr = activity.Call<AndroidJavaObject>("getSystemService", "power"))
                {
                    int thermal = powerMgr.Call<int>("getCurrentThermalStatus");
                    return thermal switch
                    {
                        0 => "none",
                        1 => "light",
                        2 => "moderate",
                        3 => "severe",
                        4 => "critical",
                        5 => "emergency",
                        6 => "shutdown",
                        _ => $"unknown_{thermal}"
                    };
                }
            }
            catch { return "unavailable"; }
#else
            return "editor";
#endif
        }

        public void StopLogging()
        {
            writer?.Dispose();
            writer = null;
        }

        private void OnDestroy() => writer?.Dispose();
    }
}

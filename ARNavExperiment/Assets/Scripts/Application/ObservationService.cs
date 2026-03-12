using System.Collections;
using UnityEngine;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Logging;

namespace ARNavExperiment.Application
{
    /// <summary>
    /// 도메인 이벤트를 구독하여 EventLogger로 위임하는 관찰 서비스.
    /// Phase 2에서 각 클래스의 EventLogger.Instance?.LogEvent() 직접 호출을
    /// DomainEventBus.Publish()로 교체하면, 이 서비스가 로깅을 대신 수행.
    ///
    /// 기존 CSV 포맷과 100% 호환 유지.
    /// </summary>
    public class ObservationService : MonoBehaviour
    {
        public static ObservationService Instance { get; private set; }

        private bool _subscribed;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable() => TrySubscribe();

        private void Start()
        {
            if (!_subscribed) TrySubscribe();
            if (!_subscribed) StartCoroutine(RetrySubscribe());
        }

        private void TrySubscribe()
        {
            var bus = DomainEventBus.Instance;
            if (bus == null) return;

            // === Experiment ===
            bus.Subscribe<ExperimentStateChanged>(OnExperimentStateChanged);
            bus.Subscribe<ConditionChanged>(OnConditionChanged);
            bus.Subscribe<SessionInitialized>(OnSessionInitialized);
            bus.Subscribe<RouteStarted>(OnRouteStarted);
            bus.Subscribe<SurveyStarted>(OnSurveyStarted);
            bus.Subscribe<ExperimentCompleted>(OnExperimentCompleted);

            // === Mission ===
            bus.Subscribe<MissionStarted>(OnMissionStarted);
            bus.Subscribe<MissionArrived>(OnMissionArrived);
            bus.Subscribe<VerificationAnswered>(OnVerificationAnswered);
            bus.Subscribe<ConfidenceRated>(OnConfidenceRated);
            bus.Subscribe<DifficultyRated>(OnDifficultyRated);
            bus.Subscribe<MissionCompleted>(OnMissionCompleted);
            bus.Subscribe<BriefingForced>(OnBriefingForced);
            bus.Subscribe<ArrivalForced>(OnArrivalForced);
            bus.Subscribe<MissionForceSkipped>(OnMissionForceSkipped);

            // === Navigation ===
            bus.Subscribe<WaypointReached>(OnWaypointReached);
            bus.Subscribe<TriggerActivated>(OnTriggerActivated);
            bus.Subscribe<TriggerDeactivated>(OnTriggerDeactivated);
            bus.Subscribe<TriggerInterrupted>(OnTriggerInterrupted);
            bus.Subscribe<ArrowShown>(OnArrowShown);
            bus.Subscribe<ArrowHidden>(OnArrowHidden);
            bus.Subscribe<ArrowOffset>(OnArrowOffset);
            bus.Subscribe<WaypointFallbackUsed>(OnWaypointFallbackUsed);
            bus.Subscribe<WaypointLateAnchorBound>(OnWaypointLateAnchorBound);
            bus.Subscribe<HeadingCalibrationApplied>(OnHeadingCalibrationApplied);
            bus.Subscribe<ManualCalibrationApplied>(OnManualCalibrationApplied);
            bus.Subscribe<ImageMarkerDetected>(OnImageMarkerDetected);

            // === Spatial ===
            bus.Subscribe<RelocalizationStarted>(OnRelocalizationStarted);
            bus.Subscribe<RelocalizationCompleted>(OnRelocalizationCompleted);
            bus.Subscribe<AnchorDiagnostics>(OnAnchorDiagnostics);

            // === Observation ===
            bus.Subscribe<DeviceScreenChanged>(OnDeviceScreenChanged);
            bus.Subscribe<BeamTabSwitched>(OnBeamTabSwitched);
            bus.Subscribe<BeamInfoCardToggled>(OnBeamInfoCardToggled);
            bus.Subscribe<BeamPOIViewed>(OnBeamPOIViewed);
            bus.Subscribe<BeamComparisonViewed>(OnBeamComparisonViewed);
            bus.Subscribe<BeamMissionRefViewed>(OnBeamMissionRefViewed);
            bus.Subscribe<BeamMapZoomed>(OnBeamMapZoomed);
            bus.Subscribe<GlassCaptureStateChanged>(OnGlassCaptureStateChanged);

            // === Diagnostics ===
            bus.Subscribe<NavigationStateSnapshot>(OnNavigationStateSnapshot);
            bus.Subscribe<RouteBindingSummary>(OnRouteBindingSummary);
            bus.Subscribe<AppLifecycleEvent>(OnAppLifecycleEvent);

            _subscribed = true;
            Debug.Log($"[{GetType().Name}] Subscribed to DomainEventBus");
        }

        private IEnumerator RetrySubscribe()
        {
            for (int i = 0; i < 10 && !_subscribed; i++)
            {
                yield return new WaitForSeconds(0.1f);
                TrySubscribe();
            }
            if (!_subscribed)
                Debug.LogError($"[{GetType().Name}] Failed to subscribe after 10 retries");
        }

        private void OnDisable()
        {
            var bus = DomainEventBus.Instance;
            if (bus == null) return;

            bus.Unsubscribe<ExperimentStateChanged>(OnExperimentStateChanged);
            bus.Unsubscribe<ConditionChanged>(OnConditionChanged);
            bus.Unsubscribe<SessionInitialized>(OnSessionInitialized);
            bus.Unsubscribe<RouteStarted>(OnRouteStarted);
            bus.Unsubscribe<SurveyStarted>(OnSurveyStarted);
            bus.Unsubscribe<ExperimentCompleted>(OnExperimentCompleted);

            bus.Unsubscribe<MissionStarted>(OnMissionStarted);
            bus.Unsubscribe<MissionArrived>(OnMissionArrived);
            bus.Unsubscribe<VerificationAnswered>(OnVerificationAnswered);
            bus.Unsubscribe<ConfidenceRated>(OnConfidenceRated);
            bus.Unsubscribe<DifficultyRated>(OnDifficultyRated);
            bus.Unsubscribe<MissionCompleted>(OnMissionCompleted);
            bus.Unsubscribe<BriefingForced>(OnBriefingForced);
            bus.Unsubscribe<ArrivalForced>(OnArrivalForced);
            bus.Unsubscribe<MissionForceSkipped>(OnMissionForceSkipped);

            bus.Unsubscribe<WaypointReached>(OnWaypointReached);
            bus.Unsubscribe<TriggerActivated>(OnTriggerActivated);
            bus.Unsubscribe<TriggerDeactivated>(OnTriggerDeactivated);
            bus.Unsubscribe<TriggerInterrupted>(OnTriggerInterrupted);
            bus.Unsubscribe<ArrowShown>(OnArrowShown);
            bus.Unsubscribe<ArrowHidden>(OnArrowHidden);
            bus.Unsubscribe<ArrowOffset>(OnArrowOffset);
            bus.Unsubscribe<WaypointFallbackUsed>(OnWaypointFallbackUsed);
            bus.Unsubscribe<WaypointLateAnchorBound>(OnWaypointLateAnchorBound);
            bus.Unsubscribe<HeadingCalibrationApplied>(OnHeadingCalibrationApplied);
            bus.Unsubscribe<ManualCalibrationApplied>(OnManualCalibrationApplied);
            bus.Unsubscribe<ImageMarkerDetected>(OnImageMarkerDetected);

            bus.Unsubscribe<RelocalizationStarted>(OnRelocalizationStarted);
            bus.Unsubscribe<RelocalizationCompleted>(OnRelocalizationCompleted);
            bus.Unsubscribe<AnchorDiagnostics>(OnAnchorDiagnostics);

            bus.Unsubscribe<DeviceScreenChanged>(OnDeviceScreenChanged);
            bus.Unsubscribe<BeamTabSwitched>(OnBeamTabSwitched);
            bus.Unsubscribe<BeamInfoCardToggled>(OnBeamInfoCardToggled);
            bus.Unsubscribe<BeamPOIViewed>(OnBeamPOIViewed);
            bus.Unsubscribe<BeamComparisonViewed>(OnBeamComparisonViewed);
            bus.Unsubscribe<BeamMissionRefViewed>(OnBeamMissionRefViewed);
            bus.Unsubscribe<BeamMapZoomed>(OnBeamMapZoomed);
            bus.Unsubscribe<GlassCaptureStateChanged>(OnGlassCaptureStateChanged);

            bus.Unsubscribe<NavigationStateSnapshot>(OnNavigationStateSnapshot);
            bus.Unsubscribe<RouteBindingSummary>(OnRouteBindingSummary);
            bus.Unsubscribe<AppLifecycleEvent>(OnAppLifecycleEvent);

            _subscribed = false;
        }

        // ── Experiment ──────────────────────────────────────────

        private void OnExperimentStateChanged(ExperimentStateChanged e)
        {
            // State transition은 ExperimentManager 내부에서 이미 개별 이벤트로 발행
            // 이 핸들러는 추가 범용 로깅이 필요할 때 사용
        }

        private void OnConditionChanged(ConditionChanged e)
        {
            EventLogger.Instance?.SetCondition(e.Condition);
            EventLogger.Instance?.LogEvent("CONDITION_CHANGE",
                extraData: $"{{\"condition\":\"{e.Condition}\"}}");
        }

        private void OnSessionInitialized(SessionInitialized e)
        {
            EventLogger.Instance?.LogEvent("EXPERIMENT_START",
                extraData: $"{{\"condition\":\"{e.Condition}\",\"mission_set\":\"{e.MissionSet}\"}}");
        }

        private void OnRouteStarted(RouteStarted e)
        {
            EventLogger.Instance?.LogEvent("ROUTE_START",
                extraData: $"{{\"mission_set\":\"{e.MissionSet}\",\"condition\":\"{e.Condition}\"}}");
        }

        private void OnSurveyStarted(SurveyStarted e)
        {
            EventLogger.Instance?.LogEvent("SURVEY_START",
                extraData: $"{{\"survey\":\"{e.SurveyType}\"}}");
        }

        private void OnExperimentCompleted(ExperimentCompleted e)
        {
            EventLogger.Instance?.LogEvent("EXPERIMENT_END",
                extraData: $"{{\"total_duration_s\":{e.TotalDurationSeconds:F0}}}");
            EventLogger.Instance?.EndSession();
        }

        // ── Mission ─────────────────────────────────────────────

        private void OnMissionStarted(MissionStarted e)
        {
            EventLogger.Instance?.SetMissionId(e.MissionId);
            EventLogger.Instance?.LogEvent("MISSION_START",
                extraData: $"{{\"mission_id\":\"{e.MissionId}\"," +
                           $"\"type\":\"{e.MissionType}\"," +
                           $"\"briefing\":\"{EscapeJson(e.BriefingText)}\"}}");
        }

        private void OnMissionArrived(MissionArrived e)
        {
            EventLogger.Instance?.LogEvent("MISSION_ARRIVAL",
                waypointId: e.WaypointId,
                extraData: $"{{\"mission_id\":\"{e.MissionId}\"}}");
        }

        private void OnVerificationAnswered(VerificationAnswered e)
        {
            EventLogger.Instance?.LogEvent("VERIFICATION_ANSWERED",
                waypointId: e.WaypointId,
                verificationCorrect: e.Correct,
                extraData: $"{{\"mission_id\":\"{e.MissionId}\"," +
                           $"\"answer\":{e.SelectedIndex},\"correct\":{e.Correct.ToString().ToLower()}," +
                           $"\"rt_s\":{e.ResponseTimeSeconds:F1}}}");
        }

        private void OnConfidenceRated(ConfidenceRated e)
        {
            EventLogger.Instance?.LogEvent("CONFIDENCE_RATED",
                waypointId: e.WaypointId,
                confidenceRating: e.Rating,
                extraData: $"{{\"mission_id\":\"{e.MissionId}\",\"rating\":{e.Rating}}}");
        }

        private void OnDifficultyRated(DifficultyRated e)
        {
            EventLogger.Instance?.LogEvent("DIFFICULTY_RATED",
                extraData: $"{{\"mission_id\":\"{e.MissionId}\",\"rating\":{e.Rating}}}");
        }

        private void OnMissionCompleted(MissionCompleted e)
        {
            EventLogger.Instance?.LogEvent("MISSION_COMPLETE",
                extraData: $"{{\"mission_id\":\"{e.MissionId}\"," +
                           $"\"correct\":{e.Correct.ToString().ToLower()},\"duration_s\":{e.DurationSeconds:F0}}}");
        }

        private void OnBriefingForced(BriefingForced e)
        {
            EventLogger.Instance?.LogEvent("BRIEFING_FORCED",
                extraData: $"{{\"mission_id\":\"{e.MissionId}\",\"forced\":true}}");
        }

        private void OnArrivalForced(ArrivalForced e)
        {
            EventLogger.Instance?.LogEvent("ARRIVAL_FORCED",
                extraData: $"{{\"mission_id\":\"{e.MissionId}\",\"forced\":true}}");
        }

        private void OnMissionForceSkipped(MissionForceSkipped e)
        {
            EventLogger.Instance?.LogEvent("MISSION_FORCE_SKIPPED",
                extraData: $"{{\"mission_id\":\"{e.MissionId}\",\"state\":\"{e.State}\",\"forced\":true}}");
        }

        // ── Navigation ──────────────────────────────────────────

        private void OnWaypointReached(WaypointReached e)
        {
            EventLogger.Instance?.LogEvent("WAYPOINT_REACHED", waypointId: e.WaypointId);
        }

        private void OnTriggerActivated(TriggerActivated e)
        {
            EventLogger.Instance?.LogEvent("TRIGGER_ACTIVATED",
                extraData: $"{{\"trigger_type\":\"{e.TriggerType}\",\"trigger_id\":\"{e.TriggerId}\"}}");
        }

        private void OnTriggerDeactivated(TriggerDeactivated e)
        {
            EventLogger.Instance?.LogEvent("TRIGGER_DEACTIVATED",
                extraData: $"{{\"trigger_type\":\"{e.TriggerType}\",\"duration_s\":{e.DurationSeconds:F1}}}");
        }

        private void OnTriggerInterrupted(TriggerInterrupted e)
        {
            EventLogger.Instance?.LogEvent("TRIGGER_INTERRUPTED",
                extraData: $"{{\"trigger_id\":\"{e.TriggerId}\",\"trigger_type\":\"{e.TriggerType}\"," +
                           $"\"duration_s\":{e.DurationSeconds:F1},\"reason\":\"{e.Reason}\"}}");
        }

        private void OnArrowShown(ArrowShown e)
        {
            EventLogger.Instance?.LogEvent("GLASS_ARROW_SHOWN");
        }

        private void OnArrowHidden(ArrowHidden e)
        {
            EventLogger.Instance?.LogEvent("GLASS_ARROW_HIDDEN");
        }

        private void OnArrowOffset(ArrowOffset e)
        {
            string key = e.TriggerId == "T1" ? "offset_angle" : "spread_angle";
            EventLogger.Instance?.LogEvent("GLASS_ARROW_OFFSET",
                extraData: $"{{\"{key}\":{e.OffsetAngle:F1},\"trigger_id\":\"{e.TriggerId}\"}}");
        }

        private void OnWaypointFallbackUsed(WaypointFallbackUsed e)
        {
            EventLogger.Instance?.LogEvent("WAYPOINT_FALLBACK_USED",
                waypointId: e.WaypointId,
                extraData: $"{{\"fallback_position\":\"{e.FallbackPosition}\"}}");
        }

        private void OnWaypointLateAnchorBound(WaypointLateAnchorBound e)
        {
            EventLogger.Instance?.LogEvent("WAYPOINT_LATE_ANCHOR_BOUND",
                waypointId: e.WaypointId,
                extraData: $"{{\"drift_m\":{e.DriftMeters:F2},\"old_pos\":\"{e.OldPosition}\",\"new_pos\":\"{e.NewPosition}\"}}");
        }

        private void OnHeadingCalibrationApplied(HeadingCalibrationApplied e)
        {
            EventLogger.Instance?.LogEvent("HEADING_CALIBRATION",
                extraData: $"{{\"source\":\"{e.Source}\",\"offset_deg\":{e.OffsetDegrees:F1}}}");
        }

        private void OnManualCalibrationApplied(ManualCalibrationApplied e)
        {
            EventLogger.Instance?.LogEvent("MANUAL_CALIBRATION",
                waypointId: e.WaypointId,
                extraData: $"{{\"camera_pos\":\"{e.CameraPosition}\",\"fallback_pos\":\"{e.FallbackPosition}\"," +
                           $"\"heading_offset\":{e.HeadingOffset:F1}}}");
        }

        private void OnImageMarkerDetected(ImageMarkerDetected e)
        {
            EventLogger.Instance?.LogEvent("IMAGE_MARKER_DETECTED",
                waypointId: e.MappedWaypointId,
                extraData: $"{{\"marker_id\":\"{e.MarkerId}\",\"slam_pos\":\"{e.SlamPosition}\"," +
                           $"\"heading_offset\":{e.HeadingOffset:F1}}}");
        }

        // ── Spatial ─────────────────────────────────────────────

        private void OnRelocalizationStarted(RelocalizationStarted e)
        {
            EventLogger.Instance?.LogEvent("RELOCALIZATION_START");
        }

        private void OnRelocalizationCompleted(RelocalizationCompleted e)
        {
            string eventType = e.Action switch
            {
                "retry" => "RELOCALIZATION_RETRY",
                "proceed_partial" => "RELOCALIZATION_PROCEED_PARTIAL",
                _ => "RELOCALIZATION_COMPLETE"
            };
            EventLogger.Instance?.LogEvent(eventType,
                extraData: $"{{\"success_rate\":{e.SuccessRate:F2}}}");
        }

        private void OnAnchorDiagnostics(AnchorDiagnostics e)
        {
            EventLogger.Instance?.LogEvent("ANCHOR_DIAGNOSTICS",
                extraData: e.DiagnosticsJson);
        }

        // ── Observation ─────────────────────────────────────────

        private void OnDeviceScreenChanged(DeviceScreenChanged e)
        {
            if (e.IsOn)
            {
                EventLogger.Instance?.LogEvent("BEAM_SCREEN_ON");
            }
            else
            {
                string extra = e.DurationSec > 0f
                    ? $"{{\"duration_s\":{e.DurationSec:F1}}}"
                    : "{}";
                EventLogger.Instance?.LogEvent("BEAM_SCREEN_OFF", extraData: extra);
            }
        }

        private void OnBeamTabSwitched(BeamTabSwitched e)
        {
            EventLogger.Instance?.LogEvent("BEAM_TAB_SWITCH",
                beamContentType: e.TabName,
                extraData: $"{{\"tab_index\":{e.TabIndex}}}");
        }

        private void OnBeamInfoCardToggled(BeamInfoCardToggled e)
        {
            EventLogger.Instance?.LogEvent(e.Opened ? "BEAM_INFO_CARD_OPENED" : "BEAM_INFO_CARD_CLOSED",
                beamContentType: e.CardId);
        }

        private void OnBeamPOIViewed(BeamPOIViewed e)
        {
            EventLogger.Instance?.LogEvent("BEAM_POI_VIEWED",
                beamContentType: e.POIId);
        }

        private void OnBeamComparisonViewed(BeamComparisonViewed e)
        {
            EventLogger.Instance?.LogEvent("BEAM_COMPARISON_VIEWED",
                beamContentType: e.ComparisonId);
        }

        private void OnBeamMissionRefViewed(BeamMissionRefViewed e)
        {
            EventLogger.Instance?.LogEvent("BEAM_MISSION_REF_VIEWED",
                beamContentType: e.MissionId);
        }

        private void OnBeamMapZoomed(BeamMapZoomed e)
        {
            EventLogger.Instance?.LogEvent("BEAM_MAP_ZOOMED",
                extraData: $"{{\"zoom_level\":{e.ZoomLevel:F1}}}");
        }

        private void OnGlassCaptureStateChanged(GlassCaptureStateChanged e)
        {
            string eventType = e.State switch
            {
                "start" => "GLASS_CAPTURE_START",
                "stop" => "GLASS_CAPTURE_STOP",
                _ => "GLASS_CAPTURE_ERROR"
            };
            EventLogger.Instance?.LogEvent(eventType,
                extraData: string.IsNullOrEmpty(e.FilePath) ? "{}" : $"{{\"file\":\"{EscapeJson(e.FilePath)}\"}}");
        }

        // ── Diagnostics ────────────────────────────────────────

        private void OnNavigationStateSnapshot(NavigationStateSnapshot e)
        {
            EventLogger.Instance?.LogEvent("NAV_STATE_SNAPSHOT",
                waypointId: e.WaypointId,
                extraData: $"{{\"anchor_bound\":{e.IsAnchorBound.ToString().ToLower()}," +
                           $"\"player_pos\":\"{e.PlayerPosition}\"," +
                           $"\"target_pos\":\"{e.TargetPosition}\"," +
                           $"\"distance_m\":{e.DistanceToTarget:F2}," +
                           $"\"arrow_visible\":{e.ArrowVisible.ToString().ToLower()}," +
                           $"\"route\":\"{e.RouteId}\"," +
                           $"\"condition\":\"{e.Condition}\"," +
                           $"\"has_map_calib\":{e.HasMapCalibration.ToString().ToLower()}," +
                           $"\"calib_source\":\"{e.CalibrationSource}\"}}");
        }

        private void OnRouteBindingSummary(RouteBindingSummary e)
        {
            EventLogger.Instance?.LogEvent("ROUTE_BINDING_SUMMARY",
                extraData: $"{{\"route\":\"{e.RouteId}\"," +
                           $"\"total\":{e.TotalWaypoints}," +
                           $"\"anchor_bound\":{e.AnchorBound}," +
                           $"\"fallback\":{e.FallbackUsed}," +
                           $"\"details\":{e.DetailsJson}}}");
        }

        private void OnAppLifecycleEvent(AppLifecycleEvent e)
        {
            string eventType = e.EventType switch
            {
                "pause" => "APP_PAUSE",
                "resume" => "APP_RESUME",
                "focus_lost" => "APP_FOCUS_LOST",
                "focus_gained" => "APP_FOCUS_GAINED",
                "quit" => "APP_QUIT",
                _ => $"APP_{e.EventType.ToUpper()}"
            };
            EventLogger.Instance?.LogEvent(eventType,
                extraData: $"{{\"time_since_startup\":{e.TimeSinceStartup:F1}}}");
        }

        // ── Util ────────────────────────────────────────────────

        private static string EscapeJson(string text)
        {
            return text?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") ?? "";
        }
    }
}

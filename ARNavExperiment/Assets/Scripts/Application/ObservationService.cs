using System.Collections;
using UnityEngine;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Logging;
using ARNavExperiment.Navigation;

namespace ARNavExperiment.Application
{
    /// <summary>
    /// 도메인 이벤트를 구독하여 EventLogger로 위임하는 관찰 서비스.
    /// ISMAR용 확장: 전용 컬럼 채우기, Beam 세그먼트 추적, 트리거 반응 추적,
    /// sidecar 로거 시작/종료 연동.
    /// </summary>
    public class ObservationService : MonoBehaviour
    {
        public static ObservationService Instance { get; private set; }

        private bool _subscribed;

        // 트리거 반응 추적
        private bool triggerActiveForResponse;
        private string activeTriggerIdForResponse;
        private string activeTriggerTypeForResponse;
        private float triggerActivatedTime;
        private bool triggerResponseRecorded;

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
            bus.Subscribe<PreflightCheckCompleted>(OnPreflightCheckCompleted);

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
            bus.Subscribe<RouteCompleted>(OnRouteCompleted);
            bus.Subscribe<MovementPaused>(OnMovementPaused);
            bus.Subscribe<MovementResumed>(OnMovementResumed);
            bus.Subscribe<TriggerResponse>(OnTriggerResponse);

            // === Spatial ===
            bus.Subscribe<RelocalizationStarted>(OnRelocalizationStarted);
            bus.Subscribe<RelocalizationCompleted>(OnRelocalizationCompleted);
            bus.Subscribe<AnchorDiagnostics>(OnAnchorDiagnostics);
            bus.Subscribe<ReferenceAnchorSaved>(OnReferenceAnchorSaved);
            bus.Subscribe<ReferenceAnchorRecovered>(OnReferenceAnchorRecovered);

            // === Observation ===
            bus.Subscribe<DeviceScreenChanged>(OnDeviceScreenChanged);
            bus.Subscribe<BeamTabSwitched>(OnBeamTabSwitched);
            bus.Subscribe<BeamInfoCardToggled>(OnBeamInfoCardToggled);
            bus.Subscribe<BeamPOIViewed>(OnBeamPOIViewed);
            bus.Subscribe<BeamComparisonViewed>(OnBeamComparisonViewed);
            bus.Subscribe<BeamMissionRefViewed>(OnBeamMissionRefViewed);
            bus.Subscribe<BeamMapZoomed>(OnBeamMapZoomed);
            bus.Subscribe<GlassCaptureStateChanged>(OnGlassCaptureStateChanged);

            // === Survey ===
            bus.Subscribe<SurveyItemAnswered>(OnSurveyItemAnswered);
            bus.Subscribe<SurveyCompleted>(OnSurveyCompleted);
            bus.Subscribe<ComparisonSurveyAnswered>(OnComparisonSurveyAnswered);
            bus.Subscribe<ComparisonSurveyCompleted>(OnComparisonSurveyCompleted);

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
            bus.Unsubscribe<PreflightCheckCompleted>(OnPreflightCheckCompleted);

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
            bus.Unsubscribe<RouteCompleted>(OnRouteCompleted);
            bus.Unsubscribe<MovementPaused>(OnMovementPaused);
            bus.Unsubscribe<MovementResumed>(OnMovementResumed);
            bus.Unsubscribe<TriggerResponse>(OnTriggerResponse);

            bus.Unsubscribe<RelocalizationStarted>(OnRelocalizationStarted);
            bus.Unsubscribe<RelocalizationCompleted>(OnRelocalizationCompleted);
            bus.Unsubscribe<AnchorDiagnostics>(OnAnchorDiagnostics);
            bus.Unsubscribe<ReferenceAnchorSaved>(OnReferenceAnchorSaved);
            bus.Unsubscribe<ReferenceAnchorRecovered>(OnReferenceAnchorRecovered);

            bus.Unsubscribe<DeviceScreenChanged>(OnDeviceScreenChanged);
            bus.Unsubscribe<BeamTabSwitched>(OnBeamTabSwitched);
            bus.Unsubscribe<BeamInfoCardToggled>(OnBeamInfoCardToggled);
            bus.Unsubscribe<BeamPOIViewed>(OnBeamPOIViewed);
            bus.Unsubscribe<BeamComparisonViewed>(OnBeamComparisonViewed);
            bus.Unsubscribe<BeamMissionRefViewed>(OnBeamMissionRefViewed);
            bus.Unsubscribe<BeamMapZoomed>(OnBeamMapZoomed);
            bus.Unsubscribe<GlassCaptureStateChanged>(OnGlassCaptureStateChanged);

            bus.Unsubscribe<SurveyItemAnswered>(OnSurveyItemAnswered);
            bus.Unsubscribe<SurveyCompleted>(OnSurveyCompleted);
            bus.Unsubscribe<ComparisonSurveyAnswered>(OnComparisonSurveyAnswered);
            bus.Unsubscribe<ComparisonSurveyCompleted>(OnComparisonSurveyCompleted);

            bus.Unsubscribe<NavigationStateSnapshot>(OnNavigationStateSnapshot);
            bus.Unsubscribe<RouteBindingSummary>(OnRouteBindingSummary);
            bus.Unsubscribe<AppLifecycleEvent>(OnAppLifecycleEvent);

            _subscribed = false;
        }

        // ── Experiment ──────────────────────────────────────────

        private void OnExperimentStateChanged(ExperimentStateChanged e)
        {
            // State transition은 ExperimentManager 내부에서 이미 개별 이벤트로 발행
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

            // sidecar 로거 시작
            StartSidecarLoggers();

            // 세션 메타 기록
            SessionMetaWriter.Instance?.WriteSessionMeta();
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
                extraData: $"{{\"total_duration_s\":{e.TotalDurationSeconds.ToString("F0")}}}");

            // 세션 요약 기록
            SessionMetaWriter.Instance?.WriteSessionSummary();

            // sidecar 로거 종료
            StopSidecarLoggers();

            EventLogger.Instance?.EndSession();
        }

        private void OnPreflightCheckCompleted(PreflightCheckCompleted e)
        {
            EventLogger.Instance?.LogEvent("PREFLIGHT_CHECK",
                extraData: $"{{\"all_passed\":{e.AllPassed.ToString().ToLower()}," +
                           $"\"failed_items\":{e.FailedItems}," +
                           $"\"overridden\":{e.Overridden.ToString().ToLower()}}}");
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
                           $"\"rt_s\":{e.ResponseTimeSeconds.ToString("F1")}}}");
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
                difficultyRating: e.Rating,
                extraData: $"{{\"mission_id\":\"{e.MissionId}\",\"rating\":{e.Rating}}}");
        }

        private void OnMissionCompleted(MissionCompleted e)
        {
            EventLogger.Instance?.LogEvent("MISSION_COMPLETE",
                verificationCorrect: e.Correct,
                durationS: e.DurationSeconds,
                extraData: $"{{\"mission_id\":\"{e.MissionId}\"," +
                           $"\"correct\":{e.Correct.ToString().ToLower()},\"duration_s\":{e.DurationSeconds.ToString("F0")}}}");
        }

        private void OnBriefingForced(BriefingForced e)
        {
            EventLogger.Instance?.LogEvent("BRIEFING_FORCED",
                extraData: $"{{\"mission_id\":\"{e.MissionId}\",\"forced\":true}}");
            SessionMetaWriter.Instance?.RecordForcedEvent();
        }

        private void OnArrivalForced(ArrivalForced e)
        {
            EventLogger.Instance?.LogEvent("ARRIVAL_FORCED",
                extraData: $"{{\"mission_id\":\"{e.MissionId}\",\"forced\":true}}");
            SessionMetaWriter.Instance?.RecordForcedEvent();
        }

        private void OnMissionForceSkipped(MissionForceSkipped e)
        {
            EventLogger.Instance?.LogEvent("MISSION_FORCE_SKIPPED",
                extraData: $"{{\"mission_id\":\"{e.MissionId}\",\"state\":\"{e.State}\",\"forced\":true}}");
            SessionMetaWriter.Instance?.RecordForcedEvent();
        }

        // ── Navigation ──────────────────────────────────────────

        private void OnWaypointReached(WaypointReached e)
        {
            EventLogger.Instance?.LogEvent("WAYPOINT_REACHED", waypointId: e.WaypointId,
                cause: e.Cause,
                extraData: $"{{\"cause\":\"{e.Cause}\",\"is_target\":{e.IsTarget.ToString().ToLower()}}}");
        }

        private void OnTriggerActivated(TriggerActivated e)
        {
            // 트리거 반응 추적 시작
            triggerActiveForResponse = true;
            activeTriggerIdForResponse = e.TriggerId;
            activeTriggerTypeForResponse = e.TriggerType;
            triggerActivatedTime = Time.time;
            triggerResponseRecorded = false;

            EventLogger.Instance?.LogEvent("TRIGGER_ACTIVATED",
                triggerId: e.TriggerId,
                triggerType: e.TriggerType,
                extraData: $"{{\"trigger_type\":\"{e.TriggerType}\",\"trigger_id\":\"{e.TriggerId}\"}}");
        }

        private void OnTriggerDeactivated(TriggerDeactivated e)
        {
            // 트리거 비활성화 시 반응 없으면 TRIGGER_RESPONSE(none) 발행
            if (triggerActiveForResponse && !triggerResponseRecorded)
            {
                float reactionTime = Time.time - triggerActivatedTime;
                DomainEventBus.Instance?.Publish(new TriggerResponse(
                    e.TriggerId, e.TriggerType, reactionTime, "none"));
            }
            triggerActiveForResponse = false;

            EventLogger.Instance?.LogEvent("TRIGGER_DEACTIVATED",
                triggerId: e.TriggerId,
                triggerType: e.TriggerType,
                durationS: e.DurationSeconds,
                extraData: $"{{\"trigger_type\":\"{e.TriggerType}\",\"duration_s\":{e.DurationSeconds.ToString("F1")}}}");
        }

        private void OnTriggerInterrupted(TriggerInterrupted e)
        {
            triggerActiveForResponse = false;

            EventLogger.Instance?.LogEvent("TRIGGER_INTERRUPTED",
                triggerId: e.TriggerId,
                triggerType: e.TriggerType,
                durationS: e.DurationSeconds,
                extraData: $"{{\"trigger_id\":\"{e.TriggerId}\",\"trigger_type\":\"{e.TriggerType}\"," +
                           $"\"duration_s\":{e.DurationSeconds.ToString("F1")},\"reason\":\"{e.Reason}\"}}");
        }

        private void OnRouteCompleted(RouteCompleted e)
        {
            EventLogger.Instance?.LogEvent("ROUTE_END",
                extraData: $"{{\"route_id\":\"{e.RouteId}\"}}");
        }

        private void OnMovementPaused(MovementPaused e)
        {
            EventLogger.Instance?.LogEvent("PAUSE_START",
                waypointId: e.WaypointId,
                extraData: $"{{\"position\":\"{e.Position}\"}}");
        }

        private void OnMovementResumed(MovementResumed e)
        {
            EventLogger.Instance?.LogEvent("PAUSE_END",
                waypointId: e.WaypointId,
                durationS: e.PauseDurationSeconds,
                extraData: $"{{\"duration_s\":{e.PauseDurationSeconds.ToString("F1")},\"position\":\"{e.Position}\"}}");
            SessionMetaWriter.Instance?.RecordPause(e.PauseDurationSeconds);
        }

        private void OnTriggerResponse(TriggerResponse e)
        {
            EventLogger.Instance?.LogEvent("TRIGGER_RESPONSE",
                triggerId: e.TriggerId,
                triggerType: e.TriggerType,
                durationS: e.ReactionTimeSeconds,
                cause: e.FirstAction,
                extraData: $"{{\"trigger_id\":\"{e.TriggerId}\",\"trigger_type\":\"{e.TriggerType}\"," +
                           $"\"reaction_time_s\":{e.ReactionTimeSeconds.ToString("F1")},\"first_action\":\"{e.FirstAction}\"}}");
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
                triggerId: e.TriggerId,
                extraData: $"{{\"{key}\":{e.OffsetAngle.ToString("F1")},\"trigger_id\":\"{e.TriggerId}\"}}");
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
                extraData: $"{{\"drift_m\":{e.DriftMeters.ToString("F2")},\"old_pos\":\"{e.OldPosition}\",\"new_pos\":\"{e.NewPosition}\"}}");
        }

        private void OnHeadingCalibrationApplied(HeadingCalibrationApplied e)
        {
            EventLogger.Instance?.LogEvent("HEADING_CALIBRATION",
                extraData: $"{{\"source\":\"{e.Source}\",\"offset_deg\":{e.OffsetDegrees.ToString("F1")}}}");
        }

        private void OnManualCalibrationApplied(ManualCalibrationApplied e)
        {
            EventLogger.Instance?.LogEvent("MANUAL_CALIBRATION",
                waypointId: e.WaypointId,
                extraData: $"{{\"camera_pos\":\"{e.CameraPosition}\",\"fallback_pos\":\"{e.FallbackPosition}\"," +
                           $"\"heading_offset\":{e.HeadingOffset.ToString("F1")}}}");
        }

        private void OnImageMarkerDetected(ImageMarkerDetected e)
        {
            EventLogger.Instance?.LogEvent("IMAGE_MARKER_DETECTED",
                waypointId: e.MappedWaypointId,
                extraData: $"{{\"marker_id\":\"{e.MarkerId}\",\"slam_pos\":\"{e.SlamPosition}\"," +
                           $"\"heading_offset\":{e.HeadingOffset.ToString("F1")}}}");
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
                "proceed_complete" => "RELOCALIZATION_PROCEED_COMPLETE",
                "proceed_partial" => "RELOCALIZATION_PROCEED_PARTIAL",
                "proceed_no_anchors" => "RELOCALIZATION_PROCEED_NO_ANCHORS",
                "proceed_fallback" => "RELOCALIZATION_PROCEED_FALLBACK",
                _ => "RELOCALIZATION_COMPLETE"
            };
            EventLogger.Instance?.LogEvent(eventType,
                extraData: $"{{\"success_rate\":{e.SuccessRate.ToString("F2")},\"action\":\"{e.Action}\"}}");
        }

        private void OnAnchorDiagnostics(AnchorDiagnostics e)
        {
            EventLogger.Instance?.LogEvent("ANCHOR_DIAGNOSTICS",
                extraData: e.DiagnosticsJson);
        }

        private void OnReferenceAnchorSaved(ReferenceAnchorSaved e)
        {
            EventLogger.Instance?.LogEvent("REFERENCE_ANCHOR_SAVED",
                extraData: $"{{\"room_id\":\"{e.RoomId}\",\"anchor_guid\":\"{e.AnchorGuid}\"}}");
        }

        private void OnReferenceAnchorRecovered(ReferenceAnchorRecovered e)
        {
            EventLogger.Instance?.LogEvent("REFERENCE_ANCHOR_RECOVERED",
                extraData: $"{{\"room_id\":\"{e.RoomId}\"}}");
        }

        // ── Observation ─────────────────────────────────────────

        private void OnDeviceScreenChanged(DeviceScreenChanged e)
        {
            if (e.IsOn)
            {
                EventLogger.Instance?.LogEvent("BEAM_SCREEN_ON");
                BeamSegmentLogger.Instance?.OnBeamScreenOn();

                // 트리거 반응 추적: Beam 켜기가 첫 번째 반응
                if (triggerActiveForResponse && !triggerResponseRecorded)
                {
                    float reactionTime = Time.time - triggerActivatedTime;
                    triggerResponseRecorded = true;
                    DomainEventBus.Instance?.Publish(new TriggerResponse(
                        activeTriggerIdForResponse, activeTriggerTypeForResponse,
                        reactionTime, "beam_switch"));
                }
            }
            else
            {
                float duration = e.DurationSec;
                string extra = duration > 0f
                    ? $"{{\"duration_s\":{duration.ToString("F1")}}}"
                    : "{}";
                EventLogger.Instance?.LogEvent("BEAM_SCREEN_OFF",
                    durationS: duration > 0f ? duration : (float?)null,
                    extraData: extra);
                BeamSegmentLogger.Instance?.OnBeamScreenOff();
                SessionMetaWriter.Instance?.RecordBeamSegment(duration);
            }
        }

        private void OnBeamTabSwitched(BeamTabSwitched e)
        {
            EventLogger.Instance?.LogEvent("BEAM_TAB_SWITCH",
                beamContentType: e.TabName,
                extraData: $"{{\"tab_index\":{e.TabIndex}}}");
            BeamSegmentLogger.Instance?.OnTabSwitch(e.TabName);
        }

        private void OnBeamInfoCardToggled(BeamInfoCardToggled e)
        {
            string eventType = e.Opened ? "BEAM_INFO_CARD_OPENED" : "BEAM_INFO_CARD_CLOSED";
            if (e.Opened)
            {
                EventLogger.Instance?.LogEvent(eventType,
                    beamContentType: e.CardId);
                BeamSegmentLogger.Instance?.OnInfoCardOpened();
            }
            else
            {
                EventLogger.Instance?.LogEvent(eventType,
                    beamContentType: e.CardId,
                    durationS: e.ViewDurationSeconds > 0f ? e.ViewDurationSeconds : (float?)null,
                    extraData: e.ViewDurationSeconds > 0f
                        ? $"{{\"view_duration_s\":{e.ViewDurationSeconds.ToString("F1")}}}"
                        : "{}");
            }
        }

        private void OnBeamPOIViewed(BeamPOIViewed e)
        {
            EventLogger.Instance?.LogEvent("BEAM_POI_VIEWED",
                beamContentType: e.POIId,
                durationS: e.ViewDurationSeconds > 0f ? e.ViewDurationSeconds : (float?)null,
                extraData: e.ViewDurationSeconds > 0f
                    ? $"{{\"view_duration_s\":{e.ViewDurationSeconds.ToString("F1")}}}"
                    : "{}");
            BeamSegmentLogger.Instance?.OnPOIViewed();
        }

        private void OnBeamComparisonViewed(BeamComparisonViewed e)
        {
            EventLogger.Instance?.LogEvent("BEAM_COMPARISON_VIEWED",
                beamContentType: e.ComparisonId);
            BeamSegmentLogger.Instance?.OnComparisonViewed();
        }

        private void OnBeamMissionRefViewed(BeamMissionRefViewed e)
        {
            EventLogger.Instance?.LogEvent("BEAM_MISSION_REF_VIEWED",
                beamContentType: e.MissionId);
            BeamSegmentLogger.Instance?.OnMissionRefViewed();
        }

        private void OnBeamMapZoomed(BeamMapZoomed e)
        {
            EventLogger.Instance?.LogEvent("BEAM_MAP_ZOOMED",
                extraData: $"{{\"zoom_level\":{e.ZoomLevel.ToString("F1")}}}");
            BeamSegmentLogger.Instance?.OnMapZoomed();
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

        // ── Survey ─────────────────────────────────────────────

        private void OnSurveyItemAnswered(SurveyItemAnswered e)
        {
            EventLogger.Instance?.LogEvent("SURVEY_ITEM_ANSWERED",
                extraData: $"{{\"survey_type\":\"{e.SurveyType}\",\"item_key\":\"{e.ItemKey}\"," +
                           $"\"rating\":{e.Rating},\"run\":{e.RunNumber},\"condition\":\"{e.Condition}\"}}");
        }

        private void OnSurveyCompleted(SurveyCompleted e)
        {
            EventLogger.Instance?.LogEvent("SURVEY_COMPLETED",
                durationS: e.DurationSec,
                extraData: $"{{\"survey_type\":\"{e.SurveyType}\",\"run\":{e.RunNumber}," +
                           $"\"condition\":\"{e.Condition}\",\"duration_s\":{e.DurationSec.ToString("F0")}}}");
        }

        private void OnComparisonSurveyAnswered(ComparisonSurveyAnswered e)
        {
            EventLogger.Instance?.LogEvent("COMPARISON_SURVEY_ANSWERED",
                extraData: $"{{\"question_key\":\"{e.QuestionKey}\"," +
                           $"\"answer\":\"{EscapeJson(e.Answer)}\",\"response_type\":\"{e.ResponseType}\"}}");
        }

        private void OnComparisonSurveyCompleted(ComparisonSurveyCompleted e)
        {
            EventLogger.Instance?.LogEvent("COMPARISON_SURVEY_COMPLETED",
                durationS: e.DurationSec,
                extraData: $"{{\"duration_s\":{e.DurationSec.ToString("F0")}}}");
        }

        // ── Diagnostics ────────────────────────────────────────

        private void OnNavigationStateSnapshot(NavigationStateSnapshot e)
        {
            EventLogger.Instance?.LogEvent("NAV_STATE_SNAPSHOT",
                waypointId: e.WaypointId,
                distanceM: e.DistanceToTarget,
                anchorBound: e.IsAnchorBound,
                arrowVisible: e.ArrowVisible,
                extraData: $"{{\"anchor_bound\":{e.IsAnchorBound.ToString().ToLower()}," +
                           $"\"player_pos\":\"{e.PlayerPosition}\"," +
                           $"\"target_pos\":\"{e.TargetPosition}\"," +
                           $"\"distance_m\":{e.DistanceToTarget.ToString("F2")}," +
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
                extraData: $"{{\"time_since_startup\":{e.TimeSinceStartup.ToString("F1")}}}");
        }

        // ── Sidecar Logger 관리 ──────────────────────────────────

        private void StartSidecarLoggers()
        {
            HeadPoseTraceLogger.Instance?.StartLogging();
            NavigationTraceLogger.Instance?.StartLogging();
            AnchorRelocLogger.Instance?.StartLogging();
            BeamSegmentLogger.Instance?.StartLogging();
            SystemHealthLogger.Instance?.StartLogging();
        }

        private void StopSidecarLoggers()
        {
            HeadPoseTraceLogger.Instance?.StopLogging();
            NavigationTraceLogger.Instance?.StopLogging();
            AnchorRelocLogger.Instance?.StopLogging();
            BeamSegmentLogger.Instance?.StopLogging();
            SystemHealthLogger.Instance?.StopLogging();
        }

        // ── Util ────────────────────────────────────────────────

        private static string EscapeJson(string text)
        {
            return text?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") ?? "";
        }
    }
}

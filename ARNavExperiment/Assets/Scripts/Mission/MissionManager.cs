using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ARNavExperiment.Core;
using ARNavExperiment.Logging;
using ARNavExperiment.Navigation;
using ARNavExperiment.UI;
using ARNavExperiment.BeamPro;

namespace ARNavExperiment.Mission
{
    public enum MissionState
    {
        Idle,
        Briefing,
        Navigation,
        Arrival,
        Verification,
        ConfidenceRating,
        DifficultyRating,
        Scored
    }

    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        [Header("Mission Data")]
        [SerializeField] private List<MissionData> routeAMissions;
        [SerializeField] private List<MissionData> routeBMissions;

        [Header("UI References")]
        [SerializeField] private MissionBriefingUI briefingUI;
        [SerializeField] private VerificationUI verificationUI;
        [SerializeField] private ConfidenceRatingUI confidenceRatingUI;
        [SerializeField] private DifficultyRatingUI difficultyRatingUI;

        [Header("Navigation")]
        [SerializeField] private ARArrowRenderer arrowRenderer;

        public MissionState CurrentState { get; private set; } = MissionState.Idle;
        public MissionData CurrentMission { get; private set; }
        public int CurrentMissionIndex { get; private set; }

        private List<MissionData> activeMissions;
        private float missionStartTime;
        private Coroutine pendingAdvance;

        public event System.Action<MissionData> OnMissionStarted;
        public event System.Action<MissionData, bool> OnMissionCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (WaypointManager.Instance != null)
                WaypointManager.Instance.OnWaypointReached += OnWaypointReached;
        }

        public void LoadMissions(string routeId)
        {
            activeMissions = routeId == "A" ? routeAMissions : routeBMissions;
            CurrentMissionIndex = 0;
            Debug.Log($"[MissionManager] Loaded {activeMissions.Count} missions for Route {routeId}");
        }

        public void StartNextMission()
        {
            if (activeMissions == null || CurrentMissionIndex >= activeMissions.Count)
            {
                Debug.Log("[MissionManager] All missions complete");
                return;
            }

            CurrentMission = activeMissions[CurrentMissionIndex];
            missionStartTime = Time.time;
            EventLogger.Instance?.SetMissionId(CurrentMission.missionId);

            TransitionTo(MissionState.Briefing);
        }

        private void TransitionTo(MissionState newState)
        {
            CurrentState = newState;

            switch (newState)
            {
                case MissionState.Briefing:
                    ShowBriefing();
                    break;
                case MissionState.Navigation:
                    StartNavigation();
                    break;
                case MissionState.Arrival:
                    OnArrival();
                    break;
                case MissionState.Verification:
                    ShowVerification();
                    break;
                case MissionState.ConfidenceRating:
                    ShowConfidenceRating();
                    break;
                case MissionState.DifficultyRating:
                    ShowDifficultyRating();
                    break;
                case MissionState.Scored:
                    CompleteMission();
                    break;
            }
        }

        private void ShowBriefing()
        {
            EventLogger.Instance?.LogEvent("MISSION_START",
                extraData: $"{{\"mission_id\":\"{CurrentMission.missionId}\"," +
                           $"\"type\":\"{CurrentMission.type}\"," +
                           $"\"briefing\":\"{EscapeJson(CurrentMission.briefingText)}\"}}");

            if (briefingUI != null)
            {
                briefingUI.Show(CurrentMission, () => TransitionTo(MissionState.Navigation));
            }
            else
            {
                Debug.LogWarning("[MissionManager] briefingUI null — auto-advancing to Navigation");
                TransitionTo(MissionState.Navigation);
            }

            // activate trigger if associated
            if (!string.IsNullOrEmpty(CurrentMission.associatedTriggerId))
            {
                var triggerType = ParseTriggerType(CurrentMission.associatedTriggerId);
                TriggerController.Instance?.ActivateTrigger(triggerType, CurrentMission.associatedTriggerId);
            }

            // BeamPro 데이터 로드
            var hub = BeamProHubController.Instance;
            if (hub != null)
            {
                hub.MissionRef?.LoadMission(CurrentMission);
                if (CurrentMission.infoCards != null && CurrentMission.infoCards.Length > 0)
                    hub.InfoCardMgr?.LoadCards(CurrentMission.infoCards);
                if (CurrentMission.relevantPOIs != null)
                    hub.MapController?.LoadPOIs(CurrentMission.relevantPOIs);
            }

            OnMissionStarted?.Invoke(CurrentMission);
        }

        private void StartNavigation()
        {
            Debug.Log($"[MissionManager] Navigating to {CurrentMission.targetWaypointId}");
            arrowRenderer?.Show();
        }

        private void OnWaypointReached(Waypoint wp)
        {
            if (CurrentMission == null || CurrentState != MissionState.Navigation) return;

            // BeamPro info card 자동 표시
            BeamProHubController.Instance?.InfoCardMgr?.TryAutoShow(wp.waypointId);

            if (wp.waypointId == CurrentMission.targetWaypointId)
            {
                TransitionTo(MissionState.Arrival);
            }
        }

        private void OnArrival()
        {
            EventLogger.Instance?.LogEvent("MISSION_ARRIVAL",
                waypointId: CurrentMission.targetWaypointId,
                extraData: $"{{\"mission_id\":\"{CurrentMission.missionId}\"}}");

            TransitionTo(MissionState.Verification);
        }

        private void ShowVerification()
        {
            if (verificationUI != null)
            {
                verificationUI.Show(CurrentMission, OnVerificationAnswered);
            }
            else
            {
                Debug.LogWarning("[MissionManager] verificationUI null — auto-advancing to ConfidenceRating");
                TransitionTo(MissionState.ConfidenceRating);
            }
        }

        private void OnVerificationAnswered(int selectedIndex, float responseTime)
        {
            bool correct = selectedIndex == CurrentMission.correctAnswerIndex;

            EventLogger.Instance?.LogEvent("VERIFICATION_ANSWERED",
                waypointId: CurrentMission.targetWaypointId,
                verificationCorrect: correct,
                extraData: $"{{\"mission_id\":\"{CurrentMission.missionId}\"," +
                           $"\"answer\":{selectedIndex},\"correct\":{correct.ToString().ToLower()}," +
                           $"\"rt_s\":{responseTime:F1}}}");

            TransitionTo(MissionState.ConfidenceRating);
        }

        private void ShowConfidenceRating()
        {
            if (confidenceRatingUI != null)
            {
                confidenceRatingUI.Show("방금 답변에 대해 얼마나 확신하십니까?\n(1: 전혀 확신 없음 ~ 7: 매우 확신)", OnConfidenceRated);
            }
            else
            {
                // UI 없으면 건너뛰기
                TransitionTo(MissionState.Scored);
            }
        }

        private void OnConfidenceRated(int rating)
        {
            EventLogger.Instance?.LogEvent("CONFIDENCE_RATED",
                waypointId: CurrentMission.targetWaypointId,
                confidenceRating: rating,
                extraData: $"{{\"mission_id\":\"{CurrentMission.missionId}\",\"rating\":{rating}}}");

            TransitionTo(MissionState.DifficultyRating);
        }

        private void ShowDifficultyRating()
        {
            if (difficultyRatingUI != null)
            {
                difficultyRatingUI.Show(CurrentMission.missionId, OnDifficultyRated);
            }
            else
            {
                TransitionTo(MissionState.Scored);
            }
        }

        private void OnDifficultyRated(int rating)
        {
            TransitionTo(MissionState.Scored);
        }

        private void CompleteMission()
        {
            bool correct = false;
            if (verificationUI != null)
                correct = verificationUI.LastAnswerCorrect;

            float duration = Time.time - missionStartTime;
            EventLogger.Instance?.LogEvent("MISSION_COMPLETE",
                extraData: $"{{\"mission_id\":\"{CurrentMission.missionId}\"," +
                           $"\"correct\":{correct.ToString().ToLower()},\"duration_s\":{duration:F0}}}");

            // deactivate trigger if active
            if (!string.IsNullOrEmpty(CurrentMission.associatedTriggerId))
                TriggerController.Instance?.DeactivateCurrentTrigger();

            OnMissionCompleted?.Invoke(CurrentMission, correct);
            CurrentMissionIndex++;

            // auto-start next mission after short delay
            if (pendingAdvance != null) StopCoroutine(pendingAdvance);
            if (CurrentMissionIndex < activeMissions.Count)
            {
                pendingAdvance = StartCoroutine(DelayedAction(2f, StartNextMission));
            }
            else
            {
                Debug.Log("[MissionManager] All missions in route completed — auto advancing experiment state");
                arrowRenderer?.Hide();
                pendingAdvance = StartCoroutine(DelayedAction(2f, AutoAdvanceExperiment));
            }
        }

        private void AutoAdvanceExperiment()
        {
            ExperimentManager.Instance?.AdvanceState();
        }

        private IEnumerator DelayedAction(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            pendingAdvance = null;
            action?.Invoke();
        }

        private TriggerType ParseTriggerType(string triggerId)
        {
            return triggerId switch
            {
                "T1" => TriggerType.T1_TrackingDegradation,
                "T2" => TriggerType.T2_InformationConflict,
                "T3" => TriggerType.T3_LowResolution,
                "T4" => TriggerType.T4_GuidanceAbsence,
                _ => TriggerType.T1_TrackingDegradation
            };
        }

        private string EscapeJson(string text)
        {
            return text?.Replace("\"", "\\\"").Replace("\n", "\\n") ?? "";
        }

        private void OnDestroy()
        {
            if (WaypointManager.Instance != null)
                WaypointManager.Instance.OnWaypointReached -= OnWaypointReached;
        }
    }
}

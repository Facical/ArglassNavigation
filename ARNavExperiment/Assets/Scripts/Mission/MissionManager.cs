using System.Collections.Generic;
using UnityEngine;
using ARNavExperiment.Core;
using ARNavExperiment.Logging;
using ARNavExperiment.Navigation;

namespace ARNavExperiment.Mission
{
    public enum MissionState
    {
        Idle,
        Briefing,
        Navigation,
        Arrival,
        Verification,
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

        public MissionState CurrentState { get; private set; } = MissionState.Idle;
        public MissionData CurrentMission { get; private set; }
        public int CurrentMissionIndex { get; private set; }

        private List<MissionData> activeMissions;
        private float missionStartTime;

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

            // activate trigger if associated
            if (!string.IsNullOrEmpty(CurrentMission.associatedTriggerId))
            {
                var triggerType = ParseTriggerType(CurrentMission.associatedTriggerId);
                TriggerController.Instance?.ActivateTrigger(triggerType, CurrentMission.associatedTriggerId);
            }

            OnMissionStarted?.Invoke(CurrentMission);
        }

        private void StartNavigation()
        {
            Debug.Log($"[MissionManager] Navigating to {CurrentMission.targetWaypointId}");
        }

        private void OnWaypointReached(Waypoint wp)
        {
            if (CurrentMission == null || CurrentState != MissionState.Navigation) return;

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
            if (CurrentMissionIndex < activeMissions.Count)
            {
                Invoke(nameof(StartNextMission), 2f);
            }
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

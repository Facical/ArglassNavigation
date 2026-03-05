using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ARNavExperiment.Core;
using ARNavExperiment.Navigation;
using ARNavExperiment.Presentation.Glass;
using ARNavExperiment.Presentation.BeamPro;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;

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
        private string activeRouteId;
        private float missionStartTime;
        private Coroutine pendingAdvance;
        private BeamProCanvasController beamProCanvasCtrl;

        public bool HasLoadedMissions => activeMissions != null && activeMissions.Count > 0;
        public bool IsMissionActive => CurrentState != MissionState.Idle && CurrentState != MissionState.Scored;

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
            beamProCanvasCtrl = FindObjectOfType<BeamProCanvasController>();
        }

        public void LoadMissions(string routeId)
        {
            var source = routeId == "A" ? routeAMissions : routeBMissions;
            if (source == null || source.Count == 0)
            {
                Debug.LogWarning($"[MissionManager] route{routeId}Missions SerializedField is null/empty — trying Resources fallback");
                source = TryRuntimeLoadMissions(routeId);
            }
            if (source == null || source.Count == 0)
            {
                Debug.LogError($"[MissionManager] route{routeId}Missions failed to load from both SerializedField and Resources!");
                activeMissions = new List<MissionData>();
                CurrentMissionIndex = 0;
                return;
            }
            activeMissions = source;
            activeRouteId = routeId;
            CurrentMissionIndex = 0;
            Debug.Log($"[MissionManager] Loaded {activeMissions.Count} missions for Route {routeId}");
        }

        /// <summary>
        /// SerializedField가 null일 때 Resources 폴더에서 미션 데이터를 로드합니다.
        /// 미션 순서: A1 → B1 → A2 → B2 → C1
        /// </summary>
        private List<MissionData> TryRuntimeLoadMissions(string routeId)
        {
            string prefix = $"Route{routeId}_";
            string[] missionOrder = { "A1", "B1", "A2", "B2", "C1" };
            var result = new List<MissionData>();

            foreach (var suffix in missionOrder)
            {
                string assetPath = $"Data/Missions/{prefix}{suffix}";
                var mission = Resources.Load<MissionData>(assetPath);
                if (mission != null)
                {
                    result.Add(mission);
                    Debug.Log($"[MissionManager] Resources fallback loaded: {assetPath}");
                }
                else
                {
                    Debug.LogWarning($"[MissionManager] Resources fallback FAILED: {assetPath}");
                }
            }

            if (result.Count > 0)
            {
                // 성공 시 SerializedField에도 캐시 (다음 호출 시 바로 사용)
                if (routeId == "A") routeAMissions = result;
                else routeBMissions = result;
            }

            return result;
        }

        public void StartNextMission()
        {
            if (activeMissions == null || activeMissions.Count == 0)
            {
                Debug.LogError("[MissionManager] Cannot start mission — no missions loaded. " +
                    "Check that routeAMissions/routeBMissions are assigned in Inspector.");
                return;
            }
            if (CurrentMissionIndex >= activeMissions.Count)
            {
                Debug.Log("[MissionManager] All missions complete");
                return;
            }

            CurrentMission = activeMissions[CurrentMissionIndex];
            missionStartTime = Time.time;
            // SetMissionId는 ObservationService.OnMissionStarted에서 처리

            TransitionTo(MissionState.Briefing);
        }

        /// <summary>
        /// 실험자가 Briefing 상태를 건너뛰고 Navigation으로 강제 전환.
        /// 글래스 WorldSpace UI 상호작용 실패 시 사용.
        /// </summary>
        public void AdvanceBriefing()
        {
            if (CurrentState != MissionState.Briefing) return;
            briefingUI?.Hide();
            DomainEventBus.Instance?.Publish(new BriefingForced(CurrentMission?.missionId ?? ""));
            TransitionTo(MissionState.Navigation);
        }

        /// <summary>
        /// 실험자가 도착을 수동 선언. Fallback 모드에서 거리 체크가 불가능할 때 사용.
        /// </summary>
        public void ForceArrival()
        {
            if (CurrentState != MissionState.Navigation) return;
            arrowRenderer?.Hide();
            DomainEventBus.Instance?.Publish(new ArrivalForced(CurrentMission?.missionId ?? ""));
            TransitionTo(MissionState.Arrival);
        }

        /// <summary>
        /// 미션을 강제 건너뛰기. 어떤 미션 상태에서든 호출 가능.
        /// UI 모두 닫고, 트리거 해제, 다음 미션으로 진행.
        /// </summary>
        public void ForceSkipMission()
        {
            if (CurrentState == MissionState.Idle || CurrentState == MissionState.Scored) return;
            if (CurrentMission == null) return;

            // 모든 UI 닫기
            briefingUI?.Hide();
            verificationUI?.Hide();
            confidenceRatingUI?.Hide();
            difficultyRatingUI?.Hide();
            arrowRenderer?.Hide();

            // 트리거 해제
            if (!string.IsNullOrEmpty(CurrentMission.associatedTriggerId))
                NavigationService.Instance?.DeactivateTrigger();

            DomainEventBus.Instance?.Publish(new MissionForceSkipped(
                CurrentMission.missionId, CurrentState.ToString()));

            OnMissionCompleted?.Invoke(CurrentMission, false);
            CurrentMissionIndex++;

            // 대기 중인 코루틴 취소
            if (pendingAdvance != null)
            {
                StopCoroutine(pendingAdvance);
                pendingAdvance = null;
            }

            // 다음 미션 또는 실험 전진
            if (CurrentMissionIndex < activeMissions.Count)
            {
                CurrentState = MissionState.Idle;
                StartNextMission();
            }
            else
            {
                CurrentState = MissionState.Idle;
                arrowRenderer?.Hide();
                DomainEventBus.Instance?.Publish(new AllMissionsCompleted(activeRouteId ?? ""));
            }
        }

        private void TransitionTo(MissionState newState)
        {
            CurrentState = newState;
            UpdateCanvasRaycasters(newState);

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

        /// <summary>
        /// GlassOnly 모드에서 Navigation 중 ExperimentCanvas raycaster를 꺼서
        /// 뒤의 BeamProCanvas 허브에 핸드트래킹 ray가 도달하게 합니다.
        /// Briefing/Verification/Rating 상태에서는 다시 켭니다.
        /// </summary>
        private void UpdateCanvasRaycasters(MissionState state)
        {
            var glassCanvas = GlassCanvasController.Instance;
            if (glassCanvas == null) return;

            bool needsExperimentRaycaster = state != MissionState.Navigation;
            glassCanvas.SetRaycasterEnabled(needsExperimentRaycaster);

            // GlassOnly WorldSpace에서 Briefing/Verification/Rating 중 BeamPro 숨김
            bool showBeamPro = state == MissionState.Navigation || state == MissionState.Idle;
            if (beamProCanvasCtrl != null)
                beamProCanvasCtrl.SetGlassVisibility(showBeamPro);
        }

        private void ShowBriefing()
        {
            DomainEventBus.Instance?.Publish(new MissionStarted(
                CurrentMission.missionId,
                activeRouteId ?? "",
                CurrentMission.type.ToString(),
                CurrentMission.briefingText ?? ""));

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
                NavigationService.Instance?.ActivateTrigger(CurrentMission.associatedTriggerId);
            }

            // BeamPro 데이터 로드는 BeamProCoordinator가 MissionStarted 이벤트를 구독하여 처리

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

            // BeamPro info card 자동 표시는 BeamProCoordinator에서 처리 가능
            // (현재는 WaypointReached 이벤트 기반으로 직접 처리)

            if (wp.waypointId == CurrentMission.targetWaypointId)
            {
                TransitionTo(MissionState.Arrival);
            }
        }

        private void OnArrival()
        {
            DomainEventBus.Instance?.Publish(new MissionArrived(
                CurrentMission.missionId, CurrentMission.targetWaypointId));

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

            DomainEventBus.Instance?.Publish(new VerificationAnswered(
                CurrentMission.missionId, CurrentMission.targetWaypointId,
                selectedIndex, correct, responseTime));

            TransitionTo(MissionState.ConfidenceRating);
        }

        private void ShowConfidenceRating()
        {
            if (confidenceRatingUI != null)
            {
                confidenceRatingUI.Show("How confident are you in your answer?\n(1: Not at all ~ 7: Very confident)", OnConfidenceRated);
            }
            else
            {
                // UI 없으면 건너뛰기
                TransitionTo(MissionState.Scored);
            }
        }

        private void OnConfidenceRated(int rating)
        {
            DomainEventBus.Instance?.Publish(new ConfidenceRated(
                CurrentMission.missionId, CurrentMission.targetWaypointId, rating));

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
            DomainEventBus.Instance?.Publish(new DifficultyRated(
                CurrentMission.missionId, rating));
            TransitionTo(MissionState.Scored);
        }

        private void CompleteMission()
        {
            bool correct = false;
            if (verificationUI != null)
                correct = verificationUI.LastAnswerCorrect;

            float duration = Time.time - missionStartTime;
            DomainEventBus.Instance?.Publish(new MissionCompleted(
                CurrentMission.missionId, correct, duration));

            // 맵 정리는 BeamProCoordinator가 MissionCompleted 이벤트를 구독하여 처리

            // deactivate trigger if active
            if (!string.IsNullOrEmpty(CurrentMission.associatedTriggerId))
                NavigationService.Instance?.DeactivateTrigger();

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
                Debug.Log("[MissionManager] All missions in route completed");
                arrowRenderer?.Hide();
                // 역방향 의존성 제거: ExperimentManager.AdvanceState() 직접 호출 대신 도메인 이벤트 발행
                // ExperimentAdvancer가 이 이벤트를 구독하여 ExperimentManager를 전진시킴
                pendingAdvance = StartCoroutine(DelayedAction(2f, () =>
                    DomainEventBus.Instance?.Publish(new AllMissionsCompleted(activeRouteId ?? ""))));
            }
        }

        private IEnumerator DelayedAction(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            pendingAdvance = null;
            action?.Invoke();
        }

        /// <summary>
        /// 외부에서 미션 진행을 강제 중단할 때 호출.
        /// 화살표 숨김, 트리거 해제, 상태 초기화.
        /// </summary>
        public void ResetState()
        {
            if (pendingAdvance != null)
            {
                StopCoroutine(pendingAdvance);
                pendingAdvance = null;
            }

            if (!string.IsNullOrEmpty(CurrentMission?.associatedTriggerId))
                NavigationService.Instance?.DeactivateTrigger();

            arrowRenderer?.Hide();
            CurrentState = MissionState.Idle;
            CurrentMission = null;
            CurrentMissionIndex = 0;
            activeMissions = null;

            Debug.Log("[MissionManager] State reset");
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

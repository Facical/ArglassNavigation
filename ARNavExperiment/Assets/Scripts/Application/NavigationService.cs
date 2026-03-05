using UnityEngine;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Navigation;
using ARNavExperiment.Mission;

namespace ARNavExperiment.Application
{
    /// <summary>
    /// MissionManager에서 분리된 내비게이션 조율 서비스.
    /// 도메인 이벤트를 구독하여 AR 화살표/트리거를 제어.
    /// MissionManager가 ARArrowRenderer/TriggerController를 직접 참조하지 않도록 중재.
    /// </summary>
    public class NavigationService : MonoBehaviour
    {
        public static NavigationService Instance { get; private set; }

        [SerializeField] private ARArrowRenderer arrowRenderer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            var bus = DomainEventBus.Instance;
            if (bus == null) return;

            bus.Subscribe<MissionCompleted>(OnMissionCompleted);
            bus.Subscribe<MissionForceSkipped>(OnMissionForceSkipped);
        }

        private void OnDisable()
        {
            var bus = DomainEventBus.Instance;
            if (bus == null) return;

            bus.Unsubscribe<MissionCompleted>(OnMissionCompleted);
            bus.Unsubscribe<MissionForceSkipped>(OnMissionForceSkipped);
        }

        public void ShowArrow() => arrowRenderer?.Show();
        public void HideArrow() => arrowRenderer?.Hide();

        public void ActivateTrigger(string triggerId)
        {
            var triggerType = ParseTriggerType(triggerId);
            TriggerController.Instance?.ActivateTrigger(triggerType, triggerId);
        }

        public void DeactivateTrigger()
        {
            TriggerController.Instance?.DeactivateCurrentTrigger();
        }

        private void OnMissionCompleted(MissionCompleted e)
        {
            // 미션 완료 시 트리거 해제는 MissionManager.CompleteMission에서 처리
        }

        private void OnMissionForceSkipped(MissionForceSkipped e)
        {
            // 강제 스킵 시 화살표/트리거 정리는 MissionManager.ForceSkipMission에서 처리
        }

        private static TriggerType ParseTriggerType(string triggerId)
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
    }
}

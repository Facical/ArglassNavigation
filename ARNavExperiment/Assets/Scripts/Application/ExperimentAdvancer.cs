using UnityEngine;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Core;

namespace ARNavExperiment.Application
{
    /// <summary>
    /// MissionManager → ExperimentManager.AdvanceState() 역방향 의존성을 제거.
    /// 전체 경로 미션 완료 시 도메인 이벤트를 구독하여 ExperimentManager를 전진시킴.
    ///
    /// 이전: MissionManager.AutoAdvanceExperiment() → ExperimentManager.Instance.AdvanceState()
    /// 이후: MissionManager publishes AllMissionsCompleted → ExperimentAdvancer → ExperimentManager.AdvanceState()
    /// </summary>
    public class ExperimentAdvancer : MonoBehaviour
    {
        private void OnEnable()
        {
            var bus = DomainEventBus.Instance;
            if (bus == null) return;

            bus.Subscribe<AllMissionsCompleted>(OnAllMissionsCompleted);
        }

        private void OnDisable()
        {
            var bus = DomainEventBus.Instance;
            if (bus == null) return;

            bus.Unsubscribe<AllMissionsCompleted>(OnAllMissionsCompleted);
        }

        private void OnAllMissionsCompleted(AllMissionsCompleted e)
        {
            Debug.Log($"[ExperimentAdvancer] All missions completed for route {e.RouteId} — advancing experiment state");
            ExperimentManager.Instance?.AdvanceState();
        }
    }
}

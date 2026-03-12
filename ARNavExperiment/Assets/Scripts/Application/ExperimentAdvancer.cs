using System.Collections;
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
        private bool _subscribed;

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

            bus.Subscribe<AllMissionsCompleted>(OnAllMissionsCompleted);

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

            bus.Unsubscribe<AllMissionsCompleted>(OnAllMissionsCompleted);

            _subscribed = false;
        }

        private void OnAllMissionsCompleted(AllMissionsCompleted e)
        {
            Debug.Log($"[ExperimentAdvancer] All missions completed for route {e.RouteId} — advancing experiment state");
            ExperimentManager.Instance?.AdvanceState();
        }
    }
}

using System.Collections;
using UnityEngine;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;

namespace ARNavExperiment.Navigation
{
    public enum TriggerType
    {
        T1_TrackingDegradation,
        T2_InformationConflict,
        T3_LowResolution,
        T4_GuidanceAbsence
    }

    public class TriggerController : MonoBehaviour
    {
        public static TriggerController Instance { get; private set; }

        [Header("References")]
        [SerializeField] private ARArrowRenderer arrowRenderer;

        [Header("T1 Settings")]
        [SerializeField] private float t1JitterDuration = 6f;
        [SerializeField] private float t1JitterAngle = 5f;
        [SerializeField] private float t1BlackoutDuration = 3f;

        [Header("T3 Settings")]
        [SerializeField] private float t3SpreadAngle = 30f;

        [Header("T4 Settings")]
        [SerializeField] private string t4MessageText = "Near destination";

        [Header("T4 UI")]
        [SerializeField] private TMPro.TextMeshProUGUI proximityText;

        private Coroutine activeTrigger;
        private float triggerStartTime;
        private TriggerType currentTriggerType;
        private string currentTriggerId;
        private bool triggerCompleted;

        /// <summary>현재 활성 트리거 ID (없으면 null)</summary>
        public string ActiveTriggerId => activeTrigger != null ? currentTriggerId : null;

        /// <summary>현재 활성 트리거 타입 문자열 (없으면 null)</summary>
        public string ActiveTriggerType => activeTrigger != null ? currentTriggerType.ToString() : null;

        /// <summary>현재 트리거 시작 시간</summary>
        public float TriggerStartTime => triggerStartTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ActivateTrigger(TriggerType type, string triggerId)
        {
            // 기존 트리거가 미완료 상태면 중단 이벤트 발행
            if (activeTrigger != null && !triggerCompleted)
            {
                float duration = Time.time - triggerStartTime;
                DomainEventBus.Instance?.Publish(new TriggerInterrupted(
                    currentTriggerId, currentTriggerType.ToString(), duration,
                    $"replaced_by_{triggerId}"));
                CleanupTriggerVisuals();
                StopCoroutine(activeTrigger);
                activeTrigger = null;
            }
            else if (activeTrigger != null)
            {
                StopCoroutine(activeTrigger);
                activeTrigger = null;
            }

            currentTriggerType = type;
            currentTriggerId = triggerId;
            triggerCompleted = false;
            triggerStartTime = Time.time;
            DomainEventBus.Instance?.Publish(new TriggerActivated(triggerId, type.ToString()));

            switch (type)
            {
                case TriggerType.T1_TrackingDegradation:
                    activeTrigger = StartCoroutine(RunT1());
                    break;
                case TriggerType.T2_InformationConflict:
                    // T2 is physical signage conflict, no arrow change needed
                    LogTriggerComplete(type, triggerId);
                    break;
                case TriggerType.T3_LowResolution:
                    activeTrigger = StartCoroutine(RunT3());
                    break;
                case TriggerType.T4_GuidanceAbsence:
                    activeTrigger = StartCoroutine(RunT4());
                    break;
            }
        }

        private IEnumerator RunT1()
        {
            arrowRenderer.SetTriggerMode(true);

            // jitter phase
            float elapsed = 0f;
            while (elapsed < t1JitterDuration)
            {
                float jitter = Random.Range(-t1JitterAngle, t1JitterAngle);
                var dir = WaypointManager.Instance.GetDirectionToNext();
                float baseYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float jitteredYaw = baseYaw + jitter;
                var jitteredDir = new Vector3(Mathf.Sin(jitteredYaw * Mathf.Deg2Rad), 0, Mathf.Cos(jitteredYaw * Mathf.Deg2Rad));
                arrowRenderer.UpdateDirection(jitteredDir);

                DomainEventBus.Instance?.Publish(new ArrowOffset("T1", jitter));

                elapsed += 0.2f;
                yield return new WaitForSeconds(0.2f);
            }

            // blackout phase
            arrowRenderer.Hide();
            yield return new WaitForSeconds(t1BlackoutDuration);

            // recovery
            arrowRenderer.Show();
            arrowRenderer.SetTriggerMode(false);
            LogTriggerComplete(TriggerType.T1_TrackingDegradation, "T1");
            activeTrigger = null;
        }

        private IEnumerator RunT3()
        {
            arrowRenderer.SetTriggerMode(true);
            arrowRenderer.SetFanMode(true, t3SpreadAngle);

            DomainEventBus.Instance?.Publish(new ArrowOffset("T3", t3SpreadAngle));

            // 미션 완료까지 유지 — DeactivateCurrentTrigger()의 StopCoroutine()으로 종료
            while (true)
            {
                var dir = WaypointManager.Instance.GetDirectionToNext();
                arrowRenderer.UpdateDirection(dir);
                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>
        /// T4: 화살표를 숨기고 근접 텍스트를 표시.
        /// 코루틴은 초기 설정 후 즉시 종료(yield return null).
        /// 시각 효과(화살표 숨김 + 텍스트 표시)는 DeactivateCurrentTrigger()가 호출될 때까지 유지.
        /// </summary>
        private IEnumerator RunT4()
        {
            arrowRenderer.Hide();

            if (proximityText != null)
            {
                proximityText.text = t4MessageText;
                proximityText.gameObject.SetActive(true);
            }

            // T4 stays active until mission completion — visuals persist until DeactivateCurrentTrigger()
            yield return null;
        }

        public void DeactivateCurrentTrigger()
        {
            if (activeTrigger != null)
            {
                StopCoroutine(activeTrigger);
                activeTrigger = null;
            }

            // T3/T4 등 미션 완료로 해제되는 트리거 로그
            if (!triggerCompleted)
                LogTriggerComplete(currentTriggerType, currentTriggerId);

            CleanupTriggerVisuals();
        }

        /// <summary>
        /// 트리거 시각 효과를 모두 초기화.
        /// </summary>
        private void CleanupTriggerVisuals()
        {
            arrowRenderer.SetFanMode(false, 0);
            arrowRenderer.SetTriggerMode(false);
            arrowRenderer.Show();

            if (proximityText != null)
                proximityText.gameObject.SetActive(false);
        }

        private void LogTriggerComplete(TriggerType type, string triggerId)
        {
            triggerCompleted = true;
            float duration = Time.time - triggerStartTime;
            DomainEventBus.Instance?.Publish(new TriggerDeactivated(triggerId, type.ToString(), duration));
        }
    }
}

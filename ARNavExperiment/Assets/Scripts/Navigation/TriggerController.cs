using System.Collections;
using UnityEngine;
using ARNavExperiment.Logging;

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
        [SerializeField] private string t4MessageText = "목적지 근처입니다";

        [Header("T4 UI")]
        [SerializeField] private TMPro.TextMeshProUGUI proximityText;

        private Coroutine activeTrigger;
        private float triggerStartTime;
        private TriggerType currentTriggerType;
        private string currentTriggerId;
        private bool triggerCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ActivateTrigger(TriggerType type, string triggerId)
        {
            if (activeTrigger != null)
                StopCoroutine(activeTrigger);

            currentTriggerType = type;
            currentTriggerId = triggerId;
            triggerCompleted = false;
            triggerStartTime = Time.time;
            EventLogger.Instance?.LogEvent("TRIGGER_ACTIVATED",
                extraData: $"{{\"trigger_type\":\"{type}\",\"trigger_id\":\"{triggerId}\"}}");

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

                EventLogger.Instance?.LogEvent("GLASS_ARROW_OFFSET",
                    extraData: $"{{\"offset_angle\":{jitter:F1},\"trigger_id\":\"T1\"}}");

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

            // fan-out arrow display
            // The visual spread effect would be handled by a shader or multiple arrow instances
            // Here we log the trigger and maintain the state
            EventLogger.Instance?.LogEvent("GLASS_ARROW_OFFSET",
                extraData: $"{{\"spread_angle\":{t3SpreadAngle},\"trigger_id\":\"T3\"}}");

            // T3 stays active until the participant makes a choice (mission completion)
            yield return null;
        }

        private IEnumerator RunT4()
        {
            arrowRenderer.Hide();

            if (proximityText != null)
            {
                proximityText.text = t4MessageText;
                proximityText.gameObject.SetActive(true);
            }

            // T4 stays active until mission completion
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

            arrowRenderer.SetTriggerMode(false);
            arrowRenderer.Show();

            if (proximityText != null)
                proximityText.gameObject.SetActive(false);
        }

        private void LogTriggerComplete(TriggerType type, string triggerId)
        {
            triggerCompleted = true;
            float duration = Time.time - triggerStartTime;
            EventLogger.Instance?.LogEvent("TRIGGER_DEACTIVATED",
                extraData: $"{{\"trigger_type\":\"{type}\",\"duration_s\":{duration:F1}}}");
        }
    }
}

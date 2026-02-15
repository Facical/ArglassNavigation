using UnityEngine;
using ARNavExperiment.Logging;

namespace ARNavExperiment.Core
{
    public enum ExperimentCondition
    {
        GlassOnly,
        Hybrid
    }

    public class ConditionController : MonoBehaviour
    {
        public static ConditionController Instance { get; private set; }

        public ExperimentCondition CurrentCondition { get; private set; }

        [SerializeField] private GameObject beamProUI;
        [SerializeField] private GameObject lockedScreenUI;

        public event System.Action<ExperimentCondition> OnConditionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SetCondition(ExperimentCondition condition)
        {
            CurrentCondition = condition;

            switch (condition)
            {
                case ExperimentCondition.GlassOnly:
                    if (beamProUI) beamProUI.SetActive(false);
                    if (lockedScreenUI) lockedScreenUI.SetActive(true);
                    DeviceStateTracker.Instance?.SetLocked(true);
                    break;

                case ExperimentCondition.Hybrid:
                    if (lockedScreenUI) lockedScreenUI.SetActive(false);
                    if (beamProUI) beamProUI.SetActive(true);
                    DeviceStateTracker.Instance?.SetLocked(false);
                    break;
            }

            string condStr = condition == ExperimentCondition.GlassOnly ? "glass_only" : "hybrid";
            EventLogger.Instance?.SetCondition(condStr);
            EventLogger.Instance?.LogEvent("CONDITION_CHANGE",
                extraData: $"{{\"condition\":\"{condStr}\"}}");

            OnConditionChanged?.Invoke(condition);
            Debug.Log($"[ConditionController] Condition set to: {condition}");
        }
    }
}

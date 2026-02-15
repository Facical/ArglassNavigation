using UnityEngine;
using ARNavExperiment.Core;
using ARNavExperiment.Logging;

namespace ARNavExperiment.BeamPro
{
    public class BeamProHubController : MonoBehaviour
    {
        public static BeamProHubController Instance { get; private set; }

        [SerializeField] private GameObject hubRoot;
        [SerializeField] private GameObject lockedScreen;

        [Header("Tab System")]
        [SerializeField] private GameObject[] tabPanels; // 0=Map, 1=InfoCard, 2=MissionRef
        [SerializeField] private UnityEngine.UI.Button[] tabButtons;

        [Header("Layer Controllers")]
        [SerializeField] private InteractiveMapController mapController;
        [SerializeField] private InfoCardManager infoCardManager;
        [SerializeField] private MissionRefPanel missionRefPanel;

        private int activeTabIndex;
        private bool isLocked;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int idx = i;
                tabButtons[i]?.onClick.AddListener(() => SwitchTab(idx));
            }

            if (DeviceStateTracker.Instance != null)
            {
                DeviceStateTracker.Instance.OnBeamProScreenOn += OnScreenOn;
                DeviceStateTracker.Instance.OnBeamProScreenOff += OnScreenOff;
            }

            if (ConditionController.Instance != null)
                ConditionController.Instance.OnConditionChanged += OnConditionChanged;
        }

        private void OnConditionChanged(ExperimentCondition condition)
        {
            isLocked = condition == ExperimentCondition.GlassOnly;
            if (hubRoot) hubRoot.SetActive(!isLocked);
            if (lockedScreen) lockedScreen.SetActive(isLocked);
        }

        private void OnScreenOn()
        {
            if (isLocked) return;
            if (hubRoot) hubRoot.SetActive(true);
        }

        private void OnScreenOff(float duration)
        {
            if (hubRoot) hubRoot.SetActive(false);
        }

        public void SwitchTab(int tabIndex)
        {
            if (tabIndex == activeTabIndex) return;

            string fromTab = GetTabName(activeTabIndex);
            string toTab = GetTabName(tabIndex);

            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i]) tabPanels[i].SetActive(i == tabIndex);
            }

            activeTabIndex = tabIndex;

            EventLogger.Instance?.LogEvent("BEAM_TAB_SWITCH",
                beamContentType: toTab,
                extraData: $"{{\"from_tab\":\"{fromTab}\",\"to_tab\":\"{toTab}\"}}");
        }

        private string GetTabName(int index)
        {
            return index switch
            {
                0 => "map",
                1 => "info_card",
                2 => "mission_ref",
                _ => "unknown"
            };
        }

        private void OnDestroy()
        {
            if (DeviceStateTracker.Instance != null)
            {
                DeviceStateTracker.Instance.OnBeamProScreenOn -= OnScreenOn;
                DeviceStateTracker.Instance.OnBeamProScreenOff -= OnScreenOff;
            }
            if (ConditionController.Instance != null)
                ConditionController.Instance.OnConditionChanged -= OnConditionChanged;
        }
    }
}

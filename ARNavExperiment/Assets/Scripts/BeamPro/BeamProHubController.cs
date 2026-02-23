using UnityEngine;
using ARNavExperiment.Core;
using ARNavExperiment.Logging;
using ARNavExperiment.UI;

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

        public InteractiveMapController MapController => mapController;
        public InfoCardManager InfoCardMgr => infoCardManager;
        public MissionRefPanel MissionRef => missionRefPanel;

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
            // 양쪽 조건 모두 정보 허브 활성 유지 (GlassOnly: 글래스 WorldSpace, Hybrid: 폰)
            isLocked = false;
            if (hubRoot) hubRoot.SetActive(true);
            if (lockedScreen) lockedScreen.SetActive(false);
        }

        private void OnScreenOn()
        {
            // WorldSpace 모드에서는 폰 화면 상태와 무관하게 항상 표시
            if (hubRoot) hubRoot.SetActive(true);
        }

        private void OnScreenOff(float duration)
        {
            // WorldSpace(GlassOnly) 모드에서는 숨기지 않음
            var canvasCtrl = GetComponent<BeamProCanvasController>();
            if (canvasCtrl != null && canvasCtrl.IsWorldSpace) return;
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

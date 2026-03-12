using UnityEngine;
using ARNavExperiment.Core;
using ARNavExperiment.Logging;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;

namespace ARNavExperiment.Presentation.BeamPro
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
        private bool conditionActivated;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            if (ConditionController.Instance != null)
                ConditionController.Instance.OnConditionChanged += OnConditionChanged;
            if (DeviceStateTracker.Instance != null)
            {
                DeviceStateTracker.Instance.OnBeamProScreenOn += OnScreenOn;
                DeviceStateTracker.Instance.OnBeamProScreenOff += OnScreenOff;
            }
        }

        private void OnDisable()
        {
            if (ConditionController.Instance != null)
                ConditionController.Instance.OnConditionChanged -= OnConditionChanged;
            if (DeviceStateTracker.Instance != null)
            {
                DeviceStateTracker.Instance.OnBeamProScreenOn -= OnScreenOn;
                DeviceStateTracker.Instance.OnBeamProScreenOff -= OnScreenOff;
            }
        }

        private void Start()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int idx = i;
                tabButtons[i]?.onClick.AddListener(() => SwitchTab(idx));
            }
        }

        private void OnConditionChanged(ExperimentCondition condition)
        {
            conditionActivated = true;

            if (condition == ExperimentCondition.GlassOnly)
            {
                // GlassOnly: 허브 활성 + TabBar 숨김 + 모든 탭 기본 숨김
                if (lockedScreen) lockedScreen.SetActive(false);
                if (hubRoot) hubRoot.SetActive(true);

                // TabBar 숨김 (Map 토글 버튼이 대체)
                if (hubRoot)
                {
                    var tabBar = hubRoot.transform.Find("TabBar");
                    if (tabBar) tabBar.gameObject.SetActive(false);
                }

                // 모든 탭 기본 숨김 — 토글로 표시
                HideAllTabs();
                Debug.Log("[BeamProHub] GlassOnly — hubRoot enabled, tabs hidden (toggle to show map)");
                return;
            }

            // Hybrid: 기존 동작 복원
            if (lockedScreen) lockedScreen.SetActive(false);
            if (hubRoot) hubRoot.SetActive(true);

            // TabBar 복원
            if (hubRoot)
            {
                var tabBar = hubRoot.transform.Find("TabBar");
                if (tabBar) tabBar.gameObject.SetActive(true);
            }
            foreach (var btn in tabButtons)
                if (btn) btn.gameObject.SetActive(true);

            activeTabIndex = -1;
            SwitchTab(0);

            Debug.Log($"[BeamProHub] Hybrid — hub active, tab=0");
        }

        private void OnScreenOn()
        {
            if (!conditionActivated) return;
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

        /// <summary>맵 탭을 토글합니다 (GlassOnly 모드에서 사용).</summary>
        public void ToggleMapTab()
        {
            if (activeTabIndex == 0)
                HideAllTabs();
            else
                SwitchTab(0);
        }

        /// <summary>모든 탭 패널을 숨깁니다.</summary>
        public void HideAllTabs()
        {
            for (int i = 0; i < tabPanels.Length; i++)
                if (tabPanels[i]) tabPanels[i].SetActive(false);
            activeTabIndex = -1;
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

            DomainEventBus.Instance?.Publish(new BeamTabSwitched(tabIndex, toTab));
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
            // 이벤트 해제는 OnDisable에서 처리
        }
    }
}

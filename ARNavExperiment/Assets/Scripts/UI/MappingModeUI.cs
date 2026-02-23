using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;

namespace ARNavExperiment.UI
{
    /// <summary>
    /// 매핑 모드 인터페이스.
    /// 연구자가 건물을 순회하며 각 웨이포인트에 공간 앵커를 생성/저장.
    /// </summary>
    public class MappingModeUI : MonoBehaviour
    {
        // === Public 접근자 ===
        public string SelectedWaypointId => selectedWaypointId;
        public string CurrentRoute => currentRoute;
        public int TotalWaypointCount => (currentRoute == "A" ? routeAWaypoints : routeBWaypoints).Count;

        // === 이벤트 ===
        public event System.Action<string, string> OnWaypointSelectedEvent;  // (waypointId, locationName)
        public event System.Action<string, bool> OnAnchorCreateResult;       // (waypointId, success)

        [Header("UI References")]
        [SerializeField] private GameObject mappingPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI qualityText;
        [SerializeField] private Image qualityBar;
        [SerializeField] private Button createAnchorButton;
        [SerializeField] private Button saveAllButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Dropdown routeDropdown;
        [SerializeField] private Transform waypointListContent;
        [SerializeField] private GameObject waypointItemPrefab;

        [Header("Waypoint Definitions")]
        [SerializeField] private List<WaypointDefinition> routeAWaypoints = new List<WaypointDefinition>();
        [SerializeField] private List<WaypointDefinition> routeBWaypoints = new List<WaypointDefinition>();

        private string currentRoute = "A";
        private string selectedWaypointId;
        private List<MappingItemUI> itemUIs = new List<MappingItemUI>();
        private bool initialized;

        [System.Serializable]
        public class WaypointDefinition
        {
            public string waypointId;
            public string locationName;
            public float radius = 2f;
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;

            if (routeAWaypoints.Count == 0)
                SetupDefaultWaypoints();

            // 드롭다운 옵션 설정
            if (routeDropdown != null)
            {
                routeDropdown.options.Clear();
                routeDropdown.options.Add(new TMP_Dropdown.OptionData("Route A"));
                routeDropdown.options.Add(new TMP_Dropdown.OptionData("Route B"));
                routeDropdown.value = 0;
                routeDropdown.RefreshShownValue();
            }

            createAnchorButton?.onClick.AddListener(OnCreateAnchor);
            saveAllButton?.onClick.AddListener(OnSaveAll);
            backButton?.onClick.AddListener(OnBack);
            routeDropdown?.onValueChanged.AddListener(OnRouteChanged);

            // 실험 UI 숨기기 (매핑 모드에서는 불필요)
            HideExperimentUI();

            RefreshWaypointList();
            UpdateQualityDisplay();
        }

        private void Update()
        {
            if (mappingPanel != null && mappingPanel.activeSelf)
            {
                UpdateQualityDisplay();
                UpdateCreateButtonState();
            }
        }

        private void UpdateCreateButtonState()
        {
            if (createAnchorButton == null) return;

            var anchorMgr = SpatialAnchorManager.Instance;
            int quality = anchorMgr != null ? anchorMgr.GetCurrentAnchorQuality() : 0;
            bool qualitySufficient = quality >= 1;
            bool waypointSelected = !string.IsNullOrEmpty(selectedWaypointId);

            createAnchorButton.interactable = qualitySufficient && waypointSelected;

            var btnText = createAnchorButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                if (!waypointSelected)
                    btnText.text = "웨이포인트를 선택하세요";
                else if (!qualitySufficient)
                    btnText.text = "품질 부족 — 주변을 둘러보세요";
                else
                    btnText.text = $"앵커 생성: {selectedWaypointId}";
            }
        }

        /// <summary>매핑 모드에서 실험 관련 UI 숨기기</summary>
        private void HideExperimentUI()
        {
            var hud = FindObjectOfType<ExperimentHUD>(true);
            if (hud != null) hud.gameObject.SetActive(false);

            var beamProHub = FindObjectOfType<BeamPro.BeamProHubController>(true);
            if (beamProHub != null) beamProHub.gameObject.SetActive(false);
        }

        /// <summary>실험 모드로 돌아갈 때 UI 복원</summary>
        private void RestoreExperimentUI()
        {
            var hud = FindObjectOfType<ExperimentHUD>(true);
            if (hud != null) hud.gameObject.SetActive(true);

            var beamProHub = FindObjectOfType<BeamPro.BeamProHubController>(true);
            if (beamProHub != null) beamProHub.gameObject.SetActive(true);
        }

        private void SetupDefaultWaypoints()
        {
            string[] routeAIds = { "A_WP01", "A_WP02", "A_WP03", "A_WP04", "A_WP05", "A_WP06", "A_WP07", "A_WP08" };
            string[] routeANames = { "시작점(계단)", "B123 강의실", "복도 교차로1", "B125 강의실", "중앙 홀", "B127 강의실", "복도 교차로2", "도착점(엘리베이터)" };

            for (int i = 0; i < routeAIds.Length; i++)
            {
                routeAWaypoints.Add(new WaypointDefinition
                {
                    waypointId = routeAIds[i],
                    locationName = routeANames[i],
                    radius = 2f
                });
            }

            string[] routeBIds = { "B_WP01", "B_WP02", "B_WP03", "B_WP04", "B_WP05", "B_WP06", "B_WP07", "B_WP08" };
            string[] routeBNames = { "시작점(계단)", "B131 강의실", "복도 교차로3", "B133 강의실", "동쪽 홀", "B135 강의실", "복도 교차로4", "도착점(엘리베이터)" };

            for (int i = 0; i < routeBIds.Length; i++)
            {
                routeBWaypoints.Add(new WaypointDefinition
                {
                    waypointId = routeBIds[i],
                    locationName = routeBNames[i],
                    radius = 2f
                });
            }
        }

        private void OnRouteChanged(int index)
        {
            currentRoute = index == 0 ? "A" : "B";
            RefreshWaypointList();
        }

        private void RefreshWaypointList()
        {
            // 기존 UI 항목 정리
            foreach (var item in itemUIs)
            {
                if (item != null && item.gameObject != null)
                    Destroy(item.gameObject);
            }
            itemUIs.Clear();

            if (titleText != null)
                titleText.text = $"매핑 모드 — Route {currentRoute}";

            var waypoints = currentRoute == "A" ? routeAWaypoints : routeBWaypoints;
            var anchorMgr = SpatialAnchorManager.Instance;
            var mappings = anchorMgr != null ? anchorMgr.GetRouteMappings(currentRoute) : new List<AnchorMapping>();

            foreach (var wp in waypoints)
            {
                var isMapped = mappings.Exists(m => m.waypointId == wp.waypointId);
                CreateWaypointListItem(wp, isMapped);
            }
        }

        private void CreateWaypointListItem(WaypointDefinition wp, bool isMapped)
        {
            if (waypointListContent == null) return;

            // === 아이템 컨테이너 ===
            var itemGO = new GameObject($"Item_{wp.waypointId}");
            itemGO.transform.SetParent(waypointListContent, false);

            var rect = itemGO.AddComponent<RectTransform>();
            var itemLayout = itemGO.AddComponent<LayoutElement>();
            itemLayout.minHeight = 48;
            itemLayout.preferredHeight = 48;
            itemLayout.flexibleWidth = 1;

            // 짝/홀 행 교대 색상
            int rowIndex = itemUIs.Count;
            var bg = itemGO.AddComponent<Image>();
            bg.color = rowIndex % 2 == 0
                ? new Color(0.14f, 0.14f, 0.19f, 0.8f)
                : new Color(0.11f, 0.11f, 0.16f, 0.8f);

            var hlg = itemGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 12;
            hlg.padding = new RectOffset(16, 16, 6, 6);

            // === 웨이포인트 ID + 이름 (유연 너비) ===
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(itemGO.transform, false);
            labelGO.AddComponent<RectTransform>();
            var labelLayout = labelGO.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1;
            labelLayout.minWidth = 200;
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = $"{wp.waypointId}  {wp.locationName}";
            label.fontSize = 16;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableWordWrapping = false;

            // === 상태 텍스트 (고정 너비) ===
            var statusGO = new GameObject("Status");
            statusGO.transform.SetParent(itemGO.transform, false);
            statusGO.AddComponent<RectTransform>();
            var statusLayout = statusGO.AddComponent<LayoutElement>();
            statusLayout.minWidth = 80;
            statusLayout.preferredWidth = 80;
            var status = statusGO.AddComponent<TextMeshProUGUI>();
            status.text = isMapped ? "완료" : "미매핑";
            status.fontSize = 15;
            status.color = isMapped ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.6f, 0.3f);
            status.alignment = TextAlignmentOptions.Center;
            status.enableWordWrapping = false;
            status.fontStyle = isMapped ? FontStyles.Bold : FontStyles.Normal;

            // === 선택 버튼 (고정 너비) ===
            var btnGO = new GameObject("SelectBtn");
            btnGO.transform.SetParent(itemGO.transform, false);
            btnGO.AddComponent<RectTransform>();
            var btnLayout = btnGO.AddComponent<LayoutElement>();
            btnLayout.minWidth = 100;
            btnLayout.preferredWidth = 100;
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.45f, 0.65f);
            var btn = btnGO.AddComponent<Button>();

            var btnTextGO = new GameObject("Text");
            btnTextGO.transform.SetParent(btnGO.transform, false);
            var btRect = btnTextGO.AddComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;
            var btTMP = btnTextGO.AddComponent<TextMeshProUGUI>();
            btTMP.text = "선택";
            btTMP.fontSize = 15;
            btTMP.color = Color.white;
            btTMP.alignment = TextAlignmentOptions.Center;

            string wpId = wp.waypointId;
            btn.onClick.AddListener(() => OnWaypointSelected(wpId));

            var itemUI = itemGO.AddComponent<MappingItemUI>();
            itemUI.waypointId = wp.waypointId;
            itemUI.statusText = status;
            itemUIs.Add(itemUI);
        }

        private void OnWaypointSelected(string waypointId)
        {
            selectedWaypointId = waypointId;
            if (createAnchorButton != null)
            {
                var btnText = createAnchorButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                    btnText.text = $"앵커 생성: {waypointId}";
            }

            // 이벤트 발행 (locationName 포함)
            var waypoints = currentRoute == "A" ? routeAWaypoints : routeBWaypoints;
            var wp = waypoints.Find(w => w.waypointId == waypointId);
            string locationName = wp != null ? wp.locationName : waypointId;
            OnWaypointSelectedEvent?.Invoke(waypointId, locationName);

            Debug.Log($"[MappingModeUI] 웨이포인트 선택: {waypointId}");
        }

        private async void OnCreateAnchor()
        {
            if (string.IsNullOrEmpty(selectedWaypointId))
            {
                Debug.LogWarning("[MappingModeUI] 웨이포인트를 먼저 선택하세요.");
                return;
            }

            // 방어적 품질 체크 (레이스 조건 방지)
            var anchorMgrCheck = SpatialAnchorManager.Instance;
            if (anchorMgrCheck != null && anchorMgrCheck.GetCurrentAnchorQuality() < 1)
            {
                Debug.LogWarning("[MappingModeUI] 앵커 품질이 부족합니다. 주변을 둘러보세요.");
                OnAnchorCreateResult?.Invoke(selectedWaypointId, false);
                return;
            }

            var waypoints = currentRoute == "A" ? routeAWaypoints : routeBWaypoints;
            var wp = waypoints.Find(w => w.waypointId == selectedWaypointId);
            if (wp == null) return;

            createAnchorButton.interactable = false;
            var btnText = createAnchorButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = "생성 중...";

            try
            {
                var anchorMgr = SpatialAnchorManager.Instance;
                if (anchorMgr != null)
                {
                    bool success = await anchorMgr.CreateAndSaveAnchor(
                        wp.waypointId, currentRoute, wp.radius, wp.locationName);

                    if (success)
                    {
                        var item = itemUIs.Find(i => i.waypointId == wp.waypointId);
                        if (item != null && item.statusText != null)
                        {
                            item.statusText.text = "완료";
                            item.statusText.color = new Color(0.4f, 1f, 0.4f);
                        }
                    }

                    OnAnchorCreateResult?.Invoke(wp.waypointId, success);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MappingModeUI] 앵커 생성 실패: {e.Message}");
                OnAnchorCreateResult?.Invoke(wp.waypointId, false);
            }

            // UpdateCreateButtonState()가 다음 프레임에서 interactable/텍스트를 품질에 맞게 복원
        }

        private void OnSaveAll()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            anchorMgr?.SaveMappingToFile();
            Debug.Log("[MappingModeUI] 모든 매핑 저장 완료");
        }

        private void OnBack()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            anchorMgr?.SaveMappingToFile();

            RestoreExperimentUI();

            var modeSelector = FindObjectOfType<AppModeSelector>(true);
            modeSelector?.ReturnToModeSelector();
        }

        private void UpdateQualityDisplay()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr == null || qualityText == null) return;

            int quality = anchorMgr.GetCurrentAnchorQuality();
            string[] qualityNames = { "부족", "보통", "좋음" };
            Color[] qualityColors = {
                new Color(1f, 0.3f, 0.3f),
                new Color(1f, 0.8f, 0.3f),
                new Color(0.3f, 1f, 0.3f)
            };

            qualityText.text = $"매핑 품질: {qualityNames[quality]}";
            qualityText.color = qualityColors[quality];

            if (qualityBar != null)
                qualityBar.fillAmount = (quality + 1) / 3f;
        }
    }

    /// <summary>웨이포인트 목록 항목의 UI 컴포넌트</summary>
    public class MappingItemUI : MonoBehaviour
    {
        public string waypointId;
        public TextMeshProUGUI statusText;
    }
}

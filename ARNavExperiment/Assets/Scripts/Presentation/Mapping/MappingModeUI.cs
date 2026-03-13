using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Presentation.BeamPro;
using ARNavExperiment.Presentation.Glass;
using ARNavExperiment.Presentation.Shared;

namespace ARNavExperiment.Presentation.Mapping
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
        public int TotalWaypointCount => waypoints.Count;

        // === 이벤트 ===
        public event System.Action<string, string> OnWaypointSelectedEvent;  // (waypointId, locationName)
        public event System.Action<string, bool> OnAnchorCreateResult;       // (waypointId, success)
        public event System.Action OnMapZoomToggle;
        public event System.Action<string, bool> OnReferenceAnchorCreateResult;  // (roomId, success)

        [Header("UI References")]
        [SerializeField] private GameObject mappingPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI qualityText;
        [SerializeField] private Image qualityBar;
        [SerializeField] private Button createAnchorButton;
        [SerializeField] private Button mapZoomButton;
        [SerializeField] private Button saveAllButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Dropdown routeDropdown;
        [SerializeField] private Transform waypointListContent;
        [SerializeField] private GameObject waypointItemPrefab;

        [Header("Waypoint Definitions")]
        [SerializeField] private List<WaypointDefinition> waypoints = new List<WaypointDefinition>();

        [Header("Reference Anchor UI")]
        [SerializeField] private TMP_Dropdown referenceRoomDropdown;
        [SerializeField] private Button createReferenceAnchorButton;
        [SerializeField] private TextMeshProUGUI referenceStatusText;

        private string currentRoute = "B";
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

            if (waypoints.Count == 0)
                SetupDefaultWaypoints();

            // 단일 경로 — 드롭다운 비활성화
            if (routeDropdown != null)
            {
                routeDropdown.options.Clear();
                routeDropdown.options.Add(new TMP_Dropdown.OptionData("경로"));
                routeDropdown.value = 0;
                routeDropdown.RefreshShownValue();
                routeDropdown.interactable = false;
            }

            createAnchorButton?.onClick.AddListener(OnCreateAnchor);
            mapZoomButton?.onClick.AddListener(() => OnMapZoomToggle?.Invoke());
            saveAllButton?.onClick.AddListener(OnSaveAll);
            backButton?.onClick.AddListener(OnBack);
            routeDropdown?.onValueChanged.AddListener(OnRouteChanged);

            // 보정 앵커 UI 초기화
            createReferenceAnchorButton?.onClick.AddListener(OnCreateReferenceAnchor);
            InitializeReferenceDropdown();

            // 실험 UI 숨기기 (매핑 모드에서는 불필요)
            HideExperimentUI();

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += RefreshLocalization;

            RefreshWaypointList();
            UpdateQualityDisplay();
        }

        private void Update()
        {
            if (mappingPanel != null && mappingPanel.activeSelf)
            {
                UpdateCreateButtonState();
            }
        }

        private void UpdateCreateButtonState()
        {
            if (createAnchorButton == null) return;

            bool waypointSelected = !string.IsNullOrEmpty(selectedWaypointId);

            var anchorMgr = SpatialAnchorManager.Instance;
            bool slamReady = anchorMgr != null ? anchorMgr.GetSlamTrackingStatus().isReady : true;

            createAnchorButton.interactable = waypointSelected && slamReady;

            var btnText = createAnchorButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                if (!slamReady)
                    btnText.text = LocalizationManager.Get("mapping.slam_not_ready");
                else if (waypointSelected)
                    btnText.text = string.Format(LocalizationManager.Get("mapping.create_anchor"), selectedWaypointId);
                else
                    btnText.text = LocalizationManager.Get("mapping.select_waypoint");
            }
        }

        /// <summary>매핑 모드에서 실험 관련 UI 숨기기</summary>
        private void HideExperimentUI()
        {
            var hud = FindObjectOfType<ExperimentHUD>(true);
            if (hud != null) hud.gameObject.SetActive(false);

            var beamProHub = FindObjectOfType<BeamProHubController>(true);
            if (beamProHub != null) beamProHub.gameObject.SetActive(false);
        }

        /// <summary>실험 모드로 돌아갈 때 UI 복원</summary>
        private void RestoreExperimentUI()
        {
            var hud = FindObjectOfType<ExperimentHUD>(true);
            if (hud != null) hud.gameObject.SetActive(true);

            var beamProHub = FindObjectOfType<BeamProHubController>(true);
            if (beamProHub != null) beamProHub.gameObject.SetActive(true);
        }

        private void SetupDefaultWaypoints()
        {
            // WP00(보정앵커) → B111 시작 → 북상 → NE U턴 → 남하
            string[] wpIds =   { "B_WP00", "B_WP01", "B_WP02", "B_WP03", "B_WP04", "B_WP05", "B_WP06", "B_WP07", "B_WP08" };
            string[] wpNames = { "B110 근처 (보정앵커)", "B111 근처 ★시작", "B107 전산지능연구실", "B105 송교수실 (T2)", "B103 부근 (경유)", "B101 이교수실", "NE 코너 U턴 (T3)", "B104/B105 비교 (C1)", "B121 남쪽복도 (End)" };
            float[] wpRadii =  { 2.5f, 2.5f, 2.5f, 3f, 2.5f, 2.5f, 3f, 2.5f, 2.5f };

            for (int i = 0; i < wpIds.Length; i++)
            {
                waypoints.Add(new WaypointDefinition
                {
                    waypointId = wpIds[i],
                    locationName = wpNames[i],
                    radius = wpRadii[i]
                });
            }
        }

        private void OnRouteChanged(int index)
        {
            // 단일 경로 고정
            currentRoute = "B";
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
                titleText.text = LocalizationManager.Get("mapping.title");

            var anchorMgr = SpatialAnchorManager.Instance;
            var mappings = anchorMgr != null ? anchorMgr.GetRouteMappings(currentRoute) : new List<AnchorMapping>();

            foreach (var wp in this.waypoints)
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
            status.text = isMapped
                ? LocalizationManager.Get("mapping.mapped")
                : LocalizationManager.Get("mapping.unmapped");
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
            btTMP.text = LocalizationManager.Get("mapping.select");
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
                    btnText.text = string.Format(LocalizationManager.Get("mapping.create_anchor"), waypointId);
            }

            // 이벤트 발행 (locationName 포함)
            var wp = this.waypoints.Find(w => w.waypointId == waypointId);
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

            var wp = this.waypoints.Find(w => w.waypointId == selectedWaypointId);
            if (wp == null) return;

            // Pre-flight SLAM check (defense in depth)
            var anchorMgrCheck = SpatialAnchorManager.Instance;
            if (anchorMgrCheck != null && !anchorMgrCheck.GetSlamTrackingStatus().isReady)
            {
                Debug.LogWarning("[MappingModeUI] Anchor creation blocked — SLAM not ready");
                OnAnchorCreateResult?.Invoke(wp.waypointId, false);
                return;
            }

            createAnchorButton.interactable = false;
            var btnText = createAnchorButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = LocalizationManager.Get("mapping.creating");

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
                            item.statusText.text = LocalizationManager.Get("mapping.mapped");
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
            if (qualityText == null) return;

            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null)
            {
                var slamStatus = anchorMgr.GetSlamTrackingStatus();
                if (!slamStatus.isReady)
                {
                    qualityText.text = slamStatus.userGuidance;
                    qualityText.color = new Color(1f, 0.3f, 0.3f);
                    if (qualityBar != null) qualityBar.fillAmount = 0f;
                    return;
                }
            }

            qualityText.text = LocalizationManager.Get("mapping.quality_hint");
            qualityText.color = new Color(0.6f, 0.6f, 0.6f);

            if (qualityBar != null)
                qualityBar.fillAmount = 0f;
        }

        private void RefreshLocalization(Language lang)
        {
            RefreshWaypointList();
            UpdateQualityDisplay();
            UpdateReferenceStatus();
        }

        // =====================================================
        // 보정 앵커 (Reference Anchor) UI
        // =====================================================

        // 드롭다운 인덱스 → ReferencePoint 매핑 (미매핑 호실만)
        private readonly System.Collections.Generic.List<Navigation.ReferencePointRegistry.ReferencePoint> unmappedRooms = new();

        private void InitializeReferenceDropdown()
        {
            if (referenceRoomDropdown == null) return;

            referenceRoomDropdown.options.Clear();
            unmappedRooms.Clear();

            var anchorMgr = SpatialAnchorManager.Instance;
            var existingRefs = anchorMgr != null ? anchorMgr.GetReferenceMappings() : new System.Collections.Generic.List<ReferenceAnchorMapping>();
            var mappedSet = new System.Collections.Generic.HashSet<string>();
            foreach (var r in existingRefs) mappedSet.Add(r.roomId);

            foreach (var pt in Navigation.ReferencePointRegistry.AllPoints)
            {
                if (mappedSet.Contains(pt.RoomId)) continue;
                unmappedRooms.Add(pt);
                referenceRoomDropdown.options.Add(new TMP_Dropdown.OptionData(pt.DisplayName));
            }

            bool hasUnmapped = unmappedRooms.Count > 0;
            referenceRoomDropdown.value = 0;
            referenceRoomDropdown.RefreshShownValue();
            if (createReferenceAnchorButton != null)
                createReferenceAnchorButton.interactable = hasUnmapped;
            UpdateReferenceStatus();
        }

        private async void OnCreateReferenceAnchor()
        {
            if (referenceRoomDropdown == null) return;

            int idx = referenceRoomDropdown.value;
            if (idx < 0 || idx >= unmappedRooms.Count) return;

            var pt = unmappedRooms[idx];

            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null && !anchorMgr.GetSlamTrackingStatus().isReady)
            {
                Debug.LogWarning("[MappingModeUI] Reference anchor blocked — SLAM not ready");
                OnReferenceAnchorCreateResult?.Invoke(pt.RoomId, false);
                return;
            }

            if (createReferenceAnchorButton != null)
                createReferenceAnchorButton.interactable = false;

            try
            {
                if (anchorMgr != null)
                {
                    bool success = await anchorMgr.CreateAndSaveReferenceAnchor(pt.RoomId, pt.DisplayName);
                    OnReferenceAnchorCreateResult?.Invoke(pt.RoomId, success);

                    if (success)
                    {
                        InitializeReferenceDropdown(); // 드롭다운 "(완료)" 갱신
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MappingModeUI] 보정 앵커 생성 실패: {e.Message}");
                OnReferenceAnchorCreateResult?.Invoke(pt.RoomId, false);
            }

            if (createReferenceAnchorButton != null)
                createReferenceAnchorButton.interactable = true;
        }

        private void UpdateReferenceStatus()
        {
            if (referenceStatusText == null) return;
            var anchorMgr = SpatialAnchorManager.Instance;
            int mapped = anchorMgr != null ? anchorMgr.GetReferenceMappings().Count : 0;
            int total = Navigation.ReferencePointRegistry.AllPoints.Length;
            referenceStatusText.text = string.Format(
                LocalizationManager.Get("mapping.ref_status"), mapped, total);
        }

        private void OnDestroy()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= RefreshLocalization;
        }
    }

    /// <summary>웨이포인트 목록 항목의 UI 컴포넌트</summary>
    public class MappingItemUI : MonoBehaviour
    {
        public string waypointId;
        public TextMeshProUGUI statusText;
    }
}

using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace ARNavExperiment.EditorTools
{
    public class SceneSetupTool : EditorWindow
    {
        // ===== Glass UI 상수 =====

        // 캔버스
        private const int CANVAS_W = 1440;
        private const int CANVAS_H = 810;

        // 폰트 크기
        private const int FS_TITLE = 48;
        private const int FS_BODY = 38;
        private const int FS_QUESTION = 42;
        private const int FS_ANSWER = 32;
        private const int FS_PROMPT = 40;
        private const int FS_RATING_BTN = 34;
        private const int FS_CONFIRM_BTN = 34;
        private const int FS_HUD = 32;
        private const int FS_PROXIMITY = 56;
        private const int FS_TAB = 30;
        private const int FS_MAPPING_HEADER = 28;
        private const int FS_MAPPING_WP = 38;
        private const int FS_MAPPING_QUALITY = 26;
        private const int FS_STATUS_TITLE = 48;
        private const int FS_STATUS_BODY = 40;
        private const int FS_STATUS_INSTR = 30;
        private const int FS_LOCKED = 44;

        // 버튼 높이
        private const int BTN_H_CONFIRM = 110;
        private const int BTN_H_ANSWER = 100;
        private const int BTN_H_RATING = 90;
        private const int BTN_H_TAB = 70;

        // 스페이싱
        private const int SPACING_PANEL = 20;
        private const int SPACING_ANSWER = 16;
        private const int PADDING_PANEL = 50;
        private const int PADDING_RATING = 35;

        // 색상
        private static readonly Color BG_PANEL = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        private static readonly Color BG_HUD = new Color(0, 0, 0, 0.5f);
        private static readonly Color BTN_PRIMARY = new Color(0.2f, 0.55f, 0.25f, 1f);
        private static readonly Color COLOR_PROXIMITY = new Color(1f, 0.75f, 0.2f);

        [MenuItem("ARNav/Setup Main Scene")]
        public static void SetupMainScene()
        {
            if (!EditorUtility.DisplayDialog("씬 셋업",
                "현재 씬에 실험 시스템을 구성합니다.\n기존 오브젝트가 있으면 삭제 후 재생성합니다.", "진행", "취소"))
                return;

            SetupMainSceneSilent();

            EditorUtility.DisplayDialog("완료", "씬 셋업이 완료되었습니다.\n\nHierarchy에서 각 컴포넌트의 참조를 연결해주세요.", "확인");
        }

        /// <summary>확인 다이얼로그 없이 실행 (MasterSetupTool에서 호출용)</summary>
        public static void SetupMainSceneSilent()
        {
            CleanupExistingObjects();
            CreateManagers();
            CreateARSystem();
            CreateBeamProUI();
            CreateExperimenterUI();
            CreateExperimentUI();
            Debug.Log("[SceneSetup] 메인 실험 씬 구성 완료!");
        }

        private static void CreateManagers()
        {
            // --- Experiment System ---
            var system = CreateGameObject("--- Experiment System ---", null);

            // ExperimentManager
            var expMgr = CreateGameObject("ExperimentManager", system.transform);
            expMgr.AddComponent<Core.ExperimentManager>();

            // ConditionController
            var condCtrl = CreateGameObject("ConditionController", system.transform);
            condCtrl.AddComponent<Core.ConditionController>();

            // LocalizationManager
            var locMgr = CreateGameObject("LocalizationManager", system.transform);
            locMgr.AddComponent<Core.LocalizationManager>();

            // MissionManager
            var missMgr = CreateGameObject("MissionManager", system.transform);
            missMgr.AddComponent<Mission.MissionManager>();

            // WaypointManager
            var wpMgr = CreateGameObject("WaypointManager", system.transform);
            wpMgr.AddComponent<Navigation.WaypointManager>();

            // TriggerController
            var trigCtrl = CreateGameObject("TriggerController", system.transform);
            trigCtrl.AddComponent<Navigation.TriggerController>();

            // SpatialAnchorManager
            var anchorMgr = CreateGameObject("SpatialAnchorManager", system.transform);
            anchorMgr.AddComponent<Core.SpatialAnchorManager>();

            // GlassViewCapture (디버그 녹화)
            var glassCap = CreateGameObject("GlassViewCapture", system.transform);
            glassCap.AddComponent<Logging.GlassViewCapture>();

            // XREAL NotificationListener (배터리/온도/SLAM/에러 경고)
            var notifPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.xreal.xr/Runtime/Prefabs/NotificationListener.prefab");
            if (notifPrefab != null)
            {
                var notifInstance = (GameObject)PrefabUtility.InstantiatePrefab(notifPrefab);
                notifInstance.transform.SetParent(system.transform, false);
                Undo.RegisterCreatedObjectUndo(notifInstance, "Create NotificationListener");
                Debug.Log("[SceneSetup] XREAL NotificationListener 프리팹 배치 완료");
            }
            else
            {
                Debug.LogWarning("[SceneSetup] NotificationListener 프리팹을 찾을 수 없습니다. " +
                    "XREAL SDK 패키지 경로를 확인하세요.");
            }

            // --- Application (DDD) ---
            var appLayer = CreateGameObject("--- Application ---", null);

            var eventBus = CreateGameObject("DomainEventBus", appLayer.transform);
            eventBus.AddComponent<Application.DomainEventBus>();

            var observationSvc = CreateGameObject("ObservationService", appLayer.transform);
            observationSvc.AddComponent<Application.ObservationService>();

            var navService = CreateGameObject("NavigationService", appLayer.transform);
            navService.AddComponent<Application.NavigationService>();

            var beamCoord = CreateGameObject("BeamProCoordinator", appLayer.transform);
            beamCoord.AddComponent<Application.BeamProCoordinator>();

            var expAdvancer = CreateGameObject("ExperimentAdvancer", appLayer.transform);
            expAdvancer.AddComponent<Application.ExperimentAdvancer>();

            // --- Logging ---
            var logging = CreateGameObject("--- Logging ---", null);

            var logger = CreateGameObject("EventLogger", logging.transform);
            logger.AddComponent<Logging.EventLogger>();

            var headTracker = CreateGameObject("HeadTracker", logging.transform);
            headTracker.AddComponent<Logging.HeadTracker>();

            var deviceTracker = CreateGameObject("DeviceStateTracker", logging.transform);
            deviceTracker.AddComponent<Logging.DeviceStateTracker>();
        }

        private static void CreateARSystem()
        {
            // --- AR System ---
            var arSystem = CreateGameObject("--- AR System ---", null);

            // AR Arrow
            var arArrow = CreateGameObject("ARArrowRenderer", arSystem.transform);
            arArrow.AddComponent<Navigation.ARArrowRenderer>();

            // Arrow visual (child) — procedural arrow mesh
            var arrowVisual = CreateGameObject("ArrowVisual", arArrow.transform);
            arrowVisual.transform.localScale = Vector3.one;
            arrowVisual.transform.localPosition = Vector3.zero;

            var mf = arrowVisual.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateArrowMesh();

            var renderer = arrowVisual.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.204f, 0.596f, 0.859f);
            renderer.material = mat;

            // Mapping Anchor Visualizer (매핑 모드에서 앵커 위치 3D 마커 표시)
            var visualizer = CreateGameObject("MappingAnchorVisualizer", arSystem.transform);
            visualizer.AddComponent<Presentation.Mapping.MappingAnchorVisualizer>();
        }

        private static void CreateBeamProUI()
        {
            // --- Beam Pro UI ---
            var beamProCanvas = CreateCanvas("BeamProCanvas", 1);

            // Hub Controller + UIAdapter + CanvasController
            var hub = beamProCanvas.gameObject;
            hub.AddComponent<Presentation.BeamPro.BeamProHubController>();
            hub.AddComponent<Presentation.Shared.BeamProUIAdapter>();
            hub.AddComponent<Presentation.BeamPro.BeamProCanvasController>();

            // Locked Screen
            var locked = CreatePanel("LockedScreen", beamProCanvas.transform, new Color(0.1f, 0.1f, 0.1f, 0.95f));
            var lockedText = CreateTMPText("LockedText", locked.transform, "Not available in this condition");
            lockedText.alignment = TextAlignmentOptions.Center;
            lockedText.fontSize = FS_LOCKED;
            locked.SetActive(false);

            // Tab Bar
            var tabBar = CreateGameObject("TabBar", beamProCanvas.transform);
            var tabBarRect = tabBar.AddComponent<RectTransform>();
            tabBarRect.anchorMin = new Vector2(0, 1);
            tabBarRect.anchorMax = new Vector2(1, 1);
            tabBarRect.pivot = new Vector2(0.5f, 1);
            tabBarRect.sizeDelta = new Vector2(0, BTN_H_TAB);
            var tabLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabLayout.childForceExpandWidth = true;
            tabLayout.childControlWidth = true;
            tabLayout.spacing = 5;
            tabLayout.padding = new RectOffset(5, 5, 5, 5);

            CreateTabButton("TabBtn_Map", tabBar.transform, "Map");
            CreateTabButton("TabBtn_Info", tabBar.transform, "Info Cards");
            CreateTabButton("TabBtn_Mission", tabBar.transform, "Mission Ref");

            // ContentArea (TabBar 아래 전체 영역)
            var contentAreaGO = CreateGameObject("ContentArea", beamProCanvas.transform);
            var contentAreaRect = contentAreaGO.AddComponent<RectTransform>();
            contentAreaRect.anchorMin = Vector2.zero;
            contentAreaRect.anchorMax = Vector2.one;
            contentAreaRect.offsetMin = Vector2.zero;
            contentAreaRect.offsetMax = new Vector2(0, -BTN_H_TAB);

            // Tab Panels → ContentArea 자식
            var mapPanel = CreatePanel("MapPanel", contentAreaGO.transform, new Color(0, 0, 0, 0));
            mapPanel.AddComponent<Presentation.BeamPro.InteractiveMapController>();

            // MapContainer (줌 대상 + 마커 컨테이너)
            var mapContainer = CreateGameObject("MapContainer", mapPanel.transform);
            var mapContainerRect = mapContainer.AddComponent<RectTransform>();
            mapContainerRect.anchorMin = Vector2.zero;
            mapContainerRect.anchorMax = Vector2.one;
            mapContainerRect.offsetMin = Vector2.zero;
            mapContainerRect.offsetMax = Vector2.zero;

            // FloorPlanImage (MapContainer 자식)
            var floorPlanGO = CreateGameObject("FloorPlanImage", mapContainer.transform);
            var floorPlanRect = floorPlanGO.AddComponent<RectTransform>();
            floorPlanRect.anchorMin = Vector2.zero;
            floorPlanRect.anchorMax = Vector2.one;
            floorPlanRect.offsetMin = Vector2.zero;
            floorPlanRect.offsetMax = Vector2.zero;
            var floorPlanImg = floorPlanGO.AddComponent<Image>();
            floorPlanImg.preserveAspect = true;
            floorPlanImg.color = new Color(1, 1, 1, 0.9f);

            // DestinationPin (MapContainer 자식, 빨간색 24x24, 초기 비활성)
            var destPinGO = CreateGameObject("DestinationPin", mapContainer.transform);
            var destPinRect = destPinGO.AddComponent<RectTransform>();
            destPinRect.sizeDelta = new Vector2(24, 24);
            destPinRect.anchoredPosition = Vector2.zero;
            var destPinImg = destPinGO.AddComponent<Image>();
            destPinImg.color = new Color(1f, 0.2f, 0.2f, 1f);
            destPinGO.SetActive(false);

            // Zoom Buttons (GlassOnly WorldSpace에서 핀치-투-줌 대체)
            var zoomPanel = CreateGameObject("ZoomButtonPanel", mapPanel.transform);
            var zoomRect = zoomPanel.AddComponent<RectTransform>();
            zoomRect.anchorMin = new Vector2(0.85f, 0.02f);
            zoomRect.anchorMax = new Vector2(0.98f, 0.25f);
            zoomRect.offsetMin = Vector2.zero;
            zoomRect.offsetMax = Vector2.zero;
            var zoomLayout = zoomPanel.AddComponent<VerticalLayoutGroup>();
            zoomLayout.spacing = 8;
            zoomLayout.childForceExpandWidth = true;
            zoomLayout.childForceExpandHeight = true;
            zoomPanel.AddComponent<Presentation.BeamPro.MapZoomButtons>();
            CreateUIButton("ZoomInBtn", zoomPanel.transform, "+");
            CreateUIButton("ZoomOutBtn", zoomPanel.transform, "\u2212");
            zoomPanel.SetActive(false); // BeamProCanvasController가 GlassOnly에서 활성화

            var infoPanel = CreatePanel("InfoCardPanel", contentAreaGO.transform, new Color(0, 0, 0, 0));
            infoPanel.AddComponent<Presentation.BeamPro.InfoCardManager>();
            infoPanel.SetActive(false);

            var missionRefPanel = CreatePanel("MissionRefPanel", contentAreaGO.transform, new Color(0, 0, 0, 0));
            missionRefPanel.AddComponent<Presentation.BeamPro.MissionRefPanel>();
            missionRefPanel.SetActive(false);

            // POI Detail Panel (overlay) → ContentArea 자식
            var poiDetail = CreatePanel("POIDetailPanel", contentAreaGO.transform, new Color(0.15f, 0.15f, 0.2f, 0.95f));
            poiDetail.AddComponent<Presentation.BeamPro.POIDetailPanel>();
            poiDetail.SetActive(false);

            // Comparison Card (overlay) → ContentArea 자식
            var comparison = CreatePanel("ComparisonCard", contentAreaGO.transform, new Color(0.15f, 0.15f, 0.2f, 0.95f));
            comparison.AddComponent<Presentation.BeamPro.ComparisonCardUI>();
            comparison.SetActive(false);

            // BeamPro Event Logger
            var bpLogger = CreateGameObject("BeamProEventLogger", beamProCanvas.transform);
            bpLogger.AddComponent<Presentation.BeamPro.BeamProEventLogger>();
        }

        private static void CreateExperimenterUI()
        {
            // ExperimenterCanvas는 별도 도구로 생성
            ExperimenterCanvasSetupTool.SetupExperimenterCanvasSilent();
        }

        private static void CreateExperimentUI()
        {
            // --- Experiment UI (Glass overlay) ---
            // 에디터: ScreenSpaceOverlay, 디바이스: GlassCanvasController가 WorldSpace로 전환
            var expCanvas = CreateCanvas("ExperimentCanvas", 2);
            expCanvas.gameObject.AddComponent<Presentation.Glass.GlassCanvasController>();

            // Mission Briefing Panel
            var briefing = CreatePanel("MissionBriefingPanel", expCanvas.transform, BG_PANEL);
            briefing.AddComponent<Presentation.Glass.MissionBriefingUI>();
            var briefRect = briefing.GetComponent<RectTransform>();
            briefRect.anchorMin = new Vector2(0.1f, 0.12f);
            briefRect.anchorMax = new Vector2(0.9f, 0.88f);
            briefRect.offsetMin = Vector2.zero;
            briefRect.offsetMax = Vector2.zero;

            var briefLayout = briefing.AddComponent<VerticalLayoutGroup>();
            briefLayout.spacing = SPACING_PANEL;
            briefLayout.padding = new RectOffset(PADDING_PANEL, PADDING_PANEL, 30, 30);
            briefLayout.childForceExpandWidth = true;
            briefLayout.childForceExpandHeight = false;
            briefLayout.childControlWidth = true;
            briefLayout.childControlHeight = true;
            briefLayout.childAlignment = TextAnchor.MiddleCenter;

            var midText = CreateTMPText("MissionIdText", briefing.transform, "Mission A1");
            midText.fontSize = FS_TITLE;
            midText.fontStyle = FontStyles.Bold;
            midText.alignment = TextAlignmentOptions.Center;
            var midLE = midText.gameObject.AddComponent<LayoutElement>();
            midLE.minHeight = 60;

            var brText = CreateTMPText("BriefingText", briefing.transform, "Mission briefing text");
            brText.fontSize = FS_BODY;
            brText.alignment = TextAlignmentOptions.Center;
            var brLE = brText.gameObject.AddComponent<LayoutElement>();
            brLE.flexibleHeight = 1;

            var confirmBtn = CreateUIButton("ConfirmBtn", briefing.transform, "Confirm");
            var confirmBtnLE = confirmBtn.gameObject.AddComponent<LayoutElement>();
            confirmBtnLE.minHeight = BTN_H_CONFIRM;
            confirmBtn.GetComponent<Image>().color = BTN_PRIMARY;
            var confirmBtnText = confirmBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (confirmBtnText) confirmBtnText.fontSize = FS_CONFIRM_BTN;
            briefing.SetActive(false);

            // Verification Panel
            var verify = CreatePanel("VerificationPanel", expCanvas.transform, BG_PANEL);
            verify.AddComponent<Presentation.Glass.VerificationUI>();
            var verRect = verify.GetComponent<RectTransform>();
            verRect.anchorMin = new Vector2(0.1f, 0.1f);
            verRect.anchorMax = new Vector2(0.9f, 0.9f);
            verRect.offsetMin = Vector2.zero;
            verRect.offsetMax = Vector2.zero;

            var verLayout = verify.AddComponent<VerticalLayoutGroup>();
            verLayout.spacing = SPACING_PANEL;
            verLayout.padding = new RectOffset(PADDING_PANEL, PADDING_PANEL, 30, 30);
            verLayout.childForceExpandWidth = true;
            verLayout.childForceExpandHeight = false;
            verLayout.childControlWidth = true;
            verLayout.childControlHeight = true;
            verLayout.childAlignment = TextAnchor.MiddleCenter;

            var qText = CreateTMPText("QuestionText", verify.transform, "Verification question");
            qText.fontSize = FS_QUESTION;
            qText.fontStyle = FontStyles.Bold;
            qText.alignment = TextAlignmentOptions.Center;
            var qLE = qText.gameObject.AddComponent<LayoutElement>();
            qLE.minHeight = 80;

            var answersLayout = CreateGameObject("Answers", verify.transform);
            answersLayout.AddComponent<RectTransform>();
            var vl = answersLayout.AddComponent<VerticalLayoutGroup>();
            vl.spacing = SPACING_ANSWER;
            vl.padding = new RectOffset(15, 15, 0, 0);
            vl.childForceExpandHeight = false;
            vl.childForceExpandWidth = true;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            var answersLE = answersLayout.AddComponent<LayoutElement>();
            answersLE.flexibleHeight = 1;

            for (int i = 0; i < 4; i++)
            {
                var ansBtn = CreateUIButton($"AnswerBtn_{i}", answersLayout.transform, $"Option {i + 1}");
                var ansBtnLE = ansBtn.gameObject.AddComponent<LayoutElement>();
                ansBtnLE.minHeight = BTN_H_ANSWER;
                var ansBtnText = ansBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (ansBtnText)
                {
                    ansBtnText.fontSize = FS_ANSWER;
                    ansBtnText.alignment = TextAlignmentOptions.Left;
                    ansBtnText.overflowMode = TextOverflowModes.Ellipsis;
                    ansBtnText.margin = new Vector4(15, 0, 15, 0);
                }
            }
            verify.SetActive(false);

            // Confidence Rating
            var confidence = CreatePanel("ConfidencePanel", expCanvas.transform, BG_PANEL);
            confidence.AddComponent<Presentation.Glass.ConfidenceRatingUI>();
            var confRect = confidence.GetComponent<RectTransform>();
            confRect.anchorMin = new Vector2(0.1f, 0.15f);
            confRect.anchorMax = new Vector2(0.9f, 0.85f);
            confRect.offsetMin = Vector2.zero;
            confRect.offsetMax = Vector2.zero;

            var confLayout = confidence.AddComponent<VerticalLayoutGroup>();
            confLayout.spacing = SPACING_PANEL;
            confLayout.padding = new RectOffset(PADDING_RATING, PADDING_RATING, 25, 25);
            confLayout.childForceExpandWidth = true;
            confLayout.childForceExpandHeight = false;
            confLayout.childControlWidth = true;
            confLayout.childControlHeight = true;
            confLayout.childAlignment = TextAnchor.MiddleCenter;

            var confPrompt = CreateTMPText("PromptText", confidence.transform, "Rate your confidence");
            confPrompt.fontSize = FS_PROMPT;
            confPrompt.fontStyle = FontStyles.Bold;
            confPrompt.alignment = TextAlignmentOptions.Center;
            var confPromptLE = confPrompt.gameObject.AddComponent<LayoutElement>();
            confPromptLE.minHeight = 60;

            var ratingBar = CreateGameObject("RatingButtons", confidence.transform);
            ratingBar.AddComponent<RectTransform>();
            var hl = ratingBar.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10;
            hl.padding = new RectOffset(10, 10, 0, 0);
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = false;
            hl.childAlignment = TextAnchor.MiddleCenter;
            var ratingBarLE = ratingBar.AddComponent<LayoutElement>();
            ratingBarLE.minHeight = BTN_H_RATING;
            for (int i = 1; i <= 7; i++)
            {
                var rBtn = CreateUIButton($"RatingBtn_{i}", ratingBar.transform, $"{i}");
                var rBtnText = rBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (rBtnText) rBtnText.fontSize = FS_RATING_BTN;
            }

            var confCurRating = CreateTMPText("CurrentRatingText", confidence.transform, "");
            confCurRating.fontSize = FS_BODY;
            confCurRating.alignment = TextAlignmentOptions.Center;
            var confCurLE = confCurRating.gameObject.AddComponent<LayoutElement>();
            confCurLE.minHeight = 40;

            var confConfirmBtn = CreateUIButton("ConfirmRatingBtn", confidence.transform, "Confirm");
            var confConfirmLE = confConfirmBtn.gameObject.AddComponent<LayoutElement>();
            confConfirmLE.minHeight = BTN_H_CONFIRM;
            confConfirmBtn.GetComponent<Image>().color = BTN_PRIMARY;
            var confConfirmText = confConfirmBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (confConfirmText) confConfirmText.fontSize = FS_CONFIRM_BTN;
            confidence.SetActive(false);

            // Difficulty Rating
            var difficulty = CreatePanel("DifficultyPanel", expCanvas.transform, BG_PANEL);
            difficulty.AddComponent<Presentation.Glass.DifficultyRatingUI>();
            var diffRect = difficulty.GetComponent<RectTransform>();
            diffRect.anchorMin = new Vector2(0.1f, 0.15f);
            diffRect.anchorMax = new Vector2(0.9f, 0.85f);
            diffRect.offsetMin = Vector2.zero;
            diffRect.offsetMax = Vector2.zero;

            var diffLayout = difficulty.AddComponent<VerticalLayoutGroup>();
            diffLayout.spacing = SPACING_PANEL;
            diffLayout.padding = new RectOffset(PADDING_RATING, PADDING_RATING, 25, 25);
            diffLayout.childForceExpandWidth = true;
            diffLayout.childForceExpandHeight = false;
            diffLayout.childControlWidth = true;
            diffLayout.childControlHeight = true;
            diffLayout.childAlignment = TextAnchor.MiddleCenter;

            var diffPrompt = CreateTMPText("PromptText", difficulty.transform, "Rate the difficulty of this mission");
            diffPrompt.fontSize = FS_PROMPT;
            diffPrompt.fontStyle = FontStyles.Bold;
            diffPrompt.alignment = TextAlignmentOptions.Center;
            var diffPromptLE = diffPrompt.gameObject.AddComponent<LayoutElement>();
            diffPromptLE.minHeight = 60;

            var diffRatingBar = CreateGameObject("RatingButtons", difficulty.transform);
            diffRatingBar.AddComponent<RectTransform>();
            var diffHL = diffRatingBar.AddComponent<HorizontalLayoutGroup>();
            diffHL.spacing = 10;
            diffHL.padding = new RectOffset(10, 10, 0, 0);
            diffHL.childForceExpandWidth = true;
            diffHL.childForceExpandHeight = false;
            diffHL.childAlignment = TextAnchor.MiddleCenter;
            var diffRatingBarLE = diffRatingBar.AddComponent<LayoutElement>();
            diffRatingBarLE.minHeight = BTN_H_RATING;
            for (int i = 1; i <= 7; i++)
            {
                var dRBtn = CreateUIButton($"RatingBtn_{i}", diffRatingBar.transform, $"{i}");
                var dRBtnText = dRBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (dRBtnText) dRBtnText.fontSize = FS_RATING_BTN;
            }

            var diffCurRating = CreateTMPText("CurrentRatingText", difficulty.transform, "");
            diffCurRating.fontSize = FS_BODY;
            diffCurRating.alignment = TextAlignmentOptions.Center;
            var diffCurLE = diffCurRating.gameObject.AddComponent<LayoutElement>();
            diffCurLE.minHeight = 40;

            var diffConfirmBtn = CreateUIButton("ConfirmRatingBtn", difficulty.transform, "Confirm");
            var diffConfirmLE = diffConfirmBtn.gameObject.AddComponent<LayoutElement>();
            diffConfirmLE.minHeight = BTN_H_CONFIRM;
            diffConfirmBtn.GetComponent<Image>().color = BTN_PRIMARY;
            var diffConfirmText = diffConfirmBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (diffConfirmText) diffConfirmText.fontSize = FS_CONFIRM_BTN;
            difficulty.SetActive(false);

            // Experiment HUD (표시 전용 — 버튼은 ExperimenterHUD로 이동)
            var hud = CreatePanel("ExperimentHUD", expCanvas.transform, BG_HUD);
            hud.AddComponent<Presentation.Glass.ExperimentHUD>();
            var hudRect = hud.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0.08f, 0.80f);
            hudRect.anchorMax = new Vector2(0.45f, 0.97f);
            hudRect.pivot = new Vector2(0.5f, 0.5f);
            hudRect.sizeDelta = Vector2.zero;
            hudRect.anchoredPosition = Vector2.zero;

            var hudLayout = hud.AddComponent<VerticalLayoutGroup>();
            hudLayout.childForceExpandWidth = true;
            hudLayout.childForceExpandHeight = false;
            hudLayout.childControlHeight = true;
            hudLayout.childControlWidth = true;
            hudLayout.spacing = 4;
            hudLayout.padding = new RectOffset(12, 12, 6, 6);

            var st = CreateTMPText("StateText", hud.transform, "State: Idle");
            st.fontSize = FS_HUD;
            var ct = CreateTMPText("ConditionText", hud.transform, "Condition: -");
            ct.fontSize = FS_HUD;
            var mt = CreateTMPText("MissionText", hud.transform, "Mission: -");
            mt.fontSize = FS_HUD;
            var wt = CreateTMPText("WPText", hud.transform, "WP: -");
            wt.fontSize = FS_HUD;

            // T4 Proximity Text (for TriggerController)
            var proximityText = CreateTMPText("ProximityText", expCanvas.transform, "Near destination");
            proximityText.alignment = TextAlignmentOptions.Center;
            proximityText.fontSize = FS_PROXIMITY;
            proximityText.color = COLOR_PROXIMITY;
            var ptRect = proximityText.GetComponent<RectTransform>();
            ptRect.anchorMin = new Vector2(0.2f, 0.4f);
            ptRect.anchorMax = new Vector2(0.8f, 0.6f);
            proximityText.gameObject.SetActive(false);

            // === Mapping Glass Overlay (매핑 모드 전용, 기본 비활성) ===
            var mappingOverlay = CreatePanel("MappingGlassOverlay", expCanvas.transform,
                new Color(0, 0, 0, 0));
            mappingOverlay.AddComponent<Presentation.Mapping.MappingGlassOverlay>();

            // Header bar (상단)
            var mgHeaderBar = CreateGameObject("MG_HeaderBar", mappingOverlay.transform);
            var mgHeaderRect = mgHeaderBar.AddComponent<RectTransform>();
            mgHeaderRect.anchorMin = new Vector2(0.05f, 0.89f);
            mgHeaderRect.anchorMax = new Vector2(0.95f, 0.98f);
            mgHeaderRect.pivot = new Vector2(0.5f, 0.5f);
            mgHeaderRect.sizeDelta = Vector2.zero;
            var mgHeaderLayout = mgHeaderBar.AddComponent<HorizontalLayoutGroup>();
            mgHeaderLayout.childForceExpandWidth = false;
            mgHeaderLayout.childControlWidth = true;
            mgHeaderLayout.childControlHeight = true;
            mgHeaderLayout.spacing = 20;
            mgHeaderLayout.padding = new RectOffset(20, 20, 8, 8);
            mgHeaderLayout.childAlignment = TextAnchor.MiddleLeft;

            var mgHeader = CreateTMPText("MG_HeaderText", mgHeaderBar.transform, "Mapping Mode");
            mgHeader.fontSize = FS_MAPPING_HEADER;
            mgHeader.fontStyle = FontStyles.Bold;
            mgHeader.color = new Color(0.4f, 0.8f, 1f);
            var mgHeaderLE = mgHeader.gameObject.AddComponent<LayoutElement>();
            mgHeaderLE.flexibleWidth = 1;

            var mgRoute = CreateTMPText("MG_RouteText", mgHeaderBar.transform, "Route A");
            mgRoute.fontSize = FS_MAPPING_QUALITY;
            mgRoute.alignment = TextAlignmentOptions.Center;
            var mgRouteLE = mgRoute.gameObject.AddComponent<LayoutElement>();
            mgRouteLE.minWidth = 100;

            var mgProgress = CreateTMPText("MG_ProgressText", mgHeaderBar.transform, "0/8 Mapped");
            mgProgress.fontSize = FS_MAPPING_QUALITY;
            mgProgress.alignment = TextAlignmentOptions.Right;
            var mgProgressLE = mgProgress.gameObject.AddComponent<LayoutElement>();
            mgProgressLE.minWidth = 100;

            // SLAM status (헤더 아래, 미니맵 오른쪽)
            var mgSlamStatus = CreateTMPText("MG_SlamStatusText", mappingOverlay.transform, "Checking SLAM...");
            mgSlamStatus.fontSize = FS_MAPPING_QUALITY;
            mgSlamStatus.fontStyle = FontStyles.Bold;
            mgSlamStatus.alignment = TextAlignmentOptions.Center;
            mgSlamStatus.color = new Color(0.6f, 0.6f, 0.6f);
            var mgSlamRect = mgSlamStatus.GetComponent<RectTransform>();
            mgSlamRect.anchorMin = new Vector2(0.40f, 0.79f);
            mgSlamRect.anchorMax = new Vector2(0.90f, 0.87f);
            mgSlamRect.offsetMin = Vector2.zero;
            mgSlamRect.offsetMax = Vector2.zero;

            // Waypoint info (중앙)
            var mgWPText = CreateTMPText("MG_WaypointText", mappingOverlay.transform, "Select a waypoint");
            mgWPText.fontSize = FS_MAPPING_WP;
            mgWPText.fontStyle = FontStyles.Bold;
            mgWPText.alignment = TextAlignmentOptions.Center;
            mgWPText.color = new Color(0.6f, 0.6f, 0.6f);
            mgWPText.overflowMode = TextOverflowModes.Ellipsis;
            var mgWPRect = mgWPText.GetComponent<RectTransform>();
            mgWPRect.anchorMin = new Vector2(0.05f, 0.35f);
            mgWPRect.anchorMax = new Vector2(0.95f, 0.65f);
            mgWPRect.offsetMin = Vector2.zero;
            mgWPRect.offsetMax = Vector2.zero;

            // Quality area (하단 왼쪽)
            var mgQualityArea = CreateGameObject("MG_QualityArea", mappingOverlay.transform);
            var mgQualityAreaRect = mgQualityArea.AddComponent<RectTransform>();
            mgQualityAreaRect.anchorMin = new Vector2(0.05f, 0.03f);
            mgQualityAreaRect.anchorMax = new Vector2(0.43f, 0.15f);
            mgQualityAreaRect.offsetMin = Vector2.zero;
            mgQualityAreaRect.offsetMax = Vector2.zero;
            var mgQualityLayout = mgQualityArea.AddComponent<HorizontalLayoutGroup>();
            mgQualityLayout.childForceExpandWidth = false;
            mgQualityLayout.childControlWidth = true;
            mgQualityLayout.childControlHeight = true;
            mgQualityLayout.spacing = 8;
            mgQualityLayout.childAlignment = TextAnchor.MiddleLeft;

            // Quality icon (색상 원)
            var mgQualityIconGO = CreateGameObject("MG_QualityIcon", mgQualityArea.transform);
            mgQualityIconGO.AddComponent<RectTransform>();
            var mgQualityIconImg = mgQualityIconGO.AddComponent<Image>();
            mgQualityIconImg.color = new Color(0.267f, 1f, 0.267f);
            var mgQualityIconLE = mgQualityIconGO.AddComponent<LayoutElement>();
            mgQualityIconLE.minWidth = 20;
            mgQualityIconLE.minHeight = 20;
            mgQualityIconLE.preferredWidth = 20;
            mgQualityIconLE.preferredHeight = 20;

            var mgQuality = CreateTMPText("MG_QualityText", mgQualityArea.transform, "Quality: Good");
            mgQuality.fontSize = FS_MAPPING_QUALITY;
            mgQuality.color = new Color(0.267f, 1f, 0.267f);

            // Quality guidance (중앙 하단 — 품질 관찰 중 표시)
            var mgGuidance = CreateTMPText("MG_GuidanceText", mappingOverlay.transform, "");
            mgGuidance.fontSize = FS_MAPPING_HEADER;
            mgGuidance.fontStyle = FontStyles.Bold;
            mgGuidance.alignment = TextAlignmentOptions.Center;
            mgGuidance.alpha = 0f;
            var mgGuidanceRect = mgGuidance.GetComponent<RectTransform>();
            mgGuidanceRect.anchorMin = new Vector2(0.1f, 0.2f);
            mgGuidanceRect.anchorMax = new Vector2(0.9f, 0.35f);
            mgGuidanceRect.offsetMin = Vector2.zero;
            mgGuidanceRect.offsetMax = Vector2.zero;

            // Flash message (하단 중앙)
            var mgFlash = CreateTMPText("MG_FlashText", mappingOverlay.transform, "");
            mgFlash.fontSize = FS_MAPPING_QUALITY;
            mgFlash.fontStyle = FontStyles.Bold;
            mgFlash.alignment = TextAlignmentOptions.Center;
            mgFlash.alpha = 0f;
            var mgFlashRect = mgFlash.GetComponent<RectTransform>();
            mgFlashRect.anchorMin = new Vector2(0.2f, 0.08f);
            mgFlashRect.anchorMax = new Vector2(0.8f, 0.2f);
            mgFlashRect.offsetMin = Vector2.zero;
            mgFlashRect.offsetMax = Vector2.zero;

            // --- MiniMap (도면 기반 매핑 진행 미니맵) ---
            var miniMapPanel = CreateGameObject("MG_MiniMapPanel", mappingOverlay.transform);
            var miniMapPanelRect = miniMapPanel.AddComponent<RectTransform>();
            miniMapPanelRect.anchorMin = new Vector2(0.05f, 0.58f);
            miniMapPanelRect.anchorMax = new Vector2(0.43f, 0.86f);
            miniMapPanelRect.offsetMin = Vector2.zero;
            miniMapPanelRect.offsetMax = Vector2.zero;
            miniMapPanel.AddComponent<Presentation.Mapping.MappingMiniMap>();

            // 반투명 배경
            var miniMapBG = CreateGameObject("MG_MiniMapBG", miniMapPanel.transform);
            var miniMapBGRect = miniMapBG.AddComponent<RectTransform>();
            miniMapBGRect.anchorMin = Vector2.zero;
            miniMapBGRect.anchorMax = Vector2.one;
            miniMapBGRect.offsetMin = Vector2.zero;
            miniMapBGRect.offsetMax = Vector2.zero;
            var miniMapBGImg = miniMapBG.AddComponent<Image>();
            miniMapBGImg.color = new Color(0, 0, 0, 0.4f);

            // 도면 이미지
            var floorPlanGO = CreateGameObject("MG_FloorPlanImage", miniMapPanel.transform);
            var floorPlanRect = floorPlanGO.AddComponent<RectTransform>();
            floorPlanRect.anchorMin = Vector2.zero;
            floorPlanRect.anchorMax = Vector2.one;
            floorPlanRect.offsetMin = new Vector2(4, 4);
            floorPlanRect.offsetMax = new Vector2(-4, -4);
            var floorPlanImg = floorPlanGO.AddComponent<Image>();
            floorPlanImg.preserveAspect = true;
            floorPlanImg.color = new Color(1, 1, 1, 0.8f);

            // 마커 컨테이너 (도면과 동일 영역)
            var markerContainer = CreateGameObject("MG_MarkerContainer", miniMapPanel.transform);
            var markerContainerRect = markerContainer.AddComponent<RectTransform>();
            markerContainerRect.anchorMin = Vector2.zero;
            markerContainerRect.anchorMax = Vector2.one;
            markerContainerRect.offsetMin = new Vector2(4, 4);
            markerContainerRect.offsetMax = new Vector2(-4, -4);

            mappingOverlay.SetActive(false);

            // === Glass Mode Status Panel (앱 모드 상태 표시, 기본 활성) ===
            var glassModeStatus = CreatePanel("GlassModeStatusPanel", expCanvas.transform,
                new Color(0.05f, 0.05f, 0.1f, 0.85f));
            glassModeStatus.AddComponent<Presentation.Glass.GlassModeStatusPanel>();
            var gmsRect = glassModeStatus.GetComponent<RectTransform>();
            gmsRect.anchorMin = new Vector2(0.25f, 0.3f);
            gmsRect.anchorMax = new Vector2(0.75f, 0.7f);
            gmsRect.offsetMin = Vector2.zero;
            gmsRect.offsetMax = Vector2.zero;

            var gmsTitle = CreateTMPText("GMS_TitleText", glassModeStatus.transform, "AR Navigation Experiment");
            gmsTitle.fontSize = FS_STATUS_TITLE;
            gmsTitle.fontStyle = FontStyles.Bold;
            gmsTitle.alignment = TextAlignmentOptions.Center;
            var gmsTitleRect = gmsTitle.GetComponent<RectTransform>();
            gmsTitleRect.anchorMin = new Vector2(0.05f, 0.65f);
            gmsTitleRect.anchorMax = new Vector2(0.95f, 0.9f);
            gmsTitleRect.offsetMin = Vector2.zero;
            gmsTitleRect.offsetMax = Vector2.zero;

            var gmsStatus = CreateTMPText("GMS_StatusText", glassModeStatus.transform, "Waiting for mode selection");
            gmsStatus.fontSize = FS_STATUS_BODY;
            gmsStatus.alignment = TextAlignmentOptions.Center;
            gmsStatus.color = new Color(0.6f, 0.8f, 1f);
            var gmsStatusRect = gmsStatus.GetComponent<RectTransform>();
            gmsStatusRect.anchorMin = new Vector2(0.05f, 0.35f);
            gmsStatusRect.anchorMax = new Vector2(0.95f, 0.6f);
            gmsStatusRect.offsetMin = Vector2.zero;
            gmsStatusRect.offsetMax = Vector2.zero;

            var gmsInstruction = CreateTMPText("GMS_InstructionText", glassModeStatus.transform, "Select a mode on Beam Pro");
            gmsInstruction.fontSize = FS_STATUS_INSTR;
            gmsInstruction.alignment = TextAlignmentOptions.Center;
            gmsInstruction.color = new Color(0.6f, 0.6f, 0.6f);
            var gmsInstrRect = gmsInstruction.GetComponent<RectTransform>();
            gmsInstrRect.anchorMin = new Vector2(0.05f, 0.1f);
            gmsInstrRect.anchorMax = new Vector2(0.95f, 0.35f);
            gmsInstrRect.offsetMin = Vector2.zero;
            gmsInstrRect.offsetMax = Vector2.zero;

        }

        /// <summary>기존 ARNav 오브젝트를 모두 삭제하여 중복 방지</summary>
        public static void CleanupExistingObjects()
        {
            string[] rootObjects = {
                "--- Experiment System ---",
                "--- Logging ---",
                "--- AR System ---",
                "BeamProCanvas",
                "ExperimenterCanvas",
                "ExperimentCanvas",
                "XR Origin (XREAL)",
                "--- Test Environment ---"
            };

            int removed = 0;
            foreach (var name in rootObjects)
            {
                GameObject existing;
                while ((existing = GameObject.Find(name)) != null)
                {
                    Undo.DestroyObjectImmediate(existing);
                    removed++;
                }
            }

            if (removed > 0)
                Debug.Log($"[SceneSetup] 기존 오브젝트 {removed}개 정리 완료");
        }

        // --- Helper Methods ---

        private static GameObject CreateGameObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }

        private static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = CreateGameObject(name, null);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CANVAS_W, CANVAS_H);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color bgColor)
        {
            var go = CreateGameObject(name, parent);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            // CanvasGroup 추가 (PanelFader 지원)
            go.AddComponent<CanvasGroup>();
            return go;
        }

        private static TextMeshProUGUI CreateTMPText(string name, Transform parent, string text)
        {
            var go = CreateGameObject(name, parent);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            return tmp;
        }

        private static Button CreateUIButton(string name, Transform parent, string label)
        {
            var go = CreateGameObject(name, parent);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 50);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.35f, 1f);
            var btn = go.AddComponent<Button>();

            // 핸드트래킹 시각 피드백을 위한 ColorBlock
            var colors = btn.colors;
            colors.normalColor = new Color(0.25f, 0.25f, 0.35f, 1f);
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.5f, 1f);
            colors.pressedColor = new Color(0.15f, 0.4f, 0.15f, 1f);
            colors.selectedColor = new Color(0.3f, 0.3f, 0.45f, 1f);
            btn.colors = colors;

            var textGo = CreateGameObject("Text", go.transform);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        private static Button CreateTabButton(string name, Transform parent, string label)
        {
            var btn = CreateUIButton(name, parent, label);
            var rect = btn.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, BTN_H_TAB);
            // 탭 버튼 텍스트 크기 명시
            var tabText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tabText) tabText.fontSize = FS_TAB;
            return btn;
        }

        /// <summary>
        /// 프로시저럴 3D 화살표 메쉬 생성 (XZ 평면, +Z 방향).
        /// 전체 길이 0.3m, 머리 너비 0.15m, 몸체 너비 0.06m, 두께 0.02m.
        /// </summary>
        private static Mesh CreateArrowMesh()
        {
            var mesh = new Mesh();
            mesh.name = "ProceduralArrow";

            // Arrow dimensions
            float bodyHalfW = 0.03f;   // half body width
            float headHalfW = 0.075f;  // half head width
            float halfH = 0.01f;       // half thickness (Y)
            float z0 = -0.15f;         // body back
            float z1 = 0.03f;          // body front / head base
            float z2 = 0.15f;          // head tip

            // 7 outline points × 2 faces (top y=+halfH, bottom y=-halfH)
            var verts = new Vector3[14];
            // Top face (0–6)
            verts[0] = new Vector3(-bodyHalfW, halfH, z0);  // body back-left
            verts[1] = new Vector3( bodyHalfW, halfH, z0);  // body back-right
            verts[2] = new Vector3( bodyHalfW, halfH, z1);  // body front-right
            verts[3] = new Vector3( headHalfW, halfH, z1);  // head right
            verts[4] = new Vector3(        0f, halfH, z2);  // tip
            verts[5] = new Vector3(-headHalfW, halfH, z1);  // head left
            verts[6] = new Vector3(-bodyHalfW, halfH, z1);  // body front-left
            // Bottom face (7–13)
            for (int i = 0; i < 7; i++)
                verts[i + 7] = new Vector3(verts[i].x, -halfH, verts[i].z);

            // 5 top + 5 bottom + 14 side = 24 triangles = 72 indices
            var tris = new int[72];
            int t = 0;

            // Top face (CW from above → +Y normal)
            int[] topFace = { 0,6,1, 1,6,2, 5,4,6, 6,4,2, 2,4,3 };
            topFace.CopyTo(tris, t);
            t += topFace.Length;

            // Bottom face (reversed winding, vertex offset +7)
            for (int i = 0; i < topFace.Length; i += 3)
            {
                tris[t++] = topFace[i] + 7;
                tris[t++] = topFace[i + 2] + 7;
                tris[t++] = topFace[i + 1] + 7;
            }

            // Side faces: outline 0→1→2→3→4→5→6→0
            int[] outline = { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,0 };
            for (int i = 0; i < outline.Length; i += 2)
            {
                int a = outline[i], b = outline[i + 1];
                tris[t++] = a;     tris[t++] = b;     tris[t++] = a + 7;
                tris[t++] = a + 7; tris[t++] = b;     tris[t++] = b + 7;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

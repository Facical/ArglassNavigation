using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace ARNavExperiment.EditorTools
{
    public class SceneSetupTool : EditorWindow
    {
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

            // Arrow visual (child)
            var arrowVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrowVisual.name = "ArrowVisual";
            arrowVisual.transform.SetParent(arArrow.transform);
            arrowVisual.transform.localScale = new Vector3(0.15f, 0.05f, 0.3f);
            arrowVisual.transform.localPosition = Vector3.zero;

            // set arrow color
            var renderer = arrowVisual.GetComponent<Renderer>();
            if (renderer)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.204f, 0.596f, 0.859f);
                renderer.material = mat;
            }

            // remove collider from arrow
            var col = arrowVisual.GetComponent<Collider>();
            if (col) Object.DestroyImmediate(col);

            // Mapping Anchor Visualizer (매핑 모드에서 앵커 위치 3D 마커 표시)
            var visualizer = CreateGameObject("MappingAnchorVisualizer", arSystem.transform);
            visualizer.AddComponent<Navigation.MappingAnchorVisualizer>();
        }

        private static void CreateBeamProUI()
        {
            // --- Beam Pro UI ---
            var beamProCanvas = CreateCanvas("BeamProCanvas", 1);

            // Hub Controller + UIAdapter + CanvasController
            var hub = beamProCanvas.gameObject;
            hub.AddComponent<BeamPro.BeamProHubController>();
            hub.AddComponent<UI.BeamProUIAdapter>();
            hub.AddComponent<UI.BeamProCanvasController>();

            // Locked Screen
            var locked = CreatePanel("LockedScreen", beamProCanvas.transform, new Color(0.1f, 0.1f, 0.1f, 0.95f));
            var lockedText = CreateTMPText("LockedText", locked.transform, "이 조건에서는 사용할 수 없습니다");
            lockedText.alignment = TextAlignmentOptions.Center;
            lockedText.fontSize = 24;
            locked.SetActive(false);

            // Tab Bar
            var tabBar = CreateGameObject("TabBar", beamProCanvas.transform);
            var tabBarRect = tabBar.AddComponent<RectTransform>();
            tabBarRect.anchorMin = new Vector2(0, 1);
            tabBarRect.anchorMax = new Vector2(1, 1);
            tabBarRect.pivot = new Vector2(0.5f, 1);
            tabBarRect.sizeDelta = new Vector2(0, 60);
            var tabLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabLayout.childForceExpandWidth = true;
            tabLayout.childControlWidth = true;
            tabLayout.spacing = 5;
            tabLayout.padding = new RectOffset(5, 5, 5, 5);

            CreateTabButton("TabBtn_Map", tabBar.transform, "맵");
            CreateTabButton("TabBtn_Info", tabBar.transform, "정보 카드");
            CreateTabButton("TabBtn_Mission", tabBar.transform, "미션 참조");

            // ContentArea (TabBar 아래 전체 영역)
            var contentAreaGO = CreateGameObject("ContentArea", beamProCanvas.transform);
            var contentAreaRect = contentAreaGO.AddComponent<RectTransform>();
            contentAreaRect.anchorMin = Vector2.zero;
            contentAreaRect.anchorMax = Vector2.one;
            contentAreaRect.offsetMin = Vector2.zero;
            contentAreaRect.offsetMax = new Vector2(0, -60);

            // Tab Panels → ContentArea 자식
            var mapPanel = CreatePanel("MapPanel", contentAreaGO.transform, new Color(0, 0, 0, 0));
            mapPanel.AddComponent<BeamPro.InteractiveMapController>();

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
            zoomPanel.AddComponent<UI.MapZoomButtons>();
            CreateUIButton("ZoomInBtn", zoomPanel.transform, "+");
            CreateUIButton("ZoomOutBtn", zoomPanel.transform, "\u2212");
            zoomPanel.SetActive(false); // BeamProCanvasController가 GlassOnly에서 활성화

            var infoPanel = CreatePanel("InfoCardPanel", contentAreaGO.transform, new Color(0, 0, 0, 0));
            infoPanel.AddComponent<BeamPro.InfoCardManager>();
            infoPanel.SetActive(false);

            var missionRefPanel = CreatePanel("MissionRefPanel", contentAreaGO.transform, new Color(0, 0, 0, 0));
            missionRefPanel.AddComponent<BeamPro.MissionRefPanel>();
            missionRefPanel.SetActive(false);

            // POI Detail Panel (overlay) → ContentArea 자식
            var poiDetail = CreatePanel("POIDetailPanel", contentAreaGO.transform, new Color(0.15f, 0.15f, 0.2f, 0.95f));
            poiDetail.AddComponent<BeamPro.POIDetailPanel>();
            poiDetail.SetActive(false);

            // Comparison Card (overlay) → ContentArea 자식
            var comparison = CreatePanel("ComparisonCard", contentAreaGO.transform, new Color(0.15f, 0.15f, 0.2f, 0.95f));
            comparison.AddComponent<BeamPro.ComparisonCardUI>();
            comparison.SetActive(false);

            // BeamPro Event Logger
            var bpLogger = CreateGameObject("BeamProEventLogger", beamProCanvas.transform);
            bpLogger.AddComponent<BeamPro.BeamProEventLogger>();
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
            expCanvas.gameObject.AddComponent<UI.GlassCanvasController>();

            // Mission Briefing Panel
            var briefing = CreatePanel("MissionBriefingPanel", expCanvas.transform, new Color(0.1f, 0.1f, 0.15f, 0.9f));
            briefing.AddComponent<Mission.MissionBriefingUI>();
            var briefRect = briefing.GetComponent<RectTransform>();
            briefRect.anchorMin = new Vector2(0.1f, 0.2f);
            briefRect.anchorMax = new Vector2(0.9f, 0.8f);
            briefRect.offsetMin = Vector2.zero;
            briefRect.offsetMax = Vector2.zero;

            CreateTMPText("MissionIdText", briefing.transform, "Mission A1");
            CreateTMPText("BriefingText", briefing.transform, "미션 브리핑 텍스트");
            CreateUIButton("ConfirmBtn", briefing.transform, "확인");
            briefing.SetActive(false);

            // Verification Panel
            var verify = CreatePanel("VerificationPanel", expCanvas.transform, new Color(0.1f, 0.1f, 0.15f, 0.9f));
            verify.AddComponent<Mission.VerificationUI>();
            var verRect = verify.GetComponent<RectTransform>();
            verRect.anchorMin = new Vector2(0.1f, 0.15f);
            verRect.anchorMax = new Vector2(0.9f, 0.85f);
            verRect.offsetMin = Vector2.zero;
            verRect.offsetMax = Vector2.zero;

            CreateTMPText("QuestionText", verify.transform, "검증 질문");
            var answersLayout = CreateGameObject("Answers", verify.transform);
            var alRect = answersLayout.AddComponent<RectTransform>();
            var vl = answersLayout.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 10;
            vl.childForceExpandHeight = false;
            for (int i = 0; i < 4; i++)
                CreateUIButton($"AnswerBtn_{i}", answersLayout.transform, $"선택지 {i + 1}");
            verify.SetActive(false);

            // Confidence Rating
            var confidence = CreatePanel("ConfidencePanel", expCanvas.transform, new Color(0.1f, 0.1f, 0.15f, 0.9f));
            confidence.AddComponent<UI.ConfidenceRatingUI>();
            var confRect = confidence.GetComponent<RectTransform>();
            confRect.anchorMin = new Vector2(0.15f, 0.2f);
            confRect.anchorMax = new Vector2(0.85f, 0.8f);
            confRect.offsetMin = Vector2.zero;
            confRect.offsetMax = Vector2.zero;

            CreateTMPText("PromptText", confidence.transform, "확신도를 평가해주세요");
            var ratingBar = CreateGameObject("RatingButtons", confidence.transform);
            var rbRect = ratingBar.AddComponent<RectTransform>();
            var hl = ratingBar.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8;
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = false;
            hl.childAlignment = TextAnchor.MiddleCenter;
            for (int i = 1; i <= 7; i++)
                CreateUIButton($"RatingBtn_{i}", ratingBar.transform, $"{i}");
            CreateTMPText("CurrentRatingText", confidence.transform, "");
            CreateUIButton("ConfirmRatingBtn", confidence.transform, "확인");
            confidence.SetActive(false);

            // Difficulty Rating
            var difficulty = CreatePanel("DifficultyPanel", expCanvas.transform, new Color(0.1f, 0.1f, 0.15f, 0.9f));
            difficulty.AddComponent<UI.DifficultyRatingUI>();
            difficulty.SetActive(false);

            // Experiment HUD (표시 전용 — 버튼은 ExperimenterHUD로 이동)
            var hud = CreatePanel("ExperimentHUD", expCanvas.transform, new Color(0, 0, 0, 0));
            hud.AddComponent<UI.ExperimentHUD>();
            var hudRect = hud.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0, 1);
            hudRect.anchorMax = new Vector2(0.4f, 1);
            hudRect.pivot = new Vector2(0, 1);
            hudRect.sizeDelta = new Vector2(0, 120);
            hudRect.anchoredPosition = new Vector2(5, -5);

            var hudLayout = hud.AddComponent<VerticalLayoutGroup>();
            hudLayout.childForceExpandWidth = true;
            hudLayout.childForceExpandHeight = false;
            hudLayout.childControlHeight = true;
            hudLayout.childControlWidth = true;
            hudLayout.spacing = 2;
            hudLayout.padding = new RectOffset(5, 5, 5, 5);

            var hudFitter = hud.AddComponent<ContentSizeFitter>();
            hudFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateTMPText("StateText", hud.transform, "State: Idle");
            CreateTMPText("ConditionText", hud.transform, "Condition: -");
            CreateTMPText("MissionText", hud.transform, "Mission: -");
            CreateTMPText("WPText", hud.transform, "WP: -");

            // T4 Proximity Text (for TriggerController)
            var proximityText = CreateTMPText("ProximityText", expCanvas.transform, "목적지 근처입니다");
            proximityText.alignment = TextAlignmentOptions.Center;
            proximityText.fontSize = 28;
            var ptRect = proximityText.GetComponent<RectTransform>();
            ptRect.anchorMin = new Vector2(0.2f, 0.4f);
            ptRect.anchorMax = new Vector2(0.8f, 0.6f);
            proximityText.gameObject.SetActive(false);

            // === Mapping Glass Overlay (매핑 모드 전용, 기본 비활성) ===
            var mappingOverlay = CreatePanel("MappingGlassOverlay", expCanvas.transform,
                new Color(0, 0, 0, 0));
            mappingOverlay.AddComponent<UI.MappingGlassOverlay>();

            // Header bar (상단)
            var mgHeaderBar = CreateGameObject("MG_HeaderBar", mappingOverlay.transform);
            var mgHeaderRect = mgHeaderBar.AddComponent<RectTransform>();
            mgHeaderRect.anchorMin = new Vector2(0, 1);
            mgHeaderRect.anchorMax = new Vector2(1, 1);
            mgHeaderRect.pivot = new Vector2(0.5f, 1);
            mgHeaderRect.sizeDelta = new Vector2(0, 50);
            var mgHeaderLayout = mgHeaderBar.AddComponent<HorizontalLayoutGroup>();
            mgHeaderLayout.childForceExpandWidth = false;
            mgHeaderLayout.childControlWidth = true;
            mgHeaderLayout.childControlHeight = true;
            mgHeaderLayout.spacing = 20;
            mgHeaderLayout.padding = new RectOffset(20, 20, 8, 8);
            mgHeaderLayout.childAlignment = TextAnchor.MiddleLeft;

            var mgHeader = CreateTMPText("MG_HeaderText", mgHeaderBar.transform, "매핑 모드");
            mgHeader.fontSize = 22;
            mgHeader.fontStyle = FontStyles.Bold;
            mgHeader.color = new Color(0.4f, 0.8f, 1f);
            var mgHeaderLE = mgHeader.gameObject.AddComponent<LayoutElement>();
            mgHeaderLE.flexibleWidth = 1;

            var mgRoute = CreateTMPText("MG_RouteText", mgHeaderBar.transform, "Route A");
            mgRoute.fontSize = 20;
            mgRoute.alignment = TextAlignmentOptions.Center;
            var mgRouteLE = mgRoute.gameObject.AddComponent<LayoutElement>();
            mgRouteLE.minWidth = 100;

            var mgProgress = CreateTMPText("MG_ProgressText", mgHeaderBar.transform, "0/8 완료");
            mgProgress.fontSize = 20;
            mgProgress.alignment = TextAlignmentOptions.Right;
            var mgProgressLE = mgProgress.gameObject.AddComponent<LayoutElement>();
            mgProgressLE.minWidth = 100;

            // Waypoint info (중앙)
            var mgWPText = CreateTMPText("MG_WaypointText", mappingOverlay.transform, "웨이포인트를 선택하세요");
            mgWPText.fontSize = 32;
            mgWPText.fontStyle = FontStyles.Bold;
            mgWPText.alignment = TextAlignmentOptions.Center;
            mgWPText.color = new Color(0.6f, 0.6f, 0.6f);
            var mgWPRect = mgWPText.GetComponent<RectTransform>();
            mgWPRect.anchorMin = new Vector2(0.05f, 0.35f);
            mgWPRect.anchorMax = new Vector2(0.95f, 0.65f);
            mgWPRect.offsetMin = Vector2.zero;
            mgWPRect.offsetMax = Vector2.zero;

            // Quality area (하단 왼쪽)
            var mgQualityArea = CreateGameObject("MG_QualityArea", mappingOverlay.transform);
            var mgQualityAreaRect = mgQualityArea.AddComponent<RectTransform>();
            mgQualityAreaRect.anchorMin = new Vector2(0, 0);
            mgQualityAreaRect.anchorMax = new Vector2(0.4f, 0.15f);
            mgQualityAreaRect.offsetMin = new Vector2(20, 10);
            mgQualityAreaRect.offsetMax = new Vector2(0, 0);
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

            var mgQuality = CreateTMPText("MG_QualityText", mgQualityArea.transform, "품질: 좋음");
            mgQuality.fontSize = 20;
            mgQuality.color = new Color(0.267f, 1f, 0.267f);

            // Flash message (하단 중앙)
            var mgFlash = CreateTMPText("MG_FlashText", mappingOverlay.transform, "");
            mgFlash.fontSize = 26;
            mgFlash.fontStyle = FontStyles.Bold;
            mgFlash.alignment = TextAlignmentOptions.Center;
            mgFlash.alpha = 0f;
            var mgFlashRect = mgFlash.GetComponent<RectTransform>();
            mgFlashRect.anchorMin = new Vector2(0.2f, 0.15f);
            mgFlashRect.anchorMax = new Vector2(0.8f, 0.35f);
            mgFlashRect.offsetMin = Vector2.zero;
            mgFlashRect.offsetMax = Vector2.zero;

            mappingOverlay.SetActive(false);
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
            scaler.referenceResolution = new Vector2(1920, 1080);
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
            rect.sizeDelta = new Vector2(0, 50);
            return btn;
        }
    }
}

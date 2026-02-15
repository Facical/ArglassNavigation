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
                "현재 씬에 실험 시스템을 구성합니다.\n기존 오브젝트는 유지됩니다.", "진행", "취소"))
                return;

            CreateManagers();
            CreateARSystem();
            CreateBeamProUI();
            CreateExperimentUI();

            Debug.Log("[SceneSetup] 메인 실험 씬 구성 완료!");
            EditorUtility.DisplayDialog("완료", "씬 셋업이 완료되었습니다.\n\nHierarchy에서 각 컴포넌트의 참조를 연결해주세요.", "확인");
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
        }

        private static void CreateBeamProUI()
        {
            // --- Beam Pro UI ---
            var beamProCanvas = CreateCanvas("BeamProCanvas", 1);

            // Hub Controller
            var hub = beamProCanvas.gameObject;
            hub.AddComponent<BeamPro.BeamProHubController>();

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
            tabLayout.spacing = 5;
            tabLayout.padding = new RectOffset(5, 5, 5, 5);

            CreateTabButton("TabBtn_Map", tabBar.transform, "맵");
            CreateTabButton("TabBtn_Info", tabBar.transform, "정보 카드");
            CreateTabButton("TabBtn_Mission", tabBar.transform, "미션 참조");

            // Tab Panels
            var mapPanel = CreatePanel("MapPanel", beamProCanvas.transform, new Color(0, 0, 0, 0));
            mapPanel.AddComponent<BeamPro.InteractiveMapController>();

            var infoPanel = CreatePanel("InfoCardPanel", beamProCanvas.transform, new Color(0, 0, 0, 0));
            infoPanel.AddComponent<BeamPro.InfoCardManager>();
            infoPanel.SetActive(false);

            var missionRefPanel = CreatePanel("MissionRefPanel", beamProCanvas.transform, new Color(0, 0, 0, 0));
            missionRefPanel.AddComponent<BeamPro.MissionRefPanel>();
            missionRefPanel.SetActive(false);

            // POI Detail Panel (overlay)
            var poiDetail = CreatePanel("POIDetailPanel", beamProCanvas.transform, new Color(0.15f, 0.15f, 0.2f, 0.95f));
            poiDetail.AddComponent<BeamPro.POIDetailPanel>();
            poiDetail.SetActive(false);

            // Comparison Card (overlay)
            var comparison = CreatePanel("ComparisonCard", beamProCanvas.transform, new Color(0.15f, 0.15f, 0.2f, 0.95f));
            comparison.AddComponent<BeamPro.ComparisonCardUI>();
            comparison.SetActive(false);

            // BeamPro Event Logger
            var bpLogger = CreateGameObject("BeamProEventLogger", beamProCanvas.transform);
            bpLogger.AddComponent<BeamPro.BeamProEventLogger>();
        }

        private static void CreateExperimentUI()
        {
            // --- Experiment UI (Glass overlay) ---
            var expCanvas = CreateCanvas("ExperimentCanvas", 2);

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
            confidence.SetActive(false);

            // Difficulty Rating
            var difficulty = CreatePanel("DifficultyPanel", expCanvas.transform, new Color(0.1f, 0.1f, 0.15f, 0.9f));
            difficulty.AddComponent<UI.DifficultyRatingUI>();
            difficulty.SetActive(false);

            // Experiment HUD
            var hud = CreatePanel("ExperimentHUD", expCanvas.transform, new Color(0, 0, 0, 0));
            hud.AddComponent<UI.ExperimentHUD>();
            var hudRect = hud.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0, 1);
            hudRect.anchorMax = new Vector2(0.4f, 1);
            hudRect.pivot = new Vector2(0, 1);
            hudRect.sizeDelta = new Vector2(0, 120);

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
            textGo.AddComponent<RectTransform>();
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

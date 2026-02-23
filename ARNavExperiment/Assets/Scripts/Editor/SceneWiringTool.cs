using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

namespace ARNavExperiment.EditorTools
{
    public class SceneWiringTool
    {
        [MenuItem("ARNav/Wire Scene References")]
        public static void WireReferences()
        {
            WireReferencesSilent();
            EditorUtility.DisplayDialog("완료", "컴포넌트 참조 연결이 완료되었습니다.", "확인");
        }

        /// <summary>다이얼로그 없이 실행 (MasterSetupTool에서 호출용)</summary>
        public static void WireReferencesSilent()
        {
            WireConditionController();
            WireTriggerController();
            WireARArrowRenderer();
            WireBeamProHub();
            WireMissionBriefingUI();
            WireVerificationUI();
            WireConfidenceRatingUI();
            WireDifficultyRatingUI();
            WireMissionManager();
            WireExperimentHUD();
            WireExperimenterHUD();
            WireInfoCardManager();
            WirePOIDetailPanel();
            WireSpatialAnchorManager();
            WireAppModeSelector();
            WireRelocalizationUI();
            WireBeamProUIAdapters();
            WireBeamProCanvasController();
            WireMapZoomButtons();
            WireMappingGlassOverlay();

            Debug.Log("[SceneWiring] 모든 참조 연결 완료!");
        }

        private static void WireConditionController()
        {
            var cc = Object.FindObjectOfType<Core.ConditionController>();
            if (cc == null) return;
            var so = new SerializedObject(cc);
            SetObjectRef(so, "beamProUI", FindGO("BeamProCanvas"));
            SetObjectRef(so, "lockedScreenUI", FindGO("LockedScreen"));
            so.ApplyModifiedProperties();
        }

        private static void WireTriggerController()
        {
            var tc = Object.FindObjectOfType<Navigation.TriggerController>();
            if (tc == null) return;
            var so = new SerializedObject(tc);
            SetObjectRef(so, "arrowRenderer", FindComponent<Navigation.ARArrowRenderer>());
            SetObjectRef(so, "proximityText", FindComponent<TextMeshProUGUI>("ProximityText"));
            so.ApplyModifiedProperties();
        }

        private static void WireARArrowRenderer()
        {
            var arr = Object.FindObjectOfType<Navigation.ARArrowRenderer>();
            if (arr == null) return;
            var so = new SerializedObject(arr);
            var arrowVisual = FindGO("ArrowVisual");
            SetObjectRef(so, "arrowObject", arrowVisual);
            so.ApplyModifiedProperties();
        }

        private static void WireBeamProHub()
        {
            var hub = Object.FindObjectOfType<BeamPro.BeamProHubController>();
            if (hub == null) return;
            var so = new SerializedObject(hub);
            SetObjectRef(so, "hubRoot", FindGO("BeamProCanvas"));
            SetObjectRef(so, "lockedScreen", FindGO("LockedScreen"));
            SetObjectRef(so, "mapController", FindComponent<BeamPro.InteractiveMapController>());
            SetObjectRef(so, "infoCardManager", FindComponent<BeamPro.InfoCardManager>());
            SetObjectRef(so, "missionRefPanel", FindComponent<BeamPro.MissionRefPanel>());

            // tab panels
            var mapPanel = FindGO("MapPanel");
            var infoPanel = FindGO("InfoCardPanel");
            var missionRefPanel = FindGO("MissionRefPanel");
            var tabPanelsProp = so.FindProperty("tabPanels");
            if (tabPanelsProp != null)
            {
                tabPanelsProp.arraySize = 3;
                if (mapPanel) tabPanelsProp.GetArrayElementAtIndex(0).objectReferenceValue = mapPanel;
                if (infoPanel) tabPanelsProp.GetArrayElementAtIndex(1).objectReferenceValue = infoPanel;
                if (missionRefPanel) tabPanelsProp.GetArrayElementAtIndex(2).objectReferenceValue = missionRefPanel;
            }

            // tab buttons
            var tabBar = FindGO("TabBar");
            if (tabBar != null)
            {
                var buttons = tabBar.GetComponentsInChildren<Button>();
                var tabBtnsProp = so.FindProperty("tabButtons");
                if (tabBtnsProp != null)
                {
                    tabBtnsProp.arraySize = buttons.Length;
                    for (int i = 0; i < buttons.Length; i++)
                        tabBtnsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireMissionBriefingUI()
        {
            var ui = Object.FindObjectOfType<Mission.MissionBriefingUI>();
            if (ui == null) return;
            var panel = ui.gameObject;
            var so = new SerializedObject(ui);
            SetObjectRef(so, "panel", panel);

            var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.gameObject.name == "MissionIdText") SetObjectRef(so, "missionIdText", t);
                if (t.gameObject.name == "BriefingText") SetObjectRef(so, "briefingText", t);
            }

            var btns = panel.GetComponentsInChildren<Button>(true);
            if (btns.Length > 0) SetObjectRef(so, "confirmButton", btns[0]);

            so.ApplyModifiedProperties();
        }

        private static void WireVerificationUI()
        {
            var ui = Object.FindObjectOfType<Mission.VerificationUI>();
            if (ui == null) return;
            var panel = ui.gameObject;
            var so = new SerializedObject(ui);
            SetObjectRef(so, "panel", panel);

            var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.gameObject.name == "QuestionText") SetObjectRef(so, "questionText", t);
            }

            // answer buttons
            var answersGO = FindChildRecursive(panel.transform, "Answers");
            if (answersGO != null)
            {
                var btns = answersGO.GetComponentsInChildren<Button>(true);
                var btnsProp = so.FindProperty("answerButtons");
                var txtsProp = so.FindProperty("answerTexts");
                if (btnsProp != null)
                {
                    btnsProp.arraySize = btns.Length;
                    for (int i = 0; i < btns.Length; i++)
                        btnsProp.GetArrayElementAtIndex(i).objectReferenceValue = btns[i];
                }
                if (txtsProp != null)
                {
                    txtsProp.arraySize = btns.Length;
                    for (int i = 0; i < btns.Length; i++)
                    {
                        var txt = btns[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (txt) txtsProp.GetArrayElementAtIndex(i).objectReferenceValue = txt;
                    }
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireConfidenceRatingUI()
        {
            var ui = Object.FindObjectOfType<UI.ConfidenceRatingUI>();
            if (ui == null) return;
            var panel = ui.gameObject;
            var so = new SerializedObject(ui);
            SetObjectRef(so, "panel", panel);

            var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.gameObject.name == "PromptText") SetObjectRef(so, "promptText", t);
                if (t.gameObject.name == "CurrentRatingText") SetObjectRef(so, "currentRatingText", t);
            }

            // Rating buttons (7 buttons inside RatingButtons container)
            var ratingBar = FindChildRecursive(panel.transform, "RatingButtons");
            if (ratingBar != null)
            {
                var btns = ratingBar.GetComponentsInChildren<Button>(true);
                var btnsProp = so.FindProperty("ratingButtons");
                if (btnsProp != null)
                {
                    btnsProp.arraySize = btns.Length;
                    for (int i = 0; i < btns.Length; i++)
                        btnsProp.GetArrayElementAtIndex(i).objectReferenceValue = btns[i];
                }
            }

            // Confirm button (last button that's not inside RatingButtons)
            var allBtns = panel.GetComponentsInChildren<Button>(true);
            foreach (var btn in allBtns)
            {
                if (btn.gameObject.name == "ConfirmRatingBtn")
                {
                    SetObjectRef(so, "confirmButton", btn);
                    break;
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireDifficultyRatingUI()
        {
            var ui = Object.FindObjectOfType<UI.DifficultyRatingUI>();
            if (ui == null) return;
            var so = new SerializedObject(ui);
            SetObjectRef(so, "panel", ui.gameObject);
            so.ApplyModifiedProperties();
        }

        private static void WireMissionManager()
        {
            var mgr = Object.FindObjectOfType<Mission.MissionManager>();
            if (mgr == null) return;
            var so = new SerializedObject(mgr);
            SetObjectRef(so, "briefingUI", FindComponent<Mission.MissionBriefingUI>());
            SetObjectRef(so, "verificationUI", FindComponent<Mission.VerificationUI>());
            SetObjectRef(so, "confidenceRatingUI", FindComponent<UI.ConfidenceRatingUI>());
            SetObjectRef(so, "difficultyRatingUI", FindComponent<UI.DifficultyRatingUI>());
            SetObjectRef(so, "arrowRenderer", FindComponent<Navigation.ARArrowRenderer>());
            so.ApplyModifiedProperties();
        }

        private static void WireExperimentHUD()
        {
            var hud = Object.FindObjectOfType<UI.ExperimentHUD>();
            if (hud == null) return;
            var panel = hud.gameObject;
            var so = new SerializedObject(hud);

            var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                switch (t.gameObject.name)
                {
                    case "StateText": SetObjectRef(so, "stateText", t); break;
                    case "ConditionText": SetObjectRef(so, "conditionText", t); break;
                    case "MissionText": SetObjectRef(so, "missionText", t); break;
                    case "WPText": SetObjectRef(so, "waypointText", t); break;
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireExperimenterHUD()
        {
            var hud = Object.FindObjectOfType<UI.ExperimenterHUD>(true);
            if (hud == null) return;
            var panel = hud.gameObject;
            var so = new SerializedObject(hud);

            // Status texts
            var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                switch (t.gameObject.name)
                {
                    case "StateText": SetObjectRef(so, "stateText", t); break;
                    case "ConditionText": SetObjectRef(so, "conditionText", t); break;
                    case "MissionText": SetObjectRef(so, "missionText", t); break;
                    case "WPText": SetObjectRef(so, "waypointText", t); break;
                    case "AnchorText": SetObjectRef(so, "anchorStatusText", t); break;
                }
            }

            // Control buttons
            var btns = panel.GetComponentsInChildren<Button>(true);
            foreach (var btn in btns)
            {
                switch (btn.gameObject.name)
                {
                    case "AdvanceBtn": SetObjectRef(so, "advanceButton", btn); break;
                    case "NextMissionBtn": SetObjectRef(so, "nextMissionButton", btn); break;
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireInfoCardManager()
        {
            var mgr = Object.FindObjectOfType<BeamPro.InfoCardManager>();
            if (mgr == null) return;
            var so = new SerializedObject(mgr);
            SetObjectRef(so, "comparisonCard", FindComponent<BeamPro.ComparisonCardUI>());
            so.ApplyModifiedProperties();
        }

        private static void WirePOIDetailPanel()
        {
            var panel = Object.FindObjectOfType<BeamPro.POIDetailPanel>(true);
            if (panel == null) return;
            var so = new SerializedObject(panel);
            SetObjectRef(so, "panel", panel.gameObject);
            so.ApplyModifiedProperties();
        }

        private static void WireSpatialAnchorManager()
        {
            var mgr = Object.FindObjectOfType<Core.SpatialAnchorManager>();
            if (mgr == null) return;
            var so = new SerializedObject(mgr);

#if XR_ARFOUNDATION
            var anchorManager = Object.FindObjectOfType<UnityEngine.XR.ARFoundation.ARAnchorManager>();
            if (anchorManager != null)
                SetObjectRef(so, "arAnchorManager", anchorManager);
#endif

            so.ApplyModifiedProperties();
        }

        private static void WireAppModeSelector()
        {
            var selector = Object.FindObjectOfType<UI.AppModeSelector>(true);
            if (selector == null) return;
            var so = new SerializedObject(selector);

            SetObjectRef(so, "modeSelectorPanel", FindGO("AppModeSelectorPanel"));
            SetObjectRef(so, "mappingModeButton", FindComponent<Button>("MappingModeBtn"));
            SetObjectRef(so, "experimentModeButton", FindComponent<Button>("ExperimentModeBtn"));
            SetObjectRef(so, "statusText", FindComponent<TextMeshProUGUI>("MappingStatusText"));
            SetObjectRef(so, "mappingModeUI", FindComponent<UI.MappingModeUI>());
            SetObjectRef(so, "sessionSetupPanel", FindGO("SessionSetupPanel"));

            so.ApplyModifiedProperties();
        }

        private static void WireRelocalizationUI()
        {
            var ui = Object.FindObjectOfType<UI.RelocalizationUI>(true);
            if (ui == null) return;
            var so = new SerializedObject(ui);
            var panel = FindGO("RelocalizationPanel");

            SetObjectRef(so, "relocalizationPanel", panel);

            if (panel != null)
            {
                var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    switch (t.gameObject.name)
                    {
                        case "RelocInstructionText": SetObjectRef(so, "instructionText", t); break;
                        case "RelocProgressText": SetObjectRef(so, "progressText", t); break;
                        case "RelocStatusText": SetObjectRef(so, "statusDetailText", t); break;
                        case "RelocSuccessText": SetObjectRef(so, "successCountText", t); break;
                        case "RelocFailedText": SetObjectRef(so, "failedCountText", t); break;
                    }
                }

                var images = panel.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    switch (img.gameObject.name)
                    {
                        case "RelocProgressBar": SetObjectRef(so, "progressBar", img); break;
                        case "RelocSuccessBar": SetObjectRef(so, "successBar", img); break;
                    }
                }

                // Result panel + buttons
                var resultPanel = FindChildRecursive(panel.transform, "ResultPanel");
                if (resultPanel != null)
                {
                    SetObjectRef(so, "resultPanel", resultPanel.gameObject);

                    var btns = resultPanel.GetComponentsInChildren<Button>(true);
                    foreach (var btn in btns)
                    {
                        switch (btn.gameObject.name)
                        {
                            case "ProceedBtn": SetObjectRef(so, "proceedButton", btn); break;
                            case "RetryBtn": SetObjectRef(so, "retryButton", btn); break;
                        }
                    }

                    var warnTexts = resultPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var t in warnTexts)
                    {
                        if (t.gameObject.name == "WarningText")
                            SetObjectRef(so, "warningText", t);
                    }
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireBeamProUIAdapters()
        {
            var adapters = Object.FindObjectsOfType<UI.BeamProUIAdapter>(true);
            foreach (var adapter in adapters)
            {
                var canvasGO = adapter.gameObject;
                var so = new SerializedObject(adapter);

                // CanvasScaler (공통)
                SetObjectRef(so, "targetScaler", canvasGO.GetComponent<CanvasScaler>());

                if (canvasGO.name == "ExperimenterCanvas")
                {
                    // HUD
                    var hudGO = FindChildRecursive(canvasGO.transform, "ExperimenterHUD");
                    if (hudGO != null)
                    {
                        SetObjectRef(so, "hudPanel", hudGO.GetComponent<RectTransform>());

                        var statusGO = hudGO.Find("StatusArea");
                        if (statusGO != null)
                        {
                            SetObjectRef(so, "statusArea", statusGO.GetComponent<RectTransform>());
                            SetObjectRef(so, "statusHLayout", statusGO.GetComponent<HorizontalLayoutGroup>());
                        }

                        var btnGO = hudGO.Find("ButtonArea");
                        if (btnGO != null)
                        {
                            SetObjectRef(so, "buttonArea", btnGO.GetComponent<RectTransform>());
                            SetObjectRef(so, "buttonHLayout", btnGO.GetComponent<HorizontalLayoutGroup>());
                        }
                    }

                    // FlowPanelArea
                    var flowArea = canvasGO.transform.Find("FlowPanelArea");
                    if (flowArea != null)
                        SetObjectRef(so, "flowPanelArea", flowArea.GetComponent<RectTransform>());
                }
                else if (canvasGO.name == "BeamProCanvas")
                {
                    // TabBar
                    var tabBarT = canvasGO.transform.Find("TabBar");
                    if (tabBarT != null)
                        SetObjectRef(so, "tabBar", tabBarT.GetComponent<RectTransform>());

                    // ContentArea
                    var contentAreaT = canvasGO.transform.Find("ContentArea");
                    if (contentAreaT != null)
                        SetObjectRef(so, "contentArea", contentAreaT.GetComponent<RectTransform>());
                }

                so.ApplyModifiedProperties();
            }
        }

        private static void WireBeamProCanvasController()
        {
            var ctrl = Object.FindObjectOfType<UI.BeamProCanvasController>(true);
            if (ctrl == null) return;
            var so = new SerializedObject(ctrl);
            SetObjectRef(so, "zoomButtonPanel", FindGO("ZoomButtonPanel"));
            so.ApplyModifiedProperties();
        }

        private static void WireMapZoomButtons()
        {
            var zoom = Object.FindObjectOfType<UI.MapZoomButtons>(true);
            if (zoom == null) return;
            var so = new SerializedObject(zoom);
            SetObjectRef(so, "mapController", FindComponent<BeamPro.InteractiveMapController>());
            SetObjectRef(so, "zoomInButton", FindComponent<Button>("ZoomInBtn"));
            SetObjectRef(so, "zoomOutButton", FindComponent<Button>("ZoomOutBtn"));
            so.ApplyModifiedProperties();
        }

        private static void WireMappingGlassOverlay()
        {
            var overlay = Object.FindObjectOfType<UI.MappingGlassOverlay>(true);
            if (overlay == null) return;
            var panel = overlay.gameObject;
            var so = new SerializedObject(overlay);

            SetObjectRef(so, "overlayPanel", panel);

            var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                switch (t.gameObject.name)
                {
                    case "MG_HeaderText": SetObjectRef(so, "headerText", t); break;
                    case "MG_RouteText": SetObjectRef(so, "routeText", t); break;
                    case "MG_ProgressText": SetObjectRef(so, "progressText", t); break;
                    case "MG_WaypointText": SetObjectRef(so, "waypointText", t); break;
                    case "MG_QualityText": SetObjectRef(so, "qualityText", t); break;
                    case "MG_FlashText": SetObjectRef(so, "flashText", t); break;
                }
            }

            var images = panel.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject.name == "MG_QualityIcon")
                    SetObjectRef(so, "qualityIcon", img);
            }

            so.ApplyModifiedProperties();
        }

        // --- Utility ---

        private static GameObject FindGO(string name)
        {
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in all)
            {
                if (go.name == name && go.scene.isLoaded)
                    return go;
            }
            return null;
        }

        private static T FindComponent<T>(string goName = null) where T : Component
        {
            if (!string.IsNullOrEmpty(goName))
            {
                var go = FindGO(goName);
                return go != null ? go.GetComponent<T>() : null;
            }
            return Object.FindObjectOfType<T>(true);
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetObjectRef(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null && value != null)
                prop.objectReferenceValue = value;
        }
    }
}

using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Presentation.Shared;
using ARNavExperiment.Presentation.Mapping;
using ARNavExperiment.Presentation.Experimenter;
using ARNavExperiment.Presentation.Glass;

namespace ARNavExperiment.EditorTools
{
    public class FlowUISetupTool
    {
        [MenuItem("ARNav/Cleanup Flow UI (중복 제거)")]
        public static void CleanupFlowUI()
        {
            string[] targets = {
                "AppModeSelectorPanel", "MappingModePanel", "RelocalizationPanel",
                "ConditionTransitionPanel",
                "SurveyPromptPanel", "CompletionPanel", "ExperimentFlowUI",
                "GlassRelocPanel", "GlassSetupPanel", "GlassRunningStartPanel",
                "GlassSurveyPanel", "GlassCompletionPanel", "GlassFlowUI"
            };

            int removed = 0;
            foreach (var name in targets)
            {
                GameObject found;
                while ((found = GameObject.Find(name)) != null)
                {
                    Undo.DestroyObjectImmediate(found);
                    removed++;
                }
            }

            if (removed > 0)
                Debug.Log($"[FlowUI Cleanup] {removed}개 오브젝트 삭제 완료");
        }

        /// <summary>다이얼로그 포함 버전 (개별 메뉴에서 호출)</summary>
        [MenuItem("ARNav/Cleanup Flow UI (중복 제거) - 대화상자")]
        public static void CleanupFlowUIWithDialog()
        {
            CleanupFlowUI();
            EditorUtility.DisplayDialog("정리 완료", "FlowUI 오브젝트 정리가 완료되었습니다.", "확인");
        }

        [MenuItem("ARNav/Setup Experiment Flow UI")]
        public static void SetupFlowUI()
        {
            var experimenterCanvas = GameObject.Find("ExperimenterCanvas");
            if (experimenterCanvas == null)
            {
                Debug.LogError("[FlowUISetup] ExperimenterCanvas를 찾을 수 없습니다. 먼저 Setup Main Scene을 실행하세요.");
                return;
            }

            var flowPanelArea = experimenterCanvas.transform.Find("FlowPanelArea");
            var canvasTransform = flowPanelArea != null ? flowPanelArea : experimenterCanvas.transform;

            // === App Mode Selector Panel (새 레이아웃) ===
            var modeSelectorPanel = CreatePanel("AppModeSelectorPanel", canvasTransform,
                new Color(0.05f, 0.05f, 0.1f, 0.98f));
            var modeSelectorUI = modeSelectorPanel.AddComponent<AppModeSelector>();

            // Title
            var modeTitleText = CreateTMP("ModeTitle", modeSelectorPanel.transform,
                "AR Navigation Experiment", 36, TextAlignmentOptions.Center);
            SetRect(modeTitleText.gameObject, new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.97f));

            // PID Label
            CreateTMP("PIDLabel", modeSelectorPanel.transform,
                "Participant ID:", 16, TextAlignmentOptions.Left,
                new Vector2(0.12f, 0.77f), new Vector2(0.35f, 0.85f));

            // PID Input
            var pidInputGO = CreateInputField("ParticipantIdInput", modeSelectorPanel.transform, "P01");
            SetRect(pidInputGO, new Vector2(0.38f, 0.77f), new Vector2(0.88f, 0.85f));

            // Route A Button
            var routeABtn = CreateButton("RouteABtn", modeSelectorPanel.transform,
                "Route A", new Color(0.2f, 0.5f, 0.8f, 1f), 18);
            SetRect(routeABtn.gameObject, new Vector2(0.12f, 0.66f), new Vector2(0.48f, 0.75f));

            // Route B Button
            var routeBBtn = CreateButton("RouteBBtn", modeSelectorPanel.transform,
                "Route B", new Color(0.3f, 0.3f, 0.35f, 1f), 18);
            SetRect(routeBBtn.gameObject, new Vector2(0.52f, 0.66f), new Vector2(0.88f, 0.75f));

            // Glass Only Button (파란색)
            var glassOnlyBtn = CreateButton("GlassOnlyBtn", modeSelectorPanel.transform,
                "Glass Only", new Color(0.15f, 0.35f, 0.65f, 1f), 22);
            SetRect(glassOnlyBtn.gameObject, new Vector2(0.12f, 0.48f), new Vector2(0.88f, 0.63f));

            // Hybrid Button (초록색)
            var hybridBtn = CreateButton("HybridBtn", modeSelectorPanel.transform,
                "Hybrid", new Color(0.15f, 0.5f, 0.25f, 1f), 22);
            SetRect(hybridBtn.gameObject, new Vector2(0.12f, 0.30f), new Vector2(0.88f, 0.45f));

            // Error Text (기본 숨김)
            var errorText = CreateTMP("ErrorText", modeSelectorPanel.transform,
                "", 14, TextAlignmentOptions.Center,
                new Vector2(0.12f, 0.22f), new Vector2(0.88f, 0.29f));
            errorText.color = new Color(1f, 0.4f, 0.4f);
            errorText.gameObject.SetActive(false);

            // Mapping Status Text
            var mappingStatusText = CreateTMP("MappingStatusText", modeSelectorPanel.transform,
                "Mapping status: checking...", 14, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.14f), new Vector2(0.9f, 0.22f));
            mappingStatusText.color = new Color(0.7f, 0.7f, 0.7f);

            // Language Button
            var langBtn = CreateButton("LanguageBtn", modeSelectorPanel.transform,
                "Language", new Color(0.4f, 0.35f, 0.5f, 1f), 16);
            SetRect(langBtn.gameObject, new Vector2(0.12f, 0.03f), new Vector2(0.45f, 0.12f));

            // Mapping Button
            var mappingBtn = CreateButton("MappingBtn", modeSelectorPanel.transform,
                "Mapping", new Color(0.3f, 0.3f, 0.4f, 1f), 16);
            SetRect(mappingBtn.gameObject, new Vector2(0.55f, 0.03f), new Vector2(0.88f, 0.12f));

            // Language Popup (기본 비활성)
            var langPopup = CreatePanel("LanguagePopup", modeSelectorPanel.transform,
                new Color(0.12f, 0.12f, 0.18f, 0.95f));
            SetRect(langPopup, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.22f));

            var langKoBtn = CreateButton("LangKoBtn", langPopup.transform,
                "\ud55c\uad6d\uc5b4", new Color(0.3f, 0.3f, 0.5f, 1f), 18);
            SetRect(langKoBtn.gameObject, new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.95f));

            var langEnBtn = CreateButton("LangEnBtn", langPopup.transform,
                "English", new Color(0.3f, 0.3f, 0.5f, 1f), 18);
            SetRect(langEnBtn.gameObject, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.45f));

            langPopup.SetActive(false);

            // === Mapping Mode Panel ===
            var mappingPanel = CreatePanel("MappingModePanel", canvasTransform,
                new Color(0.05f, 0.05f, 0.1f, 0.98f));
            var mappingUI = mappingPanel.AddComponent<MappingModeUI>();
            mappingPanel.SetActive(false);

            // Mapping Title
            var mappingTitle = CreateTMP("MappingTitle", mappingPanel.transform,
                "Mapping Mode", 24, TextAlignmentOptions.Center);
            SetRect(mappingTitle.gameObject, new Vector2(0.2f, 0.92f), new Vector2(0.8f, 0.98f));

            // Quality Text
            var qualityText = CreateTMP("QualityText", mappingPanel.transform,
                "Mapping quality: checking...", 15, TextAlignmentOptions.Left,
                new Vector2(0.03f, 0.84f), new Vector2(0.28f, 0.91f));

            // Quality Bar Background
            var qualityBarBG = CreatePanel("QualityBarBG", mappingPanel.transform,
                new Color(0.25f, 0.25f, 0.3f));
            SetRect(qualityBarBG, new Vector2(0.28f, 0.86f), new Vector2(0.52f, 0.89f));

            // Quality Bar Fill
            var qualityBarFill = new GameObject("QualityBar");
            qualityBarFill.transform.SetParent(qualityBarBG.transform, false);
            var qbRect = qualityBarFill.AddComponent<RectTransform>();
            qbRect.anchorMin = Vector2.zero;
            qbRect.anchorMax = Vector2.one;
            qbRect.offsetMin = Vector2.zero;
            qbRect.offsetMax = Vector2.zero;
            var qbImg = qualityBarFill.AddComponent<Image>();
            qbImg.color = new Color(0.3f, 0.8f, 0.3f);
            qbImg.type = Image.Type.Filled;
            qbImg.fillMethod = Image.FillMethod.Horizontal;
            qbImg.fillAmount = 0f;

            // Route Dropdown
            var routeDropdown = CreateDropdown("RouteDropdown", mappingPanel.transform);
            SetRect(routeDropdown, new Vector2(0.76f, 0.84f), new Vector2(0.97f, 0.91f));

            // Waypoint List ScrollView
            var waypointListGO = new GameObject("WaypointList");
            waypointListGO.transform.SetParent(mappingPanel.transform, false);
            waypointListGO.AddComponent<RectTransform>();
            SetRect(waypointListGO, new Vector2(0.03f, 0.19f), new Vector2(0.97f, 0.82f));
            var wlImg = waypointListGO.AddComponent<Image>();
            wlImg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(waypointListGO.transform, false);
            var vpRect2 = viewportGO.AddComponent<RectTransform>();
            vpRect2.anchorMin = Vector2.zero;
            vpRect2.anchorMax = Vector2.one;
            vpRect2.offsetMin = Vector2.zero;
            vpRect2.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = Color.white;
            var vpMask2 = viewportGO.AddComponent<Mask>();
            vpMask2.showMaskGraphic = false;

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);
            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = waypointListGO.AddComponent<ScrollRect>();
            sr.content = contentRect;
            sr.viewport = vpRect2;
            sr.vertical = true;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;

            var createAnchorBtn = CreateButton("CreateAnchorBtn", mappingPanel.transform,
                "Select a waypoint", new Color(0.3f, 0.5f, 0.7f, 1f), 16);
            SetRect(createAnchorBtn.gameObject, new Vector2(0.03f, 0.11f), new Vector2(0.97f, 0.18f));

            var mapZoomBtn = CreateButton("MapZoomBtn", mappingPanel.transform,
                "Zoom Map", new Color(0.35f, 0.25f, 0.55f, 1f), 16);
            SetRect(mapZoomBtn.gameObject, new Vector2(0.03f, 0.02f), new Vector2(0.34f, 0.09f));

            var saveAllBtn = CreateButton("SaveAllBtn", mappingPanel.transform,
                "Save All", new Color(0.2f, 0.5f, 0.2f, 1f), 16);
            SetRect(saveAllBtn.gameObject, new Vector2(0.35f, 0.02f), new Vector2(0.66f, 0.09f));

            var backBtn = CreateButton("BackBtn", mappingPanel.transform,
                "Back", new Color(0.4f, 0.4f, 0.4f, 1f), 16);
            SetRect(backBtn.gameObject, new Vector2(0.67f, 0.02f), new Vector2(0.97f, 0.09f));

            WireMappingModeUI(mappingUI, mappingPanel, mappingTitle, qualityText,
                qbImg, createAnchorBtn, mapZoomBtn, saveAllBtn, backBtn, routeDropdown, contentGO.transform);

            // === Relocalization Panel ===
            var relocPanel = CreatePanel("RelocalizationPanel", canvasTransform,
                new Color(0.05f, 0.05f, 0.1f, 0.98f));
            var relocUI = relocPanel.AddComponent<RelocalizationUI>();
            relocPanel.SetActive(false);

            var relocInstr = CreateTMP("RelocInstructionText", relocPanel.transform,
                "Scanning the environment...\nPlease look around slowly.", 24, TextAlignmentOptions.Center);
            SetRect(relocInstr.gameObject, new Vector2(0.1f, 0.6f), new Vector2(0.9f, 0.8f));

            var relocProgress = CreateTMP("RelocProgressText", relocPanel.transform,
                "Preparing...", 18, TextAlignmentOptions.Center);
            SetRect(relocProgress.gameObject, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.58f));

            var relocBarBG = CreatePanel("RelocBarBG", relocPanel.transform,
                new Color(0.2f, 0.2f, 0.2f));
            SetRect(relocBarBG, new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.45f));

            var relocBarFill = new GameObject("RelocProgressBar");
            relocBarFill.transform.SetParent(relocBarBG.transform, false);
            var rbRect = relocBarFill.AddComponent<RectTransform>();
            rbRect.anchorMin = Vector2.zero;
            rbRect.anchorMax = Vector2.one;
            rbRect.offsetMin = Vector2.zero;
            rbRect.offsetMax = Vector2.zero;
            var rbImg = relocBarFill.AddComponent<Image>();
            rbImg.color = new Color(0.2f, 0.6f, 1f);
            rbImg.type = Image.Type.Filled;
            rbImg.fillMethod = Image.FillMethod.Horizontal;
            rbImg.fillAmount = 0f;

            var relocStatus = CreateTMP("RelocStatusText", relocPanel.transform,
                "", 14, TextAlignmentOptions.Center);
            SetRect(relocStatus.gameObject, new Vector2(0.2f, 0.32f), new Vector2(0.8f, 0.38f));
            relocStatus.color = new Color(0.7f, 0.7f, 0.7f);

            var successBarBG = CreatePanel("SuccessBarBG", relocPanel.transform,
                new Color(0.2f, 0.2f, 0.2f));
            SetRect(successBarBG, new Vector2(0.2f, 0.25f), new Vector2(0.8f, 0.3f));

            var successBarFill = new GameObject("RelocSuccessBar");
            successBarFill.transform.SetParent(successBarBG.transform, false);
            var sbRect = successBarFill.AddComponent<RectTransform>();
            sbRect.anchorMin = Vector2.zero;
            sbRect.anchorMax = Vector2.one;
            sbRect.offsetMin = Vector2.zero;
            sbRect.offsetMax = Vector2.zero;
            var sbImg = successBarFill.AddComponent<Image>();
            sbImg.color = new Color(0.3f, 0.8f, 0.3f);
            sbImg.type = Image.Type.Filled;
            sbImg.fillMethod = Image.FillMethod.Horizontal;
            sbImg.fillAmount = 0f;

            var relocSuccessText = CreateTMP("RelocSuccessText", relocPanel.transform,
                "", 14, TextAlignmentOptions.Left,
                new Vector2(0.2f, 0.19f), new Vector2(0.5f, 0.25f));
            relocSuccessText.color = new Color(0.3f, 0.9f, 0.3f);

            var relocFailedText = CreateTMP("RelocFailedText", relocPanel.transform,
                "", 14, TextAlignmentOptions.Right,
                new Vector2(0.5f, 0.19f), new Vector2(0.8f, 0.25f));
            relocFailedText.color = new Color(1f, 0.4f, 0.4f);

            var resultPanel = CreatePanel("ResultPanel", relocPanel.transform,
                new Color(0.1f, 0.1f, 0.15f, 0.95f));
            SetRect(resultPanel, new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.28f));

            var relocWarningText = CreateTMP("WarningText", resultPanel.transform,
                "", 13, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.95f));
            relocWarningText.color = new Color(1f, 0.8f, 0.4f);

            var proceedBtn = CreateButton("ProceedBtn", resultPanel.transform,
                "Proceed", new Color(0.2f, 0.5f, 0.2f, 1f), 14);
            SetRect(proceedBtn.gameObject, new Vector2(0.05f, 0.05f), new Vector2(0.48f, 0.32f));

            var retryBtn = CreateButton("RetryBtn", resultPanel.transform,
                "Retry", new Color(0.2f, 0.4f, 0.6f, 1f), 14);
            SetRect(retryBtn.gameObject, new Vector2(0.52f, 0.05f), new Vector2(0.95f, 0.32f));

            resultPanel.SetActive(false);

            WireRelocalizationUI(relocUI, relocPanel, relocInstr, relocProgress, rbImg, relocStatus,
                relocSuccessText, relocFailedText, sbImg, resultPanel, proceedBtn, retryBtn, relocWarningText);

            // === Condition Transition Panel ===
            var transPanel = CreatePanel("ConditionTransitionPanel", canvasTransform,
                new Color(0.08f, 0.08f, 0.12f, 0.98f));
            transPanel.SetActive(false);

            var transTitle = CreateTMP("TransitionTitle", transPanel.transform,
                "Condition Start", 28, TextAlignmentOptions.Center);
            SetRect(transTitle.gameObject, new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.92f));

            var transDetail = CreateTMP("TransitionDetail", transPanel.transform,
                "", 16, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.75f));
            transDetail.enableWordWrapping = true;

            var transContinueBtn = CreateButton("TransitionContinueBtn", transPanel.transform,
                "Continue", new Color(0.2f, 0.5f, 0.2f, 1f), 20);
            SetRect(transContinueBtn.gameObject, new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.2f));

            // === Survey Prompt Panel ===
            var surveyPanel = CreatePanel("SurveyPromptPanel", canvasTransform,
                new Color(0.08f, 0.08f, 0.12f, 0.98f));
            surveyPanel.SetActive(false);

            var surveyTitle = CreateTMP("SurveyTitle", surveyPanel.transform,
                "Survey", 28, TextAlignmentOptions.Center);
            SetRect(surveyTitle.gameObject, new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.92f));

            var surveyInstr = CreateTMP("SurveyInstruction", surveyPanel.transform,
                "", 16, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.75f));
            surveyInstr.enableWordWrapping = true;

            var surveyDoneBtn = CreateButton("SurveyDoneBtn", surveyPanel.transform,
                "Survey Done", new Color(0.2f, 0.5f, 0.2f, 1f), 20);
            SetRect(surveyDoneBtn.gameObject, new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.2f));

            // === Completion Panel ===
            var completePanel = CreatePanel("CompletionPanel", canvasTransform,
                new Color(0.08f, 0.08f, 0.12f, 0.98f));
            completePanel.SetActive(false);

            var completeText = CreateTMP("CompletionText", completePanel.transform,
                "", 20, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.75f));

            // === ExperimentFlowUI Component ===
            var flowUIGO = new GameObject("ExperimentFlowUI");
            Undo.RegisterCreatedObjectUndo(flowUIGO, "Create ExperimentFlowUI");
            flowUIGO.transform.SetParent(experimenterCanvas.transform, false);
            var flowUI = flowUIGO.AddComponent<ExperimentFlowUI>();

            WireFlowUI(flowUI,
                modeSelectorPanel, relocPanel, relocUI,
                transPanel, surveyPanel, completePanel,
                transTitle, transDetail, transContinueBtn,
                surveyTitle, surveyInstr, surveyDoneBtn,
                completeText);

            // === GlassFlowUI (GlassOnly 조건 전용 — ExperimentCanvas에 배치) ===
            CreateGlassFlowPanels();

            Debug.Log("[FlowUISetup] 실험 플로우 UI 구성 완료!");
        }

        /// <summary>
        /// ExperimentCanvas에 GlassFlowUI 컴포넌트 + 5개 패널 생성.
        /// SceneWiringTool.WireGlassFlowUI()가 이름 기반으로 와이어링.
        /// </summary>
        private static void CreateGlassFlowPanels()
        {
            var expCanvas = GameObject.Find("ExperimentCanvas");
            if (expCanvas == null)
            {
                Debug.LogWarning("[FlowUISetup] ExperimentCanvas를 찾을 수 없어 GlassFlowUI를 생성하지 않습니다.");
                return;
            }

            // 이미 존재하면 스킵
            if (expCanvas.GetComponentInChildren<GlassFlowUI>(true) != null)
            {
                Debug.Log("[FlowUISetup] GlassFlowUI가 이미 존재합니다. 스킵.");
                return;
            }

            var canvasTransform = expCanvas.transform;

            // --- GlassFlowUI 컴포넌트 ---
            var flowUIGO = new GameObject("GlassFlowUI");
            Undo.RegisterCreatedObjectUndo(flowUIGO, "Create GlassFlowUI");
            flowUIGO.transform.SetParent(canvasTransform, false);
            flowUIGO.AddComponent<GlassFlowUI>();

            // --- GlassRelocPanel ---
            var gRelocPanel = CreateGlassFlowPanel("GlassRelocPanel", canvasTransform);

            var gfRelocProgressText = CreateTMP("GF_RelocProgressText", gRelocPanel.transform,
                "Scanning...", 38, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.75f));

            // Progress bar background
            var gfRelocBarBG = CreatePanel("GF_RelocBarBG", gRelocPanel.transform,
                new Color(0.2f, 0.2f, 0.2f));
            SetRect(gfRelocBarBG, new Vector2(0.2f, 0.42f), new Vector2(0.8f, 0.48f));

            var gfRelocBarFill = new GameObject("GF_RelocProgressBar");
            gfRelocBarFill.transform.SetParent(gfRelocBarBG.transform, false);
            var gfRbRect = gfRelocBarFill.AddComponent<RectTransform>();
            gfRbRect.anchorMin = Vector2.zero;
            gfRbRect.anchorMax = Vector2.one;
            gfRbRect.offsetMin = Vector2.zero;
            gfRbRect.offsetMax = Vector2.zero;
            var gfRbImg = gfRelocBarFill.AddComponent<Image>();
            gfRbImg.color = new Color(0.2f, 0.6f, 1f);
            gfRbImg.type = Image.Type.Filled;
            gfRbImg.fillMethod = Image.FillMethod.Horizontal;
            gfRbImg.fillAmount = 0f;

            var gfRelocStatusText = CreateTMP("GF_RelocStatusText", gRelocPanel.transform,
                "", 32, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.4f));
            gfRelocStatusText.color = new Color(0.7f, 0.7f, 0.7f);

            var gfRelocProceedBtn = CreateButton("GF_RelocProceedBtn", gRelocPanel.transform,
                "Proceed", new Color(0.2f, 0.55f, 0.25f, 1f), 34);
            SetRect(gfRelocProceedBtn.gameObject, new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.25f));
            gfRelocProceedBtn.gameObject.SetActive(false);

            gRelocPanel.SetActive(false);

            // --- GlassSetupPanel ---
            var gSetupPanel = CreateGlassFlowPanel("GlassSetupPanel", canvasTransform);
            CreateGlassFlowContent(gSetupPanel, "GF_SetupTitle", "Experiment Setup",
                "GF_SetupDetail", "", "GF_SetupContinueBtn", "Continue");
            gSetupPanel.SetActive(false);

            // --- GlassRunningStartPanel ---
            var gRunningPanel = CreateGlassFlowPanel("GlassRunningStartPanel", canvasTransform);
            CreateGlassFlowContent(gRunningPanel, "GF_RunningTitle", "Start Navigation",
                "GF_RunningDetail", "", "GF_RunningStartBtn", "Start");
            gRunningPanel.SetActive(false);

            // --- GlassSurveyPanel ---
            var gSurveyPanel = CreateGlassFlowPanel("GlassSurveyPanel", canvasTransform);
            CreateGlassFlowContent(gSurveyPanel, "GF_SurveyTitle", "Survey Time",
                "GF_SurveyInstr", "", "GF_SurveyDoneBtn", "Done");
            gSurveyPanel.SetActive(false);

            // --- GlassCompletionPanel ---
            var gCompletionPanel = CreateGlassFlowPanel("GlassCompletionPanel", canvasTransform);

            var gfCompTitle = CreateTMP("GF_CompletionTitle", gCompletionPanel.transform,
                "Complete!", 48, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.6f), new Vector2(0.9f, 0.8f));

            var gfCompDetail = CreateTMP("GF_CompletionDetail", gCompletionPanel.transform,
                "Experiment complete!\nThank you.", 38, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.55f));

            gCompletionPanel.SetActive(false);

            Debug.Log("[FlowUISetup] GlassFlowUI 패널 생성 완료 (ExperimentCanvas)");
        }

        /// <summary>
        /// 글래스 플로우 패널 공통 생성 (반투명 배경 + PanelFader + anchorMin/Max 설정)
        /// </summary>
        private static GameObject CreateGlassFlowPanel(string name, Transform parent)
        {
            var panel = CreatePanel(name, parent, new Color(0.08f, 0.08f, 0.12f, 0.92f));
            SetRect(panel, new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.88f));
            panel.AddComponent<PanelFader>();
            return panel;
        }

        /// <summary>
        /// 글래스 플로우 패널 공통 콘텐츠 (타이틀 + 상세 + 버튼)
        /// </summary>
        private static void CreateGlassFlowContent(GameObject panel,
            string titleName, string titleText,
            string detailName, string detailText,
            string btnName, string btnLabel)
        {
            var title = CreateTMP(titleName, panel.transform, titleText,
                48, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.7f), new Vector2(0.9f, 0.88f));

            var detail = CreateTMP(detailName, panel.transform, detailText,
                38, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.65f));
            detail.enableWordWrapping = true;

            var btn = CreateButton(btnName, panel.transform, btnLabel,
                new Color(0.2f, 0.55f, 0.25f, 1f), 34);
            SetRect(btn.gameObject, new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.25f));
        }

        // === Wiring Methods ===

        private static void WireFlowUI(ExperimentFlowUI flowUI,
            GameObject modeSelector, GameObject relocPanel, RelocalizationUI relocUI,
            GameObject transition,
            GameObject survey, GameObject completion,
            TextMeshProUGUI transTitle, TextMeshProUGUI transDetail, Button transContinue,
            TextMeshProUGUI surveyTitle, TextMeshProUGUI surveyInstr, Button surveyDone,
            TextMeshProUGUI completeText)
        {
            var so = new SerializedObject(flowUI);
            so.FindProperty("appModeSelectorPanel").objectReferenceValue = modeSelector;
            so.FindProperty("relocalizationPanel").objectReferenceValue = relocPanel;
            so.FindProperty("relocalizationUI").objectReferenceValue = relocUI;
            so.FindProperty("conditionTransitionPanel").objectReferenceValue = transition;
            so.FindProperty("surveyPromptPanel").objectReferenceValue = survey;
            so.FindProperty("completionPanel").objectReferenceValue = completion;

            so.FindProperty("transitionTitleText").objectReferenceValue = transTitle;
            so.FindProperty("transitionDetailText").objectReferenceValue = transDetail;
            so.FindProperty("transitionContinueButton").objectReferenceValue = transContinue;

            so.FindProperty("surveyTitleText").objectReferenceValue = surveyTitle;
            so.FindProperty("surveyInstructionText").objectReferenceValue = surveyInstr;
            so.FindProperty("surveyDoneButton").objectReferenceValue = surveyDone;

            so.FindProperty("completionText").objectReferenceValue = completeText;
            so.ApplyModifiedProperties();
        }

        private static void WireMappingModeUI(MappingModeUI ui, GameObject panel,
            TextMeshProUGUI title, TextMeshProUGUI qualityText,
            Image qualityBar, Button createAnchorBtn, Button mapZoomBtn,
            Button saveAllBtn, Button backBtn,
            GameObject routeDropdown, Transform waypointListContent)
        {
            var so = new SerializedObject(ui);
            so.FindProperty("mappingPanel").objectReferenceValue = panel;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("qualityText").objectReferenceValue = qualityText;
            so.FindProperty("qualityBar").objectReferenceValue = qualityBar;
            so.FindProperty("createAnchorButton").objectReferenceValue = createAnchorBtn;
            so.FindProperty("mapZoomButton").objectReferenceValue = mapZoomBtn;
            so.FindProperty("saveAllButton").objectReferenceValue = saveAllBtn;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.FindProperty("routeDropdown").objectReferenceValue =
                routeDropdown.GetComponent<TMP_Dropdown>();
            so.FindProperty("waypointListContent").objectReferenceValue = waypointListContent;
            so.ApplyModifiedProperties();
        }

        private static void WireRelocalizationUI(RelocalizationUI ui, GameObject panel,
            TextMeshProUGUI instructionText, TextMeshProUGUI progressText,
            Image progressBar, TextMeshProUGUI statusText,
            TextMeshProUGUI successCountText, TextMeshProUGUI failedCountText,
            Image successBar, GameObject resultPanel,
            Button proceedButton, Button retryButton, TextMeshProUGUI warningText)
        {
            var so = new SerializedObject(ui);
            so.FindProperty("relocalizationPanel").objectReferenceValue = panel;
            so.FindProperty("instructionText").objectReferenceValue = instructionText;
            so.FindProperty("progressText").objectReferenceValue = progressText;
            so.FindProperty("progressBar").objectReferenceValue = progressBar;
            so.FindProperty("statusDetailText").objectReferenceValue = statusText;
            so.FindProperty("successCountText").objectReferenceValue = successCountText;
            so.FindProperty("failedCountText").objectReferenceValue = failedCountText;
            so.FindProperty("successBar").objectReferenceValue = successBar;
            so.FindProperty("resultPanel").objectReferenceValue = resultPanel;
            so.FindProperty("proceedButton").objectReferenceValue = proceedButton;
            so.FindProperty("retryButton").objectReferenceValue = retryButton;
            so.FindProperty("warningText").objectReferenceValue = warningText;
            so.ApplyModifiedProperties();
        }

        // === UI Creation Helpers ===

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static TextMeshProUGUI CreateTMP(string name, Transform parent,
            string text, float fontSize, TextAlignmentOptions align,
            Vector2? anchorMin = null, Vector2? anchorMax = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            if (anchorMin.HasValue && anchorMax.HasValue)
            {
                rect.anchorMin = anchorMin.Value;
                rect.anchorMax = anchorMax.Value;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = align;
            return tmp;
        }

        private static void SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null) rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Button CreateButton(string name, Transform parent, string label,
            Color bgColor, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        private static GameObject CreateInputField(string name, Transform parent, string placeholder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            var textAreaGO = new GameObject("Text Area");
            textAreaGO.transform.SetParent(go.transform, false);
            var taRect = textAreaGO.AddComponent<RectTransform>();
            taRect.anchorMin = Vector2.zero;
            taRect.anchorMax = Vector2.one;
            taRect.offsetMin = new Vector2(10, 0);
            taRect.offsetMax = new Vector2(-10, 0);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textAreaGO.transform, false);
            var tRect = textGO.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;
            var textComp = textGO.AddComponent<TextMeshProUGUI>();
            textComp.fontSize = 16;
            textComp.color = Color.white;

            var phGO = new GameObject("Placeholder");
            phGO.transform.SetParent(textAreaGO.transform, false);
            var phRect = phGO.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = Vector2.zero;
            phRect.offsetMax = Vector2.zero;
            var phText = phGO.AddComponent<TextMeshProUGUI>();
            phText.text = placeholder;
            phText.fontSize = 16;
            phText.color = new Color(0.5f, 0.5f, 0.5f);
            phText.fontStyle = FontStyles.Italic;

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = taRect;
            input.textComponent = textComp;
            input.placeholder = phText;
            input.text = placeholder;

            return go;
        }

        private static GameObject CreateDropdown(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 0);
            labelRect.offsetMax = new Vector2(-30, 0);
            var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.fontSize = 14;
            labelTMP.color = Color.white;

            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(go.transform, false);
            var arrowRect = arrowGO.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.sizeDelta = new Vector2(20, 20);
            arrowRect.anchoredPosition = new Vector2(-15, 0);
            var arrowImg = arrowGO.AddComponent<Image>();
            arrowImg.color = Color.white;

            var templateGO = new GameObject("Template");
            templateGO.transform.SetParent(go.transform, false);
            var tmplRect = templateGO.AddComponent<RectTransform>();
            tmplRect.anchorMin = new Vector2(0, 0);
            tmplRect.anchorMax = new Vector2(1, 0);
            tmplRect.pivot = new Vector2(0.5f, 1);
            tmplRect.sizeDelta = new Vector2(0, 120);
            var tmplImg = templateGO.AddComponent<Image>();
            tmplImg.color = new Color(0.15f, 0.15f, 0.2f, 1f);
            templateGO.AddComponent<CanvasGroup>();
            var scrollRect = templateGO.AddComponent<ScrollRect>();

            var viewportGO2 = new GameObject("Viewport");
            viewportGO2.transform.SetParent(templateGO.transform, false);
            var vpRect = viewportGO2.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            var vpMask = viewportGO2.AddComponent<Mask>();
            var vpImg = viewportGO2.AddComponent<Image>();
            vpImg.color = Color.white;
            vpMask.showMaskGraphic = false;

            var contentGO2 = new GameObject("Content");
            contentGO2.transform.SetParent(viewportGO2.transform, false);
            var contentRect2 = contentGO2.AddComponent<RectTransform>();
            contentRect2.anchorMin = new Vector2(0, 1);
            contentRect2.anchorMax = new Vector2(1, 1);
            contentRect2.pivot = new Vector2(0.5f, 1);
            contentRect2.sizeDelta = new Vector2(0, 28);

            scrollRect.viewport = vpRect;
            scrollRect.content = contentRect2;

            var itemGO = new GameObject("Item");
            itemGO.transform.SetParent(contentGO2.transform, false);
            var itemRect = itemGO.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 28);
            var itemToggle = itemGO.AddComponent<Toggle>();

            var itemLabelGO = new GameObject("Item Label");
            itemLabelGO.transform.SetParent(itemGO.transform, false);
            var ilRect = itemLabelGO.AddComponent<RectTransform>();
            ilRect.anchorMin = Vector2.zero;
            ilRect.anchorMax = Vector2.one;
            ilRect.offsetMin = new Vector2(10, 0);
            ilRect.offsetMax = Vector2.zero;
            var ilTMP = itemLabelGO.AddComponent<TextMeshProUGUI>();
            ilTMP.fontSize = 14;
            ilTMP.color = Color.white;

            var dropdown = go.AddComponent<TMP_Dropdown>();
            dropdown.captionText = labelTMP;
            dropdown.itemText = ilTMP;
            dropdown.template = tmplRect;

            templateGO.SetActive(false);

            return go;
        }
    }
}

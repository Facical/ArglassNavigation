using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.UI;

namespace ARNavExperiment.EditorTools
{
    public class FlowUISetupTool
    {
        [MenuItem("ARNav/Cleanup Flow UI (중복 제거)")]
        public static void CleanupFlowUI()
        {
            string[] targets = {
                "AppModeSelectorPanel", "MappingModePanel", "RelocalizationPanel",
                "SessionSetupPanel", "PracticePanel", "ConditionTransitionPanel",
                "SurveyPromptPanel", "CompletionPanel", "ExperimentFlowUI"
            };

            int removed = 0;
            foreach (var name in targets)
            {
                // 같은 이름의 오브젝트를 모두 찾아서 전부 삭제
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
            // 플로우 패널(실험자 제어)은 ExperimenterCanvas의 FlowPanelArea에 배치
            var experimenterCanvas = GameObject.Find("ExperimenterCanvas");
            if (experimenterCanvas == null)
            {
                Debug.LogError("[FlowUISetup] ExperimenterCanvas를 찾을 수 없습니다. 먼저 Setup Main Scene을 실행하세요.");
                return;
            }

            // FlowPanelArea가 있으면 그 안에, 없으면 Canvas 직접 자식으로 배치
            var flowPanelArea = experimenterCanvas.transform.Find("FlowPanelArea");
            var canvasTransform = flowPanelArea != null ? flowPanelArea : experimenterCanvas.transform;

            // === App Mode Selector Panel ===
            var modeSelectorPanel = CreatePanel("AppModeSelectorPanel", canvasTransform,
                new Color(0.05f, 0.05f, 0.1f, 0.98f));
            var modeSelectorUI = modeSelectorPanel.AddComponent<AppModeSelector>();

            // Mode Selector Title
            var modeTitleText = CreateTMP("ModeTitle", modeSelectorPanel.transform,
                "AR 내비게이션 실험", 36, TextAlignmentOptions.Center);
            SetRect(modeTitleText.gameObject, new Vector2(0.1f, 0.75f), new Vector2(0.9f, 0.9f));

            // Mode Selector Subtitle
            var modeSubtitle = CreateTMP("ModeSubtitle", modeSelectorPanel.transform,
                "모드를 선택하세요", 20, TextAlignmentOptions.Center);
            SetRect(modeSubtitle.gameObject, new Vector2(0.1f, 0.67f), new Vector2(0.9f, 0.75f));
            modeSubtitle.color = new Color(0.7f, 0.7f, 0.7f);

            // Mapping Mode Button
            var mappingBtn = CreateButton("MappingModeBtn", modeSelectorPanel.transform,
                "매핑 모드 (사전 준비)", new Color(0.2f, 0.4f, 0.6f, 1f), 22);
            SetRect(mappingBtn.gameObject, new Vector2(0.15f, 0.45f), new Vector2(0.85f, 0.6f));

            // Experiment Mode Button
            var expBtn = CreateButton("ExperimentModeBtn", modeSelectorPanel.transform,
                "실험 모드 (참가자 진행)", new Color(0.2f, 0.5f, 0.2f, 1f), 22);
            SetRect(expBtn.gameObject, new Vector2(0.15f, 0.25f), new Vector2(0.85f, 0.4f));

            // Mapping Status Text
            var mappingStatusText = CreateTMP("MappingStatusText", modeSelectorPanel.transform,
                "매핑 상태: 확인 중...", 14, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.2f));
            mappingStatusText.color = new Color(0.7f, 0.7f, 0.7f);

            // === Mapping Mode Panel ===
            var mappingPanel = CreatePanel("MappingModePanel", canvasTransform,
                new Color(0.05f, 0.05f, 0.1f, 0.98f));
            var mappingUI = mappingPanel.AddComponent<MappingModeUI>();
            mappingPanel.SetActive(false);

            // Mapping Title
            var mappingTitle = CreateTMP("MappingTitle", mappingPanel.transform,
                "매핑 모드", 24, TextAlignmentOptions.Center);
            SetRect(mappingTitle.gameObject, new Vector2(0.2f, 0.92f), new Vector2(0.8f, 0.98f));

            // Quality Text
            var qualityText = CreateTMP("QualityText", mappingPanel.transform,
                "매핑 품질: 확인 중...", 15, TextAlignmentOptions.Left,
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

            // Route Dropdown (오른쪽 상단)
            var routeDropdown = CreateDropdown("RouteDropdown", mappingPanel.transform);
            SetRect(routeDropdown, new Vector2(0.76f, 0.84f), new Vector2(0.97f, 0.91f));

            // Waypoint List ScrollView
            var waypointListGO = new GameObject("WaypointList");
            waypointListGO.transform.SetParent(mappingPanel.transform, false);
            waypointListGO.AddComponent<RectTransform>();
            SetRect(waypointListGO, new Vector2(0.03f, 0.19f), new Vector2(0.97f, 0.82f));
            var wlImg = waypointListGO.AddComponent<Image>();
            wlImg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

            // Viewport (Mask로 스크롤 영역 클리핑)
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

            // Create Anchor Button
            var createAnchorBtn = CreateButton("CreateAnchorBtn", mappingPanel.transform,
                "웨이포인트를 선택하세요", new Color(0.3f, 0.5f, 0.7f, 1f), 16);
            SetRect(createAnchorBtn.gameObject, new Vector2(0.03f, 0.04f), new Vector2(0.48f, 0.15f));

            // Save All Button
            var saveAllBtn = CreateButton("SaveAllBtn", mappingPanel.transform,
                "모든 앵커 저장", new Color(0.2f, 0.5f, 0.2f, 1f), 16);
            SetRect(saveAllBtn.gameObject, new Vector2(0.5f, 0.04f), new Vector2(0.76f, 0.15f));

            // Back Button
            var backBtn = CreateButton("BackBtn", mappingPanel.transform,
                "돌아가기", new Color(0.4f, 0.4f, 0.4f, 1f), 16);
            SetRect(backBtn.gameObject, new Vector2(0.78f, 0.04f), new Vector2(0.97f, 0.15f));

            // Wire MappingModeUI
            WireMappingModeUI(mappingUI, mappingPanel, mappingTitle, qualityText,
                qbImg, createAnchorBtn, saveAllBtn, backBtn, routeDropdown, contentGO.transform);

            // === Relocalization Panel ===
            var relocPanel = CreatePanel("RelocalizationPanel", canvasTransform,
                new Color(0.05f, 0.05f, 0.1f, 0.98f));
            var relocUI = relocPanel.AddComponent<RelocalizationUI>();
            relocPanel.SetActive(false);

            // Relocalization Instruction
            var relocInstr = CreateTMP("RelocInstructionText", relocPanel.transform,
                "환경을 인식하고 있습니다...\n주변을 천천히 둘러봐주세요.", 24, TextAlignmentOptions.Center);
            SetRect(relocInstr.gameObject, new Vector2(0.1f, 0.6f), new Vector2(0.9f, 0.8f));

            // Relocalization Progress Text
            var relocProgress = CreateTMP("RelocProgressText", relocPanel.transform,
                "준비 중...", 18, TextAlignmentOptions.Center);
            SetRect(relocProgress.gameObject, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.58f));

            // Relocalization Progress Bar Background
            var relocBarBG = CreatePanel("RelocBarBG", relocPanel.transform,
                new Color(0.2f, 0.2f, 0.2f));
            SetRect(relocBarBG, new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.45f));

            // Relocalization Progress Bar Fill
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

            // Relocalization Status
            var relocStatus = CreateTMP("RelocStatusText", relocPanel.transform,
                "", 14, TextAlignmentOptions.Center);
            SetRect(relocStatus.gameObject, new Vector2(0.2f, 0.32f), new Vector2(0.8f, 0.38f));
            relocStatus.color = new Color(0.7f, 0.7f, 0.7f);

            // Success Bar Background (녹색 프로그레스 바)
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

            // Success/Failed Count Texts
            var relocSuccessText = CreateTMP("RelocSuccessText", relocPanel.transform,
                "", 14, TextAlignmentOptions.Left,
                new Vector2(0.2f, 0.19f), new Vector2(0.5f, 0.25f));
            relocSuccessText.color = new Color(0.3f, 0.9f, 0.3f);

            var relocFailedText = CreateTMP("RelocFailedText", relocPanel.transform,
                "", 14, TextAlignmentOptions.Right,
                new Vector2(0.5f, 0.19f), new Vector2(0.8f, 0.25f));
            relocFailedText.color = new Color(1f, 0.4f, 0.4f);

            // Result Panel (기본 비활성)
            var resultPanel = CreatePanel("ResultPanel", relocPanel.transform,
                new Color(0.1f, 0.1f, 0.15f, 0.95f));
            SetRect(resultPanel, new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.18f));

            var relocWarningText = CreateTMP("WarningText", resultPanel.transform,
                "", 11, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.95f));
            relocWarningText.color = new Color(1f, 0.8f, 0.4f);

            var proceedBtn = CreateButton("ProceedBtn", resultPanel.transform,
                "계속 진행", new Color(0.2f, 0.5f, 0.2f, 1f), 14);
            SetRect(proceedBtn.gameObject, new Vector2(0.05f, 0.05f), new Vector2(0.48f, 0.32f));

            var retryBtn = CreateButton("RetryBtn", resultPanel.transform,
                "재시도", new Color(0.2f, 0.4f, 0.6f, 1f), 14);
            SetRect(retryBtn.gameObject, new Vector2(0.52f, 0.05f), new Vector2(0.95f, 0.32f));

            resultPanel.SetActive(false);

            // Wire RelocalizationUI
            WireRelocalizationUI(relocUI, relocPanel, relocInstr, relocProgress, rbImg, relocStatus,
                relocSuccessText, relocFailedText, sbImg, resultPanel, proceedBtn, retryBtn, relocWarningText);

            // === Session Setup Panel ===
            var setupPanel = CreatePanel("SessionSetupPanel", canvasTransform,
                new Color(0.08f, 0.08f, 0.12f, 0.98f));
            var setupUI = setupPanel.AddComponent<SessionSetupUI>();
            setupPanel.SetActive(false);

            // Title
            var titleText = CreateTMP("TitleText", setupPanel.transform,
                "AR 내비게이션 실험", 32, TextAlignmentOptions.Center);
            SetRect(titleText.gameObject, new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.92f));

            // Subtitle
            var subtitleText = CreateTMP("SubtitleText", setupPanel.transform,
                "실험자 설정 화면", 18, TextAlignmentOptions.Center);
            SetRect(subtitleText.gameObject, new Vector2(0.1f, 0.73f), new Vector2(0.9f, 0.8f));
            subtitleText.color = new Color(0.7f, 0.7f, 0.7f);

            // Participant ID Label
            CreateTMP("PIDLabel", setupPanel.transform,
                "참가자 ID:", 16, TextAlignmentOptions.Left,
                new Vector2(0.2f, 0.6f), new Vector2(0.45f, 0.67f));

            // Participant ID Input
            var pidInputGO = CreateInputField("ParticipantIdInput", setupPanel.transform, "P01");
            SetRect(pidInputGO, new Vector2(0.45f, 0.6f), new Vector2(0.8f, 0.67f));

            // Order Group Label
            CreateTMP("GroupLabel", setupPanel.transform,
                "순서 그룹:", 16, TextAlignmentOptions.Left,
                new Vector2(0.2f, 0.5f), new Vector2(0.45f, 0.57f));

            // Order Group Dropdown
            var dropdownGO = CreateDropdown("OrderGroupDropdown", setupPanel.transform);
            SetRect(dropdownGO, new Vector2(0.45f, 0.5f), new Vector2(0.8f, 0.57f));

            // Preview Text
            var previewText = CreateTMP("PreviewText", setupPanel.transform,
                "참가자: P01\n그룹: S1\n1차: Glass Only + Route A\n2차: Hybrid + Route B",
                14, TextAlignmentOptions.Left,
                new Vector2(0.2f, 0.28f), new Vector2(0.8f, 0.47f));
            previewText.color = new Color(0.6f, 0.8f, 1f);

            // Error Text
            var errorText = CreateTMP("ErrorText", setupPanel.transform,
                "", 14, TextAlignmentOptions.Center,
                new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.27f));
            errorText.color = new Color(1f, 0.4f, 0.4f);
            errorText.gameObject.SetActive(false);

            // Start Button
            var startBtn = CreateButton("StartButton", setupPanel.transform, "실험 시작",
                new Color(0.2f, 0.5f, 0.2f, 1f), 22);
            SetRect(startBtn.gameObject, new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.18f));

            // Wire SessionSetupUI references
            WireSessionSetup(setupUI, setupPanel);

            // === Practice Panel ===
            var practicePanel = CreatePanel("PracticePanel", canvasTransform,
                new Color(0.08f, 0.08f, 0.12f, 0.98f));
            practicePanel.SetActive(false);

            var practiceTitle = CreateTMP("PracticeTitle", practicePanel.transform,
                "연습 모드", 28, TextAlignmentOptions.Center);
            SetRect(practiceTitle.gameObject, new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.92f));

            var practiceInstr = CreateTMP("PracticeInstruction", practicePanel.transform,
                "", 16, TextAlignmentOptions.Left,
                new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.78f));

            var practiceStartBtn = CreateButton("PracticeStartBtn", practicePanel.transform,
                "연습 시작", new Color(0.2f, 0.4f, 0.6f, 1f), 18);
            SetRect(practiceStartBtn.gameObject, new Vector2(0.15f, 0.08f), new Vector2(0.48f, 0.18f));

            var practiceSkipBtn = CreateButton("PracticeSkipBtn", practicePanel.transform,
                "건너뛰기", new Color(0.4f, 0.4f, 0.4f, 1f), 18);
            SetRect(practiceSkipBtn.gameObject, new Vector2(0.52f, 0.08f), new Vector2(0.85f, 0.18f));

            // === Condition Transition Panel ===
            var transPanel = CreatePanel("ConditionTransitionPanel", canvasTransform,
                new Color(0.08f, 0.08f, 0.12f, 0.98f));
            transPanel.SetActive(false);

            var transTitle = CreateTMP("TransitionTitle", transPanel.transform,
                "조건 시작", 28, TextAlignmentOptions.Center);
            SetRect(transTitle.gameObject, new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.92f));

            var transDetail = CreateTMP("TransitionDetail", transPanel.transform,
                "", 16, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.75f));

            var transContinueBtn = CreateButton("TransitionContinueBtn", transPanel.transform,
                "계속", new Color(0.2f, 0.5f, 0.2f, 1f), 20);
            SetRect(transContinueBtn.gameObject, new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.2f));

            // === Survey Prompt Panel ===
            var surveyPanel = CreatePanel("SurveyPromptPanel", canvasTransform,
                new Color(0.08f, 0.08f, 0.12f, 0.98f));
            surveyPanel.SetActive(false);

            var surveyTitle = CreateTMP("SurveyTitle", surveyPanel.transform,
                "설문", 28, TextAlignmentOptions.Center);
            SetRect(surveyTitle.gameObject, new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.92f));

            var surveyInstr = CreateTMP("SurveyInstruction", surveyPanel.transform,
                "", 16, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.75f));

            var surveyDoneBtn = CreateButton("SurveyDoneBtn", surveyPanel.transform,
                "설문 완료", new Color(0.2f, 0.5f, 0.2f, 1f), 20);
            SetRect(surveyDoneBtn.gameObject, new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.2f));

            // === Completion Panel ===
            var completePanel = CreatePanel("CompletionPanel", canvasTransform,
                new Color(0.08f, 0.08f, 0.12f, 0.98f));
            completePanel.SetActive(false);

            var completeText = CreateTMP("CompletionText", completePanel.transform,
                "", 20, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.75f));

            // === ExperimentFlowUI Component (비시각적 컨트롤러 — Canvas 직접 자식) ===
            var flowUIGO = new GameObject("ExperimentFlowUI");
            Undo.RegisterCreatedObjectUndo(flowUIGO, "Create ExperimentFlowUI");
            flowUIGO.transform.SetParent(experimenterCanvas.transform, false);
            var flowUI = flowUIGO.AddComponent<ExperimentFlowUI>();

            // Wire ExperimentFlowUI references
            WireFlowUI(flowUI,
                modeSelectorPanel, relocPanel, relocUI,
                setupPanel, practicePanel, transPanel, surveyPanel, completePanel,
                practiceInstr, practiceStartBtn, practiceSkipBtn,
                transTitle, transDetail, transContinueBtn,
                surveyTitle, surveyInstr, surveyDoneBtn,
                completeText);

            Debug.Log("[FlowUISetup] 실험 플로우 UI 구성 완료!");
        }

        // === Wiring Methods ===

        private static void WireSessionSetup(SessionSetupUI ui, GameObject panel)
        {
            var so = new SerializedObject(ui);
            so.FindProperty("participantIdInput").objectReferenceValue =
                panel.transform.Find("ParticipantIdInput")?.GetComponent<TMP_InputField>();
            so.FindProperty("orderGroupDropdown").objectReferenceValue =
                panel.transform.Find("OrderGroupDropdown")?.GetComponent<TMP_Dropdown>();
            so.FindProperty("previewText").objectReferenceValue =
                panel.transform.Find("PreviewText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("errorText").objectReferenceValue =
                panel.transform.Find("ErrorText")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("startButton").objectReferenceValue =
                panel.transform.Find("StartButton")?.GetComponent<Button>();
            so.ApplyModifiedProperties();
        }

        private static void WireFlowUI(ExperimentFlowUI flowUI,
            GameObject modeSelector, GameObject relocPanel, RelocalizationUI relocUI,
            GameObject setup, GameObject practice, GameObject transition,
            GameObject survey, GameObject completion,
            TextMeshProUGUI practiceInstr, Button practiceStart, Button practiceSkip,
            TextMeshProUGUI transTitle, TextMeshProUGUI transDetail, Button transContinue,
            TextMeshProUGUI surveyTitle, TextMeshProUGUI surveyInstr, Button surveyDone,
            TextMeshProUGUI completeText)
        {
            var so = new SerializedObject(flowUI);
            so.FindProperty("appModeSelectorPanel").objectReferenceValue = modeSelector;
            so.FindProperty("relocalizationPanel").objectReferenceValue = relocPanel;
            so.FindProperty("relocalizationUI").objectReferenceValue = relocUI;
            so.FindProperty("sessionSetupPanel").objectReferenceValue = setup;
            so.FindProperty("practicePanel").objectReferenceValue = practice;
            so.FindProperty("conditionTransitionPanel").objectReferenceValue = transition;
            so.FindProperty("surveyPromptPanel").objectReferenceValue = survey;
            so.FindProperty("completionPanel").objectReferenceValue = completion;

            so.FindProperty("practiceInstructionText").objectReferenceValue = practiceInstr;
            so.FindProperty("practiceStartButton").objectReferenceValue = practiceStart;
            so.FindProperty("practiceSkipButton").objectReferenceValue = practiceSkip;

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
            Image qualityBar, Button createAnchorBtn, Button saveAllBtn, Button backBtn,
            GameObject routeDropdown, Transform waypointListContent)
        {
            var so = new SerializedObject(ui);
            so.FindProperty("mappingPanel").objectReferenceValue = panel;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("qualityText").objectReferenceValue = qualityText;
            so.FindProperty("qualityBar").objectReferenceValue = qualityBar;
            so.FindProperty("createAnchorButton").objectReferenceValue = createAnchorBtn;
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

            // Button label
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

            // Text Area
            var textAreaGO = new GameObject("Text Area");
            textAreaGO.transform.SetParent(go.transform, false);
            var taRect = textAreaGO.AddComponent<RectTransform>();
            taRect.anchorMin = Vector2.zero;
            taRect.anchorMax = Vector2.one;
            taRect.offsetMin = new Vector2(10, 0);
            taRect.offsetMax = new Vector2(-10, 0);

            // Text
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

            // Placeholder
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

            // Input Field component
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

            // Label
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

            // Arrow
            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(go.transform, false);
            var arrowRect = arrowGO.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.sizeDelta = new Vector2(20, 20);
            arrowRect.anchoredPosition = new Vector2(-15, 0);
            var arrowImg = arrowGO.AddComponent<Image>();
            arrowImg.color = Color.white;

            // Template
            var templateGO = new GameObject("Template");
            templateGO.transform.SetParent(go.transform, false);
            var tmplRect = templateGO.AddComponent<RectTransform>();
            tmplRect.anchorMin = new Vector2(0, 0);
            tmplRect.anchorMax = new Vector2(1, 0);
            tmplRect.pivot = new Vector2(0.5f, 1);
            tmplRect.sizeDelta = new Vector2(0, 120);
            var tmplImg = templateGO.AddComponent<Image>();
            tmplImg.color = new Color(0.15f, 0.15f, 0.2f, 1f);
            templateGO.AddComponent<CanvasGroup>(); // TMP_Dropdown AlphaFade에 필수
            var scrollRect = templateGO.AddComponent<ScrollRect>();

            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(templateGO.transform, false);
            var vpRect = viewportGO.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            var vpMask = viewportGO.AddComponent<Mask>();
            var vpImg = viewportGO.AddComponent<Image>();
            vpImg.color = Color.white;
            vpMask.showMaskGraphic = false;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 28);

            scrollRect.viewport = vpRect;
            scrollRect.content = contentRect;

            // Item
            var itemGO = new GameObject("Item");
            itemGO.transform.SetParent(contentGO.transform, false);
            var itemRect = itemGO.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 28);
            var itemToggle = itemGO.AddComponent<Toggle>();

            // Item Label
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

            // Dropdown component
            var dropdown = go.AddComponent<TMP_Dropdown>();
            dropdown.captionText = labelTMP;
            dropdown.itemText = ilTMP;
            dropdown.template = tmplRect;

            templateGO.SetActive(false);

            return go;
        }
    }
}

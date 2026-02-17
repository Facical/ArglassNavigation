using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.UI;

namespace ARNavExperiment.EditorTools
{
    public class FlowUISetupTool
    {
        [MenuItem("ARNav/Setup Experiment Flow UI")]
        public static void SetupFlowUI()
        {
            // Find or create ExperimentCanvas
            var expCanvas = GameObject.Find("ExperimentCanvas");
            if (expCanvas == null)
            {
                EditorUtility.DisplayDialog("에러",
                    "ExperimentCanvas를 찾을 수 없습니다.\n먼저 Setup Main Scene을 실행하세요.", "확인");
                return;
            }

            var canvasTransform = expCanvas.transform;

            // === Session Setup Panel ===
            var setupPanel = CreatePanel("SessionSetupPanel", canvasTransform,
                new Color(0.08f, 0.08f, 0.12f, 0.98f));
            var setupUI = setupPanel.AddComponent<SessionSetupUI>();

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

            // === ExperimentFlowUI Component ===
            var flowUIGO = new GameObject("ExperimentFlowUI");
            Undo.RegisterCreatedObjectUndo(flowUIGO, "Create ExperimentFlowUI");
            flowUIGO.transform.SetParent(canvasTransform, false);
            var flowUI = flowUIGO.AddComponent<ExperimentFlowUI>();

            // Wire ExperimentFlowUI references
            WireFlowUI(flowUI, setupPanel, practicePanel, transPanel, surveyPanel, completePanel,
                practiceInstr, practiceStartBtn, practiceSkipBtn,
                transTitle, transDetail, transContinueBtn,
                surveyTitle, surveyInstr, surveyDoneBtn,
                completeText);

            EditorUtility.DisplayDialog("완료",
                "실험 플로우 UI 구성 완료!\n\n" +
                "추가된 패널:\n" +
                "- SessionSetupPanel (참가자 설정)\n" +
                "- PracticePanel (연습 안내)\n" +
                "- ConditionTransitionPanel (조건 전환)\n" +
                "- SurveyPromptPanel (설문 안내)\n" +
                "- CompletionPanel (실험 완료)\n\n" +
                "씬을 저장하세요 (Cmd+S)", "확인");
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
            GameObject setup, GameObject practice, GameObject transition,
            GameObject survey, GameObject completion,
            TextMeshProUGUI practiceInstr, Button practiceStart, Button practiceSkip,
            TextMeshProUGUI transTitle, TextMeshProUGUI transDetail, Button transContinue,
            TextMeshProUGUI surveyTitle, TextMeshProUGUI surveyInstr, Button surveyDone,
            TextMeshProUGUI completeText)
        {
            var so = new SerializedObject(flowUI);
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

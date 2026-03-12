using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace ARNavExperiment.EditorTools
{
    /// <summary>
    /// ExperimenterCanvas(Beam Pro 실험자 제어 패널) 생성 에디터 도구.
    /// ScreenSpaceOverlay sortOrder=10으로 최상위 표시.
    /// </summary>
    public class ExperimenterCanvasSetupTool
    {
        [MenuItem("ARNav/Setup Experimenter Canvas")]
        public static void SetupExperimenterCanvas()
        {
            SetupExperimenterCanvasSilent();
            EditorUtility.DisplayDialog("완료",
                "ExperimenterCanvas 구성 완료!\n\n" +
                "구성:\n" +
                "- ExperimenterHUD (상태 표시 + 제어 버튼)\n" +
                "- Advance Button (상태 전환)\n" +
                "- Next Mission Button (다음 미션)\n\n" +
                "씬을 저장하세요 (Cmd+S)", "확인");
        }

        public static void SetupExperimenterCanvasSilent()
        {
            // 기존 ExperimenterCanvas 삭제
            GameObject existing;
            while ((existing = GameObject.Find("ExperimenterCanvas")) != null)
                Undo.DestroyObjectImmediate(existing);

            // ExperimenterCanvas 생성 (sortOrder=10, 최상위)
            var canvasGO = new GameObject("ExperimenterCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create ExperimenterCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1200, 540);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            canvasGO.AddComponent<Presentation.Shared.BeamProUIAdapter>();

            // ExperimenterHUD 패널 (하단 12%)
            var hudPanel = CreatePanel("ExperimenterHUD", canvasGO.transform,
                new Color(0.05f, 0.05f, 0.1f, 0.85f));
            hudPanel.AddComponent<Presentation.Experimenter.ExperimenterHUD>();
            var hudRect = hudPanel.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0, 0);
            hudRect.anchorMax = new Vector2(1, 0.12f);
            hudRect.offsetMin = new Vector2(10, 10);
            hudRect.offsetMax = new Vector2(-10, 0);

            // Status area (left side)
            var statusArea = new GameObject("StatusArea");
            statusArea.transform.SetParent(hudPanel.transform, false);
            var statusRect = statusArea.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 0);
            statusRect.anchorMax = new Vector2(0.65f, 1);
            statusRect.offsetMin = new Vector2(10, 5);
            statusRect.offsetMax = new Vector2(0, -5);
            var statusLayout = statusArea.AddComponent<HorizontalLayoutGroup>();
            statusLayout.spacing = 20;
            statusLayout.childForceExpandWidth = true;
            statusLayout.childForceExpandHeight = true;
            statusLayout.childControlWidth = true;
            statusLayout.childControlHeight = true;
            statusLayout.padding = new RectOffset(5, 5, 5, 5);

            CreateStatusText("StateText", statusArea.transform, "State: Idle");
            CreateStatusText("ConditionText", statusArea.transform, "Condition: \u2014");
            CreateStatusText("MissionText", statusArea.transform, "Mission: \u2014");
            CreateStatusText("WPText", statusArea.transform, "WP: \u2014");

            // Button area (right side)
            var btnArea = new GameObject("ButtonArea");
            btnArea.transform.SetParent(hudPanel.transform, false);
            var btnRect = btnArea.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.65f, 0);
            btnRect.anchorMax = new Vector2(1, 1);
            btnRect.offsetMin = new Vector2(10, 5);
            btnRect.offsetMax = new Vector2(-10, -5);
            var btnLayout = btnArea.AddComponent<HorizontalLayoutGroup>();
            btnLayout.spacing = 10;
            btnLayout.childForceExpandWidth = true;
            btnLayout.childForceExpandHeight = true;
            btnLayout.childControlWidth = true;
            btnLayout.childControlHeight = true;
            btnLayout.padding = new RectOffset(5, 5, 4, 4);

            CreateControlButton("AdvanceBtn", btnArea.transform,
                "Advance State", new Color(0.2f, 0.5f, 0.2f, 1f));
            CreateControlButton("NextMissionBtn", btnArea.transform,
                "Next Mission", new Color(0.2f, 0.4f, 0.6f, 1f));
            CreateControlButton("CaptureBtn", btnArea.transform,
                "Capture", new Color(0.5f, 0.3f, 0.1f, 1f));

            // Heading Calibration area (하단 HUD 아래 추가 영역)
            var headingArea = new GameObject("HeadingArea");
            headingArea.transform.SetParent(hudPanel.transform, false);
            var headingRect = headingArea.AddComponent<RectTransform>();
            headingRect.anchorMin = new Vector2(0, 1f);
            headingRect.anchorMax = new Vector2(0.45f, 1f);
            headingRect.pivot = new Vector2(0, 0);
            headingRect.anchoredPosition = new Vector2(10, 2);
            headingRect.sizeDelta = new Vector2(0, 32);
            var headingLayout = headingArea.AddComponent<HorizontalLayoutGroup>();
            headingLayout.spacing = 6;
            headingLayout.childForceExpandWidth = false;
            headingLayout.childForceExpandHeight = true;
            headingLayout.childControlWidth = true;
            headingLayout.childControlHeight = true;
            headingLayout.padding = new RectOffset(2, 2, 2, 2);

            CreateControlButton("HeadingLeftBtn", headingArea.transform,
                "\u2190 5\u00b0", new Color(0.35f, 0.35f, 0.45f, 1f));
            var leftLE = headingArea.transform.Find("HeadingLeftBtn")?.gameObject.AddComponent<LayoutElement>();
            if (leftLE != null) leftLE.preferredWidth = 50;

            CreateControlButton("HeadingRightBtn", headingArea.transform,
                "5\u00b0 \u2192", new Color(0.35f, 0.35f, 0.45f, 1f));
            var rightLE = headingArea.transform.Find("HeadingRightBtn")?.gameObject.AddComponent<LayoutElement>();
            if (rightLE != null) rightLE.preferredWidth = 50;

            CreateControlButton("ManualCalibrateBtn", headingArea.transform,
                "Calibrate", new Color(0.5f, 0.25f, 0.5f, 1f));
            var calLE = headingArea.transform.Find("ManualCalibrateBtn")?.gameObject.AddComponent<LayoutElement>();
            if (calLE != null) calLE.preferredWidth = 70;

            CreateStatusText("HeadingOffsetText", headingArea.transform, "Heading: 0\u00b0");
            var offsetLE = headingArea.transform.Find("HeadingOffsetText")?.gameObject.AddComponent<LayoutElement>();
            if (offsetLE != null) offsetLE.preferredWidth = 140;

            // FlowPanelArea 컨테이너 (HUD 위 전체 영역)
            var flowPanelArea = new GameObject("FlowPanelArea");
            flowPanelArea.transform.SetParent(canvasGO.transform, false);
            var flowRect = flowPanelArea.AddComponent<RectTransform>();
            flowRect.anchorMin = new Vector2(0, 0.12f);
            flowRect.anchorMax = new Vector2(1, 1);
            flowRect.offsetMin = Vector2.zero;
            flowRect.offsetMax = Vector2.zero;

            // === EventSystem 입력 모듈 검증 ===
            ValidateEventSystemInputModule();

            Debug.Log("[ExperimenterCanvasSetup] ExperimenterCanvas 구성 완료!");
        }

        /// <summary>EventSystem에 입력 모듈이 존재하는지 검증</summary>
        private static void ValidateEventSystemInputModule()
        {
            var eventSystem = Object.FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogError(
                    "[ExperimenterCanvasSetup] EventSystem이 씬에 없습니다!\n" +
                    "터치/마우스 입력이 작동하지 않습니다.\n" +
                    "ARNav > Setup XR Origin (XREAL)을 먼저 실행하세요.");
                return;
            }

            var inputModule = eventSystem.GetComponent<BaseInputModule>();
            if (inputModule == null)
            {
                Debug.LogError(
                    "[ExperimenterCanvasSetup] EventSystem에 입력 모듈이 없습니다!\n" +
                    "XRUIInputModule 또는 InputSystemUIInputModule이 필요합니다.\n" +
                    "ARNav > Setup XR Origin (XREAL)을 먼저 실행하세요.");
                return;
            }

#if XR_INTERACTION
            var xrUIModule = eventSystem.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
            if (xrUIModule == null)
            {
                Debug.LogWarning(
                    "[ExperimenterCanvasSetup] EventSystem에 XRUIInputModule이 없습니다.\n" +
                    "핸드트래킹 UI 인터랙션이 작동하지 않을 수 있습니다.");
            }
            else if (!xrUIModule.enableTouchInput)
            {
                Debug.LogWarning(
                    "[ExperimenterCanvasSetup] XRUIInputModule.enableTouchInput이 false입니다.\n" +
                    "Beam Pro 터치 입력이 작동하지 않을 수 있습니다.");
            }
#endif

            Debug.Log("[ExperimenterCanvasSetup] EventSystem 입력 모듈 검증 통과");
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
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

        private static void CreateStatusText(string name, Transform parent, string text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 7;
            tmp.fontSizeMax = 14;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static void CreateControlButton(string name, Transform parent,
            string label, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            go.AddComponent<Button>();

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 11;
            tmp.fontSizeMax = 16;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }
    }
}

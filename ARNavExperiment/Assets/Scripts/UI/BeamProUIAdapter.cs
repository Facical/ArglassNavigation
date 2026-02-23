using UnityEngine;
using UnityEngine.UI;

namespace ARNavExperiment.UI
{
    /// <summary>
    /// Beam Pro 디바이스의 세로/가로 방향 변경을 감지하여
    /// CanvasScaler, HUD, FlowPanelArea, TabBar 레이아웃을 동적으로 조정합니다.
    /// ExperimenterCanvas 또는 BeamProCanvas에 부착하여 사용합니다.
    /// </summary>
    public class BeamProUIAdapter : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private CanvasScaler targetScaler;

        [Header("ExperimenterCanvas - HUD")]
        [SerializeField] private RectTransform hudPanel;
        [SerializeField] private RectTransform statusArea;
        [SerializeField] private RectTransform buttonArea;
        [SerializeField] private HorizontalLayoutGroup statusHLayout;
        [SerializeField] private HorizontalLayoutGroup buttonHLayout;

        [Header("ExperimenterCanvas - Flow")]
        [SerializeField] private RectTransform flowPanelArea;

        [Header("BeamProCanvas - Tab")]
        [SerializeField] private RectTransform tabBar;
        [SerializeField] private RectTransform contentArea;

        private bool isPortrait;
        private float checkTimer;
        private const float CheckInterval = 0.3f;

        // StatusArea의 VerticalLayoutGroup 캐시 (Portrait용, 런타임 생성)
        private VerticalLayoutGroup statusVLayout;

        private void Start()
        {
            // 초기 방향 판정 및 레이아웃 적용
            isPortrait = Screen.height > Screen.width;
            ApplyLayout();
        }

        private void Update()
        {
            // WorldSpace 모드에서는 방향 감지 불필요 (BeamProCanvasController가 비활성화하지만 안전 가드)
            var canvasCtrl = GetComponent<BeamProCanvasController>();
            if (canvasCtrl != null && canvasCtrl.IsWorldSpace) return;

            checkTimer += Time.unscaledDeltaTime;
            if (checkTimer < CheckInterval) return;
            checkTimer = 0f;

            bool currentPortrait = Screen.height > Screen.width;
            if (currentPortrait != isPortrait)
            {
                isPortrait = currentPortrait;
                ApplyLayout();
            }
        }

        private void ApplyLayout()
        {
            ApplyCanvasScaler();
            ApplyHUDLayout();
            ApplyFlowPanelArea();
            ApplyTabBarLayout();
        }

        private void ApplyCanvasScaler()
        {
            if (targetScaler == null) return;

            if (isPortrait)
            {
                targetScaler.referenceResolution = new Vector2(540, 960);
                targetScaler.matchWidthOrHeight = 0.5f;
            }
            else
            {
                targetScaler.referenceResolution = new Vector2(960, 540);
                targetScaler.matchWidthOrHeight = 0.5f;
            }
        }

        private void ApplyHUDLayout()
        {
            if (hudPanel == null) return;

            if (isPortrait)
            {
                // Portrait: HUD 하단 18%
                hudPanel.anchorMin = new Vector2(0, 0);
                hudPanel.anchorMax = new Vector2(1, 0.20f);

                // StatusArea: 상단 55%에 수직 배치
                if (statusArea != null)
                {
                    statusArea.anchorMin = new Vector2(0, 0.45f);
                    statusArea.anchorMax = new Vector2(1, 1);
                    statusArea.offsetMin = new Vector2(10, 0);
                    statusArea.offsetMax = new Vector2(-10, -5);

                    // HorizontalLayout 비활성 → VerticalLayout 활성
                    if (statusHLayout != null)
                        statusHLayout.enabled = false;

                    EnsureStatusVLayout();
                    if (statusVLayout != null)
                        statusVLayout.enabled = true;
                }

                // ButtonArea: 하단 45%에 수평 유지
                if (buttonArea != null)
                {
                    buttonArea.anchorMin = new Vector2(0, 0);
                    buttonArea.anchorMax = new Vector2(1, 0.45f);
                    buttonArea.offsetMin = new Vector2(10, 5);
                    buttonArea.offsetMax = new Vector2(-10, 0);

                    if (buttonHLayout != null)
                    {
                        buttonHLayout.enabled = true;
                        buttonHLayout.spacing = 15;
                    }
                }
            }
            else
            {
                // Landscape: HUD 하단 12%
                hudPanel.anchorMin = new Vector2(0, 0);
                hudPanel.anchorMax = new Vector2(1, 0.15f);

                // StatusArea: 왼쪽 60%에 수평 배치
                if (statusArea != null)
                {
                    statusArea.anchorMin = new Vector2(0, 0);
                    statusArea.anchorMax = new Vector2(0.6f, 1);
                    statusArea.offsetMin = new Vector2(10, 5);
                    statusArea.offsetMax = new Vector2(0, -5);

                    if (statusHLayout != null)
                    {
                        statusHLayout.enabled = true;
                        statusHLayout.spacing = 20;
                    }

                    if (statusVLayout != null)
                        statusVLayout.enabled = false;
                }

                // ButtonArea: 오른쪽 40%
                if (buttonArea != null)
                {
                    buttonArea.anchorMin = new Vector2(0.6f, 0);
                    buttonArea.anchorMax = new Vector2(1, 1);
                    buttonArea.offsetMin = new Vector2(10, 5);
                    buttonArea.offsetMax = new Vector2(-10, -5);

                    if (buttonHLayout != null)
                    {
                        buttonHLayout.enabled = true;
                        buttonHLayout.spacing = 10;
                    }
                }
            }
        }

        private void EnsureStatusVLayout()
        {
            if (statusVLayout != null) return;
            if (statusArea == null) return;

            statusVLayout = statusArea.GetComponent<VerticalLayoutGroup>();
            if (statusVLayout == null)
            {
                statusVLayout = statusArea.gameObject.AddComponent<VerticalLayoutGroup>();
                statusVLayout.spacing = 4;
                statusVLayout.childForceExpandWidth = true;
                statusVLayout.childForceExpandHeight = true;
                statusVLayout.childControlWidth = true;
                statusVLayout.childControlHeight = true;
                statusVLayout.padding = new RectOffset(5, 5, 2, 2);
            }
        }

        private void ApplyFlowPanelArea()
        {
            if (flowPanelArea == null) return;

            float hudTop = isPortrait ? 0.20f : 0.15f;
            flowPanelArea.anchorMin = new Vector2(0, hudTop);
            flowPanelArea.anchorMax = new Vector2(1, 1);
            flowPanelArea.offsetMin = Vector2.zero;
            flowPanelArea.offsetMax = Vector2.zero;
        }

        private void ApplyTabBarLayout()
        {
            if (tabBar == null) return;

            float tabHeight = isPortrait ? 70f : 60f;
            tabBar.sizeDelta = new Vector2(0, tabHeight);

            if (contentArea != null)
                contentArea.offsetMax = new Vector2(0, -tabHeight);
        }

        [ContextMenu("Force Portrait Layout")]
        private void ForcePortraitLayout()
        {
            isPortrait = true;
            ApplyLayout();
            Debug.Log("[BeamProUIAdapter] Forced Portrait layout");
        }

        [ContextMenu("Force Landscape Layout")]
        private void ForceLandscapeLayout()
        {
            isPortrait = false;
            ApplyLayout();
            Debug.Log("[BeamProUIAdapter] Forced Landscape layout");
        }
    }
}

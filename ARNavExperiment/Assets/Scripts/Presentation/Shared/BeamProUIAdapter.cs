using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Presentation.BeamPro;

namespace ARNavExperiment.Presentation.Shared
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
            isPortrait = DetectPortrait();
            var rt = transform as RectTransform;
            Debug.Log($"[BeamProUIAdapter] Start — Screen={Screen.width}×{Screen.height}, " +
                      $"Canvas={rt?.rect.width:F0}×{rt?.rect.height:F0}, isPortrait={isPortrait}");
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

            bool currentPortrait = DetectPortrait();
            if (currentPortrait != isPortrait)
            {
                isPortrait = currentPortrait;
                Debug.Log($"[BeamProUIAdapter] 방향 전환 — isPortrait={isPortrait}");
                ApplyLayout();
            }
        }

        /// <summary>
        /// Canvas RectTransform의 실제 크기로 Portrait 여부를 판정합니다.
        /// Screen.width/height는 Beam Pro에서 글래스 디스플레이 해상도를 반환할 수 있어 신뢰 불가.
        /// </summary>
        private bool DetectPortrait()
        {
            var rt = transform as RectTransform;
            if (rt != null && rt.rect.width > 0 && rt.rect.height > 0)
                return rt.rect.height > rt.rect.width;

            return Screen.height > Screen.width;
        }

        private void ApplyLayout()
        {
            ApplyCanvasScaler();
            ApplyHUDLayout();
            ApplyFlowPanelArea();
            ApplyTabBarLayout();
            ApplySafeArea();
        }

        private void ApplyCanvasScaler()
        {
            if (targetScaler == null) return;

            if (isPortrait)
            {
                targetScaler.referenceResolution = new Vector2(540, 1200);
                targetScaler.matchWidthOrHeight = 0.5f;
            }
            else
            {
                targetScaler.referenceResolution = new Vector2(1200, 540);
                targetScaler.matchWidthOrHeight = 0.5f;
            }
        }

        private void ApplyHUDLayout()
        {
            if (hudPanel == null) return;

            if (isPortrait)
            {
                // Portrait: HUD 하단 20% (20:9 대응)
                hudPanel.anchorMin = new Vector2(0, 0);
                hudPanel.anchorMax = new Vector2(1, 0.20f);

                // StatusArea: 상단 60%에 수직 배치
                if (statusArea != null)
                {
                    statusArea.anchorMin = new Vector2(0, 0.40f);
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

                // ButtonArea: 하단 40%에 수평 유지
                if (buttonArea != null)
                {
                    buttonArea.anchorMin = new Vector2(0, 0);
                    buttonArea.anchorMax = new Vector2(1, 0.40f);
                    buttonArea.offsetMin = new Vector2(10, 5);
                    buttonArea.offsetMax = new Vector2(-10, 0);

                    if (buttonHLayout != null)
                    {
                        buttonHLayout.enabled = true;
                        buttonHLayout.spacing = 10;
                    }
                }

                ApplyAutoSizingToChildren();
            }
            else
            {
                // Landscape: HUD 하단 12%
                hudPanel.anchorMin = new Vector2(0, 0);
                hudPanel.anchorMax = new Vector2(1, 0.12f);

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
                        statusHLayout.spacing = 12;
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

                DisableAutoSizingOnChildren();
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
                statusVLayout.padding = new RectOffset(8, 8, 4, 4);
            }
        }

        private void ApplyFlowPanelArea()
        {
            if (flowPanelArea == null) return;

            float hudTop = isPortrait ? 0.20f : 0.12f;
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

        private void ApplySafeArea()
        {
            if (hudPanel == null) return;

            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) return;

            float bottomInset = safe.y / Screen.height;
            if (bottomInset > 0.001f)
            {
                hudPanel.offsetMin = new Vector2(hudPanel.offsetMin.x,
                    bottomInset * (targetScaler != null ? targetScaler.referenceResolution.y : 1200f));
            }
        }

        private void ApplyAutoSizingToChildren()
        {
            // StatusArea 자식 TMP에 autoSizing 활성화
            if (statusArea != null)
            {
                foreach (var tmp in statusArea.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 12;
                    tmp.fontSizeMax = 18;
                }
            }

            // ButtonArea 자식 TMP에 autoSizing 활성화
            if (buttonArea != null)
            {
                foreach (var tmp in buttonArea.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 14;
                    tmp.fontSizeMax = 20;
                }
            }
        }

        private void DisableAutoSizingOnChildren()
        {
            // StatusArea 자식 TMP autoSizing 해제 + 고정 fontSize 복원
            if (statusArea != null)
            {
                foreach (var tmp in statusArea.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    tmp.enableAutoSizing = false;
                    tmp.fontSize = 14;
                }
            }

            // ButtonArea 자식 TMP autoSizing 해제 + 고정 fontSize 복원
            if (buttonArea != null)
            {
                foreach (var tmp in buttonArea.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    tmp.enableAutoSizing = false;
                    tmp.fontSize = 16;
                }
            }
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

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

        // (VLG 제거됨 — HLG만 유지하여 레이아웃 충돌 방지)

        private void Start()
        {
            // SerializedField null 참조 자동 복구 (와이어링 누락 대비)
            AutoResolveReferences();

            // 초기 방향 판정 및 레이아웃 적용
            isPortrait = DetectPortrait();
            var rt = transform as RectTransform;
            Debug.Log($"[BeamProUIAdapter] Start — Screen={Screen.width}×{Screen.height}, " +
                      $"Canvas={rt?.rect.width:F0}×{rt?.rect.height:F0}, isPortrait={isPortrait}, " +
                      $"refs: scaler={targetScaler != null}, hud={hudPanel != null}, " +
                      $"status={statusArea != null}, statusHLG={statusHLayout != null}, " +
                      $"btnArea={buttonArea != null}, flow={flowPanelArea != null}");
            ApplyLayout();
        }

        /// <summary>
        /// 와이어링 누락 시 이름 기반으로 참조를 자동 검색합니다.
        /// </summary>
        private void AutoResolveReferences()
        {
            if (targetScaler == null)
                targetScaler = GetComponent<CanvasScaler>();

            if (gameObject.name == "ExperimenterCanvas")
            {
                if (hudPanel == null)
                {
                    var hud = FindChildRecursive(transform, "ExperimenterHUD");
                    if (hud != null)
                    {
                        hudPanel = hud.GetComponent<RectTransform>();
                        Debug.Log("[BeamProUIAdapter] Auto-resolved hudPanel");
                    }
                }

                if (hudPanel != null && statusArea == null)
                {
                    var sa = hudPanel.Find("StatusArea");
                    if (sa != null)
                    {
                        statusArea = sa.GetComponent<RectTransform>();
                        statusHLayout = sa.GetComponent<HorizontalLayoutGroup>();
                        Debug.Log("[BeamProUIAdapter] Auto-resolved statusArea + HLayout");
                    }
                }

                if (hudPanel != null && buttonArea == null)
                {
                    var ba = hudPanel.Find("ButtonArea");
                    if (ba != null)
                    {
                        buttonArea = ba.GetComponent<RectTransform>();
                        buttonHLayout = ba.GetComponent<HorizontalLayoutGroup>();
                        Debug.Log("[BeamProUIAdapter] Auto-resolved buttonArea + HLayout");
                    }
                }

                if (flowPanelArea == null)
                {
                    var fp = transform.Find("FlowPanelArea");
                    if (fp != null)
                    {
                        flowPanelArea = fp.GetComponent<RectTransform>();
                        Debug.Log("[BeamProUIAdapter] Auto-resolved flowPanelArea");
                    }
                }
            }
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            return null;
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

                // StatusArea: 전체 너비, 상단 55%에 수평 배치 유지 (HLG만 사용)
                if (statusArea != null)
                {
                    statusArea.anchorMin = new Vector2(0, 0.45f);
                    statusArea.anchorMax = new Vector2(1, 1);
                    statusArea.offsetMin = new Vector2(10, 0);
                    statusArea.offsetMax = new Vector2(-10, -5);

                    // HLG 유지 (VLG 생성하지 않음 — 레이아웃 충돌 방지)
                    var hlg = statusHLayout != null ? statusHLayout
                        : statusArea.GetComponent<HorizontalLayoutGroup>();
                    if (hlg != null)
                    {
                        hlg.enabled = true;
                        hlg.spacing = 8;
                    }

                    // 혹시 이전에 생성된 VLG가 있으면 제거
                    var vlg = statusArea.GetComponent<VerticalLayoutGroup>();
                    if (vlg != null) Destroy(vlg);
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

                    var hlg = statusHLayout != null ? statusHLayout
                        : statusArea.GetComponent<HorizontalLayoutGroup>();
                    if (hlg != null)
                    {
                        hlg.enabled = true;
                        hlg.spacing = 12;
                    }

                    // 혹시 이전에 생성된 VLG가 있으면 제거
                    var vlg = statusArea.GetComponent<VerticalLayoutGroup>();
                    if (vlg != null) Destroy(vlg);
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

        // EnsureStatusVLayout 제거됨 — HLG만 사용하여 HLG/VLG 충돌 방지

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
            {
                contentArea.offsetMax = new Vector2(0, -tabHeight);

                // ExperimenterHUD(sortOrder=10)와의 겹침 방지
                // Portrait: HUD 20%(240px) + HeadingArea 34px + margin = 280px
                // Landscape: HUD 12%(65px) + HeadingArea 34px + margin = 110px
                float bottomInset = isPortrait ? 280f : 110f;
                contentArea.offsetMin = new Vector2(0, bottomInset);
            }
        }

        private void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) return;

            float refHeight = targetScaler != null ? targetScaler.referenceResolution.y : 1200f;

            // ExperimenterCanvas: 하단 SafeArea
            if (hudPanel != null)
            {
                float bottomInset = safe.y / Screen.height;
                if (bottomInset > 0.001f)
                {
                    hudPanel.offsetMin = new Vector2(hudPanel.offsetMin.x,
                        bottomInset * refHeight);
                }
            }

            // BeamProCanvas: 상단 SafeArea — 카메라 노치 회피
            if (tabBar != null)
            {
                float topInsetNorm = 1f - (safe.y + safe.height) / Screen.height;
                if (topInsetNorm > 0.001f)
                {
                    float topInsetPx = topInsetNorm * refHeight;
                    tabBar.anchoredPosition = new Vector2(
                        tabBar.anchoredPosition.x, -topInsetPx);

                    if (contentArea != null)
                    {
                        float tabHeight = isPortrait ? 70f : 60f;
                        contentArea.offsetMax = new Vector2(0, -(tabHeight + topInsetPx));
                    }
                }
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

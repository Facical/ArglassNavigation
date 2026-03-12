using UnityEngine;
using UnityEngine.UI;
using ARNavExperiment.Core;
using ARNavExperiment.Presentation.Shared;

namespace ARNavExperiment.Presentation.BeamPro
{
    /// <summary>
    /// BeamProCanvas의 렌더 모드를 조건에 따라 동적 전환합니다.
    /// - GlassOnly: WorldSpace로 전환하여 글래스 head-locked UI로 표시
    /// - Hybrid: ScreenSpaceOverlay로 복원하여 Beam Pro 폰 표시
    /// GlassCanvasController 패턴을 따르되 양방향 전환이 가능합니다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class BeamProCanvasController : MonoBehaviour
    {
        [Header("Glass Display Settings")]
#pragma warning disable CS0414 // !UNITY_EDITOR 블록에서만 사용
        [SerializeField] private float distanceFromCamera = 1.3f;
        [SerializeField] private float canvasScale = 0.00075f;
#pragma warning restore CS0414
        [SerializeField] private Vector2 canvasSize = new Vector2(1440, 810);
        [SerializeField] private Vector3 glassOffset = new Vector3(0, 0.10f, 0);

        [Header("Zoom UI")]
        [SerializeField] private GameObject zoomButtonPanel;

        [Header("GlassOnly Map Toggle")]
        [SerializeField] private UnityEngine.UI.Button glassMapToggleButton;

        public bool IsWorldSpace { get; private set; }

        private Canvas canvas;
        private CanvasScaler scaler;
        private GraphicRaycaster originalRaycaster;
        private CanvasGroup canvasGroup;

        // 원본 설정 캐시
        private RenderMode cachedRenderMode;
        private int cachedSortOrder;
        private CanvasScaler.ScaleMode cachedScaleMode;
        private Vector2 cachedRefResolution;
        private float cachedMatchWidthOrHeight;
        private Transform cachedParent;
        private Vector3 cachedLocalPos;
        private Quaternion cachedLocalRot;
        private Vector3 cachedLocalScale;
        private Vector2 cachedSizeDelta;
        private bool hasCachedSettings;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            scaler = GetComponent<CanvasScaler>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            CacheOriginalSettings();

            // Map 토글 버튼 이벤트 바인딩
            if (glassMapToggleButton != null)
            {
                glassMapToggleButton.onClick.AddListener(OnGlassMapToggle);
                glassMapToggleButton.gameObject.SetActive(false); // 기본 숨김
            }
        }

        private void OnGlassMapToggle()
        {
            var hub = BeamProHubController.Instance;
            if (hub != null)
                hub.ToggleMapTab();
        }

        private void OnEnable()
        {
            if (ConditionController.Instance != null)
            {
                ConditionController.Instance.OnConditionChanged += OnConditionChanged;
                // ApplyCondition 즉시 호출 제거 — 기본 enum값(GlassOnly=0)으로 불필요한 전환 방지
            }
        }

        private void OnDisable()
        {
            if (ConditionController.Instance != null)
                ConditionController.Instance.OnConditionChanged -= OnConditionChanged;
        }

        private void OnConditionChanged(ExperimentCondition condition)
        {
            ApplyCondition(condition);
        }

        private void ApplyCondition(ExperimentCondition condition)
        {
            if (condition == ExperimentCondition.GlassOnly)
            {
                // GlassOnly: WorldSpace로 전환하여 글래스 head-locked UI로 정보 허브 표시
                // 폰 화면은 ExperimenterCanvas(ScreenSpaceOverlay, sortOrder=10)가 미러링 방지
                SwitchToWorldSpace();
            }
            else
            {
                SwitchToScreenSpace();  // Hybrid: 폰 화면에 정보 허브
            }
        }

        private void SwitchToWorldSpace()
        {
#if !UNITY_EDITOR
            if (IsWorldSpace) return;

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[BeamProCanvasCtrl] Main Camera를 찾을 수 없습니다.");
                return;
            }

            // WorldSpace로 전환
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 5; // ExperimentCanvas(0)보다 높게

            // Canvas 크기 설정
            var rt = GetComponent<RectTransform>();
            rt.sizeDelta = canvasSize;
            rt.pivot = new Vector2(0.5f, 0.5f);

            // CanvasScaler → ConstantPixelSize
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
            }

            // GraphicRaycaster → TrackedDeviceGraphicRaycaster 교체
            var oldRaycaster = GetComponent<GraphicRaycaster>();
            if (oldRaycaster) DestroyImmediate(oldRaycaster);

#if XR_INTERACTION
            gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            Debug.Log("[BeamProCanvasCtrl] TrackedDeviceGraphicRaycaster 추가");
#else
            gameObject.AddComponent<GraphicRaycaster>();
            Debug.LogWarning("[BeamProCanvasCtrl] XR Interaction Toolkit 미설치 — 기본 GraphicRaycaster 사용");
#endif

            // XR 카메라에 head-lock 부착
            transform.SetParent(cam.transform, false);
            transform.localPosition = new Vector3(glassOffset.x, glassOffset.y, distanceFromCamera);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * canvasScale;

            // BeamProUIAdapter 비활성화 (WorldSpace에서는 방향 감지 불필요)
            var adapter = GetComponent<BeamProUIAdapter>();
            if (adapter != null) adapter.enabled = false;

            // 줌 버튼 활성화
            if (zoomButtonPanel) zoomButtonPanel.SetActive(true);

            // GlassOnly Map 토글 버튼 표시
            if (glassMapToggleButton) glassMapToggleButton.gameObject.SetActive(true);

            IsWorldSpace = true;
            Debug.Log($"[BeamProCanvasCtrl] WorldSpace로 전환 — distance={distanceFromCamera}m, scale={canvasScale}");
#else
            // 에디터에서는 ScreenSpaceOverlay 유지
            IsWorldSpace = false;
            if (zoomButtonPanel) zoomButtonPanel.SetActive(true);
            if (glassMapToggleButton) glassMapToggleButton.gameObject.SetActive(true);
            Debug.Log("[BeamProCanvasCtrl] 에디터 — WorldSpace 전환 건너뜀 (ScreenSpaceOverlay 유지)");
#endif
        }

        private void SwitchToScreenSpace()
        {
#if !UNITY_EDITOR
            if (!IsWorldSpace && hasCachedSettings) return;

            // 캐시된 원본 설정 복원
            RestoreOriginalSettings();

            // GraphicRaycaster 복원
            var currentRaycaster = GetComponent<GraphicRaycaster>();
            if (currentRaycaster == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // BeamProUIAdapter 재활성화
            var adapter = GetComponent<BeamProUIAdapter>();
            if (adapter != null) adapter.enabled = true;

            // 줌 버튼 비활성화 (폰에서는 핀치-투-줌 사용)
            if (zoomButtonPanel) zoomButtonPanel.SetActive(false);

            // GlassOnly Map 토글 버튼 숨김
            if (glassMapToggleButton) glassMapToggleButton.gameObject.SetActive(false);

            IsWorldSpace = false;
            Debug.Log("[BeamProCanvasCtrl] ScreenSpaceOverlay로 복원");
#else
            IsWorldSpace = false;
            if (zoomButtonPanel) zoomButtonPanel.SetActive(false);
            if (glassMapToggleButton) glassMapToggleButton.gameObject.SetActive(false);
            Debug.Log("[BeamProCanvasCtrl] 에디터 — ScreenSpaceOverlay 유지");
#endif
        }

        /// <summary>
        /// GlassOnly WorldSpace 모드에서 BeamPro UI의 가시성을 제어합니다.
        /// Briefing/Verification/Rating 중에 BeamPro UI를 숨겨 겹침을 방지합니다.
        /// </summary>
        public void SetGlassVisibility(bool visible)
        {
            if (!IsWorldSpace) return;
            if (canvasGroup == null) return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void CacheOriginalSettings()
        {
            if (canvas == null) return;

            cachedRenderMode = canvas.renderMode;
            cachedSortOrder = canvas.sortingOrder;

            if (scaler != null)
            {
                cachedScaleMode = scaler.uiScaleMode;
                cachedRefResolution = scaler.referenceResolution;
                cachedMatchWidthOrHeight = scaler.matchWidthOrHeight;
            }

            cachedParent = transform.parent;
            cachedLocalPos = transform.localPosition;
            cachedLocalRot = transform.localRotation;
            cachedLocalScale = transform.localScale;

            var rt = GetComponent<RectTransform>();
            if (rt != null)
                cachedSizeDelta = rt.sizeDelta;

            hasCachedSettings = true;
        }

        private void RestoreOriginalSettings()
        {
            if (!hasCachedSettings) return;

            // 부모 복원 (WorldSpace에서 카메라 자식이었을 수 있음)
            transform.SetParent(cachedParent, false);
            transform.localPosition = cachedLocalPos;
            transform.localRotation = cachedLocalRot;
            transform.localScale = cachedLocalScale;

            canvas.renderMode = cachedRenderMode;
            canvas.sortingOrder = cachedSortOrder;

            if (scaler != null)
            {
                scaler.uiScaleMode = cachedScaleMode;
                scaler.referenceResolution = cachedRefResolution;
                scaler.matchWidthOrHeight = cachedMatchWidthOrHeight;
            }

            var rt = GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = cachedSizeDelta;
        }
    }
}

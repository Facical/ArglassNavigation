using UnityEngine;
using UnityEngine.UI;
using ARNavExperiment.Core;

namespace ARNavExperiment.UI
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
        [SerializeField] private float distanceFromCamera = 2.2f;
        [SerializeField] private float canvasScale = 0.0004f;
        [SerializeField] private Vector2 canvasSize = new Vector2(1920, 1080);
        [SerializeField] private Vector3 glassOffset = new Vector3(0, -0.15f, 0);

        [Header("Zoom UI")]
        [SerializeField] private GameObject zoomButtonPanel;

        public bool IsWorldSpace { get; private set; }

        private Canvas canvas;
        private CanvasScaler scaler;
        private GraphicRaycaster originalRaycaster;

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
            CacheOriginalSettings();
        }

        private void OnEnable()
        {
            // 조건 컨트롤러가 이미 설정된 경우 즉시 적용
            if (ConditionController.Instance != null)
            {
                ConditionController.Instance.OnConditionChanged += OnConditionChanged;
                ApplyCondition(ConditionController.Instance.CurrentCondition);
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
                SwitchToWorldSpace();
            else
                SwitchToScreenSpace();
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
            if (oldRaycaster) Destroy(oldRaycaster);

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

            IsWorldSpace = true;
            Debug.Log($"[BeamProCanvasCtrl] WorldSpace로 전환 — distance={distanceFromCamera}m, scale={canvasScale}");
#else
            // 에디터에서는 ScreenSpaceOverlay 유지
            IsWorldSpace = false;
            if (zoomButtonPanel) zoomButtonPanel.SetActive(true);
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

            IsWorldSpace = false;
            Debug.Log("[BeamProCanvasCtrl] ScreenSpaceOverlay로 복원");
#else
            IsWorldSpace = false;
            if (zoomButtonPanel) zoomButtonPanel.SetActive(false);
            Debug.Log("[BeamProCanvasCtrl] 에디터 — ScreenSpaceOverlay 유지");
#endif
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

using UnityEngine;
using UnityEngine.UI;

namespace ARNavExperiment.UI
{
    /// <summary>
    /// 디바이스 실행 시 ExperimentCanvas를 WorldSpace로 전환하여
    /// XR 글래스에 렌더링합니다. 에디터에서는 ScreenSpaceOverlay 유지.
    /// 디바이스에서는 TrackedDeviceGraphicRaycaster로 교체하여
    /// 핸드트래킹 XR Ray Interactor의 레이캐스트를 수신합니다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class GlassCanvasController : MonoBehaviour
    {
        [Header("Glass Display Settings")]
        [SerializeField] private float distanceFromCamera = 2.0f;
        [SerializeField] private float canvasScale = 0.0005f;
        [SerializeField] private Vector2 canvasSize = new Vector2(1920, 1080);

        private Canvas canvas;
        private CanvasScaler scaler;
        private bool isAttached;

        private void Start()
        {
#if !UNITY_EDITOR
            canvas = GetComponent<Canvas>();
            scaler = GetComponent<CanvasScaler>();
            TryAttachToCamera();
#endif
        }

        private void LateUpdate()
        {
#if !UNITY_EDITOR
            if (!isAttached)
                TryAttachToCamera();
#endif
        }

        private void TryAttachToCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // WorldSpace로 전환
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;

            // Canvas 크기 설정
            var rt = GetComponent<RectTransform>();
            rt.sizeDelta = canvasSize;
            rt.pivot = new Vector2(0.5f, 0.5f);

            // CanvasScaler 조정 (WorldSpace에서는 ConstantPixelSize 사용)
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
            }

            // GraphicRaycaster → TrackedDeviceGraphicRaycaster 교체
            // XR Ray Interactor의 레이캐스트를 WorldSpace Canvas에 전달
            var oldRaycaster = GetComponent<GraphicRaycaster>();
            if (oldRaycaster) Destroy(oldRaycaster);

#if XR_INTERACTION
            gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            Debug.Log("[GlassCanvas] TrackedDeviceGraphicRaycaster 추가 — 핸드트래킹 UI 인터랙션 활성화");
#else
            // XR Interaction Toolkit 없으면 기본 GraphicRaycaster 유지
            gameObject.AddComponent<GraphicRaycaster>();
            Debug.LogWarning("[GlassCanvas] XR Interaction Toolkit 미설치 — 기본 GraphicRaycaster 사용");
#endif

            // XR 카메라에 부착 (head-locked UI)
            transform.SetParent(cam.transform, false);
            transform.localPosition = new Vector3(0, 0, distanceFromCamera);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * canvasScale;

            isAttached = true;
            Debug.Log($"[GlassCanvas] XR 카메라({cam.name})에 부착 완료 — distance={distanceFromCamera}m, scale={canvasScale}");
        }
    }
}

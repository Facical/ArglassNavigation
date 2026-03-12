using UnityEngine;
using UnityEngine.UI;
using ARNavExperiment.Utils;

namespace ARNavExperiment.Presentation.Glass
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
        public static GlassCanvasController Instance { get; private set; }

        [Header("Glass Display Settings")]
        [SerializeField] private float distanceFromCamera = 1.5f;
        [SerializeField] private float canvasScale = 0.00088f;
        [SerializeField] private Vector2 canvasSize = new Vector2(1440, 810);

        private Canvas canvas;
        private CanvasScaler scaler;
#pragma warning disable CS0414 // !UNITY_EDITOR 블록에서만 사용
        private bool isAttached;
#pragma warning restore CS0414
        private int retryCount;
        private float lastWarningTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// ExperimentCanvas의 Raycaster를 활성/비활성화합니다.
        /// Navigation 상태에서 OFF하여 BeamPro 캔버스로 ray가 통과하게 합니다.
        /// </summary>
        public void SetRaycasterEnabled(bool enabled)
        {
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = enabled;
#if XR_INTERACTION
            var tracked = GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            if (tracked != null)
                tracked.enabled = enabled;
#endif
        }

        private void Start()
        {
#if !UNITY_EDITOR
            canvas = GetComponent<Canvas>();
            scaler = GetComponent<CanvasScaler>();

            // 부트 진단 로그
            var mainCam = Camera.main;
            var allCams = FindObjectsOfType<Camera>();
#if XR_ARFOUNDATION
            var xrOrigin = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
            Debug.Log($"[GlassCanvas] 부트 진단: Camera.main={mainCam?.name ?? "NULL"}, " +
                $"XROrigin={(xrOrigin != null ? "found" : "NULL")}, " +
                $"XROrigin.Camera={xrOrigin?.Camera?.name ?? "NULL"}, " +
                $"모든 Camera 수: {allCams.Length}");
#else
            Debug.Log($"[GlassCanvas] 부트 진단: Camera.main={mainCam?.name ?? "NULL"}, " +
                $"XR_ARFOUNDATION 미정의, 모든 Camera 수: {allCams.Length}");
#endif

            TryAttachToCamera();
#endif
        }

        private void LateUpdate()
        {
#if !UNITY_EDITOR
            if (!isAttached)
            {
                TryAttachToCamera();

                // 5초마다 경고 로그
                if (Time.time - lastWarningTime >= 5f)
                {
                    lastWarningTime = Time.time;
                    Debug.LogWarning($"[GlassCanvas] 카메라 재시도 {retryCount}회 실패 — " +
                        $"Camera.main={(Camera.main != null ? Camera.main.name : "NULL")}, " +
                        $"XRCameraHelper={(XRCameraHelper.GetCamera()?.name ?? "NULL")}");
                }
            }
#endif
        }

        private void TryAttachToCamera()
        {
            retryCount++;
            var cam = XRCameraHelper.GetCamera();
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
            if (oldRaycaster) DestroyImmediate(oldRaycaster);

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
            Debug.Log($"[GlassCanvas] XR 카메라({cam.name})에 부착 완료 — retries={retryCount}, " +
                $"distance={distanceFromCamera}m, scale={canvasScale}, " +
                $"renderMode={canvas.renderMode}, sizeDelta={rt.sizeDelta}, " +
                $"rect={rt.rect}, childCount={transform.childCount}, " +
                $"canvasEnabled={canvas.enabled}");
        }

        /// <summary>캔버스가 카메라에 부착되었는지 여부</summary>
        public bool IsAttached => isAttached;
    }
}

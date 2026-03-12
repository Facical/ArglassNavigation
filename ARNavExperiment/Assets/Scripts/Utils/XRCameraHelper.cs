using UnityEngine;
#if XR_ARFOUNDATION
using Unity.XR.CoreUtils;
#endif

namespace ARNavExperiment.Utils
{
    /// <summary>
    /// XREAL SDK 환경에서 Camera.main이 null을 반환하는 문제를 해결합니다.
    /// XROrigin.Camera 우선 → Camera.main 폴백 → FindObjectOfType 최종 폴백.
    /// 결과를 캐시하여 매 프레임 검색 비용을 제거합니다 (0.5초 주기 재검증).
    /// </summary>
    public static class XRCameraHelper
    {
        private static Camera cachedCamera;
        private static float lastCheckTime;
        private const float RecheckInterval = 0.5f;

        /// <summary>
        /// XR 환경에서 안전하게 카메라를 가져옵니다.
        /// XROrigin.Camera → Camera.main → FindObjectOfType&lt;Camera&gt; 순서로 탐색.
        /// </summary>
        public static Camera GetCamera()
        {
            // 캐시가 유효하고 재검증 주기 전이면 캐시 반환
            if (cachedCamera != null && Time.time - lastCheckTime < RecheckInterval)
                return cachedCamera;

            lastCheckTime = Time.time;

            // 캐시된 카메라가 아직 살아있으면 그대로 사용
            if (cachedCamera != null)
                return cachedCamera;

            // 전략 1: XROrigin.Camera
#if XR_ARFOUNDATION
            var xrOrigin = Object.FindObjectOfType<XROrigin>();
            if (xrOrigin != null && xrOrigin.Camera != null)
            {
                cachedCamera = xrOrigin.Camera;
                Debug.Log($"[XRCameraHelper] XROrigin.Camera 발견: {cachedCamera.name}");
                return cachedCamera;
            }
#endif

            // 전략 2: Camera.main
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                cachedCamera = mainCam;
                Debug.Log($"[XRCameraHelper] Camera.main 발견: {cachedCamera.name}");
                return cachedCamera;
            }

            // 전략 3: FindObjectOfType<Camera>
            var anyCam = Object.FindObjectOfType<Camera>();
            if (anyCam != null)
            {
                cachedCamera = anyCam;
                Debug.Log($"[XRCameraHelper] FindObjectOfType<Camera> 발견: {cachedCamera.name}");
                return cachedCamera;
            }

            Debug.LogWarning("[XRCameraHelper] 씬에서 카메라를 찾을 수 없습니다!");
            return null;
        }

        /// <summary>캐시를 강제로 무효화합니다 (씬 전환 시 등).</summary>
        public static void InvalidateCache()
        {
            cachedCamera = null;
            lastCheckTime = 0f;
        }
    }
}

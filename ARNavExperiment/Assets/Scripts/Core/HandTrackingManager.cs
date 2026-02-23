using UnityEngine;

namespace ARNavExperiment.Core
{
    /// <summary>
    /// 핸드트래킹 초기화 및 XR Interaction 설정 관리.
    /// XREAL Air2 Ultra의 핸드트래킹(Pinch=클릭)으로 글래스 UI 직접 조작.
    /// 에디터에서는 비활성화, 디바이스에서만 활성화.
    /// </summary>
    public class HandTrackingManager : MonoBehaviour
    {
        public static HandTrackingManager Instance { get; private set; }

        [SerializeField] private bool enableHandTracking = true;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
#if !UNITY_EDITOR
            if (enableHandTracking)
                InitializeHandTracking();
#else
            Debug.Log("[HandTracking] 에디터 모드 — 핸드트래킹 비활성 (마우스 사용)");
#endif
        }

        private void InitializeHandTracking()
        {
#if XR_HANDS
            Debug.Log("[HandTracking] XR Hands 패키지 감지 — 핸드트래킹 초기화");
            // XR Interaction Hands Setup prefab이 씬에 있으면 자동 활성화
            // 없으면 XRHandTrackingManager가 XR Origin 하위에서 동작
#else
            Debug.LogWarning("[HandTracking] XR Hands 패키지 미설치 — 핸드트래킹 사용 불가");
#endif
        }

        public bool IsHandTrackingAvailable()
        {
#if XR_HANDS && !UNITY_EDITOR
            return enableHandTracking;
#else
            return false;
#endif
        }
    }
}

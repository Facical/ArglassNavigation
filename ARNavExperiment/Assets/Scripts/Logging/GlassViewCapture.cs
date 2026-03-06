using UnityEngine;
using System;
using System.IO;
using System.Threading;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;

namespace ARNavExperiment.Logging
{
    /// <summary>
    /// XREAL 글래스 뷰(가상 오버레이)를 MP4로 녹화하는 디버그 도구.
    /// 디바이스에서는 XREAL SDK XREALVideoCapture API (VirtualOnly 모드) 사용, 에디터에서는 상태 시뮬레이션.
    /// R키 단축키 또는 ExperimenterHUD 버튼으로 녹화 토글.
    /// </summary>
    public class GlassViewCapture : MonoBehaviour
    {
        public static GlassViewCapture Instance { get; private set; }

        public bool IsRecording { get; private set; }

        /// <summary>녹화 상태 변경 시 발행 (isRecording)</summary>
        public event Action<bool> OnRecordingStateChanged;

        [SerializeField] private bool autoRecord = true;

        private string captureDirectory;
        private string currentFilePath;
        private bool isPausedWhileRecording;
        private int segmentIndex;

#if UNITY_ANDROID && !UNITY_EDITOR
        private Unity.XR.XREAL.XREALVideoCapture videoCapture;
        private bool isInitializing;
        private bool pendingStartAfterInit;
        private readonly ManualResetEventSlim stopCompleteEvent = new ManualResetEventSlim(true);
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            captureDirectory = Path.Combine(UnityEngine.Application.persistentDataPath, "glass_captures");
            if (!Directory.Exists(captureDirectory))
                Directory.CreateDirectory(captureDirectory);

#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeVideoCapture();
#endif
        }

        private void Start()
        {
            if (autoRecord)
            {
                Debug.Log("[GlassViewCapture] 자동 녹화 시작");
                StartCapture();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
                ToggleCapture();
        }

        /// <summary>녹화 시작/중지 토글</summary>
        public void ToggleCapture()
        {
            if (IsRecording)
            {
                isPausedWhileRecording = false;
                StopCapture();
            }
            else
            {
                StartCapture();
            }
        }

        public void StartCapture()
        {
            if (IsRecording) return;

            segmentIndex++;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string participantId = GetParticipantId();
            string fileName = participantId != "unknown"
                ? $"glass_{participantId}_{timestamp}_seg{segmentIndex:D2}.mp4"
                : $"glass_{timestamp}_seg{segmentIndex:D2}.mp4";
            currentFilePath = Path.Combine(captureDirectory, fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
            if (videoCapture == null)
            {
                if (!isInitializing)
                {
                    pendingStartAfterInit = true;
                    InitializeVideoCapture();
                }
                else
                {
                    pendingStartAfterInit = true;
                }
                return;
            }

            StartVideoMode();
#else
            // 에디터 시뮬레이션
            IsRecording = true;
            OnRecordingStateChanged?.Invoke(true);
            Debug.Log($"[GlassViewCapture] (Editor) 녹화 시뮬레이션 시작: {currentFilePath}");
            DomainEventBus.Instance?.Publish(new GlassCaptureStateChanged("start", fileName));
#endif
        }

        public void StopCapture(bool synchronous = false)
        {
            if (!IsRecording) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (videoCapture != null && videoCapture.IsRecording)
            {
                stopCompleteEvent.Reset();
                videoCapture.StopRecordingAsync(OnStoppedRecording);

                if (synchronous)
                {
                    Debug.Log("[GlassViewCapture] 동기 대기 중 (최대 3초)...");
                    stopCompleteEvent.Wait(3000);
                }
            }
#else
            // 에디터 시뮬레이션
            IsRecording = false;
            OnRecordingStateChanged?.Invoke(false);
            Debug.Log($"[GlassViewCapture] (Editor) 녹화 시뮬레이션 중지: {currentFilePath}");
            DomainEventBus.Instance?.Publish(new GlassCaptureStateChanged("stop", Path.GetFileName(currentFilePath)));
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void InitializeVideoCapture()
        {
            if (isInitializing) return;
            isInitializing = true;
            Debug.Log("[GlassViewCapture] SDK VideoCapture 초기화 중...");

            Unity.XR.XREAL.XREALVideoCaptureUtility.CreateAsync(false, (captureObj) =>
            {
                isInitializing = false;
                if (captureObj == null)
                {
                    Debug.LogError("[GlassViewCapture] VideoCapture 생성 실패");
                    DomainEventBus.Instance?.Publish(new GlassCaptureStateChanged("error"));
                    return;
                }

                videoCapture = captureObj;
                Debug.Log("[GlassViewCapture] VideoCapture 초기화 완료");

                if (pendingStartAfterInit)
                {
                    pendingStartAfterInit = false;
                    StartVideoMode();
                }
            });
        }

        private void StartVideoMode()
        {
            var cameraParams = new Unity.XR.XREAL.CameraParameters(
                Unity.XR.XREAL.CamMode.VideoMode,
                Unity.XR.XREAL.BlendMode.VirtualOnly);
            cameraParams.audioState = Unity.XR.XREAL.AudioState.None;

            Debug.Log("[GlassViewCapture] StartVideoMode(VirtualOnly)");

            videoCapture.StartVideoModeAsync(cameraParams, (result) =>
            {
                if (result.resultType != Unity.XR.XREAL.CaptureResultType.Success)
                {
                    Debug.LogError($"[GlassViewCapture] VideoMode 시작 실패: {result.resultType}");
                    DomainEventBus.Instance?.Publish(new GlassCaptureStateChanged("error"));
                    return;
                }

                videoCapture.StartRecordingAsync(currentFilePath, OnStartedRecording);
            });
        }

        private void OnStartedRecording(Unity.XR.XREAL.XREALVideoCapture.VideoCaptureResult result)
        {
            if (result.resultType == Unity.XR.XREAL.CaptureResultType.Success)
            {
                IsRecording = true;
                OnRecordingStateChanged?.Invoke(true);
                string fileName = Path.GetFileName(currentFilePath);
                Debug.Log($"[GlassViewCapture] 녹화 시작: {fileName}");
                DomainEventBus.Instance?.Publish(new GlassCaptureStateChanged("start", fileName));
            }
            else
            {
                Debug.LogError($"[GlassViewCapture] 녹화 시작 실패: {result.resultType}");
                DomainEventBus.Instance?.Publish(new GlassCaptureStateChanged("error"));
                videoCapture.StopVideoModeAsync((_) => { });
            }
        }

        private void OnStoppedRecording(Unity.XR.XREAL.XREALVideoCapture.VideoCaptureResult result)
        {
            IsRecording = false;
            OnRecordingStateChanged?.Invoke(false);

            string fileName = Path.GetFileName(currentFilePath);
            if (result.resultType == Unity.XR.XREAL.CaptureResultType.Success)
            {
                Debug.Log($"[GlassViewCapture] 녹화 중지 완료 (moov 기록됨): {fileName}");
                DomainEventBus.Instance?.Publish(new GlassCaptureStateChanged("stop", fileName));
            }
            else
            {
                Debug.LogWarning($"[GlassViewCapture] 녹화 중지 오류: {result.resultType}");
                DomainEventBus.Instance?.Publish(new GlassCaptureStateChanged("error"));
            }

            // StopRecording 완료 시점에서 moov atom은 이미 기록됨 → 즉시 signal
            // StopVideoMode는 비동기로 진행 (파일 안전성에 영향 없음)
            stopCompleteEvent.Set();

            videoCapture.StopVideoModeAsync((_) =>
            {
                Debug.Log("[GlassViewCapture] VideoMode 종료");
            });
        }
#endif

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // Android: 앱 종료 시에도 OnApplicationPause(true)가 먼저 호출됨
                // 여기서 반드시 녹화를 중지해야 moov atom이 기록됨
                if (IsRecording)
                {
                    isPausedWhileRecording = true;
                    Debug.Log("[GlassViewCapture] 앱 일시정지/종료 → 녹화 동기 중지");
                    StopCapture(synchronous: true);
                }
            }
            else
            {
                if (autoRecord && isPausedWhileRecording)
                {
                    isPausedWhileRecording = false;
                    Debug.Log("[GlassViewCapture] 앱 복귀 → 녹화 자동 재개");
                    StartCapture();
                }
            }
        }

        private void OnApplicationQuit()
        {
            // OnApplicationPause(true)에서 이미 중지했을 수 있지만, 안전장치
            if (IsRecording)
            {
                Debug.Log("[GlassViewCapture] 앱 종료 → 녹화 동기 저장");
                StopCapture(synchronous: true);
            }
        }

        private void OnDestroy()
        {
            if (IsRecording)
                StopCapture(synchronous: true);

#if UNITY_ANDROID && !UNITY_EDITOR
            if (videoCapture != null)
            {
                videoCapture.Dispose();
                videoCapture = null;
            }
            stopCompleteEvent.Dispose();
#endif

            if (Instance == this)
                Instance = null;
        }

        private string GetParticipantId()
        {
            var expMgr = Core.ExperimentManager.Instance;
            if (expMgr?.session != null)
                return expMgr.session.participantId;
            return "unknown";
        }
    }
}

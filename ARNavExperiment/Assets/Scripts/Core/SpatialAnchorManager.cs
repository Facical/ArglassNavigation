using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;
#if XR_ARFOUNDATION
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.XREAL;
#endif

namespace ARNavExperiment.Core
{
    /// <summary>
    /// 앵커 재인식 상태
    /// </summary>
    public enum AnchorRelocState { Pending, Tracking, TimedOut, LoadFailed }

    /// <summary>
    /// 개별 앵커 재인식 결과
    /// </summary>
    public class AnchorRelocResult
    {
        public string waypointId;
        public AnchorRelocState state;
        public float elapsedTime;
    }

    /// <summary>
    /// Waypoint-Anchor 매핑 데이터 (JSON 직렬화용)
    /// </summary>
    [Serializable]
    public class AnchorMapping
    {
        public string waypointId;
        public string anchorGuid;
        public float radius = 2f;
        public string locationName;
    }

    [Serializable]
    public class RouteMappingData
    {
        public List<AnchorMapping> waypoints = new List<AnchorMapping>();
    }

    [Serializable]
    public class MappingFileData
    {
        public string createdAt;
        public RouteMappingData routeA = new RouteMappingData();
        public RouteMappingData routeB = new RouteMappingData();
    }

    /// <summary>
    /// XREAL Spatial Anchor 생명주기 관리.
    /// 매핑 모드에서 앵커 생성/저장, 실험 모드에서 앵커 로드/재인식.
    /// </summary>
    public class SpatialAnchorManager : MonoBehaviour
    {
        public static SpatialAnchorManager Instance { get; private set; }

#if XR_ARFOUNDATION
        [Header("AR Foundation")]
        [SerializeField] private ARAnchorManager arAnchorManager;
#endif

        [Header("Settings")]
        [SerializeField] private string mappingFileName = "anchor_mapping.json";
#pragma warning disable CS0414
        [SerializeField] private float anchorTrackingTimeout = 20f;
#pragma warning restore CS0414

        /// <summary>매핑 데이터 (waypointId → anchorGuid)</summary>
        private MappingFileData mappingData;

        /// <summary>로드된 앵커 Transform (waypointId → Transform)</summary>
        private Dictionary<string, Transform> anchorTransforms = new Dictionary<string, Transform>();

        /// <summary>개별 앵커 재인식 결과</summary>
        private Dictionary<string, AnchorRelocResult> relocResults = new Dictionary<string, AnchorRelocResult>();

        /// <summary>처리 완료된 앵커 수 (성공+타임아웃+로드실패)</summary>
#pragma warning disable CS0414 // XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR 블록에서만 사용
        private int processedAnchorCount;
#pragma warning restore CS0414

        /// <summary>백그라운드 재인식 코루틴</summary>
        private Coroutine backgroundReanchoringCoroutine;

        /// <summary>진단 로그 파일 writer (현장 테스트용 — Logcat 없이 확인 가능)</summary>
        private StreamWriter diagWriter;
        private string DiagLogDir => Path.Combine(UnityEngine.Application.persistentDataPath, "diagnostics");
        private string DiagLogPath { get; set; }

        /// <summary>재인식 완료 여부</summary>
        public bool IsRelocalized { get; private set; }

        /// <summary>총 앵커 수</summary>
        public int TotalAnchorCount { get; private set; }

        /// <summary>재인식된 앵커 수 (호환용 — 처리 완료 수)</summary>
        public int RelocalizedAnchorCount { get; private set; }

        /// <summary>성공적으로 Tracking된 앵커 수</summary>
        public int SuccessfulAnchorCount { get; private set; }

        /// <summary>타임아웃된 앵커 수</summary>
        public int TimedOutAnchorCount { get; private set; }

        /// <summary>로드 자체가 실패한 앵커 수</summary>
        public int FailedAnchorCount { get; private set; }

        /// <summary>재인식 성공률 (0~1)</summary>
        public float RelocalizationSuccessRate =>
            TotalAnchorCount > 0 ? (float)SuccessfulAnchorCount / TotalAnchorCount : 0f;

        /// <summary>실패한(fallback 사용) waypointId 목록</summary>
        public List<string> FallbackWaypoints
        {
            get
            {
                var list = new List<string>();
                foreach (var kv in relocResults)
                {
                    if (kv.Value.state != AnchorRelocState.Tracking)
                        list.Add(kv.Key);
                }
                return list;
            }
        }

        public event Action<int, int> OnRelocalizationProgress; // (relocalized, total) — 호환용
        public event Action OnRelocalizationComplete; // 호환용

#pragma warning disable CS0067 // XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR 블록에서만 사용
        /// <summary>상세 진행 이벤트 (wpId, state, successCount, timedOutCount, total)</summary>
        public event Action<string, AnchorRelocState, int, int, int> OnRelocalizationDetailedProgress;

        /// <summary>전체 재인식 완료 (성공률)</summary>
        public event Action<float> OnRelocalizationCompleteWithRate;

        /// <summary>백그라운드 재인식 성공 (wpId, transform)</summary>
        public event Action<string, Transform> OnAnchorLateRecovered;

        /// <summary>매핑 품질 관찰 진행 이벤트 (quality 0~2, guidanceText)</summary>
        public event Action<int, string> OnMappingQualityUpdate;
#pragma warning restore CS0067

        public event Action<string, bool> OnAnchorSaved; // (waypointId, success)

        private string MappingFilePath => Path.Combine(UnityEngine.Application.persistentDataPath, mappingFileName);
        private string AnchorStoragePath => Path.Combine(UnityEngine.Application.persistentDataPath, "AnchorMaps");

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InitDiagLog();
        }

        private void Start()
        {
            // 매핑 파일이 있으면 로드
            LoadMappingFromFile();

#if XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR
            // XREAL 앵커 저장 디렉토리 설정
            if (arAnchorManager != null)
            {
                arAnchorManager.SetAndCreateAnchorMappingDirectory(AnchorStoragePath);
                Diag($"Start — MapPath set to: {AnchorStoragePath}, dirExists={Directory.Exists(AnchorStoragePath)}");
            }
            else
            {
                DiagError("Start — arAnchorManager is NULL! Anchor operations will fail.");
            }
#endif
        }

        // =====================================================
        // 진단 로그 파일 (현장 테스트용)
        // =====================================================

        /// <summary>
        /// 진단 로그 파일 초기화.
        /// 파일 위치: persistentDataPath/diagnostics/anchor_diag_{timestamp}.log
        /// Beam Pro에서 adb pull 또는 파일 탐색기로 확인 가능.
        /// </summary>
        private void InitDiagLog()
        {
            try
            {
                if (!Directory.Exists(DiagLogDir))
                    Directory.CreateDirectory(DiagLogDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                DiagLogPath = Path.Combine(DiagLogDir, $"anchor_diag_{timestamp}.log");
                diagWriter = new StreamWriter(DiagLogPath, append: true) { AutoFlush = true };
                diagWriter.WriteLine($"=== Anchor Diagnostic Log — {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                diagWriter.WriteLine($"persistentDataPath: {UnityEngine.Application.persistentDataPath}");
                diagWriter.WriteLine($"AnchorStoragePath: {AnchorStoragePath}");
                diagWriter.WriteLine($"MappingFilePath: {MappingFilePath}");
                diagWriter.WriteLine();
                Debug.Log($"[SpatialAnchorManager] Diagnostic log: {DiagLogPath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SpatialAnchorManager] Failed to init diagnostic log: {e.Message}");
            }
        }

        /// <summary>Debug.Log + 파일에 동시 기록</summary>
        private void Diag(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Debug.Log($"[SpatialAnchorManager] {message}");
            try { diagWriter?.WriteLine(line); }
            catch { /* 파일 쓰기 실패해도 Debug.Log는 유지 */ }
        }

        /// <summary>Debug.LogWarning + 파일에 동시 기록</summary>
        private void DiagWarn(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] WARN: {message}";
            Debug.LogWarning($"[SpatialAnchorManager] {message}");
            try { diagWriter?.WriteLine(line); }
            catch { }
        }

        /// <summary>Debug.LogError + 파일에 동시 기록</summary>
        private void DiagError(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] ERROR: {message}";
            Debug.LogError($"[SpatialAnchorManager] {message}");
            try { diagWriter?.WriteLine(line); }
            catch { }
        }

        /// <summary>외부 컴포넌트가 진단 .log 파일에 기록할 수 있도록 하는 public API.</summary>
        public void WriteDiagnostic(string message)
        {
            Diag(message);
        }

        /// <summary>TimedOut 상태인 웨이포인트 ID 목록을 쉼표 구분 문자열로 반환.</summary>
        private string PendingAnchorNames()
        {
            var names = new List<string>();
            foreach (var kv in relocResults)
            {
                if (kv.Value.state == AnchorRelocState.TimedOut)
                    names.Add(kv.Key);
            }
            return names.Count > 0 ? string.Join(",", names) : "none";
        }

        private void OnDestroy()
        {
            try
            {
                diagWriter?.WriteLine($"\n=== Log closed — {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                diagWriter?.Dispose();
                diagWriter = null;
            }
            catch { }
        }

        // =====================================================
        // SLAM Tracking State API
        // =====================================================

        /// <summary>SLAM tracking state query result.</summary>
        public struct SlamTrackingStatus
        {
            public bool isReady;
            public string reasonKey;
            public string userGuidance;
        }

        /// <summary>
        /// Returns the current SLAM tracking status.
        /// On device: queries XREALPlugin. In editor: always ready.
        /// </summary>
        public SlamTrackingStatus GetSlamTrackingStatus()
        {
#if XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // AR Foundation 공개 API — XRSessionSubsystem 경유
                var subsystems = new List<XRSessionSubsystem>();
                SubsystemManager.GetSubsystems(subsystems);

                if (subsystems.Count == 0)
                {
                    return new SlamTrackingStatus
                    {
                        isReady = false,
                        reasonKey = "NoSubsystem",
                        userGuidance = "XR Session not started"
                    };
                }

                var sub = subsystems[0];
                var state = sub.trackingState;
                var reason = sub.notTrackingReason;

                if (state == TrackingState.Tracking && reason == NotTrackingReason.None)
                {
                    return new SlamTrackingStatus
                    {
                        isReady = true,
                        reasonKey = "Ready",
                        userGuidance = "SLAM Ready"
                    };
                }

                string guidance = reason switch
                {
                    NotTrackingReason.Initializing
                        => "SLAM initializing... Look around slowly",
                    NotTrackingReason.Relocalizing
                        => "Relocalizing... Move to a known area",
                    NotTrackingReason.InsufficientLight
                        => "Too dark — need more light",
                    NotTrackingReason.InsufficientFeatures
                        => "Not enough visual features — look at textured surfaces",
                    NotTrackingReason.ExcessiveMotion
                        => "Moving too fast — hold still",
                    NotTrackingReason.CameraUnavailable
                        => "Camera unavailable",
                    _ => $"Not tracking ({reason})"
                };

                if (state == TrackingState.Limited && reason == NotTrackingReason.None)
                {
                    guidance = "Tracking limited — look around to improve";
                }

                return new SlamTrackingStatus
                {
                    isReady = false,
                    reasonKey = reason.ToString(),
                    userGuidance = guidance
                };
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SpatialAnchorManager] SLAM state query failed: {e.Message}");
                return new SlamTrackingStatus
                {
                    isReady = false,
                    reasonKey = "Error",
                    userGuidance = "SLAM state unknown"
                };
            }
#else
            return new SlamTrackingStatus
            {
                isReady = true,
                reasonKey = "Editor",
                userGuidance = "Editor Mode (always ready)"
            };
#endif
        }

        // =====================================================
        // 매핑 모드 API (앵커 생성/저장)
        // =====================================================

        /// <summary>
        /// 현재 카메라 위치에 앵커를 생성합니다.
        /// </summary>
        public async Task<bool> CreateAndSaveAnchor(string waypointId, string routeId, float radius, string locationName)
        {
#if XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR
            if (arAnchorManager == null)
            {
                DiagError("ARAnchorManager가 설정되지 않았습니다.");
                OnAnchorSaved?.Invoke(waypointId, false);
                return false;
            }

            // SLAM tracking state check
            var slamStatus = GetSlamTrackingStatus();
            if (!slamStatus.isReady)
            {
                DiagWarn($"Anchor creation blocked — SLAM not ready: {slamStatus.reasonKey}");
                OnAnchorSaved?.Invoke(waypointId, false);
                return false;
            }

            // 카메라 위치에 앵커 생성
            var cameraPose = new Pose(Camera.main.transform.position, Camera.main.transform.rotation);
            var anchorGO = new GameObject($"Anchor_{waypointId}");
            anchorGO.transform.SetPositionAndRotation(cameraPose.position, cameraPose.rotation);
            var arAnchor = anchorGO.AddComponent<ARAnchor>();

            // 프레임 대기 (AR Foundation이 앵커를 인식하도록)
            await Task.Delay(500);

            // 품질 관찰 단계 — 앵커 주변을 둘러보도록 유도
            await ObserveAnchorQuality(arAnchor.trackableId);

            // MapPath 불일치 방지
            EnsureMapPath();

            // 앵커 저장
            var result = await arAnchorManager.TrySaveAnchorAsync(arAnchor);
            if (result.success)
            {
                string guidStr = GuidToSdkString(result.value.guid);

                // 매핑 데이터에 추가
                var mapping = new AnchorMapping
                {
                    waypointId = waypointId,
                    anchorGuid = guidStr,
                    radius = radius,
                    locationName = locationName
                };

                var routeData = routeId == "A" ? mappingData.routeA : mappingData.routeB;

                // 기존 매핑 제거 (덮어쓰기)
                routeData.waypoints.RemoveAll(m => m.waypointId == waypointId);
                routeData.waypoints.Add(mapping);

                anchorTransforms[waypointId] = anchorGO.transform;

                Diag($"앵커 저장 완료: {waypointId} → {guidStr}");
                OnAnchorSaved?.Invoke(waypointId, true);

                // 앵커 GUID 유실 방지 — 앱 강제종료 대비 즉시 저장
                SaveMappingToFile();
                Diag($"Auto-saved mapping after anchor creation: {waypointId}");

                return true;
            }
            else
            {
                Destroy(anchorGO);
                DiagError($"앵커 저장 실패: {waypointId}");
                OnAnchorSaved?.Invoke(waypointId, false);
                return false;
            }
#else
            // 에디터에서는 시뮬레이션
            Debug.Log($"[SpatialAnchorManager] (에디터 시뮬레이션) 앵커 생성: {waypointId}");
            await Task.CompletedTask;

            var mapping = new AnchorMapping
            {
                waypointId = waypointId,
                anchorGuid = Guid.NewGuid().ToString(),
                radius = radius,
                locationName = locationName
            };

            if (mappingData == null) mappingData = new MappingFileData();

            var route = routeId == "A" ? mappingData.routeA : mappingData.routeB;
            route.waypoints.RemoveAll(m => m.waypointId == waypointId);
            route.waypoints.Add(mapping);

            // 에디터에서도 카메라 위치를 anchorTransforms에 저장 (미니맵 보정용)
            var simGO = new GameObject($"SimAnchor_{waypointId}");
            simGO.transform.position = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            anchorTransforms[waypointId] = simGO.transform;

            OnAnchorSaved?.Invoke(waypointId, true);
            return true;
#endif
        }

        /// <summary>
        /// [Obsolete] invalidId 기반 품질 추정은 SDK에서 항상 INSUFFICIENT를 반환합니다.
        /// 실제 품질 피드백은 CreateAndSaveAnchor → ObserveAnchorQuality(realTrackableId)가 담당합니다.
        /// </summary>
        [System.Obsolete("invalidId 기반 품질 추정 불가. ObserveAnchorQuality(realTrackableId)를 사용하세요.")]
        public int GetCurrentAnchorQuality()
        {
#if !UNITY_EDITOR
            return 0;
#else
            return 2; // 에디터에서는 항상 GOOD
#endif
        }

        // =====================================================
        // 실험 모드 API (앵커 로드/재인식)
        // =====================================================

        private void ResetRelocCounters(int total)
        {
            TotalAnchorCount = total;
            RelocalizedAnchorCount = 0;
            SuccessfulAnchorCount = 0;
            TimedOutAnchorCount = 0;
            FailedAnchorCount = 0;
            processedAnchorCount = 0;
            relocResults.Clear();
            IsRelocalized = false;
        }

        private void SimulateEditorReloc(string label)
        {
            Debug.Log($"[SpatialAnchorManager] (에디터) {label}: {TotalAnchorCount}개 앵커 시뮬레이션 로드");
            RelocalizedAnchorCount = TotalAnchorCount;
            SuccessfulAnchorCount = TotalAnchorCount;
            IsRelocalized = true;
            OnRelocalizationProgress?.Invoke(RelocalizedAnchorCount, TotalAnchorCount);
            OnRelocalizationComplete?.Invoke();
            OnRelocalizationCompleteWithRate?.Invoke(1f);
        }

        /// <summary>
        /// 저장된 모든 앵커를 로드합니다.
        /// </summary>
        public void LoadAllAnchors()
        {
            LoadRouteAnchors(null);
        }

        /// <summary>
        /// 특정 경로의 앵커만 로드합니다.
        /// routeId가 null이면 Route A + Route B 전체를 로드합니다.
        /// </summary>
        public void LoadRouteAnchors(string routeId)
        {
            string label = routeId ?? "All";
            Diag($"LoadRouteAnchors({label}) — JSON path: {MappingFilePath}");
            if (File.Exists(MappingFilePath))
            {
                var fileInfo = new FileInfo(MappingFilePath);
                Diag($"JSON file: size={fileInfo.Length}B, lastWrite={fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                DiagWarn($"JSON file does NOT exist: {MappingFilePath}");
            }

            if (mappingData == null)
                LoadMappingFromFile();

            if (mappingData == null)
            {
                Debug.LogWarning("[SpatialAnchorManager] 매핑 데이터가 없습니다.");
                return;
            }

            // routeId == null → 전체, 그 외 → 해당 경로만
            List<AnchorMapping> mappings;
            if (routeId == null)
            {
                mappings = new List<AnchorMapping>();
                mappings.AddRange(mappingData.routeA.waypoints);
                mappings.AddRange(mappingData.routeB.waypoints);
            }
            else
            {
                mappings = new List<AnchorMapping>(GetRouteMappings(routeId));
            }

            ResetRelocCounters(mappings.Count);

            if (TotalAnchorCount == 0)
            {
                Debug.LogWarning($"[SpatialAnchorManager] {label}: 저장된 앵커가 없습니다.");
                IsRelocalized = true;
                OnRelocalizationComplete?.Invoke();
                OnRelocalizationCompleteWithRate?.Invoke(1f);
                return;
            }

#if XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR
            LoadAnchorsFromDevice(mappings);
#else
            SimulateEditorReloc(label);
#endif
        }

        /// <summary>
        /// Condition2 시작 시 추가 경로 앵커를 로드합니다.
        /// 기존 relocResults/카운터를 유지하면서 추가 앵커를 누적 로드.
        /// </summary>
        public void LoadAdditionalRouteAnchors(string routeId)
        {
            Diag($"LoadAdditionalRouteAnchors({routeId})");

            if (mappingData == null)
            {
                Debug.LogWarning("[SpatialAnchorManager] 매핑 데이터가 없습니다.");
                return;
            }

            var newMappings = new List<AnchorMapping>();
            foreach (var m in GetRouteMappings(routeId))
            {
                // 이미 Tracking 상태인 앵커는 건너뜀
                if (relocResults.TryGetValue(m.waypointId, out var result) &&
                    result.state == AnchorRelocState.Tracking)
                {
                    Diag($"  Skip (already tracking): {m.waypointId}");
                    continue;
                }
                newMappings.Add(m);
            }

            if (newMappings.Count == 0)
            {
                Diag("LoadAdditionalRouteAnchors — 추가할 앵커 없음");
                return;
            }

            TotalAnchorCount += newMappings.Count;
            Diag($"  추가 앵커 {newMappings.Count}개 로드 시작 (전체 {TotalAnchorCount})");

#if XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR
            LoadAnchorsFromDevice(newMappings);
#else
            Debug.Log($"[SpatialAnchorManager] (에디터) 추가 Route {routeId}: {newMappings.Count}개 시뮬레이션 로드");
            foreach (var m in newMappings)
            {
                SuccessfulAnchorCount++;
                RelocalizedAnchorCount++;
                processedAnchorCount++;
                relocResults[m.waypointId] = new AnchorRelocResult
                {
                    waypointId = m.waypointId,
                    state = AnchorRelocState.Tracking,
                    elapsedTime = 0f
                };
            }
            OnRelocalizationProgress?.Invoke(processedAnchorCount, TotalAnchorCount);
#endif
        }

#if XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// SDK Provider의 MapPath가 AnchorStoragePath와 일치하도록 보장.
        /// ARAnchorManager disable/re-enable 시 Provider.Start()가 경로를
        /// persistentDataPath로 리셋하므로, 매 앵커 연산 직전에 호출.
        /// </summary>
        private void EnsureMapPath()
        {
            if (arAnchorManager != null)
            {
                arAnchorManager.SetAndCreateAnchorMappingDirectory(AnchorStoragePath);
            }
        }

        /// <summary>
        /// 앵커 진단 정보 덤프 — LoadFailed 원인을 Logcat만으로 판별 가능하도록.
        /// MapPath/디스크 파일/JSON GUID 교차검증.
        /// </summary>
        private void DumpAnchorDiagnostics(List<AnchorMapping> mappings)
        {
            Diag("=== ANCHOR DIAGNOSTICS ===");
            Diag($"  AnchorStoragePath: {AnchorStoragePath}");
            Diag($"  Directory exists: {Directory.Exists(AnchorStoragePath)}");
            Diag($"  persistentDataPath: {UnityEngine.Application.persistentDataPath}");

            // SDK가 인식하는 앵커 GUID 목록
            var sdkGuids = new List<string>();
            try
            {
                foreach (var guid in arAnchorManager.TryGetSavedAnchorIds())
                {
                    sdkGuids.Add(guid.ToString());
                }
                Diag($"  SDK saved anchor count: {sdkGuids.Count}");
                foreach (var g in sdkGuids)
                    Diag($"    SDK GUID: {g}");
            }
            catch (Exception e)
            {
                DiagWarn($"  SDK TryGetSavedAnchorIds failed: {e.Message}");
            }

            // AnchorMaps 디렉토리 내 파일 목록
            if (Directory.Exists(AnchorStoragePath))
            {
                var files = Directory.GetFiles(AnchorStoragePath);
                Diag($"  AnchorMaps files: {files.Length}");
                foreach (var f in files)
                    Diag($"    File: {Path.GetFileName(f)}");
            }
            else
            {
                DiagWarn($"  AnchorMaps directory does NOT exist!");
            }

            // persistentDataPath 루트에 GUID 파일이 있는지 검사 (MapPath 불일치 증거)
            int rootGuidCount = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(UnityEngine.Application.persistentDataPath))
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (Guid.TryParse(fileName, out _))
                    {
                        rootGuidCount++;
                        if (rootGuidCount <= 5)
                            DiagWarn($"  MISPLACED GUID in root: {Path.GetFileName(file)}");
                    }
                }
                if (rootGuidCount > 0)
                    DiagError($"  *** {rootGuidCount} GUID files found in persistentDataPath root — MapPath mismatch detected! ***");
            }
            catch (Exception e)
            {
                DiagWarn($"  Root GUID scan failed: {e.Message}");
            }

            // JSON GUID와 디스크 파일 교차검증
            int found = 0, missing = 0;
            foreach (var m in mappings)
            {
                if (string.IsNullOrEmpty(m.anchorGuid)) { missing++; continue; }

                string expectedPath = Path.Combine(AnchorStoragePath, m.anchorGuid);
                bool existsInMaps = File.Exists(expectedPath);

                string rootPath = Path.Combine(UnityEngine.Application.persistentDataPath, m.anchorGuid);
                bool existsInRoot = File.Exists(rootPath);

                if (existsInMaps) found++;
                else missing++;

                bool inSdk = sdkGuids.Contains(m.anchorGuid);

                Diag($"  Cross-check {m.waypointId}: GUID={m.anchorGuid}, " +
                     $"inMaps={existsInMaps}, inRoot={existsInRoot}, inSDK={inSdk}");

                // 엔디안 불일치 감지
                if (!existsInMaps && !inSdk)
                {
                    try
                    {
                        string swapped = GuidToSdkString(new Guid(m.anchorGuid));
                        bool swappedInMaps = File.Exists(Path.Combine(AnchorStoragePath, swapped));
                        if (swappedInMaps)
                            DiagError($"  ENDIAN MISMATCH detected: stored={m.anchorGuid} → correct={swapped}");
                    }
                    catch (FormatException) { }
                }
            }
            Diag($"  Cross-check result: {found} found, {missing} missing in AnchorMaps");

            // DomainEventBus로 기록
            DomainEventBus.Instance?.Publish(new AnchorDiagnostics(
                $"{{\"storagePath\":\"{AnchorStoragePath}\",\"dirExists\":{Directory.Exists(AnchorStoragePath).ToString().ToLower()}," +
                $"\"sdkCount\":{sdkGuids.Count},\"jsonCount\":{mappings.Count}," +
                $"\"diskFound\":{found},\"diskMissing\":{missing},\"rootMisplaced\":{rootGuidCount}}}"));

            Diag("=== END ANCHOR DIAGNOSTICS ===");
        }

        /// <summary>매핑 목록 캐시 (재시도용)</summary>
        private List<AnchorMapping> lastLoadedMappings;

        /// <summary>waypointId → TrackableId (재인식 폴링용)</summary>
        private Dictionary<string, TrackableId> pendingTrackableIds = new Dictionary<string, TrackableId>();

        private void LoadAnchorsFromDevice(List<AnchorMapping> mappings)
        {
            lastLoadedMappings = mappings;

            // MapPath 불일치 방지
            EnsureMapPath();

            // 진단 덤프
            DumpAnchorDiagnostics(mappings);

            foreach (var mapping in mappings)
            {
                if (string.IsNullOrEmpty(mapping.anchorGuid)) continue;

                // 이미 성공한 앵커는 건너뛰기 (재시도 시)
                if (relocResults.ContainsKey(mapping.waypointId) &&
                    relocResults[mapping.waypointId].state == AnchorRelocState.Tracking)
                    continue;

                var guid = SerializableGuidFromString(mapping.anchorGuid);
                if (arAnchorManager.TryLoadAnchor(guid, out XRAnchor xrAnchor))
                {
                    relocResults[mapping.waypointId] = new AnchorRelocResult
                    {
                        waypointId = mapping.waypointId,
                        state = AnchorRelocState.Pending,
                        elapsedTime = 0f
                    };
                    pendingTrackableIds[mapping.waypointId] = xrAnchor.trackableId;
                    StartCoroutine(WaitForAnchorTracking(mapping.waypointId, xrAnchor.trackableId));
                    Diag($"앵커 로드 요청: {mapping.waypointId}");
                }
                else
                {
                    string expectedFile = Path.Combine(AnchorStoragePath, mapping.anchorGuid);
                    bool fileExists = File.Exists(expectedFile);
                    var slamAtFail = GetSlamTrackingStatus();
                    DiagWarn($"앵커 로드 실패: {mapping.waypointId} " +
                             $"(GUID={mapping.anchorGuid}, expectedPath={expectedFile}, fileExists={fileExists}, SLAM={slamAtFail.reasonKey})");
                    FailedAnchorCount++;
                    relocResults[mapping.waypointId] = new AnchorRelocResult
                    {
                        waypointId = mapping.waypointId,
                        state = AnchorRelocState.LoadFailed,
                        elapsedTime = 0f
                    };
                    RecordAnchorProcessed(mapping.waypointId, AnchorRelocState.LoadFailed);
                }
            }
        }

        private IEnumerator WaitForAnchorTracking(string waypointId, TrackableId trackableId)
        {
            float timeout = anchorTrackingTimeout;
            float elapsed = 0f;
            float lastSlamLog = 0f;
            float lastRemapAttempt = 0f;

            while (elapsed < timeout)
            {
                foreach (var anchor in arAnchorManager.trackables)
                {
                    if (anchor.trackableId == trackableId &&
                        anchor.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
                    {
                        anchorTransforms[waypointId] = anchor.transform;
                        SuccessfulAnchorCount++;
                        RelocalizedAnchorCount++;

                        if (relocResults.ContainsKey(waypointId))
                        {
                            relocResults[waypointId].state = AnchorRelocState.Tracking;
                            relocResults[waypointId].elapsedTime = elapsed;
                        }

                        Diag($"앵커 재인식 완료: {waypointId} ({elapsed:F1}s)");
                        RecordAnchorProcessed(waypointId, AnchorRelocState.Tracking);
                        yield break;
                    }
                }

                // 5초마다 SLAM 상태 로그
                if (elapsed - lastSlamLog >= 5f)
                {
                    lastSlamLog = elapsed;
                    var slam = GetSlamTrackingStatus();
                    Diag($"Waiting {waypointId} ({elapsed:F0}s/{timeout:F0}s) — SLAM: {slam.reasonKey}");
                }

                // 10초마다 TryRemap으로 재인식 촉진
                if (elapsed - lastRemapAttempt >= 10f && elapsed > 0f)
                {
                    lastRemapAttempt = elapsed;
                    bool remapped = arAnchorManager.TryRemap(trackableId);
                    Diag($"TryRemap {waypointId} at {elapsed:F0}s — result={remapped}");
                }

                elapsed += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            var finalSlam = GetSlamTrackingStatus();
            Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            DiagWarn($"앵커 재인식 타임아웃: {waypointId} ({timeout}s) | " +
                $"SLAM={finalSlam.reasonKey} | camPos={camPos}");
            TimedOutAnchorCount++;
            RelocalizedAnchorCount++;

            if (relocResults.ContainsKey(waypointId))
            {
                relocResults[waypointId].state = AnchorRelocState.TimedOut;
                relocResults[waypointId].elapsedTime = timeout;
            }

            RecordAnchorProcessed(waypointId, AnchorRelocState.TimedOut);
        }

        /// <summary>앵커 처리 완료 공통 로직 — 카운터/이벤트 관리</summary>
        private void RecordAnchorProcessed(string waypointId, AnchorRelocState state)
        {
            processedAnchorCount++;

            // 호환용 이벤트
            OnRelocalizationProgress?.Invoke(processedAnchorCount, TotalAnchorCount);

            // 상세 이벤트
            OnRelocalizationDetailedProgress?.Invoke(
                waypointId, state, SuccessfulAnchorCount, TimedOutAnchorCount, TotalAnchorCount);

            if (processedAnchorCount >= TotalAnchorCount)
            {
                CheckInitialRelocalizationDone();
            }
        }

        /// <summary>초기 재인식 완료 판정 — 이벤트 발행 + 백그라운드 재인식 시작</summary>
        private void CheckInitialRelocalizationDone()
        {
            IsRelocalized = true;
            float rate = RelocalizationSuccessRate;

            // 도메인 이벤트 발행
            DomainEventBus.Instance?.Publish(new RelocalizationCompleted(rate, "complete"));

            Diag($"재인식 완료 — 성공:{SuccessfulAnchorCount}, " +
                 $"타임아웃:{TimedOutAnchorCount}, 로드실패:{FailedAnchorCount}, " +
                 $"성공률:{rate:P0}");

            // 호환용 이벤트
            OnRelocalizationComplete?.Invoke();

            // 새 이벤트
            OnRelocalizationCompleteWithRate?.Invoke(rate);

            // 실패한 앵커가 있으면 백그라운드 재인식 시작
            if (TimedOutAnchorCount > 0)
            {
                StartBackgroundReanchoring();
            }
        }

        /// <summary>실패/타임아웃된 앵커만 재시도</summary>
        public void RetryFailedAnchors()
        {
            if (lastLoadedMappings == null) return;

            var failedMappings = new List<AnchorMapping>();
            foreach (var mapping in lastLoadedMappings)
            {
                if (relocResults.TryGetValue(mapping.waypointId, out var result) &&
                    result.state != AnchorRelocState.Tracking)
                {
                    failedMappings.Add(mapping);
                }
            }

            if (failedMappings.Count == 0) return;

            // 카운터 보정
            RelocalizedAnchorCount -= (TimedOutAnchorCount + FailedAnchorCount);
            processedAnchorCount -= (TimedOutAnchorCount + FailedAnchorCount);
            TimedOutAnchorCount = 0;
            FailedAnchorCount = 0;
            IsRelocalized = false;

            StopBackgroundReanchoring();

            // MapPath 불일치 방지
            EnsureMapPath();

            Diag($"{failedMappings.Count}개 앵커 재시도");
            LoadAnchorsFromDevice(failedMappings);
        }

        /// <summary>백그라운드에서 타임아웃 앵커를 주기적으로 폴링</summary>
        public void StartBackgroundReanchoring()
        {
            StopBackgroundReanchoring();
            backgroundReanchoringCoroutine = StartCoroutine(BackgroundReanchoringLoop());
        }

        public void StopBackgroundReanchoring()
        {
            if (backgroundReanchoringCoroutine != null)
            {
                StopCoroutine(backgroundReanchoringCoroutine);
                backgroundReanchoringCoroutine = null;
            }
        }

        private IEnumerator BackgroundReanchoringLoop()
        {
            Diag("백그라운드 재인식 시작");

            int loopCount = 0;

            while (true)
            {
                yield return new WaitForSeconds(5f);
                loopCount++;

                if (loopCount % 5 == 0) // 25초마다
                {
                    var slam = GetSlamTrackingStatus();
                    Diag($"Background #{loopCount} — SLAM={slam.reasonKey}, pending={PendingAnchorNames()}");
                }

                bool allRecovered = true;

                foreach (var kv in relocResults)
                {
                    if (kv.Value.state != AnchorRelocState.TimedOut) continue;
                    allRecovered = false;

                    if (!pendingTrackableIds.TryGetValue(kv.Key, out var trackableId))
                        continue;

                    // 2회차마다 TryRemap으로 재인식 강제 시도 (10초 간격)
                    if (loopCount % 2 == 0)
                    {
                        bool remapped = arAnchorManager.TryRemap(trackableId);
                        if (remapped)
                            Diag($"Background Remap 요청: {kv.Key}");
                    }

                    foreach (var anchor in arAnchorManager.trackables)
                    {
                        if (anchor.trackableId == trackableId &&
                            anchor.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
                        {
                            anchorTransforms[kv.Key] = anchor.transform;
                            kv.Value.state = AnchorRelocState.Tracking;
                            SuccessfulAnchorCount++;
                            TimedOutAnchorCount--;

                            Diag($"백그라운드 재인식 성공: {kv.Key}");
                            OnAnchorLateRecovered?.Invoke(kv.Key, anchor.transform);
                            break;
                        }
                    }
                }

                if (allRecovered)
                {
                    Diag("백그라운드 재인식 완료 — 모든 앵커 복구됨");
                    backgroundReanchoringCoroutine = null;
                    yield break;
                }
            }
        }

        /// <summary>
        /// 앵커 생성 후 품질 관찰 단계.
        /// SDK 권장: "앵커 주변을 여러 방향으로 이동하며 5-15초 관찰".
        /// GOOD 품질 도달 시 즉시 종료, 아니면 maxDuration 후 종료.
        /// </summary>
        private async Task ObserveAnchorQuality(TrackableId trackableId, float maxDuration = 8f)
        {
            string[] guidanceMessages = {
                "Please look around slowly",
                "Move around a bit more",
                "Quality sufficient!"
            };

            float elapsed = 0f;
            int lastQuality = -1;

            OnMappingQualityUpdate?.Invoke(0, guidanceMessages[0]);

            while (elapsed < maxDuration)
            {
                var cameraPose = new Pose(Camera.main.transform.position, Camera.main.transform.rotation);
                var quality = arAnchorManager.GetAnchorQuality(trackableId, cameraPose);
                int q = (int)quality;

                if (q != lastQuality)
                {
                    lastQuality = q;
                    OnMappingQualityUpdate?.Invoke(q, guidanceMessages[q]);
                    Diag($"매핑 품질 변경: {quality} ({guidanceMessages[q]})");
                }

                if (quality == XREALAnchorEstimateQuality.XREAL_ANCHOR_ESTIMATE_QUALITY_GOOD)
                {
                    // GOOD 도달 — 1초 추가 관찰 후 종료
                    await Task.Delay(1000);
                    break;
                }

                await Task.Delay(500);
                elapsed += 0.5f;
            }

            Diag($"품질 관찰 종료 ({elapsed:F1}s, quality={lastQuality})");
        }

        /// <summary>
        /// XREAL SDK의 SaveTrackableAnchor가 반환하는 Guid는 RFC 4122 바이트가
        /// .NET Guid struct에 직접 복사된 형태. .NET은 첫 3개 그룹을 LE 정수로 해석하여
        /// ToString() 결과가 디스크 파일명과 불일치. 바이트 스왑으로 보정.
        /// </summary>
        private string GuidToSdkString(Guid guid)
        {
            var bytes = guid.ToByteArray();
            Array.Reverse(bytes, 0, 4);  // int32 그룹
            Array.Reverse(bytes, 4, 2);  // int16 그룹
            Array.Reverse(bytes, 6, 2);  // int16 그룹
            return new Guid(bytes).ToString();
        }

        private SerializableGuid SerializableGuidFromString(string guidStr)
        {
            var guid = new Guid(guidStr);
            var bytes = guid.ToByteArray();
            ulong low = BitConverter.ToUInt64(bytes, 0);
            ulong high = BitConverter.ToUInt64(bytes, 8);
            return new SerializableGuid(low, high);
        }
#endif

#if !XR_ARFOUNDATION || !UNITY_ANDROID || UNITY_EDITOR
        public void RetryFailedAnchors()
        {
            Debug.Log("[SpatialAnchorManager] (에디터) RetryFailedAnchors — 스텁");
        }

        public void StartBackgroundReanchoring()
        {
            Debug.Log("[SpatialAnchorManager] (에디터) StartBackgroundReanchoring — 스텁");
        }

        public void StopBackgroundReanchoring()
        {
            Debug.Log("[SpatialAnchorManager] (에디터) StopBackgroundReanchoring — 스텁");
        }
#endif

        // =====================================================
        // 위치 조회 API
        // =====================================================

        /// <summary>
        /// 특정 웨이포인트의 앵커 Transform을 반환합니다.
        /// </summary>
        public Transform GetAnchorTransform(string waypointId)
        {
            bool found = anchorTransforms.TryGetValue(waypointId, out Transform t);
            if (!found)
            {
                Debug.LogWarning($"[SpatialAnchorManager] GetAnchorTransform({waypointId}): " +
                    $"키 없음. anchorTransforms 키 목록=[{string.Join(", ", anchorTransforms.Keys)}]");
            }
            else if (t == null)
            {
                Debug.LogWarning($"[SpatialAnchorManager] GetAnchorTransform({waypointId}): " +
                    "키 존재하지만 Transform이 null (오브젝트 파괴됨?)");
            }
            return t;
        }

        /// <summary>
        /// 특정 웨이포인트의 앵커 위치를 반환합니다.
        /// 앵커가 없으면 null 반환.
        /// </summary>
        public Vector3? GetAnchorPosition(string waypointId)
        {
            if (anchorTransforms.TryGetValue(waypointId, out Transform t) && t != null)
                return t.position;
            return null;
        }

        /// <summary>
        /// 앵커 간 공간 정합성을 검증합니다.
        /// 앵커 간 최대 거리가 80m 초과이거나, 카메라와 가장 가까운 앵커가 50m 초과이면 false.
        /// </summary>
        public bool VerifySpatialCoherence(out string warningMessage)
        {
            var positions = new List<Vector3>();
            var names = new List<string>();
            foreach (var kv in anchorTransforms)
            {
                if (kv.Value != null)
                {
                    positions.Add(kv.Value.position);
                    names.Add(kv.Key);
                }
            }

            warningMessage = "";
            if (positions.Count < 2) return true; // 검증 불가 — 통과 처리

            // 앵커 간 최대 거리 체크
            float maxDist = 0f;
            string maxPairA = "", maxPairB = "";
            for (int i = 0; i < positions.Count; i++)
            {
                for (int j = i + 1; j < positions.Count; j++)
                {
                    float d = Vector3.Distance(positions[i], positions[j]);
                    if (d > maxDist)
                    {
                        maxDist = d;
                        maxPairA = names[i];
                        maxPairB = names[j];
                    }
                }
            }

            bool coherent = true;

            if (maxDist > 80f)
            {
                warningMessage += $"Anchor spread too large: {maxPairA}↔{maxPairB} = {maxDist:F1}m (>80m)\n";
                coherent = false;
            }

            // 카메라 ↔ 가장 가까운 앵커 거리 체크
            var cam = Camera.main;
            if (cam != null && positions.Count > 0)
            {
                float minCamDist = float.MaxValue;
                string nearestWp = "";
                for (int i = 0; i < positions.Count; i++)
                {
                    float d = Vector3.Distance(cam.transform.position, positions[i]);
                    if (d < minCamDist)
                    {
                        minCamDist = d;
                        nearestWp = names[i];
                    }
                }
                if (minCamDist > 50f)
                {
                    warningMessage += $"Camera far from nearest anchor: {nearestWp} = {minCamDist:F1}m (>50m)\n";
                    coherent = false;
                }
            }

            return coherent;
        }

        /// <summary>
        /// 실험자용 앵커 위치 요약 문자열을 반환합니다.
        /// </summary>
        public string GetAnchorPositionSummary()
        {
            var sb = new System.Text.StringBuilder();
            int count = 0;
            foreach (var kv in anchorTransforms)
            {
                if (kv.Value != null)
                {
                    var p = kv.Value.position;
                    sb.AppendLine($"  {kv.Key}: ({p.x:F1}, {p.y:F1}, {p.z:F1})");
                    count++;
                }
            }
            if (count == 0) return "  (No tracked anchors)";
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 특정 경로의 매핑 데이터를 반환합니다.
        /// </summary>
        public List<AnchorMapping> GetRouteMappings(string routeId)
        {
            if (mappingData == null) return new List<AnchorMapping>();
            return routeId == "A" ? mappingData.routeA.waypoints : mappingData.routeB.waypoints;
        }

        /// <summary>
        /// 매핑 데이터가 존재하는지 확인합니다.
        /// </summary>
        public bool HasMappingData()
        {
            return mappingData != null &&
                   (mappingData.routeA.waypoints.Count > 0 || mappingData.routeB.waypoints.Count > 0);
        }

        // =====================================================
        // JSON 매핑 파일 관리
        // =====================================================

        /// <summary>
        /// 매핑 데이터를 JSON 파일로 저장합니다.
        /// </summary>
        public void SaveMappingToFile()
        {
            if (mappingData == null) mappingData = new MappingFileData();
            mappingData.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string json = JsonUtility.ToJson(mappingData, true);
            File.WriteAllText(MappingFilePath, json);
            Diag($"매핑 파일 저장: {MappingFilePath}");
        }

        /// <summary>
        /// JSON 매핑 파일을 로드합니다.
        /// </summary>
        public void LoadMappingFromFile()
        {
            if (File.Exists(MappingFilePath))
            {
                string json = File.ReadAllText(MappingFilePath);
                mappingData = JsonUtility.FromJson<MappingFileData>(json);
                Diag($"매핑 파일 로드: Route A={mappingData.routeA.waypoints.Count}, Route B={mappingData.routeB.waypoints.Count}");
            }
            else
            {
                mappingData = new MappingFileData();
                Diag("매핑 파일 없음 — 새로 생성됩니다.");
            }

#if XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR
            MigrateEndianGuids();
#endif
        }

#if XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// 기존 매핑 데이터의 GUID 엔디안 불일치를 자동 보정.
        /// AnchorMaps/ 디렉토리의 실제 파일명과 비교하여 바이트스왑이 필요한 GUID를 수정.
        /// </summary>
        private void MigrateEndianGuids()
        {
            if (mappingData == null) return;
            if (!Directory.Exists(AnchorStoragePath)) return;

            // 디스크의 실제 앵커 파일명 수집
            var diskFiles = new HashSet<string>();
            foreach (var f in Directory.GetFiles(AnchorStoragePath))
                diskFiles.Add(Path.GetFileName(f));

            if (diskFiles.Count == 0) return;

            bool changed = false;
            var allRoutes = new[] { mappingData.routeA, mappingData.routeB };

            foreach (var route in allRoutes)
            {
                foreach (var m in route.waypoints)
                {
                    if (string.IsNullOrEmpty(m.anchorGuid)) continue;
                    if (diskFiles.Contains(m.anchorGuid)) continue; // 이미 일치

                    // 바이트스왑 시도
                    try
                    {
                        string swapped = GuidToSdkString(new Guid(m.anchorGuid));
                        if (diskFiles.Contains(swapped))
                        {
                            Diag($"GUID 마이그레이션: {m.waypointId} — {m.anchorGuid} → {swapped}");
                            m.anchorGuid = swapped;
                            changed = true;
                        }
                    }
                    catch (FormatException)
                    {
                        DiagWarn($"GUID 파싱 실패 (마이그레이션 건너뜀): {m.waypointId} — {m.anchorGuid}");
                    }
                }
            }

            if (changed)
            {
                SaveMappingToFile();
                Diag("매핑 파일 마이그레이션 완료 — JSON 재저장됨");
            }
        }
#endif

        /// <summary>
        /// 특정 웨이포인트의 앵커를 삭제합니다.
        /// </summary>
        public async Task<bool> EraseAnchor(string waypointId, string routeId)
        {
            var route = routeId == "A" ? mappingData.routeA : mappingData.routeB;
            var mapping = route.waypoints.Find(m => m.waypointId == waypointId);
            if (mapping == null) return false;

#if XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR
            if (arAnchorManager != null && !string.IsNullOrEmpty(mapping.anchorGuid))
            {
                var guid = SerializableGuidFromString(mapping.anchorGuid);
                await arAnchorManager.TryEraseAnchorAsync(guid);
            }
#else
            await Task.CompletedTask;
#endif

            route.waypoints.Remove(mapping);
            anchorTransforms.Remove(waypointId);
            Diag($"앵커 삭제: {waypointId}");
            return true;
        }
    }
}

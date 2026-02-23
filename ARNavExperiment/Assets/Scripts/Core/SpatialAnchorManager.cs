using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using ARNavExperiment.Logging;
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

        /// <summary>매핑 데이터 (waypointId → anchorGuid)</summary>
        private MappingFileData mappingData;

        /// <summary>로드된 앵커 Transform (waypointId → Transform)</summary>
        private Dictionary<string, Transform> anchorTransforms = new Dictionary<string, Transform>();

        /// <summary>현재 세션의 TrackableId (waypointId → TrackableId)</summary>
        private Dictionary<string, string> waypointToTrackableId = new Dictionary<string, string>();

        /// <summary>개별 앵커 재인식 결과</summary>
        private Dictionary<string, AnchorRelocResult> relocResults = new Dictionary<string, AnchorRelocResult>();

        /// <summary>처리 완료된 앵커 수 (성공+타임아웃+로드실패)</summary>
        private int processedAnchorCount;

        /// <summary>백그라운드 재인식 코루틴</summary>
        private Coroutine backgroundReanchoringCoroutine;

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

        /// <summary>상세 진행 이벤트 (wpId, state, successCount, timedOutCount, total)</summary>
        public event Action<string, AnchorRelocState, int, int, int> OnRelocalizationDetailedProgress;

        /// <summary>전체 재인식 완료 (성공률)</summary>
        public event Action<float> OnRelocalizationCompleteWithRate;

        /// <summary>백그라운드 재인식 성공 (wpId, transform)</summary>
        public event Action<string, Transform> OnAnchorLateRecovered;

        public event Action<string, bool> OnAnchorSaved; // (waypointId, success)

        private string MappingFilePath => Path.Combine(Application.persistentDataPath, mappingFileName);
        private string AnchorStoragePath => Path.Combine(Application.persistentDataPath, "AnchorMaps");

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
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
            }
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
                Debug.LogError("[SpatialAnchorManager] ARAnchorManager가 설정되지 않았습니다.");
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

            // 앵커 저장
            var result = await arAnchorManager.TrySaveAnchorAsync(arAnchor);
            if (result.success)
            {
                string guidStr = result.value.ToString();

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

                Debug.Log($"[SpatialAnchorManager] 앵커 저장 완료: {waypointId} → {guidStr}");
                OnAnchorSaved?.Invoke(waypointId, true);
                return true;
            }
            else
            {
                Destroy(anchorGO);
                Debug.LogError($"[SpatialAnchorManager] 앵커 저장 실패: {waypointId}");
                OnAnchorSaved?.Invoke(waypointId, false);
                return false;
            }
#else
            // 에디터에서는 시뮬레이션
            Debug.Log($"[SpatialAnchorManager] (에디터 시뮬레이션) 앵커 생성: {waypointId}");

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

            OnAnchorSaved?.Invoke(waypointId, true);
            return true;
#endif
        }

        /// <summary>
        /// 앵커 매핑 품질을 확인합니다.
        /// </summary>
        public int GetCurrentAnchorQuality()
        {
#if XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR
            if (arAnchorManager == null) return 0;

            var cameraPose = new Pose(Camera.main.transform.position, Camera.main.transform.rotation);
            // TrackableId.invalidId는 새 앵커의 품질 추정에 사용
            var quality = arAnchorManager.GetAnchorQuality(TrackableId.invalidId, cameraPose);

            switch (quality)
            {
                case XREALAnchorEstimateQuality.XREAL_ANCHOR_ESTIMATE_QUALITY_INSUFFICIENT:
                    return 0;
                case XREALAnchorEstimateQuality.XREAL_ANCHOR_ESTIMATE_QUALITY_SUFFICIENT:
                    return 1;
                case XREALAnchorEstimateQuality.XREAL_ANCHOR_ESTIMATE_QUALITY_GOOD:
                    return 2;
                default:
                    return 0;
            }
#else
            return 2; // 에디터에서는 항상 GOOD
#endif
        }

        // =====================================================
        // 실험 모드 API (앵커 로드/재인식)
        // =====================================================

        /// <summary>
        /// 저장된 모든 앵커를 로드합니다.
        /// </summary>
        public void LoadAllAnchors()
        {
            if (mappingData == null)
            {
                LoadMappingFromFile();
            }

            if (mappingData == null)
            {
                Debug.LogWarning("[SpatialAnchorManager] 매핑 데이터가 없습니다. 매핑 모드를 먼저 실행하세요.");
                return;
            }

            var allMappings = new List<AnchorMapping>();
            allMappings.AddRange(mappingData.routeA.waypoints);
            allMappings.AddRange(mappingData.routeB.waypoints);
            TotalAnchorCount = allMappings.Count;
            RelocalizedAnchorCount = 0;
            SuccessfulAnchorCount = 0;
            TimedOutAnchorCount = 0;
            FailedAnchorCount = 0;
            processedAnchorCount = 0;
            relocResults.Clear();
            IsRelocalized = false;

            if (TotalAnchorCount == 0)
            {
                Debug.LogWarning("[SpatialAnchorManager] 저장된 앵커가 없습니다.");
                IsRelocalized = true;
                OnRelocalizationComplete?.Invoke();
                OnRelocalizationCompleteWithRate?.Invoke(1f);
                return;
            }

#if XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR
            LoadAnchorsFromDevice(allMappings);
#else
            // 에디터: 즉시 재인식 완료 시뮬레이션
            Debug.Log($"[SpatialAnchorManager] (에디터) {TotalAnchorCount}개 앵커 시뮬레이션 로드");
            RelocalizedAnchorCount = TotalAnchorCount;
            SuccessfulAnchorCount = TotalAnchorCount;
            IsRelocalized = true;
            OnRelocalizationProgress?.Invoke(RelocalizedAnchorCount, TotalAnchorCount);
            OnRelocalizationComplete?.Invoke();
            OnRelocalizationCompleteWithRate?.Invoke(1f);
#endif
        }

#if XR_ARFOUNDATION && UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>매핑 목록 캐시 (재시도용)</summary>
        private List<AnchorMapping> lastLoadedMappings;

        /// <summary>waypointId → TrackableId (재인식 폴링용)</summary>
        private Dictionary<string, TrackableId> pendingTrackableIds = new Dictionary<string, TrackableId>();

        private void LoadAnchorsFromDevice(List<AnchorMapping> mappings)
        {
            lastLoadedMappings = mappings;

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
                    Debug.Log($"[SpatialAnchorManager] 앵커 로드 요청: {mapping.waypointId}");
                }
                else
                {
                    Debug.LogWarning($"[SpatialAnchorManager] 앵커 로드 실패: {mapping.waypointId} ({mapping.anchorGuid})");
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
            float timeout = 30f;
            float elapsed = 0f;

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

                        Debug.Log($"[SpatialAnchorManager] 앵커 재인식 완료: {waypointId} ({elapsed:F1}s)");
                        RecordAnchorProcessed(waypointId, AnchorRelocState.Tracking);
                        yield break;
                    }
                }

                elapsed += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            Debug.LogWarning($"[SpatialAnchorManager] 앵커 재인식 타임아웃: {waypointId}");
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

            // 로그 기록
            EventLogger.Instance?.LogEvent("RELOCALIZATION_COMPLETE", extraData:
                $"{{\"success_rate\":{rate:F2},\"success\":{SuccessfulAnchorCount}," +
                $"\"timed_out\":{TimedOutAnchorCount},\"failed\":{FailedAnchorCount}," +
                $"\"total\":{TotalAnchorCount}}}");

            Debug.Log($"[SpatialAnchorManager] 재인식 완료 — 성공:{SuccessfulAnchorCount}, " +
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

            Debug.Log($"[SpatialAnchorManager] {failedMappings.Count}개 앵커 재시도");
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
            Debug.Log("[SpatialAnchorManager] 백그라운드 재인식 시작");

            while (true)
            {
                yield return new WaitForSeconds(5f);

                bool allRecovered = true;

                foreach (var kv in new Dictionary<string, AnchorRelocResult>(relocResults))
                {
                    if (kv.Value.state != AnchorRelocState.TimedOut) continue;
                    allRecovered = false;

                    if (!pendingTrackableIds.TryGetValue(kv.Key, out var trackableId))
                        continue;

                    foreach (var anchor in arAnchorManager.trackables)
                    {
                        if (anchor.trackableId == trackableId &&
                            anchor.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
                        {
                            anchorTransforms[kv.Key] = anchor.transform;
                            kv.Value.state = AnchorRelocState.Tracking;
                            SuccessfulAnchorCount++;
                            TimedOutAnchorCount--;

                            Debug.Log($"[SpatialAnchorManager] 백그라운드 재인식 성공: {kv.Key}");
                            OnAnchorLateRecovered?.Invoke(kv.Key, anchor.transform);
                            break;
                        }
                    }
                }

                if (allRecovered)
                {
                    Debug.Log("[SpatialAnchorManager] 백그라운드 재인식 완료 — 모든 앵커 복구됨");
                    backgroundReanchoringCoroutine = null;
                    yield break;
                }
            }
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

        // =====================================================
        // 위치 조회 API
        // =====================================================

        /// <summary>
        /// 특정 웨이포인트의 앵커 Transform을 반환합니다.
        /// </summary>
        public Transform GetAnchorTransform(string waypointId)
        {
            anchorTransforms.TryGetValue(waypointId, out Transform t);
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
            Debug.Log($"[SpatialAnchorManager] 매핑 파일 저장: {MappingFilePath}");
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
                Debug.Log($"[SpatialAnchorManager] 매핑 파일 로드: Route A={mappingData.routeA.waypoints.Count}, Route B={mappingData.routeB.waypoints.Count}");
            }
            else
            {
                mappingData = new MappingFileData();
                Debug.Log("[SpatialAnchorManager] 매핑 파일 없음 — 새로 생성됩니다.");
            }
        }

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
            Debug.Log($"[SpatialAnchorManager] 앵커 삭제: {waypointId}");
            return true;
        }
    }
}

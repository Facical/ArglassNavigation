using System;
using System.Collections.Generic;
using UnityEngine;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;
using ARNavExperiment.Navigation;
#if XR_ARFOUNDATION
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

namespace ARNavExperiment.Core
{
    /// <summary>
    /// Image Tracking 기반 SLAM↔도면 좌표 정렬.
    /// ARTrackedImageManager에서 마커 감지 시, 해당 마커의 SLAM 좌표와
    /// 알려진 도면 좌표를 매핑하여 WaypointManager에 가상 앵커 쌍으로 주입합니다.
    ///
    /// 기존 보정/도착 감지 로직을 그대로 활용 — 최소 코드 변경.
    /// </summary>
    public class ImageTrackingAligner : MonoBehaviour
    {
        public static ImageTrackingAligner Instance { get; private set; }

#if XR_ARFOUNDATION
        [Header("AR Foundation")]
        [SerializeField] private ARTrackedImageManager trackedImageManager;
#endif

        [Header("Data")]
        [SerializeField] private MarkerMappingData markerMapping;

        [Header("Settings")]
        [Tooltip("정렬 완료로 간주하는 최소 마커 감지 수")]
        [SerializeField] private int minMarkersForAlignment = 1;

        [Tooltip("Heading 보정에 마커 방향 사용")]
        [SerializeField] private bool useMarkerHeading = true;

        /// <summary>정렬 완료 시 발행 (successRate: 감지된 마커 / 전체 마커)</summary>
        public event Action<float> OnAlignmentComplete;

        /// <summary>마커 감지 시 발행 (markerName, detected count)</summary>
        public event Action<string, int> OnMarkerDetected;

        /// <summary>현재 감지된 마커 수</summary>
        public int DetectedMarkerCount => detectedMarkers.Count;

        /// <summary>전체 등록된 마커 수</summary>
        public int TotalMarkerCount => markerMapping != null ? markerMapping.mappings.Count : 0;

        /// <summary>트래킹 활성 여부</summary>
        public bool IsTracking { get; private set; }

        // 감지된 마커 → (SLAM 좌표, 도면 좌표) 쌍
        private List<(Vector2 slamXZ, Vector2 fallbackXZ)> detectedPairs = new();
        private HashSet<string> detectedMarkers = new();
        private bool alignmentCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

#if XR_ARFOUNDATION
        /// <summary>
        /// Image Tracking을 시작합니다. ARTrackedImageManager를 활성화하고 이벤트를 구독합니다.
        /// </summary>
        public void StartTracking()
        {
            // ARTrackedImageManager 자동 탐색
            if (trackedImageManager == null)
            {
                trackedImageManager = FindObjectOfType<ARTrackedImageManager>();
                if (trackedImageManager == null)
                {
                    Debug.LogError("[ImageTrackingAligner] ARTrackedImageManager를 찾을 수 없습니다!");
                    return;
                }
            }

            // 마커 매핑 데이터 확인
            if (markerMapping == null || markerMapping.mappings.Count == 0)
            {
                Debug.LogError("[ImageTrackingAligner] MarkerMappingData가 설정되지 않았거나 비어 있습니다!");
                return;
            }

            // XRReferenceImageLibrary 설정
            if (trackedImageManager.referenceLibrary == null)
            {
                Debug.LogError("[ImageTrackingAligner] ARTrackedImageManager에 ReferenceImageLibrary가 설정되지 않았습니다!");
                return;
            }

            // 상태 초기화
            detectedPairs.Clear();
            detectedMarkers.Clear();
            alignmentCompleted = false;

            // 이벤트 구독
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;

            // ARTrackedImageManager 활성화
            trackedImageManager.enabled = true;
            IsTracking = true;

            Debug.Log($"[ImageTrackingAligner] Tracking started — {markerMapping.mappings.Count} markers registered, " +
                $"library has {trackedImageManager.referenceLibrary.count} images");
        }

        /// <summary>Image Tracking을 중지합니다.</summary>
        public void StopTracking()
        {
            if (trackedImageManager != null)
            {
                trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
                // ARTrackedImageManager는 비활성화하지 않음 — 다른 시스템에서 사용할 수 있으므로
            }

            IsTracking = false;
            Debug.Log($"[ImageTrackingAligner] Tracking stopped — {detectedMarkers.Count} markers detected");
        }

        private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
        {
            foreach (var image in args.added)
                ProcessDetectedImage(image);

            // 위치 업데이트 — 드리프트 보정
            foreach (var image in args.updated)
            {
                if (image.trackingState == TrackingState.Tracking)
                    ProcessUpdatedImage(image);
            }
        }

        private void ProcessDetectedImage(ARTrackedImage image)
        {
            string imageName = image.referenceImage.name;
            var mapping = markerMapping.GetByName(imageName);
            if (mapping == null)
            {
                Debug.LogWarning($"[ImageTrackingAligner] Unknown marker: {imageName}");
                return;
            }

            bool isNewMarker = detectedMarkers.Add(imageName);
            if (!isNewMarker) return; // 이미 감지된 마커

            var slamXZ = new Vector2(image.transform.position.x, image.transform.position.z);
            var fallbackXZ = new Vector2(mapping.floorPlanPosition.x, mapping.floorPlanPosition.z);
            detectedPairs.Add((slamXZ, fallbackXZ));

            Debug.Log($"[ImageTrackingAligner] Marker detected: {imageName} → {mapping.waypointId}, " +
                $"SLAM({slamXZ}), FloorPlan({fallbackXZ}), total={detectedMarkers.Count}");

            // Heading 보정: 첫 번째 마커의 방향에서 heading offset 추출
            if (useMarkerHeading && detectedMarkers.Count == 1)
            {
                float slamYaw = image.transform.eulerAngles.y;
                float mapYaw = mapping.markerHeading;
                float headingOffset = slamYaw - mapYaw;
                WaypointManager.Instance?.SetHeadingCalibrationOffset(headingOffset);
                Debug.Log($"[ImageTrackingAligner] Heading from marker: slamYaw={slamYaw:F1}°, " +
                    $"mapYaw={mapYaw:F1}°, offset={headingOffset:F1}°");
            }

            // WaypointManager에 가상 앵커 쌍 주입
            WaypointManager.Instance?.InjectCalibratedPairs(detectedPairs);

            // 도메인 이벤트 발행
            DomainEventBus.Instance?.Publish(new ImageMarkerDetected(
                imageName, mapping.waypointId,
                image.transform.position.ToString("F2"),
                detectedMarkers.Count == 1 ? (image.transform.eulerAngles.y - mapping.markerHeading) : 0f));

            // 이벤트 알림
            OnMarkerDetected?.Invoke(imageName, detectedMarkers.Count);

            // 정렬 완료 체크
            if (!alignmentCompleted && detectedMarkers.Count >= minMarkersForAlignment)
            {
                alignmentCompleted = true;
                float rate = (float)detectedMarkers.Count / markerMapping.mappings.Count;
                OnAlignmentComplete?.Invoke(rate);
                Debug.Log($"[ImageTrackingAligner] Alignment complete! " +
                    $"{detectedMarkers.Count}/{markerMapping.mappings.Count} markers");
            }
        }

        private void ProcessUpdatedImage(ARTrackedImage image)
        {
            string imageName = image.referenceImage.name;
            var mapping = markerMapping.GetByName(imageName);
            if (mapping == null || !detectedMarkers.Contains(imageName)) return;

            var newSlamXZ = new Vector2(image.transform.position.x, image.transform.position.z);
            var fallbackXZ = new Vector2(mapping.floorPlanPosition.x, mapping.floorPlanPosition.z);

            // 기존 쌍의 SLAM 좌표 업데이트
            for (int i = 0; i < detectedPairs.Count; i++)
            {
                if (Vector2.Distance(detectedPairs[i].fallbackXZ, fallbackXZ) < 0.1f)
                {
                    detectedPairs[i] = (newSlamXZ, fallbackXZ);
                    break;
                }
            }

            // 보정 재계산
            WaypointManager.Instance?.InjectCalibratedPairs(detectedPairs);
        }

        private void OnDestroy()
        {
            if (trackedImageManager != null)
                trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }

#else
        // XR_ARFOUNDATION 미정의 시 stub
        public void StartTracking()
        {
            IsTracking = true;
            Debug.LogWarning("[ImageTrackingAligner] XR_ARFOUNDATION not defined — Image Tracking unavailable");
        }

        public void StopTracking()
        {
            IsTracking = false;
        }
#endif
    }
}

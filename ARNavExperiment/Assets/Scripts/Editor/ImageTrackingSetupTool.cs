using UnityEngine;
using UnityEditor;

namespace ARNavExperiment.EditorTools
{
    /// <summary>
    /// Image Tracking용 마커 매핑 데이터 생성 에디터 도구.
    /// XRReferenceImageLibrary는 Unity 에디터에서 수동 생성해야 합니다:
    /// Assets > Create > XR > Reference Image Library
    /// </summary>
    public class ImageTrackingSetupTool
    {
        private const string DATA_PATH = "Assets/Data/ImageTracking";

        [MenuItem("ARNav/Setup Image Tracking")]
        public static void SetupImageTracking()
        {
            SetupImageTrackingSilent();
            EditorUtility.DisplayDialog("Image Tracking 설정",
                "MarkerMappingData 에셋이 생성되었습니다!\n\n" +
                "다음 단계:\n" +
                "1. Assets/Data/ImageTracking/ 에 마커 이미지(PNG) 추가\n" +
                "2. Assets > Create > XR > Reference Image Library 생성\n" +
                "3. 라이브러리에 마커 이미지 등록 (크기: 0.21m = A4 폭)\n" +
                "4. MarkerMappingData에서 마커↔웨이포인트 매핑 설정\n" +
                "5. ImageTrackingAligner에 라이브러리 + 매핑 연결\n\n" +
                "씬을 저장하세요 (Cmd+S)", "확인");
        }

        public static void SetupImageTrackingSilent()
        {
            // 폴더 생성
            if (!AssetDatabase.IsValidFolder(DATA_PATH))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data"))
                    AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.CreateFolder("Assets/Data", "ImageTracking");
            }

            // MarkerMappingData 에셋 생성 (없으면)
            string mappingPath = $"{DATA_PATH}/MarkerMappingData.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Core.MarkerMappingData>(mappingPath);
            if (existing == null)
            {
                var mappingData = ScriptableObject.CreateInstance<Core.MarkerMappingData>();

                // 기본 마커 매핑 템플릿 (사용자가 수정 필요)
                mappingData.mappings.Add(new Core.MarkerWaypointMapping
                {
                    markerName = "MARKER_WP01",
                    waypointId = "B_WP01",
                    floorPlanPosition = new Vector3(36, 0, 18),  // WP01 fallback 좌표
                    markerHeading = 0f
                });
                mappingData.mappings.Add(new Core.MarkerWaypointMapping
                {
                    markerName = "MARKER_WP04",
                    waypointId = "B_WP04",
                    floorPlanPosition = new Vector3(36, 0, 54),  // WP04 fallback 좌표
                    markerHeading = 0f
                });
                mappingData.mappings.Add(new Core.MarkerWaypointMapping
                {
                    markerName = "MARKER_WP07",
                    waypointId = "B_WP07",
                    floorPlanPosition = new Vector3(36, 0, 76),  // WP07 fallback 좌표
                    markerHeading = 0f
                });

                AssetDatabase.CreateAsset(mappingData, mappingPath);
                Debug.Log($"[ImageTrackingSetup] MarkerMappingData 에셋 생성: {mappingPath}");
            }
            else
            {
                Debug.Log($"[ImageTrackingSetup] MarkerMappingData 에셋 이미 존재: {mappingPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ImageTrackingSetup] Image Tracking 설정 완료!");
        }
    }
}

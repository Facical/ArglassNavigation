using UnityEngine;
using UnityEditor;

#if XR_ARFOUNDATION
using UnityEngine.XR.ARSubsystems;
using UnityEditor.XR.ARSubsystems;
#endif

namespace ARNavExperiment.EditorTools
{
    /// <summary>
    /// Image Tracking 자동 셋업 도구.
    /// 1. MarkerMappingData SO 생성
    /// 2. XRReferenceImageLibrary 자동 생성 + 마커 PNG 자동 등록
    /// 3. ARTrackedImageManager.referenceLibrary 자동 할당
    /// </summary>
    public class ImageTrackingSetupTool
    {
        private const string DATA_PATH = "Assets/Data/ImageTracking";
        private const string LIB_PATH = "Assets/Data/ImageTracking/MarkerLibrary.asset";
        private const float MARKER_SIZE_METERS = 0.126f; // ArUco 마커 물리 크기 126mm

        [MenuItem("ARNav/Setup Image Tracking")]
        public static void SetupImageTracking()
        {
            SetupImageTrackingSilent();
            EditorUtility.DisplayDialog("Image Tracking 설정",
                "Image Tracking 설정 완료!\n\n" +
                "생성된 에셋:\n" +
                "- MarkerMappingData (마커\u2194웨이포인트 매핑)\n" +
#if XR_ARFOUNDATION
                "- MarkerLibrary (XRReferenceImageLibrary)\n" +
#endif
                "\n다음 단계:\n" +
                "1. ExperimentManager Inspector에서 useImageTracking = true\n" +
                "2. 씬 저장 (Cmd+S)", "확인");
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
            CreateMarkerMappingData();

#if XR_ARFOUNDATION
            // XRReferenceImageLibrary 생성 + 마커 등록
            CreateReferenceImageLibrary();

            // ARTrackedImageManager에 라이브러리 할당
            AssignLibraryToManager();
#else
            Debug.LogWarning("[ImageTrackingSetup] XR_ARFOUNDATION define 없음 \u2014 " +
                "XRReferenceImageLibrary 자동 생성 건너뜀. Android 빌드에서는 자동 적용됩니다.");
#endif

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ImageTrackingSetup] Image Tracking 설정 완료!");
        }

        private static void CreateMarkerMappingData()
        {
            string mappingPath = $"{DATA_PATH}/MarkerMappingData.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Core.MarkerMappingData>(mappingPath);
            if (existing != null)
            {
                Debug.Log($"[ImageTrackingSetup] MarkerMappingData 에셋 이미 존재: {mappingPath}");
                return;
            }

            var mappingData = ScriptableObject.CreateInstance<Core.MarkerMappingData>();

            mappingData.mappings.Add(new Core.MarkerWaypointMapping
            {
                markerName = "MARKER_WP01",
                waypointId = "B_WP01",
                floorPlanPosition = new Vector3(36, 0, 18),
                markerHeading = 90f
            });
            mappingData.mappings.Add(new Core.MarkerWaypointMapping
            {
                markerName = "MARKER_B101E",
                waypointId = "B_WP05",
                floorPlanPosition = new Vector3(39, 0, 66),
                markerHeading = 270f
            });
            mappingData.mappings.Add(new Core.MarkerWaypointMapping
            {
                markerName = "MARKER_WP07",
                waypointId = "B_WP07",
                floorPlanPosition = new Vector3(36, 0, 48),
                markerHeading = 90f
            });

            AssetDatabase.CreateAsset(mappingData, mappingPath);
            Debug.Log($"[ImageTrackingSetup] MarkerMappingData 에셋 생성: {mappingPath}");
        }

#if XR_ARFOUNDATION
        private static void CreateReferenceImageLibrary()
        {
            var existingLib = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LIB_PATH);
            if (existingLib != null)
            {
                Debug.Log($"[ImageTrackingSetup] XRReferenceImageLibrary 이미 존재: {LIB_PATH}");
                AddMissingMarkers(existingLib);
                return;
            }

            var lib = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
            AssetDatabase.CreateAsset(lib, LIB_PATH);
            Debug.Log($"[ImageTrackingSetup] XRReferenceImageLibrary 생성: {LIB_PATH}");

            AddMissingMarkers(lib);
        }

        private static void AddMissingMarkers(XRReferenceImageLibrary lib)
        {
            string[] markerNames = { "MARKER_WP01", "MARKER_B101E", "MARKER_WP07" };

            foreach (var markerName in markerNames)
            {
                // 이미 등록된 마커인지 확인
                bool exists = false;
                for (int i = 0; i < lib.count; i++)
                {
                    if (lib[i].name == markerName)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                {
                    Debug.Log($"[ImageTrackingSetup] 마커 이미 등록됨: {markerName}");
                    continue;
                }

                // 마커 이미지 로드
                string texPath = $"{DATA_PATH}/{markerName}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null)
                {
                    Debug.LogWarning($"[ImageTrackingSetup] 마커 이미지 없음: {texPath}");
                    continue;
                }

                // TextureImporter에서 readable 설정
                var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }

                // XRReferenceImageLibraryExtensions API 사용
                lib.Add();
                int idx = lib.count - 1;
                lib.SetName(idx, markerName);
                lib.SetTexture(idx, tex, true);
                lib.SetSpecifySize(idx, true);
                lib.SetSize(idx, new Vector2(MARKER_SIZE_METERS, MARKER_SIZE_METERS));

                Debug.Log($"[ImageTrackingSetup] 마커 등록: {markerName} (크기: {MARKER_SIZE_METERS}m)");
            }

            EditorUtility.SetDirty(lib);
        }

        private static void AssignLibraryToManager()
        {
            var manager = Object.FindObjectOfType<UnityEngine.XR.ARFoundation.ARTrackedImageManager>(true);
            if (manager == null)
            {
                Debug.LogWarning("[ImageTrackingSetup] ARTrackedImageManager가 씬에 없습니다. " +
                    "XR Origin 설정 후 다시 실행하세요.");
                return;
            }

            var lib = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LIB_PATH);
            if (lib == null) return;

            manager.referenceLibrary = lib;
            EditorUtility.SetDirty(manager);
            Debug.Log("[ImageTrackingSetup] ARTrackedImageManager.referenceLibrary 할당 완료");
        }
#endif
    }
}

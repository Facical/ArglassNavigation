using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
#if XR_INTERACTION
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif

namespace ARNavExperiment.EditorTools
{
    public class XROriginSetupTool
    {
        // XRI Starter Assets 샘플 경로 (Import 후 생성되는 위치)
        private const string XRI_INPUT_ACTIONS_PATH =
            "Assets/Samples/XR Interaction Toolkit/2.5.4/Starter Assets/XRI Default Input Actions.inputactions";

        // 대안 경로 (버전 번호가 다를 경우)
        private static readonly string[] XRI_ALTERNATIVE_PATHS = new[]
        {
            "Assets/Samples/XR Interaction Toolkit/2.5.4/Starter Assets/XRI Default Input Actions.inputactions",
            "Assets/Samples/XR Interaction Toolkit/2.5.3/Starter Assets/XRI Default Input Actions.inputactions",
            "Assets/Samples/XR Interaction Toolkit/2.5.2/Starter Assets/XRI Default Input Actions.inputactions",
        };

        [MenuItem("ARNav/Setup XR Origin (XREAL)")]
        public static void SetupXROrigin()
        {
            // Check if XR Origin already exists
            if (Object.FindObjectOfType<XROrigin>() != null)
            {
                if (!EditorUtility.DisplayDialog("XR Origin 존재",
                    "이미 XR Origin이 씬에 있습니다. 새로 생성하시겠습니까?", "새로 생성", "취소"))
                    return;
            }

            // Remove default Main Camera if exists
            var defaultCam = GameObject.Find("Main Camera");
            if (defaultCam != null && defaultCam.GetComponent<XROrigin>() == null)
            {
                if (EditorUtility.DisplayDialog("기본 카메라",
                    "기본 Main Camera를 삭제하고 XR Origin으로 교체하시겠습니까?", "삭제", "유지"))
                {
                    Undo.DestroyObjectImmediate(defaultCam);
                }
            }

            SetupXROriginSilent();

            EditorUtility.DisplayDialog("완료",
                "XR Origin (XREAL) 구성 완료!\n\n" +
                "구성:\n" +
                "- AR Session (ARSession + ARInputManager)\n" +
                "- XR Origin + XR Interaction Manager\n" +
                "- Camera Offset / Main Camera\n" +
                "- TrackedPoseDriver (머리 추적)\n" +
                "- ARAnchorManager (Spatial Anchor)\n" +
                "- InputActionManager (액션 자동 Enable)\n" +
                "- XREAL 핸드트래킹 바인딩 자동 추가\n" +
                "- 투명 배경 (AR 모드)\n\n" +
                "씬을 저장하세요 (Cmd+S)", "확인");
        }

        /// <summary>확인 다이얼로그 없이 실행 (MasterSetupTool에서 호출용)</summary>
        public static void SetupXROriginSilent()
        {
            // === InputActionAsset 로드 (먼저 확인) ===
            var xriInputActions = LoadXRIInputActions();
            if (xriInputActions == null)
            {
                Debug.LogError(
                    "[XROriginSetup] XRI Default Input Actions 에셋을 찾을 수 없습니다!\n" +
                    "Window > Package Manager > XR Interaction Toolkit > Samples > 'Starter Assets' Import 후 다시 실행하세요.");
                return;
            }

            // === XREAL 핸드트래킹 바인딩 자동 추가 ===
            AddXREALBindings(xriInputActions);

            // 기존 XR Origin 모두 삭제 (복수)
            var existingOrigins = Object.FindObjectsOfType<XROrigin>();
            foreach (var origin in existingOrigins)
                Undo.DestroyObjectImmediate(origin.gameObject);

            // "XR Origin (XREAL)" 이름으로도 검색하여 삭제
            GameObject xrGO;
            while ((xrGO = GameObject.Find("XR Origin (XREAL)")) != null)
                Undo.DestroyObjectImmediate(xrGO);

            // 기존 Reticle(Clone) 정리 (XRInteractorReticleVisual이 에디터에서 인스턴스를 생성한 잔여물)
            CleanupReticleClones();

            // 기본 Main Camera 삭제
            var defaultCam = GameObject.Find("Main Camera");
            if (defaultCam != null)
                Undo.DestroyObjectImmediate(defaultCam);

            // 기존 InputActionManager 삭제 (중복 방지)
            var existingIAM = GameObject.Find("Input Action Manager");
            if (existingIAM != null)
                Undo.DestroyObjectImmediate(existingIAM);

            // === XR Origin (Root) ===
            var xrOriginGO = new GameObject("XR Origin (XREAL)");
            Undo.RegisterCreatedObjectUndo(xrOriginGO, "Create XR Origin");

            var xrOrigin = xrOriginGO.AddComponent<XROrigin>();
            xrOriginGO.AddComponent<XRInteractionManager>();

            // === Camera Offset ===
            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(xrOriginGO.transform, false);
            xrOrigin.CameraFloorOffsetObject = cameraOffset;

            // === Main Camera ===
            var cameraGO = new GameObject("Main Camera");
            cameraGO.transform.SetParent(cameraOffset.transform, false);
            cameraGO.tag = "MainCamera";

            // Camera component
            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0); // transparent for AR
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            // Audio Listener
            cameraGO.AddComponent<AudioListener>();

            // Tracked Pose Driver (head tracking) — InputActionReference 기반 참조 (m_UseReference=1)
            var trackedPose = cameraGO.AddComponent<TrackedPoseDriver>();
            var headPosRef = FindInputActionReference(xriInputActions, "XRI Head", "Position");
            var headRotRef = FindInputActionReference(xriInputActions, "XRI Head", "Rotation");
            if (headPosRef != null && headRotRef != null)
            {
                trackedPose.positionInput = new InputActionProperty(headPosRef);
                trackedPose.rotationInput = new InputActionProperty(headRotRef);
                Debug.Log("[XROriginSetup] TrackedPoseDriver: InputActionReference 기반 XRI Head 설정 완료 (m_UseReference=1)");
            }
            else
            {
                // 폴백: 인라인 InputAction (XRI Head InputActionReference가 없는 경우)
                Debug.LogWarning("[XROriginSetup] 'XRI Head' InputActionReference 없음 — 인라인 InputAction 폴백 사용");
                trackedPose.positionInput = new InputActionProperty(
                    new InputAction("Position", InputActionType.Value, "<XRHMD>/centerEyePosition"));
                trackedPose.rotationInput = new InputActionProperty(
                    new InputAction("Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));
            }

            // Set XR Origin camera reference
            xrOrigin.Camera = camera;

            // === XR Origin Settings ===
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;

            // === AR Session (AR Foundation 생명주기 — 별도 GameObject 필수) ===
#if XR_ARFOUNDATION
            // 기존 ARSession 제거
            var existingSession = Object.FindObjectOfType<UnityEngine.XR.ARFoundation.ARSession>();
            if (existingSession != null)
                Undo.DestroyObjectImmediate(existingSession.gameObject);

            var arSessionGO = new GameObject("AR Session");
            Undo.RegisterCreatedObjectUndo(arSessionGO, "Create AR Session");
            arSessionGO.AddComponent<UnityEngine.XR.ARFoundation.ARSession>();
            arSessionGO.AddComponent<UnityEngine.XR.ARFoundation.ARInputManager>();
#endif

            // === AR Anchor Manager (Spatial Anchor용) ===
#if XR_ARFOUNDATION
            // ARSessionOrigin은 XROrigin과 중복 → 추가하지 않음 (deprecated)
            if (xrOriginGO.GetComponent<UnityEngine.XR.ARFoundation.ARAnchorManager>() == null)
                xrOriginGO.AddComponent<UnityEngine.XR.ARFoundation.ARAnchorManager>();

            // === AR Tracked Image Manager (Image Tracking 보정용) ===
            if (xrOriginGO.GetComponent<UnityEngine.XR.ARFoundation.ARTrackedImageManager>() == null)
            {
                var trackedImageMgr = xrOriginGO.AddComponent<UnityEngine.XR.ARFoundation.ARTrackedImageManager>();
                trackedImageMgr.enabled = false; // ImageTrackingAligner.StartTracking()에서 활성화
            }
#endif

            // === XREAL Session Manager ===
#if UNITY_ANDROID
            var sessionMgr = xrOriginGO.AddComponent<Unity.XR.XREAL.XREALSessionManager>();
#endif

            // === InputActionManager (모든 InputAction 자동 Enable) ===
#if XR_INTERACTION
            var iamGO = new GameObject("Input Action Manager");
            iamGO.transform.SetParent(xrOriginGO.transform, false);
            Undo.RegisterCreatedObjectUndo(iamGO, "Create InputActionManager");
            var inputActionManager = iamGO.AddComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
            // XRI Default Input Actions 등록
            inputActionManager.actionAssets =
                new System.Collections.Generic.List<InputActionAsset> { xriInputActions };
            Debug.Log("[XROriginSetup] InputActionManager 생성 — XRI Default Input Actions 등록 완료");
#else
            Debug.LogWarning("[XROriginSetup] XR_INTERACTION define 누락 — InputActionManager 생성 건너뜀");
#endif

            // === Hand Tracking (XR Interaction) ===
            // XREAL Reticle 프리팹 로드 (양손 공용)
#if XR_INTERACTION
            var reticlePrefab = LoadReticlePrefab();
#endif

            // === InputActionAsset에서 Hand 액션맵 로드 ===
            // XRI 2.x 구조: "XRI RightHand" (Aim Position/Rotation) + "XRI RightHand Interaction" (Select/UIPress)
            InputActionMap rightHandMap = xriInputActions.FindActionMap("XRI RightHand");
            InputActionMap rightInteractionMap = xriInputActions.FindActionMap("XRI RightHand Interaction");
            InputActionMap leftHandMap = xriInputActions.FindActionMap("XRI LeftHand");
            InputActionMap leftInteractionMap = xriInputActions.FindActionMap("XRI LeftHand Interaction");

            bool hasRightMaps = rightHandMap != null && rightInteractionMap != null;
            bool hasLeftMaps = leftHandMap != null && leftInteractionMap != null;

            if (!hasRightMaps || !hasLeftMaps)
            {
                Debug.LogWarning(
                    "[XROriginSetup] XRI Hand 액션맵 일부 누락.\n" +
                    $"  RightHand={rightHandMap != null}, RightInteraction={rightInteractionMap != null}\n" +
                    $"  LeftHand={leftHandMap != null}, LeftInteraction={leftInteractionMap != null}\n" +
                    "인라인 InputAction 폴백을 사용합니다.");
            }

            // Right Hand Ray Interactor (핸드트래킹 포인터)
            // 비활성 상태에서 생성 → XRInteractorReticleVisual이 Reticle(Clone) 즉시 생성하는 것 방지
            var rightHand = new GameObject("Right Hand Ray");
            rightHand.SetActive(false);
            rightHand.transform.SetParent(cameraOffset.transform, false);

#if XR_INTERACTION
            var rightController = rightHand.AddComponent<ActionBasedController>();

            if (hasRightMaps)
            {
                SetControllerActions(rightController, xriInputActions, "XRI RightHand", "XRI RightHand Interaction");
                Debug.Log("[XROriginSetup] Right Hand: InputActionReference 기반 액션 설정 완료 (m_UseReference=1)");
            }
            else
            {
                SetControllerInlineActions(rightController, "RightHand");
                Debug.LogWarning("[XROriginSetup] Right Hand: 인라인 InputAction 폴백 사용");
            }

            var rightRayInteractor = rightHand.AddComponent<XRRayInteractor>();
            rightRayInteractor.maxRaycastDistance = 10f;
            var rightLineRenderer = rightHand.AddComponent<LineRenderer>();
            rightLineRenderer.startWidth = 0.005f;
            rightLineRenderer.endWidth = 0.005f;
            rightLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            rightLineRenderer.startColor = new Color(0.3f, 0.7f, 1f, 0.8f);
            rightLineRenderer.endColor = new Color(0.3f, 0.7f, 1f, 0.3f);
            var rightLineVisual = rightHand.AddComponent<XRInteractorLineVisual>();
            rightLineVisual.lineLength = 5f;

            // Reticle (레이 끝 조준점 — XREAL Hands Setup 프리팹 패턴)
            var rightReticle = rightHand.AddComponent<XRInteractorReticleVisual>();
            rightReticle.maxRaycastDistance = 10f;
            if (reticlePrefab != null) rightReticle.reticlePrefab = reticlePrefab;
#endif

            // Left Hand Ray Interactor
            var leftHand = new GameObject("Left Hand Ray");
            leftHand.SetActive(false);
            leftHand.transform.SetParent(cameraOffset.transform, false);

#if XR_INTERACTION
            var leftController = leftHand.AddComponent<ActionBasedController>();

            if (hasLeftMaps)
            {
                SetControllerActions(leftController, xriInputActions, "XRI LeftHand", "XRI LeftHand Interaction");
                Debug.Log("[XROriginSetup] Left Hand: InputActionReference 기반 액션 설정 완료 (m_UseReference=1)");
            }
            else
            {
                SetControllerInlineActions(leftController, "LeftHand");
                Debug.LogWarning("[XROriginSetup] Left Hand: 인라인 InputAction 폴백 사용");
            }

            var leftRayInteractor = leftHand.AddComponent<XRRayInteractor>();
            leftRayInteractor.maxRaycastDistance = 10f;
            var leftLineRenderer = leftHand.AddComponent<LineRenderer>();
            leftLineRenderer.startWidth = 0.005f;
            leftLineRenderer.endWidth = 0.005f;
            leftLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            leftLineRenderer.startColor = new Color(0.3f, 0.7f, 1f, 0.8f);
            leftLineRenderer.endColor = new Color(0.3f, 0.7f, 1f, 0.3f);
            var leftLineVisual = leftHand.AddComponent<XRInteractorLineVisual>();
            leftLineVisual.lineLength = 5f;

            // Reticle (레이 끝 조준점 — XREAL Hands Setup 프리팹 패턴)
            var leftReticle = leftHand.AddComponent<XRInteractorReticleVisual>();
            leftReticle.maxRaycastDistance = 10f;
            if (reticlePrefab != null) leftReticle.reticlePrefab = reticlePrefab;
#endif

            // 혹시 컴포넌트 추가 과정에서 Reticle(Clone)이 생겼다면 정리
            CleanupReticleClones();

            // === EventSystem + XRUIInputModule (Hand Ray UI 인터랙션 필수) ===
            var eventSystem = Object.FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                var esGO = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
                eventSystem = esGO.AddComponent<EventSystem>();
            }
            // 기존 입력 모듈 제거 (XRUIInputModule과 충돌 방지)
            var standaloneIM = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneIM != null) Object.DestroyImmediate(standaloneIM);
            var inputSystemIM = eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (inputSystemIM != null) Object.DestroyImmediate(inputSystemIM);
#if XR_INTERACTION
            var xrUIModule = eventSystem.GetComponent<XRUIInputModule>();
            if (xrUIModule == null)
                xrUIModule = eventSystem.gameObject.AddComponent<XRUIInputModule>();

            // BeamPro 터치 + XR Hand Ray 동시 지원
            xrUIModule.enableXRInput = true;
            xrUIModule.enableTouchInput = true;
            xrUIModule.enableMouseInput = true;

            // 내장 폴백 활성화 — 핸드트래킹 초기화 타이밍과 무관하게 터치 항상 작동
            xrUIModule.enableBuiltinActionsAsFallback = true;
            Debug.Log("[XROriginSetup] XRUIInputModule: enableBuiltinActionsAsFallback = true 설정 완료");
#endif

            // Fallback: 입력 모듈이 하나도 없으면 InputSystemUIInputModule 추가
            // (XR_INTERACTION define 누락 시 터치/마우스 입력 완전 불능 방지)
            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Debug.LogWarning("[XROriginSetup] XRUIInputModule 사용 불가 — InputSystemUIInputModule으로 대체. " +
                    "Scripting Define Symbols에 XR_INTERACTION 추가 필요.");
            }

            // === XRHandTrackingEvents (Hand Ray에 추가) ===
#if XR_HANDS
            var rightHandEvents = rightHand.AddComponent<UnityEngine.XR.Hands.XRHandTrackingEvents>();
            rightHandEvents.handedness = UnityEngine.XR.Hands.Handedness.Right;

            var leftHandEvents = leftHand.AddComponent<UnityEngine.XR.Hands.XRHandTrackingEvents>();
            leftHandEvents.handedness = UnityEngine.XR.Hands.Handedness.Left;
#endif

            // === HandTrackingManager ===
            var handTrackingGO = new GameObject("HandTrackingManager");
            handTrackingGO.transform.SetParent(xrOriginGO.transform, false);
            handTrackingGO.AddComponent<Core.HandTrackingManager>();

            // Select the created object
            Selection.activeGameObject = xrOriginGO;

            Debug.Log("[XROriginSetup] XR Origin (XREAL) 구성 완료! (ARSession + 핸드트래킹 + InputActionManager + XREAL 바인딩 포함)");
        }

        // ============================================================
        // XREAL 핸드트래킹 바인딩 자동 추가
        // (XREAL SDK의 XREALHandTrackingSetup.SetupHandTracking 로직 복제)
        // ============================================================

        /// <summary>XRI Default Input Actions에 XREAL 핸드트래킹 바인딩을 추가하고 저장</summary>
        private static void AddXREALBindings(InputActionAsset actionAsset)
        {
            int addedCount = 0;

            // XRI 2.x 액션맵
            addedCount += AddBindingIfMissing(actionAsset, "XRI LeftHand", "Aim Position", "<XREALHandTracking>{LeftHand}/pointerPosition");
            addedCount += AddBindingIfMissing(actionAsset, "XRI LeftHand", "Aim Rotation", "<XREALHandTracking>{LeftHand}/pointerRotation");
            addedCount += AddBindingIfMissing(actionAsset, "XRI LeftHand Interaction", "Select", "<XREALHandTracking>{LeftHand}/indexPressed");
            addedCount += AddBindingIfMissing(actionAsset, "XRI LeftHand Interaction", "Select Value", "<XREALHandTracking>{LeftHand}/pinchStrengthIndex");
            addedCount += AddBindingIfMissing(actionAsset, "XRI LeftHand Interaction", "UI Press", "<XREALHandTracking>{LeftHand}/indexPressed");
            addedCount += AddBindingIfMissing(actionAsset, "XRI LeftHand Interaction", "UI Press Value", "<XREALHandTracking>{LeftHand}/pinchStrengthIndex");
            addedCount += AddBindingIfMissing(actionAsset, "XRI LeftHand", "Is Tracked", "<XREALHandTracking>{LeftHand}/isTracked");
            addedCount += AddBindingIfMissing(actionAsset, "XRI LeftHand", "Tracking State", "<XREALHandTracking>{LeftHand}/trackingState");

            addedCount += AddBindingIfMissing(actionAsset, "XRI RightHand", "Aim Position", "<XREALHandTracking>{RightHand}/pointerPosition");
            addedCount += AddBindingIfMissing(actionAsset, "XRI RightHand", "Aim Rotation", "<XREALHandTracking>{RightHand}/pointerRotation");
            addedCount += AddBindingIfMissing(actionAsset, "XRI RightHand Interaction", "Select", "<XREALHandTracking>{RightHand}/indexPressed");
            addedCount += AddBindingIfMissing(actionAsset, "XRI RightHand Interaction", "Select Value", "<XREALHandTracking>{RightHand}/pinchStrengthIndex");
            addedCount += AddBindingIfMissing(actionAsset, "XRI RightHand Interaction", "UI Press", "<XREALHandTracking>{RightHand}/indexPressed");
            addedCount += AddBindingIfMissing(actionAsset, "XRI RightHand Interaction", "UI Press Value", "<XREALHandTracking>{RightHand}/pinchStrengthIndex");
            addedCount += AddBindingIfMissing(actionAsset, "XRI RightHand", "Is Tracked", "<XREALHandTracking>{RightHand}/isTracked");
            addedCount += AddBindingIfMissing(actionAsset, "XRI RightHand", "Tracking State", "<XREALHandTracking>{RightHand}/trackingState");

            if (addedCount > 0)
            {
                // 에셋 파일에 저장
                string assetPath = AssetDatabase.GetAssetPath(actionAsset);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    File.WriteAllText(assetPath, actionAsset.ToJson());
                    AssetDatabase.ImportAsset(assetPath);
                    Debug.Log($"[XROriginSetup] XREAL 핸드트래킹 바인딩 {addedCount}개 추가 완료 → {assetPath}");
                }
            }
            else
            {
                Debug.Log("[XROriginSetup] XREAL 핸드트래킹 바인딩 이미 존재 — 추가 불필요");
            }
        }

        /// <summary>바인딩이 없으면 추가. 반환: 1=추가됨, 0=이미 존재 또는 액션 없음</summary>
        private static int AddBindingIfMissing(InputActionAsset asset, string mapName, string actionName, string binding)
        {
            var map = asset.FindActionMap(mapName);
            if (map == null) return 0;

            var action = map.FindAction(actionName);
            if (action == null) return 0;

            // 이미 동일 바인딩이 있는지 확인
            if (action.bindings.Any(b => b.path == binding))
                return 0;

            action.AddBinding(path: binding, groups: "Generic XR Controller");
            return 1;
        }

        // ============================================================
        // InputActionAsset 로드
        // ============================================================

        /// <summary>XRI Default Input Actions 에셋 로드</summary>
        private static InputActionAsset LoadXRIInputActions()
        {
            // 기본 경로
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(XRI_INPUT_ACTIONS_PATH);
            if (asset != null) return asset;

            // 대안 경로 시도
            foreach (var path in XRI_ALTERNATIVE_PATHS)
            {
                asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                if (asset != null)
                {
                    Debug.Log($"[XROriginSetup] XRI Input Actions 발견: {path}");
                    return asset;
                }
            }

            // GUID 기반 검색 (경로가 변경된 경우)
            var guids = AssetDatabase.FindAssets("XRI Default Input Actions t:InputActionAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                if (asset != null)
                {
                    Debug.Log($"[XROriginSetup] XRI Input Actions GUID 검색으로 발견: {path}");
                    return asset;
                }
            }

            return null;
        }

        // ============================================================
        // ActionBasedController 액션 설정
        // ============================================================

        /// <summary>
        /// InputActionAsset의 InputActionReference 서브에셋을 사용하여 ActionBasedController 액션 설정.
        /// InputActionReference를 통해 m_UseReference=1로 직렬화되어, 런타임에 에셋 참조가 유지됨.
        /// XRI 2.x 구조: handMap(Aim Position/Rotation) + interactionMap(Select/UIPress) 분리.
        /// </summary>
        private static void SetControllerActions(
            ActionBasedController controller,
            InputActionAsset actionAsset,
            string handMapName,
            string interactionMapName)
        {
            // 헬퍼: null-safe InputActionProperty 변환
            InputActionProperty Prop(InputActionReference r)
                => r != null ? new InputActionProperty(r) : new InputActionProperty();

            // Position/Rotation — "Aim Position/Rotation" 사용 (XREAL 핸드트래킹 포인터)
            var aimPosRef = FindInputActionReference(actionAsset, handMapName, "Aim Position");
            var aimRotRef = FindInputActionReference(actionAsset, handMapName, "Aim Rotation");
            var isTrackedRef = FindInputActionReference(actionAsset, handMapName, "Is Tracked");
            var trackingStateRef = FindInputActionReference(actionAsset, handMapName, "Tracking State");

            // Interaction
            var selectRef = FindInputActionReference(actionAsset, interactionMapName, "Select");
            var selectValueRef = FindInputActionReference(actionAsset, interactionMapName, "Select Value");
            var uiPressRef = FindInputActionReference(actionAsset, interactionMapName, "UI Press");
            var uiPressValueRef = FindInputActionReference(actionAsset, interactionMapName, "UI Press Value");
            var activateRef = FindInputActionReference(actionAsset, interactionMapName, "Activate");
            var activateValueRef = FindInputActionReference(actionAsset, interactionMapName, "Activate Value");

            if (aimPosRef != null)
                controller.positionAction = Prop(aimPosRef);
            else
                Debug.LogWarning($"[XROriginSetup] '{handMapName}'에서 'Aim Position' InputActionReference를 찾을 수 없음");

            if (aimRotRef != null)
                controller.rotationAction = Prop(aimRotRef);
            else
                Debug.LogWarning($"[XROriginSetup] '{handMapName}'에서 'Aim Rotation' InputActionReference를 찾을 수 없음");

            // Tracking
            if (isTrackedRef != null)
                controller.isTrackedAction = Prop(isTrackedRef);
            if (trackingStateRef != null)
                controller.trackingStateAction = Prop(trackingStateRef);

            // Interaction (Select, UIPress, Activate)
            if (selectRef != null)
                controller.selectAction = Prop(selectRef);
            else
                Debug.LogWarning($"[XROriginSetup] '{interactionMapName}'에서 'Select' InputActionReference를 찾을 수 없음");

            if (selectValueRef != null)
                controller.selectActionValue = Prop(selectValueRef);

            if (uiPressRef != null)
                controller.uiPressAction = Prop(uiPressRef);
            else
                Debug.LogWarning($"[XROriginSetup] '{interactionMapName}'에서 'UI Press' InputActionReference를 찾을 수 없음");

            if (uiPressValueRef != null)
                controller.uiPressActionValue = Prop(uiPressValueRef);
            if (activateRef != null)
                controller.activateAction = Prop(activateRef);
            if (activateValueRef != null)
                controller.activateActionValue = Prop(activateValueRef);

            Debug.Log($"[XROriginSetup] {handMapName}: InputActionReference 기반 액션 설정 완료 (m_UseReference=1)");
        }

        /// <summary>
        /// InputActionAsset에서 특정 액션맵/액션의 InputActionReference 서브에셋을 검색.
        /// AssetDatabase.LoadAllAssetsAtPath()로 .inputactions 파일의 모든 서브에셋을 조회.
        /// </summary>
        private static InputActionReference FindInputActionReference(
            InputActionAsset actionAsset, string mapName, string actionName)
        {
            string assetPath = AssetDatabase.GetAssetPath(actionAsset);
            if (string.IsNullOrEmpty(assetPath)) return null;

            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var sub in allAssets)
            {
                if (sub is InputActionReference r &&
                    r.action != null &&
                    r.action.actionMap?.name == mapName &&
                    r.action.name == actionName)
                {
                    return r;
                }
            }
            return null;
        }

        /// <summary>폴백: 인라인 InputAction (InputActionAsset이 없을 때)</summary>
        private static void SetControllerInlineActions(ActionBasedController controller, string handedness)
        {
            string hand = "{" + handedness + "}";
            controller.positionAction = new InputActionProperty(
                new InputAction("Position", InputActionType.Value,
                    $"<XREALHandTracking>{hand}/pointerPosition"));
            controller.rotationAction = new InputActionProperty(
                new InputAction("Rotation", InputActionType.Value,
                    $"<XREALHandTracking>{hand}/pointerRotation"));
            controller.isTrackedAction = new InputActionProperty(
                new InputAction("IsTracked", InputActionType.Button,
                    $"<XREALHandTracking>{hand}/isTracked"));
            controller.trackingStateAction = new InputActionProperty(
                new InputAction("TrackingState", InputActionType.Value,
                    $"<XREALHandTracking>{hand}/trackingState"));
            controller.selectAction = new InputActionProperty(
                new InputAction("Select", InputActionType.Button,
                    $"<XREALHandTracking>{hand}/indexPressed"));
            controller.selectActionValue = new InputActionProperty(
                new InputAction("SelectValue", InputActionType.Value,
                    $"<XREALHandTracking>{hand}/pinchStrengthIndex"));
            controller.uiPressAction = new InputActionProperty(
                new InputAction("UIPress", InputActionType.Button,
                    $"<XREALHandTracking>{hand}/indexPressed"));
            controller.uiPressActionValue = new InputActionProperty(
                new InputAction("UIPressValue", InputActionType.Value,
                    $"<XREALHandTracking>{hand}/pinchStrengthIndex"));
        }

        /// <summary>XREAL SDK의 Reticle 프리팹 로드</summary>
        private static GameObject LoadReticlePrefab()
        {
            // XREAL SDK 로컬 패키지 경로
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.xreal.xr/Runtime/Prefabs/Reticle.prefab");
            if (prefab != null) return prefab;

            // 대안: GUID로 검색
            string path = AssetDatabase.GUIDToAssetPath("4340b549d8be42149bdd0364ee2d2319");
            if (!string.IsNullOrEmpty(path))
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                Debug.LogWarning("[XROriginSetup] XREAL Reticle.prefab을 찾을 수 없음 — 레티클 없이 진행");
            return prefab;
        }

        /// <summary>
        /// XRInteractorReticleVisual이 에디터에서 즉시 인스턴스화한 Reticle(Clone) 오브젝트 정리.
        /// Setup 실행 시마다 루트에 쌓이는 것을 방지.
        /// </summary>
        private static void CleanupReticleClones()
        {
            // 루트 오브젝트에서 Reticle(Clone) 검색
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            int removed = 0;
            foreach (var go in rootObjects)
            {
                if (go.name == "Reticle(Clone)")
                {
                    Undo.DestroyObjectImmediate(go);
                    removed++;
                }
            }
            if (removed > 0)
                Debug.Log($"[XROriginSetup] Reticle(Clone) {removed}개 정리 완료");
        }
    }
}

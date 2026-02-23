using UnityEngine;
using UnityEditor;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.EventSystems;

namespace ARNavExperiment.EditorTools
{
    public class XROriginSetupTool
    {
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
                "- XR Origin + XR Interaction Manager\n" +
                "- Camera Offset / Main Camera\n" +
                "- TrackedPoseDriver (머리 추적)\n" +
                "- ARAnchorManager (Spatial Anchor)\n" +
                "- 투명 배경 (AR 모드)\n\n" +
                "씬을 저장하세요 (Cmd+S)", "확인");
        }

        /// <summary>확인 다이얼로그 없이 실행 (MasterSetupTool에서 호출용)</summary>
        public static void SetupXROriginSilent()
        {
            // 기존 XR Origin 모두 삭제 (복수)
            var existingOrigins = Object.FindObjectsOfType<XROrigin>();
            foreach (var origin in existingOrigins)
                Undo.DestroyObjectImmediate(origin.gameObject);

            // "XR Origin (XREAL)" 이름으로도 검색하여 삭제
            GameObject xrGO;
            while ((xrGO = GameObject.Find("XR Origin (XREAL)")) != null)
                Undo.DestroyObjectImmediate(xrGO);

            // 기본 Main Camera 삭제
            var defaultCam = GameObject.Find("Main Camera");
            if (defaultCam != null)
                Undo.DestroyObjectImmediate(defaultCam);

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

            // Tracked Pose Driver (head tracking)
            var trackedPose = cameraGO.AddComponent<TrackedPoseDriver>();
            trackedPose.positionInput = new UnityEngine.InputSystem.InputActionProperty(
                new UnityEngine.InputSystem.InputAction("Position",
                    UnityEngine.InputSystem.InputActionType.Value,
                    "<XRHMD>/centerEyePosition"));
            trackedPose.rotationInput = new UnityEngine.InputSystem.InputActionProperty(
                new UnityEngine.InputSystem.InputAction("Rotation",
                    UnityEngine.InputSystem.InputActionType.Value,
                    "<XRHMD>/centerEyeRotation"));

            // Set XR Origin camera reference
            xrOrigin.Camera = camera;

            // === XR Origin Settings ===
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;

            // === AR Anchor Manager (Spatial Anchor용) ===
#if XR_ARFOUNDATION
            xrOriginGO.AddComponent<UnityEngine.XR.ARFoundation.ARSessionOrigin>();
            xrOriginGO.AddComponent<UnityEngine.XR.ARFoundation.ARAnchorManager>();
#endif

            // === XREAL Session Manager ===
#if UNITY_ANDROID
            var sessionMgr = xrOriginGO.AddComponent<Unity.XR.XREAL.XREALSessionManager>();
#endif

            // === Hand Tracking (XR Interaction) ===
            // Right Hand Ray Interactor (핸드트래킹 포인터)
            var rightHand = new GameObject("Right Hand Ray");
            rightHand.transform.SetParent(cameraOffset.transform, false);

#if XR_INTERACTION
            var rightController = rightHand.AddComponent<XRController>();
            rightController.controllerNode = UnityEngine.XR.XRNode.RightHand;
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
#endif

            // Left Hand Ray Interactor
            var leftHand = new GameObject("Left Hand Ray");
            leftHand.transform.SetParent(cameraOffset.transform, false);

#if XR_INTERACTION
            var leftController = leftHand.AddComponent<XRController>();
            leftController.controllerNode = UnityEngine.XR.XRNode.LeftHand;
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
#endif

            // 에디터에서는 핸드 레이 비활성화 (마우스 클릭 사용)
            rightHand.SetActive(false);
            leftHand.SetActive(false);

            // === HandTrackingManager ===
            var handTrackingGO = new GameObject("HandTrackingManager");
            handTrackingGO.transform.SetParent(xrOriginGO.transform, false);
            handTrackingGO.AddComponent<Core.HandTrackingManager>();

            // Select the created object
            Selection.activeGameObject = xrOriginGO;

            Debug.Log("[XROriginSetup] XR Origin (XREAL) 구성 완료! (핸드트래킹 포함)");
        }
    }
}

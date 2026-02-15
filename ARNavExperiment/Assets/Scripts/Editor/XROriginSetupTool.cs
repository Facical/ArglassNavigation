using UnityEngine;
using UnityEditor;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;

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

            // === XREAL Session Manager ===
#if UNITY_ANDROID
            var sessionMgr = xrOriginGO.AddComponent<Unity.XR.XREAL.XREALSessionManager>();
#endif

            // Select the created object
            Selection.activeGameObject = xrOriginGO;

            Debug.Log("[XROriginSetup] XR Origin (XREAL) 구성 완료!");
            EditorUtility.DisplayDialog("완료",
                "XR Origin (XREAL) 구성 완료!\n\n" +
                "구성:\n" +
                "- XR Origin + XR Interaction Manager\n" +
                "- Camera Offset / Main Camera\n" +
                "- TrackedPoseDriver (머리 추적)\n" +
                "- 투명 배경 (AR 모드)\n\n" +
                "씬을 저장하세요 (Cmd+S)", "확인");
        }
    }
}

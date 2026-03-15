using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if XR_HANDS
using UnityEngine.XR.Hands;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARNavExperiment.Core
{
    /// <summary>
    /// 핸드트래킹 초기화 및 XR Interaction 설정 관리.
    /// XREAL Air2 Ultra의 핸드트래킹(Pinch=클릭)으로 글래스 UI 직접 조작.
    /// Hand Ray 오브젝트를 찾아 활성화/비활성화하고, 서브시스템 상태를 추적.
    /// </summary>
    public class HandTrackingManager : MonoBehaviour
    {
        public static HandTrackingManager Instance { get; private set; }

#pragma warning disable CS0414 // 조건부 컴파일(XR_HANDS, !UNITY_EDITOR)에서만 사용
        [SerializeField] private bool enableHandTracking = true;
#pragma warning restore CS0414
        /// <summary>핸드트래킹 활성 상태 변경 시 발행 (true=활성, false=비활성)</summary>
        public event Action<bool> OnHandTrackingStateChanged;

        public bool IsInitialized { get; private set; }
        public bool AreHandRaysActive { get; private set; }

        private GameObject leftHandRay;
        private GameObject rightHandRay;
        private Coroutine diagCoroutine;

        private const float INIT_DELAY = 1.0f;
        private const float RETRY_DELAY = 2.0f;
        private const int MAX_RETRIES = 3;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
#if !UNITY_EDITOR
            if (enableHandTracking)
                StartCoroutine(DelayedInitialize());
#else
            Debug.Log("[HandTracking] 에디터 모드 — 핸드트래킹 비활성 (마우스 사용)");
#endif
        }

        /// <summary>XR 서브시스템 준비를 기다린 후 초기화</summary>
        private IEnumerator DelayedInitialize()
        {
            // XR 서브시스템이 준비될 때까지 대기
            Debug.Log("[HandTracking] XR 서브시스템 준비 대기 중...");
            yield return new WaitForSeconds(INIT_DELAY);

            // InputActionManager 존재 확인
            CheckInputActionManager();

            InitializeHandTracking();

            // 초기화 실패 시 재시도
            int retryCount = 0;
            while (!IsInitialized && retryCount < MAX_RETRIES)
            {
                retryCount++;
                Debug.Log($"[HandTracking] 초기화 재시도 ({retryCount}/{MAX_RETRIES})...");
                yield return new WaitForSeconds(RETRY_DELAY);
                InitializeHandTracking();
            }

            if (!IsInitialized)
                Debug.LogError("[HandTracking] 최대 재시도 횟수 초과 — Hand Ray를 찾을 수 없음");
        }

        /// <summary>InputActionManager가 씬에 존재하는지 확인 (경고용)</summary>
        private void CheckInputActionManager()
        {
#if XR_INTERACTION
            var iam = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
            if (iam == null)
            {
                Debug.LogWarning(
                    "[HandTracking] InputActionManager가 씬에 없습니다!\n" +
                    "InputAction이 자동 Enable되지 않아 핸드트래킹/터치 입력이 작동하지 않을 수 있습니다.\n" +
                    "ARNav > Master Setup > Full Setup을 재실행하세요.");
            }
            else
            {
                Debug.Log($"[HandTracking] InputActionManager 확인 — {iam.actionAssets?.Count ?? 0}개 에셋 등록됨");
            }
#endif
        }

        private void InitializeHandTracking()
        {
#if XR_HANDS
            Debug.Log("[HandTracking] XR Hands 패키지 감지 — 핸드트래킹 초기화");

            // 서브시스템 상태 진단
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            Debug.Log($"[HandTracking] XRHandSubsystem 수: {subsystems.Count}");
            foreach (var sub in subsystems)
                Debug.Log($"[HandTracking] XRHandSubsystem 실행 중: {sub.running}");

            // InputSystem 디바이스 진단
#if ENABLE_INPUT_SYSTEM
            int handDevices = 0;
            foreach (var d in InputSystem.devices)
            {
                if (d.GetType().Name.Contains("HandTracking") ||
                    d.description.manufacturer == "XREAL")
                {
                    handDevices++;
                    Debug.Log($"[HandTracking] 디바이스 감지: {d.name} ({d.GetType().Name})");
                }
            }
            Debug.Log($"[HandTracking] XREAL 핸드트래킹 디바이스 수: {handDevices}");
#endif

            FindHandRays();
            if (leftHandRay != null || rightHandRay != null)
            {
                IsInitialized = true;
                Debug.Log($"[HandTracking] Hand Ray 발견 — L:{leftHandRay?.name ?? "없음"}, R:{rightHandRay?.name ?? "없음"}");
            }
            else
            {
                Debug.LogWarning("[HandTracking] Hand Ray를 찾을 수 없음");
            }
#else
            Debug.LogWarning("[HandTracking] XR Hands 패키지 미설치 — 핸드트래킹 사용 불가");
#endif
        }

        /// <summary>XR Origin 하위에서 Hand Ray 오브젝트를 검색</summary>
        private void FindHandRays()
        {
            // XR Origin의 CameraFloorOffsetObject 하위에서 검색
            var xrOrigin = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
            Transform searchRoot = xrOrigin != null
                ? xrOrigin.CameraFloorOffsetObject?.transform ?? xrOrigin.transform
                : transform.root;

            // 재귀 검색
            leftHandRay = FindChildByName(searchRoot, "Left Hand Ray");
            rightHandRay = FindChildByName(searchRoot, "Right Hand Ray");

            // 이름이 다를 수 있으므로 대안 검색
            if (leftHandRay == null)
                leftHandRay = FindChildByName(searchRoot, "LeftHand Ray");
            if (rightHandRay == null)
                rightHandRay = FindChildByName(searchRoot, "RightHand Ray");
        }

        private static GameObject FindChildByName(Transform root, string name)
        {
            if (root.name == name) return root.gameObject;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChildByName(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Hand Ray를 활성화합니다.</summary>
        public void ActivateHandRays()
        {
#if !UNITY_EDITOR
            if (!IsInitialized)
            {
                Debug.Log("[HandTracking] 미초기화 상태 — Hand Ray 재검색");
                FindHandRays();
                IsInitialized = leftHandRay != null || rightHandRay != null;
                if (!IsInitialized)
                    Debug.LogWarning("[HandTracking] Hand Ray 재검색 실패");
            }

            // XREAL 입력 소스를 Hands로 전환 (글래스 측 핸드트래킹 활성화)
            SwitchInputSourceToHands();
#endif
            // ActionBasedController의 InputAction이 Enable 상태인지 확인 (빈 바인딩 교체 포함)
            // SetActive(true) 전에 호출하여 OnEnable()이 빈 액션으로 실행되는 것을 방지
            EnsureActionsEnabled();

            if (leftHandRay != null) leftHandRay.SetActive(true);
            if (rightHandRay != null) rightHandRay.SetActive(true);

            AreHandRaysActive = true;
            OnHandTrackingStateChanged?.Invoke(true);
            Debug.Log($"[HandTracking] Hand Ray 활성화 — L:{leftHandRay != null}, R:{rightHandRay != null}");

#if !UNITY_EDITOR
            // 진단: ActionBasedController 데이터 흐름 확인 — 노이즈 로그 비활성화
            // if (diagCoroutine != null) StopCoroutine(diagCoroutine);
            // diagCoroutine = StartCoroutine(DiagnoseHandRayData());
#endif
        }

#if !UNITY_EDITOR
        private IEnumerator DiagnoseHandRayData()
        {
            yield return new WaitForSeconds(1f);
            for (int i = 0; i < 5; i++)
            {
                DiagnoseController(rightHandRay, "R");
                DiagnoseController(leftHandRay, "L");

                // XREALHandTracking 디바이스 직접 조회
#if ENABLE_INPUT_SYSTEM
                foreach (var d in InputSystem.devices)
                {
                    if (d is Unity.XR.XREAL.XREALHandTracking ht)
                    {
                        var pos = ht.pointerPosition.ReadValue();
                        var pressed = ht.indexPressed.ReadValue();
                        var pinch = ht.pinchStrengthIndex.ReadValue();
                        var usage = string.Join(",", d.usages);
                        Debug.Log($"[HandDiag] Device={d.name} usage=[{usage}] pos={pos} pressed={pressed:F2} pinch={pinch:F2} tracked={d.added}");
                    }
                }
#endif
                yield return new WaitForSeconds(2f);
            }
        }

        private void DiagnoseController(GameObject handRayGO, string label)
        {
            if (handRayGO == null) return;
#if XR_INTERACTION
            var ctrl = handRayGO.GetComponent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>();
            if (ctrl == null) { Debug.Log($"[HandDiag] {label}: ActionBasedController 없음"); return; }

            var pos = ctrl.positionAction.action?.ReadValue<Vector3>() ?? Vector3.zero;
            var rot = ctrl.rotationAction.action?.ReadValue<Quaternion>() ?? Quaternion.identity;
            bool posEnabled = ctrl.positionAction.action?.enabled ?? false;
            bool selEnabled = ctrl.selectAction.action?.enabled ?? false;
            float selVal = ctrl.selectAction.action?.ReadValue<float>() ?? -1f;
            int bindingCount = ctrl.positionAction.action?.bindings.Count ?? 0;
            int controlCount = ctrl.positionAction.action?.controls.Count ?? 0;

            Debug.Log($"[HandDiag] {label}: pos={pos} rot={rot.eulerAngles} posEn={posEnabled} selEn={selEnabled} selVal={selVal:F2} bindings={bindingCount} controls={controlCount}");
#endif
        }
#endif

        /// <summary>Hand Ray의 ActionBasedController 액션이 Enable 상태인지 확인하고, 아니면 강제 활성화</summary>
        private void EnsureActionsEnabled()
        {
#if XR_INTERACTION && !UNITY_EDITOR && ENABLE_INPUT_SYSTEM
            // 빈 바인딩 감지 및 인라인 XREAL 바인딩으로 런타임 교체 (수정 전 씬 APK 대응)
            DetectAndFixEmptyBindings();
#endif
#if XR_INTERACTION && !UNITY_EDITOR
            EnsureControllerActionsEnabled(rightHandRay, "Right");
            EnsureControllerActionsEnabled(leftHandRay, "Left");
#endif
#if XR_INTERACTION && !UNITY_EDITOR && ENABLE_INPUT_SYSTEM
            // Enable 후에도 isTracked/trackingState가 resolve 안 된 경우 인라인 교체
            FixUnboundTrackingActions();
#endif
        }

#if XR_INTERACTION && !UNITY_EDITOR
        private static void EnsureControllerActionsEnabled(GameObject handRayGO, string label)
        {
            if (handRayGO == null) return;

            var controller = handRayGO.GetComponent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>();
            if (controller == null) return;

            int enabledCount = 0;
            enabledCount += EnableAction(controller.positionAction, $"{label}-Position");
            enabledCount += EnableAction(controller.rotationAction, $"{label}-Rotation");
            enabledCount += EnableAction(controller.selectAction, $"{label}-Select");
            enabledCount += EnableAction(controller.selectActionValue, $"{label}-SelectValue");
            enabledCount += EnableAction(controller.uiPressAction, $"{label}-UIPress");
            enabledCount += EnableAction(controller.uiPressActionValue, $"{label}-UIPressValue");
            enabledCount += EnableAction(controller.isTrackedAction, $"{label}-IsTracked");
            enabledCount += EnableAction(controller.trackingStateAction, $"{label}-TrackingState");
            enabledCount += EnableAction(controller.activateAction, $"{label}-Activate");
            enabledCount += EnableAction(controller.activateActionValue, $"{label}-ActivateValue");

            if (enabledCount > 0)
                Debug.Log($"[HandTracking] {label} Hand: {enabledCount}개 액션 수동 Enable 완료");
        }

        /// <returns>1 if action was enabled by this call, 0 otherwise</returns>
        private static int EnableAction(UnityEngine.InputSystem.InputActionProperty actionProperty, string debugName)
        {
            var action = actionProperty.action;
            if (action == null) return 0;
            if (action.enabled) return 0;

            action.Enable();
            Debug.Log($"[HandTracking] 액션 수동 Enable: {debugName} ({action.name})");
            return 1;
        }
#endif

#if XR_INTERACTION && !UNITY_EDITOR && ENABLE_INPUT_SYSTEM
        /// <summary>
        /// 런타임 안전망: ActionBasedController의 바인딩이 비어 있으면
        /// 인라인 XREAL 바인딩으로 교체 (수정 전 씬으로 빌드된 APK 대응).
        /// m_UseReference=0 + 빈 바인딩 상태에서도 핸드트래킹이 작동하도록 함.
        /// </summary>
        private void DetectAndFixEmptyBindings()
        {
            FixControllerBindings(rightHandRay, "RightHand");
            FixControllerBindings(leftHandRay, "LeftHand");
        }

        private static void FixControllerBindings(GameObject handRayGO, string handedness)
        {
            if (handRayGO == null) return;

            var controller = handRayGO.GetComponent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>();
            if (controller == null) return;

            // positionAction의 바인딩 수로 판별 — 바인딩이 있으면 정상
            var posAction = controller.positionAction.action;
            if (posAction == null || posAction.bindings.Count > 0) return;

            Debug.LogWarning($"[HandTracking] {handedness}: 빈 바인딩 감지 — 인라인 XREAL 바인딩으로 런타임 교체");

            string hand = "{" + handedness + "}";

            // 새 인라인 액션으로 전체 교체
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

            Debug.Log($"[HandTracking] {handedness}: 인라인 XREAL 바인딩 교체 완료 (8개 액션)");
        }

        /// <summary>
        /// Enable 상태에서도 isTracked/trackingState controls가 0이면 바인딩 미해석 —
        /// 인라인 XREAL 바인딩으로 교체. DetectAndFixEmptyBindings()는 positionAction.bindings.Count == 0
        /// 일 때만 작동하므로, 바인딩은 있지만 XREAL 디바이스에 resolve 안 되는 경우를 잡지 못함.
        /// </summary>
        private void FixUnboundTrackingActions()
        {
            FixTrackingActionsForHand(rightHandRay, "RightHand");
            FixTrackingActionsForHand(leftHandRay, "LeftHand");
        }

        private static void FixTrackingActionsForHand(GameObject handRayGO, string handedness)
        {
            if (handRayGO == null) return;

            var controller = handRayGO.GetComponent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>();
            if (controller == null) return;

            string hand = "{" + handedness + "}";
            bool anyFixed = false;

            // isTrackedAction: enabled 상태에서 controls.Count == 0 → 바인딩이 XREAL 디바이스에 resolve 안 됨
            var isTracked = controller.isTrackedAction.action;
            if (isTracked == null || (isTracked.enabled && isTracked.controls.Count == 0))
            {
                var newAction = new InputAction("IsTracked", InputActionType.Button,
                    $"<XREALHandTracking>{hand}/isTracked");
                newAction.Enable();
                controller.isTrackedAction = new InputActionProperty(newAction);
                anyFixed = true;
            }

            // trackingStateAction 동일 처리
            var trackingState = controller.trackingStateAction.action;
            if (trackingState == null || (trackingState.enabled && trackingState.controls.Count == 0))
            {
                var newAction = new InputAction("TrackingState", InputActionType.Value,
                    $"<XREALHandTracking>{hand}/trackingState");
                newAction.Enable();
                controller.trackingStateAction = new InputActionProperty(newAction);
                anyFixed = true;
            }

            if (anyFixed)
                Debug.LogWarning($"[HandTracking] {handedness}: isTracked/trackingState 바인딩 미해석 — 인라인 XREAL 바인딩으로 교체");
        }
#endif

        /// <summary>Hand Ray를 비활성화합니다.</summary>
        public void DeactivateHandRays()
        {
            if (leftHandRay != null) leftHandRay.SetActive(false);
            if (rightHandRay != null) rightHandRay.SetActive(false);

#if !UNITY_EDITOR
            // 진단 코루틴 중지
            if (diagCoroutine != null)
            {
                StopCoroutine(diagCoroutine);
                diagCoroutine = null;
            }

            // XREAL 입력 소스를 Controller로 복원 (가상 컨트롤러 복귀)
            SwitchInputSourceToController();
#endif

            AreHandRaysActive = false;
            OnHandTrackingStateChanged?.Invoke(false);
            Debug.Log("[HandTracking] Hand Ray 비활성화");
        }


#if !UNITY_EDITOR
        /// <summary>XREAL 입력 소스를 ControllerAndHands로 전환 (핸드트래킹 + 가상 컨트롤러 동시 활성화)</summary>
        private void SwitchInputSourceToHands()
        {
            try
            {
                if (!Unity.XR.XREAL.XREALPlugin.IsHandTrackingSupported())
                {
                    Debug.LogWarning("[HandTracking] 이 디바이스는 핸드트래킹을 지원하지 않습니다.");
                    return;
                }

                var current = Unity.XR.XREAL.XREALPlugin.GetInputSource();
                // ControllerAndHands: SDK가 네이티브 HandRay 데이터 파이프라인을 완전히 활성화
                // Hands만 설정하면 SDK HandRay가 비활성화되어 포인터 데이터가 XRI로 전달되지 않음
                if (current == Unity.XR.XREAL.InputSource.ControllerAndHands)
                {
                    Debug.Log("[HandTracking] 이미 ControllerAndHands 입력 소스입니다.");
                    return;
                }

                bool success = Unity.XR.XREAL.XREALPlugin.SetInputSource(Unity.XR.XREAL.InputSource.ControllerAndHands);
                var after = Unity.XR.XREAL.XREALPlugin.GetInputSource();
                Debug.Log($"[HandTracking] 입력 소스 전환: {current} → ControllerAndHands (성공={success}, 현재={after})");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HandTracking] 입력 소스 전환 실패: {e.Message}");
            }
        }

        /// <summary>XREAL 입력 소스를 Controller로 복원</summary>
        private void SwitchInputSourceToController()
        {
            try
            {
                var current = Unity.XR.XREAL.XREALPlugin.GetInputSource();
                if (current == Unity.XR.XREAL.InputSource.Controller)
                {
                    Debug.Log("[HandTracking] 이미 Controller 입력 소스입니다.");
                    return;
                }

                // ControllerAndHands에서 Controller로 복원
                bool success = Unity.XR.XREAL.XREALPlugin.SetInputSource(Unity.XR.XREAL.InputSource.Controller);
                var after = Unity.XR.XREAL.XREALPlugin.GetInputSource();
                Debug.Log($"[HandTracking] 입력 소스 복원: {current} → Controller (성공={success}, 현재={after})");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HandTracking] 입력 소스 복원 실패: {e.Message}");
            }
        }
#endif

        /// <summary>XR Hand 서브시스템이 현재 실행 중인지 확인</summary>
        public bool IsSubsystemRunning()
        {
#if XR_HANDS
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var sub in subsystems)
            {
                if (sub.running) return true;
            }
#endif
            return false;
        }

        /// <summary>핸드트래킹 사용 가능 여부</summary>
        public bool IsHandTrackingAvailable()
        {
#if XR_HANDS && !UNITY_EDITOR
            return enableHandTracking;
#else
            return false;
#endif
        }

        /// <summary>사람이 읽기 쉬운 상태 문자열 반환</summary>
        public string GetStatusText()
        {
#if UNITY_EDITOR
            return "에디터 모드 (마우스)";
#else
            if (!enableHandTracking) return "비활성 (설정 OFF)";
            if (!IsInitialized) return "미초기화";
            if (AreHandRaysActive)
            {
                bool subsystem = IsSubsystemRunning();
                return subsystem ? "활성 (핸드레이 ON)" : "핸드레이 ON (서브시스템 대기)";
            }
            return "대기 (핸드레이 OFF)";
#endif
        }

        /// <summary>서브시스템 상태 문자열</summary>
        public string GetSubsystemStatusText()
        {
#if UNITY_EDITOR
            return "에디터 (N/A)";
#elif XR_HANDS
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);

            // InputSystem에서 XREAL 핸드트래킹 디바이스 감지
            int handDeviceCount = 0;
#if ENABLE_INPUT_SYSTEM
            foreach (var d in InputSystem.devices)
            {
                if (d.description.manufacturer == "XREAL" ||
                    d.GetType().Name.Contains("HandTracking"))
                    handDeviceCount++;
            }
#endif
            string deviceSuffix = handDeviceCount > 0 ? $" ({handDeviceCount} devices)" : "";

            if (subsystems.Count == 0) return "서브시스템 없음" + deviceSuffix;
            foreach (var sub in subsystems)
            {
                if (sub.running) return "실행 중" + deviceSuffix;
            }
            return "정지 상태" + deviceSuffix;
#else
            return "XR Hands 미설치";
#endif
        }

        /// <summary>Hand Ray 진단 정보 (디버그용)</summary>
        public string GetHandRayDiagnostics()
        {
            var lines = new System.Text.StringBuilder();

            // Hand Ray 오브젝트 존재 여부
            bool hasLeft = leftHandRay != null;
            bool hasRight = rightHandRay != null;
            lines.AppendLine($"Ray: L={hasLeft} R={hasRight}");

#if XR_INTERACTION && !UNITY_EDITOR
            // ActionBasedController 확인
            if (hasRight)
            {
                var abc = rightHandRay.GetComponent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>();
                var ray = rightHandRay.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRRayInteractor>();
                lines.AppendLine($"R-Ctrl: {(abc != null ? "OK" : "없음")} Ray: {(ray != null ? "OK" : "없음")}");
                if (abc != null)
                {
                    bool posActive = abc.positionAction.action?.enabled ?? false;
                    bool selActive = abc.selectAction.action?.enabled ?? false;
                    bool uiActive = abc.uiPressAction.action?.enabled ?? false;
                    lines.AppendLine($"R-Action: pos={posActive} sel={selActive} ui={uiActive}");

                    // InputAction 참조 타입 표시 (에셋 vs 인라인)
                    var posRef = abc.positionAction.action;
                    bool isAssetRef = posRef?.actionMap?.asset != null;
                    lines.AppendLine($"R-Ref: {(isAssetRef ? "Asset" : "Inline")}");
                }
            }
            if (hasLeft)
            {
                var abc = leftHandRay.GetComponent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>();
                var ray = leftHandRay.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRRayInteractor>();
                lines.AppendLine($"L-Ctrl: {(abc != null ? "OK" : "없음")} Ray: {(ray != null ? "OK" : "없음")}");
            }

            // XRUIInputModule 존재 확인
            var uiModule = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
            lines.AppendLine($"XRUIInputModule: {(uiModule != null ? "OK" : "없음!")}");
            if (uiModule != null)
                lines.AppendLine($"  Fallback={uiModule.enableBuiltinActionsAsFallback} Touch={uiModule.enableTouchInput}");

            // InputActionManager 존재 확인
            var iam = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
            lines.AppendLine($"InputActionManager: {(iam != null ? $"OK ({iam.actionAssets?.Count ?? 0} assets)" : "없음!")}");

            // XREAL 입력 소스 상태
            try
            {
                var inputSrc = Unity.XR.XREAL.XREALPlugin.GetInputSource();
                bool htSupported = Unity.XR.XREAL.XREALPlugin.IsHandTrackingSupported();
                lines.AppendLine($"InputSource: {inputSrc} (HT지원={htSupported})");
            }
            catch (System.Exception e)
            {
                lines.AppendLine($"InputSource: 조회실패 ({e.Message})");
            }
#elif UNITY_EDITOR
            lines.AppendLine("에디터: 마우스 클릭으로 테스트");
#endif

            return lines.ToString().TrimEnd();
        }
    }
}

using UnityEngine;

namespace ARNavExperiment.DebugTools
{
    /// <summary>
    /// Editor Play 모드에서 WASD + 마우스로 이동/회전하는 테스트용 컨트롤러.
    /// XR Origin의 Camera에 붙여서 사용.
    /// 빌드 시 자동 비활성화 (UNITY_EDITOR 전용).
    /// </summary>
    public class EditorPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float sprintMultiplier = 2.5f;

        [Header("Mouse Look")]
        [SerializeField] private float mouseSensitivity = 2f;

        [Header("Info")]
        [SerializeField] private bool showDebugInfo = true;

        private float rotationX;
        private float rotationY;
        private bool cursorLocked;

#if !UNITY_EDITOR
        private void Awake()
        {
            // 빌드에서는 비활성화
            enabled = false;
        }
#endif

        private void Start()
        {
            // UI 조작을 위해 커서 잠금 해제 상태로 시작
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            cursorLocked = false;

            // TrackedPoseDriver가 에디터에서 위치/회전을 덮어쓰므로 비활성화
            var tpd = GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            if (tpd != null)
            {
                tpd.enabled = false;
                Debug.Log("[EditorPlayerController] TrackedPoseDriver 비활성화 (에디터 테스트 모드)");
            }

            var euler = transform.eulerAngles;
            rotationX = euler.x;
            rotationY = euler.y;
        }

        private void Update()
        {
            HandleCursorToggle();
            HandleDebugKeys();

            if (cursorLocked)
            {
                HandleMouseLook();
            }

            HandleMovement();
        }

        private void HandleCursorToggle()
        {
            // 우클릭 누르는 동안 시점 회전 모드
            if (Input.GetMouseButtonDown(1))
            {
                cursorLocked = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Input.GetMouseButtonUp(1))
            {
                cursorLocked = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void HandleMouseLook()
        {
            // 우클릭 드래그 중에만 시점 회전
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            rotationY += mouseX;
            rotationX -= mouseY;
            rotationX = Mathf.Clamp(rotationX, -80f, 80f);

            transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0);
        }

        private void HandleDebugKeys()
        {
            // N키: 실험 상태 다음 단계로 전환
            if (Input.GetKeyDown(KeyCode.N))
            {
                Core.ExperimentManager.Instance?.AdvanceState();
                Debug.Log("[EditorTest] N키 → AdvanceState");
            }

            // M키: 다음 미션 시작
            if (Input.GetKeyDown(KeyCode.M))
            {
                Mission.MissionManager.Instance?.StartNextMission();
                Debug.Log("[EditorTest] M키 → StartNextMission");
            }
        }

        private void HandleMovement()
        {
            float h = Input.GetAxis("Horizontal"); // A/D
            float v = Input.GetAxis("Vertical");   // W/S

            float speed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
                speed *= sprintMultiplier;

            // XZ 평면에서 이동 (Y 고정)
            var forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
            var right = new Vector3(transform.right.x, 0, transform.right.z).normalized;

            var move = (forward * v + right * h) * speed * Time.deltaTime;
            transform.position += move;

            // Y 높이 고정 (바닥 레벨)
            var pos = transform.position;
            pos.y = 1.6f; // 눈높이
            transform.position = pos;
        }

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.yellow }
            };

            // 화면 하단 좌측에 배치
            float y = Screen.height - 100;

            GUI.Label(new Rect(10, y, 500, 20), $"위치: ({transform.position.x:F1}, {transform.position.z:F1})", style);
            y += 18;

            var wpMgr = Navigation.WaypointManager.Instance;
            if (wpMgr != null)
            {
                var wp = wpMgr.CurrentWaypoint;
                if (wp != null)
                {
                    float dist = wpMgr.GetDistanceToNext();
                    GUI.Label(new Rect(10, y, 500, 20), $"다음 WP: {wp.waypointId} ({wp.locationName}) - {dist:F1}m", style);
                    y += 18;
                }
            }

            var missMgr = Mission.MissionManager.Instance;
            if (missMgr != null && missMgr.CurrentMission != null)
            {
                GUI.Label(new Rect(10, y, 500, 20),
                    $"미션: {missMgr.CurrentMission.missionId} ({missMgr.CurrentState})", style);
                y += 18;
            }

            var expMgr = Core.ExperimentManager.Instance;
            if (expMgr != null)
            {
                style.normal.textColor = Color.cyan;
                GUI.Label(new Rect(10, y, 500, 20), $"상태: {expMgr.CurrentState}", style);
                y += 18;
            }

            style.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(10, y, 700, 20), "WASD:이동 / Shift:달리기 / 우클릭드래그:시점 / N:다음단계 / M:다음미션", style);
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}

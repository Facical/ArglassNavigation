using UnityEngine;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;
using ARNavExperiment.Utils;

namespace ARNavExperiment.Navigation
{
    public class ARArrowRenderer : MonoBehaviour
    {
        [Header("Arrow Settings")]
        [SerializeField] private GameObject arrowObject;
        [SerializeField] private Color normalColor = new Color(0.204f, 0.596f, 0.859f); // #3498db
        [SerializeField] private Color triggerColor = new Color(0.902f, 0.494f, 0.133f); // #e67e22
        [SerializeField] private float rotationSmoothTime = 0.3f;

        [Header("View-Locked Position")]
        [SerializeField] private Vector3 offsetFromCamera = new Vector3(0, -0.15f, 0.8f);

        [Header("Fan Mode (T3)")]
        [SerializeField] private int fanArrowCount = 3; // 한쪽 방향 개수 (총 6개 + 중앙 = 7)

        private Renderer arrowRenderer;
        private float targetYaw;
        private float currentYaw;
        private float yawVelocity;
        private bool isVisible = false;
        public bool IsVisible => isVisible;
        private bool isTriggerActive;

        private GameObject[] fanObjects;
        private float[] fanOffsets;
        private bool isFanActive;
        private float lastCameraWarningTime;
        private bool isTransparentMode;
        private Color cachedOriginalColor;

        private void Start()
        {
            if (arrowObject != null)
                arrowRenderer = arrowObject.GetComponent<Renderer>();
            // 경로 로드 전까지 숨김 (로그 없이)
            isVisible = false;
            if (arrowObject) arrowObject.SetActive(false);
        }

        private float lastDebugLogTime;

        private void LateUpdate()
        {
            if (!isVisible || arrowObject == null) return;

            // auto-update direction from WaypointManager during normal navigation
            if (!isTriggerActive && WaypointManager.Instance != null)
            {
                var dir = WaypointManager.Instance.GetDirectionToNext();
                if (dir.sqrMagnitude > 0.001f)
                    targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

                /* 노이즈 로그 비활성화 — logcat 디버깅 시 ~150회/실험 반복
                if (Time.time - lastDebugLogTime > 2f)
                {
                    lastDebugLogTime = Time.time;
                    var wpMgr = WaypointManager.Instance;
                    bool isFallback = wpMgr.CurrentWaypoint?.anchorTransform == null;
                    var xrCam = XRCameraHelper.GetCamera();
                    Debug.Log($"[Arrow] dir=({dir.x:F2},{dir.y:F2},{dir.z:F2}) yaw={targetYaw:F1}° " +
                        $"wp={wpMgr.CurrentWaypoint?.waypointId} fallback={isFallback} " +
                        $"playerPos={xrCam?.transform.position}");
                }
                */
            }

            // 약한 보정 시 반투명 처리
            if (!isTriggerActive && arrowRenderer != null && WaypointManager.Instance != null)
            {
                bool shouldBeTransparent = WaypointManager.Instance.IsWeakCalibration();
                if (shouldBeTransparent && !isTransparentMode)
                {
                    cachedOriginalColor = arrowRenderer.material.color;
                    SetMaterialTransparent(arrowRenderer.material, 0.4f);
                    arrowRenderer.material.color = new Color(
                        cachedOriginalColor.r, cachedOriginalColor.g, cachedOriginalColor.b, 0.4f);
                    isTransparentMode = true;
                }
                else if (!shouldBeTransparent && isTransparentMode)
                {
                    arrowRenderer.material.color = cachedOriginalColor;
                    arrowRenderer.material.SetFloat("_Surface", 0); // Opaque
                    arrowRenderer.material.SetInt("_ZWrite", 1);
                    arrowRenderer.material.renderQueue = -1;
                    isTransparentMode = false;
                }
            }

            // view-locked positioning
            var viewCam = XRCameraHelper.GetCamera();
            if (viewCam != null)
            {
                var cam = viewCam.transform;
                arrowObject.transform.position = cam.TransformPoint(offsetFromCamera);
            }
            else if (Time.time - lastCameraWarningTime >= 5f)
            {
                lastCameraWarningTime = Time.time;
                Debug.LogWarning("[Arrow] 카메라를 찾을 수 없어 화살표 위치 갱신 불가");
            }

            // smooth rotation toward target direction
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, rotationSmoothTime);
            arrowObject.transform.rotation = Quaternion.Euler(0, currentYaw, 0);

            // fan mode: update fan arrow positions and rotations
            if (isFanActive && fanObjects != null)
            {
                for (int i = 0; i < fanObjects.Length; i++)
                {
                    if (fanObjects[i] == null) continue;
                    fanObjects[i].transform.position = arrowObject.transform.position;
                    fanObjects[i].transform.rotation = Quaternion.Euler(0, currentYaw + fanOffsets[i], 0);
                }
            }
        }

        public void UpdateDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        public void Show()
        {
            isVisible = true;
            if (arrowObject) arrowObject.SetActive(true);

            var cam = XRCameraHelper.GetCamera();
            Debug.Log($"[Arrow] Show() — arrowObject.active={arrowObject?.activeSelf}, " +
                $"renderer={arrowRenderer != null}, camera={cam?.name ?? "NULL"}, " +
                $"mesh={arrowObject?.GetComponent<MeshFilter>()?.mesh != null}");

            DomainEventBus.Instance?.Publish(ArrowShown.Default);
        }

        public void Hide()
        {
            isVisible = false;
            if (arrowObject) arrowObject.SetActive(false);
            DomainEventBus.Instance?.Publish(ArrowHidden.Default);
        }

        /// <summary>
        /// 이벤트 발행 없이 arrowObject의 가시성만 토글 (T1 깜빡임용).
        /// isVisible 플래그는 변경하지 않음.
        /// </summary>
        public void SetVisualOnly(bool visible)
        {
            if (arrowObject) arrowObject.SetActive(visible);
        }

        public void SetTriggerMode(bool active)
        {
            isTriggerActive = active;
            if (arrowRenderer != null)
                arrowRenderer.material.color = active ? triggerColor : normalColor;
        }

        public void SetFanMode(bool active, float spreadAngle)
        {
            if (active)
            {
                // 기존 팬이 있으면 정리
                CleanupFanObjects();

                int totalFan = fanArrowCount * 2; // 양쪽
                fanObjects = new GameObject[totalFan];
                fanOffsets = new float[totalFan];

                float step = spreadAngle / fanArrowCount; // 예: 30/3 = 10°

                for (int i = 0; i < fanArrowCount; i++)
                {
                    float angle = step * (i + 1);
                    float alpha = 1f - (float)(i + 1) / (fanArrowCount + 1); // 0.75, 0.5, 0.25 for count=3

                    // +angle 쪽
                    int idxPos = i * 2;
                    fanObjects[idxPos] = Instantiate(arrowObject, arrowObject.transform.parent);
                    fanObjects[idxPos].name = $"FanArrow_+{angle:F0}";
                    fanOffsets[idxPos] = angle;
                    SetupFanArrow(fanObjects[idxPos], alpha);

                    // -angle 쪽
                    int idxNeg = i * 2 + 1;
                    fanObjects[idxNeg] = Instantiate(arrowObject, arrowObject.transform.parent);
                    fanObjects[idxNeg].name = $"FanArrow_-{angle:F0}";
                    fanOffsets[idxNeg] = -angle;
                    SetupFanArrow(fanObjects[idxNeg], alpha);
                }

                isFanActive = true;
            }
            else
            {
                CleanupFanObjects();
                isFanActive = false;
            }
        }

        private void SetupFanArrow(GameObject fanArrow, float alpha)
        {
            fanArrow.SetActive(true);
            var renderer = fanArrow.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(renderer.material);
                SetMaterialTransparent(renderer.material, alpha);
                var c = triggerColor;
                renderer.material.color = new Color(c.r, c.g, c.b, alpha);
            }
        }

        private void CleanupFanObjects()
        {
            if (fanObjects == null) return;
            for (int i = 0; i < fanObjects.Length; i++)
            {
                if (fanObjects[i] != null)
                    Destroy(fanObjects[i]);
            }
            fanObjects = null;
            fanOffsets = null;
        }

        private void SetMaterialTransparent(Material mat, float alpha)
        {
            // URP Lit 호환
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha
            mat.SetOverrideTag("RenderType", "Transparent");

            // Standard/URP 공통 블렌드 설정
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }
}

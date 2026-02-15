using UnityEngine;
using ARNavExperiment.Logging;

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
        [SerializeField] private Vector3 offsetFromCamera = new Vector3(0, -0.15f, 0.5f);

        private Renderer arrowRenderer;
        private float targetYaw;
        private float currentYaw;
        private float yawVelocity;
        private bool isVisible = true;
        private bool isTriggerActive;

        private void Start()
        {
            if (arrowObject != null)
                arrowRenderer = arrowObject.GetComponent<Renderer>();
        }

        private void LateUpdate()
        {
            if (!isVisible || arrowObject == null) return;

            // view-locked positioning
            if (Camera.main != null)
            {
                var cam = Camera.main.transform;
                arrowObject.transform.position = cam.TransformPoint(offsetFromCamera);
            }

            // smooth rotation toward target direction
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, rotationSmoothTime);
            arrowObject.transform.rotation = Quaternion.Euler(0, currentYaw, 0);
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
            EventLogger.Instance?.LogEvent("GLASS_ARROW_SHOWN",
                extraData: $"{{\"angle\":{targetYaw:F0}}}");
        }

        public void Hide()
        {
            isVisible = false;
            if (arrowObject) arrowObject.SetActive(false);
            EventLogger.Instance?.LogEvent("GLASS_ARROW_HIDDEN");
        }

        public void SetTriggerMode(bool active)
        {
            isTriggerActive = active;
            if (arrowRenderer != null)
                arrowRenderer.material.color = active ? triggerColor : normalColor;
        }
    }
}

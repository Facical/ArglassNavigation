using UnityEngine;

namespace ARNavExperiment.Logging
{
    public class DeviceStateTracker : MonoBehaviour
    {
        public static DeviceStateTracker Instance { get; private set; }

        public bool IsBeamProActive { get; private set; }

        [SerializeField] private float liftThreshold = 0.5f;
        [SerializeField] private float idleTimeout = 5f;

        private float lastMotionTime;
        private float screenOnTime;
        private Vector3 lastAccel;

        public event System.Action OnBeamProScreenOn;
        public event System.Action<float> OnBeamProScreenOff;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            Vector3 accel = Input.acceleration;
            float delta = (accel - lastAccel).magnitude;
            lastAccel = accel;

            if (!IsBeamProActive)
            {
                if (delta > liftThreshold)
                {
                    ActivateScreen();
                }
            }
            else
            {
                if (delta > 0.05f)
                    lastMotionTime = Time.time;

                if (Time.time - lastMotionTime > idleTimeout)
                {
                    DeactivateScreen();
                }
            }
        }

        public void ActivateScreen()
        {
            if (IsBeamProActive) return;
            IsBeamProActive = true;
            screenOnTime = Time.time;
            lastMotionTime = Time.time;
            OnBeamProScreenOn?.Invoke();
            EventLogger.Instance?.LogEvent("BEAM_SCREEN_ON", deviceActive: "beam_pro");
        }

        public void DeactivateScreen()
        {
            if (!IsBeamProActive) return;
            IsBeamProActive = false;
            float duration = Time.time - screenOnTime;
            OnBeamProScreenOff?.Invoke(duration);
            EventLogger.Instance?.LogEvent("BEAM_SCREEN_OFF", deviceActive: "glass",
                extraData: $"{{\"duration_s\":{duration:F1}}}");
        }

        public void SetLocked(bool locked)
        {
            if (locked && IsBeamProActive)
                DeactivateScreen();
            enabled = !locked;
        }
    }
}

using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

namespace ARNavExperiment.Logging
{
    public class HeadTracker : MonoBehaviour
    {
        public static HeadTracker Instance { get; private set; }

        public Vector3 CurrentRotation { get; private set; }
        public Vector3 CurrentPosition { get; private set; }
        public float AngularVelocityYaw { get; private set; }

        [SerializeField] private float sampleRate = 10f;
        private float nextSampleTime;
        private float previousYaw;
        private bool hasPreviousYaw;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (Time.time < nextSampleTime) return;
            float dt = 1f / sampleRate;
            nextSampleTime = Time.time + dt;

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            bool gotXR = false;

            var devices = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.Head, devices);
            if (devices.Count > 0)
            {
                if (devices[0].TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
                    gotXR = true;
                devices[0].TryGetFeatureValue(CommonUsages.devicePosition, out position);
            }

            if (!gotXR && Camera.main != null)
            {
                rotation = Camera.main.transform.rotation;
                position = Camera.main.transform.position;
            }

            CurrentRotation = rotation.eulerAngles;
            CurrentPosition = position;

            float currentYaw = CurrentRotation.y;
            if (hasPreviousYaw)
            {
                float deltaYaw = Mathf.DeltaAngle(previousYaw, currentYaw);
                AngularVelocityYaw = deltaYaw / dt;
            }
            previousYaw = currentYaw;
            hasPreviousYaw = true;
        }
    }
}

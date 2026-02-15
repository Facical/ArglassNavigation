using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

namespace ARNavExperiment.Logging
{
    public class HeadTracker : MonoBehaviour
    {
        public static HeadTracker Instance { get; private set; }

        public Vector3 CurrentRotation { get; private set; }

        [SerializeField] private float sampleRate = 10f;
        private float nextSampleTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (Time.time < nextSampleTime) return;
            nextSampleTime = Time.time + (1f / sampleRate);

            var devices = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.Head, devices);
            if (devices.Count > 0 && devices[0].TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                CurrentRotation = rotation.eulerAngles;
            }
            else if (Camera.main != null)
            {
                CurrentRotation = Camera.main.transform.eulerAngles;
            }
        }
    }
}

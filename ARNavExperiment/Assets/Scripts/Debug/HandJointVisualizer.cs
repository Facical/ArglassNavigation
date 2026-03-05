using System.Collections.Generic;
using UnityEngine;
#if XR_HANDS
using UnityEngine.XR.Hands;
#endif

namespace ARNavExperiment.DebugTools
{
    /// <summary>
    /// Unity XR Hands HandVisualizer 패턴 기반 손 관절 시각화.
    /// XRHandSubsystem에서 관절 데이터를 받아 디버그 큐브 + 연결선으로 렌더링.
    /// XREAL 문서 권장 방식(HandVisualizer)의 핵심 로직을 외부 에셋 의존 없이 구현.
    ///
    /// 참고: Unity XR Hands 1.4.1 Samples~/HandVisualizer/Scripts/HandVisualizer.cs
    /// </summary>
    public class HandJointVisualizer : MonoBehaviour
    {
#if XR_HANDS
        [Header("시각화 설정")]
        [SerializeField] private bool drawJoints = true;
        [SerializeField] private bool drawConnections = true;
        [SerializeField] private float jointSize = 0.008f;
        [SerializeField] private float connectionWidth = 0.002f;

        [Header("색상")]
        [SerializeField] private Color jointColor = Color.cyan;
        [SerializeField] private Color fingertipColor = Color.green;
        [SerializeField] private Color wristColor = Color.yellow;
        [SerializeField] private Color connectionColor = new Color(1f, 1f, 1f, 0.5f);

        private XRHandSubsystem subsystem;
        private HandJointObjects leftHand;
        private HandJointObjects rightHand;

        private Material jointMat;
        private Material fingertipMat;
        private Material wristMat;
        private Material lineMat;

        private static readonly List<XRHandSubsystem> s_Subsystems = new();

        // 핑거팁 관절 ID
        private static readonly HashSet<XRHandJointID> s_FingertipJoints = new()
        {
            XRHandJointID.ThumbTip,
            XRHandJointID.IndexTip,
            XRHandJointID.MiddleTip,
            XRHandJointID.RingTip,
            XRHandJointID.LittleTip
        };

        // === HandVisualizer 패턴: Update()에서 서브시스템 폴링 ===
        private void Update()
        {
            if (subsystem != null && subsystem.running)
                return;

            // 서브시스템 검색
            SubsystemManager.GetSubsystems(s_Subsystems);
            bool found = false;
            foreach (var sub in s_Subsystems)
            {
                if (sub.running)
                {
                    Unsubscribe();
                    subsystem = sub;
                    found = true;
                    break;
                }
            }

            if (!found)
                return;

            // 매터리얼 초기화
            if (jointMat == null)
                CreateMaterials();

            // 손 오브젝트 초기화
            if (leftHand == null)
                leftHand = new HandJointObjects("L", transform);
            if (rightHand == null)
                rightHand = new HandJointObjects("R", transform);

            Subscribe();

            Debug.Log("[HandJointVisualizer] 서브시스템 연결 완료");
        }

        private void OnDisable()
        {
            Unsubscribe();
            SetVisibility(leftHand, false);
            SetVisibility(rightHand, false);
        }

        private void OnDestroy()
        {
            leftHand?.Destroy();
            rightHand?.Destroy();
            leftHand = null;
            rightHand = null;
            DestroyMaterials();
        }

        private void Subscribe()
        {
            if (subsystem == null) return;
            subsystem.trackingAcquired += OnTrackingAcquired;
            subsystem.trackingLost += OnTrackingLost;
            subsystem.updatedHands += OnUpdatedHands;
        }

        private void Unsubscribe()
        {
            if (subsystem == null) return;
            subsystem.trackingAcquired -= OnTrackingAcquired;
            subsystem.trackingLost -= OnTrackingLost;
            subsystem.updatedHands -= OnUpdatedHands;
            subsystem = null;
        }

        private void OnTrackingAcquired(XRHand hand)
        {
            var target = hand.handedness == Handedness.Left ? leftHand : rightHand;
            SetVisibility(target, true);
        }

        private void OnTrackingLost(XRHand hand)
        {
            var target = hand.handedness == Handedness.Left ? leftHand : rightHand;
            SetVisibility(target, false);
        }

        private void OnUpdatedHands(XRHandSubsystem sub,
            XRHandSubsystem.UpdateSuccessFlags flags,
            XRHandSubsystem.UpdateType updateType)
        {
            // HandVisualizer 패턴: Dynamic 업데이트는 스킵
            if (updateType == XRHandSubsystem.UpdateType.Dynamic)
                return;

            if ((flags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0)
                UpdateJoints(sub.leftHand, leftHand);
            else
                SetVisibility(leftHand, false);

            if ((flags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0)
                UpdateJoints(sub.rightHand, rightHand);
            else
                SetVisibility(rightHand, false);
        }

        private void UpdateJoints(XRHand hand, HandJointObjects objects)
        {
            if (objects == null) return;

            // Wrist
            var wristJoint = hand.GetJoint(XRHandJointID.Wrist);
            UpdateSingleJoint(XRHandJointID.Wrist, wristJoint, objects);

            // Palm
            var palmJoint = hand.GetJoint(XRHandJointID.Palm);
            UpdateSingleJoint(XRHandJointID.Palm, palmJoint, objects);

            // 각 손가락 관절 업데이트 + 연결선
            for (int finger = (int)XRHandFingerID.Thumb;
                 finger <= (int)XRHandFingerID.Little; finger++)
            {
                var fingerId = (XRHandFingerID)finger;
                var frontIndex = fingerId.GetFrontJointID().ToIndex();
                var backIndex = fingerId.GetBackJointID().ToIndex();

                int parentIdx = XRHandJointID.Wrist.ToIndex();

                for (int ji = frontIndex; ji <= backIndex; ji++)
                {
                    var jointId = XRHandJointIDUtility.FromIndex(ji);
                    var joint = hand.GetJoint(jointId);

                    if (UpdateSingleJoint(jointId, joint, objects) && drawConnections)
                    {
                        // 부모-자식 연결선
                        var line = objects.GetLine(jointId);
                        if (line != null && jointId != XRHandJointID.Wrist)
                        {
                            var parentGo = objects.GetJointGO(XRHandJointIDUtility.FromIndex(parentIdx));
                            var childGo = objects.GetJointGO(jointId);
                            if (parentGo != null && childGo != null &&
                                parentGo.activeSelf && childGo.activeSelf)
                            {
                                line.enabled = true;
                                line.SetPosition(0, parentGo.transform.position);
                                line.SetPosition(1, childGo.transform.position);
                            }
                        }
                    }
                    parentIdx = ji;
                }
            }
        }

        private bool UpdateSingleJoint(XRHandJointID jointId, XRHandJoint joint,
            HandJointObjects objects)
        {
            if (!joint.TryGetPose(out Pose pose))
            {
                var go = objects.GetJointGO(jointId);
                if (go != null) go.SetActive(false);
                return false;
            }

            var jointGO = objects.GetOrCreateJoint(jointId, jointSize, jointMat,
                fingertipMat, wristMat, lineMat, connectionWidth);
            if (drawJoints)
            {
                jointGO.SetActive(true);
                jointGO.transform.position = pose.position;
                jointGO.transform.rotation = pose.rotation;
            }
            return true;
        }

        private void SetVisibility(HandJointObjects objects, bool visible)
        {
            objects?.SetAllVisible(visible);
        }

        private void CreateMaterials()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            jointMat = new Material(shader) { color = jointColor };
            fingertipMat = new Material(shader) { color = fingertipColor };
            wristMat = new Material(shader) { color = wristColor };
            lineMat = new Material(shader) { color = connectionColor };
        }

        private void DestroyMaterials()
        {
            if (jointMat != null) Destroy(jointMat);
            if (fingertipMat != null) Destroy(fingertipMat);
            if (wristMat != null) Destroy(wristMat);
            if (lineMat != null) Destroy(lineMat);
        }

        /// <summary>
        /// 한 손의 관절 오브젝트를 관리하는 내부 클래스.
        /// HandVisualizer.HandGameObjects 패턴 참조.
        /// </summary>
        private class HandJointObjects
        {
            private readonly string side;
            private readonly Transform parent;
            private readonly Dictionary<XRHandJointID, GameObject> joints = new();
            private readonly Dictionary<XRHandJointID, LineRenderer> lines = new();

            public HandJointObjects(string side, Transform parent)
            {
                this.side = side;
                this.parent = parent;
            }

            public GameObject GetJointGO(XRHandJointID id)
            {
                joints.TryGetValue(id, out var go);
                return go;
            }

            public LineRenderer GetLine(XRHandJointID id)
            {
                lines.TryGetValue(id, out var line);
                return line;
            }

            public GameObject GetOrCreateJoint(XRHandJointID id, float size,
                Material defaultMat, Material tipMat, Material wristMat,
                Material lineMat, float lineWidth)
            {
                if (joints.TryGetValue(id, out var existing))
                    return existing;

                // 관절 큐브 생성 (HandVisualizer Joint.prefab 패턴)
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"{side}_{id}";
                go.transform.SetParent(parent, false);

                var collider = go.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);

                // 관절 종류별 크기/색상
                float scale;
                Material mat;
                if (id == XRHandJointID.Wrist || id == XRHandJointID.Palm)
                {
                    scale = size * 1.5f;
                    mat = wristMat;
                }
                else if (s_FingertipJoints.Contains(id))
                {
                    scale = size * 1.25f;
                    mat = tipMat;
                }
                else
                {
                    scale = size;
                    mat = defaultMat;
                }

                go.transform.localScale = Vector3.one * scale;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null) renderer.material = mat;

                // LineRenderer 추가 (관절 연결선, HandVisualizer 패턴)
                var line = go.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                line.material = lineMat;
                line.useWorldSpace = true;
                line.enabled = false;

                joints[id] = go;
                lines[id] = line;
                return go;
            }

            public void SetAllVisible(bool visible)
            {
                foreach (var kvp in joints)
                {
                    if (kvp.Value != null)
                        kvp.Value.SetActive(visible);
                }
                if (!visible)
                {
                    foreach (var kvp in lines)
                    {
                        if (kvp.Value != null)
                            kvp.Value.enabled = false;
                    }
                }
            }

            public void Destroy()
            {
                foreach (var kvp in joints)
                    if (kvp.Value != null) Object.Destroy(kvp.Value);
                joints.Clear();
                lines.Clear();
            }
        }
#endif
    }
}

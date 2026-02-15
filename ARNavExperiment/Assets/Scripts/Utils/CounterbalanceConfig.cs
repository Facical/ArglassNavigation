using UnityEngine;

namespace ARNavExperiment.Utils
{
    [CreateAssetMenu(fileName = "CounterbalanceConfig", menuName = "Experiment/CounterbalanceConfig")]
    public class CounterbalanceConfig : ScriptableObject
    {
        [Header("Group Assignments (24 participants)")]
        [Tooltip("S1: Glass Only first, then Hybrid\nS2: Hybrid first, then Glass Only")]
        public GroupAssignment[] assignments = new GroupAssignment[24];

        public GroupAssignment GetAssignment(int participantNumber)
        {
            int index = participantNumber - 1;
            if (index >= 0 && index < assignments.Length)
                return assignments[index];

            // default: alternating groups
            return new GroupAssignment
            {
                participantId = $"P{participantNumber:D2}",
                orderGroup = participantNumber % 2 == 1 ? "S1" : "S2"
            };
        }

        [ContextMenu("Generate Default Assignments")]
        private void GenerateDefaults()
        {
            assignments = new GroupAssignment[24];
            for (int i = 0; i < 24; i++)
            {
                assignments[i] = new GroupAssignment
                {
                    participantId = $"P{(i + 1):D2}",
                    orderGroup = (i + 1) % 2 == 1 ? "S1" : "S2"
                };
            }
        }
    }

    [System.Serializable]
    public class GroupAssignment
    {
        public string participantId;
        public string orderGroup;
    }
}

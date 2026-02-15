using UnityEngine;

namespace ARNavExperiment.Mission
{
    public enum MissionType
    {
        A_DirectionVerify,
        B_AmbiguousDecision,
        C_InfoIntegration
    }

    [CreateAssetMenu(fileName = "Mission", menuName = "Experiment/MissionData")]
    public class MissionData : ScriptableObject
    {
        public string missionId;
        public MissionType type;

        [TextArea(2, 5)]
        public string briefingText;

        public string targetWaypointId;

        [TextArea(1, 3)]
        public string verificationQuestion;

        public string[] answerOptions;
        public int correctAnswerIndex;
        public string associatedTriggerId;

        public POIData[] relevantPOIs;
        public InfoCardData[] infoCards;
        public ComparisonData comparisonData;
        public Sprite[] referenceImages;
    }
}

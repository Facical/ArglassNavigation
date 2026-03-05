using UnityEngine;
using ARNavExperiment.Core;

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

        [Header("Korean Localization")]
        [TextArea(2, 5)]
        public string briefingTextKo;

        [TextArea(1, 3)]
        public string verificationQuestionKo;

        public string[] answerOptionsKo;

        public string GetBriefingText()
        {
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.CurrentLanguage == Language.KO
                && !string.IsNullOrEmpty(briefingTextKo))
                return briefingTextKo;
            return briefingText;
        }

        public string GetVerificationQuestion()
        {
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.CurrentLanguage == Language.KO
                && !string.IsNullOrEmpty(verificationQuestionKo))
                return verificationQuestionKo;
            return verificationQuestion;
        }

        public string GetAnswerOption(int i)
        {
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.CurrentLanguage == Language.KO
                && answerOptionsKo != null && i < answerOptionsKo.Length && !string.IsNullOrEmpty(answerOptionsKo[i]))
                return answerOptionsKo[i];
            return (answerOptions != null && i < answerOptions.Length) ? answerOptions[i] : "";
        }

        public string[] GetAnswerOptions()
        {
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.CurrentLanguage == Language.KO
                && answerOptionsKo != null && answerOptionsKo.Length > 0)
                return answerOptionsKo;
            return answerOptions;
        }
    }
}

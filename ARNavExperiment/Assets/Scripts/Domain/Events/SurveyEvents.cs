namespace ARNavExperiment.Domain.Events
{
    /// <summary>
    /// 설문 항목 응답 시 발행 (NASA-TLX / Trust).
    /// </summary>
    public readonly struct SurveyItemAnswered : IDomainEvent
    {
        public readonly string SurveyType; // "nasa_tlx" | "trust"
        public readonly string ItemKey;
        public readonly int Rating; // 1-7
        public readonly int RunNumber; // 1 or 2
        public readonly string Condition;

        public SurveyItemAnswered(string surveyType, string itemKey, int rating,
            int runNumber, string condition)
        {
            SurveyType = surveyType;
            ItemKey = itemKey;
            Rating = rating;
            RunNumber = runNumber;
            Condition = condition;
        }
    }

    /// <summary>
    /// 설문 섹션 완료 시 발행.
    /// </summary>
    public readonly struct SurveyCompleted : IDomainEvent
    {
        public readonly string SurveyType; // "nasa_tlx" | "trust" | "post_condition"
        public readonly int RunNumber;
        public readonly string Condition;
        public readonly float DurationSec;

        public SurveyCompleted(string surveyType, int runNumber, string condition, float durationSec)
        {
            SurveyType = surveyType;
            RunNumber = runNumber;
            Condition = condition;
            DurationSec = durationSec;
        }
    }

    /// <summary>
    /// 비교 설문 항목 응답 시 발행.
    /// </summary>
    public readonly struct ComparisonSurveyAnswered : IDomainEvent
    {
        public readonly string QuestionKey;
        public readonly string Answer;
        public readonly string ResponseType; // "choice" | "text"

        public ComparisonSurveyAnswered(string questionKey, string answer, string responseType)
        {
            QuestionKey = questionKey;
            Answer = answer;
            ResponseType = responseType;
        }
    }

    /// <summary>
    /// 비교 설문 완료 시 발행.
    /// </summary>
    public readonly struct ComparisonSurveyCompleted : IDomainEvent
    {
        public readonly float DurationSec;

        public ComparisonSurveyCompleted(float durationSec)
        {
            DurationSec = durationSec;
        }
    }
}

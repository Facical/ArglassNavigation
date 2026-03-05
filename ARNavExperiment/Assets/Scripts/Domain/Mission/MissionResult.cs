namespace ARNavExperiment.Domain.Mission
{
    /// <summary>
    /// 미션 실행 결과를 담는 값 객체.
    /// MissionManager → MissionOrchestrator 분리 시 상태 전달용.
    /// </summary>
    public readonly struct MissionResult
    {
        public readonly string MissionId;
        public readonly bool Correct;
        public readonly int SelectedAnswerIndex;
        public readonly float ResponseTimeSeconds;
        public readonly int ConfidenceRating;
        public readonly int DifficultyRating;
        public readonly float TotalDurationSeconds;

        public MissionResult(string missionId, bool correct, int selectedAnswerIndex,
            float responseTimeSeconds, int confidenceRating, int difficultyRating,
            float totalDurationSeconds)
        {
            MissionId = missionId;
            Correct = correct;
            SelectedAnswerIndex = selectedAnswerIndex;
            ResponseTimeSeconds = responseTimeSeconds;
            ConfidenceRating = confidenceRating;
            DifficultyRating = difficultyRating;
            TotalDurationSeconds = totalDurationSeconds;
        }
    }
}

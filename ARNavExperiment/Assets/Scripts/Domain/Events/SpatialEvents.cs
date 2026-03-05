namespace ARNavExperiment.Domain.Events
{
    /// <summary>
    /// 재인식 프로세스 시작 시 발행.
    /// </summary>
    public readonly struct RelocalizationStarted : IDomainEvent
    {
        public static readonly RelocalizationStarted Default = new RelocalizationStarted();
    }

    /// <summary>
    /// 재인식 진행 상황 업데이트 시 발행.
    /// </summary>
    public readonly struct RelocalizationProgress : IDomainEvent
    {
        public readonly int Total;
        public readonly int Success;
        public readonly int Fail;

        public RelocalizationProgress(int total, int success, int fail)
        {
            Total = total;
            Success = success;
            Fail = fail;
        }
    }

    /// <summary>
    /// 재인식 완료 시 발행.
    /// </summary>
    public readonly struct RelocalizationCompleted : IDomainEvent
    {
        public readonly float SuccessRate;
        public readonly string Action; // "complete", "retry", "proceed_partial"

        public RelocalizationCompleted(float successRate, string action)
        {
            SuccessRate = successRate;
            Action = action;
        }
    }

    /// <summary>
    /// 앵커 지연 복구 시 발행.
    /// </summary>
    public readonly struct AnchorLateRecovered : IDomainEvent
    {
        public readonly string WaypointId;
        public readonly string AnchorGuid;

        public AnchorLateRecovered(string waypointId, string anchorGuid)
        {
            WaypointId = waypointId;
            AnchorGuid = anchorGuid;
        }
    }

    /// <summary>
    /// 앵커 저장 완료 시 발행 (매핑 모드).
    /// </summary>
    public readonly struct AnchorSaved : IDomainEvent
    {
        public readonly string WaypointId;
        public readonly string AnchorGuid;
        public readonly string Quality;

        public AnchorSaved(string waypointId, string anchorGuid, string quality)
        {
            WaypointId = waypointId;
            AnchorGuid = anchorGuid;
            Quality = quality;
        }
    }

    /// <summary>
    /// 앵커 진단 정보 발행.
    /// </summary>
    public readonly struct AnchorDiagnostics : IDomainEvent
    {
        public readonly string DiagnosticsJson;

        public AnchorDiagnostics(string diagnosticsJson)
        {
            DiagnosticsJson = diagnosticsJson;
        }
    }
}

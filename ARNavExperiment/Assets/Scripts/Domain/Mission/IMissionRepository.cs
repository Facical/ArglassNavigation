using System.Collections.Generic;

namespace ARNavExperiment.Domain.Mission
{
    /// <summary>
    /// 미션 데이터 로드 추상화.
    /// Phase 3에서 MissionManager의 SO 직접 참조를 이 인터페이스로 교체.
    /// </summary>
    public interface IMissionRepository
    {
        /// <summary>경로별 미션 목록 로드 (순서: A1→B1→A2→B2→C1)</summary>
        List<IMissionReadModel> GetMissionsForRoute(string routeId);
    }

    /// <summary>
    /// MissionData SO의 읽기 전용 인터페이스.
    /// Domain 계층이 SO에 직접 의존하지 않도록 추상화.
    /// </summary>
    public interface IMissionReadModel
    {
        string MissionId { get; }
        string Type { get; }
        string TargetWaypointId { get; }
        string BriefingText { get; }
        int CorrectAnswerIndex { get; }
        string AssociatedTriggerId { get; }
    }
}

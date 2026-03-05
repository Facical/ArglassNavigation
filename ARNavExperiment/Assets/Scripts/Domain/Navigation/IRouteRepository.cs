using System.Collections.Generic;

namespace ARNavExperiment.Domain.Navigation
{
    /// <summary>
    /// 경로/웨이포인트 데이터 로드 추상화.
    /// Phase 3에서 WaypointManager의 SO 직접 참조를 이 인터페이스로 교체.
    /// </summary>
    public interface IRouteRepository
    {
        /// <summary>경로에 포함된 웨이포인트 ID 목록 반환</summary>
        List<string> GetWaypointIds(string routeId);
    }
}

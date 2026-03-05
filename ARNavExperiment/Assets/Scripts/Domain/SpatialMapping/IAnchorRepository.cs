using System.Collections.Generic;

namespace ARNavExperiment.Domain.SpatialMapping
{
    /// <summary>
    /// 앵커 매핑 데이터 저장/로드 추상화.
    /// Phase 3에서 SpatialAnchorManager의 JSON 직접 I/O를 이 인터페이스로 교체.
    /// </summary>
    public interface IAnchorRepository
    {
        /// <summary>경로별 앵커 매핑 데이터 로드</summary>
        List<AnchorMapping> LoadMappings(string routeId);

        /// <summary>앵커 매핑 데이터 저장</summary>
        void SaveMapping(AnchorMapping mapping);
    }

    /// <summary>
    /// 앵커-웨이포인트 매핑 데이터 (순수 C# 값 객체).
    /// </summary>
    public readonly struct AnchorMapping
    {
        public readonly string WaypointId;
        public readonly string AnchorGuid;
        public readonly float Radius;
        public readonly string LocationName;

        public AnchorMapping(string waypointId, string anchorGuid, float radius, string locationName)
        {
            WaypointId = waypointId;
            AnchorGuid = anchorGuid;
            Radius = radius;
            LocationName = locationName;
        }
    }
}

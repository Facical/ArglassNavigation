using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARNavExperiment.Core
{
    /// <summary>
    /// 마커 이미지 ↔ 웨이포인트 매핑 데이터.
    /// Image Tracking에서 감지된 마커를 도면 좌표와 연결합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "MarkerMappingData", menuName = "ARNav/Marker Mapping Data")]
    public class MarkerMappingData : ScriptableObject
    {
        public List<MarkerWaypointMapping> mappings = new();

        /// <summary>마커 이름으로 매핑 검색</summary>
        public MarkerWaypointMapping GetByName(string markerName)
        {
            foreach (var m in mappings)
            {
                if (m.markerName == markerName)
                    return m;
            }
            return null;
        }

        /// <summary>웨이포인트 ID로 매핑 검색</summary>
        public MarkerWaypointMapping GetByWaypointId(string waypointId)
        {
            foreach (var m in mappings)
            {
                if (m.waypointId == waypointId)
                    return m;
            }
            return null;
        }
    }

    [Serializable]
    public class MarkerWaypointMapping
    {
        [Tooltip("XRReferenceImageLibrary에 등록된 이미지 이름")]
        public string markerName;       // "MARKER_WP01"

        [Tooltip("매핑된 웨이포인트 ID")]
        public string waypointId;       // "B_WP01"

        [Tooltip("도면 좌표 (WaypointDataGenerator의 fallbackPosition과 동일)")]
        public Vector3 floorPlanPosition;

        [Tooltip("마커가 바라보는 방향 (도면 기준 yaw, 도 단위)")]
        public float markerHeading;
    }
}

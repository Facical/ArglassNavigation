using UnityEngine;

namespace ARNavExperiment.Navigation
{
    /// <summary>
    /// 19개 호실 도면 좌표 정적 레지스트리.
    /// 보정 앵커(Reference Anchor)의 도면 좌표를 제공하여
    /// SLAM↔도면 좌표 보정 정밀도를 극대화합니다.
    /// </summary>
    public static class ReferencePointRegistry
    {
        public readonly struct ReferencePoint
        {
            public readonly string RoomId;
            public readonly string DisplayName;
            public readonly Vector2 FloorPlanXZ;

            public ReferencePoint(string roomId, string displayName, float x, float z)
            {
                RoomId = roomId;
                DisplayName = displayName;
                FloorPlanXZ = new Vector2(x, z);
            }
        }

        /// <summary>19개 호실 보정 앵커 도면 좌표</summary>
        public static readonly ReferencePoint[] AllPoints = new[]
        {
            // 동쪽 복도 서쪽 벽 (x=36) — 14개
            new ReferencePoint("REF_B114", "B114", 36, 3),
            new ReferencePoint("REF_B113", "B113", 36, 8),
            new ReferencePoint("REF_B112", "B112", 36, 13),
            new ReferencePoint("REF_B111", "B111", 36, 18),
            new ReferencePoint("REF_B110", "B110", 36, 24),
            new ReferencePoint("REF_B109", "B109", 36, 28),
            new ReferencePoint("REF_B108", "B108", 36, 33),
            new ReferencePoint("REF_B107", "B107", 36, 37),
            new ReferencePoint("REF_B106", "B106", 36, 39),
            new ReferencePoint("REF_B105", "B105", 36, 45),
            new ReferencePoint("REF_B104", "B104", 36, 51),
            new ReferencePoint("REF_B103", "B103", 36, 57),
            new ReferencePoint("REF_B102", "B102", 36, 63),
            new ReferencePoint("REF_B101", "B101", 36, 66),
            // 동쪽 복도 동쪽 벽 (x=39) — 4개
            new ReferencePoint("REF_B119", "B119", 39, 24),
            new ReferencePoint("REF_B118", "B118", 39, 33),
            new ReferencePoint("REF_B117", "B117", 39, 42),
            new ReferencePoint("REF_B116", "B116", 39, 57),
            // B121 — 1개
            new ReferencePoint("REF_B121", "B121", 36, -7),
        };

        /// <summary>roomId로 ReferencePoint를 검색합니다.</summary>
        public static ReferencePoint? GetByRoomId(string roomId)
        {
            foreach (var pt in AllPoints)
            {
                if (pt.RoomId == roomId) return pt;
            }
            return null;
        }

        /// <summary>roomId로 도면 좌표를 반환합니다.</summary>
        public static Vector2? GetFloorPlanPosition(string roomId)
        {
            var pt = GetByRoomId(roomId);
            return pt?.FloorPlanXZ;
        }
    }
}

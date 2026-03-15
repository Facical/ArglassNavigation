namespace ARNavExperiment.Domain.Events
{
    /// <summary>
    /// Beam Pro 화면 켜짐/꺼짐 시 발행.
    /// </summary>
    public readonly struct DeviceScreenChanged : IDomainEvent
    {
        public readonly bool IsOn;
        public readonly float DurationSec;

        public DeviceScreenChanged(bool isOn, float durationSec = 0f)
        {
            IsOn = isOn;
            DurationSec = durationSec;
        }
    }

    /// <summary>
    /// Beam Pro 탭 전환 시 발행.
    /// </summary>
    public readonly struct BeamTabSwitched : IDomainEvent
    {
        public readonly int TabIndex;
        public readonly string TabName;

        public BeamTabSwitched(int tabIndex, string tabName)
        {
            TabIndex = tabIndex;
            TabName = tabName;
        }
    }

    /// <summary>
    /// Beam Pro 정보 카드 열기/닫기 시 발행.
    /// </summary>
    public readonly struct BeamInfoCardToggled : IDomainEvent
    {
        public readonly string CardId;
        public readonly bool Opened;
        public readonly float ViewDurationSeconds;

        public BeamInfoCardToggled(string cardId, bool opened, float viewDurationSeconds = 0f)
        {
            CardId = cardId;
            Opened = opened;
            ViewDurationSeconds = viewDurationSeconds;
        }
    }

    /// <summary>
    /// Beam Pro POI 조회 시 발행.
    /// </summary>
    public readonly struct BeamPOIViewed : IDomainEvent
    {
        public readonly string POIId;
        public readonly float ViewDurationSeconds;

        public BeamPOIViewed(string poiId, float viewDurationSeconds = 0f)
        {
            POIId = poiId;
            ViewDurationSeconds = viewDurationSeconds;
        }
    }

    /// <summary>
    /// Beam Pro 비교 카드 조회 시 발행.
    /// </summary>
    public readonly struct BeamComparisonViewed : IDomainEvent
    {
        public readonly string ComparisonId;

        public BeamComparisonViewed(string comparisonId) { ComparisonId = comparisonId; }
    }

    /// <summary>
    /// Beam Pro 미션 참조 패널 조회 시 발행.
    /// </summary>
    public readonly struct BeamMissionRefViewed : IDomainEvent
    {
        public readonly string MissionId;

        public BeamMissionRefViewed(string missionId) { MissionId = missionId; }
    }

    /// <summary>
    /// Beam Pro 맵 줌 시 발행.
    /// </summary>
    public readonly struct BeamMapZoomed : IDomainEvent
    {
        public readonly float ZoomLevel;

        public BeamMapZoomed(float zoomLevel) { ZoomLevel = zoomLevel; }
    }

    /// <summary>
    /// 글래스 뷰 캡처 상태 변경 시 발행.
    /// </summary>
    public readonly struct GlassCaptureStateChanged : IDomainEvent
    {
        public readonly string State; // "start", "stop", "error"
        public readonly string FilePath;

        public GlassCaptureStateChanged(string state, string filePath = "")
        {
            State = state;
            FilePath = filePath;
        }
    }

    /// <summary>
    /// 앱 생명주기 이벤트 (pause/resume/focus_lost/focus_gained/quit).
    /// </summary>
    public readonly struct AppLifecycleEvent : IDomainEvent
    {
        public readonly string EventType;
        public readonly float TimeSinceStartup;

        public AppLifecycleEvent(string eventType, float timeSinceStartup)
        {
            EventType = eventType;
            TimeSinceStartup = timeSinceStartup;
        }
    }
}

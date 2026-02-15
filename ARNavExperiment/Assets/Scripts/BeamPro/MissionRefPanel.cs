using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Logging;
using ARNavExperiment.Mission;

namespace ARNavExperiment.BeamPro
{
    public class MissionRefPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI missionIdText;
        [SerializeField] private TextMeshProUGUI briefingText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Image[] referenceImages;

        private MissionData currentMission;
        private bool hasLogged;

        public void LoadMission(MissionData mission)
        {
            currentMission = mission;
            hasLogged = false;

            if (missionIdText) missionIdText.text = $"Mission {mission.missionId}";
            if (briefingText) briefingText.text = mission.briefingText;

            // mission type specific hints
            if (hintText)
            {
                hintText.text = mission.type switch
                {
                    MissionType.A_DirectionVerify => "목적지 POI를 지도에서 확인하세요",
                    MissionType.B_AmbiguousDecision => "주변 지도 정보를 참고하세요",
                    MissionType.C_InfoIntegration => "비교 대상의 속성을 확인하세요",
                    _ => ""
                };
            }

            // reference images
            if (referenceImages != null && mission.referenceImages != null)
            {
                for (int i = 0; i < referenceImages.Length; i++)
                {
                    if (i < mission.referenceImages.Length && mission.referenceImages[i] != null)
                    {
                        referenceImages[i].sprite = mission.referenceImages[i];
                        referenceImages[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        referenceImages[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (currentMission != null && !hasLogged)
            {
                hasLogged = true;
                EventLogger.Instance?.LogEvent("BEAM_MISSION_REF_VIEWED",
                    beamContentType: "mission_ref",
                    extraData: $"{{\"mission_id\":\"{currentMission.missionId}\",\"ref_type\":\"briefing_review\"}}");
            }
        }
    }
}

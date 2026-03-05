using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;

namespace ARNavExperiment.Presentation.BeamPro
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

            if (missionIdText) missionIdText.text = string.Format(
                LocalizationManager.Get("missionref.title"), mission.missionId);
            if (briefingText) briefingText.text = mission.GetBriefingText();

            // mission type specific hints
            if (hintText)
            {
                hintText.text = mission.type switch
                {
                    MissionType.A_DirectionVerify => LocalizationManager.Get("missionref.hint_a"),
                    MissionType.B_AmbiguousDecision => LocalizationManager.Get("missionref.hint_b"),
                    MissionType.C_InfoIntegration => LocalizationManager.Get("missionref.hint_c"),
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
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

            if (currentMission != null && !hasLogged)
            {
                hasLogged = true;
                DomainEventBus.Instance?.Publish(new BeamMissionRefViewed(currentMission.missionId));
            }
        }

        private void OnDisable()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(Language lang)
        {
            if (currentMission != null)
                LoadMission(currentMission);
        }
    }
}

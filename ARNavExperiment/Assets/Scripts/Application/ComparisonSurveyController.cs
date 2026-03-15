using UnityEngine;
using ARNavExperiment.Core;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Presentation.BeamPro;

namespace ARNavExperiment.Application
{
    /// <summary>
    /// 비교 설문 진행 컨트롤러. ComparisonSurvey 상태에서 활성화.
    /// ComparisonSurveyUI(BeamProCanvas)를 제어하며, 완료 시 ExperimentManager.AdvanceState() 호출.
    /// </summary>
    public class ComparisonSurveyController : MonoBehaviour
    {
        private ComparisonSurveyUI surveyUI;
        private float startTime;
        private bool isActive;
        private ExperimentCondition savedCondition;

        private void Awake()
        {
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged += OnStateChanged;
        }

        private void OnStateChanged(ExperimentState state)
        {
            if (state == ExperimentState.ComparisonSurvey)
            {
                StartComparisonSurvey();
            }
            else if (isActive)
            {
                isActive = false;
            }
        }

        private void StartComparisonSurvey()
        {
            isActive = true;
            startTime = Time.time;

            // BeamProCanvasController를 ScreenSpaceOverlay로 강제 전환
            var beamCtrl = FindObjectOfType<BeamProCanvasController>(true);
            if (beamCtrl != null)
            {
                // 현재 조건 저장 후 Hybrid 모드로 전환하여 ScreenSpaceOverlay 복원
                savedCondition = ConditionController.Instance?.CurrentCondition ?? ExperimentCondition.Hybrid;
                ConditionController.Instance?.SetCondition(ExperimentCondition.Hybrid);
            }

            // ComparisonSurveyUI 찾기 및 활성화
            surveyUI = FindObjectOfType<ComparisonSurveyUI>(true);
            if (surveyUI != null)
            {
                surveyUI.StartSurvey(OnItemAnswered, OnSurveyCompleted);
            }
            else
            {
                Debug.LogError("[ComparisonSurveyController] ComparisonSurveyUI를 찾을 수 없습니다!");
                // fallback: 즉시 완료
                OnSurveyCompleted();
            }

            Debug.Log("[ComparisonSurveyController] Started");
        }

        private void OnItemAnswered(string questionKey, string answer, string responseType)
        {
            DomainEventBus.Instance?.Publish(new ComparisonSurveyAnswered(
                questionKey, answer, responseType));
        }

        private void OnSurveyCompleted()
        {
            float duration = Time.time - startTime;
            DomainEventBus.Instance?.Publish(new ComparisonSurveyCompleted(duration));

            // 원래 조건 복원 (GlassOnly였으면 핸드 레이 재활성화)
            if (savedCondition != ExperimentCondition.Hybrid)
                ConditionController.Instance?.SetCondition(savedCondition);

            isActive = false;
            Debug.Log($"[ComparisonSurveyController] Completed — duration={duration:F0}s");

            ExperimentManager.Instance?.AdvanceState();
        }

        private void OnDestroy()
        {
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged -= OnStateChanged;
        }
    }
}

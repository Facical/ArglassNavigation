using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;
using ARNavExperiment.Logging;
using ARNavExperiment.Navigation;

namespace ARNavExperiment.Presentation.Experimenter
{
    public class ExperimentFlowUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject appModeSelectorPanel;
        [SerializeField] private GameObject relocalizationPanel;
        [SerializeField] private GameObject conditionTransitionPanel;
        [SerializeField] private GameObject surveyPromptPanel;
        [SerializeField] private GameObject completionPanel;

        [Header("Relocalization")]
        [SerializeField] private RelocalizationUI relocalizationUI;

        [Header("Condition Transition")]
        [SerializeField] private TextMeshProUGUI transitionTitleText;
        [SerializeField] private TextMeshProUGUI transitionDetailText;

        [SerializeField] private Button transitionContinueButton;

        [Header("Survey Prompt")]
        [SerializeField] private TextMeshProUGUI surveyTitleText;
        [SerializeField] private TextMeshProUGUI surveyInstructionText;
        [SerializeField] private Button surveyDoneButton;

        [Header("Completion")]
        [SerializeField] private TextMeshProUGUI completionText;

        // Preflight state
        private bool preflightPassed;
        private List<string> preflightFailedItems = new();
        private Coroutine preflightOverrideCoroutine;

        private void Start()
        {
            transitionContinueButton?.onClick.AddListener(OnTransitionContinue);
            surveyDoneButton?.onClick.AddListener(OnSurveyDone);

            if (ExperimentManager.Instance != null)
            {
                ExperimentManager.Instance.OnStateChanged -= OnStateChanged;
                ExperimentManager.Instance.OnStateChanged += OnStateChanged;
            }

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

            HideAllPanels();

            // 앱 시작 시 모드 선택 화면 표시
            if (appModeSelectorPanel != null)
                appModeSelectorPanel.SetActive(true);
        }

        private void OnLanguageChanged(Language lang)
        {
            RefreshLocalization();
        }

        private void RefreshLocalization()
        {
            if (ExperimentManager.Instance != null)
                OnStateChanged(ExperimentManager.Instance.CurrentState);
        }

        private void HideAllPanels()
        {
            if (appModeSelectorPanel) appModeSelectorPanel.SetActive(false);
            if (relocalizationPanel) relocalizationPanel.SetActive(false);
            if (conditionTransitionPanel) conditionTransitionPanel.SetActive(false);
            if (surveyPromptPanel) surveyPromptPanel.SetActive(false);
            if (completionPanel) completionPanel.SetActive(false);
        }

        private void OnStateChanged(ExperimentState state)
        {
            // GlassOnly 조건에서도 Relocalization 시 앵커 로드 + 실험자 폰에 가이드 표시
            if (ConditionController.Instance != null &&
                ConditionController.Instance.CurrentCondition == ExperimentCondition.GlassOnly)
            {
                HideAllPanels();
                if (state == ExperimentState.Relocalization)
                {
                    if (relocalizationPanel != null) relocalizationPanel.SetActive(true);
                    relocalizationUI?.StartRelocalization("B"); // routeId 파라미터 유지
                    return;
                }
                if (state == ExperimentState.Setup)
                {
                    ShowSetupTransition();
                    return;
                }
                return;
            }

            HideAllPanels();

            switch (state)
            {
                case ExperimentState.Relocalization:
                    ShowRelocalization();
                    break;

                case ExperimentState.Setup:
                    ShowSetupTransition();
                    break;

                case ExperimentState.Running:
                    ShowConditionStart();
                    break;

                case ExperimentState.Survey:
                    ShowSurveyPrompt();
                    break;

                case ExperimentState.Complete:
                    ShowCompletion();
                    break;
            }
        }

        private void ShowRelocalization()
        {
            if (relocalizationPanel != null)
                relocalizationPanel.SetActive(true);

            if (relocalizationUI != null)
                relocalizationUI.StartRelocalization("B"); // routeId 파라미터 유지
        }

        private void ShowSetupTransition()
        {
            if (conditionTransitionPanel == null) return;
            conditionTransitionPanel.SetActive(true);

            var session = ExperimentManager.Instance?.session;
            if (transitionTitleText)
                transitionTitleText.text = LocalizationManager.Get("preflight.header");

            // Preflight 체크 실행
            RunPreflightChecks();
            string report = FormatPreflightResults(session);

            if (transitionDetailText)
                transitionDetailText.text = string.Format(
                    LocalizationManager.Get("flow.setup_detail"),
                    session?.participantId, session?.condition, session?.missionSet)
                    + "\n\n" + report;

            // 전체 통과 → Continue 즉시 활성, 실패 → 5초 지연 후 활성화
            if (transitionContinueButton != null)
            {
                if (preflightPassed)
                {
                    transitionContinueButton.interactable = true;
                }
                else
                {
                    transitionContinueButton.interactable = false;
                    StopPreflightOverride();
                    preflightOverrideCoroutine = StartCoroutine(PreflightOverrideCountdown(5f));
                }
            }
        }

        private void ShowConditionStart()
        {
            if (conditionTransitionPanel == null) return;
            conditionTransitionPanel.SetActive(true);

            var session = ExperimentManager.Instance?.session;
            string condition = session?.condition;
            string missionSet = session?.missionSet;
            string conditionLabel = condition == "glass_only"
                ? LocalizationManager.Get("flow.condition_glass")
                : LocalizationManager.Get("flow.condition_hybrid");
            string setLabel = missionSet == "Set1"
                ? LocalizationManager.Get("appmode.set1")
                : LocalizationManager.Get("appmode.set2");

            if (transitionTitleText)
                transitionTitleText.text = LocalizationManager.Get("flow.condition_title");
            if (transitionDetailText)
            {
                string deviceInfo = condition == "glass_only"
                    ? LocalizationManager.Get("flow.condition_detail_glass")
                    : LocalizationManager.Get("flow.condition_detail_hybrid");

                transitionDetailText.text = string.Format(
                    LocalizationManager.Get("flow.condition_info"),
                    conditionLabel, setLabel, deviceInfo);
            }

            // Load missions for selected set
            MissionManager.Instance?.LoadMissions(missionSet);
        }

        private void ShowSurveyPrompt()
        {
            if (surveyPromptPanel == null) return;
            surveyPromptPanel.SetActive(true);

            var session = ExperimentManager.Instance?.session;
            string condition = session?.condition;
            string conditionLabel = condition == "glass_only"
                ? LocalizationManager.Get("flow.condition_glass")
                : LocalizationManager.Get("flow.condition_hybrid");

            if (surveyTitleText)
                surveyTitleText.text = string.Format(
                    LocalizationManager.Get("flow.survey_title_single"), conditionLabel);
            if (surveyInstructionText)
                surveyInstructionText.text = LocalizationManager.Get("flow.survey_instr");
        }

        private void ShowCompletion()
        {
            if (completionPanel == null) return;
            completionPanel.SetActive(true);

            if (completionText)
                completionText.text = LocalizationManager.Get("flow.complete_text");
        }

        // === Preflight ===

        private void RunPreflightChecks()
        {
            preflightFailedItems.Clear();
            preflightPassed = true;

            // 1. EventLogger
            if (EventLogger.Instance == null || !EventLogger.Instance.IsReady)
            {
                preflightFailedItems.Add("logger");
                preflightPassed = false;
            }

            // 2. Spatial calibration
            bool spatialOk = (WaypointManager.Instance != null && WaypointManager.Instance.HasMapCalibration)
                || (SpatialAnchorManager.Instance != null && SpatialAnchorManager.Instance.SuccessfulAnchorCount > 0);
            if (!spatialOk)
            {
                preflightFailedItems.Add("spatial");
                preflightPassed = false;
            }

            // 3. Condition
            var session = ExperimentManager.Instance?.session;
            if (session != null && ConditionController.Instance != null)
            {
                string expected = session.condition;
                string actual = ConditionController.Instance.CurrentCondition == ExperimentCondition.GlassOnly
                    ? "glass_only" : "hybrid";
                if (expected != actual)
                {
                    preflightFailedItems.Add("condition");
                    preflightPassed = false;
                }
            }

            // 4. BeamPro (Hybrid만)
            if (session != null && session.condition == "hybrid")
            {
                if (DeviceStateTracker.Instance == null)
                {
                    preflightFailedItems.Add("beampro");
                    preflightPassed = false;
                }
            }

            Debug.Log($"[ExperimentFlowUI] Preflight: passed={preflightPassed}, failed=[{string.Join(",", preflightFailedItems)}]");
        }

        private string FormatPreflightResults(ParticipantSession session)
        {
            var sb = new System.Text.StringBuilder();

            // Logger
            bool loggerOk = !preflightFailedItems.Contains("logger");
            sb.AppendLine(loggerOk
                ? LocalizationManager.Get("preflight.logger_ok")
                : $"<color=#FF6666>{LocalizationManager.Get("preflight.logger_fail")}</color>");

            // Spatial
            bool spatialOk = !preflightFailedItems.Contains("spatial");
            sb.AppendLine(spatialOk
                ? LocalizationManager.Get("preflight.spatial_ok")
                : $"<color=#FF6666>{LocalizationManager.Get("preflight.spatial_fail")}</color>");

            // Condition
            bool conditionOk = !preflightFailedItems.Contains("condition");
            if (conditionOk)
            {
                sb.AppendLine(string.Format(
                    LocalizationManager.Get("preflight.condition_ok"),
                    session?.condition ?? "?"));
            }
            else
            {
                string actual = ConditionController.Instance != null
                    ? (ConditionController.Instance.CurrentCondition == ExperimentCondition.GlassOnly ? "glass_only" : "hybrid")
                    : "?";
                sb.AppendLine($"<color=#FF6666>{string.Format(LocalizationManager.Get("preflight.condition_fail"), session?.condition ?? "?", actual)}</color>");
            }

            // BeamPro (Hybrid만)
            if (session != null && session.condition == "hybrid")
            {
                bool beamOk = !preflightFailedItems.Contains("beampro");
                sb.AppendLine(beamOk
                    ? LocalizationManager.Get("preflight.beam_ok")
                    : $"<color=#FF6666>{LocalizationManager.Get("preflight.beam_fail")}</color>");
            }

            return sb.ToString();
        }

        private IEnumerator PreflightOverrideCountdown(float seconds)
        {
            float remaining = seconds;
            while (remaining > 0f)
            {
                if (transitionDetailText != null)
                {
                    // 카운트다운을 마지막 줄에 추가
                    string current = transitionDetailText.text;
                    string overrideMsg = string.Format(
                        LocalizationManager.Get("preflight.override_available"),
                        $"{remaining:F0}");
                    // 이전 오버라이드 메시지 제거 후 새로 추가
                    int idx = current.LastIndexOf("\n<color=#FFAA00>");
                    if (idx >= 0)
                        current = current.Substring(0, idx);
                    transitionDetailText.text = current + $"\n<color=#FFAA00>{overrideMsg}</color>";
                }
                yield return new WaitForSeconds(1f);
                remaining -= 1f;
            }

            // 타이머 만료 → 버튼 활성화 (주황색 스타일은 텍스트로 표현)
            if (transitionContinueButton != null)
                transitionContinueButton.interactable = true;

            if (transitionDetailText != null)
            {
                string current = transitionDetailText.text;
                int idx = current.LastIndexOf("\n<color=#FFAA00>");
                if (idx >= 0)
                    current = current.Substring(0, idx);
                transitionDetailText.text = current;
            }

            preflightOverrideCoroutine = null;
        }

        private void StopPreflightOverride()
        {
            if (preflightOverrideCoroutine != null)
            {
                StopCoroutine(preflightOverrideCoroutine);
                preflightOverrideCoroutine = null;
            }
        }

        // === Button Callbacks ===

        private void OnTransitionContinue()
        {
            StopPreflightOverride();
            var state = ExperimentManager.Instance?.CurrentState;
            if (state == ExperimentState.Setup)
            {
                // Preflight 결과 이벤트 발행
                bool overridden = !preflightPassed;
                string failedJson = "[" + string.Join(",", preflightFailedItems.ConvertAll(s => $"\"{s}\"")) + "]";
                DomainEventBus.Instance?.Publish(new PreflightCheckCompleted(
                    preflightPassed, failedJson, overridden));

                conditionTransitionPanel?.SetActive(false);
                ExperimentManager.Instance?.AdvanceState(); // → Running
            }
            else if (state == ExperimentState.Running)
            {
                var mm = MissionManager.Instance;
                if (mm == null || !mm.HasLoadedMissions)
                {
                    Debug.LogWarning("[ExperimentFlowUI] Missions not loaded — retrying LoadMissions...");
                    var session2 = ExperimentManager.Instance?.session;
                    string missionSet2 = session2?.missionSet;
                    if (mm != null && missionSet2 != null)
                        mm.LoadMissions(missionSet2);

                    if (mm == null || !mm.HasLoadedMissions)
                    {
                        Debug.LogError("[ExperimentFlowUI] Missions still not loaded after retry");
                        if (transitionDetailText != null)
                        {
                            transitionDetailText.text = LocalizationManager.Get("flow.error_missions")
                                + "\n\n" + LocalizationManager.Get("flow.error_missions_hint");
                        }
                        return;
                    }
                }
                conditionTransitionPanel?.SetActive(false);
                mm.StartNextMission();
            }
        }

        private void OnSurveyDone()
        {
            ExperimentManager.Instance?.AdvanceState();
        }

        private void OnDestroy()
        {
            StopPreflightOverride();

            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged -= OnStateChanged;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }
    }
}

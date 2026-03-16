using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;

namespace ARNavExperiment.Presentation.BeamPro
{
    /// <summary>
    /// Hybrid 모드에서 BeamProCanvas(ScreenSpaceOverlay)에 미션 상호작용 UI를 표시합니다.
    /// Hybrid 모드에서는 핸드트래킹이 비활성화되어 글래스 WorldSpace UI 터치가 불가하므로,
    /// BeamPro 폰 터치스크린에 Briefing/Verification/Rating UI를 표시합니다.
    /// </summary>
    public class HybridMissionOverlay : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject overlayPanel;

        [Header("Briefing")]
        [SerializeField] private GameObject briefingContent;
        [SerializeField] private TextMeshProUGUI missionIdText;
        [SerializeField] private TextMeshProUGUI briefingText;
        [SerializeField] private Button confirmButton;

        [Header("Verification")]
        [SerializeField] private GameObject verificationContent;
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private Button[] answerButtons;

        [Header("Rating (Confidence / Difficulty shared)")]
        [SerializeField] private GameObject ratingContent;
        [SerializeField] private TextMeshProUGUI ratingTitleText;
        [SerializeField] private TextMeshProUGUI ratingPromptText;
        [SerializeField] private Button[] ratingButtons; // 7 buttons for 1-7
        [SerializeField] private TextMeshProUGUI currentRatingText;
        [SerializeField] private Button confirmRatingButton;

        [SerializeField] private TextMeshProUGUI autoAdvanceText;
        [SerializeField] private float autoAdvanceTimeout = 15f;

        public bool LastAnswerCorrect { get; private set; }

        private System.Action onBriefingConfirm;
        private System.Action<int, float> onVerificationAnswered;
        private System.Action<int> onRatingConfirm;
        private int correctAnswerIndex;
        private float verificationShowTime;
        private int selectedRating = -1;
        private Coroutine autoAdvanceCoroutine;

        private GameObject tabBar;
        private GameObject hubRoot;
        private GraphicRaycaster experimenterRaycaster;
        private CanvasGroup experimenterCanvasGroup;

        private void Awake()
        {
            if (overlayPanel == null)
                overlayPanel = gameObject;

            // 직렬화 참조 누락 시 런타임 자동 와이어링
            EnsureContentReferences();

            // 버튼 리스너 등록
            confirmButton?.onClick.AddListener(OnBriefingConfirm);
            confirmRatingButton?.onClick.AddListener(OnRatingConfirm);

            if (answerButtons != null)
            {
                for (int i = 0; i < answerButtons.Length; i++)
                {
                    int idx = i;
                    answerButtons[i]?.onClick.AddListener(() => OnAnswerSelected(idx));
                }
            }

            if (ratingButtons != null)
            {
                for (int i = 0; i < ratingButtons.Length; i++)
                {
                    int rating = i + 1;
                    ratingButtons[i]?.onClick.AddListener(() => SelectRating(rating));
                }
            }
        }

        /// <summary>
        /// 직렬화 참조가 누락된 경우 이름 기반으로 런타임 자동 복구합니다.
        /// SceneWiringTool이 비활성 오브젝트를 찾지 못하거나, 씬 저장 누락 시 발생.
        /// </summary>
        private void EnsureContentReferences()
        {
            bool anyMissing = false;

            if (briefingContent == null)
            {
                briefingContent = FindChildByName(overlayPanel.transform, "OverlayBriefingContent");
                if (briefingContent != null) anyMissing = true;
            }
            if (verificationContent == null)
            {
                verificationContent = FindChildByName(overlayPanel.transform, "OverlayVerificationContent");
                if (verificationContent != null) anyMissing = true;
            }
            if (ratingContent == null)
            {
                ratingContent = FindChildByName(overlayPanel.transform, "OverlayRatingContent");
                if (ratingContent != null) anyMissing = true;
            }

            // 답변 버튼 배열 복구
            if ((answerButtons == null || answerButtons.Length == 0) && verificationContent != null)
            {
                var answersGO = FindChildByName(verificationContent.transform, "OvAnswers");
                if (answersGO != null)
                {
                    answerButtons = answersGO.GetComponentsInChildren<Button>(true);
                    if (answerButtons.Length > 0)
                    {
                        anyMissing = true;
                        for (int i = 0; i < answerButtons.Length; i++)
                        {
                            int idx = i;
                            answerButtons[i]?.onClick.AddListener(() => OnAnswerSelected(idx));
                        }
                    }
                }
            }

            // 평점 버튼 배열 복구
            if ((ratingButtons == null || ratingButtons.Length == 0) && ratingContent != null)
            {
                var ratingsGO = FindChildByName(ratingContent.transform, "OvRatingButtons");
                if (ratingsGO != null)
                {
                    ratingButtons = ratingsGO.GetComponentsInChildren<Button>(true);
                    if (ratingButtons.Length > 0)
                    {
                        anyMissing = true;
                        for (int i = 0; i < ratingButtons.Length; i++)
                        {
                            int rating = i + 1;
                            ratingButtons[i]?.onClick.AddListener(() => SelectRating(rating));
                        }
                    }
                }
            }

            // 텍스트 참조 복구
            if (verificationContent != null && questionText == null)
            {
                var t = FindChildByName(verificationContent.transform, "OvQuestionText");
                if (t != null) { questionText = t.GetComponent<TextMeshProUGUI>(); anyMissing = true; }
            }
            if (briefingContent != null)
            {
                if (missionIdText == null)
                {
                    var t = FindChildByName(briefingContent.transform, "OvMissionIdText");
                    if (t != null) { missionIdText = t.GetComponent<TextMeshProUGUI>(); anyMissing = true; }
                }
                if (briefingText == null)
                {
                    var t = FindChildByName(briefingContent.transform, "OvBriefingText");
                    if (t != null) { briefingText = t.GetComponent<TextMeshProUGUI>(); anyMissing = true; }
                }
                if (confirmButton == null)
                {
                    var t = FindChildByName(briefingContent.transform, "OvConfirmBtn");
                    if (t != null) { confirmButton = t.GetComponent<Button>(); anyMissing = true;
                        confirmButton?.onClick.AddListener(OnBriefingConfirm); }
                }
                if (autoAdvanceText == null)
                {
                    var t = FindChildByName(briefingContent.transform, "OvAutoAdvanceText");
                    if (t != null) { autoAdvanceText = t.GetComponent<TextMeshProUGUI>(); anyMissing = true; }
                }
            }
            if (ratingContent != null)
            {
                if (ratingTitleText == null)
                {
                    var t = FindChildByName(ratingContent.transform, "OvRatingTitleText");
                    if (t != null) { ratingTitleText = t.GetComponent<TextMeshProUGUI>(); anyMissing = true; }
                }
                if (ratingPromptText == null)
                {
                    var t = FindChildByName(ratingContent.transform, "OvRatingPromptText");
                    if (t != null) { ratingPromptText = t.GetComponent<TextMeshProUGUI>(); anyMissing = true; }
                }
                if (currentRatingText == null)
                {
                    var t = FindChildByName(ratingContent.transform, "OvCurrentRatingText");
                    if (t != null) { currentRatingText = t.GetComponent<TextMeshProUGUI>(); anyMissing = true; }
                }
                if (confirmRatingButton == null)
                {
                    var t = FindChildByName(ratingContent.transform, "OvConfirmRatingBtn");
                    if (t != null) { confirmRatingButton = t.GetComponent<Button>(); anyMissing = true;
                        confirmRatingButton?.onClick.AddListener(OnRatingConfirm); }
                }
            }

            if (anyMissing)
                Debug.LogWarning($"[HybridOverlay] 런타임 자동 와이어링 실행됨! " +
                    $"briefing={briefingContent != null} verify={verificationContent != null} " +
                    $"rating={ratingContent != null} answers={answerButtons?.Length ?? 0} " +
                    $"ratings={ratingButtons?.Length ?? 0}");

            // 버튼 컨테이너의 LayoutGroup childControl 설정 보정
            // SceneSetupTool에서 childControlWidth 누락 시 버튼 너비가 0이 되는 버그 방지
            FixButtonContainerLayouts();
        }

        private void FixButtonContainerLayouts()
        {
            // OvAnswers (Verification 답변 버튼 컨테이너)
            if (verificationContent != null)
            {
                var answersGO = FindChildByName(verificationContent.transform, "OvAnswers");
                if (answersGO != null)
                {
                    var vlg = answersGO.GetComponent<VerticalLayoutGroup>();
                    if (vlg != null)
                    {
                        vlg.childControlWidth = true;
                        vlg.childControlHeight = true;
                    }
                }
            }

            // OvRatingButtons (평점 버튼 컨테이너)
            if (ratingContent != null)
            {
                var ratingsGO = FindChildByName(ratingContent.transform, "OvRatingButtons");
                if (ratingsGO != null)
                {
                    var hlg = ratingsGO.GetComponent<HorizontalLayoutGroup>();
                    if (hlg != null)
                    {
                        hlg.childControlWidth = true;
                        hlg.childControlHeight = true;
                    }
                }
            }
        }

        private static GameObject FindChildByName(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child.gameObject;
                var found = FindChildByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void Update()
        {
            if (!overlayPanel || !overlayPanel.activeSelf) return;

            // 터치 감지 시 레이캐스트 진단
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    DiagnoseTouchRaycast(touch.position);
                }
            }
#if UNITY_EDITOR
            if (Input.GetMouseButtonDown(0))
            {
                DiagnoseTouchRaycast(Input.mousePosition);
            }
#endif
        }

        private void DiagnoseTouchRaycast(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null)
            {
                Debug.LogError("[HybridOverlay] TOUCH — EventSystem is NULL!");
                return;
            }

            var ped = new PointerEventData(es) { position = screenPos };
            var results = new List<RaycastResult>();
            es.RaycastAll(ped, results);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[HybridOverlay] TOUCH at ({screenPos.x:F0},{screenPos.y:F0}) " +
                $"screen={Screen.width}x{Screen.height} hits={results.Count}");

            for (int i = 0; i < Mathf.Min(results.Count, 8); i++)
            {
                var r = results[i];
                var btn = r.gameObject.GetComponent<Button>();
                var cg = r.gameObject.GetComponentInParent<CanvasGroup>();
                sb.AppendLine($"  [{i}] {r.gameObject.name} depth={r.depth} " +
                    $"sortOrder={r.sortingOrder} module={r.module?.GetType().Name} " +
                    $"btn={(btn != null ? (btn.interactable ? "ON" : "OFF") : "none")} " +
                    $"parentCG={(cg != null ? $"i={cg.interactable} br={cg.blocksRaycasts}" : "none")}");
            }

            if (results.Count == 0)
            {
                sb.AppendLine("  NO HITS — possible causes:");
                sb.AppendLine($"  overlayPanel.activeInHierarchy={overlayPanel.activeInHierarchy}");

                // 오버레이 패널 자체의 CanvasGroup 확인
                var overlayCG = overlayPanel.GetComponent<CanvasGroup>();
                if (overlayCG != null)
                    sb.AppendLine($"  overlayPanel CG: a={overlayCG.alpha:F1} i={overlayCG.interactable} br={overlayCG.blocksRaycasts}");

                // 부모 체인의 CanvasGroup 확인
                var t = overlayPanel.transform.parent;
                while (t != null)
                {
                    var pcg = t.GetComponent<CanvasGroup>();
                    if (pcg != null)
                        sb.AppendLine($"  ancestor CG on '{t.name}': a={pcg.alpha:F1} i={pcg.interactable} br={pcg.blocksRaycasts}");
                    t = t.parent;
                }

                // 활성 raycaster 목록
                var raycasters = FindObjectsOfType<BaseRaycaster>();
                sb.AppendLine($"  Active raycasters: {raycasters.Length}");
                foreach (var rc in raycasters)
                    sb.AppendLine($"    {rc.gameObject.name}: {rc.GetType().Name} enabled={rc.enabled}");
            }

            Debug.Log(sb.ToString());
        }

        private void CacheHubReferences()
        {
            if (hubRoot != null) return;
            var hub = BeamProHubController.Instance;
            if (hub == null) return;
            hubRoot = hub.gameObject;
            tabBar = hubRoot.transform.Find("TabBar")?.gameObject;
        }

        private void CacheExperimenterRaycaster()
        {
            if (experimenterRaycaster != null) return;
            var expCanvas = GameObject.Find("ExperimenterCanvas");
            if (expCanvas != null)
            {
                experimenterRaycaster = expCanvas.GetComponent<GraphicRaycaster>();
                experimenterCanvasGroup = expCanvas.GetComponent<CanvasGroup>();
                if (experimenterCanvasGroup == null)
                    experimenterCanvasGroup = expCanvas.AddComponent<CanvasGroup>();
            }

            if (experimenterRaycaster == null)
                Debug.LogError($"[HybridOverlay] ExperimenterCanvas raycaster 캐싱 실패! " +
                    $"Find결과={(expCanvas != null ? "found" : "NULL")}");
        }

        // ===== Briefing =====

        public void ShowBriefing(MissionData mission, System.Action onConfirm)
        {
            CacheHubReferences();
            onBriefingConfirm = onConfirm;

            if (missionIdText)
                missionIdText.text = string.Format(LocalizationManager.Get("briefing.mission"), mission.missionId);
            if (briefingText)
                briefingText.text = mission.GetBriefingText();

            ShowOverlay();
            SetContentActive(briefingContent);

            autoAdvanceCoroutine = StartCoroutine(AutoAdvanceAfterTimeout());

            Debug.Log($"[HybridMissionOverlay] ShowBriefing({mission.missionId})");
        }

        private IEnumerator AutoAdvanceAfterTimeout()
        {
            float remaining = autoAdvanceTimeout;
            while (remaining > 0f)
            {
                if (autoAdvanceText)
                {
                    string autoText = string.Format(
                        LocalizationManager.Get("briefing.auto_advance"), Mathf.CeilToInt(remaining));
                    autoAdvanceText.text = autoText;
                }
                yield return new WaitForSeconds(1f);
                remaining -= 1f;
            }
            autoAdvanceCoroutine = null;
            OnBriefingConfirm();
        }

        private void StopAutoAdvance()
        {
            if (autoAdvanceCoroutine != null)
            {
                StopCoroutine(autoAdvanceCoroutine);
                autoAdvanceCoroutine = null;
            }
        }

        private void OnBriefingConfirm()
        {
            StopAutoAdvance();
            if (autoAdvanceText) autoAdvanceText.text = "";
            var cb = onBriefingConfirm;
            onBriefingConfirm = null;
            Hide();
            cb?.Invoke();
        }

        // ===== Verification =====

        public void ShowVerification(MissionData mission, System.Action<int, float> onAnswered)
        {
            CacheHubReferences();
            onVerificationAnswered = onAnswered;
            correctAnswerIndex = mission.correctAnswerIndex;
            verificationShowTime = Time.time;
            LastAnswerCorrect = false;

            if (questionText)
                questionText.text = mission.GetVerificationQuestion();

            if (answerButtons != null)
            {
                for (int i = 0; i < answerButtons.Length; i++)
                {
                    if (i < mission.answerOptions.Length)
                    {
                        answerButtons[i].gameObject.SetActive(true);
                        var txt = answerButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (txt != null)
                            txt.text = $"{i + 1}. {mission.GetAnswerOption(i)}";
                    }
                    else
                    {
                        answerButtons[i].gameObject.SetActive(false);
                    }
                }
            }

            ShowOverlay();
            SetContentActive(verificationContent);

            Debug.Log($"[HybridMissionOverlay] ShowVerification({mission.missionId})");
            StartCoroutine(LogVerificationLayout());
        }

        private IEnumerator LogVerificationLayout()
        {
            // 1프레임 대기 — LayoutGroup 재계산
            yield return null;
            yield return null;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[HybridOverlay] === Verification 레이아웃 진단 ===");

            if (verificationContent != null)
            {
                var vrt = verificationContent.GetComponent<RectTransform>();
                sb.AppendLine($"  VerifyContent: active={verificationContent.activeInHierarchy} " +
                    $"rect={vrt?.rect} worldPos={vrt?.position}");
            }

            if (answerButtons != null)
            {
                for (int i = 0; i < answerButtons.Length; i++)
                {
                    if (answerButtons[i] == null) { sb.AppendLine($"  Btn[{i}]: NULL"); continue; }
                    var go = answerButtons[i].gameObject;
                    var rt = go.GetComponent<RectTransform>();
                    var img = go.GetComponent<Image>();
                    sb.AppendLine($"  Btn[{i}] '{go.name}': activeSelf={go.activeSelf} " +
                        $"activeHierarchy={go.activeInHierarchy} " +
                        $"rect={rt?.rect} worldPos={rt?.position} " +
                        $"imgRaycast={img?.raycastTarget} interactable={answerButtons[i].interactable}");

                    // 스크린 좌표 변환
                    if (rt != null)
                    {
                        var corners = new Vector3[4];
                        rt.GetWorldCorners(corners);
                        var cam = GetComponentInParent<Canvas>()?.worldCamera;
                        var screenMin = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                        var screenMax = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                        sb.AppendLine($"         screenRect=({screenMin.x:F0},{screenMin.y:F0})-({screenMax.x:F0},{screenMax.y:F0})");
                    }
                }
            }
            else
            {
                sb.AppendLine("  answerButtons array is NULL!");
            }

            // 부모 체인 activeInHierarchy 확인
            if (answerButtons != null && answerButtons.Length > 0 && answerButtons[0] != null)
            {
                var t = answerButtons[0].transform.parent;
                while (t != null && t != overlayPanel.transform.parent)
                {
                    sb.AppendLine($"  Parent '{t.name}': activeSelf={t.gameObject.activeSelf} " +
                        $"activeHierarchy={t.gameObject.activeInHierarchy}");
                    t = t.parent;
                }
            }

            Debug.Log(sb.ToString());
        }

        private void OnAnswerSelected(int index)
        {
            LastAnswerCorrect = index == correctAnswerIndex;
            float rt = Time.time - verificationShowTime;
            var cb = onVerificationAnswered;
            onVerificationAnswered = null;
            Hide();
            cb?.Invoke(index, rt);
        }

        // ===== Confidence Rating =====

        public void ShowConfidenceRating(string prompt, System.Action<int> onRated)
        {
            CacheHubReferences();
            onRatingConfirm = onRated;
            selectedRating = -1;

            if (ratingTitleText)
                ratingTitleText.text = LocalizationManager.Get("hybrid.confidence_title");
            if (ratingPromptText)
                ratingPromptText.text = prompt;
            if (currentRatingText)
                currentRatingText.text = "";

            ShowOverlay();
            SetContentActive(ratingContent);

            Debug.Log("[HybridMissionOverlay] ShowConfidenceRating");
        }

        // ===== Difficulty Rating =====

        public void ShowDifficultyRating(string missionId, System.Action<int> onRated)
        {
            CacheHubReferences();
            onRatingConfirm = onRated;
            selectedRating = -1;

            if (ratingTitleText)
                ratingTitleText.text = LocalizationManager.Get("hybrid.difficulty_title");
            if (ratingPromptText)
                ratingPromptText.text = LocalizationManager.Get("difficulty.prompt");
            if (currentRatingText)
                currentRatingText.text = "";

            ShowOverlay();
            SetContentActive(ratingContent);

            Debug.Log($"[HybridMissionOverlay] ShowDifficultyRating({missionId})");
        }

        private void SelectRating(int rating)
        {
            selectedRating = rating;
            if (currentRatingText)
                currentRatingText.text = $"{rating}/7";
        }

        private void OnRatingConfirm()
        {
            if (selectedRating < 1) return;
            var cb = onRatingConfirm;
            onRatingConfirm = null;
            Hide();
            cb?.Invoke(selectedRating);
        }

        // ===== Show / Hide =====

        private void ShowOverlay()
        {
            // 1) ExperimenterCanvas raycaster 비활성화 — sortOrder=10이 터치 가로채는 것 방지
            CacheExperimenterRaycaster();
            if (experimenterRaycaster != null) experimenterRaycaster.enabled = false;

            // 2) ExperimenterCanvas CanvasGroup — 터치 완전 차단 해제
            if (experimenterCanvasGroup != null)
            {
                experimenterCanvasGroup.blocksRaycasts = false;
                experimenterCanvasGroup.interactable = false;
            }

            // 3) BeamProCanvas CanvasGroup — 명시적 활성화 (이전 GlassOnly 조건에서 비활성일 수 있음)
            var beamCanvasGroup = GetComponentInParent<CanvasGroup>();
            if (beamCanvasGroup != null)
            {
                beamCanvasGroup.alpha = 1f;
                beamCanvasGroup.interactable = true;
                beamCanvasGroup.blocksRaycasts = true;
            }

            // 허브 탭/탭바 숨기기
            BeamProHubController.Instance?.HideAllTabs();
            if (tabBar) tabBar.SetActive(false);

            overlayPanel.SetActive(true);

            // 진단 로그
            LogOverlayDiagnostics("ShowOverlay");
        }

        private void SetContentActive(GameObject content)
        {
            if (briefingContent) briefingContent.SetActive(briefingContent == content);
            if (verificationContent) verificationContent.SetActive(verificationContent == content);
            if (ratingContent) ratingContent.SetActive(ratingContent == content);

            Debug.Log($"[HybridOverlay] SetContentActive target={content?.name ?? "NULL"} | " +
                $"briefing={briefingContent?.activeSelf} verify={verificationContent?.activeSelf} " +
                $"rating={ratingContent?.activeSelf} | " +
                $"verifyRef={(verificationContent != null ? "OK" : "NULL")} " +
                $"ratingRef={(ratingContent != null ? "OK" : "NULL")}");
        }

        public void Hide()
        {
            StopAutoAdvance();
            if (autoAdvanceText) autoAdvanceText.text = "";
            onBriefingConfirm = null;
            onVerificationAnswered = null;
            onRatingConfirm = null;

            overlayPanel.SetActive(false);

            // ExperimenterCanvas raycaster + CanvasGroup 복원
            if (experimenterRaycaster != null) experimenterRaycaster.enabled = true;
            if (experimenterCanvasGroup != null)
            {
                experimenterCanvasGroup.blocksRaycasts = true;
                experimenterCanvasGroup.interactable = true;
            }

            // 허브 탭바 + 맵 탭 복원
            if (tabBar) tabBar.SetActive(true);
            BeamProHubController.Instance?.SwitchTab(0);

            Debug.Log("[HybridOverlay] Hide — hub+experimenter restored");
        }

        private void LogOverlayDiagnostics(string context)
        {
            var allCanvases = FindObjectsOfType<Canvas>();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[HybridOverlay] === {context} 진단 ===");
            foreach (var c in allCanvases)
            {
                if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                var rc = c.GetComponent<GraphicRaycaster>();
                var cg = c.GetComponent<CanvasGroup>();
                sb.AppendLine($"  Canvas: {c.name} | sortOrder={c.sortingOrder} | " +
                    $"raycaster={(rc != null ? (rc.enabled ? "ON" : "OFF") : "NONE")} | " +
                    $"CG={(cg != null ? $"a={cg.alpha:F1} i={cg.interactable} br={cg.blocksRaycasts}" : "NONE")}");
            }
            sb.AppendLine($"  OverlayPanel.active={overlayPanel?.activeSelf}");
            var es = UnityEngine.EventSystems.EventSystem.current;
            sb.AppendLine($"  EventSystem={(es != null ? "OK" : "NULL")}");
            var inputModule = es?.currentInputModule;
            sb.AppendLine($"  InputModule={(inputModule != null ? inputModule.GetType().Name : "NULL")}");
            Debug.Log(sb.ToString());
        }
    }
}

using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;
namespace ARNavExperiment.Presentation.Glass
{
    public class MissionBriefingUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI missionIdText;
        [SerializeField] private TextMeshProUGUI briefingText;
        [SerializeField] private TextMeshProUGUI autoAdvanceText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private float autoAdvanceTimeout = 15f;

        private System.Action onConfirm;
        private Coroutine autoAdvanceCoroutine;

        private void Awake()
        {
            if (panel == null)
            {
                panel = gameObject;
                Debug.LogWarning("[MissionBriefingUI] panel null — self-wired to gameObject");
            }
            if (missionIdText == null || briefingText == null || confirmButton == null)
                AutoWireChildren();

            // 버튼 리스너 (Start에서 이동 — 비활성 오브젝트의 Start()는 Show() 후 다음 프레임에 실행되어 패널을 다시 숨김)
            confirmButton?.onClick.AddListener(OnConfirmClicked);
        }

        private void AutoWireChildren()
        {
            var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.gameObject.name == "MissionIdText" && missionIdText == null) missionIdText = t;
                if (t.gameObject.name == "BriefingText" && briefingText == null) briefingText = t;
                if (t.gameObject.name == "AutoAdvanceText" && autoAdvanceText == null) autoAdvanceText = t;
            }
            if (confirmButton == null)
            {
                var btns = panel.GetComponentsInChildren<Button>(true);
                if (btns.Length > 0) confirmButton = btns[0];
            }
            Debug.LogWarning($"[MissionBriefingUI] AutoWireChildren — missionIdText={missionIdText != null}, " +
                $"briefingText={briefingText != null}, autoAdvanceText={autoAdvanceText != null}, " +
                $"confirmButton={confirmButton != null}");
        }

        public void Show(MissionData mission, System.Action onConfirmCallback)
        {
            onConfirm = onConfirmCallback;
            if (missionIdText) missionIdText.text = string.Format(
                LocalizationManager.Get("briefing.mission"), mission.missionId);
            if (briefingText) briefingText.text = mission.GetBriefingText();

            Debug.Log($"[MissionBriefingUI] Show({mission.missionId}) — panel={panel != null}, " +
                $"missionIdText={missionIdText != null}, briefingText={briefingText != null}, " +
                $"confirmButton={confirmButton != null}, " +
                $"gameObject.active={gameObject.activeSelf}, " +
                $"activeInHierarchy={gameObject.activeInHierarchy}");

            if (panel)
            {
                panel.SetActive(true);
                Debug.Log($"[MissionBriefingUI] panel.SetActive(true) — " +
                    $"panel.activeInHierarchy={panel.activeInHierarchy}, " +
                    $"rectSize={panel.GetComponent<RectTransform>()?.sizeDelta}");
            }
            else
            {
                Debug.LogError("[MissionBriefingUI] panel is NULL — UI will not show!");
            }

            autoAdvanceCoroutine = StartCoroutine(AutoAdvanceAfterTimeout());
            StartCoroutine(VerifyVisibility(mission.missionId));
        }

        private IEnumerator VerifyVisibility(string missionId)
        {
            yield return new WaitForSeconds(0.5f);
            if (panel == null || !panel.activeInHierarchy) yield break;

            var cg = panel.GetComponent<CanvasGroup>();
            var canvas = panel.GetComponentInParent<Canvas>();
            var canvasRt = canvas?.GetComponent<RectTransform>();
            var rt = panel.GetComponent<RectTransform>();

            // 조상 CanvasGroup 체크
            var t = panel.transform.parent;
            while (t != null)
            {
                var acg = t.GetComponent<CanvasGroup>();
                if (acg != null && acg.alpha < 1f)
                    Debug.LogError($"[MissionBriefingUI] ANCESTOR CG alpha={acg.alpha} on {t.name}!");
                t = t.parent;
            }

            Debug.Log($"[MissionBriefingUI] VERIFY {missionId} @0.5s — " +
                $"active={panel.activeInHierarchy}, cg.alpha={cg?.alpha}, " +
                $"rect={rt?.rect}, canvas.renderMode={canvas?.renderMode}, " +
                $"canvas.worldCam={canvas?.worldCamera?.name ?? "NULL"}, " +
                $"canvasRect={canvasRt?.rect}, canvasScale={canvas?.transform.localScale}, " +
                $"font={missionIdText?.font?.name ?? "NULL"}, " +
                $"textColor={missionIdText?.color}");
        }

        public void Hide()
        {
            StopAutoAdvance();
            if (autoAdvanceText) autoAdvanceText.text = "";
            if (panel) panel.SetActive(false);
            onConfirm = null;
        }

        private void OnConfirmClicked()
        {
            StopAutoAdvance();
            var cb = onConfirm;
            onConfirm = null;
            if (panel) panel.SetActive(false);
            cb?.Invoke();
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
            OnConfirmClicked();
        }

        private void StopAutoAdvance()
        {
            if (autoAdvanceCoroutine != null)
            {
                StopCoroutine(autoAdvanceCoroutine);
                autoAdvanceCoroutine = null;
            }
        }
    }
}

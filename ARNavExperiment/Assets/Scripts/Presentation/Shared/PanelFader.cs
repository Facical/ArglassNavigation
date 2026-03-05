using System;
using System.Collections;
using UnityEngine;

namespace ARNavExperiment.Presentation.Shared
{
    [RequireComponent(typeof(CanvasGroup))]
    public class PanelFader : MonoBehaviour
    {
        [SerializeField] private float fadeDuration = 0.25f;

        private CanvasGroup canvasGroup;
        private Coroutine fadeCoroutine;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void FadeIn(Action onComplete = null)
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            gameObject.SetActive(true);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            fadeCoroutine = StartCoroutine(FadeRoutine(0f, 1f, () =>
            {
                onComplete?.Invoke();
            }));
        }

        public void FadeOut(Action onComplete = null)
        {
            if (!gameObject.activeInHierarchy)
            {
                onComplete?.Invoke();
                return;
            }

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            fadeCoroutine = StartCoroutine(FadeRoutine(canvasGroup.alpha, 0f, () =>
            {
                gameObject.SetActive(false);
                onComplete?.Invoke();
            }));
        }

        private IEnumerator FadeRoutine(float from, float to, Action onDone)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            float elapsed = 0f;
            canvasGroup.alpha = from;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
                yield return null;
            }

            canvasGroup.alpha = to;
            fadeCoroutine = null;
            onDone?.Invoke();
        }
    }
}

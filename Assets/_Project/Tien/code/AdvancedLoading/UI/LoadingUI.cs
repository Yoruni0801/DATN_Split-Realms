using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Tien.AdvancedLoading.Data;

namespace Tien.AdvancedLoading.UI
{
    public class LoadingUI : MonoBehaviour
    {
        [SerializeField] private GameObject contentPanel;
        [SerializeField] private Image background;
        [SerializeField] private Text tipText;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private CanvasGroup canvasGroup;

        public void Initialize(LoadingProfile profile)
        {
            if (profile != null)
            {
                if (background != null && profile.background != null)
                {
                    background.sprite = profile.background;
                    background.color = Color.white;
                }

                if (tipText != null)
                {
                    tipText.text = profile.tipText;
                    tipText.color = profile.textColor;
                }
            }

            EnsureCanvasGroup();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            Hide();
        }

        private void EnsureCanvasGroup()
        {
            if (contentPanel != null && canvasGroup == null)
            {
                canvasGroup = contentPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = contentPanel.AddComponent<CanvasGroup>();
            }
        }

        public void Show()
        {
            if (contentPanel != null) contentPanel.SetActive(true);
        }

        public void Hide()
        {
            if (contentPanel != null) contentPanel.SetActive(false);
        }

        public IEnumerator FadeInUI(float duration)
        {
            EnsureCanvasGroup();
            if (canvasGroup == null) yield break;

            canvasGroup.alpha = 0f;
            Show();

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / duration);
                float alpha = fadeCurve != null ? fadeCurve.Evaluate(progress) : Mathf.SmoothStep(0f, 1f, progress);
                canvasGroup.alpha = alpha;
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        public IEnumerator FadeOutUI(float duration)
        {
            EnsureCanvasGroup();
            if (canvasGroup == null) yield break;

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / duration);
                float alpha = fadeCurve != null ? 1f - fadeCurve.Evaluate(progress) : Mathf.SmoothStep(1f, 0f, progress);
                canvasGroup.alpha = alpha;
                yield return null;
            }
            canvasGroup.alpha = 0f;
            Hide();
        }
    }
}

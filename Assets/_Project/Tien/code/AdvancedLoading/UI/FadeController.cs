using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Tien.AdvancedLoading.UI
{
    public class FadeController : MonoBehaviour
    {
        [SerializeField] private Image faderImage;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (faderImage != null)
            {
                faderImage.color = new Color(0, 0, 0, 0);
                faderImage.raycastTarget = false;
            }
        }

        public IEnumerator FadeToBlack(float duration)
        {
            yield return Fade(1f, duration);
        }

        public IEnumerator FadeToClear(float duration)
        {
            yield return Fade(0f, duration);
        }

        private IEnumerator Fade(float targetAlpha, float duration)
        {
            if (faderImage == null) yield break;

            faderImage.raycastTarget = true;
            Color c = faderImage.color;
            float startAlpha = c.a;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / duration);
                
                // Lấy giá trị từ AnimationCurve (nếu có chỉnh trên Inspector) hoặc dùng SmoothStep mặc định
                float curveValue = (fadeCurve != null && fadeCurve.length > 0)
                    ? fadeCurve.Evaluate(progress)
                    : Mathf.SmoothStep(0f, 1f, progress);

                c.a = Mathf.Lerp(startAlpha, targetAlpha, curveValue);
                faderImage.color = c;
                yield return null;
            }

            c.a = targetAlpha;
            faderImage.color = c;
            faderImage.raycastTarget = targetAlpha > 0;
        }
    }
}

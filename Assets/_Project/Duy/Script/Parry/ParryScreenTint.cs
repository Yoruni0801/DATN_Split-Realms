using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ParryScreenTint : MonoBehaviour
{
    [Header("Screen Tint")]
    [SerializeField] private Color tintColor = new Color(0.35f, 0.9f, 1f, 0.28f);
    [SerializeField, Min(0f)] private float duration = 0.18f;
    [SerializeField] private AnimationCurve fadeCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0f));
    [SerializeField] private int sortingOrder = 32767;

    private Image tintImage;
    private Coroutine tintRoutine;

    private void OnDisable()
    {
        StopTint();
    }

    public void Play()
    {
        if (duration <= 0f || tintColor.a <= 0f)
        {
            return;
        }

        CreateOverlay();
        StopTint();
        tintRoutine = StartCoroutine(TintRoutine());
    }

    private IEnumerator TintRoutine()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            Color color = tintColor;
            color.a *= Mathf.Clamp01(fadeCurve.Evaluate(normalizedTime));
            tintImage.color = color;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        tintImage.color = Color.clear;
        tintRoutine = null;
    }

    private void CreateOverlay()
    {
        if (tintImage != null)
        {
            return;
        }

        GameObject overlay = new GameObject("Parry Screen Tint", typeof(Canvas), typeof(CanvasScaler), typeof(Image));
        overlay.transform.SetParent(transform, false);

        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        tintImage = overlay.GetComponent<Image>();
        tintImage.raycastTarget = false;
        tintImage.color = Color.clear;

        RectTransform rectTransform = tintImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void StopTint()
    {
        if (tintRoutine != null)
        {
            StopCoroutine(tintRoutine);
            tintRoutine = null;
        }

        if (tintImage != null)
        {
            tintImage.color = Color.clear;
        }
    }
}

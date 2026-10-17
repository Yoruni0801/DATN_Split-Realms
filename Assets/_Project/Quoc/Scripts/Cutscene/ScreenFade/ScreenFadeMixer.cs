using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

public class ScreenFadeMixer : PlayableBehaviour
{
    private GameObject canvasObject;
    private Image runtimeImage;
    private bool overlayCreated;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        Image targetImage = playerData as Image;

        int inputCount = playable.GetInputCount();
        Color finalColor = Color.black;
        float finalAlpha = 0f;
        float totalWeight = 0f;
        bool hasActiveClip = false;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            ScriptPlayable<ScreenFadeBehaviour> input = (ScriptPlayable<ScreenFadeBehaviour>)playable.GetInput(i);
            ScreenFadeBehaviour behaviour = input.GetBehaviour();

            double duration = input.GetDuration();
            double time = input.GetTime();
            float t = duration > 0 ? Mathf.Clamp01((float)(time / duration)) : 1f;

            if (behaviour.easeCurve != null)
            {
                t = behaviour.easeCurve.Evaluate(t);
            }

            float alpha = Mathf.Lerp(behaviour.startAlpha, behaviour.endAlpha, t);
            finalColor = behaviour.fadeColor;
            finalAlpha += alpha * weight;
            totalWeight += weight;
            hasActiveClip = true;
        }

        if (!hasActiveClip)
        {
            CleanupOverlay();
            return;
        }

        if (totalWeight > 0f)
        {
            finalAlpha = Mathf.Clamp01(finalAlpha / totalWeight);
        }

        finalColor.a = finalAlpha;

        if (targetImage != null)
        {
            targetImage.color = finalColor;
            targetImage.enabled = finalAlpha > 0.001f;
        }
        else
        {
            CreateOverlay();
            if (runtimeImage != null)
            {
                runtimeImage.color = finalColor;
                runtimeImage.enabled = finalAlpha > 0.001f;
            }
        }
    }

    private void CreateOverlay()
    {
        if (overlayCreated) return;

        canvasObject = new GameObject("Timeline_ScreenFade_Runtime");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32766;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject imgObj = new GameObject("FadeOverlay");
        imgObj.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = imgObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        runtimeImage = imgObj.AddComponent<Image>();
        runtimeImage.raycastTarget = false;
        runtimeImage.sprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0, 0, 4, 4),
            new Vector2(0.5f, 0.5f)
        );

        overlayCreated = true;
    }

    private void CleanupOverlay()
    {
        if (canvasObject != null)
        {
            Object.Destroy(canvasObject);
        }

        canvasObject = null;
        runtimeImage = null;
        overlayCreated = false;
    }

    public override void OnGraphStop(Playable playable)
    {
        CleanupOverlay();
    }
}

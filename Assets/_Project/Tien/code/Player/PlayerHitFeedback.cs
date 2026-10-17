using System.Collections;
using Spine.Unity;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerHitFeedback : MonoBehaviour
{
    [Header("Camera Shake")]
    [SerializeField, Min(0f)] private float shakeDuration = 0.12f;
    [SerializeField, Min(0f)] private float shakeMagnitude = 0.08f;
    [SerializeField, Min(0f)] private float cinemachineShakeFrequency = 8f;

    [Header("Player Flash")]
    [SerializeField, Min(0f)] private float flashDuration = 1f;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float flashAmount = 1f;

    [Header("Screen Tint")]
    [SerializeField, Min(0f)] private float screenTintDuration = 0.25f;
    [SerializeField] private Color screenTintColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] private AnimationCurve screenTintFadeCurve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -2f),
        new Keyframe(1f, 0f, -2f, 0f));
    [SerializeField] private int screenTintSortOrder = 32767;

    [Header("Timing")]
    [SerializeField] private bool useUnscaledTime = true;

    private PlayerHealth playerHealth;
    private SkeletonAnimation skeletonAnimation;
    private Coroutine flashCoroutine;
    private Coroutine screenTintCoroutine;
    private Image screenTintImage;
    private Color originalSkeletonColor = Color.white;
    private bool hasOriginalSkeletonColor;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        skeletonAnimation = GetComponent<SkeletonAnimation>();
        CacheOriginalColor();
    }

    private void OnEnable()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        playerHealth.OnHurt += Play;
        playerHealth.OnDied += Play;
        playerHealth.OnDamageTaken += PlayScreenTint;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHurt -= Play;
            playerHealth.OnDied -= Play;
            playerHealth.OnDamageTaken -= PlayScreenTint;
        }

        StopFlash();
        StopScreenTint();
        RestoreColor();
    }

    public void Play()
    {
        ShakeTargetingCinemachineCameras();

        if (flashDuration <= 0f)
        {
            return;
        }

        CacheOriginalColor();
        StopFlash();
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private void ShakeTargetingCinemachineCameras()
    {
        if (GlobalGraphicsManager.Instance != null && !GlobalGraphicsManager.Instance.GetScreenShake())
        {
            return;
        }

        if (shakeDuration <= 0f || shakeMagnitude <= 0f)
        {
            return;
        }

        CinemachineVirtualCameraBase[] cameras = Object.FindObjectsByType<CinemachineVirtualCameraBase>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        bool foundTargetingCamera = false;

        for (int index = 0; index < cameras.Length; index++)
        {
            CinemachineVirtualCameraBase camera = cameras[index];
            if (camera.Follow != transform && camera.LookAt != transform)
            {
                continue;
            }

            PlayerDamageCinemachineShake shake = camera.GetComponent<PlayerDamageCinemachineShake>();
            if (shake == null)
            {
                shake = camera.gameObject.AddComponent<PlayerDamageCinemachineShake>();
            }

            shake.Play(shakeDuration, shakeMagnitude, cinemachineShakeFrequency, useUnscaledTime);
            foundTargetingCamera = true;
        }

        if (!foundTargetingCamera && CameraController.Instance != null)
        {
            CameraController.Instance.ShakeCamera(shakeDuration, shakeMagnitude);
        }
    }

    private void PlayScreenTint(int damage)
    {
        if (screenTintDuration <= 0f || screenTintColor.a <= 0f)
        {
            return;
        }

        CreateScreenTintOverlay();
        StopScreenTint();
        screenTintCoroutine = StartCoroutine(ScreenTintRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        ApplyFlashColor();

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        RestoreColor();
        flashCoroutine = null;
    }

    private IEnumerator ScreenTintRoutine()
    {
        screenTintImage.color = screenTintColor;

        float elapsed = 0f;
        while (elapsed < screenTintDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            Color color = screenTintColor;
            color.a *= Mathf.Clamp01(screenTintFadeCurve.Evaluate(Mathf.Clamp01(elapsed / screenTintDuration)));
            screenTintImage.color = color;
            yield return null;
        }

        screenTintImage.color = Color.clear;
        screenTintCoroutine = null;
    }

    private void CreateScreenTintOverlay()
    {
        if (screenTintImage != null)
        {
            return;
        }

        GameObject overlay = new GameObject("Player Damage Screen Tint", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
        overlay.transform.SetParent(transform, false);

        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = screenTintSortOrder;

        CanvasScaler canvasScaler = overlay.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);

        GraphicRaycaster raycaster = overlay.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        screenTintImage = overlay.GetComponent<Image>();
        screenTintImage.raycastTarget = false;
        screenTintImage.color = Color.clear;

        RectTransform rectTransform = screenTintImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void CacheOriginalColor()
    {
        if (hasOriginalSkeletonColor || skeletonAnimation == null)
        {
            return;
        }

        if (skeletonAnimation.Skeleton == null)
        {
            skeletonAnimation.Initialize(false);
        }

        if (skeletonAnimation.Skeleton == null)
        {
            return;
        }

        originalSkeletonColor = skeletonAnimation.Skeleton.GetColor();
        hasOriginalSkeletonColor = true;
    }

    private void ApplyFlashColor()
    {
        if (skeletonAnimation != null && skeletonAnimation.Skeleton != null)
        {
            Color targetColor = Color.Lerp(originalSkeletonColor, flashColor, flashAmount);
            skeletonAnimation.Skeleton.SetColor(targetColor);
        }
    }

    private void RestoreColor()
    {
        if (hasOriginalSkeletonColor && skeletonAnimation != null && skeletonAnimation.Skeleton != null)
        {
            skeletonAnimation.Skeleton.SetColor(originalSkeletonColor);
        }
    }

    private void StopFlash()
    {
        if (flashCoroutine == null)
        {
            return;
        }

        StopCoroutine(flashCoroutine);
        flashCoroutine = null;
    }

    private void StopScreenTint()
    {
        if (screenTintCoroutine != null)
        {
            StopCoroutine(screenTintCoroutine);
            screenTintCoroutine = null;
        }

        if (screenTintImage != null)
        {
            screenTintImage.color = Color.clear;
        }
    }
}

[DisallowMultipleComponent]
public sealed class PlayerDamageCinemachineShake : CinemachineExtension
{
    private float amplitude;
    private float frequency;
    private float endTime;
    private bool useUnscaledTime;

    public void Play(float duration, float newAmplitude, float newFrequency, bool useUnscaled)
    {
        amplitude = newAmplitude;
        frequency = newFrequency;
        useUnscaledTime = useUnscaled;
        endTime = CurrentTime + duration;
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase virtualCamera,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || frequency <= 0f || amplitude <= 0f)
        {
            return;
        }

        float remaining = endTime - CurrentTime;
        if (remaining <= 0f)
        {
            amplitude = 0f;
            return;
        }

        float time = CurrentTime * frequency;
        Vector3 offset = new Vector3(
            Mathf.PerlinNoise(time, 0.17f) - 0.5f,
            Mathf.PerlinNoise(0.43f, time) - 0.5f,
            0f) * (amplitude * 2f);
        state.PositionCorrection += state.GetCorrectedOrientation() * offset;
    }

    private float CurrentTime => useUnscaledTime ? Time.unscaledTime : Time.time;
}

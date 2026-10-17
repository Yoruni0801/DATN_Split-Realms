using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

public struct MapHangArrivalConfig
{
    public CinemachineCamera camera;
    public float cameraDuration;
    public float preGateDelay;
    public float gateAppearDuration;
    public float gateStayDuration;
    public float playerFadeDelay;
    public float playerFadeDuration;
    public float postPlayerDelay;
    public float gateDisappearDuration;
}

[DisallowMultipleComponent]
public sealed class MapHangTransitionFade2D : MonoBehaviour
{
    private static MapHangTransitionFade2D instance;

    private Image fadeImage;
    private Transform _savedCameraFollow;
    private Coroutine currentArrivalRoutine;

    public static MapHangTransitionFade2D GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<MapHangTransitionFade2D>();
        if (instance != null)
        {
            return instance;
        }

        GameObject fadeObject = new GameObject("Map Hang Transition Fade");
        instance = fadeObject.AddComponent<MapHangTransitionFade2D>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        transform.SetParent(null);
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }
        CreateOverlay();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public IEnumerator FadeToBlack(float duration)
    {
        yield return FadeTo(1f, duration);
    }

    public IEnumerator FadeToClear(float duration)
    {
        yield return FadeTo(0f, duration);
    }

    public IEnumerator FadeToClearWithAppear(float duration, Transform target, float appearDuration, Vector3 targetScale)
    {
        if (fadeImage == null)
        {
            yield break;
        }

        fadeImage.gameObject.SetActive(true);
        fadeImage.raycastTarget = true;

        Color color = fadeImage.color;
        float startAlpha = color.a;
        float elapsed = 0f;
        float fadeDuration = Mathf.Max(0.01f, duration);
        float appearDurationClamped = Mathf.Max(0.01f, appearDuration);

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));

            color.a = Mathf.Lerp(startAlpha, 0f, t);
            fadeImage.color = color;

            if (target != null)
            {
                float appearT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / appearDurationClamped));
                target.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, appearT);
            }

            yield return null;
        }

        color.a = 0f;
        fadeImage.color = color;
        fadeImage.raycastTarget = false;
        fadeImage.gameObject.SetActive(false);

        if (target != null)
        {
            target.localScale = targetScale;
        }
    }

    public void BeginMapArrival(
        GoalBlackHoleExit2D destinationExit,
        BlackHolePortalPlayerContext playerContext,
        MapHangStageRoot2D previousStage,
        float blackHoldDuration,
        float fadeToClearDuration)
    {
        if (currentArrivalRoutine != null)
        {
            StopCoroutine(currentArrivalRoutine);
        }

        currentArrivalRoutine = StartCoroutine(MapArrivalRoutine(
            destinationExit,
            playerContext,
            previousStage,
            blackHoldDuration,
            fadeToClearDuration,
            default));
    }

    public void BeginMapArrival(
        GoalBlackHoleExit2D destinationExit,
        BlackHolePortalPlayerContext playerContext,
        MapHangStageRoot2D previousStage,
        float blackHoldDuration,
        float fadeToClearDuration,
        MapHangArrivalConfig config)
    {
        if (currentArrivalRoutine != null)
        {
            StopCoroutine(currentArrivalRoutine);
        }

        currentArrivalRoutine = StartCoroutine(MapArrivalRoutine(
            destinationExit,
            playerContext,
            previousStage,
            blackHoldDuration,
            fadeToClearDuration,
            config));
    }

    private IEnumerator MapArrivalRoutine(
        GoalBlackHoleExit2D destinationExit,
        BlackHolePortalPlayerContext playerContext,
        MapHangStageRoot2D previousStage,
        float blackHoldDuration,
        float fadeToClearDuration,
        MapHangArrivalConfig config)
    {
        if (destinationExit == null)
        {
            yield break;
        }

        CinemachineCamera activeCamera = config.camera != null
            ? config.camera
            : FindFirstObjectByType<CinemachineCamera>();

        // Lấy CinemachineBrain và Camera chính để kiểm soát trực tiếp trong lúc chuyển cảnh
        CinemachineBrain brain = FindFirstObjectByType<CinemachineBrain>();
        Camera mainCamera = Camera.main;

        float preGateDelay = config.preGateDelay > 0f ? config.preGateDelay : 0.25f;
        float gateAppearDuration = config.gateAppearDuration > 0f ? config.gateAppearDuration : 0.7f;
        float gateStayDuration = config.gateStayDuration > 0f ? config.gateStayDuration : 0.35f;
        float playerFadeDelay = config.playerFadeDelay > 0f ? config.playerFadeDelay : 0f;
        float playerFadeDuration = config.playerFadeDuration > 0f ? config.playerFadeDuration : 0.7f;
        float postPlayerDelay = config.postPlayerDelay > 0f ? config.postPlayerDelay : 0.4f;
        float gateDisappearDuration = config.gateDisappearDuration > 0f ? config.gateDisappearDuration : 0.7f;

        Player resolvedPlayer = playerContext != null && playerContext.Player != null
            ? playerContext.Player
            : (playerContext?.Root != null ? playerContext.Root.GetComponent<Player>() : PlayerRegistry.Current);

        PlayerSceneScaleController scaleCtrl = resolvedPlayer != null
            ? resolvedPlayer.GetComponent<PlayerSceneScaleController>()
            : null;

        Vector3 originalVisualScale;
        if (scaleCtrl != null && scaleCtrl.BasePlayerScale != Vector3.zero)
        {
            float targetMultiplier = scaleCtrl.CurrentScaleMultiplier > 0f ? scaleCtrl.CurrentScaleMultiplier : 1f;
            float facingSign = resolvedPlayer != null ? (resolvedPlayer.transform.localScale.x < 0f ? -1f : 1f) : 1f;
            originalVisualScale = new Vector3(
                scaleCtrl.BasePlayerScale.x * targetMultiplier * facingSign,
                scaleCtrl.BasePlayerScale.y * targetMultiplier,
                scaleCtrl.BasePlayerScale.z != 0f ? scaleCtrl.BasePlayerScale.z : 1f);
        }
        else
        {
            originalVisualScale = playerContext != null && playerContext.VisualScale != Vector3.zero
                ? playerContext.VisualScale
                : (playerContext?.Visual != null ? playerContext.Visual.localScale : Vector3.one);
        }

        if (originalVisualScale == Vector3.zero)
        {
            originalVisualScale = Vector3.one;
        }

        Vector2 portalCenter = destinationExit.PortalEffect != null
            ? destinationExit.PortalEffect.PortalCenterPosition
            : (Vector2)destinationExit.transform.position;

        Vector2 releasePos = destinationExit.ResolveReleasePosition();

        // Tính toán góc xoay Surface đích cho Camera và Player
        Quaternion targetCameraRotation = Quaternion.identity;
        if (!destinationExit.IsFinalExit)
        {
            PlayerRotaSurfaceController.SurfaceSide exitSurface = destinationExit.ResolveExitSurface();
            float targetAngle = PlayerRotaSurfaceController.GetSurfaceAngle(PlayerRotaSurfaceController.GetNormal(exitSurface));
            targetCameraRotation = Quaternion.Euler(0f, 0f, targetAngle);
        }

        // Đảm bảo cổng đến và stage chứa cổng đến luôn ở trạng thái Active
        if (destinationExit.StageRoot != null)
        {
            destinationExit.StageRoot.SetStageActive(true);
        }
        destinationExit.gameObject.SetActive(true);

        // 1. Đặt player tại tâm cổng và KHÓA CHẶT physics
        destinationExit.PlacePlayerAtRelease(playerContext);
        SetPlayerPosition(playerContext, portalCenter);

        // ÁP DỤNG NGAY SURFACE VÀ GÓC XOAY ĐÍCH CHO PLAYER & CAMERA TRONG MÀN ĐEN
        if (!destinationExit.IsFinalExit)
        {
            destinationExit.ApplySurfaceOverride(playerContext);
        }
        else
        {
            Player player = playerContext != null && playerContext.Player != null
                ? playerContext.Player
                : playerContext != null && playerContext.Root != null
                    ? playerContext.Root.GetComponent<Player>()
                    : PlayerRegistry.Current;

            if (player != null)
            {
                player.SetRotaMode(false);
                player.transform.rotation = Quaternion.identity;
            }
        }

        if (destinationExit.PortalEffect != null && playerContext != null)
        {
            destinationExit.PortalEffect.LockPlayer(playerContext);
        }

        // Ẩn visual player (scale = 0)
        if (playerContext?.Visual != null)
        {
            playerContext.Visual.localScale = Vector3.zero;
        }

        // 2. Ẩn visual của cổng ra trước để chuẩn bị animation xuất hiện từ từ
        if (destinationExit.PortalEffect != null)
        {
            destinationExit.PortalEffect.HidePortalImmediate();
        }

        // 3. Tắt stage cũ (CHỈ TẮT NẾU previousStage KHÔNG PHẢI là stage chứa cổng đến!)
        if (previousStage != null && previousStage != destinationExit.StageRoot)
        {
            previousStage.SetStageActive(false);
        }

        // 4. CẬP NHẬT GÓC XOAY VÀ RESET DAMPING STATE CỦA CAMERA TRONG MÀN ĐEN
        // Giữ CinemachineBrain hoạt động và không can thiệp đè Follow/vị trí thủ công để tránh giật camera
        if (activeCamera != null)
        {
            activeCamera.transform.rotation = targetCameraRotation;
            activeCamera.PreviousStateIsValid = false;
        }

        if (mainCamera != null)
        {
            mainCamera.transform.rotation = targetCameraRotation;
        }

        // 5. Giữ màn đen một lát để cảnh load ổn định
        if (blackHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(blackHoldDuration);
        }

        // 6. Màn hình từ từ sáng lên (Fade out lớp đen)
        yield return FadeToClear(fadeToClearDuration);

        // 7. Dừng một chút để người chơi kịp định hình không gian mới
        if (preGateDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(preGateDelay);
        }

        // 8. CỔNG TỪ TỪ XUẤT HIỆN TRƯỚC
        if (destinationExit.PortalEffect != null)
        {
            yield return destinationExit.PortalEffect.ShowPortalWithAnimation(gateAppearDuration);
        }

        // 9. Cổng dừng lại một nhịp mở rõ ràng
        if (gateStayDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(gateStayDuration);
        }

        if (playerFadeDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(playerFadeDelay);
        }

        // 10. PLAYER TỪ TỪ XUẤT HIỆN TỪ LÒNG CỔNG BƯỚC RA VỊ TRÍ ĐÍCH
        if (destinationExit.PortalEffect != null && playerContext != null)
        {
            destinationExit.PortalEffect.LockPlayer(playerContext);
        }

        yield return PlayerAppearRoutine(playerContext, portalCenter, releasePos, originalVisualScale, playerFadeDuration);

        // Giải phóng và mở khóa Player ngay khi bước ra vị trí đích
        destinationExit.ReleasePlayerImmediately(playerContext);

        // 11. Dừng một nhịp để người chơi kịp nhìn rõ player đứng trước cổng
        if (postPlayerDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(postPlayerDelay);
        }

        // 12. Cổng từ từ biến mất hoàn toàn
        yield return destinationExit.CompleteArrival(playerContext, gateDisappearDuration);

        // 13. Cập nhật góc xoay camera sau khi hoàn thành
        if (activeCamera != null)
        {
            activeCamera.transform.rotation = targetCameraRotation;
        }

        currentArrivalRoutine = null;
    }

    private IEnumerator PlayerAppearRoutine(
        BlackHolePortalPlayerContext playerContext,
        Vector2 startPosition,
        Vector2 targetPosition,
        Vector3 targetScale,
        float duration)
    {
        if (playerContext == null)
        {
            yield break;
        }

        Transform visual = playerContext.Visual;
        if (visual != null)
        {
            visual.localScale = Vector3.zero;
        }

        SetPlayerPosition(playerContext, startPosition);

        float elapsed = 0f;
        float clampedDuration = Mathf.Max(0.01f, duration);

        while (elapsed < clampedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / clampedDuration));

            if (visual != null)
            {
                visual.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, t);
            }

            Vector2 currentPos = Vector2.Lerp(startPosition, targetPosition, t);
            SetPlayerPosition(playerContext, currentPos);

            yield return null;
        }

        if (visual != null)
        {
            visual.localScale = targetScale;
        }

        SetPlayerPosition(playerContext, targetPosition);
    }

    private static void SetPlayerPosition(BlackHolePortalPlayerContext context, Vector2 position)
    {
        if (context == null)
            return;

        if (context.Rigidbody != null)
        {
            context.Rigidbody.position = position;
            context.Rigidbody.linearVelocity = Vector2.zero;
        }
        else if (context.Root != null)
        {
            context.Root.position = position;
        }
    }


    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeImage == null)
        {
            yield break;
        }

        fadeImage.gameObject.SetActive(true);
        fadeImage.raycastTarget = true;

        Color color = fadeImage.color;
        float startAlpha = color.a;
        float elapsed = 0f;
        float fadeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / fadeDuration));

            color.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            fadeImage.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        fadeImage.color = color;
        fadeImage.raycastTarget = targetAlpha > 0f;

        if (targetAlpha <= 0f)
        {
            fadeImage.gameObject.SetActive(false);
        }
    }

    private void CreateOverlay()
    {
        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        GameObject imageObject = new GameObject(
            "Fade",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        imageObject.transform.SetParent(transform, false);

        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        fadeImage = imageObject.GetComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;
        fadeImage.gameObject.SetActive(false);
    }
}

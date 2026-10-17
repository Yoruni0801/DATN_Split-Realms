using System.Collections;
using UnityEngine;

public enum BlackHolePortalPhase
{
    Idle,
    PullingIn,
    Releasing,
    Consumed
}

public sealed class BlackHolePortalPlayerContext
{
    public Transform Root { get; set; }
    public Player Player { get; set; }
    public Rigidbody2D Rigidbody { get; set; }
    public Transform Visual { get; set; }
    public Vector3 VisualScale { get; set; }
    public Quaternion VisualRotation { get; set; }
    public Vector3 VisualPosition { get; set; }
    public float GravityScale { get; set; }
    public Vector2 Velocity { get; set; }
    public float AngularVelocity { get; set; }
    public RigidbodyType2D BodyType { get; set; }
    public RigidbodyInterpolation2D Interpolation { get; set; }
    public AnimationSpine Spine { get; set; }
    public float SpineScaleX { get; set; }
    public bool HasSpineScaleX { get; set; }
}

[RequireComponent(typeof(Collider2D))]
public sealed class BlackHolePortalEffect2D : MonoBehaviour
{
    [Header("Portal Visual")]
    [SerializeField] private Transform portalCenter;
    [SerializeField] private Transform portalVisualRoot;
    [SerializeField] private float portalDisappearDuration = 0.2f;

    [Header("Player Visual")]
    private Transform playerVisual;

    [Header("Suction")]
    private float suctionDuration = 2.5f;
    [SerializeField] private float orbitTurns = 0.6f;
    [SerializeField] private AnimationCurve suctionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Distortion")]
    [SerializeField] private bool enableDistortion = true;
    [SerializeField, Range(0f, 1f)] private float distortionStrength = 0.3f;
    [SerializeField] private float distortionFrequency = 6f;
    [SerializeField, Range(0f, 2f)] private float stretchStrength = 0.5f;
    [SerializeField, Range(0.01f, 1f)] private float finalScale = 0.05f;

    [Header("Arrival")]
    [SerializeField] private bool resetVelocity = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip suctionClip;
    [SerializeField, Range(0f, 1f)] private float suctionVolume = 1f;

    private Collider2D portalCollider;
    private Vector3 originalPortalScale;

    public Vector2 PortalCenterPosition =>
        portalCenter != null ? portalCenter.position : transform.position;

    private void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    private void Awake()
    {
        EnsureInitialized();
        // Không tự ý đặt portalVisualRoot.localScale = originalPortalScale ở Awake
        // để tránh pop visual scale nếu đối tượng là Exit hoặc đang chờ animation xuất hiện.
        // Component cổng vào (GoalBlackHolePortal2D) sẽ tự gọi ShowPortal() khi cần.
    }

    private void EnsureInitialized()
    {
        if (portalCollider == null)
        {
            portalCollider = GetComponent<Collider2D>();
        }

        if (portalCollider != null)
        {
            portalCollider.isTrigger = true;
        }

        if (portalCenter == null)
        {
            portalCenter = transform;
        }

        if (portalVisualRoot == null)
        {
            portalVisualRoot = portalCenter;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (originalPortalScale == Vector3.zero)
        {
            if (portalVisualRoot != null && portalVisualRoot.localScale != Vector3.zero)
            {
                originalPortalScale = portalVisualRoot.localScale;
            }
            else if (transform.localScale != Vector3.zero)
            {
                originalPortalScale = transform.localScale;
            }
            else
            {
                originalPortalScale = Vector3.one;
            }
        }
    }

    public void SetTriggerEnabled(bool enabled)
    {
        if (portalCollider == null)
        {
            portalCollider = GetComponent<Collider2D>();
        }

        if (portalCollider != null)
        {
            portalCollider.enabled = enabled;
        }
    }

    public void ShowPortal()
    {
        EnsureInitialized();

        if (portalVisualRoot != null)
        {
            portalVisualRoot.localScale = originalPortalScale != Vector3.zero
                ? originalPortalScale
                : Vector3.one;
        }

        gameObject.SetActive(true);
        SetTriggerEnabled(true);
    }

    public IEnumerator ShowPortalWithAnimation(float duration)
    {
        EnsureInitialized();

        float durationClamped = Mathf.Max(0.01f, duration);
        Vector3 startScale = portalVisualRoot != null ? portalVisualRoot.localScale : Vector3.zero;
        Vector3 targetScale = originalPortalScale != Vector3.zero
            ? originalPortalScale
            : Vector3.one;

        gameObject.SetActive(true);
        SetTriggerEnabled(true);

        float elapsed = 0f;
        while (elapsed < durationClamped)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / durationClamped));

            if (portalVisualRoot != null)
            {
                portalVisualRoot.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            }

            yield return null;
        }

        if (portalVisualRoot != null)
        {
            portalVisualRoot.localScale = targetScale;
        }
    }

    public void HidePortalImmediate()
    {
        EnsureInitialized();

        if (portalVisualRoot != null)
        {
            portalVisualRoot.localScale = Vector3.zero;
        }

        SetTriggerEnabled(false);
    }

    public void TeleportPlayer(
        BlackHolePortalPlayerContext context,
        Vector2 destination,
        bool unlockPlayer)
    {
        SetPlayerPosition(context, destination);
        if (unlockPlayer)
        {
            RestorePlayer(context, true);
        }
        else
        {
            LockPlayer(context);
        }
    }

    public void UnlockPlayer(BlackHolePortalPlayerContext context)
    {
        RestoreFacing(context);

        if (context.Player != null)
        {
            context.Player.SetControlLocked(false);
        }
    }

    public bool TryCreatePlayerContext(
        Collider2D other,
        string playerTag,
        out BlackHolePortalPlayerContext context)
    {
        context = null;

        if (!TryResolvePlayer(other, playerTag, out Transform playerRoot))
        {
            return false;
        }

        Player player = playerRoot.GetComponent<Player>();
        if (player == null)
        {
            player = playerRoot.GetComponentInParent<Player>();
        }

        Rigidbody2D rb = playerRoot.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = playerRoot.GetComponentInParent<Rigidbody2D>();
        }

        Transform actualRoot = rb != null ? rb.transform : playerRoot;
        Transform visual = ResolvePlayerVisual(actualRoot);
        AnimationSpine spine = player != null
            ? player.GetComponentInChildren<AnimationSpine>()
            : actualRoot.GetComponentInChildren<AnimationSpine>();
        bool hasSpineScaleX = spine != null && spine.Skeleton != null;

        context = new BlackHolePortalPlayerContext
        {
            Root = actualRoot,
            Player = player,
            Rigidbody = rb,
            Visual = visual,
            VisualScale = visual != null ? visual.localScale : Vector3.one,
            VisualRotation = visual != null ? visual.localRotation : Quaternion.identity,
            VisualPosition = visual != null ? visual.localPosition : Vector3.zero,
            GravityScale = rb != null ? rb.gravityScale : 0f,
            Velocity = rb != null ? rb.linearVelocity : Vector2.zero,
            AngularVelocity = rb != null ? rb.angularVelocity : 0f,
            BodyType = rb != null ? rb.bodyType : RigidbodyType2D.Dynamic,
            Interpolation = rb != null
                ? rb.interpolation
                : RigidbodyInterpolation2D.None,
            Spine = spine,
            SpineScaleX = hasSpineScaleX ? spine.Skeleton.ScaleX : 1f,
            HasSpineScaleX = hasSpineScaleX
        };

        return true;
    }

    public void LockPlayer(BlackHolePortalPlayerContext context)
    {
        if (context.Rigidbody != null)
        {
            context.Rigidbody.interpolation = RigidbodyInterpolation2D.None;
            context.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
            context.Rigidbody.linearVelocity = Vector2.zero;
            context.Rigidbody.angularVelocity = 0f;
            context.Rigidbody.gravityScale = 0f;
        }

        if (context.Player != null)
        {
            context.Player.SetControlLocked(true);
        }
    }

    public IEnumerator PullPlayerIn(BlackHolePortalPlayerContext context)
    {
        Vector2 center = portalCenter.position;
        Vector2 startOffset = (Vector2)context.Root.position - center;
        float startRadius = startOffset.magnitude;

        float startAngle = Mathf.Atan2(startOffset.y, startOffset.x);
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, suctionDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float t = Mathf.Clamp01(suctionCurve.Evaluate(normalizedTime));
            float radius = Mathf.Lerp(startRadius, 0f, Mathf.Pow(t, 1.35f));
            float angle = startAngle + orbitTurns * Mathf.PI * 2f * t;

            SetPlayerPosition(
                context,
                center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            ApplyPlayerVisual(context, t, elapsed, false);
            yield return null;
        }

        SetPlayerPosition(context, center);
        ApplyPlayerVisual(context, 1f, elapsed, false);
        yield return new WaitForSecondsRealtime(0.08f);
    }

    public IEnumerator HidePortal()
    {
        yield return HidePortalWithAnimation(portalDisappearDuration);
    }

    public IEnumerator HidePortalWithAnimation(float duration)
    {
        float durationClamped = Mathf.Max(0.01f, duration);
        Vector3 startScale = portalVisualRoot != null ? portalVisualRoot.localScale : originalPortalScale;
        float elapsed = 0f;

        while (elapsed < durationClamped)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / durationClamped));
            if (portalVisualRoot != null)
            {
                portalVisualRoot.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, t);
            }
            yield return null;
        }

        if (portalVisualRoot != null)
        {
            portalVisualRoot.localScale = Vector3.zero;
        }
        SetTriggerEnabled(false);
    }

    public void PlaySuctionSound()
    {
        if (suctionClip == null)
        {
            return;
        }

        if (audioSource != null && audioSource.isActiveAndEnabled)
        {
            audioSource.PlayOneShot(suctionClip, suctionVolume);
            return;
        }

        AudioSource.PlayClipAtPoint(suctionClip, transform.position, suctionVolume);
    }

    private void RestorePlayer(
        BlackHolePortalPlayerContext context,
        bool unlockPlayer)
    {
        if (context.Visual != null)
        {
            context.Visual.localScale = context.VisualScale;

            if (context.Visual != context.Root)
            {
                context.Visual.localRotation = context.VisualRotation;
                context.Visual.localPosition = context.VisualPosition;
            }
        }

        if (context.Rigidbody != null)
        {
            context.Rigidbody.bodyType = context.BodyType;
            context.Rigidbody.gravityScale = context.GravityScale;
            context.Rigidbody.interpolation = context.Interpolation;
            context.Rigidbody.linearVelocity = resetVelocity
                ? Vector2.zero
                : context.Velocity;
            context.Rigidbody.angularVelocity = resetVelocity
                ? 0f
                : context.AngularVelocity;
        }

        Physics2D.SyncTransforms();
        RestoreFacing(context);

        if (unlockPlayer && context.Player != null)
        {
            context.Player.SetControlLocked(false);
        }
    }

    private void SetPlayerPosition(
        BlackHolePortalPlayerContext context,
        Vector2 position)
    {
        if (context.Rigidbody != null)
        {
            context.Rigidbody.position = position;
            context.Rigidbody.linearVelocity = Vector2.zero;
        }
        else
        {
            context.Root.position = position;
        }
    }

    private void ApplyPlayerVisual(
        BlackHolePortalPlayerContext context,
        float progress,
        float elapsed,
        bool reverse)
    {
        if (context.Visual == null)
        {
            return;
        }

        float overallScale = reverse
            ? Mathf.Lerp(finalScale, 1f, Mathf.Pow(progress, 2f))
            : Mathf.Lerp(1f, finalScale, Mathf.Pow(progress, 2f));

        if (context.Visual != context.Root)
        {
            context.Visual.localRotation = context.VisualRotation;
            context.Visual.localPosition = context.VisualPosition;
        }

        context.Visual.localScale = new Vector3(
            context.VisualScale.x * overallScale,
            context.VisualScale.y * overallScale,
            context.VisualScale.z);
    }

    private Transform ResolvePlayerVisual(Transform playerRoot)
    {
        Player registeredPlayer = PlayerRegistry.Current;
        if (registeredPlayer != null)
        {
            return registeredPlayer.transform;
        }

        Transform visual = playerVisual;
        return visual == playerRoot ? null : visual;
    }

    private static void RestoreFacing(BlackHolePortalPlayerContext context)
    {
        if (context.Spine != null)
        {
            context.Spine.SetFlipX(false);
        }

        if (context.Player == null)
        {
            return;
        }

        PlayerRotaSurfaceController rotaController =
            context.Player.GetComponent<PlayerRotaSurfaceController>();
        if (rotaController != null)
        {
            rotaController.ResetHorizontalMotionAfterTeleport();
        }
    }

    private static bool TryResolvePlayer(
        Collider2D other,
        string playerTag,
        out Transform playerTransform)
    {
        playerTransform = null;

        if (other == null || string.IsNullOrEmpty(playerTag))
        {
            return false;
        }

        if (other.CompareTag(playerTag))
        {
            playerTransform = other.transform;
            return true;
        }

        if (other.attachedRigidbody != null &&
            other.attachedRigidbody.CompareTag(playerTag))
        {
            playerTransform = other.attachedRigidbody.transform;
            return true;
        }

        Transform root = other.transform.root;
        if (root != null && root.CompareTag(playerTag))
        {
            playerTransform = root;
            return true;
        }

        return false;
    }

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Transform center = portalCenter != null ? portalCenter : transform;
        Gizmos.DrawWireSphere(center.position, 0.25f);
    }

#endif
}

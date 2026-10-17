using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BlackHoleTeleport2D : MonoBehaviour
{
    [Header("Black Hole")]
    [SerializeField] private Transform blackHoleCenter;
    [SerializeField] private Transform destinationPoint;

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Chỉ object hình ảnh của Player sẽ xoay/méo. Không kéo Camera vào đây.")]
    [SerializeField] private Transform playerVisual;

    [Header("Suction")]
    [SerializeField] private float suctionDuration = 1.8f;

    [Tooltip("Số vòng Player xoay quanh hố đen.")]
    [SerializeField] private float orbitTurns = 1.5f;

    [SerializeField] private AnimationCurve suctionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Visual Spin")]
    [Tooltip("Tốc độ xoay của hình Player.")]
    [SerializeField] private float playerSpinSpeed = 140f;

    [Tooltip("Tăng tốc nhẹ khi gần tâm.")]
    [SerializeField] private float spinAcceleration = 1.4f;

    [Header("Distortion")]
    [SerializeField] private bool enableDistortion = true;

    [Tooltip("Độ bóp méo qua lại.")]
    [SerializeField, Range(0f, 1f)]
    private float distortionStrength = 0.35f;

    [Tooltip("Tốc độ rung/bóp méo.")]
    [SerializeField] private float distortionFrequency = 7f;

    [Tooltip("Độ kéo dài cơ thể khi bị hút.")]
    [SerializeField, Range(0f, 2f)]
    private float stretchStrength = 0.55f;

    [Tooltip("Độ nghiêng/vặn phụ khi bị hút.")]
    [SerializeField, Range(0f, 45f)]
    private float twistStrength = 12f;

    [Tooltip("Player nhỏ còn bao nhiêu khi vào tâm.")]
    [SerializeField, Range(0.01f, 1f)]
    private float finalScale = 0.05f;

    [Header("Teleport")]
    [SerializeField] private bool resetVelocity = true;

    [Header("Surface Override")]
    [Tooltip("Hướng bề mặt bám tại điểm đến Destination Point (Floor, LeftWall, Ceiling, RightWall).")]
    [SerializeField] private GoalExitSurfaceMode destinationSurfaceMode = GoalExitSurfaceMode.None;

    [Tooltip("Điểm trả Player về sau khi hoàn thành minigame. Để trống sẽ dùng vị trí bên phải portal.")]
    [SerializeField] private Transform returnPoint;

    [SerializeField] private Vector2 fallbackReturnOffset = new Vector2(2f, 0f);

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip suctionClip;

    [SerializeField, Range(0f, 1f)]
    private float suctionVolume = 1f;

    private bool isSucking;

    public Vector3 ReturnPosition
    {
        get
        {
            if (returnPoint != null)
            {
                return returnPoint.position;
            }

            Transform center = blackHoleCenter != null
                ? blackHoleCenter
                : transform;
            Vector3 worldOffset = center.TransformDirection(
                new Vector3(fallbackReturnOffset.x, fallbackReturnOffset.y, 0f));

            return center.position + worldOffset;
        }
    }

    public void ApplyTeleportCooldown(float customCooldown = -1f)
    {
    }

    private void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    private void Awake()
    {
        if (blackHoleCenter == null)
        {
            blackHoleCenter = transform;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartTeleport(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartTeleport(other);
    }

    private void TryStartTeleport(Collider2D other)
    {
        if (isSucking)
        {
            return;
        }

        if (!TryGetPlayer(other, out Transform playerRoot))
        {
            return;
        }

        PlayerHealth playerHealth = playerRoot.GetComponent<PlayerHealth>()
            ?? playerRoot.GetComponentInChildren<PlayerHealth>()
            ?? playerRoot.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        StartCoroutine(SuckPlayerRoutine(playerRoot));
    }

    private IEnumerator SuckPlayerRoutine(Transform playerRoot)
    {
        if (destinationPoint == null)
        {
            Debug.LogWarning(
                "[BlackHoleTeleport2D] Destination Point chưa được gán.",
                this);

            yield break;
        }

        PlayerHealth health = playerRoot.GetComponent<PlayerHealth>()
            ?? playerRoot.GetComponentInChildren<PlayerHealth>()
            ?? playerRoot.GetComponentInParent<PlayerHealth>();

        if (health != null && health.IsDead)
        {
            yield break;
        }

        isSucking = true;

        Player player = PlayerRegistry.Current;
        if (player == null)
        {
            player = playerRoot.GetComponent<Player>();
            if (player == null)
            {
                player = playerRoot.GetComponentInParent<Player>();
            }
        }

        Rigidbody2D rb = playerRoot.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = playerRoot.GetComponentInParent<Rigidbody2D>();
        }

        Transform actualRoot = rb != null ? rb.transform : playerRoot;
        Transform visual = player != null ? player.transform : playerVisual;
        Vector3 originalVisualScale = visual != null
            ? visual.localScale
            : Vector3.one;
        Quaternion originalVisualRotation = visual != null
            ? visual.localRotation
            : Quaternion.identity;
        Vector3 originalVisualLocalPosition = visual != null
            ? visual.localPosition
            : Vector3.zero;
        AnimationSpine spine = player != null
            ? player.GetComponentInChildren<AnimationSpine>()
            : actualRoot.GetComponentInChildren<AnimationSpine>();
        bool hasSpineScaleX = spine != null && spine.Skeleton != null;
        float originalSpineScaleX = hasSpineScaleX
            ? spine.Skeleton.ScaleX
            : 1f;

        float originalGravityScale = rb != null ? rb.gravityScale : 0f;
        Vector2 originalVelocity = rb != null
            ? rb.linearVelocity
            : Vector2.zero;
        float originalAngularVelocity = rb != null
            ? rb.angularVelocity
            : 0f;
        RigidbodyType2D originalBodyType = rb != null
            ? rb.bodyType
            : RigidbodyType2D.Dynamic;
        RigidbodyInterpolation2D originalInterpolation = rb != null
            ? rb.interpolation
            : RigidbodyInterpolation2D.None;

        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation2D.None;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
        }

        if (player != null)
        {
            player.SetControlLocked(true);
        }

        PlaySuctionSound();

        Vector2 centerPosition = blackHoleCenter.position;
        Vector2 startOffset = (Vector2)actualRoot.position - centerPosition;
        float startRadius = startOffset.magnitude;
        float startAngle = Mathf.Atan2(startOffset.y, startOffset.x);
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, suctionDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float suctionT = Mathf.Clamp01(
                suctionCurve.Evaluate(normalizedTime));
            float radius = Mathf.Lerp(
                startRadius,
                0f,
                Mathf.Pow(suctionT, 1.35f));
            float angle = startAngle +
                orbitTurns * Mathf.PI * 2f * suctionT;
            Vector2 targetPosition = centerPosition +
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            SetPlayerPosition(actualRoot, rb, targetPosition);
            ApplyPlayerShrink(
                actualRoot,
                visual,
                originalVisualScale,
                originalVisualRotation,
                originalVisualLocalPosition,
                suctionT);

            yield return null;
        }

        SetPlayerPosition(actualRoot, rb, centerPosition);
        ApplyPlayerShrink(
            actualRoot,
            visual,
            originalVisualScale,
            originalVisualRotation,
            originalVisualLocalPosition,
            1f);

        yield return new WaitForSecondsRealtime(0.08f);

        SetDestinationState(
            actualRoot,
            rb,
            player,
            visual,
            spine,
            originalVisualScale,
            originalVisualRotation,
            originalVisualLocalPosition,
            originalBodyType,
            originalGravityScale,
            originalInterpolation,
            originalVelocity,
            originalAngularVelocity);

        isSucking = false;
    }

    public PlayerRotaSurfaceController.SurfaceSide ResolveDestinationSurface()
    {
        switch (destinationSurfaceMode)
        {
            case GoalExitSurfaceMode.LeftWall:
                return PlayerRotaSurfaceController.SurfaceSide.LeftWall;
            case GoalExitSurfaceMode.Ceiling:
                return PlayerRotaSurfaceController.SurfaceSide.Ceiling;
            case GoalExitSurfaceMode.RightWall:
                return PlayerRotaSurfaceController.SurfaceSide.RightWall;
            case GoalExitSurfaceMode.Floor:
                return PlayerRotaSurfaceController.SurfaceSide.Floor;
            default:
                GoalBlackHoleExit2D initialExit = destinationPoint != null
                    ? (destinationPoint.GetComponent<GoalBlackHoleExit2D>()
                        ?? destinationPoint.GetComponentInChildren<GoalBlackHoleExit2D>()
                        ?? destinationPoint.GetComponentInParent<GoalBlackHoleExit2D>())
                    : null;
                return initialExit != null
                    ? initialExit.ResolveExitSurface()
                    : PlayerRotaSurfaceController.SurfaceSide.Floor;
        }
    }

    private void SetDestinationState(
        Transform actualRoot,
        Rigidbody2D rb,
        Player player,
        Transform visual,
        AnimationSpine spine,
        Vector3 originalVisualScale,
        Quaternion originalVisualRotation,
        Vector3 originalVisualLocalPosition,
        RigidbodyType2D originalBodyType,
        float originalGravityScale,
        RigidbodyInterpolation2D originalInterpolation,
        Vector2 originalVelocity,
        float originalAngularVelocity)
    {
        Vector2 destination = destinationPoint.position;
        PlayerRotaSurfaceController.SurfaceSide initialSurface = ResolveDestinationSurface();

        Tien.Minigame.MinigameCheckpointState.BeginMinigame(destination, initialSurface);
        SceneRuntimeStateManager.SaveCheckpoint(gameObject.scene.name, destination);
        SetPlayerPosition(actualRoot, rb, destination);

        GoalBlackHoleExit2D initialExit = destinationPoint.GetComponent<GoalBlackHoleExit2D>()
            ?? destinationPoint.GetComponentInChildren<GoalBlackHoleExit2D>()
            ?? destinationPoint.GetComponentInParent<GoalBlackHoleExit2D>();

        if (initialExit != null)
        {
            initialExit.PrepareForRelease();
        }

        if (visual != null)
        {
            visual.localScale = originalVisualScale;

            if (visual != actualRoot)
            {
                visual.localRotation = originalVisualRotation;
                visual.localPosition = originalVisualLocalPosition;
            }
        }

        if (rb != null)
        {
            rb.bodyType = originalBodyType;
            rb.gravityScale = originalGravityScale;
            rb.interpolation = originalInterpolation;
            rb.linearVelocity = resetVelocity
                ? Vector2.zero
                : originalVelocity;
            rb.angularVelocity = resetVelocity
                ? 0f
                : originalAngularVelocity;
        }

        Physics2D.SyncTransforms();

        if (player != null)
        {
            if (spine != null)
            {
                spine.SetFlipX(false);
            }

            if (RotaSurfaceZone.IsPlayerInAnyZone(player) || RotaSurfaceZone.IsPositionInAnyZone(destination))
            {
                player.SetRotaMode(true);

                PlayerRotaSurfaceController rotaController =
                    player.GetComponent<PlayerRotaSurfaceController>();
                if (rotaController != null)
                {
                    rotaController.AttachToSurface(initialSurface, false);
                    rotaController.ResetHorizontalMotionAfterTeleport();
                }
            }
            else
            {
                player.SetRotaMode(false);
            }

            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.UpdateCheckpoint(destination);
            }

            Unity.Cinemachine.CinemachineCamera cam = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (cam != null)
            {
                Vector3 camPos = cam.transform.position;
                camPos.x = destination.x;
                camPos.y = destination.y;
                cam.transform.position = camPos;
                cam.Follow = player.transform;
                cam.PreviousStateIsValid = false;

                if (RotaSurfaceZone.IsPlayerInAnyZone(player) || RotaSurfaceZone.IsPositionInAnyZone(destination))
                {
                    float targetAngle = PlayerRotaSurfaceController.GetSurfaceAngle(PlayerRotaSurfaceController.GetNormal(initialSurface));
                    cam.transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
                }
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 mainPos = mainCam.transform.position;
                mainPos.x = destination.x;
                mainPos.y = destination.y;
                mainCam.transform.position = mainPos;

                if (RotaSurfaceZone.IsPlayerInAnyZone(player) || RotaSurfaceZone.IsPositionInAnyZone(destination))
                {
                    float targetAngle = PlayerRotaSurfaceController.GetSurfaceAngle(PlayerRotaSurfaceController.GetNormal(initialSurface));
                    mainCam.transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
                }
            }

            player.SetControlLocked(false);
        }
    }

    private static void SetPlayerPosition(
        Transform playerRoot,
        Rigidbody2D rb,
        Vector2 position)
    {
        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        playerRoot.position = position;
    }

    private void ApplyPlayerShrink(
        Transform playerRoot,
        Transform visual,
        Vector3 originalScale,
        Quaternion originalRotation,
        Vector3 originalLocalPosition,
        float progress)
    {
        if (visual == null)
        {
            return;
        }

        if (visual != playerRoot)
        {
            visual.localRotation = originalRotation;
            visual.localPosition = originalLocalPosition;
        }

        float scale = Mathf.Lerp(
            1f,
            finalScale,
            Mathf.Pow(progress, 2f));
        visual.localScale = new Vector3(
            originalScale.x * scale,
            originalScale.y * scale,
            originalScale.z);
    }

    private bool TryGetPlayer(
        Collider2D other,
        out Transform playerTransform)
    {
        playerTransform = null;

        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag))
        {
            playerTransform =
                other.transform;

            return true;
        }

        if (other.attachedRigidbody != null &&
            other.attachedRigidbody.CompareTag(playerTag))
        {
            playerTransform =
                other.attachedRigidbody.transform;

            return true;
        }

        Transform root =
            other.transform.root;

        if (root != null &&
            root.CompareTag(playerTag))
        {
            playerTransform =
                root;

            return true;
        }

        return false;
    }

    private void PlaySuctionSound()
    {
        if (suctionClip == null)
        {
            return;
        }

        if (audioSource != null &&
            audioSource.isActiveAndEnabled)
        {
            audioSource.PlayOneShot(
                suctionClip,
                suctionVolume);

            return;
        }

        AudioSource.PlayClipAtPoint(
            suctionClip,
            transform.position,
            suctionVolume);
    }

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Transform center =
            blackHoleCenter != null
                ? blackHoleCenter
                : transform;

        Gizmos.DrawWireSphere(
            center.position,
            0.25f);

        if (destinationPoint != null)
        {
            Gizmos.DrawLine(
                center.position,
                destinationPoint.position);

            Gizmos.DrawWireSphere(
                destinationPoint.position,
                0.3f);
        }
    }

#endif
}

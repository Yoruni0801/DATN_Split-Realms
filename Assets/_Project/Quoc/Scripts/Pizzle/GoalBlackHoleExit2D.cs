using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
public enum GoalExitSurfaceMode
{
    None,
    Floor,
    LeftWall,
    Ceiling,
    RightWall
}

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(BlackHolePortalEffect2D))]
public sealed class GoalBlackHoleExit2D : MonoBehaviour
{
    [Header("Exit")]
    [SerializeField] private Transform releasePoint;

    [Header("Surface Override")]
    [SerializeField] private GoalExitSurfaceMode surfaceMode = GoalExitSurfaceMode.None;

    [Header("Cross-Scene Configuration")]
    [SerializeField] private string exitID;

    [Header("Spawn Settings")]
    [Tooltip("Tick chọn để cổng tự động thực hiện hoạt ảnh nhả người chơi lúc đầu màn (hoạt động như spawn point).")]
    [SerializeField] private bool playArrivalOnStart = false;

    [Header("Final Exit Settings")]
    [Tooltip("Tích chọn nếu đây là cổng Exit cuối cùng của minigame. Khi đi ra cổng này sẽ hoàn thành minigame.")]
    [SerializeField] private bool isFinalExit = false;

    private BlackHolePortalEffect2D portalEffect;
    private BlackHolePortalPhase phase;

    public bool IsFinalExit => isFinalExit;
    public BlackHolePortalPhase Phase => phase;
    public bool CanReleasePlayer => true;
    public BlackHolePortalEffect2D PortalEffect => portalEffect;
    public MapHangStageRoot2D StageRoot =>
        GetComponentInParent<MapHangStageRoot2D>(true);

    private void Awake()
    {
        portalEffect = GetComponent<BlackHolePortalEffect2D>();
        phase = BlackHolePortalPhase.Idle;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        if (portalEffect != null)
        {
            portalEffect.HidePortalImmediate();
        }
    }

    private void Start()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (portalEffect != null && phase != BlackHolePortalPhase.Releasing)
        {
            portalEffect.HidePortalImmediate();
        }

        if (BlackHoleCrossSceneData.IsPending && !string.IsNullOrEmpty(exitID) && BlackHoleCrossSceneData.TargetExitID == exitID)
        {
            BlackHoleCrossSceneData.IsPending = false;
            StartCoroutine(HandleCrossSceneArrivalRoutine());
        }
        else if (playArrivalOnStart)
        {
            StartCoroutine(HandleInitialSpawnRoutine());
        }
    }

    private void OnEnable()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (portalEffect != null && phase != BlackHolePortalPhase.Releasing)
        {
            portalEffect.HidePortalImmediate();
        }
        PlayerRespawnController.RespawnRequested += HandleRespawnRequested;
    }

    private void OnDisable()
    {
        PlayerRespawnController.RespawnRequested -= HandleRespawnRequested;
    }

    private void HandleRespawnRequested(PlayerRespawnController controller)
    {
        if (portalEffect != null)
        {
            portalEffect.HidePortalImmediate();
        }
    }

    public void PrepareForRelease()
    {
        phase = BlackHolePortalPhase.Releasing;
    }

    public void SetReleasingPhase()
    {
        phase = BlackHolePortalPhase.Releasing;
    }

    public PlayerRotaSurfaceController.SurfaceSide ResolveExitSurface()
    {
        switch (surfaceMode)
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
                Vector2 dir = transform.up;
                if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
                {
                    return dir.x < 0 
                        ? PlayerRotaSurfaceController.SurfaceSide.LeftWall
                        : PlayerRotaSurfaceController.SurfaceSide.RightWall;
                }
                else
                {
                    return dir.y < 0 
                        ? PlayerRotaSurfaceController.SurfaceSide.Ceiling
                        : PlayerRotaSurfaceController.SurfaceSide.Floor;
                }
        }
    }

    public void PlacePlayerAtRelease(BlackHolePortalPlayerContext playerContext)
    {
        Vector2 releasePosition = ResolveReleasePosition();
        portalEffect.TeleportPlayer(playerContext, releasePosition, false);

        Player player = playerContext != null && playerContext.Player != null
            ? playerContext.Player
            : playerContext != null && playerContext.Root != null
                ? playerContext.Root.GetComponent<Player>()
                : PlayerRegistry.Current;

        bool hasRotaZone = RotaSurfaceZone.IsPlayerInAnyZone(player)
            || RotaSurfaceZone.IsPositionInAnyZone(releasePosition)
            || RotaSurfaceZone.IsPositionInAnyZone(transform.position);

        if (!isFinalExit && hasRotaZone)
        {
            PlayerRotaSurfaceController.SurfaceSide surface = ResolveExitSurface();
            Tien.Minigame.MinigameCheckpointState.BeginMinigame(releasePosition, surface);
            Tien.Minigame.MinigameCheckpointState.SetCheckpoint(releasePosition, surface);
            ApplySurfaceOverride(playerContext);
        }
        else
        {
            Tien.Minigame.MinigameCheckpointState.ClearCheckpoint();
            if (player != null)
            {
                player.SetRotaMode(false);
                player.transform.rotation = Quaternion.identity;
            }
        }

        SceneRuntimeStateManager.SaveCheckpoint(gameObject.scene.name, releasePosition);

        PlayerHealth playerHealth = player != null
            ? player.GetComponent<PlayerHealth>()
            : playerContext != null && playerContext.Root != null
                ? playerContext.Root.GetComponent<PlayerHealth>()
                : null;

        if (playerHealth != null)
        {
            playerHealth.UpdateCheckpoint(releasePosition);
        }
    }

    public void ReleasePlayerImmediately(BlackHolePortalPlayerContext playerContext)
    {
        Player player = playerContext != null && playerContext.Player != null
            ? playerContext.Player
            : playerContext != null && playerContext.Root != null
                ? playerContext.Root.GetComponent<Player>()
                : PlayerRegistry.Current;

        Vector2 releasePosition = ResolveReleasePosition();
        bool hasRotaZone = RotaSurfaceZone.IsPlayerInAnyZone(player)
            || RotaSurfaceZone.IsPositionInAnyZone(releasePosition)
            || RotaSurfaceZone.IsPositionInAnyZone(transform.position);

        if (isFinalExit || !hasRotaZone)
        {
            Tien.Minigame.MinigameCheckpointState.ClearCheckpoint();

            if (isFinalExit)
            {
                MapHangMinigameManager stageManager =
                    GetComponentInParent<MapHangMinigameManager>(true)
                    ?? FindFirstObjectByType<MapHangMinigameManager>();
                if (stageManager != null)
                {
                    stageManager.EndMinigame();
                }
            }

            if (player != null)
            {
                player.SetRotaMode(false);
                player.transform.rotation = Quaternion.identity;

                PlayerSceneScaleController scaleCtrl = player.GetComponent<PlayerSceneScaleController>();
                if (scaleCtrl != null)
                {
                    scaleCtrl.ApplyScaleForActiveScene();
                }
            }

            CinemachineCamera cam = FindFirstObjectByType<CinemachineCamera>();
            if (cam != null)
            {
                cam.transform.rotation = Quaternion.identity;
                cam.PreviousStateIsValid = false;
                Transform root = ResolvePlayerRoot(playerContext);
                if (root != null && cam.Follow == null)
                {
                    cam.Follow = root;
                }
            }
        }
        else
        {
            if (player != null)
            {
                player.SetRotaMode(true);
                ApplySurfaceOverride(playerContext);
            }
        }

        RestorePlayerPhysics(playerContext);

        if (player != null)
        {
            player.SetInputActive(true);
            player.SetControlLocked(false);
        }

        if (portalEffect != null && playerContext != null)
        {
            portalEffect.UnlockPlayer(playerContext);
        }
    }

    public IEnumerator CompleteArrival(BlackHolePortalPlayerContext playerContext, float disappearDuration = -1f)
    {
        // Giải phóng và mở khóa player ngay khi bắt đầu CompleteArrival
        // để player có thể điều khiển di chuyển mượt mà trong khi animation đóng cổng diễn ra.
        ReleasePlayerImmediately(playerContext);

        // For final exits we want a visible gate closing animation even if caller passed -1
        if (isFinalExit && disappearDuration < 0f)
        {
            disappearDuration = 0.7f;
        }

        if (portalEffect != null)
        {
            if (disappearDuration > 0f)
            {
                yield return portalEffect.HidePortalWithAnimation(disappearDuration);
            }
            else
            {
                yield return portalEffect.HidePortal();
            }
        }

        phase = BlackHolePortalPhase.Consumed;
    }

    /// <summary>
    /// Khôi phục Rigidbody2D về trạng thái ban đầu đã lưu trong playerContext.
    /// LockPlayer set bodyType=Kinematic, gravity=0, interpolation=None.
    /// Method này trả lại các giá trị gốc để player hoạt động bình thường.
    /// </summary>
    public void RestorePlayerPhysics(BlackHolePortalPlayerContext context)
    {
        if (context == null || context.Rigidbody == null)
        {
            return;
        }

        Rigidbody2D rb = context.Rigidbody;
        Player player = context.Player != null
            ? context.Player
            : (context.Root != null ? context.Root.GetComponent<Player>() : PlayerRegistry.Current);

        if (isFinalExit)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            if (player != null && player.Mover != null)
            {
                player.Mover.RestoreGravity();
                player.Mover.FreezeRotation();
            }
            else
            {
                rb.gravityScale = context.GravityScale != 0f ? context.GravityScale : 1f;
            }
        }
        else
        {
            // Trong chế độ Rota, PlayerRotaSurfaceController tự quản lý trọng lực (GravityDirection)
            // nên rb.gravityScale PHẢI BẰNG 0. Nếu > 0, trọng lực Unity sẽ kéo player rơi tự do theo trục Y.
            bool isInRota = player != null && player.IsRotaModeActive;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.gravityScale = isInRota ? 0f : (context.GravityScale != 0f ? context.GravityScale : 1f);
        }

        rb.interpolation = context.Interpolation != RigidbodyInterpolation2D.None
            ? context.Interpolation
            : RigidbodyInterpolation2D.Interpolate;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        Physics2D.SyncTransforms();
    }

    private Transform ResolvePlayerRoot(BlackHolePortalPlayerContext playerContext)
    {
        if (playerContext == null)
            return null;

        // Prefer explicit player reference
        if (playerContext.Player != null)
            return playerContext.Player.transform;

        // Fallback to root object if it contains a Player component
        if (playerContext.Root != null)
        {
            var playerComp = playerContext.Root.GetComponent<Player>();
            if (playerComp != null)
                return playerComp.transform;
        }

        // Last resort: use the globally registered player
        var current = PlayerRegistry.Current;
        return current != null ? current.transform : null;
    }

    public void ApplySurfaceOverride(BlackHolePortalPlayerContext playerContext)
    {
        Player player = playerContext != null && playerContext.Player != null
            ? playerContext.Player
            : playerContext != null && playerContext.Root != null
                ? playerContext.Root.GetComponent<Player>()
                : PlayerRegistry.Current;

        PlayerRotaSurfaceController rotaController = player != null
            ? player.GetComponent<PlayerRotaSurfaceController>()
            : null;

        if (rotaController == null)
        {
            return;
        }

        if (!RotaSurfaceZone.IsPlayerInAnyZone(player) && !RotaSurfaceZone.IsPositionInAnyZone(transform.position))
        {
            return;
        }

        PlayerRotaSurfaceController.SurfaceSide surface = ResolveExitSurface();
        rotaController.AttachToSurface(surface, false);
        rotaController.ResetHorizontalMotionAfterTeleport();
    }

    public Vector2 ResolveReleasePosition()
    {
        Vector2 rawPosition = releasePoint != null
            ? (Vector2)releasePoint.position
            : (portalEffect != null ? portalEffect.PortalCenterPosition : (Vector2)transform.position);

        if (isFinalExit)
        {
            // Bắn raycast xuống tìm Ground Layer để đảm bảo player không bao giờ bị rơi tự do hoặc lơ lửng ngoài map
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(PlayerLayerConfig.GroundLayer);
            filter.useLayerMask = true;
            filter.useTriggers = false;

            RaycastHit2D[] hits = new RaycastHit2D[4];
            int hitCount = Physics2D.Raycast(rawPosition, Vector2.down, filter, hits, 5f);
            if (hitCount > 0)
            {
                // Thả player ngay trên bề mặt ground khoảng 0.5f (chiều cao an toàn)
                return hits[0].point + Vector2.up * 0.5f;
            }
        }

        return rawPosition;
    }

    private IEnumerator HandleCrossSceneArrivalRoutine()
    {
        yield return null;

        BlackHolePortalPlayerContext context = BlackHoleCrossSceneData.OriginalPlayerContext;
        BlackHoleCrossSceneData.OriginalPlayerContext = null;

        if (context == null || context.Player == null)
        {
            Player player = PlayerRegistry.Current;
            if (player == null)
            {
                yield break;
            }
            context = CreateContextFromPlayer(player);
            if (context == null) yield break;
        }

        MapHangTransitionFade2D transitionFade = MapHangTransitionFade2D.GetOrCreate();
        
        SetReleasingPhase();

        if (transitionFade != null)
        {
            MapHangArrivalConfig config = BlackHoleCrossSceneData.ArrivalConfig;
            if (config.camera == null) config.camera = FindFirstObjectByType<CinemachineCamera>();

            transitionFade.BeginMapArrival(
                this,
                context,
                null,
                BlackHoleCrossSceneData.BlackHoldDuration,
                BlackHoleCrossSceneData.FadeToClearDuration,
                config);
        }
        else
        {
            PlacePlayerAtRelease(context);
            yield return CompleteArrival(context);
        }
    }

    private IEnumerator HandleInitialSpawnRoutine()
    {
        yield return null;

        Player player = PlayerRegistry.Current;
        if (player == null) yield break;

        BlackHolePortalPlayerContext context = CreateContextFromPlayer(player);
        if (context == null) yield break;

        MapHangTransitionFade2D transitionFade = MapHangTransitionFade2D.GetOrCreate();
        SetReleasingPhase();

        if (transitionFade != null)
        {
            MapHangArrivalConfig config = new MapHangArrivalConfig
            {
                camera = FindFirstObjectByType<CinemachineCamera>(),
                cameraDuration = 0f,
                preGateDelay = 0.2f,
                gateAppearDuration = 0.7f,
                gateStayDuration = 0.4f,
                playerFadeDelay = 0f,
                playerFadeDuration = 0.6f,
                postPlayerDelay = 0.4f,
                gateDisappearDuration = 0.6f
            };

            transitionFade.BeginMapArrival(
                this,
                context,
                null,
                0f, 
                0f, 
                config);
        }
        else
        {
            PlacePlayerAtRelease(context);
            yield return CompleteArrival(context);
        }
    }

    private BlackHolePortalPlayerContext CreateContextFromPlayer(Player player)
    {
        if (player == null) return null;
        Transform playerRoot = player.transform;
        Rigidbody2D rb = playerRoot.GetComponent<Rigidbody2D>();
        
        AnimationSpine spine = player.GetComponentInChildren<AnimationSpine>();
        bool hasSpineScaleX = spine != null && spine.Skeleton != null;

        return new BlackHolePortalPlayerContext
        {
            Root = playerRoot,
            Player = player,
            Rigidbody = rb,
            Visual = playerRoot,
            VisualScale = playerRoot.localScale,
            VisualRotation = playerRoot.localRotation,
            VisualPosition = playerRoot.localPosition,
            GravityScale = rb != null ? rb.gravityScale : 0f,
            Velocity = rb != null ? rb.linearVelocity : Vector2.zero,
            AngularVelocity = rb != null ? rb.angularVelocity : 0f,
            BodyType = rb != null ? rb.bodyType : RigidbodyType2D.Dynamic,
            Interpolation = rb != null ? rb.interpolation : RigidbodyInterpolation2D.None,
            Spine = spine,
            SpineScaleX = hasSpineScaleX ? spine.Skeleton.ScaleX : 1f,
            HasSpineScaleX = hasSpineScaleX
        };
    }

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Vector2 target = ResolveReleasePosition();
        Gizmos.DrawLine(transform.position, target);
        Gizmos.DrawWireSphere(target, 0.3f);
    }

#endif
}

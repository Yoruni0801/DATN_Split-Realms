using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class FairyPlayerCompanionController : MonoBehaviour
{
    private const int WallSafetyColliderBufferSize = 8;

    [Header("Player Synchronization")]
    private Player player;
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private bool useTransitionOnElementChange = true;

    [Header("Fairy Element")]
    [SerializeField] private FairyElement fairyElement = FairyElement.Water;

    private FairyElementVfxController elementController;
    private Vector3 followOffset = new Vector3(-1.2f, 1.4f, 0f);
    private bool offsetUsesPlayerFacing = true;
    private float followSmoothTime = 0.08f;
    private float maxFollowSpeed = 55f;
    private float snapDistance = 8f;

    [Header("Companion Formation & Spacing")]
    [SerializeField] private float companionSlotSpacingX = 0.85f;
    [SerializeField] private float companionSlotSpacingY = 0.65f;
    [SerializeField] private float minFairySeparationDistance = 0.75f;
    [SerializeField] private float separationStrength = 0.45f;

    [Header("Wall Safety")]
    [SerializeField] private float wallSafetyRadius = 0.15f;
    [SerializeField] private float wallSafetySearchRadius = 1.5f;
    [SerializeField] private float wallCheckHeightOffset = 0.5f;

    [Header("Possession")]
    [SerializeField] private float possessedMoveSpeed = 22f;
    private bool isPossessed;
    private bool isDead;
    private Vector2 possessedMoveInput;

    [Header("Fairy Follow")]
    [SerializeField] private bool followOtherFairyWhenUnpossessed = true;
    [SerializeField] private float followFairySmoothTime = 0.15f;
    [SerializeField] private float followFairyMaxSpeed = 35f;
    [SerializeField] private float followFairyOffset = 1.5f;
    private FairyPlayerCompanionController _followTargetFairy;
    private bool _isFollowingFairy;

    [Header("Recall")]
    [SerializeField] private float recallMoveSpeed = 20f;
    [SerializeField] private float recallArrivalDistance = 1.0f;
    private bool isRecalling;
    [Header("3D Facing")]
    private Transform visualRoot;
    private bool matchPlayerFacing = true;
    private float leftFacingRotationY = 40f;
    private float rightFacingRotationY = 120f;
    private float leftFacingScaleZ = -1f;
    private float rightFacingScaleZ = 1f;
    private bool scaleWithPlayer = true;

    private Player activePlayer;
    private Player subscribedPlayer;
    private PlayerRotaSurfaceController activeRotaController;
    private Rigidbody2D activePlayerRb;
    private bool lastRotaFacingRight = true;
    private Vector3 followVelocity;
    private Vector3 baseVisualScale = Vector3.one;
    private bool hasBaseVisualScale;
    private Vector3 baseVisualEulerAngles;
    private bool hasBaseVisualEulerAngles;
    private bool isCastingLocked;

    private Vector3 lastClearPosition;
    private bool hasLastClearPosition;
    private Vector3 wallCheckHeightVector;
    private float cachedWallCheckHeightOffset = float.NaN;
    private CancellationTokenSource lifetimeCts;
    private static readonly List<FairyPlayerCompanionController> ActiveFairies = new List<FairyPlayerCompanionController>();
    private static readonly Collider2D[] WallSafetyColliderBuffer = new Collider2D[WallSafetyColliderBufferSize];

    private static readonly Vector2[] SearchDirections =
    {
        new Vector2( 1f,  0f),   // right
        new Vector2(-1f,  0f),   // left
        new Vector2( 0f,  1f),   // up
        new Vector2( 1f,  1f).normalized,   // up-right
        new Vector2(-1f,  1f).normalized,   // up-left
        new Vector2( 1f, -1f).normalized,   // down-right
        new Vector2(-1f, -1f).normalized,   // down-left
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveFairies.Clear();
    }

    public static IReadOnlyList<FairyPlayerCompanionController> AllActiveFairies => ActiveFairies;
    public static event Action OnActiveFairiesChanged;
    public Player ActivePlayer => activePlayer;
    public bool AutoFindPlayer
    {
        get => autoFindPlayer;
        set
        {
            autoFindPlayer = value;
            if (value)
            {
                ResolveReferences();
            }
        }
    }

    public void SetAutoFindPlayer(bool enabled)
    {
        AutoFindPlayer = enabled;
    }

    public FairyElement CurrentElement { get; private set; }
    public event Action<FairyElement> OnFairyElementChanged;
    public event Action OnRecallCompleted;
    public event Action OnFairyDied;

    // Lever interaction
    private LeverInteractable currentLever;
    public bool IsInteractingWithLever => currentLever != null && currentLever.IsBusy;
    public void SetCurrentLever(LeverInteractable lever) => currentLever = lever;

    private Rigidbody2D rb;
    private CircleCollider2D circleCollider;
    private Animator cachedAnimator;
    private SpriteRenderer[] cachedRenderers;

    private void Reset()
    {
        elementController = GetComponent<FairyElementVfxController>();
        visualRoot = transform;
    }

    private void Awake()
    {
        lifetimeCts = new CancellationTokenSource();
        RemoveDestroyedFairies();

        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        cachedAnimator = GetComponentInChildren<Animator>();
        cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        ResolveReferences();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Kinematic;

        circleCollider = GetComponent<CircleCollider2D>();
        if (circleCollider == null)
        {
            circleCollider = gameObject.AddComponent<CircleCollider2D>();
            circleCollider.radius = wallSafetyRadius;
        }
        circleCollider.enabled = true;
    }

    private FairyElementUnlockController subscribedUnlockController;

    private void OnEnable()
    {
        RegisterFairy();
        ResolveReferences();
        SubscribeToUnlockController();
        SetElement(fairyElement, false);
        SetCastingLocked(false);
        SnapToFollowTarget();
    }

    private void OnDisable()
    {
        UnregisterFairy();
        UnsubscribeFromPlayer();
        UnsubscribeFromUnlockController();
    }

    private void OnDestroy()
    {
        lifetimeCts?.Cancel();
        lifetimeCts?.Dispose();
        lifetimeCts = null;

        if (ActiveFairies.Remove(this))
        {
            OnActiveFairiesChanged?.Invoke();
        }
        RemoveDestroyedFairies();
        UnsubscribeFromPlayer();
        UnsubscribeFromUnlockController();
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        if (isPossessed && !isCastingLocked)
        {
            HandlePossessedMovement();
        }
    }

    private void LateUpdate()
    {
        if (isDead) return;

        if (activePlayer == null)
        {
            ResolveReferences();
        }

        UpdateRotaFacing();
        if (!isCastingLocked)
        {
            if (!isPossessed)
            {
                FollowPlayer();
                FacePlayerDirection();
            }
        }
    }

    public bool IsPossessed => isPossessed;

    public void SetPossessed(bool possessed)
    {
        isPossessed = possessed;
        if (!possessed)
        {
            possessedMoveInput = Vector2.zero;
            followVelocity = Vector3.zero;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }
        else
        {
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }
        }
    }

    public void TeleportToPlayer()
    {
        SetCastingLocked(false);
        if (activePlayer == null) return;
        Vector3 targetPos = GetDesiredFollowPosition(activePlayer.transform);
        transform.position = targetPos;
        followVelocity = Vector3.zero;
        lastClearPosition = targetPos;
        hasLastClearPosition = true;
        isRecalling = false;
        SetFollowOtherFairyEnabled(true);
    }

    public void BeginRecall()
    {
        isRecalling = true;
        followVelocity = Vector3.zero;
    }

    /// <summary>
    /// Xử lý khi tinh linh bị chết (vd: chạm luồng lửa FireSpray).
    /// Chơi animation Die, trả quyền điều khiển về Player và đưa tinh linh về Checkpoint.
    /// </summary>
    public void Die()
    {
        if (!gameObject.activeInHierarchy) return;
        _ = DieAndRespawnAsync(null, lifetimeCts != null ? lifetimeCts.Token : default);
    }

    public void DieWithCustomRespawn(Vector3 respawnPoint)
    {
        if (!gameObject.activeInHierarchy) return;
        _ = DieAndRespawnAsync(respawnPoint, lifetimeCts != null ? lifetimeCts.Token : default);
    }

    private async Task DieAndRespawnAsync(Vector3? customRespawnPos, CancellationToken ct)
    {
        if (isDead) return;
        isDead = true;

        OnFairyDied?.Invoke();

        // 1. Chơi animation Die
        if (cachedAnimator != null)
        {
            cachedAnimator.CrossFadeInFixedTime("Die", 0.05f, 0, 0f);
        }

        // Tạm khóa điều khiển/di chuyển
        bool wasPossessed = isPossessed;
        SetPossessed(false);

        // 2. Tìm vị trí hồi sinh ưu tiên customRespawnPos
        Vector3 respawnPos = transform.position;
        if (customRespawnPos.HasValue)
        {
            respawnPos = customRespawnPos.Value;
        }
        else
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (SceneRuntimeStateManager.TryGetCheckpoint(sceneName, out Vector3 savedCheckpoint))
            {
                respawnPos = savedCheckpoint;
            }
            else if (activePlayer != null)
            {
                respawnPos = activePlayer.transform.position;
            }
        }

        if (this == null) return;

        // Dịch chuyển NGAY LẬP TỨC về Checkpoint
        transform.position = respawnPos;
        followVelocity = Vector3.zero;
        lastClearPosition = respawnPos;
        hasLastClearPosition = true;

        // Trả quyền điều khiển về Player nếu KHÔNG dùng customRespawnPos
        if (wasPossessed)
        {
            if (!customRespawnPos.HasValue)
            {
                FairyPossessionController.ReturnAllToPlayer();
            }
            else
            {
                SetPossessed(true);
            }
        }

        // 3. Hiệu ứng chớp tắt (blink) TẠI ĐIỂM HỒI SINH
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        float elapsed = 0f;
        while (elapsed < 1.2f)
        {
            if (ct.IsCancellationRequested || this == null) return;

            float alpha = Mathf.PingPong(elapsed * 10f, 1f) > 0.5f ? 1f : 0.2f;
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null)
                {
                    Color c = cachedRenderers[i].color;
                    c.a = alpha;
                    cachedRenderers[i].color = c;
                }
            }

            elapsed += Time.deltaTime;
            try
            {
                await Task.Yield();
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        if (this == null) return;

        // Khôi phục Alpha = 1
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                Color c = cachedRenderers[i].color;
                c.a = 1f;
                cachedRenderers[i].color = c;
            }
        }

        // 4. Trả lại animation Idle và bật lại Element VFX
        if (cachedAnimator != null)
        {
            cachedAnimator.CrossFadeInFixedTime("Fly Forward In Place", 0.1f, 0, 0f);
        }
        if (elementController != null)
        {
            elementController.gameObject.SetActive(true);
        }

        isDead = false;
    }

    public void SetPossessedMoveInput(Vector2 input)
    {
        if (isPossessed)
        {
            possessedMoveInput = Vector2.ClampMagnitude(input, 1f);
        }
    }

    private void HandlePossessedMovement()
    {
        if (isCastingLocked)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        if (rb != null)
        {
            rb.linearVelocity = possessedMoveInput * possessedMoveSpeed;
        }
        else
        {
            if (possessedMoveInput.sqrMagnitude > 0.01f)
            {
                Vector3 movement = new Vector3(possessedMoveInput.x, possessedMoveInput.y, 0f) * (possessedMoveSpeed * Time.deltaTime);
                transform.position += movement;
            }
        }

        if (possessedMoveInput.sqrMagnitude > 0.01f)
        {
            FaceDirection(possessedMoveInput);
        }
    }

    public void SetCastingLocked(bool locked)
    {
        isCastingLocked = locked;
        if (locked)
        {
            followVelocity = Vector3.zero;
        }
    }

    public void FaceDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        FaceVisual(direction.x >= 0f);
    }

    public void CompletePendingElementTransition()
    {
        ResolveReferences();
        if (elementController == null)
        {
            return;
        }

        CurrentElement = fairyElement;
        OnFairyElementChanged?.Invoke(fairyElement);
        elementController.SetElementImmediate(fairyElement);
    }

    private void OnValidate()
    {
        followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
        maxFollowSpeed = Mathf.Max(0.01f, maxFollowSpeed);
        snapDistance = Mathf.Max(0f, snapDistance);

        if (elementController == null)
        {
            elementController = GetComponent<FairyElementVfxController>();
        }

        if (elementController != null)
        {
            elementController.SetElementImmediate(fairyElement);
        }

        if (circleCollider != null)
        {
            circleCollider.radius = wallSafetyRadius;
        }

        CaptureBaseVisualTransformIfNeeded();
    }

    private void ResolveReferences()
    {
        if (elementController == null)
        {
            elementController = GetComponent<FairyElementVfxController>();
        }

        activePlayer = player != null
            ? player
            : autoFindPlayer
                ? (PlayerRegistry.Current != null ? PlayerRegistry.Current : FindFirstObjectByType<Player>())
                : null;

        if (activePlayer != null)
        {
            if (activeRotaController == null || activeRotaController.gameObject != activePlayer.gameObject)
            {
                activePlayer.TryGetComponent(out activeRotaController);
                activePlayer.TryGetComponent(out activePlayerRb);
            }
        }
        else
        {
            activeRotaController = null;
            activePlayerRb = null;
        }

        if (activePlayer != subscribedPlayer)
        {
            SubscribeToPlayer(activePlayer);
        }
    }

    private bool IsRotaActive()
    {
        if (activePlayer == null)
        {
            return false;
        }

        if (activePlayer.IsRotaModeActive)
        {
            return true;
        }

        if (activeRotaController != null && activeRotaController.IsActive)
        {
            return true;
        }

        return false;
    }

    private bool IsPlayerGravityInverted()
    {
        if (IsRotaActive())
        {
            return false;
        }

        if (activePlayer == null)
        {
            return false;
        }

        if (activePlayer.IsGravityInverted)
        {
            return true;
        }

        if (activePlayerRb != null && activePlayerRb.gravityScale < 0f)
        {
            return true;
        }

        if (activePlayer.transform.localScale.y < 0f)
        {
            return true;
        }

        return false;
    }

    private void UpdateRotaFacing()
    {
        if (!IsRotaActive() || activePlayer == null)
        {
            return;
        }

        if (activePlayerRb != null)
        {
            Vector2 velocity = activePlayerRb.linearVelocity;
            Vector2 surfaceTangent = activePlayer.transform.right;
            float tangentSpeed = Vector2.Dot(velocity, surfaceTangent);
            const float moveThreshold = 0.05f;
            if (tangentSpeed > moveThreshold)
            {
                lastRotaFacingRight = true;
            }
            else if (tangentSpeed < -moveThreshold)
            {
                lastRotaFacingRight = false;
            }
            else if (!Mathf.Approximately(tangentSpeed, 0f))
            {
                lastRotaFacingRight = activePlayer.FacingDirectionSign >= 0f;
            }
        }
    }

    private void SubscribeToUnlockController()
    {
        FairyElementUnlockController targetController = ResolveUnlockController();
        if (targetController == subscribedUnlockController)
        {
            return;
        }

        UnsubscribeFromUnlockController();
        subscribedUnlockController = targetController;
        if (subscribedUnlockController != null)
        {
            subscribedUnlockController.ElementUnlocked += HandleElementUnlocked;
        }
    }

    private void UnsubscribeFromUnlockController()
    {
        if (subscribedUnlockController != null)
        {
            subscribedUnlockController.ElementUnlocked -= HandleElementUnlocked;
            subscribedUnlockController = null;
        }
    }

    private FairyElementUnlockController ResolveUnlockController()
    {
        if (TryGetComponent(out FairyElementUnlockController fairyUnlockController))
        {
            return fairyUnlockController;
        }

        if (activePlayer != null && activePlayer.TryGetComponent(out FairyElementUnlockController playerUnlockController))
        {
            return playerUnlockController;
        }

        return FindFirstObjectByType<FairyElementUnlockController>();
    }

    public void SetElement(FairyElement element, bool updatePlayer = false)
    {
        CurrentElement = element;
        if (updatePlayer && activePlayer != null)
        {
            activePlayer.TryChangeElement(MapFairyElement(element));
        }

        OnFairyElementChanged?.Invoke(element);
        if (elementController != null)
        {
            if (useTransitionOnElementChange)
            {
                elementController.SetElement(element);
            }
            else
            {
                elementController.SetElementImmediate(element);
            }
        }
    }

    private void HandleElementUnlocked(SkillElementType unlockedElement)
    {
        // Giữ nguyên hệ cố định của Tinh Linh theo fairyElement
    }

    private static FairyElement MapSkillElementToFairy(SkillElementType elementType)
    {
        switch (elementType)
        {
            case SkillElementType.Fire:
                return FairyElement.Fire;
            case SkillElementType.Earth:
                return FairyElement.Earth;
            case SkillElementType.Wind:
                return FairyElement.Wind;
            case SkillElementType.Water:
            default:
                return FairyElement.Water;
        }
    }

    private void SubscribeToPlayer(Player nextPlayer)
    {
        UnsubscribeFromPlayer();
        subscribedPlayer = nextPlayer;
        if (subscribedPlayer == null)
        {
            return;
        }

        subscribedPlayer.OnElementChanged += HandlePlayerElementChanged;
    }

    private void UnsubscribeFromPlayer()
    {
        if (subscribedPlayer != null)
        {
            subscribedPlayer.OnElementChanged -= HandlePlayerElementChanged;
            subscribedPlayer = null;
        }
    }

    private void HandlePlayerElementChanged(SkillSetSO skillSet, int elementIndex, int elementCount)
    {
        // Tinh linh có hệ cố định riêng (Fire, Wind, Earth, ...) thì KHÔNG bao giờ bị Player ghi đè hệ
        if (fairyElement != FairyElement.Water)
        {
            return;
        }

        // Khi có từ 2 tinh linh trở lên trong scene, không đồng bộ hệ theo Player
        if (ActiveFairies.Count >= 2)
        {
            return;
        }

        if (TryResolveSkillElement(skillSet, out SkillElementType elementType))
        {
            SetElement(MapSkillElementToFairy(elementType), false);
        }
        else
        {
            SetElement((FairyElement)Mathf.Clamp(elementIndex, 0, 3), false);
        }
    }

    private static SkillElementType MapFairyElement(FairyElement fairyElement)
    {
        switch (fairyElement)
        {
            case FairyElement.Fire:
                return SkillElementType.Fire;
            case FairyElement.Earth:
                return SkillElementType.Earth;
            case FairyElement.Wind:
                return SkillElementType.Wind;
            case FairyElement.Water:
            default:
                return SkillElementType.Water;
        }
    }

    private static bool TryResolveSkillElement(SkillSetSO skillSet, out SkillElementType elementType)
    {
        elementType = SkillElementType.Water;
        if (skillSet == null)
        {
            return false;
        }

        for (int i = 0; i < skillSet.SkillCount; i++)
        {
            if (!skillSet.TryGetSkill(i, out SkillData skill) || skill == null)
            {
                continue;
            }

            elementType = skill.ElementType;
            return true;
        }

        return false;
    }

    private void FollowPlayer()
    {
        Transform target = ResolveFollowTarget();
        if (target == null)
        {
            return;
        }

        float radius = Mathf.Max(0.01f, wallSafetyRadius);
        LayerMask mask = PlayerLayerConfig.GroundLayer;

        bool currentlyStuck = !IsCircleClear(transform.position, radius, mask);
        if (currentlyStuck)
        {
            float searchRange = Mathf.Max(0f, wallSafetySearchRadius);
            if (TryFindClearPosition(transform.position, radius, searchRange, mask, out Vector3 escapePos))
            {
                transform.position = escapePos;
                followVelocity = Vector3.zero;
                lastClearPosition = escapePos;
                hasLastClearPosition = true;
                currentlyStuck = false;
            }
        }

        if (!currentlyStuck)
        {
            lastClearPosition = transform.position;
            hasLastClearPosition = true;
        }

        Vector3 targetPosition = GetDesiredFollowPosition(target);

        // Soft separation: Đẩy nhẹ các Tinh Linh nếu đang bay quá gần nhau
        if (ActiveFairies.Count > 1 && minFairySeparationDistance > 0f && separationStrength > 0f)
        {
            Vector3 separationOffset = Vector3.zero;
            for (int i = 0; i < ActiveFairies.Count; i++)
            {
                var other = ActiveFairies[i];
                if (other != null && other != this && !other.isDead)
                {
                    Vector3 diff = transform.position - other.transform.position;
                    diff.z = 0f;
                    float dist = diff.magnitude;
                    if (dist > 0.0001f && dist < minFairySeparationDistance)
                    {
                        float pushStrength = (1f - (dist / minFairySeparationDistance)) * separationStrength;
                        separationOffset += (diff / dist) * pushStrength;
                    }
                }
            }
            targetPosition += separationOffset;
        }

        float distance = Vector3.Distance(transform.position, targetPosition);

        if (isRecalling)
        {
            if (activePlayer != null)
            {
                Vector3 targetPos = GetDesiredFollowPosition(activePlayer.transform);
                Vector3 dir = (targetPos - transform.position).normalized;
                transform.position += dir * recallMoveSpeed * Time.deltaTime;
                FaceDirection(dir);

                float distToPlayer = Vector3.Distance(transform.position, targetPos);
                if (distToPlayer <= recallArrivalDistance)
                {
                    isRecalling = false;
                    OnRecallCompleted?.Invoke();
                }
            }
            return;
        }

        if (snapDistance > 0f && distance >= snapDistance)
        {
            transform.position = targetPosition;
            followVelocity = Vector3.zero;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref followVelocity,
            followSmoothTime,
            maxFollowSpeed);
    }

    private void SnapToFollowTarget()
    {
        Transform target = ResolveFollowTarget();
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = GetDesiredFollowPosition(target);
        transform.position = targetPosition;
        lastClearPosition = targetPosition;
        hasLastClearPosition = true;
        followVelocity = Vector3.zero;
    }

    public bool IsFollowingFairy => _isFollowingFairy;

    public bool IsFacingRight
    {
        get
        {
            Transform targetVisual = visualRoot != null ? visualRoot : transform;
            float midpoint = (leftFacingRotationY + rightFacingRotationY) * 0.5f;
            return targetVisual.localEulerAngles.y >= midpoint;
        }
    }

    public float FacingDirectionSign => IsFacingRight ? 1f : -1f;

    /// <summary>
    /// Bật/Tắt tính năng tinh linh tự động đi theo nhau khi một con được điều khiển.
    /// </summary>
    public void SetFollowOtherFairyEnabled(bool enabled)
    {
        followOtherFairyWhenUnpossessed = enabled;
        if (!enabled)
        {
            StopFollowingFairy();
        }
    }

    public void StopFollowingFairy()
    {
        _isFollowingFairy = false;
        _followTargetFairy = null;
        followVelocity = Vector3.zero;
    }
    
    private Transform ResolveFollowTarget()
    {
        // Nếu bản thân tinh linh này không có FairyPossessionController (tinh linh bám theo Player bình thường),
        // luôn luôn đi theo Player, không bị cuốn theo trạng thái Possession của minigame.
        if (GetComponent<FairyPossessionController>() == null)
        {
            _isFollowingFairy = false;
            _followTargetFairy = null;
            return activePlayer != null ? activePlayer.transform : null;
        }

        if (FairyPossessionController.IsPossessionActive)
        {
            if (followOtherFairyWhenUnpossessed)
            {
                foreach (var fairy in ActiveFairies)
                {
                    if (fairy != null && fairy != this && fairy.IsPossessed && fairy.GetComponent<FairyPossessionController>() != null)
                    {
                        _followTargetFairy = fairy;
                        _isFollowingFairy = true;
                        return fairy.transform;
                    }
                }
            }

            bool anyPossessed = false;
            foreach (var fairy in ActiveFairies)
            {
                if (fairy != null && fairy.IsPossessed && fairy.GetComponent<FairyPossessionController>() != null)
                {
                    anyPossessed = true;
                    break;
                }
            }

            if (!anyPossessed)
            {
                _isFollowingFairy = false;
                _followTargetFairy = null;
                return activePlayer != null ? activePlayer.transform : null;
            }

            _isFollowingFairy = false;
            _followTargetFairy = null;
            return null;
        }

        _isFollowingFairy = false;
        _followTargetFairy = null;
        return activePlayer != null ? activePlayer.transform : null;
    }

    private Vector3 GetDesiredFollowPosition(Transform target)
    {
        float radius     = Mathf.Max(0.01f, wallSafetyRadius);
        float searchRange = Mathf.Max(0f, wallSafetySearchRadius);
        LayerMask mask   = PlayerLayerConfig.GroundLayer;

        float currentFacing;
        if (IsRotaActive())
        {
            currentFacing = lastRotaFacingRight ? 1f : -1f;
        }
        else
        {
            currentFacing = activePlayer != null ? activePlayer.FacingDirectionSign : 1f;
        }

        Vector3 offset = followOffset;
        if (scaleWithPlayer && activePlayer != null && activePlayer.ScaleProvider != null)
        {
            offset = activePlayer.ScaleProvider.ScaleOffset(offset);
        }

        float gravityYSign = IsPlayerGravityInverted() ? -1f : 1f;
        float signedOffsetX = offsetUsesPlayerFacing ? offset.x * currentFacing : offset.x;
        Vector3 companionSlotOffset = GetCompanionSlotOffset();

        Vector3 localFollowOffset = new Vector3(
            signedOffsetX + companionSlotOffset.x,
            (offset.y + companionSlotOffset.y) * gravityYSign,
            0f);

        Vector3 worldFollowOffset = target != null ? target.TransformDirection(localFollowOffset) : localFollowOffset;
        Vector3 desired = new Vector3(
            target.position.x + worldFollowOffset.x,
            target.position.y + worldFollowOffset.y,
            transform.position.z);

        if (TryFindClearPosition(desired, radius, searchRange, mask, out Vector3 bestCandidate))
        {
            return bestCandidate;
        }

        followVelocity = Vector3.zero;
        return hasLastClearPosition ? lastClearPosition : transform.position;
    }

    private void RegisterFairy()
    {
        RemoveDestroyedFairies();
        if (!ActiveFairies.Contains(this))
        {
            ActiveFairies.Add(this);
            OnActiveFairiesChanged?.Invoke();
        }
    }

    private void UnregisterFairy()
    {
        if (ActiveFairies.Remove(this))
        {
            OnActiveFairiesChanged?.Invoke();
        }
        RemoveDestroyedFairies();
    }

    private Vector3 GetCompanionSlotOffset()
    {
        int fairyCount = ActiveFairies.Count;
        if (fairyCount <= 1)
        {
            return Vector3.zero;
        }

        int slotIndex = ActiveFairies.IndexOf(this);
        if (slotIndex <= 0)
        {
            if (slotIndex < 0)
            {
                RemoveDestroyedFairies();
            }
            return Vector3.zero;
        }

        // Bố trí theo đội hình cánh cung (Wedge / Fan formation) lùi dần về phía sau
        int tier = (slotIndex - 1) / 2 + 1;
        bool isUpper = (slotIndex % 2 == 1);
        float offsetX = -tier * companionSlotSpacingX;
        float offsetY = (isUpper ? 1f : -1f) * (0.6f + (tier - 1) * 0.4f) * companionSlotSpacingY;

        return new Vector3(offsetX, offsetY, 0f);
    }

    private static void RemoveDestroyedFairies()
    {
        ActiveFairies.RemoveAll(fairy => fairy == null);
    }

    private bool TryFindClearPosition(Vector3 origin, float radius, float searchRange, LayerMask mask, out Vector3 clearPos)
    {
        Vector3 heightOffset = GetWallCheckHeightOffset();
        Vector3 checkPoint = origin + heightOffset;
        int colliderCount = Physics2D.OverlapCircleNonAlloc(checkPoint, radius, WallSafetyColliderBuffer, mask);

        if (colliderCount > 0)
        {
            Collider2D col = WallSafetyColliderBuffer[0];
            Vector3 closest = col.ClosestPoint(checkPoint);
            Vector3 outwardDir;

            if (Vector3.Distance(checkPoint, closest) > 0.001f)
            {
                outwardDir = (checkPoint - closest).normalized;
            }
            else
            {
                Vector3 referenceCenter = activePlayer != null ? activePlayer.transform.position : origin;
                if (Vector3.Distance(checkPoint, referenceCenter) > 0.001f)
                {
                    outwardDir = (checkPoint - referenceCenter).normalized;
                }
                else
                {
                    outwardDir = Vector3.up;
                }
            }

            Vector3 escapeCandidateCheckPoint = closest + outwardDir * (radius + 0.05f);
            Vector3 escapeCandidate = escapeCandidateCheckPoint - heightOffset;

            if (IsCircleClear(escapeCandidate, radius, mask))
            {
                clearPos = escapeCandidate;
                return true;
            }
        }
        else
        {
            clearPos = origin;
            return true;
        }

        const int ringCount = 4;
        for (int ring = 1; ring <= ringCount; ring++)
        {
            float ringRadius = searchRange * ring / ringCount;

            foreach (Vector2 dir in SearchDirections)
            {
                Vector3 candidate = new Vector3(
                    origin.x + dir.x * ringRadius,
                    origin.y + dir.y * ringRadius,
                    origin.z);

                if (IsCircleClear(candidate, radius, mask))
                {
                    clearPos = candidate;
                    return true;
                }
            }
        }

        clearPos = origin;
        return false;
    }

    private bool IsCircleClear(Vector3 point, float radius, LayerMask mask)
    {
        Vector3 checkPoint = point + GetWallCheckHeightOffset();
        return Physics2D.OverlapCircleNonAlloc(checkPoint, radius, WallSafetyColliderBuffer, mask) == 0;
    }

    private Vector3 GetWallCheckHeightOffset()
    {
        if (IsRotaActive() && activeRotaController != null)
        {
            Vector2 normal = activeRotaController.SurfaceNormal;
            if (normal.sqrMagnitude > 0.001f)
            {
                return (Vector3)normal.normalized * wallCheckHeightOffset;
            }
            if (activePlayer != null)
            {
                return activePlayer.transform.up * wallCheckHeightOffset;
            }
        }

        float gravityYSign = IsPlayerGravityInverted() ? -1f : 1f;
        float effectiveHeightOffset = wallCheckHeightOffset * gravityYSign;

        if (!Mathf.Approximately(cachedWallCheckHeightOffset, effectiveHeightOffset))
        {
            wallCheckHeightVector = new Vector3(0f, effectiveHeightOffset, 0f);
            cachedWallCheckHeightOffset = effectiveHeightOffset;
        }

        return wallCheckHeightVector;
    }

    private void FacePlayerDirection()
    {
        if (!matchPlayerFacing || activePlayer == null)
        {
            return;
        }

        if (_isFollowingFairy && _followTargetFairy != null)
        {
            FaceVisual(_followTargetFairy.IsFacingRight);
            return;
        }

        bool facingRight = IsRotaActive() ? lastRotaFacingRight : (activePlayer.FacingDirectionSign >= 0f);
        FaceVisual(facingRight);
    }

    private void FaceVisual(bool facingRight)
    {
        Transform targetVisual = visualRoot != null ? visualRoot : transform;
        CaptureBaseVisualTransformIfNeeded();

        Vector3 eulerAngles = targetVisual.localEulerAngles;
        if (IsRotaActive() && activePlayer != null)
        {
            eulerAngles.x = activePlayer.transform.eulerAngles.z;
            eulerAngles.z = activePlayer.transform.eulerAngles.z;
        }
        else if (hasBaseVisualEulerAngles)
        {
            eulerAngles.x = baseVisualEulerAngles.x;
            eulerAngles.z = baseVisualEulerAngles.z;
        }
        else
        {
            eulerAngles.x = 0f;
            eulerAngles.z = 0f;
        }
        eulerAngles.y = facingRight ? rightFacingRotationY : leftFacingRotationY;
        targetVisual.localEulerAngles = eulerAngles;

        float playerScale = scaleWithPlayer && activePlayer != null && activePlayer.ScaleProvider != null
            ? activePlayer.ScaleProvider.CurrentScaleMultiplier
            : 1f;
        float gravityYSign = IsPlayerGravityInverted() ? -1f : 1f;
        Vector3 scale = baseVisualScale * playerScale;
        scale.y = Mathf.Abs(scale.y) * gravityYSign;
        scale.z = Mathf.Abs(scale.z) * (facingRight ? rightFacingScaleZ : leftFacingScaleZ);
        targetVisual.localScale = scale;
    }

    private void CaptureBaseVisualTransformIfNeeded()
    {
        if (hasBaseVisualScale && hasBaseVisualEulerAngles)
        {
            return;
        }

        Transform targetVisual = visualRoot != null ? visualRoot : transform;
        if (!hasBaseVisualScale)
        {
            baseVisualScale = targetVisual.localScale;
            baseVisualScale.x = Mathf.Abs(baseVisualScale.x);
            baseVisualScale.y = Mathf.Abs(baseVisualScale.y);
            baseVisualScale.z = Mathf.Abs(baseVisualScale.z);
            hasBaseVisualScale = true;
        }

        if (!hasBaseVisualEulerAngles)
        {
            baseVisualEulerAngles = targetVisual.localEulerAngles;
            hasBaseVisualEulerAngles = true;
        }
    }
}

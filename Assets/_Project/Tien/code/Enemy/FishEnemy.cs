using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class FishEnemy : MonoBehaviour
{
    // ─── Trạng thái máy trạng thái ──────────────────────────────────────────
    private enum State
    {
        Patrol,     // Bơi qua lại khi chưa thấy người chơi hoặc người chơi chưa xuống nước
        Orbit,      // Lượn quanh / bơi qua lại ở khoảng cách trung bình khi đã phát hiện
        Lunge,      // Lao tấn công
        Retreat,    // Rút lui bắt buộc sau mỗi đòn tấn công
        Approach,   // Tiếp cận nếu người chơi quá xa
    }

    [Header("References")]
    private Transform player;
    private SpriteRenderer spriteRenderer;

    [Header("Water")]
    private LayerMask waterLayer;
    [SerializeField] private float waterSearchRadius = 14f;
    [SerializeField] private float boundPadding = 0.25f;

    [Header("Swimming")]
    [SerializeField] private float patrolSpeed = 2.6f;
    [SerializeField] private float orbitSpeed = 3.2f;
    [SerializeField] private float approachSpeed = 4.8f;
    [SerializeField] private float retreatSpeed = 5.5f;
    [SerializeField] private float swimMinYOffset = -1f;
    [SerializeField] private float swimMaxYOffset = 1f;
    [SerializeField] private bool startFacingRight = true;

    [Header("Smooth Steering")]
    [SerializeField] private float turnSmoothness = 5f;
    [SerializeField] private float waveAmplitude = 0.08f;
    [SerializeField] private float waveFrequency = 1.4f;
    [SerializeField] private float maxVerticalSpeed = 1.8f;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 7f;
    [SerializeField] private float loseInterestRange = 11f;
    [Tooltip("Nếu true, cá sẽ lập tức lao vào đớp ngay khi người chơi vừa rơi xuống nước")]
    [SerializeField] private bool instantAttackOnDetect = true;
    [Tooltip("Số lượng cá tối đa sẽ lao vào cắn ngay lập tức khi người chơi vừa nhảy xuống nước")]
    [SerializeField] private int maxInstantAttackers = 2;
    [Tooltip("Thời gian (giây) reset bộ đếm cắn tức thì kể từ lần cắn cuối. Khi hết thời gian, đợt nhảy nước tiếp theo sẽ lại bị cắn.")]
    [SerializeField] private float instantAttackResetTime = 3.5f;

    [Header("Orbit — Lượn quanh quan sát")]
    [SerializeField] private float orbitMinRadius = 2.2f;
    [SerializeField] private float orbitMaxRadius = 3.8f;
    [SerializeField] private float orbitDurationMin = 0.8f;
    [SerializeField] private float orbitDurationMax = 2.0f;
    [SerializeField] private float orbitPickNewTargetInterval = 0.9f;
    [SerializeField] private float orbitYWander = 0.6f;

    [Header("Lunge — Lao tấn công")]
    [SerializeField] private float attackSpeed = 8.5f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackRange = 1.35f;
    [SerializeField] private float lungeDuration = 1.0f;
    [SerializeField] private float lungeVerticalBias = 0.35f;
    [Tooltip("Xác suất cá chọn lao tấn công thay vì tiếp tục orbit (0-1)")]
    [SerializeField, Range(0f, 1f)] private float lungeChance = 0.55f;

    [Header("Retreat — Rút lui sau tấn công")]
    [SerializeField] private float retreatDistanceMin = 3.5f;
    [SerializeField] private float retreatDistanceMax = 5.5f;
    [SerializeField] private float retreatDurationMin = 0.8f;
    [SerializeField] private float retreatDurationMax = 1.6f;

    [Header("Approach — Tiếp cận khi quá xa")]
    [SerializeField] private float approachThreshold = 6f;
    [Tooltip("Khoảng cách quá gần → ưu tiên giữ khoảng cách thay vì lao vào")]
    [SerializeField] private float tooCloseThreshold = 2f;
    [SerializeField] private float approachRepickInterval = 0.6f;
    [SerializeField] private float approachYWander = 0.4f;

    [Header("Patrol")]
    [SerializeField] private float patrolTurnIntervalMin = 1.2f;
    [SerializeField] private float patrolTurnIntervalMax = 2.5f;
    [SerializeField] private float patrolWaypointRadius = 3.5f;
    [SerializeField] private float patrolMinYDifference = 0.5f;

    [Header("Flip Stability")]
    [SerializeField] private float flipCooldown = 0.08f;
    [SerializeField] private float flipDeadzone = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    // ─── Runtime ─────────────────────────────────────────────────────────────
    private Rigidbody2D rb;
    private Vector2 homePosition;
    private Vector2 moveTarget;
    private float stateTimer;
    private float subTimer;
    private float facingDirection = 1f;
    private State currentState;
    private bool dealtDamageThisLunge;
    private Vector2 currentVelocity;
    private float wavePhase;
    private float flipCooldownTimer;
    private float pendingFacingDirection;

    // ─── Static Fleet Management ──────────────────────────────────────────────
    private static readonly List<FishEnemy> allFish = new List<FishEnemy>();
    private static bool playerWasInWater = false;
    private static float lastGlobalLungeTime = -999f;
    private static int globalLungeCount = 0;

    private LayerMask WaterLayerMask => waterLayer.value != 0 ? waterLayer : PlayerLayerConfig.WaterLayer;

    private Transform TargetPlayer
    {
        get
        {
            if (player != null && player.gameObject.activeInHierarchy)
            {
                return player;
            }

            Player current = PlayerRegistry.Current;
            if (current != null && current.isActiveAndEnabled)
            {
                player = current.transform;
                return player;
            }

            player = null;
            return null;
        }
    }

    // ─── Init & Lifecycle ───────────────────────────────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 0f;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        homePosition = transform.position;
        facingDirection = startFacingRight ? 1f : -1f;
        pendingFacingDirection = facingDirection;
        wavePhase = Random.Range(0f, Mathf.PI * 2f);
        EnterState(State.Patrol);
    }

    private void OnEnable()
    {
        if (!allFish.Contains(this))
            allFish.Add(this);
    }

    private void OnDisable()
    {
        allFish.Remove(this);
    }

    // ─── State Machine ──────────────────────────────────────────────────────
    private void Update()
    {
        Transform target = TargetPlayer;
        if (target == null)
        {
            if (currentState != State.Patrol)
            {
                EnterState(State.Patrol);
            }
            UpdatePatrol();
            return;
        }

        // Kiểm tra xem người chơi có đang ở trong nước không
        bool isPlayerInWater = IsPlayerInWater(target, WaterLayerMask);

        // BẮT SỰ KIỆN: Người chơi VỪA NHẢY XUỐNG NƯỚC!
        if (isPlayerInWater && !playerWasInWater)
        {
            playerWasInWater = true;
            TriggerWaterEntryAmbush(target.position);
        }
        else if (!isPlayerInWater && playerWasInWater)
        {
            playerWasInWater = false;
        }

        stateTimer -= Time.deltaTime;

        switch (currentState)
        {
            case State.Patrol:   UpdatePatrol();   break;
            case State.Orbit:    UpdateOrbit();    break;
            case State.Lunge:    UpdateLunge();    break;
            case State.Retreat:  UpdateRetreat();  break;
            case State.Approach: UpdateApproach(); break;
        }
    }

    private void FixedUpdate()
    {
        Vector2 toTarget = moveTarget - (Vector2)transform.position;
        float distanceToTarget = toTarget.magnitude;

        if (distanceToTarget > 0.0001f)
        {
            toTarget /= distanceToTarget;
        }

        float speed = GetCurrentSpeed();
        Vector2 desiredVelocity = toTarget * speed;

        // Lunge uses direct velocity for snappy attack; all other states use smoothed steering
        if (currentState == State.Lunge)
        {
            currentVelocity = desiredVelocity;
        }
        else
        {
            float smoothFactor = 1f - Mathf.Exp(-turnSmoothness * Time.fixedDeltaTime);
            currentVelocity = Vector2.Lerp(currentVelocity, desiredVelocity, smoothFactor);

            wavePhase += waveFrequency * Time.fixedDeltaTime * Mathf.PI * 2f;
            if (wavePhase > Mathf.PI * 2f) wavePhase -= Mathf.PI * 2f;

            float waveTargetY = Mathf.Sin(wavePhase) * waveAmplitude;
            waveTargetY *= GetWaterBoundsDamping(waveTargetY);
            currentVelocity.y = Mathf.MoveTowards(
                currentVelocity.y,
                Mathf.Clamp(currentVelocity.y + waveTargetY, -maxVerticalSpeed, maxVerticalSpeed),
                turnSmoothness * Time.fixedDeltaTime);
            currentVelocity.y = Mathf.Clamp(currentVelocity.y, -maxVerticalSpeed, maxVerticalSpeed);
        }

        rb.linearVelocity = currentVelocity;

        Face(currentVelocity.x);

        // Giữ cá trong nước
        if (!IsInsideWater())
        {
            Vector2 clamped = ClampToNearestWater(rb.position);
            rb.position = clamped;
            transform.position = clamped;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // WATER ENTRY AMBUSH — 1 đến 2 con cá gần nhất lao vào đớp ngay lập tức
    // ────────────────────────────────────────────────────────────────────────
    private static void TriggerWaterEntryAmbush(Vector2 playerPos)
    {
        var nearbyFishes = new List<FishEnemy>();
        for (int i = 0; i < allFish.Count; i++)
        {
            FishEnemy fish = allFish[i];
            if (fish != null && fish.gameObject.activeInHierarchy)
            {
                float d = Vector2.Distance(fish.transform.position, playerPos);
                if (d <= fish.waterSearchRadius)
                {
                    nearbyFishes.Add(fish);
                }
            }
        }

        if (nearbyFishes.Count == 0) return;

        // Sắp xếp cá từ gần người chơi nhất đến xa nhất
        nearbyFishes.Sort((a, b) =>
            Vector2.Distance(a.transform.position, playerPos)
            .CompareTo(Vector2.Distance(b.transform.position, playerPos))
        );

        globalLungeCount = 0;
        lastGlobalLungeTime = Time.time;

        int ambushCount = Mathf.Min(nearbyFishes[0].maxInstantAttackers, nearbyFishes.Count);
        for (int i = 0; i < ambushCount; i++)
        {
            globalLungeCount++;
            nearbyFishes[i].TriggerInstantLunge();
        }

        // Các con cá còn lại lập tức chuyển sang Orbit quan sát quanh người chơi
        for (int i = ambushCount; i < nearbyFishes.Count; i++)
        {
            if (nearbyFishes[i].currentState == State.Patrol)
            {
                nearbyFishes[i].EnterState(State.Orbit);
            }
        }
    }

    public void TriggerInstantLunge()
    {
        EnterState(State.Lunge);
    }

    // ────────────────────────────────────────────────────────────────────────
    // PATROL — Bơi qua lại khi chưa phát hiện người chơi hoặc người chơi chưa ở trong nước
    // ────────────────────────────────────────────────────────────────────────
    private void UpdatePatrol()
    {
        Transform target = TargetPlayer;
        // Chỉ phản ứng nếu người chơi đang ở trong nước và trong tầm phát hiện
        if (target != null && IsPlayerInWater(target, WaterLayerMask) && PlayerInRange(detectionRange))
        {
            if (instantAttackOnDetect)
            {
                if (Time.time - lastGlobalLungeTime > instantAttackResetTime)
                {
                    globalLungeCount = 0;
                }

                if (globalLungeCount < maxInstantAttackers)
                {
                    globalLungeCount++;
                    lastGlobalLungeTime = Time.time;
                    EnterState(State.Lunge);
                    return;
                }
            }

            EnterState(State.Orbit);
            return;
        }

        // Đến gần target hoặc hết timer → chọn target mới
        if (stateTimer <= 0f || ReachedTarget(0.3f))
        {
            moveTarget = PickPatrolWaypoint();
            stateTimer = Random.Range(patrolTurnIntervalMin, patrolTurnIntervalMax);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // ORBIT — Lượn quanh người chơi ở khoảng cách trung bình
    // ────────────────────────────────────────────────────────────────────────
    private void UpdateOrbit()
    {
        Transform target = TargetPlayer;
        // Mất mục tiêu hoặc người chơi đã rời khỏi nước → về tuần tra
        if (target == null || !IsPlayerInWater(target, WaterLayerMask) || !PlayerInRange(loseInterestRange))
        {
            EnterState(State.Patrol);
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, target.position);

        // Quá xa → tiếp cận trước
        if (distToPlayer > approachThreshold)
        {
            EnterState(State.Approach);
            return;
        }

        // Cập nhật điểm orbit mới theo interval
        subTimer -= Time.deltaTime;
        if (subTimer <= 0f || ReachedTarget(0.4f))
        {
            moveTarget = PickOrbitPoint();
            subTimer = orbitPickNewTargetInterval + Random.Range(-0.2f, 0.2f);
        }

        // Hết thời gian orbit → quyết định hành động tiếp
        if (stateTimer <= 0f)
        {
            DecideAfterOrbit(distToPlayer);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // LUNGE — Lao tấn công (Đớp dứt khoát)
    // ────────────────────────────────────────────────────────────────────────
    private void UpdateLunge()
    {
        Transform target = TargetPlayer;
        if (target != null)
            moveTarget = (Vector2)target.position;

        // Kiểm tra va chạm tầm đánh
        if (!dealtDamageThisLunge && target != null
            && Vector2.Distance(transform.position, target.position) <= attackRange)
        {
            TryHitPlayerInRange();
            // Đớp trúng → kết thúc đòn sau tích tắc để rút lui ngay
            stateTimer = Mathf.Min(stateTimer, 0.12f);
        }

        // Hết thời gian lunge → bắt buộc rút lui
        if (stateTimer <= 0f)
        {
            EnterState(State.Retreat);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // RETREAT — Rút lui bắt buộc sau tấn công
    // ────────────────────────────────────────────────────────────────────────
    private void UpdateRetreat()
    {
        if (stateTimer <= 0f || ReachedTarget(0.5f))
        {
            Transform target = TargetPlayer;
            if (target != null && IsPlayerInWater(target, WaterLayerMask) && PlayerInRange(detectionRange))
                EnterState(State.Orbit);
            else
                EnterState(State.Patrol);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // APPROACH — Bơi lại gần khi quá xa
    // ────────────────────────────────────────────────────────────────────────
    private void UpdateApproach()
    {
        Transform target = TargetPlayer;
        if (target == null || !IsPlayerInWater(target, WaterLayerMask) || !PlayerInRange(loseInterestRange))
        {
            EnterState(State.Patrol);
            return;
        }

        // Đã đủ gần → chuyển sang Orbit
        float dist = Vector2.Distance(transform.position, target.position);
        if (dist <= orbitMaxRadius)
        {
            EnterState(State.Orbit);
            return;
        }

        subTimer -= Time.deltaTime;
        if (subTimer <= 0f || ReachedTarget(0.5f))
        {
            Vector2 toPlayer = ((Vector2)target.position - (Vector2)transform.position).normalized;
            Vector2 perpendicular = new Vector2(-toPlayer.y, toPlayer.x) * Random.Range(-1f, 1f);
            moveTarget = (Vector2)target.position - toPlayer * orbitMaxRadius + perpendicular * 0.8f;
            moveTarget.y += Random.Range(-approachYWander, approachYWander);
            moveTarget = ClampToNearestWater(moveTarget);
            subTimer = approachRepickInterval + Random.Range(-0.1f, 0.1f);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Quyết định sau khi lượn quanh (Orbit)
    // ────────────────────────────────────────────────────────────────────────
    private void DecideAfterOrbit(float distToPlayer)
    {
        float roll = Random.value;

        // Quá gần → ưu tiên giữ khoảng cách
        if (distToPlayer < tooCloseThreshold)
        {
            if (roll < 0.6f)
                EnterState(State.Retreat);
            else
                EnterState(State.Orbit);
            return;
        }

        // Tấn công / Orbit / Rút lui
        if (roll < lungeChance)
        {
            EnterState(State.Lunge);
        }
        else if (roll < lungeChance + 0.3f)
        {
            EnterState(State.Orbit);
        }
        else
        {
            EnterState(State.Retreat);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Enter State
    // ────────────────────────────────────────────────────────────────────────
    private void EnterState(State newState)
    {
        currentState = newState;
        Transform target = TargetPlayer;

        switch (newState)
        {
            case State.Patrol:
                moveTarget = PickPatrolWaypoint();
                stateTimer = Random.Range(patrolTurnIntervalMin, patrolTurnIntervalMax);
                break;

            case State.Orbit:
                moveTarget = PickOrbitPoint();
                stateTimer = Random.Range(orbitDurationMin, orbitDurationMax);
                subTimer = orbitPickNewTargetInterval;
                break;

            case State.Lunge:
                dealtDamageThisLunge = false;
                float dist = target != null ? Vector2.Distance(transform.position, target.position) : 3f;
                // Thời gian lunge tính theo khoảng cách thực tế để chắc chắn cá bơi tới được người chơi
                stateTimer = Mathf.Clamp(dist / attackSpeed + 0.35f, 0.6f, 1.6f);
                if (target != null)
                {
                    Vector2 lungeTarget = (Vector2)target.position;
                    lungeTarget.y += Random.Range(-lungeVerticalBias, lungeVerticalBias);
                    moveTarget = lungeTarget;
                }
                break;

            case State.Retreat:
                moveTarget = PickRetreatPoint();
                stateTimer = Random.Range(retreatDurationMin, retreatDurationMax);
                break;

            case State.Approach:
                stateTimer = 4f; // safety timeout
                subTimer = 0f;   // force immediate target pick
                break;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Chọn waypoint tuần tra gần, với Y khác biệt rõ ràng
    // ────────────────────────────────────────────────────────────────────────
    private Vector2 PickPatrolWaypoint()
    {
        Collider2D water = GetClosestWaterCollider();
        Vector2 pos = (Vector2)transform.position;

        if (water == null)
        {
            Vector2 offset = Random.insideUnitCircle * patrolWaypointRadius;
            if (Mathf.Abs(offset.y) < patrolMinYDifference)
                offset.y = patrolMinYDifference * Mathf.Sign(offset.y == 0f ? 1f : offset.y);
            return pos + offset;
        }

        Bounds bounds = water.bounds;
        float minX = bounds.min.x + boundPadding;
        float maxX = bounds.max.x - boundPadding;
        float minY = Mathf.Max(bounds.min.y + boundPadding, homePosition.y + swimMinYOffset);
        float maxY = Mathf.Min(bounds.max.y - boundPadding, homePosition.y + swimMaxYOffset);

        const int maxAttempts = 5;
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle * patrolWaypointRadius;
            Vector2 candidate = pos + offset;
            candidate.x = Mathf.Clamp(candidate.x, minX, maxX);
            candidate.y = Mathf.Clamp(candidate.y, minY, maxY);

            if (Mathf.Abs(candidate.y - pos.y) >= patrolMinYDifference)
                return candidate;
        }

        float fallbackX = Mathf.Clamp(pos.x + Random.Range(-patrolWaypointRadius, patrolWaypointRadius), minX, maxX);
        float yDir = Random.value > 0.5f ? 1f : -1f;
        float fallbackY = Mathf.Clamp(pos.y + yDir * Random.Range(patrolMinYDifference, patrolWaypointRadius), minY, maxY);
        return new Vector2(fallbackX, fallbackY);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Chọn điểm lượn quanh người chơi — biased to sides with height variation
    // ────────────────────────────────────────────────────────────────────────
    private Vector2 PickOrbitPoint()
    {
        Transform target = TargetPlayer;
        if (target == null) return PickPatrolWaypoint();

        float side = Random.value > 0.5f ? 1f : -1f;
        float angle = Random.Range(-60f, 60f) * Mathf.Deg2Rad;
        float radius = Random.Range(orbitMinRadius, orbitMaxRadius);
        Vector2 offset = new Vector2(Mathf.Cos(angle) * side, Mathf.Sin(angle)) * radius;
        Vector2 point = (Vector2)target.position + offset;
        point.y += Random.Range(-orbitYWander, orbitYWander);
        return ClampToNearestWater(point);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Chọn điểm rút lui xa người chơi
    // ────────────────────────────────────────────────────────────────────────
    private Vector2 PickRetreatPoint()
    {
        Transform target = TargetPlayer;
        if (target == null) return PickPatrolWaypoint();

        Vector2 awayFromPlayer = ((Vector2)transform.position - (Vector2)target.position);
        if (awayFromPlayer.sqrMagnitude < 0.01f)
            awayFromPlayer = Random.insideUnitCircle.normalized;
        else
            awayFromPlayer.Normalize();

        float angleJitter = Random.Range(-45f, 45f) * Mathf.Deg2Rad;
        Vector2 rotated = new Vector2(
            awayFromPlayer.x * Mathf.Cos(angleJitter) - awayFromPlayer.y * Mathf.Sin(angleJitter),
            awayFromPlayer.x * Mathf.Sin(angleJitter) + awayFromPlayer.y * Mathf.Cos(angleJitter));

        float dist = Random.Range(retreatDistanceMin, retreatDistanceMax);
        Vector2 point = (Vector2)transform.position + rotated * dist;
        return ClampToNearestWater(point);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Tốc độ theo trạng thái
    // ────────────────────────────────────────────────────────────────────────
    private float GetCurrentSpeed()
    {
        switch (currentState)
        {
            case State.Patrol:   return patrolSpeed;
            case State.Orbit:    return orbitSpeed;
            case State.Lunge:    return attackSpeed;
            case State.Retreat:  return retreatSpeed;
            case State.Approach: return approachSpeed;
            default:             return patrolSpeed;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────
    private bool PlayerInRange(float range)
    {
        Transform target = TargetPlayer;
        return target != null && Vector2.Distance(transform.position, target.position) <= range;
    }

    private bool ReachedTarget(float threshold)
    {
        return Vector2.Distance(transform.position, moveTarget) <= threshold;
    }

    public static bool IsPlayerInWater(Transform targetPlayer, LayerMask waterMask)
    {
        if (targetPlayer == null) return false;

        // 1. Kiểm tra qua PlayerSwimController
        PlayerSwimController swim = targetPlayer.GetComponent<PlayerSwimController>();
        if (swim != null)
        {
            if (swim.IsInWaterMovementState || swim.WaterZoneCount > 0)
                return true;
        }

        // 2. Kiểm tra qua Physics Overlap với WaterLayer
        if (waterMask.value != 0)
        {
            if (Physics2D.OverlapPoint(targetPlayer.position, waterMask) != null)
                return true;
            if (Physics2D.OverlapCircle(targetPlayer.position, 0.4f, waterMask) != null)
                return true;
        }

        return false;
    }

    // ─── Damage ──────────────────────────────────────────────────────────────
    private void TryHitPlayerInRange()
    {
        Transform target = TargetPlayer;
        if (target == null) return;

        Collider2D playerCollider = target.GetComponent<Collider2D>();
        if (playerCollider == null)
            playerCollider = target.GetComponentInChildren<Collider2D>();
        if (playerCollider == null) return;

        if (Vector2.Distance(transform.position, target.position) > attackRange) return;

        TryApplyPlayerHit(playerCollider);
    }

    private void TryApplyPlayerHit(Collider2D playerCollider)
    {
        if (dealtDamageThisLunge || playerCollider == null) return;

        PlayerHitResolver.TryApplyHitToPlayer(
            playerCollider,
            attackDamage,
            ParryableAttackType.Melee,
            EnemyParryResponder.Ensure(this),
            transform,
            playerCollider.transform.position - transform.position);

        dealtDamageThisLunge = true;
    }

    // ─── Water bounds ──────────────────────────────────────────────────────
    private Collider2D GetClosestWaterCollider()
    {
        LayerMask mask = WaterLayerMask;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, waterSearchRadius, mask);
        if (hits == null || hits.Length == 0) return null;

        Collider2D best = null;
        float bestDistance = float.MaxValue;
        Vector2 pos = transform.position;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            float distance = Vector2.Distance(pos, hit.bounds.ClosestPoint(pos));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = hit;
            }
        }
        return best;
    }

    private bool IsInsideWater()
    {
        LayerMask mask = WaterLayerMask;
        if (mask.value == 0) return true;
        return Physics2D.OverlapPoint(transform.position, mask) != null;
    }

    private Vector2 ClampToNearestWater(Vector2 position)
    {
        Collider2D water = GetClosestWaterCollider();
        if (water == null) return position;

        Bounds bounds = water.bounds;
        float x = Mathf.Clamp(position.x, bounds.min.x + boundPadding, bounds.max.x - boundPadding);
        float y = Mathf.Clamp(position.y, bounds.min.y + boundPadding, bounds.max.y - boundPadding);
        return new Vector2(x, y);
    }

    private float GetWaterBoundsDamping(float verticalWave)
    {
        Collider2D water = GetClosestWaterCollider();
        if (water == null || Mathf.Approximately(verticalWave, 0f)) return 1f;

        Bounds bounds = water.bounds;
        float y = rb.position.y;
        float minY = bounds.min.y + boundPadding;
        float maxY = bounds.max.y - boundPadding;
        float fadeDistance = Mathf.Max(0.1f, boundPadding * 2f);

        if (verticalWave > 0f)
            return Mathf.InverseLerp(maxY, maxY - fadeDistance, y);

        return Mathf.InverseLerp(minY, minY + fadeDistance, y);
    }

    // ─── Facing ──────────────────────────────────────────────────────────────
    private void Face(float xVelocity)
    {
        if (Mathf.Abs(xVelocity) < flipDeadzone)
        {
            flipCooldownTimer = 0f;
            return;
        }

        float targetFacing = Mathf.Sign(-xVelocity);
        if (Mathf.Approximately(facingDirection, targetFacing))
        {
            flipCooldownTimer = 0f;
            return;
        }

        if (!Mathf.Approximately(pendingFacingDirection, targetFacing))
        {
            pendingFacingDirection = targetFacing;
            flipCooldownTimer = 0f;
        }

        flipCooldownTimer += Time.fixedDeltaTime;
        if (flipCooldownTimer < flipCooldown) return;

        facingDirection = targetFacing;
        flipCooldownTimer = 0f;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDirection;
        transform.localScale = scale;
    }

    // ─── Collision damage ────────────────────────────────────────────────────
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState == State.Lunge && collision.collider.CompareTag("Player"))
            TryApplyPlayerHit(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (currentState == State.Lunge && other.CompareTag("Player"))
            TryApplyPlayerHit(other);
    }

    // ─── Gizmos ──────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, waterSearchRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0.5f, 0f); // cam — orbit range
        Gizmos.DrawWireSphere(transform.position, orbitMinRadius);
        Gizmos.DrawWireSphere(transform.position, orbitMaxRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, tooCloseThreshold);
    }
}

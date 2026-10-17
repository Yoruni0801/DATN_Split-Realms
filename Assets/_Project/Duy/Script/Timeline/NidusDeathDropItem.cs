using UnityEngine;

public sealed class NidusDeathDropItem : MonoBehaviour
{
    public static NidusDeathDropItem Latest { get; private set; }

    public Transform Transform => transform;
    public bool IsPickedUp { get; private set; }

    [Header("Hover")]
    [SerializeField] private bool hoverAboveGround = true;
    [SerializeField, Min(0f)] private float fallingGravityScale = 1.5f;
    [SerializeField, Min(0f)] private float hoverHeight = 0.45f;
    [SerializeField, Min(0f)] private float hoverStartDistance = 0.25f;
    [SerializeField, Min(0f)] private float groundCheckDistance = 4f;
    [SerializeField, Min(0f)] private float settleVelocityThreshold = 0.35f;
    [SerializeField, Min(0f)] private float hoverBobAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float hoverBobFrequency = 2f;
    [SerializeField, Min(0f)] private float hoverMoveSpeed = 8f;
    [SerializeField] private LayerMask groundLayer = Physics2D.DefaultRaycastLayers;

    private Rigidbody2D body;
    private Collider2D[] colliders;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
    private Vector3 hoverBasePosition;
    private bool hovering;

    private void Awake()
    {
        RegisterAsLatest();
        CachePhysics();
    }

    private void OnEnable()
    {
        if (!IsPickedUp)
        {
            RegisterAsLatest();
        }
    }

    private void OnDestroy()
    {
        if (Latest == this)
        {
            Latest = null;
        }
    }

    private void FixedUpdate()
    {
        if (!hoverAboveGround || IsPickedUp)
        {
            return;
        }

        CachePhysics();
        if (body == null)
        {
            return;
        }

        if (!hovering)
        {
            body.gravityScale = Mathf.Max(body.gravityScale, fallingGravityScale);
            TryStartHover();
            return;
        }

        UpdateHover();
    }

    public void MarkPickedUp(Transform parent, Vector3 localOffset)
    {
        IsPickedUp = true;
        hovering = false;
        CachePhysics();

        if (Latest == this)
        {
            Latest = null;
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.freezeRotation = true;
            body.simulated = false;
        }

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        transform.SetParent(parent, worldPositionStays: false);
        transform.localPosition = localOffset;
        transform.localRotation = Quaternion.identity;
    }

    public void RegisterAsLatest()
    {
        IsPickedUp = false;
        hovering = false;
        Latest = this;

        CachePhysics();
    }

    /// <summary>
    /// Starts the pickup's idle bob immediately, without dropping to or
    /// sampling the ground. Used for the Nidus death crystal.
    /// </summary>
    public void BeginHoverAtCurrentPosition()
    {
        CachePhysics();

        transform.rotation = Quaternion.identity;
        hoverBasePosition = transform.position;
        hovering = true;

        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    public static bool TryGetLatestUnpicked(out NidusDeathDropItem dropItem)
    {
        dropItem = Latest;
        return dropItem != null && !dropItem.IsPickedUp;
    }

    private void CachePhysics()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (colliders == null || colliders.Length == 0)
        {
            colliders = GetComponentsInChildren<Collider2D>();
        }
    }

    private void TryStartHover()
    {
        if (body.linearVelocity.y > settleVelocityThreshold)
        {
            return;
        }

        if (!TryFindGroundBelow(out RaycastHit2D groundHit))
        {
            return;
        }

        float bottomOffset = GetBottomOffset();
        float maxHoverStartDistance = bottomOffset + hoverHeight + hoverStartDistance;
        if (groundHit.distance > maxHoverStartDistance)
        {
            return;
        }

        hoverBasePosition = transform.position;
        hoverBasePosition.y = groundHit.point.y + bottomOffset + hoverHeight;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.freezeRotation = true;
        transform.rotation = Quaternion.identity;
        body.gravityScale = 0f;
        hovering = true;
    }

    private void UpdateHover()
    {
        float bob = hoverBobAmplitude > 0f
            ? Mathf.Sin(Time.time * Mathf.Max(0.01f, hoverBobFrequency) * Mathf.PI * 2f) * hoverBobAmplitude
            : 0f;

        Vector3 targetPosition = hoverBasePosition + Vector3.up * bob;
        Vector3 nextPosition = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            hoverMoveSpeed * Time.fixedDeltaTime);

        body.MovePosition(nextPosition);
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private float GetBottomOffset()
    {
        if (colliders == null || colliders.Length == 0)
        {
            return 0f;
        }

        float lowestY = float.PositiveInfinity;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider != null && collider.enabled)
            {
                lowestY = Mathf.Min(lowestY, collider.bounds.min.y);
            }
        }

        return float.IsPositiveInfinity(lowestY)
            ? 0f
            : Mathf.Max(0f, transform.position.y - lowestY);
    }

    private bool TryFindGroundBelow(out RaycastHit2D groundHit)
    {
        groundHit = default;

        int hitCount = Physics2D.RaycastNonAlloc(
            transform.position,
            Vector2.down,
            groundHits,
            groundCheckDistance,
            groundLayer);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = groundHits[i];
            if (hit.collider == null || hit.collider.isTrigger || IsOwnCollider(hit.collider))
            {
                continue;
            }

            groundHit = hit;
            return true;
        }

        return false;
    }

    private bool IsOwnCollider(Collider2D collider)
    {
        if (collider == null || colliders == null)
        {
            return false;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == collider)
            {
                return true;
            }
        }

        return collider.transform.IsChildOf(transform);
    }
}

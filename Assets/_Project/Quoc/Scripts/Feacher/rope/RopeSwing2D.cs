using UnityEngine;

[DisallowMultipleComponent]
public class RopeSwing2D : MonoBehaviour
{
    [Header("Rope")]
    [SerializeField] private Transform anchor;
    [SerializeField] private Transform grabPoint;
    [SerializeField] private Rigidbody2D ropeEndBody;
    [SerializeField] private bool autoResolveSkinnedRopeBones = true;
    [SerializeField] private float ropeLength = 4f;
    [SerializeField] private float attachRadius = 0.8f;
    [SerializeField] private LayerMask playerLayer = ~0;

    [Header("Attach")]
    [SerializeField] private bool requireUpwardJump = true;
    [SerializeField] private float minimumUpwardVelocity = 0.5f;
    [SerializeField] private float attachCooldown = 0.2f;
    [SerializeField] private bool attachWhenFallingIntoEndpoint = true;
    [SerializeField] private bool autoCatchAfterRopeRelease = true;
    [SerializeField] private float ropeToRopeCatchWindow = 1.25f;
    [SerializeField] private float sameRopeReattachDelay = 0.75f;
    [SerializeField] private Vector2 playerGrabOffset = new Vector2(0f, -0.35f);

    [Header("Swing")]
    [SerializeField] private float gravity = 28f;
    [SerializeField] private float pumpAcceleration = 4.5f;
    [SerializeField] private float ropeEndForce = 16f;
    [SerializeField] private float damping = 0.08f;
    [SerializeField] private float maxAngle = 70f;
    [SerializeField] private float maxAngularVelocity = 7f;

    [Header("Release")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private float releaseTangentBoost = 1.1f;
    [SerializeField] private float releaseForwardBoost = 3f;
    [SerializeField] private float releaseJumpBoost = 8f;
    [SerializeField] private float minimumReleaseHorizontalSpeed = 5f;
    [SerializeField] private float releaseHorizontalHoldTime = 0.18f;

    [Header("Flexibility")]
    [SerializeField] private float flexAmount = 0.35f;
    [SerializeField] private float flexWaveSpeed = 4.5f;


    private Rigidbody2D attachedBody;
    private Player attachedPlayer;
    private float cachedGravityScale;
    private RigidbodyConstraints2D cachedConstraints;
    private float angle;
    private float angularVelocity;
    private float nextAttachTime;
    private Transform[] skinnedBones;

    private static Rigidbody2D lastReleasedBody;
    private static RopeSwing2D lastReleasedRope;
    private static float lastReleaseCatchUntil;
    private static float lastSameRopeReattachBlockedUntil;

    public bool HasPlayer => attachedBody != null;
    public Vector2 AnchorPosition => anchor != null ? anchor.position : transform.position;
    public Vector2 GrabPosition => GetGrabPosition();
    public Vector2 RopeEndPosition => ropeEndBody != null ? ropeEndBody.position : attachedBody != null ? GetBobPosition(angle) : GetGrabPosition();

    private void Reset()
    {
        anchor = transform;
        grabPoint = null;
        ropeEndBody = null;
    }

    private void Awake()
    {
        if (autoResolveSkinnedRopeBones)
        {
            ResolveSkinnedRopeReferences();
        }
        CacheSkinnedBones();

        if (anchor == null)
        {
            anchor = transform;
        }
    }

    private void Update()
    {
        if (attachedBody == null || !Input.GetKeyDown(jumpKey))
        {
            return;
        }

        Release(applyJumpBoost: true);
    }

    private void FixedUpdate()
    {
        if (attachedBody == null)
        {
            TryAttachPlayerInRange();
            return;
        }

        TickSwing(Time.fixedDeltaTime);
        MoveAttachedPlayer();
    }

    private void LateUpdate()
    {
        if (attachedBody != null)
        {
            MoveAttachedPlayer();
        }
        UpdateSkinnedBoneFlex();
    }


    private void OnDisable()
    {
        if (attachedBody != null)
        {
            Release(applyJumpBoost: false);
        }
    }

    private void TryAttachPlayerInRange()
    {
        if (Time.time < nextAttachTime)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(GetGrabPosition(), attachRadius, playerLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            if (TryAttachFromEndpoint(hits[i]))
            {
                return;
            }
        }
    }

    public bool TryAttachFromEndpoint(Collider2D hit)
    {
        if (attachedBody != null || hit == null || Time.time < nextAttachTime)
        {
            return false;
        }

        Rigidbody2D body = hit.attachedRigidbody;
        if (body == null || !IsPlayer(hit, body))
        {
            return false;
        }

        bool canAutoCatchFromRope = CanAutoCatchFromPreviousRope(body);
        if (IsSameRopeReattachBlocked(body))
        {
            return false;
        }

        bool canCatchFallingEndpoint = attachWhenFallingIntoEndpoint && body.linearVelocity.y <= minimumUpwardVelocity;
        if (requireUpwardJump
            && body.linearVelocity.y < minimumUpwardVelocity
            && !canAutoCatchFromRope
            && !canCatchFallingEndpoint)
        {
            return false;
        }

        Attach(body);
        return true;
    }

    private void Attach(Rigidbody2D body)
    {
        attachedBody = body;
        attachedPlayer = body.GetComponent<Player>();
        cachedGravityScale = body.gravityScale;
        cachedConstraints = body.constraints;

        attachedPlayer?.SetControlLocked(true);
        attachedPlayer?.PlayRopeGrabAnimation();
        attachedBody.gravityScale = 0f;
        attachedBody.angularVelocity = 0f;
        attachedBody.constraints = RigidbodyConstraints2D.FreezeRotation;

        Vector2 anchorPosition = AnchorPosition;
        Vector2 playerPosition = body.position - playerGrabOffset;
        Vector2 direction = playerPosition - anchorPosition;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector2.down;
        }

        ropeLength = Mathf.Max(0.5f, direction.magnitude);
        angle = Mathf.Atan2(direction.x, -direction.y);
        angle = Mathf.Clamp(angle, -maxAngle * Mathf.Deg2Rad, maxAngle * Mathf.Deg2Rad);
        angularVelocity = Vector2.Dot(GetAttachVelocity(), GetTangent(angle)) / ropeLength;

        ClearRopeReleaseState(body);
        MoveAttachedPlayer();
    }

    private void TickSwing(float dt)
    {
        if (ropeEndBody != null)
        {
            TickSkinnedRopeSwing();
            return;
        }

        float input = Input.GetAxisRaw("Horizontal");
        float angularAcceleration = -(gravity / Mathf.Max(ropeLength, 0.1f)) * Mathf.Sin(angle);
        angularAcceleration += input * pumpAcceleration;

        angularVelocity += angularAcceleration * dt;
        angularVelocity *= Mathf.Clamp01(1f - damping * dt);
        angularVelocity = Mathf.Clamp(angularVelocity, -maxAngularVelocity, maxAngularVelocity);

        angle += angularVelocity * dt;

        float maxRadians = maxAngle * Mathf.Deg2Rad;
        if (Mathf.Abs(angle) > maxRadians)
        {
            angle = Mathf.Sign(angle) * maxRadians;
            if (Mathf.Sign(angularVelocity) == Mathf.Sign(angle))
            {
                angularVelocity *= -0.35f;
            }
        }
    }

    private void TickSkinnedRopeSwing()
    {
        float input = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(input) > 0.01f)
        {
            ropeEndBody.AddForce(Vector2.right * (input * ropeEndForce), ForceMode2D.Force);
        }

        Vector2 direction = ropeEndBody.position - AnchorPosition;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        ropeLength = Mathf.Max(0.5f, direction.magnitude);
        angle = Mathf.Atan2(direction.x, -direction.y);
        angularVelocity = Vector2.Dot(ropeEndBody.linearVelocity, GetTangent(angle)) / ropeLength;
    }

    private void MoveAttachedPlayer()
    {
        Vector2 position = GetAttachPosition() + playerGrabOffset;
        Vector2 velocity = GetAttachVelocity();

        attachedBody.MovePosition(position);
        attachedBody.linearVelocity = velocity;
    }

    private void Release(bool applyJumpBoost)
    {
        Rigidbody2D releasedBody = attachedBody;
        Vector2 swingVelocity = GetAttachVelocity();
        if (swingVelocity.sqrMagnitude < releasedBody.linearVelocity.sqrMagnitude)
        {
            swingVelocity = releasedBody.linearVelocity;
        }

        float releaseDirection = GetReleaseDirection(swingVelocity);
        Vector2 releaseVelocity = swingVelocity * releaseTangentBoost;
        releaseVelocity.x += releaseDirection * releaseForwardBoost;

        if (Mathf.Abs(releaseVelocity.x) < minimumReleaseHorizontalSpeed)
        {
            releaseVelocity.x = releaseDirection * minimumReleaseHorizontalSpeed;
        }

        if (applyJumpBoost)
        {
            releaseVelocity.y = Mathf.Max(releaseVelocity.y, releaseJumpBoost);
        }

        releasedBody.gravityScale = cachedGravityScale;
        releasedBody.constraints = cachedConstraints;
        attachedPlayer?.ClearRopeGrabAnimation();
        attachedPlayer?.SetControlLocked(false);
        if (attachedPlayer != null)
        {
            attachedPlayer.ApplyExternalLaunch(releaseVelocity, releaseHorizontalHoldTime);
        }
        else
        {
            releasedBody.linearVelocity = releaseVelocity;
        }

        RememberRopeRelease(releasedBody);
        attachedBody = null;
        attachedPlayer = null;
        nextAttachTime = Time.time + attachCooldown;
    }

    private float GetReleaseDirection(Vector2 swingVelocity)
    {
        if (Mathf.Abs(swingVelocity.x) > 0.05f)
        {
            return Mathf.Sign(swingVelocity.x);
        }

        float tangentX = GetTangent(angle).x;
        if (Mathf.Abs(tangentX) > 0.05f)
        {
            return Mathf.Sign(tangentX);
        }

        return Mathf.Sign(Mathf.Approximately(Mathf.Sin(angle), 0f) ? 1f : Mathf.Sin(angle));
    }

    private bool CanAutoCatchFromPreviousRope(Rigidbody2D body)
    {
        return autoCatchAfterRopeRelease
            && lastReleasedBody == body
            && lastReleasedRope != this
            && Time.time <= lastReleaseCatchUntil;
    }

    private bool IsSameRopeReattachBlocked(Rigidbody2D body)
    {
        return lastReleasedBody == body
            && lastReleasedRope == this
            && Time.time <= lastSameRopeReattachBlockedUntil;
    }

    private void RememberRopeRelease(Rigidbody2D body)
    {
        if (body == null)
        {
            return;
        }

        lastReleasedBody = body;
        lastReleasedRope = this;
        lastSameRopeReattachBlockedUntil = Time.time + Mathf.Max(attachCooldown, sameRopeReattachDelay);
        lastReleaseCatchUntil = autoCatchAfterRopeRelease
            ? Time.time + Mathf.Max(0f, ropeToRopeCatchWindow)
            : 0f;
    }

    private static void ClearRopeReleaseState(Rigidbody2D body)
    {
        if (lastReleasedBody != body)
        {
            return;
        }

        lastReleasedBody = null;
        lastReleasedRope = null;
        lastReleaseCatchUntil = 0f;
        lastSameRopeReattachBlockedUntil = 0f;
    }

    private Vector2 GetGrabPosition()
    {
        if (grabPoint != null)
        {
            return grabPoint.position;
        }

        return AnchorPosition + Vector2.down * ropeLength;
    }

    private Vector2 GetAttachPosition()
    {
        return ropeEndBody != null ? ropeEndBody.position : GetBobPosition(angle);
    }

    private Vector2 GetAttachVelocity()
    {
        return ropeEndBody != null ? ropeEndBody.linearVelocity : GetTangent(angle) * (angularVelocity * ropeLength);
    }

    private Vector2 GetBobPosition(float currentAngle)
    {
        return AnchorPosition + new Vector2(Mathf.Sin(currentAngle), -Mathf.Cos(currentAngle)) * ropeLength;
    }

    private static Vector2 GetTangent(float currentAngle)
    {
        return new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle));
    }

    private static bool IsPlayer(Collider2D hit, Rigidbody2D body)
    {
        return hit.CompareTag("Player")
            || body.CompareTag("Player")
            || body.GetComponent<Player>() != null
            || hit.GetComponentInParent<Player>() != null;
    }

    private void ResolveSkinnedRopeReferences()
    {
        if (anchor == null)
        {
            Transform hook = FindChildByName(transform, "Hook");
            if (hook != null)
            {
                anchor = hook;
            }
        }

        if (grabPoint == null)
        {
            Transform endBone = FindChildByName(transform, "bone_10");
            if (endBone != null)
            {
                grabPoint = endBone;
            }
        }

        if (ropeEndBody == null && grabPoint != null)
        {
            ropeEndBody = grabPoint.GetComponent<Rigidbody2D>();
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private void CacheSkinnedBones()
    {
        var list = new System.Collections.Generic.List<Transform>();
        Transform searchRoot = FindChildByName(transform, "bone_1");
        if (searchRoot == null && anchor != null)
        {
            searchRoot = FindChildByName(anchor, "bone_1");
        }

        Transform curr = searchRoot;
        while (curr != null)
        {
            list.Add(curr);
            if (curr == grabPoint || curr.childCount == 0)
            {
                break;
            }
            curr = curr.GetChild(0);
        }

        if (list.Count > 0)
        {
            skinnedBones = list.ToArray();
        }
    }

    private void UpdateSkinnedBoneFlex()
    {
        if (skinnedBones == null || skinnedBones.Length <= 1)
        {
            return;
        }

        int count = skinnedBones.Length;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float bendFactor = Mathf.Sin(t * Mathf.PI);
            float wave = Mathf.Sin(Time.time * flexWaveSpeed - t * 3f) * 0.12f;
            float flexOffset = (-angularVelocity * flexAmount + wave) * bendFactor * Mathf.Rad2Deg;

            skinnedBones[i].localRotation = Quaternion.Euler(0f, 0f, flexOffset);
        }
    }


    private void OnDrawGizmosSelected()
    {
        Vector2 anchorPosition = anchor != null ? anchor.position : transform.position;
        Vector2 grabPosition = grabPoint != null ? grabPoint.position : anchorPosition + Vector2.down * ropeLength;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(anchorPosition, grabPosition);
        Gizmos.DrawWireSphere(grabPosition, attachRadius);
    }
}

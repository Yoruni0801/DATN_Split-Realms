using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


[DisallowMultipleComponent]
public class PushableRotatorVFX : MonoBehaviour
{
    [SerializeField] private ReferenceAxis referenceAxis = ReferenceAxis.LocalRight;
    [SerializeField] private Transform targetVfx;
    
    [SerializeField] private Vector3 customDirection = Vector3.right;
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;

    [SerializeField] private float minAngle = 0f;
    [SerializeField] private float maxAngle = 360f;
    [SerializeField] private float minLimit = -3f;
    [SerializeField] private float maxLimit = 3f;

    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool scaleSpeedWithVelocity = true;
    [SerializeField] private bool clampRotation = true;
    [SerializeField] private bool useLimits = true;
    [SerializeField] private bool lockPerpendicularMovement = false;
    public enum ReferenceAxis
    {
        LocalRight,
        LocalForward,
        WorldRight,
        WorldUp,
        Custom
    }

    private Rigidbody2D rb2d;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float currentRotationAngle = 0f;
    private bool isInitialized;
    private bool isPlaying;

    private void Awake()
    {
        if (targetVfx == null) targetVfx = transform;
        rb2d = GetComponentInParent<Rigidbody2D>();

        initialPosition = transform.position;
        initialRotation = targetVfx.localRotation;
        currentRotationAngle = minAngle;

        isInitialized = true;
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        isPlaying = true;
    }

    public void Stop()
    {
        isPlaying = false;
    }

    private void OnEnable()
    {
        if (!isInitialized)
        {
            initialPosition = transform.position;
            if (targetVfx != null)
            {
                initialRotation = targetVfx.localRotation;
                currentRotationAngle = minAngle;
            }
            isInitialized = true;
        }
    }

    private void FixedUpdate()
    {
        // Triệt tiêu quán tính trôi tự do khi không có ngoại lực đẩy
        if (rb2d != null && rb2d.bodyType == RigidbodyType2D.Dynamic)
        {
            rb2d.linearVelocity = Vector2.zero;
        }
    }

    private void Update()
    {
        if (!isPlaying) return;

        HandleRotation();
    }

    private void LateUpdate()
    {
        if (useLimits)
        {
            ApplyPositionLimits();
        }
    }

    private void HandleRotation()
    {
        // Map vị trí trên ray đẩy -> góc xoay: minLimit = minAngle, maxLimit = maxAngle.
        // Đẩy hết minLimit -> maxLimit thì xoay đúng đúng khoảng góc đó (mặc định 0 -> 360 = 1 vòng).
        Vector3 refDir = ResolveReferenceDirection();
        if (refDir.sqrMagnitude < 0.000001f) return;

        float span = maxLimit - minLimit;
        if (span <= 0.0001f) return;

        float currentDistance = Vector3.Dot(transform.position - initialPosition, refDir.normalized);
        float t = Mathf.Clamp01((currentDistance - minLimit) / span);

        float endAngle = clampRotation ? maxAngle : minAngle + 360f;
        currentRotationAngle = Mathf.Lerp(minAngle, endAngle, t);
        targetVfx.localRotation = initialRotation * Quaternion.AngleAxis(currentRotationAngle, rotationAxis);
    }

    private void ApplyPositionLimits()
    {
        Vector3 refDir = ResolveReferenceDirection();
        if (refDir.sqrMagnitude < 0.000001f) return;

        Vector3 currentPos = transform.position;
        Vector3 offsetFromStart = currentPos - initialPosition;

        // Chiếu khoảng lệch lên trục tham chiếu
        float currentDistance = Vector3.Dot(offsetFromStart, refDir);
        float clampedDistance = Mathf.Clamp(currentDistance, minLimit, maxLimit);

        if (!Mathf.Approximately(currentDistance, clampedDistance) || lockPerpendicularMovement)
        {
            Vector3 targetPos;
            if (lockPerpendicularMovement)
            {
                // Chỉ trượt dọc theo ray từ initialPosition
                targetPos = initialPosition + (refDir * clampedDistance);
            }
            else
            {
                // Giữ nguyên toạ độ các trục khác, chỉ kẹp dọc trục refDir
                Vector3 delta = refDir * (clampedDistance - currentDistance);
                targetPos = currentPos + delta;
            }

            transform.position = targetPos;
        }
    }

    private Vector3 ResolveReferenceDirection()
    {
        switch (referenceAxis)
        {
            case ReferenceAxis.LocalRight: return transform.right;
            case ReferenceAxis.LocalForward: return transform.forward;
            case ReferenceAxis.WorldRight: return Vector3.right;
            case ReferenceAxis.WorldUp: return Vector3.up;
            case ReferenceAxis.Custom:
                return customDirection.sqrMagnitude < 0.000001f ? Vector3.right : customDirection.normalized;
            default: return transform.right;
        }
    }
}

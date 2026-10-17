using UnityEngine;

public sealed class FireballMapHazard : MonoBehaviour
{
    public enum MovementMode
    {
        Stationary,
        MoveBetweenPoints
    }

    [Header("Display")]
    [SerializeField] private ParticleSystem visualEffect;
    [SerializeField] private bool loopVisualEffect = true;

    [Header("Movement")]
    [SerializeField] private MovementMode movementMode = MovementMode.Stationary;
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [SerializeField, Min(0.001f)] private float arriveDistance = 0.05f;
    [SerializeField] private bool snapToPointAOnStart = true;
    [SerializeField] private bool flipWhenMoving = true;

    private Transform currentTarget;
    private Quaternion initialRotation;
    private Vector3 reverseRotationEuler;
    private float lastHorizontalDirection;

    private void Awake()
    {
        initialRotation = transform.localRotation;
        Vector3 initialEuler = transform.localEulerAngles;
        // Keep the signed Euler angles so reversing the movement flips every axis.
        reverseRotationEuler = new Vector3(
            -Mathf.DeltaAngle(0f, initialEuler.x),
            -Mathf.DeltaAngle(0f, initialEuler.y),
            -Mathf.DeltaAngle(0f, initialEuler.z));
        PlayVisualEffect();

        if (movementMode != MovementMode.MoveBetweenPoints || pointA == null || pointB == null)
        {
            return;
        }

        if (snapToPointAOnStart)
        {
            transform.position = pointA.position;
        }

        currentTarget = pointB;
        UpdateFacing(force: true);
    }

    private void Update()
    {
        if (movementMode != MovementMode.MoveBetweenPoints ||
            pointA == null ||
            pointB == null ||
            currentTarget == null ||
            moveSpeed <= 0f)
        {
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            currentTarget.position,
            moveSpeed * Time.deltaTime);

        if (Vector3.SqrMagnitude(transform.position - currentTarget.position) <=
            arriveDistance * arriveDistance)
        {
            currentTarget = currentTarget == pointA ? pointB : pointA;
            UpdateFacing(force: true);
            return;
        }

        UpdateFacing();
    }

    private void UpdateFacing(bool force = false)
    {
        if (!flipWhenMoving)
        {
            return;
        }

        float horizontalDirection = currentTarget.position.x - transform.position.x;
        if (Mathf.Abs(horizontalDirection) <= Mathf.Epsilon)
        {
            return;
        }

        float direction = Mathf.Sign(horizontalDirection);
        if (!force && Mathf.Approximately(direction, lastHorizontalDirection))
        {
            return;
        }

        transform.localRotation = direction > 0f
            ? initialRotation
            : Quaternion.Euler(reverseRotationEuler);
        lastHorizontalDirection = direction;
    }

    private void PlayVisualEffect()
    {
        if (visualEffect == null)
        {
            visualEffect = GetComponentInChildren<ParticleSystem>(true);
        }

        if (visualEffect == null)
        {
            return;
        }

        visualEffect.gameObject.SetActive(true);
        ParticleSystem.MainModule main = visualEffect.main;
        main.loop = loopVisualEffect;
        visualEffect.Play(true);
    }

    private void OnDrawGizmosSelected()
    {
        if (movementMode != MovementMode.MoveBetweenPoints || pointA == null || pointB == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawLine(pointA.position, pointB.position);
        Gizmos.DrawWireSphere(pointA.position, arriveDistance);
        Gizmos.DrawWireSphere(pointB.position, arriveDistance);
    }
}

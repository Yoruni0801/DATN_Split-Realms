using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlinkoSlider : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private Vector2 localAxis = Vector2.right;
    [SerializeField] private float travelDistance = 2.5f;
    [SerializeField] private float speed = 1.2f;
    [SerializeField] private bool useUnscaledTime = false;

    private Rigidbody2D rb;
    private Vector2 startLocalPosition;
    private Vector2 normalizedAxis;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Configure Rigidbody2D
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        
        // Try to set continuous collision detection if available
        #if UNITY_2020_1_OR_NEWER || UNITY_6000
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        #endif

        // Cache starting local position and normalized axis
        startLocalPosition = transform.localPosition;
        normalizedAxis = localAxis.normalized;

        // Guard invalid values
        if (normalizedAxis == Vector2.zero)
        {
            Debug.LogWarning($"{nameof(PlinkoSlider)} on {gameObject.name}: localAxis is zero. Using Vector2.right.", this);
            normalizedAxis = Vector2.right;
        }

        travelDistance = Mathf.Max(0f, travelDistance);
        speed = Mathf.Max(0f, speed);
    }

    private void FixedUpdate()
    {
        if (speed <= 0f || travelDistance <= 0f)
        {
            Vector2 stationaryWorldPosition = transform.parent != null ?
                (Vector2)transform.parent.TransformPoint(startLocalPosition) :
                startLocalPosition;
            rb.MovePosition(stationaryWorldPosition);
            return;
        }

        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float pingPongValue = Mathf.PingPong(time * speed, travelDistance);
        
        Vector2 offset = normalizedAxis * pingPongValue;
        Vector2 targetLocalPosition = startLocalPosition + offset;
        
        Vector2 worldPosition = transform.parent != null ?
            (Vector2)transform.parent.TransformPoint(targetLocalPosition) :
            targetLocalPosition;

        rb.MovePosition(worldPosition);
    }

    public void ResetToStart()
    {
        Vector2 worldPosition = transform.parent != null ?
            (Vector2)transform.parent.TransformPoint(startLocalPosition) :
            startLocalPosition;
        rb.MovePosition(worldPosition);
    }
}

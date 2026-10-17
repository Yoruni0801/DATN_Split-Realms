using UnityEngine;

/// <summary>
/// Tu dong theo doi vi tri dung an toan gan nhat cua Player (Hazard Safe Respawn).
/// KHONG can sua hay can thiep vao bat ky script nao cua Player.
/// </summary>
public class HazardSafeTracker : MonoBehaviour
{
    public static HazardSafeTracker Instance { get; private set; }

    [Header("Safe Ground Settings")]
    [Tooltip("Ban kinh quet tim mat dat duoi chan Player.")]
    [SerializeField] private float groundCheckRadius = 0.25f;

    [Tooltip("Thoi gian dung vung an toan (giay) truoc khi cap nhat vi tri Safe.")]
    [SerializeField] private float safePositionMinInterval = 0.15f;

    private Vector3 lastSafePosition;
    private bool hasSafePosition = false;
    private float safeTimer = 0f;
    private Transform playerTransform;
    private Rigidbody2D playerRb;
    private Collider2D playerCol;

    public static void EnsureExists()
    {
        if (Instance == null)
        {
            GameObject obj = new GameObject("[HazardSafeTracker]");
            Instance = obj.AddComponent<HazardSafeTracker>();
            DontDestroyOnLoad(obj);
        }
    }

    public static Vector3 GetSafePosition(Transform player)
    {
        EnsureExists();
        if (Instance != null && Instance.hasSafePosition)
        {
            return Instance.lastSafePosition;
        }
        return player != null ? player.position : Vector3.zero;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // if (playerTransform == null)
        // {
        //     GameObject p = GameObject.FindGameObjectWithTag("Player");
        //     if (p != null)
        //     {
        //         playerTransform = p.transform;
        //         playerRb = p.GetComponent<Rigidbody2D>();
        //         playerCol = p.GetComponent<Collider2D>();
        //         lastSafePosition = playerTransform.position;
        //         hasSafePosition = true;
        //     }
        //     else
        //     {
        //         return;
        //     }
        // }

        // if (playerCol == null) return;

        // // Kiem tra chan Player cham vao Ground
        // Vector2 checkPos = new Vector2(playerCol.bounds.center.x, playerCol.bounds.min.y + 0.05f);
        // LayerMask groundMask = PlayerLayerConfig.GroundLayer;
        // Collider2D groundHit = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundMask);

        // // Kiem tra Player khong nam trong vung Hazard/Spike trigger
        // bool isTouchingHazard = false;
        // Collider2D[] overlaps = Physics2D.OverlapPointAll(checkPos);
        // foreach (var c in overlaps)
        // {
        //     if (c.isTrigger && (c.GetComponent<IcicleSpikeDamage>() != null || c.CompareTag("Hazard")))
        //     {
        //         isTouchingHazard = true;
        //         break;
        //     }
        // }

        // // Mat dat an toan: Dang dung tren Ground, khong dẫm Gai, van toc Y nhan dinh khong phai dang roi/bay vọt
        // bool isGroundedAndStable = groundHit != null && !isTouchingHazard && (playerRb == null || Mathf.Abs(playerRb.linearVelocity.y) < 0.8f);

        // if (isGroundedAndStable)
        // {
        //     safeTimer += Time.deltaTime;
        //     if (safeTimer >= safePositionMinInterval)
        //     {
        //         lastSafePosition = playerTransform.position;
        //         hasSafePosition = true;
        //     }
        // }
        // else
        // {
        //     safeTimer = 0f;
        // }
    }

    private void OnDrawGizmosSelected()
    {
        if (hasSafePosition)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lastSafePosition, 0.4f);
        }
    }
}

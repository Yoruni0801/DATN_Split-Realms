using UnityEngine;

public class WormEnemy : MonoBehaviour
{
    // Trạng thái của quái
    public enum WormState { Idle, Emerging, Active, Retracting }
    private WormState currentState = WormState.Idle;

    [Header("Thời gian")]
    [SerializeField] private float delayBeforeEmerge = 2f;        // Thời gian chờ trước khi mọc lên (lần đầu)
    [SerializeField] private float emergeTime = 1.5f;             // Thời gian mọc lên
    [SerializeField] private float activeTime = 3f;               // Thời gian ở trên mặt đất
    [SerializeField] private float retractTime = 1.5f;            // Thời gian rút lại

    [Header("Vị trí")]
    [SerializeField] private Vector3 emergingPosition = Vector3.zero;   // Vị trí trên mặt đất (mục tiêu)
    private Vector3 hiddenPosition;                                     // Vị trí ẩn dưới đất (vị trí ban đầu)

    [Header("Lặp lại")]
    [SerializeField] private bool isLooping = true;               // Có lặp lại không
    [SerializeField] private float loopDelay = 1f;                // Thời gian chờ giữa các chu kỳ sau lần đầu

    [Header("Sát thương (Damage)")]
    [SerializeField] private int damage = 10;                     // Sát thương gây ra cho người chơi
    [SerializeField] private float damageCooldown = 1f;           // Thời gian hồi sát thương giữa các lần chạm
    private float lastDamageTime = 0f;

    private float stateTimer = 0f;
    private float globalTimer = 0f;
    private float currentDelay = 0f;                              // Thời gian chờ hiện tại (được set ngẫu nhiên)

    void Start()
    {
        // Lưu lại vị trí ban đầu (vị trí ẩn dưới đất)
        hiddenPosition = transform.position;
        
        // Nếu emergingPosition chưa được set, dùng vị trí hiện tại
        if (emergingPosition == Vector3.zero)
            emergingPosition = transform.position;

        // Sử dụng thời gian chờ ban đầu được quy định
        currentDelay = delayBeforeEmerge;

        // Bắt đầu ẩn
        transform.position = hiddenPosition;
        currentState = WormState.Idle;
        stateTimer = 0f;
    }

    void Update()
    {
        stateTimer += Time.deltaTime;
        globalTimer += Time.deltaTime;

        switch (currentState)
        {
            case WormState.Idle:
                HandleIdleState();
                break;
            case WormState.Emerging:
                HandleEmergingState();
                break;
            case WormState.Active:
                HandleActiveState();
                break;
            case WormState.Retracting:
                HandleRetractingState();
                break;
        }
    }

    private void HandleIdleState()
    {
        // Chờ đủ thời gian rồi bắt đầu mọc lên
        if (stateTimer >= currentDelay)
        {
            ChangeState(WormState.Emerging);
        }
    }

    private void HandleEmergingState()
    {
        // Mọc lên từ từ (di chuyển từ hiddenPosition lên emergingPosition)
        float progress = Mathf.Clamp01(stateTimer / emergeTime);
        transform.position = Vector3.Lerp(hiddenPosition, emergingPosition, progress);

        // Khi mọc xong
        if (stateTimer >= emergeTime)
        {
            ChangeState(WormState.Active);
        }
    }

    private void HandleActiveState()
    {
        // Ở trên mặt đất
        transform.position = emergingPosition;

        // Khi hết thời gian ở trên
        if (stateTimer >= activeTime)
        {
            ChangeState(WormState.Retracting);
        }
    }

    private void HandleRetractingState()
    {
        // Rút lại từ từ (di chuyển từ emergingPosition xuống hiddenPosition)
        float progress = Mathf.Clamp01(stateTimer / retractTime);
        transform.position = Vector3.Lerp(emergingPosition, hiddenPosition, progress);

        // Khi rút xong
        if (stateTimer >= retractTime)
        {
            if (isLooping)
            {
                ChangeState(WormState.Idle);
                currentDelay = loopDelay;
                stateTimer = 0f;
            }
            else
            {
                // Dừng nếu không lặp
                enabled = false;
            }
        }
    }

    private void ChangeState(WormState newState)
    {
        currentState = newState;
        stateTimer = 0f;
    }

    // Hàm có thể gọi từ bên ngoài để kích hoạt sự xuất hiện
    public void TriggerEmerge()
    {
        ChangeState(WormState.Idle);
        stateTimer = 0f;
    }

    // Hàm để lấy trạng thái hiện tại
    public WormState GetCurrentState()
    {
        return currentState;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Gây sát thương khi player bước vào vùng va chạm của worm
        if (collision.CompareTag("Player"))
        {
            TryDealDamage(collision.gameObject);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // Liên tục gây sát thương theo thời gian giãn cách nếu player đứng im trong vùng va chạm
        if (collision.CompareTag("Player"))
        {
            TryDealDamage(collision.gameObject);
        }
    }

    private void TryDealDamage(GameObject playerObj)
    {
        // Chỉ gây sát thương khi quái đang nhô lên (Emerging) hoặc đang hoạt động (Active)
        if (currentState == WormState.Emerging || currentState == WormState.Active)
        {
            if (Time.time - lastDamageTime >= damageCooldown)
            {
                Collider2D playerCollider = playerObj.GetComponent<Collider2D>();
                if (playerCollider != null)
                {
                    PlayerHitResolver.TryApplyHitToPlayer(
                        playerCollider,
                        damage,
                        ParryableAttackType.Melee,
                        transform,
                        playerObj.transform.position - transform.position,
                        canBeBlocked: false);
                    lastDamageTime = Time.time;
                }
            }
        }
    }
}

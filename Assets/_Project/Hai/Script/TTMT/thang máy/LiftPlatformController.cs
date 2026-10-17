using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiftPlatformController : MonoBehaviour
{
    public enum LiftPositionState
    {
        AtPointA,  // Ở dưới
        AtPointB,  // Ở trên
        Moving     // Đang di chuyển
    }

    [Header("Cấu hình di chuyển")]
    [SerializeField] private Transform pointA;          // Điểm xuất phát (ở dưới)
    [SerializeField] private Transform pointB;          // Điểm đích (ở trên)
    [SerializeField] private float speed = 2f;          // Tốc độ di chuyển

    [Header("Cấu hình tự động quay về từ Point B")]
    [Tooltip("Thời gian (giây) chờ ở trên Point B trước khi tự động đi xuống Point A")]
    [SerializeField] private float autoReturnWaitTime = 5f;

    [Header("Tương tác phím F")]
    [SerializeField] private bool allowFKeyInteraction = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private GameObject interactionPrompt;   // Gợi ý "Nhấn F"

    private Rigidbody2D rb;
    private bool _isMoving = false;
    private bool _isPlayerInRange = false;
    
    // Tọa độ thế giới cố định của A và B
    private Vector2 _posA;
    private Vector2 _posB;
    private Vector2 _targetPos;

    private LiftPositionState currentState = LiftPositionState.AtPointA;
    private Coroutine autoReturnCoroutine;

    private readonly HashSet<Rigidbody2D> passengers = new HashSet<Rigidbody2D>();

    // Sự kiện kích hoạt khi thang máy chạm đích
    public event Action OnReachedTarget;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }

    private void Start()
    {
        if (pointA != null && pointB != null)
        {
            // LƯU CỐ ĐỊNH TỌA ĐỘ THẾ GIỚI LÚC START
            _posA = pointA.position;
            _posB = pointB.position;

            // Tự động đưa thang máy về đúng vị trí Point A (ở dưới) khi mới bắt đầu game
            transform.position = new Vector3(_posA.x, _posA.y, transform.position.z);
            if (rb != null)
            {
                rb.position = _posA;
            }

            currentState = LiftPositionState.AtPointA;
            _targetPos = _posA;
            _isMoving = false;
            Debug.Log("[LiftPlatformController] Đã đặt vị trí xuất phát của thang máy về đúng Point A (ở dưới).");
        }
        else
        {
            Debug.LogError($"[LiftPlatformController] {gameObject.name} chưa được gán đủ Point A và Point B!");
        }

        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }

    private void Update()
    {
        if (!allowFKeyInteraction || !_isPlayerInRange) return;

        // Nhấn phím F tương tác
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (currentState == LiftPositionState.AtPointA)
            {
                // Ở DƯỚI: Nhấn F -> Đi LÊN Point B
                MoveToB();
            }
            else if (currentState == LiftPositionState.AtPointB)
            {
                // Ở TRÊN: Nhấn F -> Hủy đếm ngược 5s & Đi XUỐNG Point A ngay lập tức
                if (autoReturnCoroutine != null)
                {
                    StopCoroutine(autoReturnCoroutine);
                    autoReturnCoroutine = null;
                }
                MoveToA();
            }
            else
            {
                // Đang ở giữa đường
                if (_isMoving)
                {
                    // Đang chạy bấm F -> Tạm dừng
                    StopMoving();
                }
                else
                {
                    // Đang dừng giữa đường bấm F -> Tiếp tục chạy tiếp
                    StartMoving();
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (_isMoving && rb != null && pointA != null && pointB != null)
        {
            Vector2 currentPos = rb.position;
            Vector2 nextPos = Vector2.MoveTowards(currentPos, _targetPos, speed * Time.fixedDeltaTime);
            Vector2 delta = nextPos - currentPos;

            rb.MovePosition(nextPos);

            // Kéo Player đứng trên thang máy di chuyển theo mượt mà
            if (passengers.Count > 0)
            {
                passengers.RemoveWhere(p => p == null);
                foreach (Rigidbody2D passenger in passengers)
                {
                    passenger.position += delta;
                }
            }

            // Kiểm tra xem đã đến vị trí đích chưa
            if (Vector2.Distance(nextPos, _targetPos) < 0.05f)
            {
                rb.MovePosition(_targetPos); // Hít chặt vào vị trí đích
                _isMoving = false;

                OnReachedTarget?.Invoke();

                if (_targetPos == _posB)
                {
                    // Vừa lên tới Point B (Ở TRÊN)
                    currentState = LiftPositionState.AtPointB;
                    Debug.Log($"[LiftPlatformController] Thang máy đã lên đến Point B (ở trên). Chờ Player bấm F HOẶC tự động đi xuống sau {autoReturnWaitTime}s.");
                    
                    // Bắt đầu đếm ngược tự động đi xuống sau autoReturnWaitTime giây
                    if (autoReturnCoroutine != null) StopCoroutine(autoReturnCoroutine);
                    autoReturnCoroutine = StartCoroutine(AutoReturnToARoutine());
                }
                else
                {
                    // Vừa xuống tới Point A (Ở DƯỚI)
                    currentState = LiftPositionState.AtPointA;
                    Debug.Log("[LiftPlatformController] Thang máy đã xuống tới Point A (ở dưới) và DỪNG HẲN chờ lượt nhấn F tiếp theo.");
                }
            }
        }
    }

    /// <summary>
    /// Cho thang máy di chuyển lên Point B
    /// </summary>
    public void MoveToB()
    {
        _targetPos = _posB;
        currentState = LiftPositionState.Moving;
        _isMoving = true;
        Debug.Log("[LiftPlatformController] Thang máy bắt đầu đi LÊN Point B...");
    }

    /// <summary>
    /// Cho thang máy di chuyển xuống Point A
    /// </summary>
    public void MoveToA()
    {
        _targetPos = _posA;
        currentState = LiftPositionState.Moving;
        _isMoving = true;
        Debug.Log("[LiftPlatformController] Thang máy bắt đầu đi XUỐNG Point A...");
    }

    private IEnumerator AutoReturnToARoutine()
    {
        yield return new WaitForSeconds(autoReturnWaitTime);

        // Hết thời gian chờ mà thang máy vẫn ở Point B -> Tự động đi xuống Point A
        if (currentState == LiftPositionState.AtPointB && !_isMoving)
        {
            Debug.Log($"[LiftPlatformController] Đã ở Point B đủ {autoReturnWaitTime}s. Tự động đi XUỐNG Point A!");
            MoveToA();
        }
    }

    public void StartMoving()
    {
        if (currentState == LiftPositionState.AtPointA)
        {
            MoveToB();
        }
        else if (currentState == LiftPositionState.AtPointB)
        {
            MoveToA();
        }
        else
        {
            // Đang dừng giữa đường -> Tiếp tục di chuyển tới đích hiện tại
            if (_targetPos == Vector2.zero)
            {
                _targetPos = _posB;
            }
            currentState = LiftPositionState.Moving;
            _isMoving = true;
            Debug.Log($"[LiftPlatformController] Thang máy tiếp tục di chuyển tới đích: {_targetPos}...");
        }
    }

    public void StopMoving()
    {
        _isMoving = false;
        if (autoReturnCoroutine != null) StopCoroutine(autoReturnCoroutine);
        Debug.Log("[LiftPlatformController] Đã tạm dừng di chuyển.");
    }

    public bool IsMoving()
    {
        return _isMoving;
    }

    public LiftPositionState GetCurrentState()
    {
        return currentState;
    }

    // --- Xử lý phát hiện Player lại gần nhấn F (Hỗ trợ cả Trigger lẫn Collision cứng) ---
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            _isPlayerInRange = true;
            if (interactionPrompt != null) interactionPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            _isPlayerInRange = false;
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
        }
    }

    // --- Giữ Player đứng mượt mà trên mặt thang máy & Nhận diện phím F khi đứng trên sàn ---
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
        {
            _isPlayerInRange = true;
            if (interactionPrompt != null) interactionPrompt.SetActive(true);
        }
        TryAddPassenger(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
        {
            _isPlayerInRange = true;
        }
        TryAddPassenger(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
        {
            _isPlayerInRange = false;
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
        }

        if (collision.rigidbody != null)
        {
            passengers.Remove(collision.rigidbody);
        }
    }

    private void TryAddPassenger(Collision2D collision)
    {
        Rigidbody2D passenger = collision.rigidbody;
        if (passenger == null || passenger.bodyType != RigidbodyType2D.Dynamic)
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y < -0.5f)
            {
                passengers.Add(passenger);
                return;
            }
        }
    }

    private void OnDisable()
    {
        passengers.Clear();
    }

    // Vẽ trực quan tuyến đường giữa Point A và Point B trong Unity Scene View
    private void OnDrawGizmos()
    {
        if (pointA != null && pointB != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(pointA.position, pointB.position);
            Gizmos.DrawWireSphere(pointA.position, 0.4f);
            Gizmos.DrawWireSphere(pointB.position, 0.4f);
        }
    }
}

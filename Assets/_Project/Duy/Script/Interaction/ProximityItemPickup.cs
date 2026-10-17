using System;
using UnityEngine;
using UnityEngine.Events;

namespace Duy.Interaction
{
    [DisallowMultipleComponent]
    public class ProximityItemPickup : MonoBehaviour
    {
        [Header("Drop State")]
        [SerializeField] private bool autoActivateOnEnable = false; // Nếu true (đặt sẵn trên map) thì tự kích hoạt, nếu false (gắn trên chim/quái) thì đợi ActivateDrop()

        [Header("Magnet / Proximity Settings")]
        [SerializeField] private float attractRadius = 5.0f;     // Bán kính khi player lại gần là tự hút
        [SerializeField] private float initialFlySpeed = 6f;      // Tốc độ bay ban đầu
        [SerializeField] private float maxFlySpeed = 22f;         // Tốc độ tối đa khi lao vào player
        [SerializeField] private float acceleration = 25f;        // Gia tốc tăng dần khi bay
        [SerializeField] private float collectDistance = 0.5f;     // Khoảng cách chạm để thu thập

        [Header("Descent Animation (Rơi từ từ xuống một đoạn rồi lơ lửng)")]
        [SerializeField] private bool animateDescentOnDrop = true;
        [SerializeField] private float dropDescentDistance = 2.0f; // Khoảng cách rơi từ từ xuống
        [SerializeField] private float dropDescentDuration = 1.0f; // Thời gian hạ độ cao

        [Header("Floating Motion (Hiệu ứng nhấp nhô lơ lửng)")]
        [SerializeField] private bool bobbingMotion = true;
        [SerializeField] private float bobbingSpeed = 2.5f;
        [SerializeField] private float bobbingHeight = 0.15f;

        [Header("Visual & Audio Feedback")]
        [SerializeField] private GameObject pickupVFXPrefab;
        [SerializeField] private AudioClip pickupSound;

        [Header("Events (Kích hoạt khi nhặt thành công)")]
        [SerializeField] private UnityEvent onPickedUp;
        public event Action OnPickedUp;

        private bool isDropped;
        private bool isPicked;
        private bool isFlyingToPlayer;
        private Transform playerTarget;
        private float currentFlySpeed;
        private Vector3 initialPosition;
        private Vector3 targetRestPosition;
        private float descentTimer;
        private bool isDescending;

        public bool IsDropped => isDropped;
        public bool IsPicked => isPicked;

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnEnable()
        {
            if (autoActivateOnEnable)
            {
                ActivateDrop();
            }
        }

        /// <summary>
        /// Kích hoạt trạng thái rơi và bắt đầu cho phép hút vào người chơi.
        /// CHỈ được gọi khi quái chết và nhả vật phẩm ra.
        /// </summary>
        public void ActivateDrop()
        {
            isDropped = true;
            initialPosition = transform.position;
            targetRestPosition = initialPosition + Vector3.down * dropDescentDistance;
            descentTimer = 0f;
            isDescending = animateDescentOnDrop;
            isFlyingToPlayer = false;
            currentFlySpeed = initialFlySpeed;
        }

        private void Start()
        {
            if (isDropped && !isDescending)
            {
                initialPosition = transform.position;
                targetRestPosition = initialPosition;
            }
        }

        private void Update()
        {
            // CHƯA RƠI (QUÁI/CHIM CHƯA CHẾT) HOẶC ĐÃ NHẶT -> CHẶN HOÀN TOÀN
            if (!isDropped || isPicked) return;

            if (playerTarget == null)
            {
                FindPlayer();
            }

            // 1. Kiểm tra nếu Player vào tầm hút -> Kích hoạt bay vào Player
            if (!isFlyingToPlayer && playerTarget != null)
            {
                float distToPlayer = Vector2.Distance(transform.position, playerTarget.position);
                if (distToPlayer <= attractRadius)
                {
                    isFlyingToPlayer = true;
                    isDescending = false; // Ngắt rơi để bay vào người chơi
                }
            }

            // 2. Logic bay vút vào Player
            if (isFlyingToPlayer)
            {
                if (playerTarget == null)
                {
                    FindPlayer();
                    if (playerTarget == null) return;
                }

                // Điểm đích là ngực / tâm của Player
                Vector3 targetPoint = playerTarget.position + Vector3.up * 0.8f;
                currentFlySpeed = Mathf.MoveTowards(currentFlySpeed, maxFlySpeed, acceleration * Time.deltaTime);

                transform.position = Vector3.MoveTowards(transform.position, targetPoint, currentFlySpeed * Time.deltaTime);

                // Khi bay trúng người chơi -> Thu thập
                if (Vector2.Distance(transform.position, targetPoint) <= collectDistance)
                {
                    Collect();
                }
                return;
            }

            // 3. Logic rơi từ từ khi chưa bị hút
            if (isDescending)
            {
                descentTimer += Time.deltaTime;
                float t = Mathf.Clamp01(descentTimer / Mathf.Max(0.1f, dropDescentDuration));
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(initialPosition, targetRestPosition, smoothT);

                if (t >= 1f)
                {
                    isDescending = false;
                    initialPosition = targetRestPosition;
                }
            }
            // 4. Lơ lửng nhấp nhô nhẹ nhàng
            else if (bobbingMotion)
            {
                float newY = initialPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
                transform.position = new Vector3(initialPosition.x, newY, initialPosition.z);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // CHƯA RƠI (QUÁI/CHIM CHƯA CHẾT) HOẶC ĐÃ NHẶT -> CHẶN
            if (!isDropped || isPicked) return;

            if (IsPlayerCollider(other))
            {
                if (playerTarget == null)
                {
                    playerTarget = other.transform;
                }
                Collect();
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // CHƯA RƠI (QUÁI/CHIM CHƯA CHẾT) HOẶC ĐÃ NHẶT -> CHẶN
            if (!isDropped || isPicked) return;

            if (IsPlayerCollider(other))
            {
                Collect();
            }
        }

        private void FindPlayer()
        {
            if (PlayerRegistry.Current != null)
            {
                playerTarget = PlayerRegistry.Current.transform;
                return;
            }

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTarget = playerObj.transform;
                return;
            }

            Player playerComp = FindFirstObjectByType<Player>();
            if (playerComp != null)
            {
                playerTarget = playerComp.transform;
                return;
            }

            PlayerHealth healthComp = FindFirstObjectByType<PlayerHealth>();
            if (healthComp != null)
            {
                playerTarget = healthComp.transform;
            }
        }

        private bool IsPlayerCollider(Collider2D col)
        {
            if (col == null) return false;
            if (col.CompareTag("Player")) return true;
            if (col.GetComponentInParent<Player>() != null) return true;
            if (col.GetComponentInParent<PlayerHealth>() != null) return true;
            if (PlayerRegistry.Current != null && (col.transform == PlayerRegistry.Current.transform || col.transform.IsChildOf(PlayerRegistry.Current.transform))) return true;
            return false;
        }

        public void Collect()
        {
            // CHƯA RƠI (QUÁI/CHIM CHƯA CHẾT) HOẶC ĐÃ NHẶT -> KHÔNG CHO PHÉP NHẶT
            if (!isDropped || isPicked) return;
            isPicked = true;

            Debug.Log($"<color=green>[ProximityItemPickup] Thu thập thành công {gameObject.name}! Kích hoạt OnPickedUp event...</color>", this);

            if (pickupVFXPrefab != null)
            {
                Instantiate(pickupVFXPrefab, transform.position, Quaternion.identity);
            }

            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }

            // Kích hoạt Event
            try
            {
                onPickedUp?.Invoke();
                OnPickedUp?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProximityItemPickup] Error invoking onPickedUp event: {ex.Message}");
            }

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, attractRadius);
        }
    }
}

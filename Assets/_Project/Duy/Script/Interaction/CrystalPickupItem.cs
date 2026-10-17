using System;
using UnityEngine;
using UnityEngine.Events;

namespace EnemySystem
{
    public class CrystalPickupItem : MonoBehaviour
    {
        [Header("Drop State")]
        [SerializeField] private bool autoActivateOnEnable = false; // Nếu là true (đặt trực tiếp trên map) thì tự bật ngay khi Enable, nếu false (gắn trên thân chim) thì đợi ActivateDrop()

        [Header("Delay & Ejection Settings (Chống nhặt tức thì & văng xa)")]
        [SerializeField] private float pickupDelay = 1.2f;        // Thời gian chờ trước khi cho phép hút/nhặt để người chơi nhìn rõ
        [SerializeField] private float closeProximityCheck = 3.5f; // Nếu player đứng gần hơn mức này khi sinh ra -> văng xa ra
        [SerializeField] private float ejectDistance = 2.8f;      // Khoảng cách văng ra hướng ngược player
        [SerializeField] private float ejectDuration = 0.8f;      // Thời gian hoàn thành cú văng

        [Header("Magnet / Proximity Settings")]
        [SerializeField] private float attractRadius = 4.5f;      // Bán kính khi player lại gần là tự hút (sau khi hết delay)
        [SerializeField] private float initialFlySpeed = 5f;       // Tốc độ bay ban đầu
        [SerializeField] private float maxFlySpeed = 20f;          // Tốc độ tối đa khi lao vào player
        [SerializeField] private float acceleration = 25f;         // Gia tốc tăng dần khi bay
        [SerializeField] private float collectDistance = 0.4f;      // Khoảng cách chạm để thu thập

        [Header("Visual & Feedback")]
        [SerializeField] private GameObject pickupVFXPrefab;
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private bool bobbingMotion = true;
        [SerializeField] private float bobbingSpeed = 2.5f;
        [SerializeField] private float bobbingHeight = 0.15f;

        [Header("Events (Kích hoạt khi bay trúng người chơi)")]
        [SerializeField] private UnityEvent onPickedUp;
        public event Action OnPickedUp;

        private bool isDropped;
        private bool isPicked;
        private bool isFlyingToPlayer;
        private Transform playerTarget;
        private float currentFlySpeed;
        private float delayTimer;

        private Vector3 startEjectPos;
        private Vector3 targetRestPos;
        private float ejectTimer;
        private bool isEjecting;

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
            // CHỈ tự kích hoạt nếu có cấu hình autoActivateOnEnable tường minh
            if (autoActivateOnEnable)
            {
                ActivateDrop();
            }
        }

        /// <summary>
        /// Kích hoạt trạng thái rơi/văng và bắt đầu cho phép hút vào người chơi.
        /// CHỈ được gọi khi quái/chim thực sự chết và nhả vật phẩm ra khỏi cơ thể.
        /// </summary>
        public void ActivateDrop()
        {
            isDropped = true;
            FindPlayer();

            startEjectPos = transform.position;
            delayTimer = pickupDelay;
            isFlyingToPlayer = false;
            currentFlySpeed = initialFlySpeed;
            isEjecting = true;
            ejectTimer = 0f;

            // Tính hướng văng ngược với Player
            if (playerTarget != null)
            {
                float dx = transform.position.x - playerTarget.position.x;
                float dirX = dx >= 0 ? 1f : -1f;

                // Nếu người chơi ở quá gần, tăng thêm khoảng cách văng
                float dist = Mathf.Abs(dx);
                if (dist < closeProximityCheck)
                {
                    targetRestPos = startEjectPos + new Vector3(dirX * ejectDistance, -1.2f, 0f);
                }
                else
                {
                    targetRestPos = startEjectPos + new Vector3(dirX * 1.0f, -1.2f, 0f);
                }
            }
            else
            {
                targetRestPos = startEjectPos + new Vector3(0f, -1.2f, 0f);
            }
        }

        private void Update()
        {
            // CHƯA RƠI (CHIM CHƯA CHẾT) HOẶC ĐÃ NHẶT RỒI -> TUYỆT ĐỐI KHÔNG CHẠY LOGIC HÚT/NHẶT
            if (!isDropped || isPicked) return;

            if (playerTarget == null)
            {
                FindPlayer();
            }

            // 1. Giai đoạn văng vút ra hướng ngược player theo đường vòng cung
            if (isEjecting)
            {
                ejectTimer += Time.deltaTime;
                float t = Mathf.Clamp01(ejectTimer / Mathf.Max(0.1f, ejectDuration));
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // Tạo độ cong parabol nhô lên nhẹ rồi rơi xuống điểm đích
                float arcHeight = Mathf.Sin(smoothT * Mathf.PI) * 0.8f;
                Vector3 currentPos = Vector3.Lerp(startEjectPos, targetRestPos, smoothT) + Vector3.up * arcHeight;
                transform.position = currentPos;

                if (t >= 1f)
                {
                    isEjecting = false;
                    startEjectPos = targetRestPos; // Cập nhật neo lơ lửng
                }
            }
            // 2. Giai đoạn lơ lửng nhấp nhô
            else if (bobbingMotion && !isFlyingToPlayer)
            {
                float newY = startEjectPos.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
                transform.position = new Vector3(startEjectPos.x, newY, startEjectPos.z);
            }

            // Đếm ngược thời gian chờ nhặt
            if (delayTimer > 0f)
            {
                delayTimer -= Time.deltaTime;
                return; // Chưa hết thời gian chờ -> Không cho phép hút hoặc nhặt
            }

            // 3. Sau khi hết delay: Kiểm tra hút vào Player
            if (!isFlyingToPlayer && playerTarget != null)
            {
                float distToPlayer = Vector2.Distance(transform.position, playerTarget.position);
                if (distToPlayer <= attractRadius)
                {
                    isFlyingToPlayer = true;
                    isEjecting = false;
                }
            }

            // 4. Logic bay lao vào Player để thu thập
            if (isFlyingToPlayer)
            {
                if (playerTarget == null)
                {
                    FindPlayer();
                    if (playerTarget == null) return;
                }

                Vector3 targetPoint = playerTarget.position + Vector3.up * 0.8f;
                currentFlySpeed = Mathf.MoveTowards(currentFlySpeed, maxFlySpeed, acceleration * Time.deltaTime);

                transform.position = Vector3.MoveTowards(transform.position, targetPoint, currentFlySpeed * Time.deltaTime);

                if (Vector2.Distance(transform.position, targetPoint) <= collectDistance)
                {
                    Collect();
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // NẾU CHƯA ĐƯỢC DROP (CHIM CHƯA CHẾT) HOẶC ĐÃ NHẶT HOẶC ĐANG TRONG THỜI GIAN DELAY -> CHẶN HOÀN TOÀN
            if (!isDropped || isPicked || delayTimer > 0f) return;

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
            // NẾU CHƯA ĐƯỢC DROP (CHIM CHƯA CHẾT) HOẶC ĐÃ NHẶT HOẶC ĐANG TRONG THỜI GIAN DELAY -> CHẶN HOÀN TOÀN
            if (!isDropped || isPicked || delayTimer > 0f) return;

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
            // NẾU CHƯA ĐƯỢC DROP (CHIM CHƯA CHẾT) HOẶC ĐÃ NHẶT RỒI -> KHÔNG CHO PHÉP NHẶT
            if (!isDropped || isPicked) return;
            isPicked = true;

            Debug.Log($"<color=green>[CrystalPickupItem] Thu thập thành công {gameObject.name}! Kích hoạt OnPickedUp event...</color>", this);

            if (pickupVFXPrefab != null)
            {
                Instantiate(pickupVFXPrefab, transform.position, Quaternion.identity);
            }

            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }

            try
            {
                onPickedUp?.Invoke();
                OnPickedUp?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CrystalPickupItem] Error invoking onPickedUp event: {ex.Message}");
            }

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, attractRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, closeProximityCheck);
        }
    }
}

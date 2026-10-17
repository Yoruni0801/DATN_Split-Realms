using UnityEngine;

namespace Tien.Minigame
{
    [RequireComponent(typeof(Collider2D))]
    public class LocalTeleporter : MonoBehaviour
    {
        public enum TeleportType
        {
            GoToDestinationAndSaveReturn,
            ReturnToSavedPosition
        }

        [Header("Settings")]
        [SerializeField] private TeleportType teleportType = TeleportType.GoToDestinationAndSaveReturn;
        [SerializeField] private Transform destination; // Chỉ cần cho GoToDestinationAndSaveReturn
        [SerializeField] private string playerTag = "Player";
        [Tooltip("Nếu để trống, script sẽ tự động tìm Player qua Tag khi đụng trigger. Nếu kéo Player vào đây thì sẽ dùng trực tiếp.")]
        [SerializeField] private Transform targetPlayer;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        [Header("UI Prompt")]
        [SerializeField] private GameObject promptUI;

        [Header("Minigame Group")]
        [Tooltip("Kéo Object tổng chứa toàn bộ Mini Game (bao gồm cả SceneGate, Camera Intro...) vào đây để tự động bật tắt.")]
        [SerializeField] private GameObject minigameRoot;

        [Header("Player Modifier")]
        [Tooltip("Có tự động AddComponent RotaSurfaceWalker2D khi chơi và tự xoá (Destroy) khi ra không?")]
        [SerializeField] private bool autoAddRotaSurfaceWalker = true;
        [Tooltip("Nhập tên các script di chuyển bình thường muốn TẮT khi đi, hoặc muốn BẬT lại khi về.")]
        [SerializeField] private string[] disableComponentNames;

        [Header("Events")]
        [Tooltip("Sự kiện kích hoạt khi ĐI ĐẾN đích (tuỳ chọn)")]
        public UnityEngine.Events.UnityEvent onTeleportToDestination;
        [Tooltip("Sự kiện kích hoạt khi TRỞ VỀ vị trí cũ (tuỳ chọn)")]
        public UnityEngine.Events.UnityEvent onTeleportToReturn;

        private static Vector3 savedReturnPosition;

        private bool playerInRange;
        private Transform playerTransform;

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            if (promptUI != null) promptUI.SetActive(false);
        }

        private void Update()
        {
            if (playerInRange && Input.GetKeyDown(interactKey))
            {
                if (playerTransform != null)
                {
                    if (teleportType == TeleportType.GoToDestinationAndSaveReturn)
                    {
                        if (destination != null)
                        {
                            savedReturnPosition = playerTransform.position;
                            playerTransform.position = destination.position;
                            
                            MinigameCheckpointState.SetCheckpoint(destination.position);
                            ToggleNormalMovement(true);
                            HandleMinigameScript(true);

                            if (minigameRoot != null)
                            {
                                minigameRoot.SetActive(true);
                            }

                            onTeleportToDestination?.Invoke();
                        }
                        else
                        {
                            Debug.LogWarning("Chưa gán Destination cho LocalTeleporter!", this);
                        }
                    }
                    else if (teleportType == TeleportType.ReturnToSavedPosition)
                    {
                        MinigameCheckpointState.ClearCheckpoint();
                        playerTransform.position = savedReturnPosition;
                        
                        ToggleNormalMovement(false);
                        HandleMinigameScript(false);

                        if (minigameRoot != null)
                        {
                            minigameRoot.SetActive(false);
                        }

                        onTeleportToReturn?.Invoke();
                    }
                }
            }
        }

        private static float savedGravityScale = 1f;

        private void HandleMinigameScript(bool isEntering)
        {
            if (!autoAddRotaSurfaceWalker || playerTransform == null) return;

            var rb = playerTransform.GetComponent<Rigidbody2D>();

            var player = playerTransform.GetComponent<Player>();
            if (isEntering)
            {
                if (rb != null && rb.gravityScale > 0f)
                {
                    savedGravityScale = rb.gravityScale;
                }

                if (player != null)
                {
                    player.SetRotaMode(true);
                }
            }
            else
            {
                if (player != null)
                {
                    player.SetRotaMode(false);
                }

                // Khôi phục góc xoay thẳng, trọng lực (gravityScale) và triệt tiêu vận tốc cũ
                playerTransform.rotation = Quaternion.identity;
                if (rb != null)
                {
                    rb.gravityScale = savedGravityScale;
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }

                // Reset quay mặt của Spine Animation và Scale về hướng mặc định
                var spine = playerTransform.GetComponentInChildren<AnimationSpine>();
                if (spine != null)
                {
                    spine.SetFlipX(false);
                    spine.SetFlipY(false);
                }

                Vector3 scale = playerTransform.localScale;
                scale.x = Mathf.Abs(scale.x);
                playerTransform.localScale = scale;

                // Reset góc xoay của Camera về góc đứng thẳng mặc định (0,0,0)
                if (Camera.main != null)
                {
                    Camera.main.transform.rotation = Quaternion.identity;
                }

                var cinemachineCam = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
                if (cinemachineCam != null)
                {
                    cinemachineCam.transform.rotation = Quaternion.identity;
                }
            }
        }

        private static System.Collections.Generic.List<Behaviour> disabledBehaviours = new System.Collections.Generic.List<Behaviour>();

        private void ToggleNormalMovement(bool isEntering)
        {
            if (isEntering)
            {
                disabledBehaviours.Clear();
                if (disableComponentNames == null || playerTransform == null) return;

                var allBehaviours = playerTransform.GetComponentsInChildren<Behaviour>(true);

                foreach (string rawName in disableComponentNames)
                {
                    if (string.IsNullOrEmpty(rawName)) continue;
                    string cleanName = rawName.Replace(".cs", "").Replace("(Script)", "").Trim();

                    foreach (var b in allBehaviours)
                    {
                        if (b != null && b.enabled && b.GetType().Name.Equals(cleanName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            b.enabled = false;
                            disabledBehaviours.Add(b);
                        }
                    }
                }
            }
            else
            {
                // Khi trở về: tự động BẬT LẠI tất cả các script đã bị tắt lúc đi
                foreach (Behaviour b in disabledBehaviours)
                {
                    if (b != null)
                    {
                        b.enabled = true;
                    }
                }
                disabledBehaviours.Clear();
            }
        }

        private bool IsPlayer(Collider2D other, out Transform rootTransform)
        {
            rootTransform = null;
            if (targetPlayer != null)
            {
                rootTransform = targetPlayer;
                return other.transform.IsChildOf(targetPlayer) || other.transform == targetPlayer;
            }

            if (other == null || string.IsNullOrEmpty(playerTag)) return false;

            if (other.CompareTag(playerTag) || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag)) || other.transform.root.CompareTag(playerTag))
            {
                rootTransform = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;
                return true;
            }

            return false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsPlayer(other, out Transform playerRoot))
            {
                playerInRange = true;
                playerTransform = playerRoot;
                if (promptUI != null) promptUI.SetActive(true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (IsPlayer(other, out _))
            {
                playerInRange = false;
                playerTransform = null;
                if (promptUI != null) promptUI.SetActive(false);
            }
        }
    }
}

using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class SnowZone : MonoBehaviour
{
    [Header("Snowball Settings")]
    public GameObject snowballPrefab;

    [Header("Spawn Offset")]
    public float spawnDistance = 1.5f;
    public float spawnHeightOffset = 0f; // Độ lệch trục Y (dương = cao hơn, âm = thấp hơn)
    public bool reverseDirection = false; // Đảo ngược hướng spawn

    private bool isPlayerInZone = false;
    private GameObject currentPlayer;

    void Start()
    {
        // Đảm bảo box collider luôn là trigger để người chơi bước vào được
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Update()
    {
        // Nhấn F khi đang đứng trong vùng tuyết để tạo cục tuyết mới
        if (isPlayerInZone && Input.GetKeyDown(KeyCode.F))
        {
            if (snowballPrefab != null && currentPlayer != null)
            {
                // Luôn tính vị trí spawn theo người chơi (đã xoá spawnPoint)
                float facingDir;
                var playerComp = currentPlayer.GetComponent<Player>();
                if (playerComp != null)
                {
                    facingDir = playerComp.FacingDirectionSign;
                }
                else
                {
                    facingDir = currentPlayer.transform.localScale.x < 0f ? 1f : -1f;
                }
                if (reverseDirection) facingDir *= -1f;

                // Tính vị trí chân thực tế của người chơi (dùng bounds.min.y để tính đúng cả offset)
                float feetY = currentPlayer.transform.position.y;
                Collider2D playerCol = currentPlayer.GetComponent<Collider2D>();
                if (playerCol != null)
                {
                    feetY = playerCol.bounds.min.y;
                }

                // Lấy bán kính thực của cục đá (ưu tiên CircleCollider2D.radius)
                float rockRadius = 0.5f;
                IceRockRoller rollerComp = snowballPrefab.GetComponent<IceRockRoller>();
                CircleCollider2D circleCol = snowballPrefab.GetComponent<CircleCollider2D>();
                if (circleCol != null)
                {
                    rockRadius = circleCol.radius;
                }
                else
                {
                    Collider2D rockCol = snowballPrefab.GetComponent<Collider2D>();
                    if (rockCol != null) rockRadius = rockCol.bounds.extents.y;
                }

                if (rollerComp != null)
                {
                    feetY += rockRadius * rollerComp.initialScale;
                }

                Vector2 spawnPos = new Vector2(
                    currentPlayer.transform.position.x + facingDir * spawnDistance,
                    feetY + spawnHeightOffset
                );

                GameObject newSnowball = Instantiate(snowballPrefab, spawnPos, Quaternion.identity);
                IceRockRoller roller = newSnowball.GetComponent<IceRockRoller>();

                if (roller != null)
                {
                    roller.StartRolling();
                }

                Debug.Log("Đã tạo và bắt đầu lăn một cục tuyết mới!");
            }
            else
            {
                Debug.LogWarning("Chưa gán snowballPrefab vào SnowZone!");
            }
        }
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
        {
            isPlayerInZone = true;
            currentPlayer = col.gameObject;

            // Kích hoạt hướng dẫn (Thử lăn tuyết xem + Nhấn F) khi người chơi bước vào vùng tuyết
            if (SnowballDialogueManager.Instance != null && SnowballDialogueManager.Instance.IsPlayerReadyToRoll() && !SnowballDialogueManager.Instance.HasPushTutorialPlayed())
            {
                SnowballDialogueManager.Instance.TriggerPushDialogue();
            }
        }
    }

    void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
        {
            isPlayerInZone = false;
            currentPlayer = null;
        }
        else
        {
            // Nếu cục đá lăn ra khỏi vùng tuyết thì tự động chốt kích thước luôn
            IceRockRoller roller = col.GetComponent<IceRockRoller>();
            if (roller != null)
            {
                roller.CompleteRock();
            }
        }
    }
}

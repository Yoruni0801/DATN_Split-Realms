using UnityEngine;

public class MaterialChangerByRaycast : MonoBehaviour
{
    [Header("Cài đặt Đối tượng")]
    [Tooltip("Gắn tag của Player vào đây (mặc định là 'Player')")]
    public string playerTag = "Player";

    [Header("Cài đặt Khoảng cách & Material")]
    [Tooltip("Khoảng cách kích hoạt đổi Material (Lại gần)")]
    public float closeDistanceThreshold = 5f;

    [Tooltip("Material khi Player đứng xa")]
    public Material farMaterial;
    [Tooltip("Material khi Player lại gần")]
    public Material closeMaterial;

    [Header("Tùy chọn Raycast (Line of Sight)")]
    [Tooltip("Bật cái này nếu muốn dùng Raycast để kiểm tra xem có vật cản giữa vật thể và Player không")]
    public bool useRaycastLineOfSight = false;
    [Tooltip("Layer của các vật cản (nếu dùng Raycast)")]
    public LayerMask obstacleLayer;

    private Transform playerTransform;
    private Renderer myRenderer;

    void Start()
    {
        // Lấy component Renderer của chính vật thể này
        myRenderer = GetComponent<Renderer>();

        // Tìm Player trong scene dựa vào Tag
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            Debug.LogWarning("Không tìm thấy GameObject nào có tag: " + playerTag);
        }
    }

    void Update()
    {
        if (playerTransform == null || myRenderer == null) return;

        // Tính khoảng cách từ vật thể này đến Player
        float distance = Vector3.Distance(transform.position, playerTransform.position);

        bool isCloseEnough = distance <= closeDistanceThreshold;

        // Nếu người dùng muốn kiểm tra thêm bằng tia Raycast xem có bị chắn tường không
        if (isCloseEnough && useRaycastLineOfSight)
        {
            Vector3 directionToPlayer = playerTransform.position - transform.position;
            RaycastHit hit;

            // Vẽ tia debug trong Scene
            Debug.DrawRay(transform.position, directionToPlayer.normalized * distance, Color.yellow);

            if (Physics.Raycast(transform.position, directionToPlayer, out hit, distance, obstacleLayer))
            {
                // Nếu tia ray đập trúng một vật gì đó KHÔNG PHẢI là player trước khi tới player
                if (hit.transform != playerTransform)
                {
                    isCloseEnough = false; // Bị chắn -> coi như đứng xa
                }
            }
        }

        // Quyết định đổi Material
        if (isCloseEnough)
        {
            // Lại gần (và không bị chắn nếu dùng Raycast)
            if (myRenderer.sharedMaterial != closeMaterial)
            {
                myRenderer.material = closeMaterial;
            }
        }
        else
        {
            // Đứng xa
            if (myRenderer.sharedMaterial != farMaterial)
            {
                myRenderer.material = farMaterial;
            }
        }
    }

    // Vẽ hình cầu trong tab Scene để dễ căn chỉnh khoảng cách
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, closeDistanceThreshold);
    }
}

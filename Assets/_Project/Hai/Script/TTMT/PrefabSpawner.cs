using UnityEngine;

public class PrefabSpawner : MonoBehaviour
{
    [Header("Cấu hình Prefab")]
    [SerializeField] private GameObject prefabToSpawn;          // Prefab cần sinh ra
    [SerializeField] private Transform spawnPoint;              // Vị trí sinh ra (nếu trống sẽ lấy vị trí của chính script này)

    [Header("Cấu hình Tương tác")]
    [SerializeField] private bool requirePlayerRange = true;    // Có bắt buộc Player phải đứng trong vùng Collider (Trigger) không?
    [SerializeField] private bool spawnOnlyOnce = false;         // Chỉ cho phép sinh ra đúng 1 lần?

    private bool _isPlayerInRange = false;
    private bool _hasSpawned = false;

    void Update()
    {
        // Kiểm tra nếu thỏa mãn điều kiện nhấn phím F
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (requirePlayerRange && !_isPlayerInRange) return; // Nếu cần đứng gần nhưng player ở xa thì bỏ qua
            if (spawnOnlyOnce && _hasSpawned) return;            // Nếu chỉ được spawn 1 lần và đã spawn rồi thì bỏ qua

            SpawnPrefab();
        }
    }

    private void SpawnPrefab()
    {
        if (prefabToSpawn == null)
        {
            Debug.LogError($"[PrefabSpawner] {gameObject.name} chưa được gán Prefab để sinh ra!");
            return;
        }

        // Xác định vị trí spawn
        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        // Sinh ra Prefab
        GameObject spawnedObject = Instantiate(prefabToSpawn, position, rotation);
        _hasSpawned = true;

        Debug.Log($"[PrefabSpawner] Đã gọi Prefab '{spawnedObject.name}' thành công tại vị trí {position}!");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (requirePlayerRange && other.CompareTag("Player"))
        {
            _isPlayerInRange = true;
            Debug.Log($"[PrefabSpawner] Player vào vùng tương tác. Nhấn F để gọi Prefab.");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (requirePlayerRange && other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
            Debug.Log($"[PrefabSpawner] Player ra khỏi vùng tương tác.");
        }
    }
}

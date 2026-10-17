// MINIgame/PlayerInventory.cs
using UnityEngine;

using System; // Thêm thư viện System cho Action

/// <summary>
/// Túi đồ (Inventory) đơn giản dạng Singleton dùng để theo dõi số lượng cành cây nhặt được.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    private static PlayerInventory _instance;
    public static PlayerInventory Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("PlayerInventory");
                    _instance = obj.AddComponent<PlayerInventory>();
                }
            }
            return _instance;
        }
    }

    public Action OnBranchCountChanged; // Event gọi khi số lượng cành cây thay đổi

    private int _branchCount = 0; // Số cành cây đang có

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Được gọi từ Tree.cs khi cây bị chặt để tăng số lượng cành cây.
    /// </summary>
    public void AddBranch()
    {
        _branchCount++;
        Debug.Log($"Đã nhặt cành cây. Tổng cộng: {_branchCount}");
        OnBranchCountChanged?.Invoke(); // Bắn event
    }

    /// <summary>
    /// Thử sử dụng cành cây để sửa cầu. Trả về true nếu có đủ cành cây để dùng.
    /// </summary>
    public bool UseBranch()
    {
        if (_branchCount > 0)
        {
            _branchCount--;
            Debug.Log($"Đã dùng 1 cành cây. Còn lại: {_branchCount}");
            OnBranchCountChanged?.Invoke(); // Bắn event
            return true;
        }
        Debug.Log("Không còn cành cây nào để dùng.");
        return false;
    }

    // Biến công khai để các script khác có thể đọc số lượng cành cây
    public int BranchCount => _branchCount;
}

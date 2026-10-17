using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Hồ Nguyên Tố 2D (Hồ Lửa / Hồ Nước / Hồ Độc).
/// Tự động nhận diện nhân vật đi vào:
/// - Nếu đúng nguyên tố an toàn -> Cho phép đi qua / bơi.
/// - Nếu khác nguyên tố -> Nhân vật bị tiêu diệt / mất máu / respawn.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ElementalPool2D : MonoBehaviour
{
    public enum PoolType
    {
        FirePool,  // Hồ Lửa (Dung nham)
        WaterPool, // Hồ Nước
        ToxicPool  // Hồ Độc (Nguy hiểm cho tất cả)
    }

    [Header("Cấu hình Loại Hồ")]
    [SerializeField] private PoolType poolType = PoolType.WaterPool;

    [Header("Cấu hình Tag An Toàn & Nguy Hiểm")]
    [Tooltip("Các Tag nhân vật ĐƯỢC PHÉP an toàn đi qua (Ví dụ: FirePlayer với Hồ Lửa, WaterPlayer với Hồ Nước)")]
    [SerializeField] private string[] safeTags = { "WaterPlayer" };

    [Tooltip("Các Tag nhân vật SẼ BỊ CHẾT LẬP TỨC khi rơi vào (Ví dụ: FirePlayer với Hồ Nước, WaterPlayer với Hồ Lửa)")]
    [SerializeField] private string[] deadlyTags = { "FirePlayer", "Player" };

    [Header("Sát thương / Tiêu diệt")]
    [Tooltip("Tự động gây sát thương / tiêu diệt Player khi rơi vào?")]
    [SerializeField] private bool instantKill = true;
    [SerializeField] private int damageAmount = 9999;

    [Header("Sự kiện Tương tác (Âm thanh / Vfx Bốc hơi / Bọt nước)")]
    public UnityEvent OnSafeEnter;
    public UnityEvent OnDeadlyEnter;

    private void Awake()
    {
        // Đảm bảo Collider là Trigger để nhân vật đi xuyên qua bề mặt hồ
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. Kiểm tra nếu là đối tượng An Toàn
        if (IsMatchingTag(other, safeTags))
        {
            OnSafeEnter?.Invoke();
            return;
        }

        // 2. Kiểm tra nếu là đối tượng Nguy Hiểm (Bị tiêu diệt / Tan biến)
        if (poolType == PoolType.ToxicPool || IsMatchingTag(other, deadlyTags))
        {
            OnDeadlyEnter?.Invoke();
            KillOrDamage(other.gameObject);
        }
    }

    private bool IsMatchingTag(Collider2D other, string[] tags)
    {
        if (tags == null || tags.Length == 0) return false;

        foreach (string tag in tags)
        {
            if (other.CompareTag(tag)) return true;
            if (other.transform.parent != null && other.transform.parent.CompareTag(tag)) return true;
        }
        return false;
    }

    private void KillOrDamage(GameObject target)
    {
        // Tìm PlayerHealth trên Player (kể cả ở GameObject cha)
        PlayerHealth health = target.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(damageAmount);
            return;
        }

        // Nếu là thùng đẩy rơi vào hồ nguy hiểm -> Reset về lại vị trí ban đầu
        PushableBlock2D block = target.GetComponentInParent<PushableBlock2D>();
        if (block != null)
        {
            block.ResetToInitialPosition();
        }
    }
}

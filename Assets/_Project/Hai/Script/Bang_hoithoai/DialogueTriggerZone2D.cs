using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class DialogueTriggerZone2D : MonoBehaviour
{
    [Header("Cài đặt kích hoạt")]
    [Tooltip("Nếu tích chọn, đoạn hội thoại mở đầu chỉ kích hoạt duy nhất 1 lần khi đi qua")]
    [SerializeField] private bool playOnlyOnce = true;

    private bool hasPlayed = false;

    private void Awake()
    {
        // Tự động chuyển Collider sang chế độ Trigger để người chơi đi xuyên qua được
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Nếu đã chơi rồi và chỉ cho phép chơi 1 lần thì bỏ qua
        if (hasPlayed && playOnlyOnce) return;

        // Kiểm tra xem đối tượng va chạm có phải là Player không
        if (other.CompareTag("Player") || other.GetComponentInParent<Player>() != null)
        {
            if (SnowballDialogueManager.Instance != null)
            {
                hasPlayed = true;
                
                // Gọi hàm kích hoạt hội thoại mở đầu và lia camera
                SnowballDialogueManager.Instance.TriggerIntro();
                
                // Vô hiệu hóa script này để tránh gọi lại
                if (playOnlyOnce)
                {
                    enabled = false;
                }
            }
            else
            {
                Debug.LogWarning("[DialogueTriggerZone2D] Không tìm thấy SnowballDialogueManager.Instance trong Scene!");
            }
        }
    }
}

using UnityEngine;
using TMPro;
using DG.Tweening;

public class PlayerDamagePopup : MonoBehaviour
{
    private TMP_Text textMesh;

    private void Awake()
    {
        textMesh = GetComponent<TMP_Text>();
    }

    // Hàm Setup bây giờ rất ngắn gọn vì chỉ lo cho mỗi Player
    public void Setup(int damageAmount)
    {
        if (textMesh == null)
        {
            return;
        }

        textMesh.text = damageAmount.ToString();
        
        // 1. CẤU HÌNH DÀNH RIÊNG CHO PLAYER (Màu đỏ xót xa)
        textMesh.color = Color.red; 
        textMesh.fontSize += 1f;

        // 2. KỊCH BẢN HOẠT ẢNH BẰNG DOTWEEN (Animation Sequence)
        Vector3 originalScale = transform.localScale;
        transform.localScale = Vector3.zero; // Ép về 0 để chuẩn bị phóng to

        // Bay lên cao và lệch sang ngang một chút
        float randomX = Random.Range(-1.5f, 1.5f);
        transform.DOMove(transform.position + new Vector3(randomX, 2f, 0f), 0.8f).SetEase(Ease.OutCubic);

        // Tạo một chuỗi hành động (Sequence)
        Sequence seq = DOTween.Sequence();
        
        // Hiện ra nảy to lên (Bouncy)
        seq.Append(transform.DOScale(originalScale * 1.5f, 0.2f).SetEase(Ease.OutBack));
        // Thu lại kích thước gốc
        seq.Append(transform.DOScale(originalScale, 0.1f));
        // Đứng im cho người chơi kịp đọc số (0.3 giây)
        seq.AppendInterval(0.3f);
        // Mờ dần Alpha về 0
        seq.Append(textMesh.DOFade(0f, 0.2f));
        
        // Xoay xong thì tự hủy Object để giải phóng RAM
        seq.OnComplete(() => Destroy(gameObject));
    }
}

using UnityEngine;

/// <summary>
/// Chấm Vàng ghim tại vạch Target, tự động nhún nhảy theo cao độ 
/// của đường sóng điện tâm đồ cuộn từ bên phải sang.
/// </summary>
public class PulseDot : MonoBehaviour
{
    [SerializeField] private ParticleSystem hitParticlePrefab;

    private ECGBaseline baseline;
    private float targetX;
    private float startY;
    private float currentOffset = 0f;

    // Hàm khởi tạo nhận vị trí ghim Target X và tham chiếu tới Baseline mới
    public void Init(float targetX, float startY, ECGBaseline ecgBaseline)
    {
        this.targetX = targetX;
        this.startY = startY;
        this.baseline = ecgBaseline;
        
        // Ghim cố định vị trí X ngay tại vạch Target ban đầu
        transform.position = new Vector3(targetX, startY, transform.position.z);
    }

    private void Update()
    {
        if (baseline == null) return;

        // 1. Tự đồng bộ thời gian trôi với tốc độ cuộn của Baseline
        currentOffset += Time.deltaTime * baseline.scrollSpeed;
        
        // 2. Gọi ĐÚNG tên hàm mới 'GetWaveValueAt' để lấy độ cao Y tại điểm ghim targetX
        float currentY = startY + baseline.GetWaveValueAt(targetX, currentOffset);
        
        // 3. Cập nhật lại vị trí nhún nhảy theo trục Y
        transform.position = new Vector3(targetX, currentY, transform.position.z);
    }

    public void PlayHitEffect()
    {
        if (hitParticlePrefab != null)
        {
            var fx = Instantiate(hitParticlePrefab, transform.position, Quaternion.identity);
            Destroy(fx.gameObject, 2f);
        }
    }
}
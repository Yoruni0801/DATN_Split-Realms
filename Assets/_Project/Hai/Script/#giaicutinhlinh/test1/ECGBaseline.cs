using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ECGBaseline : MonoBehaviour
{
    [Header("Cấu hình Cuộn Sóng")]
    public float scrollSpeed = 8f;       // Tốc độ sóng trôi sang trái
    public float waveSpacing = 15f;      // Khoảng cách giữa cụm sóng (Số càng to khoảng phẳng càng dài)
    public float amplitude = 6f;         // Độ cao đỉnh sóng
    public float spikeWidth = 0.12f;     // Độ rộng nhịp tim

    [Header("Phạm vi hiển thị Trục X")]
    public float minX = -25f; 
    public float maxX = 25f;
    public int pointsCount = 350; 

    private LineRenderer lineRenderer;
    private float timeOffset = 0f;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Start()
    {
        lineRenderer.startWidth = 1.8f;
        lineRenderer.endWidth = 1.8f;
    }

    private void Update()
    {
        // Liên tục cuộn offset theo thời gian để sóng trôi sang trái
        timeOffset += Time.deltaTime * scrollSpeed;
        DrawScrollingECG();
    }

    private void DrawScrollingECG()
    {
        lineRenderer.positionCount = pointsCount;
        float step = (maxX - minX) / (pointsCount - 1);
        float startY = transform.position.y;

        for (int i = 0; i < pointsCount; i++)
        {
            float currentX = minX + (step * i);
            // Áp dụng vị trí X kết hợp timeOffset để đẩy sóng trôi đi
            float currentY = startY + GetWaveValueAt(currentX, timeOffset);

            lineRenderer.SetPosition(i, new Vector3(currentX, currentY, transform.position.z));
        }
    }

    // Hàm tính toán cao độ sóng tại một điểm X cụ thể dựa trên thời gian trôi
    public float GetWaveValueAt(float x, float offset)
    {
        // Dịch chuyển tọa độ dựa trên tốc độ cuộn
        float SampleX = x - offset;

        // Tạo khoảng phẳng lặng bằng cách dùng toán lặp khoảng rộng (waveSpacing)
        float p = Mathf.Repeat(SampleX + (waveSpacing * 0.5f), waveSpacing) - (waveSpacing * 0.5f);
        
        // Chuẩn hóa p về khoảng hẹp để đưa vào hàm Gauss chuẩn
        p /= waveSpacing; 

        // Khối 5 pha sóng Gauss y tế lấy từ code tham khảo
        float q = Gaussian(p, -spikeWidth * 0.8f, spikeWidth * 0.35f) * -0.35f;
        float r = Gaussian(p, 0f, spikeWidth * 0.22f) * 1.25f;
        float s = Gaussian(p, spikeWidth * 0.55f, spikeWidth * 0.28f) * -0.55f;
        float pWave = Gaussian(p, -0.18f, 0.045f) * 0.18f;
        float tWave = Gaussian(p, 0.23f, 0.08f) * 0.243f;

        // Nhân hệ số phóng đại mạnh để đẩy sóng cao lớn trên màn hình 4K
        return (q + r + s + pWave + tWave) * amplitude * 40f;
    }

    private static float Gaussian(float x, float center, float width)
    {
        if (width <= 0f) return 0f;
        float d = (x - center) / width;
        return Mathf.Exp(-d * d);
    }
}
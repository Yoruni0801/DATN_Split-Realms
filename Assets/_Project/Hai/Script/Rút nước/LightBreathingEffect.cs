using UnityEngine;
using UnityEngine.Rendering.Universal; // Cần namespace này để gọi Light2D

public class LightBreathingEffect : MonoBehaviour
{
    [Header("Light Components")]
    [SerializeField] private Light2D targetLight;

    [Header("Intensity Settings")]
    [Tooltip("Độ sáng ban đầu khi chưa có Player lại gần")]
    [SerializeField] private float idleIntensity = 0.3f;

    [Tooltip("Độ sáng tối đa khi lại gần")]
    [SerializeField] private float maxIntensity = 1.4f;

    [Tooltip("Mức giảm xuống thấp nhất trong nhịp thở")]
    [SerializeField] private float minPulseIntensity = 1.0f;

    [Header("Speed Settings")]
    [Tooltip("Tốc độ tăng sáng ban đầu")]
    [SerializeField] private float fadeInSpeed = 2f;

    [Tooltip("Tốc độ nhịp thở (tăng/giảm)")]
    [SerializeField] private float pulseSpeed = 2f;

    [Tooltip("Tốc độ giảm sáng khi Player rời đi")]
    [SerializeField] private float fadeOutSpeed = 2f;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    private bool isPlayerNearby = false;
    private bool hasReachedMax = false;
    private float pulseTimer = 0f;

    private void Reset()
    {
        // Tự động tìm Light2D nếu gắn script trực tiếp lên object đèn
        targetLight = GetComponent<Light2D>();
    }

    private void Start()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light2D>();

        if (targetLight != null)
            targetLight.intensity = idleIntensity;
    }

    private void Update()
    {
        if (targetLight == null) return;

        if (isPlayerNearby)
        {
            if (!hasReachedMax)
            {
                // Bước 1: Tăng sáng từ từ lên mức Max (1.4)
                targetLight.intensity = Mathf.MoveTowards(targetLight.intensity, maxIntensity, fadeInSpeed * Time.deltaTime);

                // Khi đã chạm mốc Max thì chuyển sang trạng thái "thở"
                if (Mathf.Approximately(targetLight.intensity, maxIntensity))
                {
                    hasReachedMax = true;
                    pulseTimer = 0f;
                }
            }
            else
            {
                // Bước 2: Tạo nhịp thở lặp đi lặp lại giữa minPulseIntensity (1.0) và maxIntensity (1.4)
                pulseTimer += Time.deltaTime * pulseSpeed;
                float t = (Mathf.Sin(pulseTimer) + 1f) / 2f; // Chuyển đổi Sin từ [-1, 1] về [0, 1] cho mượt
                targetLight.intensity = Mathf.Lerp(minPulseIntensity, maxIntensity, t);
            }
        }
        else
        {
            // Bước 3: Player rời xa -> Trả độ sáng về mức Idle ban đầu
            hasReachedMax = false;
            targetLight.intensity = Mathf.MoveTowards(targetLight.intensity, idleIntensity, fadeOutSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(playerTag))
        {
            isPlayerNearby = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag(playerTag))
        {
            isPlayerNearby = false;
        }
    }
}
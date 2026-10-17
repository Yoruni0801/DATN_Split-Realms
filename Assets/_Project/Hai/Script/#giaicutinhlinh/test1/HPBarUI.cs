using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thanh sinh lực của mini game. Thanh không nhảy theo từng mốc HP mà luôn
/// lướt tới giá trị đích; khi nhịp đang đi vào checkpoint, nó hạ nhẹ để tạo
/// cảm giác người chơi đang giữ sự sống cho sinh vật.
/// </summary>
public class HPBarUI : MonoBehaviour
{
    [SerializeField] private Slider hpSlider; // min=0, max=20 set trong Inspector
    [SerializeField] private Text hpText;     // tùy chọn, hiển thị số
    [Min(0f)]
    [SerializeField] private float smoothDuration = 0.35f;

    [Header("Cinematic Progress Motion")]
    [Tooltip("Tốc độ thanh lướt lên sau khi bấm trúng.")]
    [Min(0.01f)] [SerializeField] private float riseUnitsPerSecond = 2.4f;
    [Tooltip("Tốc độ thanh hạ xuống khi bấm sai hoặc bỏ lỡ nhịp.")]
    [Min(0.01f)] [SerializeField] private float fallUnitsPerSecond = 3.4f;
    [Tooltip("Mức tụt tối đa chỉ để biểu đạt áp lực khi nhịp đang ở checkpoint.")]
    [Min(0f)] [SerializeField] private float timingDipLimit = 0.38f;
    [Tooltip("Tốc độ tụt nhẹ khi người chơi chần chừ ở checkpoint.")]
    [Min(0f)] [SerializeField] private float timingDrainPerSecond = 0.62f;
    [Tooltip("Tốc độ hồi lại mức hiển thị khi nhịp đã được giải quyết.")]
    [Min(0f)] [SerializeField] private float timingRecoveryPerSecond = 1.6f;

    [Header("Energy FX")]
    [Tooltip("Tô sáng Fill của Slider theo nhịp và thêm quét năng lượng chạy bên trong.")]
    [SerializeField] private bool enableEnergyEffects = true;
    [SerializeField] private Color pressureTint = new Color(1f, 0.67f, 0.18f, 1f);

    private float logicalProgress;
    private float pressureProgress;
    private float sliderVelocity;
    private bool hasProgressValue;
    private bool timingPressureActive;
    private Image fillImage;
    private Color fillBaseColor;
    private ProgressBarEnergyFX energyOverlay;

    private void Start()
    {
        if (hpSlider != null)
        {
            // Giữ interactable = true để Slider luôn sáng rực rỡ, không bị mờ xám
            hpSlider.interactable = true;
            
            // Tắt Raycast Target trên toàn bộ ảnh/chữ của Slider để chuột bấm xuyên qua (không cho kéo)
            Graphic[] graphics = hpSlider.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic g in graphics)
            {
                g.raycastTarget = false;
            }
            
            // Tắt Navigation để không bị chọn nhầm bằng phím/gamepad
            var nav = hpSlider.navigation;
            nav.mode = Navigation.Mode.None;
            hpSlider.navigation = nav;

            if (enableEnergyEffects)
            {
                Transform fillTransform = hpSlider.fillRect;
                if (fillTransform != null)
                {
                    fillImage = fillTransform.GetComponent<Image>();
                    if (fillImage != null)
                    {
                        fillBaseColor = fillImage.color;
                        energyOverlay = fillTransform.GetComponent<ProgressBarEnergyFX>();
                        if (energyOverlay == null)
                        {
                            energyOverlay = fillTransform.gameObject.AddComponent<ProgressBarEnergyFX>();
                        }
                    }
                }
            }
        }
    }

    public void UpdateHP(int currentHP)
    {
        float maxHP = currentHP;

        if (hpSlider != null) 
        {
            // Tự động đồng bộ maxValue của Slider với GameManager
            if (GameManager.Instance != null)
            {
                hpSlider.maxValue = GameManager.Instance.MaxHP;
            }

            maxHP = hpSlider.maxValue;

            logicalProgress = Mathf.Clamp(currentHP, hpSlider.minValue, hpSlider.maxValue);
            if (!hasProgressValue)
            {
                // Giữ trạng thái hiện tại làm điểm xuất phát để lần khởi động đầu
                // cũng có chuyển động mềm, không bị snap tới mốc HP.
                pressureProgress = hpSlider.value;
                hasProgressValue = true;
            }

            // Nếu giá trị gameplay giảm thật sự, phần hiển thị không được phép ở
            // cao hơn HP mới. Với hit, nó được phép lướt từ vị trí hiện tại lên.
            pressureProgress = Mathf.Min(pressureProgress, logicalProgress);
        }

        if (hpText != null)
        {
            hpText.text = $"{currentHP} / {maxHP}";
        }
    }

    /// <summary>
    /// Được GameManager gọi khi nhịp vàng tiến vào vùng cần bấm. Đây chỉ là
    /// phản hồi hình ảnh; HP thật vẫn chỉ thay đổi khi người chơi hit/miss.
    /// </summary>
    public void SetTimingPressure(bool isActive)
    {
        timingPressureActive = isActive;
    }

    private void Update()
    {
        if (hpSlider == null || !hasProgressValue)
        {
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;

        if (timingPressureActive)
        {
            float minimumPressureValue = Mathf.Max(hpSlider.minValue, logicalProgress - timingDipLimit);
            pressureProgress = Mathf.MoveTowards(
                pressureProgress,
                minimumPressureValue,
                timingDrainPerSecond * deltaTime);
        }
        else
        {
            pressureProgress = Mathf.MoveTowards(
                pressureProgress,
                logicalProgress,
                timingRecoveryPerSecond * deltaTime);
        }

        float visualTarget = Mathf.Min(logicalProgress, pressureProgress);
        float speed = visualTarget >= hpSlider.value ? riseUnitsPerSecond : fallUnitsPerSecond;

        // SmoothDamp cho đuôi lướt mềm, còn max speed giữ thanh không bao giờ
        // nhảy qua các thay đổi lớn từ event bên ngoài.
        hpSlider.value = smoothDuration <= 0f
            ? Mathf.MoveTowards(hpSlider.value, visualTarget, speed * deltaTime)
            : Mathf.SmoothDamp(
                hpSlider.value,
                visualTarget,
                ref sliderVelocity,
                smoothDuration,
                speed,
                deltaTime);

        UpdateEnergyEffects();
    }

    private void UpdateEnergyEffects()
    {
        if (!enableEnergyEffects)
        {
            return;
        }

        float movement = Mathf.Clamp01(Mathf.Abs(sliderVelocity) * 0.35f);
        float pressure = timingPressureActive ? 1f : 0f;
        if (fillImage != null)
        {
            float pulse = 0.05f + movement * 0.18f + pressure * (0.1f + Mathf.PingPong(Time.unscaledTime * 1.8f, 0.12f));
            Color target = Color.Lerp(fillBaseColor, pressureTint, pressure * 0.45f);
            fillImage.color = Color.Lerp(target, Color.white, pulse);
        }

        if (energyOverlay != null)
        {
            energyOverlay.SetEnergyState(pressure, movement);
        }
    }

    private void OnDisable()
    {
        timingPressureActive = false;
        sliderVelocity = 0f;
        if (fillImage != null)
        {
            fillImage.color = fillBaseColor;
        }
    }
}

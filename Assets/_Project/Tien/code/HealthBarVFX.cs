using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// SCRIPT: HealthBarVFX
// Mô tả: Điều khiển tất cả hiệu ứng VFX cho thanh máu người chơi.
//   - Rung thanh máu khi nhận sát thương (Shake)
//   - Co giãn khi mất máu (Scale Punch)
//   - Flash shader khi bị đánh
//   - Đồng bộ FillAmount cho shader
//   - Viền đổi màu khi máu thấp
//   - Particle burst khi mất máu
// 
// CÁCH SỬ DỤNG:
//   1. Gắn script này vào cùng GameObject có PlayerHealth
//   2. Kéo thả các Image thanh máu vào
//   3. Tạo Material mới từ shader "Custom/UI/HealthBarEffect" → gán cho mainHealthBar
//   4. Tạo Material mới từ shader "Custom/UI/HealthBarDelayedEffect" → gán cho delayedHealthBar
//   5. Điều chỉnh các thông số trong Inspector
// ============================================================================
public class HealthBarVFX : MonoBehaviour
{
    // =====================================================
    //  REFERENCES (Kéo thả từ Inspector)
    // =====================================================
    [Header("Tham chiếu UI")]
    [Tooltip("Image thanh máu chính (dùng shader HealthBarEffect)")]
    public Image mainHealthBar;

    [Tooltip("Image thanh máu delayed/trailing (dùng shader HealthBarDelayedEffect)")]
    public Image delayedHealthBar;

    [Tooltip("RectTransform cha bao bọc toàn bộ thanh máu (dùng để Shake)")]
    public RectTransform healthBarContainer;

    [Tooltip("Image viền ngoài thanh máu (tùy chọn, đổi màu khi máu thấp)")]
    public Image healthBarBorder;

    // =====================================================
    //  HIỆU ỨNG RUNG (SHAKE)
    // =====================================================
    [Header("Hiệu ứng Rung khi bị đánh (Shake)")]
    [Tooltip("Cường độ rung (pixel)")]
    public float shakeIntensity = 8f;

    [Tooltip("Thời gian rung (giây)")]
    public float shakeDuration = 0.25f;

    [Tooltip("Tần suất rung (lần/giây)")]
    public float shakeFrequency = 35f;

    [Tooltip("Tốc độ giảm dần rung")]
    public float shakeDamping = 5f;

    // =====================================================
    //  HIỆU ỨNG CO GIÃN (SCALE PUNCH)
    // =====================================================
    [Header("Hiệu ứng Co giãn khi mất máu (Scale Punch)")]
    [Tooltip("Độ phóng to khi bị đánh (VD: 1.15 = phóng 15%)")]
    public float punchScale = 1.12f;

    [Tooltip("Tốc độ thu nhỏ về bình thường")]
    public float punchRecoverSpeed = 8f;

    // =====================================================
    //  HIỆU ỨNG FLASH (SHADER)
    // =====================================================
    [Header("Hiệu ứng Flash khi bị đánh (Shader)")]
    [Tooltip("Thời gian flash trắng/đỏ (giây)")]
    public float flashDuration = 0.3f;

    [Tooltip("Cường độ flash tối đa")]
    [Range(0f, 1f)]
    public float flashMaxIntensity = 0.7f;

    // =====================================================
    //  HIỆU ỨNG VIỀN MÁU THẤP
    // =====================================================
    [Header("Hiệu ứng Viền khi máu thấp")]
    [Tooltip("% máu bắt đầu cảnh báo viền")]
    [Range(0f, 1f)]
    public float lowHealthThreshold = 0.3f;

    [Tooltip("Màu viền khi máu thấp")]
    public Color lowHealthBorderColor = new Color(0.9f, 0.1f, 0.05f, 1f);

    [Tooltip("Màu viền bình thường")]
    public Color normalBorderColor = new Color(0.15f, 0.15f, 0.2f, 1f);

    [Tooltip("Tốc độ nhấp nháy viền")]
    public float borderPulseSpeed = 4f;

    // =====================================================
    //  HIỆU ỨNG PARTICLE (TÙY CHỌN)
    // =====================================================
    [Header("Particle khi mất máu (Tùy chọn)")]
    [Tooltip("ParticleSystem gắn lên thanh máu (sinh tia lửa/mảnh vỡ)")]
    public ParticleSystem damageParticles;

    [Tooltip("Số hạt mỗi lần mất máu")]
    public int particleBurstCount = 12;

    // =====================================================
    //  HIỆU ỨNG SMOOTH FILL (THANH MÁU CHÍNH)
    // =====================================================
    [Header("Smooth Fill cho thanh máu chính")]
    [Tooltip("Bật/tắt hiệu ứng fill mượt (lerp) cho thanh chính")]
    public bool useSmoothFill = true;

    [Tooltip("Tốc độ fill mượt")]
    public float smoothFillSpeed = 5f;

    // =====================================================
    //  BIẾN NỘI BỘ
    // =====================================================
    private Material mainBarMat;
    private Material delayedBarMat;
    private Vector2 originalContainerPos;
    private Vector3 originalContainerScale;

    // Shake
    private float shakeTimer;
    private float currentShakeIntensity;

    // Flash
    private float flashTimer;

    // Scale Punch
    private float currentPunchScale = 1f;

    // Smooth Fill
    private float displayedFill = 1f;
    private float targetFill = 1f;

    // Border pulse
    private Color currentBorderColor;

    // Cache shader property IDs (tối ưu hiệu năng)
    private static readonly int ID_FillAmount   = Shader.PropertyToID("_FillAmount");
    private static readonly int ID_FlashAmount  = Shader.PropertyToID("_FlashAmount");

    // =====================================================
    //  UNITY LIFECYCLE
    // =====================================================
    private void Awake()
    {
        // Tạo Material instance để không ảnh hưởng Material gốc
        if (mainHealthBar != null && mainHealthBar.material != null)
        {
            mainBarMat = Instantiate(mainHealthBar.material);
            mainHealthBar.material = mainBarMat;
        }

        if (delayedHealthBar != null && delayedHealthBar.material != null)
        {
            delayedBarMat = Instantiate(delayedHealthBar.material);
            delayedHealthBar.material = delayedBarMat;
        }

        // Lưu vị trí gốc
        if (healthBarContainer != null)
        {
            originalContainerPos = healthBarContainer.anchoredPosition;
            originalContainerScale = healthBarContainer.localScale;
        }

        currentBorderColor = normalBorderColor;
    }

    private void Update()
    {
        UpdateShake();
        UpdateFlash();
        UpdateScalePunch();
        UpdateSmoothFill();
        UpdateBorderEffect();
        SyncShaderProperties();
    }

    private void OnDestroy()
    {
        // Dọn dẹp Material instance tránh memory leak
        if (mainBarMat != null) Destroy(mainBarMat);
        if (delayedBarMat != null) Destroy(delayedBarMat);
    }

    // =====================================================
    //  PUBLIC API (Gọi từ PlayerHealth)
    // =====================================================

    /// <summary>
    /// Gọi khi người chơi nhận sát thương. Kích hoạt tất cả hiệu ứng.
    /// </summary>
    /// <param name="newHealthPercent">% máu mới (0 → 1)</param>
    /// <param name="damagePercent">% sát thương vừa nhận (0 → 1)</param>
    public void PlayDamageEffect(float newHealthPercent, float damagePercent)
    {
        targetFill = newHealthPercent;

        // Shake - cường độ tỉ lệ với sát thương nhận
        float shakeMultiplier = Mathf.Clamp01(damagePercent * 3f);
        TriggerShake(shakeIntensity * (0.5f + shakeMultiplier * 0.5f));

        // Flash
        TriggerFlash();

        // Scale Punch
        TriggerScalePunch();

        // Particles
        TriggerParticles();
    }

    /// <summary>
    /// Gọi khi hồi sinh hoặc hồi máu. Reset tất cả hiệu ứng.
    /// </summary>
    /// <param name="healthPercent">% máu hiện tại (0 → 1)</param>
    public void ResetEffects(float healthPercent)
    {
        targetFill = healthPercent;
        displayedFill = healthPercent;

        // Reset shake
        shakeTimer = 0f;
        if (healthBarContainer != null)
        {
            healthBarContainer.anchoredPosition = originalContainerPos;
            healthBarContainer.localScale = originalContainerScale;
        }

        // Reset flash
        flashTimer = 0f;
        if (mainBarMat != null) mainBarMat.SetFloat(ID_FlashAmount, 0f);

        // Reset scale
        currentPunchScale = 1f;
    }

    /// <summary>
    /// Cập nhật fill khi máu thay đổi không do sát thương (hồi máu, buff).
    /// </summary>
    public void SetTargetFill(float healthPercent)
    {
        targetFill = healthPercent;
    }

    // =====================================================
    //  CẬP NHẬT HIỆU ỨNG RUNG (SHAKE)
    // =====================================================
    private void TriggerShake(float intensity)
    {
        shakeTimer = shakeDuration;
        currentShakeIntensity = intensity;
    }

    private void UpdateShake()
    {
        if (healthBarContainer == null || shakeTimer <= 0f) return;

        shakeTimer -= Time.deltaTime;
        float dampFactor = Mathf.Exp(-shakeDamping * (shakeDuration - shakeTimer));
        float shakeAmount = currentShakeIntensity * dampFactor;

        // Rung theo cả X và Y với tần suất cao
        float offsetX = Mathf.Sin(Time.time * shakeFrequency) * shakeAmount;
        float offsetY = Mathf.Cos(Time.time * shakeFrequency * 1.3f) * shakeAmount * 0.6f;

        healthBarContainer.anchoredPosition = originalContainerPos + new Vector2(offsetX, offsetY);

        // Hết rung → về vị trí gốc
        if (shakeTimer <= 0f)
        {
            healthBarContainer.anchoredPosition = originalContainerPos;
        }
    }

    // =====================================================
    //  CẬP NHẬT HIỆU ỨNG FLASH
    // =====================================================
    private void TriggerFlash()
    {
        flashTimer = flashDuration;
    }

    private void UpdateFlash()
    {
        if (mainBarMat == null) return;

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            // Ease-out: flash mạnh lúc đầu → giảm dần mượt
            float t = flashTimer / flashDuration;
            float flashValue = t * t * flashMaxIntensity; // Quadratic ease-out
            mainBarMat.SetFloat(ID_FlashAmount, flashValue);
        }
        else
        {
            mainBarMat.SetFloat(ID_FlashAmount, 0f);
        }
    }

    // =====================================================
    //  CẬP NHẬT HIỆU ỨNG CO GIÃN (SCALE PUNCH)
    // =====================================================
    private void TriggerScalePunch()
    {
        currentPunchScale = punchScale;
    }

    private void UpdateScalePunch()
    {
        if (healthBarContainer == null) return;

        if (Mathf.Abs(currentPunchScale - 1f) > 0.001f)
        {
            currentPunchScale = Mathf.Lerp(currentPunchScale, 1f, punchRecoverSpeed * Time.deltaTime);
            healthBarContainer.localScale = originalContainerScale * currentPunchScale;
        }
        else if (currentPunchScale != 1f)
        {
            currentPunchScale = 1f;
            healthBarContainer.localScale = originalContainerScale;
        }
    }

    // =====================================================
    //  CẬP NHẬT SMOOTH FILL
    // =====================================================
    private void UpdateSmoothFill()
    {
        if (mainHealthBar == null) return;

        if (useSmoothFill)
        {
            displayedFill = Mathf.Lerp(displayedFill, targetFill, smoothFillSpeed * Time.deltaTime);
            // Snap khi gần đủ
            if (Mathf.Abs(displayedFill - targetFill) < 0.002f)
                displayedFill = targetFill;

            mainHealthBar.fillAmount = displayedFill;
        }
        else
        {
            displayedFill = targetFill;
        }
    }

    // =====================================================
    //  CẬP NHẬT VIỀN MÁU THẤP
    // =====================================================
    private void UpdateBorderEffect()
    {
        if (healthBarBorder == null) return;

        if (displayedFill <= lowHealthThreshold && displayedFill > 0f)
        {
            // Nhấp nháy giữa màu cảnh báo và tối
            float pulse = Mathf.Sin(Time.time * borderPulseSpeed) * 0.5f + 0.5f;
            float strength = 1f - (displayedFill / lowHealthThreshold);
            Color targetColor = Color.Lerp(normalBorderColor, lowHealthBorderColor, strength);
            Color pulsedColor = Color.Lerp(targetColor * 0.6f, targetColor, pulse);
            currentBorderColor = Color.Lerp(currentBorderColor, pulsedColor, 8f * Time.deltaTime);
        }
        else
        {
            currentBorderColor = Color.Lerp(currentBorderColor, normalBorderColor, 5f * Time.deltaTime);
        }

        healthBarBorder.color = currentBorderColor;
    }

    // =====================================================
    //  ĐỒNG BỘ SHADER PROPERTIES
    // =====================================================
    private void SyncShaderProperties()
    {
        // Đồng bộ FillAmount vào shader để shader biết vị trí fill (cho edge glow, shimmer...)
        if (mainBarMat != null)
        {
            mainBarMat.SetFloat(ID_FillAmount, displayedFill);
        }

        // Đồng bộ FillAmount cho delayed bar shader
        if (delayedBarMat != null && delayedHealthBar != null)
        {
            delayedBarMat.SetFloat(ID_FillAmount, delayedHealthBar.fillAmount);
        }
    }

    // =====================================================
    //  PARTICLE BURST
    // =====================================================
    private void TriggerParticles()
    {
        if (damageParticles == null) return;

        var burst = new ParticleSystem.Burst(0f, particleBurstCount);
        damageParticles.emission.SetBurst(0, burst);
        damageParticles.Play();
    }
}

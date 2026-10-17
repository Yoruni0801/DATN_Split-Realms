using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider mainHealthSlider;
    [SerializeField] private Slider delayedHealthSlider;
    [SerializeField] private Image mainHealthFillImage; // Assign the 'Fill' image of the main slider here if you want color change
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Settings")]
    [SerializeField] private Gradient healthColor;
    [SerializeField] private float drainSpeed = 1.5f;

    private float delayedHealth;
    private int currentMaxHealth = 100;

    private int cachedCurrentHealthText = -1;
    private int cachedMaxHealthText = -1;
    private float cachedMaterialFillAmount = -1f;

    private void Awake()
    {
        // Ensure sliders are set from 0 to 1 since we use percentage
        if (mainHealthSlider != null)
        {
            mainHealthSlider.minValue = 0f;
            mainHealthSlider.maxValue = 1f;
        }
        if (delayedHealthSlider != null)
        {
            delayedHealthSlider.minValue = 0f;
            delayedHealthSlider.maxValue = 1f;
        }
    }

    private void Start()
    {
        ResetAndRefreshUI();
    }

    private void OnEnable()
    {
        PlayerHealth.OnGlobalHealthChanged += UpdateMainHealth;
        ResetAndRefreshUI();
    }

    private void OnDisable()
    {
        PlayerHealth.OnGlobalHealthChanged -= UpdateMainHealth;
    }

    public void ResetAndRefreshUI()
    {
        cachedCurrentHealthText = -1;
        cachedMaxHealthText = -1;
        cachedMaterialFillAmount = -1f;

        int current = PlayerHealth.GlobalCurrentHealth > 0 ? PlayerHealth.GlobalCurrentHealth : 100;
        int max = Mathf.Max(1, PlayerHealth.GlobalMaxHealth > 0 ? PlayerHealth.GlobalMaxHealth : 100);

        delayedHealth = current;
        currentMaxHealth = max;

        UpdateMainHealth(current, max);
    }

    private void Update()
    {
        float currentTargetHealth = mainHealthSlider != null ? mainHealthSlider.value * currentMaxHealth : 0f;
        if (delayedHealth <= currentTargetHealth)
        {
            return;
        }

        delayedHealth = Mathf.Max(currentTargetHealth, delayedHealth - drainSpeed * currentMaxHealth * Time.deltaTime);
        SetDelayedFill(delayedHealth, currentMaxHealth);
    }

    private void UpdateMainHealth(int currentHealth, int maxHealth)
    {
        currentMaxHealth = Mathf.Max(1, maxHealth); // Tránh chia cho 0
        if (currentHealth >= delayedHealth)
        {
            delayedHealth = currentHealth;
        }

        float percent = GetPercent(currentHealth, currentMaxHealth);

        if (healthText != null && (cachedCurrentHealthText != currentHealth || cachedMaxHealthText != maxHealth))
        {
            healthText.text = $"{currentHealth}/{maxHealth}";
            cachedCurrentHealthText = currentHealth;
            cachedMaxHealthText = maxHealth;
        }

        if (mainHealthSlider != null)
        {
            mainHealthSlider.value = percent;
        }

        if (mainHealthFillImage != null)
        {
            Color targetColor = healthColor.Evaluate(percent);
            if (mainHealthFillImage.color != targetColor)
            {
                mainHealthFillImage.color = targetColor;
            }

            // Gửi giá trị % máu vào Material để Shader tự đổi màu
            if (mainHealthFillImage.material != null && cachedMaterialFillAmount != percent)
            {
                mainHealthFillImage.material.SetFloat("_FillAmount", percent);
                cachedMaterialFillAmount = percent;
            }
        }

        SetDelayedFill(delayedHealth, currentMaxHealth);
    }

    private void SetDelayedFill(float currentHealth, int maxHealth)
    {
        if (delayedHealthSlider != null)
        {
            float percent = GetPercent(currentHealth, maxHealth);
            delayedHealthSlider.value = percent;
        }
    }

    private static float GetPercent(float currentHealth, int maxHealth)
    {
        return maxHealth <= 0 ? 0f : currentHealth / (float)maxHealth;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthDisplayUI : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private bool findPlayerOnStart = true;

    [Header("Health UI")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Display")]
    [SerializeField] private bool showMaxHealth = true;
    [SerializeField] private bool smoothFill = true;
    [SerializeField] private float fillSpeed = 8f;

    private float targetPercent = 1f;
    private float targetWidth;
    private float fullWidth;
    private float healthFillPivotX;
    private Vector2 fullAnchoredPosition;
    private RectTransform healthFillRect;
    private PlayerHealth boundHealth;

    private void Awake()
    {
        CacheHealthBarSize();
    }

    private void OnEnable()
    {
        PlayerRegistry.OnCurrentChanged += HandleCurrentPlayerChanged;
        BindToPlayer(PlayerRegistry.Current);
    }

    private void OnDisable()
    {
        PlayerRegistry.OnCurrentChanged -= HandleCurrentPlayerChanged;
        UnbindHealth();
    }

    private void Update()
    {
        if (!smoothFill || healthFillRect == null)
        {
            return;
        }

        float currentWidth = healthFillRect.rect.width;
        float nextWidth = Mathf.MoveTowards(
            currentWidth,
            targetWidth,
            fillSpeed * fullWidth * Time.deltaTime);

        SetHealthBarWidth(nextWidth);
    }

    private void UpdateHealth(int currentHealth, int maxHealth)
    {
        if (healthFillRect == null)
        {
            CacheHealthBarSize();
        }

        targetPercent = GetHealthPercent(currentHealth, maxHealth);
        targetWidth = fullWidth * targetPercent;

        if (!smoothFill)
        {
            SetHealthBarWidth(targetWidth);
        }

        if (healthText != null)
        {
            healthText.text = showMaxHealth
                ? $"{currentHealth}/{maxHealth}"
                : currentHealth.ToString();
        }
    }

    private void HandleCurrentPlayerChanged(Player player)
    {
        BindToPlayer(player);
    }

    private void BindToPlayer(Player player)
    {
        UnbindHealth();

        PlayerHealth nextHealth = null;
        if (player != null)
        {
            nextHealth = player.GetComponent<PlayerHealth>();
        }
        else if (findPlayerOnStart)
        {
            Player foundPlayer = PlayerRegistry.Current ?? FindFirstObjectByType<Player>();
            if (foundPlayer != null)
            {
                nextHealth = foundPlayer.GetComponent<PlayerHealth>();
            }
        }

        if (nextHealth == null)
        {
            return;
        }

        boundHealth = nextHealth;
        playerHealth = boundHealth;
        boundHealth.OnHealthChanged += UpdateHealth;
        UpdateHealth(boundHealth.CurrentHealth, boundHealth.MaxHealth);
    }

    private void UnbindHealth()
    {
        if (boundHealth != null)
        {
            boundHealth.OnHealthChanged -= UpdateHealth;
            boundHealth = null;
        }
    }

    private void CacheHealthBarSize()
    {
        if (healthFillImage == null)
        {
            return;
        }

        healthFillRect = healthFillImage.rectTransform;
        healthFillPivotX = healthFillRect.pivot.x;
        fullAnchoredPosition = healthFillRect.anchoredPosition;
        fullWidth = healthFillRect.rect.width;
        if (fullWidth <= 0f)
        {
            fullWidth = healthFillRect.sizeDelta.x;
        }

        targetWidth = fullWidth * targetPercent;
    }

    private void SetHealthBarWidth(float width)
    {
        if (healthFillRect == null)
        {
            return;
        }

        healthFillRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            Mathf.Max(0f, width));

        Vector2 anchoredPosition = fullAnchoredPosition;
        anchoredPosition.x -= healthFillPivotX * (fullWidth - Mathf.Max(0f, width));
        healthFillRect.anchoredPosition = anchoredPosition;
    }

    private static float GetHealthPercent(int currentHealth, int maxHealth)
    {
        if (maxHealth <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)currentHealth / maxHealth);
    }
}

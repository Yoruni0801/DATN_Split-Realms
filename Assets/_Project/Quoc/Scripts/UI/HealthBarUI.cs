using UnityEngine;
using UnityEngine.UI;

public class EnemyHealth : MonoBehaviour
{
    public float maxHP = 100;
    public float currentHP;

    [Header("UI")]
    public Slider hpBar;
    public CanvasGroup canvasGroup;

    private float hideTimer;
    public float showTime = 3f;

    void Start()
    {
        currentHP = maxHP;
        hpBar.maxValue = maxHP;
        hpBar.value = maxHP;

        canvasGroup.alpha = 0; // ẩn lúc đầu
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;
        hpBar.value = currentHP;

        // hiện thanh máu
        canvasGroup.alpha = 1;
        hideTimer = showTime;

        if (currentHP <= 0)
        {
            Die();
        }
    }

    void Update()
    {
        // đếm thời gian ẩn
        if (canvasGroup.alpha > 0)
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0)
            {
                canvasGroup.alpha -= Time.deltaTime; // fade out
            }
        }
    }

    void Die()
    {
        Destroy(gameObject);
    }
}
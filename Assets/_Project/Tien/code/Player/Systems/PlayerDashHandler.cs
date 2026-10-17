using System.Collections;
using UnityEngine;

public class PlayerDashHandler : MonoBehaviour
{
    private PlayerHealth health;
    private Collider2D bodyCollider;

    public void Configure(PlayerHealth health, Collider2D bodyCollider)
    {
        this.health = health;
        this.bodyCollider = bodyCollider;
    }

    /// <summary>Bắt đầu IFrame routine khi dash thành công.</summary>
    public void StartDashIFrame()
    {
        StartCoroutine(DashIFrameRoutine());
    }

    private IEnumerator DashIFrameRoutine()
    {
        health.SetInvincible(true);
        SetEnemyCollisionsIgnored(true);

        yield return new WaitForSeconds(0.4f);

        health.SetInvincible(false);
        SetEnemyCollisionsIgnored(false);
    }

    private void SetEnemyCollisionsIgnored(bool ignore)
    {
        if (bodyCollider == null)
        {
            return;
        }

        Bao.Enemies.BaoEnemyBase[] enemies = FindObjectsByType<Bao.Enemies.BaoEnemyBase>(FindObjectsSortMode.None);
        foreach (Bao.Enemies.BaoEnemyBase enemy in enemies)
        {
            if (enemy != null && enemy.Collider2D != null)
            {
                Physics2D.IgnoreCollision(bodyCollider, enemy.Collider2D, ignore);
            }
        }
    }
}

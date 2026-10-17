using UnityEngine;
using System.Collections.Generic;

public class TrapDamage : MonoBehaviour
{
    public float damage = 20f;
    public float cooldown = 1f;

    private Dictionary<EnemyHealth, float> lastHitTime = new Dictionary<EnemyHealth, float>();

    private void OnTriggerStay2D(Collider2D other)
    {
        EnemyHealth enemy = other.GetComponent<EnemyHealth>();

        if (enemy == null) return;

        if (!lastHitTime.ContainsKey(enemy))
        {
            lastHitTime[enemy] = 0;
        }

        if (Time.time - lastHitTime[enemy] >= cooldown)
        {
            enemy.TakeDamage(damage);
            lastHitTime[enemy] = Time.time;
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HitboxDamage : MonoBehaviour
{
    [Header("Hitbox")]
    [SerializeField] private bool continuous;
    [SerializeField] private float damageInterval = 0.2f;
    [SerializeField] private bool singleTarget;

    private Player owner;
    private SkillHitData hitData;
    private readonly List<IEnemyTarget> enemiesInZone = new List<IEnemyTarget>();
    private readonly Dictionary<IEnemyTarget, float> lastHitTime = new Dictionary<IEnemyTarget, float>();
    private bool hasTarget;

    public void Configure(Player player, SkillHitData hitData)
    {
        owner = player;
        this.hitData = hitData;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!TryGetEnemy(collision, out IEnemyTarget enemy))
        {
            return;
        }

        if (singleTarget && hasTarget)
        {
            return;
        }

        if (!enemiesInZone.Contains(enemy))
        {
            enemiesInZone.Add(enemy);
            hasTarget = true;

            if (!continuous)
            {
                ProcessHit(enemy);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (TryGetEnemy(collision, out IEnemyTarget enemy))
        {
            enemiesInZone.Remove(enemy);
            lastHitTime.Remove(enemy);
        }
    }

    private void Update()
    {
        if (!continuous)
        {
            return;
        }

        enemiesInZone.RemoveAll(enemy => enemy == null || !enemy.IsAlive);
        foreach (IEnemyTarget enemy in enemiesInZone)
        {
            if (!lastHitTime.TryGetValue(enemy, out float lastHit) || Time.time - lastHit >= damageInterval)
            {
                ProcessHit(enemy);
                lastHitTime[enemy] = Time.time;
            }
        }
    }

    private void ProcessHit(IEnemyTarget enemy)
    {
        if (owner == null || hitData.Skill == null)
        {
            return;
        }

        owner.ApplySkillHit(hitData, enemy, transform.position);
    }

    private static bool TryGetEnemy(Collider2D collider, out IEnemyTarget enemy)
    {
        if (PlayerHitResolver.IsThreatSensorCollider(collider))
        {
            enemy = null;
            return false;
        }

        enemy = collider == null ? null : collider.GetComponentInParent<IEnemyTarget>();
        return enemy != null && enemy.IsAlive;
    }
}

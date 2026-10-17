using UnityEngine;

public sealed class BossCombatAdapter
{
    private readonly BossAttackHitbox attackHitbox;
    private readonly Transform owner;

    public BossCombatAdapter(BossAttackHitbox attackHitbox, Transform owner)
    {
        this.attackHitbox = attackHitbox;
        this.owner = owner;
    }

    public bool HasHitbox => attackHitbox != null;

    public void BeginWeaponDamage(float damage, int direction, bool canBeBlocked)
    {
        if (attackHitbox == null)
        {
            return;
        }

        attackHitbox.BeginAttack(Mathf.RoundToInt(damage), owner, canBeBlocked);
    }

    public void EndWeaponDamage()
    {
        if (attackHitbox == null)
        {
            return;
        }

        attackHitbox.CancelAttack();
    }

    public GameObject SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return prefab != null ? Object.Instantiate(prefab, position, rotation) : null;
    }
}

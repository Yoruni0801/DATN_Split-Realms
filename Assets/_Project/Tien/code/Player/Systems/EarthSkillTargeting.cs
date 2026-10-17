using UnityEngine;

public static class EarthSkillTargeting
{
    public static Collider2D ResolveClosestEnemy(Player owner, Vector3 origin, float radius, LayerMask targetLayer)
    {
        if (owner == null || radius <= 0f)
        {
            return null;
        }

        Collider2D[] hits = targetLayer.value != 0
            ? Physics2D.OverlapCircleAll(origin, radius, targetLayer)
            : Physics2D.OverlapCircleAll(origin, radius);

        Collider2D closest = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (FairyBeamSkillExecutor.IsSelfCollider(hit, owner) ||
                !FairyBeamSkillExecutor.IsValidEnemyTarget(hit))
            {
                continue;
            }

            float distance = ((Vector2)hit.bounds.center - (Vector2)origin).sqrMagnitude;
            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closest = hit;
        }

        return closest;
    }
}

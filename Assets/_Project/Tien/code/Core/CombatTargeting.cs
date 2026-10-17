using UnityEngine;

public static class CombatTargeting
{
    public static Collider2D GetClosest(Collider2D[] targets, Vector3 origin)
    {
        if (targets == null || targets.Length == 0)
        {
            return null;
        }

        Collider2D closest = null;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (Collider2D target in targets)
        {
            if (target == null)
            {
                continue;
            }

            float sqrDistance = ((Vector2)target.transform.position - (Vector2)origin).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closest = target;
                closestSqrDistance = sqrDistance;
            }
        }

        return closest;
    }
}

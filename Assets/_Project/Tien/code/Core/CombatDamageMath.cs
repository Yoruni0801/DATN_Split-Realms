using UnityEngine;

public static class CombatDamageMath
{
    public static int ApplyPercentReduction(int damage, int reductionPercent)
    {
        int clampedReduction = Mathf.Clamp(reductionPercent, 0, 100);
        return damage - damage * clampedReduction / 100;
    }

    public static Vector2 KnockbackVelocity(Vector3 receiverPosition, Vector3 sourcePosition, float pushX, float pushY)
    {
        float direction = receiverPosition.x < sourcePosition.x ? -1f : 1f;
        return new Vector2(direction * pushX, pushY);
    }
}

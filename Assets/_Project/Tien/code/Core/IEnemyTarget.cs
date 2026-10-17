using UnityEngine;

public interface IEnemyTarget
{
    Transform Transform { get; }
    bool IsAlive { get; }
    float CurrentHealth { get; }
    void TakeDamage(float damage);
    void ApplyStun(float duration);
    void FlyToTarget(Vector3 targetPosition, float holdTime);
    void ApplyVelocity(Vector2 velocity, bool forceDynamicBody);
}

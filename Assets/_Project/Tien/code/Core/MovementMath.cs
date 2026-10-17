using UnityEngine;

public static class MovementMath
{
    public static float MoveTowardsHorizontalVelocity(float currentVelocity, float inputX, float speed, float acceleration, float deceleration, float deltaTime)
    {
        float targetVelocity = inputX * speed;
        float accelRate = Mathf.Abs(inputX) > 0.01f ? acceleration : deceleration;
        return Mathf.MoveTowards(currentVelocity, targetVelocity, accelRate * deltaTime);
    }

    public static float MoveTowardsStop(float currentVelocity, float deceleration, float deltaTime)
    {
        return Mathf.MoveTowards(currentVelocity, 0f, deceleration * deltaTime);
    }
}

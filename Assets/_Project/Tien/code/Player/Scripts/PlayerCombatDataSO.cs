using UnityEngine;

[CreateAssetMenu(fileName = "PlayerCombatData", menuName = "Duy/Game Data/Player Combat Data")]
public class PlayerCombatDataSO : ScriptableObject
{
    [Header("Normal Attack")]
    public int normalDamage = 20;
    public int specialDamage = 50;
    public float comboResetWindow = 1.5f;
    public float normalAttackDuration = 0.4f;
    [Tooltip("Time before the player can start another normal attack.")]
    [Min(0f)] public float normalAttackCooldown = 0.4f;
    public float normalAttackCancelWindow = 0.18f;
    public float attackInputBufferTime = 0.45f;
    [Tooltip("Normalized animation time when the normal attack hitbox becomes active (0 = start, 1 = end).")]
    [Range(0f, 1f)] public float normalAttackHitboxActivationTime = 0.3f;
    [Tooltip("How long the normal attack hitbox remains active.")]
    [Min(0f)] public float normalAttackHitboxActiveDuration = 0.1f;
    public float attackRange = 0.5f;
    public float firstAttackBrakeDuration = 0.1f;
    [Range(0f, 1f)] public float firstAttackBrakeStrength = 1f;
    public float secondAttackLungeSpeed = 7f;
    public float secondAttackLungeDuration = 0.12f;

    [Header("Auto Aim")]
    public Vector2 autoAimBoxSize = new Vector2(6f, 3f);
    public Vector2 autoAimOffset = new Vector2(0f, 0f);

    public float EffectiveNormalAttackCancelWindow
        => normalAttackCancelWindow > 0f ? normalAttackCancelWindow : 0.18f;

    public float EffectiveNormalAttackCooldown
        => Mathf.Max(0f, normalAttackCooldown);

    public float EffectiveNormalAttackHitboxActivationTime
        => Mathf.Clamp01(normalAttackHitboxActivationTime);

    public float EffectiveNormalAttackHitboxActiveDuration
        => Mathf.Max(0f, normalAttackHitboxActiveDuration);

    public float EffectiveAttackInputBufferTime
        => attackInputBufferTime > 0f ? attackInputBufferTime : 0.45f;

    public float EffectiveFirstAttackBrakeDuration
        => firstAttackBrakeDuration > 0f ? firstAttackBrakeDuration : 0.1f;

    public float EffectiveFirstAttackBrakeStrength
        => firstAttackBrakeStrength > 0f ? Mathf.Clamp01(firstAttackBrakeStrength) : 1f;

    public float EffectiveSecondAttackLungeSpeed
        => secondAttackLungeSpeed > 0f ? secondAttackLungeSpeed : 7f;

    public float EffectiveSecondAttackLungeDuration
        => secondAttackLungeDuration > 0f ? secondAttackLungeDuration : 0.12f;
}

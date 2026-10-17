using UnityEngine;

[CreateAssetMenu(fileName = "PlayerParryData", menuName = "Duy/Player Parry Data")]
public class PlayerParryDataSO : ScriptableObject
{
    [Header("Timing")]
    public float perfectWindowDuration = 0.18f;
    public float guardGraceDuration = 0.15f;
    public float cooldownDuration = 0.65f;
    public float failRecoveryDuration = 0.22f;
    public float successRecoveryDuration = 0.08f;

    [Header("Animation Hold")]
    public float failAnimationDuration = 0.3f;
    public float successAnimationDuration = 0.12f;

    [Header("Guard")]
    [Range(0f, 1f)] public float guardDamageReduction = 1f / 3f;

    [Header("Facing")]
    public bool requireFacingSource = true;
    [Range(1f, 180f)] public float defenseFacingAngle = 120f;

    [Header("Control")]
    public bool lockMovementDuringWindow = true;
    public bool lockMovementDuringGuard;
    public bool lockMovementDuringRecovery = true;
    public bool suppressBlockDuringParry = true;

    [Header("Feedback")]
    public float hitStopDuration = 0.05f;
    public GameObject successVfxPrefab;
    public Vector2 successVfxOffset = new Vector2(0f, 0.45f);
    public bool useDefaultSuccessVfx = true;
    public Color defaultSuccessVfxColor = new Color(0.35f, 0.9f, 1f, 1f);
    public float defaultSuccessVfxDuration = 0.18f;
    public float defaultSuccessVfxStartRadius = 0.25f;
    public float defaultSuccessVfxEndRadius = 1.15f;

    [Header("Guard Feedback")]
    public GameObject guardHitVfxPrefab;
    public Vector2 guardHitVfxOffset = new Vector2(0f, 0.35f);

    private void OnValidate()
    {
        perfectWindowDuration = Mathf.Max(0f, perfectWindowDuration);
        guardGraceDuration = Mathf.Max(0f, guardGraceDuration);
        cooldownDuration = Mathf.Max(0f, cooldownDuration);
        failRecoveryDuration = Mathf.Max(0f, failRecoveryDuration);
        successRecoveryDuration = Mathf.Max(0f, successRecoveryDuration);
        failAnimationDuration = Mathf.Max(0f, failAnimationDuration);
        successAnimationDuration = Mathf.Max(0f, successAnimationDuration);
        guardDamageReduction = Mathf.Clamp01(guardDamageReduction);
        defenseFacingAngle = Mathf.Clamp(defenseFacingAngle, 1f, 180f);
        hitStopDuration = Mathf.Max(0f, hitStopDuration);
        defaultSuccessVfxDuration = Mathf.Max(0f, defaultSuccessVfxDuration);
        defaultSuccessVfxStartRadius = Mathf.Max(0f, defaultSuccessVfxStartRadius);
        defaultSuccessVfxEndRadius = Mathf.Max(0f, defaultSuccessVfxEndRadius);
    }
}

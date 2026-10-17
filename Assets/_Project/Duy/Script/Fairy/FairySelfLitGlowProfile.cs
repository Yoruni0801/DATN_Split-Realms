using UnityEngine;

[CreateAssetMenu(fileName = "Fairy Self Lit Glow Profile", menuName = "Duy/Game Data/Fairy/Self Lit Glow Profile")]
public sealed class FairySelfLitGlowProfile : ScriptableObject
{
    public const float DefaultSceneLightInfluence = 0f;
    public const float DefaultSelfEmissionIntensity = 1.25f;
    public const float DefaultGlowIntensity = 0.6f;
    public const float DefaultGlowPulseAmount = 0.15f;
    public const float DefaultGlowPulseSpeed = 2f;
    public const float DefaultOuterGlowSize = 0.03f;
    public static readonly Color DefaultGlowColor = new Color(1f, 0.9f, 0.55f, 1f);

    [SerializeField, Range(0f, 1f)] private float sceneLightInfluence = DefaultSceneLightInfluence;
    [SerializeField, Range(0f, 3f)] private float selfEmissionIntensity = DefaultSelfEmissionIntensity;
    [SerializeField] private Color glowColor = new Color(1f, 0.9f, 0.55f, 1f);
    [SerializeField, Range(0f, 3f)] private float glowIntensity = DefaultGlowIntensity;
    [SerializeField, Range(0f, 1f)] private float glowPulseAmount = DefaultGlowPulseAmount;
    [SerializeField, Range(0f, 8f)] private float glowPulseSpeed = DefaultGlowPulseSpeed;
    [SerializeField, Range(0f, 0.2f)] private float outerGlowSize = DefaultOuterGlowSize;

    public float SceneLightInfluence => sceneLightInfluence;
    public float SelfEmissionIntensity => selfEmissionIntensity;
    public Color GlowColor => glowColor;
    public float GlowIntensity => glowIntensity;
    public float GlowPulseAmount => glowPulseAmount;
    public float GlowPulseSpeed => glowPulseSpeed;
    public float OuterGlowSize => outerGlowSize;
}

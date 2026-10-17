using UnityEngine;

public sealed class AcidVfxController : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer acidRenderer;
    [SerializeField] private Color mainColor = new Color(0.35f, 1f, 0.02f, 1f);
    [SerializeField] private Color deepColor = new Color(0f, 0.25f, 0.03f, 1f);
    [SerializeField] private Color edgeGlowColor = new Color(0.75f, 1f, 0.02f, 1f);
    [SerializeField, Range(0f, 1f)] private float alpha = 0.82f;
    [SerializeField, Range(0f, 3f)] private float intensity = 1f;

    [Header("Shader Motion")]
    [SerializeField, Min(0f)] private float rippleSpeed = 1.25f;
    [SerializeField, Min(1f)] private float rippleScale = 24f;
    [SerializeField, Range(0f, 0.12f)] private float distortionStrength = 0.035f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem bubbleParticles;
    [SerializeField] private ParticleSystem vaporParticles;
    [SerializeField] private bool applyRecommendedParticleSettingsOnAwake;
    [SerializeField, Min(0f)] private float bubbleRate = 16f;
    [SerializeField, Min(0f)] private float vaporRate = 7f;
    [SerializeField, Min(0)] private int splashBubbleBurst = 22;
    [SerializeField, Min(0)] private int splashVaporBurst = 10;
    [SerializeField] private bool splashTrigger;

    private static readonly int MainColorId = Shader.PropertyToID("_MainColor");
    private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int RippleSpeedId = Shader.PropertyToID("_RippleSpeed");
    private static readonly int RippleScaleId = Shader.PropertyToID("_RippleScale");
    private static readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
    private static readonly int BubbleStrengthId = Shader.PropertyToID("_BubbleStrength");
    private static readonly int SurfaceFoamId = Shader.PropertyToID("_SurfaceFoam");

    private MaterialPropertyBlock propertyBlock;

    public float Intensity
    {
        get => intensity;
        set
        {
            intensity = Mathf.Max(0f, value);
            ApplyVfxState();
        }
    }

    public float BubbleRate
    {
        get => bubbleRate;
        set
        {
            bubbleRate = Mathf.Max(0f, value);
            ApplyVfxState();
        }
    }

    public float VaporRate
    {
        get => vaporRate;
        set
        {
            vaporRate = Mathf.Max(0f, value);
            ApplyVfxState();
        }
    }

    public Color MainColor
    {
        get => mainColor;
        set
        {
            mainColor = value;
            ApplyVfxState();
        }
    }

    public Color DeepColor
    {
        get => deepColor;
        set
        {
            deepColor = value;
            ApplyVfxState();
        }
    }

    public Color EdgeGlowColor
    {
        get => edgeGlowColor;
        set
        {
            edgeGlowColor = value;
            ApplyVfxState();
        }
    }

    public bool SplashTrigger
    {
        get => splashTrigger;
        set
        {
            splashTrigger = false;

            if (value)
            {
                TriggerSplash();
            }
        }
    }

    private void Reset()
    {
        acidRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (applyRecommendedParticleSettingsOnAwake)
        {
            ApplyRecommendedParticleSettings();
        }

        ApplyVfxState();
    }

    private void OnValidate()
    {
        intensity = Mathf.Max(0f, intensity);
        bubbleRate = Mathf.Max(0f, bubbleRate);
        vaporRate = Mathf.Max(0f, vaporRate);
        rippleSpeed = Mathf.Max(0f, rippleSpeed);
        rippleScale = Mathf.Max(1f, rippleScale);
        ApplyVfxState();

        if (splashTrigger)
        {
            splashTrigger = false;
            TriggerSplash();
        }
    }

    private void Update()
    {
        ApplyVfxState();
    }

    public void SetIntensity(float value)
    {
        Intensity = value;
    }

    public void TriggerSplash()
    {
        if (bubbleParticles != null && splashBubbleBurst > 0)
        {
            bubbleParticles.Emit(splashBubbleBurst);
        }

        if (vaporParticles != null && splashVaporBurst > 0)
        {
            vaporParticles.Emit(splashVaporBurst);
        }
    }

    [ContextMenu("Apply Recommended Acid Particle Settings")]
    public void ApplyRecommendedParticleSettings()
    {
        ConfigureBubbleParticles();
        ConfigureVaporParticles();
        ApplyVfxState();
    }

    private void ApplyVfxState()
    {
        ApplyShaderState();
        ApplyParticleRate(bubbleParticles, bubbleRate * intensity);
        ApplyParticleRate(vaporParticles, vaporRate * intensity);
    }

    private void ApplyShaderState()
    {
        if (acidRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        acidRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(MainColorId, mainColor);
        propertyBlock.SetColor(DeepColorId, deepColor);
        propertyBlock.SetColor(EdgeColorId, edgeGlowColor);
        propertyBlock.SetFloat(AlphaId, alpha);
        propertyBlock.SetFloat(RippleSpeedId, rippleSpeed * Mathf.Lerp(0.45f, 1.75f, intensity));
        propertyBlock.SetFloat(RippleScaleId, rippleScale);
        propertyBlock.SetFloat(DistortionStrengthId, distortionStrength * Mathf.Lerp(0.65f, 1.35f, intensity));
        propertyBlock.SetFloat(BubbleStrengthId, Mathf.Lerp(0.25f, 1.2f, intensity));
        propertyBlock.SetFloat(SurfaceFoamId, Mathf.Lerp(0.45f, 1.35f, intensity));
        acidRenderer.SetPropertyBlock(propertyBlock);
    }

    private static void ApplyParticleRate(ParticleSystem particles, float rate)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = Mathf.Max(0f, rate);
    }

    private void ConfigureBubbleParticles()
    {
        if (bubbleParticles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = bubbleParticles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 1f, 0.03f, 0.28f), new Color(0.9f, 1f, 0.08f, 0.72f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 80;

        ParticleSystem.ShapeModule shape = bubbleParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.8f, 0.08f, 0.1f);
        shape.position = new Vector3(0f, -0.28f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = bubbleParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);

        ParticleSystem.NoiseModule noise = bubbleParticles.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.9f;
        noise.scrollSpeed = 0.25f;

        ParticleSystem.SizeOverLifetimeModule size = bubbleParticles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.35f, 0.78f, 1f));

        ParticleSystem.ColorOverLifetimeModule color = bubbleParticles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.5f, 1f, 0.02f), 0f),
                new GradientColorKey(new Color(0.9f, 1f, 0.1f), 0.65f),
                new GradientColorKey(new Color(0.45f, 0.9f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.75f, 0.18f),
                new GradientAlphaKey(0.45f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;
    }

    private void ConfigureVaporParticles()
    {
        if (vaporParticles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = vaporParticles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.32f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.9f, 0.04f, 0.08f), new Color(0.78f, 1f, 0.12f, 0.22f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 45;

        ParticleSystem.ShapeModule shape = vaporParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.8f, 0.04f, 0.1f);
        shape.position = new Vector3(0f, 0.14f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = vaporParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.22f, 0.55f);

        ParticleSystem.NoiseModule noise = vaporParticles.noise;
        noise.enabled = true;
        noise.strength = 0.24f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.12f;

        ParticleSystem.SizeOverLifetimeModule size = vaporParticles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1.35f));

        ParticleSystem.ColorOverLifetimeModule color = vaporParticles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.35f, 0.8f, 0.04f), 0f),
                new GradientColorKey(new Color(0.78f, 1f, 0.12f), 0.45f),
                new GradientColorKey(new Color(0.22f, 0.55f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.18f, 0.2f),
                new GradientAlphaKey(0.1f, 0.62f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;
    }
}

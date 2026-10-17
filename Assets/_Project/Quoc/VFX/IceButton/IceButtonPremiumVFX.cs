using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class IceButtonPremiumVFX : MonoBehaviour
{
    [Header("Artwork & Shaders")]
    [SerializeField] private SpriteRenderer sourceRenderer;
    [SerializeField] private Shader surfaceGradeShader;
    [SerializeField] private Shader runeGlowShader;
    [SerializeField] private Shader haloShader;
    [SerializeField] private Shader topRingShader;
    [SerializeField] private Shader groundShadowShader;
    [SerializeField] private Shader particleShader;
    [SerializeField] private Shader smokeShader;

    [Header("Ancient Cryo Palette")]
    [SerializeField, ColorUsage(true, true)] private Color runeColor = new(0.03f, 0.45f, 2.8f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color runeCoreColor = new(1.35f, 1.8f, 2.2f, 1f);
    [SerializeField] private Color haloColor = new(0.004f, 0.035f, 0.12f, 0.94f);
    [SerializeField] private Color backdropColor = new(0.015f, 0.1f, 0.28f, 0.48f);

    [Header("Golden Well Focus")]
    [SerializeField, ColorUsage(true, true)] private Color topRingRuneColor = new(1.65f, 0.72f, 0.1f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color topRingEdgeColor = new(1.45f, 1.08f, 0.52f, 1f);
    [SerializeField] private Color topRingAbyssColor = new(0.006f, 0.02f, 0.085f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color topRingMistColor = new(1.05f, 0.72f, 0.24f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color topRingSmokeColor = new(0.95f, 0.65f, 0.28f, 0.68f);

    [Header("Golden Well Smoke Tuning")]
    [SerializeField, Range(0.25f, 3f)] private float smokeSizeMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float smokeDensityMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float smokeRiseMultiplier = 1f;
    [SerializeField, Range(0.25f, 2.5f)] private float smokeSpreadMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float smokeOpacity = 0.92f;
    [SerializeField, Range(0.4f, 2.5f)] private float wellRimIntensity = 1.32f;
    [SerializeField, Range(0.5f, 2f)] private float wellGlowSize = 1f;
    [SerializeField, Range(0f, 0.8f)] private float wellGlowAlpha = 0.2f;

    [Header("Ice Particle Palette")]
    [SerializeField] private Color snowColor = new(0.72f, 0.96f, 1f, 0.9f);
    [SerializeField] private Color crystalColor = new(0.18f, 0.9f, 1f, 1f);

    [Header("Idle")]
    [SerializeField, Range(0f, 3f)] private float idleGlow = 0.48f;
    [SerializeField, Range(0.1f, 5f)] private float idlePulseSpeed = 0.72f;
    [SerializeField, Range(0f, 30f)] private float frostMotesPerSecond = 7f;
    [SerializeField, Range(0f, 30f)] private float orbitSparklesPerSecond = 9f;

    [Header("Activation")]
    [SerializeField] private string activatorTag = "IceRock";
    [SerializeField, Min(0.1f)] private float chargeDuration = 0.72f;
    [SerializeField, Min(0f)] private float climaxHold = 0.45f;
    [SerializeField, Range(0f, 5f)] private float impactGlow = 2.4f;
    [SerializeField, Range(0.1f, 1f)] private float shockwaveEllipse = 0.38f;
    [SerializeField, Range(8, 120)] private int snowBurstCount = 48;
    [SerializeField, Range(4, 80)] private int crystalShardCount = 28;

    private const string RuntimeRootName = "__IceButtonPremiumVFX";
    private Material originalSourceMaterial;
    private Material surfaceGradeMaterial;
    private Material runeMaterial;
    private Material haloMaterial;
    private Material topRingMaterial;
    private Material backdropMaterial;
    private Material groundShadowMaterial;
    private Material wellGlowMaterial;
    private Material softParticleMaterial;
    private Material smokeParticleMaterial;
    private Material shardParticleMaterial;
    private SpriteRenderer runeOverlay;
    private SpriteRenderer haloOverlay;
    private SpriteRenderer topRingOverlay;
    private SpriteRenderer wellBeacon;
    private Vector3 wellBeaconBaseScale;
    private ParticleSystem frostMotes;
    private ParticleSystem lowWellVapor;
    private ParticleSystem orbitSparkles;
    private ParticleSystem snowBurst;
    private ParticleSystem crystalBurst;
    private Transform runtimeRoot;
    private Sprite softSprite;
    private Sprite smokeSprite;
    private Sprite shardSprite;
    private Sprite ringSprite;
    private bool activated;
    private float charge;
    private float activationBoost;
    private Coroutine activationRoutine;

    private void Awake()
    {
        BuildRuntimeVFX();
    }

    private void OnEnable()
    {
        PlinkoBreakTarget.OnPlinkoCompleted += HandlePlinkoCompleted;
    }

    private void OnDisable()
    {
        PlinkoBreakTarget.OnPlinkoCompleted -= HandlePlinkoCompleted;
    }

    private void Update()
    {
        if (runeMaterial == null || haloMaterial == null)
            return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * idlePulseSpeed * Mathf.PI * 2f);
        float shimmer = Mathf.SmoothStep(0f, 1f, pulse);
        runeMaterial.SetFloat("_Pulse", shimmer);
        runeMaterial.SetFloat("_Charge", charge);
        runeMaterial.SetFloat("_Intensity", idleGlow + activationBoost);
        haloMaterial.SetFloat("_Pulse", shimmer);
        haloMaterial.SetFloat("_Intensity", 0.72f + activationBoost * 0.16f);
        if (topRingMaterial != null)
        {
            topRingMaterial.SetFloat("_Pulse", shimmer);
            topRingMaterial.SetFloat("_Charge", charge);
        }
        if (wellBeacon != null)
        {
            float beaconPulse = 0.97f + shimmer * 0.045f + charge * 0.06f;
            wellBeacon.transform.localScale = wellBeaconBaseScale * beaconPulse;
            float beaconAlpha = Mathf.Clamp01(wellGlowAlpha + shimmer * 0.09f + charge * 0.14f);
            wellBeacon.color = new Color(1f, 1f, 1f, beaconAlpha);
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && runtimeRoot != null)
            ApplyLiveInspectorTuning();
    }

    private void ApplyLiveInspectorTuning()
    {
        if (sourceRenderer == null || sourceRenderer.sprite == null)
            return;

        Bounds topRingBounds = GetTopRingBounds();
        float width = Mathf.Max(0.5f, topRingBounds.size.x);
        float height = Mathf.Max(0.3f, topRingBounds.size.y);

        if (topRingMaterial != null)
            topRingMaterial.SetFloat("_RimIntensity", wellRimIntensity);
        if (smokeParticleMaterial != null)
            smokeParticleMaterial.SetFloat("_Opacity", smokeOpacity);

        if (wellBeacon != null)
        {
            wellBeaconBaseScale = new Vector3(width * 1.26f * wellGlowSize, height * 0.76f * wellGlowSize, 1f);
            wellBeacon.transform.localScale = wellBeaconBaseScale;
            wellBeacon.color = new Color(1f, 1f, 1f, wellGlowAlpha);
        }

        if (frostMotes != null)
        {
            var main = frostMotes.main;
            main.startSize = new ParticleSystem.MinMaxCurve(width * 0.075f * smokeSizeMultiplier, width * 0.17f * smokeSizeMultiplier);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.74f, 0.45f, 0.38f), topRingSmokeColor);

            var emission = frostMotes.emission;
            emission.rateOverTime = Mathf.Max(8f, frostMotesPerSecond * 1.18f) * smokeDensityMultiplier;

            var shape = frostMotes.shape;
            shape.scale = new Vector3(width * 0.64f * smokeSpreadMultiplier, height * 0.18f, 0.05f);

            var velocity = frostMotes.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(-width * 0.034f * smokeSpreadMultiplier, width * 0.034f * smokeSpreadMultiplier);
            velocity.y = new ParticleSystem.MinMaxCurve(height * 0.08f * smokeRiseMultiplier, height * 0.23f * smokeRiseMultiplier);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        if (lowWellVapor != null)
        {
            var main = lowWellVapor.main;
            main.startSize = new ParticleSystem.MinMaxCurve(width * 0.13f * smokeSizeMultiplier, width * 0.27f * smokeSizeMultiplier);

            var emission = lowWellVapor.emission;
            emission.rateOverTime = Mathf.Max(5.4f, frostMotesPerSecond * 0.78f) * smokeDensityMultiplier;

            var shape = lowWellVapor.shape;
            shape.radius = width * 0.29f * smokeSpreadMultiplier;

            var velocity = lowWellVapor.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(-width * 0.032f * smokeSpreadMultiplier, width * 0.032f * smokeSpreadMultiplier);
            velocity.y = new ParticleSystem.MinMaxCurve(height * 0.026f * smokeRiseMultiplier, height * 0.082f * smokeRiseMultiplier);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!activated && IsActivator(other))
            PlayActivationVFX();
    }

    private void HandlePlinkoCompleted()
    {
        PlayActivationVFX();
    }

    [ContextMenu("Preview Activation VFX")]
    public void PlayActivationVFX()
    {
        if (!Application.isPlaying || activated)
            return;

        if (runtimeRoot == null)
            BuildRuntimeVFX();

        activated = true;
        if (activationRoutine != null)
            StopCoroutine(activationRoutine);
        activationRoutine = StartCoroutine(ActivationSequence());
    }

    private IEnumerator ActivationSequence()
    {
        if (snowBurst != null)
            snowBurst.Emit(snowBurstCount);
        if (crystalBurst != null)
            crystalBurst.Emit(crystalShardCount);

        StartCoroutine(SpawnShockwave(0f, 1f));
        StartCoroutine(SpawnShockwave(0.12f, 0.68f));

        float elapsed = 0f;
        Vector3 runeBaseScale = runeOverlay != null ? runeOverlay.transform.localScale : Vector3.one;
        Vector3 haloBaseScale = haloOverlay != null ? haloOverlay.transform.localScale : Vector3.one;

        while (elapsed < chargeDuration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, chargeDuration));
            float eased = 1f - Mathf.Pow(1f - normalized, 3f);
            charge = eased;
            activationBoost = Mathf.Lerp(0f, impactGlow, Mathf.Sin(normalized * Mathf.PI));

            float punch = 1f + Mathf.Sin(normalized * Mathf.PI) * 0.055f;
            if (runeOverlay != null)
                runeOverlay.transform.localScale = runeBaseScale * punch;
            if (haloOverlay != null)
                haloOverlay.transform.localScale = haloBaseScale * (1f + (punch - 1f) * 1.8f);

            yield return null;
        }

        charge = 1f;
        activationBoost = impactGlow * 0.62f;
        if (runeOverlay != null)
            runeOverlay.transform.localScale = runeBaseScale;
        if (haloOverlay != null)
            haloOverlay.transform.localScale = haloBaseScale;

        if (climaxHold > 0f)
            yield return new WaitForSeconds(climaxHold);

        elapsed = 0f;
        const float settleDuration = 0.65f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            activationBoost = Mathf.Lerp(impactGlow * 0.62f, 0.32f, elapsed / settleDuration);
            yield return null;
        }

        activationBoost = 0.32f;
        activationRoutine = null;
    }

    private IEnumerator SpawnShockwave(float delay, float strength)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        if (runtimeRoot == null || ringSprite == null || softParticleMaterial == null)
            yield break;

        GameObject waveObject = new("Cryo Shockwave");
        waveObject.transform.SetParent(runtimeRoot, false);
        SpriteRenderer wave = waveObject.AddComponent<SpriteRenderer>();
        wave.sprite = ringSprite;
        wave.sharedMaterial = new Material(softParticleMaterial)
        {
            name = "Cryo Shockwave Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        wave.color = new Color(crystalColor.r, crystalColor.g, crystalColor.b, 0f);
        CopySorting(sourceRenderer, wave, 5);

        float width = Mathf.Max(0.5f, GetTopRingBounds().size.x);
        float duration = 0.48f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float diameter = Mathf.Lerp(width * 0.18f, width * 1.16f, eased);
            waveObject.transform.localScale = new Vector3(diameter, diameter * shockwaveEllipse, 1f);
            float alpha = Mathf.Sin(t * Mathf.PI) * 0.82f * strength;
            wave.color = new Color(crystalColor.r, crystalColor.g, crystalColor.b, alpha);
            yield return null;
        }

        Destroy(wave.sharedMaterial);
        Destroy(waveObject);
    }

    private void BuildRuntimeVFX()
    {
        if (!Application.isPlaying || runtimeRoot != null)
            return;

        ResolveSourceRenderer();
        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            Debug.LogWarning("[IceButtonPremiumVFX] No source SpriteRenderer was found.", this);
            return;
        }

        ResolveShaders();
        if (surfaceGradeShader == null || runeGlowShader == null || haloShader == null || topRingShader == null || groundShadowShader == null || particleShader == null || smokeShader == null)
        {
            Debug.LogWarning("[IceButtonPremiumVFX] One or more VFX shaders are missing.", this);
            return;
        }

        GameObject rootObject = new(RuntimeRootName);
        runtimeRoot = rootObject.transform;
        runtimeRoot.SetParent(transform, false);
        runtimeRoot.position = GetTopRingBounds().center;
        runtimeRoot.rotation = Quaternion.identity;
        Vector3 lossy = transform.lossyScale;
        runtimeRoot.localScale = new Vector3(SafeReciprocal(lossy.x), SafeReciprocal(lossy.y), SafeReciprocal(lossy.z));

        originalSourceMaterial = sourceRenderer.sharedMaterial;
        surfaceGradeMaterial = NewRuntimeMaterial(surfaceGradeShader, "Ice Button Deep Cryo Surface (Runtime)");
        surfaceGradeMaterial.SetColor("_ShadowColor", new Color(0.035f, 0.15f, 0.33f, 1f));
        surfaceGradeMaterial.SetColor("_MidColor", new Color(0.16f, 0.49f, 0.72f, 1f));
        surfaceGradeMaterial.SetColor("_SnowColor", new Color(0.92f, 1.08f, 1.18f, 1f));
        surfaceGradeMaterial.SetFloat("_GradeStrength", 0.74f);
        surfaceGradeMaterial.SetFloat("_Contrast", 1.2f);
        sourceRenderer.sharedMaterial = surfaceGradeMaterial;

        runeMaterial = NewRuntimeMaterial(runeGlowShader, "Ice Button Rune Glow (Runtime)");
        runeMaterial.SetColor("_GlowColor", runeColor);
        runeMaterial.SetColor("_CoreColor", runeCoreColor);
        runeMaterial.SetFloat("_CyanThreshold", 0.24f);
        runeMaterial.SetFloat("_BrightnessThreshold", 0.42f);
        runeMaterial.SetFloat("_ScanSpeed", 0.55f);

        haloMaterial = NewRuntimeMaterial(haloShader, "Ice Button Frost Halo (Runtime)");
        haloMaterial.SetColor("_HaloColor", haloColor);
        haloMaterial.SetFloat("_Thickness", 6.5f);

        topRingMaterial = NewRuntimeMaterial(topRingShader, "Ice Button Top Ring (Runtime)");
        topRingMaterial.SetColor("_RuneColor", topRingRuneColor);
        topRingMaterial.SetColor("_EdgeColor", topRingEdgeColor);
        topRingMaterial.SetColor("_AbyssColor", topRingAbyssColor);
        topRingMaterial.SetColor("_MistColor", topRingMistColor);
        topRingMaterial.SetFloat("_Contrast", 1.3f);
        topRingMaterial.SetFloat("_SurfaceLift", 0.07f);
        topRingMaterial.SetFloat("_DepthStrength", 0.8f);
        topRingMaterial.SetFloat("_RimIntensity", wellRimIntensity);
        topRingMaterial.SetVector("_RingRect", new Vector4(0.23177f, 0.55469f, 0.54167f, 0.24609f));

        runeOverlay = CreateSpriteOverlay("Rune Energy Overlay", runeMaterial, 2, Vector3.one);
        haloOverlay = CreateSpriteOverlay("Navy Separation Silhouette", haloMaterial, -2, Vector3.one * 1.085f);
        topRingOverlay = CreateSpriteOverlay("Top Ring Energy - Full Sprite Mask", topRingMaterial, 3, Vector3.one);

        softSprite = CreateSoftSprite(64);
        smokeSprite = CreateSmokeSprite(96);
        shardSprite = CreateShardSprite(48);
        ringSprite = CreateRingSprite(96);
        softParticleMaterial = CreateParticleMaterial(softSprite.texture, "Ice Soft Particle Material", 0.72f);
        smokeParticleMaterial = CreateSmokeMaterial(smokeSprite.texture);
        shardParticleMaterial = CreateParticleMaterial(shardSprite.texture, "Ice Crystal Particle Material", 0.52f);
        CreateBackdropSeparation();
        CreateGroundSeparationShadow();

        Bounds topRingBounds = GetTopRingBounds();
        float width = Mathf.Max(0.5f, topRingBounds.size.x);
        float height = Mathf.Max(0.3f, topRingBounds.size.y);
        CreateGoldenWellBeacon(width, height);
        lowWellVapor = CreateLowWellVapor(width, height);
        frostMotes = CreateFrostMotes(width, height);
        orbitSparkles = CreateOrbitSparkles(width);
        snowBurst = CreateSnowBurst(width);
        crystalBurst = CreateCrystalBurst(width);

        lowWellVapor.Play();
        frostMotes.Play();
        orbitSparkles.Play();
    }

    private void ResolveSourceRenderer()
    {
        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            float largestArea = -1f;
            foreach (SpriteRenderer candidate in renderers)
            {
                if (candidate == null || candidate.sprite == null || candidate.gameObject.name.StartsWith("__"))
                    continue;

                Vector3 size = candidate.bounds.size;
                float area = size.x * size.y;
                if (candidate.enabled)
                    area *= 2f;
                if (area <= largestArea)
                    continue;

                largestArea = area;
                sourceRenderer = candidate;
            }
        }
    }

    private Bounds GetTopRingBounds()
    {
        Bounds sourceBounds = sourceRenderer.bounds;
        const float normalizedMinX = 0.2253f;
        const float normalizedMinY = 0.6114f;
        const float normalizedWidth = 0.5514f;
        const float normalizedHeight = 0.3695f;

        Vector3 center = new(
            sourceBounds.min.x + sourceBounds.size.x * (normalizedMinX + normalizedWidth * 0.5f),
            sourceBounds.min.y + sourceBounds.size.y * (normalizedMinY + normalizedHeight * 0.5f),
            sourceBounds.center.z);
        Vector3 size = new(
            sourceBounds.size.x * normalizedWidth,
            sourceBounds.size.y * normalizedHeight,
            sourceBounds.size.z);
        return new Bounds(center, size);
    }

    private void ResolveShaders()
    {
        if (surfaceGradeShader == null)
            surfaceGradeShader = Shader.Find("DATN/IceButton/SurfaceGrade");
        if (runeGlowShader == null)
            runeGlowShader = Shader.Find("DATN/IceButton/RuneGlow");
        if (haloShader == null)
            haloShader = Shader.Find("DATN/IceButton/FrostHalo");
        if (topRingShader == null)
            topRingShader = Shader.Find("DATN/IceButton/TopRing");
        if (groundShadowShader == null)
            groundShadowShader = Shader.Find("DATN/IceButton/GroundShadow");
        if (particleShader == null)
            particleShader = Shader.Find("DATN/IceButton/ParticleAdditive");
        if (smokeShader == null)
            smokeShader = Shader.Find("DATN/IceButton/MagicSmoke");
    }

    private SpriteRenderer CreateSpriteOverlay(string objectName, Material material, int sortingOffset, Vector3 scale)
    {
        GameObject overlayObject = new(objectName);
        overlayObject.transform.SetParent(sourceRenderer.transform, false);
        overlayObject.transform.localPosition = Vector3.zero;
        overlayObject.transform.localRotation = Quaternion.identity;
        overlayObject.transform.localScale = scale;

        SpriteRenderer overlay = overlayObject.AddComponent<SpriteRenderer>();
        overlay.sprite = sourceRenderer.sprite;
        overlay.drawMode = sourceRenderer.drawMode;
        overlay.size = sourceRenderer.size;
        overlay.flipX = sourceRenderer.flipX;
        overlay.flipY = sourceRenderer.flipY;
        overlay.maskInteraction = sourceRenderer.maskInteraction;
        overlay.spriteSortPoint = sourceRenderer.spriteSortPoint;
        overlay.sharedMaterial = material;
        CopySorting(sourceRenderer, overlay, sortingOffset);
        return overlay;
    }

    private ParticleSystem CreateFrostMotes(float width, float height)
    {
        ParticleSystem system = CreateParticleSystem("Golden Well Rising Billows", 5);
        var main = system.main;
        main.loop = true;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(width * 0.004f, width * 0.016f);
        main.startSize = new ParticleSystem.MinMaxCurve(width * 0.075f * smokeSizeMultiplier, width * 0.17f * smokeSizeMultiplier);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.9f, 0.74f, 0.45f, 0.38f),
            topRingSmokeColor);
        main.maxParticles = 72;

        var emission = system.emission;
        emission.rateOverTime = Mathf.Max(8f, frostMotesPerSecond * 1.18f) * smokeDensityMultiplier;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width * 0.64f * smokeSpreadMultiplier, height * 0.18f, 0.05f);

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-width * 0.034f * smokeSpreadMultiplier, width * 0.034f * smokeSpreadMultiplier);
        velocity.y = new ParticleSystem.MinMaxCurve(height * 0.08f * smokeRiseMultiplier, height * 0.23f * smokeRiseMultiplier);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = system.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(width * 0.022f, width * 0.052f);
        noise.frequency = 0.34f;
        noise.scrollSpeed = 0.12f;
        noise.damping = true;

        var rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);

        ConfigureSmokeLifetime(system);
        ConfigureParticleRenderer(system, smokeParticleMaterial, 5);
        return system;
    }

    private ParticleSystem CreateLowWellVapor(float width, float height)
    {
        ParticleSystem system = CreateParticleSystem("Golden Well Low Vapor", 4);
        var main = system.main;
        main.loop = true;
        main.duration = 3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 4.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(width * 0.002f, width * 0.01f);
        main.startSize = new ParticleSystem.MinMaxCurve(width * 0.13f * smokeSizeMultiplier, width * 0.27f * smokeSizeMultiplier);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.74f, 0.6f, 0.34f, 0.28f),
            new Color(1f, 0.76f, 0.38f, 0.5f));
        main.maxParticles = 38;

        var emission = system.emission;
        emission.rateOverTime = Mathf.Max(5.4f, frostMotesPerSecond * 0.78f) * smokeDensityMultiplier;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = width * 0.29f * smokeSpreadMultiplier;
        shape.radiusThickness = 1f;
        shape.scale = new Vector3(1f, shockwaveEllipse * 0.72f, 1f);

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-width * 0.032f * smokeSpreadMultiplier, width * 0.032f * smokeSpreadMultiplier);
        velocity.y = new ParticleSystem.MinMaxCurve(height * 0.026f * smokeRiseMultiplier, height * 0.082f * smokeRiseMultiplier);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = system.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(width * 0.025f, width * 0.06f);
        noise.frequency = 0.24f;
        noise.scrollSpeed = 0.08f;
        noise.damping = true;

        var rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.34f, 0.34f);

        ConfigureSmokeLifetime(system);
        ConfigureParticleRenderer(system, smokeParticleMaterial, 4);
        return system;
    }

    private ParticleSystem CreateOrbitSparkles(float width)
    {
        ParticleSystem system = CreateParticleSystem("Rune Orbit Sparkles", 5);
        var main = system.main;
        main.loop = true;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, width * 0.035f);
        main.startSize = new ParticleSystem.MinMaxCurve(width * 0.007f, width * 0.02f);
        main.startColor = new ParticleSystem.MinMaxGradient(topRingRuneColor, topRingEdgeColor);
        main.maxParticles = 64;

        var emission = system.emission;
        emission.rateOverTime = orbitSparklesPerSecond;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = width * 0.31f;
        shape.radiusThickness = 0.08f;
        shape.scale = new Vector3(1f, shockwaveEllipse, 1f);

        var rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.8f, 1.8f);

        ConfigureFadeAndShrink(system, 0.05f, 0.95f);
        ConfigureParticleRenderer(system, softParticleMaterial, 5);
        return system;
    }

    private ParticleSystem CreateSnowBurst(float width)
    {
        ParticleSystem system = CreateParticleSystem("Impact Snow Powder", 6);
        var main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.92f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(width * 0.32f, width * 0.72f);
        main.startSize = new ParticleSystem.MinMaxCurve(width * 0.018f, width * 0.065f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.92f, 1f, 0.55f), snowColor);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.maxParticles = 120;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = width * 0.08f;
        shape.radiusThickness = 1f;
        shape.rotation = new Vector3(0f, 0f, 0f);
        shape.scale = new Vector3(1f, 0.32f, 1f);

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(width * 0.02f, width * 0.12f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = system.noise;
        noise.enabled = true;
        noise.strength = width * 0.025f;
        noise.frequency = 0.7f;

        ConfigureFadeAndShrink(system, 0.08f, 0.78f);
        ConfigureParticleRenderer(system, softParticleMaterial, 6);
        return system;
    }

    private ParticleSystem CreateCrystalBurst(float width)
    {
        ParticleSystem system = CreateParticleSystem("Cryo Crystal Shards", 7);
        var main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(width * 0.25f, width * 0.62f);
        main.startSize = new ParticleSystem.MinMaxCurve(width * 0.012f, width * 0.042f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(crystalColor, new Color(0.82f, 1f, 1f, 1f));
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.14f, 0.38f);
        main.maxParticles = 96;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = width * 0.06f;
        shape.radiusThickness = 1f;
        shape.scale = new Vector3(1f, 0.42f, 1f);

        var rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-7f, 7f);

        ConfigureFadeAndShrink(system, 0.04f, 0.9f);
        ConfigureParticleRenderer(system, shardParticleMaterial, 7);
        return system;
    }

    private ParticleSystem CreateParticleSystem(string objectName, int sortingOffset)
    {
        GameObject particleObject = new(objectName);
        particleObject.transform.SetParent(runtimeRoot, false);
        ParticleSystem system = particleObject.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.playOnAwake = false;

        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.sortingOrder = sourceRenderer.sortingOrder + sortingOffset;
        return system;
    }

    private void ConfigureParticleRenderer(ParticleSystem system, Material material, int sortingOffset)
    {
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.sortingOrder = sourceRenderer.sortingOrder + sortingOffset;
    }

    private static void ConfigureFadeAndShrink(ParticleSystem system, float fadeIn, float fadeOutStart)
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, fadeIn),
                new GradientAlphaKey(0.8f, fadeOutStart),
                new GradientAlphaKey(0f, 1f)
            });

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = gradient;

        AnimationCurve sizeCurve = new(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.16f, 1f),
            new Keyframe(1f, 0f));
        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
    }

    private static void ConfigureSmokeLifetime(ParticleSystem system)
    {
        Gradient smokeFade = new();
        smokeFade.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.82f, 0.58f), 0.55f),
                new GradientColorKey(new Color(0.9f, 0.62f, 0.22f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.16f),
                new GradientAlphaKey(0.78f, 0.68f),
                new GradientAlphaKey(0f, 1f)
            });

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = smokeFade;

        AnimationCurve smokeExpansion = new(
            new Keyframe(0f, 0.32f),
            new Keyframe(0.2f, 0.72f),
            new Keyframe(0.68f, 1.12f),
            new Keyframe(1f, 1.38f));
        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, smokeExpansion);
    }

    private void CreateBackdropSeparation()
    {
        if (groundShadowShader == null || softSprite == null || runtimeRoot == null)
            return;

        backdropMaterial = NewRuntimeMaterial(groundShadowShader, "Ice Button Radial Backdrop (Runtime)");
        backdropMaterial.SetTexture("_MainTex", softSprite.texture);
        backdropMaterial.SetColor("_ShadowColor", backdropColor);

        Bounds bounds = sourceRenderer.bounds;
        GameObject backdropObject = new("Deep Cryo Radial Backdrop");
        backdropObject.transform.SetParent(runtimeRoot, false);
        backdropObject.transform.position = new Vector3(bounds.center.x, bounds.center.y + bounds.size.y * 0.03f, bounds.center.z);
        backdropObject.transform.rotation = Quaternion.identity;
        backdropObject.transform.localScale = new Vector3(bounds.size.x * 1.38f, bounds.size.y * 1.08f, 1f);

        SpriteRenderer backdrop = backdropObject.AddComponent<SpriteRenderer>();
        backdrop.sprite = softSprite;
        backdrop.sharedMaterial = backdropMaterial;
        backdrop.color = Color.white;
        CopySorting(sourceRenderer, backdrop, -4);
    }

    private void CreateGroundSeparationShadow()
    {
        if (groundShadowShader == null || softSprite == null || runtimeRoot == null)
            return;

        groundShadowMaterial = NewRuntimeMaterial(groundShadowShader, "Ice Button Ground Separation (Runtime)");
        groundShadowMaterial.SetTexture("_MainTex", softSprite.texture);
        groundShadowMaterial.SetColor("_ShadowColor", new Color(0.003f, 0.02f, 0.075f, 0.88f));

        Bounds bounds = sourceRenderer.bounds;
        GameObject shadowObject = new("Deep Navy Contact Shadow");
        shadowObject.transform.SetParent(runtimeRoot, false);
        shadowObject.transform.position = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.07f, bounds.center.z);
        shadowObject.transform.rotation = Quaternion.identity;
        shadowObject.transform.localScale = new Vector3(bounds.size.x * 0.94f, bounds.size.y * 0.24f, 1f);

        SpriteRenderer shadow = shadowObject.AddComponent<SpriteRenderer>();
        shadow.sprite = softSprite;
        shadow.sharedMaterial = groundShadowMaterial;
        shadow.color = Color.white;
        CopySorting(sourceRenderer, shadow, -3);
    }

    private void CreateGoldenWellBeacon(float width, float height)
    {
        if (particleShader == null || softSprite == null || runtimeRoot == null)
            return;

        wellGlowMaterial = CreateParticleMaterial(softSprite.texture, "Golden Well Beacon Material", 0.52f);
        wellGlowMaterial.SetColor("_TintColor", new Color(1.3f, 0.62f, 0.12f, 1f));

        GameObject beaconObject = new("Golden Well Gameplay Beacon");
        beaconObject.transform.SetParent(runtimeRoot, false);
        beaconObject.transform.localPosition = new Vector3(0f, height * 0.03f, 0f);
        beaconObject.transform.localRotation = Quaternion.identity;
        wellBeaconBaseScale = new Vector3(width * 1.26f * wellGlowSize, height * 0.76f * wellGlowSize, 1f);
        beaconObject.transform.localScale = wellBeaconBaseScale;

        wellBeacon = beaconObject.AddComponent<SpriteRenderer>();
        wellBeacon.sprite = softSprite;
        wellBeacon.sharedMaterial = wellGlowMaterial;
        wellBeacon.color = new Color(1f, 1f, 1f, wellGlowAlpha);
        CopySorting(sourceRenderer, wellBeacon, -1);
    }

    private Material CreateParticleMaterial(Texture texture, string materialName, float softness)
    {
        Material material = NewRuntimeMaterial(particleShader, materialName);
        material.SetTexture("_MainTex", texture);
        material.SetColor("_TintColor", Color.white);
        material.SetFloat("_Softness", softness);
        return material;
    }

    private Material CreateSmokeMaterial(Texture texture)
    {
        Material material = NewRuntimeMaterial(smokeShader, "Golden Well Volumetric Smoke Material");
        material.SetTexture("_MainTex", texture);
        material.SetColor("_TintColor", Color.white);
        material.SetFloat("_Softness", 0.56f);
        material.SetFloat("_Opacity", smokeOpacity);
        return material;
    }

    private static Material NewRuntimeMaterial(Shader shader, string materialName)
    {
        return new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private bool IsActivator(Collider2D other)
    {
        if (other == null)
            return false;

        Transform candidate = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
        while (candidate != null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(activatorTag) && candidate.CompareTag(activatorTag))
                    return true;
            }
            catch (UnityException)
            {
                return false;
            }

            candidate = candidate.parent;
        }

        return false;
    }

    private static void CopySorting(SpriteRenderer source, SpriteRenderer destination, int offset)
    {
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder + offset;
    }

    private static float SafeReciprocal(float value)
    {
        return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
    }

    private static Sprite CreateSoftSprite(int size)
    {
        Texture2D texture = NewTexture(size, "Ice Button Soft Particle Texture");
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float distance = uv.magnitude;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.7f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return NewSprite(texture, "Ice Button Soft Particle Sprite");
    }

    private static Sprite CreateSmokeSprite(int size)
    {
        Texture2D texture = NewTexture(size, "Golden Well Smoke Texture");
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                uv.x *= 0.82f;
                float body = Mathf.Pow(Mathf.Clamp01(1f - uv.magnitude), 1.18f);
                float noiseA = Mathf.PerlinNoise((uv.x + 1.8f) * 2.75f, (uv.y + 2.2f) * 3.1f);
                float noiseB = Mathf.PerlinNoise((uv.x + 4.3f) * 5.2f, (uv.y + 1.1f) * 4.65f);
                float billow = Mathf.Lerp(0.38f, 1f, noiseA * 0.72f + noiseB * 0.28f);
                float alpha = Mathf.Pow(Mathf.Clamp01(body * (0.72f + billow * 0.55f)), 0.72f);
                pixels[y * size + x] = new Color(1f, 0.9f, 0.62f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return NewSprite(texture, "Golden Well Smoke Sprite");
    }

    private static Sprite CreateShardSprite(int size)
    {
        Texture2D texture = NewTexture(size, "Ice Button Crystal Shard Texture");
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new(Mathf.Abs((x + 0.5f) / size * 2f - 1f), Mathf.Abs((y + 0.5f) / size * 2f - 1f));
                float diamond = uv.x * 1.75f + uv.y;
                float alpha = Mathf.SmoothStep(1f, 0.72f, diamond);
                float core = Mathf.SmoothStep(0.9f, 0.15f, diamond);
                pixels[y * size + x] = new Color(0.65f + core * 0.35f, 0.9f + core * 0.1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return NewSprite(texture, "Ice Button Crystal Shard Sprite");
    }

    private static Sprite CreateRingSprite(int size)
    {
        Texture2D texture = NewTexture(size, "Ice Button Shockwave Texture");
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float radius = uv.magnitude;
                float ring = 1f - Mathf.Clamp01(Mathf.Abs(radius - 0.73f) / 0.14f);
                ring = ring * ring * (3f - 2f * ring);
                pixels[y * size + x] = new Color(0.55f, 0.95f, 1f, ring);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return NewSprite(texture, "Ice Button Shockwave Sprite");
    }

    private static Texture2D NewTexture(int size, string textureName)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private static Sprite NewSprite(Texture2D texture, string spriteName)
    {
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
        sprite.name = spriteName;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private void OnDestroy()
    {
        if (sourceRenderer != null && sourceRenderer.sharedMaterial == surfaceGradeMaterial)
            sourceRenderer.sharedMaterial = originalSourceMaterial;

        DestroyRuntimeObject(surfaceGradeMaterial);
        DestroyRuntimeObject(runeMaterial);
        DestroyRuntimeObject(haloMaterial);
        DestroyRuntimeObject(topRingMaterial);
        DestroyRuntimeObject(backdropMaterial);
        DestroyRuntimeObject(groundShadowMaterial);
        DestroyRuntimeObject(wellGlowMaterial);
        DestroyRuntimeObject(softParticleMaterial);
        DestroyRuntimeObject(smokeParticleMaterial);
        DestroyRuntimeObject(shardParticleMaterial);
        DestroyRuntimeObject(softSprite);
        DestroyRuntimeObject(smokeSprite);
        DestroyRuntimeObject(shardSprite);
        DestroyRuntimeObject(ringSprite);
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target != null)
            Destroy(target);
    }
}

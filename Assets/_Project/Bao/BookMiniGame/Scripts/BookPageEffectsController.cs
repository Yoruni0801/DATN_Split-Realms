using echo17.EndlessBook;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class BookPageEffectsController : MonoBehaviour
{
    private static readonly int EffectModeId = Shader.PropertyToID("_EffectMode");
    private static readonly int PageSideId = Shader.PropertyToID("_PageSide");
    private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
    private static readonly int PetrifyAmountId = Shader.PropertyToID("_PetrifyAmount");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int PageTextureId = Shader.PropertyToID("_PageTexture");
    private static readonly int PageTextureStId = Shader.PropertyToID("_PageTexture_ST");

    private static readonly Color[] SpreadColors =
    {
        new Color(1f, 0.12f, 0.025f),
        new Color(0.48f, 0.62f, 0.51f),
        // Same amethyst palette as the crystal illustration on spread 5-6.
        new Color(0.57f, 0.12f, 1f),
        new Color(0.18f, 0.42f, 0.86f)
    };

    private static readonly float[] SpreadLightIntensities = { 0f, 0f, 0f, 0.72f };
    private static readonly float[] SpreadOverlayIntensities = { 1.0f, 0.86f, 1.28f, 0.76f };

    private EndlessBook book;
    private GameObject overlayRoot;
    private Material overlayMaterial;
    private Mesh pageOneMesh;
    private Mesh pageTwoMesh;
    private MeshRenderer pageOneRenderer;
    private MeshRenderer pageTwoRenderer;
    private MaterialPropertyBlock pageOneBlock;
    private MaterialPropertyBlock pageTwoBlock;
    private Light pageLight;
    private ParticleSystem motes;
    private Material particleMaterial;
    private Texture2D particleTexture;
    private Vector3 effectsCenter;
    private Vector3 effectsSize = new Vector3(0.68f, 0.02f, 0.38f);
    private int activeSpread = -1;
    private float spreadTime;
    private float visibility;
    private float petrifyAmount;

    private void Awake()
    {
        book = GetComponent<EndlessBook>();
        if (book == null)
        {
            enabled = false;
            return;
        }

        Shader overlayShader = Resources.Load<Shader>("BookPageMagic");
        if (overlayShader == null) overlayShader = Shader.Find("Custom/Book/Page Magic Overlay");
        if (overlayShader == null || !overlayShader.isSupported)
        {
            Debug.LogError("[BookPageEffects] Không tìm thấy shader overlay tương thích URP.", this);
            enabled = false;
            return;
        }

        CreateSpreadOverlay(overlayShader);
        CreatePageLight();
        CreateMagicMotes();
        SetVisualsActive(false);
    }

    private void LateUpdate()
    {
        bool pagesVisible = book.CurrentState == EndlessBook.StateEnum.OpenMiddle && !book.IsTurningPages;
        visibility = Mathf.MoveTowards(visibility, pagesVisible ? 1f : 0f, Time.deltaTime * 5.5f);

        if (pagesVisible)
        {
            int spread = Mathf.Clamp((book.CurrentLeftPageNumber - 1) / 2, 0, SpreadColors.Length - 1);
            if (spread != activeSpread)
            {
                activeSpread = spread;
                spreadTime = 0f;
                petrifyAmount = 0f;
                ApplyPageTextures();
                ApplySpreadAtmosphere(spread);
            }

            spreadTime += Time.deltaTime;
            if (activeSpread == 1)
            {
                float normalizedTime = Mathf.Clamp01((spreadTime - 0.2f) / 3.2f);
                petrifyAmount = normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
            }
        }
        else
        {
            activeSpread = -1;
            spreadTime = 0f;
            petrifyAmount = 0f;
        }

        SetVisualsActive(pagesVisible);
        UpdateOverlay();
        UpdateAtmosphere(pagesVisible);
    }

    private void CreateSpreadOverlay(Shader overlayShader)
    {
        Transform standin = transform.Find("Standins/BookStandinOpenMiddle_High");
        MeshFilter sourceFilter = standin != null ? standin.GetComponent<MeshFilter>() : null;
        Mesh oddTemplate = Resources.Load<Mesh>("BookPageOddOverlay");
        Mesh evenTemplate = Resources.Load<Mesh>("BookPageEvenOverlay");
        if (standin == null || sourceFilter == null || sourceFilter.sharedMesh == null || oddTemplate == null || evenTemplate == null)
        {
            Debug.LogWarning("[BookPageEffects] Không tìm thấy overlay mesh đã bake, dùng fallback.", this);
            CreateFallbackOverlay(overlayShader);
            return;
        }

        pageOneMesh = Instantiate(evenTemplate);
        pageTwoMesh = Instantiate(oddTemplate);
        pageOneMesh.name = "Page 1 VFX Mesh (Runtime)";
        pageTwoMesh.name = "Page 2 VFX Mesh (Runtime)";

        overlayRoot = new GameObject("Page VFX Overlay");
        overlayRoot.transform.SetParent(transform, false);
        overlayRoot.transform.localPosition = standin.localPosition;
        overlayRoot.transform.localRotation = standin.localRotation;
        overlayRoot.transform.localScale = standin.localScale;

        overlayMaterial = new Material(overlayShader)
        {
            name = "Tối Cổ Hội Thư Page Overlay (Runtime)",
            renderQueue = (int)RenderQueue.Transparent + 25
        };

        pageOneBlock = new MaterialPropertyBlock();
        pageTwoBlock = new MaterialPropertyBlock();
        pageOneRenderer = CreateOverlayRenderer("Page 1 VFX", pageOneMesh, pageOneBlock, 0f);
        pageTwoRenderer = CreateOverlayRenderer("Page 2 VFX", pageTwoMesh, pageTwoBlock, 1f);

        Bounds sourceBounds = sourceFilter.sharedMesh.bounds;
        effectsCenter = transform.InverseTransformPoint(standin.TransformPoint(sourceBounds.center));
        effectsSize = sourceBounds.size;
    }
    private MeshRenderer CreateOverlayRenderer(string objectName, Mesh mesh, MaterialPropertyBlock propertyBlock, float pageSide)
    {
        GameObject overlay = new GameObject(objectName);
        overlay.transform.SetParent(overlayRoot.transform, false);

        MeshFilter meshFilter = overlay.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = overlay.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = overlayMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        propertyBlock.SetFloat(PageSideId, pageSide);
        meshRenderer.SetPropertyBlock(propertyBlock);
        return meshRenderer;
    }

    private void ApplyPageTextures()
    {
        ApplyPageTexture(pageOneRenderer, pageOneBlock, book.CurrentLeftPageNumber);
        ApplyPageTexture(pageTwoRenderer, pageTwoBlock, book.CurrentRightPageNumber);
    }

    private void ApplyPageTexture(MeshRenderer renderer, MaterialPropertyBlock propertyBlock, int pageNumber)
    {
        PageData page = book.GetPageData(pageNumber);
        Material pageMaterial = page != null ? page.material : null;
        Texture pageTexture = pageMaterial != null && pageMaterial.mainTexture != null
            ? pageMaterial.mainTexture
            : Texture2D.whiteTexture;
        Vector2 textureScale = pageMaterial != null ? pageMaterial.mainTextureScale : Vector2.one;
        Vector2 textureOffset = pageMaterial != null ? pageMaterial.mainTextureOffset : Vector2.zero;

        propertyBlock.SetTexture(PageTextureId, pageTexture);
        propertyBlock.SetVector(PageTextureStId, new Vector4(textureScale.x, textureScale.y, textureOffset.x, textureOffset.y));
        renderer.SetPropertyBlock(propertyBlock);
    }

    private void CreateFallbackOverlay(Shader overlayShader)
    {
        overlayRoot = new GameObject("Page VFX Overlay");
        overlayRoot.transform.SetParent(transform, false);
        overlayMaterial = new Material(overlayShader) { name = "Tối Cổ Hội Thư Page Overlay (Runtime)" };
        pageOneMesh = CreateFallbackMesh("Page 1 VFX Mesh", -0.34f, -0.006f, 0f, 1f);
        pageTwoMesh = CreateFallbackMesh("Page 2 VFX Mesh", 0.006f, 0.34f, 0.5f, 1f);
        pageOneBlock = new MaterialPropertyBlock();
        pageTwoBlock = new MaterialPropertyBlock();
        pageOneRenderer = CreateOverlayRenderer("Page 1 VFX", pageOneMesh, pageOneBlock, 0f);
        pageTwoRenderer = CreateOverlayRenderer("Page 2 VFX", pageTwoMesh, pageTwoBlock, 1f);
        effectsCenter = new Vector3(0f, 0.037f, 0f);
        effectsSize = new Vector3(0.68f, 0.02f, 0.38f);
    }

    private Mesh CreateFallbackMesh(string meshName, float minX, float maxX, float minU, float maxU)
    {
        Mesh mesh = new Mesh { name = meshName };
        mesh.vertices = new[]
        {
            new Vector3(minX, 0.037f, -0.19f),
            new Vector3(minX, 0.037f, 0.19f),
            new Vector3(maxX, 0.037f, 0.19f),
            new Vector3(maxX, 0.037f, -0.19f)
        };
        mesh.uv = new[]
        {
            new Vector2(minU, 0f),
            new Vector2(minU, 1f),
            new Vector2(maxU, 1f),
            new Vector2(maxU, 0f)
        };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void CreatePageLight()
    {
        GameObject lightObject = new GameObject("Page Magic Light");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = effectsCenter + Vector3.up * 0.11f;

        pageLight = lightObject.AddComponent<Light>();
        pageLight.type = LightType.Point;
        pageLight.color = SpreadColors[0];
        pageLight.range = Mathf.Max(effectsSize.x, effectsSize.z) * 2.5f;
        pageLight.intensity = 0f;
        pageLight.shadows = LightShadows.None;
        pageLight.renderMode = LightRenderMode.ForcePixel;
    }

    private void CreateMagicMotes()
    {
        GameObject particleObject = new GameObject("Page Magic Motes");
        particleObject.transform.SetParent(transform, false);
        particleObject.transform.localPosition = effectsCenter + Vector3.up * 0.02f;

        motes = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = motes.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 120;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.004f, 0.014f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.016f);
        main.gravityModifier = -0.001f;

        ParticleSystem.ShapeModule shape = motes.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(effectsSize.x * 0.82f, 0.004f, effectsSize.z * 0.78f);

        ParticleSystem.NoiseModule noise = motes.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.012f;
        noise.frequency = 0.7f;
        noise.scrollSpeed = 0.1f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = motes.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = CreateParticleGradient(SpreadColors[0]);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = motes.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.18f, 1f),
            new Keyframe(0.76f, 0.72f),
            new Keyframe(1f, 0f)));

        ParticleSystemRenderer particleRenderer = motes.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        particleTexture = CreateParticleTexture();
        Shader particleShader = Shader.Find("Custom/Book/Soft Spark");
        if (particleShader != null)
        {
            particleMaterial = new Material(particleShader) { name = "Book Magic Soft Spark (Runtime)" };
            particleMaterial.SetTexture("_MainTex", particleTexture);
            particleMaterial.SetColor("_Color", Color.white);
            particleRenderer.sharedMaterial = particleMaterial;
        }
    }

    private void ApplySpreadAtmosphere(int spread)
    {
        Color accent = SpreadColors[spread];
        pageLight.color = accent;

        ParticleSystem.MainModule main = motes.main;
        ParticleSystem.EmissionModule emission = motes.emission;
        ParticleSystem.NoiseModule noise = motes.noise;
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = motes.colorOverLifetime;

        switch (spread)
        {
            case 0:
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.018f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.012f);
                main.gravityModifier = -0.002f;
                emission.rateOverTime = 0f;
                noise.strength = 0.018f;
                break;
            case 1:
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.7f, 3.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.002f, 0.01f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.011f);
                main.gravityModifier = 0.002f;
                emission.rateOverTime = 0f;
                noise.strength = 0.009f;
                break;
            case 2:
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.25f, 2.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.006f, 0.02f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.005f, 0.015f);
                main.gravityModifier = -0.003f;
                emission.rateOverTime = 0f;
                noise.strength = 0.02f;
                break;
            default:
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.001f, 0.008f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.009f, 0.025f);
                main.gravityModifier = -0.0005f;
                emission.rateOverTime = 12f;
                noise.strength = 0.007f;
                break;
        }

        colorOverLifetime.color = CreateParticleGradient(accent);
        motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        motes.Play(true);
    }

    private void UpdateOverlay()
    {
        if (overlayMaterial == null) return;

        int spread = Mathf.Clamp(activeSpread, 0, SpreadColors.Length - 1);
        float pulse = 0.88f + Mathf.Sin(spreadTime * (spread == 3 ? 1.1f : 2.25f)) * 0.12f;

        overlayMaterial.SetFloat(EffectModeId, spread);
        // Crystal visuals belong to BookPageSubjectVFXController and must remain
        // clipped to the crystal sprite. Only the cave spread uses a page overlay.
        overlayMaterial.SetFloat(VisibilityId, spread == 3 ? visibility : 0f);
        overlayMaterial.SetFloat(PetrifyAmountId, petrifyAmount);
        overlayMaterial.SetFloat(PhaseId, spreadTime);
        overlayMaterial.SetFloat(IntensityId, SpreadOverlayIntensities[spread] * pulse);
        overlayMaterial.SetColor(TintId, SpreadColors[spread]);
    }

    private void UpdateAtmosphere(bool pagesVisible)
    {
        int spread = Mathf.Clamp(activeSpread, 0, SpreadColors.Length - 1);
        float lightPulse = 0.88f + Mathf.Sin(spreadTime * (spread == 3 ? 1.05f : 2.05f)) * 0.12f;
        pageLight.intensity = pagesVisible ? visibility * SpreadLightIntensities[spread] * lightPulse : 0f;

        float horizontalDrift = 0f;
        if (spread == 0) horizontalDrift = -effectsSize.x * 0.12f;
        if (spread == 2) horizontalDrift = effectsSize.x * 0.1f;
        pageLight.transform.localPosition = effectsCenter + new Vector3(horizontalDrift, 0.11f, 0f);

        if (pagesVisible)
        {
            if (!motes.gameObject.activeSelf) motes.gameObject.SetActive(true);
            if (!motes.isPlaying) motes.Play(true);
        }
        else
        {
            if (motes.isPlaying) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (motes.gameObject.activeSelf) motes.gameObject.SetActive(false);
        }
    }

    private void SetVisualsActive(bool active)
    {
        if (overlayRoot != null && overlayRoot.activeSelf != active) overlayRoot.SetActive(active);
        if (pageLight != null) pageLight.enabled = active;
    }

    private ParticleSystem.MinMaxGradient CreateParticleGradient(Color accent)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(Color.white, accent, 0.35f), 0f),
                new GradientColorKey(accent, 0.42f),
                new GradientColorKey(Color.Lerp(accent, Color.black, 0.28f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.8f, 0.18f),
                new GradientAlphaKey(0.38f, 0.68f),
                new GradientAlphaKey(0f, 1f)
            });
        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private Texture2D CreateParticleTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Book Magic Soft Spark",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2((x + 0.5f) / size, (y + 0.5f) / size) * 2f - Vector2.one;
                float radial = Mathf.Clamp01(1f - point.magnitude);
                float core = radial * radial * radial;
                float cross = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Min(Mathf.Abs(point.x), Mathf.Abs(point.y)) * 10f), 6f) * radial * 0.22f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(core + cross));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDestroy()
    {
        if (overlayMaterial != null) Destroy(overlayMaterial);
        if (pageOneMesh != null) Destroy(pageOneMesh);
        if (pageTwoMesh != null) Destroy(pageTwoMesh);
        if (particleMaterial != null) Destroy(particleMaterial);
        if (particleTexture != null) Destroy(particleTexture);
    }
}



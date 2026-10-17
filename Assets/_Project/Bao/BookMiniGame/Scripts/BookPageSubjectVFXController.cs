using echo17.EndlessBook;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class BookPageSubjectVFXController : MonoBehaviour
{
    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int EffectModeId = Shader.PropertyToID("_EffectMode");
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");
    private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int SpriteScaleId = Shader.PropertyToID("_SpriteScale");
    private static readonly int SpriteTargetId = Shader.PropertyToID("_SpriteTarget");
    private static readonly int SpriteCropMinId = Shader.PropertyToID("_SpriteCropMin");
    private static readonly int SpriteCropMaxId = Shader.PropertyToID("_SpriteCropMax");

    private static readonly Color[] ModeColors =
    {
        new Color(1f, 0.08f, 0.015f),
        new Color(0.48f, 0.68f, 0.54f),
        // Amethyst reference: a saturated violet shell with a white-lavender core.
        new Color(0.57f, 0.12f, 1f)
    };

    private static readonly float[] ModeIntensities = { 1.15f, 0.95f, 1.68f };

    private static readonly Vector2[] SpriteScales =
    {
        new Vector2(0.673f, 0.649f),
        new Vector2(0.730f, 0.610f),
        new Vector2(1.025f, 0.840f)
    };

    private static readonly Vector4[] SpriteTargets =
    {
        // Page artwork is packed into EndlessBook's visible U=0..0.8 range.
        // Shift anchors left by 0.125 so overlays stay on top of the subjects
        // instead of following the old cropped-texture layout.
        new Vector4(0.223f, 0.194f, 0f, 0f),
        new Vector4(0.319f, 0.200f, 0f, 0f),
        new Vector4(0.409f, 0.065f, 0f, 0f)
    };

    private static readonly Vector4[] SpriteCropMins =
    {
        new Vector4(0.1244f, 0.0829f, 0f, 0f),
        new Vector4(0.2831f, 0.0805f, 0f, 0f),
        new Vector4(0.4019f, 0.0566f, 0f, 0f)
    };

    private static readonly Vector4[] SpriteCropMaxs =
    {
        new Vector4(0.9904f, 0.8102f, 0f, 0f),
        new Vector4(0.7225f, 0.8732f, 0f, 0f),
        new Vector4(0.5917f, 0.8365f, 0f, 0f)
    };

    [System.Serializable]
    public sealed class SubjectTuning
    {
        [Tooltip("Bù vị trí trên trang. +X sang phải, +Y lên trên.")]
        public Vector2 pageOffset = Vector2.zero;

        [Tooltip("Nhân scale riêng theo chiều ngang và dọc.")]
        public Vector2 scaleMultiplier = Vector2.one;

        [Range(0f, 2f)]
        public float effectIntensity = 1f;

        [Range(0f, 2f)]
        public float particleAmount = 1f;

        [Range(0f, 2f)]
        public float lightIntensity = 1f;
    }

    [Header("Subject Sprites")]
    public Texture2D bossSprite;
    public Texture2D boySprite;
    public Texture2D crystalSprite;

    [Header("Manual Prefab Tuning")]
    public SubjectTuning bossTuning = new SubjectTuning();
    public SubjectTuning boyTuning = new SubjectTuning();
    public SubjectTuning crystalTuning = new SubjectTuning();

    [Tooltip("Khoảng nâng overlay khỏi mặt giấy để tránh z-fighting.")]
    [Min(0.0001f)]
    public float surfaceOffset = 0.0016f;

    [Header("Page Transition")]
    [Tooltip("Khoảng nghỉ ngắn sau khi trang nằm phẳng trước khi hiệu ứng xuất hiện.")]
    [Range(0f, 0.5f)]
    public float revealDelay = 0.08f;

    [Tooltip("Thời gian hiệu ứng hiện mềm sau khi hoàn tất lật trang.")]
    [Range(0.05f, 1f)]
    public float revealDuration = 0.32f;

    private EndlessBook book;
    private GameObject overlayRoot;
    private GameObject subjectObject;
    private Material subjectMaterial;
    private Mesh subjectMesh;
    private MeshRenderer subjectRenderer;
    private ParticleSystem particles;
    private Material particleMaterial;
    private Texture2D particleTexture;
    private Light subjectLight;
    private Vector3 subjectBasePosition;
    private Vector3 subjectCenter;
    private Vector3 subjectSize;
    private int activeMode = -1;
    private float phase;
    private float visibility;
    private float petrifyProgress;
    private float revealTimer;

    private void Awake()
    {
        book = GetComponent<EndlessBook>();
        if (book == null)
        {
            enabled = false;
            return;
        }

        Shader subjectShader = Resources.Load<Shader>("BookSubjectVFX");
        if (subjectShader == null) subjectShader = Shader.Find("Custom/Book/Subject VFX");
        Mesh pageTemplate = Resources.Load<Mesh>("BookPageEvenOverlay");
        Transform standin = transform.Find("Standins/BookStandinOpenMiddle_High");
        if (subjectShader == null || !subjectShader.isSupported || pageTemplate == null || standin == null)
        {
            Debug.LogError("[BookSubjectVFX] Thiếu shader, page mesh hoặc standin.", this);
            enabled = false;
            return;
        }

        CreateSubjectOverlay(subjectShader, pageTemplate, standin);
        CreateSubjectLight();
        CreateSubjectParticles();
        SetVisualsActive(false);
    }

    private void LateUpdate()
    {
        bool pageIsStable = book.CurrentState == EndlessBook.StateEnum.OpenMiddle
            && !book.IsTurningPages
            && !book.IsDraggingPage;

        if (!pageIsStable)
        {
            HideForPageMotion();
            return;
        }

        int spread = Mathf.Clamp((book.CurrentLeftPageNumber - 1) / 2, 0, 3);
        int desiredMode = spread >= 0 && spread <= 2 ? spread : -1;

        if (desiredMode != activeMode)
        {
            activeMode = desiredMode;
            phase = 0f;
            visibility = 0f;
            petrifyProgress = 0f;
            revealTimer = 0f;
            ConfigureMode(activeMode);
        }

        if (activeMode >= 0)
        {
            phase += Time.deltaTime;
            revealTimer += Time.deltaTime;
            float revealProgress = Mathf.Clamp01((revealTimer - revealDelay) / Mathf.Max(0.05f, revealDuration));
            visibility = revealProgress * revealProgress * (3f - 2f * revealProgress);

            if (activeMode == 1)
            {
                float normalizedTime = Mathf.Clamp01((phase - 0.2f) / 3.4f);
                petrifyProgress = normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
            }
        }
        else
        {
            visibility = 0f;
        }

        UpdateMaterial();
        UpdateAtmosphere();
        SetVisualsActive(activeMode >= 0 || visibility > 0.001f);
    }

    private void HideForPageMotion()
    {
        if (activeMode >= 0)
        {
            activeMode = -1;
            ConfigureMode(activeMode);
        }

        visibility = 0f;
        petrifyProgress = 0f;
        revealTimer = 0f;
        UpdateMaterial();
        UpdateAtmosphere();
        SetVisualsActive(false);
    }

    private void CreateSubjectOverlay(Shader shader, Mesh template, Transform standin)
    {
        subjectMesh = CreateNormalizedPageMesh(template);
        overlayRoot = new GameObject("Page Subject VFX");
        overlayRoot.transform.SetParent(transform, false);
        overlayRoot.transform.localPosition = standin.localPosition;
        overlayRoot.transform.localRotation = standin.localRotation;
        overlayRoot.transform.localScale = standin.localScale;

        subjectObject = new GameObject("Left Page Subject");
        subjectObject.transform.SetParent(overlayRoot.transform, false);
        subjectBasePosition = Vector3.up * surfaceOffset;
        subjectObject.transform.localPosition = subjectBasePosition;

        MeshFilter meshFilter = subjectObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = subjectMesh;

        subjectMaterial = new Material(shader)
        {
            name = "Tối Cổ Hội Thư Subject VFX (Runtime)",
            renderQueue = (int)RenderQueue.Transparent + 35
        };

        subjectRenderer = subjectObject.AddComponent<MeshRenderer>();
        subjectRenderer.sharedMaterial = subjectMaterial;
        subjectRenderer.shadowCastingMode = ShadowCastingMode.Off;
        subjectRenderer.receiveShadows = false;
        subjectRenderer.lightProbeUsage = LightProbeUsage.Off;
        subjectRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        subjectRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private Mesh CreateNormalizedPageMesh(Mesh template)
    {
        Mesh mesh = Instantiate(template);
        mesh.name = "Left Page Subject Mesh (Runtime)";
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        bool[] usedVertices = new bool[vertices.Length];
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;

        foreach (int index in triangles)
        {
            usedVertices[index] = true;
            Vector3 vertex = vertices[index];
            minX = Mathf.Min(minX, vertex.x);
            maxX = Mathf.Max(maxX, vertex.x);
            minZ = Mathf.Min(minZ, vertex.z);
            maxZ = Mathf.Max(maxZ, vertex.z);
        }

        Vector2[] uvs = new Vector2[vertices.Length];
        for (int index = 0; index < vertices.Length; index++)
        {
            if (!usedVertices[index]) continue;
            float u = Mathf.InverseLerp(minX, maxX, vertices[index].x);
            float v = Mathf.InverseLerp(minZ, maxZ, vertices[index].z);
            uvs[index] = new Vector2(u, v);
        }

        mesh.uv = uvs;
        mesh.RecalculateBounds();
        subjectCenter = new Vector3((minX + maxX) * 0.5f, 0.025f, (minZ + maxZ) * 0.5f);
        subjectSize = new Vector3(maxX - minX, 0.01f, maxZ - minZ);
        return mesh;
    }

    private void CreateSubjectLight()
    {
        GameObject lightObject = new GameObject("Subject Magic Light");
        lightObject.transform.SetParent(overlayRoot.transform, false);
        lightObject.transform.localPosition = subjectCenter + Vector3.up * 0.065f;
        subjectLight = lightObject.AddComponent<Light>();
        subjectLight.type = LightType.Point;
        subjectLight.range = Mathf.Max(subjectSize.x, subjectSize.z) * 1.05f;
        subjectLight.intensity = 0f;
        subjectLight.shadows = LightShadows.None;
        subjectLight.renderMode = LightRenderMode.ForcePixel;
    }

    private void CreateSubjectParticles()
    {
        GameObject particleObject = new GameObject("Subject Magic Particles");
        particleObject.transform.SetParent(overlayRoot.transform, false);
        particleObject.transform.localPosition = subjectCenter + Vector3.up * 0.018f;
        particles = particleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 80;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.003f, 0.012f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.011f);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(subjectSize.x * 0.56f, 0.004f, subjectSize.z * 0.58f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.012f;
        noise.frequency = 0.85f;
        noise.scrollSpeed = 0.12f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = CreateParticleGradient(ModeColors[0]);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.18f, 1f),
            new Keyframe(0.78f, 0.58f),
            new Keyframe(1f, 0f)));

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        particleTexture = CreateParticleTexture();
        Shader particleShader = Resources.Load<Shader>("BookSoftSpark");
        if (particleShader == null) particleShader = Shader.Find("Custom/Book/Soft Spark");
        if (particleShader != null)
        {
            particleMaterial = new Material(particleShader) { name = "Book Subject Soft Spark (Runtime)" };
            particleMaterial.SetTexture("_MainTex", particleTexture);
            particleMaterial.SetColor("_Color", Color.white);
            particleRenderer.sharedMaterial = particleMaterial;
        }
    }

    private void ConfigureMode(int mode)
    {
        if (mode < 0)
        {
            if (particles != null && particles.gameObject.activeInHierarchy)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            return;
        }

        if (particles != null && !particles.gameObject.activeSelf)
        {
            particles.gameObject.SetActive(true);
        }

        Texture2D texture = mode == 0 ? bossSprite : mode == 1 ? boySprite : crystalSprite;
        subjectMaterial.SetTexture(MainTextureId, texture != null ? texture : Texture2D.blackTexture);
        subjectMaterial.SetColor(TintId, Color.white);
        subjectMaterial.SetFloat(EffectModeId, mode);
        SubjectTuning tuning = GetTuning(mode);
        Vector2 scale = Vector2.Scale(SpriteScales[mode], tuning.scaleMultiplier);
        Vector4 target = SpriteTargets[mode];
        target.x += tuning.pageOffset.x;
        target.y += tuning.pageOffset.y;

        subjectMaterial.SetFloat(IntensityId, ModeIntensities[mode] * tuning.effectIntensity);
        subjectMaterial.SetVector(SpriteScaleId, new Vector4(scale.x, scale.y, 0f, 0f));
        subjectMaterial.SetVector(SpriteTargetId, target);
        subjectMaterial.SetVector(SpriteCropMinId, SpriteCropMins[mode]);
        subjectMaterial.SetVector(SpriteCropMaxId, SpriteCropMaxs[mode]);
        subjectLight.color = ModeColors[mode];
        ConfigureSubjectBounds(mode, target, scale);

        ParticleSystem.MainModule main = particles.main;
        ParticleSystem.EmissionModule emission = particles.emission;
        ParticleSystem.NoiseModule noise = particles.noise;
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;

        if (mode == 0)
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 2.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.003f, 0.012f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.0025f, 0.008f);
            main.gravityModifier = -0.0015f;
            emission.rateOverTime = 10f * tuning.particleAmount;
            noise.strength = 0.014f;
        }
        else if (mode == 1)
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.001f, 0.007f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.009f);
            main.gravityModifier = 0.002f;
            emission.rateOverTime = 7f * tuning.particleAmount;
            noise.strength = 0.006f;
        }
        else
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 2.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.007f, 0.022f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.0025f, 0.0105f);
            main.gravityModifier = -0.003f;
            emission.rateOverTime = 19f * tuning.particleAmount;
            noise.strength = 0.011f;
        }

        colorOverLifetime.color = CreateParticleGradient(ModeColors[mode]);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.Play(true);
    }

    private SubjectTuning GetTuning(int mode)
    {
        if (mode == 0) return bossTuning ?? (bossTuning = new SubjectTuning());
        if (mode == 1) return boyTuning ?? (boyTuning = new SubjectTuning());
        return crystalTuning ?? (crystalTuning = new SubjectTuning());
    }

    private void ConfigureSubjectBounds(int mode, Vector4 target, Vector2 scale)
    {
        if (subjectLight == null || particles == null) return;

        Vector2 cropMin = new Vector2(SpriteCropMins[mode].x, SpriteCropMins[mode].y);
        Vector2 cropMax = new Vector2(SpriteCropMaxs[mode].x, SpriteCropMaxs[mode].y);
        Vector2 visibleMin = new Vector2(target.x, target.y);
        Vector2 visibleMax = visibleMin + Vector2.Scale(scale, cropMax - cropMin);
        Vector2 visibleCenter = (visibleMin + visibleMax) * 0.5f;
        Vector2 visibleSize = visibleMax - visibleMin;

        Vector3 localCenter = subjectCenter + new Vector3(
            (visibleCenter.x - 0.5f) * subjectSize.x,
            0f,
            (visibleCenter.y - 0.5f) * subjectSize.z);

        subjectLight.transform.localPosition = localCenter + Vector3.up * 0.065f;
        subjectLight.range = Mathf.Max(visibleSize.x * subjectSize.x, visibleSize.y * subjectSize.z) * 1.4f;
        particles.transform.localPosition = localCenter + Vector3.up * 0.018f;

        ParticleSystem.ShapeModule shape = particles.shape;
        float horizontalCoverage = mode == 2 ? 0.42f : 0.78f;
        float verticalCoverage = mode == 2 ? 0.66f : 0.78f;
        shape.scale = new Vector3(
            Mathf.Max(0.015f, visibleSize.x * subjectSize.x * horizontalCoverage),
            0.004f,
            Mathf.Max(0.015f, visibleSize.y * subjectSize.z * verticalCoverage));
    }

    private void UpdateMaterial()
    {
        if (subjectMaterial == null) return;
        subjectMaterial.SetFloat(VisibilityId, visibility);
        subjectMaterial.SetFloat(PhaseId, phase);
        subjectMaterial.SetFloat(ProgressId, petrifyProgress);
    }

    private void UpdateAtmosphere()
    {
        if (activeMode < 0)
        {
            subjectLight.intensity = 0f;
            return;
        }

        float pulse = 0.82f + Mathf.Sin(phase * (activeMode == 2 ? 2.8f : 2.1f)) * 0.18f;
        float targetIntensity = activeMode == 0 ? 0.2f : activeMode == 1 ? 0.07f : 0.46f;
        subjectLight.intensity = visibility * targetIntensity * pulse * GetTuning(activeMode).lightIntensity;

        float lift = activeMode == 2
            ? Mathf.Sin(phase * 1.8f) * 0.0012f
            : activeMode == 0 ? Mathf.Sin(phase * 1.4f) * 0.00035f : 0f;
        subjectObject.transform.localPosition = subjectBasePosition + Vector3.up * lift;
    }

    private void SetVisualsActive(bool active)
    {
        if (overlayRoot != null && overlayRoot.activeSelf != active) overlayRoot.SetActive(active);
        if (subjectLight != null) subjectLight.enabled = active;
        if (particles != null && particles.gameObject.activeSelf != active) particles.gameObject.SetActive(active);
        if (active && activeMode >= 0 && particles != null && !particles.isPlaying) particles.Play(true);
    }

    private ParticleSystem.MinMaxGradient CreateParticleGradient(Color accent)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(Color.white, accent, 0.35f), 0f),
                new GradientColorKey(accent, 0.38f),
                new GradientColorKey(Color.Lerp(accent, Color.black, 0.22f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.78f, 0.16f),
                new GradientAlphaKey(0.35f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private Texture2D CreateParticleTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Book Subject Soft Spark",
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
                float cross = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Min(Mathf.Abs(point.x), Mathf.Abs(point.y)) * 10f), 6f) * radial * 0.18f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(core + cross));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDestroy()
    {
        if (subjectMaterial != null) Destroy(subjectMaterial);
        if (subjectMesh != null) Destroy(subjectMesh);
        if (particleMaterial != null) Destroy(particleMaterial);
        if (particleTexture != null) Destroy(particleTexture);
    }
}

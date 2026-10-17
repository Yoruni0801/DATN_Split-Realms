using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LaserMirrorCavePortal2D : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    //  VISUAL ELEMENTS — Các thành phần hiển thị chính của cổng
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Visual Elements ——————————————")]

    // Transform gốc của phần visual cổng (Quad/Sprite). Scale = kích thước cổng.
    [SerializeField] private Transform portalVisualRoot;

    // SpriteRenderer (nếu cổng dùng Sprite thay vì Mesh Quad).
    [SerializeField] private SpriteRenderer portalSpriteRenderer;

    // MeshRenderer (nếu cổng dùng Quad mesh + shader tùy chỉnh).
    [SerializeField] private MeshRenderer portalMeshRenderer;

    // Material ghi đè — kéo thả material cổng vào đây để ghi đè mặc định.
    [SerializeField] private Material portalMaterialOverride;

    // Tất cả ParticleSystem con của cổng (tự thu thập nếu để trống).
    [SerializeField] private ParticleSystem[] portalParticles;

    // ParticleSystem tàn lửa bay lên từ lòng cổng (tự tạo nếu để trống).
    [SerializeField] private ParticleSystem portalEmbersParticle;

    // ParticleSystem hút ngược năng lượng vào tâm trước khi mở cổng (Implosion).
    [SerializeField] private ParticleSystem portalImplosionParticle;

    // ParticleSystem phun trào tàn lửa ma thuật dữ dội khi cổng bùng nổ (Eruption).
    [SerializeField] private ParticleSystem portalEruptionParticle;

    // ═══════════════════════════════════════════════════════════════
    //  SHOCKWAVE & SEISMIC TREMOR — Sóng xung kích và rung chấn khai mở
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Shockwave & Seismic Tremor ——————————————")]

    // Bật/tắt sóng xung kích hình vòng tròn nở rộng khi cổng mở.
    [SerializeField] private bool enableShockwave = true;

    // SpriteRenderer của quầng sáng sóng xung kích (tự tạo nếu để trống).
    [SerializeField] private SpriteRenderer shockwaveRenderer;

    // Thời gian sóng xung kích quét từ tâm ra ngoài (giây).
    [SerializeField, Range(0.2f, 1.2f)] private float shockwaveDuration = 0.45f;

    // Bán kính quét tối đa của sóng xung kích (world unit).
    [SerializeField, Min(1f)] private float shockwaveMaxRadius = 8.0f;

    // Bật/tắt hiệu ứng rung chấn màn hình (Camera Shake) khi cổng khai mở.
    [SerializeField] private bool enableCameraShake = true;

    // Thời gian rung chấn màn hình (giây).
    [SerializeField, Range(0.1f, 1.0f)] private float cameraShakeDuration = 0.5f;

    // Cường độ rung chấn màn hình.
    [SerializeField, Range(0.05f, 0.6f)] private float cameraShakeMagnitude = 0.25f;

    // ═══════════════════════════════════════════════════════════════
    //  ENERGY PALETTE — Bảng màu năng lượng (đọc từ material cổng)
    // ═══════════════════════════════════════════════════════════════

    [System.Serializable]
    public struct PortalEnergyPalette
    {
        // Màu neon chính (viền phát sáng, tia laser, quầng sáng)
        public Color neonColor;
        // Màu lõi sáng nhất (trung tâm tia, flash crystal)
        public Color coreColor;
        // Màu ngọn lửa chính (phần nóng)
        public Color flameColorA;
        // Màu ngọn lửa phụ (phần tối/khói)
        public Color flameColorB;

        public static PortalEnergyPalette Default => new PortalEnergyPalette
        {
            neonColor = new Color(1.05f, 0.2f, 0.62f, 1f),
            coreColor = new Color(0.88f, 0.12f, 0.48f, 1f),
            flameColorA = new Color(0.62f, 0.045f, 0.2f, 1f),
            flameColorB = new Color(0.16f, 0.015f, 0.2f, 1f)
        };

        public bool EqualsPalette(PortalEnergyPalette other)
        {
            return neonColor == other.neonColor
                && coreColor == other.coreColor
                && flameColorA == other.flameColorA
                && flameColorB == other.flameColorB;
        }
    }

    private static readonly int PropNeonColor = Shader.PropertyToID("_NeonColor");
    private static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
    private static readonly int PropFlameColorA = Shader.PropertyToID("_FlameColorA");
    private static readonly int PropFlameColorB = Shader.PropertyToID("_FlameColorB");
    private static readonly int PropColor = Shader.PropertyToID("_Color");
    private static readonly int PropEmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int PropOpenProgress = Shader.PropertyToID("_OpenProgress");
    private static readonly int PropArchP0 = Shader.PropertyToID("_ArchP0");
    private static readonly int PropArchP1 = Shader.PropertyToID("_ArchP1");
    private static readonly int PropArchP2 = Shader.PropertyToID("_ArchP2");
    private static readonly int PropArchP3 = Shader.PropertyToID("_ArchP3");

    private MaterialPropertyBlock propertyBlock;

    // ═══════════════════════════════════════════════════════════════
    //  PORTAL SETTINGS — Cài đặt hoạt ảnh mở cổng
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Portal Open Settings ——————————————")]

    // Tự động mở cổng khi puzzle được giải (gọi từ LaserMirrorPuzzle2D).
    [SerializeField] private bool autoActivateOnPuzzleSolved = true;

    // Thời gian (giây) hoạt ảnh mở cổng từ 0% → 100%.
    [SerializeField, Min(0.1f)] private float openDuration = 1.0f;

    // Kích thước cổng khi mở hoàn toàn (width, height, 1).
    // Tự động tính nếu bật autoFitPortalToCrystals trong Puzzle.
    [SerializeField] private Vector3 openScale = new Vector3(7.8f, 4.9f, 1f);

    // Đường cong easing cho hoạt ảnh mở cổng.
    // EaseInOut = mở từ từ đầu cuối, nhanh ở giữa.
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ═══════════════════════════════════════════════════════════════
    //  TRIGGER & TRANSITION — Vùng trigger cho player đi vào cổng
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Trigger & Transition ——————————————")]

    // Collider2D vùng trigger — player chạm vào sẽ kích hoạt onPlayerEntered.
    // Tự tìm nếu để trống.
    [SerializeField] private Collider2D portalTrigger;

    // Tag của player object để nhận diện khi vào trigger.
    [SerializeField] private string playerTag = "Player";

    // Tự động chuyển Scene khi Player chạm vào cổng mở.
    [SerializeField] private bool changeSceneOnEnter = true;

    // Tên Scene chuyển tới khi Player đi vào cổng.
    [SerializeField] private string targetSceneName;

    // Reset trạng thái Scene đích trước khi nạp.
    [SerializeField] private bool resetSceneState = true;

    // Trạng thái hiện tại: true = cổng đang mở, false = đang đóng.
    [SerializeField] private bool isOpened = false;

    // ═══════════════════════════════════════════════════════════════
    //  AUDIO — Âm thanh cổng mở, ambient và player đi vào
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Audio ——————————————")]

    // AudioSource phát âm thanh (tự tìm nếu để trống).
    [SerializeField] private AudioSource audioSource;

    // Âm thanh khi cổng bắt đầu mở (one-shot).
    [SerializeField] private AudioClip openSound;

    // Âm thanh ambient loop chạy sau khi cổng mở xong.
    [SerializeField] private AudioClip ambientLoopSound;

    // Âm thanh khi player bước vào cổng (one-shot).
    [SerializeField] private AudioClip enterSound;

    // ═══════════════════════════════════════════════════════════════
    //  EVENTS — Sự kiện Unity cho designer hook logic bên ngoài
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Events ——————————————")]

    // Gọi khi cổng mở xong hoàn toàn (sau openDuration).
    [SerializeField] private UnityEvent onPortalOpened;

    // Gọi khi player đi vào cổng (chạm trigger).
    [SerializeField] private UnityEvent onPlayerEntered;

    // ═══════════════════════════════════════════════════════════════

    private Coroutine openCoroutine;
    private Coroutine shockwaveCoroutine;
    private Coroutine fallbackShakeCoroutine;
    private Sprite runtimeShockwaveSprite;
    private Texture2D runtimeShockwaveTexture;

    public bool IsOpened => isOpened;
    public string TargetSceneName
    {
        get => targetSceneName;
        set => targetSceneName = value;
    }
    public Vector3 OpenScale
    {
        get => openScale;
        set => openScale = value;
    }

    public bool TryGetEnergyPalette(out PortalEnergyPalette palette)
    {
        EnsureReferences();
        Material targetMat = null;

        if (portalMeshRenderer != null && portalMeshRenderer.sharedMaterial != null)
        {
            targetMat = portalMeshRenderer.sharedMaterial;
        }
        else if (portalSpriteRenderer != null && portalSpriteRenderer.sharedMaterial != null)
        {
            targetMat = portalSpriteRenderer.sharedMaterial;
        }
        else if (portalMaterialOverride != null)
        {
            targetMat = portalMaterialOverride;
        }

        if (targetMat == null)
        {
            palette = PortalEnergyPalette.Default;
            return false;
        }

        palette = new PortalEnergyPalette();

        if (targetMat.HasProperty(PropNeonColor))
        {
            palette.neonColor = targetMat.GetColor(PropNeonColor);
        }
        else if (targetMat.HasProperty(PropEmissionColor))
        {
            palette.neonColor = targetMat.GetColor(PropEmissionColor);
        }
        else if (targetMat.HasProperty(PropColor))
        {
            palette.neonColor = targetMat.GetColor(PropColor);
        }
        else
        {
            palette.neonColor = PortalEnergyPalette.Default.neonColor;
        }

        if (targetMat.HasProperty(PropCoreColor))
        {
            palette.coreColor = targetMat.GetColor(PropCoreColor);
        }
        else
        {
            palette.coreColor = Color.Lerp(palette.neonColor, Color.white, 0.4f);
        }

        if (targetMat.HasProperty(PropFlameColorA))
        {
            palette.flameColorA = targetMat.GetColor(PropFlameColorA);
        }
        else
        {
            palette.flameColorA = palette.neonColor * 0.7f;
            palette.flameColorA.a = 1f;
        }

        if (targetMat.HasProperty(PropFlameColorB))
        {
            palette.flameColorB = targetMat.GetColor(PropFlameColorB);
        }
        else
        {
            palette.flameColorB = palette.neonColor * 0.25f;
            palette.flameColorB.a = 1f;
        }

        return true;
    }

    private void Awake()
    {
        EnsureReferences();

        if (portalTrigger != null)
        {
            portalTrigger.isTrigger = true;
            portalTrigger.enabled = isOpened;
        }

        if (portalVisualRoot != null)
        {
            portalVisualRoot.localScale = openScale;
        }

        SetOpenProgress(isOpened ? 1f : 0f);
    }

    private void OnDestroy()
    {
        DestroyRuntimeAsset(runtimeShockwaveSprite);
        DestroyRuntimeAsset(runtimeShockwaveTexture);
    }

    private static void DestroyRuntimeAsset(Object runtimeAsset)
    {
        if (runtimeAsset == null) return;
        if (Application.isPlaying)
        {
            Destroy(runtimeAsset);
        }
        else
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(runtimeAsset)) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (runtimeAsset != null && !UnityEditor.EditorUtility.IsPersistent(runtimeAsset))
                {
                    DestroyImmediate(runtimeAsset, true);
                }
            };
#else
            DestroyImmediate(runtimeAsset, true);
#endif
        }
    }

    private void OnValidate()
    {
        EnsureReferences();
    }

    public void SetPortalMaterial(Material newMat)
    {
        portalMaterialOverride = newMat;
        EnsureReferences();
        if (portalMeshRenderer != null && newMat != null)
        {
            portalMeshRenderer.material = newMat;
        }
        else if (portalSpriteRenderer != null && newMat != null)
        {
            portalSpriteRenderer.material = newMat;
        }
    }

    private void EnsureReferences()
    {
        if (portalVisualRoot == null)
        {
            Transform found = transform.Find("PortalVisual");
            portalVisualRoot = found != null ? found : transform;
        }

        if (portalVisualRoot != null)
        {
            if (portalMeshRenderer == null) portalMeshRenderer = portalVisualRoot.GetComponent<MeshRenderer>();
            if (portalSpriteRenderer == null) portalSpriteRenderer = portalVisualRoot.GetComponent<SpriteRenderer>();

            if (portalMaterialOverride != null)
            {
                if (portalMeshRenderer != null && portalMeshRenderer.sharedMaterial != portalMaterialOverride)
                {
                    portalMeshRenderer.sharedMaterial = portalMaterialOverride;
                }
                else if (portalSpriteRenderer != null && portalSpriteRenderer.sharedMaterial != portalMaterialOverride)
                {
                    portalSpriteRenderer.sharedMaterial = portalMaterialOverride;
                }
            }
        }

        if (portalTrigger == null)
        {
            portalTrigger = GetComponent<Collider2D>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (portalParticles == null || portalParticles.Length == 0)
        {
            portalParticles = GetComponentsInChildren<ParticleSystem>(true);
        }

        EnsurePortalEmbersParticle();
        EnsurePortalImplosionParticle();
        EnsurePortalEruptionParticle();
        EnsureShockwaveRenderer();
    }

    // Lấy palette trực tiếp từ material mà KHÔNG gọi EnsureReferences() — tránh vòng đệ quy
    private PortalEnergyPalette GetPaletteDirect()
    {
        Material targetMat = null;

        if (portalMeshRenderer != null && portalMeshRenderer.sharedMaterial != null)
            targetMat = portalMeshRenderer.sharedMaterial;
        else if (portalSpriteRenderer != null && portalSpriteRenderer.sharedMaterial != null)
            targetMat = portalSpriteRenderer.sharedMaterial;
        else if (portalMaterialOverride != null)
            targetMat = portalMaterialOverride;

        if (targetMat == null)
            return PortalEnergyPalette.Default;

        PortalEnergyPalette palette = new PortalEnergyPalette();

        if (targetMat.HasProperty(PropNeonColor))
            palette.neonColor = targetMat.GetColor(PropNeonColor);
        else if (targetMat.HasProperty(PropEmissionColor))
            palette.neonColor = targetMat.GetColor(PropEmissionColor);
        else if (targetMat.HasProperty(PropColor))
            palette.neonColor = targetMat.GetColor(PropColor);
        else
            palette.neonColor = PortalEnergyPalette.Default.neonColor;

        if (targetMat.HasProperty(PropCoreColor))
            palette.coreColor = targetMat.GetColor(PropCoreColor);
        else
            palette.coreColor = Color.Lerp(palette.neonColor, Color.white, 0.4f);

        if (targetMat.HasProperty(PropFlameColorA))
            palette.flameColorA = targetMat.GetColor(PropFlameColorA);
        else
        {
            palette.flameColorA = palette.neonColor * 0.7f;
            palette.flameColorA.a = 1f;
        }

        if (targetMat.HasProperty(PropFlameColorB))
            palette.flameColorB = targetMat.GetColor(PropFlameColorB);
        else
        {
            palette.flameColorB = palette.neonColor * 0.25f;
            palette.flameColorB.a = 1f;
        }

        return palette;
    }

    private static Texture2D runtimeGlowParticleTexture;

    private static Material GetOrCreateParticleGlowMaterial()
    {
        Shader shader = Shader.Find("Bao/MiniGame/PortalEmberGlowParticle");
        if (shader == null || !shader.isSupported)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material mat = new Material(shader);

        if (runtimeGlowParticleTexture == null)
        {
            runtimeGlowParticleTexture = CreateSoftGlowParticleTexture(64);
        }

        mat.mainTexture = runtimeGlowParticleTexture;
        return mat;
    }

    private static Texture2D CreateSoftGlowParticleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - dist);
                // Tạo falloff mượt mà dạng Gaussian mềm
                alpha = Mathf.Pow(alpha, 2.2f);

                // Thêm lõi sáng chói ở trung tâm
                float core = Mathf.Clamp01(1f - dist * 2.5f);
                core = Mathf.Pow(core, 3.0f);

                float finalAlpha = Mathf.Clamp01(alpha + core * 0.8f);
                Color pixelColor = new Color(1f, 1f, 1f, finalAlpha);
                tex.SetPixel(x, y, pixelColor);
            }
        }

        tex.Apply();
        return tex;
    }

    private void EnsurePortalImplosionParticle()
    {
        if (portalImplosionParticle == null)
        {
            Transform found = transform.Find("PortalImplosionParticles");
            if (found != null)
            {
                portalImplosionParticle = found.GetComponent<ParticleSystem>();
            }
            else
            {
                GameObject psObj = new GameObject("PortalImplosionParticles");
                psObj.transform.SetParent(transform, false);
                portalImplosionParticle = psObj.AddComponent<ParticleSystem>();
                // Dừng hẳn trước khi cấu hình — Unity không cho gán duration khi đang play
                portalImplosionParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                PortalEnergyPalette palette = GetPaletteDirect();

                var main = portalImplosionParticle.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.25f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.25f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(-12f, -18f); // Hút cực nhanh vào tâm
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                main.startColor = new ParticleSystem.MinMaxGradient(Color.white, palette.coreColor);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 60;

                var emission = portalImplosionParticle.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });

                var shape = portalImplosionParticle.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 3.5f;
                shape.radiusThickness = 0.1f; // Sinh hạt ở vòng tròn ngoài cùng

                var renderer = portalImplosionParticle.GetComponent<ParticleSystemRenderer>();
                renderer.sortingOrder = 88;
                renderer.material = GetOrCreateParticleGlowMaterial();
            }
        }
    }

    private void EnsurePortalEruptionParticle()
    {
        if (portalEruptionParticle == null)
        {
            Transform found = transform.Find("PortalEruptionParticles");
            if (found != null)
            {
                portalEruptionParticle = found.GetComponent<ParticleSystem>();
            }
            else
            {
                GameObject psObj = new GameObject("PortalEruptionParticles");
                psObj.transform.SetParent(transform, false);
                portalEruptionParticle = psObj.AddComponent<ParticleSystem>();
                // Dừng hẳn trước khi cấu hình — Unity không cho gán duration khi đang play
                portalEruptionParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                PortalEnergyPalette palette = GetPaletteDirect();

                var main = portalEruptionParticle.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.8f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(4.0f, 9.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
                main.startColor = new ParticleSystem.MinMaxGradient(palette.neonColor, Color.white);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 0.4f;
                main.maxParticles = 80;

                var emission = portalEruptionParticle.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });

                var shape = portalEruptionParticle.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 65f;
                shape.radius = 0.4f;

                var colorOverLifetime = portalEruptionParticle.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(palette.neonColor, 0.4f), new GradientColorKey(palette.flameColorA, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) }
                );
                colorOverLifetime.color = grad;

                var renderer = portalEruptionParticle.GetComponent<ParticleSystemRenderer>();
                renderer.sortingOrder = 89;
                renderer.material = GetOrCreateParticleGlowMaterial();
            }
        }
    }

    private void EnsureShockwaveRenderer()
    {
        if (shockwaveRenderer == null)
        {
            Transform found = transform.Find("PortalShockwaveRing");
            if (found != null)
            {
                shockwaveRenderer = found.GetComponent<SpriteRenderer>();
            }
            else
            {
                GameObject swObj = new GameObject("PortalShockwaveRing");
                swObj.transform.SetParent(transform, false);
                shockwaveRenderer = swObj.AddComponent<SpriteRenderer>();
                shockwaveRenderer.sortingOrder = 90;

                Sprite loadedSw = Resources.Load<Sprite>("shockwave_1");
                if (loadedSw == null)
                {
                    if (runtimeShockwaveTexture == null)
                    {
                        runtimeShockwaveTexture = CreateShockwaveRingTexture(128);
                        runtimeShockwaveSprite = Sprite.Create(runtimeShockwaveTexture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 128f);
                    }
                    shockwaveRenderer.sprite = runtimeShockwaveSprite;
                }
                else
                {
                    shockwaveRenderer.sprite = loadedSw;
                }

                shockwaveRenderer.material = new Material(Shader.Find("Sprites/Default"));
                shockwaveRenderer.enabled = false;
            }
        }
    }

    private static Texture2D CreateShockwaveRingTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerR = size * 0.48f;
        float innerR = size * 0.38f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 0f;

                if (dist >= innerR && dist <= outerR)
                {
                    float mid = (innerR + outerR) * 0.5f;
                    float halfThick = (outerR - innerR) * 0.5f;
                    alpha = 1f - Mathf.Abs(dist - mid) / halfThick;
                    alpha = Mathf.Pow(alpha, 1.5f);
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    private void EnsurePortalEmbersParticle()
    {
        if (portalEmbersParticle == null)
        {
            Transform found = transform.Find("PortalFireEmbers");
            if (found != null)
            {
                portalEmbersParticle = found.GetComponent<ParticleSystem>();
            }
            else
            {
                GameObject psObj = new GameObject("PortalFireEmbers");
                psObj.transform.SetParent(transform, false);
                portalEmbersParticle = psObj.AddComponent<ParticleSystem>();
                // Dừng hẳn trước khi cấu hình — Unity không cho gán duration khi đang play
                portalEmbersParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                PortalEnergyPalette palette = GetPaletteDirect();

                var main = portalEmbersParticle.main;
                main.playOnAwake = false;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                main.startColor = new ParticleSystem.MinMaxGradient(palette.neonColor, Color.white);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 30;

                var emission = portalEmbersParticle.emission;
                emission.rateOverTime = 8f;

                var shape = portalEmbersParticle.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(openScale.x * 0.5f, openScale.y * 0.6f, 0.1f);

                var velOverLife = portalEmbersParticle.velocityOverLifetime;
                velOverLife.enabled = true;
                // Tất cả 3 trục phải cùng mode (TwoConstants) để Unity không lỗi
                velOverLife.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                velOverLife.y = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                velOverLife.z = new ParticleSystem.MinMaxCurve(0f, 0f);

                var colorOverLifetime = portalEmbersParticle.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(palette.neonColor, 0.6f), new GradientColorKey(palette.flameColorA, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) }
                );
                colorOverLifetime.color = grad;

                var renderer = portalEmbersParticle.GetComponent<ParticleSystemRenderer>();
                renderer.sortingOrder = 77;
                renderer.material = GetOrCreateParticleGlowMaterial();
            }
        }

        // Đảm bảo đồng bộ curve mode và gán material phát sáng mềm mại cả khi particle system được load từ Prefab có sẵn
        if (portalEmbersParticle != null)
        {
            var psRenderer = portalEmbersParticle.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null && (psRenderer.sharedMaterial == null || psRenderer.sharedMaterial.shader == null || psRenderer.sharedMaterial.shader.name == "Sprites/Default"))
            {
                psRenderer.material = GetOrCreateParticleGlowMaterial();
            }

            var velOverLife = portalEmbersParticle.velocityOverLifetime;
            velOverLife.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velOverLife.y = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            velOverLife.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velOverLife.enabled = true;
        }
    }

    public void AutoFitToCrystals(
        IList<LaserMirrorCrystal2D> crystalList,
        float horizontalMargin = 0.45f,
        float topMargin = 0.38f,
        float bottomMargin = 0.15f)
    {
        if (crystalList == null || crystalList.Count == 0)
        {
            return;
        }

        EnsureReferences();

        Transform parentTransform = transform.parent != null ? transform.parent : transform;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        int validCount = 0;

        for (int i = 0; i < crystalList.Count; i++)
        {
            LaserMirrorCrystal2D crystal = crystalList[i];
            if (crystal == null) continue;

            Vector3 localPos = parentTransform.InverseTransformPoint(crystal.transform.position);
            minX = Mathf.Min(minX, localPos.x);
            maxX = Mathf.Max(maxX, localPos.x);
            minY = Mathf.Min(minY, localPos.y);
            maxY = Mathf.Max(maxY, localPos.y);
            validCount++;
        }

        if (validCount < 2)
        {
            return;
        }

        float totalWidth = (maxX - minX) + horizontalMargin * 2f;
        float totalHeight = (maxY - minY) + topMargin + bottomMargin;

        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY - bottomMargin) + totalHeight * 0.5f;

        transform.localPosition = new Vector3(centerX, centerY, transform.localPosition.z);

        openScale = new Vector3(totalWidth, totalHeight, 1f);
        if (portalVisualRoot != null)
        {
            portalVisualRoot.localScale = openScale;
            portalVisualRoot.localPosition = Vector3.zero;
        }

        if (portalTrigger is BoxCollider2D boxCol)
        {
            boxCol.size = new Vector2(totalWidth * 0.65f, totalHeight * 0.7f);
            boxCol.offset = new Vector2(0f, -totalHeight * 0.1f);
        }

        if (portalEmbersParticle != null)
        {
            var shape = portalEmbersParticle.shape;
            shape.scale = new Vector3(totalWidth * 0.5f, totalHeight * 0.6f, 0.1f);
        }

        // Tính toán các điểm kiểm soát UV (_ArchP0, _ArchP1, _ArchP2, _ArchP3) trực tiếp từ vị trí thực tế của các crystal
        UpdateArchGeometryProperties(crystalList, totalWidth, totalHeight, centerX, minY, bottomMargin);
    }

    private void UpdateArchGeometryProperties(
        IList<LaserMirrorCrystal2D> crystalList,
        float totalWidth,
        float totalHeight,
        float centerX,
        float minY,
        float bottomMargin)
    {
        if (crystalList == null || crystalList.Count == 0 || totalWidth <= 0.001f || totalHeight <= 0.001f)
        {
            return;
        }

        Transform parentTransform = transform.parent != null ? transform.parent : transform;
        float halfWidth = totalWidth * 0.5f;
        float bottomY = minY - bottomMargin;

        // Mặc định fallback
        Vector4 p0 = new Vector4(0.000f, 0.923f, 0f, 0f);
        Vector4 p1 = new Vector4(0.430f, 0.761f, 0f, 0f);
        Vector4 p2 = new Vector4(0.760f, 0.436f, 0f, 0f);
        Vector4 p3 = new Vector4(0.886f, 0.030f, 0f, 0f);

        if (crystalList.Count >= 7)
        {
            // Tính UV vị trí cho Crystal 3 (Apex)
            if (crystalList[3] != null)
            {
                Vector3 lp = parentTransform.InverseTransformPoint(crystalList[3].transform.position);
                float normX = Mathf.Abs(lp.x - centerX) / halfWidth;
                float uvY = (lp.y - bottomY) / totalHeight;
                p0 = new Vector4(normX, uvY, 0f, 0f);
            }

            // Tính UV trung bình cho Crystal 2 & 4 (High)
            float highNormX = 0f, highUvY = 0f;
            int highCount = 0;
            if (crystalList[2] != null)
            {
                Vector3 lp = parentTransform.InverseTransformPoint(crystalList[2].transform.position);
                highNormX += Mathf.Abs(lp.x - centerX) / halfWidth;
                highUvY += (lp.y - bottomY) / totalHeight;
                highCount++;
            }
            if (crystalList[4] != null)
            {
                Vector3 lp = parentTransform.InverseTransformPoint(crystalList[4].transform.position);
                highNormX += Mathf.Abs(lp.x - centerX) / halfWidth;
                highUvY += (lp.y - bottomY) / totalHeight;
                highCount++;
            }
            if (highCount > 0)
            {
                p1 = new Vector4(highNormX / highCount, highUvY / highCount, 0f, 0f);
            }

            // Tính UV trung bình cho Crystal 1 & 5 (Low)
            float lowNormX = 0f, lowUvY = 0f;
            int lowCount = 0;
            if (crystalList[1] != null)
            {
                Vector3 lp = parentTransform.InverseTransformPoint(crystalList[1].transform.position);
                lowNormX += Mathf.Abs(lp.x - centerX) / halfWidth;
                lowUvY += (lp.y - bottomY) / totalHeight;
                lowCount++;
            }
            if (crystalList[5] != null)
            {
                Vector3 lp = parentTransform.InverseTransformPoint(crystalList[5].transform.position);
                lowNormX += Mathf.Abs(lp.x - centerX) / halfWidth;
                lowUvY += (lp.y - bottomY) / totalHeight;
                lowCount++;
            }
            if (lowCount > 0)
            {
                p2 = new Vector4(lowNormX / lowCount, lowUvY / lowCount, 0f, 0f);
            }

            // Tính UV trung bình cho Crystal 0 & 6 (Base)
            float baseNormX = 0f, baseUvY = 0f;
            int baseCount = 0;
            if (crystalList[0] != null)
            {
                Vector3 lp = parentTransform.InverseTransformPoint(crystalList[0].transform.position);
                baseNormX += Mathf.Abs(lp.x - centerX) / halfWidth;
                baseUvY += (lp.y - bottomY) / totalHeight;
                baseCount++;
            }
            if (crystalList[6] != null)
            {
                Vector3 lp = parentTransform.InverseTransformPoint(crystalList[6].transform.position);
                baseNormX += Mathf.Abs(lp.x - centerX) / halfWidth;
                baseUvY += (lp.y - bottomY) / totalHeight;
                baseCount++;
            }
            if (baseCount > 0)
            {
                p3 = new Vector4(baseNormX / baseCount, baseUvY / baseCount, 0f, 0f);
            }
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (portalMeshRenderer != null)
        {
            portalMeshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(PropArchP0, p0);
            propertyBlock.SetVector(PropArchP1, p1);
            propertyBlock.SetVector(PropArchP2, p2);
            propertyBlock.SetVector(PropArchP3, p3);
            portalMeshRenderer.SetPropertyBlock(propertyBlock);
        }
        else if (portalSpriteRenderer != null)
        {
            portalSpriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(PropArchP0, p0);
            propertyBlock.SetVector(PropArchP1, p1);
            propertyBlock.SetVector(PropArchP2, p2);
            propertyBlock.SetVector(PropArchP3, p3);
            portalSpriteRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    public void SetOpenProgress(float progress)
    {
        EnsureReferences();
        progress = Mathf.Clamp01(progress);

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (portalMeshRenderer != null)
        {
            portalMeshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(PropOpenProgress, progress);
            portalMeshRenderer.SetPropertyBlock(propertyBlock);
        }
        else if (portalSpriteRenderer != null)
        {
            portalSpriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(PropOpenProgress, progress);
            portalSpriteRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    public void OpenPortal()
    {
        if (isOpened)
        {
            return;
        }

        isOpened = true;
        EnsureReferences();

        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
        }

        if (gameObject.activeInHierarchy && Application.isPlaying)
        {
            openCoroutine = StartCoroutine(OpenPortalRoutine());
        }
        else
        {
            if (portalVisualRoot != null) portalVisualRoot.localScale = openScale;
            SetOpenProgress(1f);
            if (portalTrigger != null) portalTrigger.enabled = true;
            if (portalEmbersParticle != null) portalEmbersParticle.Play(true);
            onPortalOpened?.Invoke();
        }
    }

    public void ClosePortal()
    {
        isOpened = false;
        EnsureReferences();

        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
            openCoroutine = null;
        }

        if (portalTrigger != null)
        {
            portalTrigger.enabled = false;
        }

        SetOpenProgress(0f);

        for (int i = 0; i < portalParticles.Length; i++)
        {
            if (portalParticles[i] != null) portalParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (portalEmbersParticle != null)
        {
            portalEmbersParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (portalImplosionParticle != null)
        {
            portalImplosionParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (portalEruptionParticle != null)
        {
            portalEruptionParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (shockwaveRenderer != null)
        {
            shockwaveRenderer.enabled = false;
        }
    }

    private IEnumerator OpenPortalRoutine()
    {
        // ── GIAI ĐOẠN 1: TỤ LỰC NÉN NĂNG LƯỢNG (Energy Implosion - 0.22s) ──
        if (portalImplosionParticle != null)
        {
            portalImplosionParticle.gameObject.SetActive(true);
            portalImplosionParticle.Play(true);
        }

        yield return new WaitForSeconds(0.22f);

        // ── GIAI ĐOẠN 2: BÙNG NỔ KHAI MỞ (Blast Shockwave, Seismic Tremor & Eruption) ──
        if (audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }

        // Bắn tung tàn lửa phun trào (Ash & Embers Eruption)
        if (portalEruptionParticle != null)
        {
            portalEruptionParticle.gameObject.SetActive(true);
            portalEruptionParticle.Play(true);
        }

        // Rung chấn màn hình (Seismic Camera Tremor)
        TriggerSeismicTremor();

        // Kích hoạt sóng xung kích nở bung (Shockwave Ring)
        if (enableShockwave)
        {
            if (shockwaveCoroutine != null) StopCoroutine(shockwaveCoroutine);
            shockwaveCoroutine = StartCoroutine(ShockwaveRingRoutine());
        }

        if (portalVisualRoot != null)
        {
            portalVisualRoot.localScale = openScale;
        }
        SetOpenProgress(0f);

        for (int i = 0; i < portalParticles.Length; i++)
        {
            if (portalParticles[i] != null && portalParticles[i] != portalImplosionParticle && portalParticles[i] != portalEruptionParticle)
            {
                portalParticles[i].gameObject.SetActive(true);
                portalParticles[i].Play(true);
            }
        }

        if (portalEmbersParticle != null)
        {
            portalEmbersParticle.gameObject.SetActive(true);
            portalEmbersParticle.Play(true);
        }

        float elapsed = 0f;
        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float t = openCurve.Evaluate(Mathf.Clamp01(elapsed / openDuration));

            SetOpenProgress(t);

            yield return null;
        }

        SetOpenProgress(1f);

        if (portalTrigger != null)
        {
            portalTrigger.enabled = true;
        }

        if (audioSource != null && ambientLoopSound != null)
        {
            audioSource.clip = ambientLoopSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        onPortalOpened?.Invoke();
        openCoroutine = null;
    }

    private void TriggerSeismicTremor()
    {
        if (!enableCameraShake) return;

        bool hasCameraController = false;
        try
        {
            if (CameraController.Instance != null)
            {
                CameraController.Instance.ShakeCamera(cameraShakeDuration, cameraShakeMagnitude);
                hasCameraController = true;
            }
        }
        catch
        {
            hasCameraController = false;
        }

        if (!hasCameraController)
        {
            if (fallbackShakeCoroutine != null) StopCoroutine(fallbackShakeCoroutine);
            fallbackShakeCoroutine = StartCoroutine(FallbackShakeRoutine());
        }
    }

    private IEnumerator FallbackShakeRoutine()
    {
        Transform target = portalVisualRoot != null ? portalVisualRoot : transform;
        Vector3 basePos = target.localPosition;
        float elapsed = 0f;

        while (elapsed < cameraShakeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cameraShakeDuration);
            float damping = Mathf.Pow(1f - t, 2.2f);
            float offsetX = Random.Range(-1f, 1f) * cameraShakeMagnitude * 0.4f * damping;
            float offsetY = Random.Range(-1f, 1f) * cameraShakeMagnitude * 0.4f * damping;

            target.localPosition = basePos + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }

        target.localPosition = basePos;
        fallbackShakeCoroutine = null;
    }

    private IEnumerator ShockwaveRingRoutine()
    {
        EnsureShockwaveRenderer();
        if (shockwaveRenderer == null) yield break;

        TryGetEnergyPalette(out PortalEnergyPalette palette);
        Color startColor = Color.Lerp(Color.white, palette.neonColor, 0.4f);

        shockwaveRenderer.enabled = true;
        shockwaveRenderer.transform.localPosition = Vector3.zero;
        shockwaveRenderer.transform.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < shockwaveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shockwaveDuration);
            // EaseOutQuad nở nhanh đầu, chậm dần
            float ease = 1f - (1f - t) * (1f - t);

            float currentRadius = Mathf.Lerp(0.2f, shockwaveMaxRadius, ease);
            shockwaveRenderer.transform.localScale = new Vector3(currentRadius * 2f, currentRadius * 2f, 1f);

            Color c = startColor;
            c.a = (1f - t) * 0.85f;
            shockwaveRenderer.color = c;

            yield return null;
        }

        shockwaveRenderer.enabled = false;
        shockwaveCoroutine = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isOpened)
        {
            return;
        }

        if (IsPlayer(other))
        {
            if (audioSource != null && enterSound != null)
            {
                audioSource.PlayOneShot(enterSound);
            }

            onPlayerEntered?.Invoke();

            if (changeSceneOnEnter && !string.IsNullOrEmpty(targetSceneName))
            {
                if (resetSceneState)
                {
                    SceneRuntimeStateManager.ResetScene(targetSceneName);
                }
                Tien.AdvancedLoading.Core.SceneLoader.Load(targetSceneName);
            }
        }
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null) return false;
        return other.CompareTag(playerTag)
            || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag))
            || other.GetComponentInParent<Player>() != null;
    }
}

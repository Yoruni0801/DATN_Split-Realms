using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class LaserMirrorStonePedestal2D : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    //  INTERACTION & KEY BINDINGS — Cài đặt tương tác và phím bấm
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Interaction & Key Bindings ——————————————")]

    // Phím bấm để kích hoạt xoay trụ (Mặc định: E).
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    // Thời gian giãn cách tối thiểu giữa 2 lần bấm E (giây) để tránh spam.
    [SerializeField, Min(0.05f)] private float retriggerDelay = 0.28f;

    // Tag nhận diện Player khi bước vào vùng tương tác.
    [SerializeField] private string playerTag = "Player";

    // Tên Layer của Player để nhận diện va chạm.
    [SerializeField] private string playerLayerName = "Player";

    // ═══════════════════════════════════════════════════════════════
    //  TARGETS — Liên kết tới Pha Lê và Câu Đố
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Target Binding ——————————————")]

    // Viên Pha Lê mà Trụ này điều khiển góc quay.
    [SerializeField] private LaserMirrorCrystal2D controlledCrystal;

    // Quản lý câu đố LaserMirror cha.
    [SerializeField] private LaserMirrorPuzzle2D puzzle;

    // ═══════════════════════════════════════════════════════════════
    //  GÓI 1: MECHANICAL POWER — Hoạt ảnh xoay đầm, nảy lò xo & lực
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Gói 1: Mechanical Rotation & Spring Easing ——————————————")]

    // Transform của đĩa xoay / đỉnh trụ (phần quay theo góc).
    [SerializeField] private Transform dial;

    // Thời gian hoàn thành 1 nhịp xoay (giây). 0.22s = rất đầm và nhanh.
    [SerializeField, Min(0.05f)] private float dialTurnDuration = 0.24f;

    // Góc giật lùi lấy đà (độ) trước khi vút xoay.
    [SerializeField, Range(0f, 10f)] private float windupAngle = 3.5f;

    // Góc văng quá đà (Overshoot) khi chốt vào vị trí mới (độ).
    [SerializeField, Range(0f, 15f)] private float overshootAngle = 5.0f;

    // Hệ số nảy lò xo (Spring Bounce) khi chốt hãm va đập.
    [SerializeField, Range(1f, 15f)] private float springStiffness = 8.0f;

    // Độ rung giật co giãn (Scale Kick) của đĩa xoay khi bấm E.
    [SerializeField, Range(0f, 0.3f)] private float scaleKickAmount = 0.12f;

    // ═══════════════════════════════════════════════════════════════
    //  GÓI 1: DUST BURST & SHOCKWAVE — Bụi đá và sóng xung kích chân trụ
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Gói 1: Dust Burst & Flash VFX ——————————————")]

    // Bật/tắt hiệu ứng tung bụi đá chân trụ khi xoay.
    [SerializeField] private bool enableDustBurst = true;

    // ParticleSystem bụi đá chân trụ (tự tạo nếu để trống).
    [SerializeField] private ParticleSystem dustBurstParticle;

    // Số lượng hạt bụi đá văng ra mỗi lần xoay.
    [SerializeField, Range(4, 30)] private int dustParticleCount = 14;

    // Bật/tắt hiệu ứng chớp sáng đỉnh trụ khi chốt góc quay.
    [SerializeField] private bool enableTopFlash = true;

    // Kích thước chớp sáng trắng chói trên đỉnh trụ.
    [SerializeField, Min(0.1f)] private float flashSize = 0.6f;

    // ═══════════════════════════════════════════════════════════════
    //  VISUALS — Thân trụ & Đĩa xoay
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Visual Renderers ——————————————")]

    // SpriteRenderer của thân trụ đá.
    [SerializeField] private SpriteRenderer pedestalRenderer;

    // SpriteRenderer của đĩa xoay / đỉnh trụ.
    [SerializeField] private SpriteRenderer dialRenderer;

    // SpriteRenderer viền đầu tròn (VienDauTron) chứa Outline Shader
    [SerializeField] private SpriteRenderer ringOutlineRenderer;

    // Bảng màu cho từng nấc xoay của Pillar
    [SerializeField] private Color[] stepColors;

    // Chỉ số nấc đúng (ứng với góc giải đố)
    [SerializeField] private int correctStepIndex = 0;

    // Độ sáng phát sáng HDR khi ở nấc đúng
    [SerializeField, Range(1f, 4f)] private float correctGlowMultiplier = 1.6f;

    // ═══════════════════════════════════════════════════════════════
    //  GÓI 2: MAGMA EMBERS & VENTING — Khói tàn lửa bốc lên từ khe trụ
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Gói 2: Magma Embers & Venting ——————————————")]

    // Bật/tắt tàn lửa bốc lên từ khe nứt thân trụ.
    [SerializeField] private bool enablePillarEmbers = true;

    // ParticleSystem tàn lửa thân trụ (tự tạo nếu để trống).
    [SerializeField] private ParticleSystem pillarEmbersParticle;

    // Màu tàn lửa thân trụ (được cập nhật theo Palette cổng/tia).
    [SerializeField] private Color embersColor = new Color(1.05f, 0.2f, 0.62f, 1f);

    // ═══════════════════════════════════════════════════════════════
    //  PROMPT & UI — Gợi ý phím bấm [E]
    // ═══════════════════════════════════════════════════════════════

    [Header("█ UI Interaction Prompt ——————————————")]

    // GameObject hiển thị phím [E] tương tác.
    [SerializeField] private GameObject interactionPrompt;

    // Tốc độ nhấp nhô của phím [E] (Hz).
    [SerializeField, Min(0f)] private float promptBobSpeed = 4.5f;

    // Biên độ nhấp nhô của phím [E] (world unit).
    [SerializeField, Min(0f)] private float promptBobAmount = 0.04f;

    // ═══════════════════════════════════════════════════════════════
    //  AUDIO — Âm thanh cơ khí nghiến đá & chốt hãm
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Audio Design ——————————————")]

    // AudioSource phát âm thanh (tự tìm nếu để trống).
    [SerializeField] private AudioSource audioSource;

    // Âm thanh khi trụ bắt đầu xoay (tiếng nghiến đá / cơ khí).
    [SerializeField] private AudioClip rotateClip;

    // Âm thanh tiếng chốt hãm "CẠCH" khi trụ ăn khớp góc mới.
    [SerializeField] private AudioClip lockClickClip;

    // ═══════════════════════════════════════════════════════════════
    //  PRIVATE RUNTIME STATE
    // ═══════════════════════════════════════════════════════════════

    private int playerContacts;
    private bool mouseHovering;
    private bool animating;
    private float nextTriggerTime;
    private Vector3 dialBaseScale = Vector3.one;
    private Vector3 promptBasePosition;
    private Coroutine dialAnimation;
    private Coroutine ringFlashCoroutine;
    private SpriteRenderer flashRenderer;
    private MaterialPropertyBlock ringPropBlock;

    private static readonly int OutlineColorProp = Shader.PropertyToID("_OutlineColor");
    private static readonly int GlowIntensityProp = Shader.PropertyToID("_GlowIntensity");
    private static readonly int FlashBoostProp = Shader.PropertyToID("_FlashBoost");

    public void Configure(
        LaserMirrorPuzzle2D newPuzzle,
        LaserMirrorCrystal2D newControlledCrystal,
        Transform newDial,
        SpriteRenderer newPedestalRenderer,
        SpriteRenderer newDialRenderer,
        GameObject newProximityGlow,
        GameObject newInteractionPrompt)
    {
        puzzle = newPuzzle;
        controlledCrystal = newControlledCrystal;
        dial = newDial;
        pedestalRenderer = newPedestalRenderer;
        dialRenderer = newDialRenderer;
        interactionPrompt = newInteractionPrompt;

        if (newProximityGlow != null)
        {
            newProximityGlow.SetActive(false);
        }

        CleanupLegacyRuneRings();
        EnsureAllVisualElements();
        CacheVisualState();
        SyncDialImmediate();
        UpdateRingOutlineVisuals(false);
        RefreshFocusVisuals();
    }

    private void Awake()
    {
        Collider2D interactionZone = GetComponent<Collider2D>();
        if (interactionZone != null)
        {
            interactionZone.isTrigger = true;
        }

        CleanupLegacyRuneRings();
        EnsureAllVisualElements();
        CacheVisualState();
        SyncDialImmediate();
        UpdateRingOutlineVisuals(false);
        RefreshFocusVisuals();
    }

    private void Start()
    {
        CleanupLegacyRuneRings();
        SyncDialImmediate();
    }

    private void Update()
    {
        if (!animating)
        {
            KeepDialSynchronized();
        }

        bool canInteract = CanInteract();
        if (canInteract && (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E)))
        {
            Interact();
        }

        UpdateVisualsRealtime();
    }

    private void UpdateVisualsRealtime()
    {
        // Hiệu ứng nhấp nhô icon phím [E]
        if (interactionPrompt != null && interactionPrompt.activeSelf)
        {
            float bob = Mathf.Sin(Time.unscaledTime * promptBobSpeed) * promptBobAmount;
            interactionPrompt.transform.localPosition = promptBasePosition + Vector3.up * bob;
        }
    }

    public void ApplyEnergyPalette(LaserMirrorCavePortal2D.PortalEnergyPalette palette)
    {
        embersColor = palette.neonColor;

        if (pillarEmbersParticle != null)
        {
            var main = pillarEmbersParticle.main;
            main.startColor = new ParticleSystem.MinMaxGradient(palette.neonColor, palette.flameColorA);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        playerContacts++;
        RefreshFocusVisuals();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        playerContacts = Mathf.Max(0, playerContacts - 1);
        RefreshFocusVisuals();
    }

    private void OnMouseEnter()
    {
        mouseHovering = true;
        RefreshFocusVisuals();
    }

    private void OnMouseExit()
    {
        mouseHovering = false;
        RefreshFocusVisuals();
    }

    private void OnMouseDown()
    {
        if (CanInteract())
        {
            Interact();
        }
    }

    private void OnDisable()
    {
        playerContacts = 0;
        mouseHovering = false;
        animating = false;
        RefreshFocusVisuals();
    }

    private void OnValidate()
    {
        dialTurnDuration = Mathf.Max(0.05f, dialTurnDuration);
        if (!Application.isPlaying)
        {
            SyncDialImmediate();
        }
    }

    private bool CanInteract()
    {
        return Application.isPlaying
            && playerContacts > 0
            && Time.time >= nextTriggerTime
            && puzzle != null
            && !puzzle.IsSolved;
    }

    private void Interact()
    {
        if (!CanInteract()) return;

        nextTriggerTime = Time.time + retriggerDelay;

        if (!puzzle.IsBeamActive)
        {
            puzzle.SetBeamActive(true);
        }

        if (controlledCrystal != null)
        {
            controlledCrystal.RotateStep();
        }

        puzzle.RebuildBeam();

        // 💥 GÓI 1: Âm thanh bắt đầu xoay
        if (audioSource != null && rotateClip != null)
        {
            audioSource.PlayOneShot(rotateClip);
        }

        // 💥 GÓI 1: Bụi đá tung ra chân trụ
        EmitDustBurst();

        // 💥 GÓI 1: Hoạt ảnh xoay đầm nảy lò xo (Spring Easing)
        if (dialAnimation != null)
        {
            StopCoroutine(dialAnimation);
        }

        dialAnimation = StartCoroutine(AnimateDialSpring());
    }

    // ═══════════════════════════════════════════════════════════════
    //  GÓI 1: HOẠT ẢNH XOAY LÒ XO NẢY ĐẦM (SPRING EASING COROUTINE)
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator AnimateDialSpring()
    {
        animating = true;
        RefreshFocusVisuals();

        if (dial == null)
        {
            animating = false;
            RefreshFocusVisuals();
            yield break;
        }

        float startAngle = dial.localEulerAngles.z;
        float targetAngle = GetControlledCrystalAngle();
        float deltaAngle = Mathf.DeltaAngle(startAngle, targetAngle);

        float elapsed = 0f;
        float duration = dialTurnDuration;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Công thức Spring Overshoot Easing:
            float currentProgressAngle;

            if (t < 0.15f)
            {
                float windupT = t / 0.15f;
                float windupOffset = -Mathf.Sin(windupT * Mathf.PI) * windupAngle * Mathf.Sign(deltaAngle);
                currentProgressAngle = startAngle + windupOffset;
            }
            else
            {
                float normalizedT = (t - 0.15f) / 0.85f;
                float decay = Mathf.Exp(-normalizedT * springStiffness);
                float oscillation = Mathf.Cos(normalizedT * Mathf.PI * 2.5f);
                float springFactor = 1f - decay * oscillation;

                float overshootAdd = Mathf.Sin(normalizedT * Mathf.PI) * (overshootAngle * (1f - normalizedT)) * Mathf.Sign(deltaAngle);
                currentProgressAngle = startAngle + deltaAngle * springFactor + overshootAdd;
            }

            dial.localRotation = Quaternion.Euler(0f, 0f, currentProgressAngle);

            // Hiệu ứng Scale Kick khi xoay
            float kick = 1f + Mathf.Sin(t * Mathf.PI) * scaleKickAmount;
            dial.localScale = dialBaseScale * kick;

            yield return null;
        }

        dial.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
        dial.localScale = dialBaseScale;

        // 💥 GÓI 1: Chốt góc - Tiếng Click và Chớp sáng đỉnh trụ
        OnRotationLocked();

        animating = false;
        dialAnimation = null;
        RefreshFocusVisuals();
    }

    private void OnRotationLocked()
    {
        // Âm thanh chốt hãm
        if (audioSource != null && lockClickClip != null)
        {
            audioSource.PlayOneShot(lockClickClip);
        }

        // Chớp sáng đỉnh trụ
        if (enableTopFlash)
        {
            TriggerTopFlash();
        }

        // Cập nhật và kích hoạt chớp sáng màu viền đầu tròn
        UpdateRingOutlineVisuals(true);
    }

    private void TriggerTopFlash()
    {
        if (flashRenderer == null)
        {
            EnsureFlashVisual();
        }

        if (flashRenderer != null)
        {
            StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        flashRenderer.gameObject.SetActive(true);
        flashRenderer.transform.localScale = Vector3.one * (flashSize * 1.4f);
        flashRenderer.color = Color.white;

        float elapsed = 0f;
        float dur = 0.14f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            flashRenderer.transform.localScale = Vector3.Lerp(Vector3.one * (flashSize * 1.4f), Vector3.zero, t);
            Color c = Color.Lerp(Color.white, embersColor, t);
            c.a = 1f - t;
            flashRenderer.color = c;
            yield return null;
        }

        flashRenderer.gameObject.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════
    //  GÓI 1: BỤI ĐÁ CHÂN TRỤ (DUST BURST)
    // ═══════════════════════════════════════════════════════════════

    private void EmitDustBurst()
    {
        if (!enableDustBurst || dustBurstParticle == null) return;
        dustBurstParticle.Emit(dustParticleCount);
    }

    // ═══════════════════════════════════════════════════════════════
    //  PROCEDURAL VISUAL BUILDERS & CLEANUP
    // ═══════════════════════════════════════════════════════════════

    private void CleanupLegacyRuneRings()
    {
        // Xoá bỏ triệt để các GameObject vòng sáng cũ nếu còn tồn tại
        Transform ringChild = transform.Find("MagicRuneRing");
        if (ringChild != null)
        {
            if (Application.isPlaying) Destroy(ringChild.gameObject);
            else
            {
#if UNITY_EDITOR
                if (!UnityEditor.EditorUtility.IsPersistent(ringChild.gameObject))
                {
                    GameObject go = ringChild.gameObject;
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (go != null && !UnityEditor.EditorUtility.IsPersistent(go))
                        {
                            DestroyImmediate(go, true);
                        }
                    };
                }
#else
                DestroyImmediate(ringChild.gameObject, true);
#endif
            }
        }

        Transform glowChild = transform.Find("DialGlow");
        if (glowChild != null)
        {
            glowChild.gameObject.SetActive(false);
        }

        Transform proxGlowChild = transform.Find("ProximityGlow");
        if (proxGlowChild != null)
        {
            proxGlowChild.gameObject.SetActive(false);
        }
    }

    private void EnsureAllVisualElements()
    {
        EnsureDustBurstParticle();
        EnsurePillarEmbersParticle();
        EnsureFlashVisual();
        EnsureRingOutlineRenderer();
    }

    private void EnsureRingOutlineRenderer()
    {
        if (ringOutlineRenderer == null)
        {
            Transform vienObj = transform.Find("VienDauTron");
            if (vienObj != null)
            {
                ringOutlineRenderer = vienObj.GetComponent<SpriteRenderer>();
            }
        }

        if (ringOutlineRenderer != null)
        {
            if (ringOutlineRenderer.sharedMaterial == null || ringOutlineRenderer.sharedMaterial.shader.name != "Custom/2D/PillarRingOutline2D")
            {
                Shader outlineShader = Shader.Find("Custom/2D/PillarRingOutline2D");
                if (outlineShader != null)
                {
                    ringOutlineRenderer.material = new Material(outlineShader);
                }
            }
        }
    }

    private void EnsureDustBurstParticle()
    {
        if (dustBurstParticle == null)
        {
            Transform found = transform.Find("PillarDustBurst");
            if (found != null)
            {
                dustBurstParticle = found.GetComponent<ParticleSystem>();
            }
            else
            {
                GameObject psObj = new GameObject("PillarDustBurst");
                psObj.transform.SetParent(transform, false);
                psObj.transform.localPosition = new Vector3(0f, -0.4f, 0f);
                dustBurstParticle = psObj.AddComponent<ParticleSystem>();
                // Dừng hẳn trước khi cấu hình — Unity không cho gán duration khi đang play
                dustBurstParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = dustBurstParticle.main;
                main.playOnAwake = false;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.8f, 0.75f, 0.8f), new Color(0.6f, 0.55f, 0.5f, 0.5f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 40;

                var emission = dustBurstParticle.emission;
                emission.enabled = false;

                var shape = dustBurstParticle.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.35f;

                var sizeOverLife = dustBurstParticle.sizeOverLifetime;
                sizeOverLife.enabled = true;
                AnimationCurve curve = new AnimationCurve();
                curve.AddKey(0f, 0.6f);
                curve.AddKey(0.3f, 1.2f);
                curve.AddKey(1f, 0.1f);
                sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

                var colorOverLife = dustBurstParticle.colorOverLifetime;
                colorOverLife.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.65f, 0.6f), 1f) },
                    new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
                );
                colorOverLife.color = grad;

                var renderer = dustBurstParticle.GetComponent<ParticleSystemRenderer>();
                renderer.sortingOrder = 78;
                renderer.material = new Material(Shader.Find("Sprites/Default"));
            }
        }
    }

    private void EnsurePillarEmbersParticle()
    {
        if (pillarEmbersParticle == null && enablePillarEmbers)
        {
            Transform found = transform.Find("PillarEmbers");
            if (found != null)
            {
                pillarEmbersParticle = found.GetComponent<ParticleSystem>();
            }
            else
            {
                GameObject psObj = new GameObject("PillarEmbers");
                psObj.transform.SetParent(transform, false);
                psObj.transform.localPosition = new Vector3(0f, 0f, 0f);
                pillarEmbersParticle = psObj.AddComponent<ParticleSystem>();
                // Dừng hẳn trước khi cấu hình — Unity không cho gán duration khi đang play
                pillarEmbersParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = pillarEmbersParticle.main;
                main.playOnAwake = true;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                main.startColor = new ParticleSystem.MinMaxGradient(embersColor, Color.white);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 18;

                var emission = pillarEmbersParticle.emission;
                emission.rateOverTime = 4.5f;

                var shape = pillarEmbersParticle.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(0.5f, 0.8f, 0.1f);

                var velOverLife = pillarEmbersParticle.velocityOverLifetime;
                velOverLife.enabled = true;
                // Tất cả 3 trục phải cùng mode (TwoConstants) để Unity không lỗi
                velOverLife.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                velOverLife.y = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
                velOverLife.z = new ParticleSystem.MinMaxCurve(0f, 0f);

                var colorOverLife = pillarEmbersParticle.colorOverLifetime;
                colorOverLife.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(embersColor, 0.6f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) }
                );
                colorOverLife.color = grad;

                var renderer = pillarEmbersParticle.GetComponent<ParticleSystemRenderer>();
                renderer.sortingOrder = 76;
                renderer.material = new Material(Shader.Find("Sprites/Default"));

                // Sau khi cấu hình xong, khởi động lại particle (đã bị Stop ở trên)
                pillarEmbersParticle.Play(true);
            }
        }

        // Đảm bảo đồng bộ curve mode cả khi particle system được load từ Prefab có sẵn
        if (pillarEmbersParticle != null)
        {
            var velOverLife = pillarEmbersParticle.velocityOverLifetime;
            velOverLife.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velOverLife.y = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            velOverLife.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velOverLife.enabled = true;
        }
    }

    private void EnsureFlashVisual()
    {
        if (flashRenderer == null)
        {
            Transform found = transform.Find("TopFlash");
            if (found != null)
            {
                flashRenderer = found.GetComponent<SpriteRenderer>();
            }
            else
            {
                GameObject flashObj = new GameObject("TopFlash");
                flashObj.transform.SetParent(dial != null ? dial : transform, false);
                flashObj.transform.localPosition = Vector3.zero;
                flashObj.transform.localScale = Vector3.zero;
                flashObj.SetActive(false);

                flashRenderer = flashObj.AddComponent<SpriteRenderer>();
                flashRenderer.sprite = CreateSoftGlowSprite();
                flashRenderer.sortingOrder = 85;

                Shader addShader = Shader.Find("Custom/LaserPlasmaStream");
                if (addShader == null || !addShader.isSupported) addShader = Shader.Find("Sprites/Default");
                flashRenderer.material = new Material(addShader);
            }
        }
    }

    private static Sprite CreateSoftGlowSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "Generated_SoftPillarGlow";
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float norm = Mathf.Clamp01(dist / radius);
                float alpha = Mathf.Pow(1f - norm, 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
    }

    private void SyncDialImmediate()
    {
        if (dial == null || controlledCrystal == null) return;
        dial.localRotation = Quaternion.Euler(0f, 0f, GetControlledCrystalAngle());

        if (dialRenderer != null && controlledCrystal.VisualSprite != null)
        {
            dialRenderer.sprite = controlledCrystal.VisualSprite;
        }
    }

    private float GetControlledCrystalAngle()
    {
        return controlledCrystal != null ? controlledCrystal.TargetVisualAngle : 0f;
    }

    private void KeepDialSynchronized()
    {
        if (dial == null || controlledCrystal == null) return;

        float targetAngle = GetControlledCrystalAngle();
        if (Mathf.Abs(Mathf.DeltaAngle(dial.localEulerAngles.z, targetAngle)) > 0.05f)
        {
            dial.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
        }

        if (dialRenderer != null
            && controlledCrystal.VisualSprite != null
            && dialRenderer.sprite != controlledCrystal.VisualSprite)
        {
            dialRenderer.sprite = controlledCrystal.VisualSprite;
        }
    }

    private void CacheVisualState()
    {
        if (dial != null)
        {
            dialBaseScale = dial.localScale;
        }

        if (interactionPrompt != null)
        {
            promptBasePosition = interactionPrompt.transform.localPosition;
        }
    }

    private void RefreshFocusVisuals()
    {
        bool playerIsNear = playerContacts > 0;

        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(playerIsNear && !animating && (puzzle == null || !puzzle.IsSolved));
        }

        if (pedestalRenderer != null)
        {
            pedestalRenderer.color = Color.white;
        }

        if (dialRenderer != null)
        {
            dialRenderer.color = Color.white;
        }
    }

    public void SetStepColors(Color[] colors, int correctIndex)
    {
        stepColors = colors;
        correctStepIndex = correctIndex;
        UpdateRingOutlineVisuals(false);
    }

    public void UpdateRingOutlineVisuals(bool triggerFlash)
    {
        if (ringOutlineRenderer == null)
        {
            EnsureRingOutlineRenderer();
        }

        if (ringOutlineRenderer == null) return;

        int currentIndex = controlledCrystal != null ? controlledCrystal.CurrentAngleIndex : 0;
        bool isCorrect = (controlledCrystal != null && controlledCrystal.CustomRotationAngles != null && controlledCrystal.CustomRotationAngles.Length > 0)
            ? (currentIndex == correctStepIndex)
            : false;

        Color targetColor = Color.cyan;
        if (stepColors != null && stepColors.Length > 0)
        {
            int colIdx = Mathf.Clamp(currentIndex, 0, stepColors.Length - 1);
            targetColor = stepColors[colIdx];
        }

        if (ringPropBlock == null)
        {
            ringPropBlock = new MaterialPropertyBlock();
        }

        ringOutlineRenderer.GetPropertyBlock(ringPropBlock);
        ringPropBlock.SetColor(OutlineColorProp, targetColor);
        float glow = isCorrect ? (2.2f * correctGlowMultiplier) : 2.2f;
        ringPropBlock.SetFloat(GlowIntensityProp, glow);
        ringOutlineRenderer.SetPropertyBlock(ringPropBlock);

        if (triggerFlash && Application.isPlaying)
        {
            if (ringFlashCoroutine != null)
            {
                StopCoroutine(ringFlashCoroutine);
            }
            ringFlashCoroutine = StartCoroutine(RingFlashRoutine(targetColor, isCorrect));
        }
    }

    private IEnumerator RingFlashRoutine(Color baseColor, bool isCorrect)
    {
        if (ringOutlineRenderer == null) yield break;

        float duration = isCorrect ? 0.35f : 0.2f;
        float elapsed = 0f;
        float maxBoost = isCorrect ? 2.5f : 1.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float boost = Mathf.Sin((1f - t) * Mathf.PI) * maxBoost;

            if (ringPropBlock == null) ringPropBlock = new MaterialPropertyBlock();
            ringOutlineRenderer.GetPropertyBlock(ringPropBlock);
            ringPropBlock.SetFloat(FlashBoostProp, boost);
            ringOutlineRenderer.SetPropertyBlock(ringPropBlock);
            yield return null;
        }

        if (ringPropBlock == null) ringPropBlock = new MaterialPropertyBlock();
        ringOutlineRenderer.GetPropertyBlock(ringPropBlock);
        ringPropBlock.SetFloat(FlashBoostProp, 0f);
        ringOutlineRenderer.SetPropertyBlock(ringPropBlock);
        ringFlashCoroutine = null;
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null) return false;

        Transform root = other.transform.root;
        int playerLayer = LayerMask.NameToLayer(playerLayerName);

        return SafeCompareTag(other.gameObject, playerTag)
            || (other.attachedRigidbody != null && SafeCompareTag(other.attachedRigidbody.gameObject, playerTag))
            || (root != null && SafeCompareTag(root.gameObject, playerTag))
            || (playerLayer >= 0 && (other.gameObject.layer == playerLayer || (root != null && root.gameObject.layer == playerLayer)))
            || other.GetComponentInParent<Player>() != null;
    }

    private static bool SafeCompareTag(GameObject target, string tagName)
    {
        if (target == null || string.IsNullOrEmpty(tagName)) return false;

        try
        {
            return target.CompareTag(tagName);
        }
        catch (UnityException)
        {
            return false;
        }
    }
}

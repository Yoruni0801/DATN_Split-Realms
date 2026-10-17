using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LaserMirrorCrystal2D : MonoBehaviour
{
    public enum FaceDirection
    {
        Right,
        Up,
        Left,
        Down,
        CustomAngle
    }

    [System.Serializable]
    public struct DirectionRoute
    {
        public FaceDirection facing;
        public FaceDirection enterFace;
        public FaceDirection exitFace;
    }

    // ═══════════════════════════════════════════════════════════════
    //  ROLE — Vai trò của crystal trong puzzle (nguồn / đích / gương phản xạ)
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Role ——————————————")]

    // true = crystal nguồn phát tia laser. Chỉ nên có 1 crystal source trong puzzle.
    [SerializeField] private bool source;

    // true = crystal đích cuối cùng tia cần chạm tới để giải puzzle.
    [SerializeField] private bool goal;

    // true = crystal phản xạ tia laser (gương). false = chỉ hấp thụ, không phản xạ.
    [SerializeField] private bool reflector = true;

    // ═══════════════════════════════════════════════════════════════
    //  BEAM & ALIGNMENT — Vùng chạm tia và hướng xoay crystal
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Beam & Alignment ——————————————")]

    // Bán kính vùng phát hiện tia laser chạm crystal (world unit).
    // Tăng = dễ bắn trúng hơn, giảm = khó hơn.
    [SerializeField, Min(0.05f)] private float hitRadius = 0.55f;

    // Hướng phát tia của crystal nguồn (chỉ áp dụng cho crystal có source = true).
    [SerializeField] private FaceDirection sourceDirection = FaceDirection.Right;

    // Hướng mặt hiện tại của crystal (ảnh hưởng bảng route phản xạ).
    [SerializeField] private FaceDirection facing = FaceDirection.Right;

    // Danh sách các góc xoay tự do (tính bằng độ: 0=phải, 90=lên, 180=trái, 270=xuống).
    // Khi người chơi bấm switch xoay, crystal sẽ xoay lần lượt qua các góc này.
    [SerializeField] private float[] customRotationAngles;

    // Danh sách các Transform mục tiêu có thể ngắm đến khi bấm switch xoay.
    // Ưu tiên cao hơn customRotationAngles nếu cả hai đều có.
    [SerializeField] private Transform[] aimTargets;

    // Index hiện tại trong danh sách aimTargets (0-based).
    [SerializeField] private int currentAimIndex = 0;

    // Góc nón phản xạ / nhận tia (độ). Tia đến trong góc nón này mới được nhận.
    // 35° = dung sai vừa phải, 90° = rất dễ nhận tia.
    [SerializeField, Range(5f, 90f)] private float angleTolerance = 35f;

    // Bảng route phản xạ: mỗi hướng facing có cặp enterFace→exitFace riêng.
    // Dùng cho crystal gương loại cố định hướng (không dùng customRotationAngles).
    [SerializeField] private DirectionRoute[] routeStates =
    {
        new DirectionRoute { facing = FaceDirection.Right, enterFace = FaceDirection.Down, exitFace = FaceDirection.Right },
        new DirectionRoute { facing = FaceDirection.Up, enterFace = FaceDirection.Left, exitFace = FaceDirection.Up },
        new DirectionRoute { facing = FaceDirection.Left, enterFace = FaceDirection.Right, exitFace = FaceDirection.Left },
        new DirectionRoute { facing = FaceDirection.Down, enterFace = FaceDirection.Left, exitFace = FaceDirection.Down },
    };

    // Tốc độ xoay mượt về góc mục tiêu (cao = xoay nhanh, thấp = xoay chậm mượt).
    [SerializeField, Min(0.01f)] private float turnSpeed = 14f;

    // ═══════════════════════════════════════════════════════════════
    //  VISUAL & MATERIALS — Ngoại hình crystal và hiệu ứng phát sáng
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Visual & Materials ——————————————")]

    // Transform gốc cho phần visual crystal (xoay, scale đều áp dụng lên đây).
    // Nếu để trống = dùng chính transform của GameObject này.
    [SerializeField] private Transform visualRoot;

    // SpriteRenderer chính của crystal (dùng để đổi màu idle/lit/flash).
    [SerializeField] private SpriteRenderer crystalRenderer;

    // GameObject chứa sprite glow phát sáng xung quanh crystal.
    // Tự ẩn/hiện theo trạng thái charge.
    [SerializeField] private GameObject glowRoot;

    // Màu crystal khi chưa nhận tia (idle state).
    [SerializeField] private Color idleColor = new Color(0.72f, 0.86f, 0.95f, 0.95f);

    // Màu crystal khi đang nhận tia (lit state).
    [SerializeField] private Color litColor = new Color(1f, 0.96f, 0.82f, 1f);

    // Scale nhân lên khi crystal ở trạng thái lit (tạo cảm giác nở ra khi kích hoạt).
    // (1.4, 1.4, 1) = nở 40% theo XY — rõ ràng, bắt mắt.
    [SerializeField] private Vector3 litScale = new Vector3(1.4f, 1.4f, 1f);

    // Tốc độ chuyển đổi giữa idle↔lit (charge animation).
    // 6 = nhanh gọn, 2 = chuyển chậm mượt hơn.
    [SerializeField, Min(0.01f)] private float crystalChargeSpeed = 6f;

    // Biên độ nhịp đập (crystal "thở" khi đang lit). 0.045 = ±4.5%.
    [SerializeField, Range(0f, 0.15f)] private float crystalPulseAmount = 0.045f;

    // ═══════════════════════════════════════════════════════════════
    //  FLOATING & MAGIC VFX — Hiệu ứng lơ lửng và tàn lửa ma thuật
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Floating & Magic VFX ——————————————")]

    // Bật/tắt hiệu ứng crystal lơ lửng trên trục Y (floating hover idle).
    [SerializeField] private bool enableFloating = false;

    // Biên độ dao động lơ lửng (world unit). 0.045 = nhẹ nhàng, 0.08 = rõ rệt.
    [SerializeField, Range(0.01f, 0.1f)] private float floatAmplitude = 0.045f;

    // Tần số dao động lơ lửng (Hz). 2.0 = tốc độ vừa phải.
    [SerializeField, Range(0.5f, 5.0f)] private float floatFrequency = 2.0f;

    // ParticleSystem tàn lửa/embers bay lên từ crystal khi charge > 50%.
    // Tự tạo nếu để trống.
    [SerializeField] private ParticleSystem crystalEmbersParticle;

    // ═══════════════════════════════════════════════════════════════
    //  AIMING GUIDE & RETICLE VFX — Tia ngắm và vòng tâm khóa mục tiêu
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Aiming Guide & Reticle VFX ——————————————")]

    // Bật/tắt hiệu ứng tia ngắm và tâm ngắm khi xoay crystal.
    [SerializeField] private bool enableAimGuide = true;

    // LineRenderer hiển thị tia ngắm ma thuật mờ (tự tạo nếu để trống).
    [SerializeField] private LineRenderer aimGuideLine;

    // SpriteRenderer hiển thị vòng tâm ngắm tại mục tiêu (tự tạo nếu để trống).
    [SerializeField] private SpriteRenderer aimReticle;

    // Thời gian hiển thị tia ngắm khi xoay (giây).
    [SerializeField, Range(0.1f, 1.0f)] private float aimGuideDuration = 0.4f;

    // Độ dày tia ngắm.
    [SerializeField, Range(0.01f, 0.08f)] private float aimGuideWidth = 0.035f;

    // Độ trong suốt mờ mặc định của tia ngắm (0.25 = mờ nhẹ tinh tế).
    [SerializeField, Range(0.1f, 1.0f)] private float aimGuideAlpha = 0.35f;

    // Khoảng cách tối đa tia ngắm chiếu ra (nếu không có mục tiêu cụ thể).
    [SerializeField, Min(1f)] private float aimGuideMaxDistance = 8.0f;

    // ═══════════════════════════════════════════════════════════════
    //  CRYSTAL STAR GLINT & TWINKLE — Ngôi sao 4 cánh lấp lánh ngẫu nhiên
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Star Glint & Sparkles ——————————————")]

    // Bật/tắt hiệu ứng lóe sáng ngôi sao 4 cánh khi crystal ở trạng thái idle/lit.
    [SerializeField] private bool enableStarGlint = true;

    // SpriteRenderer của ngôi sao 4 cánh lấp lánh (tự tạo nếu để trống).
    [SerializeField] private SpriteRenderer starGlintRenderer;

    // Khoảng thời gian ngẫu nhiên tối thiểu giữa các lần lóe sáng (giây).
    [SerializeField, Range(0.5f, 6.0f)] private float glintMinInterval = 2.0f;

    // Khoảng thời gian ngẫu nhiên tối đa giữa các lần lóe sáng (giây).
    [SerializeField, Range(1.0f, 10.0f)] private float glintMaxInterval = 4.5f;

    // Thời gian diễn ra 1 lần chớp sao (scale 0 -> 1 -> 0).
    [SerializeField, Range(0.15f, 0.8f)] private float glintDuration = 0.32f;

    // Kích thước tối đa của ngôi sao lấp lánh.
    [SerializeField, Range(0.2f, 1.5f)] private float glintScale = 0.65f;

    // ═══════════════════════════════════════════════════════════════
    //  OVERCHARGE & ACTIVATION FLASH — Trạng thái siêu nạp và chớp sáng
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Overcharge & Activation Flash ——————————————")]

    // true = crystal đang ở trạng thái siêu nạp (puzzle solved).
    // Tự động bật khi puzzle hoàn thành, tắt khi puzzle reset.
    [SerializeField] private bool isOvercharged = false;

    // Thời gian chớp trắng chói khi crystal lần đầu nhận tia (giây).
    // 0.18 = chớp nhanh. Tăng lên 0.3 để chớp lâu hơn.
    [SerializeField, Range(0.05f, 0.5f)] private float flashDuration = 0.18f;

    // ═══════════════════════════════════════════════════════════════

    private float flashTimer = 0f;

    private Quaternion targetRotation;
    private Vector3 startScale;
    private Vector3 glowStartScale;
    private Vector3 initialLocalPos;
    private float floatPhaseOffset;
    private int currentAngleIndex = 0;
    private bool lit;
    private float charge;

    // Trạng thái tia ngắm & tâm khóa mục tiêu (Aim Guide runtime state)
    private float aimGuideTimer = 0f;      // Đếm ngược thời gian hiển thị tia ngắm
    private float lockOnFlashTimer = 0f;   // Đếm ngược hiệu ứng Lock-On Flash chói sáng
    private Transform lockOnTargetCache;   // Mục tiêu hợp lệ được khóa (nếu có)
    private Vector3 aimReticleBaseScale = Vector3.one * 0.5f;
    private Color aimGuideColor = new Color(1.05f, 0.2f, 0.62f, 1f);
    private Sprite runtimeReticleSprite;
    private Texture2D runtimeReticleTexture;

    // Trạng thái ngôi sao lấp lánh (Star Glint runtime state)
    private float nextGlintTime = 0f;      // Thời điểm kế tiếp phát chớp sao
    private float glintTimer = 0f;         // Đếm ngược thời gian chớp sao đang diễn ra
    private float glintBaseAngle = 0f;     // Góc xoay ngẫu nhiên của lần chớp hiện tại
    private Vector3 glintLocalOffset = Vector3.zero;
    private Sprite runtimeStarSprite;
    private Texture2D runtimeStarTexture;

    public bool IsSource => source;
    public bool IsGoal => goal;
    public bool CanReflect => reflector;
    public float HitRadius => hitRadius;
    public Vector2 Position => transform.position;
    public FaceDirection Facing => facing;
    public bool IsLit => lit;
    public float Charge => charge;
    public Sprite VisualSprite => crystalRenderer != null ? crystalRenderer.sprite : null;
    public float TargetVisualAngle => targetRotation.eulerAngles.z;
    public float[] CustomRotationAngles => customRotationAngles;
    public int CurrentAngleIndex => currentAngleIndex;
    public Transform[] AimTargets => aimTargets;
    public int CurrentAimIndex => currentAimIndex;
    public bool IsOvercharged => isOvercharged;

    public void ApplyEnergyPalette(LaserMirrorCavePortal2D.PortalEnergyPalette palette)
    {
        Color neon = palette.neonColor;
        Color core = palette.coreColor;
        Color flameB = palette.flameColorB;

        litColor = Color.Lerp(core, Color.white, 0.35f);
        idleColor = Color.Lerp(new Color(0.6f, 0.7f, 0.8f, 0.85f), flameB, 0.35f);

        if (glowRoot != null)
        {
            SpriteRenderer glowSr = glowRoot.GetComponent<SpriteRenderer>();
            if (glowSr != null)
            {
                glowSr.color = new Color(neon.r, neon.g, neon.b, 0.45f);
            }
            // Kích thước glow hội tụ mềm mại vừa vặn quanh tâm tinh thể
            glowRoot.transform.localScale = Vector3.one * 1.6f;
        }

        if (crystalEmbersParticle != null)
        {
            var main = crystalEmbersParticle.main;
            main.startColor = new ParticleSystem.MinMaxGradient(neon, Color.white);
        }

        // Đồng bộ màu tia ngắm & tâm khóa mục tiêu theo bảng màu năng lượng cổng
        aimGuideColor = neon;
        ApplyAimGuideColors();
    }

    // Áp màu hiện tại lên tia ngắm, reticle và ngôi sao lấp lánh
    private void ApplyAimGuideColors()
    {
        if (aimGuideLine != null)
        {
            Color start = Color.Lerp(aimGuideColor, Color.white, 0.35f);
            start.a = aimGuideAlpha;
            Color end = aimGuideColor;
            end.a = aimGuideAlpha * 0.25f;
            aimGuideLine.startColor = start;
            aimGuideLine.endColor = end;
        }

        if (aimReticle != null)
        {
            Color reticleColor = Color.Lerp(aimGuideColor, Color.white, 0.25f);
            reticleColor.a = aimGuideAlpha;
            aimReticle.color = reticleColor;
        }
    }

    public void SetOvercharged(bool overcharged)
    {
        isOvercharged = overcharged;
        if (overcharged)
        {
            flashTimer = flashDuration * 1.5f;
            if (crystalEmbersParticle != null)
            {
                crystalEmbersParticle.Play(true);
            }
        }
    }

    public void Configure(
        bool isSource,
        bool isGoal,
        bool canReflect,
        FaceDirection initialSourceDirection,
        FaceDirection initialFacing,
        float[] newCustomAngles,
        Transform[] newAimTargets,
        Transform newVisualRoot,
        SpriteRenderer newCrystalRenderer,
        GameObject newGlowRoot)
    {
        source = isSource;
        goal = isGoal;
        reflector = canReflect;
        sourceDirection = initialSourceDirection;
        facing = initialFacing;
        customRotationAngles = newCustomAngles;
        aimTargets = newAimTargets;
        visualRoot = newVisualRoot != null ? newVisualRoot : transform;
        crystalRenderer = newCrystalRenderer;
        glowRoot = newGlowRoot;
        startScale = visualRoot.localScale;
        glowStartScale = glowRoot != null ? glowRoot.transform.localScale : Vector3.one;

        UpdateTargetRotationImmediate();
        ApplyLit(false);
    }

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (crystalRenderer == null)
        {
            crystalRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        startScale = visualRoot.localScale;
        glowStartScale = glowRoot != null ? glowRoot.transform.localScale : Vector3.one;
        initialLocalPos = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        floatPhaseOffset = (transform.position.x * 1.7f + transform.position.y * 2.3f) % (Mathf.PI * 2f);

        EnsureEmbersParticle();
        EnsureAimGuideVisuals();
        EnsureStarGlintVisual();
        ScheduleNextGlint();
        UpdateTargetRotationImmediate();
        ApplyLit(false);
    }

    private void OnDestroy()
    {
        DestroyRuntimeAsset(runtimeReticleSprite);
        DestroyRuntimeAsset(runtimeReticleTexture);
        DestroyRuntimeAsset(runtimeStarSprite);
        DestroyRuntimeAsset(runtimeStarTexture);
    }

    private static void DestroyRuntimeAsset(Object runtimeAsset)
    {
        if (runtimeAsset == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(runtimeAsset);
        }
        else
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(runtimeAsset)) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (runtimeAsset != null && !UnityEditor.EditorUtility.IsPersistent(runtimeAsset))
                {
                    Object.DestroyImmediate(runtimeAsset, true);
                }
            };
#else
            Object.DestroyImmediate(runtimeAsset, true);
#endif
        }
    }

    private void Update()
    {
        charge = Mathf.MoveTowards(charge, lit ? 1f : 0f, Time.deltaTime * crystalChargeSpeed);

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
        }

        if (visualRoot != null)
        {
            // Xoay mượt về góc mục tiêu
            visualRoot.localRotation = Quaternion.Slerp(
                visualRoot.localRotation,
                targetRotation,
                Mathf.Clamp01(turnSpeed * Time.deltaTime));

            // Hiệu ứng nhịp đập & Kích thước khi kích hoạt / Overcharge
            float pulseScale = isOvercharged ? 1.4f : 1f;
            float pulse = 1f + Mathf.Sin(Time.time * (isOvercharged ? 6f : 2.5f)) * crystalPulseAmount * (charge + (isOvercharged ? 0.5f : 0f));
            visualRoot.localScale = Vector3.Scale(startScale, Vector3.Lerp(Vector3.one, litScale * pulseScale, charge) * pulse);

            // Hiệu ứng lơ lửng ma thuật (Floating Hover Idle)
            if (enableFloating && Application.isPlaying)
            {
                float floatOffset = Mathf.Sin(Time.time * floatFrequency + floatPhaseOffset) * floatAmplitude;
                visualRoot.localPosition = initialLocalPos + new Vector3(0f, floatOffset, 0f);
            }
        }

        if (glowRoot != null)
        {
            bool showGlow = charge > 0.01f || isOvercharged;
            glowRoot.SetActive(showGlow);
            if (glowRoot.activeSelf)
            {
                float overchargeMultiplier = isOvercharged ? 1.8f : 1f;
                glowRoot.transform.localScale = glowStartScale * ((1f + charge * 0.35f) * overchargeMultiplier);
            }
        }

        if (crystalRenderer != null)
        {
            Color baseColor = Color.Lerp(idleColor, litColor, charge);
            if (flashTimer > 0f)
            {
                // Activation Flash Overdrive (sáng chói màu trắng neon trong flashDuration đầu)
                float flashProgress = Mathf.Clamp01(flashTimer / flashDuration);
                crystalRenderer.color = Color.Lerp(baseColor, Color.white * 1.5f, flashProgress);
            }
            else if (isOvercharged)
            {
                crystalRenderer.color = Color.Lerp(litColor, Color.white, 0.6f);
            }
            else
            {
                crystalRenderer.color = baseColor;
            }
        }

        if (crystalEmbersParticle != null && Application.isPlaying)
        {
            if (charge > 0.5f || isOvercharged)
            {
                if (!crystalEmbersParticle.isPlaying) crystalEmbersParticle.Play();
            }
            else
            {
                if (crystalEmbersParticle.isPlaying) crystalEmbersParticle.Stop();
            }
        }

        UpdateAimGuide();
        UpdateStarGlint();
    }

    private void EnsureEmbersParticle()
    {
        if (crystalEmbersParticle == null)
        {
            Transform found = transform.Find("CrystalEmbers");
            if (found != null)
            {
                crystalEmbersParticle = found.GetComponent<ParticleSystem>();
            }
            else
            {
                GameObject psObj = new GameObject("CrystalEmbers");
                psObj.transform.SetParent(transform, false);
                crystalEmbersParticle = psObj.AddComponent<ParticleSystem>();

                var main = crystalEmbersParticle.main;
                main.playOnAwake = false;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
                main.startColor = new ParticleSystem.MinMaxGradient(litColor, Color.white);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 18;

                var emission = crystalEmbersParticle.emission;
                emission.rateOverTime = 5f;

                var shape = crystalEmbersParticle.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.35f;

                var colorOverLifetime = crystalEmbersParticle.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(litColor, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) }
                );
                colorOverLifetime.color = grad;

                var renderer = crystalEmbersParticle.GetComponent<ParticleSystemRenderer>();
                renderer.sortingOrder = 79;
                renderer.material = new Material(Shader.Find("Sprites/Default"));
            }
        }
    }

    private void OnValidate()
    {
        hitRadius = Mathf.Max(0.05f, hitRadius);
        turnSpeed = Mathf.Max(0.01f, turnSpeed);

        if (visualRoot != null && !Application.isPlaying)
        {
            UpdateTargetRotationImmediate();
        }
    }

    public void RotateStep(int steps = 1)
    {
        if (aimTargets != null && aimTargets.Length > 0)
        {
            currentAimIndex = (currentAimIndex + steps) % aimTargets.Length;
            if (currentAimIndex < 0) currentAimIndex += aimTargets.Length;
            UpdateTargetRotationImmediate();
            TriggerAimGuide();
            return;
        }

        if (customRotationAngles != null && customRotationAngles.Length > 0)
        {
            currentAngleIndex = (currentAngleIndex + steps) % customRotationAngles.Length;
            if (currentAngleIndex < 0) currentAngleIndex += customRotationAngles.Length;
            targetRotation = Quaternion.Euler(0f, 0f, customRotationAngles[currentAngleIndex]);
            TriggerAimGuide();
            return;
        }

        int directionCount = 4;
        int next = ((int)facing + steps) % directionCount;
        if (next < 0)
        {
            next += directionCount;
        }

        facing = (FaceDirection)next;
        targetRotation = Quaternion.Euler(0f, 0f, DirectionToAngle(facing));
        TriggerAimGuide();
    }

    public void SetCustomAngles(float[] angles, int startIndex = 0)
    {
        customRotationAngles = angles;
        currentAngleIndex = Mathf.Clamp(startIndex, 0, angles != null && angles.Length > 0 ? angles.Length - 1 : 0);
        UpdateTargetRotationImmediate();
    }

    public void SetAimTargets(Transform[] targets, int startIndex = 0)
    {
        aimTargets = targets;
        currentAimIndex = Mathf.Clamp(startIndex, 0, targets != null && targets.Length > 0 ? targets.Length - 1 : 0);
        UpdateTargetRotationImmediate();
    }

    public Vector2 GetSourceDirection()
    {
        if (aimTargets != null && aimTargets.Length > 0 && aimTargets[currentAimIndex] != null)
        {
            return ((Vector2)aimTargets[currentAimIndex].position - (Vector2)transform.position).normalized;
        }

        if (customRotationAngles != null && customRotationAngles.Length > 0)
        {
            float rad = customRotationAngles[currentAngleIndex] * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        }

        return FaceToVector(sourceDirection);
    }

    public Vector2 GetCurrentAimDirection()
    {
        return GetSourceDirection();
    }

    public bool TryReflect(Vector2 incomingDirection, out Vector2 outgoingDirection)
    {
        outgoingDirection = Vector2.zero;

        if (!reflector)
        {
            return false;
        }

        if ((aimTargets != null && aimTargets.Length > 0) || (customRotationAngles != null && customRotationAngles.Length > 0))
        {
            outgoingDirection = GetCurrentAimDirection();
            return outgoingDirection.sqrMagnitude > 0.001f;
        }

        FaceDirection incomingFace = GetIncomingFace(incomingDirection);
        DirectionRoute route = GetCurrentRoute();

        if (incomingFace == route.enterFace)
        {
            outgoingDirection = FaceToVector(route.exitFace);
        }
        else if (incomingFace == route.exitFace)
        {
            outgoingDirection = FaceToVector(route.enterFace);
        }
        else
        {
            return false;
        }

        return outgoingDirection.sqrMagnitude > 0.001f;
    }

    public void SetLit(bool value)
    {
        if (lit == value)
        {
            return;
        }

        lit = value;
        if (value)
        {
            flashTimer = flashDuration;
        }
        ApplyLit(value);
    }

    private void ApplyLit(bool value)
    {
        if (!Application.isPlaying)
        {
            charge = value ? 1f : 0f;
        }
    }

    private void UpdateTargetRotationImmediate()
    {
        if (aimTargets != null && aimTargets.Length > 0 && aimTargets[currentAimIndex] != null)
        {
            Vector2 dir = (Vector2)aimTargets[currentAimIndex].position - (Vector2)transform.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            targetRotation = Quaternion.Euler(0f, 0f, angle);
        }
        else if (customRotationAngles != null && customRotationAngles.Length > 0)
        {
            targetRotation = Quaternion.Euler(0f, 0f, customRotationAngles[currentAngleIndex]);
        }
        else
        {
            targetRotation = Quaternion.Euler(0f, 0f, DirectionToAngle(facing));
        }

        if (visualRoot != null && !Application.isPlaying)
        {
            visualRoot.localRotation = targetRotation;
        }
    }

    private DirectionRoute GetCurrentRoute()
    {
        if (routeStates == null || routeStates.Length == 0)
        {
            routeStates = CreateDefaultRoutes();
        }

        for (int i = 0; i < routeStates.Length; i++)
        {
            if (routeStates[i].facing == facing)
            {
                return routeStates[i];
            }
        }

        return routeStates[0];
    }

    private static FaceDirection GetIncomingFace(Vector2 incomingDirection)
    {
        FaceDirection travelDirection = VectorToFace(incomingDirection);
        return Opposite(travelDirection);
    }

    private static FaceDirection VectorToFace(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            return direction.x >= 0f ? FaceDirection.Right : FaceDirection.Left;
        }

        return direction.y >= 0f ? FaceDirection.Up : FaceDirection.Down;
    }

    private static FaceDirection Opposite(FaceDirection direction)
    {
        switch (direction)
        {
            case FaceDirection.Right:
                return FaceDirection.Left;
            case FaceDirection.Up:
                return FaceDirection.Down;
            case FaceDirection.Left:
                return FaceDirection.Right;
            default:
                return FaceDirection.Up;
        }
    }

    private static Vector2 FaceToVector(FaceDirection direction)
    {
        switch (direction)
        {
            case FaceDirection.Right:
                return Vector2.right;
            case FaceDirection.Up:
                return Vector2.up;
            case FaceDirection.Left:
                return Vector2.left;
            default:
                return Vector2.down;
        }
    }

    private static float DirectionToAngle(FaceDirection direction)
    {
        switch (direction)
        {
            case FaceDirection.Right:
                return 0f;
            case FaceDirection.Up:
                return 90f;
            case FaceDirection.Left:
                return 180f;
            default:
                return 270f;
        }
    }

    private static DirectionRoute[] CreateDefaultRoutes()
    {
        return new[]
        {
            new DirectionRoute { facing = FaceDirection.Right, enterFace = FaceDirection.Down, exitFace = FaceDirection.Right },
            new DirectionRoute { facing = FaceDirection.Up, enterFace = FaceDirection.Left, exitFace = FaceDirection.Up },
            new DirectionRoute { facing = FaceDirection.Left, enterFace = FaceDirection.Right, exitFace = FaceDirection.Left },
            new DirectionRoute { facing = FaceDirection.Down, enterFace = FaceDirection.Left, exitFace = FaceDirection.Down },
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  AIMING GUIDE & RETICLE RUNTIME — Tạo & cập nhật tia ngắm và reticle
    // ═══════════════════════════════════════════════════════════════

    public void TriggerAimGuide()
    {
        if (!enableAimGuide)
        {
            return;
        }

        EnsureAimGuideVisuals();
        aimGuideTimer = aimGuideDuration;

        // Kiểm tra xem hướng mới có nhắm trúng viên crystal nào tiếp theo không
        lockOnTargetCache = DetectTargetInAimDirection();
        if (lockOnTargetCache != null)
        {
            // Bùng nổ chớp sáng Lock-On Flash
            lockOnFlashTimer = 0.18f;
        }
        else
        {
            lockOnFlashTimer = 0f;
        }

        UpdateAimGuideVisualPositions();
    }

    private Transform DetectTargetInAimDirection()
    {
        if (aimTargets != null && aimTargets.Length > 0 && aimTargets[currentAimIndex] != null)
        {
            return aimTargets[currentAimIndex];
        }

        Vector2 origin = transform.position;
        Vector2 aimDir = GetCurrentAimDirection();

        // Raycast / Overlap kiểm tra các crystal khác trong scene
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin + aimDir * 2.5f, aimGuideMaxDistance);
        Transform closest = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
            {
                continue;
            }

            LaserMirrorCrystal2D otherCrystal = col.GetComponentInParent<LaserMirrorCrystal2D>();
            if (otherCrystal != null && otherCrystal != this)
            {
                Vector2 toOther = (Vector2)otherCrystal.transform.position - origin;
                float angle = Vector2.Angle(aimDir, toOther.normalized);
                if (angle <= angleTolerance)
                {
                    float dist = toOther.magnitude;
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = otherCrystal.transform;
                    }
                }
            }
        }

        return closest;
    }

    private void EnsureAimGuideVisuals()
    {
        if (aimGuideLine == null)
        {
            Transform lineObj = transform.Find("AimGuideRay");
            if (lineObj != null)
            {
                aimGuideLine = lineObj.GetComponent<LineRenderer>();
            }
            else
            {
                GameObject newObj = new GameObject("AimGuideRay");
                newObj.transform.SetParent(transform, false);
                aimGuideLine = newObj.AddComponent<LineRenderer>();
                aimGuideLine.useWorldSpace = true;
                aimGuideLine.positionCount = 2;
                aimGuideLine.startWidth = aimGuideWidth;
                aimGuideLine.endWidth = aimGuideWidth * 0.6f;
                aimGuideLine.sortingOrder = 78;
                aimGuideLine.material = new Material(Shader.Find("Sprites/Default"));
                aimGuideLine.enabled = false;
            }
        }

        if (aimReticle == null)
        {
            Transform reticleObj = transform.Find("AimReticle");
            if (reticleObj != null)
            {
                aimReticle = reticleObj.GetComponent<SpriteRenderer>();
            }
            else
            {
                GameObject newObj = new GameObject("AimReticle");
                newObj.transform.SetParent(transform, false);
                aimReticle = newObj.AddComponent<SpriteRenderer>();
                aimReticle.sortingOrder = 82;

                Sprite loadedReticle = Resources.Load<Sprite>("shockwave_1");
                if (loadedReticle == null)
                {
                    // Tạo procedural ring sprite nếu không có sẵn texture
                    if (runtimeReticleTexture == null)
                    {
                        runtimeReticleTexture = CreateReticleRingTexture(64);
                        runtimeReticleSprite = Sprite.Create(runtimeReticleTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
                    }
                    aimReticle.sprite = runtimeReticleSprite;
                }
                else
                {
                    aimReticle.sprite = loadedReticle;
                }

                aimReticle.material = new Material(Shader.Find("Sprites/Default"));
                aimReticle.enabled = false;
            }
        }

        ApplyAimGuideColors();
    }

    private static Texture2D CreateReticleRingTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerR = size * 0.46f;
        float innerR = size * 0.32f;
        float dotR = size * 0.12f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float ringAlpha = 0f;

                if (dist >= innerR && dist <= outerR)
                {
                    float edgeDist = Mathf.Min(dist - innerR, outerR - dist);
                    ringAlpha = Mathf.Clamp01(edgeDist / 2f);
                }
                else if (dist <= dotR)
                {
                    ringAlpha = Mathf.Clamp01((dotR - dist) / 1.5f);
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, ringAlpha));
            }
        }

        tex.Apply();
        return tex;
    }

    private void UpdateAimGuide()
    {
        if (aimGuideTimer <= 0f)
        {
            if (aimGuideLine != null && aimGuideLine.enabled) aimGuideLine.enabled = false;
            if (aimReticle != null && aimReticle.enabled) aimReticle.enabled = false;
            return;
        }

        aimGuideTimer -= Time.deltaTime;
        if (lockOnFlashTimer > 0f)
        {
            lockOnFlashTimer -= Time.deltaTime;
        }

        float normalizedLife = Mathf.Clamp01(aimGuideTimer / aimGuideDuration);
        float alpha = normalizedLife * aimGuideAlpha;

        // Nếu đang trong đợt Lock-On Flash: boost alpha lên 1.0 và màu neon trắng chói
        bool isLockOn = lockOnFlashTimer > 0f;
        if (isLockOn)
        {
            alpha = Mathf.Lerp(alpha, 1.0f, lockOnFlashTimer / 0.18f);
        }

        UpdateAimGuideVisualPositions();

        if (aimGuideLine != null)
        {
            aimGuideLine.enabled = true;
            Color startCol = isLockOn ? Color.white : Color.Lerp(aimGuideColor, Color.white, 0.4f);
            startCol.a = alpha;
            Color endCol = isLockOn ? aimGuideColor : aimGuideColor * 0.8f;
            endCol.a = alpha * (isLockOn ? 0.8f : 0.25f);
            aimGuideLine.startColor = startCol;
            aimGuideLine.endColor = endCol;
            aimGuideLine.startWidth = aimGuideWidth * (isLockOn ? 1.6f : 1f);
            aimGuideLine.endWidth = aimGuideWidth * (isLockOn ? 1.2f : 0.5f);
        }

        if (aimReticle != null)
        {
            aimReticle.enabled = true;
            Color retColor = isLockOn ? Color.white : aimGuideColor;
            retColor.a = alpha * (isLockOn ? 1.0f : 0.7f);
            aimReticle.color = retColor;

            float punchScale = isLockOn ? (1f + Mathf.Sin((0.18f - lockOnFlashTimer) / 0.18f * Mathf.PI) * 0.5f) : 1f;
            aimReticle.transform.localScale = aimReticleBaseScale * punchScale * (0.8f + (1f - normalizedLife) * 0.2f);
            aimReticle.transform.Rotate(0f, 0f, 180f * Time.deltaTime);
        }
    }

    private void UpdateAimGuideVisualPositions()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos;

        if (lockOnTargetCache != null)
        {
            endPos = lockOnTargetCache.position;
        }
        else if (aimTargets != null && aimTargets.Length > 0 && aimTargets[currentAimIndex] != null)
        {
            endPos = aimTargets[currentAimIndex].position;
        }
        else
        {
            Vector2 aimDir = GetCurrentAimDirection();
            endPos = startPos + (Vector3)aimDir * aimGuideMaxDistance;
        }

        if (aimGuideLine != null)
        {
            aimGuideLine.SetPosition(0, startPos);
            aimGuideLine.SetPosition(1, endPos);
        }

        if (aimReticle != null)
        {
            aimReticle.transform.position = endPos;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  CRYSTAL STAR GLINT & TWINKLE RUNTIME — Ngôi sao 4 cánh lấp lánh
    // ═══════════════════════════════════════════════════════════════

    private void EnsureStarGlintVisual()
    {
        if (starGlintRenderer == null)
        {
            Transform glintObj = transform.Find("StarGlint");
            if (glintObj != null)
            {
                starGlintRenderer = glintObj.GetComponent<SpriteRenderer>();
            }
            else
            {
                GameObject newObj = new GameObject("StarGlint");
                newObj.transform.SetParent(visualRoot != null ? visualRoot : transform, false);
                starGlintRenderer = newObj.AddComponent<SpriteRenderer>();
                starGlintRenderer.sortingOrder = 85;

                Sprite loadedStar = Resources.Load<Sprite>("T_Spark4");
                if (loadedStar == null)
                {
                    if (runtimeStarTexture == null)
                    {
                        runtimeStarTexture = CreateStarGlintTexture(48);
                        runtimeStarSprite = Sprite.Create(runtimeStarTexture, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 48f);
                    }
                    starGlintRenderer.sprite = runtimeStarSprite;
                }
                else
                {
                    starGlintRenderer.sprite = loadedStar;
                }

                starGlintRenderer.material = new Material(Shader.Find("Sprites/Default"));
                starGlintRenderer.enabled = false;
            }
        }
    }

    private static Texture2D CreateStarGlintTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center.x);
                float dy = Mathf.Abs(y - center.y);

                // Ngôi sao 4 cánh siêu sắc nét
                float crossX = Mathf.Max(0f, 1f - dx / radius) * Mathf.Max(0f, 1f - dy / (radius * 0.22f));
                float crossY = Mathf.Max(0f, 1f - dy / radius) * Mathf.Max(0f, 1f - dx / (radius * 0.22f));
                float centerGlow = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), center) / (radius * 0.35f));

                float alpha = Mathf.Clamp01(crossX + crossY + centerGlow * 1.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    private void ScheduleNextGlint()
    {
        float intervalMultiplier = (lit || isOvercharged) ? 0.45f : 1.0f;
        nextGlintTime = Time.time + Random.Range(glintMinInterval, glintMaxInterval) * intervalMultiplier;
    }

    private void UpdateStarGlint()
    {
        if (!enableStarGlint || !Application.isPlaying)
        {
            if (starGlintRenderer != null && starGlintRenderer.enabled)
            {
                starGlintRenderer.enabled = false;
            }
            return;
        }

        EnsureStarGlintVisual();

        // Kích hoạt lần chớp mới khi đến thời điểm
        if (glintTimer <= 0f && Time.time >= nextGlintTime)
        {
            glintTimer = glintDuration;
            glintBaseAngle = Random.Range(15f, 75f);

            // Random vị trí lấp lánh quanh các góc pha lê
            float rad = Random.Range(0.12f, 0.32f);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            glintLocalOffset = new Vector3(Mathf.Cos(angle) * rad, Mathf.Sin(angle) * rad, 0f);

            ScheduleNextGlint();
        }

        if (glintTimer > 0f)
        {
            glintTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(glintTimer / glintDuration);
            // Sinusoidal scale envelope: 0 -> 1 -> 0
            float envelope = Mathf.Sin(t * Mathf.PI);

            if (starGlintRenderer != null)
            {
                starGlintRenderer.enabled = true;
                starGlintRenderer.transform.localPosition = glintLocalOffset;
                float currentScale = glintScale * envelope * ((lit || isOvercharged) ? 1.35f : 1.0f);
                starGlintRenderer.transform.localScale = Vector3.one * currentScale;
                starGlintRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, glintBaseAngle + t * 45f);

                Color starCol = (lit || isOvercharged) ? Color.Lerp(Color.white, litColor, 0.3f) : Color.white;
                starCol.a = envelope;
                starGlintRenderer.color = starCol;
            }
        }
        else
        {
            if (starGlintRenderer != null && starGlintRenderer.enabled)
            {
                starGlintRenderer.enabled = false;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = goal ? Color.green : source ? Color.cyan : Color.white;
        Gizmos.DrawWireSphere(transform.position, hitRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)GetSourceDirection() * 1.5f);
    }
}

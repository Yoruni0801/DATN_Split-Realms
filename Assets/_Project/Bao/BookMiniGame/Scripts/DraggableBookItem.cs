using UnityEngine;

/// <summary>
/// Gắn lên từng cuốn sách 3D trong đống.
/// Xử lý: Raycast click, kiểm tra bị che, Drag & Drop.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class DraggableBookItem : MonoBehaviour
{
    [Header("Runtime Data (Tự gán bởi Manager)")]
    [HideInInspector] public BookEntry bookEntry;
    [HideInInspector] public int bookIndex;

    [Header("Drag Settings")]
    [Tooltip("Độ cao nâng lên khi đang kéo sách")]
    public float dragLiftHeight = 1.5f;

    [Tooltip("Tốc độ di chuyển mượt khi kéo")]
    public float dragSmoothSpeed = 15f;

    [Tooltip("Layer mask dùng cho Raycast kiểm tra sách bị che")]
    public LayerMask bookLayerMask = ~0;

    [Header("Pile Stability")]
    [Tooltip("Vận tốc tối đa khi thả sách để đống sách không bị nổ tung khi người chơi kéo nhanh.")]
    [Range(2f, 20f)]
    public float maxReleaseSpeed = 9f;

    [Tooltip("Giới hạn vận tốc quay khi sách va chạm trong đống.")]
    [Range(1f, 20f)]
    public float maxAngularSpeed = 7f;

    // Internal state
    private bool isDragging = false;
    private bool isDismissed = false;
    private bool hasBeenDragged = false;
    private bool isRecovering = false;
    private Camera mainCamera;
    private Rigidbody rb;
    private Collider col;
    private Vector3 dragOffset;
    private float dragPlaneY;
    private ParticleSystem landingDust;
    private static Material sharedLandingDustMaterial;
    private static Texture2D sharedLandingDustTexture;
    private float lastLandingParticleTime = -10f;

    // Tham chiếu đến Manager
    private BookMiniGameManager gameManager;

    // Visual feedback
    private Renderer[] renderers;

    public bool IsDragging => isDragging;
    public bool IsDismissed => isDismissed;
    public bool HasBeenDragged => hasBeenDragged;
    public bool IsRecovering => isRecovering;
    public float DragPlaneY => dragPlaneY;
    public Vector3 InteractionCenter => col != null ? col.bounds.center : transform.position;
    public Collider InteractionCollider => col;

    private float baseHeightY;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
        mainCamera = Camera.main;

        // Vô hiệu hóa lật sách khi sách đang nằm trong đống để tránh xung đột thao tác kéo (Drag)
        ManualBookController manualCtrl = GetComponent<ManualBookController>();
        if (manualCtrl != null)
        {
            manualCtrl.enabled = false;
        }
    }

    /// <summary>
    /// Khởi tạo cuốn sách với dữ liệu từ Manager
    /// </summary>
    public void Initialize(BookEntry entry, int index, BookMiniGameManager manager)
    {
        bookEntry = entry;
        bookIndex = index;
        gameManager = manager;

        // Đảm bảo rb và col đã được tham chiếu (phòng trường hợp được AddComponent bằng script)
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (col == null) col = GetComponent<Collider>();

        // Sách phải rơi tự nhiên khi bắt đầu Play Mode; chỉ cú thả của người chơi mới dùng soft landing.
        rb.mass = 1f;
        rb.linearDamping = 3.5f;
        rb.angularDamping = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.isKinematic = false;
        rb.useGravity = true;

        baseHeightY = transform.position.y;
    }

    /// <summary>
    /// Kiểm tra xem cuốn sách này có bị che bởi sách khác phía trên không.
    /// Bắn Raycast từ trên xuống, nếu không chạm vật nào khác trước khi chạm cuốn này thì OK.
    /// </summary>
    public bool IsBlockedByOtherBooks()
    {
        if (col == null) return false;

        // Lấy điểm cao nhất trên bề mặt cuốn sách
        Vector3 topPoint = col.bounds.center + Vector3.up * col.bounds.extents.y;

        // Bắn ray từ xa phía trên xuống
        Vector3 rayOrigin = topPoint + Vector3.up * 5f;
        Ray ray = new Ray(rayOrigin, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 10f, bookLayerMask);

        // Kiểm tra có vật nào nằm giữa ray origin và cuốn sách này không
        foreach (var hit in hits)
        {
            if (hit.collider.gameObject == gameObject) continue;

            DraggableBookItem otherBook = hit.collider.GetComponent<DraggableBookItem>();
            if (otherBook != null && !otherBook.IsDismissed)
            {
                // Nếu cuốn sách khác nằm phía trên cuốn này
                if (hit.point.y > topPoint.y - 0.1f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Lấy chính xác mặt rộng nhất của BoxCollider theo xoay/scale thực tế của cuốn sách.
    /// Mặt này dùng cho VFX hover và dấu hiệu giả, tránh dùng Bounds theo trục world.
    /// </summary>
    public bool TryGetBookSurface(out Vector3 center, out Vector3 axisU, out Vector3 axisV, out Vector3 normal)
    {
        center = transform.position;
        axisU = transform.right * 0.2f;
        axisV = transform.forward * 0.14f;
        normal = transform.up;

        if (!(col is BoxCollider box)) return false;

        Transform boxTransform = box.transform;
        Vector3 boxCenter = boxTransform.TransformPoint(box.center);
        Vector3[] dimensions =
        {
            boxTransform.TransformVector(Vector3.right * box.size.x),
            boxTransform.TransformVector(Vector3.up * box.size.y),
            boxTransform.TransformVector(Vector3.forward * box.size.z)
        };

        // Cạnh ngắn nhất là độ dày của sách. Hai cạnh còn lại chính là mặt bìa.
        int thicknessAxis = 0;
        if (dimensions[1].sqrMagnitude < dimensions[thicknessAxis].sqrMagnitude) thicknessAxis = 1;
        if (dimensions[2].sqrMagnitude < dimensions[thicknessAxis].sqrMagnitude) thicknessAxis = 2;

        int firstSurfaceAxis = (thicknessAxis + 1) % 3;
        int secondSurfaceAxis = (thicknessAxis + 2) % 3;
        axisU = dimensions[firstSurfaceAxis] * 0.5f;
        axisV = dimensions[secondSurfaceAxis] * 0.5f;

        normal = dimensions[thicknessAxis].normalized;
        // Luôn đưa VFX lên phía trên trong thế giới, kể cả khi sách bị lật nhẹ khi rơi.
        if (Vector3.Dot(normal, Vector3.up) < 0f) normal = -normal;

        center = boxCenter + normal * (dimensions[thicknessAxis].magnitude * 0.5f + 0.008f);
        return true;
    }

    /// <summary>
    /// Bắt đầu kéo sách
    /// </summary>
    public void StartDrag(Vector3 hitPoint, float targetDragPlaneY, bool forceCenter = false)
    {
        if (isDismissed) return;

        StopRecoveryIfNeeded();
        isDragging = true;
        hasBeenDragged = true;

        // Tắt vật lý khi đang kéo
        rb.isKinematic = true;
        rb.useGravity = false;

        // Cài đặt độ cao mặt phẳng kéo
        dragPlaneY = targetDragPlaneY;

        // Đảm bảo drag plane không bao giờ cao hơn camera (Tránh lỗi Raycast bị ngược khi đang Zoom)
        if (mainCamera != null && dragPlaneY > mainCamera.transform.position.y - 0.5f)
        {
            dragPlaneY = mainCamera.transform.position.y - 0.5f;
        }

        if (forceCenter)
        {
            // Ép vị trí chuột nằm ngay tâm vật lý (Local) của Collider để không bị lệch khi sách xoay
            if (col is BoxCollider boxCol)
            {
                Vector3 scaledCenter = new Vector3(
                    boxCol.center.x * transform.lossyScale.x,
                    boxCol.center.y * transform.lossyScale.y,
                    boxCol.center.z * transform.lossyScale.z
                );
                dragOffset = -scaledCenter;
            }
            else
            {
                dragOffset = Vector3.zero;
            }
        }
        else
        {
            // Giữ nguyên khoảng cách lúc click
            dragOffset = transform.position - hitPoint;
        }
        dragOffset.y = 0;
    }

    private Vector3 lastPos;
    private Vector3 throwVelocity;

    /// <summary>
    /// Cập nhật vị trí kéo mỗi frame
    /// </summary>
    public void UpdateDrag(Vector3 targetWorldPos)
    {
        if (!isDragging) return;

        Vector3 targetPos = new Vector3(
            targetWorldPos.x + dragOffset.x,
            dragPlaneY,
            targetWorldPos.z + dragOffset.z
        );

        Vector3 nextPos = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * dragSmoothSpeed);

        // Lưu lại vận tốc thực tế trong frame này để dùng cho lúc thả ra (ném đi)
        if (Time.deltaTime > 0)
        {
            throwVelocity = (nextPos - transform.position) / Time.deltaTime;
        }

        // Lấy vector vận tốc tương đối để bẻ cong sách (hiệu ứng Tilt)
        Vector3 tiltVelocity = (targetPos - transform.position) * 0.5f;

        // Cập nhật vị trí
        transform.position = nextPos;

        // Tạo hiệu ứng nghiêng theo quán tính
        Quaternion targetTilt = Quaternion.Euler(tiltVelocity.z * 5f, 0f, -tiltVelocity.x * 5f);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetTilt, Time.deltaTime * dragSmoothSpeed);
    }

    /// <summary>
    /// Kết thúc kéo sách
    /// </summary>
    public void EndDrag()
    {
        if (!isDragging) return;

        isDragging = false;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.ClampMagnitude(throwVelocity, maxReleaseSpeed);
        rb.angularVelocity = Vector3.ClampMagnitude(
            new Vector3(Random.Range(-2f, 2f), Random.Range(-5f, 5f), Random.Range(-2f, 2f)),
            maxAngularSpeed);
    }

    private void FixedUpdate()
    {
        if (rb == null || isDragging || isRecovering || isDismissed || rb.isKinematic) return;

        // Va chạm liên tiếp không thể tích lũy thành một cú nảy quá mạnh.
        rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxReleaseSpeed * 1.15f);
        rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, maxAngularSpeed);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDragging || isRecovering || isDismissed || collision.relativeVelocity.magnitude < 1.35f) return;
        if (Time.time - lastLandingParticleTime < 0.14f) return;

        lastLandingParticleTime = Time.time;
        Vector3 contactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : InteractionCenter;
        PlayLandingDust(contactPoint);
    }

    private void PlayLandingDust(Vector3 contactPoint)
    {
        if (col == null) return;
        EnsureLandingDust();
        if (landingDust == null) return;

        Bounds bounds = col.bounds;
        landingDust.transform.position = contactPoint + Vector3.up * 0.012f;
        landingDust.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        ParticleSystem.ShapeModule shape = landingDust.shape;
        shape.radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.5f, 0.08f, 0.3f);
        landingDust.Emit(Random.Range(5, 9));
    }

    private void EnsureLandingDust()
    {
        if (landingDust != null) return;

        GameObject dustObject = new GameObject("Book Landing Paper Dust");
        dustObject.transform.SetParent(transform, false);
        landingDust = dustObject.AddComponent<ParticleSystem>();
        landingDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = landingDust.main;
        main.playOnAwake = false;
        main.loop = false;
        main.maxParticles = 36;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.34f, 0.72f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.38f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.055f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.45f, 0.29f, 0.13f, 0.06f),
            new Color(0.92f, 0.7f, 0.37f, 0.32f));

        ParticleSystem.EmissionModule emission = landingDust.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = landingDust.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;

        ParticleSystem.NoiseModule noise = landingDust.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.08f;
        noise.frequency = 0.7f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = landingDust.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.95f, 0.72f, 0.36f), 0f),
                new GradientColorKey(new Color(0.48f, 0.27f, 0.1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.42f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer renderer = landingDust.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = GetLandingDustMaterial();
    }

    private static Material GetLandingDustMaterial()
    {
        if (sharedLandingDustMaterial != null) return sharedLandingDustMaterial;

        Shader shader = Shader.Find("Custom/Book/Soft Spark");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        sharedLandingDustMaterial = new Material(shader) { name = "Book Landing Dust (Runtime)" };
        if (sharedLandingDustMaterial.HasProperty("_MainTex"))
        {
            sharedLandingDustMaterial.SetTexture("_MainTex", GetLandingDustTexture());
        }
        return sharedLandingDustMaterial;
    }

    private static Texture2D GetLandingDustTexture()
    {
        if (sharedLandingDustTexture != null) return sharedLandingDustTexture;

        const int size = 32;
        sharedLandingDustTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Book Landing Dust Texture (Runtime)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = (x + 0.5f) / size * 2f - 1f;
                float py = (y + 0.5f) / size * 2f - 1f;
                float radial = Mathf.Clamp01(1f - Mathf.Sqrt(px * px + py * py));
                float alpha = radial * radial;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        sharedLandingDustTexture.SetPixels(pixels);
        sharedLandingDustTexture.Apply(false, true);
        return sharedLandingDustTexture;
    }

    /// <summary>
    /// Đưa sách nhiệm vụ trở lại vùng nhìn thấy khi bị ném ra ngoài camera
    /// hoặc bị khuất sau UI tutorial. Chỉ Manager gọi hàm này cho sách đúng.
    /// </summary>
    public void RecoverTo(Vector3 targetPosition, float duration = 0.55f, bool keepAtRecoveryPoint = false)
    {
        if (isDismissed || isDragging || isRecovering) return;
        StartCoroutine(RecoverRoutine(targetPosition, duration, keepAtRecoveryPoint));
    }

    private System.Collections.IEnumerator RecoverRoutine(Vector3 targetPosition, float duration, bool keepAtRecoveryPoint)
    {
        isRecovering = true;
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        col.enabled = false;

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.Euler(
            Random.Range(-6f, 6f),
            Random.Range(0f, 360f),
            Random.Range(-6f, 6f));
        float elapsed = 0f;
        duration = Mathf.Max(0.1f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float arc = Mathf.Sin(t * Mathf.PI) * 0.55f;

            transform.position = Vector3.Lerp(startPosition, targetPosition, eased) + Vector3.up * arc;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);
            yield return null;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;
        col.enabled = true;
        rb.isKinematic = keepAtRecoveryPoint;
        rb.useGravity = !keepAtRecoveryPoint;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        isRecovering = false;
    }

    private void StopRecoveryIfNeeded()
    {
        if (!isRecovering) return;

        StopAllCoroutines();
        isRecovering = false;
        if (col != null) col.enabled = true;
    }

    /// <summary>
    /// Loại bỏ sách sau khi kiểm tra sai (làm mờ và vô hiệu hóa)
    /// </summary>
    public void Dismiss()
    {
        isDismissed = true;

        // Làm mờ sách
        foreach (var rend in renderers)
        {
            foreach (var mat in rend.materials)
            {
                Color c = mat.color;
                c.a = 0.3f;
                mat.color = c;
            }
        }

        // Vô hiệu hóa collider để không cản trở
        col.enabled = false;
        rb.isKinematic = true;
    }

    /// <summary>
    /// Di chuyển sách đến vị trí chỉ định (dùng khi snap vào bàn kiểm tra)
    /// </summary>
    public void SnapToPosition(Vector3 position, Quaternion rotation)
    {
        rb.isKinematic = true;
        rb.useGravity = false;
        StartCoroutine(SnapRoutine(position, rotation));
    }

    private System.Collections.IEnumerator SnapRoutine(Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;
        float duration = 0.2f; // Thời gian đặt sách xuống (mượt mà)

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Easing Out Cubic để sách rơi xuống có cảm giác nhẹ nhàng rồi nằm im
            float easeT = 1f - Mathf.Pow(1f - t, 3f);

            transform.position = Vector3.Lerp(startPos, targetPos, easeT);
            transform.rotation = Quaternion.Lerp(startRot, targetRot, easeT);
            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;
    }

    /// <summary>
    /// Lướt sách mượt mà sang một bên (ví dụ bên phải) và hủy object
    /// </summary>
    public void ThrowRightAndDestroy(Vector3 direction, float speed = 12f)
    {
        isDismissed = true; // Đánh dấu bị loại bỏ, không thể click trúng nữa

        rb.isKinematic = true; // Tắt hoàn toàn vật lý để trượt mượt mà
        rb.useGravity = false;
        col.enabled = false; // Tắt va chạm

        StartCoroutine(SlideAndDestroyRoutine(direction, 0.7f)); // Lướt trong 0.7 giây rồi hủy
    }

    private System.Collections.IEnumerator SlideAndDestroyRoutine(Vector3 direction, float duration)
    {
        Vector3 startPos = transform.position;
        // Điểm đến: trượt sang ngang một đoạn dài để ra khỏi màn hình
        Vector3 targetPos = startPos + direction.normalized * 15f; 
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Easing In (Bắt đầu từ từ rồi bay vút đi)
            float easeT = t * t * t;

            transform.position = Vector3.Lerp(startPos, targetPos, easeT);
            yield return null;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Ném sách ngược về đống và giữ nguyên trạng thái (cho phép nhặt lại)
    /// </summary>
    public void ThrowBack(Vector3 throwDirection, float force = 4f)
    {
        rb.isKinematic = false;
        rb.useGravity = true;
        col.enabled = true;

        rb.linearVelocity = throwDirection * force;
        rb.angularVelocity = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f));
    }
}

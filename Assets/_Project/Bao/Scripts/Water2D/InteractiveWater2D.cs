using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Interactive 2D Water Simulation - Spring-based surface with dynamic mesh.
/// Features: Wave physics, splash on impact, buoyancy, mouse interaction.
/// Place at the water surface Y position. Water extends downward by 'depth'.
/// </summary>
[ExecuteAlways]
public class InteractiveWater2D : MonoBehaviour
{
    [Header("=== Water Body ===")]
    [Tooltip("Độ rộng của khối nước tính theo đơn vị thế giới (world units).")]
    public float width = 12f;
    [Tooltip("Độ sâu của khối nước tính từ bề mặt trở xuống.")]
    public float depth = 4f;
    [Range(10, 200)]
    [Tooltip("Số lượng các lò xo (điểm chia) trên bề mặt. Độ phân giải cao giúp sóng uốn lượn mượt mà hơn nhưng tốn hiệu năng hơn.")]
    public int surfaceResolution = 100;

    [Header("=== Wave Physics ===")]
    [Range(0.001f, 0.5f)]
    [Tooltip("Độ cứng lò xo bề mặt. Giá trị lớn làm mặt nước hồi phục về trạng thái phẳng nhanh hơn và dao động mạnh hơn.")]
    public float springConstant = 0.02f;
    [Range(0.001f, 0.2f)]
    [Tooltip("Hệ số giảm chấn. Giá trị cao giúp sóng dập tắt dao động và ổn định lại nhanh hơn.")]
    public float damping = 0.04f;
    [Range(0.01f, 1f)]
    [Tooltip("Tốc độ lan truyền sóng dọc theo bề mặt nước.")]
    public float spreadSpeed = 0.1f;
    [Range(1, 16)]
    [Tooltip("Số lần tính toán lan truyền sóng mỗi frame. Cao hơn giúp lan sóng mượt hơn nhưng tốn CPU.")]
    public int spreadIterations = 8;

    [Header("=== Splash ===")]
    [Tooltip("Hệ số nhân lực bắn nước. Vận tốc va chạm nhân với hệ số này sẽ tạo ra độ sâu/cao của sóng bắn.")]
    public float splashForceMultiplier = 0.05f;
    [Range(1, 10)]
    [Tooltip("Độ rộng ảnh hưởng (số lò xo lân cận) xung quanh điểm va chạm khi tạo sóng.")]
    public int splashWidth = 4;

    [Header("=== Visuals ===")]
    [Tooltip("Màu sắc của nước ở sát bề mặt.")]
    public Color surfaceColor = new Color(0.3f, 0.75f, 1f, 0.85f);
    [Tooltip("Màu sắc của nước dưới đáy sâu (tạo gradient chuyển đổi từ màu bề mặt xuống màu đáy).")]
    public Color depthColor = new Color(0.05f, 0.15f, 0.45f, 0.95f);
    [Tooltip("Bật/tắt đường viền sáng nổi bật trên bề mặt nước.")]
    public bool showSurfaceLine = true;
    [Tooltip("Màu sắc của đường viền bề mặt nước.")]
    public Color surfaceLineColor = new Color(0.85f, 0.95f, 1f, 0.95f);
    [Range(0.02f, 0.15f)]
    [Tooltip("Độ dày của đường viền bề mặt nước.")]
    public float surfaceLineWidth = 0.07f;

    [Header("Buoyancy Settings")]
    [Tooltip("Lực đẩy nổi phụ thêm cho vật thể chìm trong nước. Giúp đưa vật thể nổi ngược lên bề mặt.")]
    public float buoyancyForce = 45f;
    [Tooltip("Lực cản tịnh tiến của nước tác dụng lên vật thể (độ đặc/nhớt). Giảm tốc độ rơi của vật ngay lập tức.")]
    public float waterDrag = 8f;
    [Tooltip("Lực cản quay của nước tác dụng lên vật thể. Giúp giảm tốc độ tự quay của vật khi chìm trong nước.")]
    public float waterAngularDrag = 5f;

    // Internal
    float[] positions;  // Y offset from rest (0 = flat surface)
    float[] velocities;
    float[] xCoords;    // Local X positions of each spring

    Mesh mesh;
    MeshFilter mf;
    MeshRenderer mr;
    Vector3[] verts;
    Color[] colors;
    LineRenderer lr;
    EdgeCollider2D edgeCol;
    BoxCollider2D boxCol;

    Dictionary<Rigidbody2D, float[]> tracked = new Dictionary<Rigidbody2D, float[]>();

    // =========================================
    // INIT
    // =========================================
    void OnEnable()
    {
        mf = GetComponent<MeshFilter>(); if (!mf) mf = gameObject.AddComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>(); if (!mr) mr = gameObject.AddComponent<MeshRenderer>();

        if (mr.sharedMaterial == null || !mr.sharedMaterial.shader.name.Contains("Sprites"))
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.renderQueue = 3000;
            mr.sharedMaterial = mat;
        }

        InitSprings();
        BuildMesh();
        SetupColliders();
        SetupLine();
    }

    void OnDisable()
    {
        foreach (var kvp in tracked)
            if (kvp.Key) { kvp.Key.linearDamping = kvp.Value[0]; kvp.Key.angularDamping = kvp.Value[1]; }
        tracked.Clear();
    }

    void InitSprings()
    {
        int n = surfaceResolution;
        positions = new float[n];
        velocities = new float[n];
        xCoords = new float[n];
        float step = width / (n - 1);
        for (int i = 0; i < n; i++)
        {
            xCoords[i] = -width / 2f + i * step;
            positions[i] = 0;
            velocities[i] = 0;
        }
    }

    // =========================================
    // MESH
    // =========================================
    void BuildMesh()
    {
        int n = surfaceResolution;
        mesh = new Mesh { name = "Water2D" };
        verts = new Vector3[n * 2];
        colors = new Color[n * 2];
        var uvs = new Vector2[n * 2];
        var tris = new int[(n - 1) * 6];

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (n - 1);
            verts[i] = new Vector3(xCoords[i], positions[i], 0);               // top
            verts[i + n] = new Vector3(xCoords[i], -depth, 0);                 // bottom
            colors[i] = surfaceColor;
            colors[i + n] = depthColor;
            uvs[i] = new Vector2(t, 1);
            uvs[i + n] = new Vector2(t, 0);
        }

        int idx = 0;
        for (int i = 0; i < n - 1; i++)
        {
            tris[idx++] = i; tris[idx++] = i + 1; tris[idx++] = i + n;
            tris[idx++] = i + 1; tris[idx++] = i + 1 + n; tris[idx++] = i + n;
        }

        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        mf.mesh = mesh;
    }

    void RefreshMesh()
    {
        if (mesh == null || verts == null) return;
        int n = surfaceResolution;
        for (int i = 0; i < n; i++)
        {
            verts[i].y = positions[i];
            colors[i] = surfaceColor;
            colors[i + n] = depthColor;
        }
        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.RecalculateBounds();
    }

    // =========================================
    // COLLIDERS
    // =========================================
    void SetupColliders()
    {
        if (boxCol == null)
        {
            boxCol = GetComponent<BoxCollider2D>();
            if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider2D>();
        }
        boxCol.isTrigger = true;
        
        // Use a very deep physics box to prevent fast-falling objects from escaping the bottom trigger zone
        float physicsDepth = Mathf.Max(depth, 100f);
        boxCol.size = new Vector2(width, physicsDepth);
        boxCol.offset = new Vector2(0, -physicsDepth / 2f);

        edgeCol = GetComponent<EdgeCollider2D>();
        if (edgeCol == null) edgeCol = gameObject.AddComponent<EdgeCollider2D>();
        edgeCol.isTrigger = false;
        edgeCol.enabled = false; // We don't need edge collider for trigger physics, BoxCollider handles it
    }

    void RefreshEdge()
    {
        if (!edgeCol) return;
        var pts = new List<Vector2>(surfaceResolution);
        for (int i = 0; i < surfaceResolution; i++)
            pts.Add(new Vector2(xCoords[i], positions[i]));
        edgeCol.SetPoints(pts);
    }

    // =========================================
    // LINE RENDERER
    // =========================================
    void SetupLine()
    {
        lr = GetComponent<LineRenderer>();
        if (showSurfaceLine)
        {
            if (!lr) lr = gameObject.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = surfaceLineColor;
            lr.startWidth = lr.endWidth = surfaceLineWidth;
            lr.positionCount = surfaceResolution;
            lr.useWorldSpace = true;
            lr.sortingOrder = 1;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
        }
        else if (lr) lr.enabled = false;
    }

    void RefreshLine()
    {
        if (!lr || !showSurfaceLine) return;
        lr.positionCount = surfaceResolution;
        lr.startColor = lr.endColor = surfaceLineColor;
        lr.startWidth = lr.endWidth = surfaceLineWidth;
        var o = transform.position;
        for (int i = 0; i < surfaceResolution; i++)
            lr.SetPosition(i, new Vector3(xCoords[i] + o.x, positions[i] + o.y, o.z - 0.01f));
    }

    // =========================================
    // PHYSICS UPDATE
    // =========================================
    void FixedUpdate()
    {
        if (!Application.isPlaying) return;
        if (positions == null) return;

        int n = surfaceResolution;

        // 1. Spring forces (Hooke's law): F = -kx - bv
        for (int i = 0; i < n; i++)
        {
            float accel = -springConstant * positions[i] - damping * velocities[i];
            velocities[i] += accel;
            positions[i] += velocities[i];
        }

        // 2. Wave propagation
        for (int p = 0; p < spreadIterations; p++)
        {
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                {
                    float diff = spreadSpeed * (positions[i] - positions[i - 1]);
                    velocities[i - 1] += diff;
                    positions[i - 1] += diff * 0.5f;
                }
                if (i < n - 1)
                {
                    float diff = spreadSpeed * (positions[i] - positions[i + 1]);
                    velocities[i + 1] += diff;
                    positions[i + 1] += diff * 0.5f;
                }
            }
            
            // Lock boundary positions to 0 at each propagation step to eliminate edge instability
            if (n > 1)
            {
                positions[0] = 0f;
                velocities[0] = 0f;
                positions[n - 1] = 0f;
                velocities[n - 1] = 0f;
            }
        }

        // 3. Buoyancy
        ApplyBuoyancy();
    }

    void LateUpdate()
    {
        if (positions == null || positions.Length != surfaceResolution)
        {
            InitSprings(); BuildMesh(); SetupColliders(); SetupLine();
            return;
        }

        RefreshMesh();
        RefreshLine();
    }

    // =========================================
    // SPLASH API
    // =========================================
    /// <summary>Create a splash at localX with given velocity (negative = push surface down)</summary>
    public void Splash(float localX, float velocity)
    {
        if (positions == null) return;
        float step = width / (surfaceResolution - 1);
        int idx = Mathf.RoundToInt((localX + width / 2f) / step);
        idx = Mathf.Clamp(idx, 0, surfaceResolution - 1);

        for (int i = -splashWidth; i <= splashWidth; i++)
        {
            int j = idx + i;
            if (j >= 0 && j < surfaceResolution)
            {
                float falloff = 1f - Mathf.Abs(i) / (splashWidth + 1f);
                velocities[j] += velocity * falloff;
            }
        }
    }

    /// <summary>Get world Y of water surface at worldX</summary>
    public float GetSurfaceHeight(float worldX)
    {
        float lx = worldX - transform.position.x;
        float step = width / (surfaceResolution - 1);
        int idx = Mathf.RoundToInt((lx + width / 2f) / step);
        idx = Mathf.Clamp(idx, 0, surfaceResolution - 1);
        return transform.position.y + positions[idx];
    }

    // =========================================
    // BUOYANCY
    // =========================================
    void ApplyBuoyancy()
    {
        List<Rigidbody2D> dead = null;
        foreach (var kvp in tracked)
        {
            var rb = kvp.Key;
            if (!rb) { (dead ??= new List<Rigidbody2D>()).Add(rb); continue; }

            float surfY = GetSurfaceHeight(rb.position.x);
            float sub = Mathf.Clamp01((surfY - rb.position.y) / 1f);
            if (sub > 0)
            {
                float targetBuoyancy = (Physics2D.gravity.magnitude * rb.gravityScale + buoyancyForce) * rb.mass * sub;
                rb.AddForce(Vector2.up * targetBuoyancy, ForceMode2D.Force);
                
                // Water Drag
                rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, waterDrag * Time.fixedDeltaTime);
                rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, 0f, waterAngularDrag * Time.fixedDeltaTime);

                if (Mathf.Abs(rb.linearVelocity.y) > 0.1f)
                    Splash(rb.position.x - transform.position.x, -rb.linearVelocity.y * 0.02f);
            }
        }
        if (dead != null) foreach (var r in dead) tracked.Remove(r);
    }

    // =========================================
    // TRIGGERS
    // =========================================
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!Application.isPlaying) return;
        Debug.Log("[Water2D] OnTriggerEnter2D: " + other.gameObject.name);
        var rb = other.attachedRigidbody;
        if (!rb) return;

        float lx = rb.position.x - transform.position.x;
        float force = rb.linearVelocity.y * splashForceMultiplier * Mathf.Clamp(rb.mass, 0.5f, 3f);
        Splash(lx, force);

        if (!tracked.ContainsKey(rb))
        {
            tracked[rb] = new float[] { 0, 0 };
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!Application.isPlaying) return;
        var rb = other.attachedRigidbody;
        if (!rb) return;

        float lx = rb.position.x - transform.position.x;
        float force = rb.linearVelocity.y * splashForceMultiplier * 0.5f * Mathf.Clamp(rb.mass, 0.5f, 3f);
        Splash(lx, force);

        if (tracked.ContainsKey(rb))
        {
            tracked.Remove(rb);
        }
    }

    // =========================================
    // GIZMOS
    // =========================================
    void OnDrawGizmosSelected()
    {
        var c = transform.position + new Vector3(0, -depth / 2f, 0);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.25f);
        Gizmos.DrawCube(c, new Vector3(width, depth, 0.1f));
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.7f);
        Gizmos.DrawWireCube(c, new Vector3(width, depth, 0.1f));
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position + Vector3.left * width / 2f, transform.position + Vector3.right * width / 2f);
    }

    void OnValidate()
    {
        if (surfaceResolution < 10) surfaceResolution = 10;
        if (width < 0.5f) width = 0.5f;
        if (depth < 0.1f) depth = 0.1f;
    }
}

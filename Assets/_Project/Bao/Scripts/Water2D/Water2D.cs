using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D))]
public class Water2D : MonoBehaviour
{
    [Header("Water Settings")]
    public float width = 10f;
    public float height = 5f;
    public int resolution = 50;
    public float tension = 0.025f;
    public float dampening = 0.025f;
    public float spread = 0.25f;

    [Header("Visuals")]
    public Color topColor = new Color(0.2f, 0.7f, 1f, 0.8f);
    public Color bottomColor = new Color(0.1f, 0.3f, 0.8f, 0.9f);

    [Header("Interaction")]
    public float splashMultiplier = 0.2f;

    private float[] xPositions;
    private float[] yPositions;
    private float[] velocities;
    private float[] accelerations;

    private Mesh mesh;
    private MeshFilter meshFilter;
    private PolygonCollider2D polyCollider;

    private void OnEnable()
    {
        meshFilter = GetComponent<MeshFilter>();
        polyCollider = GetComponent<PolygonCollider2D>();
        polyCollider.isTrigger = true;

        InitializeSprings();
        GenerateMesh();
    }

    private void InitializeSprings()
    {
        xPositions = new float[resolution];
        yPositions = new float[resolution];
        velocities = new float[resolution];
        accelerations = new float[resolution];

        float step = width / (resolution - 1);
        for (int i = 0; i < resolution; i++)
        {
            xPositions[i] = i * step - width / 2f;
            yPositions[i] = height; // Initial height
            velocities[i] = 0f;
            accelerations[i] = 0f;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) 
        {
            UpdateMesh();
            return;
        }

        // 1. Calculate spring forces
        for (int i = 0; i < resolution; i++)
        {
            float targetHeight = height;
            float force = tension * (targetHeight - yPositions[i]) - velocities[i] * dampening;
            accelerations[i] = force;
            yPositions[i] += velocities[i];
            velocities[i] += accelerations[i];
        }

        // 2. Wave propagation
        float[] leftDeltas = new float[resolution];
        float[] rightDeltas = new float[resolution];

        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < resolution; i++)
            {
                if (i > 0)
                {
                    leftDeltas[i] = spread * (yPositions[i] - yPositions[i - 1]);
                    velocities[i - 1] += leftDeltas[i];
                }
                if (i < resolution - 1)
                {
                    rightDeltas[i] = spread * (yPositions[i] - yPositions[i + 1]);
                    velocities[i + 1] += rightDeltas[i];
                }
            }
            for (int i = 0; i < resolution; i++)
            {
                if (i > 0) yPositions[i - 1] += leftDeltas[i];
                if (i < resolution - 1) yPositions[i + 1] += rightDeltas[i];
            }
        }

        UpdateMesh();
    }

    private void GenerateMesh()
    {
        mesh = new Mesh();
        mesh.name = "WaterMesh";
        Vector3[] vertices = new Vector3[resolution * 2];
        Vector2[] uv = new Vector2[resolution * 2];
        Color[] colors = new Color[resolution * 2];
        int[] triangles = new int[(resolution - 1) * 6];

        for (int i = 0; i < resolution; i++)
        {
            vertices[i] = new Vector3(xPositions[i], yPositions[i], 0);
            vertices[i + resolution] = new Vector3(xPositions[i], 0, 0); // Bottom vertices

            uv[i] = new Vector2((float)i / (resolution - 1), 1);
            uv[i + resolution] = new Vector2((float)i / (resolution - 1), 0);
            
            colors[i] = topColor;
            colors[i + resolution] = bottomColor;
        }

        int ti = 0;
        for (int i = 0; i < resolution - 1; i++)
        {
            // First triangle (Top-Left, Top-Right, Bottom-Left)
            triangles[ti] = i;
            triangles[ti + 1] = i + 1;
            triangles[ti + 2] = i + resolution;

            // Second triangle (Top-Right, Bottom-Right, Bottom-Left)
            triangles[ti + 3] = i + 1;
            triangles[ti + 4] = i + resolution + 1;
            triangles[ti + 5] = i + resolution;
            ti += 6;
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.colors = colors;
        mesh.triangles = triangles;
        meshFilter.mesh = mesh;

        UpdateCollider();
    }

    private void UpdateMesh()
    {
        if (mesh == null) return;
        Vector3[] vertices = mesh.vertices;
        Color[] colors = mesh.colors;
        if (colors == null || colors.Length != vertices.Length)
            colors = new Color[vertices.Length];

        for (int i = 0; i < resolution; i++)
        {
            vertices[i] = new Vector3(xPositions[i], yPositions[i], 0);
            colors[i] = topColor;
            colors[i + resolution] = bottomColor;
        }
        mesh.vertices = vertices;
        mesh.colors = colors;
    }

    private void UpdateCollider()
    {
        Vector2[] points = new Vector2[resolution + 2];
        for (int i = 0; i < resolution; i++)
        {
            points[i] = new Vector2(xPositions[i], height);
        }
        points[resolution] = new Vector2(xPositions[resolution - 1], 0);
        points[resolution + 1] = new Vector2(xPositions[0], 0);

        polyCollider.SetPath(0, points);
    }

    public void Splash(float xPos, float velocity)
    {
        if (xPos >= xPositions[0] && xPos <= xPositions[resolution - 1])
        {
            xPos -= xPositions[0];
            float step = width / (resolution - 1);
            int index = Mathf.RoundToInt(xPos / step);
            if (index >= 0 && index < resolution)
            {
                velocities[index] += velocity;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Rigidbody2D rb = collision.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            float impact = rb.linearVelocity.y * splashMultiplier; 
            if (impact < 0) 
            {
                Splash(collision.transform.position.x - transform.position.x, impact);
            }
        }
    }
}

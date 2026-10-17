using UnityEngine;

/// <summary>
/// Spawns objects periodically that fall into the water to demonstrate splash interaction.
/// Attach to a GameObject positioned above the water.
/// </summary>
public class WaterDropSpawner : MonoBehaviour
{
    [Header("Spawner")]
    public GameObject prefabToSpawn;
    public float spawnInterval = 2f;
    public float randomXRange = 4f;
    public float objectScale = 0.5f;
    public int maxObjects = 15;

    [Header("Auto-Create (if no prefab)")]
    public bool autoCreateCube = true;

    private float timer;
    private int spawnedCount;

    void Update()
    {
        if (!Application.isPlaying) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval && spawnedCount < maxObjects)
        {
            timer = 0;
            SpawnObject();
        }
    }

    void SpawnObject()
    {
        float x = transform.position.x + Random.Range(-randomXRange, randomXRange);
        float y = transform.position.y;
        Vector3 pos = new Vector3(x, y, 0);

        GameObject obj;
        if (prefabToSpawn != null)
        {
            obj = Instantiate(prefabToSpawn, pos, Quaternion.identity);
        }
        else if (autoCreateCube)
        {
            obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.transform.position = pos;
            obj.transform.localScale = Vector3.one * objectScale;

            // Remove 3D collider and add 2D
            var col3d = obj.GetComponent<Collider>();
            if (col3d != null) DestroyImmediate(col3d);

            obj.AddComponent<CircleCollider2D>();
            var rb = obj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1f;

            // Random color
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Sprites/Default"));
                renderer.material.color = Color.HSVToRGB(Random.value, 0.7f, 0.9f);
            }
        }
        else
        {
            return;
        }

        spawnedCount++;

        // Auto-destroy after some time
        Destroy(obj, 10f);
    }
}

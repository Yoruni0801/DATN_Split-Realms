using UnityEngine;

/// <summary>
/// Test helper for InteractiveWater2D. 
/// Spawns falling objects and allows mouse click interaction.
/// </summary>
public class Water2DTestHelper : MonoBehaviour
{
    public InteractiveWater2D water;
    public float spawnInterval = 1.5f;
    public float spawnHeight = 5f;
    public float objectSize = 0.4f;
    public int maxObjects = 20;
    
    [Header("Mouse Interaction")]
    public float clickSplashForce = -5f;
    
    private float timer;
    private int count;
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
        if (water == null) water = FindAnyObjectByType<InteractiveWater2D>();
    }

    void Update()
    {
        // Auto-spawn falling objects
        timer += Time.deltaTime;
        if (timer >= spawnInterval && count < maxObjects)
        {
            timer = 0;
            SpawnFallingObject();
        }

        // Mouse click splash
        if (Input.GetMouseButtonDown(0) && mainCam != null && water != null)
        {
            Vector3 worldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
            float localX = worldPos.x - water.transform.position.x;
            water.Splash(localX, clickSplashForce);
        }
    }

    void SpawnFallingObject()
    {
        if (water == null) return;
        
        float halfWidth = water.width / 2f - 1f;
        float x = water.transform.position.x + Random.Range(-halfWidth, halfWidth);
        float y = water.transform.position.y + spawnHeight;

        GameObject obj = new GameObject("WaterTestObj_" + count);
        obj.transform.position = new Vector3(x, y, 0);

        // Add sprite renderer
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite();
        sr.color = Color.HSVToRGB(Random.value, 0.6f, 0.95f);
        obj.transform.localScale = Vector3.one * objectSize;

        // Add physics
        var rb = obj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 2f;
        rb.mass = Random.Range(0.5f, 2f);
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = obj.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        count++;
        Destroy(obj, 12f);
    }

    Sprite MakeCircleSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size / 2f;
        float radius = center - 1;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= radius)
                    tex.SetPixel(x, y, Color.white);
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
    }
}

using UnityEngine;

public class IcicleDrip : MonoBehaviour
{
    private SpriteRenderer sr;
    private ParticleSystem dripPS;
    
    [Header("Cấu hình Nhỏ giọt")]
    [Tooltip("Thời gian ngẫu nhiên giữa các giọt nước")]
    public float minDripInterval = 2f;
    public float maxDripInterval = 5f;
    
    [Tooltip("Màu sắc của giọt nước")]
    public Color dropColor = new Color(0.6f, 0.85f, 1.0f, 0.8f);

    [Tooltip("Kích thước giọt nước nhỏ nhất và lớn nhất")]
    public float minDropSize = 0.08f;
    public float maxDropSize = 0.18f;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            enabled = false;
            return;
        }

        CreateDripParticleSystem();
    }

    void CreateDripParticleSystem()
    {
        // 1. Tạo GameObject con tại đầu nhọn của nhũ băng
        GameObject dripObj = new GameObject("DripEmitter");
        
        // Tính toán đầu nhọn (thấp nhất) của nhũ băng dựa trên Sprite Bounds
        Vector3 tipPos = new Vector3(sr.bounds.center.x, sr.bounds.min.y, transform.position.z);
        dripObj.transform.position = tipPos;
        dripObj.transform.SetParent(transform);

        // 2. Gán Particle System
        dripPS = dripObj.AddComponent<ParticleSystem>();
        dripPS.Stop();

        // 3. Cấu hình các thuộc tính của Particle System để rơi giọt nước tự nhiên
        var main = dripPS.main;
        main.duration = 1f;
        main.loop = true;
        main.startDelay = Random.Range(0f, 3f); // Tránh nhỏ giọt đồng thanh cùng lúc
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f); // Rơi khoảng 2s thì biến mất
        main.startSpeed = 0f; // Bắt đầu rơi tự do từ vận tốc 0
        main.startSize = new ParticleSystem.MinMaxCurve(minDropSize, maxDropSize); // Kích thước giọt nước điều chỉnh được
        main.gravityModifier = 0.8f; // Trọng lực hút giọt nước rơi thẳng xuống
        main.startColor = dropColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 5;

        // Tần suất nhỏ giọt ngẫu nhiên và thưa thớt (Ambient)
        var emission = dripPS.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(1f / maxDripInterval, 1f / minDripInterval);

        // Dạng phát hạt: Phát từ 1 điểm nhỏ gọn ở đầu nhọn nhũ băng
        var shape = dripPS.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
        shape.radius = 0.01f;
        
        // Tạo Renderer dùng Shader mặc định của Sprite để không bị lỗi Pink Shader
        var psr = dripObj.GetComponent<ParticleSystemRenderer>();
        psr.material = new Material(Shader.Find("Sprites/Default"));

        // 4. Kích hoạt va chạm vật lý (Collision) để giọt nước vỡ/tạo ripple khi chạm đất
        var collision = dripPS.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision2D;
        collision.bounce = 0.1f;
        collision.lifetimeLoss = 1f; // Chạm đất thì biến mất ngay
        collision.collidesWith = LayerMask.GetMask("Default", "Ground", "Water"); // Va chạm với đất và nước
        collision.sendCollisionMessages = false;

        dripPS.Play();
    }
}

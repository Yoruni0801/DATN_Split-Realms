using UnityEngine;
using System.Collections;

public class IceDoorController : MonoBehaviour
{
    private Material mat;
    private Collider2D col;
    private static readonly int DissolveProp = Shader.PropertyToID("_DissolveAmount");

    [Header("Cài đặt Phá Cửa")]
    public float shatterDuration = 0.6f; // Thời gian vỡ
    public float massThreshold = 5f;     // Khối lượng tối thiểu để phá cửa
    
    [Header("Tùy chỉnh: Mảnh Băng Vỡ")]
    public float chunkLifetimeMin = 0.5f;
    public float chunkLifetimeMax = 1.2f;
    public float chunkSpeedMin = 8f;
    public float chunkSpeedMax = 20f;
    public float chunkGravity = 2.0f;
    public int chunkBurstCount = 40;
    
    [Header("Tùy chỉnh: Sương Lạnh")]
    public float mistLifetimeMin = 0.8f;
    public float mistLifetimeMax = 1.5f;
    public float mistSpeedMin = 1f;
    public float mistSpeedMax = 4f;
    public int mistBurstCount = 30;

    // Cờ báo hiệu để tránh va chạm nhiều lần
    private bool isShattering = false; 

    void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) mat = sr.material;
        col = GetComponent<Collider2D>();
    }

    void OnTriggerEnter2D(Collider2D collider)
    {
        if (isShattering) return;

        // KIỂM TRA ĐIỀU KIỆN PHÁ CỬA:
        // Cập nhật: Khối đá băng (có IceRockRoller) đụng vào là vỡ luôn, không cần quan tâm khối lượng nữa.
        Rigidbody2D rb = collider.gameObject.GetComponent<Rigidbody2D>();
        bool isIceRock = collider.gameObject.GetComponent<IceRockRoller>() != null;
        
        if (isIceRock || (rb != null && rb.mass >= massThreshold) || collider.gameObject.name.Contains("tinhthe_cutted"))
        {
            // Lấy tọa độ điểm bị đập trúng để làm tâm vụ nổ (dùng ClosestPoint cho trigger)
            Vector2 hitPoint = collider.ClosestPoint(transform.position);
            StartCoroutine(ShatterRoutine(hitPoint));
            
            // Xử lý nứt vỡ Cục Đá (Fragmentation / Break Apart)
            StartCoroutine(RockShatterRoutine(collider.gameObject, hitPoint));
        }
    }

    IEnumerator ShatterRoutine(Vector2 hitPoint)
    {
        isShattering = true;

        // 1. Lập tức tắt va chạm vật lý để Viên Đá tiếp tục bay xuyên qua
        if (col != null) col.enabled = false;

        // 2. Bắn ra hàng trăm mảnh vụn băng (Particle System)
        SpawnIceExplosion(hitPoint);

        // 3. Hiệu ứng từ từ nứt vỡ rực sáng bằng Shader
        float timer = 0;
        while (timer < shatterDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / shatterDuration;
            
            if (mat != null && mat.HasProperty(DissolveProp))
            {
                // Tăng mức độ vỡ từ 0 đến 1.1 (để chắc chắn cửa rã thành nước)
                mat.SetFloat(DissolveProp, progress * 1.1f);
            }
            yield return null;
        }

        // 4. Xóa vĩnh viễn cánh cửa khỏi trò chơi
        Destroy(gameObject);
    }

    IEnumerator RockShatterRoutine(GameObject rock, Vector2 hitPoint)
    {
        // 1. Dừng khối đá lại và vô hiệu hóa va chạm
        Collider2D rockCol = rock.GetComponent<Collider2D>();
        if (rockCol != null) rockCol.enabled = false;
        
        Rigidbody2D rb = rock.GetComponent<Rigidbody2D>();
        Vector2 currentVelocity = Vector2.zero;
        if (rb != null) 
        {
            currentVelocity = rb.linearVelocity;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // 2. Tắt hiển thị khối đá ngay lập tức để mô phỏng sự vỡ nát tức thì
        SpriteRenderer sr = rock.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        // 3. Tạo hiệu ứng vỡ vụn 2 lớp (Fragmentation + Dust Cloud) cực kỳ mãn nhãn
        SpawnPremiumFragmentation(rock.transform.position, currentVelocity, hitPoint);

        // Chờ hiệu ứng bay xong
        yield return new WaitForSeconds(1.5f);

        // 4. Xóa khối đá
        Destroy(rock);
    }

    // Hàm tự động code Particle System băng
    void SpawnIceExplosion(Vector2 pos)
    {
        GameObject vfx = new GameObject("IceDoorExplosion");
        vfx.transform.position = pos;
        
        ParticleSystem ps = vfx.AddComponent<ParticleSystem>();
        
        // FIX LỖI: Unity tự động Play khi tạo Particle System, phải Stop nó trước mới chỉnh được Duration
        ps.Stop();
        
        // Setup cấu hình mảnh vỡ băng
        var main = ps.main;
        main.duration = 1f;
        main.loop = false; // Không lặp lại
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f); // Rơi và tan nhanh
        main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 25f);      // Lực văng siêu mạnh
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);     // Mảnh vụn to nhỏ
        main.startColor = new Color(0.6f, 0.9f, 1f, 1f);                 // Màu xanh tuyết
        main.gravityModifier = 2.0f;                                     // Bị hút mạnh xuống đất
        
        // 1 phát nổ bắn ra 100 mảnh vụn ngay lập tức, không bắn thêm rải rác
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, 100) }); 
        
        // Nổ dạng bán cầu (văng về phía trước)
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere; 
        shape.radius = 1f;
        
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        
        // Tự dọn rác sau 2 giây
        Destroy(vfx, 2f);
    }

    // Hiệu ứng Break Apart / Fragmentation đẳng cấp
    void SpawnPremiumFragmentation(Vector2 rockPosition, Vector2 rockVelocity, Vector2 hitPoint)
    {
        GameObject vfx = new GameObject("PremiumRockFragmentation");
        vfx.transform.position = rockPosition;
        
        // ==========================================
        // ==========================================
        // LỚP 1: CÁC MẢNH BĂNG VỠ (Bouncing Ice Fragments)
        // ==========================================
        ParticleSystem psChunks = vfx.AddComponent<ParticleSystem>();
        psChunks.Stop();
        
        var main = psChunks.main;
        main.duration = 2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(chunkLifetimeMin, chunkLifetimeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(chunkSpeedMin, chunkSpeedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.8f); 
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        
        // Màu Xanh Lơ (Cyan) sáng chói như tinh thể băng
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.3f, 0.85f, 1f, 1f), 
            new Color(0.6f, 0.95f, 1f, 1f)
        );
        main.gravityModifier = chunkGravity; // Băng nhẹ hơn đá một chút
        
        var emission = psChunks.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, (short)chunkBurstCount) });
        
        var shape = psChunks.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.8f;

        // Kế thừa quán tính của viên đá lúc lao tới
        var inheritVelocity = psChunks.inheritVelocity;
        inheritVelocity.enabled = true;
        inheritVelocity.mode = ParticleSystemInheritVelocityMode.Initial;
        inheritVelocity.curveMultiplier = 0.7f;
        
        // Tương tác vật lý: Nảy trên các bề mặt trong Scene 2D
        var collision = psChunks.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision2D;
        collision.bounce = 0.2f;
        collision.dampen = 0.2f;
        collision.collidesWith = Physics2D.AllLayers;

        // Xoay liên tục khi văng
        var rotOverLifetime = psChunks.rotationOverLifetime;
        rotOverLifetime.enabled = true;
        rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-360f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);

        var renderer = psChunks.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.sortingOrder = 10;

        // ==========================================
        // LỚP 2: KHÓI LẠNH / SƯƠNG BĂNG (Frost Mist)
        // ==========================================
        GameObject dustObj = new GameObject("FrostMist");
        dustObj.transform.SetParent(vfx.transform);
        dustObj.transform.localPosition = Vector3.zero;
        
        ParticleSystem psDust = dustObj.AddComponent<ParticleSystem>();
        psDust.Stop();
        
        var mainDust = psDust.main;
        mainDust.duration = 2f;
        mainDust.loop = false;
        mainDust.startLifetime = new ParticleSystem.MinMaxCurve(mistLifetimeMin, mistLifetimeMax);
        mainDust.startSpeed = new ParticleSystem.MinMaxCurve(mistSpeedMin, mistSpeedMax);
        mainDust.startSize = new ParticleSystem.MinMaxCurve(1.0f, 2.5f);
        
        // Màu trắng pha xanh ngọc lợt của hơi lạnh
        mainDust.startColor = new Color(0.8f, 0.95f, 1f, 0.5f); 
        mainDust.gravityModifier = -0.05f; // Sương lạnh bốc lên nhẹ nhàng
        
        var emissionDust = psDust.emission;
        emissionDust.rateOverTime = 0f;
        emissionDust.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, (short)mistBurstCount) });
        
        var shapeDust = psDust.shape;
        shapeDust.shapeType = ParticleSystemShapeType.Circle;
        shapeDust.radius = 1.2f;
        
        // Sương nở to ra
        var sizeOverLife = psDust.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.3f);
        sizeCurve.AddKey(0.5f, 1f);
        sizeCurve.AddKey(1f, 1.5f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Khói lạnh tan dần
        var colorOverLife = psDust.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLife.color = grad;

        var rendererDust = psDust.GetComponent<ParticleSystemRenderer>();
        rendererDust.material = new Material(Shader.Find("Sprites/Default"));
        rendererDust.sortingOrder = 9;

        psChunks.Play();
        psDust.Play();

        Destroy(vfx, 3f);
    }
}

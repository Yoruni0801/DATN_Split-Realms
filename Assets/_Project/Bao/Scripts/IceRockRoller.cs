using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class IceRockRoller : MonoBehaviour
{
    private Rigidbody2D rb;
    private Collider2D col;
    private Transform player;
    private Rigidbody2D playerRb;
    private SpriteRenderer sr;

    [Header("Rolling Mechanic")]
    public float interactionRange = 4f;
    
    [Tooltip("Cách tính tốc độ to lên: True = theo thời gian lăn (giây), False = theo khoảng cách lăn (mét)")]
    public bool growByTime = false;
    
    [Tooltip("Thời gian (giây) đẩy liên tục để đạt kích thước tối đa (chỉ dùng khi growByTime = true)")]
    public float timeToMaxScale = 15f;
    
    [Tooltip("Tốc độ to lên theo khoảng cách (đơn vị scale / mét) (chỉ dùng khi growByTime = false)")]
    public float growthRate = 0.5f; 
    public float massGrowthRate = 2f;
    public float maxScale = 8f; // Giới hạn to tối đa
    public float maxMass = 20f;
    
    [Header("Khởi tạo")]
    public bool shrinkAtStart = true;
    public float initialScale = 2f;
    public float initialMass = 2f;
    
    [Header("Visual Effects Settings (Tùy chỉnh VFX)")]
    public bool enableRollingDust = true;
    public Color dustColor = new Color(0.85f, 0.95f, 1f, 0.75f);
    public bool enableSnowDebris = true;
    public Color debrisColor = new Color(0.9f, 0.96f, 1f, 0.8f);
    public Color rollingTint = new Color(0.9f, 1f, 1f, 1f);
    public Color completionFlashColor = new Color(0.5f, 1f, 1f, 1f);
    [Range(0f, 3f)]
    public float maxGrowthGlowBoost = 1.5f;

    [Header("Audio Settings (Âm Thanh Lăn & Thả Tuyết)")]
    public bool enableAudio = true;
    [Tooltip("Âm thanh lăn tuyết êm dịu (nhẹ nhàng, không gây ồn)")]
    public AudioClip rollingLoopSound;
    [Tooltip("Âm thanh nhẹ khi thả tay / ngừng đẩy cục tuyết")]
    public AudioClip releaseSound;
    [Tooltip("Âm thanh hoàn thành khi cục tuyết đạt độ to tối đa")]
    public AudioClip completionSound;
    [Range(0f, 1f)] public float rollingAudioVolume = 0.25f;
    [Range(0f, 1f)] public float releaseAudioVolume = 0.4f;
    [Range(0f, 1f)] public float completionAudioVolume = 0.8f;

    private bool isRollingMode = false;
    private bool isComplete = false;
    private Vector2 lastPosition;

    private ParticleSystem rollingVFX;
    private ParticleSystem sparkleVFX;
    private AudioSource rollingAudioSource;
    private MaterialPropertyBlock propBlock;
    private static readonly int GrowthBoostID = Shader.PropertyToID("_GrowthEmissionBoost");
    private static readonly int CoreIntensityID = Shader.PropertyToID("_CoreIntensity");

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) 
        {
            player = p.transform;
            playerRb = p.GetComponent<Rigidbody2D>();
        }
        
        if (shrinkAtStart)
        {
            // Bắt đầu từ 0 để tạo hiệu ứng nặn tuyết
            transform.localScale = Vector3.zero;
            rb.mass = initialMass;
        }

        propBlock = new MaterialPropertyBlock();
        UpdateShaderGlow(0f);
        CreateRollingVFX();
        InitializeAudio();
    }

    private void InitializeAudio()
    {
        if (rollingLoopSound == null)
        {
            #if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Ditrenbang t:AudioClip");
            if (guids.Length == 0) guids = UnityEditor.AssetDatabase.FindAssets("Chaytrenco t:AudioClip");
            if (guids.Length == 0) guids = UnityEditor.AssetDatabase.FindAssets("windloop t:AudioClip");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                rollingLoopSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            #endif
        }

        if (releaseSound == null)
        {
            #if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Chaytrenco t:AudioClip");
            if (guids.Length == 0) guids = UnityEditor.AssetDatabase.FindAssets("sfx_walk t:AudioClip");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                releaseSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            #endif
        }

        if (completionSound == null)
        {
            #if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("sfx_dreadworm_sanctuary_shield_up t:AudioClip");
            if (guids.Length == 0) guids = UnityEditor.AssetDatabase.FindAssets("genericimpact t:AudioClip");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                completionSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            #endif
        }

        if (rollingLoopSound != null)
        {
            rollingAudioSource = gameObject.AddComponent<AudioSource>();
            rollingAudioSource.clip = rollingLoopSound;
            rollingAudioSource.loop = true;
            rollingAudioSource.playOnAwake = false;
            rollingAudioSource.volume = 0f;
            rollingAudioSource.spatialBlend = 0.2f;
            rollingAudioSource.minDistance = 3f;
            rollingAudioSource.maxDistance = 20f;
            rollingAudioSource.rolloffMode = AudioRolloffMode.Linear;

            // Định tuyến trực tiếp vào SFX Mixer Group có sẵn
            if (AudioManager.Instance != null && AudioManager.Instance.mainMixer != null)
            {
                var sfxGroups = AudioManager.Instance.mainMixer.FindMatchingGroups("SFX");
                if (sfxGroups != null && sfxGroups.Length > 0)
                {
                    rollingAudioSource.outputAudioMixerGroup = sfxGroups[0];
                }
            }
        }
    }

    void CreateRollingVFX()
    {
        // 1. Hệ thống bụi tuyết lăn ở chân
        GameObject vfxObj = new GameObject("SnowRollingVFX_" + gameObject.GetInstanceID());
        rollingVFX = vfxObj.AddComponent<ParticleSystem>();
        rollingVFX.Stop();

        var main = rollingVFX.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startColor = dustColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = rollingVFX.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 18f;

        var shape = rollingVFX.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1f, 0.1f, 1f);

        var vel = rollingVFX.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.3f, 1.5f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var size = rollingVFX.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0f);
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = rollingVFX.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.sortingOrder = sr != null ? sr.sortingOrder - 1 : 5;

        rollingVFX.Stop();

        // 2. Hệ thống hạt bông tuyết xốp rơi lại phía sau khi lăn (Fluffy Snow Debris)
        GameObject sparkleObj = new GameObject("SnowDebrisVFX_" + gameObject.GetInstanceID());
        sparkleVFX = sparkleObj.AddComponent<ParticleSystem>();
        sparkleVFX.Stop();

        var sMain = sparkleVFX.main;
        sMain.duration = 1f;
        sMain.loop = true;
        sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        sMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        sMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        sMain.startColor = debrisColor; // Bông tuyết xốp tự nhiên, không neon
        sMain.simulationSpace = ParticleSystemSimulationSpace.World;

        var sEmission = sparkleVFX.emission;
        sEmission.rateOverTime = 0f;
        sEmission.rateOverDistance = 10f; // Chỉ rơi khi lăn di chuyển

        var sShape = sparkleVFX.shape;
        sShape.shapeType = ParticleSystemShapeType.Circle;
        sShape.radius = 0.4f;

        var sVel = sparkleVFX.velocityOverLifetime;
        sVel.enabled = true;
        sVel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        sVel.y = new ParticleSystem.MinMaxCurve(-0.5f, 0.2f); // Rơi nhẹ xuống
        sVel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var sSize = sparkleVFX.sizeOverLifetime;
        sSize.enabled = true;
        AnimationCurve sCurve = new AnimationCurve();
        sCurve.AddKey(0f, 0.8f);
        sCurve.AddKey(1f, 0f);
        sSize.size = new ParticleSystem.MinMaxCurve(1f, sCurve);

        var sRenderer = sparkleVFX.GetComponent<ParticleSystemRenderer>();
        sRenderer.material = new Material(Shader.Find("Sprites/Default"));
        sRenderer.sortingOrder = sr != null ? sr.sortingOrder - 1 : 4; // Nằm sau cục tuyết

        sparkleVFX.Stop();
    }

    void Update()
    {
        if (isComplete || player == null) return;

        // Nếu cục đá bị ẩn đi (do chạm cửa và vỡ), dừng mọi logic
        if (sr != null && !sr.enabled)
        {
            StopRollingAudio(playRelease: false);
            return;
        }

        // Tính khoảng cách từ người chơi đến bề mặt cục đá (thay vì tâm cục đá) để không bị lỗi khi đá to ra
        float distanceToSurface = Vector2.Distance(transform.position, player.position) - col.bounds.extents.x;

        // Buông phím F khi đang lăn để hoàn thiện cục đá luôn (không cần nhấn E nữa)
        if (isRollingMode && Input.GetKeyUp(KeyCode.F))
        {
            StopRollingAudio(playRelease: true);
            CompleteRock();
            return;
        }

        // Nhấn F để tiếp tục đẩy (nếu lỡ tay làm rơi cục tuyết)
        if (!isRollingMode && distanceToSurface <= interactionRange && Input.GetKeyDown(KeyCode.F))
        {
            StartRolling();
        }
    }

    void UpdateShaderGlow(float growthProgress)
    {
        if (sr != null)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            sr.GetPropertyBlock(propBlock);
            propBlock.SetFloat(GrowthBoostID, Mathf.Clamp01(growthProgress) * 1.5f);
            sr.SetPropertyBlock(propBlock);
        }
    }

    public void CompleteRock()
    {
        if (isComplete) return;

        isRollingMode = false;
        isComplete = true; // Chốt kích thước, không to thêm nữa

        StopRollingAudio(playRelease: false);
        PlayCompletionSound();

        if (sr != null) sr.color = Color.white; // Trả lại màu gốc
        if (rollingVFX != null) rollingVFX.Stop(); // Tắt xịt tuyết
        // Sparkle vẫn có thể phát nhẹ hoặc duy trì
        UpdateShaderGlow(1f);

        // Hiệu ứng chớp nháy báo hiệu hoàn thành
        StartCoroutine(CompletionFlash());

        Debug.Log("Hoàn thiện cục đá!");
    }

    private void PlayCompletionSound()
    {
        if (!enableAudio || completionSound == null) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.PlaySFXIfAvailable(completionSound, transform.position);
            return;
        }

        AudioSource.PlayClipAtPoint(completionSound, transform.position, completionAudioVolume);
    }

    private void UpdateRollingAudio(float speed)
    {
        if (!enableAudio || rollingAudioSource == null) return;

        if (speed > 0.1f)
        {
            if (!rollingAudioSource.isPlaying)
            {
                rollingAudioSource.volume = rollingAudioVolume;
                rollingAudioSource.Play();
            }
            float targetVol = Mathf.Lerp(rollingAudioVolume * 0.5f, rollingAudioVolume, Mathf.Clamp01(speed / 6f));
            rollingAudioSource.volume = targetVol;
            rollingAudioSource.pitch = Mathf.Lerp(0.9f, 1.1f, Mathf.Clamp01(speed / 8f));
        }
        else
        {
            // Dừng ngay lập tức khi không di chuyển
            StopRollingAudio(playRelease: false);
        }
    }

    private void StopRollingAudio(bool playRelease = false)
    {
        if (rollingAudioSource != null && rollingAudioSource.isPlaying)
        {
            rollingAudioSource.Stop();
            rollingAudioSource.volume = 0f;

            if (playRelease && releaseSound != null && enableAudio)
            {
                PlayClipAtPoint(releaseSound, transform.position, releaseAudioVolume);
            }
        }
    }

    private void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.PlaySFXIfAvailable(clip, position);
            return;
        }

        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    void FixedUpdate()
    {
        if (isComplete || playerRb == null)
        {
            StopRollingAudio(playRelease: false);
            return;
        }
        if (sr != null && !sr.enabled)
        {
            StopRollingAudio(playRelease: false);
            return;
        }

        // Cơ chế lăn theo người chơi (chỉ diễn ra trong FixedUpdate để mượt mà vật lý)
        if (isRollingMode)
        {
            // Kiểm tra nếu người chơi đi ra quá xa khỏi cục đá thì tự động ngắt đẩy
            float distanceToSurface = Vector2.Distance(transform.position, player.position) - col.bounds.extents.x;
            if (distanceToSurface > interactionRange)
            {
                isRollingMode = false;
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                if (sr != null) sr.color = Color.white; // Trả lại màu gốc
                StopRollingAudio(playRelease: true);
                return;
            }

            float playerSpeedX = playerRb.linearVelocity.x;
            
            // Kiểm tra hướng người chơi và hướng của cục đá so với người chơi
            float dirToRock = Mathf.Sign(transform.position.x - player.position.x);
            float playerMoveDir = Mathf.Sign(playerSpeedX);
            
            // CHỈ di chuyển và to lên nếu người chơi di chuyển đủ nhanh VÀ đi về hướng cục đá (Đang đẩy)
            if (Mathf.Abs(playerSpeedX) > 0.1f && dirToRock == playerMoveDir)
            {
                // Cục đá di chuyển theo đúng vận tốc ngang của người chơi
                rb.linearVelocity = new Vector2(playerSpeedX, rb.linearVelocity.y);

                UpdateRollingAudio(Mathf.Abs(playerSpeedX));

                float distMoved = Mathf.Abs(playerSpeedX) * Time.fixedDeltaTime;

                // Xoay cục đá (Lăn)
                float rotateAmount = -playerSpeedX * 50f * Time.fixedDeltaTime;
                transform.Rotate(0, 0, rotateAmount);

                // Tăng kích thước
                float currentScale = transform.localScale.x;
                if (currentScale < maxScale)
                {
                    float scaleIncrease = 0f;
                    float massIncrease = 0f;
                    
                    if (growByTime)
                    {
                        // Tăng trưởng theo thời gian đẩy: chia đều lượng tăng từ initialScale lên maxScale
                        float totalScaleGrowth = maxScale - initialScale;
                        scaleIncrease = (totalScaleGrowth / timeToMaxScale) * Time.fixedDeltaTime;
                        
                        float totalMassGrowth = maxMass - initialMass;
                        massIncrease = (totalMassGrowth / timeToMaxScale) * Time.fixedDeltaTime;
                    }
                    else
                    {
                        // Tăng trưởng theo quãng đường người chơi đẩy
                        scaleIncrease = growthRate * distMoved;
                        massIncrease = massGrowthRate * distMoved;
                    }

                    float newScale = currentScale + scaleIncrease;
                    newScale = Mathf.Min(newScale, maxScale);
                    transform.localScale = new Vector3(newScale, newScale, newScale);

                    // Khối lượng tỉ lệ thuận với kích thước
                    rb.mass += massIncrease;
                    rb.mass = Mathf.Min(rb.mass, maxMass);

                    // Cập nhật shader glow tăng theo kích thước
                    float progress = Mathf.InverseLerp(initialScale, maxScale, newScale);
                    UpdateShaderGlow(progress);

                    // Nếu đã đạt max scale thì tự động chốt luôn
                    if (newScale >= maxScale)
                    {
                        CompleteRock();
                    }
                }
            }
            else
            {
                // Người chơi buông ra hoặc đứng im -> TẮT ÂM THANH NGAY LẬP TỨC!
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                StopRollingAudio(playRelease: true);
            }
        }
        else
        {
            StopRollingAudio(playRelease: false);
        }
    }

    // Hàm này được gọi từ SnowZone khi vừa tạo ra cục tuyết mới
    public void StartRolling()
    {
        isRollingMode = true;
        Debug.Log("Bắt đầu lăn tạo tuyết (StartRolling)");
        if (sr != null) sr.color = rollingTint;

        // Kích hoạt hiệu ứng nặn tuyết
        StartCoroutine(SpawnScaleRoutine());
    }

    IEnumerator SpawnScaleRoutine()
    {
        float timer = 0;
        float duration = 0.4f; // Nặn nhanh trong 0.4s
        Vector3 targetScale = new Vector3(initialScale, initialScale, initialScale);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, timer / duration);
            yield return null;
        }
        transform.localScale = targetScale;

        if (enableRollingDust && rollingVFX != null) rollingVFX.Play(); // Bật vfx sau khi nặn xong
        if (enableSnowDebris && sparkleVFX != null) sparkleVFX.Play(); // Bật sparkle vfx
    }

    void LateUpdate()
    {
        // Luôn giữ VFX ở dưới chân cục đá và scale chiều ngang theo cục đá
        if (rollingVFX != null && col != null)
        {
            rollingVFX.transform.position = transform.position + new Vector3(0, -col.bounds.extents.y, 0);

            var shape = rollingVFX.shape;
            shape.scale = new Vector3(transform.localScale.x * 0.8f, 0.1f, 1f); // Rộng ra khi đá to lên
        }

        // Sparkle đi theo tâm của cục đá và bán kính tỏa theo kích thước
        if (sparkleVFX != null)
        {
            sparkleVFX.transform.position = transform.position;

            var sShape = sparkleVFX.shape;
            sShape.radius = Mathf.Max(0.2f, transform.localScale.x * 0.5f);
        }
    }

    void OnDestroy()
    {
        StopRollingAudio(playRelease: false);

        // Dọn dẹp VFX khi cục đá bị phá hủy
        if (rollingVFX != null)
        {
            Destroy(rollingVFX.gameObject);
        }
        if (sparkleVFX != null)
        {
            Destroy(sparkleVFX.gameObject);
        }
    }

    void OnDisable()
    {
        StopRollingAudio(playRelease: false);
    }

    IEnumerator CompletionFlash()
    {
        if (sr != null)
        {
            Color origColor = sr.color;
            Color flashColor = completionFlashColor; // Chớp sáng theo cấu hình Inspector

            sr.color = flashColor;
            yield return new WaitForSeconds(0.1f);
            sr.color = origColor;
            yield return new WaitForSeconds(0.1f);
            sr.color = flashColor;
            yield return new WaitForSeconds(0.1f);
            sr.color = origColor;
        }
    }
}

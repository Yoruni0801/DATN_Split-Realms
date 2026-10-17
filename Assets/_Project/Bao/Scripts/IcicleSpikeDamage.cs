using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gai bang (nhũ băng nhọn).
/// 1. Gay sat thuong khi Player cham / roi xuong.
/// 2. Hất Player ve be an toan (Safe Respawn / Hazard Teleport) - Co the tu chinh Transform customRespawnPoint trong Inspector.
/// 3. Chop nhe man hinh (Screen Flash) va rung camera.
/// 4. Tao hieu ung Suong Lanh Boc Len (Frost Mist Particles) sieu dep huyen ao.
/// KHONG can sua hay can thiep vao bat ky script nao cua Player.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IcicleSpikeDamage : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Sat thuong gay ra khi Player cham vao gai.")]
    [SerializeField] private int damage = 15;

    [Tooltip("Neu bat, giet Player ngay lap tuc nhu roi vuc (KillByFall).")]
    [SerializeField] private bool instantKill = false;

    [Header("Safe Respawn (Hazard Teleport)")]
    [Tooltip("Neu bat, khi cham gai Player se duoc dua ve be an toan.")]
    [SerializeField] private bool safeRespawn = true;

    [Tooltip("Diem respawn an toan tuy chinh (Keo Transform bat ky trong Scene vao day). Neu de NULL, script se TU DONG dung vi tri dung an toan gan nhat cua Player.")]
    [SerializeField] private Transform customRespawnPoint;

    [Header("Screen Flash Effect")]
    [Tooltip("Neu bat, man hinh se chot nhe mau lanh/do khi dính đòn.")]
    [SerializeField] private bool enableScreenFlash = true;

    [Tooltip("Mau sac chot man hinh.")]
    [SerializeField] private Color flashColor = new Color(0.75f, 0.92f, 1.0f, 0.6f);

    [Tooltip("Thoi gian chot man hinh (giay).")]
    [SerializeField] private float flashDuration = 0.25f;

    [Header("Frost Mist Visual (Suong Lanh)")]
    [Tooltip("Neu bat, tu dong tao hieu ung suong lanh boc len tu thảm gai.")]
    [SerializeField] private bool enableFrostMist = true;

    [Tooltip("Mat do suong lanh (Ty le so hat suong).")]
    [SerializeField] private float mistDensityMultiplier = 1.0f;

    [Header("Hit Cooldown")]
    [Tooltip("Thoi gian nghi giua 2 lan gay sat thuong (tranh spam damage).")]
    [SerializeField] private float hitCooldown = 0.6f;

    private float lastHitTime = -999f;

    private void Awake()
    {
        // Dam bao Collider la Trigger
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }

        HazardSafeTracker.EnsureExists();
    }

    private void Start()
    {
        if (enableFrostMist)
        {
            CreateFrostMistParticles();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryDamage(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryDamage(collision);
    }

    private void TryDamage(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;
        if (Time.time - lastHitTime < hitCooldown) return;

        PlayerHealth playerHealth = collision.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null || playerHealth.IsDead) return;

        // Neu dang trong i-frame (IsHurt), khong tinh hit de tranh trung phat 2 lan
        if (playerHealth.IsHurt) return;

        lastHitTime = Time.time;

        if (instantKill)
        {
            playerHealth.KillByFall();
            return;
        }

        // 1. Trừ máu Player qua PlayerHealth
        playerHealth.TakeDamage(damage, transform);

        // 2. Chớp nhẹ màn hình & Rung Camera
        if (enableScreenFlash)
        {
            TriggerScreenFlash(flashColor, flashDuration);
        }

        if (CombatFeedbackManager.Instance != null)
        {
            CombatFeedbackManager.Instance.PlayPlayerHurtFeedback();
        }

        // 3. Safe Respawn: Đưa Player về bệ an toàn (Custom hoặc Auto)
        if (safeRespawn && !playerHealth.IsDead)
        {
            Transform playerTr = playerHealth.transform;
            Vector3 targetRespawnPos = GetRespawnPosition(playerTr);

            // Dich chuyển Player
            playerTr.position = targetRespawnPos;

            // Reset van toc va toc do roi de Player khong bi vang tiep xuong gai
            Rigidbody2D playerRb = playerHealth.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// Lay vi tri respawn: uu tien Custom Transform trong Inspector, neu bang NULL thi dung Tracker tu dong
    /// </summary>
    public Vector3 GetRespawnPosition(Transform playerTr)
    {
        if (customRespawnPoint != null)
        {
            return customRespawnPoint.position;
        }

        return HazardSafeTracker.GetSafePosition(playerTr);
    }

    #region Screen Flash Implementation
    private static Canvas flashCanvas;
    private static Image flashImage;

    private static void TriggerScreenFlash(Color color, float duration)
    {
        if (flashCanvas == null)
        {
            GameObject canvasObj = new GameObject("[HazardScreenFlashCanvas]");
            flashCanvas = canvasObj.AddComponent<Canvas>();
            flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            flashCanvas.sortingOrder = 9999;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            GameObject imgObj = new GameObject("FlashOverlay");
            imgObj.transform.SetParent(canvasObj.transform, false);
            flashImage = imgObj.AddComponent<Image>();
            flashImage.rectTransform.anchorMin = Vector2.zero;
            flashImage.rectTransform.anchorMax = Vector2.one;
            flashImage.rectTransform.sizeDelta = Vector2.zero;
            flashImage.raycastTarget = false;

            DontDestroyOnLoad(canvasObj);
        }

        flashCanvas.gameObject.SetActive(true);
        IcicleSpikeDamageRunner.RunCoroutine(DoFlashRoutine(color, duration));
    }

    private static IEnumerator DoFlashRoutine(Color color, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float alpha = Mathf.Lerp(color.a, 0f, t);
            if (flashImage != null)
            {
                flashImage.color = new Color(color.r, color.g, color.b, alpha);
            }
            yield return null;
        }

        if (flashImage != null)
        {
            flashImage.color = new Color(color.r, color.g, color.b, 0f);
        }
    }
    #endregion

    #region Frost Mist VFX Generator
    private void CreateFrostMistParticles()
    {
        Transform existing = transform.Find("FrostMistEmitter");
        if (existing != null) return;

        Bounds bounds = GetSpikeBounds();

        GameObject mistObj = new GameObject("FrostMistEmitter");
        mistObj.transform.SetParent(transform, false);
        mistObj.transform.position = new Vector3(bounds.center.x, bounds.min.y + 0.1f, transform.position.z);

        ParticleSystem ps = mistObj.AddComponent<ParticleSystem>();
        ps.Stop();

        var main = ps.main;
        main.duration = 4f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.Deg2Rad * 360f);
        main.gravityModifier = -0.015f; // Bay bồng bềnh nhẹ lên trên
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        main.startColor = new Color(0.8f, 0.95f, 1.0f, 0.35f);

        // Ty le hat phat ra dua tren chieu roi rong cua dai gai bang
        float width = Mathf.Max(bounds.size.x, 1.2f);
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = width * 5f * mistDensityMultiplier;

        // Shape dạng Box theo chiều ngang thảm gai
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, 0.2f, 1f);

        // Color Over Lifetime (Nở dần mờ mượt -> Tan biến)
        var colLifetime = ps.colorOverLifetime;
        colLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.9f, 0.97f, 1.0f), 0f),
                new GradientColorKey(new Color(0.6f, 0.88f, 1.0f), 0.5f),
                new GradientColorKey(new Color(0.4f, 0.75f, 0.95f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.45f, 0.2f),
                new GradientAlphaKey(0.35f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colLifetime.color = grad;

        // Size Over Lifetime (Suong lan rong ra khi bốc len)
        var sizeLifetime = ps.sizeOverLifetime;
        sizeLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.4f);
        sizeCurve.AddKey(0.4f, 0.9f);
        sizeCurve.AddKey(1f, 1.5f);
        sizeLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Translucent Soft Noise (Xoáy mây sương tự nhiên)
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.15f;

        // Renderer voi Shader Sprites/Default de khong bi loi hong (Pink Shader)
        ParticleSystemRenderer psr = mistObj.GetComponent<ParticleSystemRenderer>();
        psr.material = new Material(Shader.Find("Sprites/Default"));
        
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        psr.sortingOrder = sr != null ? sr.sortingOrder + 1 : 10;

        ps.Play();
    }

    private Bounds GetSpikeBounds()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) return sr.bounds;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) return col.bounds;

        // Neu gom nhieu child (danh sach gai con)
        SpriteRenderer[] childSrs = GetComponentsInChildren<SpriteRenderer>();
        if (childSrs != null && childSrs.Length > 0)
        {
            Bounds b = childSrs[0].bounds;
            for (int i = 1; i < childSrs.Length; i++)
            {
                b.Encapsulate(childSrs[i].bounds);
            }
            return b;
        }

        return new Bounds(transform.position, new Vector3(2f, 1f, 1f));
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        if (customRespawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(customRespawnPoint.position, 0.5f);
            Gizmos.DrawLine(transform.position, customRespawnPoint.position);
        }
    }
}

/// <summary>
/// Helper runner static coroutines cho IcicleSpikeDamage
/// </summary>
public class IcicleSpikeDamageRunner : MonoBehaviour
{
    private static IcicleSpikeDamageRunner instance;

    public static void RunCoroutine(IEnumerator routine)
    {
        if (instance == null)
        {
            GameObject obj = new GameObject("[IcicleSpikeDamageRunner]");
            instance = obj.AddComponent<IcicleSpikeDamageRunner>();
            DontDestroyOnLoad(obj);
        }
        instance.StartCoroutine(routine);
    }
}

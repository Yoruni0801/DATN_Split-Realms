using System.Collections;
using UnityEngine;

public class SoundPillar : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("Âm thanh duy nhất của cột này")]
    public AudioClip sound;
    [Range(0f, 3f)]
    [Tooltip("Hệ số điều chỉnh âm lượng cho đồng đều")]
    public float volumeMultiplier = 1f;
    [Tooltip("Thời gian vang của nốt nhạc (giây)")]
    public float noteDuration = 1.0f; 
    [Tooltip("Thời gian fade out để tắt mượt (giây)")]
    public float fadeOutTime = 0.2f;

    private AudioSource audioSource;
    private Coroutine audioCoroutine;

    [Header("Visuals")]
    public SpriteRenderer pillarRenderer;
    [ColorUsage(true, true)] // Enables HDR color picker
    public Color glowColor = Color.cyan;
    private Material pillarMaterial;
    private Color basePillarColor;
    private Coroutine glowCoroutine;
    private Coroutine wrongNoteCoroutine;
    private float ignorePlayerContactsUntil;

    [Header("Game Logic")]
    public int pillarID;
    [Tooltip("Vị trí NPC đáp xuống so với tâm cột khi MelodyManager phát bài mẫu.")]
    public Vector3 npcLandingOffset = new Vector3(0f, 1.1f, 0f);

    // Event báo cho Manager biết cột nào vừa được nhảy vào
    public delegate void PillarActivated(int p_pillarID);
    public static event PillarActivated OnPillarActivated;

    [Header("Note Visual Effect")]
    [Tooltip("Sprite nốt nhạc sẽ bay lên")]
    public Sprite noteSprite;
    [Tooltip("Vị trí nốt nhạc bay ra so với chân cột")]
    public Vector3 spawnOffset = new Vector3(0, 1f, 0); // Hạ thấp độ cao spawn nốt nhạc

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        
        if (pillarRenderer != null)
        {
            // Tạo một bản sao của material để làm sáng từng cột độc lập
            pillarMaterial = pillarRenderer.material;
            basePillarColor = pillarRenderer.color;
        }
    }

    public void PlaySound(Color? customNoteColor = null)
    {
        if (sound != null)
        {
            // Sử dụng PlayOneShot để các nốt nhạc có thể vang lên đè lên nhau (overlap) một cách tự nhiên
            // Tránh việc nốt sau cắt ngang nốt trước gây ra tiếng "khựng khựng"
            audioSource.PlayOneShot(sound, volumeMultiplier);
        }
        TriggerGlowEffect();
        SpawnNoteEffect(customNoteColor);
    }

    public void TriggerWrongNoteFeedback(Color feedbackColor, float duration = 0.42f)
    {
        if (wrongNoteCoroutine != null) StopCoroutine(wrongNoteCoroutine);
        if (glowCoroutine != null) StopCoroutine(glowCoroutine);

        Color gentlePurple = Color.Lerp(feedbackColor, new Color(0.64f, 0.30f, 0.88f, 1f), 0.85f);
        SetEmissionColor(gentlePurple * 1.7f);
        if (pillarRenderer != null)
            pillarRenderer.color = Color.Lerp(basePillarColor, gentlePurple, 0.55f);

        wrongNoteCoroutine = StartCoroutine(WrongNoteRoutine(gentlePurple, duration));
    }
    private IEnumerator WrongNoteRoutine(Color feedbackColor, float duration)
    {
        if (glowCoroutine != null) StopCoroutine(glowCoroutine);

        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float flash = Mathf.Sin(t * Mathf.PI);
            SetEmissionColor(feedbackColor * (flash * 2.4f));
            if (pillarRenderer != null)
                pillarRenderer.color = Color.Lerp(basePillarColor, feedbackColor, flash * 0.48f);
            yield return null;
        }

        SetGlowIntensity(0f);
        if (pillarRenderer != null) pillarRenderer.color = basePillarColor;
        wrongNoteCoroutine = null;
    }
    public void TriggerFinalePulse()
    {
        TriggerGlowEffect();
    }
    public void IgnorePlayerContactsFor(float duration)
    {
        ignorePlayerContactsUntil = Mathf.Max(ignorePlayerContactsUntil, Time.time + Mathf.Max(0f, duration));
    }

    public Vector3 GetNpcLandingPosition()
    {
        return transform.position + npcLandingOffset;
    }

    private void SpawnNoteEffect(Color? customNoteColor = null)
    {
        if (noteSprite == null) return;

        // Tạo 1 GameObject tạm thời để chứa nốt nhạc
        GameObject noteGO = new GameObject("NoteEffect");
        noteGO.transform.position = transform.position + spawnOffset;

        SpriteRenderer sr = noteGO.AddComponent<SpriteRenderer>();
        sr.sprite = noteSprite;
        // Lấy màu glow hiện tại gán cho nốt nhạc (hoặc màu chỉ định)
        Color noteColor = customNoteColor ?? new Color(glowColor.r, glowColor.g, glowColor.b, 1f);
        sr.color = noteColor;

        // Xoay nhẹ nốt nhạc để nhìn tự nhiên hơn
        float randomAngle = Random.Range(-15f, 15f);
        noteGO.transform.rotation = Quaternion.Euler(0, 0, randomAngle);

        StartCoroutine(AnimateNote(noteGO, sr));
    }

    private IEnumerator AnimateNote(GameObject noteGO, SpriteRenderer sr)
    {
        float duration = 1.5f;
        float elapsed = 0f;
        Vector3 startPos = noteGO.transform.position;
        // Nốt nhạc bay thẳng cao lên thêm 3 đơn vị
        Vector3 endPos = startPos + new Vector3(0, 3f, 0); 
        
        Vector3 startScale = Vector3.zero;
        Vector3 peakScale = Vector3.one * 1.3f; // Phóng to ra một chút khi vừa nảy lên
        Vector3 endScale = Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Hiệu ứng di chuyển chậm dần ở điểm cuối (Ease Out)
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            
            noteGO.transform.position = Vector3.Lerp(startPos, endPos, easeOut);
            
            // Hiệu ứng Pop-up (Phóng to nhanh rồi thu lại kích thước thật)
            if (t < 0.15f) {
                noteGO.transform.localScale = Vector3.Lerp(startScale, peakScale, t / 0.15f);
            } else {
                noteGO.transform.localScale = Vector3.Lerp(peakScale, endScale, (t - 0.15f) / 0.85f);
            }

            // Hiệu ứng mờ dần (Fade Out) ở nửa sau của animation
            if (t > 0.5f) {
                float alpha = Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }

            yield return null;
        }

        Destroy(noteGO);
    }


    // Xử lý va chạm 2D (khi người chơi hoặc NPC nhảy chạm vào)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("NPC"))
        {
            PlaySound(Color.white);
        }
        else if (Time.time >= ignorePlayerContactsUntil && collision.gameObject.CompareTag("Player"))
        {
            InteractPillar();
        }
    }

    // Xử lý trigger 2D (phòng trường hợp Collider của bạn là dạng Trigger)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("NPC"))
        {
            PlaySound(Color.white);
        }
        else if (Time.time >= ignorePlayerContactsUntil && collision.gameObject.CompareTag("Player"))
        {
            InteractPillar();
        }
    }

    public void InteractPillar()
    {
        // Nhảy vào thì phát âm thanh và sáng chớp (giống phím đàn)
        PlaySound();

        // Báo cho Manager kiểm tra
        OnPillarActivated?.Invoke(pillarID);
    }

    public void TriggerGlowEffect()
    {
        if (glowCoroutine != null) StopCoroutine(glowCoroutine);
        glowCoroutine = StartCoroutine(GlowRoutine());
    }

    private IEnumerator GlowRoutine()
    {
        // A sharp musical attack followed by a softer shimmer makes each pillar feel alive.
        const float duration = 0.62f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, t);
            float ripple = Mathf.Sin(t * Mathf.PI * 3.2f) * 0.34f * fade;
            float intensity = Mathf.Max(0f, 4.2f * fade + ripple);
            SetGlowIntensity(intensity);

            if (pillarRenderer != null)
            {
                float whiteFlash = Mathf.Clamp01(intensity * 0.18f);
                pillarRenderer.color = Color.Lerp(basePillarColor, Color.white, whiteFlash);
            }

            yield return null;
        }

        SetGlowIntensity(0f);
        if (pillarRenderer != null) pillarRenderer.color = basePillarColor;
    }
    private void SetEmissionColor(Color color)
    {
        if (pillarMaterial != null && pillarMaterial.HasProperty("_EmissionColor"))
            pillarMaterial.SetColor("_EmissionColor", color);
    }
    private void SetGlowIntensity(float intensity)
    {
        if (pillarMaterial == null) return;

        if (pillarMaterial.HasProperty("_EmissionColor"))
            SetEmissionColor(glowColor * intensity);

        if (pillarMaterial.HasProperty("_GlowIntensity"))
            pillarMaterial.SetFloat("_GlowIntensity", intensity);
    }

}

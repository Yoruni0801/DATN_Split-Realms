using System.Collections;
using UnityEngine;

/// <summary>
/// Quản lý toàn bộ hiệu ứng âm thanh chuẩn Studio cho Tinh linh (Fairy):
/// - Tách biệt 3 AudioSource độc lập:
///   1. flyAudioSource: Tiếng bay lơ lửng êm ái nền (gió nhẹ và hào quang ma thuật).
///   2. elementAudioSource: Tiếng nguyên tố riêng biệt (Lửa cháy bập bùng nổ lách tách / Nước suối chảy róc rách).
///   3. sfxAudioSource: Hiệu ứng đập cánh khi di chuyển, đổi Tinh linh phím [F], chết, hồi sinh.
/// - Hoàn toàn không bẻ méo cao độ (pitch cố định 1.0f) giúp âm thanh luôn trong trẻo và chân thực.
/// - Tự động tắt/Fade out toàn bộ âm thanh khi xong minigame hoặc khi trả quyền điều khiển về cho Player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(FairyPlayerCompanionController))]
public class FairyAudioController : MonoBehaviour
{
    [Header("=== AUDIO SOURCES ===")]
    [Tooltip("AudioSource phát tiếng bay lơ lửng nền")]
    [SerializeField] private AudioSource flyAudioSource;
    [Tooltip("AudioSource phát tiếng nguyên tố riêng (Lửa hoặc Nước)")]
    [SerializeField] private AudioSource elementAudioSource;
    [Tooltip("AudioSource phát các hiệu ứng âm thanh tức thời (SFX)")]
    [SerializeField] private AudioSource sfxAudioSource;

    [Header("=== PLAY CONTROL ===")]
    [Tooltip("Chỉ phát âm thanh khi đang được điều khiển trong minigame")]
    [SerializeField] private bool playLoopOnlyWhenPossessed = true;

    [Header("=== FLYING SOUNDS ===")]
    [Tooltip("Âm thanh bay lơ lửng ma thuật êm ái của Tinh linh (Loop)")]
    [SerializeField] private AudioClip flyLoopClip;
    [Range(0f, 1f)]
    [SerializeField] private float flyVolume = 0.25f;

    [Tooltip("Âm thanh đập cánh nhẹ khi di chuyển")]
    [SerializeField] private AudioClip wingFlutterClip;
    [Range(0f, 1f)]
    [SerializeField] private float wingVolume = 0.35f;
    [SerializeField] private float minWingInterval = 0.4f;

    [Header("=== ELEMENTAL SOUNDS (LỬA & NƯỚC) ===")]
    [Tooltip("Âm thanh ngọn lửa bùng cháy & nổ lách tách của Tinh linh Lửa (Loop)")]
    [SerializeField] private AudioClip fireAmbientClip;
    [Range(0f, 1f)]
    [SerializeField] private float fireVolume = 0.32f;

    [Tooltip("Âm thanh nước chảy róc rách & bọt nước của Tinh linh Nước (Loop)")]
    [SerializeField] private AudioClip waterAmbientClip;
    [Range(0f, 1f)]
    [SerializeField] private float waterVolume = 0.35f;

    [Header("=== ELEMENT SELECTION ===")]
    [Tooltip("Bật nếu muốn chọn thủ công hệ nguyên tố trên Inspector")]
    [SerializeField] private bool manualElementOverride = false;
    [SerializeField] private FairyElement manualElement = FairyElement.Water;

    [Header("=== ONE-SHOT SFX ===")]
    [Tooltip("Âm thanh đàn Harp ma thuật khi bấm phím [F] đổi sang Tinh linh này")]
    [SerializeField] private AudioClip possessSwitchClip;
    [Tooltip("Âm thanh khi Tinh linh chạm bẫy lửa tử trận")]
    [SerializeField] private AudioClip deathClip;
    [Tooltip("Âm thanh chuông tiên khi Tinh linh hồi sinh")]
    [SerializeField] private AudioClip respawnClip;

    [Header("=== 3D SPATIAL SETTINGS ===")]
    [Range(0f, 1f)]
    [SerializeField] private float spatialBlend = 0.3f;
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 30f;

    private FairyPlayerCompanionController fairy;
    private Rigidbody2D rb;
    private Vector3 lastPosition;
    private float lastWingTime;
    private Coroutine fadeCoroutine;
    private bool isShuttingDown;

    private void Awake()
    {
        fairy = GetComponent<FairyPlayerCompanionController>();
        rb = GetComponent<Rigidbody2D>();
        lastPosition = transform.position;

        EnsureAudioSources();
#if UNITY_EDITOR
        AutoAssignClips();
#endif
    }

#if UNITY_EDITOR
    private void Reset()
    {
        EnsureAudioSources();
        AutoAssignClips();
    }

    private void OnValidate()
    {
        EnsureAudioSources();
        AutoAssignClips();
    }

    private void AutoAssignClips()
    {
        string baseDir = "Assets/_Project/Duy/Audio/Fairy";
        if (flyLoopClip == null)
            flyLoopClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{baseDir}/fairy_fly_loop.wav");
        if (wingFlutterClip == null)
            wingFlutterClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{baseDir}/fairy_wing_flutter.wav");
        if (fireAmbientClip == null)
            fireAmbientClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{baseDir}/fairy_fire_ambient.wav");
        if (waterAmbientClip == null)
            waterAmbientClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{baseDir}/fairy_water_ambient.wav");
        if (possessSwitchClip == null)
            possessSwitchClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{baseDir}/fairy_swap_switch.wav");
        if (deathClip == null)
            deathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{baseDir}/fairy_death_sizzle.wav");
        if (respawnClip == null)
            respawnClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{baseDir}/fairy_respawn_chime.wav");
    }
#endif

    private void OnEnable()
    {
        isShuttingDown = false;

        if (fairy != null)
        {
            fairy.OnFairyElementChanged += HandleElementChanged;
            fairy.OnFairyDied += HandleFairyDied;
        }

        UpdateAmbientLoops();
    }

    private void OnDisable()
    {
        if (fairy != null)
        {
            fairy.OnFairyElementChanged -= HandleElementChanged;
            fairy.OnFairyDied -= HandleFairyDied;
        }

        StopLoopSounds();
    }

    private void Update()
    {
        if (isShuttingDown) return;

        bool isPossessed = fairy != null && fairy.IsPossessed;

        if (playLoopOnlyWhenPossessed && !isPossessed)
        {
            if (IsAnyLoopPlaying())
            {
                StopLoopSounds();
            }
            return;
        }

        if (!IsAnyLoopPlaying())
        {
            UpdateAmbientLoops();
        }

        // Tính toán vận tốc để phát tiếng đập cánh nhẹ tự nhiên khi di chuyển
        float speed = 0f;
        if (rb != null)
        {
            speed = rb.linearVelocity.magnitude;
        }
        else
        {
            float dist = Vector3.Distance(transform.position, lastPosition);
            speed = Time.deltaTime > 0f ? (dist / Time.deltaTime) : 0f;
        }
        lastPosition = transform.position;

        if (speed > 2.5f && Time.time - lastWingTime >= minWingInterval)
        {
            PlayWingFlutterSound();
            lastWingTime = Time.time;
        }
    }

    private void EnsureAudioSources()
    {
        if (flyAudioSource == null)
        {
            flyAudioSource = gameObject.AddComponent<AudioSource>();
            flyAudioSource.playOnAwake = false;
            flyAudioSource.loop = true;
            flyAudioSource.spatialBlend = spatialBlend;
            flyAudioSource.minDistance = minDistance;
            flyAudioSource.maxDistance = maxDistance;
        }

        if (elementAudioSource == null)
        {
            elementAudioSource = gameObject.AddComponent<AudioSource>();
            elementAudioSource.playOnAwake = false;
            elementAudioSource.loop = true;
            elementAudioSource.spatialBlend = spatialBlend;
            elementAudioSource.minDistance = minDistance;
            elementAudioSource.maxDistance = maxDistance;
        }

        if (sfxAudioSource == null)
        {
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
            sfxAudioSource.playOnAwake = false;
            sfxAudioSource.loop = false;
            sfxAudioSource.spatialBlend = spatialBlend;
            sfxAudioSource.minDistance = minDistance;
            sfxAudioSource.maxDistance = maxDistance;
        }
    }

    public void UpdateAmbientLoops()
    {
        if (isShuttingDown) return;

        bool isPossessed = fairy != null && fairy.IsPossessed;
        if (playLoopOnlyWhenPossessed && !isPossessed)
        {
            StopLoopSounds();
            return;
        }

        // 1. Âm thanh bay nền lơ lửng êm ái
        if (flyAudioSource != null && flyLoopClip != null)
        {
            if (flyAudioSource.clip != flyLoopClip || !flyAudioSource.isPlaying)
            {
                flyAudioSource.clip = flyLoopClip;
                flyAudioSource.volume = flyVolume;
                flyAudioSource.pitch = 1.0f;
                flyAudioSource.loop = true;
                flyAudioSource.Play();
            }
        }

        // 2. Âm thanh nguyên tố riêng biệt (Lửa / Nước)
        if (elementAudioSource != null)
        {
            AudioClip targetElementClip = null;
            float targetElementVol = 0.35f;

            FairyElement effectiveElement = ResolveEffectiveElement();

            if (effectiveElement == FairyElement.Fire)
            {
                targetElementClip = fireAmbientClip;
                targetElementVol = fireVolume;
            }
            else if (effectiveElement == FairyElement.Water)
            {
                targetElementClip = waterAmbientClip;
                targetElementVol = waterVolume;
            }

            if (targetElementClip != null)
            {
                if (elementAudioSource.clip != targetElementClip || !elementAudioSource.isPlaying)
                {
                    elementAudioSource.clip = targetElementClip;
                    elementAudioSource.volume = targetElementVol;
                    elementAudioSource.pitch = 1.0f;
                    elementAudioSource.loop = true;
                    elementAudioSource.Play();
                }
            }
            else
            {
                elementAudioSource.Stop();
            }
        }
    }

    public FairyElement ResolveEffectiveElement()
    {
        if (manualElementOverride) return manualElement;

        string n = gameObject.name.ToLowerInvariant();
        if (n.Contains("lua") || n.Contains("fire"))
        {
            return FairyElement.Fire;
        }
        if (n.Contains("nuoc") || n.Contains("water"))
        {
            return FairyElement.Water;
        }

        if (fairy != null)
        {
            return fairy.CurrentElement;
        }

        return FairyElement.Water;
    }

    private bool IsAnyLoopPlaying()
    {
        return (flyAudioSource != null && flyAudioSource.isPlaying)
            || (elementAudioSource != null && elementAudioSource.isPlaying);
    }

    public void StopLoopSounds()
    {
        if (flyAudioSource != null && flyAudioSource.isPlaying)
        {
            flyAudioSource.Stop();
        }

        if (elementAudioSource != null && elementAudioSource.isPlaying)
        {
            elementAudioSource.Stop();
        }
    }

    public void PlayWingFlutterSound()
    {
        if (wingFlutterClip != null && sfxAudioSource != null)
        {
            sfxAudioSource.pitch = 1.0f;
            sfxAudioSource.PlayOneShot(wingFlutterClip, wingVolume);
        }
    }

    public void PlayPossessSwitchSound()
    {
        if (possessSwitchClip != null && sfxAudioSource != null)
        {
            sfxAudioSource.pitch = 1.0f;
            sfxAudioSource.PlayOneShot(possessSwitchClip, 0.75f);
        }
    }

    public void PlayDeathSound()
    {
        StopLoopSounds();
        if (deathClip != null && sfxAudioSource != null)
        {
            sfxAudioSource.pitch = 1.0f;
            sfxAudioSource.PlayOneShot(deathClip, 0.8f);
        }
    }

    public void PlayRespawnSound()
    {
        if (respawnClip != null && sfxAudioSource != null)
        {
            sfxAudioSource.pitch = 1.0f;
            sfxAudioSource.PlayOneShot(respawnClip, 0.8f);
        }
        UpdateAmbientLoops();
    }

    public void FadeOutAndStop(float duration = 0.5f)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        isShuttingDown = true;
        float elapsed = 0f;
        float startFlyVol = flyAudioSource != null ? flyAudioSource.volume : 0f;
        float startElemVol = elementAudioSource != null ? elementAudioSource.volume : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / duration);

            if (flyAudioSource != null)
                flyAudioSource.volume = startFlyVol * t;

            if (elementAudioSource != null)
                elementAudioSource.volume = startElemVol * t;

            yield return null;
        }

        StopLoopSounds();
        isShuttingDown = false;
        fadeCoroutine = null;
    }

    private void HandleElementChanged(FairyElement newElement)
    {
        UpdateAmbientLoops();
    }

    private void HandleFairyDied()
    {
        PlayDeathSound();
    }
}

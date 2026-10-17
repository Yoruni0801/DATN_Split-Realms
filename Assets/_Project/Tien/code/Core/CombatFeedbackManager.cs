using System.Collections;
using UnityEngine;

public class CombatFeedbackManager : MonoBehaviour
{
    public static CombatFeedbackManager Instance { get; private set; }

    [Header("Hit Feedback (Light/Normal)")]
    [SerializeField] private float normalHitStop = 0.015f;
    [SerializeField] private float normalShakeDuration = 0.05f;
    [SerializeField] private float normalShakeMagnitude = 0.05f;
    [SerializeField] private GameObject normalHitVFX;
    [SerializeField] private AudioClip normalHitSFX;

    [Header("Hit Feedback (Heavy/Crit)")]
    [SerializeField] private float heavyHitStop = 0.04f;
    [SerializeField] private float heavyShakeDuration = 0.1f;
    [SerializeField] private float heavyShakeMagnitude = 0.15f;
    [SerializeField] private GameObject heavyHitVFX;
    [SerializeField] private AudioClip heavyHitSFX;

    [Header("Player Hurt Feedback")]
    [SerializeField] private float hurtHitStop = 0.04f;
    [SerializeField] private float hurtShakeDuration = 0.15f;
    [SerializeField] private float hurtShakeMagnitude = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;

    private bool isHitStopping = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void PlayHitFeedback(Vector3 hitPoint, bool isHeavy = false)
    {
        float hitStop = isHeavy ? heavyHitStop : normalHitStop;
        float shakeDur = isHeavy ? heavyShakeDuration : normalShakeDuration;
        float shakeMag = isHeavy ? heavyShakeMagnitude : normalShakeMagnitude;
        GameObject vfxPrefab = isHeavy ? heavyHitVFX : normalHitVFX;
        AudioClip clip = isHeavy ? heavyHitSFX : normalHitSFX;

        // Hit Stop
        if (hitStop > 0f) TriggerHitStop(hitStop);

        // Camera Shake
        if (shakeDur > 0f && shakeMag > 0f && CameraController.Instance != null)
        {
            CameraController.Instance.ShakeCamera(shakeDur, shakeMag);
        }

        // VFX
        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, hitPoint, Quaternion.identity);
        }

        // SFX
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    public void PlayPlayerHurtFeedback()
    {
        // Hit Stop
        if (hurtHitStop > 0f) TriggerHitStop(hurtHitStop);

        // Camera Shake
        if (hurtShakeDuration > 0f && hurtShakeMagnitude > 0f && CameraController.Instance != null)
        {
            CameraController.Instance.ShakeCamera(hurtShakeDuration, hurtShakeMagnitude);
        }
    }

    private void TriggerHitStop(float duration)
    {
        if (isHitStopping) return;
        StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        isHitStopping = true;
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0.05f; // Freeze frame impact
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = originalTimeScale;
        isHitStopping = false;
    }
}

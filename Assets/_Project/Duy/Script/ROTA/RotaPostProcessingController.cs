using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class RotaPostProcessingController : MonoBehaviour
{
    [Header("Volume Reference")]
    [SerializeField] private Volume rotaVolume;
    [SerializeField] private VolumeProfile rotaProfile;
    
    [Header("Fade Settings")]
    [SerializeField] private float fadeInDuration = 0.8f;
    [SerializeField] private float fadeOutDuration = 0.6f;
    [SerializeField] private AnimationCurve blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    [SerializeField] private bool isActiveInZone;
    [SerializeField, Range(0f, 1f)] private float currentWeight;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        EnsureVolumeSetup();
    }

    private void EnsureVolumeSetup()
    {
        if (rotaVolume == null)
        {
            rotaVolume = GetComponent<Volume>();
            if (rotaVolume == null)
            {
                rotaVolume = gameObject.AddComponent<Volume>();
            }
        }

        if (rotaProfile != null && rotaVolume.profile == null)
        {
            rotaVolume.profile = rotaProfile;
        }

        rotaVolume.isGlobal = true;
        rotaVolume.weight = 0f;
        currentWeight = 0f;
    }

    public void OnPlayerEnterZone()
    {
        isActiveInZone = true;
        StartFade(1f, fadeInDuration);
    }

    public void OnPlayerExitZone()
    {
        isActiveInZone = false;
        StartFade(0f, fadeOutDuration);
    }

    private void StartFade(float targetWeight, float duration)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }
        fadeRoutine = StartCoroutine(FadeRoutine(targetWeight, duration));
    }

    private IEnumerator FadeRoutine(float targetWeight, float duration)
    {
        if (rotaVolume == null) yield break;

        float startWeight = rotaVolume.weight;
        float elapsed = 0f;

        if (duration <= 0.001f)
        {
            rotaVolume.weight = targetWeight;
            currentWeight = targetWeight;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = blendCurve != null ? blendCurve.Evaluate(t) : t;
            
            rotaVolume.weight = Mathf.Lerp(startWeight, targetWeight, curveT);
            currentWeight = rotaVolume.weight;
            yield return null;
        }

        rotaVolume.weight = targetWeight;
        currentWeight = targetWeight;
        fadeRoutine = null;
    }

    private void OnDisable()
    {
        if (rotaVolume != null)
        {
            rotaVolume.weight = 0f;
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HeartbeatNote : MonoBehaviour
{
    [Header("Optional Visuals")]
    [SerializeField] private Text labelText;
    [SerializeField] private float pulseScale = 0.08f;
    [SerializeField] private float pulseSpeed = 10f;
    [SerializeField] private float flatlineScaleMultiplier = 0.08f;
    [SerializeField] private float flatlinePulseSpeed = 1.4f;
    [SerializeField] private float heartbeatTravelAmplitude = 18f;
    [SerializeField] private float heartbeatFollowSpeed = 6f;

    private RectTransform rectTransform;
    private HeartbeatPattern pattern;
    private Vector2 startPosition;
    private Vector2 hitPosition;
    private Vector2 endPosition;
    private float travelDuration;
    private float elapsed;
    private Vector2 movementPosition;
    private Vector3 baseScale;
    private Vector2 shakeOffset;
    private float heartbeatIntensity = 1f;
    private float heartbeatTargetIntensity = 1f;
    private float heartbeatPhase;
    private Coroutine shakeRoutine;
    private bool initialized;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public HeartbeatPattern Pattern => pattern;

    public Vector2 HitPosition => hitPosition;

    public bool IsInitialized => initialized;

    public void Initialize(HeartbeatPattern newPattern, Vector2 start, Vector2 hit, Vector2 end, float speed)
    {
        pattern = newPattern;
        startPosition = start;
        hitPosition = hit;
        endPosition = end;
        elapsed = 0f;
        initialized = true;
        baseScale = transform.localScale;

        float distance = Vector2.Distance(startPosition, endPosition);
        travelDuration = Mathf.Max(0.01f, distance / Mathf.Max(0.01f, speed));

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = startPosition;
        }
        else
        {
            transform.localPosition = startPosition;
        }

        if (labelText != null)
        {
            labelText.text = pattern.DisplayText;
        }

        heartbeatPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    public void SetHeartbeatIntensity(float intensity)
    {
        heartbeatTargetIntensity = Mathf.Clamp01(intensity);
        if (!initialized)
        {
            heartbeatIntensity = heartbeatTargetIntensity;
        }
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        elapsed += Time.deltaTime;

        float t = Mathf.Clamp01(elapsed / travelDuration);
        movementPosition = Vector2.Lerp(startPosition, endPosition, t);
        Vector2 visualPosition = movementPosition;
        UpdateHeartbeatMotion(ref visualPosition);

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = visualPosition + shakeOffset;
        }
        else
        {
            Vector3 shakenPosition = new Vector3(visualPosition.x + shakeOffset.x, visualPosition.y + shakeOffset.y, transform.localPosition.z);
            transform.localPosition = shakenPosition;
        }
    }

    private void UpdateHeartbeatMotion(ref Vector2 position)
    {
        heartbeatIntensity = Mathf.MoveTowards(
            heartbeatIntensity,
            heartbeatTargetIntensity,
            Time.deltaTime * heartbeatFollowSpeed);

        float beatSpeed = Mathf.Lerp(flatlinePulseSpeed, pulseSpeed, heartbeatIntensity);
        float beatStrength = Mathf.Lerp(0f, pulseScale, heartbeatIntensity);
        float beatWave = Mathf.Sin((Time.time + heartbeatPhase) * beatSpeed);
        float secondaryWave = Mathf.Sin((Time.time + heartbeatPhase * 0.73f) * beatSpeed * 1.92f + 1.2f) * 0.35f;
        float heartbeatOffset = (beatWave + secondaryWave) * heartbeatTravelAmplitude * heartbeatIntensity;
        position.y += heartbeatOffset;

        float flattenedScale = Mathf.Lerp(flatlineScaleMultiplier, 1f, heartbeatIntensity);
        float beatScale = 1f + ((beatWave * 0.65f) + (secondaryWave * 0.35f)) * beatStrength;

        Vector3 scale = baseScale;
        scale.y = baseScale.y * Mathf.Max(0.01f, flattenedScale * beatScale);
        transform.localScale = scale;
    }

    public void PlayMissShake(float duration = 0.14f, float strength = 0.08f, float frequency = 24f)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }

        shakeRoutine = StartCoroutine(ShakeRoutine(duration, strength, frequency));
    }

    private IEnumerator ShakeRoutine(float duration, float strength, float frequency)
    {
        float elapsedTime = 0f;
        shakeOffset = Vector2.zero;

        while (elapsedTime < duration && initialized)
        {
            elapsedTime += Time.deltaTime;
            float x = Mathf.Sin(Time.time * frequency * 1.37f) * strength;
            float y = Mathf.Cos(Time.time * frequency) * strength;
            shakeOffset = new Vector2(x, y);
            yield return null;
        }

        shakeOffset = Vector2.zero;
        shakeRoutine = null;
    }

    public bool IsWithinHitWindow(float hitWindowDistance)
    {
        if (!initialized)
        {
            return false;
        }

        return Vector2.Distance(movementPosition, hitPosition) <= hitWindowDistance;
    }

    public bool HasPassedMissWindow(float missWindowDistance)
    {
        if (!initialized)
        {
            return false;
        }

        Vector2 travelDirection = (endPosition - startPosition).normalized;
        if (travelDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        bool hasPassedHitPoint = Vector2.Dot(movementPosition - hitPosition, travelDirection) > 0f;
        return hasPassedHitPoint && Vector2.Distance(movementPosition, hitPosition) > missWindowDistance;
    }

    public bool HasReachedDestroyPoint()
    {
        if (!initialized)
        {
            return false;
        }

        return elapsed >= travelDuration;
    }
}

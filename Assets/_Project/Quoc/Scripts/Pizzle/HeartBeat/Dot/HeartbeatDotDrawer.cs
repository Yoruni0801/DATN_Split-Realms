using UnityEngine;

public class HeartbeatDotDrawer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform dot;
    [SerializeField] private ECGUILine ecgLine;
    [SerializeField] private RectTransform startPoint;
    [SerializeField] private RectTransform hitPoint;
    [SerializeField] private RectTransform missPoint;

    [Header("Line Area")]
    [SerializeField] private float startX = -430f;
    [SerializeField] private float endX = 430f;
    [SerializeField] private float centerY = 0f;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 180f;
    [SerializeField] private float pointSpacing = 4f;

    [Header("Heartbeat")]
    [SerializeField] private float amplitude = 70f;
    [SerializeField] private float currentPower = 1f;
    [SerializeField] private float powerFollowSpeed = 3.5f;
    [SerializeField] private float hitPowerBoost = 0.22f;
    [SerializeField] private float hitImmediatePower = 1.35f;
    [SerializeField] private float hitAmplitudeMultiplier = 1.22f;
    [SerializeField] private float hitSpeedMultiplier = 1.06f;
    [SerializeField] private float victoryPower = 1.55f;
    [SerializeField] private float defeatPower = 0f;
    [SerializeField] private float maxPower = 1.75f;
    [SerializeField] private float victorySpeedMultiplier = 1.2f;
    [SerializeField] private float defeatSpeedMultiplier = 0.75f;
    [SerializeField] private float victoryAmplitudeMultiplier = 1.15f;
    [SerializeField] private float defeatAmplitudeMultiplier = 0.06f;

    private float x;
    private float lastPointX;
    private bool hasWarnedMissingReferences;
    private RectTransform ecgLineRect;
    private float targetPower = 1f;
    private float runtimeMoveSpeed;
    private float runtimeAmplitudeMultiplier = 1f;
    private float runtimeSpeedMultiplier = 1f;
    private Vector2 startLocalPoint;
    private Vector2 hitLocalPoint;
    private Vector2 missLocalPoint;
    private bool hasResolvedPoints;
    private Transform cachedSearchRoot;

    private void Start()
    {
        if (AutoBindReferences())
        {
            ApplyPlayingState();
            ResetLine();
        }
    }

    private void Awake()
    {
        AutoBindReferences();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        hasResolvedPoints = false;
        cachedSearchRoot = null;
        AutoBindReferences();
    }
#endif

    private void Update()
    {
        if (!AutoBindReferences())
        {
            if (!hasWarnedMissingReferences)
            {
                Debug.LogError("[HeartbeatDotDrawer] Chưa tìm thấy Dot hoặc ECGLine.");
                hasWarnedMissingReferences = true;
            }
            return;
        }

        ResolvePointsIfNeeded();

        currentPower = Mathf.MoveTowards(currentPower, targetPower, Time.deltaTime * powerFollowSpeed);
        float effectiveMoveSpeed = runtimeMoveSpeed * runtimeSpeedMultiplier;
        float effectiveAmplitude = amplitude * runtimeAmplitudeMultiplier * currentPower;

        x += effectiveMoveSpeed * Time.deltaTime;

        if (x >= missLocalPoint.x)
        {
            ResetLine();
            return;
        }

        float y = centerY + GetECGWave(x) * effectiveAmplitude;

        Vector2 pos = new Vector2(x, y);

        SetDotPosition(pos);

        if (Mathf.Abs(x - lastPointX) >= pointSpacing)
        {
            ecgLine.AddPoint(pos);
            lastPointX = x;
        }
    }

    private float GetECGWave(float xPos)
    {
        float travelStart = startLocalPoint.x;
        float travelEnd = missLocalPoint.x;

        if (Mathf.Abs(travelEnd - travelStart) <= 0.0001f)
        {
            return 0f;
        }

        float t = Mathf.InverseLerp(travelStart, travelEnd, xPos);
        float hitT = Mathf.Clamp01(Mathf.InverseLerp(travelStart, travelEnd, hitLocalPoint.x));

        float pStart = Mathf.Clamp01(hitT - 0.26f);
        float pPeak = Mathf.Clamp01(hitT - 0.20f);
        float pEnd = Mathf.Clamp01(hitT - 0.14f);
        float qEnd = Mathf.Clamp01(hitT - 0.03f);
        float rEnd = Mathf.Clamp01(hitT + 0.02f);
        float sEnd = Mathf.Clamp01(hitT + 0.08f);
        float tStart = Mathf.Clamp01(hitT + 0.18f);
        float tPeak = Mathf.Clamp01(hitT + 0.30f);
        float tEnd = Mathf.Clamp01(hitT + 0.42f);

        if (t < pStart) return 0f;
        if (t < pPeak) return Mathf.Lerp(0f, 0.22f, Mathf.InverseLerp(pStart, pPeak, t));
        if (t < pEnd) return Mathf.Lerp(0.22f, -0.25f, Mathf.InverseLerp(pPeak, pEnd, t));
        if (t < qEnd) return Mathf.Lerp(-0.25f, 0f, Mathf.InverseLerp(pEnd, qEnd, t));

        if (t < hitT) return Mathf.Lerp(0f, 1.2f, Mathf.InverseLerp(qEnd, hitT, t));
        if (t < rEnd) return Mathf.Lerp(1.2f, -0.9f, Mathf.InverseLerp(hitT, rEnd, t));
        if (t < sEnd) return Mathf.Lerp(-0.9f, 0f, Mathf.InverseLerp(rEnd, sEnd, t));

        if (t < tStart) return 0f;
        if (t < tPeak) return Mathf.Lerp(0f, 0.32f, Mathf.InverseLerp(tStart, tPeak, t));
        if (t < tEnd) return Mathf.Lerp(0.32f, 0f, Mathf.InverseLerp(tPeak, tEnd, t));

        return 0f;
    }

    public void OnMiss()
    {
        currentPower = 0f;
        targetPower = 0f;
        runtimeAmplitudeMultiplier = defeatAmplitudeMultiplier;
        runtimeSpeedMultiplier = defeatSpeedMultiplier;
        hasResolvedPoints = false;
        ResolvePointsIfNeeded();
    }

    public void OnHit()
    {
        targetPower = Mathf.Clamp(Mathf.Max(targetPower + hitPowerBoost, hitImmediatePower), defeatPower, maxPower);
        currentPower = Mathf.Max(currentPower, hitImmediatePower);
        runtimeAmplitudeMultiplier = hitAmplitudeMultiplier;
        runtimeSpeedMultiplier = hitSpeedMultiplier;
    }

    public void ApplyPlayingState()
    {
        targetPower = 1f;
        currentPower = 1f;
        runtimeAmplitudeMultiplier = 1f;
        runtimeSpeedMultiplier = 1f;
        hasResolvedPoints = false;
    }

    public void ApplyVictoryState()
    {
        targetPower = Mathf.Clamp(victoryPower, defeatPower, maxPower);
        currentPower = targetPower;
        runtimeAmplitudeMultiplier = victoryAmplitudeMultiplier;
        runtimeSpeedMultiplier = victorySpeedMultiplier;
    }

    public void ApplyDefeatState()
    {
        targetPower = Mathf.Clamp(defeatPower, defeatPower, maxPower);
        currentPower = 0f;
        runtimeAmplitudeMultiplier = defeatAmplitudeMultiplier;
        runtimeSpeedMultiplier = defeatSpeedMultiplier;
    }

    private void ResetLine()
    {
        ResolvePointsIfNeeded();
        x = startLocalPoint.x;
        lastPointX = startLocalPoint.x;

        ecgLine.Clear();

        Vector2 startPos = new Vector2(x, centerY);
        SetDotPosition(startPos);
        ecgLine.AddPoint(startPos);
    }

    private bool AutoBindReferences()
    {
        if (dot == null)
        {
            dot = transform as RectTransform;
        }

        ecgLine = ResolveECGLine();

        ecgLineRect = ecgLine != null ? ecgLine.RectTransformCached : null;

        return dot != null && ecgLine != null;
    }

    private ECGUILine ResolveECGLine()
    {
        if (ecgLine != null)
        {
            return ecgLine;
        }

        ECGUILine localLine = GetComponentInParent<ECGUILine>(true);
        if (localLine != null)
        {
            return localLine;
        }

        localLine = GetComponentInChildren<ECGUILine>(true);
        if (localLine != null)
        {
            return localLine;
        }

        Transform searchRoot = GetSearchRoot();
        if (searchRoot != null)
        {
            ECGUILine bestLine = GetBestECGLine(searchRoot.GetComponentsInChildren<ECGUILine>(true));
            if (bestLine != null)
            {
                return bestLine;
            }
        }

#if UNITY_2023_1_OR_NEWER
        return GetBestECGLine(FindObjectsByType<ECGUILine>(FindObjectsInactive.Include, FindObjectsSortMode.None));
#else
        return GetBestECGLine(Resources.FindObjectsOfTypeAll<ECGUILine>());
#endif
    }

    private Transform GetSearchRoot()
    {
        if (cachedSearchRoot != null)
        {
            return cachedSearchRoot;
        }

        Canvas canvas = GetComponentInParent<Canvas>(true);
        cachedSearchRoot = canvas != null ? canvas.transform : transform.root;
        return cachedSearchRoot;
    }

    private ECGUILine GetBestECGLine(ECGUILine[] candidates)
    {
        ECGUILine bestLine = null;
        int bestScore = int.MinValue;
        Vector3 dotPosition = dot != null ? dot.position : transform.position;
        Canvas ownCanvas = GetComponentInParent<Canvas>(true);

        for (int i = 0; i < candidates.Length; i++)
        {
            ECGUILine candidate = candidates[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            int score = 0;
            if (candidate.transform.root == transform.root)
            {
                score += 1000;
            }

            Canvas candidateCanvas = candidate.GetComponentInParent<Canvas>(true);
            if (ownCanvas != null && candidateCanvas == ownCanvas)
            {
                score += 500;
            }

            if (candidate.transform.parent == transform.parent)
            {
                score += 250;
            }

            if (candidate.gameObject.activeInHierarchy)
            {
                score += 50;
            }

            float distance = Vector3.SqrMagnitude(candidate.RectTransformCached.position - dotPosition);
            score -= Mathf.RoundToInt(distance * 0.001f);

            if (score > bestScore)
            {
                bestScore = score;
                bestLine = candidate;
            }
        }

        return bestLine;
    }

    private void ResolvePointsIfNeeded()
    {
        if (hasResolvedPoints)
        {
            return;
        }

        if (ecgLineRect == null)
        {
            return;
        }

        startLocalPoint = ResolveLocalPoint(startPoint, new Vector2(startX, centerY));
        hitLocalPoint = ResolveLocalPoint(hitPoint, new Vector2((startLocalPoint.x + endX) * 0.5f, centerY));
        missLocalPoint = ResolveLocalPoint(missPoint, new Vector2(endX, centerY));
        centerY = startLocalPoint.y;
        startX = startLocalPoint.x;
        endX = missLocalPoint.x;
        runtimeMoveSpeed = moveSpeed;
        hasResolvedPoints = true;
    }

    private Vector2 ResolveLocalPoint(RectTransform marker, Vector2 fallback)
    {
        if (marker == null || ecgLineRect == null)
        {
            return fallback;
        }

        return ecgLineRect.InverseTransformPoint(marker.position);
    }

    private void SetDotPosition(Vector2 localPoint)
    {
        if (dot == null)
        {
            return;
        }

        RectTransform dotParent = dot.parent as RectTransform;
        if (dotParent == null || ecgLineRect == null || dotParent == ecgLineRect)
        {
            dot.anchoredPosition = localPoint;
            return;
        }

        Vector3 worldPoint = ecgLineRect.TransformPoint(localPoint);
        dot.anchoredPosition = dotParent.InverseTransformPoint(worldPoint);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        DrawMarkerGizmo(startPoint, Color.cyan, "Start");
        DrawMarkerGizmo(hitPoint, Color.green, "Hit");
        DrawMarkerGizmo(missPoint, Color.red, "Miss");
    }

    private void DrawMarkerGizmo(RectTransform marker, Color color, string label)
    {
        if (marker == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireSphere(marker.position, 10f);

#if UNITY_EDITOR
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(marker.position + Vector3.up * 18f, label);
#endif
    }
#endif

}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ECG dạng UI cho mini game giải cứu. Đường được vẽ nhiều lớp (aura, thân
/// sóng, lõi sáng) để vẫn chạy tốt trên Canvas thường nhưng có cảm giác ma
/// thuật/sinh lực thay vì một LineRenderer đỏ phẳng.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class EcgLineGraphic : Graphic
{
    [Header("Line Settings")]
    [Min(48)] public int pointCount = 420;
    [Min(1f)] public float lineWidth = 6.5f;
    public float baseline = 0f;

    [Header("ECG Form")]
    [Min(1f)] public float baseAmplitude = 72f;
    [Min(0.1f)] public float waveSpacing = 0.35f;
    [Range(0.01f, 0.2f)] public float spikeWidth = 0.05f;
    [Range(0f, 0.5f)] public float smallWaveAmount = 0.18f;

    [Header("Natural Rhythm")]
    [Tooltip("Lệch nhẹ khoảng cách giữa các nhịp để ECG không lặp máy móc.")]
    [Range(0f, 0.16f)] [SerializeField] private float beatSpacingJitter = 0.14f;
    [Tooltip("Độ cao các đỉnh R thay đổi nhẹ nhưng vẫn dễ đọc.")]
    [Range(0f, 0.35f)] [SerializeField] private float beatAmplitudeVariation = 0.24f;

    [Header("Scrolling Control")]
    [Min(0.01f)] public float scrollSpeed = 0.4f;

    [Header("Cinematic Palette")]
    [SerializeField] private Color32 normalColor = new Color32(32, 184, 196, 205);
    [SerializeField] private Color32 normalCoreColor = new Color32(86, 238, 240, 220);
    [SerializeField] private Color32 activeColor = new Color32(255, 178, 46, 255);
    [SerializeField] private Color32 activeCoreColor = new Color32(255, 229, 145, 255);
    [SerializeField] [Range(0.5f, 3f)] private float beatHighlightRange = 1.34f;
    [SerializeField] [Range(1.5f, 7f)] private float auraWidthMultiplier = 3.1f;
    [SerializeField] [Range(0.1f, 1f)] private float coreWidthMultiplier = 0.24f;
    [SerializeField] [Range(0f, 1f)] private float targetMarkerNormalized = 0.75f;

    [Header("Baseline Life")]
    [SerializeField] private bool useNoise = true;
    [SerializeField] [Range(0f, 0.15f)] private float noiseAmplitude = 0.012f;
    [SerializeField] [Min(0f)] private float noiseSpeed = 16f;

    [Header("ECG Amplitude Levels (0 to 9)")]
    [SerializeField] private float[] levelMultipliers =
    {
        0.22f, 0.4f, 0.62f, 0.84f, 1.0f,
        1.12f, 1.24f, 1.36f, 1.48f, 1.6f
    };

    private const int NoActiveBeat = -999;
    private readonly List<Vector2> points = new List<Vector2>();

    public float TimeOffset { get; private set; }
    public bool isPaused { get; set; }

    private float currentAmplitudeMultiplier = 1f;
    private int activeBeatId = NoActiveBeat;
    private float activeBeatPulse;
    private float successPulse;
    private float missPulse;

    protected override void Awake()
    {
        base.Awake();
        ApplyCinematicArtDirection();
        SetHeartLevel(3);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        ApplyCinematicArtDirection();
        pointCount = Mathf.Max(48, pointCount);
        lineWidth = Mathf.Max(1f, lineWidth);
        waveSpacing = Mathf.Max(0.1f, waveSpacing);
        scrollSpeed = Mathf.Max(0.01f, scrollSpeed);
    }
#endif

    /// <summary>Đưa ECG về trạng thái sạch cho lần chơi/retry tiếp theo.</summary>
    public void ResetVisualState()
    {
        TimeOffset = 0f;
        activeBeatId = NoActiveBeat;
        activeBeatPulse = 0f;
        successPulse = 0f;
        missPulse = 0f;
        isPaused = false;
        SetVerticesDirty();
    }

    public void SetHeartLevel(int level)
    {
        if (levelMultipliers == null || levelMultipliers.Length == 0)
        {
            return;
        }

        int safeLevel = Mathf.Clamp(level, 0, levelMultipliers.Length - 1);
        currentAmplitudeMultiplier = levelMultipliers[safeLevel];
        SetVerticesDirty();
    }

    public void ForceFlatline()
    {
        currentAmplitudeMultiplier = 0f;
        DeactivateBeat();
        SetVerticesDirty();
    }

    /// <summary>Kích hoạt nhịp tiếp theo; targetT giữ tương thích với API cũ.</summary>
    public void ActivateNextBeat(float targetT)
    {
        ActivateNextBeat(targetT, 1.2f);
    }

    /// <summary>
    /// Chọn đỉnh R đang ở trước mốc theo thời gian báo trước. Nhờ vậy nhịp đôi
    /// và cao trào có thể tới nhanh hơn mà không cần tăng tốc hoặc làm rối ECG.
    /// </summary>
    public void ActivateNextBeat(float targetT, float secondsUntilTarget)
    {
        targetMarkerNormalized = Mathf.Clamp01(targetT);
        float desiredX = Mathf.Clamp(
            targetMarkerNormalized - Mathf.Max(0.25f, secondsUntilTarget) * scrollSpeed,
            0.03f,
            targetMarkerNormalized - 0.025f);
        activeBeatId = FindBeatNearestToX(desiredX, targetMarkerNormalized);
        activeBeatPulse = 1f;
        SetVerticesDirty();
    }

    public void DeactivateBeat()
    {
        activeBeatId = NoActiveBeat;
        SetVerticesDirty();
    }

    public void PlaySuccessPulse()
    {
        successPulse = 1f;
        SetVerticesDirty();
    }

    public void PlayMissPulse()
    {
        missPulse = 1f;
        SetVerticesDirty();
    }

    public Vector2 GetLocalPoint(float normalizedX)
    {
        Rect rect = rectTransform.rect;
        float t = Mathf.Clamp01(normalizedX);
        float centerY = rect.yMin + rect.height * (0.5f + baseline);
        return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, t), centerY + GetEcgValue(t) * baseAmplitude);
    }

    public float GetCurrentWaveXNormalized(float targetXNormalized)
    {
        return activeBeatId == NoActiveBeat
            ? -999f
            : GetBeatCenter(activeBeatId) - TimeOffset;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        activeBeatPulse = Mathf.MoveTowards(activeBeatPulse, 0f, deltaTime * 1.8f);
        successPulse = Mathf.MoveTowards(successPulse, 0f, deltaTime * 2.6f);
        missPulse = Mathf.MoveTowards(missPulse, 0f, deltaTime * 2.3f);

        if (Application.isPlaying && !isPaused)
        {
            TimeOffset -= deltaTime * scrollSpeed;
        }

        // Dù đã dừng cuộn (tutorial), pulse vẫn cần được render giảm dần.
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f || pointCount < 2)
        {
            return;
        }

        BuildPoints(rect);

        // Ribbon liên tục thay cho từng quad riêng lẻ: không còn các khe đen
        // hoặc răng cưa giữa các đoạn sóng khi Canvas bị scale lớn.
        AddRibbon(vh, lineWidth * auraWidthMultiplier, 0);
        AddRibbon(vh, lineWidth, 1);
        AddRibbon(vh, lineWidth * coreWidthMultiplier, 2);

        DrawCheckpointMarker(vh);
    }

    private void BuildPoints(Rect rect)
    {
        points.Clear();
        float centerY = rect.yMin + rect.height * (0.5f + baseline);

        for (int i = 0; i < pointCount; i++)
        {
            float t = (float)i / (pointCount - 1);
            float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
            float y = centerY + GetEcgValue(t) * baseAmplitude;
            points.Add(new Vector2(x, y));
        }
    }

    private void DrawCheckpointMarker(VertexHelper vh)
    {
        Vector2 position = GetLocalPoint(targetMarkerNormalized);
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5.5f) * 0.08f + activeBeatPulse * 0.22f;
        float outerRadius = lineWidth * 3.25f * pulse;
        float innerRadius = lineWidth * 1.45f * pulse;

        AddDiamond(vh, position, outerRadius, WithAlpha(activeColor, 32));
        AddDiamond(vh, position, innerRadius, WithAlpha(activeColor, 105));
        AddDiamond(vh, position, lineWidth * 0.62f, activeCoreColor);

        float guideHalfHeight = lineWidth * 3.6f;
        AddSegment(vh,
            new Vector2(position.x, position.y - guideHalfHeight),
            new Vector2(position.x, position.y + guideHalfHeight),
            lineWidth * 0.18f,
            WithAlpha(activeColor, 145));
    }

    private float GetSegmentP(float t)
    {
        GetBeatSample(t, out _, out float phase);
        return phase;
    }

    private bool IsActiveBeatSegment(float t)
    {
        if (activeBeatId == NoActiveBeat || currentAmplitudeMultiplier <= 0f)
        {
            return false;
        }

        GetBeatSample(t, out int beatId, out float phase);
        // Bôi sáng riêng cụm QRS (Q -> R -> S), đúng chỗ người chơi cần canh.
        return beatId == activeBeatId && phase >= -0.08f && phase <= 0.085f;
    }

    private float GetEcgValue(float t)
    {
        GetBeatSample(t, out int beatId, out float p);

        float beatAmplitude = 1f + GetBeatAmplitudeOffset(beatId);
        if (beatId == activeBeatId)
        {
            beatAmplitude *= 1f + activeBeatPulse * 0.1f;
        }

        // P-Q-R-S-T bằng các đoạn chuyển tiếp có chủ đích: P/T tròn, QRS gần
        // như thẳng và sắc như ECG monitor thật, thay vì các "u" Gaussian mềm.
        float ecg = GetClinicalPqrst(p, beatAmplitude);

        if (useNoise && currentAmplitudeMultiplier > 0f)
        {
            float sampledT = t + TimeOffset;
            float noise = (Mathf.PerlinNoise(sampledT * noiseSpeed, 0.37f) - 0.5f) * noiseAmplitude;
            float nearQrs = Mathf.Clamp01(Mathf.Abs(p) / (spikeWidth * beatHighlightRange));
            ecg += noise * nearQrs;
        }

        return ecg * currentAmplitudeMultiplier;
    }

    private float GetClinicalPqrst(float phase, float qrsAmplitude)
    {
        // P wave: nhô nhỏ, tròn.
        if (phase >= -0.34f && phase < -0.21f)
        {
            return Mathf.Sin(Mathf.InverseLerp(-0.34f, -0.21f, phase) * Mathf.PI)
                * (0.12f + smallWaveAmount * 0.12f);
        }

        // Q -> R -> S: ba cạnh rõ, có tỷ lệ giống lead II ECG thông thường.
        if (phase >= -0.075f && phase < -0.035f)
        {
            return Mathf.Lerp(0f, -0.22f * qrsAmplitude, Mathf.InverseLerp(-0.075f, -0.035f, phase));
        }
        if (phase >= -0.035f && phase < 0f)
        {
            return Mathf.Lerp(-0.22f * qrsAmplitude, 1.32f * qrsAmplitude, Mathf.InverseLerp(-0.035f, 0f, phase));
        }
        if (phase >= 0f && phase < 0.05f)
        {
            return Mathf.Lerp(1.32f * qrsAmplitude, -0.48f * qrsAmplitude, Mathf.InverseLerp(0f, 0.05f, phase));
        }
        if (phase >= 0.05f && phase < 0.105f)
        {
            return Mathf.Lerp(-0.48f * qrsAmplitude, 0f, Mathf.InverseLerp(0.05f, 0.105f, phase));
        }

        // T wave: hồi phục rộng, thấp hơn R và tròn hơn P.
        if (phase >= 0.145f && phase < 0.34f)
        {
            return Mathf.Sin(Mathf.InverseLerp(0.145f, 0.34f, phase) * Mathf.PI)
                * (0.30f + qrsAmplitude * 0.05f);
        }

        return 0f;
    }

    private int FindBeatNearestToX(float desiredX, float targetX)
    {
        float safeSpacing = Mathf.Max(0.1f, waveSpacing);
        int around = Mathf.RoundToInt((TimeOffset + desiredX) / safeSpacing);
        int selectedId = around;
        float bestDistance = float.MaxValue;

        // Chỉ xét đỉnh nằm trước target; không bao giờ nhảy sang một đỉnh đã qua.
        for (int id = around - 3; id <= around + 3; id++)
        {
            float x = GetBeatCenter(id) - TimeOffset;
            if (x < -0.1f || x >= targetX - 0.01f)
            {
                continue;
            }

            float distance = Mathf.Abs(x - desiredX);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                selectedId = id;
            }
        }

        return selectedId;
    }

    private void GetBeatSample(float t, out int beatId, out float phase)
    {
        float sampledT = t + TimeOffset;
        float safeSpacing = Mathf.Max(0.1f, waveSpacing);
        int around = Mathf.RoundToInt(sampledT / safeSpacing);
        beatId = around;
        float nearestDistance = float.MaxValue;

        for (int id = around - 2; id <= around + 2; id++)
        {
            float distance = Mathf.Abs(sampledT - GetBeatCenter(id));
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                beatId = id;
            }
        }

        phase = (sampledT - GetBeatCenter(beatId)) / safeSpacing;
    }

    private float GetBeatCenter(int beatId)
    {
        float jitter = Mathf.Sin(beatId * 12.9898f + 78.233f) * 43758.5453f;
        jitter -= Mathf.Floor(jitter);
        return beatId * waveSpacing + (jitter - 0.5f) * waveSpacing * beatSpacingJitter;
    }

    private float GetBeatAmplitudeOffset(int beatId)
    {
        float variation = Mathf.Sin(beatId * 4.734f + 1.618f) * 0.5f + 0.5f;
        return (variation - 0.5f) * 2f * beatAmplitudeVariation;
    }

    private Color32 GetOuterColor(bool isActiveSegment)
    {
        Color32 baseColor = isActiveSegment ? activeColor : normalColor;
        byte alpha = isActiveSegment ? (byte)105 : (byte)38;

        if (missPulse > 0f && !isActiveSegment)
        {
            baseColor = Color.Lerp(baseColor, new Color32(238, 76, 104, 255), missPulse);
            alpha = (byte)Mathf.Lerp(alpha, 70, missPulse);
        }
        else if (successPulse > 0f)
        {
            baseColor = Color.Lerp(baseColor, activeColor, successPulse * 0.5f);
            alpha = (byte)Mathf.Lerp(alpha, 92, successPulse);
        }

        return WithAlpha(baseColor, alpha);
    }

    private Color32 GetBodyColor(bool isActiveSegment)
    {
        if (currentAmplitudeMultiplier <= 0f)
        {
            return new Color32(122, 59, 76, 185);
        }

        Color body = isActiveSegment ? activeColor : normalColor;
        if (missPulse > 0f && !isActiveSegment)
        {
            body = Color.Lerp(body, new Color32(232, 67, 92, 255), missPulse);
        }
        else if (successPulse > 0f)
        {
            body = Color.Lerp(body, activeColor, successPulse * 0.28f);
        }

        body.a = isActiveSegment ? 0.94f : 0.72f;
        return body;
    }

    private Color32 GetCoreColor(bool isActiveSegment)
    {
        if (currentAmplitudeMultiplier <= 0f)
        {
            return new Color32(204, 103, 120, 200);
        }

        Color core = isActiveSegment ? activeCoreColor : normalCoreColor;
        if (successPulse > 0f)
        {
            core = Color.Lerp(core, Color.white, successPulse * 0.55f);
        }

        core.a = isActiveSegment ? 0.92f : 0.52f;
        return core;
    }

    private void ApplyCinematicArtDirection()
    {
        // Các scene/prefab trước đây có nhiều bản ECG cùng tên và lưu lại màu
        // hồng/đỏ cũ. Profile này được áp trực tiếp để mọi instance nhất quán.
        normalColor = new Color32(32, 184, 196, 205);
        normalCoreColor = new Color32(86, 238, 240, 220);
        activeColor = new Color32(255, 178, 46, 255);
        activeCoreColor = new Color32(255, 229, 145, 255);
        lineWidth = 6.5f;
        baseAmplitude = 72f;
        beatHighlightRange = 1.34f;
        auraWidthMultiplier = 3.1f;
        coreWidthMultiplier = 0.24f;
        beatSpacingJitter = 0.14f;
        beatAmplitudeVariation = 0.24f;
        noiseAmplitude = 0.012f;
        noiseSpeed = 16f;
    }

    private void AddRibbon(VertexHelper vh, float width, int layer)
    {
        if (width <= 0f || points.Count < 2)
        {
            return;
        }

        int startIndex = vh.currentVertCount;
        int lastIndex = points.Count - 1;

        for (int i = 0; i <= lastIndex; i++)
        {
            Vector2 tangent;
            if (i == 0)
            {
                tangent = points[1] - points[0];
            }
            else if (i == lastIndex)
            {
                tangent = points[lastIndex] - points[lastIndex - 1];
            }
            else
            {
                tangent = points[i + 1] - points[i - 1];
            }

            Vector2 normal = tangent.sqrMagnitude > 0.0001f
                ? new Vector2(-tangent.y, tangent.x).normalized * (width * 0.5f)
                : Vector2.up * (width * 0.5f);
            float t = (float)i / lastIndex;
            bool isActive = IsActiveBeatSegment(t);
            Color32 color = layer == 0
                ? GetOuterColor(isActive)
                : layer == 1 ? GetBodyColor(isActive) : GetCoreColor(isActive);
            Vector2 uv = new Vector2(t, layer * 0.5f);

            vh.AddVert(points[i] - normal, color, uv);
            vh.AddVert(points[i] + normal, color, uv);
        }

        for (int i = 0; i < lastIndex; i++)
        {
            int index = startIndex + i * 2;
            vh.AddTriangle(index, index + 1, index + 3);
            vh.AddTriangle(index, index + 3, index + 2);
        }
    }

    private static void AddSegment(VertexHelper vh, Vector2 start, Vector2 end, float width, Color32 segmentColor)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.0001f || segmentColor.a == 0)
        {
            return;
        }

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
        int index = vh.currentVertCount;

        vh.AddVert(start - normal, segmentColor, Vector2.zero);
        vh.AddVert(start + normal, segmentColor, Vector2.zero);
        vh.AddVert(end + normal, segmentColor, Vector2.zero);
        vh.AddVert(end - normal, segmentColor, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    private static void AddDiamond(VertexHelper vh, Vector2 center, float radius, Color32 diamondColor)
    {
        if (diamondColor.a == 0)
        {
            return;
        }

        int index = vh.currentVertCount;
        vh.AddVert(new Vector2(center.x, center.y + radius), diamondColor, Vector2.zero);
        vh.AddVert(new Vector2(center.x + radius, center.y), diamondColor, Vector2.zero);
        vh.AddVert(new Vector2(center.x, center.y - radius), diamondColor, Vector2.zero);
        vh.AddVert(new Vector2(center.x - radius, center.y), diamondColor, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    private static Color32 WithAlpha(Color32 color, byte alpha)
    {
        color.a = alpha;
        return color;
    }
}

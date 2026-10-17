using UnityEngine;

[DisallowMultipleComponent]
public class ParrySuccessEffect : MonoBehaviour
{
    private const int RingSegments = 48;

    private LineRenderer ring;
    private LineRenderer slashA;
    private LineRenderer slashB;
    private Material material;
    private Color color = Color.cyan;
    private float duration = 0.18f;
    private float startRadius = 0.25f;
    private float endRadius = 1.15f;
    private float elapsed;

    public static ParrySuccessEffect Spawn(
        Vector3 position,
        Color effectColor,
        float effectDuration,
        float ringStartRadius,
        float ringEndRadius)
    {
        GameObject effectObject = new GameObject("ParrySuccessEffect");
        effectObject.transform.position = position;

        ParrySuccessEffect effect = effectObject.AddComponent<ParrySuccessEffect>();
        effect.Configure(effectColor, effectDuration, ringStartRadius, ringEndRadius);
        return effect;
    }

    private void Awake()
    {
        material = CreateLineMaterial();
        ring = CreateLine("Ring", RingSegments + 1, 0.055f);
        slashA = CreateLine("SlashA", 2, 0.08f);
        slashB = CreateLine("SlashB", 2, 0.05f);
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        float radius = Mathf.Lerp(startRadius, endRadius, eased);
        float alpha = Mathf.Lerp(1f, 0f, t);

        Color currentColor = new Color(color.r, color.g, color.b, color.a * alpha);
        ApplyColor(ring, currentColor);
        ApplyColor(slashA, currentColor);
        ApplyColor(slashB, currentColor * 0.85f);

        DrawRing(radius);
        DrawSlash(slashA, new Vector2(-radius * 0.7f, -radius * 0.18f), new Vector2(radius * 0.7f, radius * 0.18f));
        DrawSlash(slashB, new Vector2(-radius * 0.38f, radius * 0.35f), new Vector2(radius * 0.38f, -radius * 0.35f));

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }

    private void Configure(Color effectColor, float effectDuration, float ringStartRadius, float ringEndRadius)
    {
        color = effectColor;
        duration = Mathf.Max(0.01f, effectDuration);
        startRadius = Mathf.Max(0.01f, ringStartRadius);
        endRadius = Mathf.Max(startRadius, ringEndRadius);
    }

    private LineRenderer CreateLine(string childName, int pointCount, float width)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);

        LineRenderer line = child.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = pointCount;
        line.widthMultiplier = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.sortingOrder = 100;
        line.material = material;
        return line;
    }

    private void DrawRing(float radius)
    {
        for (int i = 0; i <= RingSegments; i++)
        {
            float angle = i / (float)RingSegments * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    private static void DrawSlash(LineRenderer line, Vector2 from, Vector2 to)
    {
        line.SetPosition(0, from);
        line.SetPosition(1, to);
    }

    private static void ApplyColor(LineRenderer line, Color lineColor)
    {
        line.startColor = lineColor;
        line.endColor = lineColor;
    }

    private static Material CreateLineMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Hidden/Internal-Colored");
        }

        return shader != null ? new Material(shader) : null;
    }
}

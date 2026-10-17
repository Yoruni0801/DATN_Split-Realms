using System;
using UnityEngine;

/// <summary>
/// Phản hồi thị giác và âm thanh rất nhẹ cho duy nhất cuốn sách người chơi đang rê chuột tới.
/// Không sửa material gốc của prefab sách.
/// </summary>
[DisallowMultipleComponent]
public class BookHoverFeedback : MonoBehaviour
{
    [SerializeField] private Color accentColor = new Color(1f, 0.7f, 0.22f, 1f);
    [SerializeField, Range(0.005f, 0.06f)] private float outlineWidth = 0.014f;
    [SerializeField, Range(0f, 1f)] private float rustleVolume = 0.12f;

    private DraggableBookItem book;
    private LineRenderer outline;
    private LineRenderer innerOutline;
    private LineRenderer[] cornerBrackets;
    private ParticleSystem dust;
    private Light hoverLight;
    private AudioSource hoverAudio;
    private bool isHovered;
    private float visibility;

    private static Material sharedLineMaterial;
    private static Material sharedParticleMaterial;
    private static AudioClip sharedRustleClip;

    private void Awake()
    {
        book = GetComponent<DraggableBookItem>();
        CreateVisuals();
    }

    public void SetHovered(bool value)
    {
        if (isHovered == value) return;

        isHovered = value;
        if (isHovered)
        {
            ConfigureDustShape();
            dust.Play(true);
            PlayRustle();
        }
        else
        {
            dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void Update()
    {
        if (book == null || book.IsDismissed || book.IsDragging || book.IsRecovering)
        {
            isHovered = false;
        }

        float targetVisibility = isHovered ? 1f : 0f;
        visibility = Mathf.MoveTowards(visibility, targetVisibility, Time.unscaledDeltaTime * 8f);
        UpdateOutline();

        if (hoverLight != null)
        {
            hoverLight.enabled = visibility > 0.01f;
            hoverLight.intensity = visibility * (0.18f + 0.045f * Mathf.Sin(Time.unscaledTime * 5.4f));
        }
    }

    private void CreateVisuals()
    {
        GameObject visualRoot = new GameObject("Book Hover Feedback");
        visualRoot.transform.SetParent(transform, false);

        outline = CreateLineRenderer(visualRoot.transform, "Outer Ancient Frame", 4, true, outlineWidth);
        innerOutline = CreateLineRenderer(visualRoot.transform, "Inner Ancient Frame", 4, true, outlineWidth * 0.5f);
        cornerBrackets = new LineRenderer[4];
        for (int i = 0; i < cornerBrackets.Length; i++)
        {
            cornerBrackets[i] = CreateLineRenderer(visualRoot.transform, $"Rune Corner {i + 1}", 3, false, outlineWidth * 0.8f);
            cornerBrackets[i].numCornerVertices = 2;
        }

        GameObject dustObject = new GameObject("Hover Dust");
        dustObject.transform.SetParent(transform, false);
        dust = dustObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = dust.main;
        main.playOnAwake = false;
        main.loop = true;
        main.maxParticles = 28;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.06f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.06f),
            new Color(1f, 0.9f, 0.65f, 0.55f));

        ParticleSystem.EmissionModule emission = dust.emission;
        emission.rateOverTime = 8f;

        ParticleSystem.ShapeModule shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f;

        ParticleSystem.NoiseModule noise = dust.noise;
        noise.enabled = true;
        noise.strength = 0.06f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.12f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = dust.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(accentColor, 0f),
                new GradientColorKey(new Color(1f, 0.92f, 0.7f), 0.6f),
                new GradientColorKey(accentColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.6f, 0.18f),
                new GradientAlphaKey(0.3f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
        dustRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        dustRenderer.alignment = ParticleSystemRenderSpace.View;
        dustRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dustRenderer.receiveShadows = false;
        dustRenderer.sharedMaterial = GetParticleMaterial();

        hoverLight = visualRoot.AddComponent<Light>();
        hoverLight.type = LightType.Point;
        hoverLight.color = accentColor;
        hoverLight.range = 1.25f;
        hoverLight.shadows = LightShadows.None;
        hoverLight.enabled = false;

        hoverAudio = visualRoot.AddComponent<AudioSource>();
        hoverAudio.playOnAwake = false;
        hoverAudio.spatialBlend = 0.35f;
        hoverAudio.volume = rustleVolume;
        hoverAudio.minDistance = 1f;
        hoverAudio.maxDistance = 9f;
    }

    private void ConfigureDustShape()
    {
        if (book == null || dust == null) return;

        float radius = 0.18f;
        if (book.TryGetBookSurface(out _, out Vector3 axisU, out Vector3 axisV, out _))
        {
            radius = Mathf.Clamp((axisU.magnitude + axisV.magnitude) * 0.32f, 0.11f, 0.42f);
        }
        ParticleSystem.ShapeModule shape = dust.shape;
        shape.radius = radius;
    }

    private void UpdateOutline()
    {
        if (outline == null) return;

        bool shouldShow = visibility > 0.01f;
        outline.enabled = shouldShow;
        if (innerOutline != null) innerOutline.enabled = shouldShow;
        if (cornerBrackets != null)
        {
            foreach (LineRenderer bracket in cornerBrackets)
            {
                if (bracket != null) bracket.enabled = shouldShow;
            }
        }
        if (!shouldShow) return;

        if (book.TryGetBookSurface(out Vector3 center, out Vector3 axisU, out Vector3 axisV, out Vector3 normal))
        {
            SetFrameGeometry(center, axisU, axisV);
            hoverLight.transform.position = center + normal * 0.04f;
        }
        else
        {
            Collider interactionCollider = book != null ? book.InteractionCollider : null;
            Bounds bounds = interactionCollider != null
                ? interactionCollider.bounds
                : new Bounds(transform.position, Vector3.one * 0.45f);
            float x = Mathf.Max(0.11f, bounds.extents.x * 1.08f);
            float z = Mathf.Max(0.11f, bounds.extents.z * 1.08f);
            Vector3 fallbackCenter = new Vector3(bounds.center.x, bounds.max.y + 0.018f, bounds.center.z);
            SetFrameGeometry(fallbackCenter, Vector3.right * x, Vector3.forward * z);
        }

        float pulse = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 5.4f);
        Color outerColor = new Color(accentColor.r, accentColor.g, accentColor.b, visibility * (0.42f + pulse * 0.18f));
        Color innerColor = new Color(1f, 0.91f, 0.68f, visibility * (0.18f + pulse * 0.17f));
        Color cornerColor = new Color(1f, 0.81f, 0.36f, visibility * (0.48f + pulse * 0.28f));

        ApplyLineColor(outline, outerColor, outlineWidth * (0.92f + 0.08f * pulse));
        ApplyLineColor(innerOutline, innerColor, outlineWidth * 0.5f);
        if (cornerBrackets != null)
        {
            foreach (LineRenderer bracket in cornerBrackets)
            {
                ApplyLineColor(bracket, cornerColor, outlineWidth * (0.72f + 0.08f * pulse));
            }
        }
    }

    private void SetFrameGeometry(Vector3 center, Vector3 halfU, Vector3 halfV)
    {
        Vector3 outerU = halfU * 1.065f;
        Vector3 outerV = halfV * 1.065f;
        Vector3 innerU = halfU * 0.91f;
        Vector3 innerV = halfV * 0.91f;

        SetClosedFrame(outline, center, outerU, outerV);
        SetClosedFrame(innerOutline, center, innerU, innerV);
        SetCornerBrackets(center, outerU, outerV);
    }

    private static void SetClosedFrame(LineRenderer line, Vector3 center, Vector3 halfU, Vector3 halfV)
    {
        if (line == null) return;
        line.SetPositions(new[]
        {
            center - halfU - halfV,
            center - halfU + halfV,
            center + halfU + halfV,
            center + halfU - halfV
        });
    }

    private void SetCornerBrackets(Vector3 center, Vector3 halfU, Vector3 halfV)
    {
        if (cornerBrackets == null || cornerBrackets.Length != 4) return;

        const float bracketLength = 0.31f;
        Vector3 bottomLeft = center - halfU - halfV;
        Vector3 topLeft = center - halfU + halfV;
        Vector3 topRight = center + halfU + halfV;
        Vector3 bottomRight = center + halfU - halfV;

        cornerBrackets[0].SetPositions(new[]
        {
            bottomLeft + halfU * bracketLength,
            bottomLeft,
            bottomLeft + halfV * bracketLength
        });
        cornerBrackets[1].SetPositions(new[]
        {
            topLeft - halfV * bracketLength,
            topLeft,
            topLeft + halfU * bracketLength
        });
        cornerBrackets[2].SetPositions(new[]
        {
            topRight - halfU * bracketLength,
            topRight,
            topRight - halfV * bracketLength
        });
        cornerBrackets[3].SetPositions(new[]
        {
            bottomRight + halfV * bracketLength,
            bottomRight,
            bottomRight - halfU * bracketLength
        });
    }

    private static void ApplyLineColor(LineRenderer line, Color color, float width)
    {
        if (line == null) return;
        line.startColor = color;
        line.endColor = color;
        line.widthMultiplier = width;
    }

    private static LineRenderer CreateLineRenderer(Transform parent, string objectName, int pointCount, bool loop, float width)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = pointCount;
        line.widthMultiplier = width;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = GetLineMaterial();
        return line;
    }

    private void PlayRustle()
    {
        if (hoverAudio == null || rustleVolume <= 0f) return;
        if (sharedRustleClip == null) sharedRustleClip = CreateRustleClip();
        hoverAudio.PlayOneShot(sharedRustleClip, rustleVolume);
    }

    private static Material GetLineMaterial()
    {
        if (sharedLineMaterial != null) return sharedLineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        sharedLineMaterial = new Material(shader) { name = "Book Hover Line (Runtime)" };
        return sharedLineMaterial;
    }

    private static Material GetParticleMaterial()
    {
        if (sharedParticleMaterial != null) return sharedParticleMaterial;

        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        sharedParticleMaterial = new Material(shader) { name = "Book Hover Dust (Runtime)" };
        return sharedParticleMaterial;
    }

    private static AudioClip CreateRustleClip()
    {
        const int sampleRate = 22050;
        const int sampleCount = 2800;
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(1597);
        float filtered = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)(sampleCount - 1);
            float envelope = Mathf.Sin(t * Mathf.PI);
            envelope *= envelope;
            float white = ((float)random.NextDouble() * 2f - 1f) * 0.22f;
            filtered = Mathf.Lerp(filtered, white, 0.58f);
            samples[i] = filtered * envelope;
        }

        AudioClip clip = AudioClip.Create("BookHoverRustle", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

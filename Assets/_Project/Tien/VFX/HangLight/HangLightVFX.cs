using UnityEngine;
using UnityEngine.Rendering.Universal;

// HangLightVFX: Controls a dynamic light effect for the hang map.
// Attach this script to a GameObject with a Light2D component.
[RequireComponent(typeof(Light2D))]
public class HangLightVFX : MonoBehaviour
{
    private Light2D _light;

    // Adjustable parameters
    [Header("Light Settings")]
    public Color baseColor = new Color(0.8f, 0.9f, 1f);
    public Color pulseColor = new Color(1f, 1f, 1f);
    public float pulseDuration = 2f; // seconds for a full pulse cycle
    public float intensityMin = 0.8f;
    public float intensityMax = 1.5f;

    [Header("Particle Settings")]
    // Particle system that will act as firefly swarm
    public ParticleSystem fireflies; // Assign a ParticleSystem prefab for fireflies
    public Gradient fireflyColorGradient = new Gradient
    {
        colorKeys = new GradientColorKey[] {
            new GradientColorKey(new Color(0.4f, 0.8f, 1f), 0f),   // light blue
            new GradientColorKey(Color.white, 0.5f),           // white
            new GradientColorKey(new Color(1f, 0.9f, 0.5f), 1f) // warm yellow
        },
        alphaKeys = new GradientAlphaKey[] {
            new GradientAlphaKey(0f, 0f),
            new GradientAlphaKey(1f, 0.2f),
            new GradientAlphaKey(0f, 1f)
        }
    };
    public float fireflyEmissionRate = 20f; // particles per second
    public float fireflyLifetime = 3f; // seconds each firefly lives
    public Vector3 fireflyAreaSize = new Vector3(5f, 5f, 1f); // size of emission box
    public ParticleSystem sparkles; // Assign a particle system for sparkles
    public float sparkleInterval = 3f; // seconds between spark bursts
    private float _sparkleTimer = 0f;

    void Awake()
    {
        _light = GetComponent<Light2D>();
        _light.color = baseColor;
        _light.intensity = intensityMin;
        // Configure firefly particle system if assigned
        if (fireflies != null)
        {
            var main = fireflies.main;
            main.startColor = fireflyColorGradient;
            main.startLifetime = fireflyLifetime;
            main.startSpeed = 0.5f;
            main.startSize = 0.1f;
            main.duration = fireflyLifetime;
            var emission = fireflies.emission;
            emission.rateOverTime = fireflyEmissionRate;
            var shape = fireflies.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(fireflyAreaSize.x, fireflyAreaSize.y, fireflyAreaSize.z);
            var noise = fireflies.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.2f;
        }
    }

    void Update()
    {
        // Pulse the light intensity and color
        float t = (Mathf.Sin(Time.time * (2 * Mathf.PI / pulseDuration)) + 1f) * 0.5f; // 0..1
        _light.intensity = Mathf.Lerp(intensityMin, intensityMax, t);
        _light.color = Color.Lerp(baseColor, pulseColor, t);

        // Trigger sparkles (optional) periodically
        if (sparkles != null)
        {
            _sparkleTimer += Time.deltaTime;
            if (_sparkleTimer >= sparkleInterval)
            {
                sparkles.Play();
                _sparkleTimer = 0f;
            }
        }
        // Fireflies are emitted automatically by the particle system; no manual trigger needed.
    }
}

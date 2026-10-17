using UnityEngine;

public class PlinkoAtmosphere : MonoBehaviour
{
    [SerializeField] private int snowflakeCount = 32;
    [SerializeField] private Vector3 areaCenter = new Vector3(-33f, 16f, 0f);
    [SerializeField] private Vector3 areaSize = new Vector3(24f, 24f, 1f);
    [SerializeField] private float fallSpeed = 0.7f;
    [SerializeField] private Color snowColor = new Color(0.65f, 0.9f, 1f, 0.32f);

    private GameObject snowObject;
    private Material snowMaterial;

    private void Awake()
    {
        int count = Mathf.Max(1, snowflakeCount);
        float width = Mathf.Max(0.01f, areaSize.x);
        float height = Mathf.Max(0.01f, areaSize.y);
        float depth = Mathf.Max(0.01f, areaSize.z);
        float speed = Mathf.Max(0.01f, fallSpeed);

        snowObject = new GameObject("PlinkoSnowfall");
        snowObject.transform.SetParent(transform, false);

        var particles = snowObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;
        float lifetime = height / speed;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.85f, lifetime * 1.15f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor = snowColor;
        main.gravityModifier = 0f;
        main.prewarm = true;

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = count / Mathf.Max(0.01f, lifetime);

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = areaCenter;
        shape.scale = new Vector3(width, height, depth);

        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(-speed);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);

        var renderer = snowObject.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = -10;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            snowMaterial = new Material(shader)
            {
                name = "PlinkoSnowMaterial",
                color = Color.white
            };
            renderer.material = snowMaterial;
        }

        particles.Play();
    }

    private void OnDestroy()
    {
        if (snowObject != null)
            Destroy(snowObject);
        if (snowMaterial != null)
            Destroy(snowMaterial);
    }
}

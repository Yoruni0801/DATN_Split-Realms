using UnityEngine;

[CreateAssetMenu(fileName = "Wind Tornado Config", menuName = "Duy/Game Data/Skill/Configs/Wind Tornado")]
public class WindTornadoSkillConfig : SkillExecutionConfig
{
    [SerializeField] private GameObject tornadoPrefab;
    [SerializeField, Min(0.05f)] private float duration = 1.5f;
    [SerializeField, Min(0.01f)] private float radius = 1.75f;
    [SerializeField, Min(0.1f)] private float targetSearchRadius = 12f;
    [SerializeField, Min(1)] private int damagePerTick = 4;
    [SerializeField, Min(0.01f)] private float damageTickInterval = 0.25f;
    [SerializeField, Min(0f)] private float liftVelocity = 4f;
    [SerializeField, Min(0f)] private float spiralAngularSpeed = 360f;
    [SerializeField, Min(0f)] private float spiralPullSpeed = 5f;
    [SerializeField, Min(0f)] private float minimumOrbitRadius = 0.35f;
    [SerializeField, Min(0f)] private float releaseTangentVelocity = 8f;
    [SerializeField, Min(0f)] private float releaseUpVelocity = 3f;
    [SerializeField, Min(0.1f)] private float travelSpeed = 8f;
    [SerializeField, Min(0f)] private float spawnForwardOffset = 1f;
    [SerializeField, Min(0f)] private float spawnHeightOffset = 0.5f;
    [SerializeField, Min(0.01f)] private float hitStopDistance = 0.6f;
    [SerializeField, Min(0f)] private float groundProbeHeight = 2f;
    [SerializeField, Min(0.1f)] private float groundProbeDistance = 5f;
    [SerializeField, Min(0f)] private float groundOffset = 0.1f;
    [SerializeField, Range(0f, 60f)] private float maxTiltAngle = 30f;
    [SerializeField, Range(0f, 20f)] private float wobbleAngle = 6f;
    [SerializeField, Min(0f)] private float wobbleFrequency = 8f;
    [SerializeField] private bool flipEachRevolution = true;
    [SerializeField] private Vector2 tornadoCenterOffset;
    [SerializeField] private Vector3 tornadoLocalEuler;
    [SerializeField] private Vector3 tornadoLocalScale = Vector3.one;
    [SerializeField, Min(0f)] private float vfxStopDelay = 0.1f;

    public override SkillAttackType AttackType => SkillAttackType.WindTornado;
    public GameObject TornadoPrefab => tornadoPrefab;
    public float Duration => duration;
    public float Radius => radius;
    public float TargetSearchRadius => targetSearchRadius;
    public int DamagePerTick => damagePerTick;
    public float DamageTickInterval => damageTickInterval;
    public float LiftVelocity => liftVelocity;
    public float SpiralAngularSpeed => spiralAngularSpeed;
    public float SpiralPullSpeed => spiralPullSpeed;
    public float MinimumOrbitRadius => minimumOrbitRadius;
    public float ReleaseTangentVelocity => releaseTangentVelocity;
    public float ReleaseUpVelocity => releaseUpVelocity;
    public float TravelSpeed => travelSpeed;
    public float SpawnForwardOffset => spawnForwardOffset;
    public float SpawnHeightOffset => spawnHeightOffset;
    public float HitStopDistance => hitStopDistance;
    public float GroundProbeHeight => groundProbeHeight;
    public float GroundProbeDistance => groundProbeDistance;
    public float GroundOffset => groundOffset;
    public float MaxTiltAngle => maxTiltAngle;
    public float WobbleAngle => wobbleAngle;
    public float WobbleFrequency => wobbleFrequency;
    public bool FlipEachRevolution => flipEachRevolution;
    public Vector2 TornadoCenterOffset => tornadoCenterOffset;
    public Vector3 TornadoLocalEuler => tornadoLocalEuler;
    public Vector3 TornadoLocalScale => tornadoLocalScale;
    public float VfxStopDelay => vfxStopDelay;
}

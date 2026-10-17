using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class WindTornadoSkillRunner : MonoBehaviour
{
    private sealed class TornadoTargetState
    {
        public Collider2D Collider;
        public Transform Transform;
        public Rigidbody2D Body;
        public IEnemyTarget EnemyTarget;
        public Vector3 OriginalLocalEuler;
        public Vector3 OriginalLocalScale;
        public float Angle;
        public float OrbitRadius;
        public float HeightOffset;
        public int LastHalfTurn;
    }

    private readonly HashSet<int> damagedTargetsThisTick = new HashSet<int>();
    private readonly Dictionary<int, TornadoTargetState> affectedTargets = new Dictionary<int, TornadoTargetState>();
    private Coroutine routine;
    private GameObject activeTornadoObject;
    private WindTornadoSkillConfig activeConfig;
    private Player activeOwner;

    public void Begin(
        Player owner,
        SkillData skill,
        WindTornadoSkillConfig config,
        Vector3 start,
        Vector3 target,
        LayerMask targetLayer)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            Finish(releaseTargets: true);
        }

        activeConfig = config;
        activeOwner = owner;
        activeOwner?.ShowSkillPresentation(skill, triggerSkillCollider: false);
        routine = StartCoroutine(Run(config, start, target, targetLayer));
    }

    private IEnumerator Run(WindTornadoSkillConfig config, Vector3 start, Vector3 target, LayerMask targetLayer)
    {
        Vector3 center = ResolveGroundedCenter(start, config);
        SpawnTornadoVfx(config, center);

        float duration = Mathf.Max(0.05f, config.Duration);
        float damageInterval = Mathf.Max(0.01f, config.DamageTickInterval);
        float totalTimer = 0f;
        float effectTimer = 0f;
        float travelTimeout = Mathf.Max(0.25f, config.TargetSearchRadius / Mathf.Max(0.1f, config.TravelSpeed) + 0.5f);
        float damageTimer = 0f;
        bool stoppedOnTarget = false;

        while (effectTimer < duration && totalTimer < travelTimeout + duration)
        {
            if (!stoppedOnTarget)
            {
                center = MoveCenterTowardTarget(config, center, target);
                UpdateTornadoVfxPosition(center);
            }

            CaptureTargets(config, center, targetLayer);
            if (!stoppedOnTarget &&
                (affectedTargets.Count > 0 || IsCloseEnoughToTarget(config, center, target)))
            {
                stoppedOnTarget = true;
            }

            bool shouldDamage = stoppedOnTarget && damageTimer <= 0f;
            ApplyTornadoEffects(config, center, targetLayer, shouldDamage);
            if (shouldDamage)
            {
                damageTimer = damageInterval;
            }

            totalTimer += Time.deltaTime;
            if (stoppedOnTarget)
            {
                effectTimer += Time.deltaTime;
                damageTimer -= Time.deltaTime;
            }

            yield return null;
        }

        Finish(releaseTargets: true);
    }

    private void SpawnTornadoVfx(WindTornadoSkillConfig config, Vector3 center)
    {
        if (config.TornadoPrefab == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(config.TornadoLocalEuler) * config.TornadoPrefab.transform.rotation;
        activeTornadoObject = Instantiate(config.TornadoPrefab, center, rotation);
        activeTornadoObject.transform.localScale = Vector3.Scale(
            activeTornadoObject.transform.localScale,
            config.TornadoLocalScale);
    }

    private void UpdateTornadoVfxPosition(Vector3 center)
    {
        if (activeTornadoObject != null)
        {
            activeTornadoObject.transform.position = center;
        }
    }

    private static bool IsCloseEnoughToTarget(WindTornadoSkillConfig config, Vector3 center, Vector3 target)
    {
        float distance = Mathf.Max(0.01f, config.HitStopDistance);
        return ((Vector2)center - (Vector2)target).sqrMagnitude <= distance * distance;
    }

    private static Vector3 MoveCenterTowardTarget(WindTornadoSkillConfig config, Vector3 center, Vector3 target)
    {
        float step = Mathf.Max(0.1f, config.TravelSpeed) * Time.deltaTime;
        float nextX = Mathf.MoveTowards(center.x, target.x, step);
        float nextY = ResolveGroundY(nextX, center.y, config, out bool foundGround)
            + config.TornadoCenterOffset.y;

        if (!foundGround)
        {
            nextY = Mathf.MoveTowards(center.y, target.y, step);
        }

        return new Vector3(nextX, nextY, center.z);
    }

    private static Vector3 ResolveGroundedCenter(Vector3 position, WindTornadoSkillConfig config)
    {
        float y = ResolveGroundY(position.x, position.y, config, out bool foundGround);
        if (foundGround)
        {
            position.y = y + config.TornadoCenterOffset.y;
        }

        return position;
    }

    private static float ResolveGroundY(
        float x,
        float currentY,
        WindTornadoSkillConfig config,
        out bool foundGround)
    {
        float probeHeight = Mathf.Max(0f, config.GroundProbeHeight);
        float probeDistance = Mathf.Max(0.1f, config.GroundProbeDistance);
        Vector2 origin = new Vector2(x, currentY + probeHeight);
        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            Vector2.down,
            probeHeight + probeDistance,
            PlayerLayerConfig.EnvironmentLayer);

        foundGround = hit.collider != null;
        return foundGround ? hit.point.y + config.GroundOffset : currentY;
    }

    private void ApplyTornadoEffects(
        WindTornadoSkillConfig config,
        Vector3 center,
        LayerMask targetLayer,
        bool shouldDamage)
    {
        if (shouldDamage)
        {
            damagedTargetsThisTick.Clear();
        }

        List<int> invalidTargets = null;
        foreach (KeyValuePair<int, TornadoTargetState> item in affectedTargets)
        {
            TornadoTargetState target = item.Value;
            if (target == null || target.Collider == null || target.Transform == null ||
                !FairyBeamSkillExecutor.IsValidEnemyTarget(target.Collider))
            {
                if (invalidTargets == null)
                {
                    invalidTargets = new List<int>();
                }

                invalidTargets.Add(item.Key);
                continue;
            }

            ApplySpiralMotion(target, config, center);

            if (!shouldDamage)
            {
                continue;
            }

            if (!damagedTargetsThisTick.Add(item.Key))
            {
                continue;
            }

            PlayerHitResolver.TryApplyDamageToEnemy(target.Collider, Mathf.Max(1, config.DamagePerTick), activeOwner != null ? activeOwner.transform : null);
        }

        if (invalidTargets == null)
        {
            return;
        }

        for (int i = 0; i < invalidTargets.Count; i++)
        {
            affectedTargets.Remove(invalidTargets[i]);
        }
    }

    private void CaptureTargets(WindTornadoSkillConfig config, Vector3 center, LayerMask targetLayer)
    {
        Collider2D[] hits = targetLayer.value != 0
            ? Physics2D.OverlapCircleAll(center, config.Radius, targetLayer)
            : Physics2D.OverlapCircleAll(center, config.Radius);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (!FairyBeamSkillExecutor.IsValidEnemyTarget(hit))
            {
                continue;
            }

            int targetId = hit.transform.root.GetInstanceID();
            if (affectedTargets.ContainsKey(targetId))
            {
                continue;
            }

            TornadoTargetState state = CreateTargetState(hit, center);
            if (state != null)
            {
                affectedTargets.Add(targetId, state);
            }
        }
    }

    private static TornadoTargetState CreateTargetState(Collider2D hit, Vector3 center)
    {
        if (PlayerHitResolver.IsThreatSensorCollider(hit))
        {
            return null;
        }

        IEnemyTarget enemyTarget = hit.GetComponentInParent<IEnemyTarget>();
        Transform targetTransform = enemyTarget != null && enemyTarget.Transform != null
            ? enemyTarget.Transform
            : hit.transform.root;
        if (targetTransform == null)
        {
            return null;
        }

        Vector2 offset = (Vector2)targetTransform.position - (Vector2)center;
        float orbitRadius = Mathf.Max(0.1f, offset.magnitude);
        return new TornadoTargetState
        {
            Collider = hit,
            Transform = targetTransform,
            Body = hit.attachedRigidbody != null ? hit.attachedRigidbody : targetTransform.GetComponentInParent<Rigidbody2D>(),
            EnemyTarget = enemyTarget,
            OriginalLocalEuler = targetTransform.localEulerAngles,
            OriginalLocalScale = targetTransform.localScale,
            Angle = Mathf.Atan2(offset.y, offset.x),
            OrbitRadius = orbitRadius,
            HeightOffset = 0f,
            LastHalfTurn = Mathf.FloorToInt(Mathf.Atan2(offset.y, offset.x) / Mathf.PI)
        };
    }

    private static void ApplySpiralMotion(TornadoTargetState target, WindTornadoSkillConfig config, Vector3 center)
    {
        float angularSpeed = config.SpiralAngularSpeed * Mathf.Deg2Rad;
        target.Angle += angularSpeed * Time.deltaTime;
        target.OrbitRadius = Mathf.MoveTowards(
            target.OrbitRadius,
            Mathf.Max(0.01f, config.MinimumOrbitRadius),
            config.SpiralPullSpeed * Time.deltaTime);
        target.HeightOffset += config.LiftVelocity * Time.deltaTime;

        Vector2 orbitOffset = new Vector2(Mathf.Cos(target.Angle), Mathf.Sin(target.Angle)) * target.OrbitRadius;
        Vector2 desiredPosition = (Vector2)center + orbitOffset + Vector2.up * target.HeightOffset;
        Vector2 currentPosition = target.Body != null
            ? target.Body.position
            : (Vector2)target.Transform.position;
        float follow = 1f - Mathf.Exp(-10f * Time.deltaTime);
        Vector2 smoothedPosition = Vector2.Lerp(currentPosition, desiredPosition, follow);
        Vector2 velocity = (smoothedPosition - currentPosition) / Mathf.Max(Time.deltaTime, 0.0001f);

        if (target.EnemyTarget != null && target.EnemyTarget.IsAlive)
        {
            target.EnemyTarget.ApplyVelocity(velocity, forceDynamicBody: true);
        }
        else if (target.Body != null)
        {
            target.Body.linearVelocity = velocity;
        }
        else
        {
            target.Transform.position = Vector3.Lerp(target.Transform.position, desiredPosition, 0.35f);
        }

        ApplySpriteTiltAndFlip(target, config);
    }

    private static void ApplySpriteTiltAndFlip(TornadoTargetState target, WindTornadoSkillConfig config)
    {
        float tangentSign = config.SpiralAngularSpeed >= 0f ? 1f : -1f;
        float wobble = Mathf.Sin(Time.time * config.WobbleFrequency + target.Angle) * config.WobbleAngle;
        float tilt = Mathf.Clamp(config.MaxTiltAngle * tangentSign + wobble, -35f, 35f);
        Vector3 euler = target.OriginalLocalEuler;
        euler.z = target.OriginalLocalEuler.z + tilt;
        target.Transform.localEulerAngles = euler;

        if (!config.FlipEachRevolution)
        {
            return;
        }

        int halfTurn = Mathf.FloorToInt(target.Angle / Mathf.PI);
        if (halfTurn == target.LastHalfTurn)
        {
            return;
        }

        target.LastHalfTurn = halfTurn;
        Vector3 scale = target.Transform.localScale;
        scale.x = -scale.x;
        target.Transform.localScale = scale;
    }

    private void ReleaseAffectedTargets(WindTornadoSkillConfig config)
    {
        foreach (KeyValuePair<int, TornadoTargetState> item in affectedTargets)
        {
            TornadoTargetState target = item.Value;
            if (target == null || target.Transform == null)
            {
                continue;
            }

            target.Transform.localEulerAngles = target.OriginalLocalEuler;
            target.Transform.localScale = target.OriginalLocalScale;

            Vector2 tangent = new Vector2(-Mathf.Sin(target.Angle), Mathf.Cos(target.Angle));
            if (config.SpiralAngularSpeed < 0f)
            {
                tangent = -tangent;
            }

            Vector2 releaseVelocity = tangent.normalized * config.ReleaseTangentVelocity +
                                      Vector2.up * config.ReleaseUpVelocity;

            if (target.EnemyTarget != null && target.EnemyTarget.IsAlive)
            {
                target.EnemyTarget.ApplyVelocity(releaseVelocity, forceDynamicBody: true);
            }
            else if (target.Body != null)
            {
                target.Body.linearVelocity = releaseVelocity;
            }
        }

        affectedTargets.Clear();
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        Finish(releaseTargets: true);
    }

    private void Finish(bool releaseTargets)
    {
        if (releaseTargets && activeConfig != null)
        {
            ReleaseAffectedTargets(activeConfig);
        }
        else
        {
            affectedTargets.Clear();
        }

        Cleanup();
        activeConfig = null;
        activeOwner?.HideSkillPresentation();
        activeOwner = null;
        routine = null;
    }

    private void Cleanup()
    {
        if (activeTornadoObject == null)
        {
            return;
        }

        Destroy(activeTornadoObject);
        activeTornadoObject = null;
    }
}

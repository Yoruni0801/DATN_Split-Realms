using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider2D))]
public sealed class FireCleaveTimedHazard : MonoBehaviour
{
    [Header("Particle Loop")]
    [FormerlySerializedAs("activeDuration")]
    [SerializeField, Min(0.01f)] private float particleRunDuration = 1f;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool loopHazard = true;
    [SerializeField, Min(0f)] private float loopDelay = 0f;

    [Header("Damage")]
    [SerializeField, Min(0)] private int damagePerTick = 10;
    [SerializeField, Min(0.01f)] private float damageInterval = 0.25f;
    [SerializeField] private LayerMask playerLayer = ~0;
    [SerializeField] private bool canBeBlocked = true;

    private readonly Dictionary<PlayerHealth, float> lastHitTimes = new Dictionary<PlayerHealth, float>();
    private ParticleSystem[] particles;
    private Collider2D hitbox;
    private Coroutine loopRoutine;
    private bool active;
    private bool stopRequested;
    private readonly List<Collider2D> overlappingColliders = new List<Collider2D>();
    private ContactFilter2D overlapFilter;

    public float ActiveDuration
    {
        get => particleRunDuration;
        set => particleRunDuration = Mathf.Max(0.01f, value);
    }

    private void Awake()
    {
        hitbox = GetComponent<Collider2D>();
        hitbox.isTrigger = true;
        particles = GetComponentsInChildren<ParticleSystem>(true);
        overlapFilter = new ContactFilter2D
        {
            useTriggers = true
        };
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            PlayHazard();
        }
    }

    private void OnDisable()
    {
        StopHazard();
    }

    public void PlayHazard()
    {
        stopRequested = false;

        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
        }

        loopRoutine = StartCoroutine(PlayHazardLoop());
    }

    public void StopHazard()
    {
        stopRequested = true;
        active = false;
        lastHitTimes.Clear();

        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        StopParticles();

        if (hitbox != null)
        {
            hitbox.enabled = false;
        }
    }

    private IEnumerator PlayHazardLoop()
    {
        if (hitbox != null)
        {
            hitbox.enabled = true;
        }

        while (!stopRequested && isActiveAndEnabled)
        {
            PlayParticles();
            active = true;
            yield return new WaitForSeconds(particleRunDuration);

            active = false;
            StopParticles();

            if (!loopHazard || stopRequested || !isActiveAndEnabled)
            {
                break;
            }

            if (loopDelay > 0f)
            {
                yield return new WaitForSeconds(loopDelay);
            }
        }

        active = false;
        loopRoutine = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamageTouch(other, forceImmediate: true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamageTouch(other, forceImmediate: false);
    }

    private void FixedUpdate()
    {
        if (!active || hitbox == null || !hitbox.enabled)
        {
            return;
        }

        overlappingColliders.Clear();
        hitbox.Overlap(overlapFilter, overlappingColliders);

        for (int i = 0; i < overlappingColliders.Count; i++)
        {
            TryDamageTouch(overlappingColliders[i], forceImmediate: false);
        }
    }

    private void TryDamageTouch(Collider2D other, bool forceImmediate)
    {
        if (!active || other == null || damagePerTick <= 0)
        {
            return;
        }

        if (PlayerHitResolver.IsThreatSensorCollider(other))
        {
            return;
        }

        if ((playerLayer.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null)
        {
            return;
        }

        float now = Time.time;
        if (!forceImmediate && lastHitTimes.TryGetValue(health, out float lastHitTime) && now - lastHitTime < damageInterval)
        {
            return;
        }

        Vector2 incomingDirection = (Vector2)other.transform.position - (Vector2)transform.position;
        IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
            other,
            damagePerTick,
            ParryableAttackType.Melee,
            transform,
            incomingDirection,
            canBeBlocked);

        if (resolution == IncomingAttackResolution.Ignored)
        {
            health.TakeDamage(damagePerTick, transform);
        }

        lastHitTimes[health] = now;
    }

    private void PlayParticles()
    {
        if (particles == null)
        {
            return;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
            {
                continue;
            }

            particle.gameObject.SetActive(true);
            particle.Clear(true);
            particle.Play(true);
        }
    }

    private void StopParticles()
    {
        if (particles == null)
        {
            return;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle != null)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void OnValidate()
    {
        particleRunDuration = Mathf.Max(0.01f, particleRunDuration);
        damageInterval = Mathf.Max(0.01f, damageInterval);
        damagePerTick = Mathf.Max(0, damagePerTick);
        loopDelay = Mathf.Max(0f, loopDelay);
    }
}

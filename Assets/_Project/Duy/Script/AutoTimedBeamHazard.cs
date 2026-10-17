using System.Collections.Generic;
using UnityEngine;

namespace Duy
{
    [DisallowMultipleComponent]
    public class AutoTimedBeamHazard : MonoBehaviour
    {
        private enum HazardState
        {
            Idle,
            Telegraph,
            Firing
        }

        [Header("Beam Prefabs (2D)")]
        [SerializeField] private GameObject beamLineRendererPrefab;
        [SerializeField] private GameObject beamStartPrefab;
        [SerializeField] private GameObject beamEndPrefab;
        [SerializeField] private GameObject telegraphLinePrefab;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float telegraphDuration = 0.5f;
        [SerializeField, Min(0.01f)] private float activeDuration = 2f;
        [SerializeField, Min(0f)] private float loopDelay = 2f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnEnable = true;

        [Header("2D Platformer Alignment")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private Vector2 direction = Vector2.right;
        [SerializeField, Min(0.1f)] private float maxDistance = 20f;
        [SerializeField, Min(0.05f)] private float beamWidth = 0.4f;
        [SerializeField] private float beamEndOffset = 0.1f;
        [SerializeField] private float textureScrollSpeed = 8f;
        [SerializeField] private float textureLengthScale = 3f;

        [Header("2D Collision & Obstacles")]
        [SerializeField] private LayerMask obstacleLayer = 0;
        [SerializeField] private LayerMask targetLayer = ~0;

        [Header("Damage")]
        [SerializeField, Min(0)] private int damagePerTick = 10;
        [SerializeField, Min(0.01f)] private float damageInterval = 0.25f;
        [SerializeField] private bool canBeBlocked = true;

        [Header("2D Rendering Sorting")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 5;

        private GameObject beamInstance;
        private GameObject beamStartInstance;
        private GameObject beamEndInstance;
        private GameObject telegraphInstance;
        private LineRenderer beamLine;
        private LineRenderer telegraphLine;

        private readonly Dictionary<PlayerHealth, float> lastHitTimes = new Dictionary<PlayerHealth, float>();
        private readonly List<Collider2D> detectedColliders = new List<Collider2D>();
        private HazardState currentState = HazardState.Idle;
        private float stateTimer;
        private float nextTickTimer;

        public bool IsFiring => currentState == HazardState.Firing;

        private void OnEnable()
        {
            if (playOnEnable)
            {
                StartHazard();
            }
        }

        private void OnDisable()
        {
            StopHazard();
        }

        public void StartHazard()
        {
            stateTimer = 0f;
            TransitionTo(telegraphDuration > 0f ? HazardState.Telegraph : HazardState.Firing);
        }

        public void StopHazard()
        {
            TransitionTo(HazardState.Idle);
            stateTimer = 0f;
        }

        private void Update()
        {
            stateTimer += Time.deltaTime;

            switch (currentState)
            {
                case HazardState.Idle:
                    if (loop && stateTimer >= loopDelay)
                    {
                        stateTimer = 0f;
                        TransitionTo(telegraphDuration > 0f ? HazardState.Telegraph : HazardState.Firing);
                    }
                    break;

                case HazardState.Telegraph:
                    UpdateTelegraphVisual();
                    if (stateTimer >= telegraphDuration)
                    {
                        stateTimer = 0f;
                        TransitionTo(HazardState.Firing);
                    }
                    break;

                case HazardState.Firing:
                    UpdateBeamVisual();
                    TickDamage2D();
                    if (stateTimer >= activeDuration)
                    {
                        stateTimer = 0f;
                        TransitionTo(HazardState.Idle);
                    }
                    break;
            }
        }

        private void TransitionTo(HazardState newState)
        {
            currentState = newState;
            lastHitTimes.Clear();
            nextTickTimer = 0f;

            CleanupAllInstances();

            if (currentState == HazardState.Telegraph)
            {
                CreateTelegraph();
            }
            else if (currentState == HazardState.Firing)
            {
                CreateBeam();
            }
        }

        private void CreateTelegraph()
        {
            if (telegraphLinePrefab != null)
            {
                telegraphInstance = Instantiate(telegraphLinePrefab, transform);
                telegraphLine = telegraphInstance.GetComponent<LineRenderer>();
                ApplySorting(telegraphLine);
            }
        }

        private void CreateBeam()
        {
            if (beamLineRendererPrefab != null)
            {
                beamInstance = Instantiate(beamLineRendererPrefab, transform);
                beamLine = beamInstance.GetComponent<LineRenderer>();
                ApplySorting(beamLine);
            }

            if (beamStartPrefab != null)
            {
                beamStartInstance = Instantiate(beamStartPrefab, transform);
                ApplySpriteSorting(beamStartInstance);
            }

            if (beamEndPrefab != null)
            {
                beamEndInstance = Instantiate(beamEndPrefab, transform);
                ApplySpriteSorting(beamEndInstance);
            }
        }

        private void CleanupAllInstances()
        {
            if (beamInstance != null) Destroy(beamInstance);
            if (beamStartInstance != null) Destroy(beamStartInstance);
            if (beamEndInstance != null) Destroy(beamEndInstance);
            if (telegraphInstance != null) Destroy(telegraphInstance);

            beamInstance = null;
            beamStartInstance = null;
            beamEndInstance = null;
            telegraphInstance = null;
            beamLine = null;
            telegraphLine = null;
        }

        private void ApplySorting(LineRenderer lr)
        {
            if (lr == null) return;
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                lr.sortingLayerName = sortingLayerName;
            }
            lr.sortingOrder = sortingOrder;
        }

        private void ApplySpriteSorting(GameObject go)
        {
            if (go == null) return;
            SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                if (!string.IsNullOrEmpty(sortingLayerName)) sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder;
            }
        }

        private Vector2 GetFireDirection()
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }
            return (Vector2)transform.right;
        }

        private Vector3 GetFireOrigin()
        {
            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            origin.z = 0f;
            return origin;
        }

        private float CalculateBeamLength(Vector2 origin, Vector2 dir, out RaycastHit2D obstacleHit)
        {
            obstacleHit = default;
            if (obstacleLayer != 0)
            {
                obstacleHit = Physics2D.Raycast(origin, dir, maxDistance, obstacleLayer);
                if (obstacleHit.collider != null)
                {
                    return Mathf.Max(0.1f, obstacleHit.distance);
                }
            }
            return maxDistance;
        }

        private void UpdateTelegraphVisual()
        {
            if (telegraphLine == null) return;

            Vector3 start = GetFireOrigin();
            Vector2 dir = GetFireDirection();
            float length = CalculateBeamLength(start, dir, out _);
            Vector3 end = start + (Vector3)(dir * length);

            telegraphLine.positionCount = 2;
            telegraphLine.SetPosition(0, start);
            telegraphLine.SetPosition(1, end);
        }

        private void UpdateBeamVisual()
        {
            Vector3 start = GetFireOrigin();
            Vector2 dir = GetFireDirection();
            float length = CalculateBeamLength(start, dir, out RaycastHit2D obstacleHit);

            float visualLength = obstacleHit.collider != null
                ? Mathf.Max(0.1f, length - beamEndOffset)
                : length;

            Vector3 end = start + (Vector3)(dir * visualLength);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Quaternion rot2D = Quaternion.Euler(0f, 0f, angle);

            if (beamLine != null)
            {
                beamLine.positionCount = 2;
                beamLine.SetPosition(0, start);
                beamLine.SetPosition(1, end);

                if (beamLine.sharedMaterial != null)
                {
                    beamLine.sharedMaterial.mainTextureScale = new Vector2(visualLength / textureLengthScale, 1f);
                    beamLine.sharedMaterial.mainTextureOffset -= new Vector2(Time.deltaTime * textureScrollSpeed, 0f);
                }
            }

            if (beamStartInstance != null)
            {
                beamStartInstance.transform.SetPositionAndRotation(start, rot2D);
            }

            if (beamEndInstance != null)
            {
                beamEndInstance.transform.SetPositionAndRotation(end, Quaternion.Euler(0f, 0f, angle + 180f));
            }
        }

        private void TickDamage2D()
        {
            if (damagePerTick <= 0) return;

            nextTickTimer += Time.deltaTime;
            if (nextTickTimer < damageInterval) return;
            nextTickTimer = 0f;

            Vector2 start = GetFireOrigin();
            Vector2 dir = GetFireDirection();
            float length = CalculateBeamLength(start, dir, out _);

            Vector2 boxCenter = start + (dir * (length * 0.5f));
            Vector2 boxSize = new Vector2(length, beamWidth);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, angle, targetLayer);
            if (hits == null || hits.Length == 0) return;

            float now = Time.time;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D other = hits[i];
                if (other == null || PlayerHitResolver.IsThreatSensorCollider(other)) continue;

                PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
                if (health == null)
                {
                    PlayerHitResolver.TryApplyDamageToEnemy(other, damagePerTick, transform);
                    continue;
                }

                if (lastHitTimes.TryGetValue(health, out float lastHit) && now - lastHit < damageInterval)
                {
                    continue;
                }

                Vector2 incomingDirection = (Vector2)other.transform.position - start;
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
        }

        private void OnValidate()
        {
            telegraphDuration = Mathf.Max(0f, telegraphDuration);
            activeDuration = Mathf.Max(0.01f, activeDuration);
            loopDelay = Mathf.Max(0f, loopDelay);
            maxDistance = Mathf.Max(0.1f, maxDistance);
            beamWidth = Mathf.Max(0.05f, beamWidth);
            damageInterval = Mathf.Max(0.01f, damageInterval);
            damagePerTick = Mathf.Max(0, damagePerTick);
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 start = GetFireOrigin();
            Vector2 dir = GetFireDirection();
            float length = Application.isPlaying
                ? CalculateBeamLength(start, dir, out _)
                : maxDistance;

            Vector3 center = start + (Vector3)(dir * (length * 0.5f));
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0, 0, angle), Vector3.one);

            Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(length, beamWidth, 0.1f));

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(length, beamWidth, 0.1f));

            Gizmos.matrix = prevMatrix;
        }
    }
}

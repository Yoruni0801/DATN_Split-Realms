using System.Collections;
using System.Collections.Generic;
using EnemySystem;
using UnityEngine;

namespace Bao.Bosses.Ashwing
{
    public enum AshwingSkill { CinderRazor, FeatherFan, HuntingDash, Ashfall }
    public enum AshwingBeat { Dormant, Hover, Windup, Release, Dash, Recovery, Stagger, Transition, Dead }

    /// <summary>Opt-in boss brain. The original component still owns health, parry, loot and Timeline events.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StandaloneFlyingShooterEnemy), typeof(AshwingBossVFX))]
    public sealed class AshwingBossCombat : MonoBehaviour
    {
        [Header("Arena / navigation")]
        [SerializeField, Min(4)] private float arenaHalfWidth = 21f;
        [SerializeField] private float arenaCenterOffsetX = -8f;
        [SerializeField, Min(1)] private float engageDistance = 26f;
        [SerializeField] private float preferredDistance = 7f;
        [SerializeField] private float hoverSpeed = 3.5f;
        [SerializeField] private LayerMask playerLayer = 1 << 6;
        [SerializeField] private LayerMask groundLayer = (1 << 3) | (1 << 18);
        [Header("Cinder razor / feather fan")]
        [SerializeField] private AshwingBossFeather featherPrefab;
        [SerializeField] private float snipeWindup = .62f;
        [SerializeField] private float fanWindup = .82f;
        [SerializeField] private float projectileSpeed = 18f;
        [SerializeField] private int projectileDamage = 10;
        [SerializeField] private float spreadAngle = 21f;
        [Header("Hunting dash")]
        [SerializeField] private float dashWindup = .85f;
        [SerializeField] private float dashSpeed = 16f;
        [SerializeField] private int dashDamage = 18;
        [SerializeField] private int parryDamage = 16;
        [SerializeField] private float staggerDuration = 1.35f;
        [Header("Ashfall / second phase")]
        [SerializeField, Range(.1f, .9f)] private float enrageHealthFraction = .5f;
        [SerializeField] private float rainCooldown = 11f;
        [SerializeField] private float rainWarning = 1.15f;
        [SerializeField] private int rainDamage = 12;
        [SerializeField] private float recoveryTime = .8f;

        public AshwingBeat Beat { get; private set; } = AshwingBeat.Dormant;
        public AshwingSkill Skill { get; private set; }
        public bool Enraged { get; private set; }
        public bool IsEngaged { get; private set; }
        public float Charge { get; private set; }
        public Vector2 Aim { get; private set; } = Vector2.left;
        public Vector2 BodyCenter => bodyCollider != null ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;
        public Vector2 Muzzle => boss != null && boss.ShootPoint != null ? (Vector2)boss.ShootPoint.position : BodyCenter;
        public int GroundMask => groundLayer.value;
        public int PlayerMask => playerLayer.value;
        public StandaloneFlyingShooterEnemy Health => boss;
        public AshwingBossVFX VFX => vfx;
        public bool CanHurtPlayer => isActiveAndEnabled && IsEngaged && boss != null && !boss.IsDead && boss.IsAggroEnabled;

        private StandaloneFlyingShooterEnemy boss;
        private AshwingBossVFX vfx;
        private Rigidbody2D body;
        private CircleCollider2D bodyCollider;
        private Animator animator;
        private Transform target;
        private Collider2D targetCollider;
        private PlayerHealth targetHealth;
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];
        private Vector2 home;
        private float flightY, nextAttack, nextRain, staggerUntil;
        private int attackIndex;
        private Coroutine attack;
        private bool initialized, deathHandled, dashing;
        private readonly List<GameObject> ordnance = new List<GameObject>();
        private static readonly WaitForFixedUpdate PhysicsTick = new WaitForFixedUpdate();
        private static readonly int FlyState = Animator.StringToHash("Base Layer.Ashwing_Fly");
        private static readonly int AttackState = Animator.StringToHash("Base Layer.Ashwing_Attack");

        private void Awake()
        {
            boss = GetComponent<StandaloneFlyingShooterEnemy>();
            vfx = GetComponent<AshwingBossVFX>();
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<CircleCollider2D>();
            animator = GetComponent<Animator>();
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (playerLayer.value == 0) playerLayer = LayerMask.GetMask("Player");
        }

        private void Update()
        {
            if (boss == null || boss.IsDead) return;
            ResolveTarget();
            bool eligible = boss.IsAggroEnabled && target != null && (targetHealth == null || !targetHealth.IsDead) &&
                Vector2.Distance(BodyCenter, TargetPoint) < engageDistance;
            if (!eligible)
            {
                if (IsEngaged) SuspendCombat();
                return;
            }
            if (!IsEngaged)
            {
                IsEngaged = true;
                // Capture after the intro: Timeline may have repositioned the whole rig.
                if (!initialized)
                {
                    home = (Vector2)transform.position + Vector2.right * arenaCenterOffsetX;
                    flightY = transform.position.y;
                    initialized = true;
                }
                Beat = AshwingBeat.Hover;
                nextAttack = Time.time + 1.1f;
                vfx.Engage();
                PlayFlight();
            }
            if (Time.time < staggerUntil) return;
            if (!Enraged && boss.CurrentHealth <= boss.MaxHealth * enrageHealthFraction)
            {
                CancelAttack();
                ClearOrdnance();
                Enraged = true;
                attack = StartCoroutine(Transition());
                return;
            }
            if (attack != null || Time.time < nextAttack) return;
            Skill = SelectSkill();
            attack = StartCoroutine(PerformSkill());
        }

        private void FixedUpdate()
        {
            if (boss == null || boss.IsDead) return;
            if (!IsEngaged || Beat != AshwingBeat.Hover || target == null)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }
            float dx = TargetPoint.x - BodyCenter.x;
            Face(dx);
            float desiredX = transform.position.x;
            if (Mathf.Abs(dx) > preferredDistance + 1.5f) desiredX += Mathf.Sign(dx) * hoverSpeed * Time.fixedDeltaTime;
            else if (Mathf.Abs(dx) < preferredDistance - 1.5f) desiredX -= Mathf.Sign(dx) * hoverSpeed * .8f * Time.fixedDeltaTime;
            desiredX = Mathf.Clamp(desiredX, home.x - arenaHalfWidth, home.x + arenaHalfWidth);
            body.MovePosition(new Vector2(desiredX, Mathf.MoveTowards(body.position.y, flightY, Time.fixedDeltaTime * 3f)));
        }

        private void ResolveTarget()
        {
            Transform candidate = PlayerRegistry.Current != null ? PlayerRegistry.Current.transform : null;
            if (candidate == null)
            {
                var hit = Physics2D.OverlapCircle(BodyCenter, engageDistance, playerLayer);
                if (hit != null) candidate = hit.transform;
            }
            if (candidate == target) return;
            target = candidate;
            targetHealth = target != null ? target.GetComponentInParent<PlayerHealth>() : null;
            targetCollider = target != null ? target.GetComponent<Collider2D>() : null;
            if (target != null && targetCollider == null) targetCollider = target.GetComponentInChildren<Collider2D>();
        }

        private Vector2 TargetPoint => targetCollider != null ? (Vector2)targetCollider.bounds.center :
            target != null ? (Vector2)target.position : BodyCenter + Vector2.left * 5;

        private AshwingSkill SelectSkill()
        {
            if (Enraged && Time.time >= nextRain) return AshwingSkill.Ashfall;
            // Teach the three silhouettes before introducing their second-phase variations.
            AshwingSkill result = (AshwingSkill)(attackIndex++ % 3);
            if (result == AshwingSkill.HuntingDash && Mathf.Abs(TargetPoint.x - BodyCenter.x) < 3f)
                return AshwingSkill.FeatherFan;
            return result;
        }

        private IEnumerator PerformSkill()
        {
            body.linearVelocity = Vector2.zero;
            switch (Skill)
            {
                case AshwingSkill.HuntingDash: yield return Dash(); break;
                case AshwingSkill.Ashfall: yield return Rain(); break;
                default: yield return Shoot(); break;
            }
            if (boss.IsDead) yield break;
            Beat = Time.time < staggerUntil ? AshwingBeat.Stagger : AshwingBeat.Recovery;
            Charge = 0;
            vfx.EndTelegraph();
            PlayFlight();
            float recover = Skill == AshwingSkill.HuntingDash ? 1.05f : recoveryTime;
            float until = Mathf.Max(Time.time + recover, staggerUntil);
            while (Time.time < until)
            {
                body.MovePosition(new Vector2(body.position.x, Mathf.MoveTowards(body.position.y, flightY, Time.deltaTime * 2.6f)));
                yield return null;
            }
            Beat = AshwingBeat.Hover;
            nextAttack = Time.time + (Enraged ? .38f : .55f);
            attack = null;
        }

        private IEnumerator Shoot()
        {
            bool fan = Skill == AshwingSkill.FeatherFan;
            Face(TargetPoint.x - BodyCenter.x);
            Aim = (TargetPoint - Muzzle).normalized;
            float duration = fan ? fanWindup : snipeWindup;
            Beat = AshwingBeat.Windup;
            vfx.BeginTelegraph(Skill);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                Charge = Mathf.Clamp01(elapsed / duration);
                // Last 0.2s is committed, so a late dodge works reliably.
                if (elapsed < duration - .2f) Aim = (TargetPoint - Muzzle).normalized;
                vfx.AimTelegraph(Muzzle, Aim, fan ? spreadAngle : 0, Charge);
                yield return null;
            }
            Beat = AshwingBeat.Release;
            vfx.EndTelegraph();
            PlayAttack();
            if (fan)
            {
                for (int i = -1; i <= 1; i++) Fire(Rotate(Aim, i * spreadAngle), projectileSpeed * .9f);
                vfx.Release(Muzzle, Aim, true);
                if (Enraged)
                {
                    yield return new WaitForSeconds(.38f);
                    // Staggered outer feathers preserve the central escape lane.
                    Fire(Rotate(Aim, -spreadAngle * 1.65f), projectileSpeed * .85f);
                    Fire(Rotate(Aim, spreadAngle * 1.65f), projectileSpeed * .85f);
                    vfx.Release(Muzzle, Aim, true);
                }
            }
            else
            {
                Fire(Aim, projectileSpeed * (Enraged ? 1.13f : 1));
                vfx.Release(Muzzle, Aim, false);
            }
            yield return new WaitForSeconds(.22f);
        }

        private IEnumerator Dash()
        {
            float direction = Mathf.Sign(TargetPoint.x - BodyCenter.x);
            if (direction == 0) direction = 1;
            Face(direction);
            Aim = Vector2.right * direction;
            float radius = bodyCollider.radius * Mathf.Abs(transform.lossyScale.x) * .82f;
            Vector2 offset = BodyCenter - body.position;
            var floor = GroundBelow(TargetPoint + Vector2.up * 2, 12);
            float laneY = Mathf.Clamp(TargetPoint.y, flightY + offset.y - 2.5f, flightY + offset.y + 1f);
            if (floor.collider != null) laneY = Mathf.Max(laneY, floor.point.y + radius + .15f);
            float dashY = laneY - offset.y;
            float startY = body.position.y;
            float targetX = Mathf.Clamp(TargetPoint.x + direction * 3.5f, home.x - arenaHalfWidth, home.x + arenaHalfWidth);
            float distance = Mathf.Abs(targetX - body.position.x);
            var wall = SweepGround(new Vector2(BodyCenter.x, laneY), radius, Aim, distance);
            if (wall.collider != null) targetX = body.position.x + direction * Mathf.Max(0, wall.distance - .12f);
            Beat = AshwingBeat.Windup;
            vfx.BeginTelegraph(Skill);
            for (float elapsed = 0; elapsed < dashWindup; elapsed += Time.deltaTime)
            {
                Charge = elapsed / dashWindup;
                body.MovePosition(new Vector2(body.position.x, Mathf.Lerp(startY, dashY, Mathf.SmoothStep(0, 1, Mathf.Clamp01(Charge * 1.7f)))));
                vfx.DashTelegraph(new Vector2(BodyCenter.x, laneY), new Vector2(targetX + offset.x, laneY), radius, Charge);
                yield return null;
            }
            vfx.EndTelegraph();
            vfx.DashLaunch(Aim);
            PlayAttack();
            Beat = AshwingBeat.Dash;
            dashing = true;
            bool hitOnce = false;
            float timeout = 0;
            while (dashing && Mathf.Abs(body.position.x - targetX) > .04f && timeout < 2.5f)
            {
                yield return PhysicsTick;
                timeout += Time.fixedDeltaTime;
                if (!dashing || !CanHurtPlayer) break;
                float x = Mathf.MoveTowards(body.position.x, targetX, dashSpeed * Time.fixedDeltaTime);
                float step = Mathf.Abs(x - body.position.x);
                if (!hitOnce)
                {
                    var hit = Physics2D.CircleCast(BodyCenter, radius, Aim, step, playerLayer);
                    if (hit.collider != null)
                    {
                        var resolution = PlayerHitResolver.TryApplyHitToPlayer(hit.collider, dashDamage,
                            ParryableAttackType.Melee, boss, transform, Aim);
                        hitOnce = resolution != IncomingAttackResolution.Ignored;
                    }
                }
                if (!dashing) break;
                // Sweep scenery each tick as well: moving obstacles can enter a committed lane.
                var obstacle = SweepGround(BodyCenter, radius, Aim, step + .05f);
                if (obstacle.collider != null) break;
                body.MovePosition(new Vector2(x, dashY));
            }
            dashing = false;
            vfx.DashBrake(Aim);
        }

        private IEnumerator Rain()
        {
            Beat = AshwingBeat.Windup;
            vfx.BeginTelegraph(Skill);
            Face(TargetPoint.x - BodyCenter.x);
            Vector2 lockedTarget = TargetPoint;
            for (float elapsed = 0; elapsed < 1f; elapsed += Time.deltaTime)
            {
                Charge = elapsed;
                yield return null;
            }
            vfx.EndTelegraph();
            Beat = AshwingBeat.Release;
            PlayAttack();
            vfx.RainRelease();
            // Four lanes around an explicitly preserved gap at the locked player position.
            float[] offsets = { -5.4f, -2.7f, 2.7f, 5.4f };
            foreach (float offset in offsets)
            {
                float x = lockedTarget.x + offset;
                if (x < home.x - arenaHalfWidth || x > home.x + arenaHalfWidth) continue;
                var ground = GroundBelow(new Vector2(x, lockedTarget.y + 4f), 14);
                if (ground.collider == null) continue;
                var go = new GameObject("Ashwing • Ashfall lane");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                Track(go);
                go.AddComponent<AshwingAshfall>().Initialize(this, ground.point + Vector2.up * .08f,
                    rainWarning + Mathf.Abs(offset) * .05f, rainDamage);
            }
            nextRain = Time.time + rainCooldown;
            // Don't layer a dash onto the active floor hazards.
            yield return new WaitForSeconds(rainWarning + 2.8f);
        }

        private IEnumerator Transition()
        {
            Beat = AshwingBeat.Transition;
            body.linearVelocity = Vector2.zero;
            PlayFlight();
            vfx.PhaseTransition();
            for (float elapsed = 0; elapsed < 1.7f; elapsed += Time.deltaTime)
            {
                Charge = Mathf.Clamp01(elapsed / 1.3f);
                yield return null;
            }
            Charge = 0;
            Beat = AshwingBeat.Hover;
            nextRain = 0;
            nextAttack = Time.time + .65f;
            attack = null;
        }

        private void Fire(Vector2 direction, float speed)
        {
            if (featherPrefab == null) return;
            var feather = Instantiate(featherPrefab, Muzzle, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(feather.gameObject, gameObject.scene);
            Track(feather.gameObject);
            feather.Initialize(this, direction, speed, projectileDamage, 3.5f);
        }

        public void Track(GameObject item) { ordnance.RemoveAll(x => x == null); ordnance.Add(item); }
        private ContactFilter2D GroundFilter => new ContactFilter2D
        {
            useLayerMask = true, layerMask = groundLayer, useTriggers = false
        };
        public bool IsArenaSurface(Collider2D collider)
        {
            // Companion fairies also use solid Wall colliders in Map 6. They are
            // neither cover for projectiles nor a landing surface for Ashfall.
            return collider != null && !collider.isTrigger &&
                (groundLayer.value & (1 << collider.gameObject.layer)) != 0 &&
                collider.GetComponentInParent<FairyPlayerCompanionController>() == null;
        }
        public RaycastHit2D GroundBelow(Vector2 origin, float distance)
        {
            int count = Physics2D.Raycast(origin, Vector2.down, GroundFilter, groundHits, distance);
            for (int i = 0; i < count; i++)
                if (IsArenaSurface(groundHits[i].collider)) return groundHits[i];
            return default;
        }
        private RaycastHit2D SweepGround(Vector2 origin, float radius, Vector2 direction, float distance)
        {
            int count = Physics2D.CircleCast(origin, radius, direction, GroundFilter, groundHits, distance);
            for (int i = 0; i < count; i++)
                if (IsArenaSurface(groundHits[i].collider)) return groundHits[i];
            return default;
        }
        private void ClearOrdnance()
        {
            foreach (var item in ordnance) if (item != null) { item.SetActive(false); Destroy(item); }
            ordnance.Clear();
        }
        private static Vector2 Rotate(Vector2 vector, float degrees) => Quaternion.Euler(0, 0, degrees) * vector;
        private void Face(float dx)
        {
            if (Mathf.Abs(dx) < .01f) return;
            var scale = transform.localScale;
            scale.x = -Mathf.Sign(dx) * Mathf.Abs(scale.x);
            transform.localScale = scale;
        }
        private void PlayFlight() { if (animator != null && animator.HasState(0, FlyState)) animator.Play(FlyState, 0, 0); }
        private void PlayAttack() { if (animator != null && animator.HasState(0, AttackState)) animator.Play(AttackState, 0, 0); }

        public ParryResult HandleParry(ParryContext context)
        {
            if (!dashing || boss.IsDead) return ParryResult.Failed;
            dashing = false;
            body.linearVelocity = Vector2.zero;
            staggerUntil = Time.time + staggerDuration;
            Beat = AshwingBeat.Stagger;
            vfx.Parried();
            boss.TakeDamage(parryDamage);
            return ParryResult.Successful(.10f, true);
        }
        public void OnDamaged() { if (vfx != null) vfx.Hit(); }
        private void CancelAttack()
        {
            if (attack != null) StopCoroutine(attack);
            attack = null;
            dashing = false;
            Charge = 0;
            if (vfx != null) vfx.EndTelegraph();
            if (body != null) body.linearVelocity = Vector2.zero;
        }
        public void SuspendCombat()
        {
            CancelAttack();
            ClearOrdnance();
            IsEngaged = false;
            if (Beat != AshwingBeat.Dead) Beat = AshwingBeat.Dormant;
        }
        public void OnBossDeath()
        {
            if (deathHandled) return;
            deathHandled = true;
            SuspendCombat();
            Beat = AshwingBeat.Dead;
            vfx.Death();
        }
        private void OnDisable() { SuspendCombat(); }
        private void OnDestroy() { ClearOrdnance(); }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying && initialized ? (Vector3)home : transform.position + Vector3.right * arenaCenterOffsetX;
            Gizmos.color = new Color(1, .55f, .12f, .5f);
            Gizmos.DrawWireCube(center + Vector3.up * 1.5f, new Vector3(arenaHalfWidth * 2, 7, 0));
        }
    }
}

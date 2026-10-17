using UnityEngine;

namespace Bao.Enemies
{
    public class IceCrabEnemy : BaoEnemyBase
    {
        [Header("IceCrab - Bubble Attack")]
        [SerializeField] private GameObject attackHitbox;
        [SerializeField] private int attackDamage = 3;
        [SerializeField] private float attackAnimationDuration = 2.6f;
        [SerializeField, Range(0f, 1f)] private float fireNormalizedTime = 0.26f;
        [SerializeField, Range(0f, 1f)] private float fireEndNormalizedTime = 1f;

        [Header("IceCrab - Particle VFX")]
        [SerializeField] private GameObject attackVFX;

        public float AttackAnimationDuration => Mathf.Max(0.01f, attackAnimationDuration);
        public float FireTime => AttackAnimationDuration * fireNormalizedTime;
        public float FireEndTime => AttackAnimationDuration * Mathf.Max(fireNormalizedTime, fireEndNormalizedTime);

        public Transform BubbleMouthAnchor => attackVFX != null ? attackVFX.transform : transform;

        public IceCrabVFXController VfxController { get; private set; }

        protected override float FacingVisualMultiplier => -1f;

        private static readonly int AttackStateHash = Animator.StringToHash("Base Layer.Attack");

        private IceCrabAttackState attackState;
        private ParticleSystem attackParticles;

        protected override void Awake()
        {
            base.Awake();
            attackState = new IceCrabAttackState(this);
            attackParticles = attackVFX != null ? attackVFX.GetComponent<ParticleSystem>() : null;
            VfxController = GetComponent<IceCrabVFXController>();

            DisableAttackHitbox();
            StopAttackVFX();
            ConfigureBubbleAttack();
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(attackState);
        }

        public override void PlayAttackAnimation()
        {
            if (Animator != null)
            {
                Animator.SetBool("isAttack", true);
                if (Animator.HasState(0, AttackStateHash))
                {
                    Animator.Play(AttackStateHash, 0, 0f);
                }
            }
        }

        public void StopAttackAnimation()
        {
            if (Animator != null)
            {
                Animator.SetBool("isAttack", false);
            }
        }

        public void StartAttackVFX()
        {
            if (attackVFX == null || attackParticles == null)
            {
                return;
            }

            attackVFX.SetActive(true);
            attackParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.EmissionModule emission = attackParticles.emission;
            emission.enabled = true;
            attackParticles.Play(true);
            attackParticles.Emit(1);
        }

        public void StopAttackVFX()
        {
            if (attackParticles != null)
            {
                attackParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (attackVFX != null)
            {
                attackVFX.SetActive(false);
            }
        }

        private void OnDisable()
        {
            StopAttackVFX();
        }

        protected override void OnDead()
        {
            VfxController?.TriggerDeathBurst();

            base.OnDead();
            StopAttackAnimation();
            StopAttackVFX();
            DisableAttackHitbox();
        }

        private void ConfigureBubbleAttack()
        {
            if (attackParticles == null)
            {
                return;
            }

            ParticleSystem.MainModule main = attackParticles.main;
            main.playOnAwake = false;
            main.loop = true;

            ParticleSystem.TriggerModule trigger = attackParticles.trigger;
            trigger.enabled = true;
            trigger.enter = ParticleSystemOverlapAction.Callback;
            trigger.inside = ParticleSystemOverlapAction.Ignore;
            trigger.outside = ParticleSystemOverlapAction.Ignore;
            trigger.exit = ParticleSystemOverlapAction.Ignore;

            ParryableProjectileAdapter adapter = ParryableProjectileAdapter.Ensure(attackParticles);
            adapter.ConfigureParticleAttack(
                attackDamage,
                isSlowAttack: true,
                canBeBlocked: true,
                response: ProjectileParryResponseMode.Destroy,
                parryable: true,
                source: EnemyParryResponder.Ensure(this));
        }

        private void DisableAttackHitbox()
        {
            if (attackHitbox != null)
            {
                attackHitbox.SetActive(false);
            }
        }
    }
}

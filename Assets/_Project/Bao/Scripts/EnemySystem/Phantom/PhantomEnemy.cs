using UnityEngine;
using UnityEngine.Events;

namespace Bao.Enemies
{
    public class PhantomEnemy : BaoEnemyBase
    {
        [Header("Phantom - Attack Bones")]
        [SerializeField] private Transform leftBone;
        [SerializeField] private Transform rightBone;
        [SerializeField] private float boneWindupDuration = 0.4f;
        [SerializeField] private float boneLaunchDistance = 2.5f;
        [SerializeField] private float boneLaunchDuration = 0.18f;
        [SerializeField] private float boneHoldDuration = 0.08f;
        [SerializeField] private float boneReturnDuration = 0.22f;
        [SerializeField] private float boneArcHeight = 0.45f;
        [SerializeField] private float boneSpinDegrees = 220f;
        [SerializeField] private float boneHitRadius = 0.35f;
        [SerializeField] private int boneDamage = 8;

        public Transform LeftBone => leftBone;
        public Transform RightBone => rightBone;
        public float BoneWindupDuration => boneWindupDuration;
        public float BoneLaunchDistance => boneLaunchDistance;
        public float BoneLaunchDuration => boneLaunchDuration;
        public float BoneHoldDuration => boneHoldDuration;
        public float BoneReturnDuration => boneReturnDuration;
        public float BoneArcHeight => boneArcHeight;
        public float BoneSpinDegrees => boneSpinDegrees;
        public float BoneHitRadius => boneHitRadius;
        public int BoneDamage => boneDamage;

        [Header("Phantom - Stun")]
        [SerializeField] private float stunDuration = 1.5f;
        
        [Header("Phantom - Events")]
        [SerializeField] private UnityEvent onDeath;

        public UnityEvent OnDeath => onDeath;

        public float StunDuration => stunDuration;
        public SpriteRenderer[] SpriteRenderers => spriteRenderers;

        private PhantomAttackState phantomAttackState;
        private PhantomStunState phantomStunState;

        protected override void Awake()
        {
            base.Awake();
            phantomAttackState = new PhantomAttackState(this);
            phantomStunState = new PhantomStunState(this);
        }

        public void SetStateToPhantomAttack()
        {
            StateMachine.SetState(phantomAttackState);
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(phantomAttackState);
        }

        public void SetStateToStun()
        {
            StateMachine.SetState(phantomStunState);
        }

        protected override void OnDead()
        {
            base.OnDead();
            onDeath?.Invoke();
        }
    }
}
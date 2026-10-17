using UnityEngine;

namespace Bao.Enemies
{
    public class HuskfangEnemy : BaoEnemyBase
    {
        [Header("Huskfang - Root Attack")]
        [SerializeField] private GameObject rootPrefab;
        [SerializeField] private float attackDelay = 0.8f; // Time from stomp to root spawn
        
        public float AttackDelay => attackDelay;

        protected override float FacingVisualMultiplier => -1f;

        private HuskfangRootAttackState rootState;

        protected override void Awake()
        {
            base.Awake();
            rootState = new HuskfangRootAttackState(this);
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(rootState);
        }
        
        public void SummonRoots()
        {
            if (rootPrefab != null && Target != null)
            {
                Vector3 spawnPos = Target.position;
                Collider2D targetCollider = Target.GetComponentInChildren<Collider2D>();

                if (targetCollider != null && targetCollider.enabled)
                {
                    spawnPos.x = targetCollider.bounds.center.x;
                    spawnPos.y = targetCollider.bounds.min.y;
                }
                else
                {
                    Collider2D col = GetComponent<Collider2D>();
                    spawnPos.y = col != null ? col.bounds.min.y : transform.position.y - 0.8f;
                }
                
                GameObject rootInstance = Instantiate(rootPrefab, spawnPos, Quaternion.identity);
                HuskfangRootAttack rootAttack = rootInstance.GetComponent<HuskfangRootAttack>();
                if (rootAttack != null)
                {
                    rootAttack.Initialize(this);
                }
            }
        }
    }
}

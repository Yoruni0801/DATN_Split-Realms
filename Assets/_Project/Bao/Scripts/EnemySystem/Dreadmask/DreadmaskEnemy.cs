using UnityEngine;

namespace Bao.Enemies
{
    public class DreadmaskEnemy : BaoEnemyBase
    {
        [Header("Dreadmask - Shoot Rock")]
        [SerializeField] private GameObject rockProjectilePrefab;
        [SerializeField] private Transform shootPoint;
        [SerializeField] private float shootDelay = 0.5f;
        
        public GameObject RockProjectilePrefab => rockProjectilePrefab;
        public Transform ShootPoint => shootPoint;
        public float ShootDelay => shootDelay;

        protected override float FacingVisualMultiplier => -1f;

        private DreadmaskShootState shootState;

        protected override void Awake()
        {
            base.Awake();
            shootState = new DreadmaskShootState(this);
            if (Rigidbody2D != null)
            {
                Rigidbody2D.gravityScale = 0f; // Looks like it flies/floats
            }
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(shootState);
        }
        
        public void SpawnRock()
        {
            if (rockProjectilePrefab != null && shootPoint != null)
            {
                var rock = Instantiate(rockProjectilePrefab, shootPoint.position, Quaternion.identity);
                var dir = FacingDirection;
                rock.transform.localScale = new Vector3(Mathf.Abs(rock.transform.localScale.x) * dir, rock.transform.localScale.y, rock.transform.localScale.z);
                
                if (rock.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    if (Target != null)
                    {
                        Vector2 targetDir = (Target.position - shootPoint.position).normalized;
                        rb.linearVelocity = targetDir * 10f;
                    }
                    else
                    {
                        rb.linearVelocity = new Vector2(dir * 10f, 0f);
                    }
                }
            }
        }
    }
}

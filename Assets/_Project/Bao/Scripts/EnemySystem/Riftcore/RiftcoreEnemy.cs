using UnityEngine;

namespace Bao.Enemies
{
    public class RiftcoreEnemy : BaoEnemyBase
    {
        [Header("Riftcore - AOE Charge Attack")]
        [SerializeField] private float chargeDuration = 4.0f; // Tăng thời gian gồng để dễ căn chỉnh warning
        [SerializeField] private float aoeRange = 5f;
        [SerializeField] private int aoeDamage = 40;
        [SerializeField] private GameObject aoeEffectPrefab; // Hiệu ứng 2 bên

        public float ChargeDuration => chargeDuration;
        public float AoeRange => aoeRange;
        public int AoeDamage => aoeDamage;

        protected override float FacingVisualMultiplier => -1f;

        public void BeginChargeVFX()
        {
            if (TryGetComponent<RiftcoreVFXController>(out var vfx))
                vfx.BeginCharge(chargeDuration);
        }

        public void SetChargeVFXProgress(float normalizedProgress)
        {
            if (TryGetComponent<RiftcoreVFXController>(out var vfx))
                vfx.SetChargeProgress(normalizedProgress);
        }

        public void EndChargeVFX()
        {
            if (TryGetComponent<RiftcoreVFXController>(out var vfx))
                vfx.EndCharge();
        }

        private RiftcoreChargeAttackState chargeState;

        protected override void Awake()
        {
            // ChamThan is kept as a child of the prefab. Resolve it before base.Awake()
            // so the base warning system can hide/show it normally, while preserving any
            // explicit Inspector assignment.
            if (warningObject == null)
            {
                Transform warningTransform = transform.Find("ChamThan");
                if (warningTransform != null)
                {
                    warningObject = warningTransform.gameObject;
                }
            }

            base.Awake();
            chargeState = new RiftcoreChargeAttackState(this);
            // if (Rigidbody2D != null)
            // {
            //     Rigidbody2D.gravityScale = 0f; // Looks like it floats
            // }
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(chargeState);
        }

        public void ExecuteAoeAttack()
        {
            if (TryGetComponent<RiftcoreVFXController>(out var vfx))
                vfx.TriggerTwinRiftImpact();

            Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, new Vector2(aoeRange * 2, 2f), 0f);
            foreach (Collider2D hit in hits)
            {
                if (!hit.CompareTag("Player"))
                    continue;

                Vector2 direction = hit.transform.position.x < transform.position.x ? Vector2.left : Vector2.right;
                PlayerHitResolver.TryApplyHitToPlayer(
                    hit,
                    aoeDamage,
                    ParryableAttackType.Melee,
                    EnemyParryResponder.Ensure(this),
                    transform,
                    direction,
                    canBeBlocked: true);
            }
        }

        private void SpawnSideVfx(Vector2 direction)
        {
            if (aoeEffectPrefab == null)
                return;

            GameObject instance = Instantiate(
                aoeEffectPrefab,
                transform.position + (Vector3)(direction * (aoeRange * 0.5f)),
                Quaternion.identity);

            if (direction.x < 0f)
            {
                Vector3 scale = instance.transform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                instance.transform.localScale = scale;
            }

            if (TryGetComponent<RiftcoreVFXController>(out var vfx))
                vfx.StyleLegacySideEffect(instance);
        }

        public override void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(aoeRange * 2, 2f, 0f));
        }
    }
}

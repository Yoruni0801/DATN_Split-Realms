using UnityEngine;

namespace Bao.Enemies
{
    public class MoltenfishJumpState : BaoEnemyState
    {
        private readonly MoltenfishEnemy moltenfish;
        private float startTime;
        private bool isFalling;

        public MoltenfishJumpState(BaoEnemyBase enemy) : base(enemy)
        {
            moltenfish = enemy as MoltenfishEnemy;
        }

        public override void Enter()
        {
            startTime = Time.time;
            moltenfish.LastJumpTime = Time.time;
            isFalling = false;
            
            if (moltenfish.Target != null)
            {
                moltenfish.FacePosition(moltenfish.Target.position);
            }
            
            moltenfish.MarkAttackStarted();
            moltenfish.SetLocomotionAnimation(false);
            
            if (moltenfish.Animator != null)
            {
                moltenfish.Animator.SetBool("isJumpAttack", true);
                moltenfish.Animator.SetBool("isDownAttack", false);
            }

            if (moltenfish.Rigidbody2D != null)
            {
                moltenfish.Rigidbody2D.gravityScale = 1f; // Standard physics for gravity jump arc
                
                // Calculate required vertical velocity to reach desired height
                float jumpVelocityY = Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * 1f * moltenfish.JumpHeight);
                
                // Jump straight up (0 horizontal velocity)
                moltenfish.Rigidbody2D.linearVelocity = new Vector2(0f, jumpVelocityY);
            }

            if (moltenfish.VFXController != null)
            {
                moltenfish.VFXController.TriggerEruption(moltenfish.transform.position - new Vector3(0, 0.5f, 0));
            }
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - startTime;
            
            // Check if we reached the peak and start falling
            if (!isFalling && elapsed > 0.1f && moltenfish.Rigidbody2D != null && moltenfish.Rigidbody2D.linearVelocity.y <= 0f)
            {
                isFalling = true;
                if (moltenfish.Animator != null)
                {
                    moltenfish.Animator.SetBool("isJumpAttack", false);
                    moltenfish.Animator.SetBool("isDownAttack", true);
                }
            }

            // Allow it to go back to swimming only if it has fallen back into the lava (below HomePosition.y + a small offset)
            bool hasFallenIntoLava = elapsed > 0.2f && isFalling && moltenfish.transform.position.y <= moltenfish.HomePosition.y + moltenfish.SwimHeightLimit;

            if (hasFallenIntoLava || elapsed > 5f)
            {
                if (moltenfish.VFXController != null && hasFallenIntoLava)
                {
                    moltenfish.VFXController.TriggerPlungeImpact(moltenfish.transform.position - new Vector3(0, 0.5f, 0));
                }
                moltenfish.SetStateToPatrol();
            }
        }

        public override void Exit()
        {
            base.Exit();
            if (moltenfish.Animator != null)
            {
                moltenfish.Animator.SetBool("isJumpAttack", false);
                moltenfish.Animator.SetBool("isDownAttack", false);
            }
        }
    }
}

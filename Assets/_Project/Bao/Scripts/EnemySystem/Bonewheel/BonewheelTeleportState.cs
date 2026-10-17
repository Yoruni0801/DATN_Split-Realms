using UnityEngine;

namespace Bao.Enemies
{
    public class BonewheelTeleportState : BaoEnemyState
    {
        private readonly BonewheelEnemy bonewheel;
        private enum Phase { Disappearing, Invisible, Appearing, Finished }
        private Phase currentPhase;
        private float phaseStartTime;
        private Vector3 targetLockPosition;

        public BonewheelTeleportState(BaoEnemyBase enemy) : base(enemy)
        {
            bonewheel = enemy as BonewheelEnemy;
        }

        public override void Enter()
        {
            phaseStartTime = Time.time;
            bonewheel.StopHorizontalMovement();
            bonewheel.SetLocomotionAnimation(false);
            currentPhase = Phase.Disappearing;
            SetAlpha(0.2f);
        }

        public override void LogicUpdate()
        {
            float timeInPhase = Time.time - phaseStartTime;

            switch (currentPhase)
            {
                case Phase.Disappearing:
                    if (timeInPhase > 0.2f)
                    {
                        SetAlpha(0f);
                        currentPhase = Phase.Invisible;
                        phaseStartTime = Time.time;
                    }
                    break;

                case Phase.Invisible:
                    if (timeInPhase > bonewheel.InvisibilityDuration)
                    {
                        UpdateTeleportPosition();
                        SetAlpha(1f);
                        
                        currentPhase = Phase.Appearing;
                        phaseStartTime = Time.time;
                    }
                    break;

                case Phase.Appearing:
                    // Continuously track the player's back while appearing 
                    // so it doesn't get left behind if the player moves.
                    UpdateTeleportPosition();

                    if (timeInPhase > bonewheel.AppearDelay)
                    {
                        currentPhase = Phase.Finished;
                    }
                    break;
            }

            if (currentPhase == Phase.Finished)
            {
                if (bonewheel.HasTargetInDetectionRange())
                {
                    bonewheel.SetStateToChase();
                }
                else
                {
                    bonewheel.SetStateToPatrol();
                }
            }
        }

        private float lockedFacingDir = 1f;

        private void UpdateTeleportPosition()
        {
            if (bonewheel.Target != null)
            {
                targetLockPosition = bonewheel.Target.position;
                
                float playerFacingDir;
                if (currentPhase == Phase.Appearing)
                {
                    // Use the cached facing direction so the monster doesn't rapidly swap sides
                    // if the player spams A and D while the monster is appearing.
                    playerFacingDir = lockedFacingDir;
                }
                else
                {
                    playerFacingDir = Mathf.Sign(bonewheel.Target.localScale.x);
                    lockedFacingDir = playerFacingDir; // Cache it for the next phase
                }
                
                Vector3 newPos = targetLockPosition;
                // Note: The player's localScale.x is positive when facing left, and negative when facing right.
                // If player faces left (localScale.x > 0, playerFacingDir = 1), using += moves Bonewheel to the RIGHT (behind the player).
                // If player faces right (localScale.x < 0, playerFacingDir = -1), using += moves Bonewheel to the LEFT (behind the player).
                newPos.x += playerFacingDir * bonewheel.AppearOffset;
                // Fix: Update Y to match player's Y instead of the monster's old Y
                newPos.y = targetLockPosition.y; 
                
                bonewheel.transform.position = newPos;
                Physics2D.SyncTransforms();
                bonewheel.FacePosition(targetLockPosition);
            }
            else
            {
                targetLockPosition = bonewheel.transform.position;
                bonewheel.FacePosition(targetLockPosition);
            }
        }

        private void SetAlpha(float alpha)
        {
            if (bonewheel.SpriteRenderers != null)
            {
                foreach (var renderer in bonewheel.SpriteRenderers)
                {
                    if (renderer != null)
                    {
                        Color c = renderer.color;
                        c.a = alpha;
                        renderer.color = c;
                    }
                }
            }
        }

        public override void Exit()
        {
            SetAlpha(1f);
            bonewheel.MarkTeleportStarted();
        }
    }
}

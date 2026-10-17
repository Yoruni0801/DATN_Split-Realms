using UnityEngine;

namespace Bao.Enemies
{
    public class IcecrawlerChargeState : BaoEnemyState
    {
        private readonly IcecrawlerEnemy icecrawler;
        private float startTime;
        private float chargeDirection;
        private bool hasDealtDamageThisCharge;
        private bool isChargingStarted;

        public bool IsChargingStarted => isChargingStarted;

        public IcecrawlerChargeState(BaoEnemyBase enemy) : base(enemy)
        {
            icecrawler = enemy as IcecrawlerEnemy;
        }

        public override void Enter()
        {
            startTime = Time.time;
            hasDealtDamageThisCharge = false;
            isChargingStarted = false;
            
            if (icecrawler.Target != null)
            {
                icecrawler.FacePosition(icecrawler.Target.position);
            }
            chargeDirection = icecrawler.FacingDirection;
            
            icecrawler.MarkAttackStarted();
            icecrawler.StartWarningRoutine(); // Bật cảnh báo chớp đỏ
            
            // Đứng yên lúc lấy đà
            icecrawler.StopHorizontalMovement(); 
            icecrawler.SetLocomotionAnimation(true); 
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - startTime;

            if (!isChargingStarted)
            {
                // Cập nhật hướng liên tục khi lấy đà, nhưng khóa hướng trước 0.15s để tạo độ "nặng" và tránh lật mặt liên tục (glitch)
                if (elapsed < icecrawler.ChargeAnticipationDuration - 0.15f)
                {
                    if (icecrawler.Target != null)
                    {
                        icecrawler.FacePosition(icecrawler.Target.position);
                        chargeDirection = icecrawler.FacingDirection;
                    }
                }

                if (elapsed >= icecrawler.ChargeAnticipationDuration)
                {
                    isChargingStarted = true;
                    icecrawler.StopWarningRoutine(); // Tắt chớp đỏ

                    if (icecrawler.Animator != null)
                    {
                        icecrawler.SetLocomotionAnimation(true); 
                    }

                    // Temporarily ignore collision with the Player's collider to pass through them
                    if (icecrawler.Target != null)
                    {
                        Collider2D playerCollider = icecrawler.Target.GetComponent<Collider2D>();
                        if (playerCollider != null && icecrawler.Collider2D != null)
                        {
                            Physics2D.IgnoreCollision(icecrawler.Collider2D, playerCollider, true);
                        }
                    }
                }
            }

            if (elapsed > icecrawler.ChargeDuration + icecrawler.ChargeAnticipationDuration)
            {
                TransitionToNextState();
            }
        }
        
        private void TransitionToNextState()
        {
            if (icecrawler.HasTargetInDetectionRange())
                icecrawler.SetStateToChase();
            else
                icecrawler.SetStateToPatrol();
        }

        public override void PhysicsUpdate()
        {
            if (isChargingStarted)
            {
                float elapsedCharging = Time.time - startTime - icecrawler.ChargeAnticipationDuration;
                float t = Mathf.Clamp01(elapsedCharging / icecrawler.ChargeDuration);
                
                // Gia tốc trượt mượt mà: Bắt đầu húc cực mạnh và giảm dần tốc độ về cuối (Ease-out)
                float speedMultiplier = 1.0f - (t * t * t); // Cubic ease out
                float currentSpeed = icecrawler.ChargeSpeed * speedMultiplier;

                icecrawler.Rigidbody2D.linearVelocity = new Vector2(chargeDirection * currentSpeed, icecrawler.Rigidbody2D.linearVelocity.y);

                // Active overlap damage detection while passing through the player
                if (!hasDealtDamageThisCharge && icecrawler.Collider2D != null)
                {
                    ContactFilter2D filter = new ContactFilter2D();
                    filter.useTriggers = true;
                    Collider2D[] results = new Collider2D[10];
                    int count = icecrawler.Collider2D.Overlap(filter, results);
                    for (int i = 0; i < count; i++)
                    {
                        if (results[i].CompareTag("Player"))
                        {
                            if (PlayerHitResolver.TryApplyHitToPlayer(
                                    results[i],
                                    icecrawler.ChargeDamage, // Restore damage
                                    ParryableAttackType.Melee,
                                    EnemyParryResponder.Ensure(icecrawler),
                                    icecrawler.transform,
                                    results[i].transform.position - icecrawler.transform.position,
                                    true,
                                    true) != IncomingAttackResolution.Ignored)
                            {
                                hasDealtDamageThisCharge = true;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                // Tiến gần mục tiêu trong lúc lấy đà, sau đó mới lao bằng chargeSpeed.
                icecrawler.MoveForward(chargeDirection);
            }
        }

        public override void Exit()
        {
            icecrawler.StopWarningRoutine();
            icecrawler.StopHorizontalMovement();

            // Restore collision with the Player's collider
            if (icecrawler.Target != null)
            {
                Collider2D playerCollider = icecrawler.Target.GetComponent<Collider2D>();
                if (playerCollider != null && icecrawler.Collider2D != null)
                {
                    Physics2D.IgnoreCollision(icecrawler.Collider2D, playerCollider, false);
                }
            }
        }
    }
}

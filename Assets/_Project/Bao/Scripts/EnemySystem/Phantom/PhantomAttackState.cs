using UnityEngine;

namespace Bao.Enemies
{
    public class PhantomAttackState : BaoEnemyState
    {
        private readonly PhantomEnemy phantom;
        private float enterTime;
        private float attackFacingDirection;
        private Vector3 leftStartLocalPosition;
        private Vector3 rightStartLocalPosition;
        private Quaternion leftStartLocalRotation;
        private Quaternion rightStartLocalRotation;
        private bool leftHit;
        private bool rightHit;
        private bool isParried;
        private float parryStartTime;
        private Vector3 leftParryPosition;
        private Vector3 rightParryPosition;
        private Quaternion leftParryRotation;
        private Quaternion rightParryRotation;

        public PhantomAttackState(BaoEnemyBase enemy) : base(enemy)
        {
            phantom = enemy as PhantomEnemy;
        }

        public override void Enter()
        {
            enterTime = Time.time;
            leftHit = false;
            rightHit = false;
            isParried = false;

            phantom.StopHorizontalMovement();
            phantom.SetFlyingAnimation(false);
            phantom.SetLocomotionAnimation(false);
            phantom.MarkAttackStarted();
            Enemy.StartWarningRoutine();

            if (phantom.Target != null)
            {
                phantom.FacePosition(phantom.Target.position);
            }

            attackFacingDirection = phantom.FacingDirection;

            if (phantom.LeftBone == null || phantom.RightBone == null)
            {
                Debug.LogWarning($"Phantom bones not assigned! Left: {phantom.LeftBone}, Right: {phantom.RightBone}", phantom);
            }

            CacheBonePositions();
            phantom.PlayAttackAnimation();
            Debug.Log($"Phantom entering attack state. Bones: L={phantom.LeftBone?.name ?? "NULL"}, R={phantom.RightBone?.name ?? "NULL"}", phantom);
        }

        public override void LogicUpdate()
        {
            if (isParried) return;

            float elapsedTime = Time.time - enterTime;
            if (elapsedTime >= GetTotalDuration())
            {
                ExitAttack();
            }
        }

        public override void PhysicsUpdate()
        {
        }

        public override void LateUpdate()
        {
            if (phantom == null)
            {
                return;
            }

            if (isParried)
            {
                float timeSinceParry = Time.time - parryStartTime;
                float returnProgress = Mathf.Clamp01(timeSinceParry / (phantom.BoneReturnDuration * 0.75f)); // Return slightly faster when parried
                float tReturn = EaseInCubic(returnProgress);

                if (phantom.LeftBone != null)
                {
                    phantom.LeftBone.position = Vector3.LerpUnclamped(leftParryPosition, phantom.LeftBone.parent.TransformPoint(leftStartLocalPosition), tReturn);
                    phantom.LeftBone.rotation = Quaternion.Slerp(leftParryRotation, phantom.LeftBone.parent.rotation * leftStartLocalRotation, tReturn);
                }

                if (phantom.RightBone != null)
                {
                    phantom.RightBone.position = Vector3.LerpUnclamped(rightParryPosition, phantom.RightBone.parent.TransformPoint(rightStartLocalPosition), tReturn);
                    phantom.RightBone.rotation = Quaternion.Slerp(rightParryRotation, phantom.RightBone.parent.rotation * rightStartLocalRotation, tReturn);
                }

                if (returnProgress >= 1f)
                {
                    phantom.SetStateToStun();
                }
                return;
            }

            float elapsedTime = Time.time - enterTime;
            float windupEndTime = phantom.BoneWindupDuration;
            float launchEndTime = windupEndTime + phantom.BoneLaunchDuration;
            float holdEndTime = launchEndTime + phantom.BoneHoldDuration;
            float totalDuration = GetTotalDuration();
            float facingDirection = attackFacingDirection;

            Vector3 leftStartWorld = phantom.LeftBone != null ? phantom.LeftBone.parent.TransformPoint(leftStartLocalPosition) : Vector3.zero;
            Quaternion leftStartRot = phantom.LeftBone != null ? phantom.LeftBone.parent.rotation * leftStartLocalRotation : Quaternion.identity;

            Vector3 rightStartWorld = phantom.RightBone != null ? phantom.RightBone.parent.TransformPoint(rightStartLocalPosition) : Vector3.zero;
            Quaternion rightStartRot = phantom.RightBone != null ? phantom.RightBone.parent.rotation * rightStartLocalRotation : Quaternion.identity;

            UpdateBone(phantom.LeftBone, leftStartWorld, leftStartRot, elapsedTime, windupEndTime, launchEndTime, holdEndTime, totalDuration, facingDirection, ref leftHit);
            UpdateBone(phantom.RightBone, rightStartWorld, rightStartRot, elapsedTime, windupEndTime, launchEndTime, holdEndTime, totalDuration, facingDirection, ref rightHit);
        }

        public override void Exit()
        {
            Enemy.StopWarningRoutine();
            RestoreBonePositions();
            phantom.StopHorizontalMovement();
        }

        private void CacheBonePositions()
        {
            if (phantom.LeftBone != null)
            {
                leftStartLocalPosition = phantom.LeftBone.localPosition;
                leftStartLocalRotation = phantom.LeftBone.localRotation;
            }

            if (phantom.RightBone != null)
            {
                rightStartLocalPosition = phantom.RightBone.localPosition;
                rightStartLocalRotation = phantom.RightBone.localRotation;
            }
        }

        private void RestoreBonePositions()
        {
            if (phantom.LeftBone != null)
            {
                phantom.LeftBone.localPosition = leftStartLocalPosition;
                phantom.LeftBone.localRotation = leftStartLocalRotation;
            }

            if (phantom.RightBone != null)
            {
                phantom.RightBone.localPosition = rightStartLocalPosition;
                phantom.RightBone.localRotation = rightStartLocalRotation;
            }
        }

        private void UpdateBone(
            Transform bone,
            Vector3 startWorldPosition,
            Quaternion startRotation,
            float elapsedTime,
            float windupEndTime,
            float launchEndTime,
            float holdEndTime,
            float totalDuration,
            float facingDirection,
            ref bool hasHit)
        {
            if (bone == null)
            {
                return;
            }

            Vector3 windupOffset = new Vector3(-facingDirection * 0.4f, 0f, 0f); // Pull back
            Vector3 actualStart = startWorldPosition + windupOffset;

            if (elapsedTime <= windupEndTime)
            {
                float t = EaseOutCubic(elapsedTime / Mathf.Max(windupEndTime, 0.0001f));
                bone.position = Vector3.LerpUnclamped(startWorldPosition, actualStart, t);

                // Shake slightly vertically
                float shake = Mathf.Sin(Time.time * 60f) * 0.05f * t;
                bone.position += new Vector3(0f, shake, 0f);

                // Rotate back slightly
                bone.rotation = Quaternion.Euler(0f, 0f, facingDirection * -45f * t) * startRotation;
                return;
            }

            Vector3 launchOffset = new Vector3(facingDirection * phantom.BoneLaunchDistance, 0f, 0f);
            Vector3 launchTarget = startWorldPosition + launchOffset;
            Vector3 travelDirection = (launchTarget - actualStart).normalized;
            Vector3 perpendicular = new Vector3(-travelDirection.y, travelDirection.x, 0f);
            float arcHeight = phantom.BoneArcHeight * (bone == phantom.LeftBone ? 1f : -1f);

            float launchTime = elapsedTime - windupEndTime;
            if (elapsedTime <= launchEndTime)
            {
                float t = EaseOutCubic(launchTime / Mathf.Max(phantom.BoneLaunchDuration, 0.0001f));
                Vector3 curvedPosition = Vector3.LerpUnclamped(actualStart, launchTarget, t);
                float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
                curvedPosition += perpendicular * arc;
                bone.position = curvedPosition;
                bone.rotation = Quaternion.Euler(0f, 0f, GetSpinAngle(t, facingDirection, bone == phantom.LeftBone)) * startRotation;
                TryDamagePlayer(bone, ref hasHit);
                return;
            }

            if (elapsedTime <= holdEndTime)
            {
                bone.position = launchTarget + perpendicular * arcHeight;
                bone.rotation = Quaternion.Euler(0f, 0f, GetSpinAngle(1f, facingDirection, bone == phantom.LeftBone)) * startRotation;
                TryDamagePlayer(bone, ref hasHit);
                return;
            }

            float returnDuration = Mathf.Max(totalDuration - holdEndTime, 0.0001f);
            float returnProgress = Mathf.Clamp01((elapsedTime - holdEndTime) / returnDuration);
            float tReturn = EaseInCubic(returnProgress);
            Vector3 returnPosition = Vector3.LerpUnclamped(launchTarget, startWorldPosition, tReturn);
            float returnArc = Mathf.Sin((1f - tReturn) * Mathf.PI) * arcHeight;
            bone.position = returnPosition + perpendicular * returnArc;
            bone.rotation = Quaternion.Euler(0f, 0f, GetSpinAngle(1f - tReturn, facingDirection, bone == phantom.LeftBone)) * startRotation;
        }

        private float GetSpinAngle(float t, float facingDirection, bool isLeftBone)
        {
            float directionMultiplier = facingDirection >= 0f ? 1f : -1f;
            float baseSpin = phantom.BoneSpinDegrees * t * directionMultiplier;
            float flourish = Mathf.Sin(t * Mathf.PI) * 25f * (isLeftBone ? 1f : -1f);
            return baseSpin + flourish;
        }

        private void TryDamagePlayer(Transform bone, ref bool hasHit)
        {
            if (hasHit || phantom.Target == null || isParried)
            {
                return;
            }

            float distanceToPlayer = Vector2.Distance(bone.position, phantom.Target.position);
            if (distanceToPlayer > phantom.BoneHitRadius)
            {
                return;
            }

            Collider2D playerCollider = phantom.Target.GetComponent<Collider2D>();
            if (playerCollider != null)
            {
                IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                    playerCollider,
                    phantom.BoneDamage,
                    ParryableAttackType.Melee,
                    EnemyParryResponder.Ensure(phantom),
                    phantom.transform,
                    phantom.Target.position - phantom.transform.position);

                if (resolution != IncomingAttackResolution.Ignored)
                {
                    hasHit = true;
                }

                if (resolution == IncomingAttackResolution.Parried)
                {
                    OnParried();
                }
            }
        }

        private void OnParried()
        {
            if (isParried) return;
            isParried = true;
            parryStartTime = Time.time;
            if (phantom.LeftBone != null)
            {
                leftParryPosition = phantom.LeftBone.position;
                leftParryRotation = phantom.LeftBone.rotation;
            }
            if (phantom.RightBone != null)
            {
                rightParryPosition = phantom.RightBone.position;
                rightParryRotation = phantom.RightBone.rotation;
            }
        }

        private float GetTotalDuration()
        {
            return phantom.BoneWindupDuration + phantom.BoneLaunchDuration + phantom.BoneHoldDuration + phantom.BoneReturnDuration;
        }

        private void ExitAttack()
        {
            if (phantom.HasTargetInDetectionRange())
            {
                phantom.SetStateToChase();
            }
            else
            {
                phantom.SetStateToPatrol();
            }
        }

        private float EaseOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            float inverted = 1f - value;
            return 1f - inverted * inverted * inverted;
        }

        private float EaseInCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value;
        }
    }
}

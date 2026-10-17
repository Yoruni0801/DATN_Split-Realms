using Bao.Enemies;
using UnityEngine;
using UnityEngine.Playables;

public sealed class TimelineAshwingPickupDropBehaviour : PlayableBehaviour
{
    public Transform ashwingOverride;
    public Transform itemTarget;
    public Transform carryPoint;
    public Transform flyAwayTarget;
    public float approachSpeed;
    public float escapeSpeed;
    public float flightSpeedMultiplier;
    public float flightAcceleration;
    public float arriveSlowdownDistance;
    public bool invertFacing;
    public float pickupDistance;
    public float forcePickupNormalizedTime;
    public bool requireReachItemBeforePickup;
    public Vector3 carryLocalOffset;
    public bool flyToExactItemPosition;
    public Vector3 targetOffset;
    public Vector3 flyAwayTargetOffset;
    public Vector2 escapeDirection;
    public float escapeDistance;
    public bool disableAshwingAtEnd;
    public bool releaseTimelineControlOnEnd;

    private Transform ashwing;
    private AshwingEnemy ashwingEnemy;
    private Rigidbody2D ashwingBody;
    private Transform item;
    private NidusDeathDropItem dropItem;
    private Vector3 escapeTarget;
    private Vector3 flightVelocity;
    private bool entered;
    private bool pickedUp;
    private bool warnedMissingAshwing;
    private bool warnedMissingItem;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TimelineSeekContext.Evaluate(playable, info);

        // The flight is integrated frame by frame and the pickup reparents the item, so neither can be rebuilt
        // for an arbitrary playhead time. A seek therefore leaves this clip's state untouched.
        if (TimelineSeekContext.IsSeeking)
        {
            return;
        }

        ResolveRuntimeReferences(playerData);
        if (ashwing == null)
        {
            WarnOnce(ref warnedMissingAshwing, "[TimelineAshwingPickupDrop] Missing Ashwing binding.");
            return;
        }

        if (item == null)
        {
            WarnOnce(ref warnedMissingItem, "[TimelineAshwingPickupDrop] Missing death drop item.");
            return;
        }

        if (!entered)
        {
            Enter();
        }

        double duration = playable.GetDuration();
        float normalizedTime = duration > 0d
            ? Mathf.Clamp01((float)(playable.GetTime() / duration))
            : 1f;

        if (!pickedUp)
        {
            Vector3 pickupPosition = ResolvePickupPosition();
            MoveToward(pickupPosition, approachSpeed);

            float distance = Vector2.Distance(ashwing.position, pickupPosition);
            bool reachedItem = distance <= pickupDistance;
            bool forcedByTimeline = !requireReachItemBeforePickup
                && normalizedTime >= forcePickupNormalizedTime;
            if (reachedItem || forcedByTimeline)
            {
                PickupItem();
            }

            return;
        }

        MoveToward(ResolveFlyAwayTarget(), escapeSpeed);
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        Exit();
    }

    public override void OnGraphStop(Playable playable)
    {
        Exit();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        Exit();
    }

    private void Enter()
    {
        entered = true;
        pickedUp = false;
        flightVelocity = Vector3.zero;

        ashwingEnemy = ashwing.GetComponentInParent<AshwingEnemy>();
        ashwingBody = ashwing.GetComponentInParent<Rigidbody2D>();

        if (ashwingEnemy != null)
        {
            ashwingEnemy.BeginTimelineControl(item, stopMovement: false);
            ashwingEnemy.SetLocomotionAnimation(true);
            ashwingEnemy.SetFlyingAnimation(true);
        }

        if (ashwingBody != null)
        {
            ashwingBody.gravityScale = 0f;
            ashwingBody.linearVelocity = Vector2.zero;
        }

        escapeTarget = ResolveFallbackEscapeTarget();
    }

    private Vector3 ResolvePickupPosition()
    {
        if (item == null)
        {
            return ashwing != null ? ashwing.position : Vector3.zero;
        }

        return flyToExactItemPosition
            ? item.position
            : item.position + targetOffset;
    }

    private void PickupItem()
    {
        pickedUp = true;

        Transform parent = carryPoint != null ? carryPoint : ashwing;
        if (dropItem != null)
        {
            dropItem.MarkPickedUp(parent, carryLocalOffset);
        }
        else if (item != null)
        {
            Rigidbody2D itemBody = item.GetComponent<Rigidbody2D>();
            if (itemBody != null)
            {
                itemBody.linearVelocity = Vector2.zero;
                itemBody.angularVelocity = 0f;
                itemBody.simulated = false;
            }

            item.SetParent(parent, worldPositionStays: false);
            item.localPosition = carryLocalOffset;
            item.localRotation = Quaternion.identity;
        }

        escapeTarget = ResolveFallbackEscapeTarget();
    }

    private Vector3 ResolveFlyAwayTarget()
    {
        if (flyAwayTarget != null)
        {
            return flyAwayTarget.position + flyAwayTargetOffset;
        }

        return escapeTarget;
    }

    private Vector3 ResolveFallbackEscapeTarget()
    {
        Vector2 direction = escapeDirection.sqrMagnitude > 0.001f
            ? escapeDirection.normalized
            : Vector2.right;
        return ashwing.position + (Vector3)(direction * escapeDistance);
    }

    private void MoveToward(Vector3 targetPosition, float speed)
    {
        float deltaTime = Application.isPlaying ? Time.deltaTime : 1f / 60f;
        Vector3 currentPosition = ashwing.position;
        Vector3 nextPosition = Vector3.MoveTowards(
            currentPosition,
            targetPosition,
            ResolveFlightStep(currentPosition, targetPosition, speed, deltaTime));

        FaceMoveDirection(nextPosition - currentPosition, targetPosition);

        if (ashwingBody != null)
        {
            ashwingBody.MovePosition(nextPosition);
            ashwingBody.linearVelocity = Vector2.zero;
        }
        else
        {
            ashwing.position = nextPosition;
        }
    }

    private float ResolveFlightStep(Vector3 currentPosition, Vector3 targetPosition, float speed, float deltaTime)
    {
        Vector3 toTarget = targetPosition - currentPosition;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
        {
            flightVelocity = Vector3.zero;
            return 0f;
        }

        float targetSpeed = Mathf.Max(0f, speed) * Mathf.Max(0.1f, flightSpeedMultiplier);
        if (arriveSlowdownDistance > 0f && distance < arriveSlowdownDistance)
        {
            targetSpeed *= Mathf.Clamp01(distance / arriveSlowdownDistance);
        }

        Vector3 desiredVelocity = toTarget / distance * targetSpeed;
        float acceleration = flightAcceleration > 0f
            ? flightAcceleration
            : targetSpeed / Mathf.Max(deltaTime, 0.001f);
        flightVelocity = Vector3.MoveTowards(
            flightVelocity,
            desiredVelocity,
            acceleration * deltaTime);

        float step = flightVelocity.magnitude * deltaTime;
        return Mathf.Min(step, distance);
    }

    private void FaceMoveDirection(Vector3 moveDelta, Vector3 targetPosition)
    {
        float deltaX = Mathf.Abs(moveDelta.x) > 0.001f
            ? moveDelta.x
            : targetPosition.x - ashwing.position.x;
        if (Mathf.Abs(deltaX) <= 0.001f)
        {
            return;
        }

        float direction = Mathf.Sign(deltaX);
        if (invertFacing)
        {
            direction *= -1f;
        }

        if (ashwingEnemy != null)
        {
            Vector2 facePosition = (Vector2)ashwingEnemy.CenterPosition + Vector2.right * direction;
            ashwingEnemy.FacePosition(facePosition);
            return;
        }

        Vector3 scale = ashwing.localScale;
        ashwing.localScale = new Vector3(Mathf.Abs(scale.x) * direction, scale.y, scale.z);
    }

    private void Exit()
    {
        if (!entered)
        {
            return;
        }

        if (ashwingBody != null)
        {
            ashwingBody.linearVelocity = Vector2.zero;
        }

        if (ashwingEnemy != null && releaseTimelineControlOnEnd)
        {
            ashwingEnemy.EndTimelineControl(item);
        }

        if (disableAshwingAtEnd && ashwing != null)
        {
            ashwing.gameObject.SetActive(false);
        }

        entered = false;
        ashwing = null;
        ashwingEnemy = null;
        ashwingBody = null;
        item = null;
        dropItem = null;
        flightVelocity = Vector3.zero;
    }

    private void ResolveRuntimeReferences(object playerData)
    {
        if (ashwing == null)
        {
            ashwing = ashwingOverride != null ? ashwingOverride : ResolveTransform(playerData);
        }

        if (item == null)
        {
            item = itemTarget;
        }

        if (item == null)
        {
            if (NidusDeathDropItem.TryGetLatestUnpicked(out dropItem))
            {
                item = dropItem.Transform;
            }
        }
        else if (dropItem == null)
        {
            dropItem = item.GetComponent<NidusDeathDropItem>();
        }
    }

    private static Transform ResolveTransform(object playerData)
    {
        if (playerData is Transform transformBinding)
        {
            return transformBinding;
        }

        if (playerData is GameObject gameObjectBinding)
        {
            return gameObjectBinding.transform;
        }

        return playerData is Component componentBinding ? componentBinding.transform : null;
    }

    private static void WarnOnce(ref bool warned, string message)
    {
        if (warned)
        {
            return;
        }

        warned = true;
        Debug.LogWarning(message);
    }
}

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TimelineClipType
{
    SpineMove,
    SpineAnimation,
    CameraFollow,
    LoadScene,
    Petrify,
    Unpetrify,
    EnemyNpcAttack,
    AshwingPickupDrop
}

[Serializable]
public class TimelineSpineMoveSettings
{
    public ExposedReference<Transform> target;

    [Tooltip("Optional authored start of the move. Assign it to make editor scrubbing deterministic; leave it empty to start from the character's current position.")]
    public ExposedReference<Transform> startTarget;
    public Vector2 startOffset;

    public TimelineSpineMoveTiming moveTiming = TimelineSpineMoveTiming.MoveSpeed;
    public AnimationSpine.State moveState = AnimationSpine.State.Run;
    public AnimationSpine.State idleState = AnimationSpine.State.Idle;
    public TimelineSpineMoveBindingMode bindingMode = TimelineSpineMoveBindingMode.CurrentPlayerWhenBindingMissing;
    public TimelineSpineFacingOverride facingOverride;
    [Min(0f)] public float moveSpeed = 3f;
    [Min(0.01f)] public float moveDuration = 1f;
    public Vector2 targetOffset;
    public float stopDistance = 0.05f;
    public int animationTrackIndex;
    public bool stopWhenReached = true;
    public bool keepStoppedOnExit;
    public bool faceMoveDirection = true;
    public bool invertFacing;
    public bool controlAnimation = true;
    public bool returnToIdleWhenStopped = true;
    public bool lockPlayerControl = true;
    public bool unlockPlayerControlOnExit = true;
    public bool applyGravity = true;
    public bool applyInEditMode;
    [HideInInspector] public TimelineSpineMoveAxis moveAxis = TimelineSpineMoveAxis.XY;
}

[Serializable]
public class TimelineSpineAnimationSettings
{
    public AnimationSpine.State state = AnimationSpine.State.Idle;
    public TimelineSpineAnimationExitMode exitMode = TimelineSpineAnimationExitMode.KeepCurrent;
    public TimelineSpineFacingOverride facingOverride;
    public int trackIndex;
    public float mixDuration = 0.1f;
    public float timeScale = 1f;
    public bool loopAnimation = true;
    public bool resetToSetupPoseBeforePlay;
    public bool restoreTimeScale = true;
    public bool reverseAnimation;
}

[Serializable]
public class TimelineCameraFollowSettings
{
    public ExposedReference<Transform> target;
    public TimelineCameraFollowAction action = TimelineCameraFollowAction.FollowTarget;
    public TimelineCameraFollowExitMode exitMode = TimelineCameraFollowExitMode.RestorePrevious;
    public Vector3 followOffset;
    public float smoothFollowDuration = 0.75f;
    public TimelineCameraFollowZMode targetZMode = TimelineCameraFollowZMode.KeepCurrentFollowZ;
    public float fixedWorldZ;
    public bool invalidatePreviousState = true;
    public bool applyInEditMode;
    public bool enableZoom;
    public float zoomOrthographicSize = 5f;
    public float zoomFieldOfView = 40f;
    public float zoomDuration = 0.5f;
    public bool restoreZoomOnExit = true;
    public bool enableShake;
    public ExposedReference<Camera> shakeCamera;
    public float shakeAmplitude = 0.3f;
    public float shakeFrequency = 30f;
    public AnimationCurve shakeEnvelope = AnimationCurve.Constant(0f, 1f, 1f);
}

[Serializable]
public class TimelineLoadSceneSettings
{
    public TimelineLoadSceneMode mode = TimelineLoadSceneMode.LoadAdditive;
    public string sceneName;
    public bool freezeCurrentScene = true;
    public bool setLoadedSceneActive = true;
}

[Serializable]
public class TimelinePetrifySettings
{
    public ExposedReference<PetrifyController> targetOverride;
    public TimelinePetrifyTargetMode targetMode = TimelinePetrifyTargetMode.BoundTarget;
    public TimelinePetrifyExitMode exitMode = TimelinePetrifyExitMode.KeepEndAmount;
    public bool animateOverClip = true;
    public bool includeInactiveTargets;
    public bool applyInEditMode;
}

[Serializable]
public class TimelineUnpetrifySettings
{
    public ExposedReference<PetrifyController> targetOverride;
    public TimelineUnpetrifyTargetMode targetMode = TimelineUnpetrifyTargetMode.BoundTarget;
    public TimelineUnpetrifyExitMode exitMode = TimelineUnpetrifyExitMode.ClearPetrify;
    public bool animateOverClip = true;
    public bool includeInactiveTargets;
    public bool applyInEditMode = true;
}

[Serializable]
public class TimelineEnemyNpcAttackSettings
{
    public ExposedReference<Transform> enemyOverride;
    public ExposedReference<Transform> npcTarget;
    public TimelineEnemyNpcAttackType attackType = TimelineEnemyNpcAttackType.Auto;
    [Min(0f)] public float impactNormalizedTime = 0.65f;
    [Min(0)] public int damage = 20;
    public bool damageNpc = true;
    public bool faceTarget = true;
    public bool triggerAnimator = true;
    public string animatorTrigger = "Attack";
    public bool stopEnemyVelocity = true;
    public UnityEvent onImpact;
}

[Serializable]
public class TimelineAshwingPickupDropSettings
{
    public ExposedReference<Transform> ashwingOverride;
    public ExposedReference<Transform> itemTarget;
    public ExposedReference<Transform> carryPoint;
    public ExposedReference<Transform> flyAwayTarget;
    [Min(0f)] public float approachSpeed = 14f;
    [Min(0f)] public float escapeSpeed = 18f;
    [Min(0.1f)] public float flightSpeedMultiplier = 1.5f;
    [Min(0f)] public float flightAcceleration = 45f;
    [Min(0f)] public float arriveSlowdownDistance = 0.6f;
    public bool invertFacing = true;
    [Min(0f)] public float pickupDistance = 0.35f;
    [Range(0f, 1f)] public float forcePickupNormalizedTime = 0.45f;
    public bool requireReachItemBeforePickup = true;
    public Vector3 carryLocalOffset = new Vector3(0f, -0.25f, 0f);
    public bool flyToExactItemPosition = true;
    public Vector3 targetOffset = Vector3.zero;
    public Vector3 flyAwayTargetOffset = Vector3.zero;
    public Vector2 escapeDirection = new Vector2(1f, 1f);
    [Min(0f)] public float escapeDistance = 8f;
    public bool disableAshwingAtEnd;
    public bool releaseTimelineControlOnEnd = true;
}

public class TimelineActionClip : PlayableAsset, ITimelineClipAsset
{
    public TimelineClipType clipType;

    public TimelineSpineMoveSettings spineMove = new TimelineSpineMoveSettings();
    public TimelineSpineAnimationSettings spineAnimation = new TimelineSpineAnimationSettings();
    public TimelineCameraFollowSettings cameraFollow = new TimelineCameraFollowSettings();
    public TimelineLoadSceneSettings loadScene = new TimelineLoadSceneSettings();
    public TimelinePetrifySettings petrify = new TimelinePetrifySettings();
    public TimelineUnpetrifySettings unpetrify = new TimelineUnpetrifySettings();
    public TimelineEnemyNpcAttackSettings enemyNpcAttack = new TimelineEnemyNpcAttackSettings();
    public TimelineAshwingPickupDropSettings ashwingPickupDrop = new TimelineAshwingPickupDropSettings();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        switch (clipType)
        {
            case TimelineClipType.SpineMove:
            {
                var playable = ScriptPlayable<TimelineSpineMoveBehaviour>.Create(graph);
                var b = playable.GetBehaviour();
                b.target = spineMove.target.Resolve(graph.GetResolver());
                b.targetOffset = spineMove.targetOffset;
                b.startTarget = spineMove.startTarget.Resolve(graph.GetResolver());
                b.startOffset = spineMove.startOffset;
                b.stopDistance = Mathf.Max(0f, spineMove.stopDistance);
                b.moveTiming = spineMove.moveTiming;
                b.moveSpeed = Mathf.Max(0f, spineMove.moveSpeed);
                b.moveDuration = Mathf.Max(0.01f, spineMove.moveDuration);
                b.stopWhenReached = spineMove.stopWhenReached;
                b.keepStoppedOnExit = spineMove.keepStoppedOnExit;
                b.faceMoveDirection = spineMove.faceMoveDirection;
                b.invertFacing = spineMove.invertFacing;
                b.facingOverride = spineMove.facingOverride;
                b.controlAnimation = spineMove.controlAnimation;
                b.moveState = spineMove.moveState;
                b.idleState = spineMove.idleState;
                b.returnToIdleWhenStopped = spineMove.returnToIdleWhenStopped;
                b.animationTrackIndex = Mathf.Max(0, spineMove.animationTrackIndex);
                b.lockPlayerControl = spineMove.lockPlayerControl;
                b.unlockPlayerControlOnExit = spineMove.unlockPlayerControlOnExit;
                b.applyGravity = spineMove.applyGravity;
                b.applyInEditMode = spineMove.applyInEditMode;
                b.bindingMode = spineMove.bindingMode;
                return playable;
            }
            case TimelineClipType.SpineAnimation:
            {
                var playable = ScriptPlayable<TimelineSpineAnimationBehaviour>.Create(graph);
                var b = playable.GetBehaviour();
                b.state = spineAnimation.state;
                b.loopAnimation = spineAnimation.loopAnimation;
                b.trackIndex = Mathf.Max(0, spineAnimation.trackIndex);
                b.mixDuration = Mathf.Max(0f, spineAnimation.mixDuration);
                b.timeScale = Mathf.Max(0f, spineAnimation.timeScale);
                b.resetToSetupPoseBeforePlay = spineAnimation.resetToSetupPoseBeforePlay;
                b.exitMode = spineAnimation.exitMode;
                b.restoreTimeScale = spineAnimation.restoreTimeScale;
                b.reverseAnimation = spineAnimation.reverseAnimation;
                b.facingOverride = spineAnimation.facingOverride;
                return playable;
            }
            case TimelineClipType.CameraFollow:
            {
                var playable = ScriptPlayable<TimelineCameraFollowBehaviour>.Create(graph);
                var b = playable.GetBehaviour();
                b.targetZMode = cameraFollow.targetZMode;
                b.fixedWorldZ = cameraFollow.fixedWorldZ;
                b.target = cameraFollow.target.Resolve(graph.GetResolver());
                b.followOffset = cameraFollow.followOffset;
                b.action = cameraFollow.action;
                b.exitMode = cameraFollow.exitMode;
                b.invalidatePreviousState = cameraFollow.invalidatePreviousState;
                b.applyInEditMode = cameraFollow.applyInEditMode;
                b.smoothFollowDuration = Mathf.Max(0f, cameraFollow.smoothFollowDuration);
                b.enableZoom = cameraFollow.enableZoom;
                b.zoomOrthographicSize = Mathf.Max(0.1f, cameraFollow.zoomOrthographicSize);
                b.zoomFieldOfView = Mathf.Max(1f, cameraFollow.zoomFieldOfView);
                b.zoomDuration = Mathf.Max(0f, cameraFollow.zoomDuration);
                b.restoreZoomOnExit = cameraFollow.restoreZoomOnExit;
                b.enableShake = cameraFollow.enableShake;
                b.shakeCamera = cameraFollow.shakeCamera.Resolve(graph.GetResolver());
                b.shakeAmplitude = cameraFollow.shakeAmplitude;
                b.shakeFrequency = cameraFollow.shakeFrequency;
                b.shakeEnvelope = cameraFollow.shakeEnvelope;
                return playable;
            }
            case TimelineClipType.LoadScene:
            {
                var playable = ScriptPlayable<TimelineLoadSceneBehaviour>.Create(graph);
                var b = playable.GetBehaviour();
                b.mode = loadScene.mode;
                b.sceneName = loadScene.sceneName;
                b.freezeCurrentScene = loadScene.freezeCurrentScene;
                b.setLoadedSceneActive = loadScene.setLoadedSceneActive;
                return playable;
            }
            case TimelineClipType.Petrify:
            {
                var playable = ScriptPlayable<TimelinePetrifyBehaviour>.Create(graph);
                var b = playable.GetBehaviour();
                b.targetMode = petrify.targetMode;
                b.targetOverride = petrify.targetOverride.Resolve(graph.GetResolver());
                b.includeInactiveTargets = petrify.includeInactiveTargets;
                b.startAmount = 0f;
                b.endAmount = 1f;
                b.animateOverClip = petrify.animateOverClip;
                b.exitMode = petrify.exitMode;
                b.applyInEditMode = petrify.applyInEditMode;
                return playable;
            }
            case TimelineClipType.Unpetrify:
            {
                var playable = ScriptPlayable<TimelineUnpetrifyBehaviour>.Create(graph);
                var b = playable.GetBehaviour();
                b.targetMode = unpetrify.targetMode;
                b.targetOverride = unpetrify.targetOverride.Resolve(graph.GetResolver());
                b.includeInactiveTargets = unpetrify.includeInactiveTargets;
                b.startAmount = 1f;
                b.endAmount = 0f;
                b.animateOverClip = unpetrify.animateOverClip;
                b.exitMode = unpetrify.exitMode;
                b.applyInEditMode = unpetrify.applyInEditMode;
                return playable;
            }
            case TimelineClipType.EnemyNpcAttack:
            {
                var playable = ScriptPlayable<TimelineEnemyNpcAttackBehaviour>.Create(graph);
                var b = playable.GetBehaviour();
                b.enemyOverride = enemyNpcAttack.enemyOverride.Resolve(graph.GetResolver());
                b.npcTarget = enemyNpcAttack.npcTarget.Resolve(graph.GetResolver());
                b.attackType = enemyNpcAttack.attackType;
                b.impactNormalizedTime = Mathf.Clamp01(enemyNpcAttack.impactNormalizedTime);
                b.damage = Mathf.Max(0, enemyNpcAttack.damage);
                b.damageNpc = enemyNpcAttack.damageNpc;
                b.faceTarget = enemyNpcAttack.faceTarget;
                b.triggerAnimator = enemyNpcAttack.triggerAnimator;
                b.animatorTrigger = enemyNpcAttack.animatorTrigger;
                b.stopEnemyVelocity = enemyNpcAttack.stopEnemyVelocity;
                b.onImpact = enemyNpcAttack.onImpact;
                return playable;
            }
            case TimelineClipType.AshwingPickupDrop:
            {
                var playable = ScriptPlayable<TimelineAshwingPickupDropBehaviour>.Create(graph);
                var b = playable.GetBehaviour();
                b.ashwingOverride = ashwingPickupDrop.ashwingOverride.Resolve(graph.GetResolver());
                b.itemTarget = ashwingPickupDrop.itemTarget.Resolve(graph.GetResolver());
                b.carryPoint = ashwingPickupDrop.carryPoint.Resolve(graph.GetResolver());
                b.flyAwayTarget = ashwingPickupDrop.flyAwayTarget.Resolve(graph.GetResolver());
                b.approachSpeed = Mathf.Max(0f, ashwingPickupDrop.approachSpeed);
                b.escapeSpeed = Mathf.Max(0f, ashwingPickupDrop.escapeSpeed);
                b.flightSpeedMultiplier = Mathf.Max(0.1f, ashwingPickupDrop.flightSpeedMultiplier);
                b.flightAcceleration = Mathf.Max(0f, ashwingPickupDrop.flightAcceleration);
                b.arriveSlowdownDistance = Mathf.Max(0f, ashwingPickupDrop.arriveSlowdownDistance);
                b.invertFacing = ashwingPickupDrop.invertFacing;
                b.pickupDistance = Mathf.Max(0f, ashwingPickupDrop.pickupDistance);
                b.forcePickupNormalizedTime = Mathf.Clamp01(ashwingPickupDrop.forcePickupNormalizedTime);
                b.requireReachItemBeforePickup = ashwingPickupDrop.requireReachItemBeforePickup;
                b.carryLocalOffset = ashwingPickupDrop.carryLocalOffset;
                b.flyToExactItemPosition = ashwingPickupDrop.flyToExactItemPosition;
                b.targetOffset = ashwingPickupDrop.targetOffset;
                b.flyAwayTargetOffset = ashwingPickupDrop.flyAwayTargetOffset;
                b.escapeDirection = ashwingPickupDrop.escapeDirection;
                b.escapeDistance = Mathf.Max(0f, ashwingPickupDrop.escapeDistance);
                b.disableAshwingAtEnd = ashwingPickupDrop.disableAshwingAtEnd;
                b.releaseTimelineControlOnEnd = ashwingPickupDrop.releaseTimelineControlOnEnd;
                return playable;
            }
        }
        return Playable.Null;
    }
}

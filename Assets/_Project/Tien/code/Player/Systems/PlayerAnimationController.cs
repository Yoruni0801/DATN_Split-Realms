using System;
using Spine;
using UnityEngine;

public sealed class PlayerAnimationController
{
    private readonly AnimationSpine _animationSpine;

    private AnimationSpine.State _movementAnimation = AnimationSpine.State.None;
    private TrackEntry _combatEntry;
    private bool _forceNextMovement;

    public event Action OnCombatVisualComplete;

    public PlayerAnimationController(AnimationSpine animationSpine)
    {
        _animationSpine = animationSpine;
    }

    public void UpdateMovement(Vector2 velocity, PlayerMovementState movementState,
                               float jumpForce, AnimationSpine.State jumpAnim, AnimationSpine.State fallAnim,
                               bool blocking, bool hurt, bool actionActive, bool isGrounded)
    {
        if (hurt || actionActive) return;

        if (blocking)  { SetMovement(AnimationSpine.State.Block); return; }

        if (movementState == PlayerMovementState.Swimming)
        {
            if (isGrounded)
            {
                bool groundedMoving = velocity.sqrMagnitude > 0.01f;
                SetMovement(groundedMoving ? AnimationSpine.State.SwimMove : AnimationSpine.State.Idle);
                return;
            }

            bool swimmingMoving = velocity.sqrMagnitude > 0.01f;
            SetMovement(swimmingMoving ? AnimationSpine.State.SwimMove : AnimationSpine.State.SwimIdle);
            return;
        }

        if (movementState == PlayerMovementState.Airborne)
        {
            SetMovement(velocity.y > 0f ? jumpAnim : fallAnim);
            return;
        }

        bool moving = Mathf.Abs(velocity.x) > 0.1f;
        if (!moving) { SetMovement(AnimationSpine.State.Idle);  return; }
        SetMovement(AnimationSpine.State.Walk);
    }

    public void PlayHurt()  => Set(AnimationSpine.State.Hurt);
    public void PlayDeath() => Set(AnimationSpine.State.Die);
    public void PlayIdle()  => Set(AnimationSpine.State.Idle);
    public void PlayDash()  => Set(AnimationSpine.State.Dash);
    public void PlayRopeGrab()
    {
        if (_animationSpine != null && _animationSpine.HasStateAnimation(AnimationSpine.State.RopeGrab))
        {
            SetMovement(AnimationSpine.State.RopeGrab);
            return;
        }

        if (_animationSpine != null && _animationSpine.HasStateAnimation(AnimationSpine.State.Block))
        {
            SetMovement(AnimationSpine.State.Block);
            return;
        }

        SetMovement(AnimationSpine.State.Fall);
    }

    // ─── Combat layer ─────────────────────────────────────────────────────────
    public TrackEntry PlayCombat(AnimationSpine.State state)
    {
        ClearCombatTrack();
        _combatEntry = Play(state, false, 1, force: true);
        if (_combatEntry != null)
        {
            _combatEntry.Complete += HandleCombatComplete;
        }
        return _combatEntry;
    }

    public static float GetAnimationDuration(TrackEntry entry, float fallbackDuration = 0f)
    {
        if (entry != null && entry.Animation != null && entry.Animation.Duration > 0f)
        {
            return entry.Animation.Duration;
        }
        return fallbackDuration;
    }

    public void PauseCombat()
    {
        if (_combatEntry != null)
        {
            _combatEntry.TimeScale = 0f;
        }
    }

    public void ResumeCombat()
    {
        if (_combatEntry != null)
        {
            _combatEntry.TimeScale = 1f;
        }
    }

    public void ClearCombat()
    {
        ResumeCombat();
        ClearCombatTrack();
        ForceRefreshMovement();
    }

    public void ForceRefreshMovement()
    {
        _movementAnimation = AnimationSpine.State.None;
        _forceNextMovement = true;
    }

    // ─── Private ─────────────────────────────────────────────────────────────
    private void SetMovement(AnimationSpine.State state, bool restart = false)
    {
        if (!restart && _movementAnimation == state) return;
        _movementAnimation = state;
        bool force = restart || _forceNextMovement;
        _forceNextMovement = false;
        Play(state, true, 0, force);
    }

    private void Set(AnimationSpine.State state)
    {
        _movementAnimation = state;
        Play(state, false, 0);
    }

    private TrackEntry Play(AnimationSpine.State state, bool loop, int trackIndex, bool force = false)
    {
        if (_animationSpine == null || state == AnimationSpine.State.None)
        {
            return null;
        }

        if (_animationSpine.HasStateAnimation(state))
        {
            if (force || !_animationSpine.IsPlaying(state))
            {
                _animationSpine.ResetPoseForAnimationChange();
            }

            if (force && trackIndex == 0)
            {
                return _animationSpine.PlayForcedFromSetupPose(state, loop, trackIndex);
            }

            return force
                ? _animationSpine.PlayForced(state, loop, trackIndex)
                : _animationSpine.Play(state, loop, trackIndex);
        }

        return null;
    }

    private void HandleCombatComplete(TrackEntry trackEntry)
    {
        if (trackEntry != _combatEntry)
        {
            return;
        }

        ClearCombatTrack();
        ForceRefreshMovement();
        OnCombatVisualComplete?.Invoke();
    }

    private void ClearCombatTrack()
    {
        ResumeCombat();
        ClearCombatEntryHandler();
        _animationSpine?.StopAnimation(1);
    }

    private void ClearMovementTrackForAction()
    {
        _movementAnimation = AnimationSpine.State.None;
        _forceNextMovement = true;
        _animationSpine?.StopAnimation(0);
        _animationSpine?.ResetPoseForAnimationChange();
    }

    private void ClearCombatEntryHandler()
    {
        if (_combatEntry == null)
        {
            return;
        }

        _combatEntry.Complete -= HandleCombatComplete;
        _combatEntry = null;
    }
}

using UnityEngine;

public sealed class PlayerMovementStateMachine
{
    public PlayerMovementState State { get; private set; } = PlayerMovementState.Airborne;

    public bool IsGrounded => State == PlayerMovementState.Grounded;
    public bool IsAirborne => State == PlayerMovementState.Airborne;
    public bool IsSwimming => State == PlayerMovementState.Swimming;
    public bool IsExitingWater => State == PlayerMovementState.ExitingWater;

    public void ForceState(PlayerMovementState state)
    {
        State = state;
    }

    public PlayerMovementState Tick(PlayerWaterSnapshot environment, Vector2 moveInput, float facingSign)
    {
        if (State == PlayerMovementState.ExitingWater)
        {
            bool stillMovingTowardExit = environment.CanExitWater
                && Mathf.Abs(moveInput.x) > 0.1f
                && Mathf.Sign(moveInput.x) == Mathf.Sign(facingSign);

            if (environment.Grounded && !environment.BodyInWater)
            {
                State = PlayerMovementState.Grounded;
                return State;
            }

            if (!environment.InWaterZone && !environment.Grounded)
            {
                State = PlayerMovementState.Airborne;
                return State;
            }

            if (environment.BodyDeepEnoughToEnterSwimming && !stillMovingTowardExit)
            {
                State = PlayerMovementState.Swimming;
                return State;
            }

            return State;
        }

        bool wantsToExitWater = State == PlayerMovementState.Swimming
            && environment.Grounded
            && environment.CanExitWater
            && (environment.BodyInWater || environment.FeetInWater)
            && Mathf.Abs(moveInput.x) > 0.1f
            && Mathf.Sign(moveInput.x) == Mathf.Sign(facingSign);

        if (wantsToExitWater)
        {
            State = PlayerMovementState.ExitingWater;
            return State;
        }

        bool shouldSwim = State == PlayerMovementState.Swimming
            ? environment.BodyDeepEnoughToStaySwimming
            : environment.BodyDeepEnoughToEnterSwimming;

        if (shouldSwim)
        {
            State = PlayerMovementState.Swimming;
            return State;
        }

        if (environment.Grounded && !environment.BodyInWater)
        {
            State = PlayerMovementState.Grounded;
            return State;
        }

        State = PlayerMovementState.Airborne;
        return State;
    }
}

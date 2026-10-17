using System;
using CutsceneEngine;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInputController : IDisposable
{
    private readonly InputSystem_Actions input;
    private readonly bool[] skillHeld = new bool[4];
    private bool controlLocked;

    public PlayerInputController()
    {
        input = new InputSystem_Actions();
        BindInput();
    }

    public event Action<Vector2> MoveChanged;
    public event Action JumpRequested;
    public event Action DashRequested;
    public event Action AttackRequested;
    public event Action<int> SkillRequested;
    public event Action<int> SkillReleased;
    public event Action ParryStarted;
    public event Action ParryReleased;
    public event Action NextElementRequested;
    public event Action<SkillElementType> ElementRequested;
    public event Action GravityToggleRequested;

    public void Enable()
    {
        if (!controlLocked)
        {
            input.Enable();
        }
    }

    public void Disable()
    {
        ReleaseHeldActions();
        input.Disable();
    }

    public void SetControlLocked(bool locked)
    {
        controlLocked = locked;
        if (locked)
        {
            ReleaseHeldActions();
            MoveChanged?.Invoke(Vector2.zero);
            input.Disable();
            return;
        }

        input.Enable();
        MoveChanged?.Invoke(ReadMoveInput());
    }

    public Vector2 ReadMoveInput()
    {
        if (controlLocked)
        {
            return Vector2.zero;
        }

        Vector2 actionInput = input.Player.Move.enabled
            ? input.Player.Move.ReadValue<Vector2>()
            : Vector2.zero;
        Vector2 keyboardInput = ReadKeyboardMoveInput();

        return keyboardInput.sqrMagnitude > actionInput.sqrMagnitude
            ? keyboardInput
            : Vector2.ClampMagnitude(actionInput, 1f);
    }


    public bool ReadJumpHeld()
    {
        if (controlLocked)
        {
            return false;
        }

        bool actionHeld = input.Player.Jump.enabled && input.Player.Jump.IsPressed();
        bool keyboardHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed && !SubtitleSkipInput.IsSkipReserved;
        return actionHeld || keyboardHeld;
    }

    public void TickKeyboardFallback(System.Collections.Generic.IReadOnlyList<SkillSetSO> skillSets)
    {
        if (controlLocked || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            GravityToggleRequested?.Invoke();
        }

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            ParryStarted?.Invoke();
        }

        if (Keyboard.current.kKey.wasReleasedThisFrame)
        {
            ParryReleased?.Invoke();
        }


        TickSkillKeyBindings(skillSets);
    }

    public void Dispose()
    {
        input.Dispose();
    }

    private void BindInput()
    {
        input.Player.Move.performed += context =>
        {
            if (!controlLocked)
            {
                MoveChanged?.Invoke(context.ReadValue<Vector2>());
            }
        };
        input.Player.Move.canceled += _ => MoveChanged?.Invoke(Vector2.zero);
        input.Player.Jump.performed += _ => InvokeIfUnlocked(JumpRequested);
        input.Player.Dash.performed += _ => InvokeIfUnlocked(DashRequested);
        input.Player.Parry.performed += _ => InvokeIfUnlocked(ParryStarted);
        input.Player.Parry.canceled += _ => InvokeIfUnlocked(ParryReleased);
        input.Player.Attack.performed += _ => InvokeIfUnlocked(AttackRequested);
        input.Player.Attack1.performed += _ => InvokeSkillIfUnlocked(0);
        input.Player.Attack1.canceled += _ => InvokeSkillReleaseIfUnlocked(0);
        input.Player.Attack2.performed += _ => InvokeSkillIfUnlocked(1);
        input.Player.Attack2.canceled += _ => InvokeSkillReleaseIfUnlocked(1);
        input.Player.Attack3.performed += _ => InvokeSkillIfUnlocked(2);
        input.Player.Attack3.canceled += _ => InvokeSkillReleaseIfUnlocked(2);
        input.Player.Attack4.performed += _ => InvokeSkillIfUnlocked(3);
        input.Player.Attack4.canceled += _ => InvokeSkillReleaseIfUnlocked(3);
        input.Player.Block.performed += _ => { };
    }

    private void InvokeIfUnlocked(Action action)
    {
        if (!controlLocked)
        {
            action?.Invoke();
        }
    }

    private void InvokeSkillIfUnlocked(int index)
    {
        if (!controlLocked && TryMarkSkillHeld(index))
        {
            SkillRequested?.Invoke(index);
        }
    }

    private void InvokeSkillReleaseIfUnlocked(int index)
    {
        if (!controlLocked && TryClearSkillHeld(index))
        {
            SkillReleased?.Invoke(index);
        }
    }

    private bool TryMarkSkillHeld(int index)
    {
        if (index < 0 || index >= skillHeld.Length)
        {
            return false;
        }

        if (skillHeld[index])
        {
            return false;
        }

        skillHeld[index] = true;
        return true;
    }

    private bool TryClearSkillHeld(int index)
    {
        if (index < 0 || index >= skillHeld.Length)
        {
            return false;
        }

        if (!skillHeld[index])
        {
            return false;
        }

        skillHeld[index] = false;
        return true;
    }

    private void ClearSkillHeldState()
    {
        for (int i = 0; i < skillHeld.Length; i++)
        {
            skillHeld[i] = false;
        }
    }

    private void ReleaseHeldActions()
    {
        ParryReleased?.Invoke();
        for (int i = 0; i < skillHeld.Length; i++)
        {
            if (!skillHeld[i])
            {
                continue;
            }

            skillHeld[i] = false;
            SkillReleased?.Invoke(i);
        }
    }

    private void TickSkillKeyBindings(System.Collections.Generic.IReadOnlyList<SkillSetSO> skillSets)
    {
        if (skillSets == null || Keyboard.current == null)
        {
            return;
        }

        for (int e = 0; e < skillSets.Count; e++)
        {
            SkillSetSO skillSet = skillSets[e];
            if (skillSet == null) continue;

            if (!skillSet.TryGetSkillSlot(0, out SkillSetSO.SkillSlot slot))
            {
                continue;
            }

            KeyCode key = slot.ActivationKey;
            if (key == KeyCode.None)
            {
                continue;
            }

            Key inputKey = ConvertKeyCodeToInputKey(key);
            if (inputKey == Key.None)
            {
                continue;
            }

            var keyControl = Keyboard.current[inputKey];
            if (keyControl != null && keyControl.wasPressedThisFrame)
            {
                InvokeSkillIfUnlocked(e);
            }

            if (keyControl != null && keyControl.wasReleasedThisFrame)
            {
                InvokeSkillReleaseIfUnlocked(e);
            }
        }
    }



    private static Key ConvertKeyCodeToInputKey(KeyCode keyCode)
    {
        return keyCode switch
        {
            KeyCode.Alpha0 => Key.Digit0,
            KeyCode.Alpha1 => Key.Digit1,
            KeyCode.Alpha2 => Key.Digit2,
            KeyCode.Alpha3 => Key.Digit3,
            KeyCode.Alpha4 => Key.Digit4,
            KeyCode.Alpha5 => Key.Digit5,
            KeyCode.Alpha6 => Key.Digit6,
            KeyCode.Alpha7 => Key.Digit7,
            KeyCode.Alpha8 => Key.Digit8,
            KeyCode.Alpha9 => Key.Digit9,
            KeyCode.A => Key.A,
            KeyCode.B => Key.B,
            KeyCode.C => Key.C,
            KeyCode.D => Key.D,
            KeyCode.E => Key.E,
            KeyCode.F => Key.F,
            KeyCode.G => Key.G,
            KeyCode.H => Key.H,
            KeyCode.I => Key.I,
            KeyCode.J => Key.J,
            KeyCode.K => Key.K,
            KeyCode.L => Key.L,
            KeyCode.M => Key.M,
            KeyCode.N => Key.N,
            KeyCode.O => Key.O,
            KeyCode.P => Key.P,
            KeyCode.Q => Key.Q,
            KeyCode.R => Key.R,
            KeyCode.S => Key.S,
            KeyCode.T => Key.T,
            KeyCode.U => Key.U,
            KeyCode.V => Key.V,
            KeyCode.W => Key.W,
            KeyCode.X => Key.X,
            KeyCode.Y => Key.Y,
            KeyCode.Z => Key.Z,
            KeyCode.Space => Key.Space,
            KeyCode.Escape => Key.Escape,
            KeyCode.Tab => Key.Tab,
            KeyCode.LeftShift => Key.LeftShift,
            KeyCode.RightShift => Key.RightShift,
            KeyCode.LeftControl => Key.LeftCtrl,
            KeyCode.RightControl => Key.RightCtrl,
            KeyCode.LeftAlt => Key.LeftAlt,
            KeyCode.RightAlt => Key.RightAlt,
            KeyCode.Backspace => Key.Backspace,
            KeyCode.Return => Key.Enter,
            KeyCode.Delete => Key.Delete,
            KeyCode.Insert => Key.Insert,
            KeyCode.Home => Key.Home,
            KeyCode.End => Key.End,
            KeyCode.PageUp => Key.PageUp,
            KeyCode.PageDown => Key.PageDown,
            KeyCode.UpArrow => Key.UpArrow,
            KeyCode.DownArrow => Key.DownArrow,
            KeyCode.LeftArrow => Key.LeftArrow,
            KeyCode.RightArrow => Key.RightArrow,
            KeyCode.F1 => Key.F1,
            KeyCode.F2 => Key.F2,
            KeyCode.F3 => Key.F3,
            KeyCode.F4 => Key.F4,
            KeyCode.F5 => Key.F5,
            KeyCode.F6 => Key.F6,
            KeyCode.F7 => Key.F7,
            KeyCode.F8 => Key.F8,
            KeyCode.F9 => Key.F9,
            KeyCode.F10 => Key.F10,
            KeyCode.F11 => Key.F11,
            KeyCode.F12 => Key.F12,
            _ => Key.None
        };
    }


    private static Vector2 ReadKeyboardMoveInput()

    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float x = 0f;
        float y = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            x -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            x += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            y -= 1f;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            y += 1f;
        }

        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }
}


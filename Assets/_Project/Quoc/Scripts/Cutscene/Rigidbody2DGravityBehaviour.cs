using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Runtime data của từng clip khóa trọng lực Rigidbody2D.
/// </summary>
[Serializable]
public class Rigidbody2DGravityBehaviour : PlayableBehaviour
{
    public Rigidbody2D targetRigidbodyOverride;
    public float lockedGravityScale = 0f;
    public bool lockYPosition = true;
    public bool restoreOriginalGravity = true;
    public float customRestoreGravityScale = 1f;
    public bool setKinematicWhileLocked = true;
    public bool freezeVelocityWhileLocked = true;
    public bool restoreLinearVelocityOnExit = false;
    public bool lockPlayerControl = false;
    public bool unlockPlayerControlOnExit = true;
    public bool pauseBossMovement = true;
    public bool applyInEditMode = true;

    /// <summary>
    /// Tìm Rigidbody2D từ override hoặc từ binding của Track (hỗ trợ cả Rigidbody2D, GameObject, Component).
    /// </summary>
    public Rigidbody2D ResolveTarget(object playerData)
    {
        if (targetRigidbodyOverride != null)
        {
            return targetRigidbodyOverride;
        }

        if (playerData is Rigidbody2D rb)
        {
            return rb;
        }

        if (playerData is GameObject go && go != null)
        {
            return go.GetComponent<Rigidbody2D>();
        }

        if (playerData is Component comp && comp != null)
        {
            return comp.GetComponent<Rigidbody2D>();
        }

        return null;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public enum ScriptActivationMode
{
    [Tooltip("Tắt script trong suốt thời gian clip chạy, tự động bật lại khi ra khỏi clip hoặc Timeline kết thúc.")]
    DisableDuringClip = 0,

    [Tooltip("Bật script trong suốt thời gian clip chạy, tự động tắt lại khi ra khỏi clip hoặc Timeline kết thúc.")]
    EnableDuringClip = 1,

    [Tooltip("Tắt script khi chạm vào clip và giữ nguyên trạng thái tắt (không tự động bật lại khi hết clip).")]
    DisableOnEnter = 2,

    [Tooltip("Bật script khi chạm vào clip và giữ nguyên trạng thái bật (không tự động tắt lại khi hết clip).")]
    EnableOnEnter = 3
}

/// <summary>
/// Runtime data của từng clip điều khiển bật/tắt (active/inactive) script trên Timeline.
/// </summary>
[Serializable]
public class ScriptActivationBehaviour : PlayableBehaviour
{
    public ScriptActivationMode mode = ScriptActivationMode.DisableDuringClip;
    public GameObject targetObjectOverride;
    public List<string> scriptNames = new List<string>();
    public bool affectAllMonoBehaviours = false;
    public bool includeChildren = false;
    public bool restoreOriginalStateOnExit = true;
    public bool applyInEditMode = true;

    /// <summary>
    /// Tìm GameObject mục tiêu từ override hoặc từ binding của Track.
    /// </summary>
    public GameObject ResolveTarget(object playerData)
    {
        if (targetObjectOverride != null)
        {
            return targetObjectOverride;
        }

        if (playerData is GameObject go && go != null)
        {
            return go;
        }

        if (playerData is Component comp && comp != null)
        {
            return comp.gameObject;
        }

        return null;
    }
}

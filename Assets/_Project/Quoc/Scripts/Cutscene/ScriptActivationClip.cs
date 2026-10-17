using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// IMPORTANT: Keep ScriptActivationClip in ScriptActivationClip.cs.
// Timeline stores PlayableAsset clips as ScriptableObject sub-assets.
// Giving the PlayableAsset its own matching script file makes its serialized type stable across Editor restarts.

/// <summary>
/// Clip asset chứa các thiết lập chọn và bật/tắt (inactive) script trên Timeline.
/// </summary>
[Serializable]
public class ScriptActivationClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Action Mode")]
    [Tooltip("Chế độ: Tắt/Bật script trong clip hoặc vĩnh viễn.")]
    public ScriptActivationMode mode = ScriptActivationMode.DisableDuringClip;

    [Header("Target Override (Tùy chọn)")]
    [Tooltip("Mặc định dùng GameObject đã bind trên Track. Nếu muốn chỉ định riêng đối tượng cho clip này, kéo vào đây.")]
    public ExposedReference<GameObject> targetObjectOverride;

    [Header("Scripts Selection")]
    [Tooltip("Danh sách tên class của các Script cần inactive/active (ví dụ: BossMovement, PlayerMover, BossController...).")]
    public List<string> scriptNames = new List<string>();

    [Tooltip("Tắt/Bật toàn bộ script MonoBehaviour trên GameObject mục tiêu (trừ PlayableDirector).")]
    public bool affectAllMonoBehaviours = false;

    [Tooltip("Tìm kiếm và áp dụng script trên cả các GameObject con (Children).")]
    public bool includeChildren = false;

    [Header("Restore & Preview Settings")]
    [Tooltip("Tự động khôi phục lại trạng thái enabled gốc của script trước khi vào clip (khi ra khỏi clip hoặc Timeline dừng).")]
    public bool restoreOriginalStateOnExit = true;

    [Tooltip("Cho phép preview bật/tắt script ngay trong Scene view ngoài Play Mode.")]
    public bool applyInEditMode = true;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<ScriptActivationBehaviour> playable =
            ScriptPlayable<ScriptActivationBehaviour>.Create(graph);
        ScriptActivationBehaviour behaviour = playable.GetBehaviour();

        IExposedPropertyTable resolver = graph.GetResolver();

        behaviour.mode = mode;
        behaviour.targetObjectOverride = targetObjectOverride.Resolve(resolver);
        behaviour.scriptNames = scriptNames != null ? new List<string>(scriptNames) : new List<string>();
        behaviour.affectAllMonoBehaviours = affectAllMonoBehaviours;
        behaviour.includeChildren = includeChildren;
        behaviour.restoreOriginalStateOnExit = restoreOriginalStateOnExit;
        behaviour.applyInEditMode = applyInEditMode;

        return playable;
    }
}

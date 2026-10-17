using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// IMPORTANT: Keep Rigidbody2DGravityClip in Rigidbody2DGravityClip.cs.
// Timeline stores PlayableAsset clips as ScriptableObject sub-assets.
// Giving the PlayableAsset its own matching script file makes its serialized type stable across Editor restarts.

/// <summary>
/// Clip asset chứa các thiết lập khóa trọng lực trên Timeline.
/// </summary>
[Serializable]
public class Rigidbody2DGravityClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Target Override (Tùy chọn)")]
    [Tooltip("Mặc định dùng Rigidbody2D đã bind trên Track. Nếu muốn chỉ định riêng đối tượng cho clip này, kéo vào đây.")]
    public ExposedReference<Rigidbody2D> targetRigidbodyOverride;

    [Header("Gravity & Position Locking")]
    [Tooltip("Hệ số trọng lực áp dụng trong suốt thời gian clip (0 = tắt/khóa hoàn toàn trọng lực).")]
    public float lockedGravityScale = 0f;

    [Tooltip("Khóa cố định độ cao trục Y (không để bất kỳ lực hay script nào kéo tụt vị trí xuống).")]
    public bool lockYPosition = true;

    [Tooltip("Tự động khôi phục lại gravityScale gốc trước khi vào clip.")]
    public bool restoreOriginalGravity = true;

    [Tooltip("Nếu không khôi phục giá trị gốc, sẽ đặt gravityScale thành giá trị này khi clip kết thúc.")]
    public float customRestoreGravityScale = 1f;

    [Header("Physics & Kinematic Locking")]
    [Tooltip("Chuyển bodyType sang Kinematic trong clip để physics Unity không kéo rơi đối tượng.")]
    public bool setKinematicWhileLocked = true;

    [Tooltip("Triệt tiêu vận tốc linearVelocity về (0,0) mỗi frame trong suốt thời gian clip.")]
    public bool freezeVelocityWhileLocked = true;

    [Tooltip("Khôi phục lại vận tốc ban đầu khi clip kết thúc.")]
    public bool restoreLinearVelocityOnExit = false;

    [Header("Player & Boss Integration")]
    [Tooltip("Nếu đối tượng là Player, tự động khóa điều khiển (SetControlLocked(true)) trong khi clip chạy.")]
    public bool lockPlayerControl = false;

    [Tooltip("Mở khóa điều khiển Player khi clip kết thúc.")]
    public bool unlockPlayerControlOnExit = true;

    [Tooltip("Nếu đối tượng là Boss (có BossMovement/BossController), tự động tạm dừng để Boss không bị kéo xuống đất bằng script raycast.")]
    public bool pauseBossMovement = true;

    [Header("Editor Preview")]
    [Tooltip("Cho phép khóa trọng lực khi preview/play Timeline ngay trong Scene view ngoài Play Mode.")]
    public bool applyInEditMode = true;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<Rigidbody2DGravityBehaviour> playable =
            ScriptPlayable<Rigidbody2DGravityBehaviour>.Create(graph);
        Rigidbody2DGravityBehaviour behaviour = playable.GetBehaviour();

        behaviour.targetRigidbodyOverride = targetRigidbodyOverride.Resolve(graph.GetResolver());
        behaviour.lockedGravityScale = lockedGravityScale;
        behaviour.lockYPosition = lockYPosition;
        behaviour.restoreOriginalGravity = restoreOriginalGravity;
        behaviour.customRestoreGravityScale = customRestoreGravityScale;
        behaviour.setKinematicWhileLocked = setKinematicWhileLocked;
        behaviour.freezeVelocityWhileLocked = freezeVelocityWhileLocked;
        behaviour.restoreLinearVelocityOnExit = restoreLinearVelocityOnExit;
        behaviour.lockPlayerControl = lockPlayerControl;
        behaviour.unlockPlayerControlOnExit = unlockPlayerControlOnExit;
        behaviour.pauseBossMovement = pauseBossMovement;
        behaviour.applyInEditMode = applyInEditMode;

        return playable;
    }
}

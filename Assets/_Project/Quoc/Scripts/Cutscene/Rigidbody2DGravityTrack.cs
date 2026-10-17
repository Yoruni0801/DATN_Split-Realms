using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Quoc.Boss;

/// <summary>
/// Timeline Track dùng trong Cutscene để khóa/tắt trọng lực của Rigidbody2D khi đi vào Clip,
/// và tự động khôi phục lại trọng lực ban đầu khi clip kết thúc (hoặc Timeline dừng).
/// </summary>
[TrackColor(0.2f, 0.65f, 0.85f)]
[TrackClipType(typeof(Rigidbody2DGravityClip))]
[TrackBindingType(typeof(Rigidbody2D))]
[System.ComponentModel.DisplayName("TimelineTrack/Rigidbody2D Gravity Lock")]
public class Rigidbody2DGravityTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<Rigidbody2DGravityMixerBehaviour>.Create(graph, inputCount);
    }
}

/// <summary>
/// Mixer behaviour quản lý việc lưu trữ trạng thái gốc, áp dụng khóa trọng lực và hoàn trả khi hết clip/track.
/// </summary>
public class Rigidbody2DGravityMixerBehaviour : PlayableBehaviour
{
    private Rigidbody2D boundTarget;
    private Player cachedPlayer;
    private PlayerMover cachedMover;
    private BossMovement cachedBossMovement;
    private BossController cachedBossController;

    private bool isLocked = false;
    private float originalGravityScale = 1f;
    private RigidbodyType2D originalBodyType = RigidbodyType2D.Dynamic;
    private RigidbodyConstraints2D originalConstraints = RigidbodyConstraints2D.None;
    private Vector2 originalLinearVelocity = Vector2.zero;
    private float originalAngularVelocity = 0f;
    private float lockedPositionY = 0f;
    private bool originalBossMovementEnabled = true;

    // Restore config từ active behaviour gần nhất
    private bool restoreOriginalGravity = true;
    private float customRestoreGravityScale = 1f;
    private bool restoreBodyType = true;
    private bool restoreLinearVelocity = false;
    private bool unlockPlayerControlOnExit = true;
    private bool restoreBossMovement = true;

    private int previousActiveClipIndex = -1;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        int inputCount = playable.GetInputCount();
        int activeClipIndex = -1;
        float maxWeight = 0f;

        // Tìm clip đang active có weight lớn nhất
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight > 0f && weight > maxWeight)
            {
                maxWeight = weight;
                activeClipIndex = i;
            }
        }

        // Nếu chuyển từ clip này sang clip khác, restore clip cũ trước để refresh baseline
        if (previousActiveClipIndex != -1 && previousActiveClipIndex != activeClipIndex)
        {
            RestoreState();
        }

        previousActiveClipIndex = activeClipIndex;

        // Trường hợp không có clip nào active (ở ngoài clip, trước/sau track, hoặc trong khoảng trống giữa các clip)
        if (activeClipIndex < 0)
        {
            if (isLocked)
            {
                RestoreState();
            }
            return;
        }

        Playable inputPlayableRaw = playable.GetInput(activeClipIndex);
        if (!inputPlayableRaw.IsValid())
        {
            if (isLocked)
            {
                RestoreState();
            }
            return;
        }

        ScriptPlayable<Rigidbody2DGravityBehaviour> inputPlayable =
            (ScriptPlayable<Rigidbody2DGravityBehaviour>)inputPlayableRaw;
        Rigidbody2DGravityBehaviour behaviour = inputPlayable.GetBehaviour();

        if (behaviour == null)
        {
            if (isLocked)
            {
                RestoreState();
            }
            return;
        }

        // Kiểm tra chế độ Edit Mode
        if (!Application.isPlaying && !behaviour.applyInEditMode)
        {
            if (isLocked)
            {
                RestoreState();
            }
            return;
        }

        Rigidbody2D target = behaviour.ResolveTarget(playerData);
        if (target == null)
        {
            if (isLocked)
            {
                RestoreState();
            }
            return;
        }

        // Nếu target thay đổi giữa chừng
        if (isLocked && boundTarget != target)
        {
            RestoreState();
        }

        // Bắt đầu khóa trạng thái khi vừa vào clip
        if (!isLocked)
        {
            LockState(target, behaviour);
        }

        // Khóa liên tục mỗi frame trong suốt thời gian clip
        ApplyLockedStateEveryFrame(behaviour);
    }

    private void LockState(Rigidbody2D target, Rigidbody2DGravityBehaviour behaviour)
    {
        boundTarget = target;
        originalGravityScale = target.gravityScale;
        originalBodyType = target.bodyType;
        originalConstraints = target.constraints;
        originalLinearVelocity = GetLinearVelocity(target);
        originalAngularVelocity = target.angularVelocity;
        lockedPositionY = target.transform.position.y;

        // Lưu cài đặt khôi phục từ clip
        restoreOriginalGravity = behaviour.restoreOriginalGravity;
        customRestoreGravityScale = behaviour.customRestoreGravityScale;
        restoreBodyType = behaviour.setKinematicWhileLocked;
        restoreLinearVelocity = behaviour.restoreLinearVelocityOnExit;
        unlockPlayerControlOnExit = behaviour.lockPlayerControl && behaviour.unlockPlayerControlOnExit;
        restoreBossMovement = behaviour.pauseBossMovement;

        // 1. Resolve Player & PlayerMover nếu target là Player
        try
        {
            cachedPlayer = target.GetComponent<Player>() ?? target.GetComponentInParent<Player>();
            if (cachedPlayer != null)
            {
                cachedMover = cachedPlayer.Mover;
            }
            else
            {
                cachedMover = target.GetComponent<PlayerMover>();
            }
        }
        catch
        {
            cachedPlayer = null;
            cachedMover = null;
        }

        // 2. Resolve Boss components nếu target là Boss
        try
        {
            cachedBossMovement = target.GetComponent<BossMovement>() ?? target.GetComponentInParent<BossMovement>();
            cachedBossController = target.GetComponent<BossController>() ?? target.GetComponentInParent<BossController>();
            if (cachedBossMovement != null)
            {
                originalBossMovementEnabled = cachedBossMovement.enabled;
            }
        }
        catch
        {
            cachedBossMovement = null;
            cachedBossController = null;
        }

        isLocked = true;

        // Khóa điều khiển Player nếu được bật
        if (behaviour.lockPlayerControl && cachedPlayer != null)
        {
            try
            {
                cachedPlayer.SetControlLocked(true);
            }
            catch { }
        }

        // Tạm dừng Boss Aggro
        if (behaviour.pauseBossMovement && cachedBossController != null)
        {
            try
            {
                cachedBossController.DisableAggro();
            }
            catch { }
        }
    }

    private void ApplyLockedStateEveryFrame(Rigidbody2DGravityBehaviour behaviour)
    {
        if (!isLocked || boundTarget == null) return;

        try
        {
            // 1. Chuyển bodyType sang Kinematic để Unity Physics hoàn toàn không tác động trọng lực / va chạm rơi
            if (behaviour.setKinematicWhileLocked && boundTarget.bodyType != RigidbodyType2D.Kinematic)
            {
                boundTarget.bodyType = RigidbodyType2D.Kinematic;
            }

            // 2. Giữ gravityScale = 0
            if (boundTarget.bodyType != RigidbodyType2D.Static)
            {
                boundTarget.gravityScale = behaviour.lockedGravityScale;
            }

            // 3. Triệt tiêu vận tốc liên tục mỗi frame
            if (behaviour.freezeVelocityWhileLocked)
            {
                SetLinearVelocity(boundTarget, Vector2.zero);
                boundTarget.angularVelocity = 0f;
            }

            // 4. Khóa giữ nguyên độ cao Y (chống rơi tuyệt đối)
            if (behaviour.lockYPosition)
            {
                Vector3 currentPos = boundTarget.transform.position;
                if (Mathf.Abs(currentPos.y - lockedPositionY) > 0.0001f)
                {
                    boundTarget.transform.position = new Vector3(currentPos.x, lockedPositionY, currentPos.z);
                }
            }

            // 5. Nếu là Player Mover
            if (cachedMover != null)
            {
                cachedMover.SetGravityScale(behaviour.lockedGravityScale);
                cachedMover.ZeroVelocity();
            }

            // 6. Nếu là Boss Movement: Tắt script và triệt tiêu vận tốc dọc
            if (behaviour.pauseBossMovement && cachedBossMovement != null)
            {
                if (cachedBossMovement.enabled)
                {
                    cachedBossMovement.enabled = false;
                }
                cachedBossMovement.SetVerticalVelocity(0f);
            }
        }
        catch { }
    }

    private void RestoreState()
    {
        if (!isLocked)
        {
            return;
        }

        if (boundTarget != null)
        {
            try
            {
                // 1. Khôi phục bodyType trước
                if (restoreBodyType && boundTarget.bodyType != RigidbodyType2D.Static)
                {
                    boundTarget.bodyType = originalBodyType;
                }

                // 2. Khôi phục trọng lực
                float targetGravity = restoreOriginalGravity ? originalGravityScale : customRestoreGravityScale;
                if (boundTarget.bodyType != RigidbodyType2D.Static)
                {
                    boundTarget.gravityScale = targetGravity;
                }

                // 3. Khôi phục constraints
                boundTarget.constraints = originalConstraints;

                // 4. Khôi phục vận tốc nếu được yêu cầu (mặc định reset về 0 để không bị văng)
                if (restoreLinearVelocity)
                {
                    SetLinearVelocity(boundTarget, originalLinearVelocity);
                    boundTarget.angularVelocity = originalAngularVelocity;
                }
                else
                {
                    SetLinearVelocity(boundTarget, Vector2.zero);
                    boundTarget.angularVelocity = 0f;
                }
            }
            catch { }
        }

        // Khôi phục Player
        if (cachedMover != null)
        {
            try
            {
                float targetGravity = restoreOriginalGravity ? originalGravityScale : customRestoreGravityScale;
                cachedMover.SetGravityScale(targetGravity);
                cachedMover.RestoreGravity();
            }
            catch { }
        }

        if (unlockPlayerControlOnExit && cachedPlayer != null)
        {
            try
            {
                cachedPlayer.SetControlLocked(false);
            }
            catch { }
        }

        // Khôi phục BossMovement & Aggro
        if (restoreBossMovement)
        {
            if (cachedBossMovement != null)
            {
                try
                {
                    cachedBossMovement.enabled = originalBossMovementEnabled;
                    cachedBossMovement.SetVerticalVelocity(0f);
                }
                catch { }
            }

            if (cachedBossController != null)
            {
                try
                {
                    cachedBossController.EnableAggro();
                }
                catch { }
            }
        }

        isLocked = false;
        boundTarget = null;
        cachedPlayer = null;
        cachedMover = null;
        cachedBossMovement = null;
        cachedBossController = null;
    }

    public override void OnGraphStop(Playable playable)
    {
        previousActiveClipIndex = -1;
        RestoreState();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        previousActiveClipIndex = -1;
        RestoreState();
    }

    private static void SetLinearVelocity(Rigidbody2D rb, Vector2 velocity)
    {
        if (rb == null || rb.bodyType == RigidbodyType2D.Static) return;
        try
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = velocity;
#else
            rb.velocity = velocity;
#endif
        }
        catch { }
    }

    private static Vector2 GetLinearVelocity(Rigidbody2D rb)
    {
        if (rb == null || rb.bodyType == RigidbodyType2D.Static) return Vector2.zero;
        try
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }
        catch
        {
            return Vector2.zero;
        }
    }
}

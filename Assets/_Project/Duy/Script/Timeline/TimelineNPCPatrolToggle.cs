using UnityEngine;

[AddComponentMenu("Timeline/Timeline NPC Patrol Toggle")]
[DisallowMultipleComponent]
public sealed class TimelineNPCPatrolToggle : MonoBehaviour
{
    [Tooltip("Danh sách NPC cụ thể. Để trống = áp dụng cho TẤT CẢ NPC trong scene.")]
    [SerializeField] private NPCRandomPatrol[] targets;

    [SerializeField] private bool includeInactive;

    public enum PostPlaybackAction
    {
        EnableAll,
        DisableAll,
        DoNothing
    }

    [Header("Post-Playback (Timeline Stop)")]
    [Tooltip("Tự động áp dụng trạng thái sau khi Timeline dừng nếu có gắn PlayableDirector.")]
    [SerializeField] private UnityEngine.Playables.PlayableDirector director;
    [SerializeField] private PostPlaybackAction postPlaybackState = PostPlaybackAction.EnableAll;

    private void OnEnable()
    {
        if (director != null)
        {
            director.stopped += OnTimelineStopped;
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.stopped -= OnTimelineStopped;
        }
    }

    private void OnTimelineStopped(UnityEngine.Playables.PlayableDirector pd)
    {
        switch (postPlaybackState)
        {
            case PostPlaybackAction.EnableAll:
                EnablePatrol();
                break;
            case PostPlaybackAction.DisableAll:
                DisablePatrol();
                break;
            case PostPlaybackAction.DoNothing:
            default:
                break;
        }
    }

    /// <summary>
    /// Bật patrol cho các targets (hoặc tất cả NPC nếu targets rỗng). Dùng cho Timeline Signal Receiver.
    /// </summary>
    public void EnablePatrol()
    {
        SetPatrol(true);
    }

    /// <summary>
    /// Tắt patrol cho các targets (hoặc tất cả NPC nếu targets rỗng). Dùng cho Timeline Signal Receiver.
    /// </summary>
    public void DisablePatrol()
    {
        SetPatrol(false);
    }

    public void SetPatrol(bool enable)
    {
        if (targets != null && targets.Length > 0)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].SetEnablePatrol(enable);
                }
            }
            return;
        }

        NPCRandomPatrol.SetAllPatrolEnabled(enable, includeInactive);
    }

    public void SetInvertFacing(bool invert)
    {
        if (targets != null && targets.Length > 0)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].SetInvertFacing(invert);
                }
            }
            return;
        }

        NPCRandomPatrol.SetAllInvertFacing(invert, includeInactive);
    }
}

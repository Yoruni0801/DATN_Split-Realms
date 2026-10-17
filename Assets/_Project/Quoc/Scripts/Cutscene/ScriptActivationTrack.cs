using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Timeline Track dùng để bật/tắt (active/inactive) các script cụ thể trên một GameObject khi Timeline chạy đến Clip,
/// và tự động khôi phục lại trạng thái ban đầu của script khi clip kết thúc (hoặc khi Timeline dừng).
/// </summary>
[TrackColor(0.85f, 0.25f, 0.35f)]
[TrackClipType(typeof(ScriptActivationClip))]
[TrackBindingType(typeof(GameObject))]
[System.ComponentModel.DisplayName("TimelineTrack/Script Activation (Enable-Disable)")]
public class ScriptActivationTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<ScriptActivationMixerBehaviour>.Create(graph, inputCount);
    }
}

/// <summary>
/// Mixer behaviour quản lý việc lưu trữ trạng thái gốc của các script, áp dụng bật/tắt trong clip và hoàn trả khi hết clip/track.
/// </summary>
public class ScriptActivationMixerBehaviour : PlayableBehaviour
{
    private readonly Dictionary<Behaviour, bool> originalStates = new Dictionary<Behaviour, bool>();
    private readonly HashSet<Behaviour> trackedBehaviours = new HashSet<Behaviour>();

    private bool isApplied = false;
    private int previousActiveClipIndex = -1;
    private ScriptActivationMode currentMode = ScriptActivationMode.DisableDuringClip;
    private bool currentRestoreOnExit = true;

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

        // Nếu chuyển từ clip này sang clip khác, restore trạng thái clip cũ trước
        if (previousActiveClipIndex != -1 && previousActiveClipIndex != activeClipIndex)
        {
            RestoreState();
        }

        previousActiveClipIndex = activeClipIndex;

        // Nếu không có clip nào active (ngoài clip hoặc trong khoảng trống)
        if (activeClipIndex < 0)
        {
            if (isApplied)
            {
                RestoreState();
            }
            return;
        }

        Playable inputPlayableRaw = playable.GetInput(activeClipIndex);
        if (!inputPlayableRaw.IsValid())
        {
            if (isApplied)
            {
                RestoreState();
            }
            return;
        }

        ScriptActivationBehaviour behaviour = null;
        try
        {
            var inputPlayable = (ScriptPlayable<ScriptActivationBehaviour>)inputPlayableRaw;
            behaviour = inputPlayable.GetBehaviour();
        }
        catch
        {
            behaviour = null;
        }

        if (behaviour == null)
        {
            if (isApplied)
            {
                RestoreState();
            }
            return;
        }

        // Kiểm tra chế độ Edit Mode
        if (!Application.isPlaying && !behaviour.applyInEditMode)
        {
            if (isApplied)
            {
                RestoreState();
            }
            return;
        }

        GameObject target = behaviour.ResolveTarget(playerData);
        if (target == null)
        {
            if (isApplied)
            {
                RestoreState();
            }
            return;
        }

        currentMode = behaviour.mode;
        currentRestoreOnExit = behaviour.restoreOriginalStateOnExit;

        // Lần đầu vào clip: tìm các script cần quản lý và lưu trạng thái gốc
        if (!isApplied)
        {
            ApplyInitialState(target, behaviour);
        }

        // Duy trì trạng thái mong muốn liên tục trong suốt clip
        MaintainState(behaviour);
    }

    private void ApplyInitialState(GameObject target, ScriptActivationBehaviour behaviour)
    {
        originalStates.Clear();
        trackedBehaviours.Clear();

        List<Behaviour> targetsToProcess = CollectTargetBehaviours(target, behaviour);

        bool targetEnabled = (behaviour.mode == ScriptActivationMode.EnableDuringClip ||
                              behaviour.mode == ScriptActivationMode.EnableOnEnter);

        for (int i = 0; i < targetsToProcess.Count; i++)
        {
            Behaviour comp = targetsToProcess[i];
            if (comp == null) continue;

            if (!originalStates.ContainsKey(comp))
            {
                try
                {
                    originalStates[comp] = comp.enabled;
                }
                catch { }
            }

            trackedBehaviours.Add(comp);

            try
            {
                comp.enabled = targetEnabled;
            }
            catch { }
        }

        isApplied = true;
    }

    private void MaintainState(ScriptActivationBehaviour behaviour)
    {
        if (!isApplied) return;

        bool targetEnabled = (behaviour.mode == ScriptActivationMode.EnableDuringClip ||
                              behaviour.mode == ScriptActivationMode.EnableOnEnter);

        foreach (Behaviour comp in trackedBehaviours)
        {
            if (comp == null) continue;

            try
            {
                if (comp.enabled != targetEnabled)
                {
                    comp.enabled = targetEnabled;
                }
            }
            catch { }
        }
    }

    private List<Behaviour> CollectTargetBehaviours(GameObject target, ScriptActivationBehaviour behaviour)
    {
        List<Behaviour> result = new List<Behaviour>();
        if (target == null) return result;

        // 1. Nếu chọn affectAllMonoBehaviours (Tất cả script trên target)
        if (behaviour.affectAllMonoBehaviours)
        {
            try
            {
                MonoBehaviour[] allScripts = behaviour.includeChildren
                    ? target.GetComponentsInChildren<MonoBehaviour>(true)
                    : target.GetComponents<MonoBehaviour>();

                for (int i = 0; i < allScripts.Length; i++)
                {
                    MonoBehaviour mb = allScripts[i];
                    if (mb == null) continue;
                    if (mb is PlayableDirector) continue;
                    if (!result.Contains(mb))
                    {
                        result.Add(mb);
                    }
                }
            }
            catch { }

            return result;
        }

        // 2. Tìm theo danh sách scriptNames
        if (behaviour.scriptNames != null && behaviour.scriptNames.Count > 0)
        {
            try
            {
                Behaviour[] allBehaviours = behaviour.includeChildren
                    ? target.GetComponentsInChildren<Behaviour>(true)
                    : target.GetComponents<Behaviour>();

                for (int i = 0; i < allBehaviours.Length; i++)
                {
                    Behaviour b = allBehaviours[i];
                    if (b == null) continue;
                    if (b is PlayableDirector) continue;

                    Type compType = b.GetType();
                    string typeName = compType.Name;
                    string typeFullName = compType.FullName;

                    for (int j = 0; j < behaviour.scriptNames.Count; j++)
                    {
                        string rawName = behaviour.scriptNames[j];
                        if (string.IsNullOrEmpty(rawName)) continue;
                        string searchName = rawName.Trim();

                        bool matches = string.Equals(typeName, searchName, StringComparison.OrdinalIgnoreCase) ||
                                       (typeFullName != null && string.Equals(typeFullName, searchName, StringComparison.OrdinalIgnoreCase));

                        // Hỗ trợ kiểm tra cả lớp cha (BaseType)
                        if (!matches && compType.BaseType != null)
                        {
                            matches = string.Equals(compType.BaseType.Name, searchName, StringComparison.OrdinalIgnoreCase);
                        }

                        if (matches)
                        {
                            if (!result.Contains(b))
                            {
                                result.Add(b);
                            }
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        return result;
    }

    private void RestoreState()
    {
        if (!isApplied)
        {
            return;
        }

        // Chỉ khôi phục nếu chế độ là DuringClip và restoreOriginalStateOnExit bật
        bool shouldRestore = currentRestoreOnExit &&
            (currentMode == ScriptActivationMode.DisableDuringClip ||
             currentMode == ScriptActivationMode.EnableDuringClip);

        if (shouldRestore)
        {
            foreach (KeyValuePair<Behaviour, bool> kvp in originalStates)
            {
                if (kvp.Key != null)
                {
                    try
                    {
                        kvp.Key.enabled = kvp.Value;
                    }
                    catch { }
                }
            }
        }

        originalStates.Clear();
        trackedBehaviours.Clear();
        isApplied = false;
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
}

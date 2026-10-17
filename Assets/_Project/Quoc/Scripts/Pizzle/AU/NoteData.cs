using System;
using UnityEngine;

// 4 lane cố định cho mini game rhythm.
public enum LaneType
{
    W = 0,
    A = 1,
    S = 2,
    D = 3
}

[Serializable]
public struct NoteData
{
    [Tooltip("Thời điểm note cần được bấm, tính bằng giây từ lúc bài nhạc bắt đầu.")]
    public float hitTime;

    [Tooltip("Lane của note.")]
    public LaneType lane;

    public NoteData(float hitTime, LaneType lane)
    {
        this.hitTime = hitTime;
        this.lane = lane;
    }
}

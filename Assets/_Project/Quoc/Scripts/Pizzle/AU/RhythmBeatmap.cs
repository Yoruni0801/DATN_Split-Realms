using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Quoc/Rhythm Mini Game/Beatmap", fileName = "RhythmBeatmap")]
public class RhythmBeatmap : ScriptableObject
{
    [Tooltip("Danh sách note của beatmap. Sắp xếp theo hitTime tăng dần để dễ đọc.")]
    public List<NoteData> notes = new List<NoteData>();
}

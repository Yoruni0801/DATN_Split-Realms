using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RhythmGameManager : MonoBehaviour
{
    public enum JudgmentType
    {
        Perfect,
        Good,
        Miss
    }

    [Header("Beatmap")]
    [SerializeField] private RhythmBeatmap beatmapAsset;

    [Tooltip("Dùng khi không gán beatmap ScriptableObject.")]
    [SerializeField] private List<NoteData> inspectorNotes = new List<NoteData>();

    [Header("References")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private NoteSpawner noteSpawner;

    [Header("Timing")]
    [Tooltip("Delay nhỏ trước khi nhạc bắt đầu để có thể spawn note sớm hơn.")]
    [SerializeField] private float startDelay = 0.5f;

    [Tooltip("Note sẽ được spawn trước hitTime bao lâu.")]
    [SerializeField] private float spawnLeadTime = 2f;

    [Tooltip("Sai lệch thời gian để được Perfect.")]
    [SerializeField] private float perfectWindow = 0.08f;

    [Tooltip("Sai lệch thời gian để được Good.")]
    [SerializeField] private float goodWindow = 0.16f;

    [Tooltip("Quá cửa sổ này thì tính Miss.")]
    [SerializeField] private float missWindow = 0.20f;

    [Header("Scoring")]
    [SerializeField] private int perfectScore = 1000;
    [SerializeField] private int goodScore = 500;
    [SerializeField] private int missScorePenalty = 0;

    [Header("Auto")]
    [SerializeField] private bool autoStartOnPlay = true;

    public event Action<int, int, int> OnScoreChanged;
    public event Action<string> OnJudgmentChanged;
    public event Action<int> OnComboChanged;

    private readonly List<NoteData> runtimeBeatmap = new List<NoteData>();
    private readonly List<RhythmNote>[] activeNotesByLane =
    {
        new List<RhythmNote>(),
        new List<RhythmNote>(),
        new List<RhythmNote>(),
        new List<RhythmNote>()
    };

    private double songStartDspTime;
    private bool songScheduled;
    private bool gameRunning;

    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int MaxCombo { get; private set; }

    public float CurrentSongTime => songScheduled ? (float)(AudioSettings.dspTime - songStartDspTime) : 0f;
    public float SpawnLeadTime => spawnLeadTime;
    public float PerfectWindow => perfectWindow;
    public float GoodWindow => goodWindow;
    public float MissWindow => missWindow;
    public bool IsRunning => gameRunning;

    private void Reset()
    {
        inspectorNotes = CreateSampleBeatmap();
    }

    private void Awake()
    {
        PrepareLaneCache();
    }

    private void Start()
    {
        if (autoStartOnPlay)
        {
            StartGame();
        }
    }

    private void Update()
    {
        if (!gameRunning)
        {
            return;
        }

        // Khi nhạc đã kết thúc nhưng còn note tồn tại, game vẫn chạy cho tới khi toàn bộ note được xử lý.
        if (musicSource != null && !musicSource.isPlaying && CurrentSongTime > GetLastNoteTime() + missWindow + 0.5f)
        {
            EndGame();
        }
    }

    public void StartGame()
    {
        if (musicSource == null)
        {
            Debug.LogWarning("[RhythmGameManager] Chưa gán AudioSource.");
            return;
        }

        if (musicSource.clip == null)
        {
            Debug.LogWarning("[RhythmGameManager] AudioSource chưa có clip nhạc.");
            return;
        }

        PrepareLaneCache();
        ClearActiveNotes();
        runtimeBeatmap.Clear();
        runtimeBeatmap.AddRange(GetSourceBeatmap().OrderBy(note => note.hitTime));

        Score = 0;
        Combo = 0;
        MaxCombo = 0;

        songStartDspTime = AudioSettings.dspTime + startDelay;
        songScheduled = true;
        gameRunning = true;

        musicSource.Stop();
        musicSource.PlayScheduled(songStartDspTime);

        if (noteSpawner != null)
        {
            noteSpawner.Begin(this, runtimeBeatmap);
        }
        else
        {
            Debug.LogWarning("[RhythmGameManager] Chưa gán NoteSpawner.");
        }

        PushStateToUI();
        ShowJudgment(string.Empty);
    }

    public void EndGame()
    {
        if (!gameRunning)
        {
            return;
        }

        gameRunning = false;
        songScheduled = false;

        if (musicSource != null)
        {
            musicSource.Stop();
        }

        noteSpawner?.Stop();
        ClearActiveNotes();
    }

    public void PressLane(LaneType lane)
    {
        if (!gameRunning)
        {
            return;
        }

        float songTime = CurrentSongTime;
        RhythmNote candidate = FindBestCandidate(lane, songTime);

        if (candidate == null)
        {
            RegisterMiss(null);
            return;
        }

        float delta = Mathf.Abs(songTime - candidate.HitTime);
        JudgmentType judgment = GetJudgmentByDelta(delta);

        if (judgment == JudgmentType.Miss)
        {
            RegisterMiss(candidate);
            return;
        }

        RegisterHit(candidate, judgment);
    }

    public void RegisterSpawnedNote(RhythmNote note)
    {
        if (note == null)
        {
            return;
        }

        int laneIndex = LaneToIndex(note.Lane);
        if (!activeNotesByLane[laneIndex].Contains(note))
        {
            activeNotesByLane[laneIndex].Add(note);
        }
    }

    public void UnregisterNote(RhythmNote note)
    {
        if (note == null)
        {
            return;
        }

        int laneIndex = LaneToIndex(note.Lane);
        activeNotesByLane[laneIndex].Remove(note);
    }

    public void NotifyAutoMiss(RhythmNote note)
    {
        if (note == null || note.IsJudged)
        {
            return;
        }

        note.MarkMissed();
        UnregisterNote(note);
        ApplyMissFeedback();
    }

    public void ShowJudgment(string text)
    {
        OnJudgmentChanged?.Invoke(text);
    }

    public void PushStateToUI()
    {
        OnScoreChanged?.Invoke(Score, Combo, MaxCombo);
        OnComboChanged?.Invoke(Combo);
    }

    private void RegisterHit(RhythmNote note, JudgmentType judgment)
    {
        if (note == null || note.IsJudged)
        {
            return;
        }

        note.MarkHit();
        UnregisterNote(note);

        if (judgment == JudgmentType.Perfect)
        {
            Score += perfectScore;
            Combo++;
            ShowJudgment("Perfect");
        }
        else if (judgment == JudgmentType.Good)
        {
            Score += goodScore;
            Combo++;
            ShowJudgment("Good");
        }

        MaxCombo = Mathf.Max(MaxCombo, Combo);
        PushStateToUI();
    }

    private void RegisterMiss(RhythmNote note)
    {
        if (note != null && !note.IsJudged)
        {
            note.MarkMissed();
            UnregisterNote(note);
        }

        ApplyMissFeedback();
    }

    private void ApplyMissFeedback()
    {
        Combo = 0;
        Score = Mathf.Max(0, Score - missScorePenalty);
        ShowJudgment("Miss");
        PushStateToUI();
    }

    private JudgmentType GetJudgmentByDelta(float delta)
    {
        if (delta <= perfectWindow)
        {
            return JudgmentType.Perfect;
        }

        if (delta <= goodWindow)
        {
            return JudgmentType.Good;
        }

        return JudgmentType.Miss;
    }

    private RhythmNote FindBestCandidate(LaneType lane, float songTime)
    {
        List<RhythmNote> notes = activeNotesByLane[LaneToIndex(lane)];
        RhythmNote best = null;
        float bestDelta = float.MaxValue;

        for (int i = 0; i < notes.Count; i++)
        {
            RhythmNote note = notes[i];
            if (note == null || note.IsJudged)
            {
                continue;
            }

            float delta = Mathf.Abs(songTime - note.HitTime);
            if (delta > goodWindow)
            {
                continue;
            }

            if (delta < bestDelta || (Mathf.Approximately(delta, bestDelta) && note.HitTime < best.HitTime))
            {
                best = note;
                bestDelta = delta;
            }
        }

        return best;
    }

    private void PrepareLaneCache()
    {
        for (int i = 0; i < activeNotesByLane.Length; i++)
        {
            if (activeNotesByLane[i] == null)
            {
                activeNotesByLane[i] = new List<RhythmNote>();
            }
        }
    }

    private void ClearActiveNotes()
    {
        for (int i = 0; i < activeNotesByLane.Length; i++)
        {
            activeNotesByLane[i].Clear();
        }
    }

    private List<NoteData> GetSourceBeatmap()
    {
        if (beatmapAsset != null && beatmapAsset.notes != null && beatmapAsset.notes.Count > 0)
        {
            return beatmapAsset.notes;
        }

        return inspectorNotes;
    }

    private float GetLastNoteTime()
    {
        List<NoteData> notes = runtimeBeatmap.Count > 0 ? runtimeBeatmap : GetSourceBeatmap();
        if (notes == null || notes.Count == 0)
        {
            return 0f;
        }

        return notes[notes.Count - 1].hitTime;
    }

    private static int LaneToIndex(LaneType lane)
    {
        return (int)lane;
    }

    private static List<NoteData> CreateSampleBeatmap()
    {
        // Beatmap mẫu khoảng 20 note để test ngay.
        return new List<NoteData>
        {
            new NoteData(1.20f, LaneType.W),
            new NoteData(1.60f, LaneType.A),
            new NoteData(2.00f, LaneType.S),
            new NoteData(2.40f, LaneType.D),
            new NoteData(2.80f, LaneType.W),
            new NoteData(3.10f, LaneType.D),
            new NoteData(3.50f, LaneType.A),
            new NoteData(3.90f, LaneType.S),
            new NoteData(4.30f, LaneType.W),
            new NoteData(4.80f, LaneType.W),
            new NoteData(5.20f, LaneType.A),
            new NoteData(5.60f, LaneType.D),
            new NoteData(6.00f, LaneType.S),
            new NoteData(6.40f, LaneType.A),
            new NoteData(6.80f, LaneType.W),
            new NoteData(7.20f, LaneType.D),
            new NoteData(7.60f, LaneType.S),
            new NoteData(8.00f, LaneType.A),
            new NoteData(8.40f, LaneType.W),
            new NoteData(8.80f, LaneType.D)
        };
    }

#if UNITY_EDITOR
    [ContextMenu("Load Sample Beatmap")]
    private void LoadSampleBeatmap()
    {
        inspectorNotes = CreateSampleBeatmap();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}

using System.Collections.Generic;
using UnityEngine;

public class NoteSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject notePrefab;

    [Tooltip("Spawn point theo từng lane. Mỗi lane là một Transform riêng.")]
    [SerializeField] private Transform[] spawnPoints = new Transform[4];

    [Tooltip("Hit point theo từng lane. Note sẽ được kéo tới đây đúng vào hitTime.")]
    [SerializeField] private Transform[] hitPoints = new Transform[4];

    [Tooltip("Parent để giữ hierarchy gọn gàng. Không bắt buộc.")]
    [SerializeField] private Transform spawnedNotesParent;

    [Header("Visual")]
    [Tooltip("Khoảng cách rung nhỏ khi note xuất hiện. Không bắt buộc.")]
    [SerializeField] private bool parentToSpawner = true;

    private RhythmGameManager gameManager;
    private readonly List<NoteData> beatmap = new List<NoteData>();
    private readonly List<RhythmNote> spawnedNotes = new List<RhythmNote>();
    private int nextSpawnIndex;
    private bool running;

    private void Update()
    {
        if (!running || gameManager == null)
        {
            return;
        }

        SpawnDueNotes(gameManager.CurrentSongTime);
    }

    public void Begin(RhythmGameManager manager, List<NoteData> notes)
    {
        Stop();
        gameManager = manager;
        beatmap.Clear();
        beatmap.AddRange(notes);
        beatmap.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
        nextSpawnIndex = 0;
        running = true;
    }

    public void Stop()
    {
        for (int i = spawnedNotes.Count - 1; i >= 0; i--)
        {
            if (spawnedNotes[i] != null)
            {
                Destroy(spawnedNotes[i].gameObject);
            }
        }

        spawnedNotes.Clear();
        running = false;
        beatmap.Clear();
        nextSpawnIndex = 0;
    }

    private void SpawnDueNotes(float songTime)
    {
        while (nextSpawnIndex < beatmap.Count)
        {
            NoteData data = beatmap[nextSpawnIndex];
            if (songTime < data.hitTime - gameManager.SpawnLeadTime)
            {
                break;
            }

            SpawnNote(data, songTime);
            nextSpawnIndex++;
        }
    }

    private void SpawnNote(NoteData data, float currentSongTime)
    {
        if (notePrefab == null)
        {
            Debug.LogWarning("[NoteSpawner] Chưa gán notePrefab.");
            return;
        }

        int laneIndex = (int)data.lane;
        Transform spawnPoint = GetLaneTransform(spawnPoints, laneIndex);
        Transform hitPoint = GetLaneTransform(hitPoints, laneIndex);

        if (spawnPoint == null || hitPoint == null)
        {
            Debug.LogWarning($"[NoteSpawner] Thiếu spawnPoint/hitPoint cho lane {data.lane}.");
            return;
        }

        Transform parent = spawnedNotesParent != null ? spawnedNotesParent : transform;
        GameObject noteObj = Instantiate(notePrefab, spawnPoint.position, Quaternion.identity, parentToSpawner ? parent : null);
        RhythmNote note = noteObj.GetComponent<RhythmNote>();
        if (note == null)
        {
            Debug.LogWarning("[NoteSpawner] Prefab chưa có component RhythmNote.");
            Destroy(noteObj);
            return;
        }

        note.Initialize(gameManager, data, spawnPoint.position, hitPoint.position, currentSongTime);
        gameManager.RegisterSpawnedNote(note);
        spawnedNotes.Add(note);
    }

    private static Transform GetLaneTransform(Transform[] points, int index)
    {
        if (points == null || index < 0 || index >= points.Length)
        {
            return null;
        }

        return points[index];
    }
}

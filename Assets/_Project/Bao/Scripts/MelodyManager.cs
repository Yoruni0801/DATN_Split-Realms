using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Read README.md before writing new code.
/// </summary>
public class MelodyManager : MonoBehaviour
{
    [System.Serializable]
    public struct MelodyNote
    {
        [Tooltip("Chỉ số nốt theo thứ tự C4, D4, E4, F4, G4, A4, B4, C5")]
        [Range(0, 7)] public int noteIndex;
    }

    private const float DefaultNoteDelayAfter = 0.5f;
    private static readonly int[] DefaultTwinkleNotes = { 0, 0, 4, 4, 5, 5, 4, 3, 3, 2, 2, 1, 1, 0 };
    public List<MelodyNote> songNotes = new List<MelodyNote>();

    [Header("Giao diện & Phản hồi")]
    [Tooltip("Thời gian chờ ban đầu trước khi bắt đầu phát nhạc")]
    public float initialDelay = 2.0f;
    [Tooltip("Nếu bật, bài mẫu tự chạy khi scene bắt đầu. Controller vùng kích hoạt có thể tắt giá trị này trong Awake để chờ người chơi tới gần.")]
    [SerializeField] private bool playOnStart = true;

    [SerializeField, Min(0f)] private float wrongNoteGraceDuration = 1.5f;
    [SerializeField] private Color wrongNoteColor = new Color(0.78f, 0.24f, 1f, 1f);
    [SerializeField, Min(0f)] private float wrongNoteFlashDuration = 0.42f;
    [SerializeField, Min(0f)] private float wrongNoteCameraShakeDuration = 0.08f;
    [SerializeField, Min(0f)] private float wrongNoteCameraShakeMagnitude = 0.025f;
    [SerializeField, Min(0f)] private float returnToStartDuration = 0.36f;

    [Header("Khúc mừng hoàn thành")]
    [Tooltip("Tự phát lại giai điệu bằng chính các cột trước khi đá/lồng băng vỡ. Không dùng NPC hay cục năng lượng.")]
    [SerializeField] private bool replayMelodyBeforeWinEffects = true;
    [SerializeField, Min(0f)] private float victoryMelodyLeadIn = 0.35f;
    [SerializeField, Range(1, 4)] private int finalePulseCount = 2;
    [SerializeField, Min(0.05f)] private float finalePulseInterval = 0.24f;
    [SerializeField, Min(0.02f)] private float soundWaveBreakDelay = 0.15f;

    /// <summary>
    /// NPC
    /// </summary>
    public Transform demoNpc;
    [SerializeField] private Transform npcAfterShowPoint;
    public float npcJumpHeight = 2.0f;
    public float npcJumpDuration = 0.45f;
    public float minNpcJumpDuration = 0.2f;
    public bool flipNpcToJumpDirection = true;
    public float npcAfterShowJumpDuration = 0.5f;

    /// <summary>
    /// Cage
    /// </summary>
    public bool breakCageOnWin = true;
    public string cageTag = "cage";
    public string alternateCageTag = "";
    public bool disableCageColliders = true;
    public bool hideCageRenderers = true;
    public bool destroyCageObjects = false;
    public float destroyCageDelay = 0.05f;
    public bool spawnCageBreakEffect = true;
    [Tooltip("Prefab Particle System để spawn khi tan băng. Kéo prefab vào đây để dùng hiệu ứng particle thay cho mảnh vỡ.")]
    public GameObject cageBreakParticlePrefab;
    public int cageShardCount = 18;
    public float cageEffectLifetime = 0.9f;
    public Color cageShardColor = new Color(0.65f, 0.95f, 1f, 1f);

    /// <summary>
    /// Camera
    /// </summary>
    [Header("Camera Focus & Zoom Settings")]
    [SerializeField] private CinemachineCamera completionCamera;
    [SerializeField] private Transform cameraFocusTarget;
    [SerializeField] private Transform cameraReturnTarget;
    [SerializeField] private bool focusCameraOnPlay = true;
    [Tooltip("Bật tùy chỉnh độ to/zoom của Camera khi di chuyển đến target")]
    [SerializeField] private bool changeCameraSizeOnFocus = true;
    [Tooltip("Độ to (Orthographic Size) của Cinemachine Camera khi Focus tới Target")]
    [SerializeField] private float targetCameraOrthographicSize = 9f;
    [Tooltip("Field of View nếu Camera dùng chế độ Perspective")]
    [SerializeField] private float targetCameraFieldOfView = 60f;
    [Tooltip("Thời gian chuyển đổi độ to/zoom của Camera khi Focus tới Target (giây)")]
    [SerializeField] private float cameraFocusZoomDuration = 0.5f;
    [SerializeField] private bool restoreCameraOnWin = true;
    [SerializeField] private float cameraRestoreDuration = 0.5f;

    [Header("Cutscene / Timeline Settings")]
    [Tooltip("PlayableDirector của Cutscene mở đầu (Kéo Timeline vào đây). Script sẽ tự động lắng nghe khi Cutscene này kết thúc để Focus Camera vào GameShow).")]
    [SerializeField] private UnityEngine.Playables.PlayableDirector introCutsceneDirector;
    [Tooltip("GameObject chứa Cutscene mở đầu (tùy chọn kéo thả GameObject)")]
    [SerializeField] private GameObject introCutsceneObject;

    [Header("Auto Play (Tool / Cheat Tab)")]
    [Tooltip("Bật phím tắt tự động hoàn thành minigame khi nhấn phím Tab")]
    [SerializeField] private bool enableAutoPlayOnTab = true;
    [Tooltip("Phím kích hoạt tự động chơi")]
    [SerializeField] private KeyCode autoPlayKey = KeyCode.Tab;
    [Tooltip("Khoảng cách thời gian giữa các nốt khi tự động chơi (giây)")]
    [SerializeField, Min(0.1f)] private float autoPlayNoteInterval = 0.38f;

    private int currentPlayerStep = 0;
    private bool isPlayerTurn = false;
    private bool hasWon = false;
    private Coroutine playSongCoroutine;
    private Coroutine wrongNoteReplayCoroutine;
    private Coroutine victorySequenceCoroutine;
    private Coroutine cameraRestoreCoroutine;
    private Coroutine cameraFocusCoroutine;
    private Coroutine autoPlayCoroutine;
    private bool isWaitingToReplayAfterWrongNote;
    private Vector3 playerTurnStartPosition;
    private bool hasPlayerTurnStartPosition;

    private Transform savedCameraFollow;
    private Transform savedCameraLookAt;
    private LensSettings savedCameraLens;
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private int savedCameraPriority;
    private bool hasSavedCameraState;
    private bool introCutsceneHasPlayed;
    private bool introCutsceneProcessed;

    public bool IsPlayerTurn => isPlayerTurn && !hasWon;
    public bool HasWon => hasWon;

    [Header("Events")]
    public UnityEvent OnDemoStarted;
    public UnityEvent OnPlayerTurnStarted;
    public UnityEvent OnWin;
    public UnityEvent OnCagesBroken;

    private void Awake()
    {
        EnsureDefaultSongNotes();
        HideDemoNpcUntilIntroFinishes();

        MusicNoteJumpController controller = GetComponent<MusicNoteJumpController>();
        if (controller == null) controller = gameObject.AddComponent<MusicNoteJumpController>();
        controller.Configure(this);
    }

    private void EnsureDefaultSongNotes()
    {
        if (songNotes.Count > 0) return;

        for (int i = 0; i < DefaultTwinkleNotes.Length; i++)
        {
            songNotes.Add(new MelodyNote { noteIndex = DefaultTwinkleNotes[i] });
        }
    }

    private void OnEnable()
    {
        SoundPillar.OnPillarActivated += HandlePillarActivated;
        HookIntroCutscene();
    }

    private void OnDisable()
    {
        SoundPillar.OnPillarActivated -= HandlePillarActivated;
        UnhookIntroCutscene(); AudioManager.Instance?.ResumeMusic();

        if (autoPlayCoroutine != null)
        {
            StopCoroutine(autoPlayCoroutine);
            autoPlayCoroutine = null;
        }

        if (wrongNoteReplayCoroutine != null)
        {
            StopCoroutine(wrongNoteReplayCoroutine);
            wrongNoteReplayCoroutine = null;
        }

        if (victorySequenceCoroutine != null)
        {
            StopCoroutine(victorySequenceCoroutine);
            victorySequenceCoroutine = null;
        }

        if (cameraRestoreCoroutine != null)
        {
            StopCoroutine(cameraRestoreCoroutine);
            cameraRestoreCoroutine = null;
        }

        if (cameraFocusCoroutine != null)
        {
            StopCoroutine(cameraFocusCoroutine);
            cameraFocusCoroutine = null;
        }
    }

    private void Update()
    {
        if (enableAutoPlayOnTab && Input.GetKeyDown(autoPlayKey))
        {
            TriggerAutoPlay();
        }

        MonitorIntroCutscene();
    }

    public void TriggerAutoPlay()
    {
        if (hasWon)
        {
            Debug.Log("[MelodyManager] Minigame đã hoàn thành!");
            return;
        }

        Debug.Log("[MelodyManager] Đang tự động chơi bằng phím TAB...");

        // Tắt hoàn toàn mọi popup hướng dẫn
        MelodyTutorialController.CloseAllActivePopups();
        TutorialPopupUI.CloseAllActivePopups();
        CameraCutawayTrigger2D.CloseAllActivePopups();

        if (playSongCoroutine != null)
        {
            StopCoroutine(playSongCoroutine);
            playSongCoroutine = null;
        }

        if (wrongNoteReplayCoroutine != null)
        {
            StopCoroutine(wrongNoteReplayCoroutine);
            wrongNoteReplayCoroutine = null;
        }

        isWaitingToReplayAfterWrongNote = false;

        // Đưa demo NPC về điểm kết thúc nếu đang nhảy
        if (demoNpc != null && npcAfterShowPoint != null)
        {
            demoNpc.position = npcAfterShowPoint.position;
            ResetNpcScaleX(demoNpc);
        }

        if (autoPlayCoroutine != null)
        {
            StopCoroutine(autoPlayCoroutine);
        }

        autoPlayCoroutine = StartCoroutine(AutoPlayRoutine());
    }

    private IEnumerator AutoPlayRoutine()
    {
        isPlayerTurn = true;
        MusicNoteJumpController noteJumper = GetComponent<MusicNoteJumpController>();
        if (noteJumper == null) noteJumper = FindAnyObjectByType<MusicNoteJumpController>();

        while (currentPlayerStep < songNotes.Count && !hasWon)
        {
            int currentStep = currentPlayerStep;
            MelodyNote note = songNotes[currentStep];
            SoundPillar pillar = ResolvePillar(note);

            if (pillar != null)
            {
                if (noteJumper != null)
                {
                    noteJumper.JumpToPillarDirectly(pillar);
                    yield return new WaitForSeconds(Mathf.Max(0.18f, autoPlayNoteInterval));
                }
                else
                {
                    pillar.InteractPillar();
                    yield return new WaitForSeconds(Mathf.Max(0.15f, autoPlayNoteInterval));
                }
            }
            else
            {
                currentPlayerStep++;
                if (currentPlayerStep >= songNotes.Count)
                {
                    isPlayerTurn = false;
                    OnWinGame();
                    break;
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        autoPlayCoroutine = null;
    }

    private void Start()
    {
        HideDemoNpcUntilIntroFinishes();

        HookIntroCutscene();

        if (introCutsceneDirector != null)
        {
            if (introCutsceneDirector.state == UnityEngine.Playables.PlayState.Playing)
            {
                introCutsceneHasPlayed = true;
            }
            return;
        }

        if (playOnStart && songNotes.Count > 0)
        {
            ShowDemoNpc();
            playSongCoroutine = StartCoroutine(PlaySong());
        }
        else if (songNotes.Count == 0)
        {
            Debug.LogWarning("Chưa cấu hình bài hát nào trong MelodyManager!");
        }
    }

    public void SetPlayOnStart(bool value)
    {
        playOnStart = value;
    }

    public void StartMiniGame()
    {
        if (hasWon)
        {
            return;
        }

        ShowDemoNpc();
        PlayDemo();
    }

    // Hàm gọi khi muốn hệ thống phát lại bài hát mẫu
    public void PlayDemo()
    {
        if (hasWon)
        {
            return;
        }

        if (playSongCoroutine != null)
        {
            StopCoroutine(playSongCoroutine);
        }

        playSongCoroutine = StartCoroutine(PlaySong());
    }

    public IEnumerator PlaySong()
    {
        if (hasWon)
        {
            yield break;
        }

        // Chờ bất kỳ Timeline/Cutscene mở đầu map chạy xong trước khi bắt đầu bài mẫu
        yield return WaitForActiveTimelines();

        // FOCUS CAMERA NGAY TRƯỚC KHI NPC BẮT ĐẦU NHẢY MẪU
        FocusCameraOnTarget();

        isPlayerTurn = false;
        currentPlayerStep = 0;
        OnDemoStarted?.Invoke();
        Debug.Log("NPC đang nhảy mẫu lên các nốt nhạc...");

        AudioManager.Instance?.PauseMusic(0.5f); yield return new WaitForSeconds(initialDelay);

        Transform npc = EnsureDemoNpc();
        if (npc != null)
        {
            npc.gameObject.SetActive(true);
        }

        SoundPillar firstPillar = GetNextValidPillar(0);
        if (firstPillar == null)
        {
            Debug.LogWarning("Bài hát chưa có cột âm thanh hợp lệ để NPC nhảy.");
            playSongCoroutine = null;
            yield break;
        }

        for (int i = 0; i < songNotes.Count; i++)
        {
            MelodyNote note = songNotes[i];
            SoundPillar pillar = ResolvePillar(note);
            if (pillar == null)
            {
                continue;
            }

            if (npc != null)
            {
                float jumpDuration = GetJumpDuration(i);
                yield return JumpNpcToPillar(npc, pillar, jumpDuration);
            }
            else
            {
                pillar.PlaySound(Color.white);
                yield return new WaitForSeconds(Mathf.Max(0.1f, DefaultNoteDelayAfter));
            }

            SoundPillar nextPillar = GetNextValidPillar(i + 1);
            if (nextPillar == null)
            {
                yield return new WaitForSeconds(Mathf.Max(0.1f, DefaultNoteDelayAfter));
            }
            else
            {
                float nextJumpDuration = GetJumpDuration(i + 1);
                float waitBeforeNextJump = Mathf.Max(0f, DefaultNoteDelayAfter - nextJumpDuration);
                if (waitBeforeNextJump > 0f)
                {
                    yield return new WaitForSeconds(waitBeforeNextJump);
                }
            }
        }

        if (npc != null && npcAfterShowPoint != null)
        {
            yield return JumpNpcToPoint(npc, npcAfterShowPoint.position, npcAfterShowJumpDuration);
            ResetNpcScaleX(npc);
        }

        CapturePlayerTurnStartPosition();

        isPlayerTurn = true;
        currentPlayerStep = 0;
        playSongCoroutine = null;

        OnPlayerTurnStarted?.Invoke();

        Debug.Log("Tới lượt người chơi đánh lại bản nhạc!");
    }

    private void HookIntroCutscene()
    {
        if (introCutsceneDirector == null && introCutsceneObject != null)
        {
            introCutsceneDirector = introCutsceneObject.GetComponentInChildren<UnityEngine.Playables.PlayableDirector>(true);
        }

        if (introCutsceneDirector != null)
        {
            introCutsceneDirector.stopped -= HandleIntroCutsceneStopped;
            introCutsceneDirector.stopped += HandleIntroCutsceneStopped;
            if (introCutsceneDirector.state == UnityEngine.Playables.PlayState.Playing)
            {
                introCutsceneHasPlayed = true;
            }
        }
    }

    private void UnhookIntroCutscene()
    {
        if (introCutsceneDirector != null)
        {
            introCutsceneDirector.stopped -= HandleIntroCutsceneStopped;
        }
    }

    private void MonitorIntroCutscene()
    {
        if (introCutsceneProcessed) return;

        if (introCutsceneDirector == null && introCutsceneObject != null)
        {
            introCutsceneDirector = introCutsceneObject.GetComponentInChildren<UnityEngine.Playables.PlayableDirector>(true);
        }

        if (introCutsceneDirector != null)
        {
            if (introCutsceneDirector.state == UnityEngine.Playables.PlayState.Playing)
            {
                introCutsceneHasPlayed = true;
            }

            if (HasCutsceneFinished(introCutsceneDirector))
            {
                HandleIntroCutsceneStopped(introCutsceneDirector);
            }
        }
    }

    private bool HasCutsceneFinished(UnityEngine.Playables.PlayableDirector director)
    {
        if (director == null || director.playableAsset == null)
        {
            return false;
        }

        if (!introCutsceneHasPlayed && director.state != UnityEngine.Playables.PlayState.Playing && director.time <= 0.01d)
        {
            return false;
        }

        double duration = director.duration;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0d)
        {
            return true;
        }

        if (director.state != UnityEngine.Playables.PlayState.Playing && introCutsceneHasPlayed)
        {
            return true;
        }

        return director.time >= duration - 0.05d;
    }

    private void HandleIntroCutsceneStopped(UnityEngine.Playables.PlayableDirector director)
    {
        if (introCutsceneProcessed) return;
        introCutsceneProcessed = true;

        Debug.Log("[MelodyManager] Cutscene mở đầu (Timeline) đã kết thúc! Bắt đầu Focus Camera vào GameShow...");
        FocusCameraOnTarget();

        if (playOnStart && songNotes.Count > 0)
        {
            ShowDemoNpc();
            if (playSongCoroutine != null) StopCoroutine(playSongCoroutine);
            playSongCoroutine = StartCoroutine(PlaySong());
        }
        else
        {
            isPlayerTurn = true;
            OnPlayerTurnStarted?.Invoke();
        }
    }

    private IEnumerator WaitForActiveTimelines()
    {
        yield return null;

        if (introCutsceneDirector != null)
        {
            while (introCutsceneDirector != null 
                   && introCutsceneDirector.gameObject.activeInHierarchy 
                   && (introCutsceneDirector.state == UnityEngine.Playables.PlayState.Playing 
                       || (introCutsceneDirector.time > 0 && introCutsceneDirector.time < introCutsceneDirector.duration - 0.05)))
            {
                yield return null;
            }
            yield break;
        }

        UnityEngine.Playables.PlayableDirector[] allDirectors = FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsSortMode.None);
        if (allDirectors == null || allDirectors.Length == 0) yield break;

        bool anyPlaying = true;
        while (anyPlaying)
        {
            anyPlaying = false;
            foreach (var dir in allDirectors)
            {
                if (dir != null && dir.gameObject.activeInHierarchy && dir.state == UnityEngine.Playables.PlayState.Playing)
                {
                    anyPlaying = true;
                    break;
                }
            }

            if (anyPlaying)
            {
                yield return null;
            }
        }
    }

    private Transform EnsureDemoNpc()
    {
        return demoNpc;
    }

    private void HideDemoNpcUntilIntroFinishes()
    {
        if (demoNpc != null)
        {
            demoNpc.gameObject.SetActive(false);
        }
    }

    private void ShowDemoNpc()
    {
        if (demoNpc != null)
        {
            demoNpc.gameObject.SetActive(true);
        }
    }


    private IEnumerator JumpNpcToPillar(Transform npc, SoundPillar pillar, float duration)
    {
        yield return JumpNpcToPoint(npc, pillar.GetNpcLandingPosition(), duration);
        if (npc != null)
        {
            pillar.PlaySound(Color.white);
        }
    }

    private IEnumerator JumpNpcToPoint(Transform npc, Vector3 endPosition, float duration)
    {
        Vector3 startPosition = npc.position;
        duration = Mathf.Max(0.05f, duration);

        FaceNpcToTarget(npc, endPosition);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 position = Vector3.Lerp(startPosition, endPosition, t);
            position.y += Mathf.Sin(t * Mathf.PI) * npcJumpHeight;
            npc.position = position;
            yield return null;
        }

        npc.position = endPosition;
    }

    private void FaceNpcToTarget(Transform npc, Vector3 targetPosition)
    {
        if (!flipNpcToJumpDirection)
        {
            return;
        }

        float direction = targetPosition.x - npc.position.x;
        if (Mathf.Abs(direction) < 0.01f)
        {
            return;
        }

        Vector3 scale = npc.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction);
        npc.localScale = scale;
    }

    private void ResetNpcScaleX(Transform npc)
    {
        Vector3 scale = npc.localScale;
        scale.x = 1f;
        npc.localScale = scale;
    }

    private float GetJumpDuration(int noteIndex)
    {
        return Mathf.Clamp(DefaultNoteDelayAfter, minNpcJumpDuration, npcJumpDuration);
    }

    private SoundPillar GetNextValidPillar(int startIndex)
    {
        for (int i = startIndex; i < songNotes.Count; i++)
        {
            SoundPillar pillar = ResolvePillar(songNotes[i]);
            if (pillar != null)
            {
                return pillar;
            }
        }

        return null;
    }

    private void HandlePillarActivated(int pillarID)
    {
        if (!isPlayerTurn || songNotes.Count == 0) return;

        // During the short grace period we keep accepting jump input for its VFX and feel,
        // but the answer is already marked as wrong and will be reset by the replay.
        if (isWaitingToReplayAfterWrongNote) return;

        SoundPillar expectedPillar = ResolvePillar(songNotes[currentPlayerStep]);
        if (expectedPillar != null && pillarID == expectedPillar.pillarID)
        {
            currentPlayerStep++;
            Debug.Log($"Đúng nốt thứ {currentPlayerStep}!");

            if (currentPlayerStep >= songNotes.Count)
            {
                isPlayerTurn = false;
                Debug.Log("BẠN ĐÃ ĐÁNH THỨC / RU NGỦ QUÁI VẬT THÀNH CÔNG!");
                OnWinGame();
            }

            return;
        }

        isWaitingToReplayAfterWrongNote = true;
        SoundPillar wrongPillar = FindPillarById(pillarID);
        wrongPillar?.TriggerWrongNoteFeedback(wrongNoteColor, wrongNoteFlashDuration);
        if (CameraController.Instance != null)
            CameraController.Instance.ShakeCamera(wrongNoteCameraShakeDuration, wrongNoteCameraShakeMagnitude);

        Debug.Log($"Đánh sai nhịp/nốt! Phát lại hướng dẫn sau {wrongNoteGraceDuration:0.##} giây.");
        wrongNoteReplayCoroutine = StartCoroutine(ReplayAfterWrongNote());
    }

    private IEnumerator ReplayAfterWrongNote()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, wrongNoteGraceDuration));
        isPlayerTurn = false;
        currentPlayerStep = 0;
        isWaitingToReplayAfterWrongNote = false;
        wrongNoteReplayCoroutine = null;

        // Let an in-flight note jump finish before taking the player back to the start marker.
        yield return new WaitForSeconds(0.55f);
        yield return ReturnPlayerToTurnStartPosition();
        PlayDemo();
    }

    private void CapturePlayerTurnStartPosition()
    {
        Player activePlayer = PlayerRegistry.Current;
        if (activePlayer == null) return;

        playerTurnStartPosition = activePlayer.transform.position;
        hasPlayerTurnStartPosition = true;
    }

    private IEnumerator ReturnPlayerToTurnStartPosition()
    {
        if (!hasPlayerTurnStartPosition) yield break;

        Player activePlayer = PlayerRegistry.Current;
        if (activePlayer == null) yield break;

        Rigidbody2D body = activePlayer.GetComponent<Rigidbody2D>();
        bool wasLocked = activePlayer.IsControlLocked;
        bool wasSimulated = body != null && body.simulated;
        activePlayer.SetControlLocked(true);
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        Vector3 start = activePlayer.transform.position;
        Vector3 end = playerTurnStartPosition;
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, returnToStartDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector3 position = Vector3.Lerp(start, end, t);
            position.y += Mathf.Sin(t * Mathf.PI) * 0.35f;
            activePlayer.transform.position = position;
            yield return null;
        }

        activePlayer.transform.position = end;
        if (body != null)
        {
            body.simulated = wasSimulated;
            body.linearVelocity = Vector2.zero;
        }
        activePlayer.SetControlLocked(wasLocked);
    }

    private SoundPillar FindPillarById(int pillarId)
    {
        foreach (MelodyNote note in songNotes)
        {
            SoundPillar pillar = ResolvePillar(note);
            if (pillar != null && pillar.pillarID == pillarId)
                return pillar;
        }

        return null;
    }

    private SoundPillar[] pillarCache;

    private void CachePillars()
    {
        if (pillarCache != null && pillarCache.Length == 8) return;
        pillarCache = new SoundPillar[8];
        for (int i = 0; i < 8; i++)
        {
            GameObject go = GameObject.Find("Pillar_" + GetNoteName(i));
            if (go != null) pillarCache[i] = go.GetComponent<SoundPillar>();
        }
    }

    private SoundPillar ResolvePillar(MelodyNote note)
    {
        CachePillars();
        int idx = Mathf.Clamp(note.noteIndex, 0, 7);
        return pillarCache != null ? pillarCache[idx] : null;
    }

    private string GetNoteName(int index)
    {
        string[] names = { "C4", "D4", "E4", "F4", "G4", "A4", "B4", "C5" };
        return names[Mathf.Clamp(index, 0, names.Length - 1)];
    }



    public void FocusCameraOnTarget()
    {
        if (completionCamera == null)
        {
            completionCamera = ResolveVirtualCamera();
        }

        if (completionCamera == null)
        {
            Debug.LogWarning("[MelodyManager] Không tìm thấy CinemachineCamera nào để Focus!");
            return;
        }

        SaveCameraState();

        if (focusCameraOnPlay && cameraFocusTarget != null)
        {
            if (!completionCamera.gameObject.activeSelf)
            {
                completionCamera.gameObject.SetActive(true);
            }

            completionCamera.Follow = cameraFocusTarget;
            completionCamera.LookAt = cameraFocusTarget;
            completionCamera.Priority = Mathf.Max(completionCamera.Priority, 100);

            if (changeCameraSizeOnFocus)
            {
                if (cameraFocusCoroutine != null)
                {
                    StopCoroutine(cameraFocusCoroutine);
                    cameraFocusCoroutine = null;
                }

                if (cameraFocusZoomDuration > 0f)
                {
                    cameraFocusCoroutine = StartCoroutine(FocusCameraZoomRoutine(targetCameraOrthographicSize, targetCameraFieldOfView, cameraFocusZoomDuration));
                }
                else
                {
                    LensSettings lens = completionCamera.Lens;
                    lens.OrthographicSize = targetCameraOrthographicSize;
                    lens.FieldOfView = targetCameraFieldOfView;
                    completionCamera.Lens = lens;
                }
            }

            Debug.Log($"[MelodyManager] Đã Focus Camera '{completionCamera.name}' vào '{cameraFocusTarget.name}' (Size: {targetCameraOrthographicSize}) thành công!");
        }
        else
        {
            Debug.LogWarning($"[MelodyManager] Không thể Focus Camera: focusCameraOnPlay={focusCameraOnPlay}, cameraFocusTarget={cameraFocusTarget}");
        }
    }

    private IEnumerator FocusCameraZoomRoutine(float targetOrthoSize, float targetFov, float duration)
    {
        LensSettings startLens = completionCamera.Lens;
        float startOrtho = startLens.OrthographicSize;
        float startFov = startLens.FieldOfView;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            LensSettings lens = completionCamera.Lens;
            lens.OrthographicSize = Mathf.Lerp(startOrtho, targetOrthoSize, t);
            lens.FieldOfView = Mathf.Lerp(startFov, targetFov, t);
            completionCamera.Lens = lens;

            yield return null;
        }

        LensSettings finalLens = completionCamera.Lens;
        finalLens.OrthographicSize = targetOrthoSize;
        finalLens.FieldOfView = targetFov;
        completionCamera.Lens = finalLens;
        cameraFocusCoroutine = null;
    }

    private CinemachineCamera ResolveVirtualCamera()
    {
        if (completionCamera != null) return completionCamera;

        CinemachineBrain brain = FindFirstObjectByType<CinemachineBrain>();
        if (brain != null && brain.ActiveVirtualCamera is CinemachineCamera activeCam && activeCam.isActiveAndEnabled)
        {
            return activeCam;
        }

        CinemachineCamera[] cams = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (cams != null && cams.Length > 0)
        {
            CinemachineCamera best = cams[0];
            for (int i = 1; i < cams.Length; i++)
            {
                if (cams[i].Priority > best.Priority) best = cams[i];
            }
            return best;
        }

        return FindFirstObjectByType<CinemachineCamera>();
    }

    private void SaveCameraState()
    {
        if (completionCamera == null || hasSavedCameraState)
        {
            return;
        }

        if (cameraFocusTarget != null && completionCamera.Follow == cameraFocusTarget)
        {
            return;
        }

        savedCameraFollow = completionCamera.Follow;
        savedCameraLookAt = completionCamera.LookAt;
        savedCameraLens = completionCamera.Lens;
        savedCameraPosition = GetCurrentCameraPosition();
        savedCameraRotation = completionCamera.transform.rotation;
        savedCameraPriority = completionCamera.Priority;
        hasSavedCameraState = true;
    }

    private void RestoreCameraToPlayer()
    {
        if (completionCamera == null)
        {
            completionCamera = ResolveVirtualCamera();
        }

        if (completionCamera == null) return;

        if (cameraFocusCoroutine != null)
        {
            StopCoroutine(cameraFocusCoroutine);
            cameraFocusCoroutine = null;
        }

        Transform target = ResolveCameraReturnTarget();
        completionCamera.Follow = target;
        completionCamera.LookAt = savedCameraLookAt;

        if (cameraRestoreCoroutine != null)
        {
            StopCoroutine(cameraRestoreCoroutine);
        }

        if (cameraRestoreDuration <= 0f || !hasSavedCameraState)
        {
            ApplySavedCameraState();
            return;
        }

        cameraRestoreCoroutine = StartCoroutine(RestoreCameraRoutine());
    }

    private IEnumerator RestoreCameraRoutine()
    {
        LensSettings startLens = completionCamera.Lens;
        Vector3 startPosition = GetCurrentCameraPosition();
        Quaternion startRotation = completionCamera.transform.rotation;
        float duration = Mathf.Max(0.01f, cameraRestoreDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            LensSettings lens = completionCamera.Lens;
            lens.OrthographicSize = Mathf.Lerp(
                startLens.OrthographicSize,
                savedCameraLens.OrthographicSize,
                t);
            lens.FieldOfView = Mathf.Lerp(
                startLens.FieldOfView,
                savedCameraLens.FieldOfView,
                t);
            completionCamera.Lens = lens;

            Vector3 position = Vector3.Lerp(startPosition, savedCameraPosition, t);
            Quaternion rotation = Quaternion.Slerp(startRotation, savedCameraRotation, t);
            completionCamera.ForceCameraPosition(position, rotation);

            yield return null;
        }

        ApplySavedCameraState();
        cameraRestoreCoroutine = null;
    }

    private void ApplySavedCameraState()
    {
        if (!hasSavedCameraState || completionCamera == null)
        {
            return;
        }

        completionCamera.Lens = savedCameraLens;
        completionCamera.Follow = ResolveCameraReturnTarget();
        completionCamera.LookAt = savedCameraLookAt;
        completionCamera.Priority = savedCameraPriority;
        completionCamera.ForceCameraPosition(savedCameraPosition, savedCameraRotation);
        hasSavedCameraState = false;
    }

    private Transform ResolveCameraReturnTarget()
    {
        if (cameraReturnTarget != null)
        {
            return cameraReturnTarget;
        }

        if (savedCameraFollow != null && savedCameraFollow != cameraFocusTarget)
        {
            return savedCameraFollow;
        }

        Player player = FindFirstObjectByType<Player>();
        if (player != null)
        {
            return player.transform;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            return playerObj.transform;
        }

        return null;
    }

    private Vector3 GetCurrentCameraPosition()
    {
        if (completionCamera.PreviousStateIsValid)
        {
            return completionCamera.State.GetFinalPosition();
        }

        return completionCamera.transform.position;
    }

    private void OnWinGame()
    {
        if (hasWon) return;

        hasWon = true;
        isPlayerTurn = false;
        isWaitingToReplayAfterWrongNote = false;

        // Tắt hoàn toàn mọi popup hướng dẫn khi hoàn thành minigame
        MelodyTutorialController.CloseAllActivePopups();
        TutorialPopupUI.CloseAllActivePopups();
        CameraCutawayTrigger2D.CloseAllActivePopups();

        if (autoPlayCoroutine != null)
        {
            StopCoroutine(autoPlayCoroutine);
            autoPlayCoroutine = null;
        }

        if (wrongNoteReplayCoroutine != null)
        {
            StopCoroutine(wrongNoteReplayCoroutine);
            wrongNoteReplayCoroutine = null;
        }

        if (playSongCoroutine != null)
        {
            StopCoroutine(playSongCoroutine);
            playSongCoroutine = null;
        }

        victorySequenceCoroutine = StartCoroutine(PlayVictoryMelodyThenFinish());
    }

    private IEnumerator PlayVictoryMelodyThenFinish()
    {
        yield return ReturnPlayerToTurnStartPosition();

        if (replayMelodyBeforeWinEffects)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, victoryMelodyLeadIn));
            foreach (MelodyNote note in songNotes)
            {
                SoundPillar pillar = ResolvePillar(note);
                if (pillar != null) pillar.PlaySound();
                yield return new WaitForSeconds(Mathf.Max(0.05f, DefaultNoteDelayAfter));
            }
        }

        // A synchronized final chord of light, then the ice breaks as a left-to-right sound wave.
        List<SoundPillar> finalePillars = GetUniquePillarsInOrder();
        for (int pulse = 0; pulse < finalePulseCount; pulse++)
        {
            foreach (SoundPillar pillar in finalePillars) pillar.TriggerFinalePulse();
            yield return new WaitForSeconds(finalePulseInterval);
        }

        yield return BreakCageObjectsInSoundWave();
        OnCagesBroken?.Invoke();
        OnWin?.Invoke();

        if (restoreCameraOnWin) RestoreCameraToPlayer(); AudioManager.Instance?.ResumeMusic();

        victorySequenceCoroutine = null;
        Debug.Log("Minigame Clear!");
    }

    private List<SoundPillar> GetUniquePillarsInOrder()
    {
        List<SoundPillar> results = new List<SoundPillar>();
        foreach (MelodyNote note in songNotes)
        {
            SoundPillar pillar = ResolvePillar(note);
            if (pillar != null && !results.Contains(pillar)) results.Add(pillar);
        }
        results.Sort((a, b) => a.pillarID.CompareTo(b.pillarID));
        return results;
    }

    private IEnumerator BreakCageObjectsInSoundWave()
    {
        if (!breakCageOnWin) yield break;

        List<GameObject> cageObjects = FindCageObjects();
        cageObjects.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        foreach (GameObject cageObject in cageObjects)
        {
            BreakCageObject(cageObject);
            yield return new WaitForSeconds(Mathf.Max(0.02f, soundWaveBreakDelay));
        }
    }

    private void BreakCageObject(GameObject cageObject)
    {
        if (cageObject == null) return;

        if (spawnCageBreakEffect)
        {
            if (cageBreakParticlePrefab != null)
            {
                Vector3 spawnPos = cageObject.transform.position;
                GameObject fx = Instantiate(cageBreakParticlePrefab, spawnPos, Quaternion.identity);
                fx.TryGetComponent(out ParticleSystem ps);
                if (ps != null)
                {
                    Destroy(fx, ps.main.duration + ps.main.startLifetime.constantMax);
                }
                else
                {
                    Destroy(fx, cageEffectLifetime);
                }
            }
            else
            {
                PlinkoBreakTarget.SpawnBreakEffect(cageObject.transform.position, cageShardCount, cageEffectLifetime, cageShardColor);
            }
        }

        if (disableCageColliders)
        {
            foreach (Collider2D targetCollider in cageObject.GetComponentsInChildren<Collider2D>())
                targetCollider.enabled = false;
        }

        if (hideCageRenderers)
        {
            foreach (Renderer targetRenderer in cageObject.GetComponentsInChildren<Renderer>())
                targetRenderer.enabled = false;
        }

        if (destroyCageObjects)
            Destroy(cageObject, Mathf.Max(0f, destroyCageDelay));
    }

    private List<GameObject> FindCageObjects()
    {
        List<GameObject> results = new List<GameObject>();
        AddObjectsWithTag(results, cageTag);

        if (!string.Equals(alternateCageTag, cageTag, System.StringComparison.Ordinal))
        {
            AddObjectsWithTag(results, alternateCageTag);
        }

        return results;
    }

    private void AddObjectsWithTag(List<GameObject> results, string targetTag)
    {
        GameObject[] taggedObjects = FindObjectsWithTagSafe(targetTag);
        foreach (GameObject taggedObject in taggedObjects)
        {
            if (taggedObject != null && !results.Contains(taggedObject))
            {
                results.Add(taggedObject);
            }
        }
    }

    private GameObject[] FindObjectsWithTagSafe(string targetTag)
    {
        if (string.IsNullOrEmpty(targetTag))
        {
            return System.Array.Empty<GameObject>();
        }

        try
        {
            return GameObject.FindGameObjectsWithTag(targetTag);
        }
        catch (UnityException)
        {
            Debug.LogWarning($"Tag '{targetTag}' is not defined. Add it in Project Settings > Tags and Layers.", this);
            return System.Array.Empty<GameObject>();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Tạo Nhạc Twinkle Twinkle")]
    public void AutoSetupTwinkle()
    {
        songNotes = new List<MelodyNote>();

        for (int i = 0; i < DefaultTwinkleNotes.Length; i++)
        {
            songNotes.Add(new MelodyNote { noteIndex = DefaultTwinkleNotes[i] });
        }

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Đã nạp chuẩn bài Twinkle Twinkle Little Star!");
    }
#endif
}

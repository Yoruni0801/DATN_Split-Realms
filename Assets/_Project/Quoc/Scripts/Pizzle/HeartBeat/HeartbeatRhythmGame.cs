using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

public class HeartbeatRhythmGame : MonoBehaviour
{
    private enum GameState
    {
        Playing,
        Win,
        Lose
    }

    private enum Wave
    {
        Wave1 = 1,
        Wave2 = 2,
        Wave3 = 3
    }

    [Header("Prefab / Scene References")]
    [SerializeField] private HeartbeatNote heartbeatPrefab;
    [SerializeField] private HeartbeatDotDrawer heartbeatDotDrawer;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform hitPoint;
    [SerializeField] private Transform destroyPoint;
    [SerializeField] private Transform missEffectRoot;
    [SerializeField] private Transform noteParent;
    [SerializeField] private Canvas canvas;
    [SerializeField] private HeartbeatHitPointDisplay hitPointDisplay;
    [SerializeField] private GameObject minigameRoot;
    [SerializeField] private PlayableDirector winCutscene;
    [SerializeField] private GameObject winCutsceneObject;
    [SerializeField] private Image winFlashImage;

    [Header("Gameplay")]
    [SerializeField] private int maxMisses = 5;
    [SerializeField] private int wave1ClearCount = 5;
    [SerializeField] private int wave2ClearCount = 7;
    [SerializeField] private int wave3ClearCount = 10;

    [Header("Wave Speed")]
    [Tooltip("Tốc độ bay note của wave 1.")]
    [SerializeField] private float wave1Speed = 3.25f;
    [Tooltip("Tốc độ bay note của wave 2.")]
    [SerializeField] private float wave2Speed = 4f;
    [Tooltip("Tốc độ bay note của wave 3.")]
    [SerializeField] private float wave3Speed = 4.75f;

    [Header("Note Path")]
    [SerializeField] private float overshootDistance = 1.5f;
    [SerializeField] private float hitWindowDistance = 0.22f;
    [SerializeField] private float comboInputWindow = 0.18f;

    [Header("Heartbeat Physiology")]
    [SerializeField] private float heartbeatBaseIntensity = 1f;
    [SerializeField] private float heartbeatHitBoost = 0.28f;
    [SerializeField] private float heartbeatMissDrop = 0.22f;
    [SerializeField] private float heartbeatPassiveDecayPerSecond = 0.08f;
    [SerializeField] private float heartbeatMinIntensity = 0.04f;
    [SerializeField] private float heartbeatVisualSyncSpeed = 8f;

    [Header("Spawn")]
    [Tooltip("Khoảng thời gian giữa 2 note của wave 1.")]
    [SerializeField] private float wave1SpawnInterval = 0.55f;
    [Tooltip("Khoảng thời gian giữa 2 note của wave 2.")]
    [SerializeField] private float wave2SpawnInterval = 0.45f;
    [Tooltip("Khoảng thời gian giữa 2 note của wave 3.")]
    [SerializeField] private float wave3SpawnInterval = 0.35f;
    [SerializeField] private float initialSpawnDelay = 0.35f;

    [Header("UI")]
    [SerializeField] private Text waveText;
    [SerializeField] private Text promptText;
    [SerializeField] private Text progressText;
    [SerializeField] private Text missText;
    [SerializeField] private Text messageText;
    [SerializeField] private Text debugText;

    [Header("Audio")]
    [SerializeField] private AudioSource feedbackAudioSource;
    [SerializeField] private AudioClip winNoteClip;
    [SerializeField] private AudioClip loseNoteClip;
    [SerializeField, Range(0f, 1f)] private float winNoteVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float loseNoteVolume = 1f;

    [Header("Input")]
    [SerializeField] private KeyCode restartKey = KeyCode.R;
    [SerializeField] private bool verboseDebug = true;
    [SerializeField] private bool showRuntimeDebugOverlay = true;
    [SerializeField] private float missCanvasScaleMultiplier = 1.04f;
    [SerializeField] private float missCanvasFlashAlpha = 0.88f;
    [SerializeField] private float missCanvasEffectDuration = 0.16f;
    [SerializeField] private bool disableMinigameOnWin = true;
    [SerializeField] private bool resetWinCutsceneTime = true;
    [SerializeField] private float winFlashDuration = 0.22f;
    [SerializeField] private float winFlashPeakAlpha = 0.98f;
    [SerializeField] private float winFlashStartAlpha = 0.02f;

    private static readonly KeyCode[] TrackedKeys =
    {
        KeyCode.A,
        KeyCode.S,
        KeyCode.D,
        KeyCode.W
    };

    private static readonly HeartbeatPattern[] SingleKeyPatterns =
    {
        HeartbeatPattern.Single(KeyCode.A),
        HeartbeatPattern.Single(KeyCode.S),
        HeartbeatPattern.Single(KeyCode.D),
        HeartbeatPattern.Single(KeyCode.W)
    };

    private static readonly HeartbeatPattern[] ComboPatterns =
    {
        HeartbeatPattern.Combo(KeyCode.A, KeyCode.W),
        HeartbeatPattern.Combo(KeyCode.W, KeyCode.D),
        HeartbeatPattern.Combo(KeyCode.D, KeyCode.S),
        HeartbeatPattern.Combo(KeyCode.S, KeyCode.A)
    };

    private readonly HashSet<KeyCode> pressedThisFrame = new HashSet<KeyCode>();
    private readonly Dictionary<KeyCode, float> recentPressTimes = new Dictionary<KeyCode, float>();
    private readonly List<HeartbeatNote> activeNotes = new List<HeartbeatNote>();

    private Coroutine spawnRoutine;
    private Coroutine missCanvasEffectRoutine;
    private Coroutine winFlashRoutine;
    private GameState gameState = GameState.Playing;
    private Wave currentWave = Wave.Wave1;
    private int wave1SuccessCount;
    private int wave2SuccessCount;
    private int wave3SuccessCount;
    private int missCount;
    private string lastInputDebug = "No input yet";
    private string lastResolutionDebug = "Waiting for note";
    private Transform missEffectRootTransform;
    private Vector3 missEffectRootBaseScale = Vector3.one;
    private CanvasGroup canvasGroup;
    private float canvasBaseAlpha = 1f;
    private AudioSource runtimeFeedbackAudioSource;
    private float heartbeatIntensity = 1f;
    private float heartbeatTargetIntensity = 1f;

    private HeartbeatNote CurrentNote
    {
        get
        {
            return activeNotes.Count > 0 ? activeNotes[0] : null;
        }
    }

    private void Start()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (heartbeatDotDrawer == null)
        {
#if UNITY_2023_1_OR_NEWER
            heartbeatDotDrawer = FindFirstObjectByType<HeartbeatDotDrawer>();
#else
            heartbeatDotDrawer = FindObjectOfType<HeartbeatDotDrawer>();
#endif
        }

        if (canvas != null)
        {
            canvasGroup = canvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            }
            canvasBaseAlpha = canvasGroup.alpha;
        }

        if (missEffectRoot != null)
        {
            missEffectRootTransform = missEffectRoot;
            missEffectRootBaseScale = missEffectRootTransform.localScale;
        }

        SetupFeedbackAudioSource();
        SetupWinFlashImage();

        Log("Start() called");
        StartGame();
    }

    private void Update()
    {
        if (Input.GetKeyDown(restartKey) && gameState != GameState.Playing)
        {
            Log("Restart key pressed");
            StartGame();
            return;
        }

        if (gameState != GameState.Playing)
        {
            UpdateDebugOverlay();
            return;
        }

        CapturePressedKeys();
        UpdateHeartbeatPhysiology();
        SyncHeartbeatVisuals();
        UpdateDebugOverlay();

        CullReachedDestroyNotes();

        HeartbeatNote currentNote = CurrentNote;
        if (currentNote == null)
        {
            return;
        }

        if (currentNote.IsWithinHitWindow(hitWindowDistance))
        {
            Log("Note entered hit window: " + currentNote.Pattern.DisplayText);
                if (currentWave == Wave.Wave3)
                {
                    if (ContainsInvalidWave3Input(currentNote.Pattern, pressedThisFrame))
                    {
                        currentNote.PlayMissShake();
                        lastResolutionDebug = "Wave 3: wrong combo pressed";
                        RegisterMiss(currentNote, "Wrong combo", true);
                        return;
                    }

                if (currentNote.Pattern.HasAllRecentPresses(recentPressTimes, comboInputWindow))
                {
                    lastResolutionDebug = "Wave 3: combo matched";
                    RegisterHit(currentNote);
                    return;
                }

                lastResolutionDebug = "Wave 3: waiting for full combo within " + comboInputWindow.ToString("0.00") + "s";
            }
            else
            {
                if (currentNote.Pattern.HasAllRecentPresses(recentPressTimes, comboInputWindow))
                {
                    if (pressedThisFrame.Count == 0 || currentNote.Pattern.MatchesExactPressed(pressedThisFrame))
                    {
                        lastResolutionDebug = "Wave " + (int)currentWave + ": matched " + currentNote.Pattern.DisplayText;
                        RegisterHit(currentNote);
                        return;
                    }

                    lastResolutionDebug = "Wave " + (int)currentWave + ": wrong key pressed";
                    currentNote.PlayMissShake();
                    RegisterMiss(currentNote, "Wrong key", true);
                    return;
                }

                if (pressedThisFrame.Count > 0)
                {
                    lastResolutionDebug = "Wave " + (int)currentWave + ": wrong key pressed";
                    currentNote.PlayMissShake();
                    RegisterMiss(currentNote, "Wrong key", true);
                    return;
                }

                lastResolutionDebug = "Wave " + (int)currentWave + ": waiting for key within " + comboInputWindow.ToString("0.00") + "s";
            }
        }
    }

    private void CullReachedDestroyNotes()
    {
        while (activeNotes.Count > 0)
        {
            HeartbeatNote note = activeNotes[0];
            if (note == null)
            {
                activeNotes.RemoveAt(0);
                continue;
            }

            if (!note.HasReachedDestroyPoint())
            {
                break;
            }

            lastResolutionDebug = "Destroy point reached";
            note.PlayMissShake();
            RegisterMiss(note, "Miss", true);

            if (gameState != GameState.Playing)
            {
                return;
            }
        }
    }

    private void StartGame()
    {
        Log("StartGame() reset state");
        StopSpawnRoutine();
        ResetMissCanvasEffect();
        ResetWinFlashEffect();

        if (minigameRoot != null)
        {
            minigameRoot.SetActive(true);
        }

        if (winCutsceneObject != null)
        {
            winCutsceneObject.SetActive(false);
        }

        if (winCutscene != null)
        {
            winCutscene.Stop();
            if (resetWinCutsceneTime)
            {
                winCutscene.time = 0;
            }
        }

        ClearActiveNotes();

        gameState = GameState.Playing;
        currentWave = Wave.Wave1;
        wave1SuccessCount = 0;
        wave2SuccessCount = 0;
        wave3SuccessCount = 0;
        missCount = 0;
        heartbeatIntensity = Mathf.Clamp01(heartbeatBaseIntensity);
        heartbeatTargetIntensity = heartbeatIntensity;
        pressedThisFrame.Clear();
        recentPressTimes.Clear();
        lastInputDebug = "No input yet";
        lastResolutionDebug = "Waiting for note";
        heartbeatDotDrawer?.ApplyPlayingState();

        UpdateUI();
        promptTextSet(string.Empty);
        HideHitPointPrompt();
        SetMessage("Ready");
        UpdateDebugOverlay();
        ScheduleNextSpawn(initialSpawnDelay);
    }

    private void CapturePressedKeys()
    {
        pressedThisFrame.Clear();

        for (int i = 0; i < TrackedKeys.Length; i++)
        {
            KeyCode key = TrackedKeys[i];
            if (Input.GetKeyDown(key))
            {
                pressedThisFrame.Add(key);
                recentPressTimes[key] = Time.time;
                Log("KeyDown: " + key);
            }
        }

        lastInputDebug = pressedThisFrame.Count > 0
            ? "Pressed this frame: " + GetPressedKeysText(pressedThisFrame)
            : "Pressed this frame: none";
    }

    private void ScheduleNextSpawn(float delay)
    {
        if (gameState != GameState.Playing)
        {
            return;
        }

        if (activeNotes.Count > 0)
        {
            return;
        }

        StopSpawnRoutine();
        spawnRoutine = StartCoroutine(SpawnNextNoteAfterDelay(Mathf.Max(0f, delay)));
    }

    private IEnumerator SpawnNextNoteAfterDelay(float delay)
    {
        Log("SpawnNextNoteAfterDelay started, delay = " + delay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        spawnRoutine = null;

        if (gameState != GameState.Playing || activeNotes.Count > 0)
        {
            yield break;
        }

        SpawnNextNote();
    }

    private void SpawnNextNote()
    {
        if (heartbeatPrefab == null || spawnPoint == null || hitPoint == null)
        {
            SetMessage("Assign prefab, spawn point, and hit point");
            LogError("Missing reference. Prefab or points not assigned.");
            gameState = GameState.Lose;
            return;
        }

        if (activeNotes.Count > 0)
        {
            Log("SpawnNextNote skipped because an active note already exists.");
            return;
        }

        heartbeatDotDrawer?.ApplyPlayingState();

        HeartbeatPattern pattern = CreatePatternForCurrentWave();
        float speed = GetCurrentWaveSpeed();

        Vector2 start = GetLocalPointOnCanvas(spawnPoint);
        Vector2 hit = GetLocalPointOnCanvas(hitPoint);
        Vector2 end = destroyPoint != null ? GetLocalPointOnCanvas(destroyPoint) : Vector2.zero;

        if (destroyPoint == null)
        {
            Vector2 travelDirection = (hit - start).normalized;
            if (travelDirection.sqrMagnitude <= 0.0001f)
            {
                travelDirection = Vector2.down;
            }

            end = hit + travelDirection * overshootDistance;
        }

        bool shouldRefreshPrompt = activeNotes.Count == 0;

        HeartbeatNote note;
        if (noteParent != null)
        {
            note = Instantiate(heartbeatPrefab, noteParent, false);
        }
        else
        {
            note = Instantiate(heartbeatPrefab);
        }

        note.transform.SetAsLastSibling();
        note.SetHeartbeatIntensity(heartbeatIntensity);
        note.Initialize(pattern, start, hit, end, speed);
        activeNotes.Add(note);

        Log(string.Format("Spawn note | wave={0} pattern={1} start={2} hit={3} end={4} speed={5}",
            currentWave,
            pattern.DisplayText,
            start,
            hit,
            end,
            speed));

        lastResolutionDebug = "Active note spawned: " + pattern.DisplayText;
        if (shouldRefreshPrompt)
        {
            ShowHitPointPrompt(pattern);
            promptTextSet(pattern.DisplayText);
        }
        UpdateUI();
        UpdateDebugOverlay();
    }

    private HeartbeatPattern CreatePatternForCurrentWave()
    {
        if (currentWave == Wave.Wave3)
        {
            return ComboPatterns[Random.Range(0, ComboPatterns.Length)];
        }

        return SingleKeyPatterns[Random.Range(0, SingleKeyPatterns.Length)];
    }

    private float GetCurrentWaveSpeed()
    {
        switch (currentWave)
        {
            case Wave.Wave1:
                return wave1Speed;
            case Wave.Wave2:
                return wave2Speed;
            case Wave.Wave3:
                return wave3Speed;
            default:
                return wave1Speed;
        }
    }

    private float GetCurrentWaveSpawnInterval()
    {
        switch (currentWave)
        {
            case Wave.Wave1:
                return wave1SpawnInterval;
            case Wave.Wave2:
                return wave2SpawnInterval;
            case Wave.Wave3:
                return wave3SpawnInterval;
            default:
                return wave1SpawnInterval;
        }
    }

    private bool AdvanceWaveAfterHit()
    {
        switch (currentWave)
        {
            case Wave.Wave1:
                if (wave1SuccessCount < wave1ClearCount)
                {
                    return false;
                }

                currentWave = Wave.Wave2;
                SetMessage("Wave 2");
                UpdateUI();
                UpdateDebugOverlay();
                Log("Wave advanced to " + currentWave);
                return true;

            case Wave.Wave2:
                if (wave2SuccessCount < wave2ClearCount)
                {
                    return false;
                }

                currentWave = Wave.Wave3;
                SetMessage("Wave 3");
                UpdateUI();
                UpdateDebugOverlay();
                Log("Wave advanced to " + currentWave);
                return true;

            case Wave.Wave3:
                if (wave3SuccessCount < wave3ClearCount)
                {
                    return false;
                }

                WinGame();
                return true;

            default:
                return false;
        }
    }

    private bool ContainsInvalidWave3Input(HeartbeatPattern pattern, HashSet<KeyCode> frameKeys)
    {
        if (pattern.Length == 0 || frameKeys == null || frameKeys.Count == 0)
        {
            return false;
        }

        foreach (KeyCode key in frameKeys)
        {
            if (!pattern.ContainsAny(new HashSet<KeyCode> { key }))
            {
                return true;
            }
        }

        return false;
    }

    private void RegisterHit(HeartbeatNote note)
    {
        Log("RegisterHit() pattern=" + (note != null ? note.Pattern.DisplayText : "none"));
        BoostHeartbeatOnHit();
        PlayFeedbackSound(true);
        if (note != null)
        {
            hitPointDisplay?.PlayHitFeedback(note.Pattern);
            RemoveNote(note, true);
        }

        switch (currentWave)
        {
            case Wave.Wave1:
                wave1SuccessCount++;
                break;

            case Wave.Wave2:
                wave2SuccessCount++;
                break;

            case Wave.Wave3:
                wave3SuccessCount++;
                break;
        }

        bool waveCompleted = AdvanceWaveAfterHit();
        heartbeatDotDrawer?.OnHit();
        UpdateUI();
        UpdateDebugOverlay();
        if (gameState != GameState.Playing)
        {
            return;
        }

        if (!waveCompleted)
        {
            SetMessage("Hit!");
        }

        if (gameState == GameState.Playing)
        {
            ScheduleNextSpawn(GetCurrentWaveSpawnInterval());
        }
    }

    private void RegisterMiss(HeartbeatNote note, string reason, bool consumeNote)
    {
        Log("RegisterMiss(): " + reason);
        lastResolutionDebug = "Miss: " + reason;
        DropHeartbeatOnMiss();
        PlayFeedbackSound(false);
        PlayMissCanvasEffect();
        if (note != null && consumeNote)
        {
            RemoveNote(note, false);
        }

        if (note != null)
        {
            hitPointDisplay?.PlayMissFeedback(note.Pattern);
        }

        missCount++;
        heartbeatDotDrawer?.OnMiss();
        UpdateUI();
        SetMessage(reason);

        if (missCount >= maxMisses)
        {
            LoseGame();
            return;
        }

        if (gameState == GameState.Playing)
        {
            ScheduleNextSpawn(GetCurrentWaveSpawnInterval());
        }
    }

    private void WinGame()
    {
        gameState = GameState.Win;
        StopSpawnRoutine();
        ResetMissCanvasEffect();
        heartbeatDotDrawer?.ApplyVictoryState();
        Log("WinGame()");
        lastResolutionDebug = "Game finished: win";

        ClearActiveNotes();
        promptTextSet(string.Empty);
        HideHitPointPrompt();
        SetMessage("You win!");
        UpdateUI();
        UpdateDebugOverlay();

        PlayWinFlashEffect();

        if (disableMinigameOnWin)
        {
            DisableMinigameRoot();
        }

        PlayWinCutscene();
    }

    private void LoseGame()
    {
        gameState = GameState.Lose;
        StopSpawnRoutine();
        ResetMissCanvasEffect();
        heartbeatDotDrawer?.ApplyDefeatState();
        Log("LoseGame()");
        lastResolutionDebug = "Game finished: lose";

        heartbeatTargetIntensity = 0f;
        heartbeatIntensity = 0f;
        SyncHeartbeatVisuals();
        promptTextSet(string.Empty);
        HideHitPointPrompt();
        SetMessage("Game over");
        UpdateUI();
        UpdateDebugOverlay();
    }

    private void DisableMinigameRoot()
    {
        GameObject target = minigameRoot != null ? minigameRoot : (canvas != null ? canvas.gameObject : null);
        if (target != null && target != gameObject)
        {
            target.SetActive(false);
        }
    }

    private void PlayWinCutscene()
    {
        if (winCutsceneObject != null)
        {
            winCutsceneObject.SetActive(true);
        }

        if (winCutscene == null)
        {
            return;
        }

        winCutscene.Stop();
        if (resetWinCutsceneTime)
        {
            winCutscene.time = 0;
        }

        winCutscene.Play();
    }

    private void SetupFeedbackAudioSource()
    {
        if (feedbackAudioSource != null)
        {
            runtimeFeedbackAudioSource = feedbackAudioSource;
            return;
        }

        runtimeFeedbackAudioSource = GetComponent<AudioSource>();
        if (runtimeFeedbackAudioSource == null)
        {
            runtimeFeedbackAudioSource = gameObject.AddComponent<AudioSource>();
        }

        runtimeFeedbackAudioSource.playOnAwake = false;
        runtimeFeedbackAudioSource.loop = false;
    }

    private void PlayFeedbackSound(bool isWin)
    {
        AudioClip clip = isWin ? winNoteClip : loseNoteClip;
        float volume = isWin ? winNoteVolume : loseNoteVolume;

        if (runtimeFeedbackAudioSource == null || clip == null)
        {
            return;
        }

        runtimeFeedbackAudioSource.PlayOneShot(clip, volume);
    }

    private void SetupWinFlashImage()
    {
        if (winFlashImage != null)
        {
            ConfigureWinFlashImage(winFlashImage);
            return;
        }

        if (canvas == null)
        {
            return;
        }

        Transform existing = canvas.transform.Find("WinFlashOverlay");
        if (existing != null)
        {
            winFlashImage = existing.GetComponent<Image>();
            ConfigureWinFlashImage(winFlashImage);
            return;
        }

        GameObject flashObject = new GameObject(
            "WinFlashOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        flashObject.transform.SetParent(canvas.transform, false);
        flashObject.transform.SetAsLastSibling();

        RectTransform rect = flashObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        winFlashImage = flashObject.GetComponent<Image>();
        ConfigureWinFlashImage(winFlashImage);
    }

    private void ConfigureWinFlashImage(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = false;
        image.gameObject.SetActive(false);
    }

    private void PlayWinFlashEffect()
    {
        if (winFlashImage == null)
        {
            return;
        }

        if (winFlashRoutine != null)
        {
            StopCoroutine(winFlashRoutine);
        }

        winFlashImage.gameObject.SetActive(true);
        winFlashRoutine = StartCoroutine(WinFlashRoutine());
    }

    private IEnumerator WinFlashRoutine()
    {
        Color color = winFlashImage.color;
        float halfDuration = Mathf.Max(0.01f, winFlashDuration * 0.5f);

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float eased = Mathf.Sin(t * Mathf.PI * 0.5f);
            color.a = Mathf.Lerp(winFlashStartAlpha, winFlashPeakAlpha, eased);
            winFlashImage.color = color;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float eased = Mathf.Sin(t * Mathf.PI * 0.5f);
            color.a = Mathf.Lerp(winFlashPeakAlpha, 0f, eased);
            winFlashImage.color = color;
            yield return null;
        }

        color.a = 0f;
        winFlashImage.color = color;
        winFlashImage.gameObject.SetActive(false);
        winFlashRoutine = null;
    }

    private void ResetWinFlashEffect()
    {
        if (winFlashRoutine != null)
        {
            StopCoroutine(winFlashRoutine);
            winFlashRoutine = null;
        }

        if (winFlashImage != null)
        {
            Color color = winFlashImage.color;
            color.a = 0f;
            winFlashImage.color = color;
            winFlashImage.gameObject.SetActive(false);
        }
    }

    private void StopSpawnRoutine()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private void UpdateUI()
    {
        if (waveText != null)
        {
            switch (currentWave)
            {
                case Wave.Wave1:
                    waveText.text = "Wave 1";
                    break;
                case Wave.Wave2:
                    waveText.text = "Wave 2";
                    break;
                case Wave.Wave3:
                    waveText.text = "Wave 3";
                    break;
                default:
                    waveText.text = "Wave";
                    break;
            }
        }

        if (missText != null)
        {
            missText.text = $"Miss: {missCount}/{maxMisses}";
        }

        if (progressText != null)
        {
            progressText.text = GetProgressText();
        }
    }

    private string GetProgressText()
    {
        switch (currentWave)
        {
            case Wave.Wave1:
                return $"Wave 1: {wave1SuccessCount}/{wave1ClearCount}";
            case Wave.Wave2:
                return $"Wave 1 cleared | Wave 2: {wave2SuccessCount}/{wave2ClearCount}";
            case Wave.Wave3:
                return $"Wave 1 cleared | Wave 2 cleared | Wave 3: {wave3SuccessCount}/{wave3ClearCount}";
            default:
                return string.Empty;
        }
    }

    private void SetMessage(string text)
    {
        if (messageText != null)
        {
            messageText.text = text;
        }
    }

    private void promptTextSet(string text)
    {
        if (promptText != null)
        {
            promptText.text = text;
        }
    }

    private void ShowHitPointPrompt(HeartbeatPattern pattern)
    {
        if (hitPointDisplay != null)
        {
            hitPointDisplay.Show(pattern);
        }

        Log("ShowHitPointPrompt: " + pattern.DisplayText);
    }

    private void HideHitPointPrompt()
    {
        if (hitPointDisplay != null)
        {
            hitPointDisplay.Hide();
        }

        Log("HideHitPointPrompt");
    }

    private Vector2 GetLocalPointOnCanvas(Transform source)
    {
        if (source == null)
        {
            return Vector2.zero;
        }

        RectTransform parentRect = noteParent as RectTransform;
        if (parentRect == null)
        {
            return source.localPosition;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(), source.position);
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, GetCanvasCamera(), out localPoint);
        return localPoint;
    }

    private Camera GetCanvasCamera()
    {
        if (canvas == null)
        {
            return null;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    private void Log(string message)
    {
        if (!verboseDebug)
        {
            return;
        }

        Debug.Log("[HeartbeatRhythmGame] " + message, this);

        if (debugText != null)
        {
            debugText.text = message;
        }
    }

    private void LogError(string message)
    {
        Debug.LogError("[HeartbeatRhythmGame] " + message, this);

        if (debugText != null)
        {
            debugText.text = "ERROR: " + message;
        }
    }

    private void UpdateDebugOverlay()
    {
        if (!showRuntimeDebugOverlay || debugText == null)
        {
            return;
        }

        string stateText = "State: " + gameState;
        string waveState = "Wave: " + currentWave;
        HeartbeatNote currentNote = CurrentNote;
        string noteState = currentNote != null
            ? "Note: " + currentNote.Pattern.DisplayText + " | within=" + currentNote.IsWithinHitWindow(hitWindowDistance)
            : "Note: none";
        string queueState = "Queued notes: " + activeNotes.Count;
        string pressedText = "Inputs: " + GetPressedKeysText(pressedThisFrame);
        string recentText = "Recent: " + GetRecentPressTimesText();
        string metricsText = "HitWindow=" + hitWindowDistance.ToString("0.00")
            + " ComboWindow=" + comboInputWindow.ToString("0.00")
            + " SpawnInterval(W1/W2/W3)="
            + wave1SpawnInterval.ToString("0.00") + "/"
            + wave2SpawnInterval.ToString("0.00") + "/"
            + wave3SpawnInterval.ToString("0.00")
            + " Heartbeat=" + heartbeatIntensity.ToString("0.00") + "->" + heartbeatTargetIntensity.ToString("0.00");

        debugText.text = string.Join("\n", new[]
        {
            stateText,
            waveState,
            noteState,
            queueState,
            pressedText,
            recentText,
            "Last input: " + lastInputDebug,
            "Last result: " + lastResolutionDebug,
            metricsText
        });
    }

    private void RemoveNote(HeartbeatNote note, bool wasHit)
    {
        if (note == null)
        {
            return;
        }

        activeNotes.Remove(note);
        if (wasHit)
        {
            Destroy(note.gameObject);
        }
        else
        {
            Destroy(note.gameObject);
        }

        recentPressTimes.Clear();
        RefreshPromptForCurrentNote();
    }

    private void ClearActiveNotes()
    {
        for (int i = 0; i < activeNotes.Count; i++)
        {
            HeartbeatNote note = activeNotes[i];
            if (note != null)
            {
                Destroy(note.gameObject);
            }
        }

        activeNotes.Clear();
    }

    private void PlayMissCanvasEffect()
    {
        if (canvasGroup == null && missEffectRootTransform == null)
        {
            return;
        }

        ResetMissCanvasEffect();
        missCanvasEffectRoutine = StartCoroutine(MissCanvasEffectRoutine());
    }

    private IEnumerator MissCanvasEffectRoutine()
    {
        Vector3 originalScale = missEffectRootTransform != null ? missEffectRootBaseScale : Vector3.one;
        float originalAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        Vector3 targetScale = originalScale * missCanvasScaleMultiplier;

        float elapsed = 0f;
        while (elapsed < missCanvasEffectDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / missCanvasEffectDuration);
            float ease = Mathf.Sin(t * Mathf.PI);
            if (missEffectRootTransform != null)
            {
                missEffectRootTransform.localScale = Vector3.Lerp(originalScale, targetScale, ease);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(originalAlpha, missCanvasFlashAlpha, ease);
            }

            yield return null;
        }

        if (missEffectRootTransform != null)
        {
            missEffectRootTransform.localScale = originalScale;
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = originalAlpha;
        }

        missCanvasEffectRoutine = null;
    }

    private void ResetMissCanvasEffect()
    {
        if (missCanvasEffectRoutine != null)
        {
            StopCoroutine(missCanvasEffectRoutine);
            missCanvasEffectRoutine = null;
        }

        if (missEffectRootTransform != null)
        {
            missEffectRootTransform.localScale = missEffectRootBaseScale;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = canvasBaseAlpha;
        }
    }

    private void RefreshPromptForCurrentNote()
    {
        HeartbeatNote currentNote = CurrentNote;
        if (currentNote != null)
        {
            ShowHitPointPrompt(currentNote.Pattern);
            promptTextSet(currentNote.Pattern.DisplayText);
        }
        else
        {
            HideHitPointPrompt();
            promptTextSet(string.Empty);
        }
    }

    private void UpdateHeartbeatPhysiology()
    {
        heartbeatTargetIntensity = Mathf.MoveTowards(
            heartbeatTargetIntensity,
            heartbeatMinIntensity,
            heartbeatPassiveDecayPerSecond * Time.deltaTime);

        heartbeatIntensity = Mathf.MoveTowards(
            heartbeatIntensity,
            heartbeatTargetIntensity,
            heartbeatVisualSyncSpeed * Time.deltaTime);
    }

    private void SyncHeartbeatVisuals()
    {
        if (activeNotes.Count == 0)
        {
            return;
        }

        for (int i = 0; i < activeNotes.Count; i++)
        {
            HeartbeatNote note = activeNotes[i];
            if (note != null)
            {
                note.SetHeartbeatIntensity(heartbeatIntensity);
            }
        }
    }

    private void BoostHeartbeatOnHit()
    {
        heartbeatTargetIntensity = Mathf.Clamp01(heartbeatTargetIntensity + heartbeatHitBoost);
        heartbeatIntensity = Mathf.Clamp01(Mathf.Max(heartbeatIntensity, heartbeatTargetIntensity));
    }

    private void DropHeartbeatOnMiss()
    {
        heartbeatTargetIntensity = Mathf.Clamp01(heartbeatTargetIntensity - heartbeatMissDrop);
        if (heartbeatTargetIntensity < heartbeatMinIntensity)
        {
            heartbeatTargetIntensity = heartbeatMinIntensity;
        }
    }

    private static string GetPressedKeysText(HashSet<KeyCode> keys)
    {
        if (keys == null || keys.Count == 0)
        {
            return "none";
        }

        string text = string.Empty;
        foreach (KeyCode key in keys)
        {
            if (text.Length > 0)
            {
                text += ", ";
            }

            text += key;
        }

        return text;
    }

    private string GetRecentPressTimesText()
    {
        if (recentPressTimes == null || recentPressTimes.Count == 0)
        {
            return "none";
        }

        string text = string.Empty;
        foreach (KeyValuePair<KeyCode, float> entry in recentPressTimes)
        {
            if (text.Length > 0)
            {
                text += " | ";
            }

            text += entry.Key + ":" + (Time.time - entry.Value).ToString("0.00") + "s ago";
        }

        return text;
    }
}

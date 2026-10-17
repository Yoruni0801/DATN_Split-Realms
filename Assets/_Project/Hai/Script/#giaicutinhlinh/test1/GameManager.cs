using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("HP Settings")]
    [SerializeField] private int minHP = 0;
    [SerializeField] private int maxHP = 9;
    [SerializeField] private int startHP = 3;

    [Header("Tutorial Settings")]
    [SerializeField] private bool isTutorialMode = true;
    [SerializeField] private RectTransform tutorialFocusRing;
    [SerializeField] private float ringShrinkDuration = 0.8f;
    [SerializeField] private float ringStartScale = 3f;
    [SerializeField] private float ringEndScale = 1f;

    [Header("References (UI Canvas)")]
    [SerializeField] private EcgLineGraphic ecgLineGraphic;
    [SerializeField] private CrossKeyUI crossKeyUI;
    [SerializeField] private GameObject miniGameUI;
    [Tooltip("Đối tượng riêng biệt để rung khi win. Nếu để trống sẽ rung miniGameUI.")]
    [SerializeField] private GameObject shakeTarget;
    [SerializeField] private Image winFlashImage;

    [Header("Win FX Settings")]
    [SerializeField] private float shakeDuration = 0.6f;
    [SerializeField] private float shakeMagnitude = 12f;
    [SerializeField] private float flashFadeInDuration = 0.15f;
    [SerializeField] private float flashFadeOutDuration = 0.5f;
    [SerializeField] private bool hideMiniGameObjectAfterUIHidden = true;

    [Tooltip("Vị trí vạch Target cố định (Mốc 0.75 ở 3/4 sát rìa bên phải màn hình).")]
    [Range(0f, 1f)]
    [SerializeField] private float targetXNormalized = 0.75f;

    [Header("Gameplay Hitbox Config")]
    [Tooltip("Độ rộng của Box va chạm (Tỉ lệ từ 0.0 đến 1.0).")]
    [SerializeField] private float hitBoxSize = 0.06f;
    [Tooltip("Thời gian chờ đổi phím bấm mới sau khi vừa kết thúc lượt cũ (giây).")]
    [SerializeField] private float delayBetweenSpawns = 0.3f;

    [Header("Rhythm Flow")]
    [Min(0f)] [SerializeField] private float phraseBeatDelay = 0.08f;
    [Min(0f)] [SerializeField] private float phraseRestDelay = 0.42f;
    [Range(0.7f, 2f)] [SerializeField] private float singleBeatLeadTime = 1.2f;
    [Range(0.35f, 1.2f)] [SerializeField] private float doubleBeatLeadTime = 0.64f;
    [Range(0.3f, 1f)] [SerializeField] private float finaleBeatLeadTime = 0.48f;
    [Range(2, 4)] [SerializeField] private int finaleBeatCount = 3;
    [Range(0.01f, 0.25f)] [SerializeField] private float progressPressureRange = 0.13f;

    [Header("Audio Settings (BGM & SFX)")]
    [SerializeField] private AudioClip miniGameBGM;
    [SerializeField] private bool stopBGMOnFinish = true;
    [SerializeField] private AudioClip hitSuccessSFX;
    [SerializeField] private AudioClip hitMissSFX;
    [SerializeField] private AudioClip winSFX;
    [SerializeField] private AudioClip loseSFX;
    [Range(0f, 0.2f)]
    [SerializeField] private float sfxPitchVariance = 0.05f;

    [Header("Events")]
    public UnityEvent OnStart;
    public UnityEvent<int> OnHPChanged;
    public UnityEvent OnWin;
    public UnityEvent OnLose;

    public int MaxHP => maxHP;

    private static readonly KeyCode[] AllKeys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };
    private static readonly RhythmPhrase[] RhythmFlow = { RhythmPhrase.Single, RhythmPhrase.Double, RhythmPhrase.Single };
    private static readonly List<CanvasGroup> s_CachedCanvasGroups = new List<CanvasGroup>();
    private static readonly List<Graphic> s_CachedGraphics = new List<Graphic>();
    private static readonly List<Canvas> s_CachedCanvases = new List<Canvas>();

    private int currentHP;
    private int currentHeartLevel = 3;
    private KeyCode currentRequiredKey;
    private KeyCode previousRequiredKey;
    private bool gameEnded;
    private bool hasWon;
    private bool hasStartedOnce;
    private bool hasActiveTurn;
    private bool waveWasInBox;
    private bool isGameRunning;
    private bool playerLockedByGameManager;
    private bool lastTimingPressureState;
    private int rhythmPhraseIndex;
    private int tutorialSuccessCount;
    private bool tutorialPaused;

    private Player lockedPlayer;
    private HPBarUI progressBarUI;
    private AudioSource fallbackAudioSource;
    private Coroutine spawnCoroutine;
    private Coroutine winSequenceCoroutine;
    private Coroutine focusRingCoroutine;

    private WaitForSeconds cachedWaitSpawnDelay;
    private WaitForSeconds cachedWaitPhraseBeatDelay;
    private WaitForSeconds cachedWaitPhraseRestDelay;

    private enum RhythmPhrase { Single, Double, Finale }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (maxHP == 20 || maxHP == 10) maxHP = 9;
        if (miniGameUI == null) miniGameUI = gameObject;

        progressBarUI = GetComponentInChildren<HPBarUI>(true);
        CacheWaitObjects();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxHP == 20 || maxHP == 10) maxHP = 9;
        CacheWaitObjects();
    }
#endif

    private void OnDestroy()
    {
        if (Instance == this)
        {
            UnlockPlayerIfNeeded();
            Instance = null;
        }
    }

    private void OnEnable()
    {
        if (hasWon) FinishMiniGame();
    }

    private void OnDisable()
    {
        if (Instance == this) StopGame();
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.F8))
        {
            StartRhythmTestForDebug();
            return;
        }
#endif
        if (isGameRunning && !gameEnded && lockedPlayer != ResolvePlayer())
        {
            LockPlayerIfNeeded();
        }

        if (gameEnded || ecgLineGraphic == null)
        {
            SetProgressTimingPressure(false);
            return;
        }

        float currentWaveX = ecgLineGraphic.GetCurrentWaveXNormalized(targetXNormalized);
        float distance = currentWaveX - targetXNormalized;
        bool isOverlapping = Mathf.Abs(distance) <= hitBoxSize * 0.5f;

        bool isTimingPressure = hasActiveTurn
            && !isTutorialMode
            && currentWaveX >= targetXNormalized - progressPressureRange
            && currentWaveX <= targetXNormalized + hitBoxSize * 0.5f;
        SetProgressTimingPressure(isTimingPressure);

        if (isOverlapping)
        {
            if (hasActiveTurn) waveWasInBox = true;
        }
        else if (hasActiveTurn && waveWasInBox && distance > hitBoxSize * 0.5f)
        {
            if (!isTutorialMode)
            {
                ResolveTurn(false);
                return;
            }
        }

        UpdateInput(isOverlapping);
    }

    private void UpdateInput(bool isOverlapping)
    {
        if (isTutorialMode && hasActiveTurn)
        {
            if (isOverlapping && !tutorialPaused)
            {
                tutorialPaused = true;
                ecgLineGraphic.isPaused = true;
                if (focusRingCoroutine != null) StopCoroutine(focusRingCoroutine);
                focusRingCoroutine = StartCoroutine(FocusRingLoop());
            }

            if (tutorialPaused && TryGetPressedKey(out KeyCode key))
            {
                if (key == currentRequiredKey)
                {
                    tutorialSuccessCount++;
                    tutorialPaused = false;
                    ecgLineGraphic.isPaused = false;

                    if (focusRingCoroutine != null)
                    {
                        StopCoroutine(focusRingCoroutine);
                        focusRingCoroutine = null;
                    }
                    if (tutorialFocusRing != null) tutorialFocusRing.gameObject.SetActive(false);
                    if (crossKeyUI != null) crossKeyUI.ResetAllScales();

                    ResolveTurn(true);
                    if (tutorialSuccessCount >= 2) isTutorialMode = false;
                }
                else
                {
                    PlaySFX(hitMissSFX);
                }
            }
            return;
        }

        if (hasActiveTurn && TryGetPressedKey(out KeyCode pressedKey))
        {
            ResolveTurn(pressedKey == currentRequiredKey && isOverlapping);
        }
    }

    private static bool TryGetPressedKey(out KeyCode pressedKey)
    {
        for (int i = 0; i < AllKeys.Length; i++)
        {
            KeyCode k = AllKeys[i];
            if (Input.GetKeyDown(k))
            {
                pressedKey = k;
                return true;
            }
        }
        pressedKey = KeyCode.None;
        return false;
    }

    public void ResetGame()
    {
        StopRunningCoroutines();

        gameEnded = false;
        hasWon = false;
        isGameRunning = true;
        hasActiveTurn = false;
        waveWasInBox = false;
        rhythmPhraseIndex = 0;
        previousRequiredKey = KeyCode.None;
        SetProgressTimingPressure(false);

        if (miniGameBGM != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMusic(miniGameBGM, true);
        }

        isTutorialMode = true;
        tutorialSuccessCount = 0;
        tutorialPaused = false;

        if (ecgLineGraphic != null) ecgLineGraphic.isPaused = false;
        if (tutorialFocusRing != null) tutorialFocusRing.gameObject.SetActive(false);
        if (crossKeyUI != null)
        {
            crossKeyUI.ResetAllScales();
            crossKeyUI.HideAll();
        }

        currentHP = startHP;
        currentHeartLevel = 3;

        OnStart?.Invoke();
        OnHPChanged?.Invoke(currentHP);
        LockPlayerIfNeeded();

        if (ecgLineGraphic != null)
        {
            ecgLineGraphic.SetHeartLevel(currentHeartLevel);
            ecgLineGraphic.DeactivateBeat();
            ecgLineGraphic.ResetVisualState();
        }

        CacheWaitObjects();
        spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    public void StartMiniGameFromTimelineOnce()
    {
        if (hasStartedOnce || hasWon) return;
        hasStartedOnce = true;
        if (miniGameUI != null) miniGameUI.SetActive(true);
        ResetGame();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Debug/Start Mini Game - Full Tutorial")]
    public void StartMiniGameForDebug()
    {
        if (miniGameUI != null && !miniGameUI.activeSelf) miniGameUI.SetActive(true);
        hasStartedOnce = true;
        ResetGame();
        Debug.Log("[MiniGameGiaiCuu] Full tutorial debug start/reset.", this);
    }

    [ContextMenu("Debug/Test Rhythm Phrases (F8)")]
    public void StartRhythmTestForDebug()
    {
        StartMiniGameForDebug();
        isTutorialMode = false;
        tutorialSuccessCount = 2;
        tutorialPaused = false;
        currentHP = Mathf.Clamp(maxHP - 6, minHP + 1, maxHP - 1);
        currentHeartLevel = Mathf.Clamp(5, 0, maxHP);
        OnHPChanged?.Invoke(currentHP);

        if (ecgLineGraphic != null)
        {
            ecgLineGraphic.isPaused = false;
            ecgLineGraphic.SetHeartLevel(currentHeartLevel);
        }
        Debug.Log("[MiniGameGiaiCuu] Rhythm phrase test via F8 (tutorial skipped).", this);
    }
#endif

    public void ContinueAfterWin() => FinishMiniGame();

    public void ExitMiniGame()
    {
        EndGame(false);
        FinishMiniGame();
    }

    private IEnumerator SpawnLoop()
    {
        while (!gameEnded)
        {
            if (isTutorialMode)
            {
                SpawnNewTurn(singleBeatLeadTime);
                while (hasActiveTurn && !gameEnded) yield return null;
                if (!gameEnded) yield return cachedWaitSpawnDelay;
                continue;
            }

            RhythmPhrase phrase = GetNextRhythmPhrase();
            int beatCount = GetBeatCount(phrase);
            for (int i = 0; i < beatCount && !gameEnded; i++)
            {
                SpawnNewTurn(GetLeadTime(phrase, i));
                while (hasActiveTurn && !gameEnded) yield return null;
                if (!gameEnded && i < beatCount - 1) yield return cachedWaitPhraseBeatDelay;
            }

            if (!gameEnded) yield return cachedWaitPhraseRestDelay;
        }
    }

    private IEnumerator FocusRingLoop()
    {
        if (tutorialFocusRing == null)
        {
            while (tutorialPaused)
            {
                if (crossKeyUI != null) crossKeyUI.SetActiveScale(1.0f + Mathf.PingPong(Time.unscaledTime * 3f, 0.15f));
                yield return null;
            }
            yield break;
        }

        tutorialFocusRing.gameObject.SetActive(true);
        if (crossKeyUI != null) tutorialFocusRing.position = crossKeyUI.GetActiveKeyWorldPosition();

        while (tutorialPaused)
        {
            float elapsed = 0f;
            tutorialFocusRing.localScale = new Vector3(ringStartScale, ringStartScale, 1f);

            while (elapsed < ringShrinkDuration && tutorialPaused)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / ringShrinkDuration);
                float currentScale = Mathf.Lerp(ringStartScale, ringEndScale, progress);

                tutorialFocusRing.localScale = new Vector3(currentScale, currentScale, 1f);
                if (crossKeyUI != null) crossKeyUI.SetActiveScale(1.0f + Mathf.PingPong(Time.unscaledTime * 3f, 0.15f));
                yield return null;
            }

            float pulseElapsed = 0f;
            while (pulseElapsed < 0.2f && tutorialPaused)
            {
                pulseElapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        tutorialFocusRing.gameObject.SetActive(false);
        if (crossKeyUI != null) crossKeyUI.ResetAllScales();
    }

    private void SpawnNewTurn(float leadTime)
    {
        waveWasInBox = false;
        hasActiveTurn = true;
        currentRequiredKey = GetNextRequiredKey();

        if (crossKeyUI != null) crossKeyUI.ShowKey(currentRequiredKey);
        if (ecgLineGraphic != null) ecgLineGraphic.ActivateNextBeat(targetXNormalized, leadTime);
    }

    private void ResolveTurn(bool isHit)
    {
        hasActiveTurn = false;
        SetProgressTimingPressure(false);
        if (crossKeyUI != null) crossKeyUI.HideAll();

        PlaySFX(isHit ? hitSuccessSFX : hitMissSFX);
        ChangeHP(isHit ? 1 : -1);
        if (gameEnded) return;

        if (ecgLineGraphic != null)
        {
            if (isHit) ecgLineGraphic.PlaySuccessPulse();
            else ecgLineGraphic.PlayMissPulse();
            ecgLineGraphic.DeactivateBeat();
        }

        currentHeartLevel = Mathf.Clamp(currentHeartLevel + (isHit ? 1 : -1), 0, maxHP);
        if (ecgLineGraphic != null) ecgLineGraphic.SetHeartLevel(currentHeartLevel);
    }

    private void ChangeHP(int delta)
    {
        currentHP = Mathf.Clamp(currentHP + delta, minHP, maxHP);
        OnHPChanged?.Invoke(currentHP);

        if (currentHP >= maxHP) TriggerWinGame();
        else if (currentHP <= minHP && !gameEnded) TriggerLoseGame();
    }

    private void EndGame(bool won)
    {
        gameEnded = true;
        isGameRunning = false;
        hasWon = won;
        hasActiveTurn = false;
        SetProgressTimingPressure(false);
        StopRunningCoroutines();

        if (crossKeyUI != null) crossKeyUI.HideAll();
        if (stopBGMOnFinish && AudioManager.Instance != null) AudioManager.Instance.StopMusic();
    }

    private void TriggerLoseGame()
    {
        if (gameEnded) return;
        EndGame(false);
        if (ecgLineGraphic != null) ecgLineGraphic.ForceFlatline();
        PlaySFX(loseSFX);
        OnLose?.Invoke();
    }

    private void TriggerWinGame()
    {
        if (gameEnded) return;
        EndGame(true);
        PlaySFX(winSFX);
        winSequenceCoroutine = StartCoroutine(WinSequenceCoroutine());
    }

    private void PlaySFX(AudioClip clip, float customPitch = 1f)
    {
        if (clip == null) return;

        float finalPitch = customPitch;
        if (sfxPitchVariance > 0.001f)
        {
            finalPitch *= 1f + Random.Range(-sfxPitchVariance, sfxPitchVariance);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip, transform.position, finalPitch);
        }
        else
        {
            if (fallbackAudioSource == null && !TryGetComponent(out fallbackAudioSource))
            {
                fallbackAudioSource = gameObject.AddComponent<AudioSource>();
                fallbackAudioSource.playOnAwake = false;
                fallbackAudioSource.spatialBlend = 0f;
            }
            fallbackAudioSource.pitch = finalPitch;
            fallbackAudioSource.PlayOneShot(clip);
        }
    }

    private IEnumerator WinSequenceCoroutine()
    {
        yield return PlayWinEffects();
        OnWin?.Invoke();
        FinishMiniGame();
    }

    private IEnumerator PlayWinEffects()
    {
        Vector3 originalPosition = Vector3.zero;
        Transform targetTransform = null;
        GameObject targetObj = shakeTarget != null ? shakeTarget : miniGameUI;

        if (targetObj != null)
        {
            targetTransform = targetObj.transform;
            originalPosition = targetTransform.localPosition;
        }

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            if (targetTransform != null)
            {
                float offsetX = Random.Range(-1f, 1f) * shakeMagnitude;
                float offsetY = Random.Range(-1f, 1f) * shakeMagnitude;
                targetTransform.localPosition = originalPosition + new Vector3(offsetX, offsetY, 0f);
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (targetTransform != null) targetTransform.localPosition = originalPosition;
        if (winFlashImage == null) yield break;

        winFlashImage.gameObject.SetActive(true);
        Color color = winFlashImage.color;
        color.a = 0f;
        winFlashImage.color = color;

        elapsed = 0f;
        while (elapsed < flashFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Clamp01(elapsed / flashFadeInDuration);
            winFlashImage.color = color;
            yield return null;
        }

        color.a = 1f;
        winFlashImage.color = color;
        DestroyMiniGameObjectAfterWin();

        elapsed = 0f;
        while (elapsed < flashFadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Clamp01(1f - elapsed / flashFadeOutDuration);
            if (winFlashImage != null) winFlashImage.color = color;
            yield return null;
        }

        if (winFlashImage != null)
        {
            color.a = 0f;
            winFlashImage.color = color;
            winFlashImage.gameObject.SetActive(false);
            Destroy(winFlashImage.gameObject);
        }
    }

    private void FinishMiniGame()
    {
        isGameRunning = false;
        UnlockPlayerIfNeeded();

        if (stopBGMOnFinish && AudioManager.Instance != null) AudioManager.Instance.StopMusic();
        if (!hideMiniGameObjectAfterUIHidden) return;

        GameObject target = miniGameUI != null ? miniGameUI : gameObject;
        if (target != null)
        {
            if (hasWon) Destroy(target);
            else target.SetActive(false);
        }
    }

    private void DestroyMiniGameObjectAfterWin()
    {
        if (!hasWon) return;

        isGameRunning = false;
        UnlockPlayerIfNeeded();

        if (winFlashImage != null) winFlashImage.transform.SetParent(null, true);

        GameObject target = miniGameUI != null ? miniGameUI : gameObject;
        if (target == null) return;

        HideMiniGameVisualsImmediately(target);
        if (target != gameObject)
        {
            Destroy(target);
            miniGameUI = null;
        }
    }

    private static void HideMiniGameVisualsImmediately(GameObject target)
    {
        target.GetComponentsInChildren(true, s_CachedCanvasGroups);
        for (int i = 0; i < s_CachedCanvasGroups.Count; i++)
        {
            s_CachedCanvasGroups[i].alpha = 0f;
            s_CachedCanvasGroups[i].interactable = false;
            s_CachedCanvasGroups[i].blocksRaycasts = false;
        }
        s_CachedCanvasGroups.Clear();

        target.GetComponentsInChildren(true, s_CachedGraphics);
        for (int i = 0; i < s_CachedGraphics.Count; i++) s_CachedGraphics[i].enabled = false;
        s_CachedGraphics.Clear();

        target.GetComponentsInChildren(true, s_CachedCanvases);
        for (int i = 0; i < s_CachedCanvases.Count; i++) s_CachedCanvases[i].enabled = false;
        s_CachedCanvases.Clear();
    }

    private void StopGame()
    {
        EndGame(false);
        UnlockPlayerIfNeeded();
    }

    private void StopRunningCoroutines()
    {
        if (spawnCoroutine != null) { StopCoroutine(spawnCoroutine); spawnCoroutine = null; }
        if (winSequenceCoroutine != null) { StopCoroutine(winSequenceCoroutine); winSequenceCoroutine = null; }
        if (focusRingCoroutine != null) { StopCoroutine(focusRingCoroutine); focusRingCoroutine = null; }
    }

    private void LockPlayerIfNeeded()
    {
        Player targetPlayer = ResolvePlayer();
        if (targetPlayer == null || (playerLockedByGameManager && lockedPlayer == targetPlayer)) return;

        if (playerLockedByGameManager && lockedPlayer != null)
        {
            lockedPlayer.SetControlLocked(false);
            lockedPlayer.SetInputLocked(false);
        }

        targetPlayer.SetControlLocked(true);
        targetPlayer.SetInputLocked(true);
        lockedPlayer = targetPlayer;
        playerLockedByGameManager = true;
    }

    private void UnlockPlayerIfNeeded()
    {
        if (!playerLockedByGameManager) { lockedPlayer = null; return; }
        if (lockedPlayer != null)
        {
            lockedPlayer.SetInputLocked(false);
            lockedPlayer.SetControlLocked(false);
        }

        lockedPlayer = null;
        playerLockedByGameManager = false;
    }

    private static Player ResolvePlayer()
    {
        if (PlayerRegistry.Current != null) return PlayerRegistry.Current;
        return FindFirstObjectByType<Player>();
    }

    private void SetProgressTimingPressure(bool isActive)
    {
        if (lastTimingPressureState == isActive) return;
        lastTimingPressureState = isActive;

        if (progressBarUI == null) progressBarUI = GetComponentInChildren<HPBarUI>(true);
        if (progressBarUI != null) progressBarUI.SetTimingPressure(isActive);
    }

    private void CacheWaitObjects()
    {
        cachedWaitSpawnDelay = new WaitForSeconds(delayBetweenSpawns);
        cachedWaitPhraseBeatDelay = new WaitForSeconds(phraseBeatDelay);
        cachedWaitPhraseRestDelay = new WaitForSeconds(phraseRestDelay);
    }

    private KeyCode GetNextRequiredKey()
    {
        if (AllKeys.Length <= 1) return AllKeys[0];
        KeyCode nextKey;
        do { nextKey = AllKeys[Random.Range(0, AllKeys.Length)]; }
        while (nextKey == previousRequiredKey);

        previousRequiredKey = nextKey;
        return nextKey;
    }

    private RhythmPhrase GetNextRhythmPhrase()
    {
        int beatsUntilWin = Mathf.Max(1, maxHP - currentHP);
        if (beatsUntilWin <= finaleBeatCount) return RhythmPhrase.Finale;

        RhythmPhrase phrase = RhythmFlow[rhythmPhraseIndex % RhythmFlow.Length];
        rhythmPhraseIndex++;
        return phrase;
    }

    private int GetBeatCount(RhythmPhrase phrase)
    {
        if (phrase == RhythmPhrase.Double) return 2;
        if (phrase == RhythmPhrase.Finale) return Mathf.Clamp(maxHP - currentHP, 1, finaleBeatCount);
        return 1;
    }

    private float GetLeadTime(RhythmPhrase phrase, int beatIndex)
    {
        if (phrase == RhythmPhrase.Finale)
        {
            return beatIndex == GetBeatCount(phrase) - 1 ? finaleBeatLeadTime + 0.12f : finaleBeatLeadTime;
        }
        if (phrase == RhythmPhrase.Double && beatIndex > 0) return doubleBeatLeadTime;
        return singleBeatLeadTime;
    }
}

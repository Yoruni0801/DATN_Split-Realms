using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MiniGameslider : MonoBehaviour
{
    [Serializable]
    public class LevelConfig
    {
        [Tooltip("Độ rộng của vùng cần giữ handle đúng vị trí.")]
        public float targetWidth = 180f;

        [Tooltip("Tốc độ thanh mục tiêu di chuyển theo trục X.")]
        public float targetMoveSpeed = 140f;

        [Tooltip("Biên độ co giãn của width.")]
        public float targetWidthPulse = 40f;

        [Tooltip("Tốc độ co giãn width.")]
        public float targetWidthPulseSpeed = 2f;

        [Tooltip("Tốc độ handle rơi khi không bấm.")]
        public float fallSpeed = 220f;

        [Tooltip("Tốc độ kéo handle khi giữ phím.")]
        public float liftSpeed = 320f;

        [Tooltip("Thời gian giữ đúng vùng để qua màn.")]
        public float holdTimeToClear = 1f;
    }

    [Header("References")]
    public RectTransform handle;
    public RectTransform targetBar;
    public RectTransform playArea;

    [Header("Level Progress")]
    [FormerlySerializedAs("spiritHealthFillImage")]
    public Image levelProgressFillImage;
    public Image levelsCompleteEffect;
    public float progressFillDuration = 0.35f;
    public float levelCompletePause = 0.2f;
    public float level2CompleteTimeout = 5f;
    public Image.OriginHorizontal levelsCompleteEffectOrigin = Image.OriginHorizontal.Right;

    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool drawGizmos = true;
    public Color playAreaGizmoColor = new Color(1f, 1f, 1f, 0.35f);
    public Color targetGizmoColor = new Color(0.2f, 1f, 0.35f, 0.7f);
    public Color handleGizmoColor = new Color(1f, 0.35f, 0.2f, 0.8f);

    [Header("Complete")]
    public GameObject gameObjectToDisableOnComplete;

    [Header("Events")]
    public UnityEvent onAllLevelsCleared;
    public UnityEvent onGameFailed;

    [Header("Level Tuning")]
    public bool allowTargetSizePulse = false;
    public LevelConfig[] levels = new LevelConfig[3];

    [Header("Target Bar")]
    [Tooltip("Tốc độ di chuyển của targetBar. Nếu lớn hơn 0, giá trị này sẽ ưu tiên hơn speed theo từng level.")]
    public float targetBarMoveSpeed = 0f;

    [Header("Handle")]
    [Tooltip("Tốc độ di chuyển của handle. Nếu lớn hơn 0, giá trị này sẽ ưu tiên hơn speed theo từng level.")]
    public float handleMoveSpeed = 0f;

    [Header("Clamp")]
    public bool useHandlePadding = false;
    public float handleLeftPadding = 10f;
    public float handleRightPadding = 10f;

    [Header("Input")]
    public KeyCode inputKey = KeyCode.Space;

    private int currentLevelIndex;
    private float holdTimer;
    private bool isRunning = true;
    private bool isCompleted;
    private bool isAdvancingLevel;

    private float targetCenterX;
    private float targetWidthCurrent;
    private float targetMoveDirection = 1f;
    private float targetMovePhase;

    private Rect playRect;

    private Vector2 originalHandleSize;
    private Vector3 originalHandleScale;
    private Vector2 originalHandleAnchoredPosition;
    private bool hasOriginalHandleTransform;
    private int clearedLevelCount;
    private float currentProgressFill;
    private Coroutine progressFillCoroutine;
    private bool level2TimeoutRunning;
    private float level2TimeoutRemaining;

    private void Reset()
    {
        InitializeDefaultLevels();
    }

    private void Awake()
    {
        InitializeDefaultLevels();
        CacheOriginalHandleTransform();
    }

    private void Start()
    {
        CachePlayRect();
        SetupLevelProgressFill();
        SetupLevelsCompleteEffect();
        ResetLevelState();
        LogDebug($"Start game. Levels={GetLevelCount()}");
    }

    private void Update()
    {
        if (!isRunning || isCompleted || isAdvancingLevel)
        {
            return;
        }

        if (handle == null || targetBar == null || playArea == null)
        {
            return;
        }

        CachePlayRect();

        LevelConfig level = GetCurrentLevel();
        float dt = Time.deltaTime;

        TickLevelsCompleteEffect(dt);
        if (!isRunning || isCompleted)
        {
            return;
        }

        UpdateTarget(level, dt);
        UpdateHandle(level, dt);
        EvaluateProgress(level, dt);
    }

    public void RestartGame()
    {
        isRunning = true;
        isCompleted = false;
        isAdvancingLevel = false;
        level2TimeoutRunning = false;
        currentLevelIndex = 0;
        clearedLevelCount = 0;
        holdTimer = 0f;

        CachePlayRect();
        SetLevelProgressFill(0f);
        SetLevelsCompleteEffectFill(0f);
        ResetLevelState();
        LogDebug("Restart game.");
    }

    public void StopGame()
    {
        isRunning = false;
    }

    private void InitializeDefaultLevels()
    {
        if (levels == null || levels.Length != 3)
        {
            levels = new LevelConfig[3];
        }

        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] == null)
            {
                levels[i] = new LevelConfig();
            }
        }

        levels[0].targetWidth = 165f;
        levels[0].targetMoveSpeed = 120f;
        levels[0].targetWidthPulse = 18f;
        levels[0].targetWidthPulseSpeed = 1.5f;
        levels[0].fallSpeed = 180f;
        levels[0].liftSpeed = 330f;
        levels[0].holdTimeToClear = 1.15f;

        levels[1].targetWidth = 135f;
        levels[1].targetMoveSpeed = 185f;
        levels[1].targetWidthPulse = 28f;
        levels[1].targetWidthPulseSpeed = 2.1f;
        levels[1].fallSpeed = 240f;
        levels[1].liftSpeed = 375f;
        levels[1].holdTimeToClear = 1.2f;

        levels[2].targetWidth = 110f;
        levels[2].targetMoveSpeed = 235f;
        levels[2].targetWidthPulse = 35f;
        levels[2].targetWidthPulseSpeed = 2.7f;
        levels[2].fallSpeed = 285f;
        levels[2].liftSpeed = 410f;
        levels[2].holdTimeToClear = 1.25f;
    }

    private void ResetLevelState()
    {
        if (handle == null || targetBar == null || playArea == null)
        {
            return;
        }

        currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, levels.Length - 1);
        holdTimer = 0f;
        targetMoveDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;
        targetMovePhase = 0f;

        CachePlayRect();
        RestoreHandleTransform();

        SetTargetToLevelDefaults(GetCurrentLevel());
        SetHandleToStartPosition();

        SyncTargetWidth();
    }

    private void CachePlayRect()
    {
        if (playArea == null)
        {
            return;
        }

        playRect = playArea.rect;
    }

    private LevelConfig GetCurrentLevel()
    {
        if (levels == null || levels.Length == 0)
        {
            return new LevelConfig();
        }

        int index = Mathf.Clamp(currentLevelIndex, 0, levels.Length - 1);
        return levels[index] ?? new LevelConfig();
    }

    private void SetTargetToLevelDefaults(LevelConfig level)
    {
        targetWidthCurrent = Mathf.Max(20f, level.targetWidth);

        Vector2 targetAnchored = targetBar.anchoredPosition;
        targetAnchored.x = 0f;
        targetBar.anchoredPosition = targetAnchored;

        Vector2 size = targetBar.sizeDelta;
        size.y = targetWidthCurrent;
        targetBar.sizeDelta = size;

        targetCenterX = 0f;
        targetMovePhase = 0f;
    }

    private void ApplyTargetWidthForLevel(LevelConfig level)
    {
        targetWidthCurrent = Mathf.Max(20f, level.targetWidth);
        SyncTargetWidth();

        float halfPlayWidth = Mathf.Max(0f, playRect.width * 0.5f);
        float targetLimit = Mathf.Max(0f, halfPlayWidth - targetWidthCurrent * 0.5f);
        targetCenterX = Mathf.Clamp(targetCenterX, -targetLimit, targetLimit);

        Vector2 anchored = targetBar.anchoredPosition;
        anchored.x = targetCenterX;
        targetBar.anchoredPosition = anchored;
    }

    private void SetHandleToStartPosition()
    {
        RestoreHandleTransform();

        Vector2 anchored = handle.anchoredPosition;
        anchored.x = Mathf.Clamp(originalHandleAnchoredPosition.x, GetHandleMinX(), GetHandleMaxX());
        handle.anchoredPosition = anchored;
    }

    private void UpdateTarget(LevelConfig level, float dt)
    {
        float pulse = allowTargetSizePulse
            ? Mathf.Sin(Time.time * level.targetWidthPulseSpeed) * level.targetWidthPulse
            : 0f;

        targetWidthCurrent = Mathf.Max(20f, level.targetWidth + pulse);

        float halfPlayWidth = Mathf.Max(0f, playRect.width * 0.5f);
        float halfTargetWidth = targetWidthCurrent * 0.5f;
        float targetLimit = Mathf.Max(0f, halfPlayWidth - halfTargetWidth);
        float moveSpeed = targetBarMoveSpeed > 0f ? targetBarMoveSpeed : level.targetMoveSpeed;

        if (targetLimit > 0f)
        {
            float angularSpeed = moveSpeed / targetLimit;
            targetMovePhase += angularSpeed * targetMoveDirection * dt;
            targetCenterX = Mathf.Sin(targetMovePhase) * targetLimit;
        }
        else
        {
            targetCenterX = 0f;
        }

        Vector2 size = targetBar.sizeDelta;
        size.y = targetWidthCurrent;
        targetBar.sizeDelta = size;

        Vector2 anchored = targetBar.anchoredPosition;
        anchored.x = targetCenterX;
        targetBar.anchoredPosition = anchored;
    }

    private void UpdateHandle(LevelConfig level, float dt)
    {
        Vector2 anchored = handle.anchoredPosition;
        bool isPressing = Input.GetKey(inputKey);
        float moveSpeed = handleMoveSpeed > 0f ? handleMoveSpeed : 1f;

        // Giữ nguyên logic cũ của bạn:
        // Bấm thì đi về một phía trục X.
        // Buông thì rơi về phía còn lại trục X.
        if (isPressing)
        {
            anchored.x -= level.liftSpeed * moveSpeed * dt;
        }
        else
        {
            anchored.x += level.fallSpeed * moveSpeed * dt;
        }

        float minX = GetHandleMinX();
        float maxX = GetHandleMaxX();

        anchored.x = Mathf.Clamp(anchored.x, minX, maxX);

        // Chỉ sửa X, không đụng Y.
        handle.anchoredPosition = anchored;
    }

    private void EvaluateProgress(LevelConfig level, float dt)
    {
        float handleX = handle.anchoredPosition.x;
        float targetMin = targetCenterX - targetWidthCurrent * 0.5f;
        float targetMax = targetCenterX + targetWidthCurrent * 0.5f;

        bool inSafeZone = handleX >= targetMin && handleX <= targetMax;

        if (inSafeZone)
        {
            holdTimer += dt;

            if (holdTimer >= level.holdTimeToClear)
            {
                AdvanceLevel();
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }

    private void AdvanceLevel()
    {
        if (isAdvancingLevel)
        {
            return;
        }

        isAdvancingLevel = true;
        holdTimer = 0f;
        clearedLevelCount = Mathf.Clamp(clearedLevelCount + 1, 0, GetLevelCount());
        LogDebug($"Level {clearedLevelCount} cleared. NextLevelIndex={currentLevelIndex + 1}");
        IncreaseLevelProgress();

        if (clearedLevelCount >= 2)
        {
            StopLevel2Timeout("Level 2 completed in time.");
        }

        currentLevelIndex++;

        if (currentLevelIndex >= levels.Length)
        {
            StartCoroutine(CompleteGameAfterLevelFeedback());
            return;
        }

        StartCoroutine(StartNextLevelAfterFeedback());
    }

    private void CompleteGame()
    {
        isCompleted = true;
        isRunning = false;
        StopLevel2Timeout("Game completed.");
        LogDebug("All levels cleared.");
        onAllLevelsCleared?.Invoke();

        if (gameObjectToDisableOnComplete != null)
        {
            StartCoroutine(DisableCompleteObjectAfterHealthFill());
        }
    }

    private void FailGame()
    {
        if (!isRunning || isCompleted)
        {
            return;
        }

        isRunning = false;
        StopLevel2Timeout("Game failed.");
        LogDebug("Game failed.");
        onGameFailed?.Invoke();
    }

    private void SetupLevelProgressFill()
    {
        if (levelProgressFillImage == null)
        {
            return;
        }

        levelProgressFillImage.type = Image.Type.Filled;
        currentProgressFill = Mathf.Clamp01(levelProgressFillImage.fillAmount);
        SetLevelProgressFill(currentProgressFill);
    }

    private void SetupLevelsCompleteEffect()
    {
        if (levelsCompleteEffect == null)
        {
            LogDebug("levelsCompleteEffect is not assigned. Level 2 timeout UI is disabled.");
            return;
        }

        levelsCompleteEffect.type = Image.Type.Filled;
        levelsCompleteEffect.fillMethod = Image.FillMethod.Horizontal;
        levelsCompleteEffect.fillOrigin = (int)levelsCompleteEffectOrigin;
        SetLevelsCompleteEffectFill(0f);
    }

    private void IncreaseLevelProgress()
    {
        if (levelProgressFillImage == null)
        {
            return;
        }

        float targetFill = GetLevelCount() <= 0
            ? 0f
            : Mathf.Clamp01((float)clearedLevelCount / GetLevelCount());

        if (progressFillCoroutine != null)
        {
            StopCoroutine(progressFillCoroutine);
        }

        progressFillCoroutine = StartCoroutine(FillLevelProgress(targetFill));
    }

    private IEnumerator FillLevelProgress(float targetFill)
    {
        float startFill = currentProgressFill;
        float elapsed = 0f;

        if (progressFillDuration <= 0f)
        {
            SetLevelProgressFill(targetFill);
            progressFillCoroutine = null;
            yield break;
        }

        while (elapsed < progressFillDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / progressFillDuration);
            SetLevelProgressFill(Mathf.Lerp(startFill, targetFill, t));
            yield return null;
        }

        SetLevelProgressFill(targetFill);
        progressFillCoroutine = null;
    }

    private void SetLevelProgressFill(float fillAmount)
    {
        currentProgressFill = Mathf.Clamp01(fillAmount);

        if (levelProgressFillImage != null)
        {
            levelProgressFillImage.fillAmount = currentProgressFill;
        }
    }

    private void StartLevel2Timeout()
    {
        if (levelsCompleteEffect == null || level2CompleteTimeout <= 0f)
        {
            LogDebug("Level 2 timeout skipped because effect is missing or duration <= 0.");
            return;
        }

        level2TimeoutRunning = true;
        level2TimeoutRemaining = level2CompleteTimeout;
        SetLevelsCompleteEffectFill(1f);
        LogDebug($"Level 2 timeout started. Duration={level2CompleteTimeout:0.##}s");
    }

    private void TickLevelsCompleteEffect(float dt)
    {
        if (!level2TimeoutRunning)
        {
            return;
        }

        level2TimeoutRemaining -= dt;
        float fill = level2CompleteTimeout <= 0f
            ? 0f
            : Mathf.Clamp01(level2TimeoutRemaining / level2CompleteTimeout);
        SetLevelsCompleteEffectFill(fill);

        if (level2TimeoutRemaining > 0f)
        {
            return;
        }

        level2TimeoutRunning = false;
        SetLevelsCompleteEffectFill(0f);
        Debug.LogWarning("[MiniGameslider] Level 2 timeout expired. Player failed before completing level 2.", this);
        FailGame();
    }

    private void StopLevel2Timeout(string reason)
    {
        if (!level2TimeoutRunning && levelsCompleteEffect == null)
        {
            return;
        }

        level2TimeoutRunning = false;
        level2TimeoutRemaining = 0f;
        SetLevelsCompleteEffectFill(0f);
        LogDebug($"Level 2 timeout stopped. Reason={reason}");
    }

    private void SetLevelsCompleteEffectFill(float fillAmount)
    {
        if (levelsCompleteEffect != null)
        {
            levelsCompleteEffect.fillAmount = Mathf.Clamp01(fillAmount);
        }
    }

    private IEnumerator DisableCompleteObjectAfterHealthFill()
    {
        float waitTime = levelProgressFillImage != null ? Mathf.Max(0f, progressFillDuration) : 0f;

        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }

        if (gameObjectToDisableOnComplete != null)
        {
            gameObjectToDisableOnComplete.SetActive(false);
        }
    }

    private IEnumerator StartNextLevelAfterFeedback()
    {
        float waitTime = Mathf.Max(levelCompletePause, progressFillDuration);

        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }

        ApplyTargetWidthForLevel(GetCurrentLevel());

        Vector2 anchored = handle.anchoredPosition;
        anchored.x = Mathf.Clamp(anchored.x, GetHandleMinX(), GetHandleMaxX());
        handle.anchoredPosition = anchored;

        if (clearedLevelCount == 1)
        {
            StartLevel2Timeout();
        }

        LogDebug($"Level {currentLevelIndex + 1} started.");
        isAdvancingLevel = false;
    }

    private IEnumerator CompleteGameAfterLevelFeedback()
    {
        float waitTime = Mathf.Max(levelCompletePause, progressFillDuration);

        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }

        isAdvancingLevel = false;
        CompleteGame();
    }

    private int GetLevelCount()
    {
        return levels == null ? 0 : levels.Length;
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[MiniGameslider] {message}", this);
        }
    }

    private void CacheOriginalHandleTransform()
    {
        if (handle == null)
        {
            return;
        }

        originalHandleSize = handle.sizeDelta;
        originalHandleScale = handle.localScale;
        originalHandleAnchoredPosition = handle.anchoredPosition;

        hasOriginalHandleTransform = true;
    }

    private void RestoreHandleTransform()
    {
        if (handle == null)
        {
            return;
        }

        if (!hasOriginalHandleTransform)
        {
            CacheOriginalHandleTransform();
        }

        handle.sizeDelta = originalHandleSize;
        handle.localScale = originalHandleScale;
    }

    private void SyncTargetWidth()
    {
        if (targetBar == null)
        {
            return;
        }

        Vector2 size = targetBar.sizeDelta;
        size.y = targetWidthCurrent;
        targetBar.sizeDelta = size;
    }

    private float GetHandleMinX()
    {
        return playRect.xMin;
    }

    private float GetHandleMaxX()
    {
        return playRect.xMax;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        DrawRectTransformGizmo(playArea, playAreaGizmoColor);
        DrawRectTransformGizmo(targetBar, targetGizmoColor);
        DrawRectTransformGizmo(handle, handleGizmoColor);
    }

    private void DrawRectTransformGizmo(RectTransform rectTransform, Color color)
    {
        if (rectTransform == null)
        {
            return;
        }

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Color previousColor = Gizmos.color;
        Gizmos.color = color;

        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[2]);
        Gizmos.DrawLine(corners[2], corners[3]);
        Gizmos.DrawLine(corners[3], corners[0]);

        Vector3 center = (corners[0] + corners[2]) * 0.5f;
        float markerSize = Vector3.Distance(corners[0], corners[2]) * 0.04f;
        Gizmos.DrawLine(center + Vector3.left * markerSize, center + Vector3.right * markerSize);
        Gizmos.DrawLine(center + Vector3.down * markerSize, center + Vector3.up * markerSize);

        Gizmos.color = previousColor;
    }

    private void OnValidate()
    {
        if (levels == null || levels.Length != 3)
        {
            InitializeDefaultLevels();
        }
    }

    private void OnDisable()
    {
        if (!isCompleted && isRunning)
        {
            FailGame();
        }
    }
}

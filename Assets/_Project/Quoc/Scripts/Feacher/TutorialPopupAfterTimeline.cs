using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TutorialPopupAfterTimeline : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayableDirector timelineDirector;
    [SerializeField] private GameObject tutorialPopup;

    [Header("Input & Close")]
    [SerializeField] private KeyCode closeKey = KeyCode.Space;
    [SerializeField] private KeyCode alternateCloseKey = KeyCode.E;
    [SerializeField] private Button closeButton;

    [Header("Settings")]
    [Tooltip("Khóa Player khi popup đang mở (Mặc định: false để chơi bình thường được)")]
    [SerializeField] private bool lockPlayerWhileOpen = false;
    [Tooltip("Tự động đóng popup khi minigame hoàn thành (PlinkoBreakTarget)")]
    [SerializeField] private bool autoCloseOnMinigameComplete = true;
    [SerializeField] private bool animatePopup = true;
    [Min(0.01f)]
    [SerializeField] private float animationDuration = 0.25f;

    private bool isWaitingForInput = false;
    private RectTransform popupRect;
    private CanvasGroup popupCanvasGroup;
    private Coroutine animationCoroutine;
    private Player cachedPlayer;
    private TutorialPopupUI cachedPopupUI;
    private bool initialized;
    private bool dismissed;
    private bool hasShownOnce;
    private bool timelineHasPlayed;
    private bool timelineIsRunning;

    private void Awake()
    {
        InitializePopup();
        if (tutorialPopup != null)
        {
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;
            tutorialPopup.SetActive(false);
        }
    }

    private void OnEnable()
    {
        PlinkoBreakTarget.OnPlinkoCompleted -= OnPlinkoCompletedHandler;
        PlinkoBreakTarget.OnPlinkoCompleted += OnPlinkoCompletedHandler;

        InitializePopup();

        if (timelineDirector != null)
        {
            timelineDirector.played += OnTimelinePlayed;
            timelineDirector.stopped += OnTimelineStopped;
            timelineIsRunning = timelineDirector.state == PlayState.Playing;
            if (timelineIsRunning)
            {
                timelineHasPlayed = true;
            }
        }
    }

    private void OnDisable()
    {
        PlinkoBreakTarget.OnPlinkoCompleted -= OnPlinkoCompletedHandler;

        if (timelineDirector != null)
        {
            timelineDirector.played -= OnTimelinePlayed;
            timelineDirector.stopped -= OnTimelineStopped;
        }

        timelineIsRunning = false;
        UnlockPlayer();
    }

    private void OnPlinkoCompletedHandler()
    {
        if (autoCloseOnMinigameComplete)
        {
            HideTutorialPopup();
        }
    }

    private void Update()
    {
        if (autoCloseOnMinigameComplete && PlinkoBreakTarget.GlobalHasBroken)
        {
            if (isWaitingForInput || (tutorialPopup != null && tutorialPopup.activeInHierarchy))
            {
                HideTutorialPopup();
                return;
            }
        }

        if (!isWaitingForInput) return;

        if (Input.GetKeyDown(closeKey)
            || Input.GetKeyDown(alternateCloseKey)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.Escape))
        {
            HideTutorialPopup();
        }
    }

    private void OnTimelineStopped(PlayableDirector director)
    {
        if (!timelineHasPlayed)
        {
            return;
        }

        timelineIsRunning = false;
        if (hasShownOnce || dismissed)
        {
            return;
        }

        if (HasTimelineReachedEnd())
        {
            ShowTutorialPopup();
        }
    }

    private void LateUpdate()
    {
        if (!timelineIsRunning || !timelineHasPlayed)
        {
            return;
        }

        if (HasTimelineReachedEnd())
        {
            timelineIsRunning = false;
            ShowTutorialPopup();
        }
    }

    private void OnTimelinePlayed(PlayableDirector director)
    {
        timelineHasPlayed = true;
        timelineIsRunning = true;
    }

    private bool HasTimelineReachedEnd()
    {
        if (timelineDirector == null || !timelineHasPlayed)
        {
            return false;
        }

        double duration = timelineDirector.duration;
        if (duration <= 0.05d)
        {
            return false;
        }

        return timelineDirector.time >= duration - 0.05d;
    }

    private void InitializePopup()
    {
        if (initialized && tutorialPopup != null)
        {
            return;
        }

        if (timelineDirector == null)
        {
            timelineDirector = GetComponent<PlayableDirector>();
        }

        if (tutorialPopup == null)
        {
            return;
        }

        if (!tutorialPopup.scene.IsValid())
        {
            Canvas targetCanvas = FindAnyObjectByType<Canvas>();
            tutorialPopup = Instantiate(tutorialPopup, targetCanvas != null ? targetCanvas.transform : null);
            tutorialPopup.SetActive(false);
        }

        popupRect = tutorialPopup.GetComponent<RectTransform>();
        popupCanvasGroup = tutorialPopup.GetComponent<CanvasGroup>();
        if (popupCanvasGroup == null)
        {
            popupCanvasGroup = tutorialPopup.AddComponent<CanvasGroup>();
        }

        if (popupCanvasGroup != null)
        {
            popupCanvasGroup.alpha = 0f;
        }

        if (tutorialPopup.TryGetComponent(out cachedPopupUI))
        {
            if (closeButton == null && cachedPopupUI.CloseButton != null)
            {
                closeButton = cachedPopupUI.CloseButton;
            }

            if (cachedPopupUI.ConfirmButton != null)
            {
                cachedPopupUI.ConfirmButton.onClick.RemoveListener(ClosePopupByButton);
                cachedPopupUI.ConfirmButton.onClick.AddListener(ClosePopupByButton);
            }
        }

        if (closeButton == null)
        {
            closeButton = tutorialPopup.GetComponentInChildren<Button>(true);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopupByButton);
            closeButton.onClick.AddListener(ClosePopupByButton);
        }

        tutorialPopup.SetActive(false);
        initialized = true;
    }

    public void ShowTutorialPopup()
    {
        InitializePopup();

        if (tutorialPopup == null || hasShownOnce || dismissed)
        {
            return;
        }

        if (autoCloseOnMinigameComplete && PlinkoBreakTarget.GlobalHasBroken)
        {
            HideTutorialPopup();
            return;
        }

        if (lockPlayerWhileOpen)
        {
            LockPlayer();
        }
        else
        {
            UnlockPlayer();
        }

        isWaitingForInput = true;
        hasShownOnce = true;

        tutorialPopup.transform.SetAsLastSibling();
        tutorialPopup.SetActive(true);

        if (popupRect != null)
        {
            popupRect.anchoredPosition = Vector2.zero;
        }

        if (animatePopup)
        {
            PlayPopupAnimation(true);
        }
        else
        {
            if (popupRect != null) popupRect.localScale = Vector3.one;
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 1f;
        }
    }

    public void HideTutorialPopup()
    {
        if (tutorialPopup == null)
        {
            return;
        }

        isWaitingForInput = false;
        dismissed = true;

        if (animatePopup)
        {
            PlayPopupAnimation(false);
        }
        else
        {
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;
            tutorialPopup.SetActive(false);
            UnlockPlayer();
        }
    }

    public void ClosePopupByButton()
    {
        HideTutorialPopup();
    }

    private void PlayPopupAnimation(bool show)
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine = StartCoroutine(AnimatePopupRoutine(show));
    }

    private IEnumerator AnimatePopupRoutine(bool show)
    {
        float duration = Mathf.Max(0.01f, animationDuration);
        float elapsed = 0f;

        Vector3 startScale = show ? Vector3.one * 0.7f : Vector3.one;
        Vector3 targetScale = show ? Vector3.one : Vector3.one * 0.8f;
        float startAlpha = show ? 0f : 1f;
        float targetAlpha = show ? 1f : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (popupRect != null)
            {
                if (show)
                {
                    float scaleT = 1f + 0.15f * Mathf.Sin(t * Mathf.PI * 1.5f) * (1f - t);
                    popupRect.localScale = Vector3.LerpUnclamped(startScale, targetScale, t) * (t < 1f ? scaleT : 1f);
                }
                else
                {
                    popupRect.localScale = Vector3.Lerp(startScale, targetScale, t);
                }
            }

            if (popupCanvasGroup != null)
            {
                popupCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            }

            yield return null;
        }

        if (show)
        {
            if (popupRect != null) popupRect.localScale = Vector3.one;
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 1f;
        }
        else
        {
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;
            tutorialPopup.SetActive(false);
            UnlockPlayer();
        }

        animationCoroutine = null;
    }

    private void LockPlayer()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = FindAnyObjectByType<Player>();
        }

        if (cachedPlayer != null)
        {
            cachedPlayer.SetControlLocked(true);
        }
    }

    private void UnlockPlayer()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = FindAnyObjectByType<Player>();
        }

        if (cachedPlayer != null)
        {
            cachedPlayer.SetControlLocked(false);
        }
    }
}

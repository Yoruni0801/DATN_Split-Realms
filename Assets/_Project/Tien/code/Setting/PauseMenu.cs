using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.EventSystems;
using DG.Tweening;
using Tien.AdvancedLoading.Core;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private CanvasGroup pauseMenuCanvasGroup;
    [SerializeField] private RectTransform pauseMenuRectTransform;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openPauseSound;
    [SerializeField] private AudioClip closePauseSound;
    [SerializeField] private AudioClip buttonClickSound;

    [Header("Animation Settings (DOTween)")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    private bool isPaused = false;
    private Tween scaleTween;
    private Tween fadeTween;

    private float lastToggleTime = 0f;
    private readonly List<PlayableDirector> pausedDirectors = new List<PlayableDirector>();

    private void Start()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
            if (pauseMenuCanvasGroup != null) pauseMenuCanvasGroup.alpha = 0f;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        // Tránh tình trạng hàm bị gọi 2 lần liên tục trong chớp mắt (do click đúp hoặc lặp event)
        if (Time.unscaledTime - lastToggleTime < 0.2f) return;
        lastToggleTime = Time.unscaledTime;

        isPaused = !isPaused;

        if (isPaused)
        {
            OpenPauseMenu();
        }
        else
        {
            ClosePauseMenu();
        }
    }

    private void OpenPauseMenu()
    {
        PlaySound(openPauseSound);
        
        if (pauseMenuPanel != null) 
            pauseMenuPanel.SetActive(true);
            
        Time.timeScale = 0f;

        // Chỉ lưu và Pause đúng những Timeline đang thực sự Play
        pausedDirectors.Clear();
        PlayableDirector[] allDirectors = FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None);
        foreach (var dir in allDirectors)
        {
            try
            {
                if (dir != null && dir.isActiveAndEnabled && dir.state == PlayState.Playing)
                {
                    dir.Pause();
                    pausedDirectors.Add(dir);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("Không thể Pause Timeline: " + e.Message); }
        }

        // Xóa tween cũ để không bị lỗi chồng chéo nếu bấm liên tục
        fadeTween?.Kill();
        scaleTween?.Kill();

        // Xóa lựa chọn của EventSystem để tránh lỗi dính nút (nhấn phím Space/Enter bị kích hoạt lại nút cũ)
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (pauseMenuCanvasGroup != null)
        {
            // Tắt block raycasts trong lúc đang animate để tránh bắt nhầm sự kiện Click (PointerUp)
            pauseMenuCanvasGroup.blocksRaycasts = false; 
            fadeTween = pauseMenuCanvasGroup.DOFade(1f, animationDuration).SetUpdate(true)
                .OnComplete(() => 
                {
                    pauseMenuCanvasGroup.blocksRaycasts = true; // Bật lại khi đã hiện xong
                });
        }
        
        if (pauseMenuRectTransform != null)
        {
            pauseMenuRectTransform.localScale = Vector3.zero;
            scaleTween = pauseMenuRectTransform.DOScale(Vector3.one, animationDuration).SetEase(openEase).SetUpdate(true);
        }
    }

    private void ClosePauseMenu()
    {
        PlaySound(closePauseSound);
        Time.timeScale = 1f;

        // Chỉ Resume đúng những Timeline đã bị Pause bởi Menu Pause
        foreach (var dir in pausedDirectors)
        {
            try
            {
                if (dir != null && dir.isActiveAndEnabled && dir.state == PlayState.Paused)
                {
                    dir.Play();
                }
            }
            catch (System.Exception e) { Debug.LogWarning("Không thể Play Timeline: " + e.Message); }
        }
        pausedDirectors.Clear();

        // Xóa tween cũ
        fadeTween?.Kill();
        scaleTween?.Kill();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (pauseMenuCanvasGroup != null)
        {
            // Tắt tương tác ngay khi bắt đầu đóng menu
            pauseMenuCanvasGroup.blocksRaycasts = false;
            fadeTween = pauseMenuCanvasGroup.DOFade(0f, animationDuration).SetUpdate(true);
        }

        if (pauseMenuRectTransform != null)
        {
            scaleTween = pauseMenuRectTransform.DOScale(Vector3.zero, animationDuration).SetEase(closeEase).SetUpdate(true)
                .OnComplete(() => 
                {
                    if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
                });
        }
        else
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        }
    }

    // Gán hàm này cho nút Pause trên màn hình (HUD)
    public void PauseGame()
    {
        if (!isPaused)
        {
            TogglePause();
        }
    }

    // Gán hàm này cho nút Tiếp Tục (Resume) trong Menu Pause
    public void ResumeGame()
    {
        PlaySound(buttonClickSound);
        if (isPaused) 
        {
            TogglePause();
        }
    }

    public void GoToMainMenu()
    {
        PlaySound(buttonClickSound);
        Time.timeScale = 1f; 
        DOTween.KillAll(); 
        TimelinePlayStateManager.ResetAll();
        SceneRuntimeStateManager.ClearAll();
        PlayerCarryData.ResetAll();
        Tien.Minigame.MinigameCheckpointState.ClearCheckpoint();
        SceneLoader.Load(mainMenuSceneName);
    }

    public void PlayButtonClickSound()
    {
        PlaySound(buttonClickSound);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}

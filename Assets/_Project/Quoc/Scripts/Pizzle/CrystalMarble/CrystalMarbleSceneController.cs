using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller quản lý scene standalone CrystalMarble.unity.
/// Kết nối với CrystalMarblePuzzle để khi thắng/hoàn thành minigame
/// sẽ tự động chuyển cảnh quay về Map5.unity và thiết lập flag JustCompletedMiniGame = true
/// để CrystalMarbleMiniGameTrigger2D ở Map5 tự động kích hoạt cutscene hạ cửa.
/// </summary>
[DisallowMultipleComponent]
public sealed class CrystalMarbleSceneController : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField, Tooltip("Tên scene Map chính cần quay về sau khi hoàn thành minigame")]
    private string targetMapSceneName = "Map5";

    [Header("Puzzle Reference")]
    [SerializeField, Tooltip("Tham chiếu đến CrystalMarblePuzzle. Để trống sẽ tự động tìm trong Scene")]
    private CrystalMarblePuzzle puzzle;

    [Header("Transition Settings")]
    [SerializeField, Tooltip("Tự động chuyển scene khi hoàn thành (nếu false sẽ đợi người chơi nhấn nút Continue trong minigame)")]
    private bool autoReturnOnSolved = true;

    [SerializeField, Tooltip("Thời gian chờ trước khi tự động chuyển scene (chỉ áp dụng nếu autoReturnOnSolved = true)")]
    private float autoReturnDelay = 1.2f;

    [Header("Fade Transition")]
    [SerializeField] private float fadeOutDuration = 1f;

    private bool hasTriggeredReturn;

    private void Awake()
    {
        if (puzzle == null)
        {
            puzzle = FindFirstObjectByType<CrystalMarblePuzzle>();
        }
    }

    private void OnEnable()
    {
        if (puzzle != null)
        {
            puzzle.OnPuzzleSolved += HandlePuzzleSolved;
        }
    }

    private void OnDisable()
    {
        if (puzzle != null)
        {
            puzzle.OnPuzzleSolved -= HandlePuzzleSolved;
        }
    }

    private void Start()
    {
        // Kiểm tra xem Puzzle đã được giải từ trước chưa
        if (puzzle != null && puzzle.IsSolved)
        {
            HandlePuzzleSolved();
        }
    }

    /// <summary>
    /// Được gọi khi người chơi hoàn thành minigame
    /// </summary>
    private void HandlePuzzleSolved()
    {
        if (autoReturnOnSolved)
        {
            StartCoroutine(AutoReturnRoutine());
        }
    }

    private IEnumerator AutoReturnRoutine()
    {
        if (autoReturnDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(autoReturnDelay);
        }

        // Fade sang đen trước khi chuyển scene
        yield return FadeToBlack(fadeOutDuration);

        ReturnToMap5Immediate();
    }

    private IEnumerator FadeToBlack(float duration)
    {
        MapHangTransitionFade2D fade = MapHangTransitionFade2D.GetOrCreate();
        if (fade != null)
        {
            yield return fade.FadeToBlack(duration);
        }
    }

    /// <summary>
    /// Thực hiện chuyển về scene Map5 và bật flag cho cutscene hạ cửa
    /// Hỗ trợ gán trực tiếp vào Button Event (ví dụ OnClick của nút Tiếp tục / ContinueButton)
    /// </summary>
    public void ReturnToMap5()
    {
        if (hasTriggeredReturn)
        {
            return;
        }

        hasTriggeredReturn = true;

        // Nếu đang chạy từ trong scene (không phải từ AutoReturnRoutine), fade rồi mới chuyển scene
        StartCoroutine(FadeReturnRoutine());
    }

    private IEnumerator FadeReturnRoutine()
    {
        yield return FadeToBlack(fadeOutDuration);
        ReturnToMap5Immediate();
    }

    private void ReturnToMap5Immediate()
    {
        CrystalMarbleTravelState.JustCompletedMiniGame = true;
        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(targetMapSceneName))
        {
            targetMapSceneName = "Map5";
        }

        Debug.Log($"[CrystalMarbleSceneController] Hoàn thành minigame CrystalMarble, đang quay lại scene '{targetMapSceneName}'...");

        // Thử load scene bằng Advanced Loading SceneLoader nếu có, ngược lại dùng SceneManager tiêu chuẩn
        try
        {
            Tien.AdvancedLoading.Core.SceneLoader.Load(targetMapSceneName);
        }
        catch
        {
            SceneManager.LoadScene(targetMapSceneName, LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// Thoát về Map5 mà không tính là thắng minigame (không kích hoạt hạ cửa)
    /// </summary>
    public void QuitWithoutWinning()
    {
        CrystalMarbleTravelState.JustCompletedMiniGame = false;
        Time.timeScale = 1f;

        try
        {
            Tien.AdvancedLoading.Core.SceneLoader.Load(targetMapSceneName);
        }
        catch
        {
            SceneManager.LoadScene(targetMapSceneName, LoadSceneMode.Single);
        }
    }
}

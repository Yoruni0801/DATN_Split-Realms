using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Tien.AdvancedLoading.Core
{
    /// <summary>
    /// Hệ thống chuyển Scene tối ưu cho game 2D:
    /// - Dùng Coroutine (yield return null) đảm bảo fade mượt đúng 1 lần/frame.
    /// - Nạp Scene ngầm song song với hiệu ứng Fade (Zero-Waste Loading).
    /// - Tối ưu băng thông tải GPU (asyncUploadTimeSlice & asyncUploadBufferSize).
    /// - Tái sử dụng UI Canvas, không tạo rác GC mỗi lần load.
    /// </summary>
    public static class SceneLoader
    {
        private const float DefaultFadeOutDuration = 0.18f;
        private const float DefaultFadeInDuration = 0.18f;
        private const int HighUploadTimeSlice = 16;
        private const int HighUploadBufferSize = 64;
        private const int CanvasSortingOrder = 32767;

        public static string TargetScene { get; private set; }
        public static string PreviousScene { get; private set; }
        public static bool IsLoading { get; private set; }

        private static SceneLoaderRunner runnerInstance;
        private static string preloadedSceneName;
        private static AsyncOperation preloadedAsyncOp;

        public static void Preload(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (IsLoading) return;
            if (preloadedSceneName == sceneName && preloadedAsyncOp != null && !preloadedAsyncOp.isDone) return;

            try
            {
                // Khi preload ngầm (ví dụ lúc đang chiếu cutscene/gameplay),
                // đặt độ ưu tiên thấp để không chiếm CPU/GPU main thread gây drop frame
                Application.backgroundLoadingPriority = ThreadPriority.Low;
                QualitySettings.asyncUploadTimeSlice = 2;

                preloadedSceneName = sceneName;
                preloadedAsyncOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (preloadedAsyncOp != null)
                {
                    preloadedAsyncOp.allowSceneActivation = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneLoader] Không thể preload Scene '{sceneName}': {ex.Message}");
                preloadedAsyncOp = null;
                preloadedSceneName = null;
            }
        }

        public static void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[SceneLoader] Tên Scene cần chuyển đến đang bị trống!");
                return;
            }

            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Đang trong quá trình nạp Scene '{TargetScene}', bỏ qua yêu cầu nạp '{sceneName}'.");
                return;
            }

            Time.timeScale = 1f;
            TargetScene = sceneName;
            PreviousScene = SceneManager.GetActiveScene().name;

            EnsureRunner();
            runnerInstance.StartTransition(sceneName);
        }

        private static void EnsureRunner()
        {
            if (runnerInstance == null)
            {
                GameObject runnerObj = new GameObject("[FastSceneTransitionManager]");
                UnityEngine.Object.DontDestroyOnLoad(runnerObj);
                runnerInstance = runnerObj.AddComponent<SceneLoaderRunner>();
            }
        }

        [DisallowMultipleComponent]
        private sealed class SceneLoaderRunner : MonoBehaviour
        {
            private Canvas fadeCanvas;
            private CanvasGroup canvasGroup;
            private Image fadeImage;
            private Coroutine activeTransition;

            private void Awake()
            {
                CreateFadeUI();
            }

            private void OnDestroy()
            {
                CancelActiveTransition();
            }

            private void CancelActiveTransition()
            {
                if (activeTransition != null)
                {
                    StopCoroutine(activeTransition);
                    activeTransition = null;
                }
            }

            private void CreateFadeUI()
            {
                if (fadeCanvas != null) return;

                GameObject canvasObj = new GameObject("TransitionCanvas");
                canvasObj.transform.SetParent(transform, false);

                fadeCanvas = canvasObj.AddComponent<Canvas>();
                fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                fadeCanvas.sortingOrder = CanvasSortingOrder;

                canvasObj.AddComponent<CanvasScaler>();

                canvasGroup = canvasObj.AddComponent<CanvasGroup>();
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;

                GameObject imgObj = new GameObject("BlackOverlay");
                imgObj.transform.SetParent(canvasObj.transform, false);

                fadeImage = imgObj.AddComponent<Image>();
                fadeImage.color = Color.black;
                fadeImage.raycastTarget = false;

                RectTransform rt = imgObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
            }

            public void StartTransition(string sceneName)
            {
                CancelActiveTransition();
                activeTransition = StartCoroutine(TransitionRoutine(sceneName));
            }

            private IEnumerator TransitionRoutine(string sceneName)
            {
                IsLoading = true;
                CreateFadeUI();

                int prevTimeSlice = QualitySettings.asyncUploadTimeSlice;
                int prevBufferSize = QualitySettings.asyncUploadBufferSize;
                ThreadPriority prevPriority = Application.backgroundLoadingPriority;

                try
                {
                    if (canvasGroup != null)
                    {
                        canvasGroup.blocksRaycasts = true;
                    }

                    // 1. Fade Out TRƯỚC — màn hình đen hoàn toàn, không tranh CPU với scene loading
                    yield return FadeCanvasRoutine(0f, 1f, DefaultFadeOutDuration);

                    // 2. Sau khi đen rồi → đẩy tốc load lên max và bắt đầu load scene
                    QualitySettings.asyncUploadTimeSlice = HighUploadTimeSlice;
                    QualitySettings.asyncUploadBufferSize = HighUploadBufferSize;
                    Application.backgroundLoadingPriority = ThreadPriority.High;

                    AsyncOperation asyncOp = null;
                    if (preloadedSceneName == sceneName && preloadedAsyncOp != null && !preloadedAsyncOp.isDone)
                    {
                        asyncOp = preloadedAsyncOp;
                        preloadedAsyncOp = null;
                        preloadedSceneName = null;
                    }
                    else
                    {
                        try
                        {
                            asyncOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[SceneLoader] Lỗi khi khởi tạo nạp Scene '{sceneName}': {ex.Message}");
                        }
                    }

                    // 3. Kích hoạt ngay (màn hình đã đen, không cần giữ allowSceneActivation = false)
                    if (asyncOp != null)
                    {
                        asyncOp.allowSceneActivation = true;

                        while (!asyncOp.isDone)
                        {
                            yield return null;
                        }
                    }

                    // Đợi 1 frame cho Scene mới ổn định rendering & camera
                    yield return null;

                    // Đảm bảo AudioListener toàn cục luôn sẵn sàng
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.EnsureAudioListener();
                    }

                    // 4. Fade In hiển thị Scene mới
                    yield return FadeCanvasRoutine(1f, 0f, DefaultFadeInDuration);
                }
                finally
                {
                    QualitySettings.asyncUploadTimeSlice = prevTimeSlice;
                    QualitySettings.asyncUploadBufferSize = prevBufferSize;
                    Application.backgroundLoadingPriority = prevPriority;

                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = 0f;
                        canvasGroup.blocksRaycasts = false;
                    }

                    IsLoading = false;
                    activeTransition = null;
                }
            }

            private IEnumerator FadeCanvasRoutine(float startAlpha, float targetAlpha, float duration)
            {
                if (canvasGroup == null) yield break;

                if (duration <= 0.01f)
                {
                    canvasGroup.alpha = targetAlpha;
                    yield break;
                }

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    if (canvasGroup == null) yield break;
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                    yield return null;
                }

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = targetAlpha;
                }
            }
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Tien.AdvancedLoading.Data;
using Tien.AdvancedLoading.UI;

namespace Tien.AdvancedLoading.Core
{
    public class LoadingController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LoadingDatabase database;
        [SerializeField] private FadeController fadeController;
        [SerializeField] private LoadingUI loadingUI;
        
        [Header("Settings")]
        [Tooltip("Thời gian mờ dần (giây). 0.25s - 0.3s cho trải nghiệm phản hồi nhanh, êm ái.")]
        [SerializeField] private float fadeDuration = 0.25f;

        private void Awake()
        {
            if (loadingUI != null) loadingUI.Hide();
            if (fadeController != null) fadeController.Initialize();
        }

        private void Start()
        {
            StartCoroutine(LoadingStateMachine());
        }

        private IEnumerator LoadingStateMachine()
        {
            string target = SceneLoader.TargetScene;
            string prev = SceneLoader.PreviousScene;

            if (string.IsNullOrEmpty(target))
            {
                Debug.LogError("[LoadingController] Target scene name is null or empty!");
                if (fadeController != null) yield return fadeController.FadeToClear(fadeDuration);
                SceneManager.UnloadSceneAsync("LoadingScene");
                yield break;
            }

            // 1. TĂNG TỐC ĐỘ STREAM ASSETS LÊN TỐI ĐA (Dồn CPU/RAM tải Scene nhanh gấp 3-5 lần)
            ThreadPriority originalPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.High;

            // 2. KHỞI CHẠY NẠP SCENE MỚI SONG SONG NGAY TỪ ĐẦU (Không đợi Fade xong mới nạp)
            AsyncOperation asyncLoad = null;
            try
            {
                asyncLoad = SceneManager.LoadSceneAsync(target, LoadSceneMode.Additive);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LoadingController] Cannot load scene '{target}': {e.Message}");
            }

            if (asyncLoad == null)
            {
                Application.backgroundLoadingPriority = originalPriority;
                if (fadeController != null) yield return fadeController.FadeToClear(fadeDuration);
                SceneManager.UnloadSceneAsync("LoadingScene");
                yield break;
            }

            asyncLoad.allowSceneActivation = true;

            // 3. Chạy hiệu ứng Fade Out (Màn hình mờ đen êm dịu 0.25s trong khi Scene mới đang tải song song)
            if (fadeController != null)
            {
                yield return fadeController.FadeToBlack(fadeDuration);
            }

            // 4. Chờ Scene mới hoàn tất tải (nếu chưa xong)
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            // Kích hoạt Scene mới làm Active Scene
            Scene targetScene = SceneManager.GetSceneByName(target);
            if (targetScene.IsValid() && targetScene.isLoaded)
            {
                SceneManager.SetActiveScene(targetScene);
            }

            // 5. Hủy Scene cũ sau khi Scene mới đã sẵn sàng
            if (!string.IsNullOrEmpty(prev) && prev != "LoadingScene" && prev != target)
            {
                Scene prevScene = SceneManager.GetSceneByName(prev);
                if (prevScene.IsValid() && prevScene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(prevScene);
                }
            }

            // Khôi phục lại mức ưu tiên background loading
            Application.backgroundLoadingPriority = originalPriority;

            // Đợi 1 frame cho Engine khởi tạo render
            yield return null;

            // 6. Fade In mở màn đen ra Scene mới cực kỳ mượt mà
            if (fadeController != null)
            {
                yield return fadeController.FadeToClear(fadeDuration);
            }

            // 7. Tự hủy LoadingScene
            SceneManager.UnloadSceneAsync("LoadingScene");
        }
    }
}

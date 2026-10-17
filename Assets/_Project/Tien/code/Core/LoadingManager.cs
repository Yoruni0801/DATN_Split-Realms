using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tien.Core
{
    public class LoadingManager : MonoBehaviour
    {
        public static LoadingManager Instance { get; private set; }

        [Header("Database")]
        [SerializeField] private MapDatabase mapDatabase;

        [Header("UI References")]
        [SerializeField] private CanvasGroup loadingCanvasGroup; 
        [SerializeField] private GameObject loadingScreenPanel;
        [SerializeField] private Image loadingBackgroundImage;
        [SerializeField] private Slider loadingBarSlider;
        [SerializeField] private Image loadingBarFill;

        [Header("Settings")]
        [SerializeField] private float fadeDuration = 0.3f;
        [SerializeField] private float minimumLoadingTime = 0.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            
            if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
            if (loadingCanvasGroup != null) 
            {
                loadingCanvasGroup.alpha = 0f;
                loadingCanvasGroup.blocksRaycasts = false;
            }
        }

        public void LoadScene(string sceneName)
        {
            StartCoroutine(LoadSceneSimple(sceneName));
        }

        private IEnumerator LoadSceneSimple(string sceneName)
        {
            // 1. Setup Data
            MapConfig config = mapDatabase != null ? mapDatabase.GetConfigForScene(sceneName) : null;
            if (config != null)
            {
                if (loadingBackgroundImage != null) loadingBackgroundImage.sprite = config.loadingBackground;
                if (loadingBarFill != null) loadingBarFill.color = config.loadingBarColor;
            }

            // 2. HIỆN LOADING SCREEN NGAY LẬP TỨC (KHÔNG FADE RƯỜM RÀ)
            if (loadingBarSlider != null) loadingBarSlider.value = 0f;
            if (loadingScreenPanel != null) loadingScreenPanel.SetActive(true);
            
            if (loadingCanvasGroup != null)
            {
                loadingCanvasGroup.alpha = 1f;
                loadingCanvasGroup.blocksRaycasts = true;
            }

            // Đợi 1 frame để Canvas render cập nhật hình ảnh Loading
            yield return null; 

            // 3. LOAD NGẦM
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.allowSceneActivation = false;

            float targetProgress = 0f;
            float currentVisualProgress = 0f;

            while (!asyncLoad.isDone)
            {
                targetProgress = asyncLoad.progress / 0.9f;
                currentVisualProgress = Mathf.MoveTowards(currentVisualProgress, targetProgress, (1f / minimumLoadingTime) * Time.unscaledDeltaTime);
                
                if (loadingBarSlider != null) loadingBarSlider.value = currentVisualProgress;

                if (asyncLoad.progress >= 0.9f && currentVisualProgress >= 1f)
                {
                    break; 
                }
                yield return null;
            }

            // 4. KÍCH HOẠT SCENE MỚI
            asyncLoad.allowSceneActivation = true;
            yield return null; // Chờ Unity chuyển scene

            // 5. TỪ TỪ MỜ MÀN HÌNH LOADING ĐỂ LỘ RA GAME (CHỈ FADE ĐÚNG 1 LẦN NÀY)
            if (loadingCanvasGroup != null)
            {
                float timer = 0f;
                while (timer < fadeDuration)
                {
                    timer += Time.unscaledDeltaTime;
                    loadingCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
                    yield return null;
                }
                loadingCanvasGroup.alpha = 0f;
                loadingCanvasGroup.blocksRaycasts = false;
            }

            // 6. Tắt hẳn Loading Screen
            if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
        }
    }
}

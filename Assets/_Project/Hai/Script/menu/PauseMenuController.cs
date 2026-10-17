using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Tien.AdvancedLoading.Core;

namespace Hai.Menu
{
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Main Panels & Elements")]
        [Tooltip("Kéo GameObject/Prefab UI Setting vào đây")]
        [SerializeField] private GameObject settingUI;

        [Tooltip("Kéo GameObject Frame của Pause Panel (cần ẩn đi khi mở Setting) vào đây")]
        [SerializeField] private GameObject frame;

        [Tooltip("Panel Pause chính (nếu script không gắn trực tiếp trên Panel Pause)")]
        [SerializeField] private GameObject pausePanel;

        [Header("Buttons (Tùy chọn - Tự động gán nếu để trống)")]
        [Tooltip("Nút Nút Icon Pause ngoài màn hình (như Canvas/UI/Pause/box/icon)")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button closeSettingButton;

        [Header("Optional Timeline / Cutscene")]
        [Tooltip("Kéo PlayableDirector (Timeline Cutscene) vào nếu muốn tạm dừng Cutscene khi Pause")]
        [SerializeField] private UnityEngine.Playables.PlayableDirector cutsceneDirector;

        [Header("Quit Confirmation Panel")]
        [Tooltip("Kéo Bảng Popup Xác nhận Thoát vào đây")]
        [SerializeField] private GameObject quitConfirmUI;

        [Tooltip("Nút Đồng ý Thoát (Yes) - Xóa tiến độ và về MainMenu")]
        [SerializeField] private Button quitYesButton;

        [Tooltip("Nút Hủy Thoát (No) - Tắt Popup xác nhận")]
        [SerializeField] private Button quitNoButton;

        [Header("Persistence (Dùng chung cho tất cả Scene)")]
        [Tooltip("Bật tích chọn này để UI Pause & Setting tự động giữ nguyên và xuất hiện ở TẤT CẢ các Scene mà không cần tạo lại")]
        [SerializeField] private bool dontDestroyOnLoad = false;

        [Header("Settings")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private bool pauseTimeScale = true;

        private static PauseMenuController instance;
        private bool isPaused = false;

        private void Awake()
        {
            if (dontDestroyOnLoad)
            {
                if (instance != null && instance != this)
                {
                    Destroy(gameObject);
                    return;
                }

                instance = this;

                // Nếu script nằm trên Object con trong Canvas -> Đẩy toàn bộ Canvas cha lên Root để DontDestroyOnLoad hoạt động
                Canvas rootCanvas = GetComponentInParent<Canvas>();
                GameObject targetObj = rootCanvas != null ? rootCanvas.gameObject : gameObject;
                targetObj.transform.SetParent(null);
                DontDestroyOnLoad(targetObj);

                SceneManager.sceneLoaded += OnSceneLoaded;
            }

            AutoBindReferences();
            ResetUIOnStart();
        }

        private void OnDestroy()
        {
            if (dontDestroyOnLoad && instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AutoBindReferences();
            ResetUIOnStart();

            // Nếu quay về MainMenu thì tự động ẩn nút Icon Pause ngoài màn hình
            if (pauseButton != null)
            {
                bool isMainMenu = (scene.name == mainMenuSceneName);
                pauseButton.gameObject.SetActive(!isMainMenu);
            }
        }

        private void OnEnable()
        {
            AutoBindReferences();
        }

        private void Start()
        {
            ResetUIOnStart();
        }

        /// <summary>
        /// Reset và ẩn tất cả UI Pause / Setting / QuitConfirm khi scene vừa tải xong
        /// </summary>
        public void ResetUIOnStart()
        {
            isPaused = false;

            if (settingUI != null)
            {
                settingUI.SetActive(false);
            }

            if (quitConfirmUI != null)
            {
                SetPanelVisibility(quitConfirmUI, false);
            }

            if (frame != null)
            {
                frame.SetActive(true);
            }

            if (pausePanel != null)
            {
                SetPanelVisibility(pausePanel, false);
            }

            if (cutsceneDirector != null && cutsceneDirector.state == UnityEngine.Playables.PlayState.Paused)
            {
                cutsceneDirector.Resume();
            }

            if (pauseTimeScale)
            {
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            // Lắng hệ phím ESC để Bật/Tắt Pause hoặc Đóng Popup
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (quitConfirmUI != null && (quitConfirmUI.activeSelf || (quitConfirmUI.GetComponent<CanvasGroup>() != null && quitConfirmUI.GetComponent<CanvasGroup>().alpha > 0)))
                {
                    // Nếu đang mở Popup Xác Nhận Thoát -> Tắt Popup
                    OnQuitConfirmNo();
                }
                else if (settingUI != null && settingUI.activeSelf)
                {
                    // Nếu đang mở Setting -> Đóng Setting và bật lại Frame
                    OnCloseSettingButtonClicked();
                }
                else
                {
                    // Nếu không mở Popup -> Toggle Pause Game
                    TogglePause();
                }
            }
        }

        /// <summary>
        /// Tự động tìm kiếm và liên kết chính xác các panel và nút bấm trong Canvas
        /// </summary>
        private void AutoBindReferences()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
#if UNITY_2023_1_OR_NEWER
                canvas = Object.FindFirstObjectByType<Canvas>();
#else
                canvas = Object.FindObjectOfType<Canvas>();
#endif
            }

            Transform rootTr = canvas != null ? canvas.transform : (transform.root != null ? transform.root : transform);

            // 1. Tìm chính xác Pause Menu Panel (Object 'Pause' chứa các nút Resume, Options, Quit hoặc Frame)
            if (pausePanel == null)
            {
                Transform[] allTrs = rootTr.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allTrs)
                {
                    if (t.name == "Pause" && (t.Find("Resume") != null || t.Find("Options") != null || t.Find("Frame") != null))
                    {
                        pausePanel = t.gameObject;
                        break;
                    }
                }
            }

            // 2. Tìm chính xác UI Setting Panel
            if (settingUI == null)
            {
                Transform sTr = rootTr.Find("Setting");
                if (sTr != null) settingUI = sTr.gameObject;
            }

            // 3. Tìm Frame bên trong Pause Panel
            if (frame == null && pausePanel != null)
            {
                Transform fTr = pausePanel.transform.Find("Frame");
                if (fTr != null) frame = fTr.gameObject;
            }

            // 4. Tìm nút Icon Pause ngoài màn hình (Canvas/UI/Pause/box/icon)
            if (pauseButton == null)
            {
                Transform iconTr = rootTr.Find("UI/Pause/box/icon") 
                                ?? rootTr.Find("Pause/box/icon") 
                                ?? rootTr.Find("box/icon");
                if (iconTr != null)
                {
                    pauseButton = iconTr.GetComponent<Button>();
                }
                else
                {
                    Button[] allBtns = rootTr.GetComponentsInChildren<Button>(true);
                    foreach (Button b in allBtns)
                    {
                        if (b.name == "icon")
                        {
                            pauseButton = b;
                            break;
                        }
                    }
                }
            }

            // 5. Tìm các nút chức năng trong Pause Panel
            if (pausePanel != null)
            {
                if (optionsButton == null)
                {
                    Transform optTr = pausePanel.transform.Find("Options") ?? pausePanel.transform.Find("Option");
                    if (optTr != null) optionsButton = optTr.GetComponent<Button>();
                }

                if (resumeButton == null)
                {
                    Transform resTr = pausePanel.transform.Find("Resume") ?? pausePanel.transform.Find("Continue");
                    if (resTr != null) resumeButton = resTr.GetComponent<Button>();
                }

                if (quitButton == null)
                {
                    Transform quitTr = pausePanel.transform.Find("Quit") ?? pausePanel.transform.Find("Exit");
                    if (quitTr != null) quitButton = quitTr.GetComponent<Button>();
                }
            }

            // 6. Tìm Cutscene PlayableDirector nếu có trong scene
            if (cutsceneDirector == null)
            {
#if UNITY_2023_1_OR_NEWER
                cutsceneDirector = Object.FindFirstObjectByType<UnityEngine.Playables.PlayableDirector>();
#else
                cutsceneDirector = Object.FindObjectOfType<UnityEngine.Playables.PlayableDirector>();
#endif
            }

            // 7. Tìm Quit Confirmation Panel & Buttons
            if (quitConfirmUI == null)
            {
                Transform qTr = rootTr.Find("QuitConfirm") 
                             ?? rootTr.Find("QuitConfirmUI") 
                             ?? rootTr.Find("ConfirmQuit")
                             ?? rootTr.Find("PopupQuit");
                if (qTr != null) quitConfirmUI = qTr.gameObject;
            }

            if (quitConfirmUI != null)
            {
                if (quitYesButton == null)
                {
                    Transform yesTr = quitConfirmUI.transform.Find("Yes") 
                                   ?? quitConfirmUI.transform.Find("Btn_Yes") 
                                   ?? quitConfirmUI.transform.Find("Confirm");
                    if (yesTr != null) quitYesButton = yesTr.GetComponent<Button>();
                }

                if (quitNoButton == null)
                {
                    Transform noTr = quitConfirmUI.transform.Find("No") 
                                  ?? quitConfirmUI.transform.Find("Btn_No") 
                                  ?? quitConfirmUI.transform.Find("Cancel");
                    if (noTr != null) quitNoButton = noTr.GetComponent<Button>();
                }
            }

            // 8. Đăng ký sự kiện Click cho tất cả các nút
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(OnPauseButtonClicked);
                pauseButton.onClick.AddListener(OnPauseButtonClicked);
            }

            if (optionsButton != null)
            {
                optionsButton.onClick.RemoveListener(OnOptionButtonClicked);
                optionsButton.onClick.AddListener(OnOptionButtonClicked);
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(ResumeGame);
                resumeButton.onClick.AddListener(ResumeGame);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuitButtonClicked);
                quitButton.onClick.AddListener(OnQuitButtonClicked);
            }

            if (quitYesButton != null)
            {
                quitYesButton.onClick.RemoveListener(OnQuitConfirmYes);
                quitYesButton.onClick.AddListener(OnQuitConfirmYes);
            }

            if (quitNoButton != null)
            {
                quitNoButton.onClick.RemoveListener(OnQuitConfirmNo);
                quitNoButton.onClick.AddListener(OnQuitConfirmNo);
            }

            BindSettingCloseButtons();
        }

        /// <summary>
        /// Xử lý khi nhấn vào nút Icon Pause ở UI góc màn hình -> Mở Pause Panel ngay lập tức
        /// </summary>
        public void OnPauseButtonClicked()
        {
            if (!isPaused)
            {
                PauseGame();
            }
            else
            {
                ResumeGame();
            }
        }

        /// <summary>
        /// Tìm và đăng ký sự kiện cho tất cả nút đóng/back bên trong panel Setting
        /// </summary>
        private void BindSettingCloseButtons()
        {
            if (settingUI == null) return;

            if (closeSettingButton != null)
            {
                closeSettingButton.onClick.RemoveListener(OnCloseSettingButtonClicked);
                closeSettingButton.onClick.AddListener(OnCloseSettingButtonClicked);
            }

            Button[] settingButtons = settingUI.GetComponentsInChildren<Button>(true);
            foreach (Button b in settingButtons)
            {
                if (b != null)
                {
                    string bName = b.name.ToLower();
                    if (bName.Contains("back") || bName.Contains("close") || bName.Contains("exit") || bName == "x" || bName.Contains("btn_close"))
                    {
                        b.onClick.RemoveListener(OnCloseSettingButtonClicked);
                        b.onClick.AddListener(OnCloseSettingButtonClicked);
                    }
                }
            }
        }

        /// <summary>
        /// Xử lý khi nhấn nút Option: Hiện UI Setting và Ẩn Frame
        /// </summary>
        public void OnOptionButtonClicked()
        {
            // 1. Hiện UI Setting
            if (settingUI != null)
            {
                settingUI.SetActive(true);
                BindSettingCloseButtons(); // Cập nhật lại listener nếu settingUI vừa được bật
            }
            else
            {
                Debug.LogWarning("[PauseMenuController] Chưa kéo thả 'settingUI' vào Inspector!");
            }

            // 2. Ẩn Frame
            if (frame != null)
            {
                frame.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[PauseMenuController] Chưa kéo thả 'frame' vào Inspector!");
            }

            // 3. Khởi tạo slider âm thanh nếu có AudioSettingsManager trong scene
            InitAudioSettings();
        }

        /// <summary>
        /// Xử lý khi đóng Setting: Ẩn UI Setting và Hiện lại Frame
        /// </summary>
        public void OnCloseSettingButtonClicked()
        {
            // 1. Ẩn UI Setting
            if (settingUI != null)
            {
                settingUI.SetActive(false);
            }

            // 2. Hiện lại Frame
            if (frame != null)
            {
                frame.SetActive(true);
            }
        }

        /// <summary>
        /// Bật/Tắt Pause Game
        /// </summary>
        public void TogglePause()
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        /// <summary>
        /// Dừng Game (Pause)
        /// </summary>
        public void PauseGame()
        {
            isPaused = true;
            if (pausePanel != null)
            {
                SetPanelVisibility(pausePanel, true);
            }

            if (settingUI != null) settingUI.SetActive(false);
            if (frame != null) frame.SetActive(true);

            if (cutsceneDirector != null && cutsceneDirector.state == UnityEngine.Playables.PlayState.Playing)
            {
                cutsceneDirector.Pause();
            }

            if (pauseTimeScale)
            {
                Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// Ẩn/Hiện Panel an toàn (không tắt script nếu script gắn trực tiếp trên Panel đó)
        /// </summary>
        private void SetPanelVisibility(GameObject panel, bool visible)
        {
            if (panel == null) return;

            if (panel == gameObject)
            {
                // Script nằm trực tiếp trên chính panel này -> Dùng CanvasGroup để giữ Script luôn sống
                CanvasGroup cg = panel.GetComponent<CanvasGroup>();
                if (cg == null)
                {
                    cg = panel.AddComponent<CanvasGroup>();
                }

                cg.alpha = visible ? 1f : 0f;
                cg.interactable = visible;
                cg.blocksRaycasts = visible;
            }
            else
            {
                // Script nằm ở object khác -> SetActive an toàn
                panel.SetActive(visible);
                CanvasGroup cg = panel.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = visible ? 1f : 0f;
                    cg.interactable = visible;
                    cg.blocksRaycasts = visible;
                }
            }
        }

        /// <summary>
        /// Tiếp tục Game (Resume) -> Tắt toàn bộ UI Pause & Setting
        /// </summary>
        public void ResumeGame()
        {
            ResetUIOnStart();
        }

        /// <summary>
        /// Nút Quit -> Mở Popup Xác nhận Thoát
        /// </summary>
        public void OnQuitButtonClicked()
        {
            if (quitConfirmUI == null)
            {
                AutoBindReferences();
            }

            if (quitConfirmUI != null)
            {
                SetPanelVisibility(quitConfirmUI, true);
            }
            else
            {
                Debug.LogWarning("[PauseMenuController] Chưa kéo thả 'quitConfirmUI' vào Inspector nên chưa mở Popup xác nhận!");
            }
        }

        /// <summary>
        /// Nút YES trong Popup -> Xóa toàn bộ dữ liệu scene/tiến độ, khôi phục TimeScale và về MainMenu như mới
        /// </summary>
        public void OnQuitConfirmYes()
        {
            // 1. Xóa toàn bộ dữ liệu đã lưu (PlayerPrefs / Save data)
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            TimelinePlayStateManager.ResetAll();
            SceneRuntimeStateManager.ClearAll();
            PlayerCarryData.ResetAll();
            Tien.Minigame.MinigameCheckpointState.ClearCheckpoint();

            // 2. Reset UI và khôi phục TimeScale = 1
            ResetUIOnStart();

            // 3. Chuyển scene về MainMenu
            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                SceneLoader.Load(mainMenuSceneName);
            }
            else
            {
                Debug.LogWarning("[PauseMenuController] Chưa thiết lập mainMenuSceneName!");
            }
        }

        /// <summary>
        /// Nút NO trong Popup -> Tắt Popup xác nhận thoát
        /// </summary>
        public void OnQuitConfirmNo()
        {
            if (quitConfirmUI != null)
            {
                SetPanelVisibility(quitConfirmUI, false);
            }
        }

        /// <summary>
        /// Tìm và khởi tạo AudioSettingsManager để cập nhật Slider Âm thanh cho Setting UI
        /// </summary>
        private void InitAudioSettings()
        {
            if (settingUI == null) return;

            AudioSettingsManager audioSettings = settingUI.GetComponent<AudioSettingsManager>();
            if (audioSettings == null)
            {
#if UNITY_2023_1_OR_NEWER
                audioSettings = Object.FindFirstObjectByType<AudioSettingsManager>();
#else
                audioSettings = Object.FindObjectOfType<AudioSettingsManager>();
#endif
                if (audioSettings == null)
                {
                    audioSettings = settingUI.AddComponent<AudioSettingsManager>();
                }
            }

            if (audioSettings != null)
            {
                audioSettings.InitUI(settingUI);
            }
        }
    }
}

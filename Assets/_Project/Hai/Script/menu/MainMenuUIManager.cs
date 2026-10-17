using UnityEngine;
using UnityEngine.UI;
using Tien.AdvancedLoading.Core;

namespace Hai.Menu
{
    public class MainMenuUIManager : MonoBehaviour
    {
        [Header("Scene Configuration")]
        [SerializeField] private string targetSceneName = "TF";

        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject settingPanel;
        [SerializeField] private GameObject settingBox; // Object 'box' chứa Audio và Hienthi
        [SerializeField] private GameObject creditsPanel; // Object Bảng Credit

        [Header("Main Menu Buttons")]
        [SerializeField] private GameObject startButton;
        [SerializeField] private GameObject settingButton;
        [SerializeField] private GameObject creditButton;
        [SerializeField] private GameObject exitButton;
        [SerializeField] private GameObject continueButton;

        [Header("Setting Components")]
        [SerializeField] private GameObject audioTab;   // Phần Cài đặt Âm thanh (Audio)
        [SerializeField] private GameObject displayTab; // Phần Cài đặt Hiển thị (Hienthi)
        [SerializeField] private GameObject tabButtonsBox; // Object 'boxhienthi' chứa 2 nút tab cũ (sẽ tự động ẩn)
        [SerializeField] private GameObject closeSettingButton; // Nút Thoát Setting để quay lại Main Menu
        [SerializeField] private GameObject closeCreditButton;  // Nút Thoát Credit để quay lại Main Menu

        private void Awake()
        {
            AutoBindReferences();
        }

        private void Start()
        {
            // Ẩn nút Continue nếu có
            if (continueButton != null)
            {
                continueButton.SetActive(false);
            }

            // Mặc định hiện MainMenu, ẩn Setting & Credit
            ShowMainMenu();
        }

        private void Update()
        {
            // TỰ ĐỘNG BẬT LẠI CÁC NÚT MAINMENU: Nếu bảng Setting & Credit bị đóng mà các nút Start/Setting/Exit đang bị ẩn -> Tự động bật lại!
            bool isSubPanelActive = (settingPanel != null && settingPanel.activeInHierarchy) ||
                                    (creditsPanel != null && creditsPanel.activeInHierarchy);

            if (!isSubPanelActive)
            {
                if ((startButton != null && !startButton.activeSelf) ||
                    (settingButton != null && !settingButton.activeSelf) ||
                    (exitButton != null && !exitButton.activeSelf) ||
                    (mainMenuPanel != null && !mainMenuPanel.activeSelf))
                {
                    ShowMainMenu();
                }
            }
        }

        /// <summary>
        /// Tự động lấy chính xác vị trí GameObject theo cấu trúc Hierarchy
        /// </summary>
        private void AutoBindReferences()
        {
            Transform rootTransform = transform;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                #if UNITY_2023_1_OR_NEWER
                canvas = Object.FindFirstObjectByType<Canvas>();
                #else
                canvas = Object.FindObjectOfType<Canvas>();
                #endif
            }

            if (canvas != null) rootTransform = canvas.transform;

            // 1. Lấy đúng các Panel trực thuộc Canvas
            if (mainMenuPanel == null)
            {
                Transform mmTr = rootTransform.Find("MainMenu");
                if (mmTr != null) mainMenuPanel = mmTr.gameObject;
            }

            if (settingPanel == null)
            {
                Transform stTr = rootTransform.Find("Setting") ?? rootTransform.Find("Settings") ?? rootTransform.Find("SettingPanel");
                if (stTr != null) settingPanel = stTr.gameObject;
            }

            if (creditsPanel == null)
            {
                Transform crTr = rootTransform.Find("Credit") ?? rootTransform.Find("Credits") ?? rootTransform.Find("CreditPanel") ?? rootTransform.Find("CreditsPanel");
                if (crTr != null) creditsPanel = crTr.gameObject;
            }

            // 2. Lấy Nút bấm trên MainMenu
            if (mainMenuPanel != null)
            {
                Transform boxTr = mainMenuPanel.transform.Find("Background/Box") ?? mainMenuPanel.transform.Find("Box");
                if (boxTr != null)
                {
                    if (startButton == null) startButton = boxTr.Find("Start")?.gameObject;
                    if (continueButton == null) continueButton = boxTr.Find("Continue")?.gameObject;
                    if (settingButton == null) settingButton = boxTr.Find("Setting")?.gameObject;
                    if (creditButton == null) creditButton = boxTr.Find("Credit")?.gameObject ?? boxTr.Find("Credits")?.gameObject;
                    if (exitButton == null) exitButton = boxTr.Find("Exit")?.gameObject;
                }

                // Fallback nếu không tìm thấy theo đường dẫn
                Transform[] allChildren = mainMenuPanel.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allChildren)
                {
                    if (t == null) continue;
                    string nameLower = t.name.ToLower();
                    if (startButton == null && nameLower == "start") startButton = t.gameObject;
                    if (continueButton == null && nameLower == "continue") continueButton = t.gameObject;
                    if (settingButton == null && nameLower == "setting") settingButton = t.gameObject;
                    if (creditButton == null && (nameLower == "credit" || nameLower == "credits" || nameLower.Contains("credit"))) creditButton = t.gameObject;
                    if (exitButton == null && nameLower == "exit") exitButton = t.gameObject;
                }
            }

            // 3. Lấy đúng các phần tử bên trong Setting (Canvas/Setting/)
            if (settingPanel != null)
            {
                if (settingBox == null) settingBox = settingPanel.transform.Find("box")?.gameObject;
                if (tabButtonsBox == null) tabButtonsBox = settingPanel.transform.Find("boxhienthi")?.gameObject;

                if (settingBox != null)
                {
                    if (audioTab == null) audioTab = settingBox.transform.Find("Audio")?.gameObject;
                    if (displayTab == null) displayTab = settingBox.transform.Find("Hienthi")?.gameObject ?? settingBox.transform.Find("Graphics")?.gameObject;
                }

                // Tự động tìm các nút thoát/đóng Setting
                if (closeSettingButton == null)
                {
                    Transform closeTr = settingPanel.transform.Find("Close")
                                     ?? settingPanel.transform.Find("CloseButton")
                                     ?? settingPanel.transform.Find("Back")
                                     ?? settingPanel.transform.Find("BackMask");
                    if (closeTr != null) closeSettingButton = closeTr.gameObject;
                }

                // Đăng ký sự kiện Click cho mọi Nút bấm / BackMask có tên chứa 'back', 'close', 'exit' hoặc nút 'X' trong Setting
                Button[] settingButtons = settingPanel.GetComponentsInChildren<Button>(true);
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

            // 4. Lấy đúng các phần tử bên trong Credit
            if (creditsPanel != null)
            {
                if (closeCreditButton == null)
                {
                    Transform closeTr = creditsPanel.transform.Find("Close")
                                     ?? creditsPanel.transform.Find("CloseButton")
                                     ?? creditsPanel.transform.Find("Back")
                                     ?? creditsPanel.transform.Find("BackMask");
                    if (closeTr != null) closeCreditButton = closeTr.gameObject;
                }

                Button[] creditButtons = creditsPanel.GetComponentsInChildren<Button>(true);
                foreach (Button b in creditButtons)
                {
                    if (b != null)
                    {
                        string bName = b.name.ToLower();
                        if (bName.Contains("back") || bName.Contains("close") || bName.Contains("exit") || bName == "x" || bName.Contains("btn_close"))
                        {
                            b.onClick.RemoveListener(OnCloseCreditButtonClicked);
                            b.onClick.AddListener(OnCloseCreditButtonClicked);
                        }
                    }
                }
            }

            // 5. Lắng nghe sự kiện Click cho nút bấm chính
            BindButtonClick(startButton, OnStartButtonClicked);
            BindButtonClick(settingButton, OnSettingButtonClicked);
            BindButtonClick(creditButton, OnCreditButtonClicked);
            BindButtonClick(exitButton, OnExitButtonClicked);
            BindButtonClick(closeSettingButton, OnCloseSettingButtonClicked);
            BindButtonClick(closeCreditButton, OnCloseCreditButtonClicked);
        }

        private void BindButtonClick(GameObject btnObj, UnityEngine.Events.UnityAction action)
        {
            if (btnObj != null)
            {
                Button btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveListener(action);
                    btn.onClick.AddListener(action);
                }
            }
        }

        /// <summary>
        /// Nút Bắt đầu -> Chuyển scene
        /// </summary>
        public void OnStartButtonClicked()
        {
            TimelinePlayStateManager.ResetAll();
            SceneRuntimeStateManager.ClearAll();
            PlayerCarryData.ResetAll();
            Tien.Minigame.MinigameCheckpointState.ClearCheckpoint();

            if (!string.IsNullOrEmpty(targetSceneName))
            {
                SceneLoader.Load(targetSceneName);
            }
            else
            {
                Debug.LogWarning("[MainMenuUIManager] Chưa thiết lập targetSceneName!");
            }
        }

        /// <summary>
        /// Nút Setting -> Tắt MainMenu và ẩn các nút con (Start/Setting/Credit/Exit), Bật Setting
        /// </summary>
        public void OnSettingButtonClicked()
        {
            // Ẩn Main Menu & Credits
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);

            // Bật Panel Setting
            if (settingPanel != null) settingPanel.SetActive(true);

            // Bật khung 'box' chứa các Slider
            if (settingBox != null) settingBox.SetActive(true);

            // Tắt nút chuyển tab cũ
            if (tabButtonsBox != null) tabButtonsBox.SetActive(false);

            // Bật cả Âm thanh VÀ Hiển thị trong cùng 1 trang Setting
            if (audioTab != null) audioTab.SetActive(true);
            if (displayTab != null) displayTab.SetActive(true);

            // Tự động khởi tạo lại Slider Âm thanh
            AudioSettingsManager audioSettings = AudioSettingsManager.Instance;
            if (audioSettings == null)
            {
#if UNITY_2023_1_OR_NEWER
                audioSettings = Object.FindFirstObjectByType<AudioSettingsManager>();
#else
                audioSettings = Object.FindObjectOfType<AudioSettingsManager>();
#endif
                if (audioSettings == null)
                {
                    GameObject targetObj = settingPanel != null ? settingPanel : gameObject;
                    audioSettings = targetObj.AddComponent<AudioSettingsManager>();
                }
            }

            if (audioSettings != null)
            {
                audioSettings.InitUI(settingPanel);
            }

            // Tự động khởi tạo UI Đồ họa (GraphicsSettingsUI)
            GraphicsSettingsUI graphicsUI = settingPanel != null ? settingPanel.GetComponentInChildren<GraphicsSettingsUI>(true) : null;
            if (graphicsUI == null && settingPanel != null)
            {
                graphicsUI = settingPanel.AddComponent<GraphicsSettingsUI>();
            }

            if (graphicsUI != null)
            {
                graphicsUI.InitUI();
            }
        }

        /// <summary>
        /// Nút Thoát Setting -> Đóng Setting và quay về Main Menu
        /// </summary>
        public void OnCloseSettingButtonClicked()
        {
            ShowMainMenu();
        }

        /// <summary>
        /// Nút Credit -> Bật Bảng Credit, ẩn Main Menu & Setting
        /// </summary>
        public void OnCreditButtonClicked()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (settingPanel != null) settingPanel.SetActive(false);

            if (creditsPanel != null)
            {
                creditsPanel.SetActive(true);
                CreditsUIManager credManager = creditsPanel.GetComponent<CreditsUIManager>();
                if (credManager != null)
                {
                    credManager.ResetScroll();
                }
            }
        }

        /// <summary>
        /// Nút Thoát Credit -> Đóng Credit và quay về Main Menu
        /// </summary>
        public void OnCloseCreditButtonClicked()
        {
            ShowMainMenu();
        }

        /// <summary>
        /// Hiện Main Menu chính và kích hoạt lại toàn bộ các nút bấm bên trong
        /// </summary>
        public void ShowMainMenu()
        {
            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(true);

                // Bật các khung container chính (Background, Box) nếu có
                Transform bgTr = mainMenuPanel.transform.Find("Background");
                if (bgTr != null) bgTr.gameObject.SetActive(true);

                Transform boxTr = mainMenuPanel.transform.Find("Background/Box") ?? mainMenuPanel.transform.Find("Box");
                if (boxTr != null) boxTr.gameObject.SetActive(true);

                // Ẩn tất cả các FocusObj (icon mũi tên trang trí chỉ hiện khi di chuột Hover)
                var hoverScripts = mainMenuPanel.GetComponentsInChildren<BamaoUIPack.Scripts.ShowObjWhenBtnHover>(true);
                foreach (var hover in hoverScripts)
                {
                    if (hover != null && hover.FocusObj != null)
                    {
                        hover.FocusObj.SetActive(false);
                    }
                }

                // Reset màu chữ bị đổi sang đen khi click (TextColorSwitcher)
                var textSwitchers = mainMenuPanel.GetComponentsInChildren<BamaoUIPack.Scripts.TextColorSwitcher>(true);
                foreach (var ts in textSwitchers)
                {
                    if (ts != null) ts.ResetColor();
                }

                // Bỏ chọn nút hiện tại trong Unity EventSystem
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                }

                // Ép Unity Selectable (Button) reset trạng thái visual (Normal Color) ngay lập tức mà không cần hover lại
                Selectable[] selectables = mainMenuPanel.GetComponentsInChildren<Selectable>(true);
                UnityEngine.EventSystems.PointerEventData dummyPointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                foreach (Selectable s in selectables)
                {
                    if (s != null)
                    {
                        s.interactable = true;
                        s.OnDeselect(dummyPointer);
                        s.OnPointerExit(dummyPointer);
                        s.enabled = false;
                        s.enabled = true;
                    }
                }

                // Đảm bảo tất cả Graphic (Text, Image) trong MainMenuPanel đều nhận chuột
                Graphic[] graphics = mainMenuPanel.GetComponentsInChildren<Graphic>(true);
                foreach (Graphic g in graphics)
                {
                    if (g != null) g.raycastTarget = true;
                }
            }

            if (settingPanel != null) settingPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);

            // Đảm bảo bật lại từng nút cụ thể và gán lại sự kiện Click
            if (startButton != null) startButton.SetActive(true);
            if (settingButton != null) settingButton.SetActive(true);
            if (creditButton != null) creditButton.SetActive(true);
            if (exitButton != null) exitButton.SetActive(true);
            if (continueButton != null) continueButton.SetActive(false);

            BindButtonClick(startButton, OnStartButtonClicked);
            BindButtonClick(settingButton, OnSettingButtonClicked);
            BindButtonClick(creditButton, OnCreditButtonClicked);
            BindButtonClick(exitButton, OnExitButtonClicked);
        }

        /// <summary>
        /// Nút Thoát Game
        /// </summary>
        public void OnExitButtonClicked()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }
}

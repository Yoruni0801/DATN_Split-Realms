using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class GraphicsSettingsUI : MonoBehaviour
{
    [Header("Horizontal Selectors (Kiểu RPG: [ ◀ ]  Giá Trị  [ ▶ ])")]
    [Header("1. Độ Phân Giải (Resolution)")]
    public Button resPrevBtn;
    public Button resNextBtn;
    public TextMeshProUGUI resValueText;
    public Text resLegacyText;

    [Header("2. Chế Độ Hiển Thị (Screen Mode)")]
    public Button modePrevBtn;
    public Button modeNextBtn;
    public TextMeshProUGUI modeValueText;
    public Text modeLegacyText;

    [Header("3. Hiệu Ứng Phát Sáng (Bloom / Glow FX)")]
    public Button bloomPrevBtn;
    public Button bloomNextBtn;
    public TextMeshProUGUI bloomValueText;
    public Text bloomLegacyText;

    [Header("4. Rung Màn Hình (Screen Shake)")]
    public Button shakePrevBtn;
    public Button shakeNextBtn;
    public TextMeshProUGUI shakeValueText;
    public Text shakeLegacyText;

    [Header("5. Giới Hạn Khung Hình (FPS Limit)")]
    public Button fpsPrevBtn;
    public Button fpsNextBtn;
    public TextMeshProUGUI fpsValueText;
    public Text fpsLegacyText;

    [Header("6. Đồng Bộ Dọc (VSync)")]
    public Button vsyncPrevBtn;
    public Button vsyncNextBtn;
    public TextMeshProUGUI vsyncValueText;
    public Text vsyncLegacyText;
    public Toggle vsyncToggle;

    [Header("7. Độ Sáng Màn Hình (Brightness)")]
    public Slider brightnessSlider;
    public TextMeshProUGUI brightnessValueText;
    public Text brightnessLegacyText;

    [Header("Fallback Dropdowns")]
    public TMP_Dropdown resolutionTmpDropdown;
    public TMP_Dropdown screenModeTmpDropdown;
    public TMP_Dropdown qualityTmpDropdown;
    public TMP_Dropdown frameRateTmpDropdown;

    public Dropdown resolutionLegacyDropdown;
    public Dropdown screenModeLegacyDropdown;
    public Dropdown qualityLegacyDropdown;
    public Dropdown frameRateLegacyDropdown;

    private List<ResolutionModel> resolutionList = new List<ResolutionModel>();
    private int currentResIndex = 0;
    private int currentModeIndex = 0;
    private bool currentBloom = true;
    private bool currentShake = true;
    private int currentFPSIndex = 1; // 60 FPS
    private bool currentVSync = true;

    private readonly string[] screenModeNames = { "Toàn màn hình (Fullscreen)", "Cửa sổ (Windowed)" };
    private readonly int[] fpsValues = { 30, 60, 90, 120, 144, 240, -1 };
    private readonly string[] fpsNames = { "30 FPS", "60 FPS", "90 FPS", "120 FPS", "144 FPS", "240 FPS", "Không giới hạn (Max)" };

    private bool isUpdatingUI = false;

    private void Awake()
    {
        GlobalGraphicsManager.EnsureInstance();
    }

    private void OnEnable()
    {
        InitUI();
    }

    public void InitUI()
    {
        isUpdatingUI = true;

        GlobalGraphicsManager.EnsureInstance();
        AutoFindUIElements();

        SetupResolution();
        SetupScreenMode();
        SetupBloom();
        SetupScreenShake();
        SetupFrameRate();
        SetupVSync();
        SetupBrightness();

        isUpdatingUI = false;
    }

    private void AutoFindUIElements()
    {
        // TỐI ƯU: Nếu đã được gán sẵn qua Prefab/Inspector thì bỏ qua quét cây phân cấp
        if (resPrevBtn != null && resNextBtn != null && resValueText != null &&
            modePrevBtn != null && modeNextBtn != null && modeValueText != null &&
            bloomPrevBtn != null && bloomNextBtn != null && bloomValueText != null &&
            shakePrevBtn != null && shakeNextBtn != null && shakeValueText != null &&
            fpsPrevBtn != null && fpsNextBtn != null && fpsValueText != null &&
            vsyncPrevBtn != null && vsyncNextBtn != null && vsyncValueText != null &&
            brightnessSlider != null)
        {
            return;
        }

        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allChildren)
        {
            if (t == null) continue;
            string n = t.name.ToLower();

            // Resolution
            if (n.Contains("res") || n.Contains("dophangiai"))
            {
                BindSelectorFromTransform(t, ref resPrevBtn, ref resNextBtn, ref resValueText, ref resLegacyText);
            }
            // Screen Mode
            else if (n.Contains("mode") || n.Contains("screen") || n.Contains("fullscreen"))
            {
                BindSelectorFromTransform(t, ref modePrevBtn, ref modeNextBtn, ref modeValueText, ref modeLegacyText);
            }
            // Bloom / Glow
            else if (n.Contains("bloom") || n.Contains("glow") || n.Contains("phatsang") || n.Contains("hieuung") || n.Contains("quality") || n.Contains("dohoa"))
            {
                BindSelectorFromTransform(t, ref bloomPrevBtn, ref bloomNextBtn, ref bloomValueText, ref bloomLegacyText);
            }
            // Screen Shake
            else if (n.Contains("shake") || n.Contains("rung") || n.Contains("camera"))
            {
                BindSelectorFromTransform(t, ref shakePrevBtn, ref shakeNextBtn, ref shakeValueText, ref shakeLegacyText);
            }
            // FPS
            else if (n.Contains("fps") || n.Contains("framerate") || n.Contains("khunghinh"))
            {
                BindSelectorFromTransform(t, ref fpsPrevBtn, ref fpsNextBtn, ref fpsValueText, ref fpsLegacyText);
            }
            // VSync
            else if (n.Contains("vsync"))
            {
                BindSelectorFromTransform(t, ref vsyncPrevBtn, ref vsyncNextBtn, ref vsyncValueText, ref vsyncLegacyText);
            }
        }

        // Quét Slider độ sáng
        if (brightnessSlider == null)
        {
            Slider[] sliders = GetComponentsInChildren<Slider>(true);
            foreach (var s in sliders)
            {
                if (s != null && (s.name.ToLower().Contains("bright") || s.name.ToLower().Contains("dosang")))
                {
                    brightnessSlider = s;
                    break;
                }
            }
        }
    }

    private void BindSelectorFromTransform(Transform row, ref Button prevBtn, ref Button nextBtn, ref TextMeshProUGUI tmpText, ref Text legacyText)
    {
        Button[] btns = row.GetComponentsInChildren<Button>(true);
        foreach (var b in btns)
        {
            if (b == null) continue;
            string bn = b.name.ToLower();
            if (prevBtn == null && (bn.Contains("left") || bn.Contains("prev") || bn.Contains("<") || bn.Contains("back")))
                prevBtn = b;
            else if (nextBtn == null && (bn.Contains("right") || bn.Contains("next") || bn.Contains(">") || bn.Contains("fwd")))
                nextBtn = b;
        }

        if (tmpText == null)
        {
            TextMeshProUGUI[] tmps = row.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                if (t == null) continue;
                string tn = t.name.ToLower();
                if (tn.Contains("value") || tn.Contains("text") || tn.Contains("val") || tn.Contains("label"))
                {
                    tmpText = t;
                    break;
                }
            }
        }

        if (legacyText == null)
        {
            Text[] txts = row.GetComponentsInChildren<Text>(true);
            foreach (var t in txts)
            {
                if (t == null) continue;
                string tn = t.name.ToLower();
                if (tn.Contains("value") || tn.Contains("text") || tn.Contains("val"))
                {
                    legacyText = t;
                    break;
                }
            }
        }
    }

    #region 1. Resolution Logic

    private void SetupResolution()
    {
        resolutionList = GlobalGraphicsManager.GetSupportedResolutions();
        int curW = GlobalGraphicsManager.Instance != null ? GlobalGraphicsManager.Instance.GetWidth() : Screen.width;
        int curH = GlobalGraphicsManager.Instance != null ? GlobalGraphicsManager.Instance.GetHeight() : Screen.height;

        currentResIndex = 0;
        for (int i = 0; i < resolutionList.Count; i++)
        {
            if (resolutionList[i].width == curW && resolutionList[i].height == curH)
            {
                currentResIndex = i;
                break;
            }
        }

        UpdateResolutionDisplay();

        if (resPrevBtn != null)
        {
            resPrevBtn.onClick.RemoveListener(OnResPrevClicked);
            resPrevBtn.onClick.AddListener(OnResPrevClicked);
        }
        if (resNextBtn != null)
        {
            resNextBtn.onClick.RemoveListener(OnResNextClicked);
            resNextBtn.onClick.AddListener(OnResNextClicked);
        }
    }

    private void OnResPrevClicked()
    {
        if (resolutionList.Count == 0) return;
        currentResIndex--;
        if (currentResIndex < 0) currentResIndex = resolutionList.Count - 1;
        ApplyResolutionByIndex(currentResIndex);
    }

    private void OnResNextClicked()
    {
        if (resolutionList.Count == 0) return;
        currentResIndex++;
        if (currentResIndex >= resolutionList.Count) currentResIndex = 0;
        ApplyResolutionByIndex(currentResIndex);
    }

    private void ApplyResolutionByIndex(int index)
    {
        if (index < 0 || index >= resolutionList.Count) return;
        ResolutionModel res = resolutionList[index];

        if (GlobalGraphicsManager.Instance != null)
        {
            GlobalGraphicsManager.Instance.SetResolution(res.width, res.height);
        }
        else
        {
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        }

        UpdateResolutionDisplay();
    }

    private void UpdateResolutionDisplay()
    {
        if (resolutionList.Count == 0) return;
        string text = resolutionList[currentResIndex].ToString();
        if (resValueText != null) resValueText.text = text;
        if (resLegacyText != null) resLegacyText.text = text;
    }

    #endregion

    #region 2. Screen Mode Logic

    private void SetupScreenMode()
    {
        currentModeIndex = GlobalGraphicsManager.Instance != null ? GlobalGraphicsManager.Instance.GetScreenMode() : (Screen.fullScreen ? 0 : 1);
        currentModeIndex = Mathf.Clamp(currentModeIndex, 0, screenModeNames.Length - 1);

        UpdateScreenModeDisplay();

        if (modePrevBtn != null)
        {
            modePrevBtn.onClick.RemoveListener(OnModeToggleClicked);
            modePrevBtn.onClick.AddListener(OnModeToggleClicked);
        }
        if (modeNextBtn != null)
        {
            modeNextBtn.onClick.RemoveListener(OnModeToggleClicked);
            modeNextBtn.onClick.AddListener(OnModeToggleClicked);
        }
    }

    private void OnModeToggleClicked()
    {
        currentModeIndex = (currentModeIndex == 0) ? 1 : 0;
        if (GlobalGraphicsManager.Instance != null)
        {
            GlobalGraphicsManager.Instance.SetScreenMode(currentModeIndex);
        }
        else
        {
            Screen.fullScreenMode = (currentModeIndex == 0) ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        UpdateScreenModeDisplay();
    }

    private void UpdateScreenModeDisplay()
    {
        string text = screenModeNames[currentModeIndex];
        if (modeValueText != null) modeValueText.text = text;
        if (modeLegacyText != null) modeLegacyText.text = text;
    }

    #endregion

    #region 3. Bloom / Glow FX Logic

    private void SetupBloom()
    {
        currentBloom = GlobalGraphicsManager.Instance != null ? GlobalGraphicsManager.Instance.GetBloom() : true;
        UpdateBloomDisplay();

        if (bloomPrevBtn != null)
        {
            bloomPrevBtn.onClick.RemoveListener(OnBloomToggleClicked);
            bloomPrevBtn.onClick.AddListener(OnBloomToggleClicked);
        }
        if (bloomNextBtn != null)
        {
            bloomNextBtn.onClick.RemoveListener(OnBloomToggleClicked);
            bloomNextBtn.onClick.AddListener(OnBloomToggleClicked);
        }
    }

    private void OnBloomToggleClicked()
    {
        currentBloom = !currentBloom;
        if (GlobalGraphicsManager.Instance != null)
        {
            GlobalGraphicsManager.Instance.SetBloom(currentBloom);
        }

        UpdateBloomDisplay();
    }

    private void UpdateBloomDisplay()
    {
        string text = currentBloom ? "BẬT (Rực rỡ)" : "TẮT (Cơ bản)";
        if (bloomValueText != null) bloomValueText.text = text;
        if (bloomLegacyText != null) bloomLegacyText.text = text;
    }

    #endregion

    #region 4. Screen Shake Logic

    private void SetupScreenShake()
    {
        currentShake = GlobalGraphicsManager.Instance != null ? GlobalGraphicsManager.Instance.GetScreenShake() : true;
        UpdateScreenShakeDisplay();

        if (shakePrevBtn != null)
        {
            shakePrevBtn.onClick.RemoveListener(OnShakeToggleClicked);
            shakePrevBtn.onClick.AddListener(OnShakeToggleClicked);
        }
        if (shakeNextBtn != null)
        {
            shakeNextBtn.onClick.RemoveListener(OnShakeToggleClicked);
            shakeNextBtn.onClick.AddListener(OnShakeToggleClicked);
        }
    }

    private void OnShakeToggleClicked()
    {
        currentShake = !currentShake;
        if (GlobalGraphicsManager.Instance != null)
        {
            GlobalGraphicsManager.Instance.SetScreenShake(currentShake);
        }

        UpdateScreenShakeDisplay();
    }

    private void UpdateScreenShakeDisplay()
    {
        string text = currentShake ? "BẬT (Mạnh mẽ)" : "TẮT (Cố định)";
        if (shakeValueText != null) shakeValueText.text = text;
        if (shakeLegacyText != null) shakeLegacyText.text = text;
    }

    #endregion

    #region 5. Frame Rate Logic

    private void SetupFrameRate()
    {
        int currentFPS = GlobalGraphicsManager.Instance != null ? GlobalGraphicsManager.Instance.GetFPSLimit() : Application.targetFrameRate;
        currentFPSIndex = 1; // Default 60
        for (int i = 0; i < fpsValues.Length; i++)
        {
            if (fpsValues[i] == currentFPS)
            {
                currentFPSIndex = i;
                break;
            }
        }
        if (currentFPS <= 0 || currentFPS >= 300) currentFPSIndex = fpsValues.Length - 1; // Max

        UpdateFrameRateDisplay();

        if (fpsPrevBtn != null)
        {
            fpsPrevBtn.onClick.RemoveListener(OnFPSPrevClicked);
            fpsPrevBtn.onClick.AddListener(OnFPSPrevClicked);
        }
        if (fpsNextBtn != null)
        {
            fpsNextBtn.onClick.RemoveListener(OnFPSNextClicked);
            fpsNextBtn.onClick.AddListener(OnFPSNextClicked);
        }
    }

    private void OnFPSPrevClicked()
    {
        currentFPSIndex--;
        if (currentFPSIndex < 0) currentFPSIndex = fpsValues.Length - 1;
        ApplyFPS(currentFPSIndex);
    }

    private void OnFPSNextClicked()
    {
        currentFPSIndex++;
        if (currentFPSIndex >= fpsValues.Length) currentFPSIndex = 0;
        ApplyFPS(currentFPSIndex);
    }

    private void ApplyFPS(int index)
    {
        int targetFPS = fpsValues[index];
        if (GlobalGraphicsManager.Instance != null)
        {
            GlobalGraphicsManager.Instance.SetFPSLimit(targetFPS);
        }
        else
        {
            Application.targetFrameRate = targetFPS;
        }

        UpdateFrameRateDisplay();
    }

    private void UpdateFrameRateDisplay()
    {
        string text = fpsNames[currentFPSIndex];
        if (fpsValueText != null) fpsValueText.text = text;
        if (fpsLegacyText != null) fpsLegacyText.text = text;
    }

    #endregion

    #region 6. VSync Logic

    private void SetupVSync()
    {
        currentVSync = GlobalGraphicsManager.Instance != null ? GlobalGraphicsManager.Instance.GetVSync() : (QualitySettings.vSyncCount > 0);
        UpdateVSyncDisplay();

        if (vsyncPrevBtn != null)
        {
            vsyncPrevBtn.onClick.RemoveListener(OnVSyncToggleClicked);
            vsyncPrevBtn.onClick.AddListener(OnVSyncToggleClicked);
        }
        if (vsyncNextBtn != null)
        {
            vsyncNextBtn.onClick.RemoveListener(OnVSyncToggleClicked);
            vsyncNextBtn.onClick.AddListener(OnVSyncToggleClicked);
        }

        if (vsyncToggle != null)
        {
            vsyncToggle.isOn = currentVSync;
            vsyncToggle.onValueChanged.RemoveListener(OnVSyncToggleChanged);
            vsyncToggle.onValueChanged.AddListener(OnVSyncToggleChanged);
        }
    }

    private void OnVSyncToggleClicked()
    {
        currentVSync = !currentVSync;
        if (GlobalGraphicsManager.Instance != null)
        {
            GlobalGraphicsManager.Instance.SetVSync(currentVSync);
        }
        else
        {
            QualitySettings.vSyncCount = currentVSync ? 1 : 0;
        }

        UpdateVSyncDisplay();
    }

    private void OnVSyncToggleChanged(bool enabled)
    {
        if (isUpdatingUI) return;
        currentVSync = enabled;
        if (GlobalGraphicsManager.Instance != null)
        {
            GlobalGraphicsManager.Instance.SetVSync(enabled);
        }
        else
        {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
        }

        UpdateVSyncDisplay();
    }

    private void UpdateVSyncDisplay()
    {
        string text = currentVSync ? "BẬT (Mượt mà)" : "TẮT (Tối đa)";
        if (vsyncValueText != null) vsyncValueText.text = text;
        if (vsyncLegacyText != null) vsyncLegacyText.text = text;
        if (vsyncToggle != null && vsyncToggle.isOn != currentVSync) vsyncToggle.isOn = currentVSync;
    }

    #endregion

    #region 7. Brightness Logic

    private void SetupBrightness()
    {
        if (brightnessSlider == null) return;

        brightnessSlider.minValue = 1f;
        brightnessSlider.maxValue = 100f;
        brightnessSlider.wholeNumbers = true;

        float currentVal = GlobalGraphicsManager.Instance != null ? GlobalGraphicsManager.Instance.GetBrightness() : 100f;
        brightnessSlider.value = Mathf.Clamp(currentVal, 1f, 100f);

        UpdateBrightnessText(brightnessSlider.value);

        brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
        brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
    }

    private void OnBrightnessChanged(float val)
    {
        if (isUpdatingUI) return;
        UpdateBrightnessText(val);

        if (GlobalGraphicsManager.Instance != null)
        {
            GlobalGraphicsManager.Instance.SetBrightness(val);
        }
    }

    private void UpdateBrightnessText(float val)
    {
        int percent = Mathf.Clamp(Mathf.RoundToInt(val), 1, 100);
        if (brightnessValueText != null) brightnessValueText.text = $"{percent}%";
        if (brightnessLegacyText != null) brightnessLegacyText.text = $"{percent}%";
    }

    #endregion
}

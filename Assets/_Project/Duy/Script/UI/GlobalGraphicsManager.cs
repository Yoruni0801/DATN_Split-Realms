using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System;
using System.Collections;
using System.Collections.Generic;

public class GlobalGraphicsManager : MonoBehaviour
{
    public static GlobalGraphicsManager Instance { get; private set; }

    // PlayerPrefs Keys
    public const string KeyResolutionWidth = "Graphic_ResWidth";
    public const string KeyResolutionHeight = "Graphic_ResHeight";
    public const string KeyScreenMode = "Graphic_ScreenMode";       // 0: Fullscreen, 1: Windowed
    public const string KeyQualityLevel = "Graphic_QualityLevel";   // 0 .. QualitySettings.names.Length - 1
    public const string KeyVSync = "Graphic_VSync";                 // 0: Off, 1: On
    public const string KeyFPSLimit = "Graphic_FPSLimit";           // 30, 60, 90, 120, 144, 240, -1
    public const string KeyBrightness = "Graphic_Brightness";       // 0.5f .. 1.5f (Default 1.0f)
    public const string KeyBloom = "Graphic_Bloom";                 // 0: Off, 1: On (Glow FX)
    public const string KeyScreenShake = "Graphic_ScreenShake";     // 0: Off, 1: On (Camera Shake)
    public const string KeyVignette = "Graphic_Vignette";           // 0: Off, 1: On (Cinematic Vignette)

    [Header("Runtime Cache")]
    [SerializeField] private int currentWidth;
    [SerializeField] private int currentHeight;
    [SerializeField] private int currentScreenMode; // 0: Fullscreen, 1: Windowed
    [SerializeField] private int currentQualityLevel;
    [SerializeField] private int currentVSync;
    [SerializeField] private int currentFPSLimit;
    [SerializeField] private float currentBrightness = 1.0f;
    [SerializeField] private bool currentBloom = true;
    [SerializeField] private bool currentScreenShake = true;
    [SerializeField] private bool currentVignette = true;

    // Cache resolutions để tránh native call liên tục
    private static List<ResolutionModel> cachedResolutions = null;

    // Overlay toàn màn hình cho độ sáng
    private Canvas brightnessCanvas;
    private Image brightnessOverlayDark;
    private Image brightnessOverlayLight;

    private Coroutine postProcessRoutine;

    public event Action OnGraphicsSettingsChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitializeBeforeSceneLoad()
    {
        EnsureInstance();
        LoadAndApplyAllSettings(false);
    }

    public static GlobalGraphicsManager EnsureInstance()
    {
        if (Instance == null)
        {
#if UNITY_2023_1_OR_NEWER
            Instance = UnityEngine.Object.FindFirstObjectByType<GlobalGraphicsManager>();
#else
            Instance = UnityEngine.Object.FindObjectOfType<GlobalGraphicsManager>();
#endif
            if (Instance == null)
            {
                GameObject go = new GameObject("GlobalGraphicsManager");
                Instance = go.AddComponent<GlobalGraphicsManager>();
            }
        }
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null || Instance == this)
        {
            Instance = this;
            transform.SetParent(null);
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
                CreateBrightnessOverlay();
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            }
            LoadAndApplyAllSettings(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Sử dụng Coroutine trì hoãn 1 frame để scene load ổn định hoàn toàn, tránh lag frame
        if (postProcessRoutine != null)
        {
            StopCoroutine(postProcessRoutine);
        }
        postProcessRoutine = StartCoroutine(DeferredApplySettingsRoutine());
    }

    private IEnumerator DeferredApplySettingsRoutine()
    {
        // Đợi 1 frame cho toàn bộ Object trong Scene mới Awake & Start xong
        yield return null;

        ApplyPostProcessingSettings();
        ApplyBrightnessOverlay();
    }

    private void CreateBrightnessOverlay()
    {
        if (brightnessCanvas != null) return;

        GameObject canvasGo = new GameObject("GlobalBrightnessOverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform);

        brightnessCanvas = canvasGo.GetComponent<Canvas>();
        brightnessCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        brightnessCanvas.sortingOrder = 9999; // Luôn nằm trên cùng

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // 1. Lớp tối (Khi Brightness < 1.0)
        GameObject darkGo = new GameObject("DarkOverlay", typeof(RectTransform), typeof(Image));
        darkGo.transform.SetParent(canvasGo.transform, false);
        RectTransform darkRt = darkGo.GetComponent<RectTransform>();
        darkRt.anchorMin = Vector2.zero;
        darkRt.anchorMax = Vector2.one;
        darkRt.sizeDelta = Vector2.zero;

        brightnessOverlayDark = darkGo.GetComponent<Image>();
        brightnessOverlayDark.color = new Color(0, 0, 0, 0);
        brightnessOverlayDark.raycastTarget = false; // Không chặn click chuột

        // 2. Lớp sáng (Khi Brightness > 1.0)
        GameObject lightGo = new GameObject("LightOverlay", typeof(RectTransform), typeof(Image));
        lightGo.transform.SetParent(canvasGo.transform, false);
        RectTransform lightRt = lightGo.GetComponent<RectTransform>();
        lightRt.anchorMin = Vector2.zero;
        lightRt.anchorMax = Vector2.one;
        lightRt.sizeDelta = Vector2.zero;

        brightnessOverlayLight = lightGo.GetComponent<Image>();
        brightnessOverlayLight.color = new Color(1f, 0.98f, 0.9f, 0);
        brightnessOverlayLight.raycastTarget = false; // Không chặn click chuột
    }

    public static void LoadAndApplyAllSettings(bool forceExpensive = false)
    {
        int defaultWidth = Screen.currentResolution.width > 0 ? Screen.currentResolution.width : 1920;
        int defaultHeight = Screen.currentResolution.height > 0 ? Screen.currentResolution.height : 1080;
        
        int width = PlayerPrefs.GetInt(KeyResolutionWidth, defaultWidth);
        int height = PlayerPrefs.GetInt(KeyResolutionHeight, defaultHeight);
        int screenMode = PlayerPrefs.GetInt(KeyScreenMode, 0); // 0: Fullscreen

        FullScreenMode fsMode = (screenMode == 1) ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        
        // TỐI ƯU: Chỉ gọi SetResolution nếu thực sự khác để tránh GPU recreate swap-chain làm đơ màn hình
        if (Screen.width != width || Screen.height != height || Screen.fullScreenMode != fsMode)
        {
            Screen.SetResolution(width, height, fsMode);
        }

        int defaultQuality = Mathf.Clamp(QualitySettings.names.Length - 1, 0, 5);
        int qualityLevel = PlayerPrefs.GetInt(KeyQualityLevel, defaultQuality);
        if (qualityLevel >= 0 && qualityLevel < QualitySettings.names.Length)
        {
            // TỐI ƯU: applyExpensiveChanges: false khi load scene để tránh reload lại toàn bộ asset texture/shader
            if (QualitySettings.GetQualityLevel() != qualityLevel)
            {
                QualitySettings.SetQualityLevel(qualityLevel, forceExpensive);
            }
        }

        int vSync = PlayerPrefs.GetInt(KeyVSync, 1);
        if (QualitySettings.vSyncCount != vSync)
        {
            QualitySettings.vSyncCount = vSync;
        }

        int fpsLimit = PlayerPrefs.GetInt(KeyFPSLimit, 60);
        if (Application.targetFrameRate != fpsLimit)
        {
            Application.targetFrameRate = fpsLimit;
        }

        float savedBrightness = PlayerPrefs.GetFloat(KeyBrightness, 100f);
        // Tương thích ngược: nếu PlayerPrefs cũ lưu theo thang 0.5 - 1.5 thì chuyển thành thang 1 - 100
        if (savedBrightness <= 1.5f && savedBrightness > 0f)
        {
            savedBrightness = Mathf.Clamp(savedBrightness * 100f, 1f, 100f);
        }
        float brightness = Mathf.Clamp(savedBrightness, 1f, 100f);

        bool bloom = PlayerPrefs.GetInt(KeyBloom, 1) == 1;
        bool shake = PlayerPrefs.GetInt(KeyScreenShake, 1) == 1;
        bool vignette = PlayerPrefs.GetInt(KeyVignette, 1) == 1;

        if (Instance != null)
        {
            Instance.currentWidth = width;
            Instance.currentHeight = height;
            Instance.currentScreenMode = screenMode;
            Instance.currentQualityLevel = qualityLevel;
            Instance.currentVSync = vSync;
            Instance.currentFPSLimit = fpsLimit;
            Instance.currentBrightness = brightness;
            Instance.currentBloom = bloom;
            Instance.currentScreenShake = shake;
            Instance.currentVignette = vignette;

            Instance.ApplyBrightnessOverlay();
            Instance.ApplyPostProcessingSettings();
            Instance.OnGraphicsSettingsChanged?.Invoke();
        }
    }

    public void ApplyBrightnessOverlay()
    {
        if (brightnessOverlayDark == null || brightnessOverlayLight == null)
        {
            CreateBrightnessOverlay();
        }

        // Thang đo 1 - 100 (100: Bình thường, < 100: Tối dần xuống)
        float norm = Mathf.Clamp(currentBrightness, 1f, 100f) / 100f;

        if (norm < 1.0f)
        {
            // 100% -> alpha 0; 1% -> alpha 0.8
            float alpha = Mathf.Lerp(0.8f, 0f, (norm - 0.01f) / 0.99f);
            if (brightnessOverlayDark != null) brightnessOverlayDark.color = new Color(0, 0, 0, alpha);
            if (brightnessOverlayLight != null) brightnessOverlayLight.color = new Color(1, 1, 1, 0);
        }
        else
        {
            if (brightnessOverlayDark != null) brightnessOverlayDark.color = new Color(0, 0, 0, 0);
            if (brightnessOverlayLight != null) brightnessOverlayLight.color = new Color(1, 1, 1, 0);
        }
    }

    public void ApplyPostProcessingSettings()
    {
#if UNITY_2023_1_OR_NEWER
        Volume[] volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        Volume[] volumes = UnityEngine.Object.FindObjectsOfType<Volume>();
#endif
        if (volumes == null) return;

        foreach (var vol in volumes)
        {
            if (vol == null) continue;

            // TỐI ƯU: Sử dụng sharedProfile trước nếu có để tránh vol.profile tự động instantiate bản copy trong runtime
            VolumeProfile prof = vol.sharedProfile != null ? vol.sharedProfile : vol.profile;
            if (prof == null) continue;

            // 1. Bloom / Glow
            if (prof.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom))
            {
                bloom.active = currentBloom;
            }

            // 2. Vignette
            if (prof.TryGet<UnityEngine.Rendering.Universal.Vignette>(out var vignette))
            {
                vignette.active = currentVignette;
            }
        }
    }

    #region Public Setters & API

    public void SetResolution(int width, int height)
    {
        currentWidth = width;
        currentHeight = height;
        FullScreenMode fsMode = (currentScreenMode == 1) ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        
        if (Screen.width != width || Screen.height != height || Screen.fullScreenMode != fsMode)
        {
            Screen.SetResolution(width, height, fsMode);
        }

        PlayerPrefs.SetInt(KeyResolutionWidth, width);
        PlayerPrefs.SetInt(KeyResolutionHeight, height);

        OnGraphicsSettingsChanged?.Invoke();
    }

    public void SetScreenMode(int modeIndex)
    {
        currentScreenMode = modeIndex;
        FullScreenMode fsMode = (modeIndex == 1) ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        
        int w = currentWidth > 0 ? currentWidth : Screen.width;
        int h = currentHeight > 0 ? currentHeight : Screen.height;
        
        if (Screen.fullScreenMode != fsMode)
        {
            Screen.SetResolution(w, h, fsMode);
        }

        PlayerPrefs.SetInt(KeyScreenMode, modeIndex);

        OnGraphicsSettingsChanged?.Invoke();
    }

    public void SetQualityLevel(int level)
    {
        if (level >= 0 && level < QualitySettings.names.Length)
        {
            currentQualityLevel = level;
            if (QualitySettings.GetQualityLevel() != level)
            {
                QualitySettings.SetQualityLevel(level, false);
            }

            PlayerPrefs.SetInt(KeyQualityLevel, level);

            OnGraphicsSettingsChanged?.Invoke();
        }
    }

    public void SetVSync(bool enabled)
    {
        currentVSync = enabled ? 1 : 0;
        QualitySettings.vSyncCount = currentVSync;

        PlayerPrefs.SetInt(KeyVSync, currentVSync);

        OnGraphicsSettingsChanged?.Invoke();
    }

    public void SetFPSLimit(int fps)
    {
        currentFPSLimit = fps;
        Application.targetFrameRate = fps;

        PlayerPrefs.SetInt(KeyFPSLimit, fps);

        OnGraphicsSettingsChanged?.Invoke();
    }

    public void SetBrightness(float val)
    {
        currentBrightness = Mathf.Clamp(val, 1f, 100f);
        PlayerPrefs.SetFloat(KeyBrightness, currentBrightness);

        ApplyBrightnessOverlay();
        OnGraphicsSettingsChanged?.Invoke();
    }

    public void SetBloom(bool enabled)
    {
        currentBloom = enabled;
        PlayerPrefs.SetInt(KeyBloom, enabled ? 1 : 0);

        ApplyPostProcessingSettings();
        OnGraphicsSettingsChanged?.Invoke();
    }

    public void SetScreenShake(bool enabled)
    {
        currentScreenShake = enabled;
        PlayerPrefs.SetInt(KeyScreenShake, enabled ? 1 : 0);

        OnGraphicsSettingsChanged?.Invoke();
    }

    public void SetVignette(bool enabled)
    {
        currentVignette = enabled;
        PlayerPrefs.SetInt(KeyVignette, enabled ? 1 : 0);

        ApplyPostProcessingSettings();
        OnGraphicsSettingsChanged?.Invoke();
    }

    #endregion

    #region Public Getters

    public int GetWidth() => currentWidth > 0 ? currentWidth : Screen.width;
    public int GetHeight() => currentHeight > 0 ? currentHeight : Screen.height;
    public int GetScreenMode() => currentScreenMode;
    public int GetQualityLevel() => currentQualityLevel;
    public bool GetVSync() => currentVSync > 0;
    public int GetFPSLimit() => currentFPSLimit;
    public float GetBrightness()
    {
        float val = currentBrightness > 0 ? currentBrightness : 100f;
        if (val <= 1.5f && val > 0f) val *= 100f;
        return Mathf.Clamp(val, 1f, 100f);
    }
    public bool GetBloom() => currentBloom;
    public bool GetScreenShake() => currentScreenShake;
    public bool GetVignette() => currentVignette;

    public static List<ResolutionModel> GetSupportedResolutions()
    {
        if (cachedResolutions != null && cachedResolutions.Count > 0)
        {
            return cachedResolutions;
        }

        cachedResolutions = new List<ResolutionModel>();
        HashSet<string> seen = new HashSet<string>();

        UnityEngine.Resolution[] sysRes = Screen.resolutions;
        if (sysRes == null || sysRes.Length == 0)
        {
            cachedResolutions.Add(new ResolutionModel(1920, 1080));
            cachedResolutions.Add(new ResolutionModel(1600, 900));
            cachedResolutions.Add(new ResolutionModel(1366, 768));
            cachedResolutions.Add(new ResolutionModel(1280, 720));
            return cachedResolutions;
        }

        for (int i = 0; i < sysRes.Length; i++)
        {
            int w = sysRes[i].width;
            int h = sysRes[i].height;
            if (w >= 1024 && h >= 600)
            {
                string key = $"{w}x{h}";
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    cachedResolutions.Add(new ResolutionModel(w, h));
                }
            }
        }

        if (cachedResolutions.Count == 0)
        {
            cachedResolutions.Add(new ResolutionModel(Screen.width, Screen.height));
        }

        return cachedResolutions;
    }

    #endregion
}

[System.Serializable]
public struct ResolutionModel
{
    public int width;
    public int height;

    public ResolutionModel(int w, int h)
    {
        width = w;
        height = h;
    }

    public override string ToString()
    {
        return $"{width} x {height}";
    }
}

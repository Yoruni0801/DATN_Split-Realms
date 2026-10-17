using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AudioSettingsManager : MonoBehaviour
{
    private const string MasterKey = "MasterVolume";
    private const string BgmKey = "BGMVolume";
    private const string SfxKey = "SFXVolume";
    private const float MinVolumeThreshold = 0.001f;

    public static AudioSettingsManager Instance { get; private set; }

    [Header("Slider")]
    public Slider Master;
    public Slider BGM;
    public Slider SFX;

    [Header("Fill Image")]
    public Image masterFill;
    public Image bgmFill;
    public Image sfxFill;

    [Header("Gradient")]
    public Gradient masterGradient;
    public Gradient bgmGradient;
    public Gradient sfxGradient;

    [Header("Mute Icon")]
    public GameObject masterMuteIcon;
    public GameObject bgmMuteIcon;
    public GameObject sfxMuteIcon;

    [Header("Active Icon")]
    public GameObject masterActiveIcon;
    public GameObject bgmActiveIcon;
    public GameObject sfxActiveIcon;

    private bool isInitializingUI = false;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        InitUI();
    }

    public void InitUI(GameObject targetPanel = null)
    {
        isInitializingUI = true;

        if (targetPanel != null)
        {
            Slider[] sliders = targetPanel.GetComponentsInChildren<Slider>(true);
            Master = null;
            BGM = null;
            SFX = null;

            foreach (Slider s in sliders)
            {
                if (s == null) continue;
                string path = GetHierarchyPath(s.transform).ToLower();
                if (path.Contains("master") || path.Contains("tong") || path.Contains("all") || path.Contains("main"))
                {
                    if (Master == null) Master = s;
                }
                else if (path.Contains("bgm") || path.Contains("music") || path.Contains("nhac"))
                {
                    if (BGM == null) BGM = s;
                }
                else if (path.Contains("sfx") || path.Contains("effect") || path.Contains("am thanh"))
                {
                    if (SFX == null) SFX = s;
                }
            }
        }

        if (Master == null || BGM == null || SFX == null)
        {
#if UNITY_EDITOR
            AutoFindSliders();
#endif
        }

        EnsureAudioManager();

        float currentMaster = PlayerPrefs.GetFloat(MasterKey, 1f);
        float currentBgm = PlayerPrefs.GetFloat(BgmKey, 1f);
        float currentSfx = PlayerPrefs.GetFloat(SfxKey, 1f);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.UpdateVolume(currentMaster, currentBgm, currentSfx, AudioManager.Instance.IsMuted(), false);
        }

        SetupSlider(Master, currentMaster, OnMasterSliderChanged);
        SetupSlider(BGM, currentBgm, OnBgmSliderChanged);
        SetupSlider(SFX, currentSfx, OnSfxSliderChanged);

        isInitializingUI = false;
        UpdateFillColor();
    }

    private void EnsureAudioManager()
    {
        if (AudioManager.Instance == null)
        {
#if UNITY_2023_1_OR_NEWER
            AudioManager existing = Object.FindFirstObjectByType<AudioManager>();
#else
            AudioManager existing = Object.FindObjectOfType<AudioManager>();
#endif
            if (existing != null)
            {
                AudioManager.Instance = existing;
            }
        }
    }

    private void SetupSlider(Slider slider, float currentValue, UnityEngine.Events.UnityAction<float> onValueChanged)
    {
        if (slider == null) return;

        slider.onValueChanged.RemoveListener(onValueChanged);
        slider.value = currentValue;
        slider.onValueChanged.AddListener(onValueChanged);

        if (!slider.TryGetComponent<EventTrigger>(out var trigger))
        {
            trigger = slider.gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers.RemoveAll(entry => entry.eventID == EventTriggerType.PointerUp);

        EventTrigger.Entry pointerUpEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUpEntry.callback.AddListener((_) =>
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SaveSettings();
            }
        });
        trigger.triggers.Add(pointerUpEntry);
    }

    private void OnMasterSliderChanged(float val)
    {
        if (isInitializingUI) return;

        if (AudioManager.Instance != null)
        {
            float bgmVal = BGM != null ? BGM.value : 1f;
            float sfxVal = SFX != null ? SFX.value : 1f;
            bool muted = val <= 0.0001f;
            AudioManager.Instance.UpdateVolume(val, bgmVal, sfxVal, muted, false);
        }
        UpdateFillColor();
    }

    private void OnBgmSliderChanged(float val)
    {
        if (isInitializingUI) return;

        if (AudioManager.Instance != null)
        {
            float masterVal = Master != null ? Master.value : 1f;
            float sfxVal = SFX != null ? SFX.value : 1f;
            bool muted = masterVal <= 0.0001f;
            AudioManager.Instance.UpdateVolume(masterVal, val, sfxVal, muted, false);
        }
        UpdateFillColor();
    }

    private void OnSfxSliderChanged(float val)
    {
        if (isInitializingUI) return;

        if (AudioManager.Instance != null)
        {
            float masterVal = Master != null ? Master.value : 1f;
            float bgmVal = BGM != null ? BGM.value : 1f;
            bool muted = masterVal <= 0.0001f;
            AudioManager.Instance.UpdateVolume(masterVal, bgmVal, val, muted, false);
        }
        UpdateFillColor();
    }

    private void UpdateFillColor()
    {
        UpdateSliderVisual(Master, masterFill, masterGradient, masterActiveIcon, masterMuteIcon);
        UpdateSliderVisual(BGM, bgmFill, bgmGradient, bgmActiveIcon, bgmMuteIcon);
        UpdateSliderVisual(SFX, sfxFill, sfxGradient, sfxActiveIcon, sfxMuteIcon);
    }

    private void UpdateSliderVisual(Slider slider, Image fill, Gradient gradient, GameObject activeIcon, GameObject muteIcon)
    {
        if (slider == null) return;

        if (fill != null && gradient != null)
        {
            fill.color = gradient.Evaluate(slider.value);
        }

        bool isActive = slider.value > (slider.minValue + MinVolumeThreshold);
        if (activeIcon != null) activeIcon.SetActive(isActive);
        if (muteIcon != null) muteIcon.SetActive(!isActive);
    }

    private string GetHierarchyPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Find Sliders & Bind Icons")]
    public void AutoFindSliders()
    {
        if (Master == null || BGM == null || SFX == null)
        {
#if UNITY_2023_1_OR_NEWER
            Slider[] sliders = Object.FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            Slider[] sliders = Object.FindObjectsOfType<Slider>(true);
#endif
            foreach (Slider s in sliders)
            {
                if (s == null) continue;
                string path = GetHierarchyPath(s.transform).ToLower();
                if (path.Contains("master") || path.Contains("tong") || path.Contains("all") || path.Contains("main"))
                {
                    if (Master == null) Master = s;
                }
                else if (path.Contains("bgm") || path.Contains("music") || path.Contains("nhac"))
                {
                    if (BGM == null) BGM = s;
                }
                else if (path.Contains("sfx") || path.Contains("effect") || path.Contains("am thanh"))
                {
                    if (SFX == null) SFX = s;
                }
            }
        }

        AutoBindIconsForSlider(Master, ref masterMuteIcon, ref masterActiveIcon, "mute 0", "mute_0", "mastermute", "mute");
        AutoBindIconsForSlider(BGM, ref bgmMuteIcon, ref bgmActiveIcon, "mute 1", "mute_1", "bgmmute");
        AutoBindIconsForSlider(SFX, ref sfxMuteIcon, ref sfxActiveIcon, "mute 2", "mute_2", "sfxmute");

        EnsureSliderInteractable(Master);
        EnsureSliderInteractable(BGM);
        EnsureSliderInteractable(SFX);
    }

    private void AutoBindIconsForSlider(Slider slider, ref GameObject muteIcon, ref GameObject activeIcon, params string[] muteKeywords)
    {
        if (slider == null) return;
        Transform parentBox = slider.transform.parent;
        if (parentBox == null) return;

        List<Transform> candidateList = new List<Transform>();
        candidateList.AddRange(parentBox.GetComponentsInChildren<Transform>(true));
        if (parentBox.parent != null)
        {
            candidateList.AddRange(parentBox.parent.GetComponentsInChildren<Transform>(true));
        }

        foreach (Transform t in candidateList)
        {
            if (t == null || t == slider.transform || t.IsChildOf(slider.transform)) continue;
            string n = t.name.ToLower();

            if (muteIcon == null)
            {
                foreach (string kw in muteKeywords)
                {
                    if (n.Contains(kw) && !n.Contains("(1)"))
                    {
                        muteIcon = t.gameObject;
                        break;
                    }
                }
            }
        }

        if (muteIcon == null)
        {
            foreach (Transform t in candidateList)
            {
                if (t == null || t == slider.transform || t.IsChildOf(slider.transform)) continue;
                string n = t.name.ToLower();
                foreach (string kw in muteKeywords)
                {
                    if (n.Contains(kw))
                    {
                        muteIcon = t.gameObject;
                        break;
                    }
                }
            }
        }

        foreach (Transform t in candidateList)
        {
            if (t == null || t == slider.transform || t.IsChildOf(slider.transform)) continue;
            if (muteIcon != null && t.gameObject == muteIcon) continue;

            string n = t.name.ToLower();
            if (activeIcon == null)
            {
                if (n.Contains("(1)") || n.Contains("active") || n.Contains("image") || n.Contains("icon") || n.Contains("cd") || n.Contains("bell") || n.Contains("loa"))
                {
                    activeIcon = t.gameObject;
                    break;
                }
            }
        }

        if (activeIcon == null)
        {
            foreach (Transform t in candidateList)
            {
                if (t != null && t.gameObject != slider.gameObject && !t.IsChildOf(slider.transform) && (muteIcon == null || t.gameObject != muteIcon))
                {
                    activeIcon = t.gameObject;
                    break;
                }
            }
        }
    }

    public void EnsureSliderInteractable(Slider slider)
    {
        if (slider == null) return;

        slider.interactable = true;
        slider.enabled = true;
        slider.gameObject.SetActive(true);

        Graphic[] graphics = slider.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic g in graphics)
        {
            if (g != null)
            {
                g.raycastTarget = true;
                g.enabled = true;
            }
        }

        CanvasGroup[] canvasGroups = slider.GetComponentsInParent<CanvasGroup>(true);
        foreach (CanvasGroup cg in canvasGroups)
        {
            if (cg != null)
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
                cg.ignoreParentGroups = false;
            }
        }
    }
#endif
}

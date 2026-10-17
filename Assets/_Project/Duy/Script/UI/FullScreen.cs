using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FullScreen : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown tmpDropdown;

    [Header("Cấu hình lưu trữ")]
    [SerializeField] private string saveKey = "ScreenMode";
    [SerializeField] private int defaultIndex = 0; // 0 = Fullscreen, 1 = Windowed

    private void Start()
    {
        InitializeDropdown();
    }

    private void InitializeDropdown()
    {
        int savedIndex = PlayerPrefs.GetInt(saveKey, defaultIndex);

        ApplyScreenMode(savedIndex);

        if (tmpDropdown != null)
        {
            tmpDropdown.value = savedIndex;
            tmpDropdown.onValueChanged.RemoveAllListeners();
            tmpDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }
    }

    private void OnDropdownValueChanged(int index)
    {
        ApplyScreenMode(index);

        PlayerPrefs.SetInt(saveKey, index);
        PlayerPrefs.Save();

        if (tmpDropdown != null && tmpDropdown.value != index) tmpDropdown.value = index;
    }

    public void ApplyScreenMode(int index)
    {
        switch (index)
        {
            case 0:
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                Screen.fullScreen = true;
                break;
            case 1:
                Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
                break;
        }

        #if UNITY_EDITOR
        Debug.Log($"[FullScreen] Đã chuyển sang chế độ: {(index == 0 ? "Full Screen" : "Windowed")}");
        #endif
    }
}


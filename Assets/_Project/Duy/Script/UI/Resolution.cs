using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class Resolution : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown tmpDropdown;

    [Header("Cấu hình lưu trữ")]
    [SerializeField] private string widthSaveKey = "ResolutionWidth";
    [SerializeField] private string heightSaveKey = "ResolutionHeight";

    private UnityEngine.Resolution[] systemResolutions;
    private List<UnityEngine.Resolution> filteredResolutions;

    private void Start()
    {
        InitializeDropdown();
    }

    private void InitializeDropdown()
    {
        // Lấy tất cả độ phân giải hệ thống hỗ trợ
        systemResolutions = Screen.resolutions;
        filteredResolutions = new List<UnityEngine.Resolution>();

        if (tmpDropdown != null)
        {
            tmpDropdown.ClearOptions();
        }

        List<string> options = new List<string>();
        HashSet<string> uniqueResSet = new HashSet<string>();

        // Lọc các độ phân giải trùng lặp (chỉ giữ lại cặp Width x Height duy nhất)
        for (int i = 0; i < systemResolutions.Length; i++)
        {
            UnityEngine.Resolution res = systemResolutions[i];
            string resText = $"{res.width} x {res.height}";

            if (!uniqueResSet.Contains(resText))
            {
                uniqueResSet.Add(resText);
                filteredResolutions.Add(res);
            }
        }

        // Trường hợp khẩn cấp nếu hệ thống không trả về độ phân giải nào (ví dụ WebGL)
        if (filteredResolutions.Count == 0)
        {
            UnityEngine.Resolution currentRes = Screen.currentResolution;
            filteredResolutions.Add(currentRes);
        }

        // Tạo danh sách text hiển thị cho Dropdown
        int currentResolutionIndex = 0;
        
        // Lấy độ phân giải đã lưu hoặc mặc định là độ phân giải hiện tại của màn hình
        int savedWidth = PlayerPrefs.GetInt(widthSaveKey, Screen.currentResolution.width);
        int savedHeight = PlayerPrefs.GetInt(heightSaveKey, Screen.currentResolution.height);

        for (int i = 0; i < filteredResolutions.Count; i++)
        {
            UnityEngine.Resolution res = filteredResolutions[i];
            options.Add($"{res.width} x {res.height}");

            // Tìm độ phân giải khớp với độ phân giải đang sử dụng/đã lưu
            if (res.width == savedWidth && res.height == savedHeight)
            {
                currentResolutionIndex = i;
            }
        }

        if (tmpDropdown != null)
        {
            tmpDropdown.AddOptions(options);
            tmpDropdown.value = currentResolutionIndex;
            tmpDropdown.RefreshShownValue();

            tmpDropdown.onValueChanged.RemoveAllListeners();
            tmpDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }

        // Áp dụng độ phân giải ban đầu
        ApplyResolution(savedWidth, savedHeight);
    }

    private void OnDropdownValueChanged(int index)
    {
        if (index < 0 || index >= filteredResolutions.Count) return;

        UnityEngine.Resolution selectedRes = filteredResolutions[index];
        ApplyResolution(selectedRes.width, selectedRes.height);

        // Lưu cài đặt vào PlayerPrefs
        PlayerPrefs.SetInt(widthSaveKey, selectedRes.width);
        PlayerPrefs.SetInt(heightSaveKey, selectedRes.height);
        PlayerPrefs.Save();
    }

    public void ApplyResolution(int width, int height)
    {
        // Giữ nguyên chế độ toàn màn hình hiện tại
        Screen.SetResolution(width, height, Screen.fullScreen);

        #if UNITY_EDITOR
        Debug.Log($"[Resolution] Đã đổi độ phân giải sang: {width} x {height} (Fullscreen: {Screen.fullScreen})");
        #endif
    }
}


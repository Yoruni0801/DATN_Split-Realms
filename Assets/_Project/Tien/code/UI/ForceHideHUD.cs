using UnityEngine;

/// <summary>
/// Thêm script này vào GameObject (ví dụ: Timeline/PlayableDirector) để ép KeepObjectOnLoad ẩn HUD UI.
/// Dùng trong trường hợp chức năng tự động ẩn UI của KeepObjectOnLoad không hoạt động (ví dụ: Timeline đặc biệt).
/// </summary>
public class ForceHideHUD : MonoBehaviour
{
    private void OnEnable()
    {
        if (KeepObjectOnLoad.Instance != null)
        {
            KeepObjectOnLoad.Instance.RequestForceHideUI(this);
        }
    }

    private void OnDisable()
    {
        if (KeepObjectOnLoad.Instance != null)
        {
            KeepObjectOnLoad.Instance.RemoveForceHideUIRequest(this);
        }
    }

    private void OnDestroy()
    {
        if (KeepObjectOnLoad.Instance != null)
        {
            KeepObjectOnLoad.Instance.RemoveForceHideUIRequest(this);
        }
    }
}

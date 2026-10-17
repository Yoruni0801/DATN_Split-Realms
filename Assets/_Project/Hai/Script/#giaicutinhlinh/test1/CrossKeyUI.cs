using UnityEngine;

/// <summary>
/// Cụm Phím Hiển Thị Động (Dynamic Cross-Layout UI)
/// Đặt tại tâm (giao điểm Thanh Target và đường Baseline).
/// Mặc định cả 4 icon đều ẩn. Chỉ 1 icon được bật sáng mỗi lượt.
/// </summary>
public class CrossKeyUI : MonoBehaviour
{
    [Header("Gán 4 GameObject icon tương ứng trong Inspector")]
    [SerializeField] private GameObject iconW; // Trên
    [SerializeField] private GameObject iconS; // Dưới
    [SerializeField] private GameObject iconA; // Trái
    [SerializeField] private GameObject iconD; // Phải

    private GameObject currentActiveIcon;

    private void Awake()
    {
        HideAll();
    }

    public void ShowKey(KeyCode key)
    {
        HideAll();
        switch (key)
        {
            case KeyCode.W: iconW.SetActive(true); currentActiveIcon = iconW; break;
            case KeyCode.S: iconS.SetActive(true); currentActiveIcon = iconS; break;
            case KeyCode.A: iconA.SetActive(true); currentActiveIcon = iconA; break;
            case KeyCode.D: iconD.SetActive(true); currentActiveIcon = iconD; break;
        }
    }

    public void HideAll()
    {
        iconW.SetActive(false);
        iconS.SetActive(false);
        iconA.SetActive(false);
        iconD.SetActive(false);
        currentActiveIcon = null;
    }

    public void SetActiveScale(float scale)
    {
        if (currentActiveIcon != null)
        {
            currentActiveIcon.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    public void ResetAllScales()
    {
        iconW.transform.localScale = Vector3.one;
        iconS.transform.localScale = Vector3.one;
        iconA.transform.localScale = Vector3.one;
        iconD.transform.localScale = Vector3.one;
    }

    public Vector3 GetActiveKeyWorldPosition()
    {
        if (currentActiveIcon != null)
        {
            return currentActiveIcon.transform.position;
        }
        return transform.position;
    }
}

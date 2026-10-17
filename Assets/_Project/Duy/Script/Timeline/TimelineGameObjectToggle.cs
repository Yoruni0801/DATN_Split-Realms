using UnityEngine;

[AddComponentMenu("Timeline/Timeline GameObject Toggle")]
[DisallowMultipleComponent]
public sealed class TimelineGameObjectToggle : MonoBehaviour
{
    [Tooltip("Cac GameObject se duoc bat/tat. Neu danh sach rong, script se tac dong len chinh GameObject nay.")]
    [SerializeField] private GameObject[] targets;

    [Header("Initial State (Optional)")]
    [Tooltip("Tu dong dat trang thai khi scene bat dau.")]
    [SerializeField] private bool applyInitialStateOnAwake;

    [SerializeField] private bool initialActiveState = true;

    private void Awake()
    {
        if (applyInitialStateOnAwake)
        {
            SetActive(initialActiveState);
        }
    }

    /// <summary>
    /// Hien tat ca GameObject da gan. Co the goi tu Timeline Signal Receiver.
    /// </summary>
    public void Show()
    {
        SetActive(true);
    }

    /// <summary>
    /// An tat ca GameObject da gan. Co the goi tu Timeline Signal Receiver.
    /// </summary>
    public void Hide()
    {
        SetActive(false);
    }

    /// <summary>
    /// Dao trang thai hien tai cua tung GameObject.
    /// </summary>
    public void Toggle()
    {
        if (HasTargets())
        {
            for (int i = 0; i < targets.Length; i++)
            {
                GameObject target = targets[i];
                if (target != null)
                {
                    target.SetActive(!target.activeSelf);
                }
            }

            return;
        }

        gameObject.SetActive(!gameObject.activeSelf);
    }

    /// <summary>
    /// Dat trang thai bat/tat. Ham bool nay cung dung duoc voi UnityEvent.
    /// </summary>
    public void SetActive(bool isActive)
    {
        if (HasTargets())
        {
            for (int i = 0; i < targets.Length; i++)
            {
                GameObject target = targets[i];
                if (target != null && target.activeSelf != isActive)
                {
                    target.SetActive(isActive);
                }
            }

            return;
        }

        if (gameObject.activeSelf != isActive)
        {
            gameObject.SetActive(isActive);
        }
    }

    private bool HasTargets()
    {
        return targets != null && targets.Length > 0;
    }
}

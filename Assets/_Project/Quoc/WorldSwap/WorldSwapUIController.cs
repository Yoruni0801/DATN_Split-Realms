using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class WorldSwapUIController : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("Object chứa toàn bộ UI cần tắt/bật (nút bấm, cooldown, v.v.)")]
    [SerializeField] private GameObject rootObject;
    [Tooltip("Ảnh hiển thị icon của tính năng World Swap")]
    [SerializeField] private Image iconImage;
    [Tooltip("Object hiển thị ổ khóa đè lên (nếu có)")]
    [SerializeField] private GameObject lockOverlay; 

    [Header("Sprites")]
    [Tooltip("Sprite khi kĩ năng đổi map bị khóa (trạng thái 1)")]
    [SerializeField] private Sprite lockedSprite;
    [Tooltip("Sprite khi ở Map 1 (hiện sprite 1 - trạng thái 2)")]
    [SerializeField] private Sprite worldASprite;
    [Tooltip("Sprite khi ở Map 2 (hiện map 2 - trạng thái 3)")]
    [SerializeField] private Sprite worldBSprite;

    [Header("Visuals")]
    [Tooltip("Màu khi kĩ năng bị khóa")]
    [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [Tooltip("Màu bình thường khi đã mở khóa")]
    [SerializeField] private Color unlockedColor = Color.white;
    [Tooltip("Ảnh đen mờ hiển thị giống hồi chiêu khi đang swap")]
    [SerializeField] private Image cooldownImage;

    [Header("Animation Settings")]
    [SerializeField] private float slideDuration = 0.3f;

    private bool hasSubscribed = false;
    private Tween cooldownTween;

    private void Start()
    {
        SubscribeEvents();
        RefreshUI();
    }

    private void OnEnable()
    {
        SubscribeEvents();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        cooldownTween?.Kill();
    }

    private void SubscribeEvents()
    {
        if (hasSubscribed) return;

        WorldSwapManager.GlobalWorldStateApplied += OnWorldStateApplied;
        WorldSwapManager.GlobalSwapUnlockStateChanged += OnGlobalSwapUnlockStateChanged;
        WorldSwapManager.GlobalBusyChanged += OnBusyChanged;
        WorldSwapManager.GlobalSwapDenied += OnSwapDenied;

        hasSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!hasSubscribed) return;

        WorldSwapManager.GlobalWorldStateApplied -= OnWorldStateApplied;
        WorldSwapManager.GlobalSwapUnlockStateChanged -= OnGlobalSwapUnlockStateChanged;
        WorldSwapManager.GlobalBusyChanged -= OnBusyChanged;
        WorldSwapManager.GlobalSwapDenied -= OnSwapDenied;

        hasSubscribed = false;
    }

    private void OnWorldStateApplied(bool isInWorldA)
    {
        if (iconImage == null) return;
        if (!WorldSwapManager.IsAnyUnlockedGlobally) return;

        iconImage.sprite = isInWorldA ? worldASprite : worldBSprite;

        iconImage.transform.DOKill();
        iconImage.transform.localScale = Vector3.one;
        iconImage.transform.DOPunchScale(new Vector3(-0.2f, -0.2f, 0f), 0.15f, 1, 0.5f);
    }

    private void OnGlobalSwapUnlockStateChanged(bool isUnlocked)
    {
        RefreshUI();
    }

    private void OnBusyChanged(bool isBusy)
    {
        if (cooldownImage != null)
        {
            cooldownTween?.Kill();

            if (isBusy)
            {
                cooldownTween = cooldownImage.DOFillAmount(1f, slideDuration).SetEase(Ease.OutQuad);
            }
            else
            {
                cooldownTween = cooldownImage.DOFillAmount(0f, slideDuration).SetEase(Ease.InQuad);
            }
        }
    }

    private void OnSwapDenied()
    {
        if (iconImage == null) return;

        iconImage.DOKill();
        iconImage.DOColor(Color.red, 0.1f).OnComplete(() => {
            iconImage.DOColor(WorldSwapManager.IsAnyUnlockedGlobally ? unlockedColor : lockedColor, 0.2f);
        });

        iconImage.transform.DOKill();
        iconImage.transform.localScale = Vector3.one;
        iconImage.transform.DOPunchScale(new Vector3(0.2f, 0.2f, 0f), 0.3f, 10, 1);
    }

    private void RefreshUI()
    {
        bool isUnlocked = WorldSwapManager.IsAnyUnlockedGlobally;

        if (rootObject != null)
        {
            rootObject.SetActive(isUnlocked);
        }
        else if (iconImage != null)
        {
            iconImage.gameObject.SetActive(isUnlocked);
        }

        if (!isUnlocked || iconImage == null) return;

        if (lockOverlay != null) lockOverlay.SetActive(false);
        iconImage.color = unlockedColor;

        // Try to get current map state from active manager if possible
#if UNITY_2023_1_OR_NEWER
        var activeManager = FindFirstObjectByType<WorldSwapManager>();
#else
        var activeManager = FindObjectOfType<WorldSwapManager>();
#endif
        if (activeManager != null)
        {
            iconImage.sprite = activeManager.IsInWorldA ? worldASprite : worldBSprite;
            if (cooldownImage != null)
            {
                cooldownTween?.Kill();
                cooldownImage.fillAmount = activeManager.IsBusy ? 1f : 0f;
            }
        }
    }
}

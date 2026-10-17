using System;
using UnityEngine;

/// <summary>
/// Script độc lập dùng để thiết lập nguyên tố (FairyElement: Fire, Water, Wind, Earth) cho Tinh Linh (Sunflower Fairy)
/// một cách trực tiếp mà không cần phụ thuộc vào logic tuần tự (không cần phải có Water mới hiện Fire).
/// Script này hoàn toàn không làm thay đổi hay can thiệp code cũ của các script có sẵn.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Fairy/Fairy Standalone Element Setter")]
public sealed class FairyStandaloneElementSetter : MonoBehaviour
{
    [Header("Standalone Fairy Element Setup")]
    [Tooltip("Hệ nguyên tố muốn gán trực tiếp cho Tinh Linh này (Fire, Water, Wind, Earth)")]
    [SerializeField] private FairyElement targetElement = FairyElement.Fire;

    [Header("Apply Trigger Settings")]
    [Tooltip("Tự động áp dụng hệ ngay khi Awake")]
    [SerializeField] private bool applyOnAwake = true;

    [Tooltip("Tự động áp dụng hệ ngay khi OnEnable")]
    [SerializeField] private bool applyOnEnable = true;

    [Tooltip("Tự động áp dụng hệ khi Start")]
    [SerializeField] private bool applyOnStart = true;

    [Header("System & Unlock Integration")]
    [Tooltip("Tự động mở khóa nguyên tố này trong FairyElementUnlockController để có thể dùng ngay kỹ năng của hệ mà không cần qua mở khóa trước đó")]
    [SerializeField] private bool autoUnlockInSystem = true;

    [Tooltip("Có đồng bộ hệ của Tinh Linh sang cho Player ngay khi xuất hiện không")]
    [SerializeField] private bool syncToPlayer = false;

    [Tooltip("Duy trì cố định hệ này (ngăn không cho các sự kiện khác vô tình ghi đè hệ)")]
    [SerializeField] private bool enforceElement = true;

    [Header("Component References (Optional - Auto-resolved if null)")]
    [SerializeField] private FairyElementVfxController vfxController;
    [SerializeField] private FairyPlayerCompanionController companionController;
    [SerializeField] private FairyElementUnlockController unlockController;

    public FairyElement TargetElement
    {
        get => targetElement;
        set => SetElement(value);
    }

    private void Awake()
    {
        ResolveComponents();
        if (applyOnAwake)
        {
            ApplyElement(targetElement);
        }
    }

    private void OnEnable()
    {
        ResolveComponents();
        if (applyOnEnable)
        {
            ApplyElement(targetElement);
        }
    }

    private void Start()
    {
        ResolveComponents();
        if (applyOnStart)
        {
            ApplyElement(targetElement);
        }
    }

    private void LateUpdate()
    {
        if (enforceElement)
        {
            EnsureCorrectElementApplied();
        }
    }

    private void OnValidate()
    {
        // Cho phép xem trước VFX / Material đổi ngay trong Unity Editor khi chỉnh Inspector
        ResolveComponents();
        if (vfxController != null)
        {
            vfxController.SetElementImmediate(targetElement);
        }
    }

    /// <summary>
    /// Tìm và gán các component cần thiết trên GameObject hoặc trong hierarchy
    /// </summary>
    private void ResolveComponents()
    {
        if (vfxController == null)
        {
            vfxController = GetComponent<FairyElementVfxController>()
                ?? GetComponentInChildren<FairyElementVfxController>(true)
                ?? GetComponentInParent<FairyElementVfxController>();
        }

        if (companionController == null)
        {
            companionController = GetComponent<FairyPlayerCompanionController>()
                ?? GetComponentInChildren<FairyPlayerCompanionController>(true)
                ?? GetComponentInParent<FairyPlayerCompanionController>();
        }

        if (unlockController == null)
        {
            unlockController = GetComponent<FairyElementUnlockController>()
                ?? GetComponentInChildren<FairyElementUnlockController>(true)
                ?? GetComponentInParent<FairyElementUnlockController>();

            if (unlockController == null && Application.isPlaying)
            {
                var player = PlayerRegistry.Current;
                if (player != null)
                {
                    unlockController = player.GetComponent<FairyElementUnlockController>();
                }
            }
        }
    }

    /// <summary>
    /// Đổi sang hệ mong muốn ngay lập tức (hiện VFX, đổi material, đổi animation, mở khóa skill)
    /// </summary>
    public void SetElement(FairyElement newElement)
    {
        targetElement = newElement;
        ApplyElement(targetElement);
    }

    public void SetElementIndex(int elementIndex)
    {
        SetElement((FairyElement)Mathf.Clamp(elementIndex, 0, 3));
    }

    [ContextMenu("Set Element: Fire (Hỏa)")]
    public void SetFire() => SetElement(FairyElement.Fire);

    [ContextMenu("Set Element: Water (Thủy)")]
    public void SetWater() => SetElement(FairyElement.Water);

    [ContextMenu("Set Element: Wind (Phong)")]
    public void SetWind() => SetElement(FairyElement.Wind);

    [ContextMenu("Set Element: Earth (Thổ)")]
    public void SetEarth() => SetElement(FairyElement.Earth);

    private void ApplyElement(FairyElement element)
    {
        ResolveComponents();

        // 1. Đổi visual / VFX / Material / Glow của Tinh Linh sang hệ mới ngay lập tức
        if (vfxController != null)
        {
            vfxController.SetElementImmediate(element);
        }

        // 2. Cập nhật hệ trên Companion Controller
        if (companionController != null)
        {
            companionController.SetElement(element, syncToPlayer);
        }

        // 3. Mở khóa nguyên tố trong hệ thống Unlock để có thể cast skill của hệ này
        if (autoUnlockInSystem)
        {
            UnlockElementInSystem(element);
        }

        // 4. Đồng bộ sang Player nếu được bật
        if (syncToPlayer && Application.isPlaying)
        {
            var player = PlayerRegistry.Current ?? (companionController != null ? companionController.ActivePlayer : null);
            if (player != null)
            {
                player.TryChangeElement(MapFairyElementToSkillElement(element));
            }
        }
    }

    private void EnsureCorrectElementApplied()
    {
        if (vfxController != null && vfxController.Element != targetElement)
        {
            vfxController.SetElementImmediate(targetElement);
        }

        if (companionController != null && companionController.CurrentElement != targetElement)
        {
            companionController.SetElement(targetElement, false);
        }
    }

    private void UnlockElementInSystem(FairyElement element)
    {
        SkillElementType skillElement = MapFairyElementToSkillElement(element);

        if (unlockController != null)
        {
            unlockController.UnlockElement(skillElement);
        }

#if UNITY_2023_1_OR_NEWER
        var allUnlockControllers = FindObjectsByType<FairyElementUnlockController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var allUnlockControllers = FindObjectsOfType<FairyElementUnlockController>();
#endif
        if (allUnlockControllers != null)
        {
            for (int i = 0; i < allUnlockControllers.Length; i++)
            {
                if (allUnlockControllers[i] != null)
                {
                    allUnlockControllers[i].UnlockElement(skillElement);
                }
            }
        }
    }

    private static SkillElementType MapFairyElementToSkillElement(FairyElement fairyElement)
    {
        switch (fairyElement)
        {
            case FairyElement.Fire:
                return SkillElementType.Fire;
            case FairyElement.Earth:
                return SkillElementType.Earth;
            case FairyElement.Wind:
                return SkillElementType.Wind;
            case FairyElement.Water:
            default:
                return SkillElementType.Water;
        }
    }
}


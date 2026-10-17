using UnityEngine;

[System.Serializable]
public struct FairyTransitionVfxSetting
{
    public GameObject prefab;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale;
    public float lifetime;
}

[System.Serializable]
public struct FairyElementTransitionSetting
{
    public FairyTransitionVfxSetting startVfx;
    public FairyTransitionVfxSetting endVfx;

    public bool HasTransition => startVfx.prefab != null || endVfx.prefab != null;
}

[CreateAssetMenu(fileName = "Fairy Element Transition Profile", menuName = "Duy/Game Data/Fairy/Element Transition Profile")]
public sealed class FairyElementTransitionProfile : ScriptableObject
{
    [SerializeField] private float switchDelay = 0.35f;
    [SerializeField] private float cleanupDelay = 1.2f;
    [SerializeField] private FairyElementTransitionSetting water = DefaultElementSetting;
    [SerializeField] private FairyElementTransitionSetting wind = DefaultElementSetting;
    [SerializeField] private FairyElementTransitionSetting fire = DefaultElementSetting;
    [SerializeField] private FairyElementTransitionSetting earth = DefaultElementSetting;

    private static FairyTransitionVfxSetting DefaultSetting => new FairyTransitionVfxSetting
    {
        localScale = Vector3.one,
        lifetime = 1.2f
    };

    private static FairyElementTransitionSetting DefaultElementSetting => new FairyElementTransitionSetting
    {
        startVfx = DefaultSetting,
        endVfx = DefaultSetting
    };

    public float SwitchDelay => switchDelay;
    public float CleanupDelay => cleanupDelay;
    public bool HasTransition => water.HasTransition || wind.HasTransition || fire.HasTransition || earth.HasTransition;

    public FairyElementTransitionSetting GetSetting(FairyElement element)
    {
        switch (element)
        {
            case FairyElement.Wind:
                return wind;

            case FairyElement.Fire:
                return fire;

            case FairyElement.Earth:
                return earth;

            default:
                return water;
        }
    }

    private void OnValidate()
    {
        switchDelay = Mathf.Max(0f, switchDelay);
        cleanupDelay = Mathf.Max(0.05f, cleanupDelay);
        EnsureValid(ref water);
        EnsureValid(ref wind);
        EnsureValid(ref fire);
        EnsureValid(ref earth);
    }

    private static void EnsureValid(ref FairyElementTransitionSetting setting)
    {
        EnsureValid(ref setting.startVfx);
        EnsureValid(ref setting.endVfx);
    }

    private static void EnsureValid(ref FairyTransitionVfxSetting setting)
    {
        if (setting.localScale == Vector3.zero)
        {
            setting.localScale = Vector3.one;
        }

        setting.lifetime = Mathf.Max(0f, setting.lifetime);
    }
}

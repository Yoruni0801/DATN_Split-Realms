using UnityEngine;

[CreateAssetMenu(fileName = "Fairy Element VFX Profile", menuName = "Duy/Game Data/Fairy/Element VFX Profile")]
public sealed class FairyElementVfxProfile : ScriptableObject
{
    [SerializeField] private FairyElementVfxSetting water = DefaultSetting;
    [SerializeField] private FairyElementVfxSetting wind = DefaultSetting;
    [SerializeField] private FairyElementVfxSetting fire = DefaultSetting;
    [SerializeField] private FairyElementVfxSetting earth = DefaultSetting;

    private static FairyElementVfxSetting DefaultSetting => new FairyElementVfxSetting
    {
        localScale = Vector3.one
    };

    public FairyElementVfxSetting GetSetting(FairyElement element)
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
        EnsureValidScale(ref water);
        EnsureValidScale(ref wind);
        EnsureValidScale(ref fire);
        EnsureValidScale(ref earth);
    }

    private static void EnsureValidScale(ref FairyElementVfxSetting setting)
    {
        if (setting.localScale == Vector3.zero)
        {
            setting.localScale = Vector3.one;
        }
    }
}

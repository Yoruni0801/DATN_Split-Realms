namespace Tien.AdvancedLoading.Core
{
    public enum LoadingState
    {
        Idle,
        FadeOutCurrentScene,
        UnloadCurrentScene,
        FadeInLoadingUI,
        LoadingAsync,
        ActivateNewScene,
        FadeInNewScene,
        Complete
    }
}

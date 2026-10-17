using UnityEngine;
using Tien.AdvancedLoading.Core;

public class SceneChanger : MonoBehaviour
{
    public string sceneName;

    public void ChangeScene()
    {
        StartScene();
    }

    public void StartScene()
    {
        TimelinePlayStateManager.BeginSceneChange(sceneName, false);
        SceneLoader.Load(sceneName);
    }

    public void ContinueScene()
    {
        TimelinePlayStateManager.BeginSceneChange(sceneName, true);
        SceneLoader.Load(sceneName);
    }
}

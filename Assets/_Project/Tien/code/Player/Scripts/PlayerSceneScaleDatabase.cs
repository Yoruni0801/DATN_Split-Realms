using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ScenePlayerScaleEntry
{
    public string sceneName;
    public float scale = 1f;
}

[CreateAssetMenu(
    fileName = "PlayerSceneScaleDatabase",
    menuName = "Duy/Player Scene Scale Database")]
public class PlayerSceneScaleDatabase : ScriptableObject
{
    private const float MinValidScale = 0.1f;

    [SerializeField] private List<ScenePlayerScaleEntry> sceneScales = new List<ScenePlayerScaleEntry>();

    private readonly Dictionary<string, float> scaleByScene = new Dictionary<string, float>();
    private bool cacheBuilt;

    public float GetScale(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return 1f;
        }

        EnsureCache();
        return scaleByScene.TryGetValue(sceneName.Trim(), out float scale)
            ? Mathf.Max(MinValidScale, scale)
            : 1f;
    }

    private void OnEnable()
    {
        RebuildCache();
    }

    private void OnValidate()
    {
        RebuildCache();
    }

    private void EnsureCache()
    {
        if (!cacheBuilt)
        {
            RebuildCache();
        }
    }

    private void RebuildCache()
    {
        scaleByScene.Clear();
        cacheBuilt = true;

        if (sceneScales == null)
        {
            return;
        }

        HashSet<string> duplicateWarnings = new HashSet<string>();
        for (int i = 0; i < sceneScales.Count; i++)
        {
            ScenePlayerScaleEntry entry = sceneScales[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.sceneName))
            {
                continue;
            }

            string sceneName = entry.sceneName.Trim();
            if (scaleByScene.ContainsKey(sceneName))
            {
                if (duplicateWarnings.Add(sceneName))
                {
                    Debug.LogWarning($"Duplicate player scene scale entry for scene '{sceneName}'. The first value will be used.", this);
                }

                continue;
            }

            scaleByScene.Add(sceneName, Mathf.Max(MinValidScale, entry.scale));
        }
    }
}

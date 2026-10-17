using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class ScenePlayerJumpEntry
{
    public string sceneName;
    public float jumpForce = 10f;
}

public sealed class PlayerSceneJumpController : MonoBehaviour
{
    [SerializeField] private float defaultJumpForce = 10f;
    [SerializeField] private List<ScenePlayerJumpEntry> sceneJumpForces = new List<ScenePlayerJumpEntry>
    {
        new ScenePlayerJumpEntry { sceneName = "TF", jumpForce = 6f },
        new ScenePlayerJumpEntry { sceneName = "MapHang", jumpForce = 10f },
        new ScenePlayerJumpEntry { sceneName = "Map3Bang", jumpForce = 13f },
        new ScenePlayerJumpEntry { sceneName = "Map5", jumpForce = 14f }
    };

    public float CurrentJumpForce { get; private set; } = 10f;
    public event Action<float> JumpForceChanged;

    private void Awake()
    {
        ApplyJumpForceForActiveScene();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        ApplyJumpForceForActiveScene();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
    }

    public void ApplyJumpForceForActiveScene()
    {
        float jumpForce = ResolveJumpForce(SceneManager.GetActiveScene().name);
        if (!Mathf.Approximately(CurrentJumpForce, jumpForce))
        {
            CurrentJumpForce = jumpForce;
            JumpForceChanged?.Invoke(CurrentJumpForce);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene == SceneManager.GetActiveScene())
        {
            ApplyJumpForceForActiveScene();
        }
    }

    private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        ApplyJumpForceForActiveScene();
    }

    private float ResolveJumpForce(string sceneName)
    {
        for (int i = 0; i < sceneJumpForces.Count; i++)
        {
            ScenePlayerJumpEntry entry = sceneJumpForces[i];
            if (entry != null && string.Equals(entry.sceneName?.Trim(), sceneName, StringComparison.Ordinal))
            {
                return Mathf.Max(0f, entry.jumpForce);
            }
        }

        return Mathf.Max(0f, defaultJumpForce);
    }
}

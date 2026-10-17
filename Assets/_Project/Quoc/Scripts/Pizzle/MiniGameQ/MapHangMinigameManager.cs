using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using Tien.AdvancedLoading.Core;

public class MapHangMinigameManager : MonoBehaviour
{
    private Player mainPlayer;
    [SerializeField] private CinemachineCamera mainCamera;
    [SerializeField] private Transform minigameStartPoint;

    [Header("Camera Overview")]
    [SerializeField] private KeyCode zoomOutKey = KeyCode.Z;
    [SerializeField, Min(0.01f)] private float zoomOutOrthographicSize = 12f;
    [SerializeField, Min(0f)] private float zoomDuration = 0.25f;

    [Header("Stage")]
    [SerializeField] private int currentStage;
    [SerializeField] private int totalStages = 3;

    [Header("Scene Transition")]
    [SerializeField] private bool autoLoadNextScene = true;

    private Coroutine zoomRoutine;
    private float normalOrthographicSize;
    private bool hasNormalOrthographicSize;
    private bool isZoomedOut;

    public int CurrentStage => currentStage;
    public int TotalStages => totalStages;
    public bool IsFinalStage => currentStage >= totalStages - 1;
    public bool HasNextScene => ResolveNextSceneName() != null;

    private void OnEnable()
    {
        PlayerRegistry.OnCurrentChanged += HandleCurrentPlayerChanged;
        mainPlayer = PlayerRegistry.Current;
    }

    private void OnDisable()
    {
        PlayerRegistry.OnCurrentChanged -= HandleCurrentPlayerChanged;
        RestoreCameraImmediately();
    }

    private void Update()
    {
        if (!Tien.Minigame.MinigameCheckpointState.IsInMinigame)
        {
            if (isZoomedOut)
            {
                RestoreCameraImmediately();
            }

            return;
        }

        if (Input.GetKeyDown(zoomOutKey))
        {
            ToggleCameraOverview();
        }
    }

    private void HandleCurrentPlayerChanged(Player player)
    {
        mainPlayer = player;
    }

    public void StartMinigame()
    {
        if (mainPlayer != null)
        {
            if (minigameStartPoint != null)
            {
                mainPlayer.transform.position = minigameStartPoint.position;
                Tien.Minigame.MinigameCheckpointState.BeginMinigame(minigameStartPoint.position);

                PlayerHealth health = mainPlayer.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    health.UpdateCheckpoint(minigameStartPoint.position);
                }
            }
            mainPlayer.SetRotaMode(true);
        }
    }

    public void EndMinigame()
    {
        RestoreCameraImmediately();

        if (mainPlayer != null)
        {
            mainPlayer.SetRotaMode(false);
        }
    }

    public bool AdvanceStage()
    {
        if (IsFinalStage)
        {
            return false;
        }

        currentStage++;
        return ApplySceneTransition();
    }

    public void SetStage(int stage)
    {
        currentStage = Mathf.Clamp(stage, 0, totalStages - 1);
    }

    private bool ApplySceneTransition()
    {
        string nextScene = ResolveNextSceneName();

        if (!autoLoadNextScene || string.IsNullOrEmpty(nextScene))
        {
            return false;
        }

        SceneLoader.Load(nextScene);
        return true;
    }

    private string ResolveNextSceneName()
    {
        return null;
    }

    private void ToggleCameraOverview()
    {
        if (mainCamera == null)
        {
            return;
        }

        if (!hasNormalOrthographicSize)
        {
            normalOrthographicSize = mainCamera.Lens.OrthographicSize;
            hasNormalOrthographicSize = true;
        }

        isZoomedOut = !isZoomedOut;
        float targetSize = isZoomedOut
            ? Mathf.Max(normalOrthographicSize, zoomOutOrthographicSize)
            : normalOrthographicSize;

        if (zoomRoutine != null)
        {
            StopCoroutine(zoomRoutine);
        }

        zoomRoutine = StartCoroutine(ZoomCameraRoutine(targetSize));
    }

    private IEnumerator ZoomCameraRoutine(float targetSize)
    {
        float startSize = mainCamera.Lens.OrthographicSize;
        float duration = Mathf.Max(0.01f, zoomDuration);
        float elapsed = 0f;

        while (elapsed < duration && mainCamera != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / duration));
            SetOrthographicSize(Mathf.Lerp(startSize, targetSize, t));
            yield return null;
        }

        if (mainCamera != null)
        {
            SetOrthographicSize(targetSize);
        }

        zoomRoutine = null;
    }

    private void RestoreCameraImmediately()
    {
        if (zoomRoutine != null)
        {
            StopCoroutine(zoomRoutine);
            zoomRoutine = null;
        }

        if (mainCamera != null && hasNormalOrthographicSize)
        {
            SetOrthographicSize(normalOrthographicSize);
        }

        isZoomedOut = false;
        hasNormalOrthographicSize = false;
    }

    private void SetOrthographicSize(float size)
    {
        LensSettings lens = mainCamera.Lens;
        lens.OrthographicSize = Mathf.Max(0.01f, size);
        mainCamera.Lens = lens;
    }
}

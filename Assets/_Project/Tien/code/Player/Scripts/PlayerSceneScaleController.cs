using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerSceneScaleController : MonoBehaviour, ICharacterScaleProvider
{
    [SerializeField] private PlayerSceneScaleDatabase scaleDatabase;

    private bool warnedMissingDatabase;
    private bool hasBaseScale;
    private int xSign = 1;
    private int ySign = 1;

    public float CurrentScaleMultiplier { get; private set; } = 1f;
    public Vector3 BasePlayerScale { get; private set; }

    private void Awake()
    {
        CaptureBaseScaleIfNeeded();
        ApplyScaleForActiveScene();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        ApplyScaleForActiveScene();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
    }

    public Vector3 ScaleOffset(Vector3 baseOffset)
    {
        return baseOffset * CurrentScaleMultiplier;
    }

    public float ScaleDistance(float baseDistance)
    {
        return baseDistance * CurrentScaleMultiplier;
    }

    public Vector3 GetManualSpawnPosition(Vector3 baseOffset, int facingDirection)
    {
        Vector3 offset = ScaleOffset(baseOffset);
        offset.x *= facingDirection >= 0 ? 1f : -1f;
        return transform.position + offset;
    }

    public void SetFacingXSign(float directionSign)
    {
        xSign = directionSign >= 0f ? -1 : 1;
        ApplyCurrentScale();
    }

    public void SetGravityYSign(bool inverted)
    {
        ySign = inverted ? -1 : 1;
        ApplyCurrentScale();
    }

    public void ApplyScaleForActiveScene()
    {
        CaptureBaseScaleIfNeeded();
        string sceneName = SceneManager.GetActiveScene().name;
        CurrentScaleMultiplier = ResolveScale(sceneName);
        ApplyCurrentScale();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene == SceneManager.GetActiveScene())
        {
            ApplyScaleForActiveScene();
        }
    }

    private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        ApplyScaleForActiveScene();
    }

    private void CaptureBaseScaleIfNeeded()
    {
        if (hasBaseScale)
        {
            return;
        }

        Vector3 scale = transform.localScale;
        if (Mathf.Abs(scale.x) < 0.1f || Mathf.Abs(scale.y) < 0.1f)
        {
            // Do not capture a temporary zero or compressed scale during transitions
            return;
        }

        xSign = scale.x < 0f ? -1 : 1;
        ySign = scale.y < 0f ? -1 : 1;
        BasePlayerScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), scale.z != 0f ? scale.z : 1f);
        hasBaseScale = true;
    }

    private float ResolveScale(string sceneName)
    {
        if (scaleDatabase != null)
        {
            return scaleDatabase.GetScale(sceneName);
        }

        if (!warnedMissingDatabase)
        {
            warnedMissingDatabase = true;
            Debug.LogWarning("PlayerSceneScaleController has no scale database assigned. Using scale 1.", this);
        }

        return 1f;
    }

    private void ApplyCurrentScale()
    {
        if (!hasBaseScale)
        {
            return;
        }

        transform.localScale = new Vector3(
            CurrentScaleMultiplier * xSign,
            CurrentScaleMultiplier * ySign,
            BasePlayerScale.z);
    }
}

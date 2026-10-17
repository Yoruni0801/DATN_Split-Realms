using UnityEngine;
using UnityEngine.SceneManagement;

public class PersistentUI : MonoBehaviour
{
    private static PersistentUI instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;

            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        PlayerRegistry.OnCurrentChanged += HandlePlayerChanged;
        HandlePlayerChanged(PlayerRegistry.Current);
    }

    private void OnDisable()
    {
        PlayerRegistry.OnCurrentChanged -= HandlePlayerChanged;
    }

    private void Start()
    {
        AttachToCanvas();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToCanvas();
        // Cập nhật trạng thái hiển thị khi scene mới load (Player có thể chưa register)
        HandlePlayerChanged(PlayerRegistry.Current);
    }

    private void AttachToCanvas()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            transform.SetParent(canvas.transform, false);
            transform.SetAsFirstSibling();
        }
    }

    private void HandlePlayerChanged(Player player)
    {
        bool shouldShow = (player != null);
        if (gameObject.activeSelf != shouldShow)
        {
            gameObject.SetActive(shouldShow);
        }
    }
}

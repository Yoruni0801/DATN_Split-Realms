using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class CrystalMarbleMiniGameTrigger2D : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("Mini Game Settings")]
    [SerializeField, Tooltip("Tên prefab / Root GameObject của minigame trong scene (ví dụ: MiniGameChiaKhoa)")]
    private string miniGameRootName = "MiniGameChiaKhoa";

    [SerializeField, Tooltip("GameObject root của minigame (nếu kéo thả trực tiếp)")]
    private GameObject miniGameRoot;

    [SerializeField, Tooltip("Tự động ẩn Scene Map (Map5) khi hiện minigame")]
    private bool hideSceneWhilePlaying = true;

    [SerializeField] private bool lockPlayerWhilePlaying = true;
    [SerializeField] private bool autoHideWhenSolved = true;
    [SerializeField] private float hideDelayAfterSolved = 1.2f;
    [SerializeField] private float miniGameCameraDepth = 10f;

    [Header("Auto-Hide Fade (Không cần ấn nút)")]
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private bool hideSuccessOverlay = true;

    [Header("Scene Transition Mode (Tùy chọn nếu qua scene)")]
    [SerializeField, Tooltip("Bật tùy chọn chuyển hẳn qua scene khác thay vì hiện prefab minigame trong scene")]
    private bool loadSceneOnInteract;

    [SerializeField, Tooltip("Tên scene minigame (chỉ dùng khi loadSceneOnInteract = true)")]
    private string miniGameSceneName = "CrystalMarble";

    [Header("Door Cutscene (Sau khi hoàn thành minigame)")]
    [SerializeField, Tooltip("Bật cutscene di camera và hạ cửa sau khi hoàn thành minigame")]
    private bool enableDoorCutscene = true;

    [SerializeField, Tooltip("Component DoorLowerCutscene2D (nếu có). Để trống sẽ tự dùng settings bên dưới")]
    private DoorLowerCutscene2D doorCutscene;

    [SerializeField, Tooltip("GameObject / Transform cửa cần hạ xuống")]
    private GameObject doorObject;

    [SerializeField, Tooltip("GameObject vị trí hạ của cửa (vị trí đích khi cửa hạ xong)")]
    private GameObject doorLoweredTarget;

    [SerializeField, Tooltip("Vị trí camera hướng tới cửa (để trống sẽ tự lấy vị trí cửa)")]
    private Transform doorCameraTarget;

    [Header("Next Cutscene (Chạy ngay sau khi cam quay về Player)")]
    [SerializeField, Tooltip("GameObject chứa Cutscene / Timeline tiếp theo (kéo thả từ Inspector)")]
    private GameObject nextCutsceneObject;

    [SerializeField, Tooltip("PlayableDirector / Timeline cutscene tiếp theo chạy sau khi cam quay về Player")]
    private PlayableDirector nextCutsceneTimeline;

    [SerializeField, Tooltip("Sự kiện gọi ngay sau khi cánh cửa hạ xong và camera quay về Player")]
    private UnityEngine.Events.UnityEvent onDoorCutsceneFinished;

    [Header("Scene Visibility Settings")]
    [SerializeField, Tooltip("Các GameObject trong scene KHÔNG bị ẩn khi chơi minigame (kéo thả từ Inspector)")]
    private GameObject[] objectsToKeepVisibleWhilePlaying;

    [Header("Objects to Show After Completion")]
    [SerializeField, Tooltip("Các GameObject cần hiện lên sau khi hoàn thành minigame (gán từ Inspector)")]
    private GameObject[] objectsToShowOnComplete;

    private Player player;
    private CrystalMarblePuzzle puzzle;
    private Collider2D triggerCollider;
    private bool playerInRange;
    private bool isMiniGameShowing;
    private bool isFadingAndHiding;
    private bool hasRunDoorCutscene;
    private Coroutine autoHideCoroutine;
    private readonly List<GameObject> hiddenSceneRoots = new List<GameObject>();
    private readonly Dictionary<Camera, string> originalCameraTags = new();

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        EnsureTrigger();
        ResolveMiniGame();
        if (miniGameRoot != null)
        {
            miniGameRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        EnsureTrigger();
    }

    private IEnumerator Start()
    {
        yield return null;

        if (enableDoorCutscene && CrystalMarbleTravelState.JustCompletedMiniGame)
        {
            CrystalMarbleTravelState.JustCompletedMiniGame = false;
            if (HasDoorCutsceneConfigured())
            {
                hasRunDoorCutscene = true;
                // Fade từ đen sang rõ để hiện Map5 objects
                yield return FadeToClear(0.5f);
                RunDoorCutscene();
            }
        }
    }

    private void OnDisable()
    {
        if (player != null && lockPlayerWhilePlaying)
        {
            player.SetControlLocked(false);
        }
    }

    private void Update()
    {
        if (!playerInRange || isMiniGameShowing || !Input.GetKeyDown(interactKey))
        {
            // Cho phép ấn Enter/Space để bỏ qua thời gian chờ sau khi giải xong (chỉ khi chưa bắt đầu fade out)
            if (isMiniGameShowing && !isFadingAndHiding && puzzle != null && puzzle.IsSolved
                && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
            {
                if (autoHideCoroutine != null)
                {
                    StopCoroutine(autoHideCoroutine);
                    autoHideCoroutine = null;
                }

                autoHideCoroutine = StartCoroutine(FadeOutAndHideRoutine());
            }

            return;
        }

        if (loadSceneOnInteract)
        {
            LoadMiniGameScene();
        }
        else
        {
            ShowMiniGame();
        }
    }

    private void LoadMiniGameScene()
    {
        if (string.IsNullOrEmpty(miniGameSceneName))
        {
            Debug.LogWarning("[CrystalMarbleMiniGameTrigger2D] Tên scene minigame đang để trống!", this);
            return;
        }

        // Lưu vị trí hiện tại của Player trước khi chuyển sang Scene minigame
        if (player != null)
        {
            SceneRuntimeStateManager.SavePlayerPosition(gameObject.scene.name, player.transform.position);
        }
        else
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null)
            {
                SceneRuntimeStateManager.SavePlayerPosition(gameObject.scene.name, playerObj.transform.position);
            }
        }

        try
        {
            Tien.AdvancedLoading.Core.SceneLoader.Load(miniGameSceneName);
        }
        catch
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(miniGameSceneName);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInRange = true;
        CachePlayer(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInRange = false;
    }

    private void ShowMiniGame()
    {
        ResolveMiniGame();
        if (miniGameRoot == null)
        {
            Debug.LogWarning("Mini game root was not found: " + miniGameRootName, this);
            return;
        }

        miniGameRoot.SetActive(true);
        EnableMiniGameCameras(true);
        HideSceneRoots();

        puzzle = miniGameRoot.GetComponentInChildren<CrystalMarblePuzzle>(true);
        if (puzzle != null)
        {
            puzzle.SetEmbeddedCloseTarget(null);
            puzzle.ResetPuzzle();
        }

        if (AudioManager.HasInstance)
        {
            AudioManager.Instance.EnsureAudioListener();
        }

        if (player != null && lockPlayerWhilePlaying)
        {
            player.SetControlLocked(true);
        }

        isMiniGameShowing = true;
        isFadingAndHiding = false;
        hasRunDoorCutscene = false;

        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
        }

        if (autoHideWhenSolved)
        {
            autoHideCoroutine = StartCoroutine(AutoHideWhenSolvedRoutine());
        }
    }

    private IEnumerator AutoHideWhenSolvedRoutine()
    {
        while (puzzle != null && !puzzle.IsSolved)
        {
            yield return null;
        }

        // Ẩn success overlay (không cần ấn nút nữa)
        if (hideSuccessOverlay && puzzle != null)
        {
            puzzle.SuppressSuccessOverlay();
        }

        if (puzzle != null && hideDelayAfterSolved > 0f){
            yield return new WaitForSecondsRealtime(hideDelayAfterSolved);
        }

        yield return FadeOutAndHideRoutine();
    }

    private IEnumerator FadeOutAndHideRoutine()
    {
        if (isFadingAndHiding)
        {
            yield break;
        }

        isFadingAndHiding = true;
        bool canRunCutscene = enableDoorCutscene && HasDoorCutsceneConfigured();

        // 1. Fade màn hình sang đen
        yield return FadeToBlack(fadeOutDuration);

        // 2. Ẩn mini-game + restore tất cả object Map5
        HideMiniGame(unlockPlayer: false);

        // 2.5 Hiện các object được gán từ Inspector
        ShowAssignedObjects();

        // 3. Fade màn hình trở lại (hiện Map5)
        yield return FadeToClear(fadeInDuration);

        // 4. Mở khóa player + chạy door cutscene (nếu có)
        if (player != null && lockPlayerWhilePlaying)
        {
            player.SetControlLocked(false);
        }

        if (canRunCutscene && !hasRunDoorCutscene)
        {
            hasRunDoorCutscene = true;
            RunDoorCutscene();
        }

        isMiniGameShowing = false;
        isFadingAndHiding = false;
    }

    private IEnumerator FadeToBlack(float duration)
    {
        MapHangTransitionFade2D fade = MapHangTransitionFade2D.GetOrCreate();
        if (fade != null)
        {
            yield return fade.FadeToBlack(duration);
        }
    }

    private IEnumerator FadeToClear(float duration)
    {
        MapHangTransitionFade2D fade = MapHangTransitionFade2D.GetOrCreate();
        if (fade != null)
        {
            yield return fade.FadeToClear(duration);
        }
    }

    private void ShowAssignedObjects()
    {
        if (objectsToShowOnComplete == null || objectsToShowOnComplete.Length == 0)
        {
            return;
        }

        for (int i = 0; i < objectsToShowOnComplete.Length; i++)
        {
            GameObject obj = objectsToShowOnComplete[i];
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
    }

    private bool HasDoorCutsceneConfigured()
    {
        if (doorCutscene != null && doorCutscene.DoorObject != null && doorCutscene.DoorLoweredTarget != null)
        {
            return true;
        }

        return doorObject != null && doorLoweredTarget != null;
    }

    private void RunDoorCutscene()
    {
        DoorLowerCutscene2D cutscene = doorCutscene;
        if (cutscene == null)
        {
            cutscene = GetComponent<DoorLowerCutscene2D>();
            if (cutscene == null)
            {
                cutscene = gameObject.AddComponent<DoorLowerCutscene2D>();
            }

            cutscene.DoorObject = doorObject;
            cutscene.DoorLoweredTarget = doorLoweredTarget;
        }

        cutscene.PlayCutscene(HandleDoorCutsceneComplete);
    }

    private void HandleDoorCutsceneComplete()
    {
        // 1. Phát Timeline cutscene tiếp theo (nếu được gán)
        if (nextCutsceneTimeline != null)
        {
            nextCutsceneTimeline.Play();
        }

        // 2. Gọi UnityEvent để chạy bất kỳ cutscene / sự kiện nào được gán ở Inspector
        onDoorCutsceneFinished?.Invoke();
    }

    private void HideMiniGame(bool unlockPlayer)
    {
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        if (miniGameRoot != null)
        {
            RestoreSceneRoots();
            EnableMiniGameCameras(false);
            miniGameRoot.SetActive(false);
        }

        if (unlockPlayer && player != null && lockPlayerWhilePlaying)
        {
            player.SetControlLocked(false);
        }

        if (AudioManager.HasInstance)
        {
            AudioManager.Instance.EnsureAudioListener();
        }

        isMiniGameShowing = false;
    }

    private void HideSceneRoots()
    {
        if (!hideSceneWhilePlaying)
        {
            return;
        }

        Transform miniGameTransform = miniGameRoot != null ? miniGameRoot.transform : null;
        Transform triggerTransform = transform;

        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == miniGameRoot || root == gameObject || !root.activeSelf)
            {
                continue;
            }

            // Đừng ẩn nếu root chứa miniGameRoot hoặc Trigger component bên trong
            if (miniGameTransform != null && miniGameTransform.IsChildOf(root.transform))
            {
                continue;
            }

            if (triggerTransform.IsChildOf(root.transform))
            {
                continue;
            }

            // Đừng ẩn nếu root nằm trong danh sách gán từ Inspector (Keep Visible)
            if (ShouldKeepVisible(root))
            {
                continue;
            }

            hiddenSceneRoots.Add(root);
            root.SetActive(false);
        }
    }

    private bool ShouldKeepVisible(GameObject root)
    {
        if (objectsToKeepVisibleWhilePlaying == null || objectsToKeepVisibleWhilePlaying.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < objectsToKeepVisibleWhilePlaying.Length; i++)
        {
            GameObject keepObj = objectsToKeepVisibleWhilePlaying[i];
            if (keepObj == null)
            {
                continue;
            }

            if (root == keepObj || keepObj.transform.IsChildOf(root.transform))
            {
                return true;
            }
        }

        return false;
    }

    private void RestoreSceneRoots()
    {
        for (int i = 0; i < hiddenSceneRoots.Count; i++)
        {
            if (hiddenSceneRoots[i] != null)
            {
                hiddenSceneRoots[i].SetActive(true);
            }
        }

        hiddenSceneRoots.Clear();
    }

    private void ResolveMiniGame()
    {
        if (miniGameRoot != null)
        {
            return;
        }

        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == miniGameRootName || roots[i].name == "MiniGameChiaKhoa" || roots[i].name == "CRYSTAL_MARBLE_MINIGAME")
            {
                miniGameRoot = roots[i];
                break;
            }
        }

        if (miniGameRoot == null)
        {
            GameObject found = GameObject.Find(miniGameRootName);
            if (found == null) found = GameObject.Find("MiniGameChiaKhoa");
            if (found == null) found = GameObject.Find("CRYSTAL_MARBLE_MINIGAME");
            if (found != null)
            {
                miniGameRoot = found;
            }
        }

        if (miniGameRoot != null)
        {
            puzzle = miniGameRoot.GetComponentInChildren<CrystalMarblePuzzle>(true);
        }
    }

    private void EnableMiniGameCameras(bool enabled)
    {
        if (miniGameRoot == null)
        {
            return;
        }

        Camera[] cameras = miniGameRoot.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null)
            {
                continue;
            }

            cam.enabled = enabled;
            if (enabled)
            {
                if (!originalCameraTags.ContainsKey(cam))
                {
                    originalCameraTags[cam] = cam.tag;
                }

                if (cam.CompareTag("MainCamera"))
                {
                    cam.tag = "Untagged";
                }

                cam.depth = miniGameCameraDepth;
            }
            else
            {
                if (originalCameraTags.TryGetValue(cam, out string originalTag))
                {
                    cam.tag = originalTag;
                }
            }
        }

        AudioListener[] listeners = miniGameRoot.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null)
            {
                listeners[i].enabled = enabled && hideSceneWhilePlaying;
            }
        }
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
        {
            return true;
        }

        Transform parent = other.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag(playerTag))
            {
                return true;
            }

            parent = parent.parent;
        }

        return other.GetComponentInParent<Player>() != null;
    }

    private void CachePlayer(Collider2D other)
    {
        if (player != null || other == null)
        {
            return;
        }

        player = other.GetComponent<Player>();
        if (player == null)
        {
            player = other.GetComponentInParent<Player>();
        }
    }

    private void EnsureTrigger()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}

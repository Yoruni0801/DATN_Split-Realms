using UnityEngine;
using UnityEngine.UI;
using Tien.AdvancedLoading.Core;

public class RespawnDeathUI : MonoBehaviour
{
    [Header("References")]
    private PlayerRespawnController respawnController;
    [SerializeField] private GameObject deathScreenRoot;
    [SerializeField] private Button respawnButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Main Menu")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Input")]
    [SerializeField] private KeyCode respawnKey = KeyCode.Space;

    [Header("Cursor")]
    [SerializeField] private bool manageCursor = true;

    private bool previousCursorVisible;
    private bool hasCapturedCursorState;
    private CursorLockMode previousCursorLockState;

    private void Awake()
    {
        SetDeathScreenActive(false);
    }

    private void OnEnable()
    {
        BindController(respawnController != null ? respawnController : PlayerRespawnController.ActiveController);
        PlayerRespawnController.ActiveControllerChanged += BindController;

        if (respawnButton != null)
        {
            respawnButton.onClick.AddListener(RequestRespawn);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(RequestMainMenu);
        }
    }

    private void OnDisable()
    {
        PlayerRespawnController.ActiveControllerChanged -= BindController;

        if (respawnButton != null)
        {
            respawnButton.onClick.RemoveListener(RequestRespawn);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(RequestMainMenu);
        }

        UnbindController();
        SetDeathScreenActive(false);
        RestoreCursorIfNeeded();
    }

    private void Update()
    {
        if (respawnController != null
            && respawnController.IsWaitingForRespawn
            && Input.GetKeyDown(respawnKey))
        {
            RequestRespawn();
        }
    }

    private void BindController(PlayerRespawnController controller)
    {
        if (respawnController == controller)
        {
            return;
        }

        UnbindController();
        respawnController = controller;

        if (respawnController == null)
        {
            SetDeathScreenActive(false);
            return;
        }

        respawnController.RespawnPromptShown += ShowDeathScreen;
        respawnController.RespawnPromptHidden += HideDeathScreen;

        if (respawnController.IsWaitingForRespawn)
        {
            ShowDeathScreen();
        }
        else
        {
            HideDeathScreen();
        }
    }

    private void UnbindController()
    {
        if (respawnController == null)
        {
            return;
        }

        respawnController.RespawnPromptShown -= ShowDeathScreen;
        respawnController.RespawnPromptHidden -= HideDeathScreen;
        respawnController = null;
    }

    private void RequestRespawn()
    {
        respawnController?.RequestRespawn();
    }

    private void RequestMainMenu()
    {
        RestoreCursorIfNeeded();
        Time.timeScale = 1f;
        SceneLoader.Load(mainMenuSceneName);
    }

    private void ShowDeathScreen()
    {
        SetDeathScreenActive(true);
        UnlockCursorIfNeeded();
    }

    private void HideDeathScreen()
    {
        SetDeathScreenActive(false);
        RestoreCursorIfNeeded();
    }

    private void SetDeathScreenActive(bool active)
    {
        if (deathScreenRoot != null)
        {
            deathScreenRoot.SetActive(active);
        }
    }

    private void UnlockCursorIfNeeded()
    {
        if (!manageCursor || hasCapturedCursorState)
        {
            return;
        }

        previousCursorVisible = Cursor.visible;
        previousCursorLockState = Cursor.lockState;
        hasCapturedCursorState = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void RestoreCursorIfNeeded()
    {
        if (!manageCursor || !hasCapturedCursorState)
        {
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockState;
        hasCapturedCursorState = false;
    }
}

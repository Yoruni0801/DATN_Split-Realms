using System;
using UnityEngine;

public class PlayerRespawnController : MonoBehaviour
{
    [Header("References")]
    private PlayerHealth playerHealth;

    public static event Action<PlayerRespawnController> ActiveControllerChanged;
    public static event Action<PlayerRespawnController> RespawnRequested;

    public event Action RespawnPromptShown;
    public event Action RespawnPromptHidden;

    public static PlayerRespawnController ActiveController { get; private set; }
    public bool IsWaitingForRespawn => isWaitingForRespawn;
    public PlayerHealth PlayerHealth => playerHealth;

    private bool isWaitingForRespawn;
    private bool isRespawning;

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }
    }

    private void OnEnable()
    {
        ActiveController = this;
        ActiveControllerChanged?.Invoke(this);

        if (playerHealth != null)
        {
            playerHealth.SetAutoRespawn(false);
            playerHealth.OnDied += HandlePlayerDied;
            playerHealth.OnRespawned += HandlePlayerRespawned;
        }
    }

    private void OnDisable()
    {
        if (ActiveController == this)
        {
            ActiveController = null;
            ActiveControllerChanged?.Invoke(null);
        }

        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
            playerHealth.OnRespawned -= HandlePlayerRespawned;
        }
    }

    private void HandlePlayerDied()
    {
        isWaitingForRespawn = true;
        isRespawning = false;
        RespawnPromptShown?.Invoke();
    }

    private void HandlePlayerRespawned()
    {
        isWaitingForRespawn = false;
        isRespawning = false;
        RespawnPromptHidden?.Invoke();
    }

    public void RequestRespawn()
    {
        if (!isWaitingForRespawn || isRespawning || playerHealth == null || !playerHealth.IsDead)
        {
            return;
        }

        isRespawning = true;
        RespawnPromptHidden?.Invoke();
        RespawnRequested?.Invoke(this);

        if (Tien.Minigame.MinigameCheckpointState.IsInMinigame)
        {
            Vector3 checkpoint =
                Tien.Minigame.MinigameCheckpointState.CurrentMinigameCheckpoint;
            playerHealth.UpdateCheckpoint(checkpoint);
            playerHealth.RespawnAtCheckpoint();

            Player player = playerHealth.GetComponent<Player>();
            if (player != null)
            {
                player.SetRotaMode(true);

                PlayerRotaSurfaceController rotaController =
                    player.GetComponent<PlayerRotaSurfaceController>();
                if (rotaController != null)
                {
                    PlayerRotaSurfaceController.SurfaceSide checkpointSurface =
                        Tien.Minigame.MinigameCheckpointState.HasCheckpointSurface
                            ? Tien.Minigame.MinigameCheckpointState.CurrentCheckpointSurface
                            : PlayerRotaSurfaceController.SurfaceSide.Floor;

                    rotaController.ResetForRespawn(checkpointSurface);
                    rotaController.AttachToSurface(checkpointSurface, false);
                }

                Tien.Minigame.MinigameCheckpointState.ClearPreservedDeathSurface();
                player.SetInputActive(true);

                Unity.Cinemachine.CinemachineCamera cam = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
                if (cam != null)
                {
                    PlayerRotaSurfaceController.SurfaceSide checkpointSurface =
                        Tien.Minigame.MinigameCheckpointState.HasCheckpointSurface
                            ? Tien.Minigame.MinigameCheckpointState.CurrentCheckpointSurface
                            : PlayerRotaSurfaceController.SurfaceSide.Floor;

                    float targetAngle = PlayerRotaSurfaceController.GetSurfaceAngle(PlayerRotaSurfaceController.GetNormal(checkpointSurface));
                    cam.transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
                    cam.Follow = player.transform;
                }
            }

            return;
        }

        // Respawn in place at the last checkpoint instead of reloading the whole scene,
        // so progress saved at a touched checkpoint is kept rather than restarting the game.
        Player normalPlayer = playerHealth.GetComponent<Player>();
        if (normalPlayer != null)
        {
            normalPlayer.SetRotaMode(false);
            normalPlayer.SetInputActive(true);
        }

        playerHealth.RespawnAtCheckpoint();
    }
}

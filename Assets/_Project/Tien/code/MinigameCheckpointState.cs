using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tien.Minigame
{
    public static class MinigameCheckpointState
    {
        public static bool IsInMinigame = false;
        public static Vector3 CurrentMinigameCheckpoint;
        public static bool HasCheckpointSurface { get; private set; }
        public static PlayerRotaSurfaceController.SurfaceSide
            CurrentCheckpointSurface { get; private set; }

        public static bool PreserveSurfaceOnNextRespawn { get; private set; }
        public static PlayerRotaSurfaceController.SurfaceSide DeathPreservedSurface { get; private set; }

        public static void PreserveSurfaceOnDeath(PlayerRotaSurfaceController.SurfaceSide surface)
        {
            PreserveSurfaceOnNextRespawn = true;
            DeathPreservedSurface = surface;
        }

        public static void ClearPreservedDeathSurface()
        {
            PreserveSurfaceOnNextRespawn = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnDomainReload()
        {
            ClearCheckpoint();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitSceneLoadedHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClearCheckpoint();
        }

        public static void BeginMinigame(Vector3 position)
        {
            IsInMinigame = true;
            SetCheckpoint(position);
        }

        public static void BeginMinigame(Vector3 position, PlayerRotaSurfaceController.SurfaceSide surface)
        {
            IsInMinigame = true;
            SetCheckpoint(position, surface);
        }

        public static void SetCheckpoint(Vector3 position)
        {
            IsInMinigame = true;
            CurrentMinigameCheckpoint = position;

            Player player = PlayerRegistry.Current;
            PlayerRotaSurfaceController rotaController = player != null
                ? player.GetComponent<PlayerRotaSurfaceController>()
                : null;

            HasCheckpointSurface = rotaController != null;
            if (rotaController != null)
            {
                CurrentCheckpointSurface = rotaController.CurrentSurface;
            }
        }

        public static void SetCheckpoint(Vector3 position, PlayerRotaSurfaceController.SurfaceSide surface)
        {
            IsInMinigame = true;
            CurrentMinigameCheckpoint = position;
            HasCheckpointSurface = true;
            CurrentCheckpointSurface = surface;
        }

        public static void ClearCheckpoint()
        {
            IsInMinigame = false;
            HasCheckpointSurface = false;
            CurrentMinigameCheckpoint = Vector3.zero;
        }
    }
}

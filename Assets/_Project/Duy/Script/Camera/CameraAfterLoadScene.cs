using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.SceneManagement;

public class CameraAfterLoadScene : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cinemachine;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindCameraToPlayer();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindCameraToPlayer();
    }

    private void BindCameraToPlayer()
    {
        Player currentPlayer = GetComponent<Player>();
        if (currentPlayer == null)
        {
            currentPlayer = PlayerRegistry.Current;
        }

        if (currentPlayer == null)
        {
            return;
        }

        if (cinemachine == null || !cinemachine.gameObject.scene.IsValid())
        {
            cinemachine = FindFirstObjectByType<CinemachineCamera>();
        }

        if (cinemachine != null)
        {
            CameraTarget newTarget = new CameraTarget
            {
                TrackingTarget = currentPlayer.transform,
                CustomLookAtTarget = true,
                LookAtTarget = currentPlayer.transform
            };

            cinemachine.Target = newTarget;
            cinemachine.ForceCameraPosition(currentPlayer.transform.position, currentPlayer.transform.rotation);
        }
    }
}

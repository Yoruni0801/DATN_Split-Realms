using UnityEngine;
using System.Collections;
using Unity.Cinemachine; 

public class CameraEffectsManager : MonoBehaviour
{
    public static CameraEffectsManager Instance { get; private set; }

    [Header("Kết nối Cinemachine")]
    [SerializeField] private CinemachineCamera virtualCamera; 
    [SerializeField] private CinemachineImpulseSource impulseSource; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    /// <summary>
    /// Hàm gọi hiệu ứng lia camera sang vị trí rút nước, rung màn hình và quay lại.
    /// </summary>
    public void PlayCinematicPan(Transform target, float panTime, float holdTime, float shakeDuration, float shakeStrength, System.Action onComplete = null)
    {
        if (virtualCamera == null || target == null)
        {
            onComplete?.Invoke();
            return;
        }

        // Truyền đầy đủ các thông số xuống Coroutine xử lý
        StartCoroutine(CinematicPanRoutine(target, panTime, holdTime, shakeStrength, onComplete));
    }

    private IEnumerator CinematicPanRoutine(Transform target, float panTime, float holdTime, float strength, System.Action onComplete)
    {
        // 1. Lưu lại mục tiêu ban đầu của Camera (thường là Player)
        Transform originalTarget = virtualCamera.Follow;

        // 2. Lia cam: Đổi mục tiêu theo dõi sang Khối nước (hoặc CamTarget)
        virtualCamera.Follow = target;

        // Đợi camera di chuyển tới vị trí mục tiêu
        yield return new WaitForSeconds(panTime);

        // 3. Rung màn hình bằng Impulse
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(strength);
        }

        // Đợi người chơi nhìn dòng nước hạ xuống
        yield return new WaitForSeconds(holdTime);

        // 4. Trả camera về vị trí cũ (Lia ngược lại Player)
        virtualCamera.Follow = originalTarget;

        // Đợi camera lia về xong rồi mới gọi callback
        yield return new WaitForSeconds(panTime);
        onComplete?.Invoke();
    }
}
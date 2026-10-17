using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Trigger 2D để phát Timeline cutscene và ẩn các GameObject liên quan.
/// Hỗ trợ ẩn ngay khi bắt đầu chạy cutscene hoặc ẩn sau khi cutscene kết thúc.
/// Hỗ trợ focus camera vào target (hoặc Player) sau khi cutscene kết thúc.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Timeline2DTrigger : MonoBehaviour
{
    public enum HideTiming
    {
        OnCutsceneStart, // Ẩn ngay khi cutscene bắt đầu chạy (chạy là ẩn luôn)
        OnCutsceneEnd    // Ẩn sau khi cutscene chạy xong
    }

    [Header("Timeline Settings")]
    [SerializeField] private PlayableDirector director;
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("Hide Settings")]
    [Tooltip("Thời điểm ẩn GameObject: Ngay khi bắt đầu (OnCutsceneStart) hoặc Khi kết thúc (OnCutsceneEnd).")]
    [SerializeField] private HideTiming hideTiming = HideTiming.OnCutsceneStart;

    [Tooltip("Danh sách các GameObject cần ẩn (SetActive(false)).")]
    [SerializeField] private List<GameObject> objectsToHide = new List<GameObject>();

    [Tooltip("Có ẩn chính GameObject chứa Trigger/Script này hay không.")]
    [SerializeField] private bool hideThisTriggerObject = true;

    [Header("Camera Focus Settings")]
    [Tooltip("Tự động focus camera vào target sau khi cutscene kết thúc.")]
    [SerializeField] private bool focusCameraAfterCutscene = true;

    [Tooltip("Target camera sẽ focus tới sau khi cutscene kết thúc. Nếu để trống sẽ tự lấy Player.")]
    [SerializeField] private Transform cameraFocusTarget;

    [Tooltip("Cinemachine Camera cần gán Follow. Nếu để trống sẽ tự tìm trong Scene.")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    private bool hasTriggered = false;

    private void Awake()
    {
        // Tự lấy PlayableDirector nếu chưa gán trong Inspector
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }

        // Đảm bảo Collider2D được chọn isTrigger
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }

        ResolveCameraReferences();
    }

    private void ResolveCameraReferences()
    {
        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
        }
    }

    private void OnEnable()
    {
        if (director != null)
        {
            director.stopped += OnCutsceneStopped;
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.stopped -= OnCutsceneStopped;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered && triggerOnlyOnce) return;

        if (other.CompareTag(targetTag))
        {
            hasTriggered = true;

            if (cameraFocusTarget == null && other.transform != null)
            {
                cameraFocusTarget = other.transform;
            }

            PlayCutscene();
        }
    }

    /// <summary>
    /// Kích hoạt chạy cutscene.
    /// </summary>
    public void PlayCutscene()
    {
        if (director == null)
        {
            Debug.LogWarning($"[Timeline2DTrigger] Chưa gán PlayableDirector trên GameObject: {gameObject.name}");
            return;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        director.Play();

        // Nếu cài đặt ẩn ngay khi vừa bấm chạy
        if (hideTiming == HideTiming.OnCutsceneStart)
        {
            HideTargetObjects();
        }
    }

    /// <summary>
    /// Callback tự động gọi khi PlayableDirector phát xong cutscene.
    /// </summary>
    private void OnCutsceneStopped(PlayableDirector pd)
    {
        if (pd != director) return;

        // Focus camera vào target sau khi cutscene kết thúc
        if (focusCameraAfterCutscene)
        {
            FocusCameraToTarget();
        }

        // Nếu cài đặt ẩn khi cutscene kết thúc
        if (hideTiming == HideTiming.OnCutsceneEnd)
        {
            HideTargetObjects();
        }

        // Ẩn trigger object sau khi cutscene dừng (đảm bảo event stopped luôn được xử lý)
        if (hideThisTriggerObject)
        {
            gameObject.SetActive(false);
        }
    }

    private void FocusCameraToTarget()
    {
        ResolveCameraReferences();

        if (cinemachineCamera == null)
        {
            Debug.LogWarning($"[Timeline2DTrigger] Không tìm thấy CinemachineCamera để focus camera.", this);
            return;
        }

        if (cameraFocusTarget == null)
        {
            GameObject playerObj = GameObject.FindWithTag(targetTag);
            if (playerObj != null)
            {
                cameraFocusTarget = playerObj.transform;
            }
        }

        if (cameraFocusTarget != null)
        {
            cinemachineCamera.Follow = cameraFocusTarget;
        }
    }

    /// <summary>
    /// Thực hiện ẩn danh sách GameObject mục tiêu.
    /// </summary>
    private void HideTargetObjects()
    {
        if (objectsToHide != null && objectsToHide.Count > 0)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
    }
}

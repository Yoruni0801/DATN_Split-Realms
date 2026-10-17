using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

public class PointA : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private Transform pointA;
    [SerializeField] private PlayableDirector timelineCutscene;
    [SerializeField] private GameObject cutsceneObject;
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("Movement Settings")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float stopDistance = 0.08f;
    [SerializeField] private float moveSpeed = 4f;

    [Header("Camera Zoom Settings")]
    [Tooltip("Số càng nhỏ thì camera càng zoom gần.")]
    [SerializeField] private float zoomInSize = 4f;

    [Tooltip("Kích thước camera sau khi tới đích.")]
    [SerializeField] private float zoomOutSize = 6f;

    [SerializeField] private float zoomInDuration = 1f;
    [SerializeField] private float zoomOutDuration = 1f;

    [Header("Camera Follow Settings")]
    [SerializeField] private bool smoothCameraFollowOnPlay = true;
    [SerializeField] private Transform cameraFollowTarget;
    [SerializeField] private float followBlendDuration = 0.5f;
    [SerializeField] private bool restoreCameraFollowOnDisable;

    private Coroutine moveCoroutine;
    private Coroutine zoomCoroutine;
    private Coroutine followBlendCoroutine;
    private Rigidbody2D playerRb;
    private Transform originalCameraFollow;
    private bool originalCameraFollowCached;
    private GameObject cameraFollowProxyObject;
    private Transform cameraFollowProxy;

    private void Awake()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
        }

        CachePlayerRigidbody();
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        if (player == null || pointA == null)
        {
            Debug.LogWarning("[AutoMoveToPointA] Missing Player or Point A.");
            return;
        }

        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }

        moveCoroutine = StartCoroutine(MoveToPointA());
    }

    private IEnumerator MoveToPointA()
    {
        CachePlayerRigidbody();

        player.SetControlLocked(true);
        StartCameraFollowBlend();

        // Bắt đầu di chuyển thì zoom in
        StartCameraZoom(zoomInSize, zoomInDuration);

        while (player != null
               && pointA != null
               && Mathf.Abs(player.transform.position.x - pointA.position.x)
               > stopDistance)
        {
            float direction =
                Mathf.Sign(pointA.position.x - player.transform.position.x);

            if (playerRb != null)
            {
                playerRb.linearVelocity = new Vector2(
                    direction * moveSpeed,
                    playerRb.linearVelocity.y);
            }
            else
            {
                Vector3 targetPosition = new Vector3(
                    pointA.position.x,
                    player.transform.position.y,
                    player.transform.position.z);

                player.transform.position = Vector3.MoveTowards(
                    player.transform.position,
                    targetPosition,
                    moveSpeed * Time.fixedDeltaTime);
            }

            yield return new WaitForFixedUpdate();
        }

        if (playerRb != null)
        {
            playerRb.linearVelocity =
                new Vector2(0f, playerRb.linearVelocity.y);
        }

        // Đến đích thì zoom out
        StartCameraZoom(zoomOutSize, zoomOutDuration);

        player.SetControlLocked(false);

        if (timelineCutscene != null)
        {
            timelineCutscene.Stop();
            timelineCutscene.time = 0;
            timelineCutscene.Play();

            StartCoroutine(DisableCutsceneWhenFinished());
        }

        moveCoroutine = null;
    }

    private void StartCameraZoom(float targetSize, float duration)
    {
        if (cinemachineCamera == null)
        {
            return;
        }

        if (zoomCoroutine != null)
        {
            StopCoroutine(zoomCoroutine);
        }

        zoomCoroutine = StartCoroutine(
            ZoomCamera(targetSize, duration));
    }

    private IEnumerator ZoomCamera(float targetSize, float duration)
    {
        LensSettings lens = cinemachineCamera.Lens;
        float startSize = lens.OrthographicSize;
        float timer = 0f;

        if (duration <= 0f)
        {
            lens.OrthographicSize = targetSize;
            cinemachineCamera.Lens = lens;
            zoomCoroutine = null;
            yield break;
        }

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float progress = Mathf.Clamp01(timer / duration);
            progress = Mathf.SmoothStep(0f, 1f, progress);

            lens = cinemachineCamera.Lens;
            lens.OrthographicSize = Mathf.Lerp(
                startSize,
                targetSize,
                progress);

            cinemachineCamera.Lens = lens;

            yield return null;
        }

        lens = cinemachineCamera.Lens;
        lens.OrthographicSize = targetSize;
        cinemachineCamera.Lens = lens;

        zoomCoroutine = null;
    }

    private void StartCameraFollowBlend()
    {
        if (cinemachineCamera == null || !smoothCameraFollowOnPlay)
        {
            return;
        }

        Transform target = cameraFollowTarget != null
            ? cameraFollowTarget
            : player != null
                ? player.transform
                : null;

        if (target == null || cinemachineCamera.Follow == target)
        {
            return;
        }

        CacheOriginalCameraFollow();

        if (followBlendCoroutine != null)
        {
            StopCoroutine(followBlendCoroutine);
        }

        followBlendCoroutine = StartCoroutine(BlendCameraFollow(target));
    }

    private IEnumerator BlendCameraFollow(Transform target)
    {
        EnsureCameraFollowProxy();

        Transform currentFollow = cinemachineCamera.Follow;
        Vector3 startPosition = currentFollow != null
            ? currentFollow.position
            : cinemachineCamera.transform.position;

        cameraFollowProxy.position = startPosition;
        cinemachineCamera.Follow = cameraFollowProxy;

        float duration = Mathf.Max(0.001f, followBlendDuration);
        float timer = 0f;

        while (target != null && timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            progress = Mathf.SmoothStep(0f, 1f, progress);

            cameraFollowProxy.position = Vector3.Lerp(
                startPosition,
                target.position,
                progress);

            yield return null;
        }

        if (target != null)
        {
            cameraFollowProxy.position = target.position;
            cinemachineCamera.Follow = target;
        }

        followBlendCoroutine = null;
    }

    private void CacheOriginalCameraFollow()
    {
        if (originalCameraFollowCached || cinemachineCamera == null)
        {
            return;
        }

        originalCameraFollow = cinemachineCamera.Follow;
        originalCameraFollowCached = true;
    }

    private void EnsureCameraFollowProxy()
    {
        if (cameraFollowProxy != null)
        {
            return;
        }

        cameraFollowProxyObject = new GameObject("PointA Camera Follow Blend Target");
        cameraFollowProxyObject.hideFlags = HideFlags.HideAndDontSave;
        cameraFollowProxy = cameraFollowProxyObject.transform;
    }

    private void CachePlayerRigidbody()
    {
        if (player == null)
        {
            playerRb = null;
            return;
        }

        if (playerRb == null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
        }
    }

    private void OnDisable()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        if (zoomCoroutine != null)
        {
            StopCoroutine(zoomCoroutine);
            zoomCoroutine = null;
        }

        if (followBlendCoroutine != null)
        {
            StopCoroutine(followBlendCoroutine);
            followBlendCoroutine = null;
        }

        if (restoreCameraFollowOnDisable
            && cinemachineCamera != null
            && originalCameraFollowCached)
        {
            cinemachineCamera.Follow = originalCameraFollow;
        }
        else if (cinemachineCamera != null
                 && cameraFollowProxy != null
                 && cinemachineCamera.Follow == cameraFollowProxy)
        {
            cinemachineCamera.Follow = ResolveCameraFollowTarget();
        }

        DestroyCameraFollowProxy();

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
        }

        if (player != null)
        {
            player.SetControlLocked(false);
        }
    }

    private IEnumerator DisableCutsceneWhenFinished()
    {
        while (timelineCutscene != null
               && timelineCutscene.state == PlayState.Playing)
        {
            yield return null;
        }

        GameObject target = cutsceneObject != null
            ? cutsceneObject
            : timelineCutscene != null
                ? timelineCutscene.gameObject
                : null;

        if (target != null)
        {
            target.SetActive(false);
        }
    }

    private void DestroyCameraFollowProxy()
    {
        if (cameraFollowProxyObject == null)
        {
            cameraFollowProxy = null;
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(cameraFollowProxyObject);
        }
        else
#endif
        {
            Destroy(cameraFollowProxyObject);
        }

        cameraFollowProxyObject = null;
        cameraFollowProxy = null;
    }

    private Transform ResolveCameraFollowTarget()
    {
        if (cameraFollowTarget != null)
        {
            return cameraFollowTarget;
        }

        return player != null ? player.transform : null;
    }
}

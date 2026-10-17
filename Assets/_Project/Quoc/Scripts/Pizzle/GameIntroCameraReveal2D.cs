using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class GameIntroCameraReveal2D : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float gameViewHoldDuration = 1f;
    [SerializeField] private float cameraMoveToTargetDuration = 1f;
    [SerializeField] private float targetViewHoldDuration = 1f;
    [SerializeField] private float cameraReturnDuration = 1f;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Transform focusTarget;

    [Header("Player Lock")]
    [SerializeField] private Player mainPlayer;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private Behaviour[] movementScriptsToDisable;
    [SerializeField] private bool lockPlayerDuringCameraFocus = true;

    [Header("Intro Fade")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private bool fadeInOnPlay = true;
    [SerializeField] private float fadeInDuration = 2.5f;
    [SerializeField] private float minimumFadeInDuration = 2f;
    [SerializeField] private float startBlackHoldDuration = 0.15f;
    [SerializeField] private Color fadeColor = Color.black;

    private bool[] movementOriginalEnabled;
    private Transform originalFollow;
    private Transform cameraProxy;
    private Coroutine introCoroutine;
    private Coroutine fadeCoroutine;
    private bool playerLocked;

    private void Awake()
    {
        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
        }

        if (cinemachineCamera != null && playerTarget == null)
        {
            playerTarget = cinemachineCamera.Follow;
        }

        if (mainPlayer == null && playerTarget != null)
        {
            mainPlayer = playerTarget.GetComponentInParent<Player>();
        }

        if (playerRigidbody == null && playerTarget != null)
        {
            playerRigidbody = playerTarget.GetComponentInParent<Rigidbody2D>();
        }

        CacheMovementState();

        if (playOnStart && fadeInOnPlay && fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.raycastTarget = false;
            SetFadeAlpha(1f);
        }
        else
        {
            HideFadeImage();
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlayIntro();
        }
    }

    private void OnDisable()
    {
        if (playerLocked)
        {
            LockPlayer(false);
        }

        StopFade();
        HideFadeImage();
        RestoreCameraFollow();
    }

    public void PlayIntro()
    {
        if (introCoroutine != null)
        {
            StopCoroutine(introCoroutine);
        }

        PlayIntroFade();
        introCoroutine = StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        if (gameViewHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(gameViewHoldDuration);
        }

        if (cinemachineCamera == null || focusTarget == null)
        {
            introCoroutine = null;
            yield break;
        }

        originalFollow = cinemachineCamera.Follow != null ? cinemachineCamera.Follow : playerTarget;
        Transform returnTarget = playerTarget != null ? playerTarget : originalFollow;

        if (lockPlayerDuringCameraFocus)
        {
            LockPlayer(true);
        }

        cameraProxy = CreateCameraProxy(originalFollow != null ? originalFollow.position : cinemachineCamera.transform.position);
        cinemachineCamera.Follow = cameraProxy;

        yield return MoveProxyRoutine(focusTarget.position, cameraMoveToTargetDuration);

        if (targetViewHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(targetViewHoldDuration);
        }

        if (returnTarget != null)
        {
            yield return MoveProxyRoutine(returnTarget.position, cameraReturnDuration);
            cinemachineCamera.Follow = returnTarget;
        }
        else
        {
            RestoreCameraFollow();
        }

        if (lockPlayerDuringCameraFocus)
        {
            LockPlayer(false);
        }

        DestroyCameraProxy();
        introCoroutine = null;
    }

    private void PlayIntroFade()
    {
        if (!fadeInOnPlay || fadeImage == null)
        {
            return;
        }

        StopFade();
        fadeCoroutine = StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        fadeImage.gameObject.SetActive(true);
        fadeImage.raycastTarget = false;

        SetFadeAlpha(1f);

        if (startBlackHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(startBlackHoldDuration);
        }

        float duration = Mathf.Max(0.01f, minimumFadeInDuration, fadeInDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetFadeAlpha(Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        HideFadeImage();
        fadeCoroutine = null;
    }

    private IEnumerator MoveProxyRoutine(Vector3 targetPosition, float duration)
    {
        if (cameraProxy == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            cameraProxy.position = targetPosition;
            yield break;
        }

        Vector3 startPosition = cameraProxy.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            cameraProxy.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        cameraProxy.position = targetPosition;
    }

    private void LockPlayer(bool locked)
    {
        if (playerLocked == locked)
        {
            return;
        }

        playerLocked = locked;

        if (mainPlayer == null)
        {
            mainPlayer = FindFirstObjectByType<Player>();
        }

        if (mainPlayer != null)
        {
            mainPlayer.SetControlLocked(locked);
        }

        if (movementScriptsToDisable != null)
        {
            for (int i = 0; i < movementScriptsToDisable.Length; i++)
            {
                if (movementScriptsToDisable[i] == null)
                {
                    continue;
                }

                bool originalEnabled = movementOriginalEnabled != null
                    && i < movementOriginalEnabled.Length
                    && movementOriginalEnabled[i];

                movementScriptsToDisable[i].enabled = locked ? false : originalEnabled;
            }
        }

        if (playerRigidbody != null && locked)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
        }
    }

    private void CacheMovementState()
    {
        if (movementScriptsToDisable == null)
        {
            movementOriginalEnabled = null;
            return;
        }

        movementOriginalEnabled = new bool[movementScriptsToDisable.Length];
        for (int i = 0; i < movementScriptsToDisable.Length; i++)
        {
            movementOriginalEnabled[i] = movementScriptsToDisable[i] != null
                && movementScriptsToDisable[i].enabled;
        }
    }

    private Transform CreateCameraProxy(Vector3 position)
    {
        DestroyCameraProxy();
        GameObject proxyObject = new GameObject("Game Intro Camera Follow Proxy");
        proxyObject.hideFlags = HideFlags.HideInHierarchy;
        proxyObject.transform.position = position;
        return proxyObject.transform;
    }

    private void DestroyCameraProxy()
    {
        if (cameraProxy == null)
        {
            return;
        }

        Destroy(cameraProxy.gameObject);
        cameraProxy = null;
    }

    private void RestoreCameraFollow()
    {
        if (cinemachineCamera != null && originalFollow != null)
        {
            cinemachineCamera.Follow = originalFollow;
        }

        DestroyCameraProxy();
    }

    private void StopFade()
    {
        if (fadeCoroutine == null)
        {
            return;
        }

        StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null)
        {
            return;
        }

        Color color = fadeColor;
        color.a = Mathf.Clamp01(alpha);
        fadeImage.color = color;
    }

    private void HideFadeImage()
    {
        if (fadeImage == null)
        {
            return;
        }

        SetFadeAlpha(0f);
        fadeImage.raycastTarget = false;
        fadeImage.gameObject.SetActive(false);
    }

}

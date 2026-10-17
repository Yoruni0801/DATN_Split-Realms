using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    private Camera controlledCamera;
    private Coroutine shakeCoroutine;
    private Coroutine zoomCoroutine;
    private float originalSize;
    private Vector3 originalLocalPosition;

    public Camera ControlledCamera => controlledCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        controlledCamera = GetComponent<Camera>();
        originalSize = controlledCamera.orthographicSize;
        originalLocalPosition = transform.localPosition;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ShakeCamera(float duration, float magnitude)
    {
        RestartCoroutine(ref shakeCoroutine, ShakeRoutine(duration, magnitude));
    }

    public void ZoomCamera(float targetSize, float duration)
    {
        RestartCoroutine(ref zoomCoroutine, ZoomRoutine(targetSize, duration));
    }

    private void RestartCoroutine(ref Coroutine coroutine, IEnumerator routine)
    {
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }

        coroutine = StartCoroutine(routine);
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            Vector2 offset = Random.insideUnitCircle * magnitude;
            transform.localPosition = originalLocalPosition + (Vector3)offset;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalLocalPosition;
        shakeCoroutine = null;
    }

    private IEnumerator ZoomRoutine(float targetSize, float duration)
    {
        yield return LerpCameraSize(controlledCamera.orthographicSize, targetSize, duration);
        yield return new WaitForSeconds(1f);
        yield return LerpCameraSize(targetSize, originalSize, duration);
        zoomCoroutine = null;
    }

    private IEnumerator LerpCameraSize(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            controlledCamera.orthographicSize = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            controlledCamera.orthographicSize = Mathf.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        controlledCamera.orthographicSize = to;
    }
}

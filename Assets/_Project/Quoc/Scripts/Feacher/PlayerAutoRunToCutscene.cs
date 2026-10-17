using System.Collections;
using CutsceneEngine;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PlayerAutoRunToCutscene : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private Transform pointB;
    [SerializeField] private Cutscene cutscene;
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private GameObject miniGameCanvas;
    [SerializeField] private CanvasGroup miniGameCanvasGroup;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float arriveDistance = 0.08f;

    [Header("Auto Move")]
    [SerializeField] private float moveSpeed = 4f;

    [Header("Camera Zoom")]
    [SerializeField] private bool enableCameraZoom = true;
    [SerializeField] private float targetOrthographicSize = 6f;
    [SerializeField] private float targetFieldOfView = 35f;
    [SerializeField] private float zoomDuration = 0.35f;

    [Header("Lock")]
    [SerializeField] private bool lockDuringMove = true;
    [SerializeField] private bool lockDuringCutscene = true;

    [Header("Mini Game Fade")]
    [SerializeField] private float miniGameFadeDuration = 0.35f;

    private Rigidbody2D playerRb;
    private RigidbodyType2D originalBodyType;
    private bool originalBodyTypeCached;

    private bool sequenceRunning;
    private bool hasTriggered;
    private bool waitingForCutsceneEnd;
    private Coroutine finishSequenceCoroutine;
    private Coroutine miniGameFadeCoroutine;
    private bool originalLensCached;
    private LensSettings originalLens;
    private Collider2D triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        EnsureTriggerCollider();

        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
        }

        CachePlayerRigidbody();
    }

    private void OnEnable()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
        }

        EnsureTriggerCollider();

        CachePlayerRigidbody();
    }

    private void OnDisable()
    {
        if (cutscene != null)
        {
            cutscene.onStateChanged -= HandleCutsceneStateChanged;
        }

        if (finishSequenceCoroutine != null)
        {
            StopCoroutine(finishSequenceCoroutine);
            finishSequenceCoroutine = null;
        }

        if (miniGameFadeCoroutine != null)
        {
            StopCoroutine(miniGameFadeCoroutine);
            miniGameFadeCoroutine = null;
        }

        RestorePlayerPhysics();
        RestoreCameraLens();

        if (player != null)
        {
            if (miniGameCanvas == null)
            {
                player.SetControlLocked(false);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (sequenceRunning || hasTriggered || pointB == null)
        {
            return;
        }

        if (other == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))
        {
            return;
        }

        if (player == null)
        {
            player = other.GetComponent<Player>();
            if (player == null)
            {
                player = other.GetComponentInParent<Player>();
            }
        }

        if (player == null)
        {
            return;
        }

        hasTriggered = true;
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        sequenceRunning = true;

        if (lockDuringMove && player != null)
        {
            player.SetControlLocked(true);
        }

        CachePlayerRigidbody();
        ApplyKinematicMovementMode();
        CacheCameraLens();

        Vector3 startPos = player.transform.position;
        Vector3 targetPos = pointB.position;
        float zoomTime = Mathf.Max(0.01f, zoomDuration);
        float elapsed = 0f;

        while (Vector2.Distance((Vector2)player.transform.position, (Vector2)targetPos) > arriveDistance)
        {
            elapsed += Time.fixedDeltaTime;

            Vector2 pseudoVelocity = ((Vector2)targetPos - (Vector2)player.transform.position).normalized * moveSpeed;

            Vector3 nextPos = Vector3.MoveTowards(
                player.transform.position,
                targetPos,
                moveSpeed * Time.fixedDeltaTime);

            if (playerRb != null)
            {
                playerRb.MovePosition(nextPos);
            }
            else
            {
                player.transform.position = nextPos;
            }

            if (playerRb != null)
            {
                // Giữ velocity để hệ animation của Player tự chuyển sang Walk trong lúc auto-run.
                playerRb.linearVelocity = pseudoVelocity;
            }

            if (enableCameraZoom)
            {
                ApplyCameraZoom(Mathf.Clamp01(elapsed / zoomTime));
            }

            yield return new WaitForFixedUpdate();
        }

        if (playerRb != null)
        {
            playerRb.MovePosition(targetPos);
            playerRb.linearVelocity = Vector2.zero;
        }
        else
        {
            player.transform.position = targetPos;
        }

        if (enableCameraZoom)
        {
            ApplyCameraZoom(1f);
        }

        if (cutscene != null)
        {
            waitingForCutsceneEnd = true;
            cutscene.onStateChanged += HandleCutsceneStateChanged;
            if (cutscene.state != CutsceneState.Playing)
            {
                cutscene.Play();
            }

            if (!lockDuringCutscene && player != null)
            {
                player.SetControlLocked(false);
            }
        }
        else
        {
            StartFinishSequence();
        }
    }

    private void HandleCutsceneStateChanged(CutsceneState state)
    {
        if (!waitingForCutsceneEnd)
        {
            return;
        }

        if (state == CutsceneState.None)
        {
            StartFinishSequence(true);
        }
    }

    private void StartFinishSequence(bool disableCutsceneObject = false)
    {
        if (finishSequenceCoroutine != null)
        {
            return;
        }

        finishSequenceCoroutine = StartCoroutine(FinishSequenceRoutine(disableCutsceneObject));
    }

    private IEnumerator FinishSequenceRoutine(bool disableCutsceneObject)
    {
        if (!waitingForCutsceneEnd && !sequenceRunning)
        {
            finishSequenceCoroutine = null;
            yield break;
        }

        waitingForCutsceneEnd = false;

        if (cutscene != null)
        {
            cutscene.onStateChanged -= HandleCutsceneStateChanged;
        }

        if (miniGameFadeCoroutine != null)
        {
            StopCoroutine(miniGameFadeCoroutine);
            miniGameFadeCoroutine = null;
        }

        if (enableCameraZoom && virtualCamera != null && originalLensCached)
        {
            LensSettings startLens = virtualCamera.Lens;
            float zoomTime = Mathf.Max(0.01f, zoomDuration);
            float elapsed = 0f;

            while (elapsed < zoomTime)
            {
                elapsed += Time.deltaTime;
                ApplyCameraZoom(startLens, originalLens, Mathf.Clamp01(elapsed / zoomTime));
                yield return null;
            }
        }

        RestoreCameraLens();
        RestorePlayerPhysics();

        if (disableCutsceneObject && cutscene != null)
        {
            cutscene.gameObject.SetActive(false);
        }

        ShowMiniGameWithFade();

        if (player != null)
        {
            player.SetControlLocked(false);
        }

        sequenceRunning = false;
        finishSequenceCoroutine = null;
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

        if (playerRb != null && !originalBodyTypeCached)
        {
            originalBodyType = playerRb.bodyType;
            originalBodyTypeCached = true;
        }
    }

    private void ShowMiniGameWithFade()
    {
        if (miniGameCanvas == null)
        {
            return;
        }

        if (miniGameCanvasGroup == null)
        {
            miniGameCanvasGroup = miniGameCanvas.GetComponent<CanvasGroup>();
        }

        miniGameCanvas.SetActive(true);

        if (miniGameCanvasGroup == null)
        {
            return;
        }

        miniGameCanvasGroup.alpha = 0f;
        miniGameCanvasGroup.interactable = false;
        miniGameCanvasGroup.blocksRaycasts = false;

        if (miniGameFadeCoroutine != null)
        {
            StopCoroutine(miniGameFadeCoroutine);
        }

        miniGameFadeCoroutine = StartCoroutine(FadeInMiniGameRoutine());
    }

    private IEnumerator FadeInMiniGameRoutine()
    {
        float fadeTime = Mathf.Max(0.01f, miniGameFadeDuration);
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeTime);

            if (miniGameCanvasGroup != null)
            {
                miniGameCanvasGroup.alpha = t;
            }

            yield return null;
        }

        if (miniGameCanvasGroup != null)
        {
            miniGameCanvasGroup.alpha = 1f;
            miniGameCanvasGroup.interactable = true;
            miniGameCanvasGroup.blocksRaycasts = true;
        }

        miniGameFadeCoroutine = null;
    }

    private void ApplyKinematicMovementMode()
    {
        if (playerRb == null)
        {
            return;
        }

        playerRb.linearVelocity = Vector2.zero;
        playerRb.angularVelocity = 0f;
        playerRb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void RestorePlayerPhysics()
    {
        if (playerRb == null || !originalBodyTypeCached)
        {
            return;
        }

        playerRb.linearVelocity = Vector2.zero;
        playerRb.angularVelocity = 0f;
        playerRb.bodyType = originalBodyType;
    }

    private void CacheCameraLens()
    {
        if (virtualCamera == null || originalLensCached)
        {
            return;
        }

        originalLens = virtualCamera.Lens;
        originalLensCached = true;
    }

    private void ApplyCameraZoom(float t)
    {
        if (virtualCamera == null)
        {
            return;
        }

        LensSettings lens = originalLensCached ? originalLens : virtualCamera.Lens;
        if (lens.Orthographic)
        {
            lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, targetOrthographicSize, t);
        }
        else
        {
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFieldOfView, t);
        }

        virtualCamera.Lens = lens;
    }

    private void ApplyCameraZoom(LensSettings startLens, LensSettings endLens, float t)
    {
        if (virtualCamera == null)
        {
            return;
        }

        LensSettings lens = startLens;
        if (startLens.Orthographic)
        {
            lens.OrthographicSize = Mathf.Lerp(startLens.OrthographicSize, endLens.OrthographicSize, t);
        }
        else
        {
            lens.FieldOfView = Mathf.Lerp(startLens.FieldOfView, endLens.FieldOfView, t);
        }

        virtualCamera.Lens = lens;
    }

    private void RestoreCameraLens()
    {
        if (virtualCamera == null || !originalLensCached)
        {
            return;
        }

        virtualCamera.Lens = originalLens;
        originalLensCached = false;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D gizmoTrigger = triggerCollider != null ? triggerCollider : GetComponent<Collider2D>();
        if (gizmoTrigger != null)
        {
            Gizmos.color = Color.yellow;
            Bounds bounds = gizmoTrigger.bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        if (pointB != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pointB.position, arriveDistance);
        }
    }

    private void OnValidate()
    {
        triggerCollider = GetComponent<Collider2D>();
        EnsureTriggerCollider();
    }

    private void EnsureTriggerCollider()
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

using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

public class CameraFollowTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Player player;

    [Header("Settings")]
    [SerializeField] private bool removeTargetOnStart = true;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool invalidatePreviousStateOnFollow;
    [SerializeField] private bool smoothFollowOnTrigger = true;
    [SerializeField] private float followBlendDuration = 0.5f;

    private bool activated;
    private Coroutine followBlendCoroutine;
    private GameObject followBlendProxyObject;
    private Transform followBlendProxy;
    private Transform pendingFollowTarget;

    private void Awake()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
        }
    }

    private void Start()
    {
        if (cinemachineCamera != null && removeTargetOnStart)
        {
            // Ban đầu camera không theo Player
            cinemachineCamera.Follow = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated && triggerOnce)
        {
            return;
        }

        Player enteredPlayer = other.GetComponentInParent<Player>();

        if (enteredPlayer == null || cinemachineCamera == null)
        {
            return;
        }

        activated = true;

        if (followBlendCoroutine != null)
        {
            StopCoroutine(followBlendCoroutine);
        }

        Transform followTarget = enteredPlayer.transform;
        if (smoothFollowOnTrigger && followBlendDuration > 0.001f)
        {
            followBlendCoroutine = StartCoroutine(BlendToFollowTarget(followTarget));
            return;
        }

        SetFollowTarget(followTarget);
        DisableAfterTriggered();
    }

    private IEnumerator BlendToFollowTarget(Transform followTarget)
    {
        EnsureFollowBlendProxy();
        pendingFollowTarget = followTarget;

        Vector3 startPosition = GetCurrentFollowPosition();
        followBlendProxy.position = startPosition;
        cinemachineCamera.Follow = followBlendProxy;

        float duration = Mathf.Max(0.001f, followBlendDuration);
        float timer = 0f;

        while (followTarget != null && timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            progress = Mathf.SmoothStep(0f, 1f, progress);

            followBlendProxy.position = Vector3.Lerp(
                startPosition,
                followTarget.position,
                progress);

            yield return null;
        }

        if (followTarget != null)
        {
            SetFollowTarget(followTarget);
        }

        pendingFollowTarget = null;
        followBlendCoroutine = null;
        DestroyFollowBlendProxy();
        DisableAfterTriggered();
    }

    private Vector3 GetCurrentFollowPosition()
    {
        Transform currentFollow = cinemachineCamera.Follow;
        if (currentFollow != null)
        {
            return currentFollow.position;
        }

        CinemachineFollow follow = cinemachineCamera.GetComponent<CinemachineFollow>();
        if (follow != null)
        {
            return cinemachineCamera.transform.position - follow.FollowOffset;
        }

        return cinemachineCamera.transform.position;
    }

    private void SetFollowTarget(Transform followTarget)
    {
        cinemachineCamera.Follow = followTarget;

        if (invalidatePreviousStateOnFollow)
        {
            cinemachineCamera.PreviousStateIsValid = false;
        }
    }

    private void EnsureFollowBlendProxy()
    {
        if (followBlendProxy != null)
        {
            return;
        }

        followBlendProxyObject = new GameObject("Camera Follow Trigger Blend Target");
        followBlendProxyObject.hideFlags = HideFlags.HideAndDontSave;
        followBlendProxy = followBlendProxyObject.transform;
    }

    private void DisableAfterTriggered()
    {
        if (triggerOnce)
        {
            enabled = false;
        }
    }

    private void OnDisable()
    {
        if (followBlendCoroutine != null)
        {
            StopCoroutine(followBlendCoroutine);
            followBlendCoroutine = null;
        }

        if (cinemachineCamera != null
            && followBlendProxy != null
            && cinemachineCamera.Follow == followBlendProxy)
        {
            cinemachineCamera.Follow = pendingFollowTarget;
        }

        pendingFollowTarget = null;
        DestroyFollowBlendProxy();
    }

    private void DestroyFollowBlendProxy()
    {
        if (followBlendProxyObject == null)
        {
            followBlendProxy = null;
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(followBlendProxyObject);
        }
        else
#endif
        {
            Destroy(followBlendProxyObject);
        }

        followBlendProxyObject = null;
        followBlendProxy = null;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

public class FairyTimelineAscendBehaviour : PlayableBehaviour
{
    public float ascentSpeed = 4f;
    public float scatterRadius = 6f;
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve alphaScaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    public bool deactivateOnComplete = true;

    [Header("Camera Follow")]
    public bool followWithCamera = true;
    public Vector3 cameraOffset = new Vector3(0f, 0f, -10f);

    private struct FairyInitialState
    {
        public FairyPlayerCompanionController fairy;
        public Vector3 startPos;
        public Vector3 startScale;
        public Vector3 flightDirection;
        public SpriteRenderer[] spriteRenderers;
        public Color[] originalColors;
    }

    private readonly List<FairyInitialState> trackedFairies = new List<FairyInitialState>();
    private bool initialized;

    private Transform cameraFocusTarget;
    private CinemachineCamera targetCinemachineCamera;
    private Transform savedCameraFollow;

    public override void OnGraphStart(Playable playable)
    {
        initialized = false;
        trackedFairies.Clear();
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying) return;

        double duration = playable.GetDuration();
        if (duration <= 0.0001) return;

        float progress = (float)(playable.GetTime() / duration);
        progress = Mathf.Clamp01(progress);

        if (!initialized)
        {
            InitializeFairies();
            initialized = true;
        }

        float evalMotion = motionCurve != null ? motionCurve.Evaluate(progress) : progress;
        float evalAlphaScale = alphaScaleCurve != null ? alphaScaleCurve.Evaluate(progress) : (1f - progress);

        Vector3 centerPoint = Vector3.zero;
        int activeCount = 0;

        for (int i = 0; i < trackedFairies.Count; i++)
        {
            var state = trackedFairies[i];
            if (state.fairy == null) continue;

            // M?i con bay theo vector hu?ng ng?u nhiên/to? tròn riêng k?t h?p bay lên trên
            Vector3 movementOffset = state.flightDirection * (evalMotion * (ascentSpeed * (float)duration + scatterRadius));

            state.fairy.transform.position = state.startPos + movementOffset;
            state.fairy.transform.localScale = state.startScale * Mathf.Max(0f, evalAlphaScale);

            centerPoint += state.fairy.transform.position;
            activeCount++;

            if (state.spriteRenderers != null)
            {
                for (int r = 0; r < state.spriteRenderers.Length; r++)
                {
                    if (state.spriteRenderers[r] != null)
                    {
                        Color c = state.originalColors[r];
                        c.a *= Mathf.Clamp01(evalAlphaScale);
                        state.spriteRenderers[r].color = c;
                    }
                }
            }

            if (deactivateOnComplete && progress >= 0.999f)
            {
                if (state.fairy.gameObject.activeSelf)
                {
                    state.fairy.gameObject.SetActive(false);
                }
            }
        }

        if (followWithCamera && activeCount > 0)
        {
            centerPoint /= activeCount;
            if (cameraFocusTarget != null)
            {
                cameraFocusTarget.position = centerPoint;
            }
            else if (Camera.main != null && targetCinemachineCamera == null)
            {
                Vector3 targetCamPos = centerPoint + cameraOffset;
                Camera.main.transform.position = new Vector3(targetCamPos.x, targetCamPos.y, Camera.main.transform.position.z);
            }
        }
    }

    private void InitializeFairies()
    {
        trackedFairies.Clear();

#if UNITY_2023_1_OR_NEWER
        var allFairies = Object.FindObjectsByType<FairyPlayerCompanionController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var allFairies = Object.FindObjectsOfType<FairyPlayerCompanionController>();
#endif

        if (allFairies == null || allFairies.Length == 0)
        {
            var staticList = FairyPlayerCompanionController.AllActiveFairies;
            if (staticList != null && staticList.Count > 0)
            {
                allFairies = new FairyPlayerCompanionController[staticList.Count];
                for (int i = 0; i < staticList.Count; i++) allFairies[i] = staticList[i];
            }
        }

        if (allFairies == null || allFairies.Length == 0) return;

        Vector3 initialCenter = Vector3.zero;
        int count = allFairies.Length;

        for (int i = 0; i < count; i++)
        {
            var fairy = allFairies[i];
            if (fairy == null) continue;

            fairy.SetAutoFindPlayer(false);
            fairy.SetCastingLocked(true);

            var srs = fairy.GetComponentsInChildren<SpriteRenderer>(true);
            Color[] colors = null;
            if (srs != null && srs.Length > 0)
            {
                colors = new Color[srs.Length];
                for (int c = 0; c < srs.Length; c++)
                {
                    colors[c] = srs[c] != null ? srs[c].color : Color.white;
                }
            }

            // Chia d?u các góc to? ra hình cánh qu?t/hình tròn hu?ng lên b?u tr?i
            // Góc phân b? t? kho?ng 30 d? d?n 150 d? (hu?ng lên trên và sang 2 bên)
            float angleDeg;
            if (count == 1)
            {
                angleDeg = 90f; // Bay th?ng lên
            }
            else
            {
                float t = (float)i / (count - 1);
                angleDeg = Mathf.Lerp(30f, 150f, t) + Random.Range(-10f, 10f);
            }

            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f).normalized;

            initialCenter += fairy.transform.position;

            trackedFairies.Add(new FairyInitialState
            {
                fairy = fairy,
                startPos = fairy.transform.position,
                startScale = fairy.transform.localScale,
                flightDirection = direction,
                spriteRenderers = srs,
                originalColors = colors
            });
        }

        if (followWithCamera && trackedFairies.Count > 0)
        {
            initialCenter /= trackedFairies.Count;
            if (cameraFocusTarget == null)
            {
                GameObject focusObj = new GameObject("FairyAscendCameraFocus");
                focusObj.transform.position = initialCenter;
                cameraFocusTarget = focusObj.transform;
            }

            CinemachineCamera[] cams = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (cams != null && cams.Length > 0)
            {
                foreach (var c in cams)
                {
                    if (c != null && c.isActiveAndEnabled)
                    {
                        targetCinemachineCamera = c;
                        break;
                    }
                }
                if (targetCinemachineCamera == null) targetCinemachineCamera = cams[0];
            }

            if (targetCinemachineCamera != null)
            {
                savedCameraFollow = targetCinemachineCamera.Follow;
                targetCinemachineCamera.Follow = cameraFocusTarget;
            }
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (deactivateOnComplete && Application.isPlaying && initialized)
        {
            for (int i = 0; i < trackedFairies.Count; i++)
            {
                if (trackedFairies[i].fairy != null && trackedFairies[i].fairy.gameObject.activeSelf)
                {
                    trackedFairies[i].fairy.gameObject.SetActive(false);
                }
            }
        }

        if (targetCinemachineCamera != null && savedCameraFollow != null)
        {
            targetCinemachineCamera.Follow = savedCameraFollow;
            targetCinemachineCamera = null;
        }

        if (cameraFocusTarget != null)
        {
            Object.Destroy(cameraFocusTarget.gameObject);
            cameraFocusTarget = null;
        }
    }
}

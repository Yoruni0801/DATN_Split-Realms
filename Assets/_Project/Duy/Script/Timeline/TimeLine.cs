using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum TimelineFocusTargetType
{
    UI,
    World,
    ScreenPoint
}

[System.Serializable]
public class TimelineFocusEvent
{
    [Header("Timeline")]
    [Tooltip("Thời gian kích hoạt focus, tính bằng giây theo audio")]
    public float triggerTime;

    [Tooltip("Thời gian duy trì focus. = 0 thì giữ cho tới event tiếp theo")]
    public float focusDuration = 2f;

    [Header("Target Type")]
    public TimelineFocusTargetType targetType = TimelineFocusTargetType.UI;

    [Header("UI Target")]
    [Tooltip("UI cần focus nếu Target Type là UI")]
    public RectTransform targetUI;

    [Tooltip("Canvas chứa target UI. Nếu để trống, script sẽ tự tìm canvas cha")]
    public Canvas targetCanvas;

    [Header("World Target")]
    [Tooltip("Object trong scene cần focus nếu Target Type là World")]
    public Transform targetWorld;

    [Tooltip("Vị trí world thủ công nếu không có targetWorld")]
    public Vector3 worldPosition;

    [Header("Screen Target")]
    [Tooltip("Vị trí focus trên màn hình nếu Target Type là ScreenPoint")]
    public Vector2 screenPosition;

    [Header("Focus Size")]
    [Tooltip("Bán kính vùng focus, tính bằng pixel với UI/ScreenPoint, hoặc đơn vị world với World")]
    public float focusSize = 120f;

    [Tooltip("Cộng thêm khoảng trống quanh target")]
    public float padding = 30f;

    [HideInInspector]
    public bool hasTriggered;
}

[System.Serializable]
public class OnFocusAction : UnityEvent<Vector2, float> { }

[RequireComponent(typeof(AudioSource))]
public class TimeLine : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip timelineAudio;
    public bool playOnStart = false;

    [Header("Timeline Events")]
    public List<TimelineFocusEvent> focusEvents = new List<TimelineFocusEvent>();

    [Header("Focus Overlay")]
    public bool useFocusOverlay = true;
    public Color overlayColor = new Color(0f, 0f, 0f, 0.8f);

    [Tooltip("Tốc độ di chuyển/zoom focus")]
    public float focusTransitionSpeed = 8f;

    [Tooltip("Kích thước focus mặc định khi target lỗi")]
    public float fallbackFocusSize = 140f;

    [Header("Debug")]
    public bool allowMouseTest = false;
    public float mouseTestSize = 140f;
    public float mouseTestDuration = 2f;

    [Header("Events")]
    public OnFocusAction onFocusTriggered;

    private Camera mainCam;

    private GameObject overlayCanvasObject;
    private Canvas overlayCanvas;
    private CanvasScaler overlayScaler;
    private CanvasGroup overlayCanvasGroup;

    private RawImage centerHole;
    private Image topSolid;
    private Image bottomSolid;
    private Image leftSolid;
    private Image rightSolid;

    private Texture2D holeTexture;

    private Vector2 targetScreenPos;
    private float targetSize;
    private float targetAlpha;

    private Vector2 currentScreenPos;
    private float currentSize;
    private float currentAlpha;

    private float currentFocusDuration;

    private bool initialized;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        mainCam = Camera.main;
    }

    private void Start()
    {
        focusEvents.Sort((a, b) => a.triggerTime.CompareTo(b.triggerTime));

        if (useFocusOverlay)
            InitOverlay();

        if (playOnStart)
            PlayTimeline();
    }

    private void Update()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            CheckTimelineEvents(audioSource.time);
        }

        if (allowMouseTest && Input.GetMouseButtonDown(0))
        {
            FocusAtScreenPoint(Input.mousePosition, mouseTestSize, mouseTestDuration);
        }

        UpdateFocusOverlayAnim();
    }

    private void CheckTimelineEvents(float currentTime)
    {
        for (int i = 0; i < focusEvents.Count; i++)
        {
            TimelineFocusEvent evt = focusEvents[i];

            if (!evt.hasTriggered && currentTime >= evt.triggerTime)
            {
                evt.hasTriggered = true;
                ApplyFocus(evt);
            }
        }
    }

    private void ApplyFocus(TimelineFocusEvent evt)
    {
        Vector2 screenPos;
        float size;

        bool success = TryGetFocusData(evt, out screenPos, out size);

        if (!success)
        {
            screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            size = fallbackFocusSize;
        }

        targetScreenPos = screenPos;
        targetSize = Mathf.Max(1f, size);
        targetAlpha = overlayColor.a;
        currentFocusDuration = evt.focusDuration;

        onFocusTriggered?.Invoke(targetScreenPos, targetSize);
    }

    private bool TryGetFocusData(TimelineFocusEvent evt, out Vector2 screenPos, out float size)
    {
        screenPos = Vector2.zero;
        size = fallbackFocusSize;

        switch (evt.targetType)
        {
            case TimelineFocusTargetType.UI:
                return TryGetUIFocusData(evt, out screenPos, out size);

            case TimelineFocusTargetType.World:
                return TryGetWorldFocusData(evt, out screenPos, out size);

            case TimelineFocusTargetType.ScreenPoint:
                screenPos = evt.screenPosition;
                size = evt.focusSize + evt.padding;
                return true;
        }

        return false;
    }

    private bool TryGetUIFocusData(TimelineFocusEvent evt, out Vector2 screenPos, out float size)
    {
        screenPos = Vector2.zero;
        size = fallbackFocusSize;

        if (evt.targetUI == null)
            return false;

        Canvas canvas = evt.targetCanvas;

        if (canvas == null)
            canvas = evt.targetUI.GetComponentInParent<Canvas>();

        Camera uiCamera = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;

        Vector3[] corners = new Vector3[4];
        evt.targetUI.GetWorldCorners(corners);

        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);

        screenPos = (bottomLeft + topRight) * 0.5f;

        float width = Mathf.Abs(topRight.x - bottomLeft.x);
        float height = Mathf.Abs(topRight.y - bottomLeft.y);

        size = Mathf.Max(width, height) * 0.5f + evt.padding;

        return true;
    }

    private bool TryGetWorldFocusData(TimelineFocusEvent evt, out Vector2 screenPos, out float size)
    {
        screenPos = Vector2.zero;
        size = fallbackFocusSize;

        if (mainCam == null)
            mainCam = Camera.main;

        if (mainCam == null)
            return false;

        Vector3 worldPos = evt.targetWorld != null ? evt.targetWorld.position : evt.worldPosition;

        Vector3 screenPoint = mainCam.WorldToScreenPoint(worldPos);

        if (screenPoint.z < 0f)
            return false;

        screenPos = new Vector2(screenPoint.x, screenPoint.y);

        Vector3 rightOffsetWorld = worldPos + mainCam.transform.right * evt.focusSize;
        Vector3 rightOffsetScreen = mainCam.WorldToScreenPoint(rightOffsetWorld);

        size = Vector2.Distance(screenPos, rightOffsetScreen) + evt.padding;

        return true;
    }

    private void UpdateFocusOverlayAnim()
    {
        if (!useFocusOverlay || !initialized)
            return;

        if (currentFocusDuration > 0f)
        {
            currentFocusDuration -= Time.deltaTime;

            if (currentFocusDuration <= 0f)
            {
                targetAlpha = 0f;
            }
        }

        float lerpValue = Time.deltaTime * focusTransitionSpeed;

        currentScreenPos = Vector2.Lerp(currentScreenPos, targetScreenPos, lerpValue);
        currentSize = Mathf.Lerp(currentSize, targetSize, lerpValue);
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, lerpValue);

        Color currentColor = new Color(
            overlayColor.r,
            overlayColor.g,
            overlayColor.b,
            currentAlpha
        );

        topSolid.color = currentColor;
        bottomSolid.color = currentColor;
        leftSolid.color = currentColor;
        rightSolid.color = currentColor;
        centerHole.color = currentColor;

        centerHole.rectTransform.anchoredPosition = currentScreenPos;
        centerHole.rectTransform.sizeDelta = new Vector2(currentSize * 2f, currentSize * 2f);

        Rect centerRect = new Rect(
            currentScreenPos.x - currentSize,
            currentScreenPos.y - currentSize,
            currentSize * 2f,
            currentSize * 2f
        );

        SetRect(
            topSolid.rectTransform,
            0f,
            centerRect.yMax,
            Screen.width,
            Screen.height - centerRect.yMax
        );

        SetRect(
            bottomSolid.rectTransform,
            0f,
            0f,
            Screen.width,
            centerRect.yMin
        );

        SetRect(
            leftSolid.rectTransform,
            0f,
            centerRect.yMin,
            centerRect.xMin,
            centerRect.height
        );

        SetRect(
            rightSolid.rectTransform,
            centerRect.xMax,
            centerRect.yMin,
            Screen.width - centerRect.xMax,
            centerRect.height
        );
    }

    private void SetRect(RectTransform rt, float x, float y, float width, float height)
    {
        width = Mathf.Max(0f, width);
        height = Mathf.Max(0f, height);

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    private void InitOverlay()
    {
        if (initialized)
            return;

        overlayCanvasObject = new GameObject("Timeline Focus Overlay");

        overlayCanvas = overlayCanvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 32000;

        overlayScaler = overlayCanvasObject.AddComponent<CanvasScaler>();
        overlayScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        overlayCanvasObject.AddComponent<GraphicRaycaster>();

        overlayCanvasGroup = overlayCanvasObject.AddComponent<CanvasGroup>();
        overlayCanvasGroup.blocksRaycasts = false;
        overlayCanvasGroup.interactable = false;

        topSolid = CreateSolid("Top Solid");
        bottomSolid = CreateSolid("Bottom Solid");
        leftSolid = CreateSolid("Left Solid");
        rightSolid = CreateSolid("Right Solid");

        centerHole = CreateCenterHole();

        currentScreenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        targetScreenPos = currentScreenPos;

        currentSize = Screen.height * 2f;
        targetSize = currentSize;

        currentAlpha = 0f;
        targetAlpha = 0f;

        initialized = true;
    }

    private Image CreateSolid(string objectName)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(overlayCanvasObject.transform, false);

        Image image = obj.AddComponent<Image>();
        image.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 0f);
        image.raycastTarget = false;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;

        return image;
    }

    private RawImage CreateCenterHole()
    {
        GameObject obj = new GameObject("Center Soft Hole");
        obj.transform.SetParent(overlayCanvasObject.transform, false);

        RawImage rawImage = obj.AddComponent<RawImage>();
        rawImage.raycastTarget = false;

        RectTransform rt = rawImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        int texSize = 256;
        holeTexture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);

        Color[] pixels = new Color[texSize * texSize];

        Vector2 center = new Vector2(texSize * 0.5f, texSize * 0.5f);
        float radius = texSize * 0.5f;

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);
                float normalized = Mathf.Clamp01(distance / radius);

                float alpha = Mathf.SmoothStep(0f, 1f, normalized);

                pixels[y * texSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        holeTexture.SetPixels(pixels);
        holeTexture.Apply();

        rawImage.texture = holeTexture;
        rawImage.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 0f);

        return rawImage;
    }

    public void PlayTimeline()
    {
        if (audioSource == null)
            return;

        if (timelineAudio != null)
            audioSource.clip = timelineAudio;

        if (audioSource.clip == null)
            return;

        ResetEvents();
        audioSource.time = 0f;
        audioSource.Play();
    }

    public void StopTimeline()
    {
        if (audioSource != null)
            audioSource.Stop();

        ResetEvents();
        HideFocus();
    }

    public void PauseTimeline()
    {
        if (audioSource != null)
            audioSource.Pause();
    }

    public void ResumeTimeline()
    {
        if (audioSource != null)
            audioSource.UnPause();
    }

    public void ResetEvents()
    {
        for (int i = 0; i < focusEvents.Count; i++)
        {
            focusEvents[i].hasTriggered = false;
        }
    }

    public void HideFocus()
    {
        targetAlpha = 0f;
        currentFocusDuration = 0f;
    }

    public void FocusAtScreenPoint(Vector2 screenPoint, float size, float duration)
    {
        if (useFocusOverlay && !initialized)
            InitOverlay();

        targetScreenPos = screenPoint;
        targetSize = Mathf.Max(1f, size);
        targetAlpha = overlayColor.a;
        currentFocusDuration = duration;

        onFocusTriggered?.Invoke(targetScreenPos, targetSize);
    }

    public void FocusUI(RectTransform targetUI, float padding = 30f, float duration = 2f)
    {
        if (targetUI == null)
            return;

        TimelineFocusEvent evt = new TimelineFocusEvent
        {
            targetType = TimelineFocusTargetType.UI,
            targetUI = targetUI,
            padding = padding,
            focusDuration = duration
        };

        ApplyFocus(evt);
    }

    public void FocusWorld(Transform targetWorld, float worldSize = 1f, float padding = 30f, float duration = 2f)
    {
        if (targetWorld == null)
            return;

        TimelineFocusEvent evt = new TimelineFocusEvent
        {
            targetType = TimelineFocusTargetType.World,
            targetWorld = targetWorld,
            focusSize = worldSize,
            padding = padding,
            focusDuration = duration
        };

        ApplyFocus(evt);
    }

    private void OnDestroy()
    {
        if (holeTexture != null)
            Destroy(holeTexture);

        if (overlayCanvasObject != null)
            Destroy(overlayCanvasObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (focusEvents == null)
            return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);

        foreach (TimelineFocusEvent evt in focusEvents)
        {
            if (evt == null)
                continue;

            if (evt.targetType == TimelineFocusTargetType.World)
            {
                Vector3 pos = evt.targetWorld != null ? evt.targetWorld.position : evt.worldPosition;
                Gizmos.DrawSphere(pos, Mathf.Max(0.1f, evt.focusSize));
            }
        }
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(TimeLine))]
public class TimeLineEditor : Editor
{
    private void OnSceneGUI()
    {
        TimeLine timeline = (TimeLine)target;

        if (timeline.focusEvents == null)
            return;

        for (int i = 0; i < timeline.focusEvents.Count; i++)
        {
            TimelineFocusEvent evt = timeline.focusEvents[i];

            if (evt.targetType != TimelineFocusTargetType.World)
                continue;

            if (evt.targetWorld != null)
                continue;

            EditorGUI.BeginChangeCheck();

            Vector3 newPosition = Handles.PositionHandle(evt.worldPosition, Quaternion.identity);

            Handles.color = Color.cyan;
            float newSize = Handles.RadiusHandle(
                Quaternion.identity,
                evt.worldPosition,
                Mathf.Max(0.1f, evt.focusSize)
            );

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(timeline, "Change Timeline Focus Event");

                evt.worldPosition = newPosition;
                evt.focusSize = Mathf.Max(0.1f, newSize);

                EditorUtility.SetDirty(timeline);
            }

            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.yellow;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 13;

            Handles.Label(
                evt.worldPosition + Vector3.up * (evt.focusSize + 0.3f),
                $"Event {i} | Time: {evt.triggerTime}s | Duration: {evt.focusDuration}s",
                style
            );
        }
    }
}
#endif
using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

public class SpineAnimationReviewerWindow : EditorWindow
{
    private enum ReviewMode
    {
        State,
        RawAnimation
    }

    private const string MenuPath = "Tools/Duy/Spine Animation Reviewer";
    private const int SkillPageSize = 30;
    private const double PreviewFrameInterval = 1.0 / 30.0;

    private static readonly string[] ReviewModeLabels = { "State", "Raw Animation" };

    private static readonly AnimationSpine.State[] ReviewStates =
    {
        AnimationSpine.State.Idle,
        AnimationSpine.State.Walk,
        AnimationSpine.State.Run,
        AnimationSpine.State.Jump,
        AnimationSpine.State.Fall,
        AnimationSpine.State.SwimIdle,
        AnimationSpine.State.SwimMove,
        AnimationSpine.State.Range,
        AnimationSpine.State.Spear,
        AnimationSpine.State.Skill,
        AnimationSpine.State.Attack,
        AnimationSpine.State.SpecialAttack,
        AnimationSpine.State.Dash,
        AnimationSpine.State.Block,
        AnimationSpine.State.Hurt,
        AnimationSpine.State.Stun,
        AnimationSpine.State.Die,
        AnimationSpine.State.ParryStart,
        AnimationSpine.State.ParryFail,
        AnimationSpine.State.ParrySuccess,
        AnimationSpine.State.NormalAttack1,
        AnimationSpine.State.NormalAttack2,
        AnimationSpine.State.NormalAttack3
    };

    private readonly Dictionary<string, Spine.Animation> animationCache =
        new Dictionary<string, Spine.Animation>(StringComparer.Ordinal);

    private readonly Dictionary<string, int> duplicateCountCache =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private readonly List<SkillData> skillCache = new List<SkillData>();

    private GameObject targetObject;
    private AnimationSpine targetAnimationSpine;
    private SkeletonAnimation targetSkeletonAnimation;

    private GameObject previewClone;
    private SkeletonAnimation previewSkeletonAnimation;
    private TrackEntry previewTrackEntry;
    private string previewTrackAnimationName;
    private bool previewTrackLoop;

    private SkeletonData cachedSkeletonData;
    private string[] rawAnimationNames = Array.Empty<string>();
    private int mappingHash;
    private bool skillCacheDirty = true;

    private ReviewMode reviewMode;
    private AnimationSpine.State selectedState = AnimationSpine.State.Idle;
    private int rawAnimationIndex;
    private string selectedAnimationName;
    private bool loopOverride;
    private bool useManualLoop;
    private bool isPlaying;
    private float playbackTime;
    private float playbackSpeed = 1f;
    private float previewScale = 1f;
    private Vector3 previewOffset = new Vector3(2.5f, 0f, 0f);
    private double lastEditorTime;
    private double nextPreviewTime;
    private Vector2 scroll;

    private bool showStateReview = true;
    private bool showSkillReview;
    private int skillPage;

    private GUIStyle okStyle;
    private GUIStyle warningStyle;
    private GUIStyle errorStyle;
    private GUIStyle infoStyle;

    [MenuItem(MenuPath)]
    public static void Open()
    {
        SpineAnimationReviewerWindow window = GetWindow<SpineAnimationReviewerWindow>("Spine Reviewer");
        window.minSize = new Vector2(760f, 520f);
        window.TryUseSelection();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.update += TickPreview;
        EditorApplication.projectChanged += OnProjectChanged;
        Undo.undoRedoPerformed += OnUndoRedo;

        TryUseSelection();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        EditorApplication.update -= TickPreview;
        EditorApplication.projectChanged -= OnProjectChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;

        DestroyPreviewClone();
    }

    private void OnProjectChanged()
    {
        skillCacheDirty = true;
        RefreshTargetCaches();
        Repaint();
    }

    private void OnUndoRedo()
    {
        RefreshTargetCaches();
        ResetPreviewTrack();
        ApplyPreviewPose(true);
        Repaint();
    }

    private void OnSelectionChanged()
    {
        if (targetObject == null)
        {
            TryUseSelection();
            Repaint();
        }
    }

    private void OnGUI()
    {
        EnsureStyles();
        DrawTargetHeader();

        if (targetAnimationSpine == null || targetSkeletonAnimation == null)
        {
            EditorGUILayout.HelpBox(
                "Select or drag a GameObject that has AnimationSpine/SkeletonAnimation.",
                MessageType.Info);
            return;
        }

        EnsureTargetCachesAreCurrent();
        DrawPreviewControls();
        EditorGUILayout.Space(6f);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        showStateReview = EditorGUILayout.Foldout(showStateReview, "State Mapping", true);
        if (showStateReview)
        {
            DrawStateReview();
        }

        EditorGUILayout.Space(8f);

        showSkillReview = EditorGUILayout.Foldout(showSkillReview, "Skill References", true);
        if (showSkillReview)
        {
            DrawSkillReview();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTargetHeader()
    {
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        GameObject nextTarget = (GameObject)EditorGUILayout.ObjectField(
            "GameObject",
            targetObject,
            typeof(GameObject),
            true);

        if (EditorGUI.EndChangeCheck())
        {
            SetTarget(nextTarget);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selection", GUILayout.Width(120f)))
            {
                TryUseSelection();
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(120f)))
            {
                RefreshAll();
            }

            if (GUILayout.Button("Focus Clone", GUILayout.Width(110f)))
            {
                FocusPreviewClone();
            }
        }

        if (targetAnimationSpine != null)
        {
            EditorGUILayout.LabelField("AnimationSpine", targetAnimationSpine.name);
        }

        if (targetSkeletonAnimation != null && targetSkeletonAnimation.SkeletonDataAsset != null)
        {
            EditorGUILayout.LabelField("SkeletonData", targetSkeletonAnimation.SkeletonDataAsset.name);
        }
    }

    private void DrawPreviewControls()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Preview uses a lightweight editor-only SkeletonAnimation clone. Preview is capped at 30 FPS.",
            MessageType.None);

        ReviewMode nextReviewMode = (ReviewMode)GUILayout.Toolbar((int)reviewMode, ReviewModeLabels);
        if (nextReviewMode != reviewMode)
        {
            reviewMode = nextReviewMode;

            if (reviewMode == ReviewMode.State)
            {
                SelectState(selectedState);
            }
            else if (rawAnimationNames.Length > 0)
            {
                rawAnimationIndex = Mathf.Clamp(rawAnimationIndex, 0, rawAnimationNames.Length - 1);
                SelectRawAnimation(rawAnimationNames[rawAnimationIndex]);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (reviewMode == ReviewMode.State)
            {
                EditorGUI.BeginChangeCheck();
                selectedState = (AnimationSpine.State)EditorGUILayout.EnumPopup("State", selectedState);
                if (EditorGUI.EndChangeCheck())
                {
                    SelectState(selectedState);
                }
            }
            else if (rawAnimationNames.Length == 0)
            {
                EditorGUILayout.LabelField("Raw Animation", "(none)");
            }
            else
            {
                rawAnimationIndex = Mathf.Clamp(rawAnimationIndex, 0, rawAnimationNames.Length - 1);

                EditorGUI.BeginChangeCheck();
                rawAnimationIndex = EditorGUILayout.Popup(
                    "Raw Animation",
                    rawAnimationIndex,
                    rawAnimationNames);

                if (EditorGUI.EndChangeCheck())
                {
                    SelectRawAnimation(rawAnimationNames[rawAnimationIndex]);
                }
            }

            bool nextUseManualLoop = EditorGUILayout.ToggleLeft(
                "Manual Loop",
                useManualLoop,
                GUILayout.Width(110f));

            bool nextLoopOverride = loopOverride;
            using (new EditorGUI.DisabledScope(!nextUseManualLoop))
            {
                nextLoopOverride = EditorGUILayout.Toggle(loopOverride, GUILayout.Width(32f));
            }

            if (nextUseManualLoop != useManualLoop || nextLoopOverride != loopOverride)
            {
                useManualLoop = nextUseManualLoop;
                loopOverride = nextLoopOverride;
                ResetPreviewTrack();
                ApplyPreviewPose(true);
            }
        }

        Spine.Animation animation = FindAnimation(selectedAnimationName);
        float duration = animation != null ? animation.Duration : 0f;
        bool hasAnimation = animation != null;

        using (new EditorGUI.DisabledScope(!hasAnimation))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(isPlaying ? "Pause" : "Play", GUILayout.Width(80f)))
                {
                    TogglePlay();
                }

                if (GUILayout.Button("Stop", GUILayout.Width(80f)))
                {
                    StopPreview();
                }

                EditorGUILayout.LabelField("Speed", GUILayout.Width(45f));
                playbackSpeed = EditorGUILayout.Slider(playbackSpeed, 0.05f, 3f);
            }

            EditorGUI.BeginChangeCheck();
            float nextPlaybackTime = EditorGUILayout.Slider(
                "Time",
                playbackTime,
                0f,
                Mathf.Max(duration, 0.001f));

            if (EditorGUI.EndChangeCheck())
            {
                isPlaying = false;
                playbackTime = nextPlaybackTime;
                ApplyPreviewPose(true);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            previewScale = EditorGUILayout.Slider("Clone Scale", previewScale, 0.1f, 3f);
            if (GUILayout.Button("Apply", GUILayout.Width(70f)))
            {
                ApplyPreviewTransform();
                RepaintSceneView();
            }
        }

        previewOffset = EditorGUILayout.Vector3Field("Clone Offset", previewOffset);

        string status = string.IsNullOrEmpty(selectedAnimationName)
            ? "(no animation selected)"
            : selectedAnimationName;

        string durationText = hasAnimation ? string.Format("{0:0.###}s", duration) : "missing";
        EditorGUILayout.LabelField("Selected", status + "  |  Duration: " + durationText);
    }

    private void DrawStateReview()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("State", GUILayout.Width(130f));
            GUILayout.Label("Animation", GUILayout.MinWidth(220f));
            GUILayout.Label("Duration", GUILayout.Width(75f));
            GUILayout.Label("Loop", GUILayout.Width(50f));
            GUILayout.Label("Status", GUILayout.Width(220f));
        }

        for (int i = 0; i < ReviewStates.Length; i++)
        {
            AnimationSpine.State state = ReviewStates[i];
            string animationName = targetAnimationSpine.GetAnimationName(state);
            Spine.Animation animation = FindAnimation(animationName);
            bool required = IsImportantState(state);
            bool missingAssignment = required && string.IsNullOrEmpty(animationName);
            bool missingAnimation = !string.IsNullOrEmpty(animationName) && animation == null;
            bool loop = targetAnimationSpine.GetResolvedDefaultLoop(state);
            bool suspiciousLoop = HasSuspiciousLoop(state, loop);

            int duplicateCount;
            bool duplicate = !string.IsNullOrEmpty(animationName)
                && duplicateCountCache.TryGetValue(animationName, out duplicateCount)
                && duplicateCount > 1;

            GUIStyle style = okStyle;
            string stateStatus = "OK";

            if (missingAnimation)
            {
                style = errorStyle;
                stateStatus = "Missing Spine animation";
            }
            else if (missingAssignment)
            {
                style = warningStyle;
                stateStatus = "Missing assignment";
            }
            else if (suspiciousLoop)
            {
                style = warningStyle;
                stateStatus = "Suspicious loop";
            }
            else if (duplicate)
            {
                style = infoStyle;
                stateStatus = "Duplicate mapping";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(state.ToString(), EditorStyles.miniButton, GUILayout.Width(130f)))
                {
                    reviewMode = ReviewMode.State;
                    SelectState(state);
                }

                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(animationName) ? "(empty)" : animationName,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight),
                    GUILayout.MinWidth(220f));

                GUILayout.Label(
                    animation != null ? string.Format("{0:0.###}s", animation.Duration) : "-",
                    GUILayout.Width(75f));

                GUILayout.Label(loop ? "Yes" : "No", GUILayout.Width(50f));
                GUILayout.Label(stateStatus, style, GUILayout.Width(220f));
            }
        }
    }

    private void DrawSkillReview()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh Skills", GUILayout.Width(110f)))
            {
                skillCacheDirty = true;
            }
        }

        EnsureSkillCache();

        if (skillCache.Count == 0)
        {
            EditorGUILayout.HelpBox("No SkillData assets found.", MessageType.Info);
            return;
        }

        int pageCount = Mathf.Max(1, Mathf.CeilToInt(skillCache.Count / (float)SkillPageSize));
        skillPage = Mathf.Clamp(skillPage, 0, pageCount - 1);

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            using (new EditorGUI.DisabledScope(skillPage <= 0))
            {
                if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(28f)))
                {
                    skillPage--;
                }
            }

            GUILayout.Label(
                string.Format("Page {0}/{1}  ({2} skills)", skillPage + 1, pageCount, skillCache.Count),
                GUILayout.Width(180f));

            using (new EditorGUI.DisabledScope(skillPage >= pageCount - 1))
            {
                if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(28f)))
                {
                    skillPage++;
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label("Skill", GUILayout.Width(220f));
            GUILayout.Label("Element", GUILayout.Width(70f));
            GUILayout.Label("State", GUILayout.Width(120f));
            GUILayout.Label("Animation", GUILayout.MinWidth(200f));
            GUILayout.Label("Status", GUILayout.Width(220f));
        }

        int start = skillPage * SkillPageSize;
        int end = Mathf.Min(start + SkillPageSize, skillCache.Count);

        for (int i = start; i < end; i++)
        {
            SkillData skill = skillCache[i];
            if (skill == null)
            {
                continue;
            }

            AnimationSpine.State state = skill.AnimationName;
            string animationName = state != AnimationSpine.State.None
                ? targetAnimationSpine.GetAnimationName(state)
                : string.Empty;

            Spine.Animation animation = FindAnimation(animationName);
            bool missingAssignment = state != AnimationSpine.State.None
                && string.IsNullOrEmpty(animationName);
            bool missingAnimation = !string.IsNullOrEmpty(animationName)
                && animation == null;

            GUIStyle style = okStyle;
            string skillStatus = "OK";

            if (state == AnimationSpine.State.None)
            {
                style = infoStyle;
                skillStatus = "No animation";
            }
            else if (missingAnimation)
            {
                style = errorStyle;
                skillStatus = "Missing Spine animation";
            }
            else if (missingAssignment)
            {
                style = warningStyle;
                skillStatus = "State not assigned";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(skill.SkillName, EditorStyles.miniButton, GUILayout.Width(220f)))
                {
                    Selection.activeObject = skill;
                }

                GUILayout.Label(skill.ElementType.ToString(), GUILayout.Width(70f));

                if (GUILayout.Button(state.ToString(), EditorStyles.miniButton, GUILayout.Width(120f)))
                {
                    reviewMode = ReviewMode.State;
                    SelectState(state);
                }

                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(animationName) ? "(empty)" : animationName,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight),
                    GUILayout.MinWidth(200f));

                GUILayout.Label(skillStatus, style, GUILayout.Width(220f));
            }
        }
    }

    private void TryUseSelection()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected != null)
        {
            SetTarget(selected);
        }
    }

    private void SetTarget(GameObject nextTarget)
    {
        if (targetObject == nextTarget
            && targetAnimationSpine != null
            && targetSkeletonAnimation != null)
        {
            return;
        }

        DestroyPreviewClone();

        targetObject = nextTarget;
        targetAnimationSpine = null;
        targetSkeletonAnimation = null;

        if (targetObject != null)
        {
            targetAnimationSpine = targetObject.GetComponent<AnimationSpine>();
            if (targetAnimationSpine == null)
            {
                targetAnimationSpine = targetObject.GetComponentInChildren<AnimationSpine>(true);
            }

            targetSkeletonAnimation = targetObject.GetComponent<SkeletonAnimation>();
            if (targetSkeletonAnimation == null)
            {
                targetSkeletonAnimation = targetObject.GetComponentInChildren<SkeletonAnimation>(true);
            }
        }

        RefreshTargetCaches();

        selectedAnimationName = targetAnimationSpine != null
            && selectedState != AnimationSpine.State.None
            ? targetAnimationSpine.GetAnimationName(selectedState)
            : string.Empty;

        loopOverride = targetAnimationSpine != null
            && selectedState != AnimationSpine.State.None
            && targetAnimationSpine.GetResolvedDefaultLoop(selectedState);

        playbackTime = 0f;
        isPlaying = false;

        RebuildPreviewClone();
        Repaint();
    }

    private void SelectState(AnimationSpine.State state)
    {
        selectedState = state;
        selectedAnimationName = targetAnimationSpine != null
            && state != AnimationSpine.State.None
            ? targetAnimationSpine.GetAnimationName(state)
            : string.Empty;

        loopOverride = targetAnimationSpine != null
            && state != AnimationSpine.State.None
            && targetAnimationSpine.GetResolvedDefaultLoop(state);

        playbackTime = 0f;
        isPlaying = false;
        ResetPreviewTrack();
        ApplyPreviewPose(true);
    }

    private void SelectRawAnimation(string animationName)
    {
        selectedAnimationName = animationName;
        playbackTime = 0f;
        isPlaying = false;
        ResetPreviewTrack();
        ApplyPreviewPose(true);
    }

    private void TogglePlay()
    {
        if (FindAnimation(selectedAnimationName) == null)
        {
            return;
        }

        isPlaying = !isPlaying;
        lastEditorTime = EditorApplication.timeSinceStartup;
        nextPreviewTime = lastEditorTime;

        if (!EnsurePreviewTrack())
        {
            isPlaying = false;
            return;
        }

        ApplyPreviewPose(false);
    }

    private void StopPreview()
    {
        isPlaying = false;
        playbackTime = 0f;
        ApplyPreviewPose(true);
    }

    private void TickPreview()
    {
        if (!isPlaying || previewSkeletonAnimation == null)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now < nextPreviewTime)
        {
            return;
        }

        float delta = Mathf.Clamp((float)(now - lastEditorTime), 0f, 0.1f);
        lastEditorTime = now;
        nextPreviewTime = now + PreviewFrameInterval;

        Spine.Animation animation = FindAnimation(selectedAnimationName);
        if (animation == null || !EnsurePreviewTrack())
        {
            isPlaying = false;
            return;
        }

        float previousTime = playbackTime;
        playbackTime += delta * playbackSpeed;
        bool loop = ResolveSelectedLoop();
        bool wrapped = false;

        if (animation.Duration > 0f)
        {
            if (loop)
            {
                if (playbackTime >= animation.Duration)
                {
                    playbackTime %= animation.Duration;
                    wrapped = playbackTime < previousTime;
                }
            }
            else if (playbackTime >= animation.Duration)
            {
                playbackTime = animation.Duration;
                isPlaying = false;
            }
        }

        ApplyPreviewPose(wrapped);
        Repaint();
    }

    private void RefreshAll()
    {
        skillCacheDirty = true;
        RefreshTargetCaches();
        RebuildPreviewClone();
        Repaint();
    }

    private void RefreshTargetCaches()
    {
        animationCache.Clear();
        duplicateCountCache.Clear();
        rawAnimationNames = Array.Empty<string>();
        cachedSkeletonData = GetSkeletonData(targetSkeletonAnimation);

        if (cachedSkeletonData != null && cachedSkeletonData.Animations != null)
        {
            int count = cachedSkeletonData.Animations.Count;
            rawAnimationNames = new string[count];

            for (int i = 0; i < count; i++)
            {
                Spine.Animation animation = cachedSkeletonData.Animations.Items[i];
                if (animation == null)
                {
                    continue;
                }

                rawAnimationNames[i] = animation.Name;
                animationCache[animation.Name] = animation;
            }
        }

        RebuildDuplicateCache();
        mappingHash = ComputeMappingHash();
    }

    private void EnsureTargetCachesAreCurrent()
    {
        if (UnityEngine.Event.current.type != EventType.Layout)
        {
            return;
        }

        SkeletonData currentSkeletonData = GetSkeletonData(targetSkeletonAnimation);
        if (!ReferenceEquals(currentSkeletonData, cachedSkeletonData))
        {
            RefreshTargetCaches();
            ResetPreviewTrack();
            ApplyPreviewPose(true);
            return;
        }

        int currentMappingHash = ComputeMappingHash();
        if (currentMappingHash == mappingHash)
        {
            return;
        }

        mappingHash = currentMappingHash;
        RebuildDuplicateCache();

        if (reviewMode == ReviewMode.State)
        {
            string nextAnimationName = selectedState != AnimationSpine.State.None
                ? targetAnimationSpine.GetAnimationName(selectedState)
                : string.Empty;

            if (!string.Equals(nextAnimationName, selectedAnimationName, StringComparison.Ordinal))
            {
                selectedAnimationName = nextAnimationName;
                playbackTime = 0f;
                isPlaying = false;
                ResetPreviewTrack();
                ApplyPreviewPose(true);
            }
        }
    }

    private int ComputeMappingHash()
    {
        if (targetAnimationSpine == null)
        {
            return 0;
        }

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < ReviewStates.Length; i++)
            {
                AnimationSpine.State state = ReviewStates[i];
                string animationName = targetAnimationSpine.GetAnimationName(state);
                bool loop = targetAnimationSpine.GetResolvedDefaultLoop(state);

                hash = hash * 31 + (animationName != null ? animationName.GetHashCode() : 0);
                hash = hash * 31 + (loop ? 1 : 0);
            }

            return hash;
        }
    }

    private void RebuildDuplicateCache()
    {
        duplicateCountCache.Clear();

        if (targetAnimationSpine == null)
        {
            return;
        }

        for (int i = 0; i < ReviewStates.Length; i++)
        {
            string animationName = targetAnimationSpine.GetAnimationName(ReviewStates[i]);
            if (string.IsNullOrEmpty(animationName))
            {
                continue;
            }

            int count;
            duplicateCountCache.TryGetValue(animationName, out count);
            duplicateCountCache[animationName] = count + 1;
        }
    }

    private void EnsureSkillCache()
    {
        if (!skillCacheDirty)
        {
            return;
        }

        skillCacheDirty = false;
        skillCache.Clear();

        string[] guids = AssetDatabase.FindAssets("t:SkillData");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if (skill != null)
            {
                skillCache.Add(skill);
            }
        }

        skillCache.Sort(CompareSkills);
        skillPage = Mathf.Clamp(
            skillPage,
            0,
            Mathf.Max(0, Mathf.CeilToInt(skillCache.Count / (float)SkillPageSize) - 1));
    }

    private static int CompareSkills(SkillData left, SkillData right)
    {
        string leftName = left != null ? left.SkillName : string.Empty;
        string rightName = right != null ? right.SkillName : string.Empty;
        return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
    }

    private void RebuildPreviewClone()
    {
        DestroyPreviewClone();

        if (targetSkeletonAnimation == null)
        {
            return;
        }

        // Clone only the visual Spine object, not the entire Player/NPC hierarchy.
        previewClone = Instantiate(targetSkeletonAnimation.gameObject);
        previewClone.name = targetSkeletonAnimation.gameObject.name + " (Spine Preview)";
        SetHideFlagsRecursive(previewClone, HideFlags.HideAndDontSave);

        previewSkeletonAnimation = previewClone.GetComponent<SkeletonAnimation>();
        if (previewSkeletonAnimation == null)
        {
            previewSkeletonAnimation = previewClone.GetComponentInChildren<SkeletonAnimation>(true);
        }

        DisableUnsafePreviewBehaviours(previewClone, previewSkeletonAnimation);
        previewClone.SetActive(true);

        if (previewSkeletonAnimation != null)
        {
            previewSkeletonAnimation.gameObject.SetActive(true);
            previewSkeletonAnimation.Initialize(false);

        }

        ApplyPreviewTransform();
        ResetPreviewTrack();
        ApplyPreviewPose(true);
    }

    private void DestroyPreviewClone()
    {
        previewTrackEntry = null;
        previewTrackAnimationName = null;
        previewSkeletonAnimation = null;

        if (previewClone != null)
        {
            DestroyImmediate(previewClone);
            previewClone = null;
        }
    }

    private bool EnsurePreviewTrack()
    {
        if (previewSkeletonAnimation == null || string.IsNullOrEmpty(selectedAnimationName))
        {
            return false;
        }

        if (!previewSkeletonAnimation.valid)
        {
            previewSkeletonAnimation.Initialize(false);
        }

        Skeleton skeleton = previewSkeletonAnimation.Skeleton;
        Spine.AnimationState animationState = previewSkeletonAnimation.AnimationState;
        Spine.Animation animation = skeleton != null && skeleton.Data != null
            ? skeleton.Data.FindAnimation(selectedAnimationName)
            : null;

        if (skeleton == null || animationState == null || animation == null)
        {
            return false;
        }

        bool loop = ResolveSelectedLoop();
        bool trackMatches = previewTrackEntry != null
            && string.Equals(previewTrackAnimationName, selectedAnimationName, StringComparison.Ordinal)
            && previewTrackLoop == loop;

        if (trackMatches)
        {
            return true;
        }

        animationState.ClearTracks();
        skeleton.SetToSetupPose();

        previewTrackEntry = animationState.SetAnimation(0, animation, loop);
        previewTrackAnimationName = selectedAnimationName;
        previewTrackLoop = loop;
        return previewTrackEntry != null;
    }

    private void ResetPreviewTrack()
    {
        previewTrackEntry = null;
        previewTrackAnimationName = null;
        previewTrackLoop = false;
    }

    private void ApplyPreviewPose(bool resetSetupPose)
    {
        if (!EnsurePreviewTrack())
        {
            return;
        }

        Skeleton skeleton = previewSkeletonAnimation.Skeleton;
        Spine.Animation animation = FindAnimation(selectedAnimationName);
        if (skeleton == null || animation == null)
        {
            return;
        }

        if (resetSetupPose)
        {
            skeleton.SetToSetupPose();
        }

        previewTrackEntry.TrackTime = Mathf.Clamp(
            playbackTime,
            0f,
            Mathf.Max(animation.Duration, 0f));

        // Reuse the same TrackEntry instead of ClearTracks + SetAnimation every frame.
        previewSkeletonAnimation.Update(0f);
        previewSkeletonAnimation.LateUpdate();
        RepaintSceneView();
    }

    private void ApplyPreviewTransform()
    {
        if (previewClone == null || targetSkeletonAnimation == null)
        {
            return;
        }

        Transform source = targetSkeletonAnimation.transform;
        Transform cloneTransform = previewClone.transform;

        cloneTransform.position = source.position + previewOffset;
        cloneTransform.rotation = source.rotation;
        cloneTransform.localScale = source.lossyScale * previewScale;
    }

    private void FocusPreviewClone()
    {
        if (previewClone == null)
        {
            return;
        }

        Selection.activeGameObject = previewClone;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }

    private void RepaintSceneView()
    {
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.Repaint();
        }
    }

    private bool ResolveSelectedLoop()
    {
        if (useManualLoop)
        {
            return loopOverride;
        }

        if (reviewMode == ReviewMode.State
            && targetAnimationSpine != null
            && selectedState != AnimationSpine.State.None)
        {
            return targetAnimationSpine.GetResolvedDefaultLoop(selectedState);
        }

        return loopOverride;
    }

    private Spine.Animation FindAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
        {
            return null;
        }

        Spine.Animation animation;
        return animationCache.TryGetValue(animationName, out animation) ? animation : null;
    }

    private static SkeletonData GetSkeletonData(SkeletonAnimation skeletonAnimation)
    {
        if (skeletonAnimation == null)
        {
            return null;
        }

        if (skeletonAnimation.Skeleton != null && skeletonAnimation.Skeleton.Data != null)
        {
            return skeletonAnimation.Skeleton.Data;
        }

        return skeletonAnimation.SkeletonDataAsset != null
            ? skeletonAnimation.SkeletonDataAsset.GetSkeletonData(false)
            : null;
    }

    private static bool IsImportantState(AnimationSpine.State state)
    {
        switch (state)
        {
            case AnimationSpine.State.Idle:
            case AnimationSpine.State.Walk:
            case AnimationSpine.State.Jump:
            case AnimationSpine.State.Fall:
            case AnimationSpine.State.Range:
            case AnimationSpine.State.Spear:
            case AnimationSpine.State.Skill:
            case AnimationSpine.State.Attack:
            case AnimationSpine.State.SpecialAttack:
            case AnimationSpine.State.Dash:
            case AnimationSpine.State.Hurt:
            case AnimationSpine.State.Die:
                return true;
            default:
                return false;
        }
    }

    private static bool HasSuspiciousLoop(AnimationSpine.State state, bool loop)
    {
        switch (state)
        {
            case AnimationSpine.State.Idle:
            case AnimationSpine.State.Walk:
            case AnimationSpine.State.Run:
            case AnimationSpine.State.Fall:
            case AnimationSpine.State.SwimIdle:
            case AnimationSpine.State.SwimMove:
            case AnimationSpine.State.Block:
            case AnimationSpine.State.Stun:
                return !loop;

            case AnimationSpine.State.Attack:
            case AnimationSpine.State.Range:
            case AnimationSpine.State.Spear:
            case AnimationSpine.State.Skill:
            case AnimationSpine.State.SpecialAttack:
            case AnimationSpine.State.Dash:
            case AnimationSpine.State.Hurt:
            case AnimationSpine.State.Die:
            case AnimationSpine.State.ParryStart:
            case AnimationSpine.State.ParryFail:
            case AnimationSpine.State.ParrySuccess:
            case AnimationSpine.State.NormalAttack1:
            case AnimationSpine.State.NormalAttack2:
            case AnimationSpine.State.NormalAttack3:
                return loop;

            default:
                return false;
        }
    }

    private static void DisableUnsafePreviewBehaviours(
        GameObject root,
        SkeletonAnimation allowedSkeletonAnimation)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == allowedSkeletonAnimation)
            {
                continue;
            }

            behaviour.enabled = false;
        }
    }

    private static void SetHideFlagsRecursive(GameObject root, HideFlags flags)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            GameObject gameObject = transforms[i].gameObject;
            gameObject.hideFlags = flags;

            Component[] components = gameObject.GetComponents<Component>();
            for (int j = 0; j < components.Length; j++)
            {
                if (components[j] != null)
                {
                    components[j].hideFlags = flags;
                }
            }
        }
    }

    private void EnsureStyles()
    {
        if (okStyle != null)
        {
            return;
        }

        okStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.35f, 0.75f, 0.35f) }
        };

        warningStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(1f, 0.68f, 0.18f) }
        };

        errorStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(1f, 0.28f, 0.22f) }
        };

        infoStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.35f, 0.65f, 1f) }
        };
    }
}
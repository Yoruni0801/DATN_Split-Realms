using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Spine;
using Spine.Unity;

[RequireComponent(typeof(SkeletonAnimation))]
public class AnimationSpine : MonoBehaviour
{
    public enum State
    {
        None,
        Idle,
        Walk,
        Run,
        Jump,
        Fall,
        Attack,
        Hurt,
        Stun,
        Die,
        Range,
        Spear,
        SwimMove,
        Block,
        Dash,
        NormalAttack1,
        NormalAttack2,
        NormalAttack3,
        ParryStart,
        ParryFail,
        ParrySuccess,
        SwimIdle,
        SpecialAttack,
        Skill,
        Magic,
        RopeGrab,
    }

    [System.Serializable]
    public class AnimationNameEvent : UnityEvent<string> { }

    [System.Serializable]
    public class StateNameEvent : UnityEvent<State> { }

    [System.Serializable]
    public class StateLoopOverride
    {
        public State state;
        public bool useOverride = true;
        public bool loop;
    }

    [System.Serializable]
    public class AnimationLoopOverride
    {
        [SpineAnimation] public string animationName;
        public bool useOverride = true;
        public bool loop;
    }

    [System.Serializable]
    public class AnimationSpeedOverride
    {
        [SpineAnimation] public string animationName;
        [Min(0f)] public float speed = 1f;
    }

    public event System.Action<TrackEntry> AnimationStarted;

    [Header("Default")]
    [SerializeField] private State startState = State.Idle;
    [SerializeField, SpineAnimation] private string startAnimation;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loopStartAnimation = true;

    [Header("Auto From SkeletonAnimation")]
    [SerializeField] private bool autoGetAnimations = true;
    [SerializeField] private bool autoMapStates = true;
    [SerializeField] private bool onlyAutoMapEmptyStates = true;
    private string[] availableAnimationNames;

    [Header("Animation States")]
    [SerializeField, SpineAnimation] private string idleAnimation;
    [SerializeField, SpineAnimation] private string walkAnimation;
    [SerializeField, SpineAnimation] private string runAnimation;
    [SerializeField, SpineAnimation] private string jumpAnimation;
    [SerializeField, SpineAnimation] private string fallAnimation;
    [SerializeField, SpineAnimation] private string swimIdleAnimation;
    [SerializeField, SpineAnimation] private string swimMoveAnimation;
    [SerializeField, SpineAnimation] private string blockAnimation;
    [SerializeField, SpineAnimation] private string dashAnimation;
    [SerializeField, SpineAnimation] private string normalAttack1Animation;
    [SerializeField, SpineAnimation] private string normalAttack2Animation;
    [SerializeField, SpineAnimation] private string normalAttack3Animation;
    [SerializeField, SpineAnimation] private string attackAnimation;
    [SerializeField, SpineAnimation] private string specialAttackAnimation;
    [SerializeField, SpineAnimation] private string parryStartAnimation;
    [SerializeField, SpineAnimation] private string parryFailAnimation;
    [SerializeField, SpineAnimation] private string parrySuccessAnimation;
    [SerializeField, SpineAnimation] private string hurtAnimation;
    [SerializeField, SpineAnimation] private string stunAnimation;
    [SerializeField, SpineAnimation] private string dieAnimation;
    [SerializeField, SpineAnimation, FormerlySerializedAs("Range")] private string rangeAnimation;
    [SerializeField, SpineAnimation] private string spearAnimation;
    [SerializeField, SpineAnimation] private string skillAnimation;
    [SerializeField, SpineAnimation] private string magicAnimation;
    [SerializeField, SpineAnimation] private string ropeGrabAnimation;

    [Header("Settings")]
    [SerializeField] private int defaultTrackIndex = 0;
    [SerializeField] private float defaultMixDuration = 0.1f;
    [SerializeField] private bool resetToSetupPoseBeforePlay;

    [Header("Loop Overrides")]
    [SerializeField] private bool useLoopOverrides = true;
    [SerializeField] private bool stateLoopOverridesTakePriority = false;
    private StateLoopOverride[] stateLoopOverrides =
    {
        new StateLoopOverride { state = State.Idle, loop = true },
        new StateLoopOverride { state = State.Walk, loop = true },
        new StateLoopOverride { state = State.Run, loop = true },
        new StateLoopOverride { state = State.Jump, loop = false },
        new StateLoopOverride { state = State.Fall, loop = true },
        new StateLoopOverride { state = State.SwimIdle, loop = true },
        new StateLoopOverride { state = State.SwimMove, loop = true },
        new StateLoopOverride { state = State.Block, loop = true },
        new StateLoopOverride { state = State.Dash, loop = false },
        new StateLoopOverride { state = State.NormalAttack1, loop = false },
        new StateLoopOverride { state = State.NormalAttack2, loop = false },
        new StateLoopOverride { state = State.NormalAttack3, loop = false },
        new StateLoopOverride { state = State.Attack, loop = false },
        new StateLoopOverride { state = State.Range, loop = false },
        new StateLoopOverride { state = State.Spear, loop = false },
        new StateLoopOverride { state = State.SpecialAttack, loop = false },
        new StateLoopOverride { state = State.ParryStart, loop = false },
        new StateLoopOverride { state = State.ParryFail, loop = false },
        new StateLoopOverride { state = State.ParrySuccess, loop = false },
        new StateLoopOverride { state = State.Hurt, loop = false },
        new StateLoopOverride { state = State.Stun, loop = true },
        new StateLoopOverride { state = State.Die, loop = false },
        new StateLoopOverride { state = State.Skill, loop = false },
        new StateLoopOverride { state = State.Magic, loop = false },
        new StateLoopOverride { state = State.RopeGrab, loop = true }
    };
    [SerializeField] private AnimationLoopOverride[] animationLoopOverrides;

    [Header("Speed Overrides")]
    [SerializeField] private AnimationSpeedOverride[] animationSpeedOverrides;

    [Header("Events")]
    public AnimationNameEvent OnAnimationComplete;
    public StateNameEvent OnStateComplete;

    private SkeletonAnimation skeletonAnimation;
    private Spine.AnimationState animationState;
    private Spine.Skeleton skeleton;
    private TrackEntry currentEntry;
    private string currentAnimationName;
    private State currentState = State.None;

    public string[] AvailableAnimationNames => availableAnimationNames;
    public SkeletonAnimation SkeletonAnimation => skeletonAnimation;
    public Spine.AnimationState AnimationState => animationState;
    public Spine.Skeleton Skeleton => skeleton;
    public string CurrentAnimationName => currentAnimationName;
    public State CurrentState => currentState;

    private void Awake()
    {
        Setup();
    }

    private void Reset()
    {
        Setup();
        RefreshAnimationsFromSkeleton();
    }

    private void OnValidate()
    {
        skeletonAnimation = GetComponent<SkeletonAnimation>();
        RefreshAnimationsFromSkeleton();
    }

    private void Start()
    {
        if (skeletonAnimation == null)
        {
            return;
        }

        skeletonAnimation.UpdateComplete -= AfterUpdateComplete;
        skeletonAnimation.UpdateComplete += AfterUpdateComplete;

        if (!playOnStart)
        {
            return;
        }

        if (startState != State.None && HasStateAnimation(startState))
        {
            Play(startState, loopStartAnimation);
        }
        else if (!string.IsNullOrEmpty(startAnimation))
        {
            PlayAnimation(startAnimation, loopStartAnimation);
        }
    }

    private void OnDestroy()
    {
        if (skeletonAnimation != null)
        {
            skeletonAnimation.UpdateComplete -= AfterUpdateComplete;
        }

        UnregisterCurrentCompleteEvent();
    }

    private void Setup()
    {
        if (skeletonAnimation != null && skeleton != null && animationState != null)
        {
            return;
        }

        if (skeletonAnimation == null)
        {
            skeletonAnimation = GetComponent<SkeletonAnimation>();
        }

        if (skeletonAnimation == null)
        {
            Debug.LogError($"{nameof(AnimationSpine)} needs a SkeletonAnimation component.", this);
            enabled = false;
            return;
        }

        if (!skeletonAnimation.valid)
        {
            skeletonAnimation.Initialize(false);
        }

        skeleton = skeletonAnimation.Skeleton;
        animationState = skeletonAnimation.AnimationState;

        if (animationState != null)
        {
            animationState.Data.DefaultMix = defaultMixDuration;
        }

        RefreshAnimationsFromSkeleton();
    }

    private void AfterUpdateComplete(ISkeletonAnimation anim)
    {
        // Called after Spine has applied animation timelines to the skeleton.
    }

    public TrackEntry PlayAnimation(string animationName)
    {
        return PlayAnimation(animationName, true, defaultTrackIndex);
    }

    public TrackEntry PlayAnimation(string animationName, bool loop)
    {
        return PlayAnimation(animationName, loop, defaultTrackIndex);
    }

    public TrackEntry PlayAnimation(string animationName, bool loop, int trackIndex)
    {
        return PlayAnimation(animationName, loop, trackIndex, State.None);
    }

    public TrackEntry PlayAnimationForced(string animationName, bool loop, int trackIndex)
    {
        return PlayAnimation(animationName, loop, trackIndex, State.None, true);
    }

    public TrackEntry PlayAnimationForcedFromSetupPose(string animationName, bool loop, int trackIndex)
    {
        return PlayAnimation(animationName, loop, trackIndex, State.None, true, true);
    }

    public TrackEntry Play(string stateOrAnimationName)
    {
        if (string.IsNullOrEmpty(stateOrAnimationName))
        {
            Debug.LogWarning("State or animation name is empty.", this);
            return null;
        }

        return Play(stateOrAnimationName, true);
    }

    public TrackEntry Play(string stateOrAnimationName, bool loop)
    {
        if (string.IsNullOrEmpty(stateOrAnimationName))
        {
            Debug.LogWarning("State or animation name is empty.", this);
            return null;
        }

        if (System.Enum.TryParse(stateOrAnimationName, true, out State state)
            && state != State.None)
        {
            return Play(state, loop);
        }

        loop = ResolveLoop(State.None, stateOrAnimationName, loop);
        return PlayAnimation(stateOrAnimationName, loop);
    }

    public TrackEntry Play(State state)
    {
        return Play(state, GetDefaultLoop(state), defaultTrackIndex);
    }

    public TrackEntry Play(State state, bool loop)
    {
        return Play(state, loop, defaultTrackIndex);
    }

    public TrackEntry Play(State state, bool loop, int trackIndex)
    {
        string animationName = GetAnimationName(state);
        if (string.IsNullOrEmpty(animationName))
        {
            Debug.LogWarning($"No Spine animation is assigned for state '{state}' on {gameObject.name}.", this);
            return null;
        }

        loop = ResolveLoop(state, animationName, loop);
        return PlayAnimation(animationName, loop, trackIndex, state);
    }

    public TrackEntry PlayForced(State state, bool loop, int trackIndex)
    {
        string animationName = GetAnimationName(state);
        if (string.IsNullOrEmpty(animationName))
        {
            Debug.LogWarning($"No Spine animation is assigned for state '{state}' on {gameObject.name}.", this);
            return null;
        }

        loop = ResolveLoop(state, animationName, loop);
        return PlayAnimation(animationName, loop, trackIndex, state, true);
    }

    public TrackEntry PlayForcedFromSetupPose(State state, bool loop, int trackIndex)
    {
        string animationName = GetAnimationName(state);
        if (string.IsNullOrEmpty(animationName))
        {
            Debug.LogWarning($"No Spine animation is assigned for state '{state}' on {gameObject.name}.", this);
            return null;
        }

        loop = ResolveLoop(state, animationName, loop);
        return PlayAnimation(animationName, loop, trackIndex, state, true, true);
    }

    public TrackEntry PlayIdle()
    {
        return Play(State.Idle, true);
    }

    public TrackEntry PlayWalk()
    {
        return Play(State.Walk, true);
    }

    public TrackEntry PlayRun()
    {
        return Play(State.Run, true);
    }

    public TrackEntry PlayJump()
    {
        return Play(State.Jump, false);
    }

    public TrackEntry PlayFall()
    {
        return Play(State.Fall, true);
    }

    public TrackEntry PlayAttack()
    {
        return Play(State.Attack, false);
    }

    public TrackEntry PlayHurt()
    {
        return Play(State.Hurt, false);
    }

    public TrackEntry PlayStun()
    {
        return Play(State.Stun, true);
    }

    public TrackEntry PlayDie()
    {
        return Play(State.Die, false);
    }

    public TrackEntry PlaySkill()
    {
        return Play(State.Skill, false);
    }

    public TrackEntry PlayMagic()
    {
        return Play(State.Magic, false);
    }

    private TrackEntry PlayAnimation(string animationName, bool loop, int trackIndex, State state)
    {
        return PlayAnimation(animationName, loop, trackIndex, state, false, false);
    }

    private TrackEntry PlayAnimation(string animationName, bool loop, int trackIndex, State state, bool forceRestart)
    {
        return PlayAnimation(animationName, loop, trackIndex, state, forceRestart, false);
    }

    private TrackEntry PlayAnimation(string animationName, bool loop, int trackIndex, State state, bool forceRestart, bool resetBonesToSetupPose)
    {
        Setup();
        if (!CanPlay(animationName))
        {
            return null;
        }

        loop = ResolveLoop(state, animationName, loop);

        if (!forceRestart
            && currentAnimationName == animationName
            && currentEntry != null
            && currentEntry.Loop == loop
            && currentEntry.TrackIndex == trackIndex)
        {
            currentState = state;
            return currentEntry;
        }

        if (resetToSetupPoseBeforePlay)
        {
            skeleton.SetToSetupPose();
        }
        else if (resetBonesToSetupPose)
        {
            skeleton.SetBonesToSetupPose();
        }

        UnregisterCurrentCompleteEvent();
        currentEntry = animationState.SetAnimation(trackIndex, animationName, loop);
        ApplyAnimationSpeed(currentEntry, animationName);
        if (resetBonesToSetupPose)
        {
            currentEntry.MixDuration = 0f;
            animationState.Apply(skeleton);
        }

        currentAnimationName = animationName;
        currentState = state;
        RegisterCurrentCompleteEvent();
        AnimationStarted?.Invoke(currentEntry);

        return currentEntry;
    }

    [ContextMenu("Refresh Spine Animations")]
    public void RefreshAnimationsFromSkeleton()
    {
        if (!autoGetAnimations)
        {
            return;
        }

        Spine.SkeletonData skeletonData = GetSkeletonData();
        if (skeletonData == null)
        {
            availableAnimationNames = new string[0];
            return;
        }

        availableAnimationNames = new string[skeletonData.Animations.Count];
        for (int i = 0; i < skeletonData.Animations.Count; i++)
        {
            availableAnimationNames[i] = skeletonData.Animations.Items[i].Name;
        }

        if (autoMapStates)
        {
            AutoMapAnimations();
        }
    }

    public TrackEntry PlayOnce(string animationName)
    {
        return PlayAnimation(animationName, false, defaultTrackIndex);
    }

    public TrackEntry QueueAnimation(string animationName, bool loop = false, float delay = 0f)
    {
        return QueueAnimation(animationName, loop, delay, defaultTrackIndex);
    }

    public TrackEntry Queue(State state, float delay = 0f)
    {
        return Queue(state, GetDefaultLoop(state), delay, defaultTrackIndex);
    }

    public TrackEntry Queue(State state, bool loop, float delay = 0f)
    {
        return Queue(state, loop, delay, defaultTrackIndex);
    }

    public TrackEntry Queue(State state, bool loop, float delay, int trackIndex)
    {
        string animationName = GetAnimationName(state);
        if (string.IsNullOrEmpty(animationName))
        {
            Debug.LogWarning($"No Spine animation is assigned for state '{state}' on {gameObject.name}.", this);
            return null;
        }

        loop = ResolveLoop(state, animationName, loop);
        return QueueAnimation(animationName, loop, delay, trackIndex, state);
    }

    public TrackEntry QueueAnimation(string animationName, bool loop, float delay, int trackIndex)
    {
        loop = ResolveLoop(State.None, animationName, loop);
        return QueueAnimation(animationName, loop, delay, trackIndex, State.None);
    }

    private TrackEntry QueueAnimation(string animationName, bool loop, float delay, int trackIndex, State state)
    {
        Setup();
        if (!CanPlay(animationName))
        {
            return null;
        }

        loop = ResolveLoop(state, animationName, loop);

        TrackEntry entry = animationState.AddAnimation(trackIndex, animationName, loop, delay);
        ApplyAnimationSpeed(entry, animationName);
        entry.Start += startedEntry => HandleQueuedAnimationStart(startedEntry, state);
        entry.Complete += HandleAnimationComplete;
        return entry;
    }

    public void StopAnimation()
    {
        StopAnimation(defaultTrackIndex);
    }

    public void StopAnimation(int trackIndex)
    {
        Setup();
        bool stoppingCurrentEntry = currentEntry != null && currentEntry.TrackIndex == trackIndex;
        if (stoppingCurrentEntry)
        {
            UnregisterCurrentCompleteEvent();
        }

        animationState.ClearTrack(trackIndex);

        if (stoppingCurrentEntry)
        {
            currentEntry = null;
            currentAnimationName = string.Empty;
            currentState = State.None;
        }
    }

    public void StopAllAnimations()
    {
        Setup();
        UnregisterCurrentCompleteEvent();
        animationState.ClearTracks();
        currentEntry = null;
        currentAnimationName = string.Empty;
        currentState = State.None;
    }

    public TrackEntry CancelAnimation()
    {
        return CancelAnimation(defaultTrackIndex);
    }

    public TrackEntry CancelAnimation(int trackIndex)
    {
        StopAnimation(trackIndex);
        return PlayForced(State.Idle, true, trackIndex);
    }

    public void ResetPoseForAnimationChange()
    {
        Setup();
        if (skeleton == null)
        {
            return;
        }

        skeleton.SetToSetupPose();
    }

    public void SetSkin(string skinName)
    {
        Setup();
        if (string.IsNullOrEmpty(skinName))
        {
            return;
        }

        skeleton.SetSkin(skinName);
        skeleton.SetSlotsToSetupPose();
        animationState.Apply(skeleton);
    }

    public void SetFlipX(bool flip)
    {
        Setup();
        skeleton.ScaleX = flip ? -Mathf.Abs(skeleton.ScaleX) : Mathf.Abs(skeleton.ScaleX);
    }

    public void SetFlipY(bool flip)
    {
        Setup();
        skeleton.ScaleY = flip ? -Mathf.Abs(skeleton.ScaleY) : Mathf.Abs(skeleton.ScaleY);
    }

    public void SetTimeScale(float timeScale)
    {
        Setup();
        skeletonAnimation.timeScale = timeScale;
    }

    public void SetAnimationSpeed(string animationName, float speed)
    {
        Setup();
        if (string.IsNullOrEmpty(animationName))
        {
            return;
        }

        speed = Mathf.Max(0f, speed);
        if (currentEntry != null && currentEntry.Animation != null && currentEntry.Animation.Name == animationName)
        {
            currentEntry.TimeScale = speed;
        }
    }

    public void SetDefaultMix(float mixDuration)
    {
        defaultMixDuration = Mathf.Max(0f, mixDuration);
        Setup();
        animationState.Data.DefaultMix = defaultMixDuration;
    }

    public bool HasAnimation(string animationName)
    {
        Setup();
        return !string.IsNullOrEmpty(animationName)
            && skeleton != null
            && skeleton.Data != null
            && skeleton.Data.FindAnimation(animationName) != null;
    }

    public bool HasStateAnimation(State state)
    {
        return HasAnimation(GetAnimationName(state));
    }

    public bool GetResolvedDefaultLoop(State state)
    {
        return GetDefaultLoop(state);
    }

    public bool GetResolvedLoop(State state, string animationName, bool requestedLoop)
    {
        return ResolveLoop(state, animationName, requestedLoop);
    }

    public bool IsPlaying(State state)
    {
        return currentState == state;
    }

    public bool IsPlaying(string stateOrAnimationName)
    {
        if (string.IsNullOrEmpty(stateOrAnimationName))
        {
            return false;
        }

        if (System.Enum.TryParse(stateOrAnimationName, true, out State state)
            && state != State.None)
        {
            return IsPlaying(state);
        }

        return currentAnimationName == stateOrAnimationName;
    }

    public string GetAnimationName(State state)
    {
        switch (state)
        {
            case State.Idle:
                return idleAnimation;
            case State.Walk:
                return walkAnimation;
            case State.Run:
                return runAnimation;
            case State.Jump:
                return jumpAnimation;
            case State.Fall:
                return fallAnimation;
            case State.SwimIdle:
                return swimIdleAnimation;
            case State.SwimMove:
                return swimMoveAnimation;
            case State.Block:
                return blockAnimation;
            case State.Dash:
                return dashAnimation;
            case State.NormalAttack1:
                return normalAttack1Animation;
            case State.NormalAttack2:
                return normalAttack2Animation;
            case State.NormalAttack3:
                return normalAttack3Animation;
            case State.Attack:
                return attackAnimation;
            case State.Range:
                return rangeAnimation;
            case State.Spear:
                return spearAnimation;
            case State.SpecialAttack:
                return specialAttackAnimation;
            case State.ParryStart:
                return parryStartAnimation;
            case State.ParryFail:
                return parryFailAnimation;
            case State.ParrySuccess:
                return parrySuccessAnimation;
            case State.Hurt:
                return hurtAnimation;
            case State.Stun:
                return stunAnimation;
            case State.Die:
                return dieAnimation;
            case State.Skill:
                return GetFirstAssignedAnimation(skillAnimation, rangeAnimation, spearAnimation);
            case State.Magic:
                return GetFirstAssignedAnimation(magicAnimation, skillAnimation, rangeAnimation);
            case State.RopeGrab:
                return ropeGrabAnimation;
            default:
                return string.Empty;
        }
    }

    public void AutoMapAnimations()
    {
        AssignAnimationIfAllowed(ref idleAnimation, State.Idle);
        AssignAnimationIfAllowed(ref walkAnimation, State.Walk);
        AssignAnimationIfAllowed(ref runAnimation, State.Run);
        AssignAnimationIfAllowed(ref jumpAnimation, State.Jump);
        AssignAnimationIfAllowed(ref fallAnimation, State.Fall);
        AssignAnimationIfAllowed(ref swimIdleAnimation, State.SwimIdle);
        AssignAnimationIfAllowed(ref swimMoveAnimation, State.SwimMove);
        AssignAnimationIfAllowed(ref blockAnimation, State.Block);
        AssignAnimationIfAllowed(ref dashAnimation, State.Dash);
        AssignAnimationIfAllowed(ref normalAttack1Animation, State.NormalAttack1);
        AssignAnimationIfAllowed(ref normalAttack2Animation, State.NormalAttack2);
        AssignAnimationIfAllowed(ref normalAttack3Animation, State.NormalAttack3);
        AssignAnimationIfAllowed(ref attackAnimation, State.Attack);
        AssignAnimationIfAllowed(ref rangeAnimation, State.Range);
        AssignAnimationIfAllowed(ref spearAnimation, State.Spear);
        AssignAnimationIfAllowed(ref specialAttackAnimation, State.SpecialAttack);
        AssignAnimationIfAllowed(ref parryStartAnimation, State.ParryStart);
        AssignAnimationIfAllowed(ref parryFailAnimation, State.ParryFail);
        AssignAnimationIfAllowed(ref parrySuccessAnimation, State.ParrySuccess);
        AssignAnimationIfAllowed(ref hurtAnimation, State.Hurt);
        AssignAnimationIfAllowed(ref stunAnimation, State.Stun);
        AssignAnimationIfAllowed(ref dieAnimation, State.Die);
        AssignAnimationIfAllowed(ref skillAnimation, State.Skill);
        AssignAnimationIfAllowed(ref magicAnimation, State.Magic);
        AssignAnimationIfAllowed(ref ropeGrabAnimation, State.RopeGrab);

        if (string.IsNullOrEmpty(startAnimation) && startState == State.None && !string.IsNullOrEmpty(idleAnimation))
        {
            startAnimation = idleAnimation;
        }
    }

    private bool GetDefaultLoop(State state)
    {
        string animationName = GetAnimationName(state);
        return ResolveLoop(state, animationName, GetBuiltInDefaultLoop(state));
    }

    private bool GetBuiltInDefaultLoop(State state)
    {
        switch (state)
        {
            case State.Idle:
            case State.Walk:
            case State.Run:
            case State.Fall:
            case State.SwimIdle:
            case State.SwimMove:
            case State.Block:
            case State.Stun:
            case State.RopeGrab:
                return true;
            default:
                return false;
        }
    }

    private bool ResolveLoop(State state, string animationName, bool requestedLoop)
    {
        if (!useLoopOverrides)
        {
            return requestedLoop;
        }

        bool stateHasOverride = TryGetStateLoopOverride(state, out bool stateLoop);
        bool animationHasOverride = TryGetAnimationLoopOverride(animationName, out bool animationLoop);

        if (stateLoopOverridesTakePriority)
        {
            if (stateHasOverride) return stateLoop;
            if (animationHasOverride) return animationLoop;
        }
        else
        {
            if (animationHasOverride) return animationLoop;
            if (stateHasOverride) return stateLoop;
        }

        return requestedLoop;
    }

    private bool TryGetStateLoopOverride(State state, out bool loop)
    {
        loop = false;
        if (state == State.None || stateLoopOverrides == null)
        {
            return false;
        }

        for (int i = 0; i < stateLoopOverrides.Length; i++)
        {
            StateLoopOverride loopOverride = stateLoopOverrides[i];
            if (loopOverride == null
                || !loopOverride.useOverride
                || loopOverride.state != state)
            {
                continue;
            }

            loop = loopOverride.loop;
            return true;
        }

        return false;
    }

    private bool TryGetAnimationLoopOverride(string animationName, out bool loop)
    {
        loop = false;
        if (string.IsNullOrEmpty(animationName) || animationLoopOverrides == null)
        {
            return false;
        }

        for (int i = 0; i < animationLoopOverrides.Length; i++)
        {
            AnimationLoopOverride loopOverride = animationLoopOverrides[i];
            if (loopOverride == null
                || !loopOverride.useOverride
                || !string.Equals(loopOverride.animationName, animationName, System.StringComparison.Ordinal))
            {
                continue;
            }

            loop = loopOverride.loop;
            return true;
        }

        return false;
    }

    private bool CanPlay(string animationName)
    {
        if (animationState == null || skeleton == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(animationName))
        {
            Debug.LogWarning("Animation name is empty.", this);
            return false;
        }

        if (!HasAnimation(animationName))
        {
            Debug.LogWarning($"Spine animation '{animationName}' was not found on {gameObject.name}.", this);
            return false;
        }

        return true;
    }

    private Spine.SkeletonData GetSkeletonData()
    {
        if (skeleton != null && skeleton.Data != null)
        {
            return skeleton.Data;
        }

        if (skeletonAnimation == null)
        {
            skeletonAnimation = GetComponent<SkeletonAnimation>();
        }

        if (skeletonAnimation == null || skeletonAnimation.SkeletonDataAsset == null)
        {
            return null;
        }

        return skeletonAnimation.SkeletonDataAsset.GetSkeletonData(false);
    }

    private void AssignAnimationIfAllowed(ref string animationName, State state)
    {
        if (onlyAutoMapEmptyStates && !string.IsNullOrEmpty(animationName))
        {
            return;
        }

        string match = FindBestAnimationName(state);
        if (!string.IsNullOrEmpty(match))
        {
            animationName = match;
        }
    }

    private string FindBestAnimationName(State state)
    {
        if (availableAnimationNames == null || availableAnimationNames.Length == 0)
        {
            return string.Empty;
        }

        string[] keywords = GetKeywords(state);
        for (int i = 0; i < keywords.Length; i++)
        {
            string exactMatch = FindAnimationByKeyword(keywords[i], true);
            if (!string.IsNullOrEmpty(exactMatch))
            {
                return exactMatch;
            }
        }

        for (int i = 0; i < keywords.Length; i++)
        {
            string containsMatch = FindAnimationByKeyword(keywords[i], false);
            if (!string.IsNullOrEmpty(containsMatch))
            {
                return containsMatch;
            }
        }

        return string.Empty;
    }

    private string FindAnimationByKeyword(string keyword, bool exact)
    {
        for (int i = 0; i < availableAnimationNames.Length; i++)
        {
            string animationName = availableAnimationNames[i];
            if (string.IsNullOrEmpty(animationName))
            {
                continue;
            }

            if (exact && string.Equals(animationName, keyword, System.StringComparison.OrdinalIgnoreCase))
            {
                return animationName;
            }

            if (!exact && animationName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return animationName;
            }
        }

        return string.Empty;
    }

    private string[] GetKeywords(State state)
    {
        switch (state)
        {
            case State.Idle:
                return new[] { "idle", "stand", "wait", "dung", "heoiidle" };
            case State.Walk:
                return new[] { "walk", "di" };
            case State.Run:
                return new[] { "run", "chay" };
            case State.Jump:
                return new[] { "jump", "nhay" };
            case State.Fall:
                return new[] { "fall", "drop", "roi" };
            case State.SwimIdle:
                return new[] { "swimidle", "swim_idle", "boiloi" };
            case State.SwimMove:
                return new[] { "swimmove", "swim_move", "swim", "boi" };
            case State.Block:
                return new[] { "block", "guard", "do" };
            case State.Dash:
                return new[] { "dash", "luot" };
            case State.NormalAttack1:
                return new[] { "normalattack1", "attack1", "danhthuong", "danh1" };
            case State.NormalAttack2:
                return new[] { "normalattack2", "attack2", "danhthuong", "danh2" };
            case State.NormalAttack3:
                return new[] { "normalattack3", "attack3", "danh3" };
            case State.Attack:
                return new[] { "attack", "atk", "hit", "shoot", "danh" };
            case State.Range:
                return new[] { "range", "ranged", "shoot", "bow", "projectile", "ban" };
            case State.Spear:
                return new[] { "spear", "lance", "stab", "thrust", "giao", "dam" };
            case State.SpecialAttack:
                return new[] { "specialattack", "special", "ultimate" };
            case State.ParryStart:
                return new[] { "parrystart", "parry_start", "parry" };
            case State.ParryFail:
                return new[] { "parryfail", "parry_fail", "parry" };
            case State.ParrySuccess:
                return new[] { "parrysuccess", "parry_success", "parry" };
            case State.Hurt:
                return new[] { "hurt", "damage", "hit", "bi_danh" };
            case State.Stun:
                return new[] { "stun", "choang" };
            case State.Die:
                return new[] { "die", "death", "dead", "chet" };
            case State.Skill:
                return new[] { "skill", "special", "cast" };
            case State.Magic:
                return new[] { "magic", "spell", "cast", "skill" };
            case State.RopeGrab:
                return new[] { "ropegrab", "rope_grab", "rope", "hang", "grab", "climb", "nam", "du" };
            default:
                return new string[0];
        }
    }

    private string GetFirstAssignedAnimation(params string[] animationNames)
    {
        if (animationNames == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < animationNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(animationNames[i]))
            {
                return animationNames[i];
            }
        }

        return string.Empty;
    }

    private void RegisterCurrentCompleteEvent()
    {
        if (currentEntry != null)
        {
            currentEntry.Complete += HandleAnimationComplete;
        }
    }

    private void UnregisterCurrentCompleteEvent()
    {
        if (currentEntry != null)
        {
            currentEntry.Complete -= HandleAnimationComplete;
        }
    }

    private void HandleAnimationComplete(TrackEntry trackEntry)
    {
        if (trackEntry == null || trackEntry.Animation == null)
        {
            return;
        }

        string animationName = trackEntry.Animation.Name;
        OnAnimationComplete?.Invoke(animationName);

        if (trackEntry == currentEntry)
        {
            OnStateComplete?.Invoke(currentState);
        }
    }

    private void HandleQueuedAnimationStart(TrackEntry trackEntry, State state)
    {
        if (trackEntry == null || trackEntry.Animation == null)
        {
            return;
        }

        UnregisterCurrentCompleteEvent();
        currentEntry = trackEntry;
        currentAnimationName = trackEntry.Animation.Name;
        currentState = state;
        AnimationStarted?.Invoke(trackEntry);
    }

    private void ApplyAnimationSpeed(TrackEntry entry, string animationName)
    {
        if (entry == null || animationSpeedOverrides == null)
        {
            return;
        }

        for (int i = 0; i < animationSpeedOverrides.Length; i++)
        {
            AnimationSpeedOverride speedOverride = animationSpeedOverrides[i];
            if (speedOverride != null && speedOverride.animationName == animationName)
            {
                entry.TimeScale = Mathf.Max(0f, speedOverride.speed);
                return;
            }
        }
    }
}

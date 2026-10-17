using UnityEngine;
using UnityEngine.Playables;

public sealed class FairyElementUnlockPlayableAsset : PlayableAsset
{
    [SerializeField] private ExposedReference<FairyElementUnlockController> target;
    [SerializeField] private SkillElementType elementToUnlock = SkillElementType.Water;
    [SerializeField] private bool useCurrentPlayerWhenTargetMissing = true;
    [SerializeField] private bool addUnlockControllerIfMissing = true;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<FairyElementUnlockPlayableBehaviour> playable =
            ScriptPlayable<FairyElementUnlockPlayableBehaviour>.Create(graph);

        FairyElementUnlockPlayableBehaviour behaviour = playable.GetBehaviour();
        behaviour.Target = target.Resolve(graph.GetResolver());
        behaviour.ElementToUnlock = elementToUnlock;
        behaviour.UseCurrentPlayerWhenTargetMissing = useCurrentPlayerWhenTargetMissing;
        behaviour.AddUnlockControllerIfMissing = addUnlockControllerIfMissing;
        return playable;
    }
}

public sealed class FairyElementUnlockPlayableBehaviour : PlayableBehaviour
{
    public FairyElementUnlockController Target;
    public SkillElementType ElementToUnlock;
    public bool UseCurrentPlayerWhenTargetMissing;
    public bool AddUnlockControllerIfMissing;

    private bool unlocked;

    public override void OnGraphStart(Playable playable)
    {
        unlocked = false;
    }

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        UnlockOnce(info.output.GetUserData() as FairyElementUnlockController);
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        UnlockOnce(playerData as FairyElementUnlockController);
    }

    private void UnlockOnce(FairyElementUnlockController trackBinding)
    {
        if (unlocked || !Application.isPlaying)
        {
            return;
        }

        FairyElementUnlockController controller = ResolveTarget(trackBinding);
        if (controller == null)
        {
            return;
        }

        controller.UnlockElement(ElementToUnlock);
        unlocked = true;
    }

    private FairyElementUnlockController ResolveTarget(FairyElementUnlockController trackBinding)
    {
        if (Target != null)
        {
            return Target;
        }

        if (trackBinding != null)
        {
            Target = trackBinding;
            return Target;
        }

        if (!UseCurrentPlayerWhenTargetMissing || PlayerRegistry.Current == null)
        {
            return null;
        }

        FairyPlayerCompanionController fairy = ResolveCurrentPlayerFairy();
        if (fairy != null)
        {
            Target = fairy.GetComponent<FairyElementUnlockController>();
            if (Target == null && AddUnlockControllerIfMissing)
            {
                Target = fairy.gameObject.AddComponent<FairyElementUnlockController>();
            }

            return Target;
        }

        Player player = PlayerRegistry.Current;
        if (!player.TryGetComponent(out Target) && AddUnlockControllerIfMissing)
        {
            Target = player.gameObject.AddComponent<FairyElementUnlockController>();
        }

        return Target;
    }

    private static FairyPlayerCompanionController ResolveCurrentPlayerFairy()
    {
        Player player = PlayerRegistry.Current;
        if (player == null)
        {
            return null;
        }

#if UNITY_2023_1_OR_NEWER
        FairyPlayerCompanionController[] companions =
            Object.FindObjectsByType<FairyPlayerCompanionController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
        FairyPlayerCompanionController[] companions =
            Object.FindObjectsOfType<FairyPlayerCompanionController>();
#endif

        for (int i = 0; i < companions.Length; i++)
        {
            FairyPlayerCompanionController companion = companions[i];
            if (companion != null && companion.ActivePlayer == player)
            {
                return companion;
            }
        }

#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<FairyPlayerCompanionController>();
#else
        return Object.FindObjectOfType<FairyPlayerCompanionController>();
#endif
    }
}

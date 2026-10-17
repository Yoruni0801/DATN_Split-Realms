using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

public static class BlackHoleCrossSceneData
{
    public static string TargetExitID = string.Empty;
    public static bool IsPending = false;
    public static MapHangArrivalConfig ArrivalConfig;
    public static float BlackHoldDuration;
    public static float FadeToClearDuration;
    public static BlackHolePortalPlayerContext OriginalPlayerContext;
}

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(BlackHolePortalEffect2D))]
public sealed class GoalBlackHolePortal2D : MonoBehaviour
{
    [Header("Next Map")]
    [SerializeField] private GoalBlackHoleExit2D destinationExit;

    [Header("Cross-Scene Loading")]
    [SerializeField] private bool loadNewScene = false;
    [SerializeField] private string sceneToLoad;
    [SerializeField] private string targetExitID;


    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";

    [Header("Map Transition Fade")]
    [SerializeField] private MapHangTransitionFade2D transitionFade;
    [SerializeField] private float fadeToBlackDuration = 0.35f;
    [SerializeField] private float blackHoldDuration = 0.15f;
    [SerializeField] private float fadeToClearDuration = 0.4f;

    [Header("Arrival Sequence")]
    [SerializeField] private CinemachineCamera arrivalCamera;
    [SerializeField] private float arrivalPreGateDelay = 0.2f;
    [SerializeField] private float arrivalGateAppearDuration = 0.7f;
    [SerializeField] private float arrivalGateStayDuration = 0.4f;
    [SerializeField] private float arrivalPlayerFadeDelay = 0f;
    [SerializeField] private float arrivalPlayerFadeDuration = 0.6f;
    [SerializeField] private float arrivalPostPlayerDelay = 0.4f;
    [SerializeField] private float arrivalGateDisappearDuration = 0.6f;

    private BlackHolePortalEffect2D portalEffect;
    private BlackHolePortalPhase phase;

    public BlackHolePortalPhase Phase => phase;

    private void Awake()
    {
        portalEffect = GetComponent<BlackHolePortalEffect2D>();
        phase = BlackHolePortalPhase.Idle;
        if (portalEffect != null)
        {
            portalEffect.ShowPortal();
            portalEffect.SetTriggerEnabled(true);
        }

        if (transitionFade == null)
        {
            transitionFade = MapHangTransitionFade2D.GetOrCreate();
        }
    }

    private void Start()
    {
        ResetPortalState();
    }

    private void OnEnable()
    {
        ResetPortalState();
        PlayerRespawnController.RespawnRequested += HandleRespawnRequested;
    }

    private void OnDisable()
    {
        PlayerRespawnController.RespawnRequested -= HandleRespawnRequested;
    }

    private void HandleRespawnRequested(PlayerRespawnController controller)
    {
        ResetPortalState();
    }

    private void ResetPortalState()
    {
        phase = BlackHolePortalPhase.Idle;
        if (portalEffect != null)
        {
            portalEffect.ShowPortal();
            portalEffect.SetTriggerEnabled(true);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartTransition(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartTransition(other);
    }

    private void TryStartTransition(Collider2D other)
    {
        if (phase != BlackHolePortalPhase.Idle)
        {
            return;
        }

        if (!loadNewScene && destinationExit == null)
        {
            return;
        }

        if (!portalEffect.TryCreatePlayerContext(
                other,
                playerTag,
                out BlackHolePortalPlayerContext playerContext))
        {
            return;
        }

        PlayerHealth health = playerContext.Player != null
            ? playerContext.Player.GetComponent<PlayerHealth>()
            : playerContext.Root != null
                ? playerContext.Root.GetComponent<PlayerHealth>()
                : null;

        if (health != null && health.IsDead)
        {
            return;
        }

        MapHangStageRoot2D currentStage = GetComponentInParent<MapHangStageRoot2D>(true);
        MapHangStageRoot2D nextStage = destinationExit != null ? destinationExit.StageRoot : null;
        MapHangMinigameManager stageManager = ResolveStageManager();

        phase = BlackHolePortalPhase.PullingIn;
        StartCoroutine(TransitionRoutine(playerContext, currentStage, nextStage, stageManager));
    }

    private IEnumerator TransitionRoutine(
        BlackHolePortalPlayerContext playerContext,
        MapHangStageRoot2D currentStage,
        MapHangStageRoot2D nextStage,
        MapHangMinigameManager stageManager)
    {
        PlayerHealth health = playerContext.Player != null
            ? playerContext.Player.GetComponent<PlayerHealth>()
            : playerContext.Root != null
                ? playerContext.Root.GetComponent<PlayerHealth>()
                : null;

        if (health != null && health.IsDead)
        {
            phase = BlackHolePortalPhase.Idle;
            yield break;
        }

        portalEffect.LockPlayer(playerContext);
        portalEffect.PlaySuctionSound();

        yield return portalEffect.PullPlayerIn(playerContext);

        if (health != null && health.IsDead)
        {
            portalEffect.UnlockPlayer(playerContext);
            phase = BlackHolePortalPhase.Idle;
            yield break;
        }

        if (transitionFade != null)
        {
            yield return transitionFade.FadeToBlack(fadeToBlackDuration);
        }

        if (loadNewScene && !string.IsNullOrEmpty(sceneToLoad))
        {
            BlackHoleCrossSceneData.TargetExitID = targetExitID;
            BlackHoleCrossSceneData.IsPending = true;
            BlackHoleCrossSceneData.BlackHoldDuration = blackHoldDuration;
            BlackHoleCrossSceneData.FadeToClearDuration = fadeToClearDuration;
            BlackHoleCrossSceneData.OriginalPlayerContext = playerContext;
            BlackHoleCrossSceneData.ArrivalConfig = new MapHangArrivalConfig
            {
                camera = arrivalCamera,
                cameraDuration = 0f,
                preGateDelay = arrivalPreGateDelay,
                gateAppearDuration = arrivalGateAppearDuration,
                gateStayDuration = arrivalGateStayDuration,
                playerFadeDelay = arrivalPlayerFadeDelay,
                playerFadeDuration = arrivalPlayerFadeDuration,
                postPlayerDelay = arrivalPostPlayerDelay,
                gateDisappearDuration = arrivalGateDisappearDuration
            };

            if (playerContext.Player != null)
            {
                Destroy(playerContext.Player.gameObject);
            }

            Tien.AdvancedLoading.Core.SceneLoader.Load(sceneToLoad);
            phase = BlackHolePortalPhase.Idle;
            yield break;
        }

        if (nextStage != null)
        {
            nextStage.SetStageActive(true);
        }

        if (stageManager != null)
        {
            stageManager.SetStage(stageManager.CurrentStage + 1);
        }

        phase = BlackHolePortalPhase.Consumed;

        if (destinationExit != null)
        {
            if (transitionFade != null)
            {
                MapHangArrivalConfig config = new MapHangArrivalConfig
                {
                    camera = arrivalCamera,
                    cameraDuration = 0f,
                    preGateDelay = arrivalPreGateDelay,
                    gateAppearDuration = arrivalGateAppearDuration,
                    gateStayDuration = arrivalGateStayDuration,
                    playerFadeDelay = arrivalPlayerFadeDelay,
                    playerFadeDuration = arrivalPlayerFadeDuration,
                    postPlayerDelay = arrivalPostPlayerDelay,
                    gateDisappearDuration = arrivalGateDisappearDuration
                };

                transitionFade.BeginMapArrival(
                    destinationExit,
                    playerContext,
                    currentStage,
                    blackHoldDuration,
                    fadeToClearDuration,
                    config);
            }
            else
            {
                destinationExit.PlacePlayerAtRelease(playerContext);
                if (currentStage != null && currentStage != nextStage)
                {
                    currentStage.SetStageActive(false);
                }
                yield return destinationExit.CompleteArrival(playerContext);
            }
        }
    }

    private MapHangMinigameManager ResolveStageManager()
    {
        MapHangMinigameManager manager =
            GetComponentInParent<MapHangMinigameManager>(true);

        if (manager == null && destinationExit != null)
        {
            manager = destinationExit.GetComponentInParent<MapHangMinigameManager>(true);
        }

        return manager != null
            ? manager
            : FindFirstObjectByType<MapHangMinigameManager>();
    }

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        if (destinationExit != null)
        {
            Gizmos.DrawLine(transform.position, destinationExit.transform.position);
        }
    }

#endif
}


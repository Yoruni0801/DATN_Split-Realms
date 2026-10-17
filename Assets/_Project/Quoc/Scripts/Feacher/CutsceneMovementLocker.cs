using CutsceneEngine;
using UnityEngine;

[DisallowMultipleComponent]
public class CutsceneMovementLocker : MonoBehaviour
{
    [SerializeField] private Cutscene cutscene;
    [SerializeField] private Player player;
    [SerializeField] private bool lockWhenPaused = true;

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponent<Player>();
            if (player == null)
            {
                player = GetComponentInParent<Player>();
            }
        }
    }

    private void OnEnable()
    {
        if (cutscene != null)
        {
            cutscene.onStateChanged += HandleCutsceneStateChanged;
            ApplyLockState(cutscene.state);
        }
    }

    private void Start()
    {
        if (cutscene != null)
        {
            ApplyLockState(cutscene.state);
        }
    }

    private void OnDisable()
    {
        if (cutscene != null)
        {
            cutscene.onStateChanged -= HandleCutsceneStateChanged;
        }

        if (player != null)
        {
            player.SetControlLocked(false);
        }
    }

    private void HandleCutsceneStateChanged(CutsceneState state)
    {
        ApplyLockState(state);
    }

    private void ApplyLockState(CutsceneState state)
    {
        if (player == null)
        {
            return;
        }

        bool shouldLock = state == CutsceneState.Playing
                          || (lockWhenPaused && state == CutsceneState.Paused);

        player.SetControlLocked(shouldLock);
    }
}

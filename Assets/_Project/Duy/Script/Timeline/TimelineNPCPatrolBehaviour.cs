using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class TimelineNPCPatrolBehaviour : PlayableBehaviour
{
    public TimelineNPCPatrolTargetMode targetMode = TimelineNPCPatrolTargetMode.AllNPCs;
    public NPCRandomPatrol targetOverride;
    public bool enablePatrol = true;
    public bool overrideInvertFacing;
    public bool invertFacing;
    public bool includeInactiveTargets = true;

    private readonly List<NPCRandomPatrol> targets = new List<NPCRandomPatrol>();
    private bool clipActive;

    public void Enter(NPCRandomPatrol boundTarget, TimelineNPCPatrolMixerBehaviour mixer)
    {
        if (clipActive)
        {
            return;
        }

        clipActive = true;
        CollectTargets(boundTarget);

        for (int i = 0; i < targets.Count; i++)
        {
            NPCRandomPatrol patrol = targets[i];
            if (patrol != null)
            {
                if (mixer != null)
                {
                    mixer.CacheOriginalState(patrol);
                }

                if (overrideInvertFacing)
                {
                    patrol.SetInvertFacing(invertFacing);
                }

                patrol.SetEnablePatrol(enablePatrol);
            }
        }
    }

    public void Exit()
    {
        clipActive = false;
        targets.Clear();
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        Exit();
    }

    private void CollectTargets(NPCRandomPatrol boundTarget)
    {
        targets.Clear();

        if (targetMode == TimelineNPCPatrolTargetMode.AllNPCs)
        {
            NPCRandomPatrol[] all = Object.FindObjectsByType<NPCRandomPatrol>(
                includeInactiveTargets ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && !targets.Contains(all[i]))
                {
                    targets.Add(all[i]);
                }
            }
            return;
        }

        NPCRandomPatrol resolved = targetOverride != null ? targetOverride : boundTarget;
        if (resolved != null)
        {
            targets.Add(resolved);
        }
    }
}

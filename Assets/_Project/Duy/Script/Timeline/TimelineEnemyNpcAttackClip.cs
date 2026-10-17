using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TimelineEnemyNpcAttackType
{
    Auto,
    RiftcoreAoe,
    HuskfangRoot
}

public class TimelineEnemyNpcAttackClip : PlayableAsset, ITimelineClipAsset
{
    public ExposedReference<Transform> enemyOverride;
    public ExposedReference<Transform> npcTarget;

    public TimelineEnemyNpcAttackType attackType = TimelineEnemyNpcAttackType.Auto;
    [Min(0f)] public float impactNormalizedTime = 0.65f;
    [Min(0)] public int damage = 20;
    public bool damageNpc = true;
    public bool faceTarget = true;
    public bool triggerAnimator = true;
    public string animatorTrigger = "Attack";
    public bool stopEnemyVelocity = true;
    public UnityEvent onImpact;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelineEnemyNpcAttackBehaviour> playable =
            ScriptPlayable<TimelineEnemyNpcAttackBehaviour>.Create(graph);

        TimelineEnemyNpcAttackBehaviour behaviour = playable.GetBehaviour();
        behaviour.enemyOverride = enemyOverride.Resolve(graph.GetResolver());
        behaviour.npcTarget = npcTarget.Resolve(graph.GetResolver());
        behaviour.attackType = attackType;
        behaviour.impactNormalizedTime = Mathf.Clamp01(impactNormalizedTime);
        behaviour.damage = Mathf.Max(0, damage);
        behaviour.damageNpc = damageNpc;
        behaviour.faceTarget = faceTarget;
        behaviour.triggerAnimator = triggerAnimator;
        behaviour.animatorTrigger = animatorTrigger;
        behaviour.stopEnemyVelocity = stopEnemyVelocity;
        behaviour.onImpact = onImpact;

        return playable;
    }
}

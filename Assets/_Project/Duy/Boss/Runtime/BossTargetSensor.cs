using UnityEngine;

public sealed class BossTargetSensor
{
    private readonly Transform self;
    private readonly LayerMask playerLayer;

    private Transform target;
    private Transform aggroTarget;

    public BossTargetSensor(Transform self, LayerMask playerLayer)
    {
        this.self = self;
        this.playerLayer = playerLayer;
    }

    public Transform Target => target;
    public bool IsAggro => aggroTarget != null;

    public void ActivateAggro(Transform attacker)
    {
        if (attacker == null)
        {
            return;
        }

        Transform root = attacker.root;
        if ((playerLayer.value & (1 << root.gameObject.layer)) == 0)
        {
            return;
        }

        aggroTarget = root;
        target = root;
    }

    public Transform Resolve(float detectRadius)
    {
        if (IsValidTarget(aggroTarget))
        {
            target = aggroTarget;
            return target;
        }

        aggroTarget = null;
        Collider2D hit = Physics2D.OverlapCircle(self.position, detectRadius, playerLayer);
        target = hit != null ? hit.transform.root : null;
        return target;
    }

    public bool IsValidTarget(Transform candidate)
    {
        return candidate != null && candidate.gameObject.activeInHierarchy;
    }
}

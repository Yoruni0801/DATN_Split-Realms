using UnityEngine;

public static class DamageColliderUtility
{
    public static bool IsNonDamageSensor(Collider2D collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (collider.GetComponent<DuyBossController>() != null)
        {
            return false;
        }

        BossThreatDetector detector = collider.GetComponent<BossThreatDetector>();
        if (detector != null)
        {
            return true;
        }

        return collider.GetComponent<ThreatSensorCollider>() != null;
    }
}

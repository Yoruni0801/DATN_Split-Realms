using UnityEngine;

public class EnemyParryResponder : ParryableObject
{
    public new static EnemyParryResponder Ensure(Component source)
    {
        if (source == null)
        {
            return null;
        }

        EnemyParryResponder responder = source.GetComponentInParent<EnemyParryResponder>();
        return responder != null ? responder : source.gameObject.AddComponent<EnemyParryResponder>();
    }
}

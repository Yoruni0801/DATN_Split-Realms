using UnityEngine;

[DisallowMultipleComponent]
public sealed class MapHangStageRoot2D : MonoBehaviour
{
    public void SetStageActive(bool active)
    {
        gameObject.SetActive(active);
    }
}

using UnityEngine;

public class ToggleGravityAction : PlayerGravityToggleInput, ITimelineInputAction
{
    public void Execute()
    {
        Debug.Log("Toggle");
        ToggleGravity();
        // Dash code
    }
}
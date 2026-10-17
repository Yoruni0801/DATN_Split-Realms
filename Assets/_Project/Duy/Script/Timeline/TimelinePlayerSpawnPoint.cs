using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Timeline/Timeline Player Spawn Point")]
public sealed class TimelinePlayerSpawnPoint : MonoBehaviour, ITimelinePlayerSpawnPoint
{
    [SerializeField] private string markerId;

    public string MarkerId => markerId;
}

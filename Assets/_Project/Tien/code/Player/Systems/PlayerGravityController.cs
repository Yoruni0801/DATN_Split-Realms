using System.Collections;
using UnityEngine;

/// <summary>
/// Gắn vào scene/map object ở những màn hình có cơ chế đảo trọng lực.
/// Tự lấy Player qua PlayerRegistry khi Enable, unsubscribe + reset khi Disable.
/// </summary>
public class PlayerGravityController : MonoBehaviour
{
    [SerializeField] private float colliderDisableDuration = 0.4f;

    private Player player;
    private Collider2D playerBodyCollider;
    private bool gravityInverted;

    public bool IsInverted => gravityInverted;

    private void OnEnable()
    {
        player = PlayerRegistry.Current;
        if (player == null)
        {
            Debug.LogWarning("[PlayerGravityController] PlayerRegistry.Current is null.", this);
            return;
        }

        playerBodyCollider = player.GetComponent<Collider2D>();
        player.OnGravityToggleRequested += Toggle;
    }

    private void OnDisable()
    {
        if (player == null)
            return;

        player.OnGravityToggleRequested -= Toggle;

        if (gravityInverted)
        {
            gravityInverted = false;
            player.SetGravityInverted(false);
        }

        player = null;
        playerBodyCollider = null;
    }

    private void Toggle()
    {
        gravityInverted = !gravityInverted;
        player.SetGravityInverted(gravityInverted);

        if (colliderDisableDuration > 0f)
            StartCoroutine(DisableColliderTemporarily(colliderDisableDuration));
    }

    private IEnumerator DisableColliderTemporarily(float duration)
    {
        if (playerBodyCollider == null)
            yield break;

        playerBodyCollider.enabled = false;
        yield return new WaitForSeconds(duration);
        playerBodyCollider.enabled = true;
    }
}

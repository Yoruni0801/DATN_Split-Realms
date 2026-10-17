using UnityEngine;

public class RotaAmbientVfxController : MonoBehaviour
{
    [Header("Particle References")]
    [SerializeField] private ParticleSystem antiGravityMotes;
    [SerializeField] private ParticleSystem floatingDebris;

    [Header("Gravity Response")]
    [SerializeField] private float gravityDriftSpeed = 1.2f;
    [SerializeField] private float responseDamping = 3.0f;

    private Vector2 currentGravityDir = Vector2.down;
    private Vector2 targetGravityDir = Vector2.down;
    private PlayerRotaSurfaceController boundPlayer;

    private void OnEnable()
    {
        RotaSurfaceZone.PlayerEnteredAnyZone += HandlePlayerEntered;
        RotaSurfaceZone.PlayerExitedAllZones += HandlePlayerExited;
    }

    private void OnDisable()
    {
        RotaSurfaceZone.PlayerEnteredAnyZone -= HandlePlayerEntered;
        RotaSurfaceZone.PlayerExitedAllZones -= HandlePlayerExited;
    }

    private void HandlePlayerEntered(Player player)
    {
        if (player != null)
        {
            boundPlayer = player.GetComponent<PlayerRotaSurfaceController>();
        }
    }

    private void HandlePlayerExited()
    {
        boundPlayer = null;
        targetGravityDir = Vector2.down;
    }

    private void Update()
    {
        if (boundPlayer != null && boundPlayer.IsActive)
        {
            // Bụi khí trôi ngược lại hướng trọng lực hoặc thuận theo trường hấp dẫn bề mặt
            Vector2 normal = boundPlayer.SurfaceNormal;
            if (normal.sqrMagnitude > 0.01f)
            {
                targetGravityDir = normal; // Motes trôi bồng bềnh hướng ra ngoài bề mặt
            }
        }
        else
        {
            targetGravityDir = Vector2.up; // Mặc định trôi bồng bềnh lên
        }

        currentGravityDir = Vector2.Lerp(currentGravityDir, targetGravityDir, Time.deltaTime * responseDamping);

        UpdateParticleForce(antiGravityMotes, currentGravityDir * gravityDriftSpeed);
        UpdateParticleForce(floatingDebris, currentGravityDir * (gravityDriftSpeed * 0.5f));
    }

    private void UpdateParticleForce(ParticleSystem ps, Vector2 drift)
    {
        if (ps == null) return;
        var forceModule = ps.forceOverLifetime;
        if (forceModule.enabled)
        {
            forceModule.x = new ParticleSystem.MinMaxCurve(drift.x);
            forceModule.y = new ParticleSystem.MinMaxCurve(drift.y);
        }
    }
}

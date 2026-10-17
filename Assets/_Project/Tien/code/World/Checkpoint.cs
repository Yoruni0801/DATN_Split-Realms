using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private Transform spawnPoint;

    [Header("Activation")]
    [SerializeField] private bool activateOnce = true;

    [Header("Audio")]
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private AudioSource activationAudio;

    [Header("Feedback")]
    [SerializeField] private Color activatedColor = Color.green;
    [SerializeField] private GameObject activatedVisual;

    private SpriteRenderer spriteRenderer;
    private bool activated;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[Checkpoint] Collider2D on '{gameObject.name}' is not set as a trigger. OnTriggerEnter2D will not fire.", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (activateOnce && activated)
        {
            return;
        }

        PlayerHealth health = collision.GetComponent<PlayerHealth>();
        if (health == null)
        {
            health = collision.GetComponentInParent<PlayerHealth>();
        }

        if (health == null || health.IsDead)
        {
            return;
        }

        activated = true;

        Vector2 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        health.UpdateCheckpoint(spawnPosition);

        SceneRuntimeStateManager.SaveCheckpoint(gameObject.scene.name, spawnPosition);

        if (activatedVisual != null)
        {
            activatedVisual.SetActive(true);
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.color = activatedColor;
        }

        if (activationAudio != null)
        {
            if (!activationAudio.isPlaying)
            {
                activationAudio.Play();
            }
        }
        else if (activationSound != null)
        {
            AudioManager.PlaySFXIfAvailable(activationSound, transform.position);
        }
    }
}

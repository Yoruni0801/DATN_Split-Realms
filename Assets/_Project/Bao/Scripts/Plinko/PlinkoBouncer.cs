using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class PlinkoBouncer : MonoBehaviour
{
    [Header("Bounce Settings")]
    [SerializeField] private float impulseForce = 8f;
    [SerializeField] private float upwardBias = 0.65f;
    [SerializeField] private float cooldown = 0.12f;
    [SerializeField] private string rockTag = "IceRock";
    [SerializeField] private bool scaleByIncomingSpeed = true;

    [Header("Audio Settings (Âm Thanh Bật Nảy SFX)")]
    [SerializeField] private bool enableAudio = true;
    [SerializeField] private AudioClip bounceSound;
    [SerializeField, Range(0f, 1f)] private float bounceVolume = 0.7f;
    [SerializeField] private bool randomizePitch = true;
    [SerializeField] private float minPitch = 0.9f;
    [SerializeField] private float maxPitch = 1.15f;

    private readonly Dictionary<int, float> lastBounceTime = new Dictionary<int, float>();
    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col != null && col.isTrigger)
        {
            Debug.LogWarning($"{nameof(PlinkoBouncer)} on {gameObject.name}: Collider is a trigger. OnCollisionEnter2D will not fire.", this);
        }

        if (bounceSound == null)
        {
            #if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("genericimpact t:AudioClip");
            if (guids.Length == 0) guids = UnityEditor.AssetDatabase.FindAssets("frostimpact t:AudioClip");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                bounceSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            #endif
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Rigidbody2D rockRb = ResolveRockRigidbody(collision);
        if (rockRb == null) return;

        GameObject rockObject = rockRb.gameObject;
        if (!IsTaggedRock(rockObject)) return;

        int instanceId = rockObject.GetInstanceID();
        
        // Check cooldown
        if (lastBounceTime.TryGetValue(instanceId, out float lastTime))
        {
            float timeSinceLastBounce = Time.time - lastTime;
            if (timeSinceLastBounce < cooldown)
                return;
        }

        // Apply bounce
        ApplyBounce(collision, rockRb, instanceId);
    }

    private Rigidbody2D ResolveRockRigidbody(Collision2D collision)
    {
        if (collision == null) return null;

        // Try collision.rigidbody first
        if (collision.rigidbody != null)
            return collision.rigidbody;

        // Try collision.collider.attachedRigidbody
        Collider2D collisionCollider = collision.collider;
        if (collisionCollider != null && collisionCollider.attachedRigidbody != null)
            return collisionCollider.attachedRigidbody;

        // Try to find Rigidbody2D on the collider's gameObject
        if (collisionCollider != null)
        {
            Rigidbody2D rb = collisionCollider.GetComponent<Rigidbody2D>();
            if (rb != null) return rb;
        }

        return null;
    }

    private bool IsTaggedRock(GameObject candidate)
    {
        if (candidate == null || string.IsNullOrEmpty(rockTag))
            return false;

        // Check the candidate itself
        if (candidate.CompareTag(rockTag))
            return true;

        // Check parents
        Transform parent = candidate.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag(rockTag))
                return true;
            parent = parent.parent;
        }

        return false;
    }

    private void ApplyBounce(Collision2D collision, Rigidbody2D rockRb, int instanceId)
    {
        // Get first contact normal
        Vector2 contactNormal = collision.contacts.Length > 0 ? 
            collision.contacts[0].normal : 
            Vector2.up;

        // Combine with upward bias
        Vector2 bounceDirection = (contactNormal + Vector2.up * upwardBias).normalized;

        // Calculate impulse magnitude
        float impulseMagnitude = impulseForce;

        if (scaleByIncomingSpeed)
        {
            float incomingSpeed = rockRb.linearVelocity.magnitude;
            float speedMultiplier = Mathf.Clamp(incomingSpeed / 5f, 0.75f, 1.5f);
            impulseMagnitude *= speedMultiplier;
        }

        // Apply impulse
        rockRb.AddForce(bounceDirection * impulseMagnitude, ForceMode2D.Impulse);

        // Record bounce time
        lastBounceTime[instanceId] = Time.time;

        // Phát âm thanh bật nảy SFX
        PlayBounceAudio(collision.contacts.Length > 0 ? (Vector3)collision.contacts[0].point : transform.position);

        // Clean stale entries opportunistically
        CleanStaleEntries();
    }

    private void PlayBounceAudio(Vector3 position)
    {
        if (!enableAudio || bounceSound == null) return;

        float pitch = randomizePitch ? Random.Range(minPitch, maxPitch) : 1f;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(bounceSound, position, pitch);
            return;
        }

        AudioSource.PlayClipAtPoint(bounceSound, position, bounceVolume);
    }

    private void CleanStaleEntries()
    {
        float currentTime = Time.time;
        List<int> keysToRemove = new List<int>();

        foreach (var kvp in lastBounceTime)
        {
            if (currentTime - kvp.Value > cooldown * 10f)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (int key in keysToRemove)
        {
            lastBounceTime.Remove(key);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlinkoMissSurface : MonoBehaviour
{
    [SerializeField] private string rockTag = "IceRock";
    [SerializeField] private bool spawnBreakEffect = true;
    [SerializeField] private int shardCount = 18;
    [SerializeField] private float effectLifetime = 0.75f;
    [SerializeField] private Color shardColor = new Color(0.75f, 0.95f, 1f, 1f);

    private readonly HashSet<int> handledRocks = new HashSet<int>();

    private void Awake()
    {
        Collider2D surfaceCollider = GetComponent<Collider2D>();
        if (surfaceCollider != null && surfaceCollider.isTrigger)
        {
            Debug.LogError($"{nameof(PlinkoMissSurface)} requires a non-trigger Collider2D.", this);
            enabled = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        GameObject rock = ResolveRock(collision);
        if (rock == null || !IsTaggedRock(rock)) return;

        int instanceId = rock.GetInstanceID();
        if (!handledRocks.Add(instanceId)) return;

        StartCoroutine(HandleMissAfterFixedUpdate(rock, instanceId));
    }

    private IEnumerator HandleMissAfterFixedUpdate(GameObject rock, int instanceId)
    {
        yield return new WaitForFixedUpdate();

        if (rock == null)
        {
            handledRocks.Remove(instanceId);
            yield break;
        }

        if (spawnBreakEffect)
            PlinkoBreakTarget.SpawnBreakEffect(rock.transform.position, shardCount, effectLifetime, shardColor);

        Collider2D[] rockColliders = rock.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < rockColliders.Length; i++)
            rockColliders[i].enabled = false;

        Rigidbody2D rockRigidbody = rock.GetComponent<Rigidbody2D>();
        if (rockRigidbody != null)
            rockRigidbody.simulated = false;

        Destroy(rock);
    }

    private GameObject ResolveRock(Collision2D collision)
    {
        if (collision == null) return null;

        if (collision.rigidbody != null)
            return collision.rigidbody.gameObject;

        Collider2D collisionCollider = collision.collider;
        if (collisionCollider == null) return null;

        if (collisionCollider.attachedRigidbody != null)
            return collisionCollider.attachedRigidbody.gameObject;

        if (IsTaggedRock(collisionCollider.gameObject))
            return collisionCollider.gameObject;

        Transform parent = collisionCollider.transform.parent;
        while (parent != null)
        {
            if (IsTaggedRock(parent.gameObject))
                return parent.gameObject;

            parent = parent.parent;
        }

        return collisionCollider.gameObject;
    }

    private bool IsTaggedRock(GameObject candidate)
    {
        return candidate != null && !string.IsNullOrEmpty(rockTag) && candidate.CompareTag(rockTag);
    }
}

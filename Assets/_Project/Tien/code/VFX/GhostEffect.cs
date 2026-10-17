using UnityEngine;

public class GhostEffect : MonoBehaviour
{
    [SerializeField] private float fadeSpeed = 5f;

    private SpriteRenderer[] renderers;

    public void Setup(Transform source, Color color)
    {
        if (source == null)
        {
            Destroy(gameObject);
            return;
        }

        SpriteRenderer[] sourceRenderers = source.GetComponentsInChildren<SpriteRenderer>();
        renderers = new SpriteRenderer[sourceRenderers.Length];

        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            renderers[i] = CreateGhostPart(sourceRenderers[i], color);
        }
    }

    private void Update()
    {
        if (renderers == null || renderers.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        bool fullyFaded = true;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = Mathf.Max(0f, color.a - fadeSpeed * Time.deltaTime);
            renderer.color = color;
            fullyFaded &= color.a <= 0f;
        }

        if (fullyFaded)
        {
            Destroy(gameObject);
        }
    }

    private SpriteRenderer CreateGhostPart(SpriteRenderer source, Color color)
    {
        GameObject part = new GameObject(source.name);
        part.transform.SetParent(transform, worldPositionStays: true);
        part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        part.transform.localScale = source.transform.lossyScale;

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder - 1;
        renderer.flipX = source.flipX;
        renderer.flipY = source.flipY;
        renderer.color = color;
        return renderer;
    }
}

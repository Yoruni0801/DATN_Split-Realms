using System.Collections;
using UnityEngine;

public sealed class PetrifyGroupController : MonoBehaviour
{
    [Header("Renderers")]
    [SerializeField] private SpriteRenderer[] spriteRenderers;

    [Header("Material")]
    [SerializeField] private Material petrifyMaterial;
    [SerializeField] private bool assignMaterialOnAwake = true;

    [Header("Petrify")]
    [SerializeField, Range(0f, 1f)] private float petrifyAmount;
    [SerializeField] private bool updateBoundsEveryFrame = true;

    private MaterialPropertyBlock propertyBlock;
    private Coroutine currentRoutine;

    private static readonly int PetrifyAmountId = Shader.PropertyToID("_PetrifyAmount");
    private static readonly int GroupBottomYId = Shader.PropertyToID("_GroupBottomY");
    private static readonly int GroupTopYId = Shader.PropertyToID("_GroupTopY");

    private void Reset()
    {
        CollectRenderers();
    }

    private void Awake()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            CollectRenderers();

        propertyBlock = new MaterialPropertyBlock();

        if (assignMaterialOnAwake && petrifyMaterial != null)
            AssignMaterialToAllSprites();

        Apply();
    }

    private void LateUpdate()
    {
        if (updateBoundsEveryFrame)
            Apply();
    }

    private void OnValidate()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            CollectRenderers();

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        Apply();
    }

    [ContextMenu("Collect Renderers")]
    private void CollectRenderers()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    [ContextMenu("Assign Material To All Sprites")]
    private void AssignMaterialToAllSprites()
    {
        if (petrifyMaterial == null || spriteRenderers == null)
            return;

        foreach (SpriteRenderer sr in spriteRenderers)
        {
            if (sr == null)
                continue;

            sr.sharedMaterial = petrifyMaterial;
        }
    }

    public void SetPetrifyAmount(float value)
    {
        petrifyAmount = Mathf.Clamp01(value);
        Apply();
    }

    public void Petrify(float duration)
    {
        Play(0f, 1f, duration);
    }

    public void Unpetrify(float duration)
    {
        Play(1f, 0f, duration);
    }

    public void Play(float from, float to, float duration)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(PlayRoutine(from, to, duration));
    }

    private IEnumerator PlayRoutine(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetPetrifyAmount(to);
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);

            // Smooth step.
            t = t * t * (3f - 2f * t);

            SetPetrifyAmount(Mathf.Lerp(from, to, t));

            yield return null;
        }

        SetPetrifyAmount(to);
        currentRoutine = null;
    }

    private void Apply()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        Bounds groupBounds = GetGroupBounds();

        float bottomY = groupBounds.min.y;
        float topY = groupBounds.max.y;

        foreach (SpriteRenderer sr in spriteRenderers)
        {
            if (sr == null)
                continue;

            sr.GetPropertyBlock(propertyBlock);

            propertyBlock.SetFloat(PetrifyAmountId, petrifyAmount);
            propertyBlock.SetFloat(GroupBottomYId, bottomY);
            propertyBlock.SetFloat(GroupTopYId, topY);

            sr.SetPropertyBlock(propertyBlock);
        }
    }

    private Bounds GetGroupBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);

        foreach (SpriteRenderer sr in spriteRenderers)
        {
            if (sr == null || !sr.enabled || sr.sprite == null)
                continue;

            if (!hasBounds)
            {
                bounds = sr.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(sr.bounds);
            }
        }

        return bounds;
    }
}
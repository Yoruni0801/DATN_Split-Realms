using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerVisualController
{
    private readonly MonoBehaviour coroutineOwner;
    private readonly PlayerAnimationController animationController;
    private readonly SpriteRenderer[] spriteRenderers;
    private readonly Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();
    private Coroutine flashRoutine;

    public PlayerVisualController(MonoBehaviour coroutineOwner, PlayerAnimationController animationController, SpriteRenderer[] spriteRenderers)
    {
        this.coroutineOwner = coroutineOwner;
        this.animationController = animationController;
        this.spriteRenderers = spriteRenderers;

        if (spriteRenderers == null)
        {
            return;
        }

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer != null)
            {
                originalColors[spriteRenderer] = spriteRenderer.color;
            }
        }
    }

    public void PlayHurt()
    {
        animationController.PlayHurt();
        if (flashRoutine == null)
        {
            flashRoutine = coroutineOwner.StartCoroutine(HitFlashRoutine());
        }
    }

    public void PlayDeath()
    {
        animationController.ClearCombat();
        animationController.PlayDeath();
    }

    public void PlayIdle()
    {
        animationController.PlayIdle();
    }

    private IEnumerator HitFlashRoutine()
    {
        if (spriteRenderers == null)
        {
            yield break;
        }

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.red;
            }
        }

        yield return new WaitForSecondsRealtime(0.15f);

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer != null && originalColors.TryGetValue(spriteRenderer, out Color originalColor))
            {
                spriteRenderer.color = originalColor;
            }
            else if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
        }

        flashRoutine = null;
    }
}

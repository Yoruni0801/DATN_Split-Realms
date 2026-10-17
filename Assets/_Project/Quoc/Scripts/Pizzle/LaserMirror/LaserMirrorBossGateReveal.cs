using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LaserMirrorBossGateReveal : MonoBehaviour
{
    [SerializeField] private GameObject bossGate;
    [SerializeField, Min(0.01f)] private float revealDuration = 0.8f;

    private SpriteRenderer gateRenderer;
    private ParticleSystem[] gateParticles;
    private CancellationTokenSource destroyCts;
    private bool isRevealed;

    private void Awake()
    {
        destroyCts = new CancellationTokenSource();

        if (bossGate != null)
        {
            gateRenderer = bossGate.GetComponent<SpriteRenderer>();
            gateParticles = bossGate.GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    private void OnDestroy()
    {
        destroyCts?.Cancel();
        destroyCts?.Dispose();
    }

    public void TriggerReveal()
    {
        if (isRevealed || bossGate == null) return;

        isRevealed = true;
        _ = RevealBossGateAsync(destroyCts.Token);
    }

    private async Task RevealBossGateAsync(CancellationToken ct)
    {
        try
        {
            bossGate.SetActive(true);

            if (gateRenderer != null)
            {
                gateRenderer.enabled = true;
                SetRendererAlpha(0f);
            }

            if (gateParticles != null)
            {
                for (int i = 0; i < gateParticles.Length; i++)
                {
                    if (gateParticles[i] != null)
                        gateParticles[i].Play(true);
                }
            }

            float elapsed = 0f;
            while (elapsed < revealDuration)
            {
                ct.ThrowIfCancellationRequested();

                elapsed += Time.deltaTime;
                float alpha = Mathf.SmoothStep(0f, 1f, elapsed / revealDuration);
                SetRendererAlpha(alpha);

                await Task.Yield();
            }

            SetRendererAlpha(1f);
        }
        catch (OperationCanceledException) { }
    }

    private void SetRendererAlpha(float alpha)
    {
        if (gateRenderer == null) return;
        Color color = gateRenderer.color;
        color.a = alpha;
        gateRenderer.color = color;
    }
}

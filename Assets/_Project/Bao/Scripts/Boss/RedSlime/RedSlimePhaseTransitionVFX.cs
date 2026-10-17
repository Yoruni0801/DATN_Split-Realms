using UnityEngine;
using System.Collections;

namespace Bao.Bosses
{
    public class RedSlimePhaseTransitionVFX : MonoBehaviour
    {
        [ColorUsage(true, true)] public Color slimeColorMain = new Color(3.2f, 2.2f, 0.2f, 1f); // Radiant Solar Amber-Gold
        [ColorUsage(true, true)] public Color slimeColorSecondary = new Color(2.8f, 1.2f, 0.1f, 1f); // Molten Honey Orange

        private void Start()
        {
            // Generate a soft circular texture for the slime droplets
            Texture2D particleTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float distance = Vector2.Distance(new Vector2(16, 16), new Vector2(x, y));
                    float alpha = Mathf.Clamp01(1f - (distance / 16f)); 
                    particleTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            particleTex.Apply();

            ParticleSystem ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // --- MAIN SLIME SPLASH (Stretching Droplets) ---
            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;
            var sizeOverLifetime = ps.sizeOverLifetime;
            var colorOverLifetime = ps.colorOverLifetime;

            ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 25; // Render on top of boss

            Shader spriteShader = Shader.Find("Sprites/Default");
            Material mat = null;
            if (spriteShader != null)
            {
                mat = new Material(spriteShader);
                mat.SetTexture("_MainTex", particleTex);
                renderer.material = mat;
            }

            // Make particles stretch along their velocity to look like gooey slime
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.08f;
            renderer.lengthScale = 2.5f;

            // Main Slime Splash Physics
            main.duration = 2.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 28f); // Faster explosive burst
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(slimeColorMain, slimeColorSecondary);
            main.gravityModifier = 0.8f; // Less gravity so they fly further outward
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Explosion Burst
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { 
                new ParticleSystem.Burst(0f, 150),
                new ParticleSystem.Burst(0.1f, 100),
                new ParticleSystem.Burst(0.2f, 50)
            });

            // Shape (Sphere outwards)
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            transform.rotation = Quaternion.Euler(0f, 0f, 0f); // Explode in all 2D directions

            // Size Curve (Starts big, shrinks)
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Color Fade Out
            colorOverLifetime.enabled = true;
            Gradient alphaGrad = new Gradient();
            alphaGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGrad);

            // --- CORE SHOCKWAVE BURST ---
            GameObject shockwaveObj = new GameObject("Shockwave");
            shockwaveObj.transform.SetParent(transform);
            shockwaveObj.transform.localPosition = Vector3.zero;
            ParticleSystem swPs = shockwaveObj.AddComponent<ParticleSystem>();
            swPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            
            var swMain = swPs.main;
            swMain.duration = 1f;
            swMain.startLifetime = 0.4f;
            swMain.startSpeed = 0f;
            swMain.startSize = 1f;
            swMain.startColor = slimeColorMain;
            swMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            
            var swEmission = swPs.emission;
            swEmission.rateOverTime = 0;
            swEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });
            
            var swShape = swPs.shape;
            swShape.enabled = false;
            
            var swSize = swPs.sizeOverLifetime;
            swSize.enabled = true;
            AnimationCurve swSizeCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 15f)); // Expands hugely
            swSize.size = new ParticleSystem.MinMaxCurve(1f, swSizeCurve);
            
            var swColor = swPs.colorOverLifetime;
            swColor.enabled = true;
            Gradient swGrad = new Gradient();
            swGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            swColor.color = new ParticleSystem.MinMaxGradient(swGrad);
            
            ParticleSystemRenderer swRenderer = shockwaveObj.GetComponent<ParticleSystemRenderer>();
            swRenderer.sortingOrder = 24; // Just behind droplets
            if (mat != null) swRenderer.material = mat;

            // Play both
            ps.Play();
            swPs.Play();

            // Destroy after effect completes
            Destroy(gameObject, 3f);
        }
    }
}

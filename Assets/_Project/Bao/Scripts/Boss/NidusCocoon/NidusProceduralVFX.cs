using UnityEngine;

namespace Bao.Bosses
{
    public enum NidusVFXType
    {
        RoarShockwave,
        BurrowDust,
        EmergeExplosion,
        BurrowTrail
    }

    public class NidusProceduralVFX : MonoBehaviour
    {
        public NidusVFXType vfxType;
        [ColorUsage(true, true)] public Color primaryColor = new Color(0.3f, 2f, 0.3f, 1f); // Neon Green HDR
        [ColorUsage(true, true)] public Color secondaryColor = new Color(1.5f, 0.8f, 0.2f, 1f); // Toxic Yellow HDR

        private void Start()
        {
            ParticleSystem ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;
            var colorOverLifetime = ps.colorOverLifetime;
            var sizeOverLifetime = ps.sizeOverLifetime;
            var noise = ps.noise;

            ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 20;

            // Generate a soft circular texture for the particles to remove the pixelated square look
            Texture2D particleTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float distance = Vector2.Distance(new Vector2(16, 16), new Vector2(x, y));
                    float alpha = Mathf.Clamp01(1f - (distance / 16f)); 
                    // Make the center solid and fade out near the edge
                    particleTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            particleTex.Apply();

            // Try to use URP Particle Shader for better visuals (HDR, blending)
            Shader urpParticle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpParticle != null)
            {
                Material mat = new Material(urpParticle);
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0); // Alpha Blend
                mat.SetTexture("_BaseMap", particleTex);
                renderer.material = mat;
            }
            else
            {
                Material mat = new Material(Shader.Find("Sprites/Default"));
                mat.SetTexture("_MainTex", particleTex);
                renderer.material = mat;
            }

            colorOverLifetime.enabled = true;
            sizeOverLifetime.enabled = true;

            // Smooth fading
            Gradient alphaGrad = new Gradient();
            alphaGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGrad);

            AnimationCurve sizeCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            switch (vfxType)
            {
                case NidusVFXType.RoarShockwave:
                    main.duration = 1.5f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 25f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 2.0f);
                    main.startColor = primaryColor;

                    emission.rateOverTime = 0;
                    emission.SetBursts(new ParticleSystem.Burst[] {
                        new ParticleSystem.Burst(0f, 150),
                        new ParticleSystem.Burst(0.2f, 80)
                    });

                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.1f;
                    shape.radiusThickness = 0.1f;

                    noise.enabled = true;
                    noise.strength = 1.5f;
                    noise.frequency = 2f;
                    noise.scrollSpeed = 1f;

                    Destroy(gameObject, 2f);
                    break;

                case NidusVFXType.BurrowDust:
                    main.duration = 0.8f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                    Color bDirt1 = new Color(0.4f, 0.25f, 0.12f, 1f);
                    Color bDirt2 = new Color(0.25f, 0.15f, 0.05f, 1f);
                    main.startColor = new ParticleSystem.MinMaxGradient(bDirt1, bDirt2);
                    main.gravityModifier = 1.5f;

                    emission.rateOverTime = 0;
                    emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 100) });

                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 60f;
                    shape.radius = 0.4f;
                    transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

                    noise.enabled = false;

                    Destroy(gameObject, 1.5f);
                    break;

                case NidusVFXType.EmergeExplosion:
                    main.duration = 1f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.75f);
                    
                    Color dirtColor = new Color(0.4f, 0.25f, 0.12f, 1f);
                    Color dirtColor2 = new Color(0.25f, 0.15f, 0.05f, 1f);
                    main.startColor = new ParticleSystem.MinMaxGradient(dirtColor, dirtColor2);
                    main.gravityModifier = 2.0f; // Dirt falls back down

                    emission.rateOverTime = 0;
                    emission.SetBursts(new ParticleSystem.Burst[] { 
                        new ParticleSystem.Burst(0f, 250),
                        new ParticleSystem.Burst(0.1f, 100)
                    });

                    shape.shapeType = ParticleSystemShapeType.Hemisphere;
                    shape.radius = 0.4f;
                    transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

                    noise.enabled = false; // Disable noise to allow smooth gravity fall

                    Destroy(gameObject, 2f);
                    break;

                case NidusVFXType.BurrowTrail:
                    main.duration = 0.3f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
                    main.startColor = secondaryColor;

                    emission.rateOverTime = 0;
                    emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25) });

                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.4f;
                    transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

                    Destroy(gameObject, 1.5f);
                    break;
            }

            ps.Play();
        }
    }
}

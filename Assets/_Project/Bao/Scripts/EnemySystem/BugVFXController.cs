using UnityEngine;

namespace Bao.Enemies
{
    internal sealed class BugVFXController
    {
        private readonly BugEnemy enemy;

        private GameObject root;
        private GameObject warningObject;
        private ParticleSystem wingMotes;
        private ParticleSystem detectionFlash;
        private ParticleSystem descentStreaks;
        private ParticleSystem landingDust;
        private ParticleSystem landingRing;
        private ParticleSystem attackTelegraph;
        private ParticleSystem biteSlash;
        private ParticleSystem biteImpact;
        private ParticleSystem takeoffDust;
        private ParticleSystem deathBurst;
        private float scaleCompensation = 1f;

        private static Material particleMaterial;

        public BugVFXController(BugEnemy enemy)
        {
            this.enemy = enemy;
        }

        public void Initialize()
        {
            float inheritedScale = Mathf.Max(
                Mathf.Abs(enemy.transform.lossyScale.x),
                Mathf.Abs(enemy.transform.lossyScale.y));
            scaleCompensation = 1f / Mathf.Max(0.01f, inheritedScale);

            root = new GameObject("Bug VFX");
            root.layer = enemy.gameObject.layer;
            root.transform.SetParent(enemy.transform, false);

            wingMotes = CreateEffect(
                "Wing Motes",
                new Vector3(0f, 3.2f, 0f),
                true,
                18f,
                0,
                0.45f,
                0.35f,
                0.65f,
                new Color(1f, 0.82f, 0.28f, 0.65f),
                1.8f);

            detectionFlash = CreateEffect(
                "Detection Flash",
                new Vector3(0f, 4.6f, 0f),
                false,
                0f,
                18,
                0.45f,
                0.75f,
                2.2f,
                new Color(1f, 0.68f, 0.12f, 0.9f),
                0.2f);

            descentStreaks = CreateEffect(
                "Descent Streaks",
                new Vector3(0f, 3f, 0f),
                true,
                28f,
                0,
                0.3f,
                0.28f,
                2.8f,
                new Color(1f, 0.86f, 0.45f, 0.75f),
                1.2f);

            landingDust = CreateEffect(
                "Landing Dust",
                Vector3.zero,
                false,
                0f,
                24,
                0.65f,
                0.55f,
                3.4f,
                new Color(0.7f, 0.54f, 0.28f, 0.75f),
                0.35f);

            landingRing = CreateEffect(
                "Landing Shock Ring",
                new Vector3(0f, 0.15f, 0f),
                false,
                0f,
                30,
                0.5f,
                0.25f,
                5.5f,
                new Color(1f, 0.72f, 0.2f, 0.9f),
                0.15f);

            attackTelegraph = CreateEffect(
                "Bite Telegraph",
                enemy.HitboxOffset,
                false,
                0f,
                14,
                0.4f,
                0.55f,
                1.4f,
                new Color(1f, 0.25f, 0.08f, 0.9f),
                0.25f);

            biteSlash = CreateEffect(
                "Bite Slash",
                enemy.HitboxOffset,
                false,
                0f,
                16,
                0.3f,
                0.5f,
                4.5f,
                new Color(1f, 0.9f, 0.55f, 0.95f),
                0.2f);

            biteImpact = CreateEffect(
                "Bite Impact",
                enemy.HitboxOffset,
                false,
                0f,
                12,
                0.35f,
                0.45f,
                3.5f,
                new Color(1f, 0.35f, 0.08f, 0.9f),
                0.18f);

            takeoffDust = CreateEffect(
                "Takeoff Dust",
                Vector3.zero,
                false,
                0f,
                20,
                0.6f,
                0.5f,
                3f,
                new Color(0.78f, 0.65f, 0.4f, 0.7f),
                0.4f);

            deathBurst = CreateEffect(
                "Death Burst",
                new Vector3(0f, 2.8f, 0f),
                false,
                0f,
                36,
                1.1f,
                0.65f,
                5f,
                new Color(0.68f, 0.92f, 0.25f, 0.9f),
                0.6f);

            ConfigureDirectionalEffects();
            CreateWarningObject();
            SetFlying(false);
            SetDescending(false);
        }

        public void SetFlying(bool active)
        {
            SetLoopingEffect(wingMotes, active);
        }

        public void ShowDetection()
        {
            if (warningObject != null)
            {
                warningObject.SetActive(true);
            }

            PlayBurst(detectionFlash);
        }

        public void HideDetection()
        {
            if (warningObject != null)
            {
                warningObject.SetActive(false);
            }
        }

        public void SetDescending(bool active)
        {
            SetLoopingEffect(descentStreaks, active);
        }

        public void PlayLanding()
        {
            SetDescending(false);
            PlayBurst(landingDust);
            PlayBurst(landingRing);
        }

        public void PlayAttackTelegraph()
        {
            PlayBurst(attackTelegraph);
        }

        public void PlayBite(Vector2 localOffset)
        {
            if (biteSlash != null)
            {
                biteSlash.transform.localPosition = localOffset;
            }

            if (biteImpact != null)
            {
                biteImpact.transform.localPosition = localOffset;
            }

            PlayBurst(biteSlash);
            PlayBurst(biteImpact);
        }

        public void StopAttack()
        {
            StopAndClear(attackTelegraph);
        }

        public void PlayTakeoff()
        {
            PlayBurst(takeoffDust);
        }

        public void PlayDeath()
        {
            SetFlying(false);
            SetDescending(false);
            StopAttack();
            PlayBurst(deathBurst);
        }

        private ParticleSystem CreateEffect(
            string effectName,
            Vector3 localPosition,
            bool loop,
            float rate,
            int burstCount,
            float lifetime,
            float size,
            float speed,
            Color color,
            float radius)
        {
            GameObject effectObject = new GameObject(effectName);
            effectObject.layer = enemy.gameObject.layer;
            effectObject.transform.SetParent(root.transform, false);
            effectObject.transform.localPosition = localPosition;

            ParticleSystem effect = effectObject.AddComponent<ParticleSystem>();
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = effect.main;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.loop = loop;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSize = size * scaleCompensation;
            main.startSpeed = speed * scaleCompensation;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = loop ? 80 : Mathf.Max(16, burstCount * 2);

            ParticleSystem.EmissionModule emission = effect.emission;
            emission.enabled = true;
            emission.rateOverTime = loop ? rate : 0f;
            if (!loop && burstCount > 0)
            {
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, (short)burstCount)
                });
            }

            ParticleSystem.ShapeModule shape = effect.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * scaleCompensation;
            shape.radiusThickness = 0.2f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = effect.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GetParticleMaterial();
            renderer.sortingOrder = 40;

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return effect;
        }

        private void ConfigureDirectionalEffects()
        {
            if (descentStreaks != null)
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = descentStreaks.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.y = 4f * scaleCompensation;
            }

            if (landingRing != null)
            {
                ParticleSystem.ShapeModule shape = landingRing.shape;
                shape.radius = 0.05f * scaleCompensation;
                shape.radiusThickness = 1f;
            }

            if (biteSlash != null)
            {
                ParticleSystem.ShapeModule shape = biteSlash.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 18f;
                shape.radius = 0.2f;
                biteSlash.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            }
        }

        private void CreateWarningObject()
        {
            warningObject = new GameObject("Detection Warning");
            warningObject.layer = enemy.gameObject.layer;
            warningObject.transform.SetParent(root.transform, false);
            warningObject.transform.localPosition = new Vector3(0f, 7.2f, 0f);

            TextMesh text = warningObject.AddComponent<TextMesh>();
            text.text = "!";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 1.25f;
            text.color = new Color(1f, 0.82f, 0.08f, 1f);

            MeshRenderer renderer = warningObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = 60;
            }

            warningObject.SetActive(false);
        }

        private static void SetLoopingEffect(ParticleSystem effect, bool active)
        {
            if (effect == null)
            {
                return;
            }

            if (active)
            {
                if (!effect.isPlaying)
                {
                    effect.Play(true);
                }
            }
            else
            {
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void PlayBurst(ParticleSystem effect)
        {
            if (effect == null)
            {
                return;
            }

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Play(true);
        }

        private static void StopAndClear(ParticleSystem effect)
        {
            if (effect != null)
            {
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static Material GetParticleMaterial()
        {
            if (particleMaterial != null)
            {
                return particleMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            particleMaterial = new Material(shader);
            particleMaterial.name = "Bug Runtime Particle Material";
            particleMaterial.mainTexture = CreateParticleTexture();
            return particleMaterial;
        }

        private static Texture2D CreateParticleTexture()
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Bug Runtime Particle";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalizedDistance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(1f - normalizedDistance);
                    alpha *= alpha;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }
    }
}

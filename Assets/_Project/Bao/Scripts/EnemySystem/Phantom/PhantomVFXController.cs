using UnityEngine;

namespace Bao.Enemies
{
    [RequireComponent(typeof(PhantomEnemy))]
    public class PhantomVFXController : MonoBehaviour
    {
        [Header("Shader Setup")]
        [SerializeField] private Shader crystalShader;
        [SerializeField] private Color crystalColor = new Color(0.6f, 0.9f, 0.2f, 1f);
        [SerializeField] private float normalGlow = 1.2f;
        [SerializeField] private float attackGlow = 4.5f;

        [Header("Bone Trails (VFX)")]
        [SerializeField] private Material trailMaterial;
        [SerializeField] private Material particleMaterial;
        [SerializeField] private Color trailStartColor = new Color(0.6f, 0.9f, 0.2f, 0.8f);
        [SerializeField] private Color trailEndColor = new Color(0.1f, 0.3f, 0.1f, 0f);
        [SerializeField] private float trailWidth = 0.35f;
        [SerializeField] private float trailTime = 0.25f;
        [SerializeField] private AnimationCurve widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        private PhantomEnemy phantom;
        private SpriteRenderer[] renderers;
        private Material[] crystalMaterials;
        private float currentGlow;

        private TrailRenderer leftTrail;
        private TrailRenderer rightTrail;
        private ParticleSystem leftParticles;
        private ParticleSystem rightParticles;
        private static Texture2D cachedParticleTex;

        private void Awake()
        {
            phantom = GetComponent<PhantomEnemy>();
            renderers = phantom.GetVisualSpriteRenderers();
            
            if (crystalShader == null)
            {
                crystalShader = Shader.Find("Bao/CrystalPhantomShader");
            }

            SetupMaterials();
            currentGlow = normalGlow;
        }

        private void SetupMaterials()
        {
            if (crystalShader == null) return;

            crystalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = new Material(crystalShader);
                mat.SetColor("_GlowColor", crystalColor);
                mat.SetFloat("_GlowIntensity", normalGlow);
                
                if (renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", renderers[i].sharedMaterial.GetTexture("_MainTex"));
                }
                
                renderers[i].material = mat;
                crystalMaterials[i] = mat;
            }
        }

        private void Start()
        {
            if (phantom.LeftBone != null)
            {
                leftTrail = CreateTrail(phantom.LeftBone);
                leftParticles = CreateBoneParticles(phantom.LeftBone);
            }
            if (phantom.RightBone != null)
            {
                rightTrail = CreateTrail(phantom.RightBone);
                rightParticles = CreateBoneParticles(phantom.RightBone);
            }
        }

        private void Update()
        {
            bool isAttacking = phantom.StateMachine?.CurrentState is PhantomAttackState;
            float targetGlow = isAttacking ? attackGlow : normalGlow;
            
            currentGlow = Mathf.Lerp(currentGlow, targetGlow, Time.deltaTime * 6f);
            
            if (crystalMaterials != null)
            {
                foreach (var mat in crystalMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetFloat("_GlowIntensity", currentGlow);
                        mat.SetFloat("_ShimmerPower", isAttacking ? 1.8f : 0.8f);
                    }
                }
            }

            // Update VFX State
            if (leftTrail != null) leftTrail.emitting = isAttacking;
            if (rightTrail != null) rightTrail.emitting = isAttacking;
            
            if (leftParticles != null)
            {
                var em = leftParticles.emission;
                em.enabled = isAttacking;
            }
            if (rightParticles != null)
            {
                var em = rightParticles.emission;
                em.enabled = isAttacking;
            }
        }

        private TrailRenderer CreateTrail(Transform bone)
        {
            GameObject trailObj = new GameObject("BoneTrail");
            trailObj.transform.SetParent(bone, false);
            trailObj.transform.localPosition = Vector3.zero;

            TrailRenderer tr = trailObj.AddComponent<TrailRenderer>();
            tr.time = trailTime;
            
            tr.widthCurve = widthCurve;
            tr.widthMultiplier = trailWidth;
            
            if (trailMaterial != null)
            {
                tr.material = trailMaterial;
            }
            else
            {
                tr.material = new Material(Shader.Find("Sprites/Default"));
            }
            
            tr.startColor = trailStartColor;
            tr.endColor = trailEndColor;
            tr.emitting = false;
            tr.minVertexDistance = 0.02f;
            tr.sortingLayerName = "Default";
            tr.sortingOrder = 10;
            
            // Adding gradient to trail
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(trailStartColor, 0.0f), new GradientColorKey(trailEndColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            tr.colorGradient = grad;

            return tr;
        }

        private ParticleSystem CreateBoneParticles(Transform bone)
        {
            GameObject psObj = new GameObject("BoneParticles");
            psObj.transform.SetParent(bone, false);
            psObj.transform.localPosition = Vector3.zero;

            ParticleSystem ps = psObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = trailStartColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 150;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 35f;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(trailStartColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            
            if (particleMaterial != null)
            {
                renderer.material = particleMaterial;
            }
            else
            {
                Material mat = new Material(Shader.Find("Sprites/Default"));
                mat.mainTexture = GetCircleTexture();
                renderer.material = mat;
            }
            
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 11;

            ps.Play(true);
            return ps;
        }

        private Texture2D GetCircleTexture()
        {
            if (cachedParticleTex != null) return cachedParticleTex;
            
            cachedParticleTex = new Texture2D(32, 32, TextureFormat.ARGB32, false);
            Color transparent = new Color(1, 1, 1, 0);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                    if (dist < 15.5f)
                    {
                        float alpha = Mathf.SmoothStep(1f, 0f, dist / 15.5f);
                        cachedParticleTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                    }
                    else
                    {
                        cachedParticleTex.SetPixel(x, y, transparent);
                    }
                }
            }
            cachedParticleTex.Apply();
            return cachedParticleTex;
        }
    }
}

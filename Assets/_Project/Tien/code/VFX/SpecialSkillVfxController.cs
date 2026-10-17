using UnityEngine;

public sealed class SpecialSkillVfxController : MonoBehaviour
{
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Transform[] _transforms;
    private Vector3[] _localPositions;
    private Quaternion[] _localRotations;
    private Vector3[] _localScales;
    private ParticleSystem[] _particles;
    private ParticleSystem.MinMaxGradient[] _particleColors;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _propertyBlock;
    private bool _initialized;

    public void Play(bool useColor, Color color)
    {
        Initialize();
        RestoreChildTransforms();
        ApplyColor(useColor, color);
        RestartParticles();
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _transforms = GetComponentsInChildren<Transform>(true);
        _localPositions = new Vector3[_transforms.Length];
        _localRotations = new Quaternion[_transforms.Length];
        _localScales = new Vector3[_transforms.Length];
        for (int i = 0; i < _transforms.Length; i++)
        {
            _localPositions[i] = _transforms[i].localPosition;
            _localRotations[i] = _transforms[i].localRotation;
            _localScales[i] = _transforms[i].localScale;
        }

        _particles = GetComponentsInChildren<ParticleSystem>(true);
        _particleColors = new ParticleSystem.MinMaxGradient[_particles.Length];
        for (int i = 0; i < _particles.Length; i++)
        {
            _particleColors[i] = _particles[i].main.startColor;
        }

        _renderers = GetComponentsInChildren<Renderer>(true);
        _propertyBlock = new MaterialPropertyBlock();
        _initialized = true;
    }

    private void RestoreChildTransforms()
    {
        for (int i = 0; i < _transforms.Length; i++)
        {
            Transform target = _transforms[i];
            if (target == null || target == transform)
            {
                continue;
            }

            target.localPosition = _localPositions[i];
            target.localRotation = _localRotations[i];
            target.localScale = _localScales[i];
        }
    }

    private void ApplyColor(bool useColor, Color color)
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            ParticleSystem particle = _particles[i];
            if (particle == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particle.main;
            main.startColor = useColor ? new ParticleSystem.MinMaxGradient(color) : _particleColors[i];
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer renderer = _renderers[i];
            if (renderer == null)
            {
                continue;
            }

            _propertyBlock.Clear();
            renderer.SetPropertyBlock(_propertyBlock);
            if (!useColor)
            {
                continue;
            }

            _propertyBlock.Clear();
            _propertyBlock.SetColor(TintColorId, color);
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetColor(RimColorId, color);
            _propertyBlock.SetColor(EmissionColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void RestartParticles()
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            ParticleSystem particle = _particles[i];
            if (particle == null || !particle.gameObject.activeInHierarchy)
            {
                continue;
            }

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
        }
    }
}

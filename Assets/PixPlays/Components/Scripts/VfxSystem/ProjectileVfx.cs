using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace PixPlays.ElementalVFX
{
    public class ProjectileVfx : BaseVfx
    {
        [SerializeField] ParticleSystem _CastEffect;
        [SerializeField] ParticleSystem _HitEffect;
        [SerializeField] ParticleSystem _ProjectileEffect;
        [SerializeField] float _FlySpeed=1;
        [SerializeField] AnimationCurve _FlyCurve;
        [SerializeField] Vector2 _FlyCurveDirection;
        [SerializeField] bool _RandomizeFlyCurveDirection;
        [SerializeField] float _FlyCurveStrength;
        [SerializeField] float _ProjectileFlyDelay;
        [SerializeField] float _ProjectileDeactivateDelay;

        public Vector3 CurrentPosition => _ProjectileEffect != null ? _ProjectileEffect.transform.position : transform.position;
        public bool IsActive => _ProjectileEffect != null && _ProjectileEffect.gameObject.activeSelf;

        private bool _isBouncing = false;
        private Vector3 _bounceTarget;

        public void Bounce(Vector3 newTarget)
        {
            _isBouncing = true;
            _bounceTarget = newTarget;
        }
        public override void Play(VfxData data)
        {
            base.Play(data);
            StartCoroutine(Coroutine_Projectile());
        }

        IEnumerator Coroutine_Projectile()
        {
            _CastEffect.gameObject.SetActive(true);
            _CastEffect.transform.position = _data.Source;
            _CastEffect.transform.forward = (_data.Target - _data.Source);
            _CastEffect.Play();

            yield return new WaitForSeconds(_ProjectileFlyDelay);
            _ProjectileEffect.gameObject.SetActive(true);
            _ProjectileEffect.transform.position = _CastEffect.transform.position;
            _ProjectileEffect.Play();

            _FlyCurveDirection = _FlyCurveDirection.normalized;
            if (_RandomizeFlyCurveDirection)
            {
                _FlyCurveDirection = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
            }

            float lerp = 0;
            Vector3 startPos = _ProjectileEffect.transform.position;
            while (lerp < 1)
            {
                if (_isBouncing)
                {
                    _isBouncing = false;
                    startPos = _ProjectileEffect.transform.position;
                    _data = new VfxData(startPos, _bounceTarget, _data.Duration, _data.Radius);
                    lerp = 0;
                }

                Vector3 pos = Vector3.Lerp(startPos, _data.Target, lerp);
                pos += (Vector3)_FlyCurveDirection * _FlyCurve.Evaluate(lerp) * _FlyCurveStrength;
                if (lerp > 0)
                {
                    _ProjectileEffect.transform.forward = (pos - _ProjectileEffect.transform.position);
                }
                _ProjectileEffect.transform.position = pos;
                
                // Dùng Duration truyền từ VfxData (trong Projectile.cs), nếu không có thì dùng _FlySpeed
                float actualDuration = _data.Duration > 0 ? _data.Duration : _FlySpeed;
                lerp += Time.deltaTime / actualDuration;
                yield return null;
            }
            _HitEffect.transform.forward = (_ProjectileEffect.transform.position - _data.Target);

            _ProjectileEffect.transform.position = _data.Target;
            _ProjectileEffect.Stop();

            
            _HitEffect.transform.position = _data.Target;
            _HitEffect.gameObject.SetActive(true);
            _HitEffect.Play();

            yield return new WaitForSeconds(_ProjectileDeactivateDelay);
            _ProjectileEffect.gameObject.SetActive(false);
        }

        public override void Stop()
        {
            base.Stop();
            if (gameObject != null)
            {
                _HitEffect.Stop();
                _ProjectileEffect.Stop();
                _CastEffect.Stop();
            }
        }
    }
}

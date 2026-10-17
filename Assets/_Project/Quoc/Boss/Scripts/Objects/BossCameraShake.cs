using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace Quoc.Boss
{
    public class BossCameraShake : MonoBehaviour
    {
        [Header("Cinemachine Impulse Shake")]
        [SerializeField, Tooltip("CinemachineImpulseSource trên Boss")]
        private CinemachineImpulseSource impulseSource;

        [Header("Shake Settings")]
        [SerializeField, Tooltip("Lực rung mặc định khi húc")]
        private float defaultForce = 1.5f;

        [SerializeField, Tooltip("Thời gian rung camera mặc định (giây)")]
        private float shakeDuration = 1.0f;

        private Coroutine activeShakeCoroutine;

        private void Awake()
        {
            if (impulseSource == null)
            {
                impulseSource = GetComponent<CinemachineImpulseSource>();
                if (impulseSource == null)
                {
                    impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
                }
            }
        }

        public void TriggerShake(float force = -1f, float duration = 1.0f)
        {
            float impulseForce = force > 0f ? force : defaultForce;
            float dur = duration > 0f ? duration : shakeDuration;

            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse(Vector3.down * impulseForce);
            }

            if (activeShakeCoroutine != null)
            {
                StopCoroutine(activeShakeCoroutine);
            }

            activeShakeCoroutine = StartCoroutine(ShakeTimerRoutine(impulseForce, dur));
        }

        private IEnumerator ShakeTimerRoutine(float force, float duration)
        {
            CinemachineCamera cam = FindAnyObjectByType<CinemachineCamera>();
            if (cam != null)
            {
                var shake = cam.GetComponent<CinemachineTimelineShakeExtension>();
                if (shake == null)
                {
                    shake = cam.gameObject.AddComponent<CinemachineTimelineShakeExtension>();
                }

                if (shake != null)
                {
                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float t = 1f - Mathf.Clamp01(elapsed / duration);

                        shake.SetShake(0.35f * force * t, 2.2f * force * t, 25f);
                        yield return null;
                    }

                    shake.SetShake(0f, 0f, 0f);
                }
            }

            activeShakeCoroutine = null;
        }

        private void OnDisable()
        {
            if (activeShakeCoroutine != null)
            {
                StopCoroutine(activeShakeCoroutine);
                activeShakeCoroutine = null;
            }

            CinemachineCamera cam = FindAnyObjectByType<CinemachineCamera>();
            if (cam != null)
            {
                var shake = cam.GetComponent<CinemachineTimelineShakeExtension>();
                if (shake != null)
                {
                    shake.SetShake(0f, 0f, 0f);
                }
            }
        }
    }
}

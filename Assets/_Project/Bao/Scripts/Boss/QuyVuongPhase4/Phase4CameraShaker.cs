using System.Collections;
using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// Screen Shake Controller (Trauma-based) cho Boss Phase 4
    /// </summary>
    public class Phase4CameraShaker : MonoBehaviour
    {
        [Header("Shake Settings")]
        [SerializeField] private float maxShakeIntensity = 1f;
        [SerializeField] private bool useTrauma = true;
        [SerializeField] private float traumaDecay = 1.5f;

        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float currentTrauma = 0f;
        private bool isShaking = false;

        private void Awake()
        {
            if (Camera.main != null && Camera.main.transform != transform)
            {
                var mainCam = Camera.main.gameObject;
                if (mainCam.GetComponent<Phase4CameraShaker>() == null)
                {
                    mainCam.AddComponent<Phase4CameraShaker>();
                }
            }
        }

        private void Start()
        {
            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;
        }

        private void Update()
        {
            if (useTrauma && currentTrauma > 0f)
            {
                currentTrauma = Mathf.Max(0f, currentTrauma - traumaDecay * Time.deltaTime);

                if (currentTrauma <= 0.01f)
                {
                    currentTrauma = 0f;
                    ResetCamera();
                }
                else
                {
                    ApplyTraumaShake();
                }
            }
        }

        public void Shake(float intensity, float duration)
        {
            if (useTrauma)
            {
                AddTrauma(intensity);
            }
            else
            {
                if (!isShaking)
                {
                    StartCoroutine(ShakeRoutine(intensity, duration));
                }
            }
        }

        public void AddTrauma(float amount)
        {
            currentTrauma = Mathf.Clamp01(currentTrauma + amount);
        }

        private void ApplyTraumaShake()
        {
            float shake = currentTrauma * currentTrauma;

            float offsetX = maxShakeIntensity * shake * Random.Range(-1f, 1f);
            float offsetY = maxShakeIntensity * shake * Random.Range(-1f, 1f);
            float offsetZ = maxShakeIntensity * shake * Random.Range(-0.5f, 0.5f);

            transform.localPosition = originalPosition + new Vector3(offsetX, offsetY, 0f);

            float rotationZ = maxShakeIntensity * 2f * shake * Random.Range(-1f, 1f);
            transform.localRotation = originalRotation * Quaternion.Euler(0f, 0f, rotationZ);
        }

        private IEnumerator ShakeRoutine(float intensity, float duration)
        {
            isShaking = true;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - (elapsed / duration);

                float offsetX = Random.Range(-1f, 1f) * intensity * t;
                float offsetY = Random.Range(-1f, 1f) * intensity * t;

                transform.localPosition = originalPosition + new Vector3(offsetX, offsetY, 0f);

                yield return null;
            }

            ResetCamera();
            isShaking = false;
        }

        private void ResetCamera()
        {
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
        }

        private void OnDisable()
        {
            ResetCamera();
        }
    }
}

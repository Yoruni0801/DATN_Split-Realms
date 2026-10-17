using UnityEngine;
using System.Collections;

namespace Bao.Enemies
{
    /// <summary>
    /// Optional lightweight shake. Add to Main Camera if you don't already use Cinemachine shake/impulse.
    /// </summary>
    public class DusthornCameraShake2D : MonoBehaviour
    {
        [SerializeField] private bool enableShake = true;

        private Coroutine shakeRoutine;

        public void Shake(float duration, float strength)
        {
            if (!enableShake || !isActiveAndEnabled)
                return;

            if (shakeRoutine != null)
                StopCoroutine(shakeRoutine);

            shakeRoutine = StartCoroutine(ShakeRoutine(
                Mathf.Max(0.01f, duration),
                Mathf.Max(0f, strength)));
        }

        private IEnumerator ShakeRoutine(float duration, float strength)
        {
            Vector3 startPos = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float envelope = Mathf.Pow(1f - t, 2.2f);
                Vector2 offset = Random.insideUnitCircle * strength * envelope;

                transform.localPosition = startPos + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            transform.localPosition = startPos;
            shakeRoutine = null;
        }

        private void OnDisable()
        {
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                shakeRoutine = null;
            }
        }
    }
}

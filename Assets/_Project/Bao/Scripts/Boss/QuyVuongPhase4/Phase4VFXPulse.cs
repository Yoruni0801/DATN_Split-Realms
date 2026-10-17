using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    public class Phase4VFXPulse : MonoBehaviour
    {
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseAmount = 0.2f;
        [SerializeField] private AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Vector3 originalScale;

        private void Start()
        {
            originalScale = transform.localScale;
        }

        private void Update()
        {
            float pulse = pulseCurve.Evaluate(Mathf.PingPong(Time.time * pulseSpeed, 1f));
            float scale = 1f + (pulse * pulseAmount);
            transform.localScale = originalScale * scale;
        }
    }
}

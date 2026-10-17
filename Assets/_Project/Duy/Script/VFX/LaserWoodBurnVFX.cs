using UnityEngine;

namespace Rota.VFX
{
    /// <summary>
    /// Điều khiển hệ thống hạt 4 tầng (4-Layer VFX System) cho hiện tượng Laser khoan cháy cành cây:
    /// 1. Directional Sparks Jet: Tia lửa bắn ngược hình nón.
    /// 2. Volumetric Smoke: Khói đen cuộn bốc lên theo khí đối lưu.
    /// 3. Rising Ash & Embers: Tàn tro than hồng lơ lửng.
    /// 4. Molten Plasma Core: Lưỡi lửa nung chảy rực sáng bám theo đầu khoan.
    /// </summary>
    public class LaserWoodBurnVFX : MonoBehaviour
    {
        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem sparksJet;
        [SerializeField] private ParticleSystem smokeVolumetric;
        [SerializeField] private ParticleSystem risingEmbers;
        [SerializeField] private ParticleSystem moltenFlameCore;
        [SerializeField] private ParticleSystem breakthroughBurst;

        [Header("Dynamic Audio")]
        [SerializeField] private AudioSource loopAudioSource;

        private bool _isEmitting = false;
        private float _stopTimer = 0f;

        public void SetPositionAndDirection(Vector3 headPos, Vector3 laserDir)
        {
            transform.position = headPos;

            // Xoay tia lửa phản lực ngược lại hướng chiếu của tia laser
            Vector3 sparkDir = -laserDir;
            if (sparkDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(Vector3.forward, sparkDir);
            }
        }

        public void SetActiveState(bool active, float heatIntensity)
        {
            _isEmitting = active;

            if (active)
            {
                _stopTimer = 0f;

                if (sparksJet != null && !sparksJet.isPlaying) sparksJet.Play();
                if (smokeVolumetric != null && !smokeVolumetric.isPlaying) smokeVolumetric.Play();
                if (risingEmbers != null && !risingEmbers.isPlaying) risingEmbers.Play();
                if (moltenFlameCore != null && !moltenFlameCore.isPlaying) moltenFlameCore.Play();

                if (loopAudioSource != null && !loopAudioSource.isPlaying)
                {
                    loopAudioSource.volume = Mathf.Clamp01(heatIntensity);
                    loopAudioSource.Play();
                }
            }
            else
            {
                if (sparksJet != null && sparksJet.isPlaying) sparksJet.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (moltenFlameCore != null && moltenFlameCore.isPlaying) moltenFlameCore.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                if (loopAudioSource != null && loopAudioSource.isPlaying)
                {
                    loopAudioSource.volume = Mathf.Lerp(loopAudioSource.volume, 0f, Time.deltaTime * 5f);
                    if (loopAudioSource.volume <= 0.02f) loopAudioSource.Stop();
                }
            }
        }

        public void TriggerBreakthrough(Vector3 exitPoint, Vector3 laserDir)
        {
            if (breakthroughBurst != null)
            {
                breakthroughBurst.transform.position = exitPoint;
                breakthroughBurst.transform.rotation = Quaternion.LookRotation(Vector3.forward, laserDir);
                breakthroughBurst.Play();
            }
        }

        private void Update()
        {
            if (!_isEmitting)
            {
                _stopTimer += Time.deltaTime;
                // Nếu ngừng đốt quá 3s và không còn hạt, cho phép tự huỷ nếu là instance tạm
            }
        }
    }
}
using UnityEngine;

namespace Rota.VFX
{
    /// <summary>
    /// Điều khiển hiệu ứng bốc hơi nước (Steam), bọt nước sôi (Boiling Bubbles)
    /// và tia nước bắn tóe (Water Mist) khi tia laser xuyên qua làn nước.
    /// </summary>
    public class WaterLaserInteractionVFX : MonoBehaviour
    {
        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem steamVapor;
        [SerializeField] private ParticleSystem boilingBubbles;
        [SerializeField] private ParticleSystem mistSplash;
        [SerializeField] private ParticleSystem refractionLightGlow;

        [Header("Dynamic Audio")]
        [SerializeField] private AudioSource sizzlingAudioSource;
        [SerializeField] private AudioClip waterBoilSFX;

        private bool _isActive = false;

        public void SetPositionAndDirection(Vector3 hitPoint, Vector3 laserDir, Vector3 refractedDir)
        {
            transform.position = hitPoint;

            if (mistSplash != null && refractedDir.sqrMagnitude > 0.001f)
            {
                mistSplash.transform.rotation = Quaternion.LookRotation(Vector3.forward, refractedDir);
            }
        }

        public void SetActive(bool active, float intensity)
        {
            _isActive = active;

            if (active)
            {
                if (steamVapor != null && !steamVapor.isPlaying) steamVapor.Play();
                if (boilingBubbles != null && !boilingBubbles.isPlaying) boilingBubbles.Play();
                if (mistSplash != null && !mistSplash.isPlaying) mistSplash.Play();
                if (refractionLightGlow != null && !refractionLightGlow.isPlaying) refractionLightGlow.Play();

                if (sizzlingAudioSource != null)
                {
                    if (!sizzlingAudioSource.isPlaying)
                    {
                        if (waterBoilSFX != null && sizzlingAudioSource.clip != waterBoilSFX)
                        {
                            sizzlingAudioSource.clip = waterBoilSFX;
                            sizzlingAudioSource.loop = true;
                        }
                        sizzlingAudioSource.Play();
                    }
                    sizzlingAudioSource.volume = Mathf.Clamp01(intensity * 0.7f);
                }
            }
            else
            {
                if (steamVapor != null && steamVapor.isPlaying) steamVapor.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (boilingBubbles != null && boilingBubbles.isPlaying) boilingBubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (mistSplash != null && mistSplash.isPlaying) mistSplash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (refractionLightGlow != null && refractionLightGlow.isPlaying) refractionLightGlow.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                if (sizzlingAudioSource != null && sizzlingAudioSource.isPlaying)
                {
                    sizzlingAudioSource.volume = Mathf.Lerp(sizzlingAudioSource.volume, 0f, Time.deltaTime * 6f);
                    if (sizzlingAudioSource.volume <= 0.02f) sizzlingAudioSource.Stop();
                }
            }
        }
    }
}
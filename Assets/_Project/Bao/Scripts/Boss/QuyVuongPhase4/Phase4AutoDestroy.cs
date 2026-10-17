using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    public class Phase4AutoDestroy : MonoBehaviour
    {
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private bool useParticleSystemDuration = true;

        private void Start()
        {
            float destroyTime = lifetime;

            if (useParticleSystemDuration)
            {
                var ps = GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    destroyTime = main.duration + main.startLifetime.constantMax;
                }
            }

            Destroy(gameObject, destroyTime);
        }
    }
}

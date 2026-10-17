using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    public class Phase4VFXScaler : MonoBehaviour
    {
        [SerializeField] private float scaleMultiplier = 1f;
        [SerializeField] private bool scaleParticleSystem = true;

        public void SetScale(float scale)
        {
            transform.localScale = Vector3.one * scale * scaleMultiplier;

            if (scaleParticleSystem)
            {
                var ps = GetComponentsInChildren<ParticleSystem>();
                foreach (var p in ps)
                {
                    var main = p.main;
                    main.startSizeMultiplier *= scale;
                }
            }
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Bao.FX
{
    public sealed class DemonicMistAuraDemoDriver : MonoBehaviour
    {
        [SerializeField] private DemonicMistAuraController controller;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private float secondsPerPreset = 4.5f;

        private Vector3 baseScale;

        public void Configure(DemonicMistAuraController aura, Transform root)
        {
            controller = aura;
            presentationRoot = root;
        }

        private void Awake()
        {
            if (presentationRoot) baseScale = presentationRoot.localScale;
        }

        private IEnumerator Start()
        {
            if (!controller) yield break;
            controller.PlayIdle();
            while (true)
            {
                yield return new WaitForSeconds(secondsPerPreset);
                controller.PlayThreat();
                controller.PlayGather();
                yield return new WaitForSeconds(secondsPerPreset);
                controller.PlayCombat();
                controller.TriggerSurge(0.72f);
                yield return new WaitForSeconds(secondsPerPreset);
                controller.PlayCinematic();
                controller.PlayPhaseTransition();
                yield return new WaitForSeconds(secondsPerPreset);
                controller.PlayIdle();
            }
        }

        private void Update()
        {
            if (!presentationRoot) return;
            float breath = 1f + Mathf.Sin(Time.time * 1.35f) * 0.012f;
            presentationRoot.localScale = baseScale * breath;
        }
    }
}

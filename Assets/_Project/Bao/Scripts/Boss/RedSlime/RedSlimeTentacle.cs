using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bao.Bosses
{
    /// <summary>
    /// Rigged Bone Controller for Red Slime Boss Tentacles.
    /// Manages the multi-bone hierarchy (Bone_0 to Bone_4) and natural organic sway.
    /// </summary>
    [ExecuteAlways]
    public class RedSlimeTentacle : MonoBehaviour
    {
        [Header("Bone Chain Hierarchy")]
        [Tooltip("Root to Tip bone chain transforms (Bone_0 -> Bone_4)")]
        [SerializeField] private Transform[] bones;
        [SerializeField] private float segmentLength = 0.8f;

        [Header("Visual & Renderers")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Organic Idle Sway")]
        [SerializeField] private float swayFrequency = 2.2f;
        [SerializeField] private float swayAmplitude = 16f;
        [SerializeField] private float swayPhaseOffset = 0f;
        [SerializeField] private float idleCurlAngle = 10f;
        [SerializeField] private float restingBaseAngle = 45f;

        public Transform[] Bones => bones;
        public SpriteRenderer SpriteRenderer => spriteRenderer;

        private void Awake()
        {
            CacheBonesIfNeeded();
        }

        private void Start()
        {
            CacheBonesIfNeeded();
        }

        private void Update()
        {
            ApplyOrganicIdleSway();
        }

        public void SetRestingBaseAngle(float angle, float phaseOffset)
        {
            restingBaseAngle = angle;
            swayPhaseOffset = phaseOffset;
        }

        public void CacheBonesIfNeeded()
        {
            if (bones == null || bones.Length == 0)
            {
                List<Transform> list = new List<Transform>();
                Transform curr = transform.Find("Bone_0");
                while (curr != null)
                {
                    list.Add(curr);
                    curr = (curr.childCount > 0) ? curr.GetChild(0) : null;
                }
                bones = list.ToArray();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        private void ApplyOrganicIdleSway()
        {
            if (bones == null || bones.Length == 0) return;

            float time = Time.time * swayFrequency + swayPhaseOffset;

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;

                if (i == 0)
                {
                    float baseSway = Mathf.Sin(time) * (swayAmplitude * 0.4f);
                    bones[i].localRotation = Quaternion.Euler(0f, 0f, restingBaseAngle + baseSway);
                }
                else
                {
                    float progress = (float)i / bones.Length;
                    float wave = Mathf.Sin(time - (i * 0.7f)) * (swayAmplitude * (1f + progress * 0.8f));
                    float curl = idleCurlAngle * Mathf.Cos(time * 0.5f + i * 0.4f);

                    bones[i].localRotation = Quaternion.Euler(0f, 0f, wave + curl);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (bones == null || bones.Length == 0) return;

            Gizmos.color = Color.green;
            for (int i = 0; i < bones.Length - 1; i++)
            {
                if (bones[i] != null && bones[i + 1] != null)
                {
                    Gizmos.DrawLine(bones[i].position, bones[i + 1].position);
                    Gizmos.DrawWireSphere(bones[i].position, 0.06f);
                }
            }
            if (bones[bones.Length - 1] != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(bones[bones.Length - 1].position, 0.08f);
            }
        }
    }
}

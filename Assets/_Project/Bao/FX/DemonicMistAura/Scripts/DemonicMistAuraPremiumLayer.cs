using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bao.FX
{
    [DisallowMultipleComponent]
    public sealed class DemonicMistAuraPremiumLayer : MonoBehaviour
    {
        [Header("Anchors")]
        [SerializeField] private DemonicMistAttachment attachment;
        [SerializeField] private Transform coreVisual;
        [SerializeField] private Transform groundVisual;
        [Header("Rift Materials")]
        [SerializeField] private Material darkRiftMaterial;
        [SerializeField] private Material glowRiftMaterial;
        [ColorUsage(true, true)] [SerializeField] private Color darkRift = new(0.22f, 0.018f, 0.3f, 0.8f);
        [ColorUsage(true, true)] [SerializeField] private Color glowRift = new(1.5f, 0.16f, 2.2f, 1f);
        [SerializeField] private bool showRiftTendrils;
        [SerializeField, Range(3, 6)] private int tendrilCount = 4;
        [SerializeField] private int sortingOrder = 84;

        private LineRenderer[] darkLines;
        private LineRenderer[] glowLines;
        private float reveal = 0.38f;
        private float pulse;
        private Coroutine sequence;

        private void Awake()
        {
            if (attachment == null) attachment = GetComponent<DemonicMistAttachment>();
            BuildRifts();
        }

        private void LateUpdate()
        {
            UpdateVisuals();
        }

        public void SetStrength(float value)
        {
            reveal = Mathf.Clamp01(value);
        }

        public void PlayGather() => BeginSequence(GatherRoutine());
        public void PlayCompress() => BeginSequence(CompressRoutine());
        public void PlayRupture() => BeginSequence(RuptureRoutine());
        public void PlayPhaseTransition() => BeginSequence(PhaseRoutine());
        public void StopSequence()
        {
            if (sequence != null) StopCoroutine(sequence);
            sequence = null;
            pulse = 0;
        }

        private void BeginSequence(IEnumerator routine)
        {
            StopSequence();
            sequence = StartCoroutine(routine);
        }

        private IEnumerator GatherRoutine()
        {
            for (float t = 0; t < 0.65f; t += Time.deltaTime)
            {
                reveal = Mathf.Lerp(0.25f, 0.9f, Smooth(t / 0.65f));
                pulse = Mathf.Sin(t * 9f) * 0.22f;
                yield return null;
            }
        }

        private IEnumerator CompressRoutine()
        {
            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                reveal = Mathf.Lerp(reveal, 1f, Smooth(t / 0.35f));
                pulse = 0.35f + Mathf.Sin(t * 22f) * 0.22f;
                yield return null;
            }
        }

        private IEnumerator RuptureRoutine()
        {
            pulse = 1f;
            reveal = 1f;
            yield return new WaitForSeconds(0.1f);
            for (float t = 0; t < 0.65f; t += Time.deltaTime)
            {
                pulse = Mathf.Lerp(1f, 0f, t / 0.65f);
                reveal = Mathf.Lerp(1f, 0.45f, t / 0.65f);
                yield return null;
            }
            pulse = 0;
        }

        private IEnumerator PhaseRoutine()
        {
            yield return GatherRoutine();
            yield return CompressRoutine();
            yield return RuptureRoutine();
        }

        private void BuildRifts()
        {
            if (!showRiftTendrils || darkRiftMaterial == null || glowRiftMaterial == null)
                return;
            darkLines = new LineRenderer[tendrilCount];
            glowLines = new LineRenderer[tendrilCount];
            for (int i = 0; i < tendrilCount; i++)
            {
                darkLines[i] = CreateLine("RiftTendril_Dark_" + i, darkRiftMaterial, 0.13f, 0.025f, sortingOrder);
                glowLines[i] = CreateLine("RiftTendril_Glow_" + i, glowRiftMaterial, 0.038f, 0.006f, sortingOrder + 1);
            }
        }

        private LineRenderer CreateLine(string objectName, Material material, float startWidth, float endWidth, int order)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 3;
            line.numCapVertices = 2;
            line.sharedMaterial = material;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.sortingOrder = order;
            return line;
        }

        private void UpdateVisuals()
        {
            if (attachment == null || darkLines == null)
                return;
            Vector3 core = attachment.CorePosition;
            Vector3 ground = attachment.GroundPosition;
            float radius = attachment.Radius;
            if (coreVisual != null)
            {
                coreVisual.position = core;
                float breath = 1f + Mathf.Sin(Time.time * 2.2f) * 0.08f + pulse * 0.22f;
                coreVisual.localScale = Vector3.one * radius * breath * 1.8f;
            }
            if (groundVisual != null) groundVisual.position = ground;
            for (int i = 0; i < darkLines.Length; i++)
            {
                float angle = Mathf.Lerp(-2.65f, -0.45f, i / (float)Mathf.Max(1, darkLines.Length - 1));
                Vector3 outer = ground + new Vector3(Mathf.Cos(angle) * radius * 1.55f, Mathf.Sin(angle) * radius * 0.28f, 0);
                Vector3 controlA = Vector3.Lerp(outer, core, .35f) + new Vector3(0, Mathf.Sin(Time.time * 1.4f + i) * radius * .18f, 0);
                Vector3 controlB = Vector3.Lerp(outer, core, .72f) + new Vector3(Mathf.Sin(Time.time * 1.9f + i * 2.1f) * radius * .22f, 0, 0);
                int visible = Mathf.Clamp(Mathf.CeilToInt(reveal * 14), 0, 14);
                SetPath(darkLines[i], outer, controlA, controlB, core, visible, darkRift, 0.55f + pulse * .4f);
                SetPath(glowLines[i], outer, controlA, controlB, core, Mathf.Max(0, visible - 1), glowRift, 0.7f + pulse * .3f);
            }
        }

        private static void SetPath(LineRenderer line, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int count, Color color, float alpha)
        {
            if (count < 2) { line.positionCount = 0; return; }
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float t = i / 13f;
                float u = 1f - t;
                line.SetPosition(i, u*u*u*p0 + 3f*u*u*t*p1 + 3f*u*t*t*p2 + t*t*t*p3);
            }
            line.startColor = new Color(color.r, color.g, color.b, alpha * color.a);
            line.endColor = new Color(color.r, color.g, color.b, alpha * color.a * .06f);
        }

        private static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }
    }
}

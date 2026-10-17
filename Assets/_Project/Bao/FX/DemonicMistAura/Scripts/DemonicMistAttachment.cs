using System.Collections.Generic;
using UnityEngine;

namespace Bao.FX
{
    public enum DemonicMistAttachmentMode
    {
        ManualAnchors,
        FollowTransform,
        BoundsFitted,
        SurfacePossession
    }

    [DisallowMultipleComponent]
    public sealed class DemonicMistAttachment : MonoBehaviour
    {
        [SerializeField] private DemonicMistAttachmentMode mode = DemonicMistAttachmentMode.BoundsFitted;
        [SerializeField] private Transform target;
        [SerializeField] private Transform coreAnchor;
        [SerializeField] private Transform groundAnchor;
        [SerializeField] private Vector3 coreOffset;
        [SerializeField] private Vector3 groundOffset;
        [SerializeField] private bool followRotation;
        [SerializeField] private bool followScale = true;

        private readonly List<Renderer> cachedRenderers = new();
        private readonly List<Collider2D> cachedColliders = new();
        private Bounds bounds;
        private bool hasBounds;

        public Transform Target => target;
        public float Radius => hasBounds ? Mathf.Max(0.35f, Mathf.Max(bounds.extents.x, bounds.extents.y)) : 1f;
        public Vector3 CorePosition => ResolveCorePosition();
        public Vector3 GroundPosition => ResolveGroundPosition();

        public void AttachTo(Transform newTarget, DemonicMistAttachmentMode newMode = DemonicMistAttachmentMode.BoundsFitted)
        {
            target = newTarget;
            mode = newMode;
            CacheTarget();
            UpdateAttachment();
        }

        public void SetAnchors(Transform core, Transform ground)
        {
            coreAnchor = core;
            groundAnchor = ground;
            mode = DemonicMistAttachmentMode.ManualAnchors;
        }

        public void ClearTarget()
        {
            target = null;
            cachedRenderers.Clear();
            cachedColliders.Clear();
            hasBounds = false;
        }

        private void LateUpdate() => UpdateAttachment();

        public void UpdateAttachment()
        {
            if (target == null)
                return;

            if (mode == DemonicMistAttachmentMode.FollowTransform || mode == DemonicMistAttachmentMode.ManualAnchors)
            {
                transform.position = target.position;
                if (followRotation)
                    transform.rotation = target.rotation;
                if (followScale)
                    transform.localScale = Vector3.one;
                return;
            }

            RefreshBounds();
            if (!hasBounds)
                return;

            transform.position = bounds.center;
            if (followRotation)
                transform.rotation = target.rotation;
            transform.localScale = Vector3.one;
        }

        private Vector3 ResolveCorePosition()
        {
            if (coreAnchor != null)
                return coreAnchor.position + coreOffset;
            if (hasBounds)
                return bounds.center + coreOffset;
            return target != null ? target.position + coreOffset : transform.position + coreOffset;
        }

        private Vector3 ResolveGroundPosition()
        {
            if (groundAnchor != null)
                return groundAnchor.position + groundOffset;
            if (hasBounds)
                return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + groundOffset;
            return target != null ? target.position + groundOffset : transform.position + groundOffset;
        }

        private void CacheTarget()
        {
            cachedRenderers.Clear();
            cachedColliders.Clear();
            if (target == null)
                return;
            cachedRenderers.AddRange(target.GetComponentsInChildren<Renderer>(true));
            cachedColliders.AddRange(target.GetComponentsInChildren<Collider2D>(true));
            RefreshBounds();
        }

        private void RefreshBounds()
        {
            hasBounds = false;
            for (int i = cachedRenderers.Count - 1; i >= 0; i--)
            {
                Renderer renderer = cachedRenderers[i];
                if (renderer == null) { cachedRenderers.RemoveAt(i); continue; }
                Encapsulate(renderer.bounds);
            }
            for (int i = cachedColliders.Count - 1; i >= 0; i--)
            {
                Collider2D collider = cachedColliders[i];
                if (collider == null) { cachedColliders.RemoveAt(i); continue; }
                Encapsulate(collider.bounds);
            }
            if (!hasBounds && target != null)
            {
                bounds = new Bounds(target.position, Vector3.one);
                hasBounds = true;
            }
        }

        private void Encapsulate(Bounds candidate)
        {
            if (!hasBounds) { bounds = candidate; hasBounds = true; }
            else bounds.Encapsulate(candidate);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Bao.BossGate
{
    /// <summary>
    /// The TheForest timeline fades the (hidden) root SpriteRenderer's alpha while the
    /// gate materializes. The visible PortalV2 shaders and VFX particles don't read that
    /// value, so without this bridge the gate pops in fully formed. Each frame this
    /// copies the root sprite alpha into "_Alpha" on the portal materials and
    /// "_GlobalAlpha" on the particle materials via property blocks (shared materials
    /// stay untouched).
    /// </summary>
    [DisallowMultipleComponent]
    public class BossGateTimelineFade : MonoBehaviour
    {
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int GlobalAlphaId = Shader.PropertyToID("_GlobalAlpha");

        private struct Entry
        {
            public Renderer Renderer;
            public MaterialPropertyBlock Block;
            public int PropertyId;
        }

        private SpriteRenderer _root;
        private readonly List<Entry> _entries = new List<Entry>();
        private float _lastAlpha = -1f;

        private void Awake()
        {
            _root = GetComponent<SpriteRenderer>();

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject == gameObject) continue;
                var mat = r.sharedMaterial;
                if (mat == null) continue;

                int propertyId;
                if (mat.HasProperty(GlobalAlphaId)) propertyId = GlobalAlphaId;
                else if (mat.HasProperty(AlphaId)) propertyId = AlphaId;
                else continue;

                _entries.Add(new Entry
                {
                    Renderer = r,
                    Block = new MaterialPropertyBlock(),
                    PropertyId = propertyId
                });
            }
        }

        private void OnEnable()
        {
            _lastAlpha = -1f;
        }

        private void LateUpdate()
        {
            float alpha = _root != null ? _root.color.a : 1f;
            if (Mathf.Approximately(alpha, _lastAlpha)) return;
            _lastAlpha = alpha;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Renderer == null) continue;
                entry.Renderer.GetPropertyBlock(entry.Block);
                entry.Block.SetFloat(entry.PropertyId, alpha);
                entry.Renderer.SetPropertyBlock(entry.Block);
            }
        }
    }
}

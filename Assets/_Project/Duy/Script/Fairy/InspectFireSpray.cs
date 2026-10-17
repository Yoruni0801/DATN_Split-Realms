using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class InspectFireSpray : MonoBehaviour
{
    private void Start()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null) return;

        var col = ps.collision;
        Debug.Log($"[InspectFireSpray] collision.enabled={col.enabled} | sendCollisionMessages={col.sendCollisionMessages} | collidesWith={(LayerMask)col.collidesWith.value} | quality={col.quality} | type={col.type}");

        var go = gameObject;
        Debug.Log($"[InspectFireSpray] FireSpray layer={LayerMask.LayerToName(go.layer)}({go.layer}) | active={go.activeInHierarchy} | pos={go.transform.position} | scale={go.transform.localScale}");

        var fairies = FairyPlayerCompanionController.AllActiveFairies;
        for (int i = 0; i < fairies.Count; i++)
        {
            var f = fairies[i];
            if (f == null) continue;
            var cc = f.GetComponent<CircleCollider2D>();
            Debug.Log($"[InspectFireSpray] Fairy '{f.name}' element={f.CurrentElement} layer={LayerMask.LayerToName(f.gameObject.layer)}({f.gameObject.layer}) pos={f.transform.position} collider={cc != null} isTrigger={(cc != null && cc.isTrigger)} radius={(cc != null ? cc.radius : 0f)}");
        }
    }
}

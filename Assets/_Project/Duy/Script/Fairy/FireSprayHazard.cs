using System.Collections.Generic;
using UnityEngine;
using Rota.VFX;

[DisallowMultipleComponent]
public class FireSprayHazard : MonoBehaviour
{
    [SerializeField] private FairyElement fatalToElement = FairyElement.Water;

    [Header("Hit Detection")]
    [SerializeField] private Vector2 hitBoxOffset = Vector2.zero;
    [SerializeField] private Vector2 hitBoxSize = new Vector2(2f, 2f);
    [SerializeField] private Vector2 blockedHitBoxSize = new Vector2(0.5f, 0.5f);
    [SerializeField] private FairyElement blockingElement = FairyElement.Fire;

    [Header("Respawn & Environment Reset")]
    [SerializeField] private Transform customRespawnPoint;
    [SerializeField] private RedirectableBeamLauncher beamToStop;
    [SerializeField] private List<BurnDissolveController> burnControllersToReset = new List<BurnDissolveController>();

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;
    private Vector2 defaultHitBoxSize;
    private Vector2 currentHitBoxSize;
    private bool isCurrentlyBlocked;

    public Vector2 HitBoxOffset => hitBoxOffset;
    public Vector2 CurrentHitBoxSize => currentHitBoxSize;
    public bool IsCurrentlyBlocked => isCurrentlyBlocked;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps == null) ps = GetComponentInChildren<ParticleSystem>();
        defaultHitBoxSize = hitBoxSize;
        currentHitBoxSize = defaultHitBoxSize;
    }

    private void Update()
    {
        if (ps == null || !ps.isPlaying) return;

        var fairies = FairyPlayerCompanionController.AllActiveFairies;
        if (fairies.Count == 0) return;

        int count = ps.particleCount;
        if (count == 0) return;

        if (particles == null || particles.Length < count)
            particles = new ParticleSystem.Particle[count];
        ps.GetParticles(particles);

        bool isLocal = ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;

        // 1. Kiểm tra tinh linh Lửa có đang chắn trong luồng lửa không (AABB)
        isCurrentlyBlocked = false;
        Vector2 defaultHalf = defaultHitBoxSize * 0.5f;

        for (int i = 0; i < fairies.Count; i++)
        {
            var fairy = fairies[i];
            if (fairy == null || !fairy.gameObject.activeInHierarchy || fairy.CurrentElement != blockingElement) continue;

            Vector2 fairyPos = fairy.transform.position;
            for (int p = 0; p < count; p++)
            {
                Vector3 pPos = particles[p].position;
                if (isLocal) pPos = transform.TransformPoint(pPos);
                Vector2 boxCenter = new Vector2(pPos.x + hitBoxOffset.x, pPos.y + hitBoxOffset.y);

                if (Mathf.Abs(fairyPos.x - boxCenter.x) <= defaultHalf.x && Mathf.Abs(fairyPos.y - boxCenter.y) <= defaultHalf.y)
                {
                    isCurrentlyBlocked = true;
                    break;
                }
            }
            if (isCurrentlyBlocked) break;
        }

        // 2. Cập nhật kích thước box hiện tại
        currentHitBoxSize = isCurrentlyBlocked ? blockedHitBoxSize : defaultHitBoxSize;
        Vector2 currentHalf = currentHitBoxSize * 0.5f;

        // 3. Kiểm tra va chạm box gây chết cho tinh linh kỵ hệ (Nước)
        for (int i = 0; i < fairies.Count; i++)
        {
            var fairy = fairies[i];
            if (fairy == null || !fairy.gameObject.activeInHierarchy || fairy.CurrentElement != fatalToElement) continue;

            Vector2 fairyPos = fairy.transform.position;

            for (int p = 0; p < count; p++)
            {
                Vector3 pPos = particles[p].position;
                if (isLocal) pPos = transform.TransformPoint(pPos);
                Vector2 boxCenter = new Vector2(pPos.x + hitBoxOffset.x, pPos.y + hitBoxOffset.y);

                if (Mathf.Abs(fairyPos.x - boxCenter.x) <= currentHalf.x && Mathf.Abs(fairyPos.y - boxCenter.y) <= currentHalf.y)
                {
                    Debug.Log($"[FireSprayHazard] {fairy.name} chạm lửa! fairy={fairyPos} boxCenter={boxCenter} (Box: {currentHitBoxSize})");
                    KillFairy(fairy);
                    return; // 1 lần chết là đủ
                }
            }
        }
    }

    public void KillFairy(FairyPlayerCompanionController killedFairy)
    {
        Debug.Log($"[FireSprayHazard] Tinh linh [{killedFairy.CurrentElement}] ({killedFairy.name}) bị ngọn lửa thiêu rụi!");

        if (beamToStop != null) beamToStop.StopBeam();

        if (burnControllersToReset != null)
            for (int i = 0; i < burnControllersToReset.Count; i++)
                if (burnControllersToReset[i] != null) burnControllersToReset[i].ResetBurn();

        var fairies = FairyPlayerCompanionController.AllActiveFairies;
        for (int i = 0; i < fairies.Count; i++)
        {
            var f = fairies[i];
            if (f == null || !f.gameObject.activeInHierarchy) continue;

            // Chỉ kéo về / hồi sinh những tinh linh có FairyPossessionController
            if (f.GetComponent<FairyPossessionController>() == null) continue;

            if (customRespawnPoint != null)
                f.DieWithCustomRespawn(customRespawnPoint.position);
            else
                f.Die();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 1. Vẽ vùng hazard tại emitter/transform
        Vector2 size = Application.isPlaying ? currentHitBoxSize : hitBoxSize;
        Vector3 originBoxCenter = transform.position + new Vector3(hitBoxOffset.x, hitBoxOffset.y, 0f);
        Gizmos.color = isCurrentlyBlocked ? new Color(0f, 1f, 0.5f, 0.4f) : new Color(1f, 0.3f, 0f, 0.3f);
        Gizmos.DrawWireCube(originBoxCenter, new Vector3(size.x, size.y, 0f));

        // 2. Vẽ custom respawn point nếu có
        if (customRespawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(customRespawnPoint.position, 0.4f);
            Gizmos.DrawLine(transform.position, customRespawnPoint.position);
        }

        // 3. Vẽ liên kết tới BeamLauncher và BurnControllers
        if (beamToStop != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, beamToStop.transform.position);
        }
        if (burnControllersToReset != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            for (int i = 0; i < burnControllersToReset.Count; i++)
            {
                if (burnControllersToReset[i] != null)
                    Gizmos.DrawLine(transform.position, burnControllersToReset[i].transform.position);
            }
        }

        // 4. Vẽ vùng hit box thực tế quanh từng hạt lửa khi Play mode
        if (!Application.isPlaying) return;
        var psComp = GetComponent<ParticleSystem>();
        if (psComp == null) psComp = GetComponentInChildren<ParticleSystem>();
        if (psComp == null || psComp.particleCount == 0) return;

        var parts = new ParticleSystem.Particle[psComp.particleCount];
        psComp.GetParticles(parts);
        bool isLocal = psComp.main.simulationSpace == ParticleSystemSimulationSpace.Local;

        for (int i = 0; i < parts.Length; i++)
        {
            Vector3 pos = parts[i].position;
            if (isLocal) pos = transform.TransformPoint(pos);
            Vector3 boxCenter = pos + new Vector3(hitBoxOffset.x, hitBoxOffset.y, 0f);

            // Vẽ khối bao quanh từng particle theo offset + size
            Gizmos.color = isCurrentlyBlocked ? new Color(0f, 1f, 0.5f, 0.2f) : new Color(1f, 0.2f, 0f, 0.25f);
            Gizmos.DrawWireCube(boxCenter, new Vector3(size.x, size.y, 0f));

            // Vẽ tâm hạt gốc
            Gizmos.color = isCurrentlyBlocked ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(new Vector3(pos.x, pos.y, 0f), 0.08f);

            // Vẽ line nối từ tâm hạt tới tâm hitbox nếu có offset
            if (hitBoxOffset != Vector2.zero)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
                Gizmos.DrawLine(pos, boxCenter);
            }
        }
    }
#endif
}

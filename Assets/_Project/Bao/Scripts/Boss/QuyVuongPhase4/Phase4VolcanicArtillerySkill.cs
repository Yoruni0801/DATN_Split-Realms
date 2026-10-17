using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// SKILL TẦM XA: Hỏa Nham Trận & Bộc Phá Địa Chấn (Phase 4 - Hỏa Nham Ma)
    /// Giai đoạn 1: Bắn gai lưng cắm bẫy Spire quanh sàn đấu (Gai gây sát thương khi bay và khi chạm đất)
    /// Giai đoạn 2: Cọc gai Magma Spire trồi lên cắm bẫy sàn đấu
    /// Giai đoạn 3: Cú đấm địa chấn (Tectonic Ground Punch) truyền sóng xung kích kích nổ dây chuyền các cọc gai
    /// </summary>
    public class Phase4VolcanicArtillerySkill : QuyVuongPhase4SkillBase
    {
        [Header("=== GIAI ĐOẠN 1: Volcanic Spine Mortar ===")]
        [SerializeField] private int spineCount = 4;
        [SerializeField] private int spineDamage = 18;
        [SerializeField] private float spineKnockback = 2f;
        [SerializeField] private float spineWindupTime = 0.6f;
        [SerializeField] private GameObject spineMortarPrefab;
        [SerializeField] private float mortarArcHeight = 7f;
        [SerializeField] private float mortarFlightTime = 1f;

        [Header("=== GIAI ĐOẠN 2: Magma Spire Grid ===")]
        [SerializeField] private GameObject magmaSpirePrefab;
        [SerializeField] private float spireSlowRadius = 1.5f;
        [SerializeField] private float spireSlowAmount = 0.2f;
        [SerializeField] private GameObject spireAuraPrefab;

        [Header("=== GIAI ĐOẠN 3: Tectonic Ground Punch & Chain Detonation ===")]
        [SerializeField] private float punchWindupTime = 0.5f;
        [SerializeField] private int punchDamage = 25;
        [SerializeField] private float punchRadius = 3f;
        [SerializeField] private GameObject punchImpactVFXPrefab;
        [SerializeField] private float chainExplosionDelay = 0.15f;

        [Header("=== Chain Explosion Settings ===")]
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private int explosionDamage = 30;

        [Header("VFX & SFX")]
        [SerializeField] private GameObject punchChargeVFXPrefab;
        [SerializeField] private AudioClip spineFireSFX;
        [SerializeField] private AudioClip spireImpactSFX;
        [SerializeField] private AudioClip punchChargeSFX;
        [SerializeField] private AudioClip punchImpactSFX;
        [SerializeField] private AudioClip explosionSFX;

        [Header("Camera Shake Settings")]
        [SerializeField] private float punchShakeIntensity = 0.5f;
        [SerializeField] private float explosionShakeIntensity = 0.45f;

        private readonly List<GameObject> spawnedSpires = new List<GameObject>();

        protected override void Awake()
        {
            base.Awake();
            skillName = "Phase 4 - Volcanic Artillery & Seismic Detonation";
        }

        public override IEnumerator ExecuteSkill(Transform playerTransform)
        {
            if (playerTransform == null) yield break;

            DebugLog("=== BẮT ĐẦU COMBO RANGED: HỎA NHAM TRẬN & ĐỊA CHẤN BỘC PHÁ (PHASE 4) ===");
            ResolveReferences();
            FlipTowardsPlayer(playerTransform);

            // Tính toán vị trí cọc gai cố định cho toàn bộ combo
            List<Vector3> targetPositions = CalculateSpireTargetPositions(playerTransform);

            // GIAI ĐOẠN 1: Volcanic Spine Mortar (Bắn gai lưng gây sát thương)
            yield return StartCoroutine(Phase1_VolcanicSpineMortar(targetPositions));

            // GIAI ĐOẠN 2: Magma Spire Grid (Cọc Spire cắm đất tạo bẫy)
            yield return StartCoroutine(Phase2_MagmaSpireGrid(targetPositions));

            // GIAI ĐOẠN 3: Tectonic Ground Punch & Chain Detonation (Nện sàn truyền địa chấn kích nổ chuỗi cọc gai)
            yield return StartCoroutine(Phase3_TectonicPunchAndChainDetonation(playerTransform));

            DebugLog("=== KẾT THÚC COMBO RANGED (PHASE 4) ===");
        }

        private IEnumerator Phase1_VolcanicSpineMortar(List<Vector3> targetPositions)
        {
            DebugLog("Giai đoạn 1: Volcanic Spine Mortar");

            PlayAttackAnimation();

            yield return new WaitForSeconds(spineWindupTime);

            if (spineFireSFX != null)
            {
                AudioSource.PlayClipAtPoint(spineFireSFX, transform.position);
            }

            Vector3 spineOrigin = transform.position + Vector3.up * 2f;
            for (int i = 0; i < spineCount; i++)
            {
                if (i < targetPositions.Count)
                {
                    Vector3 targetPos = targetPositions[i];
                    GameObject spine;
                    if (spineMortarPrefab != null)
                    {
                        spine = Instantiate(spineMortarPrefab, spineOrigin, Quaternion.identity);
                    }
                    else
                    {
                        spine = new GameObject("Phase4_Runtime_SpineMortar");
                        spine.transform.position = spineOrigin;
                    }

                    var projectile = spine.GetComponent<Phase4SpineMortarProjectile>();
                    if (projectile == null)
                    {
                        projectile = spine.AddComponent<Phase4SpineMortarProjectile>();
                    }
                    projectile.Initialize(targetPos, mortarArcHeight, mortarFlightTime, spineDamage, spineKnockback);
                }
            }

            yield return new WaitForSeconds(mortarFlightTime);
        }

        private IEnumerator Phase2_MagmaSpireGrid(List<Vector3> targetPositions)
        {
            DebugLog("Giai đoạn 2: Magma Spire Grid");

            for (int i = 0; i < targetPositions.Count; i++)
            {
                Vector3 targetPos = targetPositions[i];
                GameObject spire;
                if (magmaSpirePrefab != null)
                {
                    spire = Instantiate(magmaSpirePrefab, targetPos, Quaternion.identity);
                }
                else
                {
                    spire = new GameObject("Phase4_Runtime_MagmaSpire");
                    spire.transform.position = targetPos;
                    var col = spire.AddComponent<BoxCollider2D>();
                    col.isTrigger = true;
                    col.size = new Vector2(1f, 2.2f);
                }
                spawnedSpires.Add(spire);

                var spireComponent = spire.GetComponent<Phase4MagmaSpire>();
                if (spireComponent == null)
                {
                    spireComponent = spire.AddComponent<Phase4MagmaSpire>();
                }
                spireComponent.Initialize(spireSlowRadius, spireSlowAmount, spireAuraPrefab);

                if (spireImpactSFX != null)
                {
                    AudioSource.PlayClipAtPoint(spireImpactSFX, targetPos);
                }

                yield return new WaitForSeconds(0.08f);
            }

            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator Phase3_TectonicPunchAndChainDetonation(Transform playerTransform)
        {
            DebugLog("Giai đoạn 3: Tectonic Ground Punch & Chain Detonation");

            // Telegraph tụ lửa vào nắm đấm
            Vector3 punchOrigin = MeleePoint != null ? MeleePoint.position : transform.position;
            GameObject chargeVFX = null;
            if (punchChargeVFXPrefab != null)
            {
                chargeVFX = Instantiate(punchChargeVFXPrefab, punchOrigin, Quaternion.identity, transform);
            }

            if (punchChargeSFX != null)
            {
                AudioSource.PlayClipAtPoint(punchChargeSFX, transform.position);
            }

            SetBossColor(new Color(1f, 0.4f, 0.1f));
            yield return new WaitForSeconds(punchWindupTime);

            if (chargeVFX != null) Destroy(chargeVFX);
            SetBossColor(Color.white);

            // Nện nắm đấm xuống sàn đấu
            PlayAttackAnimation();
            Vector3 impactPos = new Vector3(punchOrigin.x, transform.position.y - 0.2f, punchOrigin.z);

            if (punchImpactVFXPrefab != null)
            {
                SpawnVFX(punchImpactVFXPrefab, impactPos, Quaternion.identity, 2f);
            }

            if (punchImpactSFX != null)
            {
                AudioSource.PlayClipAtPoint(punchImpactSFX, transform.position);
            }

            TriggerCameraShake(punchShakeIntensity, 0.35f);
            StartCoroutine(Hitstop(0.08f));

            // Sát thương cận chiến vùng đấm
            Collider2D[] meleeHits = Physics2D.OverlapCircleAll(impactPos, punchRadius);
            foreach (var hit in meleeHits)
            {
                if (hit != null && hit.CompareTag("Player"))
                {
                    QuyVuongPhase4SkillBase.ApplyPlayerDamage(hit.gameObject, punchDamage, 5f);
                }
            }

            // Kích nổ dây chuyền các cọc Magma Spire từ gần ra xa
            yield return StartCoroutine(DetonateSpiresInSequence(impactPos));

            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator DetonateSpiresInSequence(Vector3 originPos)
        {
            // Sắp xếp các cọc theo khoảng cách từ điểm nện sàn
            spawnedSpires.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                float distA = Vector3.Distance(originPos, a.transform.position);
                float distB = Vector3.Distance(originPos, b.transform.position);
                return distA.CompareTo(distB);
            });

            List<GameObject> spiresToExplode = new List<GameObject>(spawnedSpires);
            spawnedSpires.Clear();

            for (int i = 0; i < spiresToExplode.Count; i++)
            {
                var spire = spiresToExplode[i];
                if (spire != null)
                {
                    ExplodeSpire(spire);
                    yield return new WaitForSeconds(chainExplosionDelay);
                }
            }
        }

        private void ExplodeSpire(GameObject spire)
        {
            if (spire == null) return;

            Vector3 position = spire.transform.position;
            DebugLog($"Địa chấn kích nổ Magma Spire tại {position}!");

            if (explosionSFX != null)
            {
                AudioSource.PlayClipAtPoint(explosionSFX, position);
            }

            TriggerCameraShake(explosionShakeIntensity, 0.22f);

            // Sát thương nổ lan
            Collider2D[] hits = Physics2D.OverlapCircleAll(position, explosionRadius);
            foreach (var hit in hits)
            {
                if (hit != null && hit.CompareTag("Player"))
                {
                    QuyVuongPhase4SkillBase.ApplyPlayerDamage(hit.gameObject, explosionDamage, 4f);
                }
            }

            Destroy(spire);
        }

        private List<Vector3> CalculateSpireTargetPositions(Transform playerTransform)
        {
            List<Vector3> positions = new List<Vector3>();
            Vector3 playerPos = playerTransform.position;
            float facing = Mathf.Sign(transform.localScale.x);

            // Cắm các cọc tạo thành một chuỗi bẫy dọc theo đường chạy của người chơi
            positions.Add(new Vector3(playerPos.x - 3.5f * facing, playerPos.y, playerPos.z));
            positions.Add(new Vector3(playerPos.x - 1f * facing, playerPos.y, playerPos.z));
            positions.Add(new Vector3(playerPos.x + 1.5f * facing, playerPos.y, playerPos.z));
            positions.Add(new Vector3(playerPos.x + 4f * facing, playerPos.y, playerPos.z));
            return positions;
        }
    }
}

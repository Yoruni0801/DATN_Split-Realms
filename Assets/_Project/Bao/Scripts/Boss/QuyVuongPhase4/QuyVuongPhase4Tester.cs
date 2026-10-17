using UnityEngine;
using Quoc.QuyVuong;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// Component hỗ trợ test ngay lập tức Phase 4 và từng skill riêng biệt trong Unity Scene
    /// Phím tắt trong Play Mode:
    /// - Phím [4]: Chuyển ngay sang Phase 4 (Hỏa Nham Ma) và kích hoạt model
    /// - Phím [1]: Kích hoạt Skill 1 (Molten Skull Dash & Uppercut)
    /// - Phím [2]: Kích hoạt Skill 2 (Obsidian Guillotine & Triệu Hồi Bom Nham Thạch)
    /// - Phím [T]: Bật / Tắt AI Combat Brain tự động
    /// </summary>
    public class QuyVuongPhase4Tester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private QuyVuongBossController bossController;
        [SerializeField] private QuyVuongBossHealth bossHealth;
        [SerializeField] private Phase4TectonicRendingSkill meleeSkill;
        [SerializeField] private Phase4ObsidianGuillotineSkill guillotineSkill;
        [SerializeField] private QuyVuongPhase4CombatBrain combatBrain;

        [Header("Test Settings")]
        [SerializeField] private bool showOnScreenGUI = true;
        [SerializeField] private Transform testPlayerTarget;

        private void Awake()
        {
            FindAllReferences();
        }

        private void Start()
        {
            FindPlayerTarget();
        }

        public void FindAllReferences()
        {
            if (bossController == null)
                bossController = GetComponent<QuyVuongBossController>() ?? GetComponentInParent<QuyVuongBossController>();
            if (bossHealth == null)
                bossHealth = GetComponent<QuyVuongBossHealth>() ?? GetComponentInParent<QuyVuongBossHealth>();
            if (meleeSkill == null)
                meleeSkill = GetComponent<Phase4TectonicRendingSkill>() ?? GetComponentInParent<Phase4TectonicRendingSkill>();
            if (guillotineSkill == null)
                guillotineSkill = GetComponent<Phase4ObsidianGuillotineSkill>() ?? GetComponentInParent<Phase4ObsidianGuillotineSkill>();
            if (combatBrain == null)
                combatBrain = GetComponent<QuyVuongPhase4CombatBrain>() ?? GetComponentInParent<QuyVuongPhase4CombatBrain>();
        }

        private void FindPlayerTarget()
        {
            if (testPlayerTarget == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    testPlayerTarget = player.transform;
                }
                else
                {
                    var ph = FindAnyObjectByType<PlayerHealth>();
                    if (ph != null) testPlayerTarget = ph.transform;
                }
            }
        }

        private void Update()
        {
            if (testPlayerTarget == null)
            {
                FindPlayerTarget();
            }

            // Phím 4: Chuyển ngay sang Phase 4
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                ForceSwitchToPhase4();
            }

            // Phím 1: Test Skill 1 (Skull Dash & Uppercut)
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                TriggerSkill1_Melee();
            }

            // Phím 2: Test Skill 2 (Obsidian Guillotine & Bom Nham Thạch)
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                TriggerSkill2_Guillotine();
            }

            // Phím T: Bật/Tắt AI Brain
            if (Input.GetKeyDown(KeyCode.T))
            {
                ToggleAIBrain();
            }
        }

        [ContextMenu("Force Switch To Phase 4")]
        public void ForceSwitchToPhase4()
        {
            FindAllReferences();

            if (bossController != null)
            {
                bossController.UpdatePhaseGameObjects(4);
            }
            else
            {
                var phases = GetComponentsInChildren<QuyVuongBossPhase>(true);
                foreach (var p in phases)
                {
                    bool isPhase4 = p.gameObject.name.Contains("Phase4") || p.gameObject.name.Contains("Lua");
                    p.gameObject.SetActive(isPhase4);
                    if (isPhase4) p.PlayIdle();
                }
            }

            if (meleeSkill != null) meleeSkill.ResolveReferences();
            if (guillotineSkill != null) guillotineSkill.ResolveReferences();

            Debug.Log("<color=orange><b>[Phase 4 Tester]</b> ĐÃ CHUYỂN SANG PHASE 4 (HỎA NHAM MA)!</color>");
        }

        [ContextMenu("Trigger Skill 1: Liệt Địa Ma Trảo & Đầu Lâu Húc")]
        public void TriggerSkill1_Melee()
        {
            FindAllReferences();
            FindPlayerTarget();

            if (meleeSkill != null)
            {
                Transform target = testPlayerTarget != null ? testPlayerTarget : transform;
                StopAllCoroutines();
                StartCoroutine(meleeSkill.ExecuteSkill(target));
                Debug.Log("<color=red><b>[Phase 4 Tester]</b> Kích hoạt Skill 1: Liệt Địa Ma Trảo & Đầu Lâu Húc!</color>");
            }
            else
            {
                Debug.LogError("[Phase 4 Tester] Chưa gán Phase4TectonicRendingSkill!");
            }
        }

        [ContextMenu("Trigger Skill 2: Bom Dung Nham & Bổ Nện Đất")]
        public void TriggerSkill2_Guillotine()
        {
            FindAllReferences();
            FindPlayerTarget();

            if (guillotineSkill != null)
            {
                Transform target = testPlayerTarget != null ? testPlayerTarget : transform;
                StopAllCoroutines();
                StartCoroutine(guillotineSkill.ExecuteSkill(target));
                Debug.Log("<color=yellow><b>[Phase 4 Tester]</b> Kích hoạt Skill 2: Bom Dung Nham & Bổ Nện Đất!</color>");
            }
            else
            {
                Debug.LogError("[Phase 4 Tester] Chưa gán Phase4ObsidianGuillotineSkill!");
            }
        }

        public void ToggleAIBrain()
        {
            if (combatBrain != null)
            {
                combatBrain.enabled = !combatBrain.enabled;
                Debug.Log($"<color=cyan><b>[Phase 4 Tester]</b> AI Combat Brain: {(combatBrain.enabled ? "BẬT (Tự động)" : "TẮT (Thủ công)")}</color>");
            }
        }

        private void OnGUI()
        {
            if (!showOnScreenGUI) return;

            GUILayout.BeginArea(new Rect(20, 20, 280, 260), GUI.skin.box);
            GUILayout.Label("<b>🔥 QUỶ VƯƠNG PHASE 4 TESTER 🔥</b>");
            GUILayout.Space(5);

            if (GUILayout.Button("⚡ [Phím 4] Kích hoạt Phase 4"))
            {
                ForceSwitchToPhase4();
            }

            if (GUILayout.Button("⚔️ [Phím 1] Skill 1: Đầu Lâu Húc & Móc Lửa"))
            {
                TriggerSkill1_Melee();
            }

            if (GUILayout.Button("💣 [Phím 2] Skill 2: Bom Nham Thạch & Bổ Nện"))
            {
                TriggerSkill2_Guillotine();
            }

            string brainStatus = combatBrain != null && combatBrain.enabled ? "TẮT AI Tự Động" : "BẬT AI Tự Động";
            if (GUILayout.Button($"🤖 [Phím T] {brainStatus}"))
            {
                ToggleAIBrain();
            }

            GUILayout.Label("<size=10>Target: " + (testPlayerTarget != null ? testPlayerTarget.name : "None") + "</size>");
            GUILayout.EndArea();
        }
    }
}
